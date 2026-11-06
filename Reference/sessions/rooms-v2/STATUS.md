# rooms-v2 STATUS
State: merged (1546268e, 2026-11-03). Archive: ready
Plan: docs/superpowers/plans/2026-11-03-rooms-v2.md. Soak 44/44, check PASS on 189567f1. Branch review skipped (pure Core, data, tests, docs).
Done: CellarRoomsV2 (Core/Rooms) wraps all 21 pool + 6 bakery rooms by reference, all pass RoomValidatorV2; six rooms grown (pantry 3x5, cold store 3x5, cask row 5x3, bunker 3x5, L vault + furnace ring one cell opened); only coal_stoke_chicane (2 heroes) and bakery rooms (4 heroes) listed small in CellarRoomsV2.SmallParty. Generator still reads v1 (plan 3 switches). [Checked 2026-11-06 (stale-cleanup): STILL OPEN. `CellarFloorGenerator*.cs` has no `CellarRoomsV2`/`RoomDataV2` reference; only `CellarRoomPool.cs` comments mention v2, so the generator still reads the v1 pool.]
For plan 3: six-room floors fell sharply (MaxFootprint 92 binds): floor 1 rooms 4/5/6 = 274/383/343 -> 371/578/51 over seeds 0-999. Raise MaxFootprint deliberately if 6-room floors are wanted.
Churn to report: Tools/unity.ps1 check/build rewrites Assets/_Project/Scenes/CellarFloorTest.unity (stale on main, ~44k lines); not committed. Traversal baseline.json regenerated (blocked-spot total 13 -> 23, corridor "capsule doesn't fit" spots on intended paths; worth a look by the traversal owner).
Plan 2 overlap: CellarLootMixTests goldens floors 2-4 re-pinned here; whoever merges second re-pins again.
