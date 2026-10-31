# CellarLantern (B11 hero prop) - sources

Generated, not hand-edited. Rebuild on the PC (Blender 5.2):

```
AAA_SCANS=C:/Dev/_private/textures_src/cands \
blender -b --factory-startup --python-exit-code 1 --python Art/Blender/cellars/sc_cellar_lantern.py -- \
  --out <scratch> --stage bake --res 4096 --export Assets/_Project/Content/Art/Cellar/B11/CellarLantern
```

| file | what |
|---|---|
| CellarLantern.fbx | low, 44,986 tris (high 1.40M), one UV set, material M_CellarLantern; pivot bottom centre; -Z forward, Y up, metres, scale 1 |
| CellarLantern_BaseColor.png | 4096, sRGB |
| CellarLantern_Normal.png | 4096, tangent space (OpenGL, Y+), linear |
| CellarLantern_ORM.png | 4096, linear: R occlusion, G roughness, B metallic |
| CellarLantern_Emission.png | 4096, sRGB: vent and wax glow (the Blender game material scales it x6) |

Glass panes and the flame are not in this FBX: in Unity the glass is a transparent material and the flame a particle or emissive card, with a point light at (0, 0.147, 0) m (Blender z 0.147).

## Third-party inputs

| asset | url | licence | author | used for |
|---|---|---|---|---|
| rusty_metal_02 (diff, rough, disp 2k) | https://polyhaven.com/a/rusty_metal_02 | CC0 1.0 (polyhaven.com/license) | Rob Tuytel | detail layer in the iron smart material (colour overlay, roughness offset, bump), baked into the maps |

Everything else is procedural (Art/Blender/tools/sc_aaa_kit.py, sc_aaa_mats2.py, sc_aaa_cuts.py).
