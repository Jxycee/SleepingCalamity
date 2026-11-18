# T11b brief: floating mountain, dark HillWest, sheet tool (Area 1 world, Run 7)

Goal: fix the two faults T11 left and the sheet tool. Read `briefs/T11_brief.md` and the T11 row in STATUS (commits 120bf779, 02a13b18, bdc7b859); this brief adds and overrides. Builders: `HollowmereBackdrop.cs` (Mawrrow mountain, far range, hills), `HollowmereZones.cs` (Z7 west), `HollowmereLook.cs` / T11's fills; tool `Reference/sessions/area1-world/tools/sheet_area.py`.

Branch: `claude/area1-world`, worktree `C:\Dev\SleepingCalamity\.claude\worktrees\elated-dubinsky-d77725`, the tip (41526d57 or later). Uncommitted `Reference/sessions/area1-world/STATUS.md` and this brief from the controller: include both unchanged in your commit, never edit STATUS.md. Do not merge main.

Controller's read of `R14/`:
- The Mawrrow mountain shows as a pale grey crumpled peak floating above the hill ridge at `Eye_Spawn` (top left), `Eye_Balcony` (top right) and `Eye_MarketStreet` (top right): its foot is not behind anything, sky shows under or around it, and its silhouette reads as a crumpled sheet, not a mountain. T11 hazed it and widened it; it still floats.
- `Eye_HillWest` is still the darkest eye view (whole-frame p50 .255): the grassed slope reads well but the revetment and the hill face behind it sink into shade.

## Do
1. **Probe the mountain** (an editor query plus one reduced capture of Spawn): its position, height, base height against the far range's crest from the three cameras, its mesh silhouette. Then fix it so that from every eye camera its foot is hidden behind the far range or hills (lower it, push it back and scale it, raise the range in front, or replace it with a properly shaped ridge mesh), its silhouette reads as rock and snow or forest at that distance (a soft, aerial-perspective shape, never a crumpled sheet), and no sky shows under it. If no placement works from all eye cameras, remove it and say so.
2. **HillWest**: lift the shade so the revetment and hill face read as form (Z7 west strength, the HillWestFill position/intensity/range, a second soft fill if under the cap of 12), without washing out the sunlit grass. Target whole-frame p50 about .30 and no large crushed area (support numbers, `sc_scene_diag.py` as T11 ran it).
3. **Sheet tool**: `tools/sheet_area.py` lays out every eye camera and draws a blank "MISSING" panel for each one the round did not capture, so sheets for partial rounds look empty to the user. Make it lay out only the cameras present in the round's PNG dir (keep the reference row and the before inset when the before frame exists; skip the panel otherwise; no blank panels). Re-make the R14 sheets with it as a check that they still look right, and use it for R14c.

## Capture (at most two rounds)
Iterate with `SC_CAPTURE_ONLY=Eye_Spawn,Eye_Balcony,Eye_MarketStreet,Eye_HillWest` (`SC_CAPTURE_PRESET=Ruined`, `SC_CAPTURE_SUN=Morning`). Final from the committed code: the same four plus `Eye_Postcard,Eye_RoadOut,Eye_Meadow` (the other views that show the backdrop) into `R14c/`; sheet `python Reference/sessions/area1-world/tools/sheet_area.py R14c <final PNG dir> Reference/sessions/area1-world/R14 Reference/sessions/area1-world/R14c`. Put each final frame beside its R14 before and ask honestly which is better. Scratch: `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\1635b7dd-88db-474e-8e1b-4f6bc1a3f6e2\scratchpad\T11b`.

## Check
Build twice (identical), churn reverted (STATUS "Known churn" plus the additions in the T09-T11 rows), no meshes embedded in the scene, scene under 47 MB, commit by path, gate (`SC MARKER` = HEAD), play loop `-Filter "HollowmereHub|LoopBot|LoopGuideHud|SquareLoopWalkQa|TraversalRegression"` (11 passed, 7 ignores), push. Delete probe scripts before committing.

## Report (under 12 lines)
Commit; gate and play loop lines; build twice; scene MB; what the mountain was and the fix (or removal); HillWest numbers before and after; the sheet tool change; what you could not do.
