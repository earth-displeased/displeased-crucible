namespace BossMod.Shadowbringers.Raid.E01EdenPrime;

[ConfigDisplay(Name = "Resurrection solo BLU - mechanics test", Parent = typeof(ShadowbringersConfig))]
public sealed class E1SoloConfig : ConfigNode
{
    [PropertyDisplay("Enable Resurrection Normal solo BLU test (later mechanics pause)", tooltip: "CFC 653 only. RSR Off, BMR AI On, empty AI preset. Entry/rewards/manual pull remain manual. Not full-fight coverage.")]
    public bool Enabled;
    [PropertyDisplay("Log Resurrection decisions")]
    public bool LogDecisions = true;
}

public enum E1Task { Hold, Shield, Heal, Mana, Damage }
public static class E1Policy
{
    // Goals stay comfortably inside the lethal arena border. No guessed attack widths.
    public static WPos BehindLight(WPos boss, Angle facing) => boss - facing.ToDirection() * 3;
    public static WPos DeltaCorner(WPos player) => new(player.X < 100 ? 85 : 115, player.Z < 100 ? 85 : 115);
    public static AOEShape BeamShape() => new AOEShapeRect(64, 2.5f); // game data: length 60, width 4; include margin
    public static AOEShape GroundShape() => new AOEShapeCircle(6.5f); // game data: radius 6
    public static (int flamed, uint hp) GuardianRank(bool flamed, uint hp) => (flamed ? 1 : 0, uint.MaxValue - hp);
    public static readonly WPos GuardianAnchor = new(100, 114);
    public static WPos GuardianCenter(WPos first, WPos second) => new((first.X + second.X) * 0.5f, (first.Z + second.Z) * 0.5f);
    public static bool ReserveBurst(BLU.AID action, bool longReserve, bool shortReserve, bool addsSeen)
        => !addsSeen && (longReserve && action is BLU.AID.Nightbloom or BLU.AID.SeaShanty or BLU.AID.Surpanakha
            or BLU.AID.ShockStrike or BLU.AID.MountainBuster or BLU.AID.GlassDance
            || shortReserve && action is BLU.AID.FeatherRain or BLU.AID.Eruption);
    public static E1Task FinishGuardian(bool enraging, bool shield, E1Task normal) => enraging ? shield ? E1Task.Hold : E1Task.Damage : normal;
    public static E1Task? GuardianDefense(bool reserve, bool vulnerable, uint maxHP, float hp)
        => !reserve ? null : vulnerable || maxHP < 28000 ? E1Task.Shield : hp < 0.95f ? E1Task.Heal : E1Task.Hold;
    public static bool InAttackRange(float distance, float targetRadius, float range) => distance <= range + targetRadius - 0.5f;
    public static bool NeedDodge(bool shield, bool safe) => !shield && !safe;
    public static BLU.AID? Prepare(bool guard, bool instinct, bool urgentDefense)
        => urgentDefense ? null : !guard ? BLU.AID.MightyGuard : !instinct ? BLU.AID.BasicInstinct : null;
    public static bool AllowModule(bool enabled, uint zone, uint duty, uint oid) => enabled && zone == 849 && duty == 653 && oid == 0x2499;
    public static E1Task Decide(bool shield, bool spear, bool tankVice, bool gravity, bool opening, float hp, uint mp, uint manaFloor = 5000)
    {
        if (shield) return E1Task.Hold;
        if (spear || tankVice) return E1Task.Shield;
        if (hp < (gravity ? 0.98f : 0.65f)) return E1Task.Heal;
        if (opening || gravity) return E1Task.Hold;
        if (mp < manaFloor) return E1Task.Mana;
        return E1Task.Damage;
    }
    public static bool CanShield(float castRemaining, float gcdRemaining) => castRemaining > 2 + gcdRemaining + 0.8f;
    public static bool ConfirmStatus(ulong raw, uint status) => (byte)raw == 14 && raw >> 48 == status;
}

// Mana Slice is instant; the replay shows first impact 11.2s after Dive, then every 15.25s.
// Start reserving early enough for a fresh GCD + Diamondback cast + network margin.
public sealed class E1GuardianProgress
{
    public DateTime NextSliceAt { get; private set; }
    public void Observed(uint action, DateTime now)
    {
        if (action == 15785) NextSliceAt = now.AddSeconds(11.2);
        else if (action == 15782) NextSliceAt = now.AddSeconds(15.25);
    }
    public bool Reserve(DateTime now) => NextSliceAt != default && now >= NextSliceAt.AddSeconds(-5);
}

// Cast snapshots, not live target positions: overlapping orb waves aim independently.
sealed class E1Hazards(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<(AOEInstance aoe, uint action, DateTime expires)> _active = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _active.Where(a => a.expires > WorldState.CurrentTime).Select(a => a.aoe).ToArray();
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        AOEShape? shape = spell.Action.ID switch
        {
            15774 => E1Policy.BeamShape(),
            15776 => E1Policy.GroundShape(),
            15779 => new AOEShapeRect(64, 30.5f), // game data: 60 by 60
            _ => null
        };
        if (shape == null) return;
        var origin = spell.Action.ID == 15776 ? spell.LocXZ : caster.Position;
        var activation = Module.CastFinishAt(spell);
        _active.RemoveAll(a => a.expires <= WorldState.CurrentTime);
        _active.Add((new(shape, origin, spell.Rotation, activation, actorID: caster.InstanceID), spell.Action.ID, activation.AddSeconds(1)));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
        => _active.RemoveAll(a => a.aoe.ActorID == caster.InstanceID && a.action == spell.Action.ID);
}

sealed class E1Opening(BossModule module) : BossComponent(module)
{
    private readonly E1SoloConfig _config = Service.Config.Get<E1SoloConfig>();
    private static readonly BLU.AID[] Required = [BLU.AID.BasicInstinct, BLU.AID.MightyGuard, BLU.AID.PomCure,
        BLU.AID.Diamondback, BLU.AID.BloodDrain, BLU.AID.GoblinPunch, BLU.AID.MortalFlame, BLU.AID.BreathOfMagic];
    private string _status = "Opening test disabled";
    private string _lastLog = "";
    private DateTime _lastLogAt;
    private uint _unimplemented;
    private bool _beamPhase;
    private bool _transition;
    private bool _longAddReserve;
    private bool _shortAddReserve;
    private bool _addsSeen;
    private readonly E1GuardianProgress _guardian = new();
    private DateTime _arrivalUntil;
    private WPos? _mechanicGoal;
    private uint _positionMechanic;
    private DateTime _positionUntil;
    private bool _firstSpearSeen;
    private bool _spear;
    private DateTime _spearUntil;
    private bool _tankVice;
    private bool _drop;
    private DateTime _dropDeadline;
    private readonly List<(WPos pos, DateTime expires)> _puddles = [];
    private readonly HashSet<ulong> _flame = [];

    internal static bool ActiveFor(Actor player)
        => Service.Config.Get<E1SoloConfig>().Enabled && player.Class == Class.BLU && player.InCombat && !player.IsDead
        && AI.AIManager.Instance?.Autorot.Bossmods.ActiveModule is E1Solo
        && AI.AIManager.Instance.Beh != null && AI.AIManager.Instance.AiPreset == null
        && RotationSolverRebornModule.Instance?.IsAutorotationActive != true;

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_config.Enabled)
        {
            hints.Add("Resurrection mechanics test: " + _status);
            var pair = LivingGuardians();
            if (pair.Length != 0) hints.Add("Guardians: " + string.Join(" / ", pair.Select(a => a.HPRatio.ToString("P0"))));
            else if (_longAddReserve && !_addsSeen) hints.Add("Saving the next large burst for the Guardian pair");
            if (!WorldState.Client.BlueMageSpells.Contains((uint)BLU.AID.SonicBoom)) hints.Add("Equip Sonic Boom for ranged offense during safe corner mechanics.");
        }
    }
    private void Status(string text)
    {
        _status = text;
        if (_config.LogDecisions && text != _lastLog && (WorldState.CurrentTime - _lastLogAt).TotalSeconds >= 0.75)
        { Service.Logger.Information("[Resurrection test] " + text); _lastLog = text; _lastLogAt = WorldState.CurrentTime; }
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == 0x249A)
        {
            if (spell.Action.ID == 15785)
            { _arrivalUntil = WorldState.FutureTime(spell.NPCTotalTime + 1); _addsSeen = true; }
            else if (spell.Action.ID is not (15783 or 15784)) _unimplemented = spell.Action.ID;
            return;
        }
        if (caster != Module.PrimaryActor) return;
        switch (spell.Action.ID)
        {
            case 15777: _spear = true; _firstSpearSeen = true; _spearUntil = WorldState.FutureTime(spell.NPCTotalTime + 3); break;
            case 15779:
                _mechanicGoal = E1Policy.BehindLight(caster.Position, spell.Rotation);
                _positionMechanic = 15779;
                _positionUntil = WorldState.FutureTime(spell.NPCTotalTime + 2);
                break;
            case 17642:
                _mechanicGoal = E1Policy.DeltaCorner(Raid.Player()?.Position ?? new WPos(100, 100));
                _positionMechanic = 17642;
                // Helpers resolve half a second after the primary cast in the replay.
                _positionUntil = WorldState.FutureTime(spell.NPCTotalTime + 1.5f);
                break;
            case 15773: _beamPhase = true; break;
            case 15775: break; // visual, damaging ground casts are separate helpers
            case 15780: _transition = true; _beamPhase = false; break;
            case 15772:
                if (_longAddReserve) _shortAddReserve = true;
                _longAddReserve = true;
                _beamPhase = false;
                break;
            case 15764: break;
            case 15767: break;
            case 17637: _drop = true; _dropDeadline = WorldState.FutureTime(10); break;
            case 17636: _tankVice = true; break;
            default:
                if (_unimplemented == 0 && _config.Enabled)
                    Service.ChatGui.Print($"[Resurrection test] Current coverage ends at action {spell.Action.ID}. Automatic actions paused. Turn BMR AI Off for manual play; keep the replay recording.");
                _unimplemented = spell.Action.ID;
                break;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID == 0x249A) _guardian.Observed(spell.Action.ID, WorldState.CurrentTime);
        if (caster == Module.PrimaryActor && spell.Action.ID == 15779 && _positionMechanic == 15779)
            _positionUntil = WorldState.FutureTime(0.5f);
        if (caster == Module.PrimaryActor && spell.Action.ID == 15778)
        { _spear = false; _spearUntil = WorldState.FutureTime(1); }
        if (spell.Action.ID == 17639) _tankVice = false;
        if (spell.Action.ID == 17640 && _drop && Raid.Player() is Actor player)
        {
            _puddles.Add((player.Position, WorldState.FutureTime(60)));
            _drop = false;
        }
        if (caster == Raid.Player() && spell.Action.ID == (uint)BLU.AID.MortalFlame)
            foreach (var target in spell.Targets)
                for (var i = 0; i < 8; ++i)
                    if (E1Policy.ConfirmStatus(target.Effects[i], (uint)BLU.SID.MortalFlame)) _flame.Add(target.ID);
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == Raid.Player()) Service.Logger.Information($"[Resurrection test] PLAYER DEATH; decision: {_status}; boss cast {Module.PrimaryActor.CastInfo?.Action.ID}");
    }
    private bool Safe(WPos point, AIHints hints, float horizon)
    {
        if (!Arena.InBounds(point)) return false;
        foreach (var zone in hints.ForbiddenZones)
            if (zone.activation <= WorldState.FutureTime(horizon) && zone.shapeDistance.Distance(point) < 0.5f) return false;
        return true;
    }
    private unsafe bool Request(BLU.AID action, Actor player, Actor target, AIHints hints, float priority = 4800)
    {
        var id = ActionID.MakeSpell(action);
        var def = ActionDefinitions.Instance[id];
        if (def == null || !def.IsUnlocked(WorldState, player)) { Status($"Unavailable action: {action}"); return false; }
        var cd = def.ReadyIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions);
        if (cd > 0.25f) { Status($"Waiting for {action} cooldown {cd:f1}s"); return false; }
        if (def.CastTime > 0 && !Safe(player.Position, hints, def.CastTime + 0.8f))
        { Status($"Moving to a safe cast window for {action}"); return false; }
        if (action == BLU.AID.Diamondback && player.HPMP.CurMP < 3000)
        { Status("BLOCKED: Diamondback requires 3000 MP"); return false; }
        if (ActionManagerEx.Instance is { } manager
            && manager.GetActionStatus(id, target.InstanceID, checkRecastActive: false, checkCastingActive: false) is var rejection && rejection != 0)
        { Status($"Game currently rejects {action}: {rejection}; trying another available action"); return false; }
        if (def.CastTime > 0)
        { hints.ForcedMovement = Vector3.Zero; hints.MaxCastTime = Math.Max(hints.MaxCastTime, def.CastTime); }
        hints.ActionsToExecute.Push(id, target, priority, expire: 0.3f, castTime: def.CastTime,
            targetPos: action is BLU.AID.FeatherRain or BLU.AID.Eruption ? new Vector3(AttackCenter(target).X, target.PosRot.Y, AttackCenter(target).Z) : default,
            facingAngle: action is BLU.AID.BreathOfMagic or BLU.AID.Surpanakha or BLU.AID.GlassDance or BLU.AID.MountainBuster ? Angle.FromDirection(AttackCenter(hints.ForcedTarget ?? Module.PrimaryActor) - player.Position) : null);
        Status($"Request {action} -> {target.Name}; HP {player.HPRatio:P0}, MP {player.HPMP.CurMP}");
        return true;
    }
    private bool EquippedReady(BLU.AID action)
        => WorldState.Client.BlueMageSpells.Contains((uint)action)
        && ActionDefinitions.Instance[ActionID.MakeSpell(action)] is { } def
        && def.ReadyIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions) <= 0.25f;

    private bool InRange(Actor player, Actor target, float range)
        => E1Policy.InAttackRange((player.Position - target.Position).Length(), target.HitboxRadius, range);

    private Actor[] LivingGuardians() => Module.Enemies(0x249A).Where(a => a.IsTargetable && !a.IsDead && !a.PendingDead).ToArray();
    private WPos AttackCenter(Actor target)
    {
        var adds = LivingGuardians();
        return target.OID == 0x249A && adds.Length == 2 ? E1Policy.GuardianCenter(adds[0].Position, adds[1].Position) : target.Position;
    }
    private bool PairInRange(Actor player, Actor target, float range)
        => target.OID != 0x249A || LivingGuardians().All(a => InRange(player, a, range));
    private bool GuardianEnraging() => LivingGuardians().Any(a => a.CastInfo?.Action.ID == 15784);

    private bool Cooldowns(Actor player, Actor target, AIHints hints)
    {
        if (!target.IsTargetable || target.IsDead || target.PendingDead || player.CastInfo != null) return false;
        if (player.HPMP.CurMP < 3500 && !GuardianEnraging()) return false;
        // Never introduce Moon Flute's healing lockout, channels or an unsolicited J Kick dash.
        // Finish an equipped Surpanakha chain before another damage spell cancels its buff.
        if (player.FindStatus(BLU.SID.SurpanakhasFury) != null && InRange(player, target, 16)
            && WorldState.Client.BlueMageSpells.Contains((uint)BLU.AID.Surpanakha))
        {
            if (EquippedReady(BLU.AID.Surpanakha)) return Request(BLU.AID.Surpanakha, player, player, hints);
            Status("Offense: waiting briefly for the next Surpanakha charge");
            return true;
        }
        foreach (var (action, range, self) in new (BLU.AID, float, bool)[] {
            (BLU.AID.Nightbloom, 10, true), (BLU.AID.SeaShanty, 10, true),
            (BLU.AID.ShockStrike, 25, false), (BLU.AID.FeatherRain, 30, false),
            (BLU.AID.Eruption, 25, false), (BLU.AID.Surpanakha, 16, true),
            (BLU.AID.GlassDance, 12, true), (BLU.AID.MountainBuster, 6, true) })
            if (!E1Policy.ReserveBurst(action, _longAddReserve, _shortAddReserve, _addsSeen)
                && EquippedReady(action) && InRange(player, target, range) && (!self || PairInRange(player, target, range))
                && (action != BLU.AID.Surpanakha || ActionDefinitions.Instance[ActionID.MakeSpell(action)]!.ChargeCapIn(WorldState.Client.Cooldowns, WorldState.Client.DutyActions, player.Level) <= 0.25f)
                && Request(action, player, self ? player : target, hints)) return true;
        return false;
    }
    private bool Offense(Actor player, Actor target, AIHints hints, bool castSpells = true)
    {
        if (!target.IsTargetable || target.IsDead || target.PendingDead || player.CastInfo != null) return false;
        hints.ForcedTarget = target;
        if (player.FindStatus(BLU.SID.SurpanakhasFury) != null && Cooldowns(player, target, hints)) return true;
        if (!castSpells) return Cooldowns(player, target, hints);
        var flame = target.FindStatus((uint)BLU.SID.MortalFlame, player.InstanceID, WorldState.FutureTime(5));
        if (!GuardianEnraging() && !_flame.Contains(target.InstanceID) && flame == null && InRange(player, target, 25)
            && Request(BLU.AID.MortalFlame, player, target, hints)) return true;
        if (Cooldowns(player, target, hints)) return true;
        var breath = target.FindStatus((uint)BLU.SID.BreathOfMagic, player.InstanceID, WorldState.FutureTime(60));
        if (InRange(player, target, 10) && PairInRange(player, target, 10) && (breath == null || breath.Value.ExpireAt < WorldState.FutureTime(6))
            && Request(BLU.AID.BreathOfMagic, player, player, hints)) return true;
        if (InRange(player, target, 3) && Request(BLU.AID.GoblinPunch, player, target, hints, 4500)) return true;
        if (EquippedReady(BLU.AID.SonicBoom) && InRange(player, target, 25)
            && Request(BLU.AID.SonicBoom, player, target, hints, 4500)) return true;
        return false;
    }

    public override void AddAIHints(int slot, Actor player, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_config.Enabled || player.Class != Class.BLU || !player.InCombat || player.IsDead) return;
        if (WorldState.CurrentCFCID != 653 || WorldState.CurrentZone != 849) return;
        if (WorldState.Party.WithoutSlot(includeDead: true, excludeNPCs: true).Length > 1)
        { Status("Blocked: opening test supports solo only"); return; }
        if (RotationSolverRebornModule.Instance?.IsAutorotationActive == true)
        { Status("Blocked: switch RSR combat Off"); return; }
        if (AI.AIManager.Instance?.Beh == null || AI.AIManager.Instance.AiPreset != null)
        { Status("Blocked: BMR AI must be On with an empty AI preset"); return; }
        var ai = Service.Config.Get<AI.AIConfig>();
        if (ai.ForbidActions || ai.ForbidMovement || !ai.FollowDuringCombat || !ai.FollowDuringActiveBossModule || !ai.FollowTarget || !Service.Config.Get<BossModuleConfig>().AllowAutomaticActions)
        { Status("Blocked: allow BMR automatic actions and movement"); return; }
        var missing = Required.Where(a => !WorldState.Client.BlueMageSpells.Contains((uint)a)).ToArray();
        if (missing.Length != 0) { Status("Missing required spells: " + string.Join(", ", missing)); return; }
        if (player.FindStatus(BLU.SID.AethericMimicryHealer) == null)
        { Status("Blocked: Healer Aetheric Mimicry is missing; obtain it from a healer before entering"); return; }
        if (_unimplemented != 0 || _drop && WorldState.CurrentTime > _dropDeadline)
        {
            hints.ForcedMovement = Vector3.Zero;
            Status($"PAUSED: unimplemented mechanic {_unimplemented}; turn BMR AI Off for manual play and keep recording");
            return;
        }
        var guardReady = player.FindStatus((uint)BLU.SID.MightyGuard, player.InstanceID, WorldState.FutureTime(5)) != null;
        var instinctReady = player.FindStatus((uint)BLU.SID.BasicInstinct, player.InstanceID, WorldState.FutureTime(5)) != null;
        var defenseNow = _spear || _tankVice || Module.PrimaryActor.CastInfo?.Action.ID is 15777 or 17636;
        var prepare = E1Policy.Prepare(guardReady, instinctReady, defenseNow);
        if (prepare != null)
        {
            if (player.CastInfo != null)
            { hints.ForcedMovement = Vector3.Zero; Status("Applying solo buffs; waiting for the cast result"); return; }
            Request(prepare.Value, player, player, hints);
            return;
        }
        _puddles.RemoveAll(p => p.expires <= WorldState.CurrentTime);
        foreach (var p in _puddles) hints.AddForbiddenZone(new AOEShapeCircle(6), p.pos, default, WorldState.CurrentTime);
        var boss = Module.PrimaryActor;
        var cast = boss.CastInfo;
        var spear = _spear || WorldState.CurrentTime < _spearUntil;
        var tankVice = _tankVice;
        var gravity = cast?.Action.ID is 15764 or 15772 or 15780;
        var shield = player.FindStatus(1722u);
        if (_mechanicGoal is WPos goal && WorldState.CurrentTime < _positionUntil)
        {
            hints.ForcedTarget = boss;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(goal, 0.75f, 100));
            var name = _positionMechanic == 15779 ? "Pure Light: get behind the corner boss" : "Delta Attack: hold the safe corner through helper impacts";
            if (!Arena.InBounds(goal))
            { hints.ForcedMovement = Vector3.Zero; Status("PAUSED: mechanic goal outside arena; manual takeover required"); return; }
            if ((player.Position - goal).Length() > 1)
            {
                if (player.CastInfo != null) hints.ForceCancelCastOther = true;
                Status(name + "; moving before casting");
                return;
            }
            if (!Safe(player.Position, hints, 2.5f))
            {
                if (player.CastInfo != null) hints.ForceCancelCastOther = true;
                Status(name + "; dodge overlapping beam/ground attacks before healing");
                return;
            }
            hints.ForcedMovement = Vector3.Zero;
            // Do not chase Eden from the safe corner. The solo targeted Delta hit is unavoidable.
            if (player.CastInfo == null && shield == null && player.HPRatio < 0.65f)
                Request(BLU.AID.PomCure, player, player, hints);
            else if (shield == null && player.CastInfo == null && player.HPMP.CurMP < 3000 && InRange(player, boss, 25))
                Request(BLU.AID.BloodDrain, player, boss, hints);
            else if (shield == null && !Offense(player, boss, hints)) Status(name + "; safe attack window");
            return;
        }
        _mechanicGoal = null;
        var guardians = LivingGuardians();
        var enraging = guardians.Any(a => a.CastInfo?.Action.ID == 15784);
        var arrival = WorldState.CurrentTime < _arrivalUntil || _transition && !boss.IsTargetable && guardians.Length == 0;
        if (arrival && shield == null)
        {
            var arrivalGoal = new WPos(100, 117);
            hints.GoalZones.Add(AIHints.GoalSingleTarget(arrivalGoal, 1, 60));
            if ((player.Position - arrivalGoal).Length() > 1.5f)
            {
                if (player.CastInfo != null) hints.ForceCancelCastOther = true;
                Status("Guardian transition: move south before the two proximity impacts");
                return;
            }
            if (player.CastInfo == null && player.HPRatio < 0.98f) Request(BLU.AID.PomCure, player, player, hints);
            else { hints.ForcedMovement = Vector3.Zero; Status("Guardian transition: hold south and prepare recovery"); }
            return;
        }
        var target = guardians.OrderBy(a => E1Policy.GuardianRank(_flame.Contains(a.InstanceID)
                || a.FindStatus((uint)BLU.SID.MortalFlame, player.InstanceID, WorldState.FutureTime(5)) != null, a.HPMP.CurHP))
            .FirstOrDefault() ?? boss;
        foreach (var potential in hints.PotentialTargets)
            if (potential.Actor.OID == 0x249A) potential.Priority = 5;
        if (E1Policy.NeedDodge(shield != null, Safe(player.Position, hints, 2.5f)))
        {
            hints.ForcedTarget = target;
            if (player.CastInfo != null) hints.ForceCancelCastOther = true;
            Status("Dodge snapshotted orb beams / Paradise Lost; casting deferred");
            return;
        }
        var moveForFlare = cast?.Action.ID == 15767 && (player.Position - boss.Position).Length() > 2.5f;
        if (moveForFlare && shield == null)
        {
            hints.ForcedTarget = boss;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(boss.Position, 2, 40));
            if (player.CastInfo != null) hints.ForceCancelCastOther = true;
            Status("Flare: move under the boss before casting");
            return;
        }
        var guardianDefense = guardians.Length != 0
            ? E1Policy.GuardianDefense(_guardian.Reserve(WorldState.CurrentTime), player.FindStatus(1789u) != null, player.HPMP.MaxHP, player.HPRatio) : null;
        var task = E1Policy.Decide(shield != null, spear, tankVice, gravity || guardians.Any(a => a.CastInfo?.Action.ID == 15783), !_firstSpearSeen, player.HPRatio, player.HPMP.CurMP,
            guardians.Length != 0 && player.FindStatus(1789u) == null ? 3500u : 6000u);
        if (shield == null && guardianDefense != null) task = guardianDefense.Value;
        task = E1Policy.FinishGuardian(enraging, shield != null, task);
        if (task == E1Task.Hold && shield != null)
        {
            hints.ForcedMovement = Vector3.Zero;
            var shieldRemaining = (shield!.Value.ExpireAt - WorldState.CurrentTime).TotalSeconds;
            Status(spear && cast?.Action.ID == 15777 && shieldRemaining < cast.NPCRemainingTime + 1.8f
                ? "Current Diamondback expires before second Spear; reserve recast after it ends"
                : "Diamondback confirmed; hold, then recover health");
            return;
        }
        if ((spear || tankVice || !enraging && guardianDefense == E1Task.Shield && player.HPMP.CurMP >= 3000) && player.CastInfo is ActorCastInfo ownCast && ownCast.Action.ID != (uint)BLU.AID.Diamondback)
        { hints.ForceCancelCastOther = true; Status("Cancel filler cast and reserve Diamondback"); return; }
        if (player.CastInfo != null)
        { if (Safe(player.Position, hints, player.CastInfo.RemainingTime + 0.5f)) hints.ForcedMovement = Vector3.Zero; Status("Casting; waiting for observed result"); return; }
        hints.ForcedTarget = target;
        if (guardians.Length == 2)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(E1Policy.GuardianAnchor, 1, 40));
        else if (_drop)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(new WPos(84, 116), 1, 40));
        else if (cast?.Action.ID == 15767)
            hints.GoalZones.Add(AIHints.GoalSingleTarget(boss.Position, 2, 40));
        else
            hints.GoalZones.Add(AIHints.GoalSingleTarget(target.Position, 2.5f + target.HitboxRadius, _beamPhase ? 1 : 5));
        if (_drop && (player.Position - new WPos(84, 116)).Length() > 2 && task is E1Task.Damage or E1Task.Mana)
        { Status("Move to the puddle bait before casting damage spells"); return; }
        if (guardians.Length == 2 && (player.Position - E1Policy.GuardianAnchor).Length() > 1.5f && task is E1Task.Damage or E1Task.Mana)
        { Status("Guardian pair: group both in front before applying centered AoE burst"); return; }
        if (enraging) Status("Unto Dust: finish the surviving Guardian now; routine recovery reservations released");
        switch (task)
        {
            case E1Task.Shield:
                if (cast is ActorCastInfo c && c.Action.ID is 15777 or 17636
                    && !E1Policy.CanShield(c.NPCRemainingTime, WorldState.Client.Cooldowns[ActionDefinitions.GCDGroup].Remaining))
                    Status("DEFENSIVE DEADLINE: Diamondback may miss first hit; requesting immediately");
                if (guardians.Length != 0 && player.HPMP.CurMP < 3000)
                {
                    if (player.HPRatio < 0.95f) Request(BLU.AID.PomCure, player, player, hints);
                    else Request(BLU.AID.BloodDrain, player, target, hints);
                    return;
                }
                if (guardians.Length != 0) Status("Guardian vulnerability/low max HP: Diamondback needed for instant cleaves");
                Request(BLU.AID.Diamondback, player, player, hints); return;
            case E1Task.Heal: Request(BLU.AID.PomCure, player, player, hints); return;
            case E1Task.Mana: if (target.IsTargetable) Request(BLU.AID.BloodDrain, player, target, hints); return;
            case E1Task.Hold:
                if (guardianDefense == E1Task.Hold)
                {
                    if (!Cooldowns(player, target, hints)) Status("Guardian cleaves: health prepared; GCD reserved for recovery, instant cooldowns allowed");
                    return;
                }
                if (Cooldowns(player, target, hints)) return;
                Status(gravity || guardians.Length != 0 ? "Raidwide: health prepared, reserve recovery GCD" : "Opening: reserve GCD for first Spear"); return;
        }
        if (!Offense(player, target, hints)) Status(target.IsTargetable ? "Offense: approaching or waiting for an available attack" : "Waiting for targetability");
    }
}

sealed class E1SoloStates : StateMachineBuilder
{
    public E1SoloStates(BossModule module) : base(module) => TrivialPhase().ActivateOnEnter<E1Hazards>().ActivateOnEnter<E1Opening>();
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = 0x2499u, Contributors = "Local solo BLU opening test",
    GroupType = BossModuleInfo.GroupType.CFC, GroupID = 653u, NameID = 8345u)]
public sealed class E1Solo(WorldState ws, Actor boss) : BossModule(ws, boss, new(100, 100), new ArenaBoundsSquare(19));
