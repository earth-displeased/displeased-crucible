using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace BeastNav.Services;

/// <summary>
/// Turns BossModReborn's own movement-only autorotation module
/// (<c>BossMod.Autorotation.MiscAI.NormalMovement</c>) on or off via IPC — the
/// same mechanism AutoDuty uses to get BMR's pathfinding/dodge logic without
/// touching combat. This never selects or uses an action; it only asks BMR's
/// own preset system to move the character out of danger, so it inherits
/// whatever a fight's BMR module (if any) already knows about forbidden zones.
/// </summary>
/// <remarks>
/// In-combat movement automation — against the FFXIV ToS, self-responsibility.
/// Off by default. Only ever touches a preset this service itself created
/// (<see cref="PresetName"/>); it never activates or clears some other preset
/// the user may already have set up in BMR for other content.
/// </remarks>
/// <remarks>
/// The preset's Destination strategy is kept at "None" outside of combat and
/// only switched to "Pathfind" while <see cref="ConditionFlag.InCombat"/> is
/// set, via <see cref="Tick"/>. Without this, an enabled preset has BMR's
/// NormalMovement module trying to path the character somewhere the moment
/// it's activated regardless of what else is going on — which was fighting
/// vnavmesh for movement control (and breaking NPC interaction) while just
/// walking around outside a fight with the preset left on from earlier
/// testing.
/// </remarks>
/// <remarks>
/// The preset is only ever active inside the Crucible arena territories
/// (1339–1343). Anywhere else it's cleared — even a "BeastHelper Dodge"
/// preset left active by an older version — because BMR's NormalMovement
/// would otherwise dodge on its own in any fight BMR has a module for
/// (e.g. an Ultimate), moving the character against the player's will.
/// </remarks>
/// <remarks>
/// Sets NormalMovement's Range track to "Any" so it preserves the safe
/// destination even when it is outside attack range, and sets
/// ForbiddenZoneCushion to "Small" (a
/// 0.5y safety margin from danger zones, instead of the default "None"
/// which paths right up to the edge). Both are set once in <see cref="Enable"/>
/// rather than toggled per-tick like Destination, since they don't need to
/// change based on combat state.
/// </remarks>
public sealed class BossModDodgeService
{
    private const string PresetName = "BeastHelper Dodge";
    private const string NormalMovementModule = "BossMod.Autorotation.MiscAI.NormalMovement";

    private readonly ICallGateSubscriber<bool, object> setSecondBoardExperimental;
    private readonly ICallGateSubscriber<bool, object> setThirdBoardExperimental;
    private readonly ICallGateSubscriber<bool> aiEnabled;
    private readonly ICallGateSubscriber<bool> experimentalEnabled;
    private readonly ICallGateSubscriber<bool, object> setExperimental;
    private readonly ICallGateSubscriber<string> activeModuleName;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<bool> hasActiveModule;
    private readonly ICallGateSubscriber<string, bool, bool> presetsCreate;
    private readonly ICallGateSubscriber<string, bool> presetsSetActive;
    private readonly ICallGateSubscriber<bool> presetsClearActive;
    private readonly ICallGateSubscriber<string> presetsGetActive;
    private readonly ICallGateSubscriber<string, string, string, string, bool> presetsAddTransientStrategy;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    // 第一盤〜特二盤 — the same range CrucibleCombatService uses.
    private const uint ArenaTerritoryMin = 1339;
    private const uint ArenaTerritoryMax = 1343;

    private readonly IClientState clientState;
    private DateTime nextOutsideCheck = DateTime.MinValue;

    private bool weActivatedIt;
    private bool? lastAppliedInCombat;
    private bool desiredEnabled;
    private readonly ICallGateSubscriber<bool> stopAi;
    private DateTime nextRetry = DateTime.MinValue;

    public BossModDodgeService(IDalamudPluginInterface pluginInterface, ICondition condition, IClientState clientState, IPluginLog log, IObjectTable objectTable)
    {
        this.setThirdBoardExperimental = pluginInterface.GetIpcSubscriber<bool, object>("BossMod.Crucible.ThirdBoard.SetExperimental");
        this.setSecondBoardExperimental = pluginInterface.GetIpcSubscriber<bool, object>("BossMod.Crucible.SecondBoard.SetExperimental");
        this.stopAi = pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.Stop");
        this.aiEnabled = pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.IsEnabled");
        this.experimentalEnabled = pluginInterface.GetIpcSubscriber<bool>("BossMod.Crucible.SecondMaster.ExperimentalEnabled");
        this.setExperimental = pluginInterface.GetIpcSubscriber<bool, object>("BossMod.Crucible.SecondMaster.SetExperimental");
        this.activeModuleName = pluginInterface.GetIpcSubscriber<string>("BossMod.ActiveModuleName");
        this.objectTable = objectTable;
        this.condition = condition;
        this.clientState = clientState;
        this.log = log;
        this.hasActiveModule = pluginInterface.GetIpcSubscriber<bool>("BossMod.HasActiveModule");
        this.presetsCreate = pluginInterface.GetIpcSubscriber<string, bool, bool>("BossMod.Presets.Create");
        this.presetsSetActive = pluginInterface.GetIpcSubscriber<string, bool>("BossMod.Presets.SetActive");
        this.presetsClearActive = pluginInterface.GetIpcSubscriber<bool>("BossMod.Presets.ClearActive");
        this.presetsGetActive = pluginInterface.GetIpcSubscriber<string>("BossMod.Presets.GetActive");
        this.presetsAddTransientStrategy = pluginInterface.GetIpcSubscriber<string, string, string, string, bool>("BossMod.Presets.AddTransientStrategy");
    }

    /// <summary>Short human status for the main window.</summary>
    public string Status { get; private set; } = "off";

    /// <summary>
    /// Whether BMR currently has a module loaded for the encounter — the
    /// dodge preset does nothing useful without one.
    /// </summary>
    public bool HasActiveModule => this.Safe(() => this.hasActiveModule.InvokeFunc(), false);

    public bool? AiModeEnabled => this.Safe<bool?>(() => this.aiEnabled.InvokeFunc(), null);
    public bool? SecondMasterExperimental => this.Safe<bool?>(() => this.experimentalEnabled.InvokeFunc(), null);
    public string EncounterName => this.Safe(() => this.activeModuleName.InvokeFunc(), null) ?? "none";
    public string MovementWarning => MovementReadinessPolicy.Warning(this.AiModeEnabled == true, this.OwnsActivePreset, this.HasActiveModule);
    public void SetSecondMasterExperimental(bool enabled)
    {
        try { this.setExperimental.InvokeAction(enabled); }
        catch (Exception ex) { this.log.Warning(ex, "[BeastHelper] Board-specific BMR support unavailable."); }
    }
    public string ActivePreset => this.Safe(() => this.presetsGetActive.InvokeFunc(), null) ?? "none / unavailable";
    public bool OwnsActivePreset => this.ActivePreset == PresetName;
    private DateTime nextOwnershipCheck = DateTime.MinValue;

    public void EmergencyStop()
    {
        this.desiredEnabled = false;
        this.Safe(() => this.stopAi.InvokeFunc(), false);
        if (this.ActivePreset == PresetName) this.Safe(() => this.presetsClearActive.InvokeFunc(), false);
        this.weActivatedIt = false;
        this.lastAppliedInCombat = null;
        this.Status = "STOPPED - movement released";
    }

    public void SetEnabled(bool enabled)
    {
        this.desiredEnabled = enabled;

        if (!enabled)
        {
            try
            {
                this.Disable();
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "[BeastHelper] BossMod dodge preset toggle failed.");
                this.Status = "error";
            }

            return;
        }

        // Only remember the wish here; Tick() activates the preset once inside an arena.
        this.Status = "on (active only in Crucible)";
    }

    private bool InArena => this.clientState.TerritoryType is >= ArenaTerritoryMin and <= ArenaTerritoryMax;

    /// <summary>
    /// Attempts to enable, tolerating BMR's IPC not being registered yet —
    /// this happens routinely at game/plugin startup if BeastHelper's own
    /// Tick loop reaches this before BMR has finished registering its IPC,
    /// and isn't a real failure: Tick() retries on its own once desired, so
    /// this only needs to log it quietly instead of as an error (which was
    /// tripping Dalamud's "this plugin is creating errors" notification for
    /// a condition that resolves itself within a second or two).
    /// </summary>
    private void TryEnable()
    {
        try
        {
            this.Enable();
        }
        catch (IpcNotReadyError)
        {
            this.Status = "Waiting for BMR";
            this.log.Information("[BeastHelper] BossMod dodge: BMR's IPC isn't registered yet, will retry.");
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "[BeastHelper] BossMod dodge preset toggle failed.");
            this.Status = "error";
        }
    }

    private void Enable()
    {
        var presetJson = $"{{\"Name\":\"{PresetName}\",\"Modules\":{{\"{NormalMovementModule}\":[]}}}}";

        if (!this.presetsCreate.InvokeFunc(presetJson, true))
        {
            this.Status = "preset creation failed";
            this.log.Warning("[BeastHelper] BossMod: failed to create the dodge preset (is BMR installed and enabled?).");
            return;
        }

        if (!this.presetsSetActive.InvokeFunc(PresetName))
        {
            this.Status = "preset activation failed";
            return;
        }

        if (!this.presetsAddTransientStrategy.InvokeFunc(PresetName, NormalMovementModule, "Destination", "None"))
        {
            this.Status = "strategy set failed";
            return;
        }

        // MaxRange clamps the safe destination to attack range, even if the
        // safe point lies farther away. Any preserves BMR's safety destination.
        if (!this.presetsAddTransientStrategy.InvokeFunc(PresetName, NormalMovementModule, "Range", "Any"))
        {
            this.Status = "BMR rejected safe-range strategy";
            return;
        }

        // Defaults to "None" (no buffer — paths right up to a danger zone's
        // edge). Small adds a 0.5y safety margin without giving up much
        // melee uptime; bump to "Medium" (1.5y) for more margin if needed.
        this.presetsAddTransientStrategy.InvokeFunc(PresetName, NormalMovementModule, "ForbiddenZoneCushion", "Small");

        this.weActivatedIt = true;
        this.lastAppliedInCombat = false;
        this.Status = this.HasActiveModule ? "Movement preset ready (safe destination priority)" : "Movement preset ready; no encounter module reported";
        this.log.Information("[BeastHelper] BossMod dodge preset enabled.");
    }

    private void Disable()
    {
        if (this.weActivatedIt && this.Safe(() => this.presetsGetActive.InvokeFunc(), null) == PresetName)
        {
            this.presetsClearActive.InvokeFunc();
            this.log.Information("[BeastHelper] BossMod dodge preset disabled.");
        }

        this.weActivatedIt = false;
        this.lastAppliedInCombat = null;
        this.Status = "off";
    }

    /// <summary>
    /// Keeps the preset's Destination strategy matched to combat state: only
    /// "Pathfind" (let BMR actually move the character) while in combat,
    /// "None" the rest of the time so an enabled preset never fights other
    /// movement (navmesh walking to an NPC, manual movement, etc.) outside an
    /// actual fight. Call once per frame; it only touches IPC when the
    /// combat state actually changes.
    /// </summary>
    public uint? RestrictedTerritory { get; set; }
    private DateTime nextSecondBoardSetup;
    private DateTime nextThirdBoardSetup;
    public void EnableThirdBoardSupport()
    {
        if (DateTime.UtcNow < this.nextThirdBoardSetup) return;
        this.nextThirdBoardSetup = DateTime.UtcNow.AddSeconds(3);
        try { this.setThirdBoardExperimental.InvokeAction(true); } catch { }
    }

    public void EnableSecondBoardSupport()
    {
        if (DateTime.UtcNow < this.nextSecondBoardSetup) return;
        this.nextSecondBoardSetup = DateTime.UtcNow.AddSeconds(3);
        try { this.setSecondBoardExperimental.InvokeAction(true); } catch { }
    }

    public void Tick()
    {
        if (!this.InArena || (this.RestrictedTerritory is uint territory && this.clientState.TerritoryType != territory))
        {
            this.ClearOutsideArena();
            return;
        }

        if (!this.weActivatedIt)
        {
            if (this.desiredEnabled && DateTime.UtcNow >= this.nextRetry)
            {
                this.nextRetry = DateTime.UtcNow + RetryInterval;
                this.TryEnable();
            }

            return;
        }

        var inCombat = this.condition[ConditionFlag.InCombat];
        var now = DateTime.UtcNow;
        if (this.lastAppliedInCombat == inCombat && now < this.nextOwnershipCheck)
        {
            return;
        }

        this.nextOwnershipCheck = now + RetryInterval;
        if (!this.OwnsActivePreset)
        {
            var current = this.Safe(() => this.presetsGetActive.InvokeFunc(), null);
            if (MovementReadinessPolicy.RestoreOwnPreset(this.weActivatedIt, string.IsNullOrEmpty(current),
                this.AiModeEnabled == false, this.objectTable.LocalPlayer is { IsDead: false }))
            {
                this.Status = "Restoring BeastHelper movement preset after it was cleared";
                this.lastAppliedInCombat = null;
                this.TryEnable();
                return;
            }
            // A non-empty foreign preset is left alone.
            this.lastAppliedInCombat = null;
            this.Status = $"MOVEMENT CONTROL LOST: active preset is {this.ActivePreset}. Use Refresh BMR movement setup.";
            return;
        }

        if (this.AiModeEnabled == true)
        { this.Status = this.MovementWarning; return; }
        if (this.lastAppliedInCombat == inCombat)
        {
            this.Status = inCombat ? this.HasActiveModule ? "Pathfind strategy accepted (BMR decides movement)" : "Waiting for encounter module; dodge coverage unavailable" : "Waiting outside combat";
            return;
        }
        if (!this.presetsAddTransientStrategy.InvokeFunc(PresetName, NormalMovementModule, "Destination", inCombat ? "Pathfind" : "None"))
        {
            this.Status = "BMR rejected movement strategy; use Refresh BMR movement setup";
            return;
        }
        this.lastAppliedInCombat = inCombat;
        this.Status = inCombat ? "Pathfind strategy accepted (BMR decides movement)" : "Waiting outside combat";
    }

    /// <summary>
    /// Outside the arenas the preset must never stay active. Also clears a
    /// "BeastHelper Dodge" preset that an older version left active in BMR,
    /// which this session didn't activate itself. Checked every couple of
    /// seconds rather than every frame to keep IPC traffic low.
    /// </summary>
    private void ClearOutsideArena()
    {
        if (DateTime.UtcNow < this.nextOutsideCheck)
        {
            return;
        }

        this.nextOutsideCheck = DateTime.UtcNow + RetryInterval;
        if (this.Safe(() => this.presetsGetActive.InvokeFunc(), null) == PresetName)
        {
            this.Safe(() => this.presetsClearActive.InvokeFunc(), false);
            this.log.Information("[BeastHelper] BossMod dodge preset cleared outside the Crucible arena.");
        }

        this.weActivatedIt = false;
        this.lastAppliedInCombat = null;
        this.Status = this.desiredEnabled ? "on (active only in Crucible)" : "off";
    }

    private T Safe<T>(Func<T> query, T fallback)
    {
        try
        {
            return query();
        }
        catch
        {
            return fallback;
        }
    }
}
