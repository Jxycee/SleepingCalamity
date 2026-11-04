# Light-work queue (overnight and quiet hours)

The coordinator hands these out when the user is away (user, 2026-11-03: "keep light things running ... add onto it when it's nearly out"). Light means docs and pure Core work: no capture rounds, no art loops, at most one gate or check per job. Sessions run Sonnet medium; subagents up to Opus high. Overnight the app refuses new session starts from event turns, so a finished session may take the next item by coordinator message. Add items when three or fewer are left. The user's workday is a break: nothing runs.

## Running now
- Plan 2 per-player loot (`claude/magical-liskov-576bbe`): tasks 6-7, then merge. Critical path.
- Story (session `story-bible`, started 2026-11-03 night): the book in chapters (`docs/story/book/`, any length, the user reads and annotates it like a novel) plus the game-facing bible draft (`docs/superpowers/specs/2026-11-04-story-bible-draft.md`) for sign-off. Tone: Abercrombie's *The Devils* maturity, Dark Cloud 1 engagement. What the game doesn't use becomes lore and reference.
- Town art junctions pass (`claude/hollowmere-junctions`): finish the current pass, merge, then stop; no new art rounds overnight.

## Next (take from the top)
1. **Plan 3 detailed plan** (writing-plans only, no code): `docs/superpowers/plans/2026-11-0X-new-dive.md` from roadmap row 3, after plan 2 merges. Fold in the reviewer notes plan 1 and plan 2 left for plan 3.
2. **Floor card and modifiers design** (spec section 12 step 4, docs): the named floor card with two modifiers, the two-valve seal and hold-the-pump objectives, as a short design note with numbers marked placeholder.
3. **Encounter templates and water-gimmick room plan** (spec step 5, docs): which of the 12 rooms get which water states, using `docs/ROOM_METRICS.md` and `CellarRoomsV2`.
4. **Flaky test fix**: `DiveSpineNetworkTests.Contents_ArmedByTheDirector_StairNeedsTheGateKey_FailedDiveClearsEnemies` (board note 2026-11-01): add timeouts or deterministic waits; one check.
5. **Parking lot and bug triage**: `docs/PARKING_LOT.md` and `Reference/qa/playtest/BUGS.md` against what has merged since Nov 1; close or re-file items (docs only).
6. **Kit UV-stretch regen plan** (polish round 4 deferred list): list the 33 stretched kit meshes with the Blender fix each needs, ready for an art session after the break. Planning only.
</content>
</invoke>
