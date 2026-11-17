using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace BeastNav.Services;

/// <summary>
/// Read-only discovery log for the master boards (territories 1342/1343). Each time a non-HUD game window
/// opens it logs one "Window opened" line; the first time a window name is seen this session it also logs
/// that window's AtkValues and node tree once. Never clicks or changes anything.
/// </summary>
/// <remarks>
/// Added 2026-11-14 to find the board route-choice, campsite and battle-start windows for route automation:
/// the BMR replay (FirstMastersBoard 2026-11-14 17:44) records board moves (director 7 = token to space N)
/// but not the windows used to choose them.
/// </remarks>
public sealed unsafe class CrucibleWindowLogService
{
    private readonly IClientState clientState;
    private readonly IPluginLog log;
    private readonly HashSet<string> dumped = [];
    private HashSet<string> visibleLastTick = [];
    private DateTime nextScan = DateTime.MinValue;

    public CrucibleWindowLogService(IClientState clientState, IPluginLog log)
    {
        this.clientState = clientState;
        this.log = log;
    }

    public void Tick()
    {
        if (this.clientState.TerritoryType is not (1342 or 1343))
        {
            this.visibleLastTick.Clear();
            return;
        }

        var now = DateTime.UtcNow;
        if (now < this.nextScan) return;
        this.nextScan = now.AddMilliseconds(250);

        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) return;
        var visible = new HashSet<string>();
        try
        {
            var list = &manager->AtkUnitManager.AllLoadedUnitsList;
            for (var i = 0; i < Math.Min((int)list->Count, 256); i++)
            {
                var addon = list->Entries[i].Value;
                if (addon == null || !addon->IsVisible) continue;
                var name = addon->NameString;
                if (string.IsNullOrEmpty(name) || name.StartsWith('_') && !name.Contains("XBM", StringComparison.Ordinal)) continue;
                if (!visible.Add(name) || this.visibleLastTick.Contains(name)) continue;

                this.log.Information("[BeastHelper] Window opened: {Name} ({Count} values)", name, addon->AtkValuesCount);
                if (this.dumped.Add(name))
                {
                    foreach (var line in BeastTamingStateService.DescribeAddon(name, addon, includeNodes: true).Take(1500))
                        this.log.Information("[BeastHelper] windowdump {Line}", line);
                }
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "[BeastHelper] Window log scan failed.");
        }

        this.visibleLastTick = visible;
    }
}
