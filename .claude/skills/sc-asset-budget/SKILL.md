---
name: sc-asset-budget
description: Performance and efficiency budgets for SleepingCalamity models and for the work of making them - triangle and density ceilings per asset class, materials, textures, instancing, where to spend detail, and how to iterate cheaply (runs, renders, tokens, LFS). Use when sizing a new model, when an audit reports FAIL/WARN on tris, density or materials, before a detail pass, or when a generator run or review loop is getting slow.
---

# Budgets for SleepingCalamity models

The facts behind every number:
- **No LODs** in the world scenes. Every triangle is drawn at every distance the object is visible.
- **One shared material per FBX** (`M_Toon_Base` / `M_Toon_Tri` / `M_Millpond_*`, SRP Batcher). Colour comes from uv1 + vertex colour, so a second material is a second draw for no visual gain.
- **Scatter** (ground cover, river stones, lilies) is GPU-instanced with a 40 m draw distance (`MillpondBuilder.AddGroundCover`). Anything placed hundreds of times must be tiny.
- **Textures are shared arrays** (`T_MillpondDetailArray` 32 layers, the env array, the 1024 foliage atlas capped by `RenderMaterialLibrary.EnsureCardAtlasImport`). A model never ships its own texture. Pick a layer.
- **Co-op on modest PCs.** Four players' cameras, water, foliage cards and lights all share the frame.

## Ceilings (measured from the 232 shipped Millpond exports, 2026-10-25)
`Art/Blender/tools/sc_asset_audit.py` enforces these (`--class`):

| class | tris per object | density warn / fail (tris/m2 of surface) | material slots | shipped median |
|---|---|---|---|---|
| prop | 6 000 | 500 / 1 500 | 1 | **1 330 tris per model** (base + `_Tri`; what sc_asset_score and the rubric use), 119 tris/m2 |
| outbuilding (kiln, privy, pump house, < 3 m) | 9 000 | 400 / 1 200 | 1 | duo kiln 3.9k, privy 2.7k, net shed 4.6k |
| building (per FBX half) | 26 000 | 200 / 600 | 1 | cottages 15-26k |
| tree (Tri + Cards each) | 9 000 | 3 600 / 8 000 | 2 | 700 tris |
| rock | 1 500 | 300 / 800 | 1 | 980 tris, 37 tris/m2 |
| card (foliage) | 4 000 | 4 000 / 9 000 | 1 | |
| run (walls, fences, hedges laid in modules; `--class run`) | prop ceilings per module | prop | 1 | **574 tris per metre** (walls 574-699, path edge 252, fence 221); WARN above 2x |
| cover (instanced shore and ground plants; `--class cover`) | **300 per season model (FAIL above)** | 2 000 / 6 000 | 1 | 137 tris per model (FlagIris 182, MarshMarigold 137, FloatingDebris 114: the only FBX cover kinds; tufts, reeds, stones and lilies are built in C#) |
| creature (birds, fish, animals; `--class creature`) | 4 000 | 9 000 / 30 000 | 1 | **1 480 tris per model** (11 shipped animals, 628-3 698; size-independent) |
| caught fish (`--class creature`, hero; coordinator 2026-10-26, from the user's realistic-fish decision 10) | 7 000 | as creature | 1 | every catchable species is held up and shown in the journal close to the camera: spend the extra on sculpted heads (brow, cheek, jaw, teeth) and curved fins. Efficiency may grade 2; the 30/40 gate still applies on the other lines. A swimming LOD near 3 000 follows once the hero passes |
| hero (the tug, a landmark seen up close) | 40 000 | 900 / 2 500 | 2 | |

- **Small objects** (bounding diagonal under 0.8 m: tins, medals, bells) are judged by count: up to **1 600 tris**, with no density check. A 300-tri medal is fine. **Animals are not small objects:** audit them with `--class creature` (no small-object cap, ceiling 4 000, median 1 480; sc-asset-craft `creatures.md`).
- **Scattered or instanced kinds** (placed 50+ times): stones under 150 tris, cover tufts under 300, lilies under 100.
- **Over the ceiling** means cut, don't argue. Known offenders to avoid copying: `Gear_CastNet_*` and `Gear_Keepnet` (10-13k tris of mesh strands). A net reads better as a few thick strands plus a dark interior than as a full mesh.

## Where triangles pay (in this order)
1. **Silhouette and outline breaks** seen from the stands and the tug deck: rooflines, posts, handles, rope loops, broken edges.
2. **Curvature the soft light rolls over:** rounded edges on big forms (`rbox` 2 segs). One rounding loop on a 2 m wall is worth more than 50 bolts.
3. **Large interior relief** that changes the value pattern: courses of stone, slates, planks. Use flat `box` boards (12 tris), not rounded ones (`rbox` 44 at segs 1, 108 at the default segs 2; measured by `sc_kit_costs.py`).
4. **Not:** hidden faces (bottoms on the ground, backs against walls, insides of closed boxes), sub-15 mm details, bolts on the far side, anything under 2 px at 20 m.

Rules of thumb:
- Lathe sides: 8 for posts and handles under 0.1 m radius, 12 for barrels and buoys, 16-24 only for hero round things and for tree trunks with bark ridges.
- Tubes: 6 sides for rope and rails. Thin strands (< 10 mm) use 3-4 sides or become one flat strip.
- Teeth, studs, bolts: `B.box` (12 tris), not `H.prism`/`rbox` (a 14-tooth rack in prisms cost +450 tris in the duo test). Only the silhouette's teeth deserve rounding.
- Delete the faces you can't see: the ground-contact cap of a post, the inside of a wall that is never entered.
- Repeated identical parts in one FBX (fence rails, slates): fine, the batcher doesn't care. Repeated *placements* of a whole prop belong in instancing data (json + MillpondBuilder), not in a bigger FBX.

## Decimating or fixing an over-budget model
- First remove: hidden faces, over-segmented lathes, rounded boxes where a flat box reads the same at 10 m.
- Then reduce: lower `sides`, `segs`, profile points on the flat parts, keep them on the silhouette.
- For an adopted or external mesh (sc-asset-adopt): Blender `Decimate` (collapse, ratio from the budget), with `use_symmetry` for symmetric props. Afterwards re-check the silhouette at 48 px and that the corner normals still make sense (the audit's smear share).
- **Never** bake a normal map to compensate. `SleepingCalamity/Toon` has no normal-map path (removed on purpose). Relief comes from geometry and AO.

## Efficient iteration (the work, not just the asset)
- Build with `--no-export` and `--only <stem>` while iterating. Export once, at the end, through `export_if_changed`, so unchanged FBX stay byte-identical (no LFS churn, no Unity reimport).
- Measure before you look: run `sc_asset_audit.py` first (seconds). Render `sc_asset_preview.py` at `--size 512` throughout (about 6 s; the scorer needs 512, so one size avoids re-rendering). One sheet per round, not a render per tweak.
- Work on one family per generator run (`--only crate,barrel`), not the whole region.
- Don't Read a whole 1 500-line generator. Grep the function you need (`grep -n "^def " file.py`) and read that range.
- A Unity check costs a local session's editor time. Batch model changes and ask for one capture of all of them from the concept cameras, not one per prop.
- Record the numbers (tris before -> after, audit status) in the commit message and on the board card, so the next session doesn't re-measure.
