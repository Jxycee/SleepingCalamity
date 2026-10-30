# B8 market dressing: sources

## MarketStall (`MarketStall/`)
- Generator: `Art/Blender/town/sc_market_stall.py` (procedural, reproducible from text).
- PC bake (2026-10-30, Blender 5.2.2): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch> --stage bake --res 2048 --export`, then
  `python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/maps MarketStall <this folder>`.
- Files: `MarketStall.fbx` (one mesh, one material, UV0 = the bake atlas; -Z forward, Y up, metres, transforms baked; pivot at the ground centre, counter faces local +Z),
  `MarketStall_BaseColor.png` (sRGB), `MarketStall_Normal.png` (tangent space, OpenGL Y+), `MarketStall_ORM.png` (R occlusion, G roughness, B metallic). 2048 px.
- Texture inputs baked into the set: the shared B1 tileables `Art/Textures/B1_town/` (painted_wood, oak_timber, canvas, iron, sandstone), graded from Poly Haven scans, CC0 1.0 (record: `Art/Textures/B1_town/SOURCES.md`), plus procedural masks. No other external assets.
