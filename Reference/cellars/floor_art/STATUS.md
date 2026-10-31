State: WORKING
Session: Cellars room art pass: dress the greybox floor
Progress: 92% - final room capture in (lantern shadow fixed), origin/main merged (00b0439), nographics build PASS, scenes committed (134d979); check running on 134d979
Sheet: Reference/cellars/floor_art/SHEET_floor_art.jpg (round 3; captures floor_overview/room/corridor.jpg beside it)
Needs: nothing
Blocker: none
Job: dress the generated Cellars floor (CellarFloorBuilder art layer) to the approved dungeon corner look
Status: pre-merge check running
Archive: not yet
Branch: claude/laughing-bell-0zcpod

Score: round 2 25/40 -> round 3 27/40 (final room re-capture after the lantern fix: p90 .51) (silhouette 3.5, proportion 3.5, form 3.5, surface 3.5, colour 4, wear 3, set 3, emissive 3); target first + 5 = 30 not reached in the 3-round cap (round 3 gained 2)
SC RESULT: build PASS on 134d979; check running
Merge commit: -

## What landed
- `Presentation/Cellars/CellarFloorBuilder.Art.cs`: per-cell, per-room data-driven art layer. Rooms: one elliptical barrel bay per cell along the room's long axis (spring 2.6, crown 5.1, the corner's 2.0 x 2.5 profile), oak valley beams, stone cornices, limewash tympana. Straight corridors: B5 VaultSegment x2 + ArchRib (across-scale 0.725). Corridor corners/junctions: new `LookTestGeometry.Shape.GroinVault` (sharp creases, tested). Wall boxes are colliders only; every closed side draws its own face: B5 EndWall (limewash) on room end walls with a backing slab, NicheWall + candles on Rack sockets, brick elsewhere.
- Socket dressing: Shelf -> LarderShelf, Chute -> CoalChute, Barrel -> Barrels + candle, Crate -> crates, Roots -> Roots from the crown; kit piers on Pillar cells; up to 2 hashed corner props per room; candle cluster (real point light) on every chest; dust motes at wall lanterns; one warm fill per room; centre hanging lamp (chain) in rooms of 12+ cells; corridor lamps every other cell; lamps in the corner's paler gold (not #d6a24a orange).
- Test scene: DungeonCorner_Profile grade, ambient 0.8, the corner's fog; new `Floor_Corridor` camera (longest straight run); overview capture hides the Ceiling groups (cutaway); SC_CAPTURE_ONLY honoured.
- Every seed still builds (the layer reads only the layout); the stage-2 tests and the network peer test compare the same groups.

## Known
- Overview: the two far rooms render dark: the cloud's software GL path caps visible lights (about 50 lights in the cutaway); the play-height views are lit. Check on a GPU session.
- Room shots are sparse in the middle (combat floor space by design); gameplay markers are still greybox.

## Usage waste
- First capture attempts bounced twice on the setup's RUNNING flag (the background setup job died at its csproj step); ~5 min.
- Round 1 shot only room+corridor (as planned); the EndWall spandrel gap cost a debug dump of the layout.
