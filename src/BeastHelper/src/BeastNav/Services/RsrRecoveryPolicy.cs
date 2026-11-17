namespace BeastNav.Services;

internal sealed class RsrRecoveryPolicy
{
    private DateTime? inactiveSince;
    public int Attempts { get; private set; }
    public void Reset() { this.inactiveSince = null; this.Attempts = 0; }
    public bool ShouldRetry(DateTime now, bool eligible, bool reportedActive)
    {
        if (!eligible || reportedActive) { this.inactiveSince = null; return false; }
        this.inactiveSince ??= now;
        if (this.Attempts >= 3 || now - this.inactiveSince.Value < TimeSpan.FromSeconds(5)) return false;
        this.inactiveSince = now;
        this.Attempts++;
        return true;
    }
}
