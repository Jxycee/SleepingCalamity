---
name: sc-asset-adopt
description: Bring an external free 3D model or base mesh (CC0 or CC BY from a checked source - Poly Haven, Kenney, MPFB2 / MakeHuman, the scenario-labs/3d-collection on Hugging Face) into Sleeping Calamity - licence check, source record, import, scale, decimate per part, then make it ours in the locked style (keep or rebake its PBR maps, or re-texture with the area's shared tileables), budget and review. Use when a brief or the user points at an outside model, when a prop or kit piece with a complex silhouette would take far longer to generate than to adopt, or for the MPFB2 hero body.
---

# Adopting an external model

## 1. Licence and source (gate)
- Allowed: CC0 (Poly Haven, Kenney, ...) and CC BY 4.0 (credit required), from a source on the trusted list in `.claude/rules/art-assets.md`. Check the licence on the asset's own page or the dataset card, not a mirror. Non-commercial (NC), personal-use and community licences are also allowed with a provenance tag so they can be swapped later (`.claude/rules/art-assets.md`; personal-use bar, user, 2026-11-06). **No** ND (it forbids modification), "editorial", or unknown licences. Never a paid asset. The trusted-source check still applies.
- **MPFB2 (MakeHuman's Blender extension) for the hero body** (PHASE1 section 5): PHASE1 expects its output to be CC0 and asks this skill to confirm it. Read the licence that ships with the extension and its asset packs (body, skins, clothes may differ) and record what it says before the body is used.
- **AI-generated sets** (scenario-labs/3d-collection: dataset card CC BY 4.0, credit "Scenario") are fine under the same rule. Record the row id, the generator note ("image-to-3d from an unstated input image"), the file and its sha256.
- Put the raw file under `Art/Source/<Provider>/<asset>/` (LFS) and add a row to `Art/Source/<Provider>/SOURCES.md` (the first row from a provider creates that file; texture batches use `Art/Textures/<batch>/SOURCES.md` instead, see sc-texture-paint): asset, id/URL, licence, author/credit line, date, what we changed. Never put raw sources under `Assets/`. A CC BY credit also goes in the game's credits list once one exists.
- **Finding a file in scenario-labs/3d-collection:** the listing has only opaque ids; search the metadata parquet's titles. FishGame's `fg_adopt_find.py` did this and is not ported (sc-asset-craft 0b): read the metadata parquet directly with pandas or pyarrow and filter on the title column. Use the `.glb`, not the `.blend` (newer Blender, and AI-built scenes).

## 2. Is it worth adopting? (2 minutes)
Adopt only if all three hold:
1. **The shape can reach the style:** real construction that can be pushed to chunky storybook proportion (PHASE1 3.4). Photoscan noise without a clean silhouette, or low-poly cartoon, rarely can.
2. **The topology survives decimation** to its tier budget (PHASE1 3.3) with the silhouette intact.
3. **Faster than generating.** A crate or a plain barrel is faster to generate in its family batch and matches its neighbours better. Adopt complex silhouettes: ornate ironwork, machinery, statues, a base body.

FishGame's test: an adopted building lost to generated ones (29/40 after 25 min vs 33 in 20 min). Generate buildings and kits; adopt complex single objects.

## 3. Import and prepare
`Art/Blender/tools/sc_asset_adopt.py` was written for FishGame's palette/vertex-colour encoding (sc-asset-craft 0b): its colour fit, encode and export read FishGame's missing `Art/Blender/sc_harbour.py`, and `--preview-yaw` uses the legacy preview, so a full conversion is **legacy, rework before use**. `--list-parts` runs, and the prepare functions (`import_any`, `prepare`, `decimate`) import cleanly for your own generator (checked 2026-10-29):
- `--list-parts` first: name, tris, islands and materials per source part. Many islands means unwelded: `--weld 0.0001`.
- **Decimate per part, not per join:** `--part-tris 'chain=0,rope=500'` gives each source part its own budget (0 drops it). One ratio on the joined mesh faceted the small round parts while the thin ropes stayed dense. Generated sets bundle things that aren't the object (the trawler's anchor chain was 15k of 40k tris).
- **Facing:** render the source at yaw 0/90/180/270 on one sheet (what `--preview-yaw` did, until the preview is reworked) and pick the one whose front faces the three-quarter view. Don't reason it out from "screen left".
- `--exclude` drops source objects by name (plants, clutter you'll generate).
- Scale by a known feature (a door, a step, a hand), not the bounding box.
- Origin at the bottom centre, front +z in Unity, real metres (sc-asset-craft 2).

## 4. Make it ours (the step that makes it look like the game)
The source's own materials are someone else's look. Pick per asset, by how the result looks:
- **Keep and grade its PBR maps** when they are good scans at the right texel density: correct the albedo toward the PHASE1 3.1 key hues (sc-asset-craft palette summary: no chroma boost on stone, plaster or roofs, no baked light), and check roughness and normal read at review distance.
- **Rebake** when the topology changes (decimated or reshaped): bake the source high onto the new low, then grade as above.
- **Re-texture with the area's shared tileables and trims** (sc-texture-paint) when it should sit in a kit: the pieces then match their neighbours and pick up every tileable fix.
- Push the shape toward storybook proportion (thicker timbers, bigger hoops, chamfers on play-distance edges) and add the 2-3 wear beats the brief names (sc-asset-craft 3).
- Do it in a small generator (one file per area for adopted assets) that imports the prepare functions, then edits, bakes and exports, so the result is reproducible from the source file plus the script.
- Commit the generator, the SOURCES.md row and the outputs together; the commit message names the source and licence.

Then, as with any model, **sc-asset-review** (audit, review renders with the reference row, rubric).

## Notes from FishGame's 2026-10-25 study (scenario-labs/3d-collection)
- Its `.blend` files were built by AI agents in Blender: UV-mapped with baked image textures (1-4K per part, 15-56 materials) and little else. Rebake them into the tier's texture sets (PHASE1 3.3) before export.
- Worth adopting from it then: the fishing trawler (silhouette and deck clutter) and the rock kit. Most other rows were sci-fi, characters or grimy realism; re-search it for this project's subjects before assuming anything fits.
- Habits worth copying even for generated assets: a README inside the asset, numbered named parts, the backdrop excluded from export, the base on z 0, pivots as named empties with hinge limits as custom properties.
