namespace BossMod.Global.CrucibleOfTheUnbroken;

/// <summary>
/// Second Master's Board, Gigantis Piece: big slimes (Congealed Lightning 0x4D05 / Congealed Kindling 0x4D07, 16,181 HP).
/// Smashing Stamp, baited onto the slime of the opposite element to Gigantis' weapon (status 2056 extra 0x499 lightning ->
/// stamp the Kindling, 0x49A fire -> stamp the Lightning), kills one slime. The other must be killed by damage before its
/// Rupture (7s cast, range 40 raidwide) lands, or the player gets Damage Down (4874) and loses Unbeastable (1,250).
/// Replays 2026-11-13 17:24 (SecondMastersBoard_BST50_Ta_Be_..._17_24_08) and 18:04 (..._18_04_43) show the same failure:
/// the pair spawned (17:39:58 / 18:23:34), the stamp killed one, and RSR stayed on Gigantis the whole time; the spare
/// slime (never damaged) cast Rupture 17:40:13 / 18:23:49 and hit the player 7s later (Damage Down 17:40:20 / 18:23:57).
/// The spare slime had ~21s from spawn; a lone slime took 10k damage in 5s (18:23:03-08). Same split as the module's
/// SlimePriority, kept free of BMR types so RSR and tests/FirstBoardScore can use it.
/// </summary>
public static class SecondMasterTargetPolicy
{
    public const uint CongealedLightning = 0x4D05;
    public const uint CongealedKindling = 0x4D07;
    public const uint WeaponElementStatus = 2056;
    public const ushort LightningWeapon = 0x499;
    public const ushort FireWeapon = 0x49A;
    public const uint RuptureLightning = 49373;
    public const uint RuptureFire = 49375;

    public const int RupturePriority = 5;
    public const int SpareSlimePriority = 3;

    /// <summary>
    /// Small slimes (0x4D06 / 0x4D08, 646 HP) self-destruct ~36s after spawning (49374 / 49376, 7s cast) and each hit adds a
    /// Damage Down stack. Replay 2026-11-13 22:39 (..._22_39_56): wave-1 Kindling smalls spawned 22:54:05, were never
    /// attacked, cast 22:54:41 and gave 8 stacks at 22:54:48; Lightning smalls 22:54:39 -> cast 22:55:14 -> 4 stacks
    /// 22:55:22. Replay 18:04: one 44893 AoE (806-1,384 per small) killed all eight at 18:23:45.
    /// </summary>
    public const uint CongealedLightningSmall = 0x4D06;
    public const uint CongealedKindlingSmall = 0x4D08;
    public const uint SmallBurstLightning = 49374;
    public const uint SmallBurstFire = 49376;
    public const int SmallBurstingPriority = 4;
    public const int SmallSlimePriority = 2;

    /// <summary>
    /// Lauda's Magicked / Indefatigable Blades (LaudaTargetPolicy priority 3). RSR must know them now that the Lauda module
    /// no longer forces targets for BST (replay 2026-11-13 23:11, blades spawned 23:32:07, 11,002 HP each).
    /// </summary>
    public const uint MagickedBlade = 0x4EB8;
    public const uint IndefatigableBlade = 0x4D22;
    public const int BladePriority = 3;
    public static bool Blade(uint oid) => oid is MagickedBlade or IndefatigableBlade;

    /// <summary>
    /// Durga's Spinner-rook Pieces (0x4CF8, 8,284 HP, cast Aether Charge on Durga every ~4s). RSR now picks them (the Durga
    /// module no longer forces a rook for BST): keep the current rook until it dies, then the NEAREST one, not the lowest
    /// HP one. Replay 2026-11-14 11:26 (..._11_26_30): the player left rooks at 1,625 / 1,172 / 279 HP for rooks 27y away
    /// and backtracked (11:34:17.9, 11:34:24.0, 11:34:45.1).
    /// </summary>
    public const uint SpinnerRook = 0x4CF8;
    public const int SpinnerRookPriority = 3;
    public static bool Rook(uint oid) => oid == SpinnerRook;
    /// <summary>After the current target, rank by distance before HP (walking costs more than the HP difference).</summary>
    public static bool PreferNearest(uint oid) => Rook(oid);

    /// <summary>Encounter actors this policy ranks (gates RSR's use of it).</summary>
    public static bool Tracked(uint oid) => Slime(oid) || Blade(oid) || Rook(oid);

    public static bool BigSlime(uint oid) => oid is CongealedLightning or CongealedKindling;
    public static bool SmallSlime(uint oid) => oid is CongealedLightningSmall or CongealedKindlingSmall;
    public static bool Slime(uint oid) => BigSlime(oid) || SmallSlime(oid);

    /// <summary>Big slime Rupture or small slime self-destruct.</summary>
    public static bool Rupture(uint castActionId) => castActionId is RuptureLightning or RuptureFire or SmallBurstLightning or SmallBurstFire;

    /// <summary>The big slime Smashing Stamp should be baited onto for Gigantis' current weapon element (0 = any).</summary>
    public static uint StampSlime(ushort weaponExtra) => weaponExtra switch
    {
        LightningWeapon => CongealedKindling,
        FireWeapon => CongealedLightning,
        _ => 0,
    };

    /// <summary>
    /// Action-target priority (0 = no opinion, normal targeting). A slime casting Rupture first; with two big slimes alive,
    /// the one the stamp will not hit; a lone big slime always.
    /// </summary>
    public static int Priority(uint oid, bool castingRupture, ushort weaponExtra, int bigSlimesAlive)
    {
        if (Blade(oid))
        {
            return BladePriority;
        }
        if (Rook(oid))
        {
            return SpinnerRookPriority;
        }
        if (SmallSlime(oid))
        {
            return castingRupture ? SmallBurstingPriority : SmallSlimePriority;
        }
        if (!BigSlime(oid))
        {
            return 0;
        }
        if (castingRupture)
        {
            return RupturePriority;
        }
        return bigSlimesAlive >= 2 && oid == StampSlime(weaponExtra) ? 0 : SpareSlimePriority;
    }
}
