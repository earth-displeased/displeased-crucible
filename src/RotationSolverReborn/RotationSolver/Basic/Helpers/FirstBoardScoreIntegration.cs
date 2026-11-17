using ECommons.DalamudServices;
namespace RotationSolver.Basic.Helpers;
internal static class FirstBoardScoreIntegration
{
    public static bool Stopped
    {
        get { try { return Svc.PluginInterface.GetIpcSubscriber<bool>("BeastHelper.AutomationStopped").InvokeFunc(); } catch { return false; } }
    }
    public static bool UrgentWyvernCrossing => KeepAuto && Svc.ClientState.TerritoryType == 1340
        && Svc.Objects.OfType<IBattleChara>().Any(a => a.BaseId == 0x4C58 && a.IsCasting && a.CastActionId is 48174 or 48176 or 48177);
    public static float PhysicalVulnerabilityRemaining(IBattleChara target)
        => target.StatusList.Where(s => s.StatusId == 5180).Select(s => s.RemainingTime).DefaultIfEmpty(0).Max();
    public static bool ReserveMindjackFacing => KeepAuto && Svc.ClientState.TerritoryType is 1340 or 1341
        && Svc.Objects.LocalPlayer is {} player && player.StatusList.Any(s =>
            s.StatusId is 1257 or 1283 || s.StatusId is 2161 or 2162 or 2163 or 2164 && s.RemainingTime <= 2f);
    public static IBattleChara? OpportunisticZombie(IEnumerable<IBattleChara>? candidates, float range)
    {
        if (candidates == null || range is <= 0 or > 5 || !KeepAuto || Svc.ClientState.TerritoryType != 1340
            || Svc.Objects.LocalPlayer is not {} player
            || !Svc.Objects.OfType<IBattleChara>().Any(a=>a.BaseId==0x4C5C && a.CurrentHp>0)) return null;
        return candidates.Where(a=>a.BaseId==0x4C5D && a.CurrentHp>0 && a.IsTargetable)
            .OrderByDescending(a=>a.TargetObjectId==player.GameObjectId)
            .ThenBy(a=>Vector3.DistanceSquared(a.Position,player.Position)).FirstOrDefault();
    }
    public static bool ReserveThirdBoardGaze
    {
        get
        {
            if(!KeepAuto || Svc.ClientState.TerritoryType!=1341) return false;
            try { return Svc.PluginInterface.GetIpcSubscriber<bool>("BossMod.Crucible.ThirdBoard.GazeImminent").InvokeFunc(); }
            catch { return false; }
        }
    }
    public static bool ThirdBoard => KeepAuto && Svc.ClientState.TerritoryType==1341;
    public static bool YmirPairAlive => ThirdBoard
        && Svc.Objects.OfType<IBattleChara>().Any(a=>a.BaseId==0x4C93 && a.CurrentHp>0)
        && Svc.Objects.OfType<IBattleChara>().Any(a=>a.BaseId==0x4C95 && a.CurrentHp>0);
    public static bool ReserveGuttlerRetreat => ThirdBoard && Svc.Objects.OfType<IBattleChara>()
        .Any(a=>a.CurrentHp>0 && a.IsCasting && ThirdBoardDefensePolicy.ReserveGapCloser(a.BaseId,a.CastActionId));
    public static bool SirenEncounter => ThirdBoard && Svc.Objects.OfType<IBattleChara>()
        .Any(a=>a.CurrentHp>0 && a.BaseId is 0x4CA1 or 0x4CA3 or 0x4CA4);
    public static bool UrgentSirenAdd => ThirdBoard && Svc.Objects.OfType<IBattleChara>()
        .Any(a=>a.BaseId==0x4CA4 && a.IsTargetable && a.CurrentHp>0 && a.DistanceToPlayer()<=20f);
    public static bool ThirdIncomingDamage => ThirdBoard && Svc.Objects.OfType<IBattleChara>()
        .Any(a=>a.CurrentHp>0 && a.IsCasting && ThirdBoardDefensePolicy.Incoming(a.BaseId,a.CastActionId));
    public static int ThirdPriority(IBattleChara target)
        => BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard.ThirdBoardTargetPolicy.Priority(target.BaseId,
            (target.BaseId==0x4C95 && (target.StatusList.Any(s=>s.StatusId==5434) || target.IsCasting && target.CastActionId==50532))
            || (target.BaseId==0x4C93 && target.StatusList.Any(s=>s.StatusId==2198))
            || (target.BaseId==0x4CA5 && target.StatusList.Any(s=>s.StatusId==2413)));
    public static int ThirdActionPriority(IBattleChara target,bool petCommand) => petCommand && target.BaseId==0x4C95 ? 3 : ThirdPriority(target);
    public static IBattleChara? FamiliarRescueTarget(IEnumerable<IBattleChara> candidates)
    {
        if(!BeastkinClear || RotationSolver.Basic.Rotations.Basic.BeastmasterRotation.Familiar is not {} familiar) return null;
        return candidates.Where(a=>a.TargetObjectId==familiar.GameObjectId)
            .OrderByDescending(a=>ThirdBoardDefensePolicy.ChallengePriority(a.BaseId,true)).ThenByDescending(a=>a.MaxHp).FirstOrDefault();
    }
    public static IBattleChara? ThirdTarget(IEnumerable<IBattleChara> candidates,bool petCommand=false)
        => !ThirdBoard || Svc.Objects.LocalPlayer is not {} player ? null : candidates.Where(a=>ThirdActionPriority(a,petCommand)>0)
            .OrderByDescending(a=>ThirdActionPriority(a,petCommand)).ThenBy(a=>Vector3.DistanceSquared(a.Position,player.Position)).FirstOrDefault();
    public static bool KnightAlive => Svc.Objects.OfType<IBattleChara>().Any(a => a.BaseId == 0x4B86 && a.CurrentHp > 0 && a.IsTargetable);
    public static bool Enabled(string name)
    {
        if (Stopped || Svc.ClientState.TerritoryType is not (1339 or 1340 or 1341)) return false;
        try { return Svc.PluginInterface.GetIpcSubscriber<bool>(name).InvokeFunc(); }
        catch { return false; }
    }
    public static bool FirstMaster
    {
        get
        {
            if (Stopped || Svc.ClientState.TerritoryType != 1342) return false;
            try { return Svc.PluginInterface.GetIpcSubscriber<bool>("BeastHelper.FirstMasterLegendaryActive").InvokeFunc(); }
            catch { return false; }
        }
    }
    /// <summary>BeastHelper's Second Master Legendary preset is active on Second Master's Board (territory 1343).</summary>
    public static bool SecondMaster
    {
        get
        {
            if (Stopped || Svc.ClientState.TerritoryType != 1343) return false;
            try { return Svc.PluginInterface.GetIpcSubscriber<bool>("BeastHelper.SecondMasterLegendaryActive").InvokeFunc(); }
            catch { return false; }
        }
    }
    /// <summary>Either Master preset: familiar cover / damage sharing rules shared by both boards.</summary>
    public static bool MasterBoard => FirstMaster || SecondMaster;
    /// <summary>Lauda Unseen Force: BMR is turning the player before a facing-based throw; do not act (actions turn).</summary>
    public static bool ReserveSecondMasterFacing
    {
        get
        {
            if (!SecondMaster) return false;
            try { return Svc.PluginInterface.GetIpcSubscriber<bool>("BossMod.Crucible.SecondMaster.FacingImminent").InvokeFunc(); }
            catch { return false; }
        }
    }
    /// <summary>Second Master preset with Lauda (0x4D1E) alive: familiar-tanking rules (SecondMasterDefensePolicy.LaudaCover).</summary>
    public static bool SecondMasterLauda => SecondMaster && Svc.Objects.OfType<IBattleChara>()
        .Any(a => a.BaseId == 0x4D1E && a.CurrentHp > 0);
    /// <summary>Gigantis slimes (SecondMasterTargetPolicy; replays 2026-11-13 17:24 and 18:04: spare slime never attacked).</summary>
    public static bool SecondMasterSlimeEncounter => SecondMaster && Svc.Objects.OfType<IBattleChara>()
        .Any(a => a.CurrentHp > 0 && a.IsTargetable && BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(a.BaseId));
    public static int SecondMasterPriority(IBattleChara target)
    {
        if (!BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Tracked(target.BaseId)) return 0;
        var objects = Svc.Objects.OfType<IBattleChara>().ToArray();
        var weapon = objects.Where(a => a.BaseId == 0x4D03 && a.CurrentHp > 0)
            .SelectMany(a => a.StatusList).Where(s => s.StatusId == BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.WeaponElementStatus)
            .Select(s => s.Param).FirstOrDefault();
        var alive = objects.Count(a => a.CurrentHp > 0 && a.IsTargetable && BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.BigSlime(a.BaseId));
        return BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Priority(target.BaseId,
            target.IsCasting && BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.Rupture(target.CastActionId), weapon, alive);
    }
    public static IBattleChara? SecondMasterTarget(IEnumerable<IBattleChara> candidates)
        => !SecondMasterSlimeEncounter || Svc.Objects.LocalPlayer is not {} player ? null
            : candidates.Where(a => a.CurrentHp > 0 && a.IsTargetable && SecondMasterPriority(a) > 0)
                // Finish what is already targeted / lowest HP first (replay 23:11: a blade was left at 620 HP).
                .OrderByDescending(SecondMasterPriority).ThenByDescending(a => a.GameObjectId == player.TargetObjectId)
                // Spinner-rooks: nearest next (replay 2026-11-14 11:26: walked 27y to a lower-HP rook, then backtracked).
                .ThenBy(a => BossMod.Global.CrucibleOfTheUnbroken.SecondMasterTargetPolicy.PreferNearest(a.BaseId) ? Vector3.DistanceSquared(a.Position, player.Position) : 0f)
                .ThenBy(a => a.CurrentHp).ThenBy(a => Vector3.DistanceSquared(a.Position, player.Position)).FirstOrDefault();
    public static bool FirstMasterTreant =>FirstMaster && Svc.Objects.OfType<IBattleChara>()
        .Any(a => a.BaseId == 0x4CCD && a.CurrentHp > 0 && a.IsTargetable);
    public static IBattleChara? FirstMasterMeteorTarget(IEnumerable<IBattleChara> candidates)
    {
        if (!FirstMasterTreant || Svc.Objects.LocalPlayer is not {} player) return null;
        var slugs = Svc.Objects.OfType<IBattleChara>().Where(a => a.BaseId == 0x4CCE && a.CurrentHp > 0 && a.IsTargetable).ToArray();
        if (slugs.Length == 0) return null;
        // Meteor is target-centered, radius 18. Leave a small margin for motion.
        return candidates.Select(a => new {Target=a,
                NearbyHits=slugs.Count(s => Vector3.DistanceSquared(s.Position,player.Position) <= 100f && Vector3.DistanceSquared(s.Position,a.Position) <= 17.5f*17.5f),
                Hits=slugs.Count(s => Vector3.DistanceSquared(s.Position,a.Position) <= 17.5f*17.5f)})
            .Where(x => x.Hits > 0).OrderByDescending(x => x.NearbyHits).ThenByDescending(x => x.Hits)
            .ThenBy(x => Vector3.DistanceSquared(x.Target.Position,player.Position)).Select(x => x.Target).FirstOrDefault();
    }
    public static bool FirstMasterPriorityEncounter => FirstMaster && Svc.Objects.OfType<IBattleChara>()
        .Any(a=>a.CurrentHp>0 && a.IsTargetable && a.BaseId is 0x4CCD or 0x4CBD);
    public static int FirstMasterPriority(IBattleChara target) => BossMod.Global.CrucibleOfTheUnbroken.FirstMasterTargetPolicy.Priority(
        target.BaseId, target.IsCasting && target.CastActionId == 48782);
    public static IBattleChara? FirstMasterTarget(IEnumerable<IBattleChara> candidates)
        => !FirstMasterPriorityEncounter || Svc.Objects.LocalPlayer is not {} player ? null
            : candidates.Where(a => a.CurrentHp > 0 && a.IsTargetable && FirstMasterPriority(a) > 0)
                .OrderByDescending(FirstMasterPriority).ThenBy(a => Vector3.DistanceSquared(a.Position, player.Position)).FirstOrDefault();
    public static bool KeepAuto => FirstMaster || Enabled("BeastHelper.FirstBoardScoreActive");
    public static bool BeastkinClear => Enabled("BeastHelper.ThirdBoardBeastkinActive") && Svc.ClientState.TerritoryType==1341;
    public static bool Vilekin => Enabled("BeastHelper.FirstBoardVilekinActive");
    public static bool HasGuard(IBattleChara enemy)
        => Svc.ClientState.TerritoryType == 1339 && enemy.BaseId == 0x4B86
            && enemy.StatusList.Any(s => s.StatusId == 680 || (s.StatusId == 2552 && s.Param == 0x425));
    public static bool HoldLinkedBossFinisher(IBattleChara target)
    {
        if (Svc.ClientState.TerritoryType != 1340 || target.BaseId is not (0x4C60 or 0x4C5F)) return false;
        return Svc.Objects.OfType<IBattleChara>().Any(a => a.BaseId is 0x4C60 or 0x4C5F
            && a.EntityId != target.EntityId && a.CurrentHp > 0 && a.MaxHp > 0
            && (double)a.CurrentHp / a.MaxHp > 0.10);
    }
    public static bool BishopAlive => Svc.Objects.OfType<IBattleChara>().Any(a => a.BaseId == 0x4B87 && a.CurrentHp > 0 && a.IsTargetable);
    public static bool Guarded(IBattleChara enemy)
    {
        if (Svc.ClientState.TerritoryType != 1339 || enemy.BaseId != 0x4B86 || Svc.Objects.LocalPlayer is not { } player) return false;
        var guarded = HasGuard(enemy);
        var offset = new Vector2(player.Position.X - enemy.Position.X, player.Position.Z - enemy.Position.Z);
        if (!guarded || offset.LengthSquared() < 0.01f) return guarded;
        return Vector2.Dot(Vector2.Normalize(offset), new Vector2(MathF.Sin(enemy.Rotation), MathF.Cos(enemy.Rotation))) > 0.65f;
    }
}
