namespace BeastNav.Services;
public static class RosterEditPolicy
{
    public static bool MayResetFilters(bool missingBeast, bool alreadyAttempted) => missingBeast && !alreadyAttempted;
    public static bool IsComplete(IReadOnlyList<uint> team, IReadOnlyList<uint> goal)
        => goal.Count > 0 && team.Count == goal.Count && team.Distinct().Count() == team.Count && goal.All(team.Contains);
}
