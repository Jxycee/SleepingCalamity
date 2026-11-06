# Queue triage notes (2026-11-05)

Docs only. Three notes from `overnight-queue.md` items A-C. Plan 3 (7-floor Hearthdeep dive) merged as `fa25ed80`; "plan 3 needs it" below means the dive build or the next playtest shows or depends on it.

## A. M7 open questions (`docs/design/m7-proposal.md` section 3), triaged

Staleness first: the proposal (2026-11-02) describes a 4-floor dive (floors 1-4, bank at floor 2 and 4). Plan 3 made it 7 floors with per-player bags. Rewrite its section 1 loop and section 4 stage table before M7 starts; none of the six questions below depends on floor count.

| Q | Topic | Blocks the playtest? | Verdict |
|---|---|---|---|
| Q3 | How tuning is applied (one playtest, one notes file, one tuning branch, debug overlay) | Yes, mildly | Answer first. The user's playtest produces the notes; the process must exist before they play. Recommended A needs the read-only overlay (dive time, damage taken, heals, loaves). |
| Q4 | What a winnable 2p dive is (bot bar 1 in 4) | Yes, for the bot half | Blocked on bot pace: `DiveBot_Tough_Seed7` times out before floor 7 (see C). Restate the bar for 7 floors (clear floors 1-7, or a checkpoint floor) once the bot finishes. The user's three-tries bar can run without the bot. |
| Q1 | 3-4 player smoke (bots + MPPM) | No | Needed at the end of M7, not for the first 2p playtest. Bots need the pace fix in C anyway. |
| Q2 | "No desync" pass bar (state hash) | No | Can wait until Q1. Hash fields must now include per-player bags and the 7-floor stir, so the risk-reviewer's list needs refreshing. |
| Q6 | Network conditions contract (150 ms / 2%, 300 ms / 5%) | No | Can wait. Plan's number is already the default; answer when the simulator walks are written. |
| Q5 | Weapon wear after playtest | No | Can wait. Default is no; the playtest ends with one question. Nothing to do now. |

Summary: answer Q3 now, restate Q4 for 7 floors; Q1, Q2, Q6 wait for the smoke stage; Q5 waits for the playtest's last question.

## B. Story bible open questions (`2026-11-04-story-bible-draft.md` section 9.2), sorted by plan 3 need

Tags are from the question text and the bible's amendment table only; the user has not signed off the bible, and `docs/STORY.md` is untouched. Sorting key: does the dive build show a floor name, an NPC or text. Plan 3 already ships seven floors, so question 6 is a "confirm what is built" item, not an open design choice.

**Touches floor names, NPCs or text the dive shows (answer before the dive gets story text):**
- 6. Hearth as the sixth town; Hearthdeep seven floors. Floor count is built; floor names and the Hearth name appear in HUD and floor card.
- 7. One freed-sleeper rule (breathed up a shaft on dives). Changes the rescue prompt and what the dive shows when a sleeper is freed.
- 8. Cinderholt scripted failed dive and story gate. Changes dive entry for that town.
- 15. Diegetic people-left lines (one token per dungeon vs lanterns). Text and props in the dive.
- 16. Nell home from ch2 and Col as the last sleeper. Names a rescued NPC the dive pays out.
- 13. Lieutenants Ivo and Dagny become residents with services. Resident names and services shown in town and rescue.
- 10. Rescued people still bound (4). Dialogue text on rescue.
- 12. Mirror floor playable (27). A floor the dive would have to build.
- 4. Odile's death and the gate "Thank you" line (23). Boss-floor text, if the finale is in the dive.

**Story only, can wait (nothing the dive shows yet):** 1 (tone amendment), 2 (children's table), 3 (Wynn re-taken), 9 (Vane's end), 11 (solo scenes show all four), 14 (Pip reads the confession), 17 (Isolde, Hob seeds), 18 (the triangle), 19 (Nell dramatic irony).

**Touches play rules (not text):** 5 (staff spells post-game) and 20 (script's cost touches play). Neither blocks plan 3; both are one-line rules for the combat and save design once decided.

Top ask for the user: 6, 7, 8, 13, 16, 15. Everything else is narrative and does not gate the dive.

## C. Plan 2 follow-ups: per-player gold, and DiveBot pace

### C1. Per-player gold source (design note, placeholder numbers)

State on main: the chest path exists. `CellarContentsSync` diverts a `CellarLoot.GoldId` roll to `DiveHaulSync.ServerAddGold` (`GoldPurse = 25`, one chest roll in `GoldOneIn = 4`). This closes half of the PARKING_LOT 2026-11-03 gold line. Still missing: enemy-kill gold, a satchel/stash view for `BagView.StashTotal`, and emptying the satchel in town (spec decision 35). The pieces below are placeholders for tuning, all in data.

- Enemy kill: killer's bag only (matches per-player loot). Purse by tier: Slime 2, Broom 3, RatKing 6, tier-2 later. Room-clear bonus split equally among players present, rounded down, remainder to the last killer.
- Chest purse scales with floor: `25 + 5 * (floor - 1)`; keeps the one-in-four odds.
- Rough budget for a clean 7-floor solo dive: about 300-400 gold. Co-op scales per player (each kills their own), so no co-op multiplier.
- Satchel/stash view: show the `StashTotal` next to the bag on the HUD; in town, turning the stash into town gold at the bank desk. A failed dive keeps half (round down) of that player's gold: placeholder, user to confirm.
- Authority: host-only grants (`ServerAddGold`), owner-only view; no new sync type needed.
- Open for the user: what gold buys (the sink). Without a sink the numbers cannot be tuned.

### C2. DiveBot pace notes for the 7-floor dive

Fact: `DiveBot_Tough_Seed7` (`Tests/PlayMode/Bot/DiveBotTests.cs`, limit 5400 s in `Play`) times out before floor 7 because Broom kills are slow. It is bot-only and skipped in `check`, and the handoff calls it a bot/budget issue, not a game bug.

Options, cheapest first (not tried; an implementer should confirm timing before choosing):
1. Raise the limit to scale with floors (for example 900 s per floor, 6300 s for seven) and keep the 3600000 ms `[Timeout]` in step (it is 60 min, below the 5400 s limit already).
2. Make the bot skip rooms with no objective: head for key, gate and stair, fight only blocking Brooms.
3. Give the bot a Broom-aware attack (approach to strike range, repeat the same combo) instead of kiting; check whether the Tough profile makes Broom slower than Slime by design (Broom is the floor-banded mix's middle weight).
4. If the dive is just longer, accept a 2-3 seed spot check at 7 floors and keep the full seed sweep at the checkpoint floor.

Pass bar for the bot (feeds M7 Q4): clear floors 1-7 in under 6000 s on Seed7 Tough, or log the floor it reached.
