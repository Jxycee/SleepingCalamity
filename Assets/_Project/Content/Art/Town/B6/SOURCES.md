# B6 plaza ground: sources

All B6 assets are fully procedural: no scans, no external models, so no external licence applies.
Generators: `Art/Blender/town/sc_cobbles.py`, `Art/Blender/town/sc_flag_ring.py`, shared helpers `Art/Blender/town/sc_plaza_common.py` and `Art/Blender/tools/sc_aaa_kit.py`.
PC bake 2026-10-30, Blender 5.2.2. Maps: `_BaseColor` sRGB, `_Normal` tangent space (OpenGL Y+), `_ORM` R occlusion, G roughness, B metallic (0: no metal),
packed with `python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/tex <Prefix> <folder>`. FBX: -Z forward, Y up, metres, transforms baked.

## Cobbles (`Cobbles/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cobbles.py -- --out <scratch> --stage bake --res 2048 --export`.
- Round 3 PC bake 2026-10-30 (cobbles round 12: salt-and-pepper granite grain, flush laid surface, open edges fade over ragged bands incl. the north edge, bed rises flush with the surround with vertex alpha = fade).
- `CobbleFamily.fbx`: 14 sett/rubble variants (sett_a-f, path_a-b, damp_a-c, small_a with `_LOD0` about 140 tris and `_LOD1` about 44 tris; frag_a-b LOD0 only, about 120 tris), setts pivoted at their top centre and frags at their base, as the placements expect, sharing material M_Cobble -> `Cobble_BaseColor/_Normal/_ORM.png` (2K family atlas).
- `PlazaBed.fbx`: the joint bed under the setts (12,620 tris, x -12..8, z -6..8 Unity), UV0 in 2 m tiles of the `CobbleBed_BaseColor/_Normal/_ORM.png` tileable (2K; its AO is flat white by design), plus the linear corner colour attribute `damp` (R damp/moss, G worn-path dust, A perimeter fade into the surround).
- `cobble_placements.json`: every placed stone (6,919 setts + 48 loose, 6,476 inside a capture frame; variant, lod, Unity position, quaternion, scale, `seen`) and 382 weed slots for B9 (incl. `edge_grass` on the fading edges). Instanced total 611,152 tris.

## FlagRing (`FlagRing/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_flag_ring.py -- --out <scratch> --stage bake --res 4096 --export`, then the packed maps downsampled to 2048 (Lanczos; normals filtered as vectors and renormalised). **Texture cap 2048 px (user, 2026-10-30: the game targets 1440p).** Future bakes: `--res 2048`. Round 3 (2026-10-30) re-bake: unchanged geometry.
- `FlagRing.fbx`: one mesh, 11,972 tris, pivot at the fountain centre, ring r 1.7-2.9 m; `FlagRing_BaseColor/_Normal/_ORM.png` 2048 px (about 500 px/m).
- `RingBed.fbx`: the dirt bed under the flags (768 tris, r 1.68-2.96 m, 8 cm down), so the ring's 2 cm joints read dark: PlazaBed stops at r 2.8. It uses the same M_CobbleBed tileable as PlazaBed (`../Cobbles/CobbleBed_*`) and the linear `damp` attribute.

## FarCobble (`FarCobble/`)
- Run: `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_cobbles.py -- --out <scratch> --stage far --res 2048`, packed with `sc_pack_unity.py --to-orm <scratch>/tex CobbleFar <folder>`.
- `CobbleFar_BaseColor/_Normal/_ORM.png` 2048 px: a 2 m x 2 m tileable of the laid fan courses (145 stones from the real family highs, 928k tris, plus the joint bed) baked top-down, for the surround ground beyond the placed patch. No mesh: put it on the surround plane with UV in 2 m tiles (1024 px/m).
- Wraps both ways (2x2 check clean; the wrapped stone copies carry a ~1.5 mm jitter, so the edge pixels differ slightly more than interior neighbours but no seam reads). The 2 m repeat shows on close inspection: break it up in Unity (rotation/offset variation, decals, blend with CobbleBed).