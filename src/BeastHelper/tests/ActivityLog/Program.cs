using BeastNav.Services;
var log = new AutomationActivityLog();
if (!log.Record("Combat", "Waiting")) throw new Exception("First change missing");
for (var frame = 0; frame < 10_000; frame++)
    if (log.Record("Combat", "Waiting")) throw new Exception("Repeated frames flooded log");
log.Record("Movement", "Idle");
if (log.Record("Combat", "Waiting")) throw new Exception("Different sources broke deduplication");
for (var i = 0; i < 300; i++) log.Record("Combat", $"Target {i}");
if (log.Entries.Count != 150 || !log.Entries.Last().Contains("Target 299")
    || log.Entries.Any(e => e.Contains("Target 149"))) throw new Exception("Bounded history failed");
log.Clear();
if (log.Entries.Count != 0 || !log.Record("Combat", "Target 299"))
    throw new Exception("Clear did not reset history and deduplication");
Console.WriteLine("Activity log checks passed: repeated frames, independent sources, 150-entry bound, newest preservation, clear/reset.");
