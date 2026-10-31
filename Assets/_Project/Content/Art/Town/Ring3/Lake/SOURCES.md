# Lake shore and stream dressing (ring 3, Q10): sources

- Generator: `Art/Blender/town/sc_lakeshore.py` (procedural, reproducible from text; reuses `sc_facade_kit.py` helpers: box members, tileable loading, review lights).
- Export (2026-10-31, Blender 4.5.3): `blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_lakeshore.py -- --out <scratch> --export`; `<scratch>/LakeShore.fbx` and `<scratch>/tex/*.png` are copied here (`Textures/`). Texture paths are stripped (`path_mode='STRIP'`) so Unity keeps the real slot names.
- Axes: -Z forward, Y up, metres, transforms baked. Seven kinds of root-level mesh objects, no empties, no vertex colours (no shader needed: plain URP Lit with the maps below; reed blades are double-skinned geometry, so back-face culling can stay on).
  | Object | Tris | Unity bounds (x / y / z, m) | Slots | Pivot |
  |---|---|---|---|---|
  | `Rock_Shore_A` | 640 | -0.60..0.60 / -0.03..0.69 / -0.48..0.48 | `LS_Rock` | ground centre, 3 cm buried |
  | `Rock_Shore_B` | 480 | -0.41..0.41 / -0.03..0.47 / -0.35..0.35 | `LS_Rock` | same |
  | `Rock_Shore_C` | 330 | -0.26..0.26 / -0.03..0.31 / -0.22..0.22 | `LS_Rock` | same |
  | `Rock_Stream_A` | 880 | -0.90..0.90 / -0.03..0.92 / -0.65..0.65 | `LS_Rock` | same (flat top) |
  | `Rock_Stream_B` | 720 | -0.65..0.65 / -0.03..0.77 / -0.55..0.55 | `LS_Rock` | same |
  | `Rock_Stream_C` | 540 | -0.47..0.48 / -0.03..0.61 / -0.42..0.42 | `LS_Rock` | same |
  | `Reed_A` | 732 | -0.77..0.75 / -0.04..2.13 / -0.90..0.75 | `LS_Reed` | clump base (3 cattails) |
  | `Reed_B` | 586 | -0.60..0.62 / -0.04..1.75 / -0.63..0.77 | `LS_Reed` | clump base (2 cattails) |
  | `Reed_C` | 560 | -0.77..0.74 / -0.04..1.33 / -0.72..0.76 | `LS_Reed` | clump base (no cattails) |
  | `Jetty` | 5,650 | -1.20..1.16 / -3.00..1.09 / -0.03..9.03 | `LS_Jetty`, `LS_Pile` | shore end of the deck; deck top y = 0, runs along local +Z, piles to y -3.0, mooring posts to +1.05 |
  Total 11,118 tris. Jetty: 1.8 x 9 m deck of 39 gapped oak planks (three off-true: one sagging, one tilted 2.6 deg, one proud), two stringers, five cross bearers, 10 round piles every ~2.1 m, two mooring posts at the far end (rope coil on the deck, a line wrapped twice round the -X post with a hanging tail), a ladder on the +X side (local +X) near the far end, iron nail heads and bands. Waterline for the wet stain is y -0.6 under the deck top (piles are painted wet and algae-green below -0.25, darkest under -0.9); shore stones carry the damp algae band in their bottom 15-18 cm (stream boulders 30-46 cm), so place shore stones with the waterline about 17 cm above their base.
- Textures (all unique, 8-bit PNG, ORM = R occlusion, G roughness, B metallic; normals are OpenGL tangent-space bakes):
  | Set | Files in `Textures/` | Size | Material |
  |---|---|---|---|
  | Rock | `LakeShore_Rock_BaseColor/Normal/ORM.png` | 2048 x 2048 | `LS_Rock`: six rocks in one atlas (bottom row shore, top row stream), baked from 20k-tri sculpts of each rock (normal, albedo, roughness, AO) |
  | Jetty | `LakeShore_Jetty_BaseColor/Normal/ORM.png` | 2048 x 2048 | `LS_Jetty`: deck planks, beams, rope, ladder, iron (metal in ORM B), self bake with AO |
  | Pile | `LakeShore_Pile_BaseColor/Normal/ORM.png` | 512 x 2048 | `LS_Pile`: piles and mooring posts, v = height (y -3.0..+1.2), wet band and algae painted along height |
  | Reed | `LakeShore_Reed_BaseColor/Normal/ORM.png` | 512 x 1024 | `LS_Reed`: four blade variants (u 0..0.5, v = base to tip, pale base to green to straw), cattail head (u 0.5..0.75), stalk (u 0.75..1) |
- Sources: Jetty and Pile are derived from the graded `B1_town/oak_timber` and `B1_town/iron` tileables (`Art/Textures/B1_town/`, graded Poly Haven scans, CC0 1.0, see `Art/Textures/B1_town/SOURCES.md`); Rock stone, moss, algae, rope and reed textures are procedural (generator code, own work, CC0 as part of the repo). No other external assets. Reference photos (study only, not used in any texture): `Reference/hollowmere/lake/REFS.md`.
- Slot to material hint for the Unity builder: `LS_Rock`, `LS_Jetty`, `LS_Pile`, `LS_Reed` -> URP Lit (or the town lit shader) with BaseColor, Normal, and ORM (occlusion R, smoothness from 1 - G, metallic B). Reed: use alpha-free opaque, no cutout.
