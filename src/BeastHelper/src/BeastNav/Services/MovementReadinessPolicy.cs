namespace BeastNav.Services;
public static class MovementReadinessPolicy
{
    public static string ScoreSetupWarning(int rsrCopies, int bmrCopies, bool inArena,
        bool bmrAvailable, bool aiOff, bool ownsPreset, bool inCombat, bool hasModule)
    {
        if (rsrCopies != 1) return $"Score mode paused: {rsrCopies} RSR copies loaded. Enable only RSR - First Board Assist.";
        if (bmrCopies != 1) return $"Score mode paused: {bmrCopies} BMR copies loaded. Enable only Crucible Assist 4.";
        if (!inArena) return string.Empty;
        if (!bmrAvailable) return "Score mode paused: BMR is loaded but its movement connection is unavailable.";
        if (!aiOff) return "Score mode paused: turn BMR AI Off (/bmrai off).";
        if (!ownsPreset) return "Score mode paused: waiting for BeastHelper Dodge movement preset.";
        if (inCombat && !hasModule) return "Score mode paused: no encounter module is active; dodge coverage is unavailable.";
        return string.Empty;
    }

    public static bool RestoreOwnPreset(bool previouslyOwned, bool empty, bool bmrAvailable, bool alive)
        => previouslyOwned && empty && bmrAvailable && alive;
    public static string Warning(bool aiEnabled, bool ownsPreset, bool hasModule)
        => aiEnabled ? "BMR AI mode is On and overrides BeastHelper movement. Use /bmrai off for this setup."
        : !ownsPreset ? "Movement preset ownership lost; use Refresh BMR movement setup."
        : !hasModule ? "No active encounter module: automatic mechanic dodges are not available yet."
        : string.Empty;
}
