namespace BeastNav.Services;

public static class WhistleSelectionPolicy
{
    public static bool MayRetainPartial(bool completionMode, IReadOnlyList<uint> selected, IReadOnlyList<uint> expected)
        => completionMode && expected.Count==3 && expected.Distinct().Count()==3 && selected.Count is >0 and <3
            && selected.Distinct().Count()==selected.Count && selected.All(expected.Contains);

    public static bool SameTrio(IReadOnlyList<uint> current, IReadOnlyList<uint> desired)
        => current.Count == 3 && desired.Count == 3 && Valid(current) && Valid(desired) && current.Order().SequenceEqual(desired.Order());
    public static uint[]? ResolveIndices(IReadOnlyList<uint> team, IReadOnlyList<uint> indices)
    {
        if (indices.Count > 3 || indices.Distinct().Count() != indices.Count || indices.Any(i => i >= team.Count)) return null;
        return indices.Select(i => team[(int)i]).ToArray();
    }

    public static bool Ready(IReadOnlyList<uint> current, IReadOnlyList<uint> desired)
        => Valid(current) && Valid(desired) && current.SequenceEqual(desired);
    public static bool Valid(IReadOnlyList<uint> values)
        => values.Count <= 3 && values.All(v => v is >= 1 and <= 50) && values.Distinct().Count() == values.Count;

    // Toggle only an observed mismatch. Remove the wrong suffix from its end, then append in slot order.
    public static uint NextToggle(IReadOnlyList<uint> current, IReadOnlyList<uint> desired)
    {
        if (!Valid(current) || !Valid(desired) || desired.Count == 0 || Ready(current, desired)) return 0;
        var prefix = 0;
        while (prefix < current.Count && prefix < desired.Count && current[prefix] == desired[prefix]) prefix++;
        return current.Count > prefix ? current[^1] : desired[prefix];
    }
    public static uint[] AfterToggle(IReadOnlyList<uint> current, uint id)
        => current.Contains(id) ? current.Where(v => v != id).ToArray() : [..current, id];
}
