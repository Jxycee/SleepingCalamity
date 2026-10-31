Session: Q12. Whole-town look pass (local PC)
Job: M5 Cellars floor generator
Progress: 65%
Archive: no

Stage 1 (pure Core, branch claude/m5-cellars-floors): `CellarFloorGenerator.Generate(seed, floor)` for floors 1-3, `PickRooms(seed, floor)`, `CellarFloorLayout` (hashes, ASCII dump) and `CellarFloorValidator`, all in Assets/_Project/Core/Cellars. Floor 4 is a fixed placeholder stub (entry, press hall, stair hall, chicane) until the bakery oven-maze step.
- Tests: Tests/EditMode/CellarFloorGeneratorTests.cs, 11 tests. Same (seed, floor) gives the same hash; 10,000 seeds x floors 1-3 all validate (about 20 s in the editor); repetition metric over 1,000 seeds (no room twice, no combat room shared by consecutive floors, floors 1-3 give 3 distinct footprints and 3+ room silhouette tags); locked share measured 0.501, own sub-theme share 0.693.
- Golden: seed 12345, floor 1, LayoutHash = 7160980227004277501.
- Checkpoint is a room ON floor 2 (the lamp nook), so floor 2 has one combat room fewer for the same 4-6 total. Floor silhouette = FNV hash of the occupied-cell footprint (non-solid room cells + corridor cells).
- Locked room: lockable combat room placed last as a leaf (only its L door linked); key in a K chest of another room; carrier = random spawn of a non-locked combat room.
- Choices beyond the brief: margin cells off the grid count as clear (rooms may touch the edge); a loop may end on an existing corridor cell (rooms have 2 doors, so door-to-door loops are scarce); PickRooms rejects room sets with too few spare doors or a footprint over 92 cells (the 6-big-room floors could not be routed); the locked coin is flipped once.
- Not done: floor 4 bakery set-piece, Editor greybox builder use of layouts, PlayMode two-client check.

Stage 2 (greybox builder, 2026-10-31): `Presentation/Cellars/CellarFloorBuilder.cs` (runtime, no editor API, deterministic: floor slab per occupied cell, walls on blocked edges, door frames with a locked gate, pillars, lamps, chest/key chest/carrier/entry/spawn/stair markers, colliders) driven by a `CellarFloorKit`; Editor `CellarFloorTestBuilder` (wired into ProjectBuilder.RunAll) writes `Scenes/CellarFloorTest.unity` for seed 12345 floor 1 (94 floor cells, 99 walls, 7 open doors, 18 lamps, SpawnPoint at the entry); `CellarFloorCapture` writes the JPGs.
- B5/B11 pieces used: CellarVault `FloorTile` (four per cell), B11 CellarLantern on the lamps, CV_Flagstone/Brick/VaultStone/Oak WearLit materials, the dungeon corner's `DungeonCorner_Profile` volume, trilight ambient (intensity 0.7 here, 0.23 in the corner), fog. Walls, door frames and pillars are primitives (VaultSegment/ArchRib do not fit a 4 m cell).
- Captures: Reference/cellars/floors/floor_overview.jpg and floor_room.jpg (1920x1080). Palette (k 7): overview value p10/p50/p90 0.09/0.09/0.28, warmth +0.13; room 0.09/0.09/0.27, warmth +0.12. p50 is the black void and fog, so both sit under the Cellars band (0.12-0.16/0.30-0.40/0.65-0.75, warmth +0.20..+0.30). Lamps (16 on wall lanterns, 14 centre, 8 corridor) are far brighter than the corner's to read at all.
- Tests: Tests/EditMode/CellarFloorBuilderTests.cs, 4 tests over 6 (seed, floor) cases (tiles per cell, door openings and walls checked with physics, marker counts, entry walkable + identical rebuild).
- Rough: no ceiling or vault (the overview needs it open), corridors are full 4 m halls, no props or dressing sockets, tile joints read as black lines, lighting flat and dim.
