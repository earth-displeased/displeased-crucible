namespace RotationSolver.Basic.Helpers;

/// <summary>Wait for a nearby add wave, without starving the rotation indefinitely.</summary>
internal sealed class TreantMeteorPolicy
{
    private ulong encounter;
    private DateTime waitingSince, waveSeen, summonedAt, executionHold;
    private bool lastActive, lastNature, summonAllowed;
    public readonly record struct Decision(bool Summon, bool Release, bool HoldDismiss);
    public Decision Update(ulong boss, DateTime now, bool hornReady, bool active, bool nature, int slugs, int nearby, bool urgent)
    {
        if (boss != encounter || boss == 0)
        {
            encounter = boss; waitingSince = waveSeen = summonedAt = executionHold = default;
            lastActive = lastNature = summonAllowed = false;
        }
        if (boss == 0) return default;
        if (slugs > 0 && waveSeen == default) waveSeen = now;
        if (slugs == 0) waveSeen = default;
        var gathered = nearby >= 3 || nearby > 0 && waveSeen != default && (now-waveSeen).TotalSeconds >= 5;
        if (!active)
        {
            if (!hornReady) { waitingSince = default; summonAllowed = false; }
            else
            {
                if (waitingSince == default) waitingSince = now;
                summonAllowed |= gathered || urgent || (now-waitingSince).TotalSeconds >= 30;
            }
        }
        else
        {
            if (!lastActive) summonedAt = now;
            if (lastActive && lastNature && !nature) executionHold = now.AddSeconds(4);
            waitingSince = default; summonAllowed = false;
        }
        var release = active && nature && (gathered || urgent || (now-summonedAt).TotalSeconds >= 6);
        lastActive = active; lastNature = nature;
        return new(hornReady && !active && summonAllowed, release, active && now < executionHold);
    }
}
