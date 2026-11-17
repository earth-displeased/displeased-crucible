# Displeased Crucible plugins

Dalamud plugins tuned for the Beastmaster *Crucible of the Unbroken* boards (First / Second / Third Board,
First and Second Master's Board).

| Plugin | InternalName | Based on | License |
|---|---|---|---|
| Displeased BeastHelper | `DispleasedBeastHelper` | [Rin-0617/BeastHelper](https://github.com/Rin-0617/BeastHelper) v0.2.0 | AGPL-3.0 |
| Rotation Solver Reborn - Board Score Assist | `RotationSolver` | [FFXIV-CombatReborn/RotationSolverReborn](https://github.com/FFXIV-CombatReborn/RotationSolverReborn) | see `src/RotationSolverReborn/COPYING` |
| BossMod Reborn - Crucible Assist | `BossModReborn` | [FFXIV-CombatReborn/BossmodReborn](https://github.com/FFXIV-CombatReborn/BossmodReborn) | BSD-3-Clause |

## Install (any FFXIV / Dalamud installation)

1. In game: `/xlsettings` → **Experimental** → **Custom Plugin Repositories**, add
   `https://raw.githubusercontent.com/earth-displeased/displeased-crucible/main/pluginmaster.json`, tick **Enabled**, Save.
2. **Uninstall the official Rotation Solver Reborn and BossMod Reborn first.** These forks use the same
   InternalName, so only one copy of each can be installed.
3. `/xlplugins` → install **Displeased BeastHelper**, **Rotation Solver Reborn - Board Score Assist**,
   **BossMod Reborn - Crucible Assist**.

Displeased BeastHelper uses its own name, settings file (`DispleasedBeastHelper.json`) and commands
(`/dbh`, `/displeasedbeasthelper`), so it can stay installed next to the official BeastHelper.
**Enable only one BeastHelper at a time.** Both automate the same game windows.

Settings start empty. To carry settings over, close the game and copy
`%APPDATA%\XIVLauncher\pluginConfigs\BeastHelper.json` to `DispleasedBeastHelper.json` in the same folder.
Then reselect your Crucible preset in the plugin.

Entry, difficulty, routes, camps, shops and rewards remain manual.

## Layout

- `pluginmaster.json`: the Dalamud custom repo manifest. Downloads come from this repo's GitHub releases.
- `src/`: source snapshots of the three plugins, published with every release.
- `notes/`: attempt reviews and setup notes.
- `tools/replay-tools/`: Python BMR replay analysis.
- `Publish-Release.ps1`: syncs the sources from the dev folders, builds, runs policy tests, zips, updates
  `pluginmaster.json`, pushes and creates the GitHub release.
