# Area 1 rework (Fable Run 6) STATUS

State: **paused for pace** (coordinator hold, 2026-11-16 about 15:00 Central: weekly all-models 96%, 5-hour 100%, Fable 77%). Brief unchanged: `side-session-notes/local_88bfb1c1-.../run6-fable-area1-brief.md`.
Branch `claude/area1-rework`, worktree `.claude/worktrees/elated-dubinsky-d77725`.
- Origin: round 1 at 61bc6134 (gate PASS, 1859/1859) plus this STATUS commit.
- **Round 2 is built and captured but NOT judged.** See "Where round 2 stands" for whether its commit reached origin.
Images used: 3 of 10 (refs + B3 sheet; baseline sheet; R1 sheet). The R2 sheet is made and unread.
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\9bc72263-ffd8-4546-b30f-19d24fe64b2b\scratchpad\run6` (`sheets\R0_baseline_sheet.jpg`, `R1_sheet.jpg`, `R2_sheet.jpg`; captures `base`, `r1`, `r2`; `sheet_round.py`). The 1280 px JPGs of every round are in the repo: `Reference/sessions/area1-rework/baseline|R1|R2/`, so a sheet can be rebuilt after a reboot (`python sheet_round.py <S> R2 <dir of this round> <dir of the previous round> <repo root>`; it accepts JPG folders).

## Scores (strict /40, eye cameras)
| camera | R0 | R1 | R2 |
|---|---|---|---|
| Spawn | 14 | 18 | not judged |
| Postcard | 16 | 23 | not judged |
| West | 13 | 18 | not judged |
| Climb | 15 | 17 | not judged |
| BellYard | 9 | 11 | not judged |
| Balcony | 10 | 13 | not judged |
| Lake | 13 | 14 | not judged (broken, below) |
Stop rule: 25/40 on the eye cameras, or 4 rounds.
Palette p10/p50/p90 (target .21-.25/.44-.50/.80-.86):
- R1: Spawn .29/.51/.70, Postcard .26/.45/.66, West .30/.49/.71, Climb .23/.45/.69, BellYard .06/.29/.59, Balcony .13/.51/.69, Lake .52/.66/.81.
- R2: Spawn .29/.52/.73, Postcard .26/.46/.70, West .30/.50/.76, Climb .20/.44/.71, BellYard .05/.26/.59, Balcony .17/.58/.72, Lake **.04/.19/.71**.

## What round 1 did (61bc6134, on origin)
Bell Hill built into the terrain (crag bank + wooded hillside north of the keep), far ranges raised, tree prefabs TR01-TR07 adopted from CC0 Poly Haven scans (`AreaTreesIntake`, d7713de7; TR07 unused so far), `HollowmereWild.cs` (great tree in an earth pit at (-15, -11), two churchyard trees, town trees, wall belt, crag boulders and ferns, 262 3D hill trees, 4394 far card trees, square grass drift), lot trees in `HollowmereRuinDressing`, the town's own Morning light in `HollowmereLook.TownMorning` (sun Euler(33, -68), sky copy `M_HM_Sky_Morning`, ambient 0.62, exposure +0.25, fog 0.0062 warmer), balcony camera moved.
R1 verdict: the forested hill behind the tower works (Postcard 23). Still flat: lavender cut-out mid ridges, a pale spike of a mountain, bare striped hill ground and a tiled-looking crag (the steep rule painted the cobble layer over the whole hill), a wall belt too thin to read (49), only 7 town trees, a lot tree blocking the balcony view, dull highlights.

## Where round 2 stands
Round 2 edits (all in `Assets/_Project/Editor/Builders/`): `HollowmereTerrain.Splat` (hill floor to grass, cobble cut 85% on the hill), `HollowmereRoad.MidHaze` (less haze and cooling, more noise), `HollowmereBackdrop.MountainHeight` 900 -> 700, `HollowmereWild` (town trees on a 4 m grid: 42; belt denser and taller: 84; 81 rock outcrops half sunk in the crag face; `Clear` got `laneGap`, `colliders`), `HollowmereRuinDressing` (no lot trees under the balcony), `HollowmereLook` (`Sun` 1.22, `Contrast` +8, parse key `sun`).
The build + capture passed (`SC HOLLOWMERE wild: 3 old trees, 42 town trees, 84 belt trees and shrubs, 262 hill trees (3D), 4394 far card trees, 105 crag boulders, 83 ferns, 60 square tufts`), then the implementer hit the 5-hour limit before the sheet, the commit and the gate. The controller made the sheet and the commit by hand.
- **If `git log origin/claude/area1-rework` shows "Area 1 rework round 2"**: the gate passed and it is pushed.
- **If not**: the commit exists only in the worktree (local branch ahead of origin). Run `gate` through this worktree's `Tools\unity.ps1` with ONE Sonnet implementer and push (guard.sh needs the PASS on HEAD). No gate has run on the round 2 code; it compiled and built.
- `HM_Overview_Ruined.png` was not shot in R2 (the `HM_Overview_` filter matched nothing; use `HM_Overview`), so the R2 sheet has no overview tile.

## Next steps, in order
1. Read `R2_sheet.jpg` (image 4 of 10) and score it.
2. Known before looking: **the Lake camera is blocked** (p50 .19): a belt tree stands in front of `Cap_HM_Eye_Lake` at (-21.5, -73) looking toward (6, -100). Round 2 narrowed the foreshore keep-clear in `HollowmereWild.WallBelt` from x -45..60 to x -8..50 (z < -62) to frame the lake. Fix: keep the view cone clear (no belt tree within about 14 m of the camera or inside the cone from it to x -8..50 at the shore), or go back to x -45..60 and add two chosen framing trees.
3. Round 3 candidates, by expected gain (check each against the R2 sheet first): standing ruin walls in the ruined rows (West, Spawn still read as an empty lot field); BellYard (9 -> 11): the church's north wall is black (p10 .05), the crag needs to read as rock, the red trapdoor marker is loud; lake shore (reeds, shore rocks, richer meadow colour: the terrain grass is pale lime and fills the play frame); ridge silhouettes (tree-line cards along the mid ridge crest if it still reads as a cut-out); highlights still under target (p90 .70-.76 after `Sun` 1.22).
4. Tuning the light costs 1 minute per try: `SC_CAPTURE_NOBUILD=1` with `SC_LOOK_VARIANTS="a:sun=1.4,exposure=0.35;b:ambient=0.75"` (keys in `HollowmereLook.Look.Parse`; for Morning `exposure` and `contrast` are ADDED to the corner's profile, `ambient` is the sky ambient intensity).
5. Owed before the landing: a traversal check for the crag (no climb), tree trunks and the outcrops (`TraversalRegressionTests` passed on round 1; round 2 is unchecked), then the landing in the brief (final `gate`, sheet to the user, board `merge-ready`, the Sonnet session runs `check`, `reviewer`, fixes).
6. `Hollowmere.unity` is now 57-60 MB (GitHub warns above 50 MB; it is LFS-less YAML). The 262 hill trees and 4394 card tiles are the growth. If it must shrink: fewer 3D hill trees past z 70, or nest the hill forest in one prefab. For the coordinator to weigh at merge.

## How to run a round (worked twice, about 9 minutes each)
One Sonnet `implementer`, blocking commands, in this worktree: env `SC_CAPTURE_DIR=<S>\rN`, `SC_CAPTURE_ONLY=Eye_,HM_Overview`, `SC_CAPTURE_SUN=Morning`, `SC_CAPTURE_PRESET=Ruined`; `Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll -OutDir <S>\rN_log` (3.6 min with the build); `python <S>\sheet_round.py <S> RN <S>\rN <S>\r(N-1) <repo root>`; commit by path (`Assets/_Project`, `Reference/sessions/area1-rework/RN`); `gate` (5.5 min); push. Tell it not to open the images.

## Facts worth keeping
- Tests pin `Ground(0,30)` = 4 +-0.3 and the terraces (`HollowmereLayoutTests` 332-345); the hill starts north of z 29.5 everywhere (`HollowmereLayout.HillFoot`, `BackHill`).
- Town wall ends at z 16 on both flanks; north is open, the crag is the boundary there.
- Play camera: pivot feet + 1.5, boom 5, pitch 15 down, FOV 50: the ground fills most of the play frame.
- `SlopeLimit` 45 (`Core/Player/MovementMath.cs`). Parapet planters at x = -22.5 + 4.5k.
- The terrain has four layers in one pass (grass, soil, path, cobble): no fifth layer for rock or forest floor; steep faces stretch any layer, so rock comes from meshes (T11a/T11b boulders).
- The backdrop haze layers are `HollowmereRoad.NearHaze/GroundHaze/MidHaze/FarHaze`; the ragged far layers and the mountain are in `HollowmereBackdrop.cs`.
- Free ground inside the wall is scarce: `HollowmereWild.Clear` passes about 1 spot in 7.
- The language server reports `HollowmereWild`'s cross-file names as missing (the worktree's csproj is a copy of main's); real compile errors still show, which caught a duplicate `case` before a Unity run.

## Run 6 lessons
- Cost: first session start 5-hour 48% / weekly 75% / Fable 44%; restart 44% / 88% / 66% (the coordinator had quoted 0 / 82 / 57); at the pause 100% / 96% / 77%. This restart: two rounds built, one judged, in about 2.5 hours; controller context about 250k at the pause.
- What worked: STATUS as the only handoff (no wide re-reads; the restart needed about 20 greps); drafting new code outside `Assets/` while the gate ran; one implementer per round with the log lines to copy named in the brief; a sheet script written once and reused; position-hashed placement, so tuning one rule never moved the rest.
- The tree adoption was the big subagent cost (267k tokens, 29 minutes) and worth it: the scans' leaves had no alpha, and the agent's card-per-leaf-island method was its own find. Brief asset adoption with "say if the brief's method cannot work and pick another", which it did unprompted.
- Brief differently: give Fable the 5-hour budget at the start and a rule to write STATUS before each implementer launch, not after: the limit took the implementer mid-round and the handoff then had to be written inside a hold.
- guard.sh let the first push of a new branch through without a gate PASS, then blocked the next plain `git push`: a possible hole in the hook, for the coordinator.
- A capture filter with a trailing underscore (`HM_Overview_`) silently drops the camera: name filters without it.
