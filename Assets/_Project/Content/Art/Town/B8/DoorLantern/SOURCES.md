# B8 DoorLantern: sources and licence

Generator: `Art/Blender/town/sc_lanterns_adopt.py` (`--res 2048 --export`).

| piece | source | licence | what we changed |
|---|---|---|---|
| lantern frame, globe | Poly Haven Lantern_01 https://polyhaven.com/a/Lantern_01 (Rajil Jose Macatangay) | CC0 1.0 | brass regraded to blackened wrought iron with worn-bright edges; decimated 30.8k -> 4.2k (globe 3k -> 0.5k); thickened 1.2 mm; scaled from 0.29 m to 0.52 m (real wall-lantern size + 10%); re-baked onto a fresh UV set with soot by height and warm amber glass |
| wall bracket (plate, arm with end curl, scrolled brace, hook, bolts) | procedural (ours) | - | - |

`SC_B8_DoorLantern` (5.8k tris): origin = the back of the wall plate at its centre; y 0 is the wall face and the arm points local -Y (Unity +z): rotate Y 180 on the facade, about 2.35 m up beside the door. Burner (point light, about 2700 K): 0.43 m out from the wall, 0.31 m below the origin.

## Unity import (controller)
- Model: Normals = Import (bent card normals are authored), Scale 1.
- Material: URP Lit. `_BaseColor` (sRGB, alpha), `_Normal` (Normal map, OpenGL / Y+), `_ORM` (linear: R occlusion, G roughness, B metallic; smoothness = 1 - G, or pack with `Art/Blender/tools/sc_pack_unity.py --orm`).
- Two slots share the set: `M_SC_B8_DoorLantern` (Opaque, no alpha clipping: the _BaseColor alpha is the glass opacity and would punch holes along UV seams at distance) and `M_SC_B8_DoorLantern_Glass` (Surface Transparent, alpha from `_BaseColor`).
