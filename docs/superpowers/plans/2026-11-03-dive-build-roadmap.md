# Dive build roadmap (2026-11-03)

Turns spec section 12 (`docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md`), steps 1-3, plus the metrics gym into four plans. **One plan per fresh session** (`.claude/rules/session-usage.md`). Plan 1 is written in full; plans 2-4 get their detailed plan (writing-plans skill) at the start of their own session, because each depends on code the earlier plans change.

Workflow for every plan (from the 2026-11-03 workflow discussion):
- Pure rules in Core, test first (test-driven-development skill); the cloud runs them with `python3 Tools/cloud/sc_core_tests.py`, no Unity licence needed.
- One fresh subagent per task, then the review tier from `CLAUDE.md` (risk-reviewer for networking, host sync and save data).
- `gate` on every branch push that touches `Assets/`, `Packages/` or `ProjectSettings/`; full `check` before merging into main.
- Shape early, looks late: no art or new rooms until the loop is proven (Cerny's "first playable", the level design order metrics, blockout, greybox, playtest, art).

| # | Plan | Goal | Depends on | Review | Size |
|---|---|---|---|---|---|
| 1 | `2026-11-03-dive-rules-core.md` | The signed-off dive rules as pure Core classes with tests: floor plan, checkpoint and floor pick, bank slots, failure gold and the lost satchel, momentum, boss retry, stir per half, piece depths, bread ladder. Nothing wired yet; the live game keeps the old 4-floor rules. | nothing | reviewer | 1 session |
| 2 | Per-player loot (plan written in its session) | Replace the host-held shared haul (`DiveHaul`, `DiveHaulSync`) with per-player bags: per-player chest and drop rolls shown only to their owner, per-player gold, bank slots and satchel; keys, pieces and people stay one shared host-held copy. Pieces go to town stock on pickup (safe). Save sections for per-player bags. | 1 | risk-reviewer | 1-2 sessions |
| 3 | The new dive, playable end to end (plan written in its session) | Wire plan 1's rules into `DiveDirector`: start at the church trapdoor, floors 1-7 (floor 1 a stand-in authored floor, floors 3 and 5 placeholder set pieces), the bank room with slots and the push-on/go-home choice, the one-time checkpoint and floor pick, stir per half, a stand-in ending after floor 7, failure with the satchel. Retire the old `DiveRoute`, `DiveState` and `StirMeter`. The dev overlay records go-home vs push-on, minutes per floor and fail floor. | 1, 2 | risk-reviewer | 2 sessions |
| 4 | Metrics gym and room data v2 (plan written in its session) | One builder-made gym scene with the game's real measurements (hero, camera boom, run and dodge distances, door widths, the smallest fight room that fits 4 heroes and 8 enemies, stair and ledge heights, water levels), then the room data v2 contract (height levels, door types, gimmick-state layers, tool sockets, enemy habitat tags, version field) with one pilot room. Before any new room is authored. | nothing (can run beside 2) | reviewer | 1 session |

After these: the user's greybox playtest of the new dive (watch, don't explain; overlay numbers), then spec section 12 steps 4-7 (objective variety, the water gimmick, set pieces, the bread ladder in play, stair suspend and crash saver, co-op extras, mixed-power growth).
