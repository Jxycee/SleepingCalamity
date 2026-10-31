State: DONE
Session: Q7. Cellars room pool, first rooms (M5 authoring)
Progress: 100%
Sheet: ROOMS.md (table + ASCII + sockets of all 21 rooms; generated from the code)
Blocker: none

Landed 2026-10-31: Core/Cellars/CellarRoom.cs (format, ASCII parser, Validate), CellarRoomPool.cs (12 combat rooms, 4 per sub-theme; entry, stair-exit,
checkpoint; 6 corridor pieces; pool rules), Tests/EditMode/CellarRoomPoolTests.cs (61 tests). No M5 floor generator existed on main, so only the
format, the pool and their tests were written. Core tests: sc_core_tests 61/61 for CellarRoom; unity.sh test-edit 670/670.

Controller (2026-10-31): code + 43 tests landed on the branch (359b691), test-edit 652/652. In review (Sonnet reviewer), then check on the tip, then merge. DONE is set after the merge.

Review done (Sonnet reviewer: no blocking findings); its fixes landed in d13db53 (rotation helpers + tests, barrel/crate blocking rule, real 70% and round-trip tests, 8 failing-case tests, nits). Room tests 61/61, test-edit 670/670. check running on 3aa1471 (main merged in); merge on PASS.

MERGED 513ac08 (2026-10-31). SC RESULT check PASS on 3aa1471 (build PASS, test-edit 670/670, test-play 32 passed / 8 skipped / 0 failed). The merged tree is identical to the checked tip in Assets/Packages/ProjectSettings.
Next for M5: the floor generator consumes CellarRoomPool (FindCorridor + Rotated) and tops chests up in dead ends to reach 6-8 per floor.
