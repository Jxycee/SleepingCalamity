---
name: sc-reference
description: Find, licence-check, store and measure reference images for a SleepingCalamity asset or surface the Riverlight concept paintings don't show (a willow, a dry-stone wall, a clinker boat, a mill wheel) - real photos for form, proportion and wear, public-domain landscape paintings for painterly colour and value - and turn them into the targets the asset skills grade against. Use before building anything absent from the concepts, when a review's colour line has no target, or when a card says "find reference".
---

# Reference images for SleepingCalamity

The concept paintings (`Art/Design/Concepts/Millpond/*_concept_1.jpg` and their README) are the source of truth. When they don't show the asset, use references, but keep **the concepts' style**: references tell you *what the thing is* (form, construction, proportion, wear, the colour relations between its parts), and the concepts tell you *how Riverlight paints it*.

## 1. What to look for (3-6 images, two kinds)
- **Photos, for form:** the asset in its real setting, ideally the English/Northern-European lowland river country of the concepts. Get it from three angles if possible: a full view for proportion, a close view for construction (how the stones lap, how the strakes overlap, where the whips leave the bole), and one of it worn or broken for the wear beats.
- **Plants:** search by botanical name plus "clump" or "habitat" ("Iris pseudacorus clump"). Common names return lookalikes (sweet flag, flax), and sky and mud become the biggest masses, so crop hard and take targets from the subject masses only.
- **Animals:** photos only. The painting search returns still lifes, prints and frames for fauna (blind test #10), so skip it. Use 3-4 field-guide-quality photos of the exact pose and plumage, and drop juveniles, flight shots and backlit frames.
- **Paintings, for the look:** public-domain landscape painters whose world is Millpond's, such as Constable (mills, locks, willows, boats), Corot and Daubigny (river banks, willows), Van Gogh's Dutch drawings (pollard willows) and Cotman (watercolour stonework). They show how a painter simplifies the subject into masses, which is exactly what the model and the atlas must do. Use them for value masses, colour relations and edge treatment, never for proportion.
- Skip renders, game screenshots, stock-photo composites, and anything stylised (cartoon, low-poly): they teach another game's look.

## 2. Fetch and record (licence gate)
```
python3 Art/Blender/tools/sc_ref_fetch.py "<phrasing 1>|<phrasing 2>|<phrasing 3>" <scratch>/refs/<asset> --n 8 --dedupe --sheet <scratch>/refs/<asset>/sheet.png
python3 Art/Blender/tools/sc_ref_fetch.py "<subject>" <scratch>/refs/<asset>_paint --paintings   # museum + Wikimedia paintings
```
- It searches Openverse (Flickr, Wikimedia Commons, museum collections), with **CC0, public domain or CC BY only** (never ND or NC). Each image is resized to 1024 px on its long side. It writes `REFS.md` and `refs.json` with the title, creator, licence and landing page per image.
- It works in cloud and local sessions alike, sends a project User-Agent and backs off on HTTP 429. Openverse can take about 15 s to answer: if a run times out with 0 images, run it again.
- The anonymous API returns at most 20 results a query, and dead links are common. Rephrase rather than raising `--n`: try "knotwilg" (Dutch), "Kopfweide" (German), or "dry stone wall Yorkshire".
- **Reference images are study material.** Look at them, crop them and measure them. Never ship them, never project or trace them onto a texture or model, and never put them under `Assets/`.
- Keep them in the scratchpad while working. To keep a set for later sessions, commit the 1024 px JPGs plus `REFS.md` under `Art/Design/References/<topic>/` (Git LFS, like other images), with the licence rows kept. A CC BY image's credit lives in its REFS.md row.
- Check one or two licences by hand on the landing page: the index is usually right, not always.
- **Run 3-4 phrasings by default** (`"q1|q2|q3"`; `--dedupe` takes one image per creator first), not as a fallback ("clinker rowing boat", "wooden rowing boat beach", "rowing boat painted old", "clinker dinghy"). Drop near-duplicates from one creator or set: one search returned 4 shots of a single replica plus 2 interpretive signs.
- **The licence filter is not a subject filter.** Signs, kayaks, gondolas, engravings and sepia scans all pass it. Choose from the contact sheet (`--sheet`: index, licence, creator, query), not from the file list.
- **Look before you measure.** Make the palette sheet (below) first and drop graded photos: infrared, sunset or golden-hour grades, HDR, black and white, heavy filters. They have no true colour. In the first willow set, 1 of 4 was infrared, with white foliage, and another was a sunset silhouette.

## 3. Measure (turn pictures into targets)
```
python3 Art/Blender/tools/sc_ref_palette.py <scratch>/refs/<asset>/*.jpg [--crops crops.json] [--exclude '#hex,...'] --sheet <scratch>/palette.png
```
- **Masses** (k-means hexes with their share), **value** (p10 / p50 / p90 and the spread), light warmth (are the lights warmer than the darks?), and a `--targets` string of the masses at 5% or more. Run it per image and pooled.
- **Pool only one variant.** References of different finishes or seasons (varnished vs painted boats, summer vs winter trees) average into a target nobody should hit. Pool the ones that match the brief and keep separate pools otherwise.
- **Drop non-subject masses** with `--exclude '#hex'`. Careful: it drops *every* mass within dE 30, so a dark background excluded also strikes the subject's own blacks (the heron lost its black targets that way). Exclude one background colour at a time and check the `x` marks. `--exclude-de <n>` narrows the radius, and a guard keeps a mass that sits nearer another kept mass, but that only helps when the background forms its own mass. Read dark subject masses off the masses line by hand. `sc_ref_fetch` takes `--timeout` (30 s) and `--retries` (2). Typical exclusions: grass under a boat, the carpet in a museum shot. `--crops crops.json` holds one box per image (`{"03_x.jpg": "10,20,90,85"}`).
- **Crop to the subject** (`--crop` in percent), one image at a time when the framing differs. Sky, water and people otherwise become the biggest masses: the uncropped willow pool's largest masses were sky blue and path gravel. Look at the sheet: a stray bar means a crop is needed.
- **Photo-to-Riverlight correction.** Photos are brighter, more saturated and noisier than the paintings.
  - Keep the **relations**: the moss darker and greener than the stone, the lit leaves warmer than the shade, the tarred bottom darker than the topsides.
  - Pull saturation and value toward the concept README palette before tinting (`sc_tint.py tint '<#hex>' --ao 0.8 --lit`).
  - Measure a concept crop with the same tool for comparison. The rundown landing reads p10 0.23 / p50 0.41 / p90 0.80, spread 57, with cool lights.
- **The concepts sit close to Constable.** Measured with `sc_ref_palette.py` (2026-10-25):

  | | darkest mass | p10 / p50 / p90 | spread | lights vs darks |
  |---|---|---|---|---|
  | Constable, *Flatford Mill* (CC0, Wikimedia) | dark green-brown, 26% | 0.21 / 0.39 / 0.75 | 54 | cooler |
  | `mill_weir` concept | dark green-brown, 33% | 0.18 / 0.44 / 0.88 | 70 | warmer |

  So a public-domain English landscape is a fair stand-in when no concept shows a subject. Riverlight has brighter, warmer lights than Constable's.
- **Paintings give the value structure.** Count how many masses the painter used (usually 3-4) and where the darkest dark sits. That is the target for rule 5 (three value masses and a contrast pair) and for the foliage clump cells.

## 4. Use them while building
- **Header.** Name the reference set in the generator header (`refs: Art/Design/References/<topic>/REFS.md`, or "scratch refs: <query>") with the 2-3 facts you took from it ("strakes overlap 20-25 mm; 7 strakes a side on a 4 m boat").
- **Proportion and construction from photos.** Measure against a known size in the frame (a person, a door, a brick) and write the dimensions down before modelling. That is rubric line 2.
- **Wear beats from photos of the broken or old thing** (rule 11): pick the 2-3 that change the value pattern at 48 px.
- **Reference colour scores are advisory.** `sc_concept_match.py --targets` against reference masses scores the subject *variant* as much as the model. The blind boat got 28 and the shipped one 44 against a pool that was mostly varnished boats. Grade the **relations** instead, which `sc_ref_palette.py` prints: the darks vs the lights (a tarred bottom much darker than the topsides), lights warmer than darks, and p90 (references 0.78). State in the review that the targets come from references.
- **Foliage and textures:** the painting references show what a crown or a surface reduces to at the painted scale. Paint atlas cells and `proc_*` textures toward that reduction, not toward the photo's leaf-level detail (sc-foliage, sc-texture-paint).

## 5. When the subject is in neither
Some things have no good free reference: a fictional machine, or a regional variant. Then build from first principles (real dimensions, how it would be made and how it would fail), name the closest real analogue in the header, and say on the board card or in the report that the asset has no reference, so a local session or the user can supply one.
