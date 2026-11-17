Lauda update 7.5.7.515 (Crucible Assist 3): attack-range goals for highest-priority living adds, stable target until defeat/unavailable, then return to targetable boss. Positive movement goals preserve existing hazard avoidance. 269 offline regression checks passed; live Lauda validation pending.
Previous 7.5.7.514 run cleared Second Master with manual movement to both Indefatigable Blades and back to Lauda.

Durga fix: retain a living Spinner-rook target and provide an attack-range movement goal, then select the closest remaining rook. Encounter forbidden zones retain control of safe movement. BeastHelper restores its previously owned movement preset only when empty, alive, and BMR AI is off. Foreign active presets are preserved.
SECOND MASTER BOARD - Durga add-phase movement fix
BeastHelper English 0.2.10 + BossMod Reborn Crucible Assist 2 (7.5.7.514)
Open /beasthelper, Crucible tab. Click Prepare Second Master's Board - reliable clears.
This enables combat-only mode, recommended roster/whistles, RSR combat, BMR movement and recovery potions below 40% HP. It does not enable automatic rewards, shops, entry, battle start or tile movement.
For this setup BMR AI must be Off: /bmrai off. BeastHelper uses its own movement-only preset, so the Blue Mage AI-On configuration conflicts with it. RSR remains the action controller; BeastHelper enables Auto inside a Crucible fight.
Optional checkbox: Allow unfinished Second Master encounter modules. It admits existing WIP Medusa, Chimera and Lauda modules only in territory 1343 and group 1092, with the SecondMasterBoard module namespace. Other duties retain the normal filter and individually disabled modules remain disabled.
BMR recording should be enabled for each attempt. Begin each battle manually after roster and whistles finish. Inspect active encounter name, movement warning, RSR status and activity log if actions or movement stop.
Coverage audit: 11 existing Second Master modules; 8 Contributed, 3 WIP. These labels are not guarantees of complete mechanics. Full-board clear not yet verified.
Build checks: BeastHelper zero warnings/errors, RSR recovery + movement-readiness regressions passed. BMR 259 offline policy/geometry checks passed, including Blue Mage regressions. BMR has six existing obsolete API warnings.
Successful Resurrection 7.5.7.512 was preserved in backups/Resurrection-SUCCESS-7.5.7.512-... before modifying the development build. Goldor and Resurrection controller source is unchanged by this update.
