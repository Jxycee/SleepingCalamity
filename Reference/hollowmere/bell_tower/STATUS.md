# Bell tower integration: status (cloud session, branch claude/brave-keller-3d91gk)

State: IN PROGRESS. The Unity editor finished installing; the first capture run (corner rebuild + CaptureAll) is running.

Done:
- Merged `7ac0a9d` into the branch. Facade kit `__main__` wrap verified harmless: old and new kit export identical tris and bounds
  (Facade 37,500, Neighbour_Left 36,844, Neighbour_Right 30,443, Infill 22,438 = SOURCES.md).
- `BellTower.fbx` exported (22,132 tris, 6 meshes, all with linear `wear`), `B4/SOURCES.md` has a `## BellTower` section.
- Builder: `NEIGHBOUR_Church` now places the art from folder `BellTower` at (-3, 0, 20.2) (where the greybox tower stood), ArtYaw 180;
  the hall greybox is its own row `NEIGHBOUR_ChurchHall`. `BeautyCornerArt.BuildTowerMaterials` adds BT_Rubble, BT_ClockFace and a lifted BT_Bronze in WearLit.

Pending: captures, side-by-side sheet, `unity.sh check`, merge to main.
SC RESULT: (not yet)
Merge commit: (not yet)
Sheet: (not yet)
Blockers: none so far.
