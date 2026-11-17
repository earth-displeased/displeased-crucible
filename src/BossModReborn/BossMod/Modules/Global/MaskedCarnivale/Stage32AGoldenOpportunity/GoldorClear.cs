namespace BossMod.Global.MaskedCarnivale.Stage32;

sealed class GoldorShapeDistance(Func<WPos, float> distance) : ShapeDistance
{
    public override float Distance(in WPos p) => distance(p);
}

[ConfigDisplay(Name = "Goldor clear mode (experimental)", Parent = typeof(GlobalConfig))]
public sealed class GoldorClearConfig : ConfigNode
{
    [PropertyDisplay("Enable Goldor clear mode (Masked Carnivale 32 only)", tooltip: "RSR must be Off. Requires BMR AI On with no autorotation AI preset. Entry, rewards, retries and dialogue remain manual.")]
    public bool Enabled;
    [PropertyDisplay("Log Goldor decisions and observed effects")]
    public bool LogDecisions = true;
}

// One spell owner: this component queues BLU actions only while RSR is Off.
// Existing encounter components remain the source of hazard/knockback geometry.
sealed class GoldorClear(BossModule module) : BossComponent(module)
{
    private readonly GoldorClearConfig _config = Service.Config.Get<GoldorClearConfig>();
    private static readonly BLU.AID[] Required = [BLU.AID.TheRamsVoice, BLU.AID.Ultravibration, BLU.AID.FlyingSardine,
        BLU.AID.Exuviation, BLU.AID.Loom, BLU.AID.Diamondback, BLU.AID.MightyGuard, BLU.AID.PomCure,
        BLU.AID.GoblinPunch, BLU.AID.SonicBoom, BLU.AID.BloodDrain, BLU.AID.FeatherRain,
        BLU.AID.MortalFlame, BLU.AID.BreathOfMagic];
    private string _status = "Goldor clear mode disabled";
    private string _lastLog = "";
    private DateTime _lastLogAt;
    private DateTime _rushUntil;
    private ulong _fireball;
    private readonly HashSet<ulong> _mortalFlameLanded = [];
    private bool _fireGap;
    private bool _fireBurnResolved;
    private WPos? _fireGapPosition;
    private bool _rushExpected;
    private readonly GoldorQuakeProgress _quake = new();
    private readonly GoldorCyclopsProgress _cyclops = new();
    private float _lastHP;
    private ulong _lastSlime;
    private bool _lastFrozen;
    private bool _lastShield;
    private DateTime _lastRequest;
    private ulong _ultraTarget;
    private DateTime _ultraRequestAt;

    internal static bool ActiveFor(Actor actor)
        => Service.Config.Get<GoldorClearConfig>().Enabled && actor.Class == Class.BLU && actor.InCombat && !actor.IsDead
        && AI.AIManager.Instance?.Beh != null && AI.AIManager.Instance.AiPreset == null
        && AI.AIManager.Instance.Autorot.Bossmods.ActiveModule is Act1.Stage32Act1 or Act2.Stage32Act2
        && RotationSolverRebornModule.Instance?.IsAutorotationActive != true
        && Service.Config.Get<BossModuleConfig>().AllowAutomaticActions
        && !Service.Config.Get<AI.AIConfig>().ForbidActions && !Service.Config.Get<AI.AIConfig>().ForbidMovement
        && NavigationConfigured()
        && Required.All(a => AI.AIManager.Instance.WorldState.Client.BlueMageSpells.Contains((uint)a))
        && actor.FindStatus(BLU.SID.AethericMimicryHealer) != null;

    private static bool NavigationConfigured()
    {
        var c = Service.Config.Get<AI.AIConfig>();
        return c.FollowDuringCombat && c.FollowDuringActiveBossModule && c.FollowTarget;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_config.Enabled) hints.Add($"Goldor clear: {_status}");
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 34468) _rushUntil = WorldState.FutureTime(8);
        _quake.Finished(spell.Action.ID);
        
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID == 34445)
        { _fireGap = false; _fireball = 0; _fireBurnResolved = false; _fireGapPosition = null; }
        if (caster == Module.PrimaryActor && caster.OID == 0x3FA7 && spell.Action.ID == 34589) _rushExpected = true;
        if (caster == Module.PrimaryActor && spell.Action.ID == 34468) _rushExpected = false;
        _quake.Started(spell.Action.ID);
        _cyclops.Started(caster.InstanceID, spell.Action.ID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        _cyclops.Resolved(caster.InstanceID, spell.Action.ID);
        if (caster.OID == 0x3FA6 && spell.Action.ID == 34446) _fireBurnResolved = true;
        if (caster == Raid.Player() && spell.Action.ID == (uint)BLU.AID.MortalFlame)
            foreach (var target in spell.Targets)
                for (var i = 0; i < 8; ++i)
                {
                    var effect = target.Effects[i];
                    if (GoldorPolicy.ConfirmsStatus(effect, (uint)BLU.SID.MortalFlame))
                        _mortalFlameLanded.Add(target.ID);
                }
    }

    public override void OnActorDeath(Actor actor)
    {
        if (GoldorPolicy.ConfirmFireGap(_fireball, actor.InstanceID, actor.HPMP.CurHP, actor.IsDead, _fireBurnResolved))
        {
            _fireGap = true;
            _fireGapPosition = actor.Position;
            Status($"Fireball {_fireball:X} kill confirmed at {actor.Position}; preparing landing gap");
        }
        if (actor == Raid.Player())
            Service.Logger.Information($"[Goldor clear] PLAYER DEATH at {actor.Position}; last decision: {_status}; boss cast {Module.PrimaryActor.CastInfo?.Action.ID}; gap confirmed {_fireGap}");
    }

    private void Status(string message)
    {
        _status = message;
        if (_config.LogDecisions && (message != _lastLog && (WorldState.CurrentTime - _lastLogAt).TotalSeconds > 0.75))
        {
            Service.Logger.Information($"[Goldor clear] {message}");
            _lastLog = message;
            _lastLogAt = WorldState.CurrentTime;
        }
    }

    private Actor? Enemy(uint oid) => Module.Enemies(oid).FirstOrDefault(a => a.IsTargetable && !a.IsDead && !a.PendingDead);
    private float Distance(Actor a, Actor b) => (a.Position - b.Position).Length();
    private float Ready(BLU.AID action) => ActionDefinitions.Instance[ActionID.MakeSpell(action)]?.ReadyIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions) ?? float.MaxValue;
    private float Recast(BLU.AID action)
    {
        var d = ActionDefinitions.Instance[ActionID.MakeSpell(action)];
        if (d == null) return float.MaxValue;
        return Math.Max(d.MainCooldownGroup != ActionDefinitions.GCDGroup ? d.MainReadyIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions) : 0,
            d.ExtraCooldownGroup != ActionDefinitions.GCDGroup ? d.ExtraReadyIn(WorldState.Client.Cooldowns) : 0);
    }
    private float GCD => WorldState.Client.Cooldowns[ActionDefinitions.GCDGroup].Remaining;

    // Includes future hazards that would land before a cast finishes; never forces
    // a normal heal/freeze through a predicted hit. Geometry is checked again per update.
    private bool Safe(WPos p, AIHints hints, float horizon)
    {
        if (!Arena.InBounds(p)) return false;
        foreach (var z in hints.ForbiddenZones)
            if (z.activation <= WorldState.FutureTime(horizon) && z.shapeDistance.Distance(p) < 0.5f) return false;
        return true;
    }

    private unsafe bool Queue(BLU.AID action, Actor player, Actor target, AIHints hints, float priority = 4800, bool hold = false, WPos? destination = null, Angle? facing = null)
    {
        if (!WorldState.Client.BlueMageSpells.Contains((uint)action)) { Status($"Blocked: {action} is not equipped"); return false; }
        var def = ActionDefinitions.Instance[ActionID.MakeSpell(action)];
        if (def == null || !def.IsUnlocked(WorldState, player)) { Status($"Blocked: {action} unavailable"); return false; }
        if (Ready(action) > 0.25f) { Status($"Waiting for {action} cooldown ({Ready(action):f1}s)"); return false; }
        var cast = player.FindStatus(BLU.SID.Swiftcast) == null ? def.CastTime : 0;
        if (!hold && cast > 0 && !Safe(player.Position, hints, cast + 0.4f)) { Status($"Moving to a cast window for {action}"); return false; }
        if (destination is WPos dest && DistanceTo(player.Position, dest) > def.Range) { Status($"Blocked: {action} destination out of range"); return false; }
        var previousMovement = hints.ForcedMovement;
        if (cast > 0 || hold)
        {
            hints.ForcedMovement = Vector3.Zero;
            hints.MaxCastTime = Math.Max(hints.MaxCastTime, cast);
        }
        if (destination == null && ActionManagerEx.Instance is { } manager)
        {
            var result = manager.GetActionStatus(ActionID.MakeSpell(action), target.InstanceID, checkRecastActive: false, checkCastingActive: false);
            if (result != 0) { Status($"Game currently rejects {action}: status {result}; HP {player.HPRatio:P0}, MP {player.HPMP.CurMP}"); return false; }
        }
        if (destination is WPos endpoint && !ActionPredicate.AllowDashToPosition(WorldState, player,
            new(ActionID.MakeSpell(action), target, priority, 0.3f, 0, cast, new Vector3(endpoint.X, player.PosRot.Y, endpoint.Z), null, false, false), hints))
        { hints.ForcedMovement = previousMovement; Status("Loom blocked by BMR action-safety checks or a pending knockback; movement remains available"); return false; }
        hints.ActionsToExecute.Push(ActionID.MakeSpell(action), target, priority, expire: 0.3f, castTime: cast,
            targetPos: destination is WPos pos ? new Vector3(pos.X, player.PosRot.Y, pos.Z)
                : action == BLU.AID.FeatherRain ? new Vector3(target.Position.X, target.PosRot.Y, target.Position.Z) : default,
            facingAngle: facing);
        Status($"Request {action} -> {target.Name}; HP {player.HPRatio:P0}, MP {player.HPMP.CurMP}");
        _lastRequest = WorldState.CurrentTime;
        return true;
    }

    private static float DistanceTo(WPos a, WPos b) => (a - b).Length();
    private void Follow(Actor enemy, AIHints hints, float range)
    {
        hints.ForcedTarget = enemy;
        hints.GoalZones.Add(AIHints.GoalSingleTarget(enemy.Position, range + enemy.HitboxRadius, 5f));
        var target = hints.FindEnemy(enemy);
        if (target != null) target.Priority = 10;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_config.Enabled) return;
        if (actor.Class != Class.BLU || actor.IsDead || !actor.InCombat) { Status("Waiting for a manually started BLU battle"); return; }
        if (RotationSolverRebornModule.Instance?.IsAutorotationActive == true) { Status("Blocked: switch RSR Off (one spell controller required)"); return; }
        if (AI.AIManager.Instance?.Beh == null) { Status("Paused: turn BMR AI On"); return; }
        if (AI.AIManager.Instance.AiPreset != null) { Status("Blocked: clear BMR autorotation AI preset"); return; }
        if (Service.Config.Get<AI.AIConfig>().ForbidActions || Service.Config.Get<AI.AIConfig>().ForbidMovement)
        { Status("Paused: clear Forbid actions / Forbid movement in BMR AI"); return; }
        if (!NavigationConfigured()) { Status("Blocked: enable Follow during combat, Follow during active boss module, and Follow target"); return; }
        if (!Service.Config.Get<BossModuleConfig>().AllowAutomaticActions) { Status("Blocked: allow automatic actions in BMR boss-module settings"); return; }
        var missing = Required.Where(a => !WorldState.Client.BlueMageSpells.Contains((uint)a)).ToArray();
        if (missing.Length != 0) { Status($"Blocked: missing spells {string.Join(", ", missing)}"); return; }
        if (actor.FindStatus(BLU.SID.AethericMimicryHealer) == null) { Status("Blocked: obtain Healer Aetheric Mimicry before entry"); return; }
        if (actor.FindStatus(1727u) != null) { Status("Blocked by Waning Nocturne; remove Moon Flute for this clear mode"); return; }

        var boss = Module.PrimaryActor;
        var slime = Enemy(0x3FAB);
        var fires = Module.Enemies(0x3FA6).Where(a => a.IsTargetable && !a.IsDead && !a.PendingDead).ToArray();
        var selectedOrb = WorldState.Actors.Find(_fireball);
        if (selectedOrb != null && GoldorPolicy.ConfirmFireGap(_fireball, selectedOrb.InstanceID, selectedOrb.HPMP.CurHP, selectedOrb.IsDead, _fireBurnResolved))
        { _fireGap = true; _fireGapPosition = selectedOrb.Position; }
        var crystal = Enemy(0x3FAC);
        var beams = Module.Enemies(0x3FA8).Where(a => a.CastInfo?.Action.ID == 34464).ToArray();
        var shield = actor.FindStatus(1722u) != null;
        var cyclops = WorldState.Actors.Find(_cyclops.ActorID);
        if (_lastShield != shield || _lastFrozen != (slime?.FindStatus(BLU.SID.DeepFreeze) != null) || _lastSlime != (slime?.InstanceID ?? 0) || Math.Abs(_lastHP - actor.HPRatio) > 0.1f)
        {
            if (_config.LogDecisions) Service.Logger.Information($"[Goldor clear] Observed HP {actor.HPRatio:P0}; Diamondback {shield}; slime {slime?.InstanceID:X}; freeze {slime?.FindStatus(BLU.SID.DeepFreeze) != null}");
            _lastShield = shield; _lastFrozen = slime?.FindStatus(BLU.SID.DeepFreeze) != null; _lastSlime = slime?.InstanceID ?? 0; _lastHP = actor.HPRatio;
        }
        var castID = boss.CastInfo?.Action.ID ?? 0;
        var rush = castID == 34468 || WorldState.CurrentTime < _rushUntil;
        var heavyBlast = GoldorPolicy.ReserveLoom(actor.FindStatus(240u) != null, castID);
        if (castID == 34460) AddLandingConstraint(actor, boss, hints);
        var task = GoldorPolicy.Decide(new(true, true, true, true, shield, castID == 34589,
            slime != null, heavyBlast, beams.Length > 1, rush, actor.FindStatus(BLU.SID.MightyGuard) != null,
            actor.HPRatio, actor.FindStatus(3779u) != null, actor.HPMP.CurMP,
            crystal != null && boss.FindStatus(3621u) != null, (fires.Length != 0 || _fireball != 0) && !_fireGap && !_fireBurnResolved,
            _cyclops.ActorID != 0,
            boss.OID == 0x3FA7 ? 0.95f : 0.8f, (_fireGap && !_fireBurnResolved) || castID == 34460 && fires.Length == 0));
        if (shield) { hints.ForcedMovement = Vector3.Zero; Status("Diamondback confirmed; holding until it expires"); return; }
        if (actor.CastInfo != null)
        {
            if (Safe(actor.Position, hints, actor.CastInfo.RemainingTime + 0.3f)) hints.ForcedMovement = Vector3.Zero;
            Status($"Casting action {actor.CastInfo.Action.ID}; waiting for result"); return;
        }

        if (_rushExpected && task is not (GoldorTask.Interrupt or GoldorTask.Slime or GoldorTask.Gravity or GoldorTask.Dolls))
        {
            Follow(boss, hints, 2.5f);
            if (actor.FindStatus(BLU.SID.MightyGuard) == null) Queue(BLU.AID.MightyGuard, actor, actor, hints);
            else if (actor.HPRatio < 0.95f) Queue(BLU.AID.PomCure, actor, actor, hints);
            else if (actor.HPMP.CurMP < 4000) Queue(BLU.AID.BloodDrain, actor, boss, hints);
            else { Status("Rush preparation ready: close range, full health, guard and MP reserved"); Damage(actor, boss, hints); }
            return;
        }

        switch (task)
        {
            case GoldorTask.Interrupt:
                Follow(boss, hints, 20);
                Queue(BLU.AID.FlyingSardine, actor, boss, hints, 4900); return;
            case GoldorTask.Slime:
                HandleSlime(actor, slime!, hints); return;
            case GoldorTask.Gravity:
                HandleLoom(actor, boss, hints); return;
            case GoldorTask.Inhale:
                if (cyclops != null) HandleCyclops(actor, cyclops, hints);
                else Status("Cyclops sequence: waiting for actor; holding action reservation");
                return;
            case GoldorTask.Dolls:
                HandleDolls(actor, beams, hints); return;
            case GoldorTask.Rush:
                HandleRush(actor, boss, hints); return;
            case GoldorTask.Knockback:
                hints.ForcedTarget = boss;
                PositionForKnockback(actor, boss, hints);
                Status($"Knockback: hold the confirmed gap bearing; HP {actor.HPRatio:P0}, remaining {boss.CastInfo?.NPCRemainingTime:f1}s"); return;
            case GoldorTask.Guard:
                Queue(BLU.AID.MightyGuard, actor, actor, hints); return;
            case GoldorTask.Heal:
                Queue(BLU.AID.PomCure, actor, actor, hints); return;
            case GoldorTask.Cleanse:
                Queue(BLU.AID.Exuviation, actor, actor, hints); return;
            case GoldorTask.Mana:
                var manaTarget = boss.FindStatus(3621u) != null && crystal != null ? crystal : boss;
                Follow(manaTarget, hints, 20);
                Queue(BLU.AID.BloodDrain, actor, manaTarget, hints); return;
            case GoldorTask.Crystal:
                Follow(crystal!, hints, 2.5f);
                Damage(actor, crystal!, hints); return;
            case GoldorTask.Fireball:
                // Pending lethal damage removes an orb from 'fires' before HP/death
                // arrives. Never use that temporary absence to select a second orb.
                _fireball = GoldorPolicy.LockFireball(_fireball, fires.MinBy(a => Distance(actor, a))?.InstanceID ?? 0);
                var fire = WorldState.Actors.Find(_fireball);
                if (fire == null || !fire.IsTargetable || fire.IsDead || fire.PendingDead)
                {
                    hints.ForcedTarget = boss;
                    Status($"Fireball {_fireball:X}: waiting for confirmed kill; no replacement target");
                    return;
                }
                // Only kill one: a dead fireball leaves the intended knockback gap.
                Follow(fire, hints, 20);
                Queue(BLU.AID.SonicBoom, actor, fire, hints, 4600);
                if (Ready(BLU.AID.FeatherRain) <= 0.25f)
                    Queue(BLU.AID.FeatherRain, actor, fire, hints, 4000);
                return;
        }
        // Prepare the Rush safety strategy while Blizzard is being interrupted,
        // rather than attempting two long casts during the four-second Rush cast.
        Follow(boss, hints, 2.5f);
        Damage(actor, boss, hints);
        if (_lastRequest != default && (WorldState.CurrentTime - _lastRequest).TotalSeconds > 12)
            Status($"No spell request for 12s: HP {actor.HPRatio:P0}, MP {actor.HPMP.CurMP}, boss cast {castID}; manual intervention may be needed");
    }

    private void Damage(Actor actor, Actor target, AIHints hints)
    {
        if (target == Module.PrimaryActor)
        {
            var immune = target.FindStatus(3621u) != null;
            var flame = target.FindStatus((uint)BLU.SID.MortalFlame, actor.InstanceID, WorldState.FutureTime(5));
            if (GoldorPolicy.ApplyDot(immune, flame != null || _mortalFlameLanded.Contains(target.InstanceID), 0, true)
                && Queue(BLU.AID.MortalFlame, actor, target, hints, 4500)) return;
            var breath = target.FindStatus((uint)BLU.SID.BreathOfMagic, actor.InstanceID, WorldState.FutureTime(60));
            if (Distance(actor, target) < 9.5f + target.HitboxRadius
                && GoldorPolicy.ApplyDot(immune, breath != null, (float)(breath?.ExpireAt - WorldState.CurrentTime ?? TimeSpan.Zero).TotalSeconds, false)
                && Queue(BLU.AID.BreathOfMagic, actor, actor, hints, 4500, facing: actor.AngleTo(target))) return;
        }
        if (Distance(actor, target) <= target.HitboxRadius + actor.HitboxRadius + 3)
            Queue(BLU.AID.GoblinPunch, actor, target, hints, 4500);
        else Status($"Approaching {target.Name} for Goblin Punch");
        // Optional physical ability; no Moon Flute, gapclosers, Sprint or broad
        // AoE damage is used, preserving control of mechanic targets and movement.
        if (WorldState.Client.BlueMageSpells.Contains((uint)BLU.AID.BothEnds) && Distance(actor, target) < 10)
            Queue(BLU.AID.BothEnds, actor, actor, hints, 3000);
    }

    private void HandleSlime(Actor actor, Actor slime, AIHints hints)
    {
        Follow(slime, hints, 4);
        var frozen = slime.FindStatus(BLU.SID.DeepFreeze) != null;
        var ultra = Recast(BLU.AID.Ultravibration);
        if (ultra > 0.25f)
        {
            if (_ultraTarget == slime.InstanceID && (WorldState.CurrentTime - _ultraRequestAt).TotalSeconds < 4) Status("Ultravibration sent; awaiting confirmed slime death");
            else Status($"SLIME BLOCKED: Ultravibration unavailable for {ultra:f1}s; handle manually");
            return;
        }
        if (!GoldorPolicy.SlimeInRange(Distance(actor, slime), slime.HitboxRadius)) { Status("Approaching Glittering Slime within freeze/Ultravibration range"); return; }
        var action = frozen ? BLU.AID.Ultravibration : BLU.AID.TheRamsVoice;
        var remaining = slime.CastInfo?.NPCRemainingTime ?? float.MaxValue;
        if (!GoldorPolicy.CanFinish(2, GCD, remaining)) { Status("SLIME DEADLINE: insufficient time for the next cast; manual intervention required"); return; }
        if (Queue(action, actor, actor, hints) && action == BLU.AID.Ultravibration)
        { _ultraTarget = slime.InstanceID; _ultraRequestAt = WorldState.CurrentTime; }
    }

    private void AddLandingConstraint(Actor actor, Actor boss, AIHints hints)
    {
        var zones = hints.ForbiddenZones.ToArray();
        var fire = Module.Enemies(0x3FA6).Where(a => !a.IsDead && !a.PendingDead).Select(a => a.Position).ToArray();
        var crosses = Module.Enemies(0x3FA9).Where(a => a.CastInfo?.Action.ID == 34465).Select(a => (a.CastInfo!.LocXZ, a.CastInfo.Rotation)).ToArray();
        var cross = new AOEShapeCross(100, 3.5f);
        var origin = boss.Position;
        var finish = WorldState.FutureTime(boss.CastInfo!.NPCRemainingTime);
        hints.AddForbiddenZone(new GoldorShapeDistance(p =>
        {
            var delta = p - origin;
            if (delta.LengthSq() < 0.01f) return -1;
            var landing = p + delta.Normalized() * 10;
            if (!Arena.InBounds(landing)) return -1;
            if (fire.Any(f => DistanceTo(landing, f) < 10.5f)) return -1;
            if (crosses.Any(g => cross.Check(landing, g.LocXZ, g.Rotation))) return -1;
            foreach (var z in zones)
                if (z.activation <= finish.AddSeconds(1) && z.shapeDistance.Distance(landing) < 0.5f) return -1;
            return 1;
        }), finish);
        // Ensure normal spells don't consume the final positioning window.
        if (boss.CastInfo.NPCRemainingTime < 2.5f) hints.MaxCastTime = 0;
        if (_fireGap) PositionForKnockback(actor, boss, hints);
    }

    private void PositionForKnockback(Actor actor, Actor boss, AIHints hints)
    {
        if (_fireGapPosition is not WPos gap) return;
        var direction = gap - boss.Position;
        if (direction.LengthSq() > 0.01f)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(boss.Position + direction.Normalized() * 5, 0.4f, 50));
    }

    private void HandleCyclops(Actor actor, Actor cyclops, AIHints hints)
    {
        // Inhale's cast can end well before the pull movement and Swing start.
        // Keep ownership through that gap and until Swing's action effect resolves.
        if (cyclops.CastInfo is not ActorCastInfo cast || cast.Action.ID != 34467)
        { Status("Cyclops pull: reserve GCD through pull and Swing startup; no filler attacks"); return; }
        var horizon = cast.NPCRemainingTime + 1;
        if (DistanceTo(actor.Position, cyclops.Position) >= 13 && Safe(actor.Position, hints, horizon))
        {
            hints.ForcedMovement = Vector3.Zero;
            Status("Cyclops: safe from Swing and overlapping attacks; hold until resolution");
            return;
        }
        var best = FindCyclopsEndpoint(actor.Position, cyclops.Position, p => Safe(p, hints, horizon));
        if (best is not WPos destination)
        { Status("CYCLOPS BLOCKED: no landing safe through the combined wave"); return; }
        hints.GoalZones.Add(AIHints.GoalSingleTarget(destination, 0.1f, 50));
        if (GoldorPolicy.CanFinish(1, GCD, cast.NPCRemainingTime))
            Queue(BLU.AID.Loom, actor, actor, hints, 4850, destination: destination);
        else Status("Cyclops: moving toward combined-wave safe point; Loom deadline unavailable");
    }

    internal static WPos? FindCyclopsEndpoint(WPos player, WPos cyclops, Func<WPos, bool> safe)
    {
        WPos? best = null;
        var score = float.MaxValue;
        // Combined cross geometry can require >14.5y from the pull center.
        // Loom has a 15y range; keep a small margin inside that actual limit.
        for (float x = -14.5f; x <= 14.5f; x += 0.5f)
            for (float z = -14.5f; z <= 14.5f; z += 0.5f)
            {
                var p = player + new WDir(x, z);
                var distance = DistanceTo(player, p);
                if (distance > 14.9f || DistanceTo(cyclops, p) < 13 || !safe(p)) continue;
                if (distance < score) { best = p; score = distance; }
            }
        return best;
    }

    private void HandleLoom(Actor actor, Actor boss, AIHints hints)
    {
        var blast = new AOEShapeRect(60, 5);
        if (boss.CastInfo is not ActorCastInfo cast || cast.Action.ID != 34450)
        {
            if (Safe(actor.Position, hints, 0.4f)) hints.ForcedMovement = Vector3.Zero;
            Status("Gravity/Heavy: reserve the GCD for both Blasts; no filler attacks"); return;
        }
        if (!blast.Check(actor.Position, boss.Position, cast.Rotation))
        {
            if (Safe(actor.Position, hints, 0.4f)) hints.ForcedMovement = Vector3.Zero;
            Status("Heavy: outside this Blast; hold position and reserve next Loom"); return;
        }
        var best = FindLoomEndpoint(actor.Position, boss.Position, cast.Rotation, p => Safe(p, hints, 1.5f));
        if (best == null) { Status("LOOM BLOCKED: no safe endpoint in range"); return; }
        if (!GoldorPolicy.CanFinish(1, GCD, cast.NPCRemainingTime))
        { Status($"LOOM DEADLINE: GCD {GCD:f2}s + cast 1s; Blast remaining {cast.NPCRemainingTime:f2}s"); return; }
        if (best is WPos destination)
            Queue(BLU.AID.Loom, actor, actor, hints, 4850, destination: destination);
    }

    internal static WPos? FindLoomEndpoint(WPos player, WPos boss, Angle rotation, Func<WPos, bool> safe)
    {
        var blast = new AOEShapeRect(60, 5);
        WPos? best = null;
        var bestScore = float.MaxValue;
        for (float x = -14; x <= 14; x += 0.75f)
            for (float z = -14; z <= 14; z += 0.75f)
            {
                var p = player + new WDir(x, z);
                var bossDistance = DistanceTo(boss, p);
                if (DistanceTo(player, p) > 14.5f || bossDistance < 3 || blast.Check(p, boss, rotation) || !safe(p)) continue;
                var behind = (p - boss).Dot(rotation.ToDirection()) < -2.5f;
                var score = DistanceTo(player, p) * 0.2f + Math.Abs(bossDistance - 4) + (behind ? 0 : 10);
                if (score < bestScore) { best = p; bestScore = score; }
            }
        return best;
    }

    internal static int ConeCount(Actor actor, IEnumerable<Actor> dolls)
        => ConeCountAt(actor.Position, dolls);

    internal static int ConeCountAt(WPos pos, IEnumerable<Actor> dolls)
    {
        var cone = new AOEShapeCone(40, 60f.Degrees());
        return dolls.Count(d => d.CastInfo is ActorCastInfo c && cone.Check(pos, c.LocXZ, c.Rotation));
    }

    private void HandleDolls(Actor actor, Actor[] dolls, AIHints hints)
    {
        var count = ConeCount(actor, dolls);
        var remain = dolls.Min(a => a.CastInfo!.NPCRemainingTime);
        if (!_quake.InnerResolved) { Status("Dolls: wait outside the first Quake, then step inward"); return; }
        hints.GoalZones.Add(AIHints.GoalSingleTarget(Arena.Center, 9.25f, 20));
        if (DistanceTo(actor.Position, Arena.Center) > 9.75f) { Status("Dolls: step inside the inner Quake circle before rooting"); return; }
        if (!GoldorPolicy.CanTankDolls(count, true)) { Status("Dolls: moving out of the lethal cone overlap before Diamondback"); return; }
        if (remain > 3.1f) { Status("Dolls: positioning through Quake before the shield window"); return; }
        if (!GoldorPolicy.CanFinish(2, GCD, remain)) { Status("DOLLS DEADLINE: Diamondback cannot finish before cones"); return; }
        if (actor.HPMP.CurMP < 3000) { Status("DOLLS BLOCKED: need 3000 MP for Diamondback"); return; }
        if (actor.FindStatus(BLU.SID.MightyGuard) == null || actor.HPRatio < 0.8f)
        { Status("DOLLS NOT READY: prepare Mighty Guard and health before this mechanic"); return; }
        if (!Safe(actor.Position, hints, 2.3f)) { Status("Dolls: position is not safe for the shield cast"); return; }
        Queue(BLU.AID.Diamondback, actor, actor, hints, 4850, hold: true);
    }

    private void HandleRush(Actor actor, Actor boss, AIHints hints)
    {
        Follow(boss, hints, 2.5f);
        if (boss.CastInfo?.Action.ID != 34468) { Status("Rush aftermath: dodge final Blast and ground effects"); return; }
        if (actor.FindStatus(BLU.SID.MightyGuard) == null || actor.HPRatio < 0.95f || actor.HPMP.CurMP < 3000)
        { Status("RUSH NOT READY: requires Mighty Guard, near-full HP and 3000 MP; handle manually"); return; }
        if (Distance(actor, boss) > boss.HitboxRadius + 4) { Status("Rush: approaching Goldor for turtle strategy"); return; }
        if (!GoldorPolicy.CanFinish(2, GCD, boss.CastInfo.NPCRemainingTime)) { Status("RUSH DEADLINE: Diamondback cannot finish before knockback"); return; }
        Queue(BLU.AID.Diamondback, actor, actor, hints, 4850, hold: true);
    }
}
