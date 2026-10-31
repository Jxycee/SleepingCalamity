# Workshop with lean-to (home kit family 4): sources

Generator: `Art/Blender/town/sc_workshop.py` (procedural, reproducible from text; imports the B4 facade kit `sc_facade_kit.py` as a library).
Export (2026-10-31, cloud, Blender 4.5.3), once per variant, each `<Variant>.fbx` copied here unchanged:

    blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_workshop.py -- --out Assets/_Project/Content/Art/Town/Ring1/Workshop/<Variant> --variant <Variant> --export --no-render

(`--variant all` exports the three into one folder.) FBX: -Z forward, Y up, `FBX_SCALE_UNITS`, baked space transform, face smoothing, linear colours, `path_mode='STRIP'` (no texture paths in the file).

## Variants

| Folder | Class | Tris (Frame / Part_Door / Part_Shutters) | Total | FOOTPRINT (w x d, m) | Look |
|---|---|---|---|---|---|
| `Workshop_A` | S (foundation 6 x 6, body inside 5 x 5) | 16,806 / 1,558 / 1,728 | 20,092 | 4.83 x 4.34 | tile roof, natural weathered boards, sage shutters and door, stove pipe, chopping block; lean-to on +X |
| `Workshop_B` | M (8 x 8, body inside 7.5 x 7.5) | 21,546 / 1,558 / 2,880 | 25,984 | 6.73 x 6.30 | board roof, ochre-painted battened boards, oxblood shutters and door, sandstone stack on the back wall; lean-to on +X |
| `Workshop_C` | M | 21,054 / 1,558 / 2,304 | 24,916 | 6.95 x 5.28 | tile roof over a board lean-to roof, tarred dark boards, slate-blue shutters and door, stove pipe with a hood; lean-to on -X |

Budgets: S under 30k, M under 35k. Roofs (eaves, verges, lean-to eave) stay inside the 6 / 8 m foundation (asserted by the generator); FOOTPRINT is inside its class.

## Contract (mass-production spec 5.2)

Root `<Variant>` (empty at the pivot) with children:
- `Frame`: everything except the parts (posts, plates, boards, plinth, roof, lean-to with its log and plank stacks, chimney, the fittings inside the open bay: floor, threshold, workbench with vise and tool rack, sawhorse, grindstone, barrel, offcuts, roof trusses, a drum stove in A and C). Bare iron hinge pintles sit at the side doorway, the loft hatch and every window.
- `Part_Door`: the planked man door with iron strap hinges and ring pull (the facade kit's door). Object origin = `SOCKET_Door`.
- `Part_Shutters`: every shutter (loft hatch, side-wall and back-wall windows), standing open against the wall. Object origin = `SOCKET_Shutters`, the centre of the loft hatch in the front gable; all shutters sit at their real positions relative to it.
- `SOCKET_Door`, `SOCKET_Shutters`: empties at the part origins.
- `FOOTPRINT`: flat 0.1 m box over the building body including the lean-to (and the stack on B); collider and NPC-block shape. Not rendered.

Axes and pivot: metres, Blender z up, front = Blender -Y (local +Z in Unity, builder yaw as for the facade). Pivot = foundation centre at ground, the body (main shed plus lean-to) centred on it (the lean-to side is +X for A and B, -X for C in Blender axes; the FBX exporter turns the axes, the empties and meshes stay consistent).

**The front is the OPEN BAY and never closes.** `Part_Door` is not on the front: it is the man door in the side wall AWAY from the lean-to, near the front end (A and B: the -X wall, C: the +X wall), so `SOCKET_Door` is on that side wall (door face outward normal along the side axis, at the threshold). Without the door the frame shows a dark-lined doorway with bare pintles and a worn step; without the shutters the loft hatch and windows show bare casings and pintles. The builder faces the building's front (Blender -Y) to the lot's lane side, independent of `SOCKET_Door`.

Re-import audit (Blender 4.5.3, all three FBX): every closed island in Frame, Part_Door and Part_Shutters has outward normals (0 inward); every mesh except `FOOTPRINT` carries the `wear` colour attribute; children and slot names as below.

## Materials and slots (T2: shared tileables, no unique maps)

Every slot samples a shared `Art/Textures/B1_town/<tileable>` set (`_albedo`, `_normal`, `_orm`, 2K) with metre UVs on `UVMap` (2 m per tile). Tints are linear multipliers on the albedo; "paint" slots use the facade variants' paint mode: greyscale(albedo) x paint colour / 0.17 (linear). The Unity side rebuilds each material from this table with the same `wear` blend as the kit (R grime, G moss, B polish).

| Slot | Tileable | Linear tint / paint | Used on |
|---|---|---|---|
| `FK_Oak` | `oak_timber` | tint (0.64, 0.60, 0.56) (darker than the facade's 0.80, 0.76, 0.72 so the frame reads against the boards), normal 1.2 | posts, plates, braces, rafters, lintel, trusses |
| `FK_OakDoor` | `oak_timber` | tint (1.35, 1.25, 1.15) | pale sawn wood: workbench top, planks, logs, offcuts, one bare door plank |
| `FK_Sandstone` | `sandstone` | tint (1, 1, 1) | plinth stones, threshold |
| `FK_PlinthJoint` | `sandstone` | tint (0.30, 0.28, 0.24) | recessed plinth mortar core |
| `FK_ChimneyStone` | `sandstone` | tint (0.80, 0.74, 0.68) | B's stack, grindstone |
| `FK_RoofTile` | `roof_tile` | tint (1, 1, 1), normal 1.3 | tile roofs (A, C main, B's stack pots) |
| `FK_Iron` | `iron` | tint (1, 1, 1) | hinges, pintles, tools, stove, pipe, rings |
| `FK_Glass` | none (flat) | base (0.012, 0.016, 0.02), roughness 0.12 | window panes |
| `FK_Shutter` (Part_Shutters) | `painted_wood` | paint: A `#4a6a48` = linear (0.0685, 0.1441, 0.0648); B `#6a2e29` = (0.1441, 0.0273, 0.0222); C `#3e5670` = (0.0482, 0.0931, 0.1620) | shutters |
| `FK_DoorPaint` (Part_Door) | `painted_wood` | same paint colour as the variant's shutters | door planks |
| `WS_Board` | `oak_timber` | A tint (2.2, 2.0, 1.75) weathered natural; C tint (1.25, 1.12, 1.0) tarred dark; normal 1.1. Not present on B | vertical cladding, gables, lean-to boards (A, C) |
| `WS_BoardPaint` | `painted_wood` | paint `#9c7f4c` = linear (0.3325, 0.2122, 0.0723), normal 0.9. Only on B | B's cladding |
| `WS_RoofBoard` | `oak_timber` | B tint (1.05, 0.95, 0.85); C tint (0.85, 0.78, 0.70); normal 1.3. B main and lean-to roof, C lean-to roof | board roofs, B ridge cap |
| `WS_Floor` | `sandstone` | tint (0.52, 0.48, 0.43) | floor slab inside the bay |
| `WS_Inner` | `oak_timber` | tint (0.78, 0.70, 0.63) | wall linings, roof and gable undersides (seen through board gaps and the open bay) |
| `FOOTPRINT` | none | collider only, hide the renderer | FOOTPRINT |

Slots present per object (re-import): A Frame `FK_Oak, WS_Board, WS_Inner, FK_Iron, FK_Glass, FK_PlinthJoint, FK_Sandstone, FK_RoofTile, FK_OakDoor, WS_Floor, FK_ChimneyStone`; B Frame the same with `WS_BoardPaint` for `WS_Board` plus `WS_RoofBoard`; C Frame as A plus `WS_RoofBoard`; Part_Door `FK_Oak, FK_Iron, FK_OakDoor, FK_DoorPaint`; Part_Shutters `FK_Iron, FK_Shutter`.

`wear` (linear 0-1, per corner): R grime (splashback at the foot, shade band under the eaves, rain streaks down the boards, roof run-off and tile-to-tile variation, soot above the roof line on pipes and the stack, chipped paint on door and shutters), G moss (plinth tops, roof eave courses, the lean-to roof), B polish (sawn pale wood, door edge by the ring, the worn threshold, sun-bleached timber tops). Amount scales per variant (1.1 to 1.35).

## Licences

The B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`). Geometry is generated; no other external assets. Reference photos used for the review sheet are listed in `Reference/hollowmere/kits/REFS.md` (study only, never shipped).
