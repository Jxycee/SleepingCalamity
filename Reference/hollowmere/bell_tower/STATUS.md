# Bell tower integration: status

State: IN PROGRESS (round 2 capture running)
Session: cloud, branch claude/brave-keller-3d91gk
Progress: 55%
Sheet: Reference/hollowmere/bell_tower/bell_tower_sheet.jpg
Blocker: none. Unity captures DO render in the cloud. Round 1 tower slots imported as 'Lit' (first import ran before the meta settled); round 2 checks whether that clears.

Done:
- Merged `7ac0a9d`. Facade kit `__main__` wrap verified harmless: old and new kit export identical tris and bounds
  (Facade 37,500, Neighbour_Left 36,844, Neighbour_Right 30,443, Infill 22,438, matching SOURCES.md).
- `BellTower.fbx` exported: 22,132 tris, 6 meshes, all with linear `wear`. `B4/SOURCES.md` `## BellTower` section added.
- Builder: `NEIGHBOUR_Church` places folder `BellTower` at (-3, 0, 20.2) (the greybox tower's spot), ArtYaw 180; the hall greybox is now its own row
  `NEIGHBOUR_ChurchHall`. `BeautyCornerArt.BuildTowerMaterials` adds BT_Rubble, BT_ClockFace and a lifted BT_Bronze (WearLit).
- Round 1 capture: the spire sits right behind the facade chimney where the greybox stood, apex inside the frame; composition unchanged.
  Materials not applied yet (flat grey placeholder), see Blocker.

Pending: round 2 capture with materials, sheet refresh, `unity.sh check`, merge to main, delete the generator branch.
SC RESULT: (not yet)
Merge commit: (not yet)
