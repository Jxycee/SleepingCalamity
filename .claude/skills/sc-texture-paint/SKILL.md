---
name: sc-texture-paint
description: Make or tune Sleeping Calamity's PBR surface materials - tileables, trim sheets, decals and the smart materials that feed bakes (town plaster, oak timber, cobble, sandstone, roof tile, painted wood, canvas, iron; cellar brick, vault stone, limewash, flagstone, barrel oak, soot, root bark) - from CC0 scans (Poly Haven, ambientCG) plus procedural layers, graded to the style bible's clean saturated colour. Covers naming the consumer, sources and licences, texel density, scan-to-style correction, repetition and seam checks, packing for URP Lit, and proving a change. Use for batches B1-B3, any "surface looks CG / photo-dull / noisy / tiled" problem, or adding a material.
---

# PBR materials, tileables and trims

The skill name is historical: nothing is hand-painted. Every surface is a real PBR set (albedo, normal, ORM) made **once per area as a smart material**: a CC0 scan plus procedural layers in the `aaa_mats2.py` style, shared by every kit piece that uses it (PHASE1 3.2, handoff 3.5). This is where most of the quality comes from, and a fix here reaches every piece.

Tools (status in sc-asset-craft 0b): `aaa_mats2.py`, `aaa_scan.py`, `fetch_textures.py`, `pack_unity.py`, `ref_match.py` are **FishGame only, port before use** (M8). `sc_ref_palette.py` is current. FishGame's texture-array generator and `fg_texture_proto.py` were for the old grey-centred painted arrays and don't apply.

## 0. Who uses it? (decide first)
A material nobody samples is the most expensive mistake (FishGame built a whole layer that no mesh read).
- Name the consumer in the first line of the report: which kit pieces, family bakes or hero props use it, and at which texel density (PHASE1 3.3: 1024 px/m for ground and walls up to 3 m, 512 px/m for upper storeys, roofs and vault crowns).
- **Tileable** (T1): surfaces with no edges to follow (plaster fields, cobble, brick, flagstone, roof tile). UVs in metres on the kit pieces.
- **Trim sheet** (B3): strips for edges and long members (timber trim, stone moulding, iron trim), one sheet per area, so beams, sills and copes share one texture set.
- **Decal** (B3): placed wear (moss, grime, drips, cracks) over the tileables, so wear is tidy and placed, not baked into the repeat.
- **Smart material for a bake** (T3/T4): the same material evaluated in part space on a high, then baked; the tileable and the bake must read as one material side by side.
- No consumer means don't build it; say so instead.

## 1. Sources (licences recorded)
- **Poly Haven CC0** and **ambientCG CC0** scans: albedo, normal, roughness, displacement, AO. Poly Haven's API: `https://api.polyhaven.com/files/<id>`; take the file URL from the JSON, since names vary. Fetch into the scratchpad while prototyping; keep scans out of the repo until the material lands.
- Record every source the material ships with in `Art/Source/<Provider>/SOURCES.md` (the same record sc-asset-adopt uses): id, URL, licence, author, date, what you changed. A procedural generator function is its own source record.
- **Procedural only** when no scan has the material (a fantasy surface, an ember crack). Plain noise reads as mottle and round dots as polka dots; build structure first (cells, strokes, cracks), then break it up.
- Reference photos (sc-reference) tell you the pattern's scale and the colour relations. They are never textures: never project or trace them.

## 2. Make it read as the style
- **Scan to style (the OoT-2026 shift, PHASE1 3.1).** Scans are dusty, desaturated and lit. Keep the **relations** (moss darker and greener than stone, wet brick darker and cooler than dry, timber darker than plaster), then:
  - push chroma +15-25% on plaster tints, roofs, painted wood, cloth and foliage; keep plaster clean near-white and roofs a confident red;
  - take the colour toward the PHASE1 3.1 key hues; measure the albedo with `sc_ref_palette.py --k 7` under a neutral render;
  - **no light in the albedo:** remove the scan's baked shadow and AO from albedo (AO ships in ORM);
  - Cellar materials stay warm (tan and orange brick, gold-lit stone); teal comes only from the grade's shadow and fog tint.
- **Structure and a value story, not mottle.** Every material has a structure (brick bond and mortar, timber grain and checks, plaster trowel relief and craze, flagstone joints) and a value story (which shapes are light and which dark). Name both before tuning, then check them on the 3 x 3 sheet.
- **Multi-scale detail.** One scale of noise reads as "sandpaper" or "felt". The mortar fix was a coarser scan tile, a weaker fine normal and stronger value contrast (tile 0.12 -> 0.25, normal 1.8 -> 0.8, contrast 0.45 -> 0.9). Detail under about 1 mm doesn't register at review distance.
- **Roughness carries the material** as much as albedo: wear, polish, damp and glaze change it. A single waxy roughness was the stand's most repeated review complaint.
- **Wear is tidy and placed:** a few chips, streaks and moss patches, via masks and decals, over a clean base. Uniform grime is the old photo look, not this one.
- **Author in part space** for smart materials, so patterns stay continuous across UV seams and vary per part; world space only for scene storytelling.

## 3. Check it (before any model uses it)
1. **3 x 3 tiling sheet** at the consumer's texel density: nothing you can point at twice (a bright flake, one big stone, a stain). A recognisable feature larger than about 1/8 of the tile becomes a repeated blob: crop, swap the source, or break it with a second, larger-scale variation layer.
2. **2 x 2 seam check:** look for a band just inside the edge as well as the edge itself.
3. **On a model:** apply it to its consumer kit piece and render studio plus mood (sc-asset-review). A tileable that looks right flat can look wrong at a grazing angle or beside its neighbour.
4. **Reference matcher** (`ref_match.py --inv`, port) against 2-3 flat-on photo crops of the same material, if the set calibrates.
5. Colour and value inside PHASE1 3.1 on the render, measured with `sc_ref_palette.py --k 7`.

## 4. Pack and ship
- Pack for URP Lit with `pack_unity.py` (port): don't hand-pack channels from memory. Normal maps are OpenGL Y+ (as Blender bakes and Unity expects).
- Emission bakes into 8 bits: keep it at or below 1 in the bake and scale it in the game material.
- Textures go through Git LFS. Commit the generator or material script, the maps and the SOURCES.md row together; the message names the material, source and the knobs changed (before -> after).
- **Cloud sessions** can fetch Poly Haven and prototype, but commit the script, not the bakes; the PC rebakes and proves it in Unity.

## 5. Proving a change
Blender renders are proxies. For a tileable change that touches many pieces, ask the PC for one round of Unity captures of the kit in its corner (PHASE1 3.7 cameras once built) and compare before and after, with the numbers you changed.
