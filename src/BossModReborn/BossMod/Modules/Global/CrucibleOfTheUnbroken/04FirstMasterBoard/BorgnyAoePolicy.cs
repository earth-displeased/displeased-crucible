namespace BossMod.Global.CrucibleOfTheUnbroken;

/// <summary>Borgny the Venomous AOE timing / geometry, kept free of BMR types so tests/FirstBoardScore can check it.</summary>
public static class BorgnyAoePolicy
{
    /// <summary>
    /// Toxic Breath (48807, 2.7s cast) hits six times; the first hit (48808) lands 5.33-5.41s after the cast starts,
    /// i.e. 2.6-2.7s after the cast ends (13 of 13 breaths in replays 2026-11-11 20:45, 21:44 and 2026-11-12 22:01).
    /// 0.2.34 used the cast end, so the pathfinder rushed to beat a deadline 2.7s too early and walked through Poison
    /// Clouds (22:01 run: 4 cloud hits, 5,118 damage, versus 19 in the 21:44 run with the old far-future activation).
    /// </summary>
    public const double ToxicBreathFirstHitAfterCastEnd = 2.6;

    /// <summary>Below this travel distance a cloud's own facing is used (flipped, see <see cref="CloudHeading"/>).</summary>
    public const float CloudMovedThreshold = 0.3f;

    /// <summary>
    /// Direction a Poison Cloud (0x4CDA) is travelling, in BMR angle convention (atan2(x, z)). Clouds move straight out
    /// from their Fuming Vomit puddle at ~1.7-2y/s, but spawn facing back towards it and only turn round after ~1.1s
    /// (replay 2026-11-12 22:01: spawn rot -0.79, travel +2.36). Use the spawn-to-current vector once the cloud has moved;
    /// before that, the spawn facing reversed.
    /// </summary>
    public static float CloudHeading(float spawnX, float spawnZ, float x, float z, float rotation)
    {
        var dx = x - spawnX;
        var dz = z - spawnZ;
        if (dx * dx + dz * dz >= CloudMovedThreshold * CloudMovedThreshold)
            return MathF.Atan2(dx, dz);
        var flipped = rotation + MathF.PI;
        return flipped > MathF.PI ? flipped - 2 * MathF.PI : flipped;
    }

    /// <summary>
    /// Toxic Mass (0x4CD9) spawns where Wriggling Phlegm was baited, idles ~3.3s, then crawls at 1.0y/s towards the nearest
    /// Toxic Vomit puddle (0x1EB704) and casts Noxious Explosion (48821, range 60, Damage Down 4874 = loses Unbeastable)
    /// when its centre is 5.3y from the puddle centre - 4 of 4 explosions (2026-11-12 22:01 x2 at +21.9s/+30.0s, 22:49
    /// +25.9s, 2026-11-13 08:26 +4.5s). 08:26: the bait locked 6.4y from a puddle, the mass exploded after 4.5s with 1,190
    /// damage taken, the only Damage Down of the run; result 17,550 (Apex) without Unbeastable (1,250).
    /// </summary>
    public const float MassExplodeClearance = 5.3f;
    public const double MassIdleSeconds = 3.3;
    public static double MassSecondsToExplode(float clearance) => MassIdleSeconds + Math.Max(0f, clearance - MassExplodeClearance);

    /// <summary>
    /// Phlegm location locks 5.37-5.41s after the lock-on icon (4 of 4: 23:55:43, 23:57:29, 08:42:10, 08:43:56), i.e.
    /// ~0.3s after BMR's default bait activation (icon + 5.1s).
    /// </summary>
    public const double PhlegmLockAfterBaitActivation = 0.3;
    /// <summary>Planning speed with margin for detours round puddles (observed run speed 7-9y/s).</summary>
    public const float PhlegmReachSpeed = 5f;
    public const double PhlegmReachMargin = 0.8;

    public static float PhlegmReachRadius(double secondsToLock)
        => PhlegmReachSpeed * (float)Math.Max(0d, secondsToLock - PhlegmReachMargin);

    /// <summary>
    /// Goal score for a candidate bait cell: puddle clearance, but cells we cannot reach before the lock rank below every
    /// reachable one (and nearer is better among those). 08:26 run: the old goal (max clearance anywhere) sent the player
    /// from 14.2y clearance towards a 22.4y spot 33y away round the puddles; the lock caught it at 6.4y.
    /// </summary>
    public static float PhlegmBaitScore(float clearance, float travel, float reachRadius)
        => travel <= reachRadius ? clearance : clearance - 100f - (travel - reachRadius);
}
