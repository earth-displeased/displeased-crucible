namespace BeastNav.Services;
public enum LootBoostStep { Ready, Use, Wait, TimedOut }
public sealed class LootBoostPreparationPolicy
{
    private ulong encounter;
    private DateTime deadline;
    private DateTime retryAt;
    private int requests;
    private bool reportedTimeout;
    public LootBoostStep Evaluate(ulong encounterId,DateTime now,bool allowed,bool buffActive,bool owned,bool usable)
    {
        if(encounter!=encounterId) { encounter=encounterId;deadline=default;retryAt=default;requests=0;reportedTimeout=false; }
        if(!allowed || buffActive || !owned) return LootBoostStep.Ready;
        if(deadline==default) deadline=now.AddSeconds(6);
        if(now>=deadline) { if(reportedTimeout) return LootBoostStep.Ready;reportedTimeout=true;return LootBoostStep.TimedOut; }
        if(now<retryAt || !usable || requests>=2) return LootBoostStep.Wait;
        return LootBoostStep.Use;
    }
    public void Requested(DateTime now) { ++requests;retryAt=now.AddSeconds(2); }
}
