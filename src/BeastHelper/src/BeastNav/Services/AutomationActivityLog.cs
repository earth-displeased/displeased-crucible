namespace BeastNav.Services;

/// <summary>Bounded, change-only activity history; never grows with frame count.</summary>
internal sealed class AutomationActivityLog
{
    private readonly Queue<string> entries = new();
    private readonly Dictionary<string, string> lastBySource = new();
    public IReadOnlyCollection<string> Entries => this.entries;

    public bool Record(string source, string message)
    {
        if (this.lastBySource.TryGetValue(source, out var last) && last == message)
            return false;
        this.lastBySource[source] = message;
        this.entries.Enqueue($"{DateTime.Now:HH:mm:ss} [{source}] {message}");
        while (this.entries.Count > 150) this.entries.Dequeue();
        return true;
    }

    public void Clear()
    {
        this.entries.Clear();
        this.lastBySource.Clear();
    }
}
