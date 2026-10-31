# B8 lanterns: sources, licences, pivots

Generator: `Art/Blender/town/sc_lanterns_adopt.py` (`--res 2048 --export`). Folder names match the corner builder's rows exactly.

| folder | source | licence | what we changed |
|---|---|---|---|
| Stall_Lantern | Poly Haven wooden_lantern_01 https://polyhaven.com/a/wooden_lantern_01 (James Ray Cock) | CC0 1.0 | decimated 8.3k -> 5.2k, widened 15%, raised 10%, +10% storybook oversize; iron handle thickened; stub candle (ours); re-baked onto fresh UVs: warmer wood, metal bits to wrought iron, soot by height, warm amber glass |
| Lantern_Bracket | Poly Haven Lantern_01 https://polyhaven.com/a/Lantern_01 (Rajil Jose Macatangay) | CC0 1.0 | brass regraded to blackened wrought iron with worn-bright edges; decimated 30.8k -> 4.2k (globe 3k -> 0.5k); thickened; scaled 0.29 -> 0.52 m; forged wall bracket (ours: plate, square-bar arm with end curl, scrolled brace, hook, bolts); re-baked with soot and amber glass |

## Pivots (Blender z up, -Y = the builder's -Z, metres, scale 1)
- `SC_B8_Stall_Lantern` (5.3k tris, 0.64 m to the handle top): pivot = ground centre of its feet; stands on the counter (row StallLantern). Candle flame 0.17 m above the pivot (point light, about 2700 K, for dusk).
- `SC_B8_Lantern_Bracket` (5.8k tris): pivot = back of the wall plate at its centre; the arm reaches 0.46 m toward -Y and the lantern hangs below it. So the row's z should be the wall face: **At(0, 2.45, 7.5)** (the table has 7.38, which would push the plate 0.12 m into the plaster). Burner 0.43 m out and 0.31 m below the pivot.

## Unity
- One texture set per lantern (`T_SC_B8_*_BaseColor/_Normal/_ORM`). The FBX has two slots (opaque + glass) sharing it; the builder assigns one opaque material, which shows the glass as solid warm amber (reads fine by day). For see-through glass, give the second slot a Transparent URP Lit using the BaseColor alpha.
- Never alpha-clip the opaque material: the BaseColor alpha is the glass opacity and would punch holes along UV seams at distance.
- ORM = R occlusion, G roughness, B metallic.

# B8 market dressing: sources (stall)

## MarketStall (`MarketStall/`)
- Generator: `Art/Blender/town/sc_market_stall.py` (procedural, reproducible from text).
- PC bake (2026-10-30 round 2: stall round 5 with the counter cloth and loaf sign; 35,328 tris; Blender 5.2.2): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_market_stall.py -- --out <scratch> --stage bake --res 2048 --export`, then
  `python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/maps MarketStall <this folder>`.
- Files: `MarketStall.fbx` (one mesh, one material, UV0 = the bake atlas; -Z forward, Y up, metres, transforms baked; pivot at the ground centre, counter faces local +Z),
  `MarketStall_BaseColor.png` (sRGB), `MarketStall_Normal.png` (tangent space, OpenGL Y+), `MarketStall_ORM.png` (R occlusion, G roughness, B metallic). 2048 px.
- Texture inputs baked into the set: the shared B1 tileables `Art/Textures/B1_town/` (painted_wood, oak_timber, canvas, iron, sandstone), graded from Poly Haven scans, CC0 1.0 (record: `Art/Textures/B1_town/SOURCES.md`), plus procedural masks. No other external assets.
