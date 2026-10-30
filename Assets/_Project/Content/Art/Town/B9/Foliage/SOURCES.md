# B9 Foliage: sources and licence

Generator: `Art/Blender/town/sc_foliage_town.py` (shared helpers `sc_b9_lib.py`), run with `--stage all --export` on the PC.

| piece | source | licence | what we changed |
|---|---|---|---|
| ivy leaves (atlas u 0-.5, v 0-.5) | ambientCG LeafSet017 https://ambientcg.com/view?id=LeafSet017 | CC0 1.0 | 6 scanned leaves composited into 3 runner cells + 4 singles, veins damped, graded to #64822f (+18% chroma), drop shadows baked into AO |
| nettle leaves (u 0-.5, v .5-1) | Poly Haven nettle_plant https://polyhaven.com/a/nettle_plant (Rob Tuytel, Rico Cilliers) | CC0 1.0 | meshes combined into clumps, decimated to 380 tris, scaled up (storybook); alpha = UV coverage minus the black padding; graded to #6f8a34 (+18% chroma) |
| weed rosettes (u .5-1, v .5-1) | Poly Haven weed_plant_02 https://polyhaven.com/a/weed_plant_02 (Rob Tuytel, Rico Cilliers) | CC0 1.0 | as nettle |
| moss tile / fringe | ambientCG Moss002 https://ambientcg.com/view?id=Moss002 | CC0 1.0 | graded to #616e2e with #b6ab28 sunlit patches; fringe cell gets a ragged alpha edge |
| grass blades, ivy bark | procedural (ours) | - | - |

Poly Haven moss_01 (246k tris) was not needed: the tufts are built from Moss002.

Pieces (Blender z up, front -Y = Unity +z; origin at the base centre unless noted):
- `SC_B9_IvyWall` (4.4k tris): 2.1 x 5 m mass (tendrils reach about 2.8 m wide and 1.1 m off the wall). Origin = the facade's front-left corner at ground level; the mass grows toward local +x. Unity: position (-5, 0, 7.5), rotation Y 180 (faces -Z like the facade); flip X scale if the corner is on the other side. Assumes the jetty underside at 3.1 m (beam 0.28 m, jettied 0.45 m); rebuild with `--jetty <m>` once B4 lands.
- `SC_B9_IvyPatchA` 1.0 x 1.4 m, `SC_B9_IvyPatchB` 0.7 x 0.6 m (flat wall; origin bottom-left on the wall face).
- `SC_B9_CrackWeed_A..E` (380 tris each): cobble joints, the facade foot, the fountain ring.
- `SC_B9_MossTuft_A..C` (132 tris): the basin foot and flagstone joints.
- `SC_B9_GrassClump_A..C` (128-224 tris): the facade corner, stall legs.
- All use one material `M_B9_Foliage` with `T_B9_Foliage_*`.

## Unity import (controller)
- Model: Normals = Import (bent card normals are authored), Scale 1.
- Material: URP Lit. `_BaseColor` (sRGB, alpha), `_Normal` (Normal map, OpenGL / Y+), `_ORM` (linear: R occlusion, G roughness, B metallic; smoothness = 1 - G, or pack with `Art/Blender/tools/sc_pack_unity.py --orm`).
- Foliage material: Alpha Clipping on, threshold 0.5, Render Face Both. BaseColor texture: **Mip Maps Preserve Coverage on** (alpha ref 0.5), or the cutout foliage thins out at the wide camera.
- Vertex colour `Col` holds a card AO (dark core to lit rim) for a future foliage/wind shader; URP Lit ignores it.
- Shadows: the ivy and weeds cast; the grass and moss tufts can skip casting.
