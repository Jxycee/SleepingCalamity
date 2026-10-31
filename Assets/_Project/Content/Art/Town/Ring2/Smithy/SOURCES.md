# Smithy (ring 2, M class): sources

- Generator: `Art/Blender/town/sc_smithy.py` (procedural, reproducible from text; imports the bakery's helpers, which build on `sc_facade_kit.py`).
- Cloud export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_smithy.py -- --out <dir> --no-render --export`; `Smithy.fbx` is written here. The export strips texture paths (`path_mode='STRIP'`) so Unity keeps the material names.
- `Smithy.fbx` hierarchy (root `Smithy`), 31,524 tris in all (FOOTPRINT excluded):
  | Object | Tris | What |
  |---|---|---|
  | `Frame` | 28,450 | workshop walls, hipped roof with the catslide over the open forge bay, stone half walls and boards, flagged floor, hearth and soot-lined chimney breast (cold, swept), tall brick stack with iron stays, weathervane, sign bracket with two empty hooks, double door (one leaf ajar), loft hatch and crane arm, quench trough, tool rack, grindstone, coal bin, scrap heap, rain barrel, bare anvil-stump mark (charred ring) |
  | `Part_Forge` | 1,750 | fire bed (coal, `BK_Ember` glow, brick rim, tuyere), bellows, sheet-iron hood, poker and tongs; pivot = `SOCKET_Forge` |
  | `Part_Anvil` | 572 | oak stump, anvil, hammer, bar; pivot = `SOCKET_Anvil` |
  | `Part_Sign` | 752 | green board with gilt anvil and iron horseshoe, chains; pivot = `SOCKET_Sign` |
  | `SOCKET_Forge` (1.099, 0.600, 0.895), `SOCKET_Anvil` (2.300, -0.600, 0.000), `SOCKET_Sign` (1.598, -2.761, 2.530) | empties | children of `Frame`, Blender coordinates (x, y, z); Forge = hearth slab centre, Anvil = floor under the stump, Sign = the hooks' midpoint |
  | `FOOTPRINT` | 12 | flat box, 7.20 x 5.60 m (inside the M class's 7.5 m), the collider / NPC-block shape; hide its renderer |
  Parts are modelled in place, so each part's pivot coincides with its socket.
- Axes: -Z forward, Y up, metres, transforms baked. Pivot = foundation centre at ground (8 x 8 m M foundation). The front (open bay, door, sign) is Blender -Y, which is **local +Z** in Unity: the builder adds yaw 180 to face the lot's -Z side. Whole asset fits x -3.95..3.80, y -3.92..3.65, z up to 8.47 (stack top).
- **No unique texture maps (T2).** Materials sample shared tileables on UV0 (metre UVs, 2 m per tile, 2K):
  FK_Plaster `B1_town/plaster`; FK_Oak, FK_OakDoor `B1_town/oak_timber`; FK_Sandstone, FK_ChimneyStone `B1_town/sandstone`; FK_RoofTile `B1_town/roof_tile`; FK_Shutter, FK_DoorPaint `B1_town/painted_wood`; FK_Iron `B1_town/iron`; BK_Sack `B1_town/canvas`; BK_Gilt `B1_town/plaster` (tinted); BK_Brick `B2_cellar/old_brick`; BK_Soot `B2_cellar/soot_coal`.
  New in this asset: BK_Leather `B1_town/canvas` tint (0.50, 0.30, 0.18); BK_Coal `B2_cellar/soot_coal` tint (0.55, 0.55, 0.58), normal 1.3; BK_Ash `B2_cellar/soot_coal` tint (2.6, 2.5, 2.4), normal 0.4; BK_Grindstone `B1_town/sandstone` tint (0.85, 0.85, 0.88); BK_Water flat colour (0.025, 0.04, 0.03), roughness 0.06 (opaque, murky; a water shader may replace it).
  FK_Glass, BK_Interior, BK_Water are flat colours; **BK_Ember is the only emissive slot** (the fire bed, on `Part_Forge`; flat orange, strength 6 in Blender).
- Placed wear lives in the CORNER colour attribute `wear` (exported LINEAR, 0-1): R grime/soot (hearth wall, stack top, roof round the shaft), G moss (plinth, back wall, catslide, trough), B polish (door step, anvil face, hammer, gilt); the URP material needs the kit's vertex-mask (WearLit) shader to show it.
- Sources: the B1 and B2 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`, `Art/Textures/B2_cellar/SOURCES.md`). No other external assets. Reference photos (study only): `Reference/hollowmere/kit_ready/smithy/REFS.md`.
