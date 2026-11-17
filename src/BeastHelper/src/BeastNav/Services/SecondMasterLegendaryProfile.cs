using BeastNav.Models;
namespace BeastNav.Services;

/// <summary>
/// Opt-in Second Master's Board (territory 1343) Legendary profile, 2026-11-13. Same trio and item/recovery settings as
/// <see cref="FirstMasterLegendaryProfile"/>, which reached Legendary on First Master (19,779): three familiars give
/// Beast Squad III (4,250) and three Beastkin give Beastkin Gambit (750). No online guide names a Second Master team
/// (Eorzean Tavern, Console Games Wiki, FullBoosts read 2026-11-13). Encounter-specific behaviour (covers, priorities)
/// still needs replay evidence; First Master-only logic (Borgny items, RSR First Master IPC) stays on territory 1342.
/// </summary>
public static class SecondMasterLegendaryProfile
{
    public const uint Territory = 1343;
    public const string Board = "特二盤";

    /// <summary>Second Master combat profile (Legendary trio or XP run): gates RSR/BMR Second Master behaviour and items.</summary>
    public static bool Active(Configuration c) => !c.FirstBoardScoreEnabled && c.CrucibleTeamMode is CrucibleTeamMode.SecondMasterLegendary or CrucibleTeamMode.SecondMasterXpRun;

    /// <summary>
    /// XP run (2026-11-14, after the Standard Legendary 19,004): the same trio fights with the same strategies, and the rest of
    /// the 15-beast roster goes to the lowest-ranked beasts. Beasts brought into a board earn EXP from the score; the board
    /// syncs at rank 25, so no beast is penalised (FullBoosts guide, read 2026-11-14). Loses Beast Squad III (4,250).
    /// </summary>
    public static bool XpRun(Configuration c) => !c.FirstBoardScoreEnabled && c.CrucibleTeamMode == CrucibleTeamMode.SecondMasterXpRun;
    public const int XpRosterCapacity = 15;

    public static void ApplyXpRun(Configuration c)
    {
        Apply(c);
        c.CrucibleTeamMode = CrucibleTeamMode.SecondMasterXpRun;
        c.ClassMinMaxRankScanEnabled = true;
    }

    /// <summary>Either Master profile that fields exactly Cu Sith / Behemoth / Coeurl (or First Master's XP roster).</summary>
    public static bool MasterTrioActive(Configuration c) => FirstMasterLegendaryProfile.Active(c) || Active(c);

    public static void Apply(Configuration c)
    {
        FirstMasterLegendaryProfile.Apply(c);
        c.CrucibleTeamMode = CrucibleTeamMode.SecondMasterLegendary;
        c.SelectedCrucibleBoard = Board;
    }
}
