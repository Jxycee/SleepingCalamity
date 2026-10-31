# Hollowmere Wave 0 layout greybox: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

Progress: 65%

**State:** in progress. Unity editor installed; the Roslyn compile of every assembly passes. The first licensed `unity.sh build` (ProjectBuilder.RunAll with HollowmereBuilder) is running now; then captures, `check`, merge. The user approved the town plan from the maps (2026-10-31, relayed by the controller).

| Item | State |
|---|---|
| Layout data + validator (`Assets/_Project/Core/Town/`) | done, 0 rule violations |
| EditMode tests (`HollowmereLayoutTests`, 17) | pass under `sc_core_tests.py` (.NET, no Unity) |
| `HollowmereBuilder` + `HollowmereCapture`, wired into `ProjectBuilder.RunAll` | compiles (cloud Roslyn); reviewer pass done, 2 fixes applied (bab7b44); Unity build running |
| Top-down maps (`Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg`) | done (PIL, `Tools/town/sc_town_map.py`) |
| Play-position captures, both presets | waiting for Unity |
| `unity.sh check` | waiting for Unity |
| Merge to main | not yet |

**Foundation counts:** ring 1: 10 (3 L, 4 M, 3 S); ring 2: 23 (4 L, 14 M, 5 S); ring 3: 3 (1 M, 2 S). 36 in all.

**Blockers:** none. (The Unity download died once in the background and was restarted; it finished.)
