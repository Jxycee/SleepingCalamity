# Side-profile skull prior and metric (round 16, 2026-11-09)

Why: the frontal plausibility prior (`human_prior.json`) cannot see a skull that is wrong in side view. This adds a side prior and a read-only scorer. Nothing here edits a stage or a run file; the scorer writes only `<run>/out/side_check/`.

| file | what |
|---|---|
| `side_gen_faces.py` | SDXL (RealVisXL V5, ComfyUI 8190) bald portraits: 150 side + 40 "3/4" |
| `side_prior.py build` | segments each portrait, measures it, writes `side_prior.json`; `--qc dir` writes contact sheets with the silhouette, anchors and reject reason |
| `side_prior.json` | prior: 11 scalar features (median, 1.4826 MAD sigma, p5/p95, per-sex medians), back/front extent curves, radial profile r(theta), PCA of r(theta) with joint-distance percentiles, per-image scalars |
| `side_profile_metric.py <run_dir> [--tag h6]` | clay side render (Blender, fixed camera, into `out/side_check/`), z-scores in words, `side_check.json/.txt`, `side_overlay.png` |

Run: `C:/AI/envs/facekeys/Scripts/python.exe side_profile_metric.py C:/AI/test_out/heroes/pipeline_runs/<run>` (about 15 s; the prior needs no GPU). Rebuild: start ComfyUI (`C:/AI/stablegen/start_comfy_8190.bat`), `side_gen_faces.py` (about 25 min for 190), `side_prior.py build --qc <dir>`. Portraits: `C:/AI/test_out/heroes/vision_prior/side/`.

## Method
- Silhouette: plain backdrop, so the background is a quadratic Lab surface fitted to the top/side border strips and the foreground is Lab distance > 9 (GrabCut cut through pale and stubbled scalps and was dropped). Mirrored to face left like the clay side render.
- Anchors (silhouette only, same code for photo and mesh): crown (top row), nose tip (most forward point near mid head), menton (the row where the front contour jumps back under the chin). H = crown-to-menton. Frame: u = (x - nose tip) / H going backwards, v = (y - crown) / H going down. The ear/porion is **not** an anchor: it is not in a silhouette and the mesh ears are a separate asset.
- Filter: 136 of 150 side portraits pass (nose projects 0.045-0.26 H ahead of the forehead at v 0.30, depth window, all scalars defined). The 14 rejects are 3/4-ish or failed background fits (contact sheets checked by eye: the rejects are right, the kept ones are clean profiles).
- Radial profile r(theta): distance from the fixed pivot (u 0.50, v 0.50; about ear height, mid depth) to the outline, theta 0 = straight back, 90 = up, 180 = front, -20..165 deg in 5 deg steps, 25 deg boxcar smoothed. Sigma floor 0.015 H (3.4 mm) because the crown row is the anchor, so r(90 deg) is pinned and its raw spread is about zero.
- Scalars (FEATS): depth (nose tip to occiput, H), occ_v (row of the back-most point), crown_frac / crown_u (crown position), back_flat_hi / back_flat_lo (back contour at v 0.20 / 0.50 relative to the occiput line; negative = sloped away), dome_round (back-minus-front extent at v 0.06 over depth = crown cap width; low = narrow / gabled), forehead_slope, nose_prot, nape_inset, neck_depth, throat_back.
- Mesh: same measurement on `clay_side.png` (alpha silhouette, 1 px = 0.486 mm).
- PCA: first 6 components of r(theta) (PC1 = overall size in H units, 96 %); joint distance = sqrt(sum of squared component z). Prior p50 2.2, p95 3.7, p99 4.7 (in-sample, slightly optimistic).

## Calibration against literature (no CC0 head mesh available)
No MPFB2 / MakeHuman base mesh exists locally (searched C:/AI, the repo, the Blender extensions folder; only the add-ons antlandscape, easyweight etc. are installed) and I did not download one without permission, so there is **no measured 3D ground truth**. Checks against rounded literature values (Farkas-type anthropometry; adult means, not measured by me):
- Nose tip to occiput / crown-to-menton: the prior median is 0.95 H (man 0.93, woman 0.98). Literature: head length (glabella to opisthocranion) about 19.0 cm plus nasal projection from glabella about 2.3 cm = 21.3 cm over a menton-vertex height of about 22.5 cm = 0.95. Matches.
- Meshes: H = 221-249 mm (literature 225 +- 11 mm): char4 221, char2_zero 233, char3b 231, cloud_v1 249 (+2 sigma tall).
- Not checked (no data): cephalic index (needs a top/front width, the existing `plausibility.py` literature check covers it), forehead slope, jaw angle, crown curve, occiput position. **These prior values are what SDXL draws, uncalibrated.** SDXL smooths heads, its sigma is narrower than a population, the camera yaw is not exactly 90 deg, and the scalp stubble adds a little to the outline. Read |z| >= 2 as "watch", >= 3 as "outside what the generator draws".
- 3/4 view: **no prior.** The 40 "three-quarter" SDXL prompts all came out frontal (the contact sheet shows frontal faces), so they were not used. The scorer renders `clay_q34.png` for the eye only. A 3/4 prior needs another route (SDXL with a ControlNet / depth pose, or a posed MPFB2 mesh).
- Neck length is **not measurable** from this silhouette (the shoulders and shirt crop the image and hide the neck base). neck_depth, nape_inset and throat_back are measured and none of the four meshes is outside +-2 sigma on them.

## Results (2026-11-09, final tag of each run, read-only)
Overlays (orange = mesh, yellow = prior median, green = +-2 sigma band): `C:/AI/test_out/heroes/pipeline_runs/<run>/out/side_check/side_overlay.png`.

| run | joint dist (p95 3.7) | outline vs median (mean sigma: back / upper back / crown / forehead) | scalar flags |
|---|---|---|---|
| char2_zero | 2.3 (typical) | +1.1 / +0.2 / -0.6 / -1.4 | crown cap too narrow -3.3 |
| char3b | 6.2 | -0.9 / -1.3 / -0.8 / -2.0 | crown cap too narrow -5.6, upper back too flat / sloped -3.1, forehead slope +2.0 (watch), occ low +1.8 |
| char4 | 4.7 (p99) | +0.9 / +0.1 / -1.2 / -2.3 | crown cap too narrow -4.5, forehead slopes back +2.2 |
| cloud_v1 | 5.5 | -0.9 / -1.2 / +0.4 / -0.7 | none beyond 2 sigma; depth -1.0, crown_u -1.5, forehead slope -1.8 |

What is wrong from the side (looked at the four overlays):
- **char3b**: the skull is shorter front-to-back (0.91 H vs 0.95) and the back is cut off: above the ears the back line runs diagonally down and forward to the neck instead of bulging into a rounded occiput (back outline 11 mm inside the median, upper back 8 mm), and the crown is a narrow cap. Worst of the four, and the two flags match the user's remark.
- **cloud_v1**: crown and forehead are fine; the back of the skull is a near-vertical line about 13 mm inside the median with no occipital bulge (depth 0.89 H, -1.0 sigma), the head is tall (250 mm). Close until sideways, as the user said, but the metric only calls it a mild -1 to -1.2 sigma, not a failure.
- **char4**: depth and back are fine or slightly full (+10 mm), but the crown is narrower / more peaked than the prior (-4.5) and the forehead is flat and slopes back (32 deg vs 26).
- **char2_zero**: closest to the prior outline (joint 2.3). It is deep (1.02 H, back 15 mm behind the median, a little full and low) with a narrow crown cap (-3.3).
- All four except cloud_v1 show a front-of-skull (theta 130-145) outline 6-10 mm inside the median. That region is pinned to the photo by the fit and it is also where SDXL's stubbled forehead and hairline add the most, so treat it as the weakest reading.
The numbers are modest because the prior sigma is wide at the back (0.05 H at theta -20 deg). The overlays show the sign of the problem more clearly than the sigma does.

## Recommended fix: a sagittal radial field stage (not implemented in the pipeline)
New stage after the skull-width / dome fits and before snap and polish, working on the head mesh vertices only (eyes follow their lids as today):
1. Measure the mesh side outline exactly as `side_prior.measure` does (H, nose tip y, crown z, r(theta)). Use the mesh's own vertices (ray-cast the sagittal outline) rather than the clay render for speed.
2. Pivot P = (y_nose + 0.5 H, z_crown - 0.5 H) in the sagittal plane (the +y direction is backwards, +z up). For each vertex, theta = atan2(dz, dy), r = hypot(dy, dz) about P. Scale r by s(theta) = clip(1 + alpha (r_prior(theta) / r_mesh(theta) - 1), 0.85, 1.15), smoothed over theta (5-point boxcar); x (lateral) unchanged.
3. Weight w = smoothstep(u, 0.22, 0.45) x (1 - smoothstep(v, 0.55, 0.80)) x smoothstep(theta, -35, -20) x (1 - smoothstep(theta, 165, 180)): pins the face and forehead (u < 0.22), fades to zero in the neck and jaw (v > 0.8), full in the cranium behind the ears. Displacement = (s - 1) w r, along the radial direction in (y, z) only, which is smooth and cannot fold while s stays within 0.85-1.15.
4. alpha 0.7-0.8, applied before the ears are re-seated and before the texture/UV step, which doesn't see a change at this size (max vertex move 10-11 mm).
5. Gate: after the warp, re-run `side_profile_metric.py` and require every outline segment within +-1.5 sigma and joint distance < 3.7. Photo-waive as the skull plausibility does: if the photo's own side view is unavailable, keep the prior median as the target only for the back (theta -20..85), never for the face.

Prototype (scratch copy, not committed; script and before/after renders under the session scratchpad: `warp_char3b/before_after.png`, `warp_cloud_v1/before_after.png`), alpha 0.8, sagittal field as above:

| | back (occiput) | upper back | crown | depth | joint dist | notes |
|---|---|---|---|---|---|---|
| char3b before / after | -0.9 -> -0.3 sigma | -1.3 -> -0.3 | -0.8 -> -1.1 | 0.91 -> 0.94 H | 6.2 -> 4.5 | back_flat_hi flag (-3.1) gone; dome_round -5.6 -> -3.7 (still flagged); forehead -2.0 unchanged (pinned) |
| cloud_v1 before / after | -0.9 -> 0.0 | -1.2 -> -0.1 | +0.4 -> -0.3 | 0.89 -> 0.95 H | 5.5 -> 4.3 | max move 11 mm, no fold in the rendered side view |

The char3b after-render shows a rounded occiput and a clean outline from the ear to the neck (no kink, no change to face or ears). The remaining flags need their own fixes: (a) crown cap too narrow on char2_zero / char3b / char4 (dome_round): a second field in the coronal plane (x scale by a smooth function of height on the top 12 % of H, widening the cap 5-8 %), which the front-view prior (`crown_taper`) already points at; (b) the forehead region inside the median: fix in the fit (forehead height/bulge), not by a cranial warp. Predicted after both: all four heads within +-2 sigma on every outline segment and scalar except possibly forehead, which depends on how much of the SDXL forehead is stubble.

## Limits and honest notes
- The prior is SDXL-drawn and uncalibrated against measured heads (see above); it agrees with literature on depth/height only.
- The radial sigma is wide at the back and floors at 0.015 H elsewhere; a "-1.2 sigma" there is still a 7-13 mm shortfall.
- Silhouette anchors depend on a clean chin (menton); a beard or high collar in a photo can move H by a few percent (the 136 kept portraits were checked on contact sheets; beards were in about a third of the men).
- One mesh render each; no soak over many heads. Tags are the runs' own final tags (h6).

## Implemented (round 16): snap stage `stages/r6_16_skulldepth.py` (config `snap.skulldepth`)
- Runs after the polish, before the symmetrize (`when: late`, default). `when: early` (before the refit/polish) was measured: the refit then lands in a different optimum (char4 ratio worst 3.4 -> 5.5, refit rejected twice), so late it is.
- Back of skull: a **y-only** field (depth only, z and x untouched) toward the prior back-contour median (one-sided: fuller than median + 1 sigma is only pulled back to that), alpha 0.8, ears and face kept by `smoothstep(t, .55, .95)`, crown row and neck faded. The first version (sagittal radial field, `mode: radial`) also moved z and cost 2-3 % skull IoU on char2_zero/char4/char3b, so it is not the default.
- Crown cap: y widening (up to 8 %, to dome_round >= -1.5 sigma) and x widening only when the front crown_taper is below -1.5 sigma (char3b only, 2 %); unconditional x widening pushed char2_zero / char4 crown_taper to +2.9 / +2.7.
- The side joint distance is **report-only** (`out/score.json: side_profile`): the pinned front of the skull (theta 120-165) sits 1.5-2 sigma inside the SDXL median on all four heads, so joint 3.6-4.6 stays above the prior p95 (3.7) from back-of-skull work alone.
