namespace RotationSolver.Basic.Helpers;

/// <summary>
/// Second Master's Board (territory 1343) Snarl cover casts. Evidence: replay 2026-11-13 16:41
/// (SecondMastersBoard_BST50_Ta_Be_2026_11_13_16_41_17), first attempt, no cover logic active.
/// </summary>
internal static class SecondMasterDefensePolicy
{
    public static bool CoverCast(uint oid, uint cast) => (oid, cast) switch
    {
        // Flauros Erratic Blaster: boss cast 49188 (6.0+1.0s), helper 49189 hits the player single-target (7.0s,
        // tankbuster + Paralysis): 1,971 at 16:43. Single-target hits were redirected by Snarl on First Master (Sweeping
        // Evisceration, Toxic Vomit, Salivous Snap). Default 1.5s hold covers the hit at boss cast end +1.0s.
        (0x4CDB, 49188) => true,
        // Lauda Gluttonous Gutting: boss cast 49445 (8.0+0.6s), helper 49446 hits LAUDA'S TARGET wherever it stands, at boss
        // cast end +0.6s, and adds Vulnerability Up (loses Unbeastable). Replays 2026-11-13: 22:39 Lauda on the player ->
        // player hit 1,722 + Vuln Up (Snarl cover 2413 up 23:01:48.5 but taken back 23:01:49.5, hit 23:01:55.2);
        // 23:11 and 17:24 Lauda on Behemoth -> Behemoth hit 2,915 / 3,186, player untouched. Default 1.5s hold covers +0.6s.
        (0x4D1E, 49445) => true,
        // Lauda Gluttonous Goring (boss 49448 -> 49449, the other Gutting variant): replay 2026-11-13 23:42 Lauda on the
        // player -> 1,587 + Vulnerability Up 00:03:22. Lauda Thunderbolt (49470, 5.0s, rect bait on Lauda's target, BMR
        // "tankbuster"): 3 hits 2,352 to the player in the same run (Lauda on the player 144 of ~146s).
        (0x4D1E, 49448) => true,
        (0x4D1E, 49470) => true,
        // Not covered: Durga Atomic Ray (49272) - BMR module: "Raidwide - Pet cover does not work"; BeastHelper pre-heals.
        _ => false,
    };

    /// <summary>
    /// Lauda: whoever Lauda targets takes her autos, Thunderbolt and Gutting/Goring, so a familiar should hold her whenever it
    /// has HP. Replay 2026-11-13 23:42 (..._23_42_47): with the First Master rules (take back when player > familiar + 20%)
    /// Snarl cover 00:01:39.5 was taken back at 00:01:40.8 ("player 77%, Coeurl 54%"); Lauda targeted the player ~144s of
    /// ~146s and the player took 5,916 autos + 2,352 Thunderbolt + 1,587 Goring, then died at 251s (Lauda 39%).
    /// In 23:11 / 17:24 Lauda on Behemoth took Gutting (2,915 / 3,186) with the player untouched.
    /// </summary>
    public const float LaudaCoverMinPet = 0.35f;
    public const float LaudaTakeBackPet = 0.25f;
    public const float LaudaTakeBackMinPlayer = 0.5f;
    public static bool LaudaCover(float player, float pet) => pet >= LaudaCoverMinPet && (player <= 0.9f || pet >= player);
    public static bool LaudaTakeBack(float player, float pet) => player > LaudaTakeBackMinPlayer && pet <= LaudaTakeBackPet;
}
