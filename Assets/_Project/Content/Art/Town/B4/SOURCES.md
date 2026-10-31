# B4 facade kit: sources

## Facade (`Facade/`)
- Generator: `Art/Blender/town/sc_facade_kit.py` (procedural, reproducible from text).
- PC export (2026-10-30, Blender 5.2.2): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_facade_kit.py -- --out <scratch> --export`; `<scratch>/FacadeKit.fbx` is copied here as `Facade.fbx`.
- `Facade.fbx`: the assembled facade, one object per kit piece (Kit_Plinth, Kit_Posts, Kit_Rails, Kit_Braces, Kit_JettyBeams, Kit_Panels, Kit_Windows, Kit_Shutters, Kit_Door, Kit_RoofModule, Kit_Chimney, Kit_FlowerBox, Kit_LeanTo, Kit_Decal_*) under the `FacadeKit` root. 37,500 tris (round 3 PC export 2026-10-30: facade rounds 11-14; round 14 fixes the inside-out timber frame, verified on re-import: every closed Posts/Rails/Braces/JettyBeams part has outward normals, where the round-2 export had all of them inward; lean-to now on the -X end).
  -Z forward, Y up, metres, transforms baked. Pivot: centre of the ground-storey front plaster face at ground level (Unity placement (-1.25, 0, 7.5)). The front is Blender -Y, which is local +Z in Unity, like the stall: to face -Z as the brief wants, the builder places it with yaw 180.
- **No unique texture maps, by design (tier T2).** Each piece samples a shared tileable on UV0 (metre UVs, 2 m per tile) through its material:
  FK_Plaster `B1_town/plaster`, FK_Oak + FK_OakDoor `B1_town/oak_timber`, FK_Sandstone + FK_ChimneyStone `B1_town/sandstone`, FK_RoofTile `B1_town/roof_tile`,
  FK_Shutter + FK_DoorPaint `B1_town/painted_wood` (green tints in the generator: SHUTTER_TINT, DOOR_TINT), FK_Iron `B1_town/iron`, FK_Decal_* `B3_trims/{crack,drip,moss}` (alpha decals).
  FK_Glass, FK_Leaf and FK_Bloom are flat procedural colours (leaf and bloom are placeholders until B9 foliage).
- Placed wear lives in the CORNER colour attribute `wear` (exported LINEAR, 0-1): R grime/soot, G moss, B polish. The URP material needs the kit's vertex-mask shader to show it.
- Sources: the B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`); the B3 decals are procedural (`Art/Textures/B3_trims/SOURCES.md`). No other external assets.

## Neighbours (`Neighbour_Left/`, `Neighbour_Right/`, `Neighbour_Infill/`)
- Same generator with `--variant`: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_facade_kit.py -- --out <scratch> --export --variant <NAME>`; `<scratch>/<NAME>.fbx` is copied as `<Folder>/<Folder>.fbx`.
  | Folder | Variant | Tris | Shape |
  |---|---|---|---|
  | `Neighbour_Left` | `NEIGHBOUR_Left` | 36,844 | 7.5 m, 3 storeys, eave front, ochre shutters, warm cream plaster |
  | `Neighbour_Right` | `NEIGHBOUR_Right` | 30,443 | 8 m, 2 storeys, gable to the plaza, oxblood shutters, faint rose plaster |
  | `Neighbour_Infill` | `INFILL_Narrow` | 22,438 | 4.5 m, 3 storeys, eave front, faded blue-green shutters, cool lime white |
- Backdrop houses: plaza front detailed, sides, back and rear gable plain. Same kit pieces, materials, tileables and linear `wear` attribute as the facade; no unique maps (T2).
- Pivot: centre of the ground-storey front plaster face at ground level (x symmetric about the front width, z 0). Front is Blender -Y = local +Z in Unity: yaw 180 to face -Z, like the facade. Timber normals verified outward on re-import.
- Shutter and door paint on the variants is greyscale(painted_wood albedo) x paint colour / 0.17 (linear), so the URP shader needs that paint mode (or a baked albedo); the hero facade keeps the tinted scan.