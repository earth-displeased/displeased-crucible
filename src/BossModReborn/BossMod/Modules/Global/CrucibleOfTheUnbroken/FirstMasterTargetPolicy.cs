namespace BossMod.Global.CrucibleOfTheUnbroken;

/// <summary>Treant add priority shared by movement and action targeting.</summary>
public static class FirstMasterTargetPolicy
{
    public static int CorpseFlowerPriority(uint oid) => oid switch {0x4CBF => 10,0x4CBD => 1,_ => 0};
    public static int Priority(uint oid,bool castingNaturalNurture=false)
        => CorpseFlowerPriority(oid) is >0 and var p ? p : TreantPriority(oid,castingNaturalNurture);
    /// <summary>
    /// Treant finish: at or below this HP ratio Treant outranks every add (adds despawn when Treant dies).
    /// Replay 2026-11-12 22:01: a Biloko spawned with Treant at 1,996/96,115 HP (2.1%); BMR switched to it within 60ms
    /// and walked 15y away, so the user had to finish Treant by hand. Natural Nurture (9s cast) first lands ~10.9s
    /// after a Biloko spawns and heals 4,805 (replays 2026-11-11 21:44, 2026-11-12 22:01).
    /// Raised 10% -> 20% after replay 2026-11-12 22:49: Biloko spawned at 17.9% (17,187), was killed first in 23s while two
    /// heals landed; Treant died 46s after the spawn. A healing Biloko costs ~437/s (4,805 per ~11s) of ~1,100/s, so staying
    /// on Treant would take ~26s. 21:44 (26.6%): Biloko first, 46s total vs ~39s staying. 20:45 (14.7%): Biloko died in
    /// 7.7s, 25s total vs ~21s staying. Break-even is ~13% with a 7.7s Biloko kill and ~40% with the usual 23s.
    /// </summary>
    public const float TreantFinishHpRatio = 0.20f;
    public const int TreantFinishPriority = 9;

    public static bool TreantFinishing(float treantHpRatio) => treantHpRatio > 0f && treantHpRatio <= TreantFinishHpRatio;

    public static int TreantPriority(uint oid, bool castingNaturalNurture, float treantHpRatio)
        => oid == 0x4CCD && TreantFinishing(treantHpRatio) ? TreantFinishPriority : TreantPriority(oid, castingNaturalNurture);

    public static int TreantPriority(uint oid, bool castingNaturalNurture = false) => oid switch
    {
        0x4CD1 when castingNaturalNurture => 7,
        0x4CCF => 8,
        0x4CD1 => 6,
        0x4CD0 => 4,
        0x4CCE => 3,
        0x4CCD => 1,
        _ => 0,
    };
}
