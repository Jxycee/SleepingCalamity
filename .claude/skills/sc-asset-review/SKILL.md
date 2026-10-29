---
name: sc-asset-review
description: Score a SleepingCalamity model (or a detail pass) for quality and realism against the Riverlight concepts, with the audit and preview tools and a fixed rubric; outputs a pass/fail, a score and the top fixes. Use after building or changing any model, before export/commit, when reviewing another session's asset, or when a board card asks whether an asset "matches the reference".
---

# Reviewing a SleepingCalamity model

A review is evidence first: numbers from the audit, pixels from the preview, a crop from the concept. Then a score against the rubric below. Then at most three fixes, in priority order. Review the model, not the code.

## 1. Gather the evidence (about 1 minute)
```
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py   -- <scratch>/audit.json --class <prop|outbuilding|building|tree|run|creature|cover|rock|card|hero> <fbx...>
blender -b --factory-startup --python Art/Blender/tools/sc_asset_preview.py -- <scratch>/sheet.png --size 512 [--ref human,door,wall] <fbx...>   # x.fbx+x_Tri.fbx = one model
```
- **Look with shadows:** add `--shadows 1` to a second preview (3x the render time) for the eye checks. Cast and contact shadows show floating parts, missing supports and weak grounding at once: the shipped restored bench's backrest turned out to float with no posts. Score only the shadow-free sheet: the `sc_asset_score.py` baselines have no shadows.
- Read the audit's FAIL and WARN lines, and `albedo_value` (p10 / mean / p90 of the drawn albedo as sRGB value). Shipped Millpond assets sit at a mean of 0.31-0.48. Under 0.22 the audit WARNs "dark". Open `sheet.png` (three close views + the play-distance panel, rendered like the game's soft light and painterly drift).
- Crop the asset from its concept frame (`Art/Design/Concepts/Millpond/*_concept_1.jpg`) and look at it next to the sheet. With PIL: open, crop, `thumbnail`, paste side by side, save to the scratchpad, Read it.
- **Scale:** `--ref human,door,wall` puts a 1.8 m figure, a 3 x 2.6 m wall and a 0.95 x 1.95 m door (dark) next to the model. The door sits in the wall, so `door` draws the wall too; use `human` alone for no wall. Use it for anything that mounts on a building (downpipes, signs, lamps) and whenever proportion is in doubt. Leave it off for the final sheet: the wall hides the back view.
- **Colour in shade views:** the preview's sky fill turns neutral greys blue in the back three-quarter view (the blind heron's colour match fell from 95 front to 51 back). Match colour on the front three-quarter view only.
- **Which light for which question** (2026-10-25): judge a material's **hue** (is this timber grey, brown or blue?) on a second sheet with `--light neutral`, which keeps the values but greys the light's colours. In the default game light, the sky fill and the cool shade make neutral greys read blue-grey. Two false "blue-grey timber" reads came from it (the beacon, the lock gate), while `B.GREY_TIMBER` renders `#6a675d`, as `sc_tint.py color` confirms. **Score and concept-match in the default light**: the `sc_asset_score.py` baselines and the concept captures use it. Check a tint's albedo with `sc_tint.py color <slot> <tint>` before you call it off. A generator's own `--preview` (Cycles, sky texture) is for shape, never colour.
- **Which way the views look:** the three-quarter view looks at the model's front (+z) from front-left. In the three-quarter and side panels, **+x is on screen left**, as for anyone standing in front of the model. The side view looks from -x, and the back three-quarter looks from behind (-z) on the +x side. Hinge a door or put a sign on the side you want seen with this in mind (duo r10: a door hung on the wrong jamb hid its own doorway).
- **Props too small to crop from a painting** (most props are under 100 px in the concepts): score against target colours instead, `sc_concept_match.py --targets '#6e6a5e,#4a463e,#545a39' <sheet.png>` (nearest model colour per target, share within 30, 0-100 match; a target counts only when its colour covers at least 5% of the model, so one stray pixel scores nothing). Take targets from the concept README's measured palette or pick them from the painting. Use the 3-4 **big masses** as targets, not the accents: a 1% teal lid correctly scores near 0 and drags the mean.
- Measure the match: `python3 Art/Blender/tools/sc_concept_match.py <concept.jpg> <x0%,y0%,x1%,y1%> <sheet.png> --out <scratch>/match.png` gives the value shift, contrast and saturation ratios, the warm/cool shift and a 0-100 match score against the concept crop. Use it to hillclimb rubric lines 3 and 4. Baseline (2026-10-25): the shipped restored cottage scores 64.5: value -8, contrast 0.62x, saturation 0.57x the concept's. The gap is contrast and colour, not brightness.
- **Test it in a group, not alone** (the Sea of Thieves rule): put it on one sheet with 3-4 neighbours from its setting (`a.fbx b.fbx c.fbx` on one preview). An asset that reads alone can merge with its neighbours or out-shout them.
- Compare with something already accepted at the same scale (a shipped neighbour: `Assets/_Project/Content/Models/Millpond/...`): render it on the same sheet. Kits must match each other.

**Region census:** `python3 Art/Blender/tools/sc_asset_census.py [--region Millpond]` audits every exported model and ranks the gate FAILs, the darkest, the flattest vertex colour and the most smeared. Start convergence sessions with it and fix from the top. Census 1 (2026-10-25): 7 FAIL of 232, all budget (nets, fish ladder, fallen tree); the bare winter trees have flat vertex colour.

Optional extra checklists: `.claude/skills/sc-asset-craft/library/qa-review/` (ship/no-ship template, screenshot angles) and `library/lookdev/` (look-dev loop). This rubric decides.

## 2. Gates (any failure = not done, whatever the score)
- Audit: no FAIL (budget, material count, uv1 encoding, Col present).
- Hygiene: no WARN for degenerate faces or non-manifold edges on a new asset.
- Origin and size: real metres. The base sits at y 0 (or as the header states). The front faces +z.
- Seasons: living surfaces (grass, leaves, reeds, moss) on seasonal palette slots, not fixed colours.
- Determinism: export twice to the scratchpad (`--out`) and diff the two audit JSONs; they are identical.

## 2b. Objective grades (use them; self-scores drift)
`python3 Art/Blender/tools/sc_asset_score.py <sheet.png> --class <prop|building|outbuilding|tree|run|creature|cover> --audit <audit.json>` grades rubric lines **1 silhouette, 3 value, 5 painterly and 8 efficiency** by percentile against the shipped Millpond library of its class (114 props, 43 buildings, 13 trees/bushes; `sc_asset_score_baseline*.json`; sheets at `--size 512`, one model, no `--ref`). A 3 = the typical shipped prop, a 5 = its top 15%. Where an objective grade exists, **use it for that line** and judge only lines 2, 4, 6, 7 by eye. Grades are hard steps at the 40th/60th/85th percentiles, so one outline break can flip a grade: **report the percentile beside the grade** ("silhouette 3 (59.8)").
- Silhouette is the better of **openness** (outline complexity at 48 px) and **breaks** (dents against the convex hull of at least 2 x 2 px), so a compact solid can score by its breaks. Efficiency grades by the ratio to the class's per-model median (1x 5, 1.5x 4, 2x 3, 3x 2), so story-beat detail costs a grade only past 1.5x. The scorer also prints `grade_soft`, interpolated to one decimal: 2 002 tris is grade 3 but soft 4.0. Don't cut a story beat to cross a step; quote the soft grade instead.
- Baseline medians (props): complexity 2.8, breaks 5, value spread 54, painterly (in-surface high-pass) 11.0. Painterly also rises with interior clutter (open lids, racks), not only with painted surfaces. It measures brush-scale variation (band-pass on a downsampled panel) and discounts per-face confetti (fine/brush contrast ratio above 0.35), so a mosaic no longer scores as painting. The scorer prints `confetti` and the `discount` it applied on every run: when painterly is low, check that before adding paint.
- **The scorer can't see a story.** In the 2026-10-25 A/B test a tidy chest with clean dark-interior value masses scored value 4 / painterly 4, a messier one with flaking paint, a replaced plank and real tools scored 1 / 2, and a blind grader preferred the messy one clearly. Grade lines 6 and 7 by eye against **the brief's own words** every time, and never stop on good percentiles alone.
- Reality check (duo, 2026-10-25): props self-scored at 32/40 graded silhouette 2, painterly 2-3 against the library. The self-scored rubric runs about 4-6 points high.
- Limits: the silhouette metric rewards any outline complexity (nets, thin legs, and star-shaped spikes score high: the blind iris's winter starburst got 5/5/5), so read it with the 48 px panel and mark it down by eye for a starburst. The painterly metric rewards blob-sized value patches, so it under-rates streak or strand surfaces (weeping-willow tresses p31 against p62 for oval clumps, though the eye prefers tresses): for those, it's advisory. Building medians: silhouette 3.0, value 47, painterly 9.8. Tree medians: 2.6, 63, 14.7. The duo's net shed (self-scored 33) graded 2/2/2 against the buildings.
- **`--class outbuilding`** has its own baseline (`sc_asset_score_baseline_outbuilding.json`, duo r11). The shipped library has only one standalone walk-in structure under 3 m, so the look metrics use the 114 props plus the market stall and the duo kiln, net shed and privy. Efficiency uses the props' per-model tris scaled by sqrt(surface area): a median of about 4 600, which the real outbuildings (2.7-5.3k) bracket. Against the building baseline a small hut got free 5s; the r10 privy fell from 3/4/5/5 to 3/3/3/5 when re-graded.
- Rebuild the baseline after a big convergence pass: render every shipped prop with `sc_asset_preview.py --size 512` into one folder, then run `sc_asset_score.py --baseline <dir> Art/Blender/tools/sc_asset_score_baseline.json <props audit.json>`.

## 3. Rubric (score each 1-5; pass = every line >= 3 and total >= 30/40)
| # | Criterion | 5 looks like | 3 looks like | 1 looks like |
|---|---|---|---|---|
| 1 | **Silhouette at play distance** (48 px panel) | identifiable instantly, the outline has 2-4 characteristic breaks | identifiable as its category (a crate, a barrel) but not as *this* thing (a chest) | a blob or a box; reads as something else |
| 2 | **Proportion and scale realism** | real dimensions, slightly chunky; right next to `--ref human,door` | plausible alone; one part off (oversized sleepers, a thin rail) | toy-like, stretched, or wrong next to its neighbours |
| 3 | **Value structure** | 3 clear masses (light/mid/dark) matching the concept; holds in the shade-side view | the masses exist but two of them are within about 10% value of each other | flat mid-grey all over, or noisy speckle |
| 4 | **Colour vs concept** | `--targets` match >= 80 or crop match >= 75; hues in the README palette | right families, one material off (saturated orange oak, a blue cast) | candy colours, or every part the same slot colour |
| 5 | **Painterly in-surface variation** | patch-scale warm/cool and value drift visible in the close views | per-part or per-board variation, flat inside each part | one flat tone per part, or per-face confetti |
| 6 | **Form and softness** | rounded edges the light rolls over; no visible facets on curved parts; thick, hand-made parts | rounded where it shows most (lids, caps) but flat boxes elsewhere | razor edges, faceted cylinders, paper-thin parts |
| 7 | **Wear storytelling** | wear where weather goes (bottoms, edges, joints, waterline); fits rundown vs restored, readable at 10 m | the right wear in the right places but only visible up close | uniform grime, or showroom-new in a rundown scene |
| 8 | **Efficiency** (per **model**: base + `_Tri` summed; shipped per-model prop median **1 330**; walk-up outbuildings under about 3 m, such as a kiln or a privy, are class *outbuilding*: 9k tris, density warn 400) | under the class median, with the detail on the silhouette | between the median and 2x it, or a density WARN on small repeated detail | at the ceiling with triangles in hidden or flat areas |

Write the review as:
```
<asset>: PASS|FAIL  score NN/40  (gates: ok | <which>)
1 silhouette 4 - <one line of evidence>
...
Top fixes: 1) ... 2) ... 3) ...   (each: what, where, expected effect on which criterion)
```

## 4. Common defects and their fix
Importing our FBX anywhere (Blender scripts, other tools): pass `colors_type='LINEAR'`. The kit exports Col as linear, and Blender's default sRGB import crushes the darks and shifts hues blue (found by the duo builder, 2026-10-25).

| Seen in the preview | Cause | Fix |
|---|---|---|
| whole model dark, shade side near black | tint x ao too low (Col mean under 0.15), or AO floor too low | raise the part's ao floor (wood 0.45, rock 0.62) or the tint value |
| neon or chalky patches | tint clipping (audit `col_clipped`), slot too saturated | move to a calmer slot, lower the tint under 1.9 |
| flat, plastic look | no patch variation, Col std under 0.02 | per-board/course tint jitter (±6-10% value, warm top / cool bottom) |
| faceted cylinders, banding across a face | too few lathe sides, or smooth normals over a hard corner | +4 sides on the silhouette parts; split normals at real corners (box parts) |
| "smeared" gradients across flat planks (smear share > 0.6) | averaged normals over thin parts | build planks as `box` (split normals) |
| reads as a blob at 48 px | no outline breaks, the value masses merge | add 2-3 silhouette features; darken the roof or base mass |
| kit pieces disagree | different tints or scale per generator | reuse the neighbour generator's tint constants (`STONE`, `SLATE`, `GREY_TIMBER` ...) |

## 5. Realism checks against the concepts
- **Scale:** render with a 1.8 m reference box (or a shipped door/cottage) on the same sheet when unsure.
- **Light:** the concepts' shade side is cool blue-grey and still readable. If ours goes black, the albedo is too dark, not the light.
- **Water contact:** anything at the waterline gets a darker wet band 0.1-0.3 m and weed/moss on the up-stream side.
- **Distance:** backdrop assets (the far bank, hills) need less detail and lower contrast. Judge them only at play distance.
- The preview is an approximation (no shadows, no texture-array grain). Before calling a set of assets "matching the concept", ask a local session for one Unity capture from the concept cameras (board card, kind review). Batch it.

## Maintaining these skills
After editing any sc-* skill, or renaming a generator, kit function or tool, run `python3 Art/Blender/tools/sc_skill_refs.py`: it checks that every path and kit function the skills mention still exists.

**Evals:** `.claude/skills/sc-asset-craft/evals/evals.json` holds behavioural evals for these skills: 3 held-out briefs, run with and without the skills and graded blind. Re-run them after a big skill change, and revert an edit that loses to the previous version.
