namespace BossMod.Global.CrucibleOfTheUnbroken;
public static class FirstMasterCombatHints
{
    /// <summary>
    /// BST only: force the highest-priority target and, unless <paramref name="approach"/> is false (e.g. while baiting),
    /// add a weak melee-range goal so the pathfinder returns to the target after dodging (forbidden zones still win).
    /// Replay 2026-11-12 22:49: modules with this goal had 13s (Treant) idle; without it Gargoyle had 74s of 181s and
    /// Borgny 47s of 132s with no action, standing 8-35y from the target after dodges (22:01 run: 88s / 88s).
    /// </summary>
    public static void ApproachPriorityTarget(Actor player,AIHints hints,bool approach=true)
    {
        if(player.Class != Class.BST) return;
        var chosen=hints.PotentialTargets.Where(e=>e.Priority>0 && e.Actor.IsTargetable && !e.Actor.IsDeadOrDestroyed)
            .OrderByDescending(e=>e.Priority).ThenBy(e=>(e.Actor.Position-player.Position).LengthSq()).FirstOrDefault();
        if(chosen == null) return;
        hints.ForcedTarget=chosen.Actor;
        if(approach) hints.GoalZones.Add(AIHints.GoalSingleTarget(chosen.Actor.Position,chosen.Actor.HitboxRadius+2.5f,2f));
    }
}
