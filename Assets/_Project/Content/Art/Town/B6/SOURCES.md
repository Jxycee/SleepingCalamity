# B6 plaza ground: sources

All B6 assets are fully procedural: no scans, no external models, so no external licence applies.
Generators: `Art/Blender/town/sc_cobbles.py`, `Art/Blender/town/sc_flag_ring.py`, shared helpers `Art/Blender/town/sc_plaza_common.py` and `Art/Blender/tools/sc_aaa_kit.py`.
PC bake 2026-10-30, Blender 5.2.2. Maps: `_BaseColor` sRGB, `_Normal` tangent space (OpenGL Y+), `_ORM` R occlusion, G roughness, B metallic (0: no metal),
packed with `python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/tex <Prefix> <folder>`. FBX: -Z forward, Y up, metres, transforms baked.

## Cobbles (`Cobbles/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cobbles.py -- --out <scratch> --stage bake --res 2048 --export`.
- `CobbleFamily.fbx`: 11 sett/rubble variants (sett_a-f, path_a-b, small_a with `_LOD0` about 140 tris and `_LOD1` about 43 tris; frag_a-b LOD0 only, about 120 tris), setts pivoted at their top centre and frags at their base, as the placements expect, sharing material M_Cobble -> `Cobble_BaseColor/_Normal/_ORM.png` (2K family atlas).
- `PlazaBed.fbx`: the joint bed under the setts (9,820 tris, x -8..8, z -6..8 Unity), UV0 in 2 m tiles of the `CobbleBed_BaseColor/_Normal/_ORM.png` tileable (2K), plus the linear corner colour attribute `damp` (R damp/moss, G worn-path dust).
- `cobble_placements.json`: every placed stone (6,732 setts + 45 loose; variant, lod, Unity position, quaternion, scale, `seen`) and 121 weed slots for B9. Instanced total 635,937 tris.

## FlagRing (`FlagRing/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_flag_ring.py -- --out <scratch> --stage bake --res 4096 --export` (4K per the generator header: the ring's 17 m2 at 1024 px/m).
- `FlagRing.fbx`: one mesh, 11,972 tris, pivot at the fountain centre, ring r 1.7-2.9 m; `FlagRing_BaseColor/_Normal/_ORM.png` 4096 px.
