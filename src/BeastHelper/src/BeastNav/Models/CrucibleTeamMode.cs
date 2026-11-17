namespace BeastNav.Models;

public enum CrucibleTeamMode
{
    /// <summary>Auto-picks the lowest-ranked owned beasts first, so the whole roster levels up toward the rank cap over repeated runs.</summary>
    Leveling,

    /// <summary>Uses exactly the beasts the user checked off in 魔獣リスト (Configuration.CrucibleManualTeamPetRowIds).</summary>
    Manual,

    /// <summary>
    /// Auto-picks the most stable owned beasts (highest known level first, then
    /// static tier, then stats) so the farming team survives rather than
    /// chasing rank-up XP. See <see cref="Services.CrucibleTeamPlanner"/>.
    /// Degrades to a tier-only ordering when no level data has been read yet.
    /// </summary>
    Recommended,

    /// <summary>Exactly three beasts of one classification with the lowest combined synced rank.</summary>
    ClassMinMax,

    /// <summary>Cu Sith, Buffalo and Coeurl plus lowest-ranked owned beasts up to Third Board capacity.</summary>
    ThirdBoardXpRun,

    /// <summary>Exactly Cu Sith, Behemoth and Coeurl for an initial First Master Legendary attempt.</summary>
    FirstMasterLegendary,

    /// <summary>Cu Sith, Behemoth and Coeurl plus the nine lowest-ranked other beasts.</summary>
    FirstMasterXpRun,

    /// <summary>Exactly Cu Sith, Behemoth and Coeurl for Second Master's Board, First Degree, Legendary.</summary>
    SecondMasterLegendary,

    /// <summary>Second Master's Board: Cu Sith, Behemoth and Coeurl fight (same strategies as SecondMasterLegendary) plus
    /// the twelve lowest-ranked other beasts up to the board's 15-beast roster, for beast XP.</summary>
    SecondMasterXpRun,
}
