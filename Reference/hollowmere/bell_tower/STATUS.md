# Bell tower integration: status

State: CHECKING (look judged OK; unity.sh check + review running on 9a0e796; merge waits for Wave 0A to land on main)
Session: cloud, branch claude/brave-keller-3d91gk
Progress: 85%
Sheet: Reference/hollowmere/bell_tower/bell_tower_sheet.jpg
Blocker: none. Unity captures render in the cloud. Root cause of the grey tower found and fixed (see below).

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

Pending: round 2 capture with materials, sheet refresh, `unity.sh check`, merge to main, delete the generator branch.
SC RESULT: (not yet)
Merge commit: (not yet)
