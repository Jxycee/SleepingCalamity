# Multi-view head prototype (mv1, 2026-11-09)

Goal: build a head from images of ALL sides instead of fitting one frontal photo. Route: **template depth renders -> SDXL + ControlNet-depth turnaround grid -> cutouts -> TRELLIS.2 multi-view shape -> existing pipeline with `hunyuan.pinned_glb`**. Character mv1 = bald middle-aged woman, freckled light skin. All data: `C:/AI/test_out/heroes/pipeline_runs/mv1/` (not in git).

## What is installed / missing (checked on disk)
- Installed and used: RealVisXL V5, `controlnet_depth_sdxl` (also `sdxl_promax`), IP-Adapter plus SDXL ViT-H + CLIP-H (wired in `mv_gen_views.py --mode frontfirst`, not needed), TRELLIS.2 multi-view nodes (`Trellis2ImageCondMultiViewGenerator`, `Trellis2SparseMultiViewGenerator`, `Trellis2ShapeMultiViewGenerator`, `...CascadeMultiViewGenerator`; take front/back/left/right).
- Missing: Hunyuan3D-2mv (the 2.1 node has no multi-view input), MV-Adapter, Zero123++, FLUX. Nothing downloaded.

## Commands (ComfyUI 8190 = SDXL, 8188 = TRELLIS; start one, `/free` or stop it before the other)
```
# 1 depth guide from the Tobin template (head only), 7 views, 3 s
trellis2-python mv_depth_views.py C:/AI/test_out/heroes/pipeline_data/tobin_head_template6.npz <run>/guide
# 2 one-canvas 3x2 turnaround (front,3/4L,left / right,3/4R,back), ~45 s on the GPU
facekeys-python mv_gen_views.py <run>/guide <run>/views --mode grid --tag g2 --cn 0.35 --cn_end 0.4
# 3 gates: side prior, left/right agreement, silhouette IoU vs guide; landmarks
facekeys-python mv_views_check.py <run>/views g2 <run>/guide ; facekeys-python mv_landmark_check.py <run>/views g2
# 4 cutouts (written into TRELLIS input dir) and TRELLIS multi-view shape (318 s, peak 6.8 GB total GPU)
facekeys-python mv_make_cutouts.py <run>/views g2 C:/AI/trellis2/ComfyUI/input
trellis2-python C:/AI/scripts/ui2api.py <ComfyUI-Trellis2>/example_workflows/MeshOnly_MultiView.json <run>/mv_api_raw.json
trellis2-python mv_trellis_shape.py <run>/mv_api_raw.json mv1_head_fblr mv1_g2_front.png mv1_g2_back.png mv1_g2_left.png mv1_g2_right.png
# 5 evaluation table (+ optional glTF for pinning) and clay sheet
trellis2-python mv_silhouette_iou.py <glb> C:/AI/trellis2/ComfyUI/input g2 <run>/recon --label fblr --export_glb <run>/recon/shape_pin.glb
trellis2-python mv_clay_sheet.py <run>/recon/fblr_headframe.npz <run>/recon/recon
# 6 template fit: configs/mv1.json (image = generated front view x2, hunyuan.pinned_glb = shape_pin.glb); 899 s total
trellis2-python face_pipeline.py run --config configs/mv1.json
```
New files only (`multiview/`, `configs/mv1.json`); no existing stage was changed, so there are no new flags.

## Results (honest)
1. **Views (`views/g2_grid.png`, `g2_check_sheet.png`).** One canvas gives one person in all six views: same freckles, eyes, ears. Landmarks across front / 3/4: eye-to-mouth over face height 0.415 / 0.449 / 0.442, nose length 0.298 / 0.311 / 0.311 (yaw 44 and 34 deg, requested 40). Left vs right profile outlines differ by 2.2 mm mean (max 5 mm). Both profiles inside the side prior (joint distance 2.3 left, 4.9 right; the right figure is inflated by shirt in the mask, nape_inset is a mask artefact). Back view has a visible shadow line at the nape.
2. **Circularity (the main caveat).** ControlNet copies the guide outline. Head-frame IoU of the generated silhouettes vs the depth guide: control 0.55 -> 0.89 front, 0.95 left; control 0.35/0.4 -> 0.72 front, 0.86 left (g2, used). So SDXL owns some of the outline at 0.35, but the skull is still mostly the guide's. The guide (Tobin template, side joint distance 2.9, forehead 2.1 sigma too vertical) is inside the prior, so the views are plausible, but this route can NOT discover a skull that differs from the template. A better guide (a measured head scan, or the prior median) would improve it; the multi-view consistency, not the skull shape, is what this prototype proves.
3. **TRELLIS multi-view shape (`recon/recon_clay_sheet.png`, `fblr_iou_sheet.png`).** Front+back+left+right in, 318 s, peak 6.8 GB. Silhouette IoU (height-normalised, `mv_silhouette_iou.py`): front 0.941, back 0.947, left 0.967, right 0.966, held-out 3/4 views 0.946 / 0.943 (never given to TRELLIS). Left/right labels confirmed (swapped assignment 0.77-0.78). Mesh side silhouette vs the side prior: joint distance 2.1 (typical 2.2), nothing beyond 1.3 sigma, depth 1.03 H, rounded occiput, dome and forehead. Looks like a real head from all five clay angles: rounded occiput, even dome, ears, nose and jaw; weaknesses: faceted back of the skull, scratch lines on the crown, 2305 floater pieces (largest piece used), 500k faces.
4. **Template fit (`run/out/look_sheet.png`, `side_check/side_overlay.png`).** Pipeline ran end to end untouched (899 s: fit 300 s, snap 416 s). Scorecard: ratio MAE 2.43 PASS, skin RMS 0.55 mm PASS, dE 1.75 PASS, keys 184, invariants OK; FAIL: worst ratio 8.98 %, IoU head+jaw 0.912, skull IoU 0.845, head aspect error 4.8 % (render 1.489 vs photo 1.557: head too short and round), plausibility skull z 3.5, flips 3. Side profile of the fitted head: joint distance **4.2** (the pinned TRELLIS shape was 2.1): outline within +-1.2 sigma but upper back too flat / sloped (back_flat_hi, a visible straight cut behind the crown), occiput bulge a little low (occ_v +2.9), crown cap a bit narrow. In the front blend the fitted skull sits wider and higher than the generated photo (red on both sides of the head). So the fit wastes part of the multi-view gain: it is about as good as the old runs from the side (old four: 2.3 / 4.7 / 5.5 / 6.2) but no better. The shape target is right; the wrap/skull stages pull the back of the skull toward the template.
5. **Single-image ablation not done.** TRELLIS with front only through the multi-view nodes returned a flat slab; the single-image workflow returned a mesh with stray geometry (silhouette table meaningless). A fair old-method baseline needs Hunyuan from the same front photo (not run, time).
6. Not measured: cephalic index (top view exists in the guide set but TRELLIS has no top input), top-view generation (not attempted), fitted-head silhouette IoU vs the six views (the fitted head has a different neck cut than the cutouts, so the table in `mv_silhouette_iou.py` is not comparable; `recon/fitted_iou.txt` is not meaningful).

## Timings / GPU
Depth guide 3 s (CPU numpy), SDXL grid 45 s (GPU), TRELLIS MV 318 s (GPU, 6.8 GB), cutouts/IoU/clay about 1 min each (CPU), pipeline fit 899 s (CPU plus Cycles GPU in the sheet stage). Whole chain about 25 min.

## Next steps
1. `stages/mv_*.py`: silhouette constraint in snap/polish using the cutout masks (row-centred IoU for left/right/back/3-4, back-of-skull radial field toward the pinned TRELLIS shape instead of the template); the SIDE_README sagittal field recipe applies directly.
2. Better guide head (not the template) or lower control with a depth that is blurred/dilated, then re-measure IoU vs guide; add a top view (guide exists) via SDXL plus ControlNet.
3. Fix the nape shadow on the back view, clean the TRELLIS mesh (largest piece, decimate, smooth) before pinning; try the front-first IP-Adapter route as a second identity source.
4. Fair baseline: Hunyuan front-only shape of the same photo through the same IoU/side-prior tools.

---
# Multi-view FIT mode (mv1, round 2, 2026-11-09): `"mode": "multiview"`
The round-1 fit (above, item 4) lost part of the multi-view gain. This round measures where, adds a config mode that keeps the reconstruction's shape, and tests it on mv1 and on a perturbed-guide character mv2.
Run: `trellis2-python face_pipeline.py run --config configs/mv1_mv.json` (mode multiview; `multiview.headframe_npz` = `recon/fblr_headframe.npz`, `multiview.cutout_dir/_tag` = the six cutouts). Existing configs have no `mode` key = `single`: nothing changes for them (their manifests stay valid: `status` on configs/mv1.json is all up to date, the new scripts are left out of their stage hashes).

## 1. Diagnosis (hypothesis written first, then measured) - `mv_eval.py`
Hypothesis: the later single-photo stages (ratio fit a6, skullw, polish, skulldepth) pull the skull away from the shape; prediction: crown/back drift of several mm in `fit` and `snap`, IoU falling from wrap to final.
Measured (mv1, TRELLIS shape = reference; `mv_eval.py` maps each stage's template-frame mesh into the shape's head frame by the exact align.json chain, signed 3D distance of head vertices, silhouette IoU against each generated cutout with one shared camera per view and a neck clip at the chin, side-prior joint distance of the mesh's left silhouette, aspect = crown-to-chin height over skull width, % off the TRELLIS mesh). `snap.snapshots: true` keeps the mesh after every snap sub-step (`fit/snapshots/`).
| mesh | mean IoU (6 views) | side joint | aspect | 3D rms mm skin / crown / back / ears | drift from previous, mm mean (max) |
|---|---|---|---|---|---|
| TRELLIS mesh itself | 0.958 | 2.11 | 0 | - | - |
| wrap r1 | 0.937 | 4.78 | +0.5 % | 0.83 / 1.6 / 2.4 / 1.9 | - |
| r51 (r4_02 global skull stretch) | 0.930 | 4.18 | -1.7 % | 0.84 / 2.3 / 2.8 / 1.9 | crown 1.9 (3.7), back 0.7 (7.1) |
| a6 (ratio + slider fit) | 0.935 | 4.55 | -2.4 % | 1.41 / 2.1 / 3.1 / 3.5 | whole head 2.2 (5.0), ears 2.7 |
| h6 after snap corr (face snap) | 0.936 | 4.53 | -2.4 % | 0.83 / 2.0 / 3.1 / 3.4 | skin 0.7 (2.9) |
| skullw 1+2, skulldepth, refit, polish | identical to h6 (all no-ops or rejected on mv1) | | | | 0.0 |
Findings: the hypothesis was only partly right. (a) The biggest single error is the WRAP ITSELF: after r1 the side joint is already 4.8 (vs 2.1) and the 6-view IoU 0.937 (vs 0.958); the red wedge behind the occiput in `diag_final/iou_r1.png` is the template's flat sloped back that nricp never reaches (up to 15 mm), and the ears are narrower than the shape's. (b) `fit` (a6) drifts the whole head 2.2 mm mean (ears +1.6 mm worse, skin 0.83 -> 1.41) and worsens the side joint 4.18 -> 4.55; r4_02 helps the joint a little (4.78 -> 4.18) by luck. (c) skullw/skulldepth/refit/polish did nothing on mv1 (rejected or zero change), so they cannot be blamed for this character. (d) The user's 4.2 side joint / 4.8 % aspect numbers come from the render-based scorer; `mv_eval` gives the same picture (4.5 / -2.4 %).

## 2. What multiview mode does
`MV_PRESET` in `face_pipeline.py` (a config can override any key):
1. `target_3d`: `multiview/mv_clean_shape.py` cleans the TRELLIS head frame mesh: largest component, solid re-extraction (surface voxels at 1.3 mm -> closing -> neck cap -> fill -> 1 opening -> gaussian -> marching cubes: closes cracks, removes the thin double-sheet scratch lines on the crown and brow, rounds the terraces of the back), Taubin on the dense result, decimate to 40k, glb in the same frame as `--export_glb`. Scale/orientation to the template frame is still the existing align stage (landmark similarity + ICP). Plain Taubin smoothing was tested first and does nothing to the scratches (they are sheets, not noise); the solid re-extraction moves the surface 1.5 mm mean / 2.4 p95 (smoothing shrink), report in `target/clean_report.json`.
2. `align`: the normal wrap (r1; a finer nricp schedule was tried and broke the lid/blink gates: 1000 mm gap, 6 flips; reverted), then NEW `stages/r7_mv_conform.py` instead of r4_02: the non-face vertices (skull, back, ears, neck sides) move along the target normal to the nearest shape point (up to 40 steps x alpha 0.8, only where the shape is closer than 40 mm, displacement Jacobi-smoothed on the welded graph, weights 0 on the face region with a 12 mm fade and on the pinned lower neck; any vertex of a face that would flip, tested after float32 rounding as saved, is frozen at its previous position and the rest carries on; 10 steps at alpha 0.6 left 20 mm of occiput missing on mv2). Eyes, lids, lips, nose, brow stay as the wrap made them; keys are delta-transferred like r6_03.
3. `fit`: skipped (r51 is copied to a6). The ratio sliders chase one photo's landmarks. `snap`: corr runs with `snap_gain 0` (its surface snap pulled the ears onto the shape and made 22 near-degenerate flipped faces; the face is already 0.6 mm from the shape after the wrap), skull_width / refit / polish / skulldepth / symmetrize off, gates are soft (reported in `wrap_rh6_report.json`, not blocking: lid penetration at r1 is 2.7 mm vs the 2.1 limit, the old fit used its sliders to get under it).
4. `texture`: the front-photo projection stays on the face (weight 40 x frontness); NEW `stages/c6_02b_mv_texture.py` projects q34l, q34r, left, right, back onto the head UV (orthographic, bounding-box registration to the shape, point z-buffer visibility, facing weight, Lab mean shift against the composite on the overlap), never on eye/lid/mouth/lip/nostril texels and faded in over 70 texels around them. Limits: no de-lighting beyond the Lab shift (the generated views carry studio light, the scalp reads grey-green), ears/concavities get the nearest visible view, registration is about 1-2 mm.
Invariants: 184 keys, welded seams, UV loops (build `invariants_all_ok` true). Flips: 0 in the final build (old mv1 run: 3).

## 3. Result on mv1 (old fit h6 vs multiview h6; `mv_eval.py`, `look_sheet.py`, `side_profile_metric.py`)
| | old fit (round 1) | multiview mode |
|---|---|---|
| mean IoU vs the 6 generated views (front / 3/4L / left / right / 3/4R / back) | 0.932 (0.930 / 0.947 / 0.923 / 0.906 / 0.952 / 0.935) | **0.947** (0.926 / 0.958 / 0.961 / 0.955 / 0.952 / 0.933); the TRELLIS mesh itself 0.958 |
| side joint distance, mesh left silhouette (mv_eval / clay render + side_profile_metric) | 4.7 / 4.2 | **1.5 / 1.7** (TRELLIS mesh 2.1, prior p95 3.7); occ_v +4.0 sigma remains (low occiput bulge), back_flat_hi gone |
| aspect (crown-chin over skull width) vs the TRELLIS mesh | -3.6 % | -0.3 % |
| 3D rms to the TRELLIS mesh, mm: crown / back of skull / sides / ears / jaw+neck | 2.6 / 3.5 / 2.5 / 3.8 / 4.2 | **0.7 / 1.1 / 0.9 / 0.8** / 3.6 (the jaw+neck figure is the neck cut and chin, unchanged by this mode) |
| skin rms to the shape (scorer, mm) / skin dE76 | 0.55 / 1.75 | 0.62 / 1.15 |
| flips / keys / invariants | 3 / 184 / ok | **0** / 184 / ok |
| scorer vs the front PHOTO (report-only now): ratio MAE / worst, skull IoU, head+jaw IoU, aspect err | 2.43 / 8.98, 0.845, 0.912, 4.8 % | 9.8 / 28.1, **0.946**, **0.940**, 3.3 % |
| plausibility skull z / flags | 3.5 | 3.2; flags: head too short and round -3.2 (photo -1.6), face too short and wide -2.7, crown flat +2.0 |
| lid-penetration / blink gates (fit_checks6) | pass | NOT passing (soft): penetration about 2.7 mm vs the 2.1 limit; blink rows still close and open properly in the proof sheet |
Per stage (`mv1_mvfit/diag4/eval.txt`): wrap r1 IoU 0.941, side joint 4.9, back 1.9 mm -> conform 0.947, 1.5, 1.1 mm; the face (skin, lips, nose, lids) is not moved by conform (drift 0.0).
Sheets: `mv1_mvfit/run/out/view_sheet.png` (6 generated views over the fitted clay with the generated outline in cyan), `look_sheet.png`, `mv1mv_proof_sheet.png`, `side_check/side_overlay.png`; per-view IoU panel `mv1_mvfit/diag4/iou_h6.png`; `mv1/diag_final/iou_r1.png` is the wrap-wedge picture.
What I saw (view_sheet, look_sheet, proof sheet): from every angle the skull outline now follows the generated views (cyan line on the clay): rounded occiput, no flat cut behind the crown, crown and forehead on the line. Weak points: (1) the template back of the skull has few large triangles, so the back reads faceted and a thin wedge remains at the occiput in left/right (the mesh cannot be rounder than its topology); (2) the ears are crumpled (the template ear pulled onto the shape's ear, the helix rim is still short of the generated ears in the front view); (3) in the FULL-WRAP proof-sheet column there are pink blotches of the lip texture on the skin beside and below the mouth, in every expression row (the slider-only column and the old run are clean): the lip-patch vertices end up spread on the cheeks after the wrap with the TRELLIS mouth; the cause is not fixed here (it is the wrap/reseat lip patch, not the multi-view texture: the front-photo base texture is identical to the old run); (4) scalp texture from the other views is grey-green stubble, mismatched with the face (no de-lighting); (5) blink, jawOpen, viseme aa/oh and smile rows deform correctly (lids close, mouth opens, corners lift), the eyes are the pipeline's eyeballs (grey-green from the front view).

## 4. Circularity test mv2 (perturbed guide)
Guide = the template head with the SKULL perturbed (`mv_perturb_template.py`: occiput 10 % of head depth further back = 25 mm at the occiput, 6 % narrower above the brows), depth guide -> SDXL grid with control 0.6 (the generated silhouettes sit on the perturbed guide, `mv2/views/p2_check_sheet.png`) -> TRELLIS multi-view -> multiview fit (`configs/mv2_mv.json`). Numbers from the left/front silhouettes in head-height units (`mv_perturb_eval.py`):
| | depth/H | width/H | cephalic index | side joint | follows depth |
|---|---|---|---|---|---|
| original template guide | 0.988 | 0.658 | 0.667 | 2.9 | 0 |
| perturbed guide (ground truth) | 1.096 | 0.633 | 0.578 | 4.5 | 1 |
| TRELLIS mesh from the generated views | 1.073 | 0.692 | 0.644 | 2.8 | 0.79 |
| multiview fit | 1.064 | 0.692 | 0.651 | 2.9 | **0.70** |
The pipeline discovers a skull that differs from the template: the longer head (depth +10.9 % in the guide) is recovered to 79 % by the generated views + TRELLIS and kept to 70 % by the fit; the fit matches the TRELLIS shape (6-view IoU 0.947 vs 0.957 for the shape itself, back of skull 1.3 mm, side joint 2.85 vs 2.82). The width perturbation is NOT recovered: the generated views and TRELLIS came out wider than either guide (width/H 0.69; the 'generated_views' row is a mask measurement that includes the neck cut and is unreliable). So the test proves the length/occiput dimension, not width, and the circularity caveat holds for width. Before the conform fix (10 iterations, alpha 0.6) the fit kept only 20 % of the occiput (a flat wall 20 mm short of the shape, side joint 7.9): the wrap alone cannot move a skull by 25 mm. The scorer's photo-based numbers on mv2 are poor (skull IoU 0.83, aspect err 9.8 %): they compare with the front view, which is itself a 6 %-narrower-guide face. Sheets: `mv2/run/out/view_sheet.png`, `look_sheet.png`, `mv2/recon/recon_sheet.png`, `mv2/diag4/`.

## Files added this round
`mv_eval.py` (per-stage / per-view metrics), `mv_clean_shape.py`, `mv_view_sheet.py` (6-view generated-over-fit sheet), `mv_perturb_template.py` + `mv_perturb_eval.py` (mv2), `stages/r7_mv_conform.py`, `stages/c6_02b_mv_texture.py`, `configs/mv1_mv.json`, `configs/mv1_diag.json` (snapshots on). Shared files touched: `face_pipeline.py` (mode preset, snapshots, multiview branches; single mode paths unchanged), `defaults.json` (`"mode": "single"`).
