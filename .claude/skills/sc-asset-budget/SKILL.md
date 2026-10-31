---
name: sc-asset-budget
description: Performance and efficiency budgets for Sleeping Calamity models and for the work of making them - triangle, texel-density and texture-set budgets per asset tier (tileables, town and cellar kit pieces, instanced sculpted pieces, hero props, buildings, characters, enemies), where to spend detail, and how to iterate and batch cheaply (bakes, renders, tokens, LFS). Use when sizing a new model or family, when an audit reports tris or density over budget, before a detail pass, or when a generator run, bake or review loop is getting slow.
---

# Budgets for Sleeping Calamity models

**The numbers are in `docs/PHASE1.md` section 3.3** (texel density, LOD0 triangles and texture sets per class) and section 9 (usage per batch). Read them there; they are draft calls ([P35], [P41]) and may change. This skill says how to apply them.

The facts behind them:
- **Quality first** (CLAUDE.md): the target is the look, not a generic frame-rate rule. Budgets exist so a whole building or a cellar room still fits, not to strip detail.
- **Most of a scene is shared** (tiers, handoff 3.5): tileables and trim sheets (T1) and kit pieces (T2) carry no unique texture, so they cost almost nothing per piece. Unique bakes are for T4 heroes and the T3 family atlases.
- **Normal maps are expected.** Real PBR sets (albedo, normal, ORM) carry the micro-detail; geometry carries silhouette and centimetre-scale form.
- **Repeated meshes are instanced:** URP's GPU Resident Drawer batches MeshRenderers that share a mesh (Forward+, `docs/research/urp-stylised-realism.md` section 7). Many copies of one small mesh are cheap; many unique meshes are not.
- **Co-op:** up to four players' cameras, volumetrics, SSAO and lights share the frame (urp-stylised-realism section 10). Alpha-tested foliage shadows are a common hidden cost.

## Applying the budgets
| Tier | Budget (PHASE1 3.3) | Judge it as |
|---|---|---|
| T1 tileable / trim | 2K shared sets; 1024 px/m (ground, walls up to 3 m), 512 px/m (upper storeys, roofs, vault crowns) | texel density on the kit pieces that use it |
| T2 kit piece | 0.5-5k tris, shared textures | per piece, and per metre of run against its kit neighbours |
| T3 instanced piece | 50-400 tris each, one 2K set per family | per variant; the family's atlas is the texture cost |
**Texture cap (user, 2026-10-30): the game targets 1440p, so nothing bakes above 2048 px.**

| T4 hero prop | 30-60k tris, up to 2 x 2K | per prop |
| Whole building (kit + heroes) | at most 75k tris; 6x2K + 2x1K | summed over every piece that belongs to it |
| Hero character / enemy | 60k, 2K body + 2K head / 25k, one 2K set | per character |

- `Art/Blender/tools/sc_asset_audit.py` (rewritten M8) reports tris, surface area, density, UV texel density against these budgets, colour attributes and bounds per object, and FAILs only on triangles and material slots. Report its numbers; the table above is the judge.
- **Over budget usually means cut,** unless the extra tris are on the silhouette or a story beat the brief names: then say so in the header and the report. The budgets themselves are placeholders ([P35]) and the controller can change them.
- A building's 75k includes its instanced roof tiles and stones: count the instances, not the variants.

## Where triangles pay (in this order)
1. **Silhouette and outline breaks** seen from play positions: ridges, chimney stacks, overhangs, hoops, handles, broken edges.
2. **Centimetre-scale form the normal map can't fake at a grazing angle:** chamfers of about 1.5 cm or more (PHASE1 3.4), proud joinery, coursed stones that stand out of the wall.
3. **Smooth profiles on turned parts:** enough lathe sides and resampled profile steps that cylinders don't facet into stripes.
4. **Not:** hidden faces (bottoms on the ground, faces into a wall, buried parts), very fine detail (roughly under 1 mm tends not to register at review distance; put it in the normal map), anything that covers only a pixel or two at play distance.

Rules of thumb:
- Delete the faces you can't see, in the generator and at assembly (the cottage's stone kit only fit once the wall-facing and buried faces were culled).
- Repeated identical parts are instances of one mesh, not copies baked into a bigger FBX.
- Spend texels where the camera is close: hero props and ground at the top of the range, roofs and vault crowns at 512 px/m.

## Fixing an over-budget model
- First remove: hidden faces, over-segmented lathes on parts that aren't round on screen, bevel loops the bake already gives.
- Then reduce: collapse-decimate organic and chipped lows (keep the silhouette); keep clean source lows on turned and hard-surface parts and lower their sides instead.
- Then move detail into the bake: a dense high baked to a lighter low is the normal path here.
- For an external or AI mesh: decimate per part (`sc_asset_adopt.py --list-parts`, `--part-tris`) rather than one ratio for the whole join, which faceted small round parts while thin ropes stayed dense.

## Efficient iteration (the work, not just the asset)
- **Batch by family** (PHASE1 9): one generator per family, all items in one headless Blender session, one joined Cycles bake per batch, one small JPEG contact sheet per batch.
- **Join highs before a selected-to-active bake:** about 1 h down to 4.5 min on the cottage.
- **Voxel size** about `min(w, h) / 12`, 4-6 mm worked on the cottage: finer gave 11M tris and a 6.3 GB peak.
- Build with `--no-export` and `--only <stem>` while iterating; export once, at the end, only when the output changed, so unchanged FBX and PNG stay byte-identical (no LFS churn, no Unity reimport).
- Measure before you look: the audit takes seconds. One review render per round, not one per tweak.
- **Spend the expensive model on judgement, not typing:** Sonnet agents run generators and renders from a brief; the controller scores the sheet (PHASE1 9). Keep sheets small JPEGs (2000 px or less: big PNGs posterise when viewed).
- Don't Read a whole generator. Grep the function (`grep -n "^def " file.py`) and read that range.
- A Unity capture costs the PC's editor time. Batch changes and ask for one round of captures for all of them.
- Record the numbers (tris before -> after, texture sets, bake time) in the commit message and the report, so the next session doesn't re-measure.
