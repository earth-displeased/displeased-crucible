using BeastNav.Services;
try
{
static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
var clock = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);
var p = new RsrRecoveryPolicy();
Check(!p.ShouldRetry(clock,true,false), "Recovery should wait");
Check(!p.ShouldRetry(clock.AddSeconds(4),true,false), "Retried before 5s");
Check(p.ShouldRetry(clock.AddSeconds(5),true,false), "Missing first retry");
Check(!p.ShouldRetry(clock.AddSeconds(6),true,false), "Retry spam");
Check(p.ShouldRetry(clock.AddSeconds(10),true,false), "Missing second retry");
Check(p.ShouldRetry(clock.AddSeconds(15),true,false), "Missing third retry");
Check(!p.ShouldRetry(clock.AddHours(1),true,false), "Retry limit exceeded");
p.Reset();
Check(!p.ShouldRetry(clock,true,false), "No wait after reset");
Check(!p.ShouldRetry(clock.AddSeconds(4),true,true), "Healthy RSR retried");
Check(!p.ShouldRetry(clock.AddSeconds(6),true,false), "Active RSR failed to reset delay");
Check(p.ShouldRetry(clock.AddSeconds(11),true,false), "Recovery did not resume");
p.Reset();
p.ShouldRetry(clock,true,false);
Check(!p.ShouldRetry(clock.AddSeconds(10),false,false), "Ineligible/dead/zoning recovered");
Check(!p.ShouldRetry(clock.AddSeconds(11),true,false), "Eligibility change did not reset delay");
Check(p.Attempts == 0, "Ineligible retry consumed allowance");
Console.WriteLine("RSR recovery checks passed: grace period, throttle, three-attempt cap, healthy/disabled reset, encounter reset.");

Check(MovementReadinessPolicy.Warning(true,true,true).Contains("/bmrai off"), "AI mode override must be visible even with module/preset present");
Check(MovementReadinessPolicy.Warning(false,true,false).Contains("No active encounter"), "Missing module cannot be reported as dodge ready");
Check(MovementReadinessPolicy.Warning(false,false,true).Contains("ownership"), "Lost preset must be reported");
Check(MovementReadinessPolicy.Warning(false,true,true) == string.Empty, "Healthy handoff must not produce false warning");

Check(MovementReadinessPolicy.RestoreOwnPreset(true,true,true,true), "Cleared owned preset must recover after revive");
Check(!MovementReadinessPolicy.RestoreOwnPreset(true,false,true,true), "Must not overwrite another plugin's non-empty preset");
Check(!MovementReadinessPolicy.RestoreOwnPreset(true,true,true,false), "Must not restore while dead");
Check(!MovementReadinessPolicy.RestoreOwnPreset(true,true,false,true), "Must not restore when BMR IPC is unavailable or AI mode conflicts");
Check(!MovementReadinessPolicy.RestoreOwnPreset(false,true,true,true), "Must not claim a preset this service did not own");
Check(MovementReadinessPolicy.ScoreSetupWarning(2,0,true,false,true,false,true,false).Contains("2 RSR"), "Recorded duplicate RSR must stop score combat");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,0,true,false,true,false,true,false).Contains("0 BMR"), "Recorded missing BMR must stop score combat");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,true,false,true,false,true,false).Contains("connection"), "Loaded plugin alone does not prove IPC readiness");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,true,true,false,true,true,true).Contains("AI Off"), "Conflicting AI must stop score combat");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,true,true,true,false,true,true).Contains("preset"), "Lost movement preset must stop score combat");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,true,true,true,true,true,false).Contains("encounter"), "Missing encounter cannot silently attack without dodging");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,true,true,true,true,true,true)==string.Empty, "Healthy live setup allowed");
Check(MovementReadinessPolicy.ScoreSetupWarning(1,1,false,false,true,false,false,false)==string.Empty, "Outside-board preset absence is expected");
Console.WriteLine("Movement readiness checks passed.");
}
catch(Exception ex) { Console.Error.WriteLine("FAILED: " + ex.Message); Environment.ExitCode = 1; }
