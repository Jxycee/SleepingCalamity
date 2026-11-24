# AetherStrand: any hairstyle onto Tobin (pipeline and Neon task)

Status 2026-11-23. Everything below is measured in `STATUS.md`. The web app is the source of truth; Unity reproduces it.

## The pipeline (all steps scripted)
1. **Recipe.** A web "recipe" JSON (schema 2.0: `presetId`, `builder` sliders + `archetype` + `seed`, `shader`, `physics`). The web UI's Copy JSON button emits it. 12 archetypes ship with the web app (`ALL_ARCHETYPES` in `hairBuilder.ts`); `tools/gen_recipes.ts` writes a recipe + geometry for each.
2. **Geometry.** `tools/web_export.ts <recipe> <outDir>` (run with cwd = AetherStrand clone, after applying `tools/aetherstrand_web_clone.patch`) writes `PresetGeometry.json`: per-lock rest points, width, thickness, seed, layerAO, zone, `scalpRoot`, `lockNormal`, `rootNormal`. It mirrors the web viewport's own load path (`loadCustomBuilder` + `bakeCollisionFreeRestPose`).
3. **Unity import.** `AetherStrand.Editor.AetherStrandWebImport.Apply(character, geometryJson, recipeJson)` (menu: Tools > AetherStrand > Apply Web Export To Selected Character). It fits the hair to the character's head (`AetherStrandHeadFit`: per-axis scale + offset from head-bone-space landmarks), builds the mesh (exact port of the web ring construction), the material (exact port of the web fragment shader, URP) and physics.
4. **Verify.** `tools/ParityRender.cs` (web bust + web light rig + web cameras, tone-mapped with three's ACES) and `tools/TobinFitTest.cs` (same on Tobin); `tools/cmp.py`, `cmp_batch.py` give overlap and error against web captures. Web captures: add `&nobust=1&sim=rest&cfg=<base64 recipe>` to `/?capture=1` (patch adds `cfg` and `nobust`).

## Measured today (Half-Up Cascade #059 and the 12 archetypes)
- Mesh: vertex-exact vs web (0.008 mm mean per-lock max error, 248-lock ponytail style).
- Hair-only render vs web, 12 archetypes (rest pose, web display transform applied in the shader, real 4x MSAA, web label boxes excluded): mean error inside the hair 0.27/255 (max 0.36), side bias at most 0.11, 0.94% of pixels differ by more than 10/255 raw and 0.003% after allowing a 1 px alignment (sub-pixel edge sampling). Earlier, larger numbers came from resolving MSAA in linear HDR before tone mapping and from counting the web's label boxes.
- Fit on Tobin: all 12 styles seat on the skull; no floating crown.
- Motion: web solver port in progress (branch `claude/aetherstrand-physics`); see `PHYSICS_PORT.md` when it lands.

## Bugs found on the way (for the plugin owner)
1. Loader mirrored X (Blender to Unity mapping had the wrong handedness).
2. Loader rescaled by `0.23/maxZ`, so tall styles shrank more; replaced by the head fit.
3. Triangle winding must be reversed when X is negated (Unity treats clockwise as front), otherwise lock normals invert and the hair renders at about half brightness.
4. Shipped shader was an older, simplified variant (one light, other constants); replaced by an exact port of the web fragment shader.
5. `AetherStrandAiDiagnostics.cs` empty in v3.0 (fixed in v3.0.1).
6. `AetherStrand_Normal.png.meta` had `textureType: 1` (Normal Map): Unity repacked the channels (R about 1.0 everywhere), so the shader's raw `rgb*2-1` decode pushed every groove normal one way, causing about 3/255 shading error and opposite-sign left/right bias. Set to Default (raw).
7. Headless test gotcha: a render pipeline asset assigned in the same call does not apply until the next frame; everything rendered with the built-in pipeline.

## Neon task (AI hairstyle authoring), proposed
Goal: given a reference image or description, produce a recipe that, run through steps 2-3, looks like the reference on Tobin.
- Input to Neon: the recipe schema (this folder `recipes/halfup059.json`), the 12 archetype ids, slider ranges (`HairBuilderConfig` in `src/types/hair.ts`), palette list (`CURATED_HAIR_PALETTES`).
- Loop: propose recipe -> `gen_recipes`/`web_export` -> `TobinFitTest` render -> compare against the reference image (Neon judges; sessions only provide renders and numbers) -> adjust sliders. Archetype is the coarse choice, sliders the fine ones.
- For shapes outside the 12 archetypes: the Blender addon's `aetherstrand.curves_to_locks` converts hand-drawn guide curves into locks; export them in the same `PresetGeometry.json` format and the rest of the pipeline is unchanged.
- Keep the web app unchanged; our fork lives in `Packages/com.aetherstrand.sculptedhair` (Neon can fold it into its zip source).
