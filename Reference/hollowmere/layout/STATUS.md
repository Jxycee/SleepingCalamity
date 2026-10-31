# Hollowmere Wave 0 layout greybox: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

Progress: 100% (Wave 0 greybox merged; terrain pass next on a new branch)
Sheet: Reference/hollowmere/layout/Hollowmere_Captures_Sheet.jpg

**State:** MERGED to main as `1f6ff88` (merge commit). `check` on main: `SC RESULT check PASS` (build PASS; test-edit 586/586; test-play 32 passed, 8 skipped, 0 failed; marker 1f6ff88). HollowmereBuilder is on main for Wave 1. Next: the terrain pass (user feedback 2026-10-31) on a new branch.

| Item | State |
|---|---|
| Layout data + validator (`Assets/_Project/Core/Town/`) | done, 0 rule violations |
| EditMode tests (`HollowmereLayoutTests`, 17) | pass in Unity (`check` test-edit 586/586) |
| `HollowmereBuilder` + `HollowmereCapture` in `ProjectBuilder.RunAll` | done; `unity.sh build` PASS; reviewer pass, fixes applied |
| Top-down maps | `Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg` (PIL from the layout data) |
| Play-position captures, both presets | `captures/HM_{HomeDoor,Fountain,TitheGate,LaneMouth,Overview}_{Ruined,Rebuilt}.jpg`, `captures/Corner_Wide_*.jpg` (Unity, cloud, xvfb) |
| `unity.sh check` | PASS on branch tip 30dfa7d and on main 1f6ff88 |
| Merge to main | done: 1f6ff88 |

**Foundation counts:** ring 1: 10 (3 L, 4 M, 3 S); ring 2: 23 (4 L, 14 M, 5 S); ring 3: 3 (1 M, 2 S). 36 in all.

**Blockers:** none.
