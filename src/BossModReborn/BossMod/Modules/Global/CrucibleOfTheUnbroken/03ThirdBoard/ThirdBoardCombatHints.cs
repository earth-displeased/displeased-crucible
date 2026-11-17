namespace BossMod.Global.CrucibleOfTheUnbroken.ThirdBoard;
public static class ThirdBoardCombatHints
{
    public static void Apply(BossModule module,Actor player,AIHints hints)
    {
        if(module.PrimaryActor.OID==0x4C8E) CavalierSafetyPolicy.Prepare(hints,module.Center,module.WorldState.CurrentTime);
        foreach(var enemy in hints.PotentialTargets)
        {
            var a=enemy.Actor;
            var guarded=(a.OID==0x4C95 && a.FindStatus(5434)!=null) || (a.OID==0x4C93 && a.FindStatus(2198)!=null)
                || (a.OID==0x4CA5 && a.FindStatus(2413)!=null);
            var priority=ThirdBoardTargetPolicy.Priority(a.OID,guarded);
            if(priority<0) enemy.Priority=AIHints.Enemy.PriorityForbidden;
            else if(enemy.Priority>=0) enemy.Priority=Math.Max(enemy.Priority,priority);
        }
        var chosen=hints.PotentialTargets.Where(e=>e.Priority>0 && e.Actor.IsTargetable && !e.Actor.IsDeadOrDestroyed)
            .OrderByDescending(e=>e.Priority).ThenBy(e=>(e.Actor.Position-player.Position).LengthSq()).FirstOrDefault();
        if(chosen!=null)
        {
            hints.ForcedTarget=chosen.Actor;
            hints.GoalZones.Add(AIHints.GoalSingleTarget(chosen.Actor.Position,chosen.Actor.HitboxRadius+2.5f,2f));
        }
    }
}
