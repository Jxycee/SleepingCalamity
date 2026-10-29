---
name: sc-ai-model-pipeline
description: End-to-end pipeline for AI-generated 3D models (Tripo, Hunyuan3D) in Sleeping Calamity - generation settings for the locked style and the PHASE1 character proportions, intake and diagnosis, the proven blur/halo texture repair, optional masked AI upscale, URP Lit import, review - plus a symptom-first troubleshooting guide for texture and shape problems. Use when a user brings a Tripo/Hunyuan export (a hero's head, hair or outfit, an organic enemy, a building part), says an AI model's texture is blurry, smeared, ghosted or haloed, asks why it looks soft or "PlayDoh" in game, or wants our own generators to build shapes the way Tripo does.
---

# AI model pipeline (generate → diagnose → repair → import → review)

Evidence for every rule here: FishGame's `docs/superpowers/research/2026-10-28-ai-model-pipeline-findings.md` (entries P1–P9, K1–K2, M1–M3, C1–C2; read it in `C:\Dev\FishGame`). Proven on a few models so far: the user's Tripo "Dark Cloud" cottage, a mill, a raccoon. **Run it on another model before you trust the per-model numbers.**

**The tools are FishGame only, port before use:** `C:\Dev\FishGame\Art\Blender\tools\experimental\` (`fg_tripo_*.py`, `tripo_repair/`, `tripo_detail/`, `tripo_sr/`). Copy them into `Art/Blender/tools/experimental/` with the `sc_` prefix (CLAUDE.md: copy, never edit FishGame). The commands below use the ported names.

## Style and proportion targets (PHASE1 3.6 and section 5)
- **The look:** stylised realism. Real PBR material at high detail (skin, stitched leather, woven cloth, fur, metal), slightly stylised form; storybook, not chibi, not toy (sc-asset-craft section 3).
- **Hero proportions:** about 6.5 heads tall; hands and feet about 1.15x real with chunky boots, gauntlets and belts; eyes about 1.1x; faces realistic in material. Heights: Tobin 1.90 m, Ash 1.75 m, Wren 1.68 m, Pip 1.58 m. Each hero reads at 30 m from silhouette alone.
- **Heroes are a hybrid** (PHASE1 5, [P29]): an MPFB2 body for topology, rig and weights (licence: sc-asset-adopt), with the **head, hair and outfit generated as separate parts** from multi-view concept images, retopologised and cleaned here and with `texture-bleed-cleanup`, then skinned by weight transfer. Outfits stay separate meshes on one skeleton. If a part fails cleanup twice, model it in Blender over the body instead.
- **Budgets:** PHASE1 3.3 (hero 60k tris, 1 x 4K body and outfit, 1 x 2K head and hair; enemy 25k, one 2K-4K set; a whole building at most 75k).
- **Inputs** should show the target directly: chunky storybook proportion, clean saturated colour, evenly lit with no baked shadows.

House rules for this pipeline:
- **Metrics serve the look.** A lower soft fraction that reads darker, busier or noisier is a break (M2: the mill's spokes). Value-match any added detail to the original paint's local mean, and cap its contrast at the part's own crisp areas.
- **Do what makes the asset look best for what it is** (user, 2026-10-29). Every step, setting and import option is chosen per asset by how the result looks, not by the default. A building, a creature and a prop each get what suits them: skip, change or add steps, and say why in the log. When two options trade off, take the better-looking result, and log the other as fixes / breaks.
- Measure before you fix. Every fix is logged as **Fixes / Breaks / Keep** in the findings log, with a render pair. A reverted fix still counts for what it fixed (user, 2026-10-28).
- Fix in 3D and by geometry (part, face, plane), **never by blurring in UV space**: UV-space blur leaves seam wedges at island borders (FishGame's character cleanup).
- Reference images are study material. Fill only from the model's own sharp texels.

## 0. Generate (source settings, ranked)
Researched from Tripo's docs; not yet A/B-tested by us.
1. Multi-view input (front/back/left/right, same light and scale). This attacks the root cause: surfaces the generator can't see come out blurred (P2).
2. `texture_quality=detailed` (the HD texture option); 4K.
3. A/B test `texture_alignment=geometry` against `original_image`.
4. A sharp, evenly lit input with no baked shadows.
5. A smear in a bad spot: re-roll `texture_seed`, or Magic Brush the patch in Tripo Studio.

Hunyuan3D (local, needs an NVIDIA GPU) textures the same way (multi-view paint), so expect the same blur. The pipeline below applies unchanged. Check its licence first: as remembered (unverified), the Tencent Hunyuan Community License excludes some territories (EU, UK, South Korea) and large services; read the licence file in the download.

## 1. Intake and diagnose (10 min)
**First, check that the export is complete** (proven 2026-10-29: two of the user's drops came out wrong, and the fix was a re-export, not a repair):
- **OBJ:** it needs `vt` lines, a `mtllib` + `usemtl`, and a `map_Kd` texture beside it. Count them: `cut -d' ' -f1 model.obj | sort | uniq -c`.
  - `v` lines with 6 numbers (x y z r g b), no `vt` and no `.mtl` mean a **vertex-colour-only export**. Colour lives only at the vertices, so walls come out as smooth gradients and parts meet at hard colour seams (the "PlayDoh" look). Ask the user to re-export with the texture on (OBJ + MTL + `_basecolor` JPG/PNG, or GLB), at 4K. Don't repair it.
- **FBX:** Tripo can hand over a **zip named `.fbx`**. `file model.fbx` says "Zip archive". Unzip it: the real FBX plus a `.fbm/` folder of loose 4K maps (basecolor, normal, roughness, metallic).
  - `sc_tripo_export_info.py` lists the parts, UV layers, colour attributes, vertex groups, armatures and images.
  - `sc_tripo_fbx2obj.py` makes the OBJ + MTL the repair tools read. It drops any rig: an OBJ holds none.
- **Rigs:** the user's rule: a Tripo rig doesn't work; ignore it or delete it, and never export one. Rigging is ours: the MPFB2 game rig and weight transfer (PHASE1 5).
- **Extra maps** (normal, roughness, metallic): URP Lit uses them, so keep them when they help the look. On the cottage Tripo's normal map held almost no cavity detail: judge it per model, and rebake our own normal and AO from a high where it is weak.
- **Preferred export: GLB** (I2). It's one file, it keeps the material and the separate parts, and its colour space is defined. Its texture equals the OBJ's. Convert GLB → FBX in Blender for Unity unless the project has added a glTF importer.
- **"Darker than Tripo's viewer" is not a defect** (I2). The raw texture always looks darker than the lit viewer. Compare with `blender -b --factory-startup --python Art/Blender/tools/experimental/sc_tripo_viewer_render.py -- <model.glb> <out.png> 1.0 0`, which matches the viewer. Never brighten the texture to match a screenshot. Judge the final look in Unity under the game's light.

Then put the raw export under `Art/Source/Tripo/<area>/<asset>/` (LFS), record its row in `Art/Source/Tripo/SOURCES.md` (sc-asset-adopt), and:
```
python3 -I Art/Blender/tools/experimental/sc_tripo_texel_analyze.py <model.obj> <basecolor>      # texel density, effective resolution
blender -b --factory-startup --python Art/Blender/tools/experimental/sc_tripo_texjudge_render.py -- <model.obj> <scratch>/orig --tag orig [--interp Closest|Linear]
```
- The render harness gives fixed views (full / win / wall / roof) with flat texture shading. Use the same views for every before/after. Its cameras are cottage-framed: add views for a new model, but keep them fixed across runs.
- **Read the numbers:**
  - Even density (P1: ~280–430 px/m at 5.2 m) together with "down ×2, up ×2 changes < 1 %" means the content is soft. Every model so far: 0.70–0.86 %.
  - **Check the per-part density spread, not just the median** (M1: the mill ran 62–305 px/m). A soft patch on a part under about 100 px/m is a UV-density defect: no step fixes it. Re-generate, or name it to the user.
  - Blur sits where the generator's views could not see. Halos sit within a few cm of another part (P2: unseen texels 68 % soft, square-on 8 %; M1: 57 % / 12 %, weaker on crowded models, AUC 0.68 vs 0.81).
  - Overlay parts (roof sheets 2–4 cm over a slab) hide a soft slab underneath. That's not a defect; leave it.
- **Anatomy to expect** (P3):
  - parts pushed into each other, hidden faces deleted;
  - Smart-UV islands with no overlap;
  - **lighting painted into the texture** (our albedo should carry no baked light; de-lighting is still open, see the end);
  - **arbitrary scale**: rescale by a known feature (a real door is about 1.95 m; ours are 2.2 m, PHASE1 3.4), not by the bounding box. The tools' 5.2 m normalisation is a working frame, not metres (cottage ×2.1, mill ×2.7 to real).
  - **Parts are not objects:** Tripo groups disconnected pieces into one part. Anything that must move (a waterwheel, a door, a gate) is split by loose piece, not by part (`sc_tripo_wheel_split.py`, M1). Do it before export, and keep parts unjoined through every step.

## 2. Repair the texture (the proven fix)
Run in this order. Each step has a before/after in the log.

| Step | Tool | What it does | Proven result |
|---|---|---|---|
| 1. Local repair (default) | `python3 Art/Blender/tools/experimental/tripo_repair/run.py <obj> <atlas> <workdir> --amin 0.1` → `<workdir>/out/basecolor.png` + `out/metrics.json`. About 8 min on 4 cores; deterministic; stages resume (`--force` reruns). See its README | Keeps the UV layout. Per 25° planar chart, in a level, metric image: holes = soft or ghosted texels; filled by guided quilting from the chart's own sharp texels; gates protect doors and frames | Smear and halos gone; **each wall keeps its own stones**; visible wall/roof soft 9.5 % → 2.9 % (P7). On the mill the default re-quilted a crisp visible wall: add `--extra "--nvg 1 --blob 0.10"`, which refills seen texels only inside smear-sized blobs ≥ 0.10 m² (M1) |
| 2. Re-texture what step 1 left soft | `python3 Art/Blender/tools/experimental/sc_tripo_retex.py <obj> <step-1 basecolor.png> <out> --only-soft --skip stone` (painted metal on the model: `SC_TIMBER_AMAX=12` and `--skip stone,slate`), **then always** `python3 Art/Blender/tools/experimental/sc_tripo_retex_writeback.py <out> <step-1 png> <final png> --skip-parts <round parts>`, which writes the changes back into the original UVs and keeps the parts. Calls `blender` from PATH | New UVs. On the left-over soft surfaces: low-pass of the paint × high-pass of a quilted class swatch, in plane space; timber grain along each part's long axis | Roof streaks calmed, posts/floor/crates get grain, timber soft 0.645 → 0.133 (P8); mill timber 0.586 → 0.096 (M1). Known breaks: round discs get a radial fan (skip them); dark glass gets timber streaks (skip the pane parts) |
| 3. Sharpen (optional) | `SC_SR_WEIGHTS=<dir> python3 Art/Blender/tools/experimental/tripo_sr/sr.py --model gen --pre 0.5 --tag rest --src <step-2 png> --out <dir>`, then `tripo_sr/mask_blend.py <step-2 png> <dir>/rest_4096.png <out.png>` | Real-ESRGAN general-x4v3 in restoration mode, blended only where the texture already has detail (local sd > 0.012) | Bricks and slates crisper; flats untouched (P5, P5b). About 3.5 min per 4K on 4 CPU cores. **Per model:** on the mill it turned dapple and grain waxy and was not kept (M1) |

- Step 1 skips charts with no sharp source in the same part ("no source", silently: 45 of 64 on the mill). Read `<workdir>/repair.log` before judging the result.
- Run step 3 on the output of step 2, so the mask sees the repaired texture. It needs torch (CPU) in a venv and the official weights (`SC_SR_WEIGHTS`). It is a download of outside code: ask the user first.
- **Never use an upscaler as the blur fix.** It can't invent missing content; it sharpens the edges of a smear (P5).
- Run `sc_tripo_retex.py` alone (no step 1, no `--only-soft`) only on a surface with no good texels of its own (P6). It replaces each wall's stone character with a class pattern.
- Leftovers no tool reaches yet: small soft parts with no sharp texels of their own (window rings, glass, rafters, a bench band). Use Magic Brush, a re-roll, or a manual per-part pick. Name them to the user rather than hiding them.

## 3. Import into Unity (URP Lit)
- The material is URP Lit with the repaired base colour, plus normal, metallic-smoothness and occlusion when they help (packed with `pack_unity.py`, port; sc-asset-craft 0b). Tripo's metallic and roughness are guesses: check them against the material (cloth and leather aren't metallic) and the review render.
- FishGame's importer cleaned every Tripo texture (Kuwahara smoothing, dark lift, dirty-brown desaturation) for its toon look. **Don't port that cleaning:** Kuwahara blobbed planks and brick edges (P9), and the lift and desaturation hit fur hard (C1). Grade colour toward PHASE1 3.1 by hand, per asset, and compare in Unity before and after.
- Import settings for 4K: max size 4096, high-quality compression, aniso 4. Don't change them to fix blur.
- **Final export:** the GLB plus the repaired maps, at real size, base on z 0, moving parts separated with their pivot on the axis, GLB and FBX, no armature. Tool: `blender -b --factory-startup --python Art/Blender/tools/experimental/sc_tripo_finalize.py -- <model.glb> <basecolor.png> <out dir> <stem> --config <model.json> [--uv <rwork>/mesh.npz <dwork>/out/UV.npy]`. The JSON config names the moving parts, the pivot axis and the real-scale factor; `tripo_detail/configs/mill.json` is the worked example.

## 2b. Bringing a building to the cottage bar (M2, M3: the mill)
When steps 1–2 leave a building short of the bar (big & visible soft about 0.03, every class about 0.03–0.05, clean by eye), run the plane-space detail pipeline. Paths are relative to `Art/Blender/tools/experimental/`:
1. `python3 tripo_repair/run.py <obj> <basecolor> <rwork> --until charts`
2. `python3 tripo_detail/run.py tripo_detail/configs/<model>.json <rwork> <basecolor> <dwork> --start <step-1/2 atlas> --wood-swatch <retex swatch_timber.png>`
3. `sc_tripo_finalize.py` (above).

What it does (config-driven; `configs/mill.json` is the worked example, and its README gives the stage order):
- per-class detail from the model's own sharpest clean texels (stone, wall, tin from clean stripes only, wood);
- re-lays low-density parts into free atlas space;
- restores the original texels wherever a pass hurt them (glass panes as their own smooth class, ivy, eave bands);
- fixes sliver UVs;
- for wheels and other round parts, a polar frame plus a straight frame per spoke, value-matched to the original paint, with contrast capped (mill: cap 0.13, gain 1.3).

Results on the mill: big & visible soft 0.185 → 0.048, every visible class under 0.05 except the smooth oak spokes, which look right as they are. Reproduces bit-identically (17 min). A new building needs its own families check: the values were tuned on one building.

## 2c. Creatures (C1, first one: the raccoon)
- **Intake:** expect stacked duplicate shells (the raccoon: 4 body shells, 44 % of tris never visible). Judge only texels a render camera sees.
- **Use the GLB's own embedded image as the atlas.** The GLB → OBJ conversion re-encodes the JPG and splits vertices at UV seams.
- **Step 1 needs creature mode:** relative softness, no ghost test (it eats dark markings such as tail stripes), keep the original's colour and take only fine detail, a fur pass with charts along each part's long axis, and eyes, nose and whiskers excluded by part.
  - Commands (raccoon values in `tripo_repair/examples/raccoon_cub/README.md`):
    1. `python3 tripo_repair/run.py <full.obj> <jpg> <w0> --creature --until visb`, then `TRIPO_WORK=<w0> python3 tripo_repair/hidden_parts.py --out hidden.txt` (the parts no view sees);
    2. `blender -b --factory-startup --python tripo_repair/sc_tripo_cull_hidden_parts.py -- <model.glb> <prep> --parts @hidden.txt --reuv <faceted part> --box <free atlas box> --density <body px/m>` (`--reuv` takes one part name);
    3. `python3 -I tripo_repair/sc_tripo_reuv_parts.py <prep>/reuv.npz <glb jpg> <prep>/atlas_reuv.png`;
    4. `python3 tripo_repair/run.py <prep>/model.obj <prep>/atlas_reuv.png <work> --creature --angle 40 --amin 0.02 --extra "<round-2 flags>"`.
  - Paths are relative to `Art/Blender/tools/experimental/`. Reproduces the accepted raccoon bit-identically (240 s).
  - Proven gain: visible soft 0.298 → 0.237, body 0.256 → 0.168.
- **Proven round-2 settings (C2):**
  - `--bfade 6`: no seam lines at chart borders;
  - `--tedge 0.035`: no patches at mask or ear edges;
  - `--relhp --srcdl 0.2 --darkok <leg parts> --zmin 0.45`: belly and leg fur, paw pads left painted;
  - result: overall 0.298 → 0.221, belly 0.476 → 0.320.
- **Faceted muzzle** (hundreds of one-triangle UV islands): weld it, re-UV it into freed atlas space at body density (not higher: it magnifies), resample its colour in 3D, and give it no fur fill. Clean beats fake detail; the softness metric rises, and that's fine.
- **Cull never-visible duplicate shells** when the fixed-view renders are unchanged: the raccoon lost 44 % of its tris and a pale poke-through at the tail base.
- **Skip step 2** (the architectural classes paint wood grain on faces) **and step 3** (the upscaler smudges fur).
- Keep or rebake a fur normal map if it reads in the review render; FishGame dropped the PBR maps only because its toon shader ignored them.

## 4. Review
`sc-asset-review` (audit, review renders with the reference row, rubric), and the before/after harness renders. Report fixes, breaks and keep to the user with the sheets.

## 5. Building it ourselves, the Tripo way (kit, K1–K2)
When we generate instead of adopt, copy Tripo's construction, not its primitives:
1. **Measure the AI part, then rebuild it as lofted rings:** `loft(rings, sides)`, with `kink` for stepped silhouettes. Rebuilt this way, chimney, barrel and dormer came out within 1.3× Tripo's tris.
2. **Deform every part:** `bend`, `twist`, `warp`, `taper`, `sag`. Nothing perfectly straight.
3. **Overlap parts into compounds, then cull buried faces:** `compound`, `cull_buried` (closed solids only).
4. **Use `cut` slices only where a deform needs vertices.** Round 1 cost 2–4× tris without this rule.
5. Let the bake and the material carry the fine detail (stone courses, shingles, planks).

The helpers are in FishGame's `fg_handmade.py` (port before use); see sc-asset-craft section 5.

## Troubleshooting (ask the user what they see, then match)
| Symptom | Likely cause | Check | Fix |
|---|---|---|---|
| Grey smear on a wall behind or under something | Generator never saw it (P2) | Harness render; `tripo_repair` visibility map | Step 1; at the source, multi-view input |
| Pale ghost or halo around frames, doors, porches | Projection halo painted into that part's own texels (P2/P3), **not** UV overlap | Distance-to-other-part map (`near.py`) | Step 1 (chart quilting with the chroma gate) |
| Whole roof or wall soft or streaky | Few good texels on that surface | Soft fraction per class | Step 2 (`--only-soft`); alone if the surface has no good texels |
| Crisp in Blender, soft or blobby in game | an import-time filter on the texture (FishGame's Kuwahara clean did this, P9), or a mip/aniso issue at grazing angles | Compare the imported texture with the source; check the import settings | No smoothing filter on import; 4K, aniso 4 |
| Contrasty but blurry (checker floors) | sd metric misses it | "Mush" metric (share of texels with no 1-px detail), P8 | Step 2 catches it |
| Bricks on a fascia, window glass or door | Class picked by colour or area, not geometry | Which part and face got repainted (`pick.py` maps a render pixel to its chart) | Geometry rules: 25° charts, feature vetoes (mean face area < 0.004 m², warm or pale) |
| Visible tiling or mirrored motifs | One exemplar | Swatch image | Several quilted exemplars, swatch ≥ 6 m, no mirroring |
| Soft patch that no step fixes | Part's texel density far below the rest (M1: 80 px/m against a 62–305 spread) | Per-part density from intake | Re-generate, or Magic Brush; tell the user |
| Wood grain on painted metal (tin, iron) | Timber class sees red-brown as "warm wood" (M1) | Lab a* of the region > 12 | `SC_TIMBER_AMAX=12`, `--skip stone,slate` |
| New bright seams, or a moving part fused, after step 2 | Step 2's new UVs joined everything (M1) | Seams at island borders; part count | Always run `sc_tripo_retex_writeback.py` |
| Step 1 changed a wall that was already crisp | Flat-faced stones read as tiny holes (M1) | Before/after diff on a visible wall | `--extra "--nvg 1 --blob 0.10"` |
| A part that should move is fused with static pieces | Tripo groups loose pieces per part (M1) | `sc_tripo_wheel_split.py` highlight render | Split by loose piece before export |
| Tail stripes or other dark markings recoloured or holed by step 1 | Ghost test reads dark markings as ghosts (C1) | Colour shift on marking parts | Creature mode (no ghost test) |
| Dark fur flagged as blur everywhere | Absolute softness thresholds (C1) | Soft fraction by relative contrast | Relative softness (creature mode) |
| Repair effort wasted, or the metrics look awful on a creature | Buried duplicate shells (C1: 60 of 117 parts never visible) | Render-visibility per part | Judge visible texels only; consider culling buried shells (saves 44 % tris on the raccoon) |
| Wood grain on a face or fur | sc_tripo_retex classes on a creature (C1) | — | Don't run step 2 on creatures |
| Export looks darker than in Tripo's viewer | Raw texture compared with a lit viewer (I2) | Render the GLB with `sc_tripo_viewer_render.py` | Nothing to fix; judge in Unity under game light |
| Model too small or large in game | Arbitrary AI scale (P3) | Measure a door (2.2 m here, PHASE1 3.4) or a hero's height (3.6) | Scale by a known feature in the finalize config |
| Shading looks doubled (dark under eaves even in sun) | Lighting painted into the texture (P3) | Down-facing faces 2–4× darker in the atlas | Open: no proven de-lighting step yet. Log any attempt |
| Our generated model looks like "PlayDoh tubes" | Straight, separate primitives | Compare to the Tripo part | Section 5 |
| Proportions drift from the target (toy-like, chibi, or plain realistic) | Generator default proportions | Measure heads-tall, hand and eye scale against PHASE1 3.6 | Re-generate from multi-view inputs that show the target; for heroes, the MPFB2 body sets proportion and the AI parts follow it |

## Open (log any attempt in the findings log)
- A second-model run, to confirm the thresholds generalise: class part area > 1 m², the |nz| bands, the veto colours, the window and patch sizes, `--amin`.
- De-lighting the painted shading.
- Small soft parts with no source of their own.
- An A/B of the Tripo source settings.
- A Hunyuan3D comparison on the user's PC.
- Promoting the tools out of `experimental/` once the second model passes.
