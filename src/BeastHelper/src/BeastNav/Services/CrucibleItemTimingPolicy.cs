namespace BeastNav.Services;

/// <summary>
/// Item timing / reservation rules (pure logic, covered by tests/FirstBoardScore).
/// Evidence: replays FirstMastersBoard 2026-11-11 20:45 and 21:44.
/// </summary>
public static class CrucibleItemTimingPolicy
{
    public const uint FirstMasterTerritory = 1342;
    public const uint Borgny = 0x4CD8;
    public const uint ToxicMass = 0x4CD9;

    public const uint SecondMasterTerritory = 1343;
    public const uint Durga = 0x4CF6;
    public const uint AtomicRay = 49272;
    /// <summary>
    /// Second Master, Durga Atomic Ray (13s cast, raidwide; BMR module: pet cover does not work). Replay 2026-11-13 16:41:
    /// 4,130 damage killed the player at 16:50:35 (max HP 7,178). 0.2.38 healed below 70% during the cast; replay 17:24:
    /// the cast started at 4,929/6,980 HP (70.6%, no heal) and the hit (4,538) left 391 HP. Use an absolute floor:
    /// during the cast, heal while HP is below the largest recorded hit plus ~15%.
    /// </summary>
    public const float AtomicRayHealBelowHp = 5300f;

    public static float RecoveryThreshold(float normal, bool secondMasterProfile, uint territory, bool atomicRayCasting, uint maxHp)
        => secondMasterProfile && territory == SecondMasterTerritory && atomicRayCasting && maxHp > 0
            ? Math.Max(normal, Math.Min(100f, AtomicRayHealBelowHp * 100f / maxHp)) : normal;

    public const uint Lauda = 0x4D1E;
    /// <summary>
    /// Second Master final boss: potions stay with the player. Replay 2026-11-13 17:24: grade 3 and grade 2 potions went to
    /// Coeurl at 17:43:07 / 17:43:36; the player had no heal left from 17:44:45 and died at 797 HP (17:47:03) with Lauda at
    /// 32.8%.
    /// </summary>
    public static bool FamiliarHealAllowedAtSecondMasterBoss(bool secondMasterProfile, uint territory, bool laudaPresent)
        => !(secondMasterProfile && territory == SecondMasterTerritory && laudaPresent);

    /// <summary>
    /// Crucible Antidote cures poison and heals 25% (Eorzean Tavern items page). Replay 2026-11-13 16:41: the player was
    /// below 55% from 16:49:08 to death at 16:50:35 holding only an Antidote, which the poison-only rule never used.
    /// Second Master profile: spend it as a heal when no other healing item is usable.
    /// </summary>
    public const ushort CrucibleAntidote = 83;
    public static bool AntidoteAsLastResortHeal(bool secondMasterProfile, uint territory)
        => secondMasterProfile && territory == SecondMasterTerritory;

    /// <summary>
    /// The shared item cooldown is 2s, but a Beast potion kit's heal landed 2.5-2.7s after use. At 2s the HP
    /// check still saw the pre-heal value, so a second potion (Gargoyle 21:53:06/08) or a G2 Reraiser as
    /// "emergency mitigation" (Borgny 21:01:06/08) was spent on top of a heal that was already on its way.
    /// </summary>
    public const double PendingHealSeconds = 4.0;
    /// <summary>Below this HP percent a second item is allowed immediately.</summary>
    public const float CriticalHpPercent = 25f;

    public static bool HealStillPending(DateTime now, DateTime pendingUntil, uint hpAtUse, uint currentHp, float hpPercent)
        => now < pendingUntil && currentHp <= hpAtUse && hpPercent >= CriticalHpPercent;

    /// <summary>
    /// First Master profile: familiar potions only from surplus, keeping one recovery item for the player.
    /// Replay 2026-11-12 22:49: three potions went to Coeurl (22:51:13, 22:59:41, 22:59:43); the player ended Treant at
    /// 14% with no healing item and died at Borgny 23:07:28 with none. Counts held duty-inventory slots.
    /// </summary>
    public static bool FamiliarHealAllowed(bool firstMasterProfile, int playerRecoveryItemsHeld)
        => !firstMasterProfile || playerRecoveryItemsHeld >= 2;

    /// <summary>
    /// Self-targeted emergency items that fail BeastHelper's own range/target check are retried without it, at most once
    /// per this interval. Tome of the Impervious (137) was held with native readiness 0 at 20:39 and throughout the 22:49
    /// Borgny fight (HP below 55%, no healing) but was never used in any recorded run.
    /// </summary>
    public const double UncheckedSelfItemRetrySeconds = 20;

    public static bool AllowUncheckedSelfItem(DateTime now, DateTime lastAttempt)
        => lastAttempt == default || now >= lastAttempt.AddSeconds(UncheckedSelfItemRetrySeconds);

    public const ushort G2AntipoisonSerum = 87;

    /// <summary>
    /// G2 Antipoison Soul Serum: 300s of 100% poison-damage resistance (user report, 22:01 run 2026-11-12, used by
    /// hand just after Borgny's pull). Borgny fights ran 182s (22:01) and 184s until death (21:44), so one use at the pull covers
    /// the fight. First Master profile only; once per Borgny encounter (id = Borgny's object id).
    /// </summary>
    public static bool UseBorgnyAntipoison(bool firstMasterProfile, uint territory, ulong borgnyId, ulong usedForBorgnyId)
        => firstMasterProfile && territory == FirstMasterTerritory && borgnyId != 0 && borgnyId != usedForBorgnyId;

    /// <summary>First Master profile: the Crimson Ribbon healing path must not spend the G2 Antipoison serum before Borgny.</summary>
    public static bool ReserveForBorgny(ushort item, bool firstMasterProfile, uint territory, bool borgnyPresent)
        => item == G2AntipoisonSerum && firstMasterProfile && territory == FirstMasterTerritory && !borgnyPresent;

    /// <summary>
    /// First Master profile: keep an attack item (fangs hit for 2.7k-7.6k) for Borgny's Toxic Mass, which
    /// otherwise explodes (best recorded attempt was 3.2k HP short). Before Borgny only spend items beyond one
    /// held slot; during Borgny only on the mass. Other boards and profiles are unchanged.
    /// </summary>
    public static bool AllowOffensive(bool firstMasterProfile, uint territory, bool borgnyPresent, uint targetOid, int offensiveSlotsHeld)
    {
        if (!firstMasterProfile || territory != FirstMasterTerritory) return true;
        if (targetOid == ToxicMass) return true;
        if (borgnyPresent || targetOid == Borgny) return false;
        return offensiveSlotsHeld >= 2;
    }
}
