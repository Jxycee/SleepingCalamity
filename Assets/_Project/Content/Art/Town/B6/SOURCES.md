# B6 plaza ground: sources

All B6 assets are fully procedural: no scans, no external models, so no external licence applies.
Generators: `Art/Blender/town/sc_cobbles.py`, `Art/Blender/town/sc_flag_ring.py`, shared helpers `Art/Blender/town/sc_plaza_common.py` and `Art/Blender/tools/sc_aaa_kit.py`.
PC bake 2026-10-30, Blender 5.2.2. Maps: `_BaseColor` sRGB, `_Normal` tangent space (OpenGL Y+), `_ORM` R occlusion, G roughness, B metallic (0: no metal),
packed with `python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/tex <Prefix> <folder>`. FBX: -Z forward, Y up, metres, transforms baked.

## Cobbles (`Cobbles/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cobbles.py -- --out <scratch> --stage bake --res 2048 --export`.
- Round 2 PC bake 2026-10-30 (cobbles round 9: stronger per-stone value, darker joints, more relief, 3 damp setts, patch extended to x -12).
- `CobbleFamily.fbx`: 14 sett/rubble variants (sett_a-f, path_a-b, damp_a-c, small_a with `_LOD0` about 140 tris and `_LOD1` about 44 tris; frag_a-b LOD0 only, about 120 tris), setts pivoted at their top centre and frags at their base, as the placements expect, sharing material M_Cobble -> `Cobble_BaseColor/_Normal/_ORM.png` (2K family atlas).
- `PlazaBed.fbx`: the joint bed under the setts (12,620 tris, x -12..8, z -6..8 Unity), UV0 in 2 m tiles of the `CobbleBed_BaseColor/_Normal/_ORM.png` tileable (2K; its AO is flat white by design), plus the linear corner colour attribute `damp` (R damp/moss, G worn-path dust).
- `cobble_placements.json`: every placed stone (8,673 setts + 56 loose, 7,587 inside a capture frame; variant, lod, Unity position, quaternion, scale, `seen`) and 149 weed slots for B9. Instanced total 748,725 tris.

## FlagRing (`FlagRing/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_flag_ring.py -- --out <scratch> --stage bake --res 4096 --export` (4K per the generator header: the ring's 17 m2 at 1024 px/m).
- `FlagRing.fbx`: one mesh, 11,972 tris, pivot at the fountain centre, ring r 1.7-2.9 m; `FlagRing_BaseColor/_Normal/_ORM.png` 4096 px.
- `RingBed.fbx`: the dirt bed under the flags (768 tris, r 1.68-2.96 m, 8 cm down), so the ring's 2 cm joints read dark: PlazaBed stops at r 2.8. It uses the same M_CobbleBed tileable as PlazaBed (`../Cobbles/CobbleBed_*`) and the linear `damp` attribute.
