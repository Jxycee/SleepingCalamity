<!-- Covers: export completeness checks (OBJ/FBX/GLB, rigs, extra maps), texel analysis, how to read the numbers, and the anatomy of an AI export. Moved from SKILL.md section 1. -->

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
