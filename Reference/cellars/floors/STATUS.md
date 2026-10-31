Session: Q12. Whole-town look pass (local PC)
Job: M5 Cellars floor generator
Progress: 100%
Status: DONE (stages 1-3)
Archive: ready

Stage 1 (pure Core, branch claude/m5-cellars-floors): `CellarFloorGenerator.Generate(seed, floor)` for floors 1-3, `PickRooms(seed, floor)`, `CellarFloorLayout` (hashes, ASCII dump) and `CellarFloorValidator`, all in Assets/_Project/Core/Cellars. Floor 4 is a fixed placeholder stub (entry, press hall, stair hall, chicane) until the bakery oven-maze step.
- Tests: Tests/EditMode/CellarFloorGeneratorTests.cs. Same (seed, floor) gives the same hash; 10,000 seeds x floors 1-3 all validate (about 20 s in the editor); repetition metric over 1,000 seeds (no room twice, no combat room shared by consecutive floors, floors 1-3 give 3 distinct footprints and 3+ room silhouette tags); locked share measured 0.501, own sub-theme share 0.693.
- Golden: seed 12345, floor 1, LayoutHash = 7160980227004277501.
- Checkpoint is a room ON floor 2 (the lamp nook), so floor 2 has one combat room fewer for the same 4-6 total. Floor silhouette = FNV hash of the occupied-cell footprint (non-solid room cells + corridor cells).
- Locked room: lockable combat room placed last as a leaf (only its L door linked); key in a K chest of another room; carrier = random spawn of a non-locked combat room.
- Choices beyond the brief: margin cells off the grid count as clear (rooms may touch the edge); a loop may end on an existing corridor cell (rooms have 2 doors, so door-to-door loops are scarce); PickRooms rejects room sets with too few spare doors or a footprint over 92 cells (the 6-big-room floors could not be routed); the locked coin is flipped once.
- Review fix (branch m5-stage1-fix): Generate never throws. Retry 0 is the original stream (goldens unchanged); on exhaustion PickRooms/placement retry over derived streams `Rng.Derive(seed, "cellars.retry", floor<<32 | n)`, then fall back to `MinimalPlan` (2-3 smallest unlocked combat rooms) via `GenerateLastResort`; `Generate(seed, floor, out fallback)` reports 0/1/2. Over 20,000 Pcg32 seeds x floors 1-4 no seed needed either fallback.
- New tests: edge seeds (0, 1, uint.Max, int.Min/Max), 20,000-seed sweep, last-resort validity/determinism, golden seed 12345 floors 2 and 3, and four validator mutations (exit behind lock, second link on locked room, key chest in locked room, carrier behind lock).

Stage 2 (greybox builder, 2026-10-31): `Presentation/Cellars/CellarFloorBuilder.cs` (runtime, no editor API, deterministic: floor slab per occupied cell, walls on blocked edges, door frames with a locked gate, pillars, lamps, chest/key chest/carrier/entry/spawn/stair markers, colliders) driven by a `CellarFloorKit`; Editor `CellarFloorTestBuilder` (wired into ProjectBuilder.RunAll) writes `Scenes/CellarFloorTest.unity` for seed 12345 floor 1 (94 floor cells, 99 walls, 7 open doors, 18 lamps, SpawnPoint at the entry); `CellarFloorCapture` writes the JPGs.
- B5/B11 pieces used: CellarVault `FloorTile` (four per cell), B11 CellarLantern on the lamps, CV_Flagstone/Brick/VaultStone/Oak WearLit materials, the dungeon corner's `DungeonCorner_Profile` volume, trilight ambient (intensity 0.7 here, 0.23 in the corner), fog. Walls, door frames and pillars are primitives (VaultSegment/ArchRib do not fit a 4 m cell).
- Captures: Reference/cellars/floors/floor_overview.jpg and floor_room.jpg (1920x1080). Palette (k 7): overview value p10/p50/p90 0.09/0.09/0.28 -> 0.09/0.20/0.36, warmth +0.13 -> +0.15; room 0.09/0.09/0.27 -> 0.09/0.16/0.41, warmth +0.12 -> +0.16 (light pass 2026-10-31: warm umber camera backdrop instead of the black void, ambient 0.7 -> 1.6, fog 0.007 -> 0.004 warmer, lamps #d6a24a). Both still sit under the Cellars band (0.12-0.16/0.30-0.40/0.65-0.75, warmth +0.20..+0.30). Lamps (16 on wall lanterns, 14 centre, 8 corridor) are far brighter than the corner's to read at all.
- Tests: Tests/EditMode/CellarFloorBuilderTests.cs over 7 (seed, floor) cases, floor 4 stub included (tiles per cell, door openings and walls checked with physics, marker counts, entry walkable + identical rebuild).
- Greybox accepted (coordinator): the layout reads; p50 rose mostly from the warm camera backdrop (no ceiling), the room's own lighting is still effectively unlit.
- Rough: no ceiling or vault (the overview needs it open), corridors are full 4 m halls, no props or dressing sockets, tile joints read as black lines, lighting flat and dim.

Stage 3 (netcode, 2026-10-31): `Presentation/Cellars/CellarFloorSync.cs` (NetworkBehaviour, prefab `Prefabs/CellarFloorSync.prefab` in the NetworkPrefabs list via PrefabBuilder). Host-authoritative: one host-written `NetworkVariable<DiveFloor>` holds (uint seed, floor 0-4) together (atomic, so late joiners get it with the spawn and no peer builds a mixed pair); every peer including the host runs CellarFloorGenerator + CellarFloorBuilder locally when the pair changes (floor 0 clears). `ServerSetFloor` refuses on a client. Host disconnect: NGO shuts the session down, the sync object despawns and the floor built under it is destroyed (no host migration in Phase 1).
- Tests: Tests/PlayMode/CellarFloorNetworkTests.cs, host + 2 clients (second joins after the seed was set): seeds 12345 and 0xC0FFEE12 x floors 1,2; LayoutHash equal on every peer and equal to the generator (and the 12345/f1 golden), built tile/wall/door/pillar/lamp counts and per-group child counts equal, one build per change on the host and the clients, floor 0 clears `Built` and destroys the old root on all 3 peers. Not covered: relay latency, floor inside a scene load (manual 2-player walk).
- Not wired yet: nothing in the game calls ServerSetFloor (the dive flow picks the seed in a later stage).

## Next steps
- Banking at checkpoint/town (host-held shared haul).
- Stir meter (0-100, thresholds 40/70/100).
- Floor-4 bakery oven-maze set-piece replacing the fixed stub (and a floor-4 PlayMode case).
- Dive flow that picks the seed and calls `ServerSetFloor`.
- Floor art pass: ceiling/vault, lamp tuning, dressing via sockets (greybox room lighting is still effectively unlit).
- 2-player walk of floors 1-4 (M5 acceptance), including relay latency.
- The user's three-run repetition check (M7).
