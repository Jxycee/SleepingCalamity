# Area 1 world build (Fable Run 7)

Written 2026-11-16 on `claude/area1-world` (from `claude/area1-rework`, 44a12aa2). The user's goal: "make the entirety of area 1 and create immersion"; after Run 6 "we really only still have our beauty corner". This plan covers every walkable metre of Hollowmere, not one corner. Concept, spots and loop stay as in `area1-hollowmere.md` (sections 2, 3, 7); this file redoes how the area is built and how it is judged. Decisions here are "decided (2026-11-16)": current direction, open to change with a reason.

Reference: Epic's Shadows of the Abbey template (layering, reuse and LOD rules only; its numbers are orders of magnitude) and our own concepts H1-H8. Art target unchanged: storybook shapes, stylised-real PBR, warm day colour, heavy atmosphere (`PHASE1.md` 3.1, 3.5).

## 1. What is wrong today (from the Run 6 sheet and three scans)

1. **The start state has no silhouette.** In the Ruined preset all 36 lots are `Empty` and carry one of nine low ruin meshes (`Ruins/Ruin_<S|M|L>_<A|B|C>`). From eye height the town is a plaza with trees and props. Design idea 1 ("the chimneys stayed") is not visible anywhere.
2. **Only the square is dressed.** No builder or camera exists for the orchard; the lanes, the Lower Lane, the meadow, the road out and the hill edge have ground and little else. No boats, no churchyard, no play boundary on the road.
3. **One light and fog state for the whole area.** One global Volume, one fog density. No district reads differently from another. No decal projectors, no APV, no local fog.
4. **The scene is too heavy to grow.** `Hollowmere.unity` is 59.5 MB: 9,441 GameObjects and 479 meshes embedded in the scene file. Nothing is GPU-instanced outside the terrain grass.
5. **Judging looked at seven cameras**, five of them in or over the square.

## 2. Judged state and cameras

**Decided: the Ruined preset in Morning light is the judged state**, because it is what the player walks first and longest. Rebuilt gets one sanity sheet at the end (six cameras) so that nothing placed for the ruin stands inside a rebuilt house.

Twenty eye cameras (1.65 m above the ground, FOV 60) and three high views replace judging by the beauty-corner set. `Cap_HM_Eye_*` names; positions are (x, z) with the look target after the arrow. The implementer may move a camera up to 2 m to stand on walkable ground and reports the change.

| # | Camera | Position -> target | District |
|---|---|---|---|
| 1 | Spawn (kept) | (12.5, 3.75) -> (-4, -6) | D1 |
| 2 | Postcard (kept) | (0, -19.5) -> (0, 20) | D1, D2 |
| 3 | West (kept) | (5, -1) -> (-30, -7) | D1 |
| 4 | Bakery | (-6, 3.5) -> (-14, 12) | D1 |
| 5 | InnFront | (16, -13) -> (40, -16.5) | D6 |
| 6 | Climb (kept) | (22.5, 8.5) -> (16.5, 22) | D2 |
| 7 | Balcony (kept) | (-2.25, 20.7) -> (-1, -30) | D2 |
| 8 | BellYard (kept) | (17, 28.7) -> (2, 29.2) | D3 |
| 9 | HillWest | (-36, 23) -> (-8, 27) | D3, D11 |
| 10 | MarketStreet | (0, -27) -> (0, -50) | D5 |
| 11 | LowerLane | (-14.5, -53) -> (-23, -68) | D5 |
| 12 | RingWest | (-46, 6) -> (-46, -28) | D4 |
| 13 | RingSouth | (-8, -48.8) -> (22, -48.5) | D4, D8 |
| 14 | Orchard | (35, -44.5) -> (43, -58) | D7 |
| 15 | Mill | (60.5, -50.5) -> (71, -60) | D9 |
| 16 | Meadow | (-8, -72) -> (50, -68) | D9 |
| 17 | Lake (kept) | (-21.5, -73) -> (6, -100) | D9 |
| 18 | JettyBack | jetty end, deck + 1.65 -> (-5, -40), y 0 | D9 |
| 19 | RoadOut | (-48, -74) -> (-100, -82) | D10 |
| 20 | RoadBack | (-85, -80) -> (-20, -55) | D10 |

High views (absolute, far clip 400): `Cap_HM_Overview` (kept), `Cap_HM_High_Lake` (10, 38, -128) -> (0, 0, -25), `Cap_HM_High_East` (118, 30, -30) -> (0, 2, -20).

Sheets: two per round at most (ten tiles each, about 1400 px wide, previous round as insets, our own concept row on top). Private frames (`Reference/private/`) only in scratch sheets, never committed. Score strictly /40 per camera; a district's score is its weakest camera. Rule from Run 6: before any change near a camera, check the placement against the camera table; a fix whose cause is a guess gets a one-camera probe (`SC_CAPTURE_NOBUILD=1`) first.

## 3. Production rules (the Abbey's method in Unity URP)

1. **One material family per surface.** `SC_WearLit` is the master. Stone, plaster, timber, roof and soil each have one base material from `Art/Textures/B1_town` and at most three tint variants (material variants, so the SRP batcher keeps working; no per-renderer property blocks on static kit). New kit pieces carry baked AO and a moss mask in vertex colour. No new texture sets for kit pieces.
2. **Modular kit on a 1 m grid, dressing off the grid.** Ruin shells, walls and fences snap to the lot footprint; beams, rubble, ivy, crates and stones are placed off-grid with 2-8 degree tilts and position-hashed yaw and scale. Target about three dressing pieces per kit piece.
3. **Placement is position-hashed and table-driven.** One table row per rule, a hash of the position picks the variant, so tuning one rule never moves the rest (worked in Run 6). Deterministic builds stay byte-identical.
4. **Ground blending by decals and edge strips.** URP Decal Projectors (moss, leaf drift, mud, puddle, grime at wall feet) where a kit piece meets the ground, plus grass and weed strips at every wall foot. Budget about 300 projectors for the area.
5. **Ground cover lives in the terrain, not the scene.** Grass, ferns, flowers, reeds and litter are terrain detail layers with a density mask per district (GPU-instanced, no shadows on small plants, 60 m range). Scene objects only for things the player can bump into.
6. **Trees and rocks carry LODGroups** (1.0 / 0.4 / 0.1 for kit, 0.35 / 0.12 / 0.02 for trees as today); small props cull by LODGroup at about 50 m.
7. **Generated meshes are assets, not scene data.** Every builder that makes a mesh saves it under `Content/Generated/Hollowmere/` and references it. Scene budget: under 45 MB at the end of the run (hard limit 50).
8. **Light by district.** One global Volume for the base look plus a local Volume per light zone (section 5); shadowless fill lights, capped at 12; light shafts through tree canopies at chosen spots; fog cards over water and in the lanes' low points. APV: one bake trial on the square late in the run (task T12); if the bake is slow or its output is not stable, it goes to the landing session's list and the trilight ambient stays.
9. **Budgets (PC only, quality first).** Textures 2K cap. Ruin shell 4-12k triangles with one LOD below; hero prop up to 30k; scattered small props under 400. Visible triangles from any eye camera under 6 million; the landing session measures frame time at 1440p.
10. **Non-Unity work never writes under `Assets/` while a Unity task runs.** Blender and fetch tasks stage their output in `Art/Staging/area1-world/<kit>/`; the next Unity task moves it in.

## 4. Districts

Each district lists: landmark and silhouette; sightlines; layers (foreground / midground / background); kit and assets; density; light zone; life; story.

**D1 The square** (x -20..20, z -21..7; ring 1 lots F01-F10).
- Landmark: the dry fountain; silhouette: a ring of gables and chimney stacks, with the great tree at (-15, -11).
- Sightlines: north to the tower over the terrace (Postcard); south down Market Street to haze; from spawn the bakery lot stays visible (F01 stays low).
- Layers: clutter and weeds at the kerbs / stalls, fountain, ruin shells / belt trees, hill, far range.
- Kit: ruin shells (tall on F03, F05, F06, F07, F09; mid on F02, F04, F08, F10; low on F01), stalls, B7 clutter, T05 debris, T16 cart and bench, T06 logs.
- Density: 6-8 props per 10 m of square edge, open middle; 4 decals per 10 m of edge; grass and weeds only at edges and footings.
- Light: Z1. Life: crows on stacks, sparrows, leaf fall under the great tree, home chimney smoke.
- Story: Tobin's bench and stock pile at the bakery step; Wren's list wall; a market cart with weeds grown through its wheel.

**D2 Keep terrace and church** (z 19.6..30, y 4).
- Landmark: the bell tower (17 m). Silhouette: tower, church roof, annex.
- Sightlines: the balcony view over the whole square and out to the lake haze.
- Layers: terrace parapet and bench / the square below / ruin ring, wall, lake haze.
- Kit: keep kit as built, T02 wall, ivy, ferns, T16 bench, lantern posts.
- Density: 3 props per 10 m; ivy on a third of the terrace wall.
- Light: Z2 (shade side lifted so the north wall is never black). Life: pigeons on the tower ledges, wind tone.
- Story: the sealed tithe gate breathing warm air, scorched threshold.

**D3 Bell yard, churchyard and crag** (north of the church, the dive).
- Landmark: the trapdoor hatch under the crag. Silhouette: crag outcrops against the hill forest; two churchyard trees.
- Sightlines: closed on purpose (a sheltered pocket); west along the church wall to the churchyard.
- Layers: dive kit and brazier / hatch, tower door, low churchyard wall with headstones / crag, forest.
- Kit: oak hatch (visual only: the dive interaction is untouched), rope coil, chest C12, brazier T16h, lamp C15, headstones (new, simple), T02 wall, T11 boulders plus new rock variants, ferns, moss decals.
- Density: 5 props per 10 m in the yard; ferns and moss at every wall foot.
- Light: Z2 with one warm point (brazier) and one cool fill. Life: pigeons, drifting motes in a shaft between the trees.
- Story: the dive kit laid out by the hatch; old graves under the trees, one freshly tended.

**D4 Ring 2 homes and lots** (Ring Lane west, south and east; 23 lots).
- Landmark per stretch so lanes can be told apart: smithy anvil and forge stack (west, R2_01), schoolhouse bell post (R2_03), a collapsed two-storey gable (south, R2_05), the workshop saw frame (east, R2_08).
- Silhouette: alternating tall, mid and low shells, never two tall in a row; chimney on every lot.
- Sightlines: along each lane to its landmark; glimpses back up to the tower between shells.
- Layers: wall-foot growth and clutter / shells with saplings inside / town wall, belt trees, far hills.
- Kit: ruin shells, lane kit, T05 debris, T12 ferns and ivy, B7 and Clutter sets, T16 furniture.
- Density: 4-5 props per 10 m of lane, always against a wall; one clutter cluster per 15 m; 3 decals per 10 m.
- Light: Z3 (dappled shade under the belt trees). Life: a crow per third stack, butterflies over the lots, a cat spot at the smithy.
- Story: a table still set under a fallen beam; a child's cart at a doorstep; a washing line left out for ten years.

**D5 Market Street and the Lower Lane** (x 0, z -21..-49; then down to the Lower Gate).
- Landmark: the Lower Gate piers with the lake behind. Silhouette: a street of shells stepping downhill, ending in sky.
- Sightlines: north to the postcard view; south to the gate and water. R2_16 at the foot stays low.
- Layers: gutter, weeds, broken cart / shells and garden wall / gate, lake, far shore.
- Kit: lane kit, shells, T16 cart, milestone, T02 wall, garden kit (R2_22).
- Density: as D4; a leaf drift in every third gutter bay.
- Light: Z1 on the street, Z5 beyond the gate. Life: swallows over the gate, mist beyond.
- Story: the broken cart at the street mouth; a handcart loaded for a move that never happened.

**D6 East Lane and the inn front** (East Stub, F09, Keep Lane).
- Landmark: the inn's tall end gable and its hanging sign bracket. Silhouette: gable, stack, the stair to the keep behind.
- Sightlines: east down the lane to the ring and the orchard trees.
- Layers: barrels and benches / inn shell / east ring shells, belt trees.
- Kit: shell (tall, L), C07 barrels, T16 benches and table, ShopSign_Inn (fallen), B7 sacks.
- Density: 6 props per 10 m at the inn front, 4 along the lane. Light: Z1. Life: a dog spot (empty for now), sparrows.
- Story: benches still round a table, tankard jugs, the sign face down in the weeds.

**D7 The orchard** (x 34..44, z -54..-44, new).
- Landmark: the fallen apple tree and the gap in the town wall with the lake in it. Silhouette: seven gnarled crowns over a dry-stone wall.
- Sightlines: through the wall gap to the lake and the mill; back north-west to the tower.
- Layers: wall, gate, long grass / trees, ladder, baskets / wall gap, lake, far shore.
- Kit (decided: no new tree model this run): TR02 and TR03 at 4.5-6 m with a yellow-green leaf variant as the apple trees, T08d apples as windfalls, T02a wall, Fence_Gate, Fence_Rail, T04b ladder, T08g baskets, Bench_A, `Wall_Crumbled` and `Wall_Breach` at the gap (the low parapet keeps its collider: the boundary does not move).
- Density: long grass and flowers everywhere but the path; 20 windfalls; 2 props per tree.
- Light: Z4. Life: butterflies, bees (motes), a blackbird.
- Story: the harvest left half done: ladder against a trunk, one full basket, one tipped.

**D8 Allotments and yards** (R2_07, R2_18, R2_22, R3_02).
- Landmark: the scarecrow. Silhouette: bean trellises and a cold frame above bolted, overgrown beds.
- Layers: fence and gate / beds, trellis, water butt / shells and wall.
- Kit: garden kit, T16c wheelbarrow, T16f pen wall, T16k-m tools, T08 vegetables gone wild.
- Density: 8 props per plot. Light: Z4. Life: butterflies; hens later. Story: one bed weeded and replanted (someone has started).

**D9 Meadow, lake shore, mill and jetty** (outside the wall, z -66..-90).
- Landmark: the mill and its wheel on the stream (east), the jetty (west). Silhouette: mill roof, reed beds, a boat on the shingle; the town above its wall when looking back.
- Sightlines: across the water to the far shore; back up to the wall, the shells and the tower (JettyBack: the arrival view).
- Layers: reeds, stones, boat / water, jetty, mill / far shore trees, hills, mist.
- Kit: LakeShore kit, reeds in three dense beds, T11b shore rocks, T13 boats (one beached, one moored), meadow detail layers (tall grass, two flowers), mill dressing (sacks, cart, fence), new rock variants.
- Density: three foliage layers everywhere; 1 prop per 10 m of shore; no bare bank (the pale bank is found by probe and fixed first).
- Light: Z5. Life: gulls, fish rings, dragonflies, mist cards drifting on the water.
- Story: nets on the jetty rail; the mill wheel stopped and mossed.

**D10 The road out** (Saltwake Road, x -24..-120).
- Landmark: the wayside shrine and the milestone; the road's own curve between hedges. Silhouette: hedgerow, fence line, a broken wagon.
- Sightlines: west to the far range (RoadOut); east back to the town on its hill (RoadBack).
- Layers: verge flowers, ruts / hedges, shrine, wagon / fields, tree lines, range.
- Kit: Road kit (Hedge_A/B, Fence_Rail, Milestone, Shrine_Ruined, Cart_Broken), T16a wagon, field detail layers.
- Density: 2 props per 10 m; hedge or fence on at least one side throughout.
- Light: Z6. Life: larks (bird cards), pollen.
- Story: the wagon with a broken axle, its load spilled; flowers left at the shrine.
- Boundary (new): at about x -118 a fallen tree and the wagon close the road, with a collider behind them. The walkable area ends in something that looks like a reason.

**D11 The hill edge** (outside the keep, west and east flanks; crag foot).
- Landmark: the crag itself; a lone standing stone at the west foot. Silhouette: outcrops and trunks.
- Layers: ferns, logs, stumps / trunks and outcrops / forest wall, far card trees.
- Kit: TR01-TR07, T11a plus new rocks, stumps and a fallen log (new adoption), fern and litter detail layers.
- Density: forest floor fully covered; a log or stump every 12 m.
- Light: Z7. Life: motes in shafts, a woodpecker tap. Story: an old boundary stone; a path worn up to the crag that goes nowhere.

## 5. Light and fog zones (Morning base; each a local Volume with a 6-10 m blend)

| Zone | Where | Grade and fog against the base |
|---|---|---|
| Z1 warm open | square, Market Street, East Lane | the base look; highlights get a shoulder change so p90 reaches .80 |
| Z2 shade pocket | terrace north side, bell yard, churchyard | exposure +0.3, cooler fill, fog x1.4, one shaft |
| Z3 dappled lanes | Ring Lane west and south | slightly green-cool shadows, fog x1.2 |
| Z4 golden green | orchard, allotments | warmer white balance, foliage saturation +8, pollen |
| Z5 bright haze | meadow, shore, jetty, mill | exposure -0.15, cooler, fog x1.5 near the water, mist cards |
| Z6 distance | road out | aerial haze rising westward |
| Z7 deep green | hill edge and forest | exposure -0.2, fog x1.6, two shafts |

## 6. Task list

One Unity task at a time (U). Blender and fetch tasks (B) run beside it and stage outside `Assets/`. Each task is one implementer run of 60-90 minutes, commits by path, and writes its result into `Reference/sessions/area1-world/`.

| # | Task | Owns |
|---|---|---|
| T01 U | Camera set (section 2), sheet tool, baseline capture and two sheets; report what the 59 MB is made of (largest embedded meshes, objects per root) | `HollowmereBuilder.cs` (camera table), `Reference/sessions/area1-world/tools/`, `R0/` |
| T02 B | Ruin shell kit: per size S, M, L a tall, a mid and a low shell set with gable and chimney, window and door openings, leaning beams, rubble; plus headstones, a signpost and the oak hatch | `Art/Blender/town/sc_ruin_shells.py`, `Art/Staging/area1-world/shells/` |
| T03 U | Scene diet: generated meshes to assets, target under 40 MB before new dressing; build twice, second build leaves no diff | the builders that embed meshes, `BuilderUtil` |
| T04 B | Nature set from Poly Haven CC0: three rocks or cliff pieces, two stumps or logs, two wildflowers; decimate, LODs, source records | `Art/Source/PolyHaven/`, `Art/Staging/area1-world/nature/` |
| T05 U | Shells into the town: height class per lot (section 4), chimney on every lot, ivy and saplings re-fitted, colliders, ruin seal and traversal green; sheet for D1, D4, D5, D6 | new `HollowmereRuinShells.cs`, `HollowmereRuinDressing.cs`, `HollowmereBuilder.BuildFoundation` |
| T06 U | Streets: decal projectors (renderer feature), wall-foot strips, clutter clusters, lane landmarks, story props for D1, D4, D5, D6 | new `HollowmereStreets.cs`, `HollowmereClutter.cs`, renderer assets |
| T07 U | Orchard and allotments (D7, D8) with the wall gap | new `HollowmereOrchard.cs`, `HollowmereGarden.cs`, `HollowmereTownWall.cs` (gap only) |
| T08 U | Meadow, shore, mill, jetty (D9): bank probe and fix, reed beds, rocks, boats, meadow detail layers, far shore | `HollowmereLakeArt.cs`, `HollowmereFoliage.cs`, new `HollowmereMeadow.cs` |
| T09 U | Keep, bell yard, churchyard, crag (D2, D3): north wall probe and fix, hatch visual, dive kit, churchyard, rock variety | new `HollowmereBellYard.cs`, `HollowmereWild.CragFoot` |
| T10 U | Road out and hill edge (D10, D11) with the road boundary | `HollowmereRoad.cs` (dressing), `HollowmereWild.cs` (hill floor) |
| T11 U | Atmosphere: light zones, fill lights, shafts, mist and fog cards, highlight shoulder, ambient life per district, mountain reshape | `HollowmereLook.cs`, `HollowmereAmbientBuilder.cs`, `HollowmereBackdrop.cs`, new `HollowmereZones.cs` |
| T12 U | LOD and cull pass, APV bake trial on the square, triangle counts from five cameras, scene size check | kit prefabs, `HollowmereLook.cs` |
| T13 U | Round 2 fixes from the sheets (one or two runs) | as the notes say |
| T14 U | Landing: traversal nodes for the orchard, shore, road and hill edge; Rebuilt sanity sheet; whole-area capture and sheet; `gate`; push | `Reference/qa/traversal/`, tests, `STATUS.md` |

Order: T01 and T02 together; then T03 with T04; then T05 to T12 in order; T13; T14. If usage runs short, T12 and the mountain reshape drop first, then T10; the shells, streets, orchard and shore do not.

## 7. Stops and what stays untouched

Stops: every district at 25/40 strict, or the list done with two rounds per district; weekly all-models above about 92%; the 5-hour window above about 90% (push, STATUS current).

Untouched: `Core/Cellars/`, the dive, combat, enemies, Networking. The playable loop (spawn, build point, placement, bell yard dive, return) and the fast `gate` stay green after every Unity task that changes colliders or ground.
