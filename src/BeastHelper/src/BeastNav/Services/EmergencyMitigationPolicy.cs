namespace BeastNav.Services;

/// <summary>General-purpose defenses without self-inflicted ailments or encounter resets.</summary>
public static class EmergencyMitigationPolicy
{
    public static ushort[] Candidates(bool enabled, bool needsRecovery, bool healingUnavailable,
        IReadOnlySet<uint> statuses, IReadOnlySet<string> names, bool familiar = false)
    {
        if (!enabled || !needsRecovery || !healingUnavailable) return [];
        List<ushort> result = [];
        if (!statuses.Contains(4857)) result.Add(102); // Tough Skin: 20% reduction.
        if (!statuses.Contains(4849)) result.Add(137); // Blink: five physical hits.
        if (!names.Contains("Reflect")) result.Add(136);
        if (!statuses.Contains(4862)) result.Add(110); // Tempered Evasion; no Blind penalty.
        if (!statuses.Contains(4863)) result.Add(112); // Tempered Constitution plus 10% heal.
        if (!familiar && !names.Contains("Spirit of the Breathtaker")) result.Add(114);
        if(!familiar && !statuses.Contains(4846)) { result.Add(99);result.Add(98); } // Last-resort Reraise.
        return result.ToArray();
    }
}
