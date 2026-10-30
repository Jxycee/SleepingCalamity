# B3 sources and record

Trim sheets are built from the graded B1/B2 tileables (oak_timber, sandstone, iron; barrel_oak, vault_stone, old_brick; see their SOURCES.md for the CC0 Poly Haven scans and licences) plus procedural profiles.
Decals (moss, grime, drip, crack) are fully procedural (no scan): `Art/Blender/textures/sc_tex_b3.py`, seeded, deterministic. No external source or licence applies to them.

| set | what |
|---|---|
| trim_town | trim sheet 2048x2048, 8 strips x 256 px (25 cm), 1024 px/m, tileable along u: 0:timber_chamfer, 1:timber_bead, 2:timber_cornice, 3:stone_cope, 4:stone_string, 5:stone_plinth, 6:iron_strap, 7:iron_band |
| trim_cellar | trim sheet 2048x2048, 8 strips x 256 px (25 cm), 1024 px/m, tileable along u: 0:timber_beam, 1:timber_batten, 2:stone_cope, 3:stone_string, 4:stone_plinth, 5:brick_corbel, 6:iron_strap, 7:iron_band |
| moss | decal 1024x1024 RGBA (alpha = opacity), edge-faded, 1024 px/m (1 m square): placed wear over the tileables |
| grime | decal 1024x1024 RGBA (alpha = opacity), edge-faded, 1024 px/m (1 m square): placed wear over the tileables |
| drip | decal 1024x1024 RGBA (alpha = opacity), edge-faded, 1024 px/m (1 m square): placed wear over the tileables |
| crack | decal 1024x1024 RGBA (alpha = opacity), edge-faded, 1024 px/m (1 m square): placed wear over the tileables |
