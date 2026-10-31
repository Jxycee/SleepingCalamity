# Bell tower integration: status

State: DONE (merged to main ee4fe79)
Session: cloud, branch claude/brave-keller-3d91gk
Progress: 100%
Sheet: Reference/hollowmere/bell_tower/bell_tower_sheet.jpg
SC RESULT: SC RESULT check PASS (12a843a: build PASS, test-edit 591/591, test-play 32 passed / 8 skipped / 0 failed); main merge differs only in Reference/docs, so no re-check
Merge commit: ee4fe79
Tris: 22,132 (BellTower.fbx, cap 40,000)
Blocker: none

Done:
- Merged `7ac0a9d`. Facade kit `__main__` wrap verified harmless: old and new kit export identical tris and bounds
  (Facade 37,500, Neighbour_Left 36,844, Neighbour_Right 30,443, Infill 22,438, matching SOURCES.md).
- `BellTower.fbx` exported: 22,132 tris, 6 meshes, all with linear `wear`. `B4/SOURCES.md` `## BellTower` section added.
- Builder: `NEIGHBOUR_Church` places folder `BellTower` at (-3, 0, 20.2) (the greybox tower's spot), ArtYaw 180; the hall greybox is now its own row
  `NEIGHBOUR_ChurchHall`. `BeautyCornerArt.BuildTowerMaterials` adds BT_Rubble, BT_ClockFace and a lifted BT_Bronze (WearLit).
- Round 1 capture: the spire sits right behind the facade chimney where the greybox stood, apex inside the frame; composition unchanged.
  Materials not applied yet (flat grey placeholder), see Blocker.

- Grey tower root cause: the cloud export wrote absolute texture paths that EXIST on the Unity machine; Unity's legacy material import then names every
  textured slot 'Lit' (verified: the same Infill neighbour exported here also came in as 'Lit'; the PC exports point at deleted worktrees so they import fine).
  Fix: `sc_bell_tower.py` exports with `path_mode='STRIP'`; re-exported FBX has the same 22,132 tris. The facade kit has the same latent issue for cloud exports (not changed here).
- Merge order noted: Wave 0A (claude/cool-ritchie-ofmy47) merges first; I pull main, keep both BeautyCornerBuilder.cs changes, rebuild, re-check, then merge.

- Round 2 capture (materials in): the spire reads red tile and sits right behind the facade chimney in both wide shots (Midday and LowSun); Mid and Close
  unchanged (tower out of frame). Composition matches the approved frames; no fix round needed. Sheet refreshed (same path).
  Not visible in any capture camera (hidden behind the facade roof): belfry, bronze bell, clock face and the rubble/quoin shaft, so their WearLit look is unverified in Unity.
- Hall: kept as the greybox row NEIGHBOUR_ChurchHall (its roof sliver is part of the approved wide frame).
- Main merged into the branch (9a0e796), no conflicts. Wave 0A (cool-ritchie) only touches HollowmereBuilder.cs / HollowmereTerrain.cs, not BeautyCornerBuilder.cs,
  so no overlap; I still wait for it to land on main as ordered, then pull main again, re-check and merge.

- check PASS on 997878b (before the terrain pass): build PASS, EditMode 586/586, PlayMode 32 passed / 8 skipped / 0 failed. Re-check runs on the final tip.
- Review (reviewer agent): no blocking findings; three doc/comment nits fixed. Its churn warning was real: the raw HDR bronze tint wrote _Color 6.8000007 with graphics
  and 6.8 without, so BT_Bronze is now a flat warm bronze, linear (0.55, 0.36, 0.16), like FK_Glass (deterministic bytes).
- Main merged again (terrain pass 1e20cb2): no conflicts; BeautyCornerBuilder.cs keeps both sides. In the town, HollowmereBuilder places the BellTower landmark at
  l.Y = KeepHeight (+4) and the tower art as its local (0, 0, 0) child, so it stands on the keep terrace; HollowmereLayout untouched. Town capture running to show it.
- Town (Hollowmere scene) captures: the tower stands on the keep terrace (+4), visible from the overview and above the keep wall at the keep stairs (last row of the sheet).

Unverified: the belfry, bronze bell, clock face and rubble/quoin shaft are hidden behind the facade roof in every corner capture, so their WearLit look was only
seen at a distance in the town shots (shaft reads grey, spire red). A close camera on the tower would be the next check. The facade kit (sc_facade_kit.py) still exports
absolute texture paths: a cloud export of it would hit the same 'Lit' slot problem (fix: path_mode='STRIP', as in sc_bell_tower.py).
Generator branch claude/determined-thompson-6z2mml deleted after the merge.
