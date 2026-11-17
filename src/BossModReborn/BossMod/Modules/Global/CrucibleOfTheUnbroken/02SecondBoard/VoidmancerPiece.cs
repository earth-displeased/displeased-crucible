namespace BossMod.Global.CrucibleOfTheUnbroken.SecondBoard.VoidmancerPiece;

public enum OID : uint {
    VoidmancerPiece = 0x4C5C,
    ZombiePiece = 0x4C5D, // R0.750, x20
    Malady = 0x4C5E, // R1.000, x0 (spawn during fight)
    Helper = 0x233C,
}

public enum AID : uint {
    AutoAttackWater = 50793, // VoidmancerPiece->player, no cast, single-target
    AutoAttackZombie = 48251, // 4C5D->player, no cast, single-target
    DeathDriveBait = 48181, // VoidmancerPiece->self, 7.0s cast, single-target
    DeathDrive = 48182, // Helper->location, 4.0s cast, range 10 circle
    DarkOrbBoss = 48185, // VoidmancerPiece->self, 6.5+1.5s cast, single-target
    DarkOrb = 48186, // Helper->self, 8.0s cast, range 18 circle
    EvilMist = 48183, // VoidmancerPiece->self, 4.0s cast, range 60 circle
    Necropurge = 48184, // 4C5E->self, 1.0s cast, range 8 circle
    Mindjack = 48187, // VoidmancerPiece->self, 4.0s cast, range 100 circle
}

public enum SID : uint {
    WitsEnd = 5424, // 4C5D/4C5E->player, extra=0x1/0x2/0x4/0x5/0x6/0x7/0x8 - reaching 12 stacks will cause confuse on the player
    AboutFace = 2162, // VoidmancerPiece->player, extra=0x0
    LeftFace = 2163, // VoidmancerPiece->player, extra=0x0
    RightFace = 2164, // VoidmancerPiece->player, extra=0x0
    ForcedMarch = 1257, // VoidmancerPiece->player, extra=0x4
}

public enum IconID : uint {
    DeathDrive = 707, // player->self
}

public enum TetherID : uint {
    ZombieTether = 17, // 4C5D->player
}

sealed class DeathDrive(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DeathDrive, 10.0f);
sealed class DarkOrb(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DarkOrb, 18.0f);

sealed class DeathDriveBait(BossModule module) : Components.BaitAwayIcon(module, 10.0f, (uint)IconID.DeathDrive, (uint)AID.DeathDriveBait, 7.5f) {
    public override void DrawArenaForeground(int pcSlot, Actor pc) {
        base.DrawArenaForeground(pcSlot, pc);

        if (!IsBaitTarget(pc) || CurrentBaits.Count == 0) {
            return;
        }

        var bait = CurrentBaits[0];
        var baitRadius = ((AOEShapeCircle)bait.Shape).Radius;

        foreach (var zombie in Module.Enemies((uint)OID.ZombiePiece)) {
            var reach = baitRadius + zombie.HitboxRadius;

            var onHitbox = (zombie.Position - bait.Target.Position).LengthSq() <= reach * reach;
            Arena.ZoneCircleOutline(zombie.Position, zombie.HitboxRadius, onHitbox ? Colors.Danger : Colors.Border);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) {
        if (!IsBaitTarget(actor) || CurrentBaits.Count == 0) {
            return;
        }

        var bait = CurrentBaits[0];
        var baitRadius = ((AOEShapeCircle)bait.Shape).Radius;

        var zombies = Module.Enemies((uint)OID.ZombiePiece);
        var count = zombies.Count;
        if (count == 0) {
            return;
        }

        for (var i = 0; i < count; i++) {
            var zombie = zombies[i];
            var reach = baitRadius + zombie.HitboxRadius;
            hints.GoalZones.Add(p => (p - zombie.Position).LengthSq() > reach * reach ? 5.0f : 0.0f);
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) {
        if (!IsBaitTarget(actor) || CurrentBaits.Count == 0) {
            return;
        }

        hints.Add("Avoid intersecting zombies hitboxes!");
    }
}

sealed class Malady(BossModule module) : Components.Voidzone(module, 8.0f, GetVoidzones) {
    private static Actor[] GetVoidzones(BossModule module) {
        var enemies = module.Enemies((uint)OID.Malady);
        var count = enemies.Count;
        if (count == 0) {
            return [];
        }

        var voidzones = new Actor[count];
        var index = 0;
        for (var i = 0; i < count; ++i) {
            var z = enemies[i];
            if (z.EventState != 7)
                voidzones[index++] = z;
        }
        return voidzones[..index];
    }
}

sealed class Mindjack(BossModule module) : Components.StatusDrivenForcedMarch(module, 3.0f, default, (uint)SID.AboutFace, (uint)SID.LeftFace,
    (uint)SID.RightFace) {
    private readonly Malady? malady = module.FindComponent<Malady>();

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) {
        if (!Arena.InBounds(pos)) {
            return true;
        }

        if (malady == null) {
            return false;
        }

        var orbs = malady.ActiveAOEs(slot, actor);
        var count = orbs.Length;
        var direction = (pos - actor.Position).Normalized();

        for (var i = 0; i < count; i++) {
            if (Intersect.RayCircle(actor.Position - orbs[i].Origin, direction, 8.0f, 18.0f)) {
                return true;
            }
        }

        return false;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) {
        var state = State.GetValueOrDefault(actor.InstanceID);
        if (state == null || state.PendingMoves.Count == 0) {
            return;
        }

        ref var move0 = ref state.PendingMoves.Ref(0);
        var act = move0.activation;
        var pos = actor.Position;
        var moveDir = move0.dir.ToDirection();

        var pools = malady?.ActiveAOEs(slot, actor).ToArray().Select(a=>a.Origin).ToArray() ?? [];
        // Test the full 18-yalm march, not just whether the initial direction points inward.
        for (var degrees=0;degrees<360;degrees+=5)
        {
            var facing=((float)degrees).Degrees();
            if (!VoidmancerSafetyPolicy.MarchSafe(pos,facing,move0.dir,Arena.Center,pools))
                hints.ForbiddenDirections.Add((facing,2.6f.Degrees(),act));
        }
    }
}

sealed class VoidmancerPieceStates : StateMachineBuilder {
    public VoidmancerPieceStates(BossModule module) : base(module) {
        TrivialPhase()
            .ActivateOnEnter<DeathDriveBait>()
            .ActivateOnEnter<DeathDrive>()
            .ActivateOnEnter<DarkOrb>()
            .ActivateOnEnter<Malady>()
            .ActivateOnEnter<Mindjack>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, PrimaryActorOID = (uint)OID.VoidmancerPiece, Contributors = "Equilius", GroupType = BossModuleInfo.GroupType.CrucibleOfTheUnbroken, GroupID = 1089u, NameID = 14552u, SortOrder = 3)]
public sealed class VoidmancerPiece(WorldState ws, Actor primary) : BossModule(ws, primary, new(120f, 0f), new ArenaBoundsRect(19f, 19f)) {
    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints) {
        Actor? nearbyZombie = null;
        foreach (var enemy in hints.PotentialTargets)
        {
            var target=enemy.Actor;
            var reachable=target.IsTargetable && !target.IsDeadOrDestroyed
                && (target.Position-actor.Position).LengthSq() <= MathF.Pow(actor.HitboxRadius+target.HitboxRadius+3,2);
            enemy.Priority = target.OID == (uint)OID.ZombiePiece ? (reachable ? 3 : 0)
                : target.OID == (uint)OID.VoidmancerPiece ? 1 : 0;
            if (target.OID == (uint)OID.ZombiePiece && reachable && (nearbyZombie == null
                || (target.Position-actor.Position).LengthSq()<(nearbyZombie.Position-actor.Position).LengthSq())) nearbyZombie=target;
        }
        if (nearbyZombie != null) hints.ForcedTarget=nearbyZombie;
        else if (PrimaryActor.IsTargetable && !PrimaryActor.IsDeadOrDestroyed) hints.ForcedTarget=PrimaryActor;
        if (actor.FindStatus((uint)SID.ForcedMarch) == null && actor.FindStatus(1283) == null)
        {
            if (Math.Abs(actor.Position.X-Center.X)>19 || Math.Abs(actor.Position.Z-Center.Z)>19)
                hints.ForcedMovement=(VoidmancerSafetyPolicy.RecoverInside(actor.Position,Center)-actor.Position).ToVec3();
            else if (hints.ForbiddenZones.Count==0 && FindComponent<DeathDriveBait>()?.CurrentBaits.Count==0)
                hints.GoalZones.Add(AIHints.GoalSingleTarget(PrimaryActor.Position,PrimaryActor.HitboxRadius+2.6f,0.5f));
        }
    }

    protected override void DrawEnemies(int pcSlot, Actor pc) {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.ZombiePiece));
    }
}
