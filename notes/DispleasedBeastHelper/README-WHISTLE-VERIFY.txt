BeastHelper 0.2.15 - verified whistle selection
The old callback toggled each row without reading its state, which could clear the game's automatic three-pet selection.
Now reads AgentXBMPetParty.SelectedPets and compares the exact slot order. A correct selection causes no callbacks. Wrong suffixes are removed one at a time and desired beasts added in order; every callback must be acknowledged in live selected state before the next. An unavailable read or four-second unacknowledged callback pauses rather than blind retries.
Do not Start Battle during Correcting whistle order or Waiting for confirmation. Only Whistles VERIFIED (3/3): damselfly -> mantis -> wespe reports ready. Score combat also requires verified whistles, not just the three-beast roster.
Open setup and wait for VERIFIED. If verification pauses, leave the screen open and report the displayed message; the live selection observation is logged. The manual Start Battle button is not intercepted or disabled by this plugin.
Emergency stop remains /beasthelper stop; Resume remains explicit. No changes to BMR/RSR or no-item restrictions.
Validation: zero-warning build and 88 score/stop/selection policy checks pass. Live native selected-list semantics and UI callback acknowledgements still need validation in the game.
