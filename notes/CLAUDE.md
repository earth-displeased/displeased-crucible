# Dalamud plugins – BeastHelper / RSR / BMR (FFXIV Crucible of the Unbroken)

Character: Tarron Berrick (BST).
**First Master's Board, First Degree: Legendary reached 2026-11-13 (19,779)** with BST trio Cu Sith (whistle 1) /
Behemoth (50) / Coeurl (33) - summary in `beasthelper\FIRST-MASTER-LEGENDARY-CLEAR.txt`. Keep that behaviour intact
(gated to the First Master profile + territory 1342).
**Current focus: Second Master's Board (territory 1343, BMR group 1092, modules `05SecondMasterBoard\*.cs`).** Earlier
setup notes: `beasthelper\README-SECOND-MASTER.txt` (BeastHelper "Prepare Second Master's Board" preset, optional
experimental WIP modules). **Second Master's Board, Standard: Legendary reached 2026-11-14 (19,004)**, left route
(Mindflayer -> camp -> Atomos), see `beasthelper\SECOND-MASTER-ATTEMPT9-REVIEW.txt`. Name new reviews
`beasthelper\SECOND-MASTER-*-REVIEW.txt`.

**BeastHelper is now "Displeased BeastHelper"** (since 0.3.0, 2026-11-16): `InternalName`/assembly
`DispleasedBeastHelper`, commands `/dbh` and `/displeasedbeasthelper`, settings `pluginConfigs\DispleasedBeastHelper.json`.
It can stay installed next to the official BeastHelper (Rin-0617), but only one should be enabled. IPC names stay
`BeastHelper.*` (the official plugin has no IPC). The fork is on branch `displeased` in the BeastHelper clone
(`origin` = Rin-0617/BeastHelper, so to take an official update run `git fetch origin` and `git merge origin/main`). RSR/BMR forks keep
the official InternalNames (`RotationSolver`, `BossModReborn`) and replace the official plugins.

## Where things live
| What | Path |
|---|---|
| Live dev plugins (loaded by Dalamud) | `E:\Dalamud_Plugins\beasthelper`, `RSR-FirstBoard-Assist`, `Resurrection-BMR-Test1` |
| BeastHelper source | `C:\Users\earth\Documents\Codex\2026-11-03\is-x20\work\BeastHelper\src\BeastNav` |
| RotationSolver (RSR) source | `C:\Users\earth\Documents\Codex\2026-11-03\is-x20\work\RotationSolverReborn-main\RotationSolver` |
| BossModReborn (BMR) source – **current copy** | `E:\Dalamud_Plugins\development\work\BossmodReborn-main\BossMod` (the copy under Codex\work is stale) |
| Policy tests | `...\work\BeastHelper\tests\FirstBoardScore` (compiles policy files from all three plugins) |
| .NET SDK (offline) | `...\work\dotnet\dotnet.exe`, packages `...\work\nuget-packages`, feed `...\work\feed`, config `...\work\NuGet.Config` |
| Replays (BMR, compressed binary) | `%APPDATA%\XIVLauncher\pluginConfigs\BossModReborn\replays\FirstMastersBoard_*.log` |
| Plugin diagnostics | `%APPDATA%\XIVLauncher\dalamud.log` (`[BeastHelper]` / `[DispleasedBeastHelper]` from 0.3.0, `[First Master defense]`, `[First Master Treant]`, `[First Master Borgny]`) |
| BeastHelper saved settings | `%APPDATA%\XIVLauncher\pluginConfigs\DispleasedBeastHelper.json` (pre-0.3.0: `BeastHelper.json`) – read only, never edit |
| Replay analysis (Python) | `E:\Dalamud_Plugins\development\replay-tools\` (see its README; `pip install brotli`) |
| Backups | `E:\Dalamud_Plugins\backups\` |
| GitHub publishing repo | `E:\Dalamud_Plugins\displeased-crucible` → https://github.com/earth-displeased/displeased-crucible (public) |
| Dalamud custom repo URL | `https://raw.githubusercontent.com/earth-displeased/displeased-crucible/main/pluginmaster.json` |

Key code: RSR `RebornRotations\Limited Jobs\BST_Reborn.cs` and `Basic\Helpers\*Policy.cs`
(FirstMasterDefensePolicy, TreantMeteorPolicy, BorgnyToxicMassPolicy); BeastHelper
`Services\CrucibleRecoveryItemService.cs`, `CrucibleItemTimingPolicy.cs`, `FirstMasterLegendaryProfile.cs`;
BMR `Modules\Global\CrucibleOfTheUnbroken\04FirstMasterBoard\*.cs`, `FirstMasterTargetPolicy.cs`.
RSR reads BeastHelper state over IPC (`BeastHelper.FirstMasterLegendaryActive`); First Master logic is
gated on that and on territory 1342.

## Build
Use `E:\Dalamud_Plugins\development\Build-BorgnyUpdate.cmd` as the reference. Environment:
```powershell
$W = 'C:\Users\earth\Documents\Codex\2026-11-03\is-x20\work'
$env:DOTNET_CLI_HOME = "$W\dotnet-home"; $env:NUGET_PACKAGES = "$W\nuget-packages"
$env:DOTNET_NOLOGO = 1; $env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DALAMUD_HOME = "$env:APPDATA\XIVLauncher\addon\Hooks\dev"
$d = "$W\dotnet\dotnet.exe"; $cfg = @('-c','Release',"-p:RestoreConfigFile=$W\NuGet.Config",'-nologo')
& $d build "$W\BeastHelper\tests\FirstBoardScore\FirstBoardScore.csproj" @cfg
& $d "$W\BeastHelper\tests\FirstBoardScore\bin\Release\net10.0\FirstBoardScore.dll"   # must print PASS
& $d build "$W\BeastHelper\src\BeastNav\BeastNav.csproj" @cfg
& $d build "$W\RotationSolverReborn-main\RotationSolver\RotationSolver.csproj" @cfg
& $d build "E:\Dalamud_Plugins\development\work\BossmodReborn-main\BossMod\BossModReborn.csproj" @cfg   # slow
```
Expect zero errors; BMR has ~6 pre-existing deprecated-API warnings. Outputs:
- BeastHelper: `src\BeastNav\bin\Release\DispleasedBeastHelper.{dll,json,deps.json}` (+ `MoonSharp.Interpreter.dll`;
  ignore the stale `BeastHelper.json` there)
- RSR: `RotationSolver\bin\Release\RotationSolver.{dll,json,deps.json}`
- BMR: `BossMod\bin\Release\BossModReborn.{dll,json,pdb}`

## Deploy
1. The game must be closed, or the three plugins unloaded / set to auto-reload. Ask the user if unsure.
2. Back up the current live files to `E:\Dalamud_Plugins\backups\<plugin>-before-<change>-<yyyymmdd-hhmmss>\`.
3. Copy only the files listed above into the live folders. Do not register `prepared\` folders as extra plugins.
4. Verify the copied files' hashes match the build output.
`Install-BorgnyUpdate.cmd` does steps 2–3 for the staged Borgny build.

## Publish to GitHub (other installs update from the custom repo)
After a change is built, tested and deployed, run
`E:\Dalamud_Plugins\displeased-crucible\Publish-Release.ps1` (`-SkipBuild` reuses the bin outputs, `-NoPush` for a local dry
run, `-Notes "..."`). It snapshots all three sources plus notes and replay-tools into the repo, runs the policy tests,
zips each plugin, rewrites `pluginmaster.json`, pushes and creates a `release-<yyyymmdd-hhmm>` GitHub release. Dalamud
only offers an update when `AssemblyVersion` goes up, so always bump versions before publishing. Ask before publishing:
it is public.

## Conventions (carried over from earlier work – keep them)
- Bump `<Version>` on every change (BeastHelper also `src\BeastNav\DispleasedBeastHelper.json` `AssemblyVersion`).
- Put decisions in pure `*Policy` classes and add `Check(...)` cases to `tests\FirstBoardScore\Program.cs`; add new
  policy files to `FirstBoardScore.csproj`. Base thresholds and timings on replay evidence and cite it in comments.
- Gate First Master behavior to the First Master profile / territory 1342; never change other boards' behavior.
- Never edit live config JSON. New defaults go in `FirstMasterLegendaryProfile.Apply`, and the user must
  **reselect the preset** for them to take effect – say so every time.
- Entry, difficulty, routes, camps, shops and rewards stay manual.
- Source files are UTF-8 with BOM and CRLF – preserve both.
- Each attempt gets a review in `beasthelper\` (what the replay shows, causes, changes, verification,
  next run). Distinguish "built and tested" from "validated in a live run"; don't claim a clear until one is recorded.

## Analyzing a new attempt
1. Newest `FirstMastersBoard_*.log` in the replays folder; matching time range in `dalamud.log`.
2. `python economy.py <replay>` for per-fight damage, damage taken and pet HP; `borgny.py <replay> -t` for the final boss;
   `pets.py <replay> <start> <end>` for summons, boss targets and Snarl.
3. IDs worth knowing: Borgny 0x4CD8, Toxic Mass 0x4CD9, Wriggling Phlegm 48817, Toxic Breath 48807/48808,
   Treant 0x4CCD; player actions 47092/47093 = releases, 44891 = Trick, 46751 = Snarl, 46750 = Challenge,
   44881/44892/44894 = battlehorn summons; player status 2413 = covered by familiar (Snarl).
