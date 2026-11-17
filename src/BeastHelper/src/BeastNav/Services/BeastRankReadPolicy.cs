namespace BeastNav.Services;

/// <summary>
/// Decides what rank to save from the game's whistle-screen rank query
/// (AgentXBMPetParty.GetSyncedRank / IsPetOverSyncedRank).
/// </summary>
/// <remarks>
/// Added 2026-11-14: the bestiary/UI text readers left 48 of 50 beasts unknown (saved Level 0), so
/// XP rosters kept picking the same "unknown" beasts run after run (dalamud.log 16:00-16:14, ten
/// Second Master entries, config still only 2 known ranks). The synced rank equals the real rank
/// unless the beast is above the board's rank cap; then only "higher than the cap" is known.
/// </remarks>
public static class BeastRankReadPolicy
{
    /// <returns>The rank to save, or null to leave the saved value unchanged.</returns>
    public static int? Resolve(uint syncedRank, bool overSynced, int? existing)
    {
        if (syncedRank is < 1 or > 25) return null;
        var synced = (int)syncedRank;
        if (!overSynced) return synced;
        // Real rank is above the cap. Keep a saved rank that agrees; otherwise save cap + 1,
        // a lower bound that still ranks the beast after every beast at or below the cap.
        if (existing is { } known && known > synced && known <= 25) return null;
        return Math.Min(25, synced + 1);
    }
}
