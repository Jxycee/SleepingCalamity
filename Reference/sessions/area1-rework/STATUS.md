# Area 1 rework (Fable Run 6) STATUS

State: building, round 3 (resumed 2026-11-16 about 16:10 Central after the user restored usage: 5-hour 0%, weekly all-models 58%, Fable 0%; stop and push at weekly all-models about 92%). Brief unchanged: `side-session-notes/local_88bfb1c1-.../run6-fable-area1-brief.md`.
Branch `claude/area1-rework`, worktree `.claude/worktrees/elated-dubinsky-d77725`.
- Origin: round 2 at bff73c2e (gate PASS 1859/1859, marker = tip). Round 3's commit follows if its gate passed (see "Round 3").
Images used: 4 of 10 (refs + B3 sheet; baseline, R1 and R2 sheets).
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\9bc72263-ffd8-4546-b30f-19d24fe64b2b\scratchpad\run6` (`sheets\R0_baseline_sheet.jpg`, `R1_sheet.jpg`, `R2_sheet.jpg`, `R3_sheet.jpg`; captures `base`, `r1`, `r2`, `r3`; `sheet_round.py`). The 1280 px JPGs of every round are in the repo: `Reference/sessions/area1-rework/baseline|R1|R2|R3/`, so a sheet can be rebuilt after a reboot (`python sheet_round.py <S> R3 <dir of this round> <dir of the previous round> <repo root>`; it accepts JPG folders).

## Scores (strict /40, eye cameras)
| camera | R0 | R1 | R2 | R3 |
|---|---|---|---|---|
| Spawn | 14 | 18 | 21 | not judged |
| Postcard | 16 | 23 | 25 | not judged |
| West | 13 | 18 | 21 | not judged |
| Climb | 15 | 17 | 20 | not judged |
| BellYard | 9 | 11 | 16 | not judged |
| Balcony | 10 | 13 | 21 | not judged |
| Lake | 13 | 14 | broken (a belt tree trunk in front of the camera) | not judged |
Stop rule: 25/40 strict on the eye cameras, or 4 rounds (round 4 is the last).
Palette p10/p50/p90 (target .21-.25/.44-.50/.80-.86):
- R1: Spawn .29/.51/.70, Postcard .26/.45/.66, West .30/.49/.71, Climb .23/.45/.69, BellYard .06/.29/.59, Balcony .13/.51/.69, Lake .52/.66/.81.
- R2: Spawn .29/.52/.73, Postcard .26/.46/.70, West .30/.50/.76, Climb .20/.44/.71, BellYard .05/.26/.59, Balcony .17/.58/.72, Lake .04/.19/.71 (blocked).

## What round 1 did (61bc6134, on origin)
Bell Hill built into the terrain (crag bank + wooded hillside north of the keep), far ranges raised, tree prefabs TR01-TR07 adopted from CC0 Poly Haven scans (`AreaTreesIntake`, d7713de7; TR07 unused so far), `HollowmereWild.cs` (great tree in an earth pit at (-15, -11), two churchyard trees, town trees, wall belt, crag boulders and ferns, 262 3D hill trees, 4394 far card trees, square grass drift), lot trees in `HollowmereRuinDressing`, the town's own Morning light in `HollowmereLook.TownMorning` (sun Euler(33, -68), sky copy `M_HM_Sky_Morning`, ambient, exposure +0.25, fog 0.0062 warmer), balcony camera moved.
R1 verdict: the forested hill behind the tower works. Still flat: lavender cut-out ridges, a pale spike of a mountain, bare striped hill ground and a tiled-looking crag (the steep rule painted the cobble layer over the whole hill), a wall belt too thin to read (49), only 7 town trees, a lot tree blocking the balcony view, dull highlights.

## What round 2 did (bff73c2e, on origin)
`HollowmereTerrain.Splat` (hill floor to grass, cobble cut 85% on the hill), `HollowmereRoad.MidHaze` (less haze and cooling, more noise), `HollowmereBackdrop.MountainHeight` 900 -> 700, `HollowmereWild` (town trees on a 4 m grid: 42; belt denser and taller: 84; 81 rock outcrops half sunk in the crag face; `Clear` got `laneGap`, `colliders`), `HollowmereRuinDressing` (no lot trees under the balcony), `HollowmereLook` (`Sun`, `Contrast` +8, parse key `sun`).
R2 verdict: the ring of trees round the square now carries Spawn, West and Balcony (the balcony view is open and reads as a town in a wooded valley); the outcrops make the bell yard's crag read as rock (stratified slabs, a bit large in texture); the hill floor is no longer a tan sheet. Still weak: the far ridges are a flat lavender band with a pale spike of a mountain over the tree line; highlights dull (p90 .70-.76); the church's north wall in the bell yard is black (p10 .05); the ruined rows are trees over rubble with no standing walls; the red trapdoor marker is loud. `HM_Overview_Ruined.png` was not shot in R2 (the filter `HM_Overview_` matched nothing; use `HM_Overview`).

## Round 3 (edits written; see origin for whether it is built)
Edits: `HollowmereWild.WallBelt` (lake foreshore keep-clear back to x -45..60; belt trees 4.5 m from lane edges), `HollowmereLook.Morning` (`Sun` 1.22 -> 1.38, `Ambient` 0.62 -> 0.56), `HollowmereRoad.FarHaze` (less violet, a touch less haze, more noise), `HollowmereTerrain` grass layer `diffuseRemapMax` (0.94, 0.90, 0.76) -> (0.85, 0.84, 0.68) (the meadow read pale lime).
- **If `git log origin/claude/area1-rework` shows "Area 1 rework round 3"**: built, captured, gated and pushed; the sheet is `<S>\sheets\R3_sheet.jpg`; score it (image 5 of 10) and fill the table.
- **If not**: the edits are uncommitted in the worktree; run a round as below.

## Next steps, in order
1. Score R3.
2. Round 4 candidates, by expected gain (check each against the R3 sheet first):
   - standing ruin walls in the ruined rows (West and Spawn: the lots are trees over rubble; the ruin kit has wall stubs, see `HollowmereBuilder.BuildFoundation` and `HollowmereArt`);
   - the far ridge and the mountain if they still read as cut-outs (the ragged layers and the mountain material are in `HollowmereBackdrop.cs`: the `Layer` list, `Mat("M_BD_Mawrrow", ...)`; a tree line of cards along the mid ridge crest is the cheap fix);
   - BellYard: the church's north wall is black (a fill, or a lighter wall material; the look's `Ambient` is the global lever), the red trapdoor marker;
   - the lake shore (reeds and shore rocks from the LS kit, `M_LS_*`), and one framing tree for the lake placed by hand, at least 8 m off the gate road.
3. Tuning the light costs 1 minute per try: `SC_CAPTURE_NOBUILD=1` with `SC_LOOK_VARIANTS="a:sun=1.5,exposure=0.35;b:ambient=0.7"` (keys in `HollowmereLook.Look.Parse`; for Morning `exposure` and `contrast` are ADDED to the corner's profile, `ambient` is the sky ambient intensity, `sun` scales the sun).
4. Owed before the landing: a traversal check for the crag (no climb), tree trunks and the outcrops (`TraversalRegressionTests` passed in the gate on rounds 1 and 2; a dedicated crag case is not written), then the landing in the brief (final `gate`, the sheet to the user with SendUserFile, board `merge-ready` + `needs: coordinator`, the Sonnet session runs `check`, `reviewer`, fixes).
5. `Hollowmere.unity` is 57 MB (GitHub warns above 50 MB). The 262 hill trees, the 126 town and belt trees and the card tiles are the growth. If it must shrink: fewer 3D hill trees past z 70, or nest the hill forest in one prefab. For the coordinator to weigh at merge.

## How to run a round (worked three times, about 9 minutes each)
One Sonnet `implementer`, blocking commands, in this worktree: env `SC_CAPTURE_DIR=<S>\rN`, `SC_CAPTURE_ONLY=Eye_,HM_Overview`, `SC_CAPTURE_SUN=Morning`, `SC_CAPTURE_PRESET=Ruined`; `Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.HollowmereCapture.CaptureAll -OutDir <S>\rN_log` (3.6 min with the build); `python <S>\sheet_round.py <S> RN <S>\rN <S>\r(N-1) <repo root>`; commit by path (`Assets/_Project`, `Reference/sessions/area1-rework`); `gate` (5.5 min); push. Tell it not to open the images. Write STATUS before launching it and let its commit carry the file.

## Facts worth keeping
- Tests pin `Ground(0,30)` = 4 +-0.3 and the terraces (`HollowmereLayoutTests` 332-345); the hill starts north of z 29.5 everywhere (`HollowmereLayout.HillFoot`, `BackHill`).
- Town wall ends at z 16 on both flanks; north is open, the crag is the boundary there.
- Play camera: pivot feet + 1.5, boom 5, pitch 15 down, FOV 50: the ground fills most of the play frame.
- `SlopeLimit` 45 (`Core/Player/MovementMath.cs`). Parapet planters at x = -22.5 + 4.5k.
- The terrain has four layers in one pass (grass, soil, path, cobble): no fifth layer for rock or forest floor; steep faces stretch any layer, so rock comes from meshes (T11a/T11b boulders).
- The backdrop haze layers are `HollowmereRoad.NearHaze/GroundHaze/MidHaze/FarHaze`; the ragged far layers and the mountain are in `HollowmereBackdrop.cs`.
- Free ground inside the wall is scarce: `HollowmereWild.Clear` passes about 1 spot in 7.
- `Cap_HM_Eye_Lake` stands on the road out of the Lower Gate at (-21.5, -73): anything tall within about 5 m of that road blocks it.
- The language server reports `HollowmereWild`'s cross-file names as missing (the worktree's csproj is a copy of main's); real compile errors still show, which caught a duplicate `case` before a Unity run.

## Run 6 lessons
- Cost: first session start 5-hour 48% / weekly 75% / Fable 44%; restart 44% / 88% / 66% (the coordinator had quoted 0 / 82 / 57); at the pause 100% / 96% / 77%; the user then restored usage (0% / 58% / 0%). This restart: two rounds built and judged and a third written in about 3 hours; controller context 282k after round 2 (past the 250k handoff mark).
- What worked: STATUS as the only handoff (no wide re-reads; the restart needed about 20 greps); drafting new code outside `Assets/` while the gate ran; one implementer per round with the log lines to copy named in the brief; a sheet script written once and reused; position-hashed placement, so tuning one rule never moved the rest.
- The tree adoption was the big subagent cost (267k tokens, 29 minutes) and worth it: the scans' leaves had no alpha, and the agent's card-per-leaf-island method was its own find. Brief asset adoption with "say if the brief's method cannot work and pick another", which it did unprompted.
- Brief differently: give Fable the 5-hour budget at the start and a rule to write STATUS before each implementer launch, not after: the limit took the implementer mid-round and the handoff then had to be written inside a hold.
- guard.sh let the first push of a new branch through without a gate PASS, then blocked the next plain `git push`: a possible hole in the hook, for the coordinator.
- A capture filter with a trailing underscore (`HM_Overview_`) silently drops the camera: name filters without it.
- A keep-clear rule tuned blind (the lake foreshore in round 2) put a trunk in front of a camera and cost that camera a round: check every placement change against the capture camera positions before building.
