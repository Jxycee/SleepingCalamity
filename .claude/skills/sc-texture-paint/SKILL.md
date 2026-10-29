---
name: sc-texture-paint
description: Make or tune SleepingCalamity's painted surface textures - the terrain layer maps and the detail texture arrays (env and Millpond) - so surfaces read as gouache/oil sketch in the Riverlight style. Covers the grey-centred detail-map rule, sources (Asset Store packs locally, Poly Haven CC0 anywhere), Kuwahara painting, chroma/contrast/ceil tuning, stroke scale vs tiling, adding a layer, and proving it. Use for any texture, grain, "surface looks CG/noisy/flat" or "add a new material layer" card.
---

# Painted textures for SleepingCalamity

All surface colour comes from the **palette slot** (seasons). Textures only add **painted grain**: a grey map centred on 0.5 that the shader multiplies by 2 x the slot colour. One generator makes them all: `Art/Blender/millpond/sc_millpond_textures.py` (deterministic, numpy, run in Blender):
```
blender -b --factory-startup --python Art/Blender/millpond/sc_millpond_textures.py -- [--only terrain|array] [--sheet <scratch>/sheet.png]
```
Outputs:
- terrain maps: `Assets/_Project/Content/Terrain/Millpond/Textures/T_Terrain_<layer>.png` (1024, linear)
- `T_MillpondDetailArray.png`: 4 x 8 tiles of 512. Layers 0-15 are a copy of the env array. 16 Stone, 17 Rust, 18 Slate, 19 PeelPaint, 20 Limewash, 21 Granite, 22 Brick (in the generator since duo r16; the array PNG gets it at the next local `--only array`). **23-31 are free.**

## 0. Who draws it? (decide first)
A layer nobody samples is the most expensive mistake (blind test #4 built a "landing path" layer for a mesh array that ground never reads).
- **Ground** (paths, banks, fields, the pasture) is **terrain**. `MillpondTerrain.LayerIds` (grass, forest, sand, dirt, gravel, rock, drygrass, lush) with `TileSizeM` per layer, and the generator's `TERRAIN` table write `T_Terrain_<id>.png`. A new ground look is a re-tune of one of those, or a new terrain layer (a change to `LayerIds`, `TileSizeM`, `TERRAIN` and the splat rules in `MillpondTerrain.cs`).
- **Meshes** (walls, roofs, boards, props, a mud strip modelled as a mesh at the slip) read **`T_MillpondDetailArray`** through uv1.y. A new layer here only shows on models that are built with its index.
- Name the consumer in the first line of your report: which builder or generator will use it, and on which assets. No consumer means don't build it; say so instead.
- Check the concept colour too: the concepts' paths are pale beige (#ae9369). Dark wet mud belongs only at the waterline and the slip.

For bakes (AO, curvature, ORM packing) and painted-albedo technique, see `.claude/skills/sc-asset-craft/library/texture-workflow/` and `library/hand-painted-style/`. They are generic: the grey-centred rule below wins.

## The house rule (never break it)
- Each map is luminance-normalised to a mean of 0.5 (`detail_normalise`): `gain = 0.5 / mean(lum)`, then `col = grey + (img*gain - grey) * chroma`, then `0.5 + (col - 0.5) * contrast`.
- Colour lives in the palette. **`chroma` (0-0.35) is the only colour a texture adds.** Use it deliberately (next section).
- Layers are never renumbered: the exported FBX carry the layer index in uv1.y. Add a layer at the next free index, and mirror it in `MillpondTextureLayers.cs` and the generator's `ML` table.

## Making it read painted (Riverlight)
| Knob | What it does | Typical |
|---|---|---|
| `kuwahara=r` | Four-quadrant Kuwahara: turns photo noise into flat brush patches with crisp edges. Half-width `(r + 1) // 2` (since duo r15; before that, odd k equalled the even k below it) | photos 2-8 (bigger = broader strokes); the stylised packs 0. **Ceiling about 10:** at k >= 12 the filter leaves 'H'/comb tick marks in dark cracks |
| `contrast` | how strongly the grain shows | soft painted ground 0.35-0.5; cracks/planes on rock 1.0-1.2; flat render with craze cracks up to 4.5 with `ceil` |
| `chroma` | the trace of the source's own hue: **painterly colour inside a surface** | 0 for lime/white (keeps pink out); 0.15-0.25 normally; 0.3-0.35 where the concept shows warm/cool strokes in one material (old timber, mossy stone) |
| `gamma` | < 1 lifts dark outline strokes in the painted packs before normalising | 0.8 for heavy-outlined packs |
| `ceil` | caps the brightest values, so a few bright blemishes don't stamp a grid at every tile | 0.56-0.7 when a source has sparse bright flakes |

Judging the result:
- **Triplanar.** `M_Millpond_Tri` (stone, brick, slate, tin) samples the array in world space on three axes, so the grain ignores each part's orientation. Painted courses (brick rows, slate lines) won't line up with the model: model the courses and keep the layer an uncoursed grain. Every coursed brick photo failed `repeat` anyway (0.31-0.71). `M_Millpond_Base` uses uv0 in metres, so boards keep their grain direction.
- **Stroke scale vs tiling.** The world materials tile at 0.6 repeats per metre (one 512 tile is about 1.7 m), and terrain mixes a second sample about 3.4x larger. A recognisable shape in the source (one leaf, one stone) becomes a giant repeated blob. Prefer sources whose features are smaller than about 1/8 of the tile, or blur them away with Kuwahara.
- **Repetition.** Look at the sheet at 3x3 tiles. Anything you can point at twice (a pipe stripe, a bright flake) must go: use another source, `ceil`, or a crop.
- **Concept check.** The concepts' surfaces are soft brush texture, "no photo noise, no grime". When unsure, go softer, and let the geometry and the AO carry the form.
- **Structure and a value story, not mottle.** The numbers (grain, stamps, seam) can all pass on a generic camo mottle. Each shipped layer has a **structure**: Stone has its crack network, Limewash a pale field with crisp crack strokes, Slate its planes, PeelPaint its flake edges. It also has a **value story**: which shapes are light and which are dark. Name both before tuning, then check the 3 x 3 sheet for them. For "wet", keep 2-3 large flat pale shapes per tile with soft dark rims: sheen comes from value.
- **Texture rubric:** five lines, each scored 1-5. A layer passes at **18/25 or more, with every line at 3 or more**. Line 1 is numbers, lines 2-5 are judged by eye.
  1. Numbers inside the pass lines. Each line comes from `sc_texture_proto.py` and was calibrated on layers 16-21 and env Wood/Rock (duo r13; structure, speckle and painted r15):

     | Metric | Pass line | Shipped range | Fails on |
     |---|---|---|---|
     | grain | 0.025-0.11 | | |
     | stamps | under 2.5% | | |
     | seam | under 1.5 | | |
     | band | under 1.0 | 0.13-0.82 | |
     | repeat | under 0.30 | 0.08-0.21 | aerial_mud_1's ruts 0.53 |
     | structure (connected thin strokes at 128 px) | 0.02 or more, unless the layer's structure is modelled (Rust corrugation, Wood boards, Granite rock meshes) | 0.014-0.094 | the WetMud camo 0.004 |
     | speckle (isolated thin dots = photo grit) | under 0.05 | max 0.034 | the r14 asphalt brick 0.084 |
     | painted (flat-patch share at 256 px) | 0.35 or more | 0.43-0.86 | the r14 asphalt brick 0.14 |

     landmark is advisory only.
  2. Repetition at 3 x 3: nothing you can point at twice, including one big feature per tile that forms a grid. Neither `stamps` nor `landmark` catches a recognisable shape (brown_mud_03's footprint): judge it by eye in the sheet.
  3. Painted structure: the material's own shapes are there as brush shapes and drawn strokes. Speckle doesn't count: an asphalt photo read as photo grit (duo r14), which `speckle` and `painted` now catch. Still compare by eye beside Stone and Limewash.
  4. Value story: light and dark shapes are clear at the 0.6/m scale.
  5. Concept colour fit: judge it on `--slot <name> --tint r,g,b`, the sheet in the shader's albedo. In its own colour, a repeat that hides in grey shows plainly. Kuwahara above about 8 erases the thin strokes (WetMud: structure 0.033 at k8, 0.027 at k14).

## Sources (licences recorded in `Art/Textures/Millpond/SOURCES.md`)
- **'pack': the user's free Unity Asset Store stylised packs** (Game Buffs, Blink, LowlyPoly). They are read from the Asset Store cache on the user's Windows PC (`%APPDATA%/Unity/Asset Store-5.x/...`). **Local sessions only.** Raw pack images are never committed (EULA). Only the processed maps ship.
- **'ph': Poly Haven CC0** diffuse, fetched once via `https://api.polyhaven.com/files/<id>`; take the 2k diffuse URL from the JSON, since its name varies (`_diff_`, `_diffuse_`). Save it as `Art/Textures/Millpond/Surfaces/<id>_diff_2k.jpg`. Works from cloud sessions too (the API is reachable). Always Kuwahara-paint photos.
- **'proc': procedural** (numpy noise, cellular cracks or stroke stamps, then Kuwahara) when no photo or pack has the material. The worked example is `proc_brick_clay` (layer 22 Brick, duo r16): laid patches in 4 flat levels with soft edges, domain-warped and dragged along one axis; a slow light/dark drift; a few long tapered strokes that fade at both ends; 2-3 cloudy bloom patches; then kuwahara 6. It took 7 versions. The traps: round dots read as polka dots, identical stroke stamps repeat, and plain noise reads as mottle. Scored 20/25; repetition (the strokes at 3 x 3) was the weakest line. Poly Haven has no uncoursed fired clay, for example. A generator function in `sc_millpond_textures.py` is its own source record: deterministic, with no licence row. Prefer it to a photo of a different material (asphalt as clay).
- Add a SOURCES.md row for every new source: id, URL, licence, author, date, what you changed.

## Cloud vs local
- **Cloud:** the 'pack' sources aren't in the cloud, so `--only array` fails there. Blender: `mkdir -p ~/apps && curl -sL https://download.blender.org/release/Blender4.5/blender-4.5.3-linux-x64.tar.xz | tar -xJ -C ~/apps`, then `~/apps/blender-4.5.3-linux-x64/blender`. Fetch Poly Haven sources into the scratchpad while prototyping; they go into `Art/Textures/Millpond/Surfaces/` only when the layer lands. Prototype with `Art/Blender/tools/sc_texture_proto.py`. It uses the generator's own kuwahara/normalise, with no array rebuild; it calls the generator's own `make_detail`, so the tile it writes is the tile the array gets:
  `blender -b --factory-startup --python Art/Blender/tools/sc_texture_proto.py -- <source.jpg> <scratch>/sheet.png 6,0.2,0.6 2,0.25,1.2` (k, chroma, contrast[, ceil[, gamma]] per setting). Each setting runs through the generator's own `make_detail`, so its numbers carry over to the array; `--tile <out.png>` writes the 512 tile. It writes a 3 x 3 sheet per setting and prints grain, stamps, seam, band, landmark, repeat and structure. `--measure <tile.png>` measures a finished tile and `--measure-array [millpond|env] [layers]` the shipped ones; both take `--sheet out.png --slot S --tint r,g,b`, so you can show today's layers in the target colour. `--relayer [slot,layer ...]` prints the layer a (slot, layer) pair is actually drawn with (the kit's RELAYER, PeelPaint and tin_roof rules); run it first to find the consumer's current layer. Shipped baselines: grain 0.027 (Wood, env layer 3) to 0.106 (Slate 18), stamps up to 2.3%. Wood and Rock are env-array layers 0-15, which the Millpond array copies; 16-21 are Millpond's own. The seam number compares only the outermost row and column, so also look at a 2 x 2 tiling for a band just inside the edge (brown_mud_02 showed edge lines at seam 0.79). Reject sources under about 0.025 grain (Poly Haven `clay_plaster`: 0.005-0.013). Then hand the rebuild to a local session. Use a Millpond Work Board card if your session has the Artifact tool; otherwise put it in your report or the branch's handoff note, for the controller. The card or note lists: the `ML` entry, the `ARRAY` source entry, the `MillpondTextureLayers.cs` const, `--only array` with a byte-check that tiles 0-21 are unchanged, the SOURCES.md row, reimport, and the consumer.
- **Local:** regenerate, then reimport (TextureArrayPostprocessor) and prove it with `MillpondCapture` (`cottage_close`, `mill_close` shots).

## Proving a texture change
1. `--sheet` contact sheet: each layer at 3x3 tiles, before and after.
2. On a model: an asset that uses the layer, through `sc_asset_preview.py`. The preview has no texture-array grain yet, so for texture work the Unity capture is the proof.
3. Unity: `MillpondCapture.Capture` from the concept cameras, side by side with the concept. Post it on the board card with the numbers you changed (for example "contrast 0.6 -> 1.05").
4. Commit: generator + PNG(s) + SOURCES.md row. Message: layer, source, knobs before -> after.
