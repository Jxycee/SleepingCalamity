Status: working

# FBX UV regen (Inn, StoneCottage A/B/C, Townhouse A/B/C, RetainWallLow)

Audit tool: `HollowmereFaultScan.Scan` (Unity, SC_SCAN_DIR, SC_CAPTURE_NOBUILD=1) covers placed Hollowmere instances only. The Inn, cottages and townhouses are not placed in the scene, so the scan cannot see them. For those, a Blender script (same metric: area-weighted singular-value ratio of the UV-to-world Jacobian, per object|material, flag > 2.5) ran on each FBX.

## BEFORE
HollowmereFaultScan (main tip): `uv_stretch 4`, all RetainWallLow, M_LK_Dressed (sandstone) 2.529:
- Ruined + Rebuilt LK0727_RetainWallLow (-3.68,-3.48,-38.60), size 0.64x2.56x0.71
- Ruined + Rebuilt LK0669_RetainWallLow (-3.69,-2.97,-36.75), size 0.63x2.56x0.71
Cause: the instance is a 0.64 m stub of the 2 m module (ScaleX about 0.32), so the shared mesh is stretched 1/0.395 along X. The LaneKit.fbx mesh itself is 1.07.

FBX audit (Blender, committed FBX, Frame unless noted; Bloom/Leaf are atlas cards, not tiling, ignored by the scan):
| FBX | material | stretch |
|---|---|---|
| Inn | FK_RoofTile | 2.52 |
| StoneCottage_A | FK_RoofTile | 2.70 |
| StoneCottage_B | FK_RoofTile | 2.83 |
| StoneCottage_C | FK_RoofTile | 5.00 |
| StoneCottage_C | FK_OakDoor | 3.05 |
| Townhouse_C | FK_OakDoor | 2.77 |
| Townhouse_A, B | none over 2.5 | |

## Fixes
- `sc_cottage.py`: tile/thatch `slab` UVs were `(x + 0.7y, 1.3z)` (sheared, non-uniform): now metre UVs in the slab plane; lean-to log cones used `x*3` on U: per-face isometric UVs; chimney pots had flat lip rings with no V extent: per-face isometric UVs.
- `sc_inn.py`: `tile_courses` butt risers had no extent along the slope direction (collapsed UVs): the height along the tile normal now feeds V; hip roof deck V used a constant `1.41*z`: now true arc length up the slope (bellcast kick, hip ends).
- Townhouse: no local UV change needed (its remaining stretch came from the kit, fixed in bc520027; regenerated).
- RetainWallLow: not a mesh problem. `LaneKitPlan.ResolveWallJunctions` leaves 0.64 m stubs of the 2 m module (ScaleX 0.32), so the shared mesh stretches 1/0.395 = 2.53. Retaining-wall stubs shorter than `RetainMinStub = 0.85` m (ScaleX < 0.425, stretch under 2.4) are now dropped, like shorter garden stubs already were (`WallMinStub = 0.5`). Plan row count 1087 -> 1084, golden hash updated in `LaneKitPlanTests`.

## AFTER
HollowmereFaultScan after `unity.ps1 build` (SC_CAPTURE_NOBUILD=1): `uv_stretch 0` (was 4). foliage 461, uv_density 4, overlap 7 unchanged.

FBX audit (Blender), worst tiling materials:
| FBX | FK_RoofTile | FK_OakDoor |
|---|---|---|
| Inn | 2.30 (was 2.52) | 1.08 |
| StoneCottage_A | 1.60 (2.70) | |
| StoneCottage_B | 1.00 (2.83) | |
| StoneCottage_C | 1.00 (5.00) | 1.00 (3.05) |
| Townhouse_A | 1.55 | |
| Townhouse_B | 1.64 | |
| Townhouse_C | 1.56 | 1.00 (2.77) |
All below 2.5. Bloom/Leaf atlas cards (10.0) are not tiling materials and the scan skips them.

Gate: PASS (compile, test-edit 1594/1594).
Not done: captures (ordered separately); merge.
