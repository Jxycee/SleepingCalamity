# KeepHall (keep landmark, +4 m terrace): sources

- Generator: `Art/Blender/town/sc_keep_hall.py` (procedural; builds on `sc_facade_kit.py` helpers). Export: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_keep_hall.py -- --out <scratch> --export --no-render`; `<scratch>/KeepHall.fbx` is copied here (path_mode STRIP).
- `KeepHall.fbx` hierarchy (root `KeepHall`):
  | Object | Tris | What |
  |---|---|---|
  | `Shell` | 22,706 | ruined state: west end fallen, ragged walls, bare rafters and tie beams over the east half, empty windows, no door, bellcote stub, rubble heaps |
  | `Frame` | 30,359 | rebuilt hall: rubble walls, quoins, buttresses, corbelled cornice, slate roof with coped gables, bellcote with bell, stone porch with slate hood, glazed windows (came lattice), empty arched west doorway onto a dark interior with a flag floor |
  | `Part_Door` | 624 | arched oak plank door with iron straps and ring; pivot = `SOCKET_Door` |
  | `SOCKET_Door` | empty | child of `Frame`, Blender (4.5, -0.5, 0) = Unity local (-4.5, 0, 0.5), the threshold centre in the west wall plane (the door leaf sits 0.32 m inside) |
  | `FOOTPRINT` | 12 | flat box 9 x 6 x 0.1 m; hide its renderer |
  Show `Shell` in Ruined, `Frame` + `Part_Door` in Rebuilt.
- Axes: -Z forward, Y up, metres, baked. Unity (x, y, z) = Blender (-x, z, -y): the door gable is Blender +X = Unity -X (west); the long side facing the square (Unity -Z) is Blender +Y (3 windows), the back (Unity +Z) is Blender -Y (2 windows). Bounds Frame (Blender): x -4.77..5.75, y -3.58..3.58, z -0.1..11.94 (body 9 x 6; porch to x 5.75 = 0.8 m outside the west wall, eave overhang 0.45 m, bellcote top 11.9). Shell x -4.77..6.55, y -4.48..4.36 (rubble heaps outside the body).
- Materials (slot names): FK_Sandstone `B1_town/sandstone` (dressed stone, untinted), KH_Rubble sandstone tint (0.62, 0.60, 0.57) (same as BT_Rubble), FK_PlinthJoint / FK_ChimneyStone sandstone tints (mortar, caps), KH_Slate sandstone tint (0.30, 0.36, 0.47) (roof), KH_Flag sandstone tint, KH_Limewash `B1_town/plaster` tint (inner walls), FK_Oak / KH_OakWeathered / KH_DoorOak `B1_town/oak_timber` tints, FK_Iron `B1_town/iron`, KH_Bronze iron tint (bell), FK_Glass flat. No emissive slot. Metre UVs, no unique maps.
- Wear: CORNER colour attribute `wear` (LINEAR): R grime (foot, sill run-off, soot under the cornice), G moss (plinth, low roof, back wall, Shell), B polish (door).
- Sources: B1 tileables are graded Poly Haven scans, CC0 (`Art/Textures/B1_town/SOURCES.md`). Reference photos: `Reference/hollowmere/keep/REFS.md`.
