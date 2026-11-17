# Hollowmere.unity composition (Run 7, commit base 2ce1fa03 + the new cameras)

Produced by `Reference/sessions/area1-world/tools/scene_composition.py Assets/_Project/Scenes/Hollowmere.unity` (streams the file; rerun after any build).

Total 59,582,323 bytes (56.8 MB), 43,992 YAML documents, 9,457 GameObjects (9,057 under the root "Hollowmere").

## What makes it 59 MB
Embedded Mesh documents are 41.5% of the file (24.7 MB, only 479 meshes): Unity writes each mesh's vertices, normals, UVs and triangles as hex text.
Two families are 19.4 MB of that (33% of the whole file): the card-forest tiles (14.4 MB, two groups) and the lane step treads (4.9 MB). MeshRenderer docs
(10.6 MB, 7,880) and PrefabInstance docs (8.5 MB, 2,446 modification lists) come next. The two presets each hold their own copy of the lanes and the foliage.

## Ten biggest YAML classes
- !u!43 Mesh: 24,731,624 bytes (41.5%), 479 docs
- !u!23 MeshRenderer: 10,612,051 (17.8%), 7,880 docs
- !u!1001 PrefabInstance: 8,507,917 (14.3%), 2,446 docs
- !u!4 Transform: 5,157,595 (8.7%), 11,503 docs
- !u!1 GameObject: 4,002,753 (6.7%), 9,457 docs
- !u!198 ParticleSystem: 2,066,660 (3.5%), 40 docs
- !u!33 MeshFilter: 1,990,847 (3.3%), 7,880 docs
- !u!65 BoxCollider: 1,323,333 (2.2%), 2,569 docs
- !u!114 MonoBehaviour: 810,047 (1.4%), 1,344 docs
- !u!64 MeshCollider: 112,020 (0.2%), 198 docs

## Ten largest mesh groups and the code that makes them
| # | Mesh group (name stem) | Bytes | Meshes | Builder file and method |
|---|---|---|---|---|
| 1 | RD_Cards_Ring_x_y (field and hedge card tiles) | 8,937,260 | 138 | `HollowmereRoad.cs` `Cards` (line 210) -> `EmitTiles` (249), mesh name prefix "RD_Cards_" |
| 2 | RD_HillCards_Ring_x_y (Bell Hill forest cards) | 5,490,214 | 82 | `HollowmereWild.cs` line 316 `HollowmereRoad.CardForest(wild, "HillCards", ...)` -> `EmitTiles` |
| 3 | LK_StoneTread_LK####_LaneStep (one mesh per lane step) | 4,925,703 | 220 | `HollowmereLanes.cs` `BuildTreadMesh` (333; `new Mesh` at line 423) |
| 4 | BD_MawrrowMountain | 1,553,917 | 1 | `HollowmereBackdrop.cs` `BuildMountain` (157; object at 203; mesh made in `MakeObject` 401) |
| 5 | RD_MidHills | 652,103 | 1 | `HollowmereRoad.cs` `Ring` (343) line 383 -> `Annulus` (423; mesh at 448) |
| 6 | BD_HillRidge | 487,544 | 1 | `HollowmereBackdrop.cs` `BuildLayer` (121), layer table line 44 |
| 7 | BD_FarRange | 487,543 | 1 | `HollowmereBackdrop.cs` `BuildLayer` (121), layer table line 46 |
| 8 | BD_TreeRidge | 487,540 | 1 | `HollowmereBackdrop.cs` `BuildLayer` (121), layer table line 42 |
| 9 | RD_FarRidge | 479,687 | 1 | `HollowmereRoad.cs` `Ring` line 384 -> `Annulus` |
| 10 | RD_BackdropRing | 473,476 | 1 | `HollowmereRoad.cs` `Ring` (343), mesh at line 371 |

Next: RD_Cards_Terrain 0.44 MB (12 meshes), RD_HillCards_Terrain 0.20 MB (8).
## 25 largest Mesh documents (bytes)
BD_MawrrowMountain 1,553,917; RD_MidHills 652,103; BD_HillRidge 487,544; BD_FarRange 487,543; BD_TreeRidge 487,540; RD_FarRidge 479,687;
RD_BackdropRing 473,476; RD_Cards_Ring_-2_1 290,344; RD_Cards_Ring_1_1 274,019; RD_Cards_Ring_0_1 223,831; RD_Cards_Ring_2_-1 222,208;
RD_Cards_Ring_-1_1 207,921; RD_Cards_Ring_-3_0 189,565; RD_Cards_Ring_2_0 185,890; RD_Cards_Ring_1_-2 180,999; RD_Cards_Ring_-1_2 175,287;
RD_Cards_Ring_-2_-1 168,764; RD_Cards_Ring_-3_-2 159,782; RD_Cards_Ring_2_-2 154,887; RD_HillCards_Ring_2_1 151,616; RD_HillCards_Ring_-4_1 150,401;
RD_HillCards_Ring_5_1 149,170; RD_Cards_Ring_-1_-3 148,360; RD_Cards_Ring_1_0 147,946; RD_Cards_Ring_-2_-2 147,546.

## Largest PrefabInstance sources (modification lists, 8.5 MB in all)
TR03 tree 163 instances, 0.49 MB; SC_B9_GrassClump_A 133, 0.46 MB; SC_B9_MossTuft_C 123, 0.42 MB; SC_B9_GrassClump_B 123, 0.42 MB; SC_B9_GrassClump_C 121, 0.41 MB.

## GameObjects by path two levels below "Hollowmere" (top 20 of 360 groups)
- Preset_Rebuilt/Lanes 1,886; Preset_Ruined/Lanes 1,852
- Preset_Ruined/Foliage 1,336; Preset_Rebuilt/Foliage 639
- Preset_Ruined/Foundations 640; Preset_Rebuilt/Foundations 237
- Preset_Rebuilt/Dressing 331; Preset_Ruined/Dressing 77
- Set/Country 183; Set/LakeShore 174; Set/Keep 132; Set/Wild 115
- Preset_Rebuilt/Clutter 102; Preset_Ruined/Landmarks 70; Preset_Rebuilt/Landmarks 70; Preset_Ruined/Wild 46
- Preset_Rebuilt/ContactAO 36; Preset_Rebuilt/Corner 33; Preset_Rebuilt/HM_Smoke 28; TownView/Bakery_F02 and eight more Bakery_* groups 25 each

## Reading
Cutting the file means cutting mesh text, not object count: the card tiles (RD_*Cards_Ring, 14.4 MB) and lane tread meshes (4.9 MB) are the targets.
Options for a later task (not done here): force binary serialisation for the scene (not a builder change, a project setting), store tread and card meshes as .asset files
instead of embedding them, or build the card tiles at load time. Lane GameObjects are doubled by the two presets (3,738 for lanes alone).
