# Area 1 nature set (N01-N07): sources and licence

All Poly Haven models, licence CC0 (https://polyhaven.com/license), no credit required (credited anyway). Fetched 2026-11-16 as 2K glTF into `Art/Source/PolyHaven/<id>/` with `Art/Blender/tools/sc_fetch_models.py` (record in `Art/Source/PolyHaven/SOURCES.md`) and adopted with `Art/Blender/town/sc_nature_adopt.py`.

Regenerate: `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b -P Art/Blender/town/sc_nature_adopt.py` (fetches what is missing).

Texture convention of the rocks, stump and log follows the adopted T11a: `<code>_albedo.jpg`, `<code>_normal.jpg` (OpenGL), `<code>_ms.png` (R metallic 0, A smoothness = 1 - roughness), `<code>_occ.png` (grey AO). Flowers carry `<code>_albedo.png` (RGBA, alpha = silhouette) and `<code>_normal.png`. FBX axes -Z forward, +Y up, meshes `<code>_LOD0/1/2`.

| code | Poly Haven id | URL | licence | author | date | what | size (m) | tris per LOD | files |
|---|---|---|---|---|---|---|---|---|---|
| N01 | rock_face_02 | https://polyhaven.com/a/rock_face_02 | CC0 | 2026-11-16 | 2026-11-16 | stratified outcrop / cliff chunk | 3.2 x 2.51 x 2.93 | 3000 / 1200 / 360 | `N01_cliff_slab/`: N01.fbx, N01_albedo.jpg, N01_normal.jpg, N01_ms.png, N01_occ.png |
| N02 | boulder_01 | https://polyhaven.com/a/boulder_01 | CC0 | 2026-11-16 | 2026-11-16 | angular boulder | 1.26 x 1.8 x 0.98 | 3000 / 1200 / 360 | `N02_angular_rock/`: N02.fbx, N02_albedo.jpg, N02_normal.jpg, N02_ms.png, N02_occ.png |
| N03 | namaqualand_stones_01 | https://polyhaven.com/a/namaqualand_stones_01 | CC0 | 2026-11-16 | 2026-11-16 | cluster of five flat stones (pebbles scaled 2.7x) | 0.96 x 0.9 x 0.22 | 1158 / 480 / 144 | `N03_scree_cluster/`: N03.fbx, N03_albedo.jpg, N03_normal.jpg, N03_ms.png, N03_occ.png |
| N04 | tree_stump_01 | https://polyhaven.com/a/tree_stump_01 | CC0 | 2026-11-16 | 2026-11-16 | flared tree stump | 1.34 x 1.5 x 0.54 | 2499 / 999 | `N04_stump/`: N04.fbx, N04_albedo.jpg, N04_normal.jpg, N04_ms.png, N04_occ.png |
| N05 | dead_tree_trunk_02 | https://polyhaven.com/a/dead_tree_trunk_02 | CC0 | 2026-11-16 | 2026-11-16 | fallen dead trunk with a fork | 3.4 x 0.88 x 0.88 | 2500 / 1000 | `N05_fallen_log/`: N05.fbx, N05_albedo.jpg, N05_normal.jpg, N05_ms.png, N05_occ.png |
| N06 | dandelion_01 | https://polyhaven.com/a/dandelion_01 | CC0 | 2026-11-16 | 2026-11-16 | yellow dandelion ground-cover clump | 0.48 x 0.46 x 0.32 | 72 | `N06_dandelion/`: N06.fbx, N06_albedo.png, N06_normal.png |
| N07 | shrub_01 | https://polyhaven.com/a/shrub_01 | CC0 | 2026-11-16 | 2026-11-16 | white-flowered meadow plant clump | 0.27 x 0.25 x 0.37 | 96 | `N07_meadow_spike/`: N07.fbx, N07_albedo.png, N07_normal.png |

Changes: scans scaled to real metres, collapse-decimated (UVs kept, source albedo/normal reused), pivot at the base centre; N03 is five of the source stones arranged as one cluster at 2.7x (they are 4-15 cm pebbles); N06/N07 are rebuilt as card clumps from side renders of the scan (the scan has no alpha).
