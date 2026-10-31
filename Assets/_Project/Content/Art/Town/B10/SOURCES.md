# B10 dry fountain hero: sources

Both B10 assets are fully procedural: no scans, no external models, so no external licence applies. Original design (the
sleeping-beast spout is ours; nothing from Dark Cloud).
Generator: `Art/Blender/town/sc_dry_fountain.py` (shared helpers `Art/Blender/tools/sc_aaa_kit.py`, `sc_aaa_cuts.py`).
PC bake 2026-10-30, Blender 5.2.2 (OptiX); round 3 PC re-bake 2026-10-30 from fountain round 7 (one lion nose, slit sleepy eyes; user approved the look):
`blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_dry_fountain.py -- --out <scratch> --stage bake --res 4096 --export`
(DryFountain bakes at 4096, BannerPole at half: 2048), then per piece
`python Art/Blender/tools/sc_pack_unity.py --to-orm <scratch>/props/tex <Name> <this folder>/<Name>`.
**Texture cap 2048 px (user, 2026-10-30: the game targets 1440p, bake nothing above 2048).** The round-7 DryFountain bake
ran at 4096 and was downsampled to 2048 (Lanczos for BaseColor/ORM; the normal map filtered as vectors and
renormalised). Future bakes: `--res 2048` (the pole then bakes at 1024 unless the generator's half rule changes).
Maps: `_BaseColor` sRGB, `_Normal` tangent space (OpenGL Y+), `_ORM` R occlusion, G roughness, B metallic (linear).
FBX: one mesh, one material, UV0 = the bake UV (the only UV set); -Z forward, Y up, metres, scale 1, transforms baked.

## DryFountain (`DryFountain/`)
- `DryFountain.fbx`: 48,725 tris (round 7; 48,713 after FBX re-import triangulation), sandstone basin r 1.70 m (footing r 1.74), coping crown 0.555 m, central column with the beast spout; silt and leaves in the bowl.
- Pivot: ground centre of the basin (Unity placement (0, 0, 0), rotation 0; the beast faces the cameras).
- `DryFountain_BaseColor/_Normal/_ORM.png` 2048 px (downsampled from the 4096 bake). No metal (ORM B = 0).
- Colliders (builder): cylinder r 1.70 h 0.55 for the basin plus a capsule on the column.

## BannerPole (`BannerPole/`)
- `BannerPole.fbx`: 8,994 tris (round 7 bake): sandstone socket, 4.5 m sun-greyed oak pole with iron bands, arm and brace, 0.7 x 2.2 m bleached madder banner with a frayed, torn hem.
- Pivot: ground centre of the pole foot. Place at Unity (-2.6, 0, 1.8), rotation 0 (the banner faces south toward the cameras; the arm points east).
- `BannerPole_BaseColor/_Normal/_ORM.png` 2048 px; the iron bands are metallic in ORM B.
- The banner is real geometry (4 mm solidified cloth, tears cut in the mesh), so it is **opaque and needs neither alpha nor a two-sided material**. It is static (no cloth sim or wind).
- Colliders (builder): a box on the socket and a thin box or capsule on the pole.

Together 57.7k tris (round 7), inside the T4 hero band (30-60k).
