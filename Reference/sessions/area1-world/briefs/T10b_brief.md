# T10b brief: road end and RoadOut fix round (Area 1 world, Run 7)

Goal: make the road end read as "the road is blocked" and hide the world edge, and clean the RoadOut foreground. Read `briefs/T10_brief.md` and T10's commit message (`git log -2`, ab228d05); this brief adds and overrides. Builders: `HollowmereRoadside.cs` (T10's), `HollowmereRoad.cs` (`EdgeBounds`, `RoadEnd`), `HollowmereBackdrop.cs`, `HollowmereTerrain.cs`, the water builder (find which one places the lake/water plane).

Branch: `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, the tip (e9b66226 or later). Uncommitted `Reference/sessions/area1-world/STATUS.md` and this brief from the controller: include both unchanged in your commit, never edit STATUS.md. Leave `briefs/T11_brief.md` alone (untracked, not yours). Do not merge main.

Controller's read of `R13/`:
- `HM_Diag_RoadEnd_Ruined_diagnostic.jpg`: the T18d trunk lies beside the road on the right, roughly along it, not across it; only a thin log and a branch cross the road. The wagon is not in frame. Beyond the road end the ground stops and a flat grey plane with mirrored (reflected) tree lines fills the view to the hills: it looks like a water plane or reflection probe showing past the terrain rim (x -150), i.e. the world edge is exposed. The ground around the end is bare dirt with a few plants.
- RoadOut: the Mid ruin is a clear gain. Faults: a big flat stone slab in the bottom-left foreground (the gate post?), a pale plank on the dirt, dark tall stalks in the foreground (T19u/T19y), and the row of bright, flat, spiky green tufts along the ruin's wall foot reads as toy grass.

## Do
1. **Probe the grey plane** (an editor query or one reduced capture from the diagnostic camera and from eye height 1.65 m at the road end looking west): what object it is (water plane, reflection, fog card, backdrop), where it starts, whether a player at the road end at eye height sees it. Fix the cause: the world beyond the blockage must read as country (fields, hedge, tree belt, rising ground) or be screened by a dense tree and hedge belt behind the blockage, never a flat grey sheet.
2. **Blockage**: turn the T18d trunk so it lies across the road (root plate on one verge, crown or broken end on the other, a slight angle, sunk into the ground), the wagon tipped against or beside it within the same view (visible from `Cap_HM_Diag_RoadEnd` and from 20 m east at eye height), the spill around them, branches and logs piled in the gaps so the blockage reads continuous verge to verge. Keep the collider wall at x -126 and traversal green; if the trunk move changes which spots are reachable, update `TraversalSpaces` the same way T10 did.
3. **Ground at the road end**: verge cover, grass and weeds over the bare dirt around the blockage (the fine-blade carpet and T21 plants), a few stones; hedges continue up to the blockage on both sides.
4. **RoadOut foreground**: keep props at least 4 m from every eye camera (as T10 proposed), drop or move the foreground slab, plank and tall dark stalks; replace the toy spiky tuft row at the ruin's wall foot with fine-blade grass and scanned plants (T21d-p) in irregular clumps, not a row.
5. Megascans: name any wanted listing in the report (title as on fab.com); do not download.

## Capture (at most three rounds)
Iterate with `SC_CAPTURE_ONLY=Eye_RoadOut,Diag_RoadEnd` plus one eye-height diagnostic at the road end looking west (`Cap_HM_Diag_RoadEndEye`, diagnostic only). The final round captures `Eye_RoadOut,Eye_RoadBack,Diag_RoadEnd,Diag_RoadEndEye` from the committed code into `R13/` (replace those frames; `R12b/` stays the before set); re-run the sheet: `python Reference/sessions/area1-world/tools/sheet_area.py R13 <final PNG dir> Reference/sessions/area1-world/R12b Reference/sessions/area1-world/R13`. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\1635b7dd-88db-474e-8e1b-4f6bc1a3f6e2\scratchpad\T10b`.

## Check
Build twice (identical), churn reverted (STATUS "Known churn" plus the T09/T09b additions in their STATUS rows), no meshes embedded in the scene, scene under 47 MB, commit by path, gate (`SC MARKER` = HEAD), play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores), push. Delete probe scripts before committing.

## Report (under 15 lines)
Commit; gate and play loop lines; build twice; scene MB; what the grey plane was and the fix; the blockage now; RoadOut foreground; what you could not do; Megascans wanted.
