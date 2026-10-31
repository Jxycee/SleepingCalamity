# Schoolhouse (ring 2, M class): sources

- Generator: `Art/Blender/town/sc_schoolhouse.py` (procedural, reproducible from text; builds on `sc_facade_kit.py` and imports helpers from `sc_bakery.py`).
- Cloud export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_schoolhouse.py -- --out <scratch> --export`; `<scratch>/Schoolhouse.fbx` is copied here. The export strips texture paths (`path_mode='STRIP'`) so Unity names slots by material, not 'Lit'.
- `Schoolhouse.fbx` hierarchy (root `Schoolhouse`), 29,480 tris in all (two runs gave the same count):
  | Object | Tris | What |
  |---|---|---|
  | `Frame` | 25,192 | limewashed walls with quoins and ground course, 8 tall windows (3 + 3 + 2 at the back, none in the front), open lined doorway, green boarded gables with round loft vents, tiled gable roof, ridge chimney, pent hood on knee brackets, bench, slate notice board, two empty iron sign hooks, empty bell-cote with bare bearing rings, yard (stepping slabs, hopscotch, water butt) |
  | `Part_Door` | 1,558 | planked green front door (`FK.kit_door`); pivot = `SOCKET_Door` |
  | `Part_Bell` | 1,962 | bronze bell, clapper, oak headstock with iron straps and gudgeons, half wheel, rope and toggle; pivot = `SOCKET_Bell` (the axle centre) |
  | `Part_Sign` | 768 | green board with gilt lettering blocks and oak edge, chains; pivot = `SOCKET_Sign` (midpoint of the two hook tips) |
  | `SOCKET_Door`, `SOCKET_Bell`, `SOCKET_Sign` | empties | children of `Frame`: Door (0.000, -2.700, 0.160), Bell (-0.068, -2.593, 9.150), Sign (-0.028, -2.943, 4.120) Blender metres (leaned) |
  | `FOOTPRINT` | 12 | flat box, 5.40 x 6.40 m (M class max 7.5 x 7.5), body incl. plinth, yard excluded; hide its renderer |
  Parts are modelled in place, so each part's pivot coincides with its socket.
- Axes: -Z forward, Y up, metres, transforms baked. Pivot = foundation centre at ground (8 x 8 m M foundation). The door side is Blender -Y, which is **local +Z** in Unity: to put the door on the lot's local -Z side the builder adds yaw 180. Whole asset fits x -3.14..3.92, y -3.89..3.95, height 10.65 m (roof, yard, hopscotch and water butt inside the foundation).
- **No unique texture maps, by design (T2).** Materials sample shared tileables on UV0 (metre UVs, 2 m per tile, 2K):
  SH_Limewash, SH_Chalk, BK_Gilt `B1_town/plaster` (tinted); FK_Oak, FK_OakDoor `B1_town/oak_timber`; FK_Sandstone, FK_ChimneyStone `B1_town/sandstone`; FK_RoofTile `B1_town/roof_tile`; FK_Shutter (the green gable boards and the sign board), FK_DoorPaint `B1_town/painted_wood`; FK_Iron, SH_Bronze `B1_town/iron` (bronze is a strong warm tint); SH_Rope `B1_town/canvas`; SH_Slate `B2_cellar/soot_coal`. Tints are in the generator (`EXTRA`). FK_Glass and SH_Interior are flat colours. No emissive slot.
- Slots in `Frame`: SH_Limewash, FK_Sandstone, FK_Oak, SH_Interior, FK_Glass, FK_Iron, FK_Shutter, FK_RoofTile, SH_Slate, SH_Chalk, FK_ChimneyStone. `Part_Door`: FK_DoorPaint, FK_OakDoor, FK_Oak, FK_Iron. `Part_Bell`: SH_Bronze, FK_Iron, FK_Oak, SH_Rope. `Part_Sign`: FK_Iron, FK_Shutter, FK_Oak, BK_Gilt.
- Placed wear lives in the CORNER colour attribute `wear` (exported LINEAR, 0-1): R grime/soot, G moss, B polish, as the facade kit; the URP material needs the kit's vertex-mask (WearLit) shader to show it.
- Sources: the B1 and B2 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`, `Art/Textures/B2_cellar/SOURCES.md`). No other external assets. Reference photos (study only): `Reference/hollowmere/kit_ready/schoolhouse/REFS.md`.
