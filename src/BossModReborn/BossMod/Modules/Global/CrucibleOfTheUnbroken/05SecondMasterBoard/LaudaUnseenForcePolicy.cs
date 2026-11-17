namespace BossMod.Global.CrucibleOfTheUnbroken;

/// <summary>
/// Lauda the Spellcleaver, Unseen Force (49484): Lauda teleports onto the player, a dagger hit applies status 5341, and when
/// it expires Shockwave (49489) throws the player along their facing (reversed when Lauda faced the player at the status
/// gain). Replay 2026-11-13 16:41 (SecondMastersBoard_..._16_41_17): status 17:01:57.19 (Lauda rot -2.22, player 0.92 =
/// opposed), Shockwave 17:02:09.29 (+12.1s), player facing 0.55, thrown along -2.59 = facing + 180 degrees, from
/// (516.6,-425.4) to (498.0,-456.9) - off the platform; Burns (3065/3066) then took 4,458 HP in 12s and killed the player.
/// Icy Veins: the throw reaches "from the north or south pocket to the opposite one". The platform is 20 wide and 40 long
/// (plus 5y pockets), so the only safe throw starts in an end pocket and runs along the long axis.
/// Kept free of BMR types so tests/FirstBoardScore can check it.
/// </summary>
public static class LaudaUnseenForcePolicy
{
    /// <summary>Pocket centre to opposite pocket centre (Icy Veins); module default was 40, observed flight >= 36.6y.</summary>
    public const float KnockbackDistance = 45f;
    public const float CenterX = 520f;
    public const float NorthPocketZ = -397.5f;
    public const float SouthPocketZ = -442.5f;
    /// <summary>Goal weight for reaching the pocket; well above the melee approach goal, below forbidden zones.</summary>
    public const float PocketGoalWeight = 100f;
    public const float PocketGoalRadius = 1.5f;
    /// <summary>Seconds before the throw during which RSR must not act (actions turn the character).</summary>
    public const double FacingHoldSeconds = 1.5;

    /// <summary>Nearer end pocket (z) for a player at <paramref name="playerZ"/>.</summary>
    public static float PocketZ(float playerZ)
        => MathF.Abs(playerZ - NorthPocketZ) <= MathF.Abs(playerZ - SouthPocketZ) ? NorthPocketZ : SouthPocketZ;

    // 7.5.7.540 also chose the start pocket to keep the landing out of Gluttonous Gutting's rect; removed in 541:
    // Gutting hits Lauda's current target wherever it stands (replays 22:39 / 23:11 / 17:24), so the pocket is irrelevant.
    // RSR covers it with Snarl instead (SecondMasterDefensePolicy).

    /// <summary>Throw direction (BMR angle, atan2(x, z)) for a facing; opposed = Lauda faced the player at the status gain.</summary>
    public static float ThrowDirection(float facing, bool opposed)
    {
        var d = opposed ? facing + MathF.PI : facing;
        while (d > MathF.PI) d -= 2 * MathF.PI;
        while (d <= -MathF.PI) d += 2 * MathF.PI;
        return d;
    }

    /// <summary>Facing to hold in a pocket so the throw runs to the opposite pocket.</summary>
    public static float SafeFacing(float pocketZ, bool opposed)
    {
        var towardOpposite = pocketZ == SouthPocketZ ? 0f : MathF.PI; // +z from the south pocket, -z from the north
        return ThrowDirection(towardOpposite, opposed); // reversing twice is the identity
    }
}
