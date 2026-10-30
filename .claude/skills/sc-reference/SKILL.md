---
name: sc-reference
description: Find, licence-check, store and measure reference images for Sleeping Calamity art (towns, dungeons, materials, props, plants) - free-licence photos for form, materials and wear, public-domain storybook and Romantic paintings for colour and value - and turn them into the measured targets the style bible and asset skills grade against. Use before building anything the style bible doesn't cover, when a review's colour line has no target, or when a card says "find reference".
---

# Reference images for Sleeping Calamity

**Target look:** Dark Cloud's whimsical town and dungeon themes rendered with the stylised realism of the 2026 Ocarina of Time Switch 2 remake (handoff section 2): real PBR materials at high detail (cobble, half-timber and plaster, dressed stone, woven cloth, leather, bark, moss), chunky readable storybook shapes, clean moody daytime colour (OoT-like, slightly muted), warm gold firelit Cellars with no teal, heavy atmosphere (haze, light shafts, drifting motes).

References tell you *what the thing is* (form, construction, proportion, wear, the colour relations between its parts). The style bible (`docs/PHASE1.md` section 3, the style bible, built on `docs/research/style-targets.md`) tells you *how this game renders it*: cleaner, tidier wear and chunkier forms than any photo.

## 1. What to look for (3-6 images per subject, two kinds)
- **Photos, for form and materials.** The subject in its real setting: a full view for proportion, a close view for construction (how the timbers meet, how tiles lap, how brick vaults spring), and a worn one for the wear beats. Material close-ups should be flat-on and evenly lit.
  - Town: half-timbered Central-European towns (Rothenburg ob der Tauber, Colmar, Bernkastel-Kues), Cotswold stone villages.
  - Dungeon: brick cisterns, barrel-vaulted cellars, crypts, root-choked ruins (Ta Prohm), coal cellars.
- **Paintings, for colour and value.** Public-domain storybook and Romantic landscape and village painters: Carl Spitzweg, Ludwig Richter, Caspar David Friedrich (atmosphere and value layering), Romantic-era German village painters. They show how a painter simplifies a scene into 3-4 value masses and where the light sits. Use them for value masses, colour relations and atmosphere, never for proportion or detail.
- **Plants:** search by botanical name plus "habitat". Sky and ground become the biggest masses, so crop hard.
- **Animals:** photos only (the painting search returns prints and frames for fauna).
- **Skip** renders, game screenshots, stock composites, anything stylised (cartoon, low-poly) and anything graded (HDR, black and white, infrared, heavy filters, orange-teal): they have no true colour and teach another look.
- **Copyrighted style frames** (official OoT 2026 screens, Dark Cloud captures from the user's own play) are study-only and live only in the gitignored `Reference/private/`. They are never committed, shipped, traced or projected. Measure them locally if useful, but record only numbers in docs.

## 2. Fetch and record (licence gate)
```
python -I Art/Blender/tools/sc_ref_fetch.py "<phrasing 1>|<phrasing 2>|<phrasing 3>" <scratch>/refs/<set> --n 8 --dedupe --sheet <scratch>/refs/<set>/sheet.png
python -I Art/Blender/tools/sc_ref_fetch.py "<subject>" <scratch>/refs/<set>_paint --paintings
```
(The tools need Pillow and numpy. `-I` ignores the user site-packages, so use an interpreter that has them in its own site-packages, e.g. a venv in the scratchpad.)
- It searches Openverse (Flickr, Wikimedia Commons, museum collections) with **CC0, public domain or CC BY only** (never ND or NC). Each image is resized to 1024 px on its long side. It writes `REFS.md` and `refs.json` with title, creator, licence and landing page per image.
- Openverse can take about 15 s to answer: if a run returns 0 images, run it again. The anonymous API returns at most 20 results a query and dead links are common. Rephrase instead of raising `--n` (try the local-language term: "Fachwerk", "Wassermühle", "colombage").
- **Run 3-4 phrasings by default** (`"q1|q2|q3"`); `--dedupe` takes one image per creator first. Drop near-duplicates from one creator.
- **The licence filter is not a subject filter.** Signs, models, engravings and sepia scans pass it. Choose from the contact sheet, not the file list.
- **Look before you measure.** Make the contact and palette sheets first and drop graded photos (HDR, B&W, sunset grades, filters).
- Spot-check one or two licences by hand on the landing page: the index is usually right, not always.
- **Reference images are study material.** Look at them, crop them, measure them. Never ship them, never project or trace them onto a texture or model, never put them under `Assets/`.

## 3. Store
- **Free-licence sets** that survive the session go in `Reference/<area>/<subject>/` (e.g. `Reference/hollowmere/architecture/`, `Reference/undercroft/cisterns/`), as the 1024 px JPGs the fetch tool wrote (Git LFS via `.gitattributes`; verify with `git check-attr filter <file>`), with a `REFS.md` in each folder: per image the file, source URL, creator, licence, and what it is for (form / material / colour / atmosphere). A CC BY image's credit lives in its REFS.md row.
- **Copyrighted style frames:** only in `Reference/private/` (gitignored).
- Contact sheets, palette sheets and logs stay in the scratchpad.

## 4. Measure (turn pictures into targets)
```
python -I Art/Blender/tools/sc_ref_palette.py <set>/*.jpg [--crops crops.json] [--exclude '#hex,...'] --k 7 --sheet <scratch>/palette.png
```
- **Masses** (k-means hexes with share), **value** (p10 / p50 / p90 and spread), **light warmth** (are the lights warmer than the darks?), and a `--targets` string of masses at 5% or more. Run per image and pooled.
- **Pool one variant only.** Summer and winter, day and lamplit, wet and dry brick average into a target nobody should hit.
- **Crop to the subject** (`--crop x0%,y0%,x1%,y1%`, or `--crops crops.json` with one box per image). Sky, water, people and signs otherwise become the biggest masses.
- **`--exclude '#hex'`** drops every mass within dE 30 of the colour, including the subject's own darks if they are close: exclude one colour at a time, check the `x` marks, and read dark subject masses off the masses line by hand.
- **Photo-to-game correction (the OoT-2026 shift).** Photos are noisy, dusty and desaturated by haze and weathering; the target is cleaner and bolder.
  - Keep the **relations**: moss darker and greener than the stone, lit plaster warmer than shaded plaster, timber darker than plaster, wet brick darker and cooler than dry.
  - Day scenes (PHASE1 3.1; key hues in sc-asset-craft's palette summary): no chroma boost on stone, plaster or roofs, +10-15% only on foliage, sky, painted wood, cloth and accents; plaster tan-cream, roofs red-terracotta, shadows slate-teal and readable, never crushed.
  - Dark scenes (the Cellars): clean warm firelight with no teal or blue-green; saturated gold lamps, shadows warm umber to plum, crisp value steps rather than brown haze; keep the darkest masses lifted enough to read shape.
  - Wear is tidy and placed (a few chips, streaks, moss patches) rather than uniform grime.
  - The measured numbers for the first area are in `docs/PHASE1.md` 3.1 (the style bible exists now) and `docs/research/style-targets.md`.
- **Paintings give the value structure.** Count the masses the painter used (usually 3-4) and where the darkest dark and the lightest light sit. That is the target for the value read of a scene or prop at thumbnail size.

## 5. Use them while building
- **Header.** Name the reference set in the generator or prefab note (`refs: Reference/<area>/<subject>/REFS.md`) with the 2-3 facts taken from it ("timber posts ~15 cm, plaster panels 1.2 m wide, jettied upper floor overhangs ~40 cm").
- **Proportion and construction from photos.** Measure against a known size in the frame (a door, a person, a brick) and write the dimensions down before modelling, then exaggerate toward chunky storybook proportion on purpose.
- **Wear beats** from photos of the old or broken thing: pick the 2-3 that change the value pattern at thumbnail size.
- **Reference colour scores are advisory.** Grade the *relations* `sc_ref_palette.py` prints, not an exact hex match, and say in the review that targets come from references.
- **Materials:** use close-ups for the pattern and scale (cobble size, tile course height, plank width) and PHASE1 3.1 for the cleaned-up colour.

## 6. When the subject has no good free reference
Some things don't exist (a jam-jar slime, a sentient broom, a root wall) or have no free photo. Build from first principles (real dimensions, how it would be made and how it would fail), name the closest real analogue in the header, and note on the board card or report that there is no reference, so the user can supply one.
