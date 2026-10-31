# Townhouse (home kit family 3, tall jettied townhouse, M class): sources

- Generator: `Art/Blender/town/sc_townhouse.py` (procedural, reproducible from text). It imports `Art/Blender/town/sc_facade_kit.py` (B4) as its kit library: timbers, windows, shutters, door, roof, chimney, flower box, plinth stones, lean-to, wear writer and materials.
- Export (cloud, 2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_townhouse.py -- --out <scratch> --export`; `<scratch>/<Variant>.fbx` is copied here as `<Variant>/<Variant>.fbx`.

| Folder | Seed | Shape | Body footprint (x by y) | Tris |
|---|---|---|---|---|
| `Townhouse_A` | 0x7A11 | 5.25 m wide, gable to the street, 0.45 m jetties, cool lime white, slate-blue shutters, chimney breast on one side, flower box | 5.45 x 6.85 m | 34,265 |
| `Townhouse_B` | 0x7B22 | 5.51 m wide, eave to the street (side gables), 0.55 m jetties, warm cream, plum shutters | 5.71 x 6.19 m | 32,346 |
| `Townhouse_C` | 0x7C33 | 4.93 m wide, gable to the street, 0.57 m jetties, log-store lean-to, twisted upper storeys, ochre wash, sage-green shutters | 5.13 x 6.89 m | 34,531 |

- Children of each root (spec 2026-10-30 section 5.2): `Frame` (every kit piece joined, multi-material), `Part_Door` (origin = `SOCKET_Door`), `Part_Shutters` (every shutter, origin = `SOCKET_Shutters`), empties `SOCKET_Door` and `SOCKET_Shutters`, `FOOTPRINT` (a 0.1 m flat box over the building body; the builder hides it and makes it the collider). All four sides are detailed (four turns).
- Axes: metres, -Z forward, Y up, transforms baked. Pivot: foundation centre at ground (the body is centred on it). The front comes in as Unity local +Z (B4 convention): `HollowmereTownhouse` turns the art 180 so it faces the lot's door side.
- **No unique texture maps (tier T2).** The B4 facade material slots (FK_Plaster, FK_Oak, FK_OakDoor, FK_Sandstone, FK_PlinthJoint, FK_ChimneyStone, FK_RoofTile, FK_Shutter, FK_DoorPaint, FK_Iron, FK_Glass, FK_Leaf, FK_Bloom, FK_Decal_*) on the shared B1 tileables (graded Poly Haven scans, CC0 1.0, `Art/Textures/B1_town/SOURCES.md`) and B3 decals (procedural). The plaster tint and shutter/door paint per variant are applied in Unity (`HollowmereTownhouse.Looks`, WearLit paint mode), as for the B4 neighbours.
- Placed wear: the CORNER colour attribute `wear`, exported LINEAR (R grime/soot, G moss, B polish), read by the WearLit shader.
- No external assets.
