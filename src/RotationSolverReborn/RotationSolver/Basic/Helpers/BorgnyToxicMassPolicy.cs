namespace RotationSolver.Basic.Helpers;

/// <summary>
/// Borgny the Venomous: Toxic Mass (0x4CD9) explodes ~20-25s after it spawns (Noxious Explosion, arena-wide
/// damage plus a Toxicosis stack). Replays 2026-11-11 20:45 and 21:44: 6 of 6 masses exploded, the best one at
/// 3,204/13,420 HP. Every spawn followed Wriggling Phlegm (48817, 7.9s cast) by ~9.7s, and the mass took no damage
/// for its first ~5s. Hold beast summons / Tempered Release / Borrow / Parting Blow from the Phlegm cast until the
/// mass is the current target, so the summon opener and release land on the mass instead of the boss.
/// </summary>
internal sealed class BorgnyToxicMassPolicy
{
    public const uint Borgny = 0x4CD8;
    public const uint ToxicMass = 0x4CD9;
    public const uint WrigglingPhlegm = 48817;
    /// <summary>Phlegm cast start -> spawn (~9.7s) -> attackable (~5s), plus margin. Bounded so the rotation never stalls.</summary>
    public const double ReserveSeconds = 17;
    /// <summary>Upper bound on waiting for the player to target a living mass.</summary>
    public const double MassWaitSeconds = 12;

    public readonly record struct Decision(bool Reserve, bool Burst);

    /// <summary>
    /// The mass counts as targeted only when the familiar is on it too: a release makes the familiar burst its own
    /// current target. Replay 2026-11-13 15:46: player targeted the mass at 16:01:32.449, release 47093 went out at .482
    /// while Cu Sith was still on Borgny, its 44935 burst (1,889) hit Borgny, Cu Sith only switched at 16:01:34.4; the mass
    /// exploded at +24.5s with 4,991 HP left (Damage Down, Unbeastable lost, final score 18,478 vs 18,750).
    /// </summary>
    public static bool MassTargeted(bool playerTargetsMass, bool familiarOut, bool familiarTargetsMass)
        => playerTargetsMass && (!familiarOut || familiarTargetsMass);

    private ulong encounter;
    private DateTime reserveUntil, massSeenAt;

    public Decision Update(ulong borgny, DateTime now, bool phlegmCasting, bool massAlive, bool massTargeted, bool emergency)
    {
        if (borgny != encounter || borgny == 0)
        {
            encounter = borgny; reserveUntil = massSeenAt = default;
        }
        if (borgny == 0) return default;
        if (phlegmCasting && now >= reserveUntil) reserveUntil = now.AddSeconds(ReserveSeconds);
        if (!massAlive) massSeenAt = default;
        else if (massSeenAt == default) massSeenAt = now;
        if (massTargeted)
        {
            reserveUntil = default;
            return new(false, true);
        }
        if (emergency) return default;
        var waitingForMass = massAlive && (now - massSeenAt).TotalSeconds < MassWaitSeconds;
        return new(now < reserveUntil || waitingForMass, false);
    }
}
