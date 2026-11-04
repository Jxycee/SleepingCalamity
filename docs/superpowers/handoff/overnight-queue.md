# Light-work queue (overnight and quiet hours)

The coordinator hands these out when the user is away (user, 2026-11-03: "keep light things running ... add onto it when it's nearly out"). Light means docs and pure Core work: no capture rounds, no art loops, at most one gate or check per job. Sessions run Sonnet medium; subagents up to Opus high. Overnight the app refuses new session starts from event turns, so a finished session may take the next item by coordinator message. Add items when three or fewer are left. The user's workday is a break: nothing runs.

## Running now
- Plan 2 per-player loot: MERGED `ee9435ef`.
- Story: MERGED `7490abe0` (book 7 chapters + bible draft; waits on the user's read and sign-off). Was: the book in chapters (`docs/story/book/`, any length, the user reads and annotates it like a novel) plus the game-facing bible draft (`docs/superpowers/specs/2026-11-04-story-bible-draft.md`) for sign-off. Tone: Abercrombie's *The Devils* maturity, Dark Cloud 1 engagement. What the game doesn't use becomes lore and reference.
- Plan 2 session (Sonnet medium, 2026-11-04 day, user's word): taking items 5, 4, 2, 3 below in that order; item 1 left for the next coordinator. Done: item 5 triage `e78852e4`; item 4 flaky spine test `38f9be2a` (check PASS on `3194f4c0`; reasoned fix, not reproduced); items 2 and 3 docs merged (last `41ff7736`). Then (user: keep it running while there is work) items 1 and 6 merged: plan 3 plan `b38c4046` (`docs/superpowers/plans/2026-11-04-new-dive.md`), UV regen plan `fa505ab7` (`2026-11-04-kit-uv-stretch-regen.md`). **Queue empty**: the next coordinator refills it. Session Archive: ready.
- Town art junctions pass: MERGED `a421ceb3`; wide join survey deferred.

## Next (take from the top)
A. **M7 open questions triage** (docs only): sort the M7 open questions by whether plan 3 needs them; list what blocks, what can wait.
B. **Story bible open questions sorted by plan 3 need** (docs only): from `docs/superpowers/specs/2026-11-04-story-bible-draft.md` (20 open questions); flag the ones touching floor names, NPCs or text the dive build will show.
C. **PARKING_LOT plan 2 follow-ups + DiveBot pace notes** (docs only): per-player gold source design note (placeholder numbers), and DiveBot pace notes for the 7-floor dive.

(Older items below; items 2 and 3 done, 1 and 4-6 merged.)
1. **Plan 3 detailed plan** (writing-plans only, no code): `docs/superpowers/plans/2026-11-0X-new-dive.md` from roadmap row 3 (plan 2 merged `ee9435ef`: ready). Fold in the reviewer notes plan 1 and plan 2 left for plan 3.
2. **Floor card and modifiers design** (spec section 12 step 4, docs): the named floor card with two modifiers, the two-valve seal and hold-the-pump objectives, as a short design note with numbers marked placeholder.
3. **Encounter templates and water-gimmick room plan** (spec step 5, docs): which of the 12 rooms get which water states, using `docs/ROOM_METRICS.md` and `CellarRoomsV2`.
4. **Flaky test fix**: `DiveSpineNetworkTests.Contents_ArmedByTheDirector_StairNeedsTheGateKey_FailedDiveClearsEnemies` (board note 2026-11-01): add timeouts or deterministic waits; one check.
5. **Parking lot and bug triage**: `docs/PARKING_LOT.md` and `Reference/qa/playtest/BUGS.md` against what has merged since Nov 1; close or re-file items (docs only).
6. **Kit UV-stretch regen plan** (polish round 4 deferred list): list the 33 stretched kit meshes with the Blender fix each needs, ready for an art session after the break. Planning only.
</content>
</invoke>
