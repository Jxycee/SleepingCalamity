# Bakery (ring 1, L class): sources

- Generator: `Art/Blender/town/sc_bakery.py` (procedural, reproducible from text; builds on the `sc_facade_kit.py` helpers).
- Cloud export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_bakery.py -- --out <scratch> --export`; `<scratch>/Bakery.fbx` is copied here.
- `Bakery.fbx` hierarchy (root `Bakery`), 44,774 tris in all:
  | Object | Tris | What |
  |---|---|---|
  | `Frame` | 40,226 | walls, roof, oven annex with the empty niche, chimney, sign bracket, shop window and bread shelf, woodpile, yard stepping stones |
  | `Part_Door` | 1,558 | planked green front door; pivot = `SOCKET_Door` |
  | `Part_Oven` | 656 | brick oven dome, mouth with ember bed (`BK_Ember`, emissive), iron door, peel; pivot = `SOCKET_Oven` |
  | `Part_Sign` | 2,334 | oval sign board (loaf, wheat), gilt pretzel, chains; pivot = `SOCKET_Sign` |
  | `SOCKET_Door`, `SOCKET_Oven`, `SOCKET_Sign` | empties | children of `Frame`, at each part's attach point (door: threshold centre in the front wall plane) |
  | `FOOTPRINT` | 12 | flat box, 9.62 x 6.84 m (inside the L class's 10.5 m), the collider / NPC-block shape; hide its renderer |
  Parts are modelled in place, so each part's pivot coincides with its socket; P28: the bakery counts when all three are placed.
- Axes: -Z forward, Y up, metres, transforms baked. Pivot = foundation centre at ground (11 x 11 m L foundation). The door side is Blender -Y, which is **local +Z** in Unity (like the facade and stall): to put the door on the lot's local -Z side the builder adds yaw 180. Whole asset fits x -5.43..5.33, z -4.42..4.99 (roof eaves and yard inside the foundation).
- **No unique texture maps, by design (T2).** Materials sample shared tileables on UV0 (metre UVs, 2 m per tile, 2K):
  FK_Plaster, BK_Limewash, BK_Bread, BK_Crust, BK_Flour, BK_Gilt `B1_town/plaster` (tinted); FK_Oak, FK_OakDoor, BK_Wicker `B1_town/oak_timber`; FK_Sandstone, FK_ChimneyStone `B1_town/sandstone`; FK_RoofTile `B1_town/roof_tile`; FK_Shutter, FK_DoorPaint `B1_town/painted_wood`; FK_Iron `B1_town/iron`; BK_Sack, BK_Cloth `B1_town/canvas`; BK_Brick `B2_cellar/old_brick`; BK_Soot `B2_cellar/soot_coal`. Tints are in the generator (`EXTRA`, `FK.M`). FK_Glass, FK_Leaf, FK_Bloom, BK_Interior are flat colours; BK_Ember is a flat emissive (orange, strength 6 in Blender).
- Placed wear lives in the CORNER colour attribute `wear` (exported LINEAR, 0-1): R grime/soot, G moss, B polish/flour, as the facade kit; the URP material needs the kit's vertex-mask (WearLit) shader to show it.
- Sources: the B1 and B2 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`, `Art/Textures/B2_cellar/SOURCES.md`). No other external assets. Reference photos (study only): `Reference/hollowmere/bakery/REFS.md`.
