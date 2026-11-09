# vision: judge a fitted head from one image and a few numbers

Read-only tools for the face pipeline (nothing here edits a stage, a config or a run's existing files; outputs go to `<run>/out/look_sheet.png` and `<run>/out/look/`).
Python: the `facekeys` interpreter (`C:/AI/envs/facekeys/Scripts/python.exe`: numpy, cv2, scipy, PIL, mediapipe) runs everything; Blender 5.2 runs in the background for the clay renders. No Unity, no GPU except `gen_prior_faces.py`.

| file | what |
|---|---|
| `look_sheet.py <run_dir>` | the one image per character: `out/look_sheet.png` (about 2360 x 1360) |
| `plausibility.py <run_dir>` | z-scores of the mesh (and of the photo) against a human face prior, in words |
| `human_prior.py all` | builds `human_prior.json` from the SDXL portraits (`gen_prior_faces.py` makes them) |
| `slider_atlas.py` | what each of the 38 fit sliders does: contact sheets + sensitivity table |
| `proportions.py`, `vcommon.py`, `clay_views.py`, `atlas_render.py` | shared measures, run access, Blender render scripts |

```
facekeys=C:/AI/envs/facekeys/Scripts/python.exe
$facekeys look_sheet.py   C:/AI/test_out/heroes/pipeline_runs/cloud_v1      # also runs the plausibility analysis for the header
$facekeys plausibility.py C:/AI/test_out/heroes/pipeline_runs/cloud_v1      # text report + out/look/plausibility.json
$facekeys slider_atlas.py                                                   # about 15 s, writes C:/AI/test_out/heroes/vision_prior/atlas/
```
Options: `--tag h6` (another wrap tag; default = `final_tag` of `out/score.json`), `--force-clay` (re-render the clay), `--arrow-gain 3` (look_sheet). The mesh is `fit/_rr_<tag>.npz` (head + eyeballs, what the scorer renders), the photo is `target/crop_src.png`, landmarks `fit/lm_<tag>.json` / `target/landmarks.json`.

## look_sheet.png: what is where
Header: scorer numbers (ratio MAE, worst, IoU, head aspect error), the guide fractions, and the plausibility flags (sigma vs the prior, photo sigma in brackets).
- Row 1, CLAY (matte, untextured, same ortho camera, scale (1 px = 0.49 mm) and light for every character; ruler = 5 cm): front with guides, 3/4, side with guides, top (nose up). Guides are the crown, eye line, nose base, mouth and chin; the label is the fraction of crown-to-chin height (0 = crown, 1 = chin).
- Row 2, left to right: the PHOTO with its own fractions (cyan); the 50 % BLEND (cyan photo guides, orange render guides: a gap between a cyan and an orange line is a vertical proportion error); the DIFFERENCE map (red = render outside the photo head mask, blue = photo head the render does not cover; rows above the chin only, because below it the neck and collar are not fitted; white outline = photo mask); LANDMARK MISS (orange dot = where the render's landmark is after registering on the 4 eye corners, red arrow x3 to the photo landmark; the eye corners are zero by construction).
- Footer: the largest landmark misses in photo pixels and as % of the inter-ocular distance.

## Rubric for a "looker" reviewer (answer each in a sentence, from the sheet alone)
1. **Head shape.** Clay front and side: is the skull a smooth egg, or elongated (football), flattened or pointed on top? Compare crown-to-eye with eye-to-chin (the guide labels: a human eye line is near 0.50 of crown-to-chin; above 0.55 the skull above the eyes is too tall). In the side view, is the skull depth plausible for its height, is the back of the head full or cut off?
2. **Does the head match the photo?** DIFFERENCE map: is the red/blue band a thin, even rim (good), a sideways shift (red one side, blue the other = the photo's yaw, not an error), or a lump (crown, jaw, cheek)? BLEND: do the cyan and orange guides coincide at crown, eye, nose, mouth, chin?
3. **Ears.** Front and 3/4: are both ears at the same height and size, level with the brow-to-nose-base band? (Ears are Tobin's, not fitted: expect ghost ears in the BLEND, and note it, do not count it as a fit error.)
4. **Jaw and chin.** Front/3/4: does the jaw line run smoothly from the ear to the chin, without a dent, a bump or a pinched chin? Side: is the chin projecting naturally, does the neck join smoothly?
5. **Features.** Are the eyes level and the same size, the nose straight, the mouth centred below the nose? LANDMARK MISS: are the arrows small and random (detector noise) or large and one-directional (a real error)? Quote the largest miss and which landmark.
6. **Symmetry.** Top view: is the skull symmetric about the nose line? Front: left and right cheeks, brows and mouth corners at matching heights? (A visible centre seam in the clay is only the mesh seam.)
7. **Plausibility flags.** For each flag in the header: is it the mesh or the photo? (If the photo's sigma is close to the mesh's, the character is built like its photo: stylised art or an unusual head, not a fit error.)
Verdict line: pass / fix, the one thing to fix first, and whether it is a shape problem (look sheet) or a number problem (scorer).

## plausibility.py: how to read the numbers
`z = (value - median) / sigma` against `human_prior.json`. The prior is **what SDXL draws**: about 200 frontal, bald, plain-backdrop adult portraits (RealVisXL V5, varied sex, age 20s to 70s, 12 ethnicity/skin-tone prompts, stubble on some men), measured with the same MediaPipe detector and the same code (`proportions.measure`) as the mesh render and the photo. Median and `1.4826 x MAD` are the robust centre and spread; mean/std, 5/95 %, per-sex medians and a shrunk-covariance Mahalanobis distance (10 features) are in the JSON. Honest limits:
- SDXL faces are smooth, symmetric and idealised, so the sigma is **narrower than a real population**. `|z| >= 2` is "watch", `>= 3` is "FLAG": read it as "outside what the generator draws", not as a medical statement. Use it to find which proportion is odd, not to reject a likeness.
- The detector pulls every face towards a mean face (gain 0.05-0.7 per landmark, README round 12), for the prior photos, the photo and the render alike, so the landmark proportions are compressed; the silhouette proportions (head aspect, eye line in the head, skull over face, dome over cheek) are not detector-limited and are the ones that catch a football head.
- Silhouette features need a clean bald crown; the prior keeps the portraits where it measured (n in the JSON). Other photos use the pipeline's own `mask_vis` head mask.
- **Side proportions are UNCALIBRATED**: the prior has frontal data only. `plausibility.py` measures the clay side silhouette (width/depth of the dome between 10 and 35 % of the head height, depth over height, head height in mm) and compares it with rounded literature values (cephalic index 0.79 +- 0.045, dome depth / head height 0.83 +- 0.06, head height 225 +- 11 mm). Treat these as a sanity flag only.
- The report prints mesh z, photo z and the gap. A mesh that is odd because the photo is odd (Cloud's photo is a stylised render) shows the same sigma on both.

## slider_atlas.py
Reads the slider list from `stages/r6_06_sliderloop.py` (PAIRS, SINGLES, SCN), so it follows the fit loop: 38 columns. For each: clay front and side at `-1 | 0 | +1` on the neutral template, the neutral silhouette in cyan on the +-1 tiles, `reach` = largest ratio change in percent. `sensitivity.md/json` (also copied here as `slider_sensitivity.*`): per slider and sign, the % change of the 13 photo ratios plus `head_aspect`, `skull_over_face`, head height and dome width in mm, and a "what moves what" list. The ratios are **geometric** (landmark = weighted template vertices at the template ray hits, as in `r6_02_warp.lm_model`); the detector follows a moved landmark only partly, so use the table to choose a slider, not to predict the detector reading. Four lid landmarks (159, 386, 145, 374) have no ray hit and are approximated by the top/bottom eyelid-ring vertex at the eye centre. The 16 global warp parameters are not sliders and are not in the atlas.

Useful reads from it: `head_aspect` is moved most by Head_Thick/Thin (width) and Head_Height; `skull_over_face` by Head_Vol and Head_Height (and SC_LowerFace_Height from the other side); the three SC_Eye_* sliders move EVERY ratio by 3-7 % because the inter-ocular distance normalises them.

## Data
`C:/AI/test_out/heroes/vision_prior/`: `faces/` (portraits, `meta.json` = prompts and attributes, `measures.json`), `atlas/` (sheets, meshes, sensitivity), `gen.log`. Regenerate the portraits with `gen_prior_faces.py --n 200` (about 30 min, ComfyUI 8190 must be up, GPU idle), then `human_prior.py all`.

## First results (2026-11-09, final tag h6 of each run; prior = 198 pose-filtered portraits, 189 with a clean silhouette)
| run | mesh flags (sigma; photo sigma in brackets) | joint distance mesh / photo (typical 3.3) |
|---|---|---|
| char4 | none beyond +-2 | 2.9 / 2.4 |
| char2_zero | crown too flat +2.1 (+1.7), skull wide for cheeks +2.1 (+2.0), skull tall above brows +2.0 (+1.7) | 3.9 / 3.5 |
| char3b | head too long +3.7, crown too pointed -3.3 (photo -2.9), lips full +2.7 | 6.7 / 6.8 |
| cloud_v1 | skull above brows too tall +5.8 (photo +6.3), skull wide for cheeks +3.6, eyes low in head +3.4 (+3.6), nose narrow -3.0 | 6.9 / 7.8 |
Reading: cloud_v1's photo is itself a tall-skull character (its head aspect is within the prior, the skull-over-face and eye line in the head are not), and the mesh copies it (gap about 0 sigma); the clay and the difference map show a smooth, tall dome, not a pointed one. char3b's clay front has a visibly gabled crown that the photo (a rounded dome) does not; the `crown_taper` feature flags it. `head_aspect` alone would not separate the two cases.
