Board Two fixes — BeastHelper 0.2.17 / RSR 7.5.7.119 / BMR 7.5.7.519

Choose Crucible score mode > Second Board. Keep BMR AI off, BMR dodge integration on, RSR Auto. Entry, fight start, rewards, shops and board movement remain manual. No duty items. /beasthelper stop stops automation.

Roster fixes: clear search/filters only while the automated roster setup is open; read the actual horn index vector; send the agent selection event rather than the inspection callback; preserve all-three automatic horn assignments in any order. The Vilekin rotation resolves species to actual horn slots, so Damselfly, Mantis and Wespe need not occupy fixed whistle numbers. Setup reports VERIFIED and current order. Combat rechecks ActionManager's horn assignments so manual setup is accepted rather than silently disabling the named rotation.

Encounter fixes: scoped opt-in loads the unfinished Loosefrox and Taurus modules without lowering the global maturity filter. Board Two enables replay auto recording for subsequent duty entries. Tablitaur switches targets with a wider hysteresis, spin counters reset each sequence, unrelated icons cannot erase the rotating-cone direction, and the earlier finisher hold remains. Taurus clears lighted-tile priorities when the tile finishes or disappears.

Validation: regression tests and builds pass. No saved replay of the reported failed run was found. Changes address verified code defects and diagnostics; the full Board Two sequence still needs live validation. User health loss during the brothers cannot yet be attributed to a specific hit.

References checked: FFXIVClientStructs AgentXBMPetParty and AgentXBMMonsterNotebook; public LazyCrucible PetSelect/FormationLogic for agent event kind and horn-index semantics. No broad native memory scanning or arbitrary writes used.
