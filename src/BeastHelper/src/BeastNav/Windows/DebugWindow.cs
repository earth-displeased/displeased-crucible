using System.Numerics;
using BeastNav.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace BeastNav.Windows;

public sealed class DebugWindow : Window
{
    private readonly BeastTamingStateService tamingState;
    private readonly LuaAutomationService luaAutomation;
    private readonly LuaRecorderService luaRecorder;
    private readonly NearbyObjectDumpService nearbyObjectDump;
    private readonly AddonNodeDumpService addonNodeDump;
    private readonly WhistleProbeService whistleProbe;
    private readonly WhistleScanService whistleScan;
    private readonly int[] whistleNumbers = [1, 2, 3];
    private string luaTestInput = string.Empty;
    private string nodeDumpAddonName = string.Empty;
    private int nodeDumpDrillId;

    public DebugWindow(
        BeastTamingStateService tamingState,
        LuaAutomationService luaAutomation,
        LuaRecorderService luaRecorder,
        NearbyObjectDumpService nearbyObjectDump,
        AddonNodeDumpService addonNodeDump,
        WhistleProbeService whistleProbe,
        WhistleScanService whistleScan)
        : base("Displeased BeastHelper Debug###DispleasedBeastHelperDebug")
    {
        this.tamingState = tamingState;
        this.luaAutomation = luaAutomation;
        this.luaRecorder = luaRecorder;
        this.nearbyObjectDump = nearbyObjectDump;
        this.addonNodeDump = addonNodeDump;
        this.whistleProbe = whistleProbe;
        this.whistleScan = whistleScan;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 280),
            MaximumSize = new Vector2(1400, 900),
        };
        this.RespectCloseHotkey = true;

        // A stale saved ImGui position (e.g. from a different monitor layout)
        // can otherwise put this off-screen with no visible sign it opened at
        // all. Force a known on-screen spot every time — this is a debug
        // window, not something worth remembering a moved position for.
        this.Position = new Vector2(120, 120);
        this.PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        if (ImGui.Button("Sync bestiary now"))
        {
            this.tamingState.TrySync();
        }

        ImGui.SameLine();
        if (ImGui.Button("Dump note addons to log"))
        {
            this.tamingState.DumpNoteAddons();
        }

        ImGui.SameLine();
        if (ImGui.Button("Dump nearby objects to log"))
        {
            this.nearbyObjectDump.DumpNearbyObjects();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Log objects within 100 yalms: type, ID, name, position, distance and targetability.");
        }

        ImGui.SetNextItemWidth(150);
        ImGui.InputTextWithHint("##nodeDumpAddon", "Addon name", ref this.nodeDumpAddonName, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(90);
        ImGui.InputInt("Inspect NodeId (0 = root)##nodeDumpDrillId", ref this.nodeDumpDrillId);
        ImGui.SameLine();
        if (ImGui.Button("Dump addon nodes to log"))
        {
            this.addonNodeDump.DumpNodeTree(this.nodeDumpAddonName, (uint)Math.Max(0, this.nodeDumpDrillId));
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Log all addon nodes, labels, click events, coordinates and sizes. Set a NodeId to inspect a child container; zero starts at the root.");
        }

        ImGui.Separator();

        var sync = this.tamingState.LastResult;
        ImGui.TextWrapped($"Last bestiary sync: {sync.Message}");
        ImGui.TextUnformatted($"Strategy: {sync.Strategy}  Source: {sync.Source}");
        ImGui.TextWrapped($"Method: {sync.Method}");
        ImGui.TextUnformatted($"Detected captured: {sync.TamedPetRowIds.Count}  Success: {sync.Success}  Applied: {sync.Synced}");

        if (sync.DataSize != 0 || sync.FileSize != 0)
        {
            ImGui.TextUnformatted($"module: data={sync.DataSize} file={sync.FileSize} header={sync.HeaderLength} type='{sync.FileType}' ver={sync.FileVersion}");
        }

        if (!string.IsNullOrWhiteSpace(sync.PayloadHex))
        {
            ImGui.TextWrapped($"Payload: {sync.PayloadHex}");
        }

        if (sync.Diagnostics.Count > 0 && ImGui.CollapsingHeader($"Diagnostics ({sync.Diagnostics.Count})"))
        {
            foreach (var line in sync.Diagnostics)
            {
                ImGui.TextUnformatted(line);
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Lua test (BeastHelper-internal MoonSharp)");
        ImGui.SetNextItemWidth(-90);
        ImGui.InputText("##luatest", ref this.luaTestInput, 512);
        ImGui.SameLine();
        if (ImGui.Button("Run##luatest"))
        {
            this.luaAutomation.Run(this.luaTestInput);
        }

        ImGui.TextDisabled($"e.g. click(\"AddonName\", 4) / clickListItem(\"AddonName\", 11, 41001, \"ButtonClick\", true) / clickMouseDown(\"AddonName\", 4) / selectItem(\"AddonName\", 11, 0) / callback(\"AddonName\", 1, 2)  —  {this.luaAutomation.Status}");

        ImGui.Separator();
        var recording = this.luaRecorder.IsRecording;
        if (ImGui.Checkbox("Record operations to log", ref recording))
        {
            this.luaRecorder.SetEnabled(recording);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Record game FireCallback operations as Lua callback lines in /xllog. Copy recorded lines into scripts. Plain button clicks such as Start Battle and Claim all are not recorded.");
        }

        ImGui.SameLine();
        ImGui.TextDisabled(this.luaRecorder.Status);
        ImGui.Separator();
        ImGui.TextUnformatted("Whistle probe: Before -> Noise (no changes) -> Reassign in bestiary -> After");
        if (ImGui.Button("Before##whistleProbe"))
        {
            this.whistleProbe.TakeBefore();
        }

        ImGui.SameLine();
        if (ImGui.Button("Noise##whistleProbe"))
        {
            this.whistleProbe.TakeNoise();
        }

        ImGui.SameLine();
        if (ImGui.Button("After: log differences##whistleProbe"))
        {
            this.whistleProbe.TakeAfterAndLog();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(this.whistleProbe.Status);

        ImGui.TextUnformatted("Whistle memory scan: enter beast numbers, scan, reassign whistles, update numbers and refine");
        string[] labels = ["1", "2", "3"];
        for (var i = 0; i < 3; i++)
        {
            if (i > 0)
            {
                ImGui.SameLine();
            }

            ImGui.SetNextItemWidth(80);
            ImGui.InputInt($"{labels[i]}##whistleNo{i}", ref this.whistleNumbers[i]);
            ImGui.SameLine();
            ImGui.TextDisabled(this.whistleScan.NameOf(this.whistleNumbers[i]));
        }

        ImGui.BeginDisabled(this.whistleScan.IsRunning);
        if (ImGui.Button("Scan all memory##whistleScan"))
        {
            this.whistleScan.Scan([.. this.whistleNumbers]);
        }

        ImGui.SameLine();
        if (ImGui.Button("Refine##whistleScan"))
        {
            this.whistleScan.Refine([.. this.whistleNumbers]);
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.TextDisabled(this.whistleScan.Status);
    }
}
