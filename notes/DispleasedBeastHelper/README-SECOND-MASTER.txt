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


SECOND MASTER'S BOARD XP RUN (BeastHelper 0.2.40, 2026-11-14 - built and tested, not yet validated live)
  Roster mode dropdown: "Second Master's Board XP Run". Same as the Legendary preset (Cu Sith / Behemoth / Coeurl on the
  whistles, same RSR/BMR fight strategies, same item and recovery settings), plus the 12 lowest-ranked other captured beasts
  to fill the 15-beast roster. Ranks must be synced (open the bestiary once). Loses Beast Squad III (4,250).
  Difficulty, entry, route, camps, rewards and shops stay manual.
  DEPLOYED 2026-11-14 12:26:21 (hashes verified): BeastHelper 0.2.40 (backup backups\BeastHelper-before-secondmasterxp-20261114-122620, 0.2.39).

DEPLOYED 2026-11-14 15:12: BeastHelper 0.2.41 - Second Master XP Run no longer pauses on unknown ranks (treated as rank 1, after known rank 1); rank readers ignore values outside 1-25 so 0 no longer overwrites real ranks. Built and tested, not yet validated live. Backup: backups\BeastHelper-before-xp-unknown-ranks-20261114-151257
DEPLOYED 2026-11-14 15:43: BeastHelper 0.2.42 - First Master and Third Board XP runs also fill rosters with unknown ranks (treated as rank 1). Second Master XP fill validated live on 0.2.41. Backup: backups\BeastHelper-before-xp-unknown-ranks-all-boards-20261114-154358
DEPLOYED 2026-11-14 16:24: BeastHelper 0.2.43 - ranks refreshed from AgentXBMPetParty.GetSyncedRank/IsPetOverSyncedRank whenever the whistle screen is open (every 2 s), before the XP roster is computed; log line 'Ranks refreshed from whistle screen'. Built and tested, not yet validated live. Backup: backups\BeastHelper-before-agent-rank-refresh-20261114-162414
