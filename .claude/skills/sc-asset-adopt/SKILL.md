---
name: sc-asset-adopt
description: Bring an external free 3D model (CC0 or CC BY, from a checked source such as Poly Haven or the scenario-labs/3d-collection on Hugging Face) into SleepingCalamity as a permanent prop - licence check, source record, conversion to the palette/vertex-colour encoding with sc_asset_adopt.py, budget, review. Use when a board card or the user points at an outside model, or when a permanent prop (boat, crate, barrel, machinery, stonework) would take far longer to generate than to adopt.
---

# Adopting an external model

**Scope (user, 2026-10-25): permanent items only.** Boats, crates, barrels, tools, machinery, stonework and furniture: things whose colour does not change with the season. Living or seasonal things (trees, grass, reeds, crops, moss-covered surfaces) stay generated, because their colour must come from a seasonal palette slot. If the model mixes the two (a cottage with a thatch roof and planters), adopt the permanent shell and generate the living parts.

## 1. Licence and source (gate)
- Allowed: CC0 (Poly Haven, Kenney, ...) and CC BY 4.0 (credit required), from a source on the trusted list in `.claude/rules/art-assets.md`. Check the licence on the asset's own page or the dataset card, not a mirror. **No** NC, ND, SA, "editorial", "personal use", or unknown licences. AI-generated sets (scenario-labs/3d-collection: CC BY 4.0, credit "Scenario") are fine under the same rule.
- Put the raw file under `Art/Source/<Provider>/<asset>/` (LFS) and add a row to the provider's record (`Art/Source/<Provider>/SOURCES.md`; Tripo keeps a table in `README.md` instead): asset, id/URL, licence, author/credit line, date, what we changed. Never put raw sources under `Assets/`.
- A CC BY credit also goes in the game's credits list when one exists. Until then the SOURCES.md row is the record.

- **AI-generated sources** (scenario-labs): the dataset card's CC BY 4.0 governs the output. Record the row's generator note too ("image-to-3d from an unstated input image"), the dataset row id, the file and its sha256. The first row from a new provider creates `Art/Source/<Provider>/` and its `SOURCES.md` header (Scenario has none yet).
- **Finding a file in scenario-labs/3d-collection:** the file listing has only opaque ids. Read the metadata parquet (17 MB; `pip install --user pyarrow`) and search its titles. Search it with **`python3 Art/Blender/tools/sc_adopt_find.py "<query>"`**: it caches the parquet in `$TMPDIR` and prints the id, licence, workflow (image-to-3d / text-to-3d = AI-generated), prompt, `.glb` path and sha256 prefix. It matches whole words and plurals (search "boat" and "tugboat" separately) and prints the true total. The dataset has 1 342 rows; "rowing boat" returns none, "boat" a tug and a steamboat. The trawler is row `c908eeca2605ca5a` ("a small weathered fishing trawler"), file `d646af767b201652.glb` (19 meshes, 40k tris). **Use the `.glb`**: `import_any` takes glb, gltf, fbx and obj, not `.blend` (which is also from a newer Blender).

## 2. Is it worth adopting? (2 minutes)
Look at the preview and the triangle count, and adopt only if all three hold:
1. **Shape fits Riverlight:** real proportions, chunky and hand-made, not photoscan-noisy and not cartoon-cute.
2. **Topology survives decimation** to the class budget (sc-asset-budget) with the silhouette intact. Scans of 500k tris with no clean silhouette usually don't.
3. **Faster than generating.** A box crate or a plain barrel is faster to generate with the kit and matches better. Adopt complex silhouettes: boats, engines, cranes, ornate ironwork, statues.

## 3. Convert
```
blender -b --factory-startup --python Art/Blender/tools/sc_asset_adopt.py -- \   # cloud: ~/apps/blender-4.5.3-linux-x64/blender, and write to the scratchpad, not Assets/
  Art/Source/<Provider>/<asset>/<file>.glb  Assets/_Project/Content/Models/Millpond/Props/Millpond_Prop_<Name>.fbx \
  --name Millpond_Prop_<Name> --size <metres> --axis height|length --tris <budget> --report <scratch>/adopt.json \
  [--exclude Planter,FlowerBox] [--rules "plaster:slot=11,layer=20,tri;stone:slot=6,layer=16,tri;timber:slot=6,tint=0.68/0.64/0.56,layer=3;iron:slot=6,tint=0.30/0.31/0.33;glow:slot=6,tint=0.3/0.3/0.32"]
```
What it does:
- joins and scales to real size (origin at the bottom centre, front +z in Unity) and decimates to the budget
- samples the albedo per face
- keeps **one tone per source material** by default (`--per-region 1`; more makes fine-faceted parts a per-face mosaic, duo round 7) and leaves the painterly variation to the paint pass. It never mixes tones across materials (a region; never across, so a 17k-tri thatch doesn't turn into per-face confetti, duo round 6)
- fits each tone to a permanent slot plus a tint, bakes our AO, and writes UVMap/Layer/Col with the project's FBX settings

The flags:
- `--rules` (substring of the material name): force a region's slot, its colour (`tint=r/g/b`, e.g. a kit constant from the craft table: the duo's tool-only cottage matched its hand script once tint was allowed), its texture layer, and send stone, render and slate regions to `<out>_Tri.fbx` (`tri`), the way the kit splits buildings.
- `--exclude`: drop source objects by name (planters, flower boxes, anything living), so you adopt the shell and generate the seasonal parts.
- White render or plaster goes on slot 11 Snow (or 5 Sand) with layer 20 Limewash. On Rock it clips at tint 1.9.

**Before converting, look at the parts list** (`--list-parts`: name, tris, islands and materials, sorted by tris; many islands means unwelded, so add `--weld 0.0001`, which merges coincident verts before decimation: the r6 cottage went from 1 612 loose verts to 0). Generated sets bundle things that aren't the object: the trawler's anchor chain was 15k of its 40k tris, and a quay bollard stood off its bow. `--part-tris 'anchorchain=0,bollard=0,ropecoils=500,gunwalerails=900'` gives each source part its own triangle budget before the join; 0 drops the part, and the loose verts it makes are removed. Otherwise the converter decimates the joined mesh at one ratio, which turned the fenders octagonal while the ropes stayed dense.

**Facing:** run `--preview-yaw` once. It renders the source at yaw 0/90/180/270 on one sheet; pick the yaw whose front faces the top-left three-quarter view. `--yaw` turns counter-clockwise seen from above. Don't reason it out from "screen left": that cost the blind run a full round.

Then, as with any model, **sc-asset-review**: audit + preview + rubric. Typical follow-ups (a slot with an odd-looking tint is fine as long as it doesn't clamp: check the rendered colour with `sc_tint.py color <slot> <tint>`):
- A patch landed on the wrong slot (white trim on WoodDark with a clipped tint): rerun with `--slots` narrowed, or `--colours` up by 2-4 so the trim gets its own patch.
- Too dark or too flat: `--ao-floor` 0.55-0.6; more colours for a busier object.
- Seams or smear across flat panels: `--smooth-angle` 30.
- Wrong facing: `--yaw 90|180|270`.

## 4. Make it ours (the step that makes it look like the game)
The converter gives a faithful but generic result. **For multi-colour textured parts it is the step that makes the asset work at all.** The trawler's hull livery (red bottom, blue stripe, white band, blue top: one material, one texture) collapsed under `--per-region 1` into a single lilac tone. The fix:
- cut the hull at the paint lines and give each band its own slot (one slot per face: the shader blends slot ids across a face whose corners differ);
- bisect the decimated faces at the stripes, or the vertex colours smear (about +800 tris).

**Now in the converter (r32):**
- `--regions 'hullpaint=4,deck=2'` gives one material several colour regions, clustered from the sampled albedo. The trawler's livery came out as red / white / blue / grey bands with no hand cuts.
- **Band edges:** regions are assigned per decimated face, so stripe edges zig-zag along a hull. **`--band-cuts 'hullpaint'`** cuts each face along the painted boundary sampled from the texture, so the bands follow the livery's own edge. On the trawler: 223 contour cuts, +584 tris, and the bands read as a worn, uneven paint edge. Generated liveries follow the sheer, not level lines, so `--band-planes` (horizontal cuts) only suits a level livery on a hand-made UV. Budget for the cuts: pass `--tris` about 10% under the ceiling.
- **`--mute 0.5`** pulls each region's colour halfway toward the nearest concept README hex (Lab) and caps chroma at the README's highest; slots are kept. The trawler's royal blue became slate and its antifoul a dull red: colour went from 3 to 4 by eye, for a slightly narrower value spread (p86 to p84). Start at 0.5 for any AI-generated source.
- **The converter's colours are a straight fit** unless you pass `--mute`. For finer control import the functions (`prepare`, `decimate`, `sample`, `regions`, `bake_ao`, `encode`, `split_and_export`) and edit the region fits between `regions()` and `encode()`.

To make it read as Millpond:
1. Re-tint toward the concept palette (README): the converter's patch tints are a starting point. Pull them to the nearest concept hex values.
2. Add the wear story for its scene (rundown/restored): the faded paint, moss slot on the up-facing edges, a rust layer on fixings. Do it in a small generator (create `Art/Blender/millpond/sc_millpond_adopted.py` the first time; one file for all adopted props): it calls the converter's functions, then edits Col/Layer per patch or region. That keeps the FBX reproducible from text (source file + script).
3. Commit the generator, the SOURCES.md row and the FBX together. The commit message names the source and licence.
4. **Normal-map detail is lost** (SleepingCalamity/Toon has no normal maps). Panelled wheelhouses and planked decks come out as flat boxes. Add the 2-3 details that read at play distance as geometry: window frames, a door, a rail.

## 5. Placement conventions
- **Harbour props:** name them `Harbour_Prop_<Name>` and put them in one adopted-props generator per region. The harbour draws the **env array**, so use layers 0-15 only (Wood 3, Rock 6, Cloth 7; there is no metal layer, so iron goes on Rock 6 or none); Millpond layers 16+ (Rust, PeelPaint) are out of range there. With no Harbour concept art, re-tint toward the Millpond README palette.
- **Boats afloat:** the converter puts the origin at the keel bottom, so placement sinks the hull (the 9.5 m trawler sits about 0.89 m low). Paint the wet band as a value band at the waterline, and keep the underwater hull out of the AO ground plane; use `--waterline <m>` (origin at the waterline, keel at -m, the AO ground plane at the waterline), so placement needs no sink.
- **Budget class for vessels:** a moored 9-10 m boat is graded as `--class building` (the houseboat precedent; the prop median gives a meaningless efficiency 1). The trawler: 5 978 tris, building grades value p84, painterly p98, silhouette p54.

## Notes from the 2026-10-25 study (scenario-labs/3d-collection)
- Its 135 `.blend` files were built by AI agents in Blender. They are mostly UV-mapped with baked image textures (1-4K per part, 15-56 materials) and almost no vertex colour. That is the opposite of our encoding, so convert them; never import them as they are (normal maps and per-asset materials are ignored by `SleepingCalamity/Toon`).
- **It has no dockside gear** (searched 2026-10-25: no crane, winch, capstan, crates, lobster pots, buoy rack, bollard or anchor; "harbour", "quay", "pier" and "mooring" return nothing). Generate harbour gear with the kit. A blind run that adopted its bronze field cannon anyway failed at 27.
- Worth adopting from it: the fishing trawler (silhouette and deck clutter) and the rock kit. **Not** the thatched cottage, as a Millpond building: there is no thatch in the Millpond concepts (slate and tin), and its storybook shapes (arched round door, finials, bulbous thatch) break R102's "real shapes, not cute". It was useful as a converter test. Most others are sci-fi, characters or too realistic and grimy.
- **Duo test (round 6): an adopted building lost to generated ones.** The converted cottage reached 29/40 at 21k tris after a converter rewrite and 25 min. The generated net shed reached 33 at 4.6k tris in 20 min. Adoption wins only on complex silhouettes (boats, machinery, ornate ironwork). Generate buildings with the kit.
- Thatch and straw are dead plant matter: permanent (Sand plus a muted straw tint, measured with `sc_tint.py --lit`). Moss on them is seasonal (Reed).
- The habits worth copying, even for generated assets: a README inside the asset, numbered named parts and collections, the backdrop excluded from export, the base on z 0, pivots as named empties with hinge limits as custom properties, and a validation note ("all 39 base vertices fixed over every frame").
