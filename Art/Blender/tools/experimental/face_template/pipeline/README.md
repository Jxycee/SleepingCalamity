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
`image, name, out_dir` required. `prep` (crop `box`, `ellipses` head/face/neck in photo px, `cut_rows`, `shoulder_cols`, `side_cuts`; `box: null` = automatic from the mediapipe face box: check `target/mask_vis.png`). `hunyuan` (`seeds`, `steps`, `faces`, `pinned_glb`, `trellis_tex_fallback`). `iris_colour` (`"auto"` = photo: cyan window, else any saturated iris hue; `null` = keep Tobin's iris; `[r,g,b]` or `"#rrggbb"`). `template` (root/npz/sliders_dir/data_dir, `sliders_enabled`). `align`, `skull`, `render`, `fit` (`seed_theta`, `iters`, weights), `snap`. `thresholds` = finishing criteria (ratio MAE <= 2.5 %, worst ratio <= 5 %, IoU head+jaw >= 0.93, skull >= 0.95, 3D skin RMS <= 0.95 mm, skin dE76 <= 2.0, keys >= 184). `switches` (`teeth_opt`, `render_sheet`, `skip: {stage: true}`). `compare_to` = another score.json for delta columns. Gate tolerances are fixed in `stages/fit_checks6.py` (lid penetration <= template + 0.45 mm, blink gap <= template + 0.5 mm, lip rings 0.7-1.4, 0 flips).

## Add a new character
1. Put the photo anywhere; `cp configs/cloud_v1.json configs/mychar.json`; set `image`, `name`, `out_dir`; delete the Cloud-specific `prep` box/ellipses/cuts (or set `box: null`), delete `hunyuan.pinned_glb`, set `hunyuan.seeds` (e.g. `[1,2,3]`), remove `compare_to`.
2. `python face_pipeline.py run --config configs/mychar.json --to target_3d`, then read `target/mask_vis.png` (head fully inside, neck not the shoulders) and `target/glb/clay_s*_front.png` + `pick.json`. If the cutout is wrong, set `prep.box/ellipses/cut_rows` and `--force prep_image`.
3. Run the rest. Read `score` and the proof sheet.

## Failure modes and what to look at
- GrabCut cutout wrong (hair/background in, ear cut off): fix `prep` windows by hand; every later stage uses `mask_vis.png`.
- Hunyuan: no face on the clay render (pick score -9) or the face turned: use a frontal photo; the seed pick only compares IoU and ratios, look at the clay yourself. Ports: 8190 Hunyuan, 8188 TRELLIS; the stage frees 8188's models first and keeps GPU under 82 C.
- Gates fail in `fit`/`snap` (blink gap, lid penetration, ring ratios, flips): the log in `logs/<stage>.log` says which gate; the fit already bisects to the last feasible step, a hard fail means the seed is infeasible: try the default seed (`pipeline_data/warp_seed_default.json`, the Cloud r5 theta; a zero seed lands near 4.3 %).
- Detector noise (0.2-0.6 mm) bounds the ratio MAE near 3 %; the Cloud result (3.7 %) does not meet the finishing thresholds either.
- Eye-ring gate is already near its limit for the base mesh, eye sliders only move ~0.02.
- Human eye needed: eyes (smaller/rounder than the photo), brows (faint pale patch near the outer brow), teeth read, nose width (-6 %), forehead midline crease, ears (Tobin's), skin tone on the shadow-side cheek.

## When a stage fails
`status` shows which; read `logs/<stage>.log` (full stdout/stderr of every command). Fix the config, then `run` again (finished stages are skipped), or `--force STAGE` / `clean --stage STAGE`.

## Files
`face_pipeline.py` orchestrator; `paths.py` all paths/tools (stage scripts read `SCP_*` env vars set from the config); `defaults.json`; `configs/`; `stages/` = the round 2-6 scripts (algorithms unchanged; hard-coded face_spike paths replaced by the env paths) + `prep_image.py`, `target_pick.py`, `photo_landmarks.py` (from cloud_landmarks), `score_final.py`, and the iris option in `c6_03_eyes.py`. Shipped data (outside git): `C:/AI/test_out/heroes/pipeline_data/` (tpl_hits/tpl_cam/lm_ids = template landmark cache, `warp_seed_default.json`, `sliders/new_sliders.npz`, `eye_sliders6.npz`, `cloud_shape_s2.glb` = pinned Hunyuan shape of the round-6 run).
