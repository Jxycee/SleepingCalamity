# Hollowmere fault scan: ranked candidates (2026-11-03)

Tool: `SleepingCalamity.EditorTools.HollowmereFaultScan.Scan` (SC_CAPTURE_NOBUILD=1, built scene as on origin/main 0b52a516). Raw TSVs: `p4\scan\scan_*.tsv`.
Scan time 7.0 s (25 s wall including editor start). Positions are world-space bounds centres (x, y, z). R = Ruined, B = Rebuilt.

Raw counts (both presets, Set objects counted twice): float 8, sunk 2, float_building 0, sunk_building 0, overlap 7, foliage 462 (10 families embedded > 10%),
uv_density 4, uv_stretch 33, zfight 2 (+1878 transparent-decal coplanar pairs not listed: blended, no depth write).

| # | Kind | Family (count) | Preset | Position | Evidence |
|---|------|----------------|--------|----------|----------|
| 1 | float | Rock_Stream_B (4) | R+B | (105.4, -10.8, -32.4); (87.0, -11.2, -44.8); (103.6, -10.9, -29.9); (89.9, -11.1, -48.0) | 0.8-2.8 m above the Terrain collider at all 5 probes (Set/LakeShore/StreamBoulders) |
| 2 | sunk | Rock_Stream_B (1) | R+B | (101.3, -10.9, -26.4) | -0.97 to -2.1 m below layout ground (no collider hit at 4/5 probes); same stream group as #1 |
| 3 | zfight | AO_Art_CrateStack_Broken (ContactAO) | R | (-13.0, -0.04, 4.2) | flat queue-3000 Lit quad 0.000 m off Set/SquareGround/Art_SquareBed at 5/5 probes |
| 4 | zfight | AO_Stall_Tools_Ruined (ContactAO) | R | (5.6, -0.02, -8.4) | 0.002 m off Set/SquarePaving at 5/5 probes |
| 5 | uv_stretch | Workshop_C Frame, M_FK_ChimneyStone (2) | B | (-8.4, -3.5, -56.3); (-51.8, -1.4, -30.9) | stretch 7.5 (sandstone), plus oak 2.9 and iron 2.7 on the same Frame; texel density 1.6x median |
| 6 | uv_stretch | Bakery Part_Sign, M_BK_Gilt (1) | B | (-16.5, 3.9, 9.0) | stretch 6.8, density 0.6x |
| 7 | uv_stretch | BellTower Tower_Spire / Tower_Belfry, M_FK_Iron (2 parts) | R+B | (5.6, 18.8, 24.0); (5.6, 14.4, 24.0) | stretch 5.9 / 5.5, density 0.7x |
| 8 | uv_stretch | Smithy Part_Sign, M_FK_Iron (1) | B | (-41.4, -1.2, 5.6) | stretch 5.4, density 0.75x |
| 9 | uv_stretch | Schoolhouse Part_Bell, M_FK_Iron (1) | B | (-40.4, 3.6, -32.3) | stretch 4.1 |
| 10 | uv_stretch | Basket_Produce (1) + overlap with Deco_Prop_S3b_Crate | B | (2.9, 0.15, -10.1) | stretch 3.5 on its baked set; AABB 37% inside the crate, 10% of verts have the crate above and below |
| 11 | uv_stretch | Mill Shell, M_FK_RoofTile (1) | R | (70.6, -4.3, -59.5) | stretch 3.2 over 88 m2 of roof |
| 12 | uv_stretch | Workshop_B Frame, iron / roof tile / oak (2) | B | (39.0, 0.3, -2.8); (53.0, 0.2, -23.0) | stretch 3.15 iron, 2.7 roof tile, 2.9 oak |
| 13 | foliage | Ivy_Facade on Keep buttress (4) | B | (21.3, 1.4, 18.8) | 23% of ivy verts embedded in Set/Keep/Buttress21 |
| 14 | foliage | SC_B9_IvyWall + LintelIvy on the corner Facade (2) | B | (-4.0, 2.7, 7.7); (-1.2, 2.7, 7.7) | 21% / 13% of verts embedded in Facade Kit_Panels |
| 15 | foliage | MossBand / BasinMoss in DryFountain (2) | R+B | (0.8, 0.08, 0.8); (-0.9, 0.55, 1.45) | 50% / 5% of verts embedded in the fountain ring |
| 16 | foliage | Weed_C / Weed_A in lane kerb LK0436 (lot R2_01 edge) (6) | R+B | (-43.2, -3.4, 11.5); (-43.2, -3.4, 11.2) | 96% / 39% embedded (buried tufts: invisible or poking through the kerb top) |
| 17 | foliage | Weed_B / Weed_A in town wall breach and wall end (R2_16, R3_03) (5) | R | (-1.2, -6.2, -63.0); (60.6, -6.7, -37.8) | 23% embedded, wall mesh above and below 55-98% of verts |
| 18 | foliage | Ivy_Patch_A/B on HeroesHome_Cistern and Mill (3) | R+B | (11.6, 0.8, 15.2); (68.0, -9.5, -61.9) | 11-12% embedded in the frame |
| 19 | uv_stretch | Facade Kit_LeanTo (oak) / Kit_Chimney (roof tile) | B | (-6.0, 1.4, 11.6); (-3.1, 9.4, 9.6) | stretch 2.8 / 2.5 |
| 20 | uv_stretch | Smithy Frame, roof tile | B | (-38.9, 1.0, 7.4) | stretch 2.6 over 155 m2 |
| 21 | uv_stretch | Bakery Part_Oven, M_BK_Brick | B | (-8.1, 1.3, 13.7) | stretch 2.55 |
| 22 | uv_stretch | Pot_Broken (3) | R | (11.5, 0.18, -6.4); (-13.4, 0.18, 5.4); (-2.5, 0.18, -16.1) | stretch 2.65 on its baked set |
| 23 | uv_stretch | GD_VegBed_A/B cabbage (3) | B | (-58.5, -9.9, -71.3); (-54.6, -9.5, -73.5) | stretch 2.85 |
| 24 | uv_stretch / uv_density | LakeShore Jetty; Rock_Shore_C (2) | R+B | (-18.8, -12.4, -83.6); (76.7, -12.1, -97.1); (116.4, -10.9, -17.6) | Jetty stretch 2.6; small shore rocks 2.6-2.7x texel density (scaled down) |
| 25 | overlap | Stall-side props: Buckets_2, Crates_3, Baskets vs Stall_1; S3_Barrel vs Stall_3; Buckets_1 vs Barrels_2; S3b_Bucket vs SackPair (6) | B | (6.0, 0.19, 3.55); (6.4, 0.25, 4.5); (2.6, 0.11, 5.0); (7.5, 0.5, -9.5); (-4.0, 0.3, 6.5); (4.4, 0.3, -10.6) | AABB 38-92% inside, 0% of verts embedded: probably contact or under-awning, lowest confidence |

Notes
- No float or sunk building pieces once composite parts (belfry on tower, chimney on roof) count as supported; all sunk building candidates were deep-by-design kit (retaining walls, stair cores).
- Wall-mounted hooks, lanterns, signs, washing lines and bunting are excluded from float/overlap by name.
- uv checks skip foliage cards, grass, decals, alpha-clipped and transparent materials (atlas or blob textures, not tiling sets).
