# B9 foliage: sources, licences, pivots

Generator: `Art/Blender/town/sc_foliage_town.py` (helpers `sc_b9_lib.py`), PC run `--stage all --export`. One folder per model, named for the
corner builder's matcher (`BeautyCornerArt.cs`): each holds its FBX plus the texture set it uses (`*_BaseColor` with alpha, `*_Normal`,
`*_ORM`). The foliage folders share identical copies of the 2K atlas `T_B9_Foliage_*` (git and LFS store it once).

| piece | source | licence | what we changed |
|---|---|---|---|
| ivy leaves (atlas u 0-.5, v 0-.5) | ambientCG LeafSet017 https://ambientcg.com/view?id=LeafSet017 | CC0 1.0 | 6 scanned leaves composited into 3 runner cells + 4 singles, veins damped, graded to #76902f (+18% chroma: yellower than the facade's green shutters), drop shadows baked into AO |
| nettle leaves (u 0-.5, v .5-1) | Poly Haven nettle_plant https://polyhaven.com/a/nettle_plant (Rob Tuytel, Rico Cilliers) | CC0 1.0 | meshes combined into joint runs, decimated to 380 tris, scaled up (storybook), squashed narrow across the joint; alpha = UV coverage minus the black padding; graded to #6f8a34 |
| weed rosettes (u .5-1, v .5-1) | Poly Haven weed_plant_02 https://polyhaven.com/a/weed_plant_02 (Rob Tuytel, Rico Cilliers) | CC0 1.0 | as nettle |
| moss (atlas cells, Basin_Moss, Moss_Band) | ambientCG Moss002 https://ambientcg.com/view?id=Moss002 | CC0 1.0 | graded to #616e2e with #b6ab28 sunlit patches; ragged alpha edges |
| grass blades, ivy bark | procedural (ours) | - | - |

Poly Haven moss_01 (246k tris) was not needed.

## Pivots and placement (Blender z up, front -Y = the builder's -Z; metres, scale 1, ground centre unless noted)
| folder | model | tris | builder row | notes |
|---|---|---|---|---|
| Ivy_Facade | SC_B9_IvyWall | 4.4k | Ivy (exact alias missing, wins the contains tie over Ivy_Patch_*) | 2.1 x 5 m; pivot = bottom centre of the mass, back on the wall plane, leaves toward -Y; dense corner side is local -x (the facade's left edge). Wraps a jetty at 3.0 m (beam 0.28, 0.45 m out); rebuild with `--jetty <m>` once B4 lands |
| Ivy_Patch_A / _B | SC_B9_IvyPatchA / B | 398 / 148 | none yet | 1.0 x 1.4 m / 0.7 x 0.6 m flat-wall patches, bottom centre on the wall plane. Suggested: (1.6, 0.6, 7.5), (-2.6, 0, 7.5) |
| Weed_A | SC_B9_CrackWeed_A | 379 | CrackWeeds (contains "weed"; A sorts first) | a 0.55 m run along local x, about 0.15 m across: yaw it along the joint or the wall foot |
| Weed_B..E | SC_B9_CrackWeed_B..E | 380 | none yet | variants (B tall nettle wall-foot run, C nettle pair, D single rosette, E rosette run) |
| Basin_Moss | SC_B9_BasinMoss | 168 | BasinMoss (exact) | 0.9 x 0.5 m cushion lying on the rim, pivot at its centre on the rim top; own 1K set `T_B9_BasinMoss_*` |
| Moss_Band | SC_B9_MossDecal | 72 | none yet | 70 deg band on the basin wall (r 1.705 m, 0-0.38 m) + 7 cm ground lip; pivot = basin centre; yaw 0 puts it on the south face the cameras see. Own 1K set `T_B9_MossDecal_*` |
| Moss_Tuft_A..C | SC_B9_MossTuft_A..C | 132 | none yet | basin foot / flagstone joints. Suggested: (-1.3, 0, -1.15), (-0.65, 0, -1.62), (-1.65, 0, -0.45) |
| Grass_A..C | SC_B9_GrassClump_A..C | 128-224 | none yet | facade corner and stall legs. Suggested: (-4.75, 0, 7.2), (-4.3, 0, 7.15), (3.3, 0, 3.7) |

## Unity
- Model: Normals = Import (bent card normals are authored and survive the FBX round trip).
- Foliage: URP Lit, Alpha Clipping 0.5, Render Face Both (the builder does this for foliage rows). On the BaseColor importer, **Mip Maps Preserve Coverage** (alpha ref 0.5) keeps the cut-out from thinning at the wide camera.
- ORM = R occlusion, G roughness, B metallic. Vertex colour `Col` = a card AO for a future foliage/wind shader (URP Lit ignores it).
