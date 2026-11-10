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
