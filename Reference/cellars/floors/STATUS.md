Session: Q12. Whole-town look pass (local PC)
Job: M5 Cellars floor generator
Progress: 35%
Archive: no

Stage 1 (pure Core, branch claude/m5-cellars-floors): `CellarFloorGenerator.Generate(seed, floor)` for floors 1-3, `PickRooms(seed, floor)`, `CellarFloorLayout` (hashes, ASCII dump) and `CellarFloorValidator`, all in Assets/_Project/Core/Cellars. Floor 4 is a fixed placeholder stub (entry, press hall, stair hall, chicane) until the bakery oven-maze step.
- Tests: Tests/EditMode/CellarFloorGeneratorTests.cs, 11 tests. Same (seed, floor) gives the same hash; 10,000 seeds x floors 1-3 all validate (about 20 s in the editor); repetition metric over 1,000 seeds (no room twice, no combat room shared by consecutive floors, floors 1-3 give 3 distinct footprints and 3+ room silhouette tags); locked share measured 0.501, own sub-theme share 0.693.
- Golden: seed 12345, floor 1, LayoutHash = 7160980227004277501.
- Checkpoint is a room ON floor 2 (the lamp nook), so floor 2 has one combat room fewer for the same 4-6 total. Floor silhouette = FNV hash of the occupied-cell footprint (non-solid room cells + corridor cells).
- Locked room: lockable combat room placed last as a leaf (only its L door linked); key in a K chest of another room; carrier = random spawn of a non-locked combat room.
- Choices beyond the brief: margin cells off the grid count as clear (rooms may touch the edge); a loop may end on an existing corridor cell (rooms have 2 doors, so door-to-door loops are scarce); PickRooms rejects room sets with too few spare doors or a footprint over 92 cells (the 6-big-room floors could not be routed); the locked coin is flipped once.
- Not done: floor 4 bakery set-piece, Editor greybox builder use of layouts, PlayMode two-client check.
- Review fix (branch m5-stage1-fix): Generate never throws. Retry 0 is the original stream (goldens unchanged); on exhaustion PickRooms/placement retry over derived streams `Rng.Derive(seed, "cellars.retry", floor<<32 | n)`, then fall back to `MinimalPlan` (2-3 smallest unlocked combat rooms) via `GenerateLastResort`; `Generate(seed, floor, out fallback)` reports 0/1/2. Over 20,000 Pcg32 seeds x floors 1-4 no seed needed either fallback.
- New tests: edge seeds (0, 1, uint.Max, int.Min/Max), 20,000-seed sweep, last-resort validity/determinism, golden seed 12345 floors 2 and 3, and four validator mutations (exit behind lock, second link on locked room, key chest in locked room, carrier behind lock).
