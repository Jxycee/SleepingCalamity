# B8 MarketLantern: sources and licence

Generator: `Art/Blender/town/sc_lanterns_adopt.py` (`--res 2048 --export`).

| piece | source | licence | what we changed |
|---|---|---|---|
| lantern body, glass, handle | Poly Haven wooden_lantern_01 https://polyhaven.com/a/wooden_lantern_01 (James Ray Cock) | CC0 1.0 | decimated 8.3k -> 5.2k, widened 15%, raised 10%, then +10% storybook oversize; handle thickened; re-baked onto a fresh UV set: warmer wood, its metal bits darkened to wrought iron, soot by height, warm amber glass |
| S-hook, ring, candle | procedural (ours) | - | - |

`SC_B8_MarketLantern` (5.7k tris, about 0.79 m including the hook): origin = the top of the ring; hang it from the stall awning beam. Candle flame (point light, about 2700 K) at 0.62 m below the origin.

## Unity import (controller)
- Model: Normals = Import (bent card normals are authored), Scale 1.
- Material: URP Lit. `_BaseColor` (sRGB, alpha), `_Normal` (Normal map, OpenGL / Y+), `_ORM` (linear: R occlusion, G roughness, B metallic; smoothness = 1 - G, or pack with `Art/Blender/tools/sc_pack_unity.py --orm`).
- Two slots share the set: `M_SC_B8_MarketLantern` (opaque, Alpha Clipping 0.5) and `M_SC_B8_MarketLantern_Glass` (Surface Transparent, alpha from `_BaseColor`, smoothness about 0.9).
