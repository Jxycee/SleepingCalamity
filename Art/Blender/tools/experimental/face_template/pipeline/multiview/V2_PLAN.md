# Face pipeline v2 plan (evaluation of the new local models, 2026-11-09)

Full write-up with the decision table: `C:/AI/test_out/heroes/model_research/INTEGRATION.md` (not in git). Everything here was measured on mv1 with the helper scripts in `multiview/v2/`. No existing stage was changed.

## What changed in the picture
1. **Views: FLUX.2 klein 9B (fp8) multi-reference edit beats SDXL+ControlNet-depth** for photoreal identity: 12-15 s per view, same freckles/eyes/ears from one front photo, natural neck and skull. It ignores a depth reference (no geometric control), so the skull is "plausible", not calibrated to the template. SDXL+depth stays the calibrated option (circular, see README).
2. **Shape: Hunyuan3D-2mv (kijai wrapper) replaces TRELLIS multi-view**: 53 s and 5.9 GB peak (TRELLIS 318 s, 6.8 GB), no scratches and 22 floater faces instead of 2305 pieces, rounded back of skull, clean nose/lips/ear. On the mv1 SDXL views: silhouette IoU front .892 / left .946 / right .932 / back .879 / held-out 3/4 .933 / .941; side joint 3.4 (TRELLIS 2.1, prior p95 3.7).
3. **Pink lip blotches come from `stages/r7_mv_conform.py`** (multiview mode). Same texture and camera, per-stage meshes (`v2_eval/H_stage_sheet.png`): old run r1/r51/a6/h6 clean; multiview r1 (wrap) clean; multiview r51 (= r7 output) and h6 blotchy. r7 zero-weights `M['face']`, eye/lip-line/seam sets and `mouth_interior` but not `lip_patch_upper_verts/lower_verts`; it moves 238 of the 800 patch vertices by >0.5 mm (max 13.3 mm) while the skin around the mouth moves 0.05 mm, so the lip-texture layer (1434 faces over the main lips) detaches and pierces the skin. Deleting the patch faces removes all blotches; undoing only that displacement removes them too (`H_fix_sheet.png`). The albedo texture is clean.
4. **Hybrid works**: skull/back from the multiview fit, face + ears from the single-photo slider fit (`hybrid_face.py`): no blotches, clean lips/nose/ears, rounded back (sheet `v2_eval/E_hybrid_sheet.png`).

## Proposed v2 steps
| # | step | model / tool | time | note |
|---|---|---|---|---|
| 1 | identity front photo | user photo or klein 9B fresh portrait | 15 s | |
| 2 | left/right/back/3/4 views | klein 9B fp8 edit (`klein_views.py`), 2 seeds each | 15 s/view | pick the seed whose left/right silhouettes mirror best; gate with `mv_views_check.py` |
| 3 | cutouts | `cutout_generic.py` (shared neck cut) | s | |
| 4 | shape | Hunyuan3D-2mv (`hy3d_mv.py`, octree 384) | 55 s | then `mesh_prep.py` (largest piece, decimate) |
| 5 | score | `mv_silhouette_iou.py` + side prior | 1 min | gate: mean IoU >= .90, side joint <= 3.7 |
| 6 | wrap + conform | existing r1 + r7 on the Hunyuan shape | existing | |
| 7 | face region | single-photo slider fit (existing fit/snap) of the same front photo | existing | |
| 8 | merge | `hybrid_face.py --ears` (optional once the r7 fix is in) | s | face smoothness + clean ears |
| 9 | texture | front projection + multi-view c6_02b (klein delight tested: does not de-light, skip) | | |

## Fix for the lip patch (one line, not applied: stages are frozen this round)
In `stages/r7_mv_conform.py` add `'lip_patch_upper_verts', 'lip_patch_lower_verts'` to the tuple of sets that are forced into the zero-weight `face` mask. Add a gate: no lip-patch vertex may move more than 1 mm relative to its paired skin vertices in any conform/snap step.

## Scrap
SDXL+ControlNet-depth grid as the shape source (circular), TRELLIS multi-view shape, FLUX.1 Depth (traces the guide, ages the face), MV-Adapter i2mv on 12 GB (79 s/step), nvfp4 klein on this machine (slower than fp8, no triton), single-photo ratio fit for the skull.

## Risks
Klein views are not calibrated: the Hunyuan mesh from klein views had depth 1.08 H (prior .95 +- .06), joint 5.7, held-out 3/4 IoU .78. Use klein views for identity/texture and, until a calibrated step exists, SDXL+depth views for shape (joint 3.4). The hybrid face is the single-photo face: the multi-view gain on the face region is nil, by design.

## Built result (mv1_v2, overnight iteration)
Config `configs/mv1_v2.json`: Hunyuan3D-2mv shape (`v2/hy_headframe.py` maps it into the TRELLIS head-frame scale; the mesh_prep'd mv1-views mesh), mode multiview, **r7 with the lip-patch fix (applied in `stages/r7_mv_conform.py`)**, then `v2/apply_donor_ears.py` (donor ears from the single-photo slider fit, one similarity per ear, 18 mm fade; skull-source face kept), multi-view texture projection OFF (`multiview.textures.enabled=false`: the generated side views put a grey-green cast, warped ear texels and a dark band under the chin onto the UVs; face comes from the front photo, the rest from the template tone). Run dir `pipeline_runs/mv1_v2/run` (proof sheet `out/mv1v2_proof_sheet.png`, blend `out/mv1v2.blend`). Result: no lip blotches, clean lips/nose/jaw (the Hunyuan-wrapped face is clean now), clean ears (2 flips before the build, 0 final), blink / jawOpen / visemes / smile rows work, teeth show. Scorer vs the front photo is poor (ratio MAE 9.2, aspect err 12 %, skull z 6.7: the Hunyuan head is long and narrow relative to the photo; those ratios are not gates in multiview mode). Open: a pale cap-shaped band at the hairline/crown (front-projection boundary, under the hair for Tobin), freckle density only on the front projection.
Tried and dropped: slider-fit face swapped over the Hunyuan face (`piece_merge.py` without `--noface`): adds crown and cheek ridges at the fade, no longer needed once the lip patch is fixed.
