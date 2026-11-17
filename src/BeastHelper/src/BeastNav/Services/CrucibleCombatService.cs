using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace BeastNav.Services;

/// <summary>
/// Walks the character to a nearby hostile enemy (like AutoDuty's own
/// MovementHelper), then hands combat off to RotationSolverReborn (RSR) via
/// its own chat commands once close.
/// </summary>
/// <remarks>
/// Approach movement was tried once before and removed for being unstable
/// ("動きが安定しない") — that version re-picked "the nearest enemy" fresh
/// every tick and re-issued NavmeshService.MoveCloseTo with whatever
/// position came back, which resets NavmeshService's whole request (forces
/// a re-snap-to-mesh, resets its retry count) on every call. If the nearest
/// candidate flickered between two enemies, or just jittered slightly frame
/// to frame, the destination kept changing and the path kept restarting.
/// This version sticks to one tracked enemy by GameObjectId once picked
/// (<see cref="trackedEnemyId"/>) and keeps re-issuing MoveCloseTo toward
/// that same enemy every tick while approaching — matching the exact
/// pattern CrucibleEntryService already uses safely to walk to the entry
/// NPC (and the same guarded-repeated-call shape AutoDuty's own
/// MovementHelper.Move uses: call every tick, but the destination doesn't
/// jump around). Only forgets the tracked enemy when it actually dies/
/// disappears, or on any of the safety gates below — never because a
/// nearer one showed up.
/// </remarks>
/// <remarks>
/// Movement stops (and RSR takes over positioning, if it has any) as soon
/// as <see cref="ConditionFlag.InCombat"/> is set — this service only gets
/// the character into engage range, not further.
/// </remarks>
/// <remarks>
/// Also used to force-use "ひきつけろ" at low HP via RSR's ActionCommand
/// IPC, but that turned out unreliable in practice (per user testing) and
/// stays removed — this only ever flips RSR's Auto/Off state and drives
/// approach movement; emergency defensives are on the player.
/// </remarks>
/// <remarks>
/// On/off goes through RSR's own chat commands ("/rotation Auto" /
/// "/rotation Off", each registered as its own exact command — confirmed
/// against RotationSolverReborn's own source) via <see cref="ICommandManager"/>,
/// the same technique used for SND macros elsewhere in this plugin. An
/// earlier version called RSR's ChangeOperatingMode IPC directly with a
/// locally mirrored copy of its StateCommandType enum (the same trick
/// AutoDuty-adjacent plugins use to call another plugin's IPC without adding
/// it as a project reference), but that silently never actually changed
/// RSR's own state — Dalamud's IPC dispatch didn't accept a same-shaped but
/// differently-typed enum the way primitives interoperate.
/// </remarks>
/// <remarks>
/// Unattended movement and combat automation — against the FFXIV ToS,
/// self-responsibility. Off by default. Never changes rotation settings,
/// positionals, or actions directly — that's entirely RSR's own job once
/// engaged.
/// </remarks>
public sealed class CrucibleCombatService
{
    // Wide enough to cover a whole board segment, not just melee-adjacent —
    // a board's move→fight→move→fight flow (each stage often separated by
    // its own "イベント中" cutscene) relies on the next enemy being pickable
    // as soon as it's found, however far away it first appears. NavmeshService
    // already walks the full length of a zone elsewhere (destination
    // teleport-and-walk), so distance itself isn't the limiting factor here —
    // this is just a sanity cap against stray object-table entries.
    private const float DetectionRange = 200f;

    // How close to get before letting RSR/the fight itself take over
    // positioning — a tight melee-adjacent buffer, not the wider detection net above.
    private const float EngageRange = 4f;

    // "イベント中" (confirmed against Lumina's Status sheet: LockActions and
    // LockControl are both set) — the status the board's intro applies while
    // it plays, before the player is actually able to move or act.
    private const uint EventBuffStatusId = 1268;

    // Exact chat commands RSR registers for these two states (RotationSolver.Basic/Service.cs).
    private const string RsrAutoCommand = "/rotation Auto";
    private const string RsrOffCommand = "/rotation Off";

    // 第一盤〜特二盤 — the Crucible arena territories (same range as
    // CrucibleRecoveryItemService / CrucibleAutoDutyIpcService). Only act
    // inside these: anywhere else (towns, the overworld, the entry zone) any
    // targetable BattleNpc — a striking dummy (木人), wildlife — was getting
    // picked up as "the nearest enemy", turning RSR on and dragging the
    // player toward it with vnavmesh every frame.
    private const uint ArenaTerritoryMin = 1339;
    private const uint ArenaTerritoryMax = 1343;

    private readonly Configuration configuration;
    private readonly IObjectTable objectTable;
    private readonly ICondition condition;
    private readonly IClientState clientState;
    private readonly ICommandManager commandManager;
    private readonly NavmeshService navmesh;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<bool> autorotationActive;
    private readonly ICallGateSubscriber<bool> vilekinSupported;
    private readonly ICallGateSubscriber<bool> beastkinSupported;
    private readonly ICallGateSubscriber<object> stopRsr;
    private readonly ICallGateSubscriber<bool> setRsrAuto;
    private readonly ICallGateSubscriber<bool> bossModuleActive;
    private readonly RsrRecoveryPolicy recovery = new();
    private bool movementHandedOff;
    private DateTime nextRsrHealthCheck = DateTime.MinValue;

    private bool weEnabledRsr;
    private ulong trackedEnemyId;
    private readonly AutomationActivityLog activity = new();
    private DateTime lastSample = DateTime.MinValue;
    private DateTime lastProgress = DateTime.UtcNow;
    private DateTime nextApproachRequest = DateTime.MinValue;
    private DateTime nextRsrEnableAttempt = DateTime.MinValue;
    private Vector3? lastPosition;
    private Vector3? lastApproachPosition;
    private ulong lastObservedEnemy;
    private uint lastObservedHp;
    private uint lastCast;
    public Func<ulong,bool>? PrepareLootBoost { get; set; }
    public DateTime LastTickUtc { get; private set; } = DateTime.MinValue;
    public string DiagnosticSummary { get; private set; } = "Waiting for first sample";
    public string RsrState { get; private set; } = "unknown";
    public string Warning { get; private set; } = string.Empty;
    public IReadOnlyCollection<string> Activity => this.activity.Entries;
    public void ClearActivity() => this.activity.Clear();

    public bool HasBossMovementModule
    {
        get { try { return this.bossModuleActive.InvokeFunc(); } catch { return false; } }
    }

    public void RecordActivity(string source, string message)
    {
        if (this.configuration.ShowAutomationDiagnostics && this.activity.Record(source, message))
            this.log.Information("[BeastHelper activity] {Source}: {Message}", source, message);
    }

    public CrucibleCombatService(Configuration configuration, IObjectTable objectTable, ICondition condition, IClientState clientState, ICommandManager commandManager, NavmeshService navmesh, IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        this.configuration = configuration;
        this.objectTable = objectTable;
        this.condition = condition;
        this.clientState = clientState;
        this.commandManager = commandManager;
        this.navmesh = navmesh;
        this.log = log;
        this.autorotationActive = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.AutorotationActive");
        this.setRsrAuto = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.FirstBoardSetAuto");
        this.stopRsr = pluginInterface.GetIpcSubscriber<object>("RotationSolverReborn.FirstBoardEmergencyStop");
        this.beastkinSupported = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.ThirdBoardBeastkinSupported");
        this.vilekinSupported = pluginInterface.GetIpcSubscriber<bool>("RotationSolverReborn.FirstBoardVilekinSupported");
        this.bossModuleActive = pluginInterface.GetIpcSubscriber<bool>("BossMod.HasActiveModule");
    }

    /// <summary>Short human status for the main window.</summary>
    public string Status { get; private set; } = "off";

    /// <summary>
    /// RSR's own report of whether its autorotation is active — not just
    /// whether we asked it to be. Lets the UI show when the IPC call
    /// "succeeded" but RSR itself never actually switched on (e.g. it's not
    /// installed, or refuses this content for a reason of its own).
    /// </summary>
    public bool RsrReportsActive
    {
        get
        {
            try
            {
                var active = this.autorotationActive.InvokeFunc();
                this.RsrState = active ? "active" : "inactive";
                return active;
            }
            catch
            {
                this.RsrState = "unavailable (IPC not responding)";
                return false;
            }
        }
    }

    public void Tick()
    {
        this.LastTickUtc = DateTime.UtcNow;
        try { this.TickCore(); }
        finally { this.SampleDiagnostics(); }
    }

    private void SampleDiagnostics()
    {
        if (!this.configuration.ShowAutomationDiagnostics) return;
        var now = DateTime.UtcNow;
        this.RecordActivity("Combat", this.Status);
        if (now - this.lastSample < TimeSpan.FromSeconds(1)) return;
        this.lastSample = now;
        var player = this.objectTable.LocalPlayer;
        if (player is null)
        {
            this.Warning = "Waiting for local player";
            this.DiagnosticSummary = this.Warning;
            return;
        }

        var target = this.FindTrackedEnemy();
        var hp = target is ICharacter character ? character.CurrentHp : 0;
        var cast = player.IsCasting ? player.CastActionId : 0;
        if (this.lastPosition is null || Vector3.DistanceSquared(player.Position, this.lastPosition.Value) > 0.01f
            || this.lastObservedEnemy != this.trackedEnemyId || hp != this.lastObservedHp || cast != this.lastCast)
        {
            this.lastProgress = now;
            this.lastPosition = player.Position;
        }

        this.lastObservedEnemy = this.trackedEnemyId;
        this.lastObservedHp = hp;
        if (cast != this.lastCast)
            this.RecordActivity("Observed cast", cast == 0 ? "Cast ended" : $"Action ID {cast}");
        this.lastCast = cast;
        var active = this.RsrReportsActive;
        this.RecordActivity("RSR", $"requested {(this.weEnabledRsr ? "Auto" : "none")}; reported {this.RsrState}");
        this.RecordActivity("Movement", this.navmesh.DiagnosticStatus);
        var seconds = (now - this.lastProgress).TotalSeconds;
        this.Warning = player.StatusList.Any(s=>s.StatusId==5383) ? "Nightmare: actions and movement blocked. Acorn Bomb needs pet cover before it lands."
            : this.weEnabledRsr && !active ? $"RSR was asked to run but reports {this.RsrState}."
            : target is not null && seconds >= 15 && (this.Status.StartsWith("Fighting:") || this.Status.StartsWith("Approaching:"))
                ? $"No observed movement, cast change or target HP change for {seconds:0}s. Check RSR and positioning."
                : string.Empty;
        this.RecordActivity("Attention", this.Warning.Length == 0 ? "No current diagnostic warning" : this.Warning.StartsWith("No observed") ? "No observed progress for at least 15s" : this.Warning);
        var distance = target is null ? "-" : Vector3.Distance(player.Position, target.Position).ToString("0.0");
        this.RecordActivity("BMR module", this.HasBossMovementModule ? "Encounter module available" : "No encounter module reported; automatic dodge movement is not confirmed");
        this.RecordActivity("Player", player.IsDead ? "Dead: combat and approach paused" : "Alive");
        this.DiagnosticSummary = $"Target: {target?.Name.ToString() ?? "none"} | distance: {distance}y | target HP: {hp}\nPlayer HP: {player.CurrentHp} | dead: {player.IsDead} | in combat: {this.condition[ConditionFlag.InCombat]} | cast ID: {cast} | observed progress: {seconds:0}s ago\nRSR: {this.RsrState} | recovery attempts: {this.recovery.Attempts}/3 | BMR module: {this.HasBossMovementModule}\n{this.navmesh.DiagnosticStatus}";
    }

    public void EmergencyStop()
    {
        this.recovery.Reset();
        this.weEnabledRsr = false;
        this.ForgetTrackedEnemy();
        var stopped = false;
        try { this.stopRsr.InvokeAction(); stopped = true; } catch { }
        if (!stopped) try { stopped = this.commandManager.ProcessCommand("/rotation Off"); } catch { }
        this.RecordActivity("Emergency stop", stopped ? "RSR stop accepted; recovery disabled" : "RSR stop connection unavailable; automatic restart remains blocked");
        this.Status = "STOPPED - explicit Resume required";
    }

    public Func<string>? ScoreSetupCheck { get; set; }
    public string ScoreSetupStatus => this.ScoreSetupCheck?.Invoke() ?? "Setup check unavailable";

    public bool VilekinRsrSupported
    {
        get { try { return this.vilekinSupported.InvokeFunc(); } catch { return false; } }
    }
    public bool BeastkinRsrSupported { get { try { return this.beastkinSupported.InvokeFunc(); } catch { return false; } } }
    public bool ScoreRosterReady { get; set; }
    public bool InCrucible => IsArenaTerritory(this.clientState.TerritoryType);

    private void TickCore()
    {
        if(FirstMasterLegendaryProfile.Active(this.configuration) && this.clientState.TerritoryType!=1342)
        {
            this.DisableRsrIfWeEnabledIt();this.ForgetTrackedEnemy();
            this.Status="First Master: waiting for First Master's Board";return;
        }

        if(SecondMasterLegendaryProfile.Active(this.configuration) && this.clientState.TerritoryType!=SecondMasterLegendaryProfile.Territory)
        {
            this.DisableRsrIfWeEnabledIt();this.ForgetTrackedEnemy();
            this.Status="Second Master: waiting for Second Master's Board";return;
        }

        if(FirstBoardScorePolicy.XpRun(this.configuration) && this.clientState.TerritoryType!=1341)
        {
            this.DisableRsrIfWeEnabledIt();this.ForgetTrackedEnemy();
            this.Status="Third Board XP Run: waiting for Third Board";return;
        }
        if (this.configuration.FirstBoardScoreEnabled && !FirstBoardScorePolicy.AllowsCombat(this.configuration, this.clientState.TerritoryType))
        {
            this.recovery.Reset();
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.Status = "Score mode: waiting for the selected score board";
            return;
        }

        if (this.configuration.FirstBoardScoreEnabled && this.ScoreSetupStatus is { Length: > 0 } setupWarning)
        {
            this.recovery.Reset();
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.Status = setupWarning;
            return;
        }
        if (this.configuration.FirstBoardScoreEnabled && !this.ScoreRosterReady)
        {
            this.recovery.Reset();
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.Status = "Score mode paused: open roster setup to verify the locked three-beast team";
            return;
        }
        if (this.configuration.FirstBoardScoreEnabled && this.configuration.ScoreVilekinCombo && !this.VilekinRsrSupported)
        {
            this.DisableRsrIfWeEnabledIt();
            this.Status = "Vilekin combo paused: enable Rotation Solver Reborn - First Board Assist";
            return;
        }
        if (FirstBoardScorePolicy.BeastkinClear(this.configuration) && !this.BeastkinRsrSupported)
        {
            this.DisableRsrIfWeEnabledIt();
            this.Status="Beastkin clear paused: load the paired Third Board RSR build";
            return;
        }
        if ((!this.configuration.CombatOnlyEnabled && !this.configuration.AutoEnterCrucibleEnabled)
            || !this.configuration.AutoCombatEnabled)
        {
            this.recovery.Reset();
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.Status = "off";
            return;
        }

        if (!IsArenaTerritory(this.clientState.TerritoryType))
        {
            this.recovery.Reset();
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.Status = "Outside supported zone";
            return;
        }

        var player = this.objectTable.LocalPlayer;
        if (player is null)
        {
            this.Status = "Waiting for local player";
            return;
        }

        if (player.IsDead)
        {
            this.DisableRsrIfWeEnabledIt();
            this.ForgetTrackedEnemy();
            this.recovery.Reset();
            this.Status = "Waiting: player is dead";
            return;
        }

        // Leaving content mid-fight (withdraw, or the fight just ending)
        // starts a zone transition where objects are being torn down —
        // touching a stale enemy reference in that window risked crashing
        // the game.
        if (this.condition[ConditionFlag.BetweenAreas]
            || this.condition[ConditionFlag.BetweenAreas51]
            || this.condition[ConditionFlag.OccupiedInCutSceneEvent])
        {
            this.recovery.ShouldRetry(DateTime.UtcNow, false, false);
            // A brief "battle start" camera flourish can set the same
            // cutscene-ish flags without actually leaving the fight — don't
            // disable RSR on that alone, only when we're not (yet, or no
            // longer) actually in combat. Otherwise this fired the instant
            // combat began and turned RSR off right when it was needed.
            if (!this.condition[ConditionFlag.InCombat] && !this.configuration.FirstBoardScoreEnabled)
            {
                this.DisableRsrIfWeEnabledIt();
            }

            this.ForgetTrackedEnemy();
            this.Status = "Waiting (zoning)";
            return;
        }

        // "イベント中" (status 1268, LockActions/LockControl both set) — the
        // board's intro plays with this on; nothing here should act yet.
        if (HasStatus(player, EventBuffStatusId))
        {
            this.recovery.ShouldRetry(DateTime.UtcNow, false, false);
            this.ForgetTrackedEnemy();
            this.Status = "Waiting (event)";
            return;
        }

        var tracked = this.FindTrackedEnemy();
        if (tracked is null && this.trackedEnemyId != 0)
        {
            // Was tracking someone; they're dead or gone now — stop moving
            // toward wherever they used to be.
            this.ForgetTrackedEnemy();
        }

        if (tracked is null)
        {
            tracked = this.FindNearestEnemy(player.Position);
            if (tracked is not null)
            {
                this.trackedEnemyId = tracked.GameObjectId;
                this.log.Information("[BeastHelper] Crucible combat: now tracking {Name}.", tracked.Name);
            }
        }

        if (tracked is null && this.configuration.FirstBoardScoreEnabled && this.weEnabledRsr)
        {
            this.EnableRsrIfNeeded();
            this.Status = "Score mode: Auto maintained between fights";
            return;
        }
        if (tracked is null)
        {
            // Same reasoning as the cutscene guard above: a tracked enemy
            // can briefly stop resolving (e.g. turns untargetable for an
            // instant as the real encounter kicks in) right as combat
            // starts. Losing track of "which" enemy isn't a reason to
            // disable RSR while we're still unambiguously in a fight.
            if (!this.condition[ConditionFlag.InCombat])
            {
                this.DisableRsrIfWeEnabledIt();
            }

            this.Status = "No enemies detected";
            return;
        }

        if(!this.condition[ConditionFlag.InCombat] && this.PrepareLootBoost is {} prepare)
        {
            // Stop the maintained between-fight rotation before consuming the item.
            if(!prepare(tracked.GameObjectId))
            {
                this.DisableRsrIfWeEnabledIt();
                if(this.navmesh.HasPendingRequest) this.navmesh.Stop();
                this.Status="Preparing loot/token boosts before combat";
                return;
            }
        }

        // Enabled as soon as a target is tracked (even before in range) so
        // RSR is ready the moment a pet-summon cast or the fight itself
        // starts — it stays idle on an out-of-range target on its own (see
        // CrucibleCombatService's own remarks on RSR's range-gated targeting).
        this.EnableRsrIfNeeded();

        if (this.condition[ConditionFlag.InCombat])
        {
            if (!this.movementHandedOff)
            {
                if (this.navmesh.HasPendingRequest) this.navmesh.Stop();
                this.movementHandedOff = true;
                this.RecordActivity("Movement ownership", "Battle started: approach finished; combat positioning delegated to BMR / player");
            }

            var healthCheckTime = DateTime.UtcNow;
            if (healthCheckTime >= this.nextRsrHealthCheck)
            {
                this.nextRsrHealthCheck = healthCheckTime.AddSeconds(1);
                var reportedActive = this.RsrReportsActive;
                if (this.recovery.ShouldRetry(healthCheckTime, this.configuration.RecoverStoppedRsrEnabled
                    && this.weEnabledRsr && this.RsrState == "inactive", reportedActive))
                {
                    try
                    {
                        var accepted = this.SendRsrAuto();
                        this.RecordActivity("RSR recovery", $"Attempt {this.recovery.Attempts}/3: /rotation Auto {(accepted ? "accepted" : "not accepted")}");
                    }
                    catch (Exception ex) { this.RecordActivity("RSR recovery", $"Attempt {this.recovery.Attempts}/3 failed: {ex.Message}"); }
                }
            }

            this.Status = $"Fighting: {tracked.Name} (actions delegated to RSR)";
            return;
        }

        this.movementHandedOff = false;
        this.recovery.Reset();
        var now = DateTime.UtcNow;
        if (!this.navmesh.HasPendingRequest && now >= this.nextApproachRequest
            || this.lastApproachPosition is { } previous && Vector3.DistanceSquared(previous, tracked.Position) > 4f
                && now >= this.nextApproachRequest)
        {
            this.navmesh.MoveCloseTo(tracked.Position, false, EngageRange);
            this.lastApproachPosition = tracked.Position;
            this.nextApproachRequest = now.AddSeconds(2);
            this.RecordActivity("Command", $"Queue approach to {tracked.Name}");
        }
        this.Status = $"Approaching: {tracked.Name}";
    }

    /// <summary>Stops re-picking "the tracked enemy" once it dies or leaves the object table; also cancels any in-flight approach toward it.</summary>
    private void ForgetTrackedEnemy()
    {
        if (this.trackedEnemyId == 0)
        {
            return;
        }

        this.trackedEnemyId = 0;
        this.movementHandedOff = false;
        this.lastApproachPosition = null;
        this.nextApproachRequest = DateTime.MinValue;
        this.navmesh.Stop();
    }

    /// <summary>Resolves the currently tracked enemy by id, or null if it's gone/dead/no longer targetable.</summary>
    private IGameObject? FindTrackedEnemy()
    {
        if (this.trackedEnemyId == 0)
        {
            return null;
        }

        foreach (var obj in this.objectTable)
        {
            if (obj.GameObjectId != this.trackedEnemyId)
            {
                continue;
            }

            return IsEnemyCandidate(obj) ? obj : null;
        }

        return null;
    }

    private bool SendRsrAuto()
    {
        if (this.configuration.AutomationStopped) return false;
        if (this.configuration.FirstBoardScoreEnabled || FirstBoardScorePolicy.XpRun(this.configuration))
            try { if (this.setRsrAuto.InvokeFunc()) return true; } catch { }
        return this.commandManager.ProcessCommand(RsrAutoCommand);
    }

    private void EnableRsrIfNeeded()
    {
        if ((this.weEnabledRsr && (!(this.configuration.FirstBoardScoreEnabled || FirstBoardScorePolicy.XpRun(this.configuration))
                || this.condition[ConditionFlag.InCombat] || this.RsrReportsActive))
            || DateTime.UtcNow < this.nextRsrEnableAttempt)
        {
            return;
        }

        try
        {
            this.nextRsrEnableAttempt = DateTime.UtcNow.AddSeconds(3);
            var accepted = this.SendRsrAuto();
            this.weEnabledRsr = accepted;
            this.RecordActivity("Command", accepted ? "Sent /rotation Auto" : "/rotation Auto was not accepted; check RSR is enabled");
            this.log.Information("[BeastHelper] Crucible combat: RotationSolverReborn Auto command accepted: {Accepted}.", accepted);
        }
        catch (Exception ex)
        {
            this.log.Warning(ex, "[BeastHelper] Crucible combat: failed to enable RotationSolverReborn (is it installed?).");
        }
    }

    private void DisableRsrIfWeEnabledIt()
    {
        if (!this.weEnabledRsr)
        {
            return;
        }

        try
        {
            this.commandManager.ProcessCommand(RsrOffCommand);
            this.RecordActivity("Command", "Sent /rotation Off");
            this.log.Information("[BeastHelper] Crucible combat: disabled RotationSolverReborn.");
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "[BeastHelper] Crucible combat: failed to disable RotationSolverReborn.");
        }
        finally
        {
            this.weEnabledRsr = false;
        }
    }

    private static bool IsArenaTerritory(uint territoryId)
        => territoryId is >= ArenaTerritoryMin and <= ArenaTerritoryMax;

    private static bool HasStatus(IPlayerCharacter character, uint statusId)
    {
        foreach (var status in character.StatusList)
        {
            if (status.StatusId == statusId)
            {
                return true;
            }
        }

        return false;
    }

    private IGameObject? FindNearestEnemy(Vector3 playerPosition)
    {
        IGameObject? nearest = null;
        var nearestDistanceSquared = float.MaxValue;

        foreach (var obj in this.objectTable)
        {
            if (!IsEnemyCandidate(obj))
            {
                continue;
            }

            var distanceSquared = Vector3.DistanceSquared(playerPosition, obj.Position);
            if (distanceSquared > DetectionRange * DetectionRange || distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearest = obj;
        }

        return nearest;
    }

    private static bool IsEnemyCandidate(IGameObject obj)
        => obj is IBattleNpc npc && npc.BattleNpcKind == BattleNpcSubKind.Combatant
            && obj.IsTargetable && !obj.IsDead;
}
