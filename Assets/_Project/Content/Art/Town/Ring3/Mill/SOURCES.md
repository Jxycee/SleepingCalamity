# Mill (ring 3, fixed landmark, two states): sources

- Generator: `Art/Blender/town/sc_mill.py` (procedural, reproducible from text; builds on the `sc_facade_kit.py` helpers).
- Export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_mill.py -- --out <scratch> --export`; `<scratch>/Mill.fbx` is copied here. Texture paths are stripped (`path_mode='STRIP'`) so Unity keeps the real slot names.
- `Mill.fbx` hierarchy (root `Mill`), Shell 24,784 tris (cap 30,000); Frame + parts 31,532 tris (cap 45,000):
  | Object | Tris | What |
  |---|---|---|
  | `Shell` | 24,784 | ruined start state: ragged double-skinned stone ground storey, broken timber upper storey with torn plaster and hanging shutters, half the roof fallen, bare rafters, broken flume on a fallen bent, broken tilted wheel (own geometry), rubble heaps, fallen timbers |
  | `Frame` | 27,404 | restored state: stone ground storey, timber-framed upper storey with green shutters, tiled roof (ridge tiles, rolled eave, barge boards), hoist hood on the -X gable, wheel pit with cheek walls, flume on trestles, two millstones, door-step stones; empty doorway and empty pit without the parts |
  | `Part_Wheel` | 3,588 | 4.3 m paddle wheel, axle along Y; pivot = `SOCKET_Wheel` at the axle (5.70, 0.00, 1.75) |
  | `Part_Door` | 540 | planked green front door with iron straps; pivot = `SOCKET_Door` at the threshold centre (-2.20, -3.50, 0.22) |
  | `SOCKET_Wheel`, `SOCKET_Door` | empties | children of `Frame` |
  | `FOOTPRINT` | 12 | flat box 9 x 7 x 0.1 m over the building body only (yard and wheel excluded); hide its renderer |
  `Shell` and `Frame` share the pivot and the footprint: show one or the other (never both); `Part_*` belong to the Frame state only (the Shell has its own broken wheel baked in).
- Axes: -Z forward, Y up, metres, transforms baked. Pivot = footprint centre at ground. The door side is Blender -Y, which is **local +Z** in Unity. Whole asset (both states, wheel, roof, yard) fits x -5.46..8.00, y -4.96..4.36, z -0.56..12.66 (wheel and flume on the +X stream side; wheel lowest point z -0.38 above the pit floor at -0.55).
- **No unique texture maps (T2).** Materials sample shared tileables on UV0 (metre UVs): FK_Plaster `B1_town/plaster`; FK_Oak, ML_OakWeathered, ML_WheelOak `B1_town/oak_timber` (tinted); FK_Sandstone, FK_ChimneyStone, ML_Rubble, ML_Millstone `B1_town/sandstone` (tinted); FK_RoofTile `B1_town/roof_tile`; FK_DoorPaint `B1_town/painted_wood`; FK_Iron `B1_town/iron`; ML_Earth `B1_town/plaster` (dark tint). FK_PlinthJoint (mortar), FK_Glass and ML_Interior (near-black room behind doorway and windows) are flat colours.
- Slots on `Frame`: ML_Rubble, FK_PlinthJoint, FK_Sandstone, ML_Interior, FK_Oak, FK_Glass, FK_Iron, FK_Plaster, FK_DoorPaint, FK_RoofTile, ML_OakWeathered, FK_ChimneyStone, ML_Millstone. `Shell` adds ML_WheelOak and ML_Earth (no glass, no millstone). `Part_Wheel`: FK_Iron, ML_WheelOak. `Part_Door`: FK_Iron, FK_DoorPaint. No emissive slots.
- Placed wear lives in the CORNER colour attribute `wear` (LINEAR, 0-1): R grime, G moss (damp cheek walls, shaded roof slope, shell), B polish (door); needs the kit's WearLit shader.
- Sources: B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`). No other external assets. Reference photos (study only): `Reference/hollowmere/mill/REFS.md`.
