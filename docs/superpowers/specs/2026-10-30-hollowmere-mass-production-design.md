# Hollowmere mass production: design (2026-10-30, session 3)

Status: APPROVED by the user 2026-10-30 ("Approve, start Wave 0"). PHASE1 7.1 and P26 amended to match.

## 1. Intent
The beauty corner proved the art pipeline (cloud Blender generators, bake, `BeautyCornerBuilder` table, captures, reference-beside-render sheets) and the user approved its look. This plan turns that one corner into the whole starting area, built around the rebuild loop (STORY 5 and 8.3): pieces carried out of the Undercroft are placed back in town across three rings. So every building needs more than one state, and every lot has to work with M6 placement.

Success: you can stand anywhere in the Hollowmere square and see final-quality art in both the ruined start state and the fully rebuilt state, and every movable ring-1 building can be placed, turned and picked up again on the foundations. The lanes and mill kits are built at final quality and ready to place.

## 2. User decisions (2026-10-30; settled)
1. **Scope:** ring 1 (the square) built and dressed at final quality. The lanes and the mill: kit variants at final quality, not placed or dressed yet.
2. **Foundations are the lots.** A building snaps onto any empty old foundation. This **replaces PHASE1 P26** (free grid, no fixed lots) for buildings.
3. **Size classes:** foundations come in small, medium and large; a building fits any free foundation of its class.
4. **Four turns:** a building can face any of four directions on its foundation, so "door facing the square" is a real choice.
5. **Any opened foundation:** rings unlock areas; once open, any building can go on any free foundation of its class (a lanes house can stand in the square).
6. **Frame first, then parts:** place the frame on a foundation, then slot each carried part on (door, oven, sign...); each part appears as it's added (P28: a building counts only when all its parts are placed).
7. **Never block progress:** anything placed can always be picked back up.
8. **Decorations go anywhere** in town on the free grid (P26 still holds for them). A decoration that blocks an NPC path breaks and returns to the player.
9. **A building that would block an NPC path is refused** (red preview).
10. **Landmarks are fixed only when a system, the story or continuity needs them there.** Everything else moves.
11. **Ruined look: mix.** Most plots are bare foundations (story 8.2); the fixed landmarks stand as broken shells restored in place.
12. **Variety:** homes from 3-4 kit families with seeded variation; shops and landmarks get their own generators.
13. **Team:** while the user is gaming, this PC session coordinates and up to 4 cloud sessions build; no Unity or Blender on the PC. Once the user is done, up to 10 parallel sessions, if the controller can manage them.

## 3. Fixed landmarks (decision 10 applied; controller's reading, confirm)
| Landmark | Why it's fixed | Start state | Restored state |
|---|---|---|---|
| Bell tower | the tithe ramp runs under it into the mine-mouth (STORY 8.1-8.2); the bell comes back after the boss | standing shell, empty belfry | bell hung (ring 3) |
| Heroes' home (cistern house + bell-loft annex) | the Undercroft stair (P25), Wren's list on the wall | patched old stone, lived in | gets additions as the story goes on (later) |
| Dry fountain | centre of the square, the anchor for "near the fountain" wishes | dry (done: B10) | running water (later) |
| Mill | its wheel needs the lake stream | broken shell | restored (ring 3) |
| Tithe gate | story set piece | sealed, breathing warm air | (story) |
| Market stalls | part of the square (user, 2026-10-30) | bare frames, torn awnings | rebuilt with goods (ring 1) |

User confirmed the list (2026-10-30). Movable: bakery, inn, smithy, schoolhouse, every home.

## 4. The town layout
- **Built from a layout file by an Editor builder** (CLAUDE.md, PHASE1 7.1), the same pattern as `BeautyCornerTable`/`BeautyCornerBuilder`. New `HollowmereLayout` data (foundations, landmarks, lanes, decoration zones, NPC path graph) and a `HollowmereBuilder` that builds scene `Hollowmere` in either state (`Ruined` / `Rebuilt` preset placements) for captures.
- **The square:** about 40 x 30 m around the fountain (PHASE1 7.1). The approved corner is the fountain-and-facade quarter of it; its placements and look (grade, lights, cobbles) carry over unchanged.
- **Foundations** are data rows: id, ring, size class, centre, default facing, ruin variant. Ring 1 starts with **10 foundations: 3 L, 4 M, 3 S** (controller's call; bakery and inn take two L, homes take M/S, the rest are spare so the player has a choice). Wave 0 may adjust the mix once the greybox is walked. Ring 2 about 20-25 along the lanes; ring 3 the mill and the road out.
- **Fixed landmarks** are their own layout rows with a fixed footprint (no size class): they block the path graph like any building and are excluded from the size-class validation (controller's call).
- **NPC paths** are a graph in the layout file (square loop, lane spines, a node at the edge of every foundation on all four sides, so any door direction has a doorstep node). M6 uses it to refuse buildings and break decorations (decisions 8-9). Art only has to provide honest footprints and colliders.
- **Wave 0 makes the layout as greybox first** (boxes per size class on every foundation, fixed landmarks as their current art or greybox) so the user can walk it and approve the town plan before building art goes in.

## 5. The asset contract (every building generator follows this)
### 5.1 Size classes (controller's call; the 4-turn rule needs square foundations)
| Class | Foundation | Building footprint fits inside | Typical use |
|---|---|---|---|
| S | 6 x 6 m | up to 5 x 5 m | cottage, stall house, shed |
| M | 8 x 8 m | up to 7.5 x 7.5 m | home, smithy, the beauty-corner facade's size |
| L | 11 x 11 m | up to 10.5 x 10.5 m | inn, bakery, schoolhouse |
Leftover space inside the foundation is yard (path, garden bed, step stones) and belongs to the building, so it turns with it. The yard is walkable: `FOOTPRINT` covers the building body only, and the path graph's foundation-edge nodes stay outside it (controller's call).

### 5.2 One generator, every state
Each building generator exports, from one run, in the B4 kit's conventions (metres, front -Y in Blender, pivot at foundation centre at ground, T2 shared tileables, linear `wear` attribute, maps max 2048):
- **Frame:** the structure that goes down first (walls, roof, chimney).
- **Parts:** each carried piece as its own mesh (`Part_Door`, `Part_Oven`, `Part_Sign`...) with an empty `SOCKET_<Part>` on the frame where it attaches. A frame without its parts must still read as a building waiting for them (an open doorway, a bare sign bracket), never as a hole.
- **Footprint:** a flat `FOOTPRINT` box (the collider and the NPC-block shape) and the door side marked by `SOCKET_Door`.
- **Shell (fixed landmarks only):** the ruined state as its own export.
- One FBX per building with these as named children, so one builder row brings in every state; parts are hidden until placed. Frame and parts share the same tileable materials and the same `wear` conventions, so they match without a unique bake (controller's call).
- **All four sides are seen** (four turns): detail every side to the level of the beauty corner's plaza fronts at mid distance; only roof backs and undersides stay cheap. The B4 neighbour variants are already freestanding volumes with plain sides; kit family 1 adds a "freestanding" mode that details all four and fits a square class footprint. That is new generator work on the B4 kit, not a free reuse (controller's call).

### 5.3 Foundations and decorations
- **Ruin kit:** foundations per size class, 3 variants each: stone footing, an old doorstep, rubble, weeds (B9 foliage). Placing a building turns the ruin object off (builder and M6 placement), so ruins may stand taller than a plinth: corner stubs up to about 1.4 m, broken wall runs, rubble spilling inward (controller, 2026-10-31, after the first ruin sheet read as a garden-bed border).
- **Decorations** (free grid): the B7 props, benches, planters, banners, bunting, lanterns. They're small, carry a footprint and break back to the player if they block a path.
- **Market stalls (user, 2026-10-30):** fixed in place as part of the square; restored where they stand, like the landmarks.

### 5.4 Kit families for homes (decision 12; controller's proposal)
1. **Half-timber cream** (the approved B4 facade kit: `sc_facade_kit.py` variants).
2. **Stone cottage** (rubble walls like the tower, thatch or tile, low and wide; S class).
3. **Tall jettied townhouse** (three storeys, narrow, leaning; M class, uses the B4 kit pieces).
4. **Workshop with lean-to** (timber and board, open front; S/M, reused for the smithy's outbuildings).
Each family is one generator with seeded variation (storeys, width within the class, roof pitch, plaster and shutter colour from a palette, chimney, flower box, wear) and must export all of 5.2. Shops and landmarks get their own generators.

## 6. Ring 1 asset list (final quality)
| Asset | Type | Class | Parts (carried pieces) | Notes |
|---|---|---|---|---|
| Bakery | unique | L | frame, oven, door, sign (P28) + the baker | Phase 1's M6 target |
| Inn | unique | L | frame, door, sign, porch, (more decided by story later) | |
| Market stalls x3 | variants of B8 | fixed | stall, awning, goods | produce, cloth, tools; ruined and rebuilt states |
| Bell tower | landmark | fixed | shell / bell | generator done (B4 cloud, merging now) |
| Heroes' home | landmark | fixed | n/a | cistern house + bell-loft annex, patched old stone |
| Tithe gate | landmark | fixed | n/a | cracked iron gate, warm breath glow |
| Ruin kit | foundations | S/M/L | n/a | 3 variants each |
| Square ground | B6 extension | n/a | n/a | cobbles over the full 40 x 30 m, lane stubs, edges, drains |
| Square dressing | decorations | free | n/a | benches, planters, banner poles, bunting, more B7 props, lanterns |
| Foliage | B9 extension | n/a | n/a | grass at foundations, ivy on landmarks, weeds in ruins |
| Homes for the square | kit families 1 and 3 | M/S | frame, door, shutters | 2-3 placed in the rebuilt preset |

Kit-ready (final quality, not placed): kit families 2 and 4, the smithy, the schoolhouse, the mill (shell and restored), garden/allotment decorations.

## 7. How it gets made (the team)
- **Per asset (cloud session):** read `docs/art/beauty-corner-brief.md` rules + this contract, write or extend the generator, proof render, reference-beside-render sheet, self-score, rounds until the moving target (25 floor, first score + 5; this applies to every asset, dressing and foliage scored per batch contact sheet like B7/B9). If the bell-tower cloud run proves the cloud can also export, integrate into Unity and pass `unity.sh check`, cloud sessions do the whole chain and merge.
- **Fallback if cloud Unity captures don't work** (controller's call): cloud still exports the FBX, edits the builder table and passes `unity.sh check` (the check itself has run in the cloud before); only captures wait, batched on the PC when the user is free, with the Blender proof renders standing in for the look judgement until then. Decided on the bell-tower report, before Wave 1 starts.
- **Controller (this PC session):** owns this plan, the layout file, `HollowmereBuilder`, merges and the handoff. Scores sheets, names the worst three, sends fix rounds. No Unity or Blender on the PC while the user games.
- **Waves.** While the user games (4 cloud):
  - **Wave 0:** the layout greybox + `HollowmereBuilder` (1 cloud, Unity) and the ruin kit (1 cloud, Blender). The user approves from a labelled top-down map (foundations, classes, landmarks, path graph) plus play-position captures, on their phone (user, 2026-10-30); Wave 1 starts right after.
  - **Wave 1:** bakery, inn, stalls x3, heroes' home (4 cloud).
  - **Wave 2:** tithe gate, square ground + dressing, kit family 3, foliage extension.
  After the user is done gaming (up to 10): kit families 2 and 4, smithy, schoolhouse, mill in parallel, plus integration and capture sessions.
- **Reviews:** art per `sc-asset-review` with the calibrated scoring; `HollowmereBuilder` and layout code get a `reviewer`; anything touching save data or networking gets `risk-reviewer`.
- **Captures:** new fixed cameras from the play positions (square entry from the heroes' home, fountain, tithe gate, a lane mouth), each in the ruined and rebuilt presets, at midday and low sun; the approved corner cameras keep working.

## 8. Not in this plan
- The M6 placement code (validation, path check, pick-up, save). This plan only gives it what it needs: foundations as data, footprints, sockets, the path graph. M6 runs as its own milestone and must implement decisions 2-9.
- Interiors, residents other than the baker, day and night, water in the fountain, the bell's return, dressing the lanes.
- Updating PHASE1: once approved, P26 and 7.1 are amended (buildings on foundations by size class, four turns; decorations stay on the free grid) and P2's scope note gets "ring 1 final, rest kit-ready" moved into Phase 1.

## 9. Anti-goals
- Any placement rule that can soft-lock the player.
- A town that reads as one house copied 30 times.
- Detail where no camera looks; textures above 2048 px.
- Breaking the approved corner's look.

## 10. Risks
- **Footprint drift:** a generator that slips past its size class breaks placement. The builder validates every building's `FOOTPRINT` against its class and fails the build.
- **Frames that read as broken:** the parts contract (5.2) needs every frame judged on its own on the sheet.
- **Cloud integration unproven:** the bell-tower run tells us; until then the PC is the bottleneck for Unity steps.
- **Controller load at 10 sessions:** waves of at most 4 assets in review at once; the rest generate.
