# Town wall kit and gatehouses (Q8): sources

- Generator: `Art/Blender/town/sc_town_wall.py` (procedural, reproducible from text; builds on the B4 facade kit helpers in `sc_facade_kit.py`).
- Command (Blender 4.5.3, cloud, 2026-10-31): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_town_wall.py -- --out Assets/_Project/Content/Art/Town/Wall --export --no-render`. It writes `<out>/<Piece>/<Piece>.fbx` for every piece below (no `.meta`: the Unity import makes them). `--only wall|gates|<Piece>`, `--figure`, `--showcase`, `--with-walls` are review options.
- Export: FBX -Z forward, Y up, FBX_SCALE_UNITS, bake_space_transform, FACE smoothing, linear colours, `path_mode='STRIP'` (no texture paths, so Unity does not rename textured slots to `Lit`). Metres, scale 1. Blender front/outside -Y is local +Z in Unity (as the B4 kit): the builder yaws a piece 180 when it needs the outside to face -Z.
- Re-import check (every FBX): names as below, slots as the slots table, `wear` colour attribute on every mesh, no closed island wound inward, ray-cast from six directions: 0.0 to 0.9 percent back-face hits, VERIFY PASS 10 of 10.

## Pieces

Bounds are Blender metres from the re-imported FBX (x run, y thickness or passage axis, z up). All meshes use the shared B1 tileables with metre UVs (2 m per tile), a linear `wear` attribute and no unique bake (T2).

| Piece (FBX) | Tris | Bounds | Pivot and axes |
|---|---|---|---|
| `Wall_Straight_A` | 3,808 | x -2.2..2.2, y -1.24..0.86, z -1.5..3.4 | module centre, wall centre line, local ground 0; run along +X (core spans -2..2, coping and footing overshoot 6 to 10 cm); OUTSIDE is -Y. Timber walkway, merlons all along, spout at x 0.9 |
| `Wall_Straight_B` | 3,448 | same | same; stone-flag walkway, loopholes in every second merlon, spout at x -0.7 |
| `Wall_Patched` (Rebuilt) | 3,816 | same | same; three tidy paler patches (two outside, one inside) and a repaired coping slab and merlon |
| `Wall_Breach` (Ruined) | 5,994 | x -2.6..2.7, y -3.5..3.0 | same; both ends full height with merlons (join straight modules), stubs 0.3 to 1.8 m in the gap, rubble spilled to -Y and +Y |
| `Wall_Crumbled` (Ruined) | 3,700 | y -2.7..2.5, z top 2.66 | same; ragged parapet 2.2 to 2.7 m, no merlons, two gaps in the boards, two slumped planks |
| `Wall_Buttress` | 820 | x -0.65..0.65, y -1.93..-0.33, z -1.53..2.84 | wall centre line at x 0; projects to -Y (outside), 0.45 m of it sits inside the wall face; clip it on any module at x 0 (or at a seam, x +-2, to hide the join) |
| `Wall_End` | 2,364 | x -2.2..0.07, y -0.85..0.85 | pivot ON the cut face at x 0, the wall body runs to -X, outside -Y. Dressed quoins both sides, pale end face, solid end pier. For the other end of an opening, scale X by -1 (the winding is flipped for you by Unity) |
| `Wall_Stair` | 1,600 | x -2..2, y 0.55..1.88, z -0.5..2.99 | module centre; on the INNER side (+Y), rises toward +X, 12 risers, the landing at x 1.5..2 is flush with the deck (2.2 m) of the module it is placed with; mirror with scale X -1 |
| `LowerGate` | Frame 31,284, Shell 18,492, Part_Door 1,200 | Frame x -5.2..5.2, y -3.2..3.2, z -1.5..13.85 | pivot at the passage centre at ground, passage axis along Y, outside -Y |
| `EastGate` | Frame 26,036, Shell 20,764, Part_Door 1,200 | Frame x -4.9..4.8, y -3.0..3.0, z -1.5..15.35 | same |

Wall dimensions: 3.4 m ground to merlon tops, 1.2 m thick (battered 12 cm over the height, so 1.44 m at the foot), parapet 0.45 m on the outer side, walkway deck top 2.2 m on the inner side (0.87 m wide, hangs 0.12 m over a corbel course), merlons 0.62 wide on a 1.0 m pitch with a 0.38 m crenel (a crenel is centred on each module seam), footing to -1.5 m (1.7 m wide).

## How the builder tiles it

- Walk the wall path (`OffsetLine(TownWallOffset = 28)` cut by `WallRun`). For each uncut stretch of length L place `round(L / 4)` modules end to end; take up the rest by scaling the LAST module's local X by up to +-10 percent (stones stretch, merlons stay a visible crenel), or finish with `Wall_End`.
- Orient each module to the chord of its own 4 m step along the polyline, not to the whole stretch; the kit takes up to 15 degrees of yaw between neighbours without a see-through crack (the convex side shows a short dark mortar wedge; the concave side overlaps). Put a `Wall_Buttress` on the outside of any corner sharper than 15 degrees (split the corner in two bends) and every third or fourth module otherwise (at a seam where it hides the join).
- Height: the module's local 0 is ground. Place the pivot at the HIGHEST ground under the module's centre line, minus 0.1 m; the 1.5 m footing then covers a 4 m span falling 1.4 m on the low side with no gap. (The old greybox wall top was 2.4 m over the highest ground; the new merlon tops are 3.4 m over it, so the wall is 1 m taller.) The raised bank should be at least 1.7 m wide at the module.
- Variants: alternate `Wall_Straight_A` and `Wall_Straight_B` (A B A B, or seeded 60/40). Preset Ruined: mostly A/B, about 15 percent `Wall_Crumbled`, and at most one `Wall_Breach` per long stretch (put the first where the lake shore wall meets the low east, where the user will see it), never two breaches in a row. Preset Rebuilt: A/B only, plus `Wall_Patched` for about one module in six (never two in a row) and no ruined variants.
- Where a lane, lot or landmark cuts the run: end the stretch with `Wall_End` on each side (the second one scaled X -1). Put `Wall_Stair` on the inner side of one module every 40 to 60 m and at each gate (its landing is flush with the deck), mirrored when the walkway you want continues to the left.
- Colliders: one box per module (x 4, y 1.3, z -1.5..3.4) plus nothing for the merlons; the stair and buttress get their own boxes.

## Gatehouses

Each FBX has the root empty `LowerGate` or `EastGate` (identity transform) and these children, exactly:

| Child | What | Notes |
|---|---|---|
| `Frame` | the restored gatehouse: both piers (replaces the pier greyboxes), passage, round arch, upper storey, roof and chimney (Lower) or battlements and turret (East), wall lanterns | one mesh, the slots below |
| `Shell` | the ruined state: roof gone, upper storey broken, the arch intact but cracked, the broken door leaf hanging, rubble heaps outside and in | one mesh; carries the `FK_Decal_crack` slot (alpha, use a transparent surface) |
| `Part_Door` | two timber leaves with iron straps, studs, hinges and ring pulls | its object origin IS the door socket (0, door plane, 0); hidden until placed, then shown on top of the Frame |
| `SOCKET_Door` | empty at (0, -(D - 1.0), 0) in Blender: LowerGate (0, -1.2, 0), EastGate (0, -1.3, 0) | where `Part_Door` attaches; the door faces the same way as the gate |
| `FOOTPRINT` | flat box 2 hw x 2 D x 0.1 m (collider and NPC-block shape) | LowerGate 9.0 x 4.4, EastGate 8.84 x 4.6; no material (hide the renderer) |

State rule: Ruined shows `Shell` (and its rubble), no `Part_Door`; Rebuilt shows `Frame` and `Part_Door`. Frame and Shell share their bottom 5.9 m footprint, so the builder swaps one for the other with no other change.

| | LowerGate | EastGate |
|---|---|---|
| Between the piers | `LowerGate_W` (-24.6, -64.6) and `LowerGate_E` (-18.0, -64.6) at yaw 0: centre (-21.3, -64.6), 6.6 m between pier centres | `EastGate_N` (59.2, -44.1) and `EastGate_S` (54.0, -47.9) at yaw 37: centre (56.6, -46.0), 6.44 m between pier centres |
| Passage | 4.2 m clear (half 2.1), round arch springing 2.9 m, crown 5.0 m | 4.04 m clear (half 2.02), crown 4.92 m |
| Block | 9.0 m wide (pier outer faces), 4.4 m deep, to 5.9 m | 8.84 m wide, 4.6 m deep, to 5.9 m |
| Top | timber-framed jettied storey (plaster, oak, green shutters) to 8.5 m, steep hip tile roof (ridge 12.8 m), stone chimney to 11.7 m | stone storey with arch windows and slits to 8.3 m, corbelled crenellated parapet to 10.5 m, stair turret under a tile pyramid (apex about 14.5 m) |

Place the gatehouse with its pivot at the midpoint of the two piers, X along the pier-to-pier line, and choose the yaw (0 or 180 about Y) so the outside, Blender -Y, points away from the town. The wall continues from each side face (the wall's 1.2 m thickness fits inside the gate's 4.4 m depth; a straight module's centre line meets the gate at its centre). The gate is 1 m deeper each side than the greybox piers (4.4 m against 2.4 m): clear the pier boxes and keep their NavMesh/colliders off.
The East Gate's piers are 6.44 m apart, so its clear passage is 4.04 m, not 4.2 m.

## Slots (materials rebuilt by Unity from this table)

Every slot is a shared B1 tileable (2048 px at most) sampled with the UV0 metre UVs (2 m per tile). Tint is a linear multiplier on the albedo; normal scale is the normal-map strength. `wear` (UV-independent corner colour, linear) drives the kit's vertex-mask shader exactly as in the B4 facade: R grime (mixes toward a warm dark grime colour), G moss, B polish.

| Slot | Tileable (`Art/Textures/B1_town/...`) | Linear tint | Normal scale | Used for |
|---|---|---|---|---|
| `TW_Rubble` | sandstone | (0.64, 0.61, 0.57) | 1.4 | grey rubble face stones, merlon bodies, rock heaps, gate vault |
| `TW_RubbleOchre` | sandstone | (1.00, 0.89, 0.73) | 1.4 | warm ochre face stones (coherent patches plus strays) |
| `TW_Coping` | sandstone | (1.14, 1.09, 1.00) | 0.9 | dressed pale stone: coping, string and corbel courses, quoins, voussoirs, stair, flags |
| `TW_Patch` | sandstone | (1.30, 1.16, 0.94) | 1.0 | newer repair stone (Wall_Patched) |
| `TW_Footing` | sandstone | (0.46, 0.43, 0.39) | 1.2 | the deep footing and dark base blocks |
| `TW_Joint` | sandstone | (0.66, 0.61, 0.54) | 1.0 | the mortar core behind the face stones |
| `TW_Crack` | sandstone | (0.10, 0.09, 0.08) | 0.6 | jagged dark crack ribbons on the Shells |
| `TW_Walkway` | oak_timber | (2.70, 2.50, 2.30) | 1.2 | weathered boards and walkway beams |
| `FK_Oak` | oak_timber | (0.80, 0.76, 0.72) | 1.2 | gate timber frame, joists, snapped posts |
| `FK_OakDoor` | oak_timber | (1.35, 1.25, 1.15) | 1.0 | door planks |
| `FK_Plaster` | plaster | (1.0, 1.0, 1.0) | 0.55 | LowerGate upper-storey infill |
| `FK_RoofTile` | roof_tile | (1.0, 1.0, 1.0) | 1.3 | roofs |
| `FK_Shutter` | painted_wood | (0.57, 0.73, 0.28) (faded green, as the B4 facade) | 1.0 | LowerGate shutters |
| `FK_Iron` | iron | (1.0, 1.0, 1.0) | 1.0 | straps, studs, brackets, lanterns, finial |
| `FK_Glass` | none (flat colour (0.012, 0.016, 0.02), roughness 0.12) | | | dark windows, loophole slits, lantern glass |
| `FK_Decal_crack` | `B3_trims/crack` (albedo alpha), opacity 0.9 | | | alpha crack decals on the Shells (transparent surface) |

`FK_*` slots are the B4 facade kit's (see `../B4/SOURCES.md`); reuse the same Unity materials.

## Licences

The B1 tileables are graded Poly Haven scans, CC0 1.0 (`Art/Textures/B1_town/SOURCES.md`); the B3 crack decal is procedural (`Art/Textures/B3_trims/SOURCES.md`). Geometry is procedural, ours. Reference photos (study only, never shipped or traced) are recorded in `Reference/hollowmere/town_wall/REFS.md` (CC BY, CC BY 2.0/3.0 and public domain only).

## Review (sc-asset-review rubric, /40; floor 25)

Round 1 plus two fix rounds (jamb gaps closed, patchy ochre, shell battlements, crack ribbons, lanterns and spouts). Sheet: `Reference/hollowmere/town_wall/wall_sheet.jpg`.
| Family | Score | Note |
|---|---|---|
| Straight A/B + Patched | 28.5 | silhouette 4, proportion 3.5, form 3.5, surface 3, colour 3.5, wear 3, set 4, efficiency 4 |
| Ruined (Breach, Crumbled) | 29 | breach reads at distance; crumbled's broken top is subtle |
| Kit (End, Buttress, Stair) | 28.5 | stair is hidden behind its own rail at low angles |
| LowerGate (Frame, Shell) | 30 | silhouette 4.5, proportion 4, form 3.5, surface 3.5, colour 3.5, wear 3.5, set 4, metal 3.5 |
| EastGate (Frame, Shell) | 30 | same lines; shell battlement fragment is a little tidy |
