using System.Text;
using Dalamud;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastNav.Services;

/// <summary>
/// Diagnostic for finding where the game keeps the 壱/弐/参 whistle
/// assignments, which ClientStructs doesn't map yet. Takes raw byte
/// snapshots of every candidate struct, then logs the offsets that changed
/// between "before" and "after" re-assigning a whistle in the 魔獣図鑑 —
/// skipping offsets that also changed in a "noise" snapshot taken without
/// touching anything (timers, cursor state, etc.).
/// </summary>
/// <remarks>Read-only: reads memory through SafeMemory, never writes.</remarks>
public sealed unsafe class WhistleProbeService
{
    private const int MaxDiffLines = 300;

    private readonly IGameGui gameGui;
    private readonly IPluginLog log;

    private Dictionary<string, byte[]>? before;
    private Dictionary<string, byte[]>? noise;

    public WhistleProbeService(IGameGui gameGui, IPluginLog log)
    {
        this.gameGui = gameGui;
        this.log = log;
    }

    public string Status { get; private set; } = "Not captured";

    public void TakeBefore()
    {
        this.before = this.Capture();
        this.noise = null;
        this.Status = $"Before captured ({Describe(this.before)})";
    }

    public void TakeNoise()
    {
        if (this.before is null)
        {
            this.Status = "Capture Before first";
            return;
        }

        this.noise = this.Capture();
        this.Status = $"Noise captured ({Describe(this.noise)})";
    }

    public void TakeAfterAndLog()
    {
        if (this.before is null)
        {
            this.Status = "Capture Before first";
            return;
        }

        var after = this.Capture();
        var lines = new List<string>();
        foreach (var (name, oldBytes) in this.before)
        {
            if (!after.TryGetValue(name, out var newBytes))
            {
                continue;
            }

            byte[]? noiseBytes = null;
            this.noise?.TryGetValue(name, out noiseBytes);
            var length = Math.Min(oldBytes.Length, newBytes.Length);
            for (var offset = 0; offset < length; offset++)
            {
                if (oldBytes[offset] == newBytes[offset])
                {
                    continue;
                }

                if (noiseBytes is not null && offset < noiseBytes.Length && noiseBytes[offset] != oldBytes[offset])
                {
                    continue;
                }

                var u32 = offset + 4 <= length ? $" u32 {BitConverter.ToUInt32(oldBytes, offset)}→{BitConverter.ToUInt32(newBytes, offset)}" : string.Empty;
                lines.Add($"{name}+0x{offset:X}: {oldBytes[offset]}→{newBytes[offset]}{u32}");
            }
        }

        this.log.Information("[BeastHelper] Whistle probe: {Count} changed bytes{Noise}.", lines.Count, this.noise is null ? " (no noise filter)" : string.Empty);
        foreach (var line in lines.Take(MaxDiffLines))
        {
            this.log.Information("[BeastHelper] Whistle probe: {Line}", line);
        }

        this.Status = $"Logged {lines.Count} changed bytes to /xllog";
    }

    private Dictionary<string, byte[]> Capture()
    {
        var regions = new Dictionary<string, byte[]>();
        Add(regions, "UIState", (nint)UIState.Instance(), sizeof(UIState));
        Add(regions, "XBMModule", (nint)XBMModule.Instance(), 0x1000);
        Add(regions, "XBMNoteModule", (nint)XBMNoteModule.Instance(), 0x200);

        var noteModule = XBMNoteModule.Instance();
        if (noteModule is not null && noteModule->PetSettings.First is not null)
        {
            var count = (int)noteModule->PetSettings.LongCount;
            Add(regions, "PetSettings", (nint)noteModule->PetSettings.First, Math.Clamp(count, 0, 64) * sizeof(XBMNoteModule.PetSetting));
        }

        var agents = AgentModule.Instance();
        if (agents is not null)
        {
            Add(regions, "AgentNotebook", (nint)agents->GetAgentXBMMonsterNotebook(), 0x200);
            Add(regions, "AgentPetParty", (nint)agents->GetAgentXBMPetParty(), 0x200);
        }

        var notebook = this.gameGui.GetAddonByName("XBMMonsterNotebook");
        if (!notebook.IsNull)
        {
            var addon = (AtkUnitBase*)notebook.Address;
            var values = new byte[addon->AtkValuesCount * 4];
            for (var i = 0; i < addon->AtkValuesCount; i++)
            {
                var value = addon->AtkValues[i];
                var number = value.Type.ToString() switch
                {
                    "Int" => value.Int,
                    "UInt" => (int)value.UInt,
                    "Bool" => value.Byte,
                    _ => 0,
                };
                BitConverter.GetBytes(number).CopyTo(values, i * 4);
            }

            // One AtkValue per 4 bytes, so offset / 4 is the AtkValue index.
            regions["NotebookAtkValues"] = values;
        }

        return regions;
    }

    private static void Add(Dictionary<string, byte[]> regions, string name, nint address, int size)
    {
        if (address == nint.Zero || size <= 0)
        {
            return;
        }

        if (SafeMemory.ReadBytes(address, size, out var bytes))
        {
            regions[name] = bytes;
        }
    }

    private static string Describe(Dictionary<string, byte[]> regions)
    {
        var builder = new StringBuilder();
        foreach (var (name, bytes) in regions)
        {
            builder.Append(builder.Length == 0 ? string.Empty : ", ").Append(name).Append(' ').Append(bytes.Length);
        }

        return builder.ToString();
    }
}
