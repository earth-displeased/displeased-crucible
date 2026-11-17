namespace RotationSolver.Basic.Helpers;
internal static class FirstMasterDefensePolicy
{
    public static bool CoverCast(uint oid,uint cast) => (oid,cast) switch
    {
        (0x4CCD,50518) => true, // Acorn Bomb: Nightmare prevents the following dodge.
        (0x4CBF,48689) => true, // Final Sting fallback when killing/stunning is too late.
        (0x4CC2,48730) => true, // Grim Fate.
        // Replays 2026-11-11 20:45/21:44, 2026-11-12 22:01/22:49: whenever Cover (status 2413) was up, these single-target
        // hits landed on the familiar instead of the player (Sweeping 3/3, Toxic Vomit 3/3 hits, Salivous Snap 1/1).
        (0x4CC2,48717) => true, // Sweeping Evisceration: 50933 hits player 689-1,443 at cast start +8.9-9.0s (cast 7.6s).
        (0x4CD8,48809) => true, // Toxic Vomit: 4 hits of 439-816 at cast start +3.5/+6.6/+9.7/+12.8s (cast 3.2s).
        (0x4CD8,48822) => true, // Salivous Snap: 733-1,610 at cast start +7.0s (cast 6.7s).
        // 10 replays 2026-11-11 20:45 .. 2026-11-13 14:57 (pet hit while player had 2413 / player hit):
        (0x4CB6,48669) => true, // Strix On the Properties of Darkness: 9/9 covered to Coeurl (1,611-1,773); 14:57 uncovered
                                // (no familiar out) hit the player for 1,940. Lands at cast end (8.0s).
        (0x4CC2,50695) => true, // Gargoyle Fivefold Fallout: hits +3.0/+4.7/+6.3/+7.9s, then 48724 -> knockback hit +12.0s.
                                // 12 covered hits to the pet; player took 13,518 over 70 hits uncovered.
        (0x4CC2,48724) => true, // Fivefold Fallout knockback cast (2.5s), hit +0.6s after it ends.
        (0x4CD8,48815) => true, // Borgny Touchdown (4.0s): knockback 48816 hits at cast end, 1,096-1,456 to the player
                                // (13 hits, 14,087); 1 covered hit went to the pet (1,273).
        _ => false,
    };
    /// <summary>How long Cover is kept after the cast ends: through the last measured hit plus margin.</summary>
    public static float AfterCastHold(uint oid,uint cast) => (oid,cast) switch
    {
        (0x4CC2,48730) => 5f,
        (0x4CC2,48717) => 2.5f,  // last hit +1.4s after cast end
        (0x4CD8,48809) => 10.5f, // last of four hits +9.7s after cast end
        (0x4CC2,50695) => 10f,   // cast 2.5s; last hit (knockback) +9.5s after cast end; inside Snarl's 15s
        _ => 1.5f,
    };
    public static bool CanCoverCast(float player,float pet) => pet>=.3f || player<=.3f;
    public static bool Cover(float player,float pet) => player<=.3f || player<=.75f && pet>=.45f && pet>=player+.1f;
    public static bool TakeBack(float player,float pet) => player>.4f && (pet<=.3f || player>pet+.2f);
}
