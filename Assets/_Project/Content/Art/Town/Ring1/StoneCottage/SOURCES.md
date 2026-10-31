# StoneCottage (home kit family 2, S class)

Generator: `Art/Blender/town/sc_cottage.py` (Blender 4.5, imports `sc_facade_kit.py` as a library). Mass-production spec 5.4 #2 and
the 5.2 export contract. Three seeded variants, one FBX each, no unique bakes (T2): only the shared B1 tileables, metre UVs
(2 m per tile, 2K maps), plus the linear `wear` colour attribute.

    blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cottage.py -- --out <scratch> --variant all --export
        [--no-render] [--views fl,fr,bl,br,front] [--rres 960] [--samples 32] [--frame-only] [--audit] [--ray] [--tris]

`--audit` re-imports `<out>/<Variant>.fbx` and prints child names, slots, `wear`, tris, FOOTPRINT, a signed-volume check of every
closed island (outward normals) and, with `--ray`, a visible-face ray test. The FBX in each variant folder are copies of that export.

## Variants

| Variant | Roof | Chimney | Extras | Shutter and door paint | w x d (wall body, m) |
|---|---|---|---|---|---|
| StoneCottage_A | red tile (B4 kit roof), low 36.6 deg | tall gable stack, +X (1.05 m over the ridge) | deep, wide gabled porch hood (1.0 m), flower box | #3e5670 slate blue | 4.09 x 3.37 |
| StoneCottage_B | thatch, steep 53.6 deg | squat gable stack, -X, offset 0.85 m toward the back slope, topping out 0.3 m under the ridge | twin gabled dormers, flower box | #4a6a48 sage green | 3.85 x 3.75 |
| StoneCottage_C | thatch, 43.6 deg | ridge stack | low tiled lean-to woodshed on +X, no dormer, no flower box | #6a2e29 oxblood | 3.48 x 3.89 |

Seeded (crc32 of the variant name): width, depth, wall height 2.3-2.55 m, roof pitch (A 34-37, B 51-54, C 42-45 deg), door side,
chimney offset, wear amount.

## Numbers (re-imported FBX)

| Variant | Frame tris | Part_Door | Part_Shutters | Total | FOOTPRINT (x by Blender-y, m) | Roof extent (m) | Height to chimney pots |
|---|---|---|---|---|---|---|---|
| StoneCottage_A | 22,202 | 1,558 | 1,728 | 25,488 | 4.85 x 4.77 | 5.19 x 5.17 | 5.75 |
| StoneCottage_B | 25,010 | 1,558 | 1,728 | 28,296 | 4.61 x 4.38 | 4.98 x 4.82 | 6.01 |
| StoneCottage_C | 22,728 | 1,558 | 1,728 | 26,014 | 4.88 x 4.52 | 5.26 x 4.94 | 6.11 |

FOOTPRINT is a flat 0.1 m box over walls, plinth, chimney breast, porch (A) and lean-to (C), centred on the pivot; all inside the
5 x 5 m body limit; the roofs stay inside the 6 x 6 m foundation.

## Export contract

Root `<Variant>` with children `Frame`, `Part_Door`, `Part_Shutters`, `SOCKET_Door`, `SOCKET_Shutters`, `FOOTPRINT`.
- Pivot: foundation centre at ground (the FOOTPRINT box is centred on it). Metres.
- Axes: Blender z up, front (door side) = -Y. FBX export -Z forward, Y up, `FBX_SCALE_UNITS`, `bake_space_transform`,
  `mesh_smooth_type FACE`, `colors_type LINEAR`, `path_mode STRIP` (slot names survive: verified on re-import).
- Part origins are their sockets. SOCKET_Door = door bottom centre inside the door recess (10 cm behind the wall face, on the
  threshold, z 0.22). SOCKET_Shutters = centre of the first front window, on the wall plane. The FBX stores these in Blender
  metres; Unity-side offsets (x, z up, front +Z after the import) follow the usual axis change.

| Variant | SOCKET_Door (Blender x, y, z) | SOCKET_Shutters (Blender x, y, z) |
|---|---|---|
| StoneCottage_A | -0.86, -0.99, 0.22 | 0.81, -1.15, 1.32 |
| StoneCottage_B | 0.83, -1.56, 0.22 | -0.70, -1.73, 1.32 |
| StoneCottage_C | -1.19, -1.63, 0.22 | 0.19, -1.80, 1.32 |

- Part_Shutters holds every shutter on the front and back windows (open, standing off the wall); the Frame keeps bare hinge pins
  at the window jambs. Without Part_Door the Frame shows a deep stone-lined doorway into a dark room (FK_PlinthJoint liner), never
  a hole; without shutters the windows keep their casings and deep reveals.
- Window panes sit 24 cm back in the reveal, built from four flat quads (SC_GlassHi, SC_GlassSky, SC_Curtain, FK_Glass) so the glass reads as a lit reflection over a dark room with a half-drawn curtain.
- `wear` (linear float colour, per corner) on every mesh incl. FOOTPRINT: R grime and run-off (splashback band, eave shade, soot at
  the chimney pots, chipped paint, older dark roof patches, a dark ridge line), G moss (plinth tops, roof, rubble foot), B polish (door step, newer paler roof patches). Same channel use as
  the B4 kit (the kit's vertex-mask tile shader reads it).

## Material slots (Unity must rebuild exactly these)

Every textured slot is a B1 tileable: albedo, normal, ORM from `Art/Textures/B1_town/<folder>/<folder>_{albedo,normal,orm}.png`,
colour multiplied by `tint` (linear), normal strength `normal`, roughness plus `rough_add`. "paint" slots use the greyscale of the
albedo times `paint / 0.17` (PAINT_LUM) instead of `tint`. `FK_*` names are the facade kit's, `SC_*` are this family's.

| Slot | Tileable (B1_town) | Linear tint | Normal | Rough add | Used by |
|---|---|---|---|---|---|
| SC_Rubble | sandstone | A (0.56, 0.64, 0.80), B (0.60, 0.65, 0.74), C (0.58, 0.63, 0.78) | 1.4 | 0 | walls, gables, chimney shaft, lean-to |
| SC_Thatch | oak_timber (vertical grain runs down the slope) | B (2.55, 2.35, 1.20), C (2.05, 1.92, 1.30) | 1.1 | +0.2 | roof, dormer roof (B, C only) |
| FK_Sandstone | sandstone | (1, 1, 1) | 1.0 | 0 | pale quoins, jambs, lintels, plinth, door step, chimney caps |
| FK_PlinthJoint | sandstone | (0.30, 0.28, 0.24) | 1.0 | 0 | dark interior liner |
| FK_RoofTile | roof_tile | (1, 1, 1) | 1.3 | 0 | tile roof and porch roof (A), lean-to roof (C), clay chimney pots (all) |
| FK_Oak | oak_timber | (0.80, 0.76, 0.72) | 1.2 | 0 | timbers, window casings, roof undersides, porch posts |
| FK_OakDoor | oak_timber | (1.35, 1.25, 1.15) | 1.0 | 0 | one bare door plank, split logs (C) |
| FK_DoorPaint | painted_wood | paint A (0.0482, 0.0931, 0.162), B (0.0685, 0.1441, 0.0648), C (0.1441, 0.0273, 0.0222) | 1.0 | 0 | Part_Door planks |
| FK_Shutter | painted_wood | same paint as FK_DoorPaint | 1.0 | 0 | Part_Shutters |
| FK_Iron | iron | (1, 1, 1) | 1.0 | 0 | hinges, pintles, lattice came, door furniture |
| FK_Glass | none (flat) | base colour (0.012, 0.016, 0.02), roughness 0.12 | - | - | window glass, dark pane (lower right) |
| SC_GlassHi | none (flat) | base colour (0.34, 0.40, 0.46), roughness 0.05 | - | - | top 18 % of every window pane: pale sky glare |
| SC_GlassSky | none (flat) | base colour (0.13, 0.17, 0.21), roughness 0.07 | - | - | next 22 % of the pane: darker reflection (gradient step) |
| SC_Curtain | none (flat) | base colour (0.52, 0.47, 0.37), roughness 0.9 | - | - | pale half-drawn curtain behind the lower left 46 % of every pane |
| FK_Leaf | none (flat) | base colour (0.07, 0.13, 0.025), roughness 0.6 | - | - | flower box foliage (A, B) |
| FK_Bloom | none (flat) | base colour (0.45, 0.04, 0.03), roughness 0.5 | - | - | flower box blooms (A, B) |

Per variant the slot lists (Frame / Part_Door / Part_Shutters):
- A Frame: FK_RoofTile, FK_Oak, SC_Rubble, FK_Sandstone, FK_PlinthJoint, SC_GlassHi, SC_GlassSky, SC_Curtain, FK_Glass, FK_Iron, FK_Leaf, FK_Bloom. B Frame: SC_Thatch, FK_Oak, SC_Rubble, FK_Sandstone, FK_PlinthJoint, SC_GlassHi, SC_GlassSky, SC_Curtain, FK_Glass, FK_Iron, FK_Leaf, FK_Bloom, FK_RoofTile (chimney pots only). C Frame: SC_Thatch, FK_Oak, SC_Rubble, FK_Sandstone, FK_PlinthJoint, SC_GlassHi, SC_GlassSky, SC_Curtain, FK_Glass, FK_Iron, FK_RoofTile, FK_OakDoor.
- Part_Door: FK_Oak, FK_Iron, FK_DoorPaint, FK_OakDoor. Part_Shutters: FK_Iron, FK_Shutter.

Linear paint colours are the sRGB hex above converted to linear. SC_Thatch has no thatch tileable in `Art/Textures`: it is a tint of
the oak grain plus layered geometry (laps, rounded eave and verge lips, a ridge roll), per T2 (no new texture set).

## Licences

All textures are the project's own B1 tileables (`Art/Textures/B1_town/SOURCES.md`). No external meshes, no new textures. Reference
photos used for the review sheet are study-only (`Reference/hollowmere/kits/REFS.md`).
