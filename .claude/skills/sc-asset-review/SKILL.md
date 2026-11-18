---
name: sc-asset-review
description: Review a Sleeping Calamity model, kit or batch against the style bible (docs/PHASE1.md section 3) and its reference row - audit numbers, PBR review renders, measured colour and value, a fixed rubric, the batch contact-sheet review and the two-reviewer blind A/B for heroes - and output the scores and the top three fixes. Use after building or changing any model or batch, before export or commit, when reviewing another session's asset, or when a brief asks whether an asset or corner "matches the reference".
---

# Reviewing a Sleeping Calamity model

A review is evidence first: numbers from the audit, pixels from the review render, the reference row beside it. Then scores against the rubric. Then at most three fixes, in priority order. Review the model, not the code.

**Sign-off:** only the user's approval on captures signs anything off (PHASE1 3.7). The scores rank items and compare rounds; they are not a pass gate. Before the corners are shown, the captures should fall inside the PHASE1 3.1 bands and a reviewer should have scored them with this rubric (3.7). The checks in section 2 are budget and hygiene only.

## 1. Gather the evidence
Tool status is in sc-asset-craft 0b. In short: `sc_asset_audit.py` for numbers; the ported render scripts (`sc_compose.py`, `sc_review.py`, `sc_sheet.py`; see sc-aaa-prop) for the look; `sc_ref_palette.py` for colour and value. `sc_asset_preview.py`, `sc_asset_score.py`, `sc_asset_census.py` and `sc_concept_match.py` are FishGame legacy and don't apply.
```
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <scratch>/audit.json --class kit <fbx...>
python -I Art/Blender/tools/sc_ref_palette.py <render.png> --k 7 --sheet <scratch>/palette.png
```
- **Audit:** `sc_asset_audit.py` now uses the PHASE1 3.3 budgets (pass `--class` and `--tex-res`). Read its triangle and material-slot FAILs, the texel-density and hygiene WARNs, bounds and origin offset; report them as they are. A second UV map or a mask-blend colour attribute on a T2 piece is fine.
- **Review renders** (`sc_compose.py` / `sc_review.py`, then `sc_sheet.py`): a neutral **studio** for albedo and hue, plus the asset's **mood** (Hollowmere midday sun, or warm Cellars lamplight). Calibrate the studio so 50% grey reads mid-grey; hot lights hid a too-pale stone albedo on the stand. Use small disk area lights (square ones put fake quads on glass and metal), an environment and a probe so metals have something to reflect, and f/11 for close-ups (shallow focus made sharp work read "blobby"). Render 2x and downscale to remove the soft-texel read.
- **Sheet:** the reference row on top (photos and paintings from `Reference/<area>/<subject>/`, later the approved corner captures), the renders below: three-quarter, side, back, a close crop of the main material, and a thumbnail at play distance. Save as JPEG, 2000 px or less (big PNGs posterise into contour bands when viewed, which looks like a lighting fault).
- **Scale:** a 1.75 m figure and a 2.2 m door beside anything whose proportion is in doubt (PHASE1 3.4).
- **Colour and value:** run `sc_ref_palette.py --k 7` on the mood render and compare with PHASE1 3.1 (key hues in sc-asset-craft's palette summary, value p10/p50/p90, warmth). For a single prop this is advisory (one prop isn't a scene); for corner captures it is the 3.1 check. Grade relations (timber darker than plaster, moss darker and greener than stone), not exact hexes.
- **Test it in a group** (the Sea of Thieves rule): render it with 3-4 neighbours from its kit or corner. An asset that reads alone can merge with its neighbours or out-shout them; kit pieces must match each other.
- **Material vs photo:** `ref_match.py` is not ported (sc-asset-craft 0b; `Art/Blender/tools/sc_ref_palette.py` replaces it). Put a crop of the material beside 2-3 flat-on photo crops, run `sc_ref_palette.py --k 7` on each, and judge detail per scale, edge crispness and value variation by eye.

## 2. Checks (budget and hygiene)
- Budget: within PHASE1 3.3 for its tier, or the header says why.
- Hygiene: no degenerate faces, loose verts or non-manifold edges on a new asset; real metres, base at y 0 (or as the header states), front +z.
- Craft advice, not checks: albedo carries no baked light (AO ships as its own map); keep generators reproducible.
Nothing else here is a gate. Approval is the user's, on captures (3.7).

## 3. Which review (PHASE1 section 9)
- **Batch review (T1-T3 families):** one contact sheet per batch with the reference row. The controller scores it and names the worst three; a fix agent fixes those three; the sheet is re-scored once.
- **Blind A/B (T4 heroes and the first item of a new family, e.g. the first B4 facade):** two fresh reviewers, one sheet: real references in the top row, candidates shuffled and unlabelled below; run `sc_imgdiff.py a.png b.png` first; self-judged results are weak evidence; snapshot with `sc_snap.py` before rebaking.
- `references/blind-ab.md` - read before setting up a blind A/B or parity test (heuristics and lessons from the stand's rounds).

## 4. Rubric (score each line 1-5)
Eight lines: 1 silhouette and read; 2 storybook proportion; 3 form and edges; 4 surface micro-detail and roughness; 5 colour and value (PHASE1 3.1); 6 wear story; 7 fits the set; 8 glass, liquid, emissive, metal (else efficiency).
- `references/rubric.md` - read when scoring: what a 5, a 3 and a 1 look like for each line.


**Side-by-side, always (user, 2026-10-30):** every review round builds one image with the reference pasted right next to the render (reference left, render right, same height, labelled) and scores from that sheet. For in-engine work, pair the Unity capture with the reference frame the same way.

**User calibration (2026-10-30):** the user reads our totals about 8-10 points low ("your 20/40 is more like a 30/40"). Treat **25/40** as the floor: if the first review already reaches it, the target becomes first score + 5 (art rounds end after two rounds in a row that don't win the blind A/B, or when the cause is unknown: probe it first, `docs/art/scene-judging.md` section 2.8; user, 2026-11-18), score only what the capture or play cameras see (backs, undersides and hidden faces don't count), and phrase every fix as a change visible at mid distance. Details: `docs/art/beauty-corner-brief.md` section 3.

**Whole-area views are not scored here.** Eye-level views of a whole place (Run 7 `HM_Eye_*` cameras, corner and room captures) use the world scorecard and blind anchored method in `docs/art/scene-judging.md` (sc-scene-look). This rubric is for single assets, kits and batches.

Rubric expectation (same as sc-aaa-prop): about 4 per line is shipped-quality art, and the /40 total is harsher than shipped games' own art scored blind, so use scores to rank and compare, not to pass or fail.

Write the review as:
```
<asset>: score NN/40  (budget/hygiene: ok | <which>)   reference row: <REFS.md or captures>
1 silhouette 4 - <one line of evidence>
...
Top fixes: 1) ... 2) ... 3) ...   (each: what, where, which line it should move)
```

## 5. Common defects and their fix
- `references/common-defects.md` - read when a render shows sandpaper or foam noise, melted rims, chip islands, stripes, black metal, neon liquid, plastic glaze or contour bands.

## 6. The engine is the final judge
Blender renders are proxies until a Unity capture agrees. Before calling a set of assets "matching the reference", ask the PC session for one round of captures (the fixed capture cameras of PHASE1 3.7 in the corner capture builders) and batch it. Report results to the controller with the numbers.


## Maintaining these skills
- `references/maintaining.md` - read after editing any sc-* skill or renaming a tool (path check command and the eval briefs).

## References
- `references/rubric.md`, `references/blind-ab.md`, `references/common-defects.md`, `references/maintaining.md`.
