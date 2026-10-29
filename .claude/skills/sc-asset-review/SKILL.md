---
name: sc-asset-review
description: Review a Sleeping Calamity model, kit or batch against the style bible (docs/PHASE1.md section 3) and its reference row - audit numbers, PBR review renders, measured colour and value, a fixed rubric, the batch contact-sheet review and the two-reviewer blind A/B for heroes - and output the scores and the top three fixes. Use after building or changing any model or batch, before export or commit, when reviewing another session's asset, or when a brief asks whether an asset or corner "matches the reference".
---

# Reviewing a Sleeping Calamity model

A review is evidence first: numbers from the audit, pixels from the review render, the reference row beside it. Then scores against the rubric. Then at most three fixes, in priority order. Review the model, not the code.

**Sign-off:** the scores rank items and compare rounds; they are not a pass gate. The art track's only sign-off is the user's approval on captures (PHASE1 3.7). Before the corners are shown, the captures must fall inside the PHASE1 3.1 bands and a reviewer must have scored them with this rubric.

## 1. Gather the evidence
Tool status is in sc-asset-craft 0b. In short: `sc_asset_audit.py` for numbers; the fg-aaa-prop render scripts (port in M8) for the look; `sc_ref_palette.py` for colour and value. `sc_asset_preview.py`, `sc_asset_score.py`, `sc_asset_census.py` and `sc_concept_match.py` are FishGame legacy and don't apply.
```
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <scratch>/audit.json --class prop <fbx...>
python -I Art/Blender/tools/sc_ref_palette.py <render.png> --k 7 --sheet <scratch>/palette.png
```
- **Audit:** read tris, density, bounds, origin offset and the hygiene lines. Judge tris against PHASE1 3.3, not the audit's own Millpond `BUDGET`, and ignore its uv1/`Col`/material-count lines.
- **Review renders** (`compose.py` / `review.py`, then `sheet.py`): a neutral **studio** for albedo and hue, plus the asset's **mood** (Hollowmere midday sun, or warm Cellars lamplight). Calibrate the studio so 50% grey reads mid-grey; hot lights hid a too-pale stone albedo on the stand. Use small disk area lights (square ones put fake quads on glass and metal), an environment and a probe so metals have something to reflect, and f/11 for close-ups (shallow focus made sharp work read "blobby"). Render 2x and downscale to remove the soft-texel read.
- **Sheet:** the reference row on top (photos and paintings from `Reference/<area>/<subject>/`, later the approved corner captures), the renders below: three-quarter, side, back, a close crop of the main material, and a thumbnail at play distance. Save as JPEG, 2000 px or less (big PNGs posterise into contour bands when viewed, which looks like a lighting fault).
- **Scale:** a 1.75 m figure and a 2.2 m door beside anything whose proportion is in doubt (PHASE1 3.4).
- **Colour and value:** run `sc_ref_palette.py --k 7` on the mood render and compare with PHASE1 3.1 (key hues, value p10/p50/p90, warmth). For a single prop this is advisory (one prop isn't a scene); for corner captures it is the 3.1 check. Grade relations (timber darker than plaster, moss darker and greener than stone), not exact hexes.
- **Test it in a group** (the Sea of Thieves rule): render it with 3-4 neighbours from its kit or corner. An asset that reads alone can merge with its neighbours or out-shout them; kit pieces must match each other.
- **Reference matcher** (`ref_match.py --inv`, port): compares a material crop with 2-3 photo crops (detail per band, edge crispness, value and saturation variation). Trust only a set that calibrates against itself (`--calib`), and use the verdict's direction ("fine detail too strong x1.6"), not the gap number.

## 2. Gates (any failure = not done)
- Audit hygiene: no degenerate faces, loose verts or non-manifold edges on a new asset.
- Origin and size: real metres, base at y 0 (or as the header states), front +z.
- Budget: within PHASE1 3.3 for its tier, or the header says why.
- Albedo carries no baked light; AO ships as its own map.
- Determinism: export twice to the scratchpad and diff the two audit JSONs; they are identical.

## 3. Which review (PHASE1 section 9)
- **Batch review (T1-T3 families):** one contact sheet per batch with the reference row. The controller scores it and names the worst three; a fix agent fixes those three; the sheet is re-scored once.
- **Blind A/B (T4 heroes and the first item of a new family, e.g. the first B4 facade):**
  - Run `imgdiff.py a.png b.png` first. Under about 0.5 (0-255) the pair is identical at review size, and reviewers will invent a preference.
  - Two fresh reviewers, one sheet: real references in the top row, the candidates shuffled and unlabelled below. The reviewer opens only the sheet: no repo files, no prior scores.
  - A result counts when the stated reason matches the change. 2-point margins are ties; a third reviewer breaks a split.
  - Self-judged and one-reviewer results are not gated results (the cottage's A/B was self-judged: treat it as unconfirmed).
  - Compare scores only within one rubric version and one reference row.
- **Lessons from the stand's A/B rounds:**
  - A defect named in a review is not a preference: removing it can still lose. Improve the features reviewers credit instead of replacing them. Visible beats correct.
  - Reviewers punish whatever is labelled "candidate": KCD2's own art scored 21/40 blind. A same-sheet A/B of two candidates removes most of that noise.
  - **Parity test:** to ask "is it at the target's level", show our render beside a crop of a similar subject from the target game (private frames, scratchpad only), shuffled, and ask which is the shipped game. Parity is picks near 50%.
  - Snapshot the baked asset with `snap.py` (the .blend and its maps) before rebaking anything you will A/B.

## 4. Rubric (score each line 1-5)
| # | Line | 5 looks like | 3 looks like | 1 looks like |
|---|---|---|---|---|
| 1 | **Silhouette and read** (thumbnail, play distance) | identifiable instantly, 2-4 characteristic outline breaks, reads at 20 m | identifiable as its category, not as this thing | a blob or a box; reads as something else |
| 2 | **Storybook proportion** (PHASE1 3.4 / 3.6) | real dimensions pushed chunky on purpose (timber ~1.4x, props ~10% over); right beside the figure and door | plausible, one part off (a thin rail, an undersized door) | toy-like or chibi, stretched, or merely real-scale |
| 3 | **Form and edges** | chamfers on every play-distance edge, square-arris spalls, no straight old work, turned parts smooth | rounded where it shows most, machined elsewhere | razor edges, melted rims, faceted cylinders, dome-bite chips |
| 4 | **Surface: micro-detail and roughness** | detail at several scales; roughness varies with wear and material | one convincing scale; roughness mostly uniform | procedural noise at one scale ("sandpaper", "foam"), waxy single roughness |
| 5 | **Colour and value** (PHASE1 3.1) | 3-4 clear masses, key hues with the clean saturated shift, no pure black | right families, one material off (grey photo-dull plaster, a blue cast) | candy or neon, or photo-desaturated mush |
| 6 | **Wear story** | every wear word in the brief delivered and visible from three-quarter; tidy, placed, where weather and hands go | the right wear, only visible up close | uniform grime, or showroom-new where the brief says worn |
| 7 | **Fits the set** | matches its kit and neighbours in scale, texel density and colour | one mismatch (texel density, a material's hue) | looks imported from another game |
| 8 | **Glass, liquid, emissive, metal** (where present; else score efficiency against PHASE1 3.3) | glass and liquid read through fake refraction, glow sits in the right place, metals reflect | readable but flat | murky shells, neon liquids, black metal |

Write the review as:
```
<asset>: score NN/40  (gates: ok | <which>)   reference row: <REFS.md or captures>
1 silhouette 4 - <one line of evidence>
...
Top fixes: 1) ... 2) ... 3) ...   (each: what, where, which line it should move)
```

## 5. Common defects and their fix
| Seen in the render | Cause | Fix |
|---|---|---|
| "sandpaper" or "foam": one scale of noise | scan tiled too fine, fine normal too strong, low value contrast | coarser scan tile, weaker fine normal, more contrast (the mortar: tile 0.12 -> 0.25, normal strength 1.8 -> 0.8, contrast 0.45 -> 0.9) |
| micro-detail invisible | bump too weak for review scale | detail normals 0.5-0.8 plus a fine noise layer |
| every rim melted | smoothing the whole mesh | fillet only the concave seams where parts meet |
| chip "islands" all over smooth faces, moon craters | ungated noise chips, dome bites | gate by curvature (convex rims), cut planar flakes with a square arris |
| stripes along a cylinder | planar facets on a turned part | clean source low, Catmull-resampled profile |
| metal renders black | nothing to reflect | environment plus a probe in the review scene; reflection probe in Unity |
| glaze reads as modern plastic | full glossy glaze | mostly bare clay, glaze over the shoulder ending in runs |
| streaks look airbrushed or like teeth | 1D-Voronoi runs on metal | noise stretched along gravity |
| liquid reads neon | bright liquid colour | dark saturated colour (linear 0.1-0.15 on the main channel) |
| pillow-puffy stones with black gaps | big bevel, no mortar | smaller bevel plus a recessed lime-mortar bed |
| photo-dull colour | scan albedo used as is | PHASE1 3.1 chroma shift (+15-25% on plaster tints, roofs, foliage, painted wood, cloth) |
| a material change has no effect | a light is doing it (the brazier's light inside its wall) | switch lights off one at a time first |
| contour bands in the sheet | big PNG posterised by the viewer | JPEG, 2000 px or less |

## 6. The engine is the final judge
Blender renders are proxies until a Unity capture agrees. Before calling a set of assets "matching the reference", ask the PC session for one round of captures (the fixed capture cameras of PHASE1 3.7 once their builder exists) and batch it. Report results to the controller with the numbers.

## Maintaining these skills
After editing any sc-* skill or renaming a tool, check that every path the skills mention exists:
`grep -rhoE '(Art|Assets|docs|Tools|Reference)/[^ )\`,]+' .claude/skills | sort -u | while read p; do [ -e "$p" ] || echo MISSING $p; done` (placeholders in `<...>` will show; check the rest).
**Evals:** `.claude/skills/sc-asset-craft/evals/evals.json` holds 3 held-out briefs (B7 barrel, B4 facade panel, B11 lantern). Re-run them after a big skill change, and revert an edit that loses to the previous version.
