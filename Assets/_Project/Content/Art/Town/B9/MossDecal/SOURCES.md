# B9 MossDecal: sources and licence

Generator: `Art/Blender/town/sc_foliage_town.py` (build_decal_tex, moss_decal).

| piece | source | licence | what we changed |
|---|---|---|---|
| moss | ambientCG Moss002 https://ambientcg.com/view?id=Moss002 | CC0 1.0 | graded to #616e2e / #b6ab28, procedural climb mask (alpha) rising from the basin foot |

`SC_B9_MossDecal` (72 tris) is a mesh decal: a 70 deg band on the basin wall (radius 1.705 m, 0-0.38 m high) plus a 7 cm ground lip. Origin = the basin centre; the band is centred on local -Y (Unity +z). Rotate it about Y to the side you want, where a camera sees it (all three capture cameras look at the basin's south face).

## Unity import (controller)
- Model: Normals = Import (bent card normals are authored), Scale 1.
- Material: URP Lit. `_BaseColor` (sRGB, alpha), `_Normal` (Normal map, OpenGL / Y+), `_ORM` (linear: R occlusion, G roughness, B metallic; smoothness = 1 - G, or pack with `Art/Blender/tools/sc_pack_unity.py --orm`).
- Material `M_B9_MossDecal`: Alpha Clipping 0.5; Render Face Front.
