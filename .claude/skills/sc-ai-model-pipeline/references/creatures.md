<!-- Covers: repair settings and commands for creature models (the raccoon). Moved from SKILL.md section 2c. -->

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
