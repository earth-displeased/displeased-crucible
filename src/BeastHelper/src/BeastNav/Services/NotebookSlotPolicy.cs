namespace BeastNav.Services;

/// <summary>
/// Where a beast sits in the bestiary list (XBMMonsterNotebook, 25 entries per page).
/// </summary>
/// <remarks>
/// 2026-11-15 23:09-23:11 (dalamud.log): with the bestiary sorted/filtered, the old "page/slot from the beast's
/// number" guess clicked the wrong beast - "spriggan adding" re-added slime 60 times and the team stayed at 4/12.
/// The game's own list (AgentXBMMonsterNotebook.Entries) is used instead.
/// </remarks>
internal static class NotebookSlotPolicy
{
    public const int EntriesPerPage = 25;

    /// <summary>Page and slot for <paramref name="petId"/>; null when the list is loaded but the beast is not in it (filtered out).</summary>
    public static (int Page, int Slot)? Locate(IReadOnlyList<uint> displayOrder, uint petId)
    {
        if (displayOrder.Count == 0)
            return ((int)((petId - 1) / EntriesPerPage), (int)((petId - 1) % EntriesPerPage));
        var index = -1;
        for (var i = 0; i < displayOrder.Count; i++)
            if (displayOrder[i] == petId) { index = i; break; }
        return index < 0 ? null : (index / EntriesPerPage, index % EntriesPerPage);
    }
}