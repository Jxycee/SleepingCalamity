# Face pipeline: one photo -> a head on Tobin's MetaPerson topology

`face_pipeline.py run --config cfg.json` takes one reference photo and produces a character head on Tobin's 7321-vertex head (UVs, the 150 base keys + 34 SC_ keys = 184, lashes/eyes/teeth re-seated, photo-projected texture, optional iris recolour), plus a scorecard and a proof sheet. It is the automated form of face-template spike rounds 2-6 (tag h6). Scripts only live in git; all data is in `<out_dir>` (default `C:/AI/test_out/heroes/pipeline_runs/<name>/`).

```
python face_pipeline.py run    --config configs/cloud_v1.json [--from STAGE] [--to STAGE] [--force STAGE]
python face_pipeline.py status --config ...      # stage table: done / stale / failed, seconds
python face_pipeline.py score  --config ...      # scorecard vs the thresholds (and delta vs compare_to)
python face_pipeline.py clean  --config ... --stage STAGE | --all
```
Interpreters (stdlib orchestrator calls them by subprocess; paths in `paths.py`): Blender 5.2 (background), trellis2 venv python (numpy/scipy/trimesh/cv2), facekeys python (mediapipe), ComfyUI venv (Hunyuan). No Unity.

## Stages (each: declared inputs/outputs in `manifest.json` with a hash of scripts + params + input contents; unchanged stages are skipped, a changed param or upstream output re-runs it and everything after)
| # | stage | does | outputs (under out_dir) | GPU | Cloud run |
|---|-------|------|-------------------------|-----|-----------|
| 1 | prep_image | crop, GrabCut cutout, grey composite, 2x Lanczos to 1536 | target/{crop_src,mask_vis,cutout_rgba}.png | no | 2 s |
| 2 | target_3d | Hunyuan 2.1 shape per seed (ComfyUI 8190), clay front render, auto pick = silhouette IoU - 2 x ratio MAE vs the photo; or `hunyuan.pinned_glb` | target/shape.glb, pick.json | yes (~2 min/seed) | pinned: 0 s |
| 3 | photo_landmarks | mediapipe 478 + yaw/pitch | target/landmarks.json | no | 1 s |
| 4 | template_prep | template cache files; append 28 SC_ + 6 eye sliders as real keys (184 keys) | fit/tobin_head_template6.npz | no | 1 s |
| 5 | align | export target, landmarks+rays, Umeyama + trimmed ICP, crop, slider fits, nricp wrap W1, skull height stretch | fit/wrap_r51_*, lm_51, rn_51 | no | 46 s |
| 6 | fit | closed loop (detector in the loop): 16 warp params + 38 slider columns, hard gates, seeded from `fit.seed_theta` | fit/wrap_ra6_* | no | see below |
| 7 | snap | smoothed snap onto the 3D target, deflip, gates re-run, delta transfer of all 184 keys | fit/wrap_rh6_*, rn_h6, lm_h6 | no | |
| 8 | key_transfer | checks the transferred key block (shape, NaN), records max delta | fit/key_transfer.json | no | |
| 9 | reseat | eyeballs follow lid rings, jaw-key hinge rescale, lashes, per-jaw teeth fit (slow) | bake/parts.npz, reseat.json | no | |
| 10 | texture | map fit, UV projection, de-light, fill, ears rule, lips mask, brow thinning, iris recolour | bake/*_char.png, texture.json | no | |
| 11 | build | blend with invariants asserted (head 7321 verts/184 keys, other meshes unchanged, UVs equal, no NaN) | out/char.blend | no | |
| 12 | score | ratio MAE/worst, IoUs, 3D RMS, skin dE, gates, invariants, keys vs thresholds | out/score.json | no | |
| 13 | sheet | Cycles CPU cells, proof sheet, closeups, overlay | out/<name>_{proof_sheet,closeups,overlay}.png, <name>.blend | no | |

Stages 6-9 are the long ones; stage 1-5 and 7 are deterministic and were checked bit-exact against the round-6 files (`wrap_r1`, `wrap_r51`, `wrap_ra6`, `wrap_rh6`: max abs diff 0).

## Config (`defaults.json` holds every default; a config only lists what differs)
`image, name, out_dir` required. `prep` (`bg_seed` (default true: pixels near the border colour are sure background; cloud_v1 sets false), crop `box`, `ellipses` head/face/neck in photo px, `cut_rows`, `shoulder_cols`, `side_cuts`; `box: null` = automatic from the mediapipe face box: check `target/mask_vis.png`). `hunyuan` (`seeds`, `steps`, `faces`, `pinned_glb`, `trellis_tex_fallback`). `iris_colour` (`"auto"` = photo: cyan window, else any saturated iris hue; `null` = keep Tobin's iris; `[r,g,b]` or `"#rrggbb"`). `template` (root/npz/sliders_dir/data_dir, `sliders_enabled`). `align` (`soft_gate`: true = a start state that misses a blink-gap gate is a warning, the fit stage re-enforces the gates), `skull`, `render`, `fit` (`seed_theta`: null = zero, `iters`, weights), `snap`. `thresholds` = finishing criteria (ratio MAE <= 2.5 %, worst ratio <= 5 %, IoU head+jaw >= 0.93, skull >= 0.95, 3D skin RMS <= 0.95 mm, skin dE76 <= 2.0, keys >= 184). `switches` (`teeth_opt`, `render_sheet`, `skip: {stage: true}`). `compare_to` = another score.json for delta columns. Gate tolerances are fixed in `stages/fit_checks6.py` (lid penetration <= template + 0.45 mm, blink gap <= template + 0.5 mm, lip rings 0.7-1.4, 0 flips).

## Add a new character
1. Put the photo anywhere; `cp configs/cloud_v1.json configs/mychar.json`; set `image`, `name`, `out_dir`; delete the Cloud-specific `prep` box/ellipses/cuts (or set `box: null`), delete `hunyuan.pinned_glb`, set `hunyuan.seeds` (e.g. `[1,2,3]`), remove `compare_to`.
2. `python face_pipeline.py run --config configs/mychar.json --to target_3d`, then read `target/mask_vis.png` (head fully inside, neck not the shoulders) and `target/glb/clay_s*_front.png` + `pick.json`. If the cutout is wrong, set `prep.box/ellipses/cut_rows` and `--force prep_image`.
3. Run the rest. Read `score` and the proof sheet.

## Failure modes and what to look at
- GrabCut cutout wrong (hair/background in, ear cut off): fix `prep` windows by hand; every later stage uses `mask_vis.png`.
- Hunyuan: no face on the clay render (pick score -9) or the face turned: use a frontal photo; the seed pick only compares IoU and ratios, look at the clay yourself. Ports: 8190 Hunyuan, 8188 TRELLIS; the stage frees 8188's models first and keeps GPU under 82 C.
- Gates fail in `fit`/`snap` (blink gap, lid penetration, ring ratios, flips): the log in `logs/<stage>.log` says which gate; the fit already bisects to the last feasible step, a hard fail means the seed is infeasible: change `fit.seed_theta` (default `null` = zero theta, the better start for a new face: char2 2.74 % vs 3.76 % with the Cloud theta; `pipeline_data/warp_seed_default.json` is the Cloud r5 theta, set in cloud_v1.json).
- Detector noise (0.2-0.6 mm) bounds the ratio MAE near 3 %; the Cloud result (3.7 %) does not meet the finishing thresholds either.
- Eye-ring gate is already near its limit for the base mesh, eye sliders only move ~0.02.
- Human eye needed: eyes (smaller/rounder than the photo), brows (faint pale patch near the outer brow), teeth read, nose width (-6 %), forehead midline crease, ears (Tobin's), skin tone on the shadow-side cheek.

## When a stage fails
`status` shows which; read `logs/<stage>.log` (full stdout/stderr of every command). Fix the config, then `run` again (finished stages are skipped), or `--force STAGE` / `clean --stage STAGE`.

## Files
`face_pipeline.py` orchestrator; `paths.py` all paths/tools (stage scripts read `SCP_*` env vars set from the config); `defaults.json`; `configs/`; `stages/` = the round 2-6 scripts (algorithms unchanged; hard-coded face_spike paths replaced by the env paths) + `prep_image.py`, `target_pick.py`, `photo_landmarks.py` (from cloud_landmarks), `score_final.py`, and the iris option in `c6_03_eyes.py`. Shipped data (outside git): `C:/AI/test_out/heroes/pipeline_data/` (tpl_hits/tpl_cam/lm_ids = template landmark cache, `warp_seed_default.json`, `sliders/new_sliders.npz`, `eye_sliders6.npz`, `cloud_shape_s2.glb` = pinned Hunyuan shape of the round-6 run).

## Second character (char2, 2026-11-09): bald middle-aged woman, local SDXL photo (`sdxl_char2.py`)
Config `configs/char2.json` (Cloud theta seed, run dir `pipeline_runs/char2`) and `configs/char2_zero.json` (zero theta, pinned to char2's shape, run dir `char2_zero`): the zero-theta run is the better one and its settings are the new defaults. Result (zero theta): ratio MAE 2.74 %, worst 5.57 %, IoU 0.967/0.955, 3D skin RMS 0.76 mm, skin dE76 2.51, gates pass in fit/snap, 184 keys, invariants OK, 2 whole-head triangle flips (score flips threshold 0 fails: see below). Cloud-seed run: MAE 3.76, worst 10.2, dE 2.13. Times: Hunyuan 3 seeds 220 s, fit 1230-1380 s (3-4x Cloud's), total 26-29 min.
**Worked untouched:** landmarks (frontal photo), template prep, reseat (teeth fit), texture with iris recolour (hazel eyes picked up from the photo), build invariants, score, sheet. Auto Hunyuan pick (seed 3 here; seed 1/2 were worse silhouettes).
**Manual steps:** only the photo and the config (image, name, out_dir, seeds). No hand windows.
**Bugs fixed (generic):** (1) `prep.side_cuts` default [150,650] were Cloud photo columns: they cut the face in half on any other photo; default is now null. (2) New `prep.bg_seed`: grey studio background leaked into the cutout beside the jaw (GrabCut sure-face ellipse is wide); pixels within Lab distance 10 of the border colour are now sure background. (3) Hunyuan shapes were cached by file name only, so a changed cutout reused old shapes: each `shape_sN.glb` now carries a `.key` (cutout + steps + seed + faces hash) and is regenerated when it differs. (4) `r4_02_global.py` used the round-3 `fit_checks` (gap mean +0.3, penetration +0.25 mm); it now uses `fit_checks6` like the other stages, and `run_checks` returns a `fails` list that is printed. (5) Align still failed the left blink gap mean (template + 0.5 mm) on this face before the fit had run: `align.soft_gate` makes it a warning; fit and snap still enforce the gates and passed. (6) `fit.seed_theta: null` is now supported and the default.
**Config fields that mattered:** `fit.seed_theta` (null vs Cloud theta: MAE 2.74 vs 3.76, worst 5.6 vs 10.2, skull IoU), `align.soft_gate`, `hunyuan.seeds`, `prep.bg_seed`.
**New failure modes:** photo-side Cloud constants (side cuts); stale Hunyuan cache; a start state that violates a blink gate before the fit (soft_gate); fit time varies 3-4x; the 2 whole-head flips appear already in the align wrap (r4 global step) and are not cleaned up by the later stages, so the `flips <= 0` criterion fails (cause not investigated).
**Likeness (eyes only, no numbers):** front: good, face length, nose, mouth, jaw and eye spacing match; skin tone is more orange and smoother than the photo, wrinkles are only the baked texture, brows faint. 3/4 and side: plausible, the ear and ear-to-jaw region is still Tobin's, side is untested against a photo. Expression rows (blink, jaw, visemes, smile) deform correctly but with Tobin's creases, not the older face's. The crown follows the shaved scalp; buzz-cut stubble is not modelled.
### Adding a character in 5 steps
1. Make or pick a frontal, even-light photo with margin around head and neck (bald/buzz cut only; hair is not modelled).
2. `cp configs/char2_zero.json configs/mychar.json`; set `image`, `name`, `out_dir`; remove `hunyuan.pinned_glb` and set `hunyuan.seeds` [1,2,3].
3. `python face_pipeline.py run --config configs/mychar.json --to target_3d`; read `target/mask_vis.png` (head inside, no background wedges) and `pick.json`; if wrong set `prep.box/ellipses/cut_rows`, `--force prep_image`.
4. Run the rest (about 25-30 min), then `score`; if a gate fails in fit/snap read `logs/fit.log` (`fails=[...]`).
5. Look at `out/<name>_proof_sheet.png` and `_overlay.png` (head height and width over the photo) before using the head.

## Round 7 fixes (2026-11-09): flips, eye-following render, skull width, chroma flatten
Final scorecards (final tag h6):
| criterion | threshold | char2_zero | cloud_v1 (r6 ref) |
|---|---|---|---|
| ratio MAE % | <= 2.5 | 2.04 PASS | 3.36 FAIL (3.70) |
| worst ratio % | <= 5 | 5.39 FAIL (3.4-6.6 with +-1 % render framing) | 7.20 FAIL (8.77) |
| IoU head+jaw (row-centred) | >= 0.93 | 0.963 PASS | 0.925 FAIL (0.889) |
| IoU skull | >= 0.95 | 0.976 PASS | 0.985 PASS (0.899) |
| 3D skin RMS mm | <= 0.95 | 0.702 PASS | 0.892 PASS (0.845) |
| skin dE76 | <= 2.0 | 1.974 PASS | 1.748 PASS (1.758) |
| flips | 0 | 0 PASS | 0 PASS |
| invariants / keys | ok / 184 | PASS / 184 | PASS / 184 |
- **Flips:** the 2 flips came from the nricp wrap itself (r1): a degenerate 0.05 mm2 template sliver at the left inner eye corner, and a thin ear/neck-side face that nricp threw out. `fit_common.repair_flips` smooths the displacement field locally (patch grows ring by ring, boundary fixed) and runs at the end of `r3_02_wrap` and `r4_02_global`.
- **Eye-following render (biggest ratio fix):** scoring and the snap rendered the head with the template eyeballs in place, while the fit loop moves them with the lids; the detector (eye corners = inter-ocular normaliser) read differently (char2: 0.84 % in the loop vs 2.14 % on the score render). `r6_prep_render.py` now makes the eyes follow the lid rings.
- **Skull width:** new `r6_04_skullw.py` (snap stage, `snap.skull_width`, default true): smooth x-scale above the brow line solved from photo-vs-render row half-widths (char2 crown was 14 % too wide; skull IoU 0.93 -> 0.976).
- **Chroma flatten:** `c6_02_texture.py` also pulls the large-scale a/b cast toward the photo skin tone (`FLATC`, default 0.9); char2 dE 2.51 -> 1.97.
- **Not fixed:** worst ratio on char2 (5.39 vs 5.0) is inside detector noise: re-rendering the same mesh with +-1 % camera framing gives MAE 1.3-1.8 and worst 3.4-6.6. A landmark-pair snap (`snap.damp` 1) was tried and made it worse (MAE 2.46, worst 6.1), so damp stays 0. Cloud still misses MAE/worst/head-jaw IoU (the Hunyuan target and Cloud theta seed bound it).
