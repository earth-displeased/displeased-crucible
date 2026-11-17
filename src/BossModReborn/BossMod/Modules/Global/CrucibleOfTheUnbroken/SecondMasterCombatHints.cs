namespace BossMod.Global.CrucibleOfTheUnbroken;

/// <summary>
/// Second Master's Board (zone 1343, group 1092): melee-range goal for BST towards the player's CURRENT target, for every
/// encounter whose module does not choose a target itself (Lauda does).
/// Why: replay 2026-11-13 17:14 (SecondMastersBoard_2026_11_13_17_14_56): at Mind Flayer the player stood still 12.8y from
/// the boss for 54s (86s idle in a 143s fight), ran out of heals and died. Attempt 1: Boogyman 41s idle, Durga 24s.
/// 7.5.7.538 used FirstMasterCombatHints.ApproachPriorityTarget, which also sets ForcedTarget from BMR priorities; RSR
/// picks its own target on this board, so the two fought: replay 17:52 (SecondMastersBoard_..._17_52_20) player target
/// changes Flauros 132 (17:24 run: 7), Drake 112 (24), Drake idle 57s (30s), player died at Drake. So: no ForcedTarget here;
/// approach whatever RSR targets, and never a target the module forbids (e.g. Abaddon while Morphos live).
/// </summary>
public static class SecondMasterCombatHints
{
    public const uint Zone = 1343;
    public const uint Group = 1092;

    public static void Apply(BossModule module, Actor player, AIHints hints)
    {
        if (player.Class != Class.BST || module.WorldState.CurrentZone != Zone || module.Info?.GroupID != Group || hints.ForcedTarget != null)
        {
            return;
        }
        var target = module.WorldState.Actors.Find(player.TargetID);
        var enemy = target == null || target.IsDeadOrDestroyed || !target.IsTargetable ? null : hints.FindEnemy(target);
        if (enemy == null || enemy.Priority < 0)
        {
            // No usable target: RSR only targets enemies in range, so nobody moved. Replay 2026-11-14 11:26
            // (SecondMastersBoard_BST50_Ta_Be_2026_11_14_11_26_30): Mindflayer targetable 11:29:22 at 20y, BeastHelper's
            // approach stopped at 14.5y when BMR took over (11:29:27); no target and no action for 22s while the boss
            // auto-attacked from range (player 78% at 11:29:49). Approach the module's best target instead (no ForcedTarget).
            enemy = BestApproach(player, hints);
            if (enemy == null)
            {
                return;
            }
            target = enemy.Actor;
        }
        hints.GoalZones.Add(AIHints.GoalSingleTarget(target!.Position, target.HitboxRadius + 2.5f, 2f));
    }

    private static AIHints.Enemy? BestApproach(Actor player, AIHints hints)
    {
        AIHints.Enemy? best = null;
        var bestDistance = float.MaxValue;
        foreach (var e in hints.PotentialTargets)
        {
            if (e.Priority < 0 || !e.Actor.IsTargetable || e.Actor.IsDeadOrDestroyed)
            {
                continue;
            }
            var d = (e.Actor.Position - player.Position).LengthSq();
            if (best == null || e.Priority > best.Priority || e.Priority == best.Priority && d < bestDistance)
            {
                best = e;
                bestDistance = d;
            }
        }
        return best;
    }
}
