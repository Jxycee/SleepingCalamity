# T09b brief: D2/D3 fix round and scene diet (Area 1 world, Run 7)

Goal: finish T09 (b978d4d2) so its final frames show the real state, fix the faults the controller sees in R12, and get the scene back under 46 MB before T10 adds the road. Read `briefs/T09_brief.md` (the T09 brief, kit, rules, churn list) and its commit messages (`git log -2`); this brief adds and overrides. Builders: `HollowmereBellYard.cs` (T09's), `HollowmereWild.cs`, `HollowmereTerrain.cs`, `GeneratedMeshes` (T03's externalize pass).

Branch: `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, the tip (b978d4d2 or later). Uncommitted `Reference/sessions/area1-world/STATUS.md` and this brief from the controller: include both unchanged in your commit, never edit STATUS.md. Leave `briefs/T10_brief.md` alone (untracked, not yours). Do not merge main.

Controller's read of `R12/` (frames taken before T09's last scarp and south-bank edits):
- BellYard: the slope boulders read as flat yellow-green slabs laid on the dirt with hard cast shadows (pasted cards, not sunk rocks); the big rock at the right edge has a bright glassy rim (a fresnel or specular edge: probe the material); the slope is a large bare dirt plane; the dive kit and hatch are too small to read from the camera (the camera may move up to 3 m closer or turn, as T07c did, if that frames the hatch and kit; keep the dive collider untouched).
- Churchyard: graves, wall and grass read well; faults: a flat bright lime-green rectangle on the slope right of the tree (moss decal or a plane: probe), dark silvery glossy plants at the lower right (T21 plants in shade: identify which and fix the material or swap the plant), the wall coping at the bottom reads as a wet black glossy slab.
- HillWest: R12 shows the pre-fix two-course T19e/f wall as black T-beams; the current state is uncaptured. Climb: the south-bank dressing is uncaptured.

## Do
1. **Probe, then fix** (no fix by guess): the glassy rock rim, the lime-green patch, the glossy dark plants, the black coping. Rule for all scanned rocks and plants in D2/D3: matte, no bright rims, no near-black in shade.
2. **Slope rocks**: sink every boulder on the BellYard and churchyard slopes by a third to half its height into the slope, align to the slope normal with a small random tilt, mix sizes, add fern/grass rings at their feet, and break the bare dirt plane (a fern and grass detail band on the slope, a few roots, scree at the foot). The slope must read as hillside, not a dirt ramp.
3. **BellYard framing**: the hatch and the dive kit must be readable from `Eye_BellYard` (move or turn the camera within 3 m, or move the kit toward the camera's line; bigger brazier light pool if it helps). The view should make you want to walk to the hatch.
4. **HillWest scarp**: whatever stands there now must read as an old dry-stone scarp wall with ivy and grass at the foot, never black. If the single course still reads as beams, replace it with N03/T20d-h rocks and T19 skirt pieces set as a rough revetment.
5. **Scene diet (required, before anything else is added)**: the scene is 49.70 MB against a hard limit of 50 and a target of 45; T10 must have room. Probe what makes up the scene file (embedded meshes: 202 remain, against rule 3.7; terrain-detail or decal serialisation; per-instance data), then externalize the embedded meshes through `GeneratedMeshes` (as T03 did) or turn repeated builder objects into prefab instances, until the scene is under 46 MB. Build stays byte-identical across two builds; the three-camera blurred diff for views you did not mean to change stays under 0.6/255 (as T03 measured).
6. Colliders unchanged except where you move rocks (keep big rocks solid). The play loop and TraversalRegression must stay green.

## Capture (at most three rounds)
Iterate with `SC_CAPTURE_ONLY=Eye_BellYard,Eye_Churchyard,Eye_HillWest` (`SC_CAPTURE_PRESET=Ruined`, `SC_CAPTURE_SUN=Morning`). The final round captures `Eye_BellYard,Eye_Churchyard,Eye_HillWest,Eye_Climb,Eye_Balcony,Eye_Postcard` from the committed code and replaces the frames in `R12/` (the R11b before set stays); re-run the sheet: `python Reference/sessions/area1-world/tools/sheet_area.py R12 <final PNG dir> Reference/sessions/area1-world/R11b Reference/sessions/area1-world/R12`. Put each final frame beside its R11b before and ask honestly which is the better world view. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\1635b7dd-88db-474e-8e1b-4f6bc1a3f6e2\scratchpad\T09b`.

## Check
Build twice (identical), churn reverted (STATUS "Known churn" plus `ShopSign_Inn.fbx.meta`, `M_HM_M_T08b_Rot.mat`, `M_HM_M_T08d_Rotten.mat`), commit by path (code, generated assets, `R12/`, STATUS.md, this brief), gate (`SC MARKER` = HEAD), play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores, as T09 reported), push.

## Report (under 15 lines)
Commit; gate and play loop lines; build twice; scene MB before and after and what the diet changed; embedded mesh count; causes and fixes for the rim, patch, plants and coping; what BellYard, Churchyard, HillWest now show; camera moves; what you could not do.
