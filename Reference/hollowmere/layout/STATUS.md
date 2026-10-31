# Hollowmere Wave 0 layout greybox: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

**State:** in progress. Layout data, validator, EditMode tests, builder and capture script written; the approval maps are drawn from the layout data. The Unity editor is still downloading in this container, so the builder has not run yet and `unity.sh check` has not run yet.

| Item | State |
|---|---|
| Layout data + validator (`Assets/_Project/Core/Town/`) | done, 0 rule violations |
| EditMode tests (`HollowmereLayoutTests`, 17) | pass under `sc_core_tests.py` (.NET, no Unity) |
| `HollowmereBuilder` + `HollowmereCapture`, wired into `ProjectBuilder.RunAll` | written, not compiled by Unity yet |
| Top-down maps (`Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg`) | done (PIL, `Tools/town/sc_town_map.py`) |
| Play-position captures, both presets | waiting for Unity |
| `unity.sh check` | waiting for Unity |
| Merge to main | not yet |

**Foundation counts:** ring 1: 10 (3 L, 4 M, 3 S); ring 2: 23 (4 L, 14 M, 5 S); ring 3: 3 (1 M, 2 S). 36 in all.

**Blockers:** none yet. The Unity download died once in the background and was restarted.
