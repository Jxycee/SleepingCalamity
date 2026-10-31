# Hollowmere Wave 0 layout greybox: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

Progress: 85%
Sheet: Reference/hollowmere/layout/Hollowmere_Captures_Sheet.jpg

**State:** builder runs in Unity (cloud `unity.sh build` PASS); captures rendered in Unity in the cloud, both presets. Next: bring main in, `unity.sh check`, merge. After the merge: the terrain pass (user feedback 2026-10-31) on a new branch.

| Item | State |
|---|---|
| Layout data + validator (`Assets/_Project/Core/Town/`) | done, 0 rule violations |
| EditMode tests (`HollowmereLayoutTests`, 17) | pass under `sc_core_tests.py`; Unity run in `check` next |
| `HollowmereBuilder` + `HollowmereCapture` in `ProjectBuilder.RunAll` | done; `unity.sh build` PASS; reviewer pass, fixes applied |
| Top-down maps | `Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg` (PIL from the layout data) |
| Play-position captures, both presets | `captures/HM_{HomeDoor,Fountain,TitheGate,LaneMouth,Overview}_{Ruined,Rebuilt}.jpg`, `captures/Corner_Wide_*.jpg` (Unity, cloud, xvfb) |
| `unity.sh check` | next |
| Merge to main | not yet |

**Foundation counts:** ring 1: 10 (3 L, 4 M, 3 S); ring 2: 23 (4 L, 14 M, 5 S); ring 3: 3 (1 M, 2 S). 36 in all.

**Blockers:** none.
