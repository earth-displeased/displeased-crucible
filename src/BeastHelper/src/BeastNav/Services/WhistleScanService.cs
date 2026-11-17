using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace BeastNav.Services;

/// <summary>
/// Cheat-Engine-style search for the 壱/弐/参 whistle assignments, which no
/// struct ClientStructs maps holds (a before/after diff of UIState, the XBM
/// modules and the notebook agent only showed selection changes). Scans all
/// committed writable memory for the three current whistle beasts stored
/// side by side — as 図鑑 numbers, 0-based 図鑑 numbers, or Pet sheet row ids,
/// in 1/2/4-byte elements at a handful of strides — then narrows the hits
/// after the player re-assigns a whistle and enters the new trio.
/// </summary>
/// <remarks>Read-only: copies memory with ReadProcessMemory, never writes.</remarks>
public sealed unsafe class WhistleScanService
{
    private const int MaxCandidates = 200_000;
    private const int MaxLoggedCandidates = 40;
    private const int ChunkSize = 16 * 1024 * 1024;
    private const int ChunkOverlap = 256;
    private const long MaxRegionSize = 1L << 30;
    private static readonly int[] Strides = [1, 2, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64];

    private readonly BeastDataService beastData;
    private readonly IPluginLog log;
    private List<Candidate> candidates = [];
    private Task? running;

    public WhistleScanService(BeastDataService beastData, IPluginLog log)
    {
        this.beastData = beastData;
        this.log = log;
    }

    private enum Encoding
    {
        NotebookNumber,
        ZeroBasedNumber,
        PetRowId,
    }

    public string Status { get; private set; } = "Not scanned";

    public bool IsRunning => this.running is { IsCompleted: false };

    public string NameOf(int notebookNumber) => this.beastData.FindPetByXbmRowId((uint)notebookNumber)?.Name ?? "?";

    /// <summary>Searches all writable memory for the given whistle trio (図鑑 numbers).</summary>
    public void Scan(int[] whistles)
    {
        if (this.IsRunning)
        {
            return;
        }

        this.Status = "Scanning…";
        this.running = Task.Run(() =>
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var found = this.ScanAll(whistles);
                this.candidates = found;
                this.Status = $"候補 {found.Count} 件（{stopwatch.Elapsed.TotalSeconds:F1}s）";
                this.LogCandidates();
            }
            catch (Exception ex)
            {
                this.Status = $"Scan failed: {ex.Message}";
                this.log.Error(ex, "[BeastHelper] Whistle scan failed.");
            }
        });
    }

    /// <summary>Keeps only the candidates that now hold the new trio.</summary>
    public void Refine(int[] whistles)
    {
        if (this.IsRunning)
        {
            return;
        }

        var kept = new List<Candidate>();
        var buffer = new byte[64 * 3];
        foreach (var candidate in this.candidates)
        {
            var values = this.Encode(candidate.Encoding, whistles);
            if (values is null)
            {
                continue;
            }

            var length = candidate.Stride * 2 + candidate.Size;
            if (ReadMemory(candidate.Address, buffer, length) && Matches(buffer, 0, values, candidate.Size, candidate.Stride))
            {
                kept.Add(candidate);
            }
        }

        this.log.Information("[BeastHelper] Whistle scan: refined {Before} → {After} candidates.", this.candidates.Count, kept.Count);
        this.candidates = kept;
        this.Status = $"Refined to {kept.Count} candidates";
        this.LogCandidates();
    }

    private List<Candidate> ScanAll(int[] whistles)
    {
        var encodings = Enum.GetValues<Encoding>()
            .Select(encoding => (Encoding: encoding, Values: this.Encode(encoding, whistles)))
            .Where(x => x.Values is not null)
            .ToList();

        var found = new List<Candidate>();
        var buffer = new byte[ChunkSize + ChunkOverlap];
        nint address = 0;
        long scanned = 0;
        while (VirtualQuery(address, out var info, (nint)sizeof(MemoryBasicInformation)) != 0)
        {
            var regionSize = (long)info.RegionSize;
            if (regionSize <= 0)
            {
                break;
            }

            if (IsScannable(info) && regionSize <= MaxRegionSize)
            {
                for (long offset = 0; offset < regionSize; offset += ChunkSize)
                {
                    var length = (int)Math.Min(ChunkSize + ChunkOverlap, regionSize - offset);
                    var chunkAddress = (nint)((long)info.BaseAddress + offset);
                    if (!ReadMemory(chunkAddress, buffer, length))
                    {
                        continue;
                    }

                    scanned += length;
                    var limit = (int)Math.Min(ChunkSize, length);
                    foreach (var (encoding, values) in encodings)
                    {
                        SearchChunk(buffer, length, limit, values!, chunkAddress, encoding, found);
                        if (found.Count >= MaxCandidates)
                        {
                            return found;
                        }
                    }
                }
            }

            address = (nint)((long)info.BaseAddress + regionSize);
        }

        this.log.Information("[BeastHelper] Whistle scan: scanned {MB} MB.", scanned / (1024 * 1024));
        return found;
    }

    private static void SearchChunk(byte[] buffer, int length, int limit, int[] values, nint chunkAddress, Encoding encoding, List<Candidate> found)
    {
        var first = (byte)values[0];
        for (var i = 0; i < limit; i++)
        {
            if (buffer[i] != first)
            {
                continue;
            }

            foreach (var size in (ReadOnlySpan<int>)[1, 2, 4])
            {
                if (values.Any(v => size == 1 ? v > byte.MaxValue : size == 2 && v > ushort.MaxValue))
                {
                    continue;
                }

                foreach (var stride in Strides)
                {
                    if (stride < size || i + stride * 2 + size > length)
                    {
                        continue;
                    }

                    if (Matches(buffer, i, values, size, stride))
                    {
                        found.Add(new Candidate((nint)((long)chunkAddress + i), encoding, size, stride));
                    }
                }
            }
        }
    }

    private static bool Matches(byte[] buffer, int start, int[] values, int size, int stride)
    {
        for (var k = 0; k < values.Length; k++)
        {
            var at = start + k * stride;
            long value = size switch
            {
                1 => buffer[at],
                2 => BitConverter.ToUInt16(buffer, at),
                _ => BitConverter.ToUInt32(buffer, at),
            };
            if (value != values[k])
            {
                return false;
            }
        }

        return true;
    }

    private int[]? Encode(Encoding encoding, int[] whistles) => encoding switch
    {
        Encoding.NotebookNumber => whistles,
        Encoding.ZeroBasedNumber => whistles.Select(n => n - 1).ToArray(),
        Encoding.PetRowId => whistles.Select(n => (int)(this.beastData.FindPetByXbmRowId((uint)n)?.PetRowId ?? 0)).ToArray() is var ids && ids.All(id => id > 0) ? ids : null,
        _ => null,
    };

    private void LogCandidates()
    {
        if (this.candidates.Count > MaxLoggedCandidates)
        {
            return;
        }

        var module = Process.GetCurrentProcess().MainModule;
        var moduleBase = module?.BaseAddress ?? 0;
        var moduleEnd = moduleBase + (module?.ModuleMemorySize ?? 0);
        var uiState = (nint)UIState.Instance();
        var uiStateEnd = uiState + sizeof(UIState);

        foreach (var c in this.candidates)
        {
            var where = c.Address >= moduleBase && c.Address < moduleEnd ? $"ffxiv+0x{c.Address - moduleBase:X}"
                : c.Address >= uiState && c.Address < uiStateEnd ? $"UIState+0x{c.Address - uiState:X}"
                : "heap";
            this.log.Information(
                "[BeastHelper] Whistle scan: 0x{Address:X} ({Where}) {Encoding} size={Size} stride={Stride}",
                (long)c.Address,
                where,
                c.Encoding,
                c.Size,
                c.Stride);
        }
    }

    private static bool IsScannable(MemoryBasicInformation info)
    {
        const uint MemCommit = 0x1000;
        const uint PageGuard = 0x100;
        const uint PageNoAccess = 0x01;
        const uint Writable = 0x04 | 0x08 | 0x40 | 0x80;
        return info.State == MemCommit
            && (info.Protect & PageGuard) == 0
            && (info.Protect & PageNoAccess) == 0
            && (info.Protect & Writable) != 0;
    }

    private static bool ReadMemory(nint address, byte[] buffer, int length)
    {
        fixed (byte* p = buffer)
        {
            return ReadProcessMemory(GetCurrentProcess(), address, p, length, out var read) && read == length;
        }
    }

    private readonly record struct Candidate(nint Address, Encoding Encoding, int Size, int Stride);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryBasicInformation
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll")]
    private static extern nint VirtualQuery(nint address, out MemoryBasicInformation buffer, nint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(nint process, nint address, byte* buffer, nint size, out nint read);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();
}
