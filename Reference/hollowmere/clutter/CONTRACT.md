# Q14 clutter: art <-> builder contract (2026-10-31)

Generator `Art/Blender/town/sc_clutter.py` (Gen method, code model `sc_garden.py` on origin/claude/ecstatic-bardeen-pr8wbk: chamfered boxes,
lathes, tubes; no alpha cards, NO BAKES, slot-named materials mapped in Unity to the shared B1 tileables). Export: `Assets/_Project/Content/Art/Town/Clutter/<Asset>/<Asset>.fbx`
plus one `Assets/_Project/Content/Art/Town/Clutter/SOURCES.md` (tris, sizes, slots per asset). FBX `path_mode='STRIP'`, no textures.

Axes and pivots (as the garden / B7): Blender z up, front -Y, metres; FBX -Z forward / Y up, so the front is Unity local +Z. Pivot = ground centre of the
footprint (signs and washing-line parts: see below). Root empty `<Asset>`, child mesh `Body`, a flat 0.1 m `FOOTPRINT` box (Unity hides its renderer).
Every piece: chunky storybook forms ~10% oversize, timber ~1.4x real section, 1.5 cm chamfers, nothing dead straight (warp, lean, boards turned 1-2 deg).

## Assets (rebuilt / ruined pair each). Footprint X x Z (m), height (m)
| Rebuilt | Ruined | Footprint | H | Notes |
|---|---|---|---|---|
| Handcart | Handcart_Broken | 1.0 x 1.9 / 1.2 x 1.9 | 1.0 / 0.8 | two spoked wheels, shafts toward +Z resting on the ground with a prop leg; a sack and a crate in the bed. Broken: tipped on one shaft end, a wheel off and lying flat, a split board |
| CrateStack | CrateStack_Broken | 1.3 x 0.9 / 1.4 x 1.1 | 1.4 / 0.7 | 4-5 crates of mixed size stacked, one turned; broken: toppled, one smashed open, loose planks |
| SackStack | SackStack_Broken | 1.2 x 0.9 / 1.2 x 1.0 | 0.9 / 0.4 | 5-6 tied sacks slumped in a pyramid on a low pallet; broken: two split sacks, grain spilled in a low mound, pallet board snapped |
| RainBarrel | RainBarrel_Broken | 0.8 x 0.8 / 0.9 x 0.9 | 1.1 / 0.9 | oak barrel on a short stone/block stand, iron hoops, lid half on, a spigot and a small dipper, dark water disc; broken: staves sprung outward at the top, a hoop fallen to the ground, no water, no lid |
| FlowerBox | FlowerBox_Broken | 1.4 x 0.45 / 1.4 x 0.5 | 0.7 / 0.45 | free-standing oak trough on short legs, soil, chunky flower clumps (leaf lenses + petal blobs, 3 colours) trailing over the front edge; broken: front board fallen out, dry soil, dead brown stems |
| Well | Well_Ruined | 2.0 x 2.0 / 2.0 x 2.0 | 3.0 / 1.4 | stone ring (outer dia 1.6, 0.8 high, chunky coping), two oak posts, small tiled gable roof, winch drum with crank and rope, bucket on the rim; ruined: ring broken down on one side (stones lying out), one post a stump, the other leaning, crossbeam fallen across the ring, no roof, no rope |
| ShopSign_Bakery / _Inn / _Smithy / _Workshop / _Tailor / _Grocer | ShopSign_Broken | n/a (wall mounted) | n/a | see signs |
| WashingLine (parts set) | (torn parts in the same set) | n/a (overhead) | n/a | see washing line |

### Shop signs
Pivot = the centre of the wall plate at the wall plane (Unity local origin on the facade, the arm runs out along local +Z, up = +Y). Iron wall bracket
(wall plate ~0.3 x 0.4, scrolled arm reaching 0.9 m out), board ~0.6 wide x 0.5 high x 0.05 thick hanging from two short chains/rings under the arm, its
face in the local X-Y plane (readable from both sides of the lane). Each trade has a chunky carved emblem proud of the board on both faces:
Bakery a loaf, Inn a tankard, Smithy an anvil, Workshop a crossed saw and hammer, Tailor open shears, Grocer an apple with a leaf. Board shapes vary
(rounded top, shield, plain with notched corners). ShopSign_Broken: bracket arm bent down ~25 deg, board hanging from one chain only, tilted ~35 deg, a corner split off.
The lowest point of any sign is at most 0.75 m below the pivot (builder mounts the pivot at 3.0 m, so the board clears 2.25 m).

### Washing line
One FBX `WashingLine` with separate objects (no FOOTPRINT): `Rope_1m` (along +X from x 0 to 1, ~1.6 cm dia), `Hook` (wall hook/eye, pivot at the wall
plane, out along +Z), cloths with the pivot at their top centre on the line: `Cloth_Shirt`, `Cloth_Sheet` (~1.2 wide x 1.0), `Cloth_Apron`,
`Cloth_Towel`, `Cloth_Socks` (pair), `Peg` (tiny); torn: `Cloth_Rag_A`, `Cloth_Rag_B` (grey, ragged hems). Cloth hangs in the local X-Y plane with soft
folds and a slight billow (a few cm in Z), max hang 1.0 m below the pivot.

## Material slots (exact names; Unity maps them, nothing else allowed)
| Slot | Unity source | Tint / colour (linear) |
|---|---|---|
| CL_Oak | oak_timber | (0.80, 0.72, 0.62) warm oak |
| CL_OakGrey | oak_timber | (0.62, 0.60, 0.58) weathered silver-grey (ruined wood) |
| CL_Paint | painted_wood | (0.60, 0.72, 0.52) faded sage (shutter green family) |
| CL_PaintRed | painted_wood | (0.85, 0.45, 0.32) faded red ochre (cart, signs) |
| CL_Iron | iron | (1, 1, 1) |
| CL_Stone | sandstone | (1, 1, 1) |
| CL_Tile | roof_tile | (1, 1, 1) |
| CL_Canvas | canvas | (1.0, 0.94, 0.82) sacking |
| CL_Rope | canvas | (1.0, 0.85, 0.62) |
| CL_Soil | flat | (0.10, 0.07, 0.045), smooth 0.15 |
| CL_Grain | flat | (0.62, 0.48, 0.25), smooth 0.2 |
| CL_Leaf | flat | (0.18, 0.30, 0.08), smooth 0.3 |
| CL_Petal | flat | (0.85, 0.35, 0.30), smooth 0.3 (Blender renders vary it by a `tint` corner attribute as the garden does; Unity gets the flat colour) |
| CL_PetalB | flat | (0.85, 0.72, 0.25), smooth 0.3 |
| CL_Dead | flat | (0.30, 0.20, 0.10), smooth 0.2 |
| CL_Water | flat | (0.025, 0.04, 0.03), smooth 0.94 |
| CL_Gilt | flat | (0.70, 0.48, 0.16), smooth 0.5 (sign emblems) |
| CL_Linen | canvas | (1.15, 1.12, 1.05) off-white cloth |
| CL_ClothBlue | canvas | (0.45, 0.58, 0.80) |
| CL_ClothRed | canvas | (0.95, 0.55, 0.45) |
| CL_Rag | canvas | (0.62, 0.58, 0.52) |

Budget: props <= 3000 tris (Well <= 6000, a sign <= 1500, each washing-line part <= 600), <= 6 slots per asset.
