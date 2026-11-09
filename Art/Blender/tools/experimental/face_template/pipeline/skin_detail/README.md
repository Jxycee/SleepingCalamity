# skin_detail: procedural skin micro-detail layer for the face pipeline (prototype)

The baked photo albedo (`bake/Head_BaseColor_char.png`) is smooth ("rubbery"), the build uses `Head_Normal.png` (nearly flat) and a constant roughness 0.5.
This folder adds pores, a fine furrow network, micro-undulation, roughness variation and faint colour mottling, only on skin.

## Use
```
PY=C:/AI/trellis2/venv/Scripts/python.exe     # torch cu128, numpy, scipy, cv2, skimage, PIL
$PY apply_skin_detail.py <run_dir> [--strength 1.5] [--pore-mm 0.8] [--line-mm 1.6] [--normal 1] [--albedo 1] [--rough 1] [--mottle 0.25] [--seed 1] [--out skin_detail]
$PY render_compare.py <run_dir> [--closeup-scale 0.6] [--samples 64]     # Blender Cycles OPTIX before/after sheet
```
Reads only: `bake/Head_BaseColor_char.png`, `bake/texture.json`, `fit/tpl_hits.json`, the template UV/mesh (through the pipeline's `c6_common`; `uvgeom.py` copies the mask block of `c6_02_texture.py` instead of importing it, since that file runs a whole stage at import). Never edits pipeline files or the run's blend. About 10 s per run on the GPU.

Writes `<run>/out/skin_detail/`: `albedo_detail.png`, `normal_detail.png` (Tobin normal + detail, whiteout blend, OpenGL +Y), `roughness_detail.png` (0.5 + modulation, Non-Color),
`layer_normal.png` and `layer_albedo_delta.png` (detail only, for a shader-side stack), `skin_mask.png`, `report.json` (dE check, mm per texel), `preview_uv.png`, and from render_compare `renders/` + `<name>_skin_detail_sheet.png` (before | after | difference x6).

## How it works
- Geometry: `uvgeom.py` rasterises the head UV at 2048, gets per-texel 3D position (m) and mm per texel along u and v (median 0.22-0.25 mm/texel over the four runs).
- Noise is a function of 3D position in mm, so scale is physical, there is no stretching and no seam at UV island borders. `tile_mm` makes every layer periodic (12.8 mm tile with default cell sizes) for a future shader-side detail tile.
- Layers (heights in mm): pores (Worley F1 pits, 0.8 mm spacing, 55% of cells active, depth 0.04 mm), furrow network (Worley F2-F1 at 1.6 and 0.8 mm, 0.015 mm), fBm 3.2 mm (0.012 mm), mottling (6.4 mm).
- Normal: height gradient divided by the local mm per texel (so stretched UV does not change strength), tangent space OpenGL, whiteout-blended onto Head_Normal. Roughness: pores/furrows rougher, mean forced to 0.5. Albedo: Lab delta (L mostly, small a/b), high-pass only.
- Mask (`skin_mask.png`): coverage minus eye sockets and eyelid rings, lips and mouth interior, nostrils, ears (feather by distance transform, 3-14 px), brows (found as dark albedo texels above the eyes, since the template has no brow set), and a fade at UV island borders. Scalp is treated as skin; hair is a separate asset not in the render.
- Colour: the local 31 px median of each detail channel is subtracted, then a smooth anchor correction (4 iterations) cancels the measured shift at the pipeline's three dE patches. Verified with the pipeline's own measure (median Lab of a 29x29 patch at cheek R, cheek L, forehead; c6_02 `tex_patch_lab`, r6_10 mean dE76 vs `photo_fit_mean_Lab`):

| run | baseline mean dE76 | detailed | delta |
|---|---|---|---|
| char2_zero | 1.4869 | 1.4734 | -0.0135 |
| char3b | 1.3130 | 1.3130 | 0.000 |
| char4 | 1.3453 | 1.3377 | -0.0076 |
| cloud_v1 | 1.7205 | 1.7205 | 0.000 |

(all within +-0.05; exact values in each `report.json`; masked mean Lab shift about -0.07 L, 0 a/b.)

## Limits (be honest)
- Resolution: at 0.22-0.25 mm/texel the 2K UV cannot hold 0.2-0.4 mm pores (1-2 texels, aliasing). The shipped scale is 0.8 mm pores / 1.6 mm furrows (cheek-realistic, 3-4 texels). True-scale micro-relief needs a shader-side tiled detail normal (use `layer`-style tile from `tile_mm`, UV tiling about 12.8 mm per repeat), or a 4K head bake.
- The effect is visible in macro closeups under raking light (sheets rows cheek/nose/forehead) and essentially invisible at full-head distance. It reads as skin pores and fine creases, not noise or dirt, at strength 1.5; at 2.0 the furrow network turns cobbled/reptilian on cheeks.
- It cannot fix a posterised or blotchy albedo (char4 has flat banded regions; the layer rides on top of them).
- The brow mask is a dark-texel heuristic: on char4 (wrinkles, stubble) it removes more skin than needed around the nose bridge and under the eyes. A real brow/hairline set in the template would be better.
- Roughness modulation is untuned against a reference; SSS weight stays 0.04 (build default).

## Plugging into the pipeline (not done here)
Add a final stage after `c6_02_texture` + before/inside `c6_04_build` (or as a post-build swap): run `apply_skin_detail.main([run_dir])`, then in `skin_mat()` of `c6_04_build.py` point Base Color at `albedo_detail.png`, the Normal Map node at `normal_detail.png`, and add an Image Texture (Non-Color) `roughness_detail.png` into Principled Roughness (what `blender_render.py` does). Add `skin_detail.delta_mean_dE76` (|x| <= 0.05) to `score.json`/regression and keep the detail PNGs next to the bake. Cached `_geom.npz` is per run, in `out/skin_detail/`.
