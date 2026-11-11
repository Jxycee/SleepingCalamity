---
name: sc-ai-model-pipeline
description: End-to-end pipeline for AI-generated 3D models (Tripo, Hunyuan3D) in Sleeping Calamity - generation settings for the locked style and the PHASE1 character proportions, intake and diagnosis, the proven blur/halo texture repair, optional masked AI upscale, URP Lit import, review - plus a symptom-first troubleshooting guide for texture and shape problems. Use when a user brings a Tripo/Hunyuan export (a hero's head, hair or outfit, an organic enemy, a building part), says an AI model's texture is blurry, smeared, ghosted or haloed, asks why it looks soft or "PlayDoh" in game, or wants our own generators to build shapes the way Tripo does.
---

# AI model pipeline (generate → diagnose → repair → import → review)

> **Status (2026-11-11):** the scripts this skill names under `Art/Blender/tools/experimental/` and `Art/Source/Tripo/` are not in this repo. The working local pipeline is Hunyuan3D shape + TRELLIS.2 texture, one command `python C:\AI\scripts\ai_prop.py` (`docs/research/2026-11-06-sam3d-trellis2-combined.md`). Use this skill's diagnosis and repair advice; port a script before relying on it.

Evidence for every rule here: FishGame's `docs/superpowers/research/2026-10-28-ai-model-pipeline-findings.md` (entries P1–P9, K1–K2, M1–M3, C1–C2; read it in `C:\Dev\FishGame`). Proven on a few models so far: the user's Tripo "Dark Cloud" cottage, a mill, a raccoon. **Run it on another model before you trust the per-model numbers.**

**The tools are FishGame only, port before use:** `C:\Dev\FishGame\Art\Blender\tools\experimental\` (`fg_tripo_*.py`, `tripo_repair/`, `tripo_detail/`, `tripo_sr/`). Copy them into `Art/Blender/tools/experimental/` with the `sc_` prefix (CLAUDE.md: copy, never edit FishGame). The commands below use the ported names.

## Style and proportion targets (PHASE1 3.6 and section 5)
- **The look:** stylised realism. Real PBR material at high detail (skin, stitched leather, woven cloth, fur, metal), slightly stylised form; storybook, not chibi, not toy (sc-asset-craft section 3).
- **Hero proportions:** about 6.5 heads tall; hands and feet about 1.15x real with chunky boots, gauntlets and belts; eyes about 1.1x; faces realistic in material. Heights: Tobin 1.90 m, Ash 1.75 m, Wren 1.68 m, Pip 1.58 m. Each hero reads at 30 m from silhouette alone.
- **Heroes are a hybrid** (PHASE1 5, [P29]): an MPFB2 body for topology, rig and weights (licence: sc-asset-adopt), with the **head, hair and outfit generated as separate parts** from multi-view concept images, retopologised and cleaned here and with `texture-bleed-cleanup`, then skinned by weight transfer. Outfits stay separate meshes on one skeleton. If a part fails cleanup twice, model it in Blender over the body instead.
- **Budgets:** PHASE1 3.3 (hero 60k tris, 1 x 2K body and outfit, 1 x 2K head and hair; enemy 25k, one 2K set; a whole building at most 75k). Nothing bakes above 2048 px.
- **Inputs** should show the target directly: chunky storybook proportion, clean colour in the PHASE1 3.1 palette, evenly lit with no baked shadows.

House rules for this pipeline:
- **Metrics serve the look.** A lower soft fraction that reads darker, busier or noisier is a break (M2: the mill's spokes). Value-match any added detail to the original paint's local mean, and cap its contrast at the part's own crisp areas.
- **Do what makes the asset look best for what it is** (user, 2026-10-29). Every step, setting and import option is chosen per asset by how the result looks, not by the default. A building, a creature and a prop each get what suits them: skip, change or add steps, and say why in the log. When two options trade off, take the better-looking result, and log the other as fixes / breaks.
- Measure before you fix. Every fix is logged as **Fixes / Breaks / Keep** in the findings log, with a render pair. A reverted fix still counts for what it fixed (user, 2026-10-28).
- Fix in 3D and by geometry (part, face, plane), **never by blurring in UV space**: UV-space blur leaves seam wedges at island borders (FishGame's character cleanup).
- Reference images are study material. Fill only from the model's own sharp texels.

## 0. Generate (source settings, ranked)
Best first: multi-view input (front/back/left/right, same light and scale), `texture_quality=detailed` at the highest source resolution Tripo offers (imported at 2048), a sharp evenly lit input with no baked shadows. Hunyuan3D textures the same way, so the pipeline below applies unchanged; **check its licence file first** (territory and service limits).
- `references/generate.md` - read before generating a Tripo/Hunyuan model: the ranked settings, the alignment A/B and the Hunyuan licence note.

## 1. Intake and diagnose (10 min)
Check the export is complete before anything else: an OBJ needs `vt`, `mtllib`/`usemtl` and a `map_Kd` texture (a vertex-colour-only export is re-exported by the user, never repaired); a Tripo ".fbx" may be a zip; **a Tripo rig is never used or exported** (rigging is ours: MPFB2 plus weight transfer). Prefer GLB. "Darker than Tripo's viewer" is not a defect. Never brighten a texture to match a screenshot.

Put the raw export under `Art/Source/Tripo/<area>/<asset>/` (LFS), record its row in `Art/Source/Tripo/SOURCES.md` (sc-asset-adopt), then:
```
python3 -I Art/Blender/tools/experimental/sc_tripo_texel_analyze.py <model.obj> <basecolor>      # texel density, effective resolution
blender -b --factory-startup --python Art/Blender/tools/experimental/sc_tripo_texjudge_render.py -- <model.obj> <scratch>/orig --tag orig [--interp Closest|Linear]
```
Use the same fixed views for every before/after. A soft patch on a part under about 100 px/m is a UV-density defect no step fixes: re-generate or name it to the user.
- `references/intake-and-diagnose.md` - read for export checks (OBJ/FBX/GLB, extra maps), how to read the density numbers and the anatomy of an AI export (scale, baked light, parts that must move).

## 2. Repair the texture (the proven fix)
Run in order, each with a before/after in the log: (1) local repair `tripo_repair/run.py` (default; keeps the UV layout); (2) re-texture what is left soft with `sc_tripo_retex.py --only-soft`, **then always** `sc_tripo_retex_writeback.py`; (3) optional masked AI sharpen. **Never use an upscaler as the blur fix.** Step 3 downloads outside code: ask the user first. Read `<workdir>/repair.log` before judging step 1. Name leftovers no tool reaches to the user rather than hiding them.
- `references/repair-steps.md` - read before running any repair step: the full table with commands, flags, proven results and known breaks.

## 3. Import into Unity (URP Lit)
URP Lit with the repaired base colour, plus normal, metallic-smoothness and occlusion when they help (`sc_pack_unity.py`). **Don't port FishGame's import cleaning** (Kuwahara, dark lift, desaturation); grade toward PHASE1 3.1 by hand per asset. Import: max size 2048 (the project texture cap; 4K source maps are imported at 2048), high-quality compression, aniso 4; don't change these to fix blur. Final export: GLB plus repaired maps, real size, base on z 0, moving parts separated, no armature.
- `references/import-and-export.md` - read for the final-export command and config.

## 2b. Bringing a building to the cottage bar (M2, M3: the mill)
- `references/building-detail.md` - read when steps 1-2 leave a building's big and visible soft fraction above about 0.03.

## 2c. Creatures (C1, first one: the raccoon)
- `references/creatures.md` - read for any creature or fur model: creature mode, duplicate shells, skip steps 2 and 3.

## 4. Review
`sc-asset-review` (audit, review renders with the reference row, rubric), and the before/after harness renders. Report fixes, breaks and keep to the user with the sheets.

## 5. Building it ourselves, the Tripo way (kit, K1-K2)
- `references/tripo-kit.md` - read when we generate a shape instead of adopting one (lofted rings, deforms, compounds). FishGame's `fg_handmade.py` helpers are not ported; see sc-asset-craft section 5.

## Troubleshooting (ask the user what they see, then match)
- `references/troubleshooting.md` - read when an AI texture looks smeared, haloed, soft, tiled or in the wrong colour, a moving part is fused, the model is the wrong scale, or an import looks different from Blender.

## Open (log any attempt in the findings log)
- A second-model run, to confirm the thresholds generalise: class part area > 1 m², the |nz| bands, the veto colours, the window and patch sizes, `--amin`.
- De-lighting the painted shading.
- Small soft parts with no source of their own.
- An A/B of the Tripo source settings.
- A Hunyuan3D comparison on the user's PC.
- Promoting the tools out of `experimental/` once the second model passes.

## References
- `references/intake-and-diagnose.md`, `references/repair-steps.md`, `references/import-and-export.md` - the numbered steps in detail.
- `references/building-detail.md`, `references/creatures.md`, `references/tripo-kit.md` - per-family recipes.
- `references/troubleshooting.md` - symptom, cause, check, fix table.
