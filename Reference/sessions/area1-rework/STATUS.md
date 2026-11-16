# Area 1 rework (Fable Run 6) STATUS

State: **context handoff written 2026-11-16 about 17:10 Central** (controller context about 300k, past the 250k mark; the coordinator restarts the session). Round 3 is judged; round 4 (the last) is written and unbuilt. Usage at resume 16:10: 5-hour 0%, weekly all-models 58%, Fable 0%; stop and push at weekly all-models about 92%. Brief unchanged: `side-session-notes/local_88bfb1c1-.../run6-fable-area1-brief.md`.
Branch `claude/area1-rework`, worktree `.claude/worktrees/elated-dubinsky-d77725`.
- Origin: round 3 at 85a941bd (gate PASS 1859/1859, marker = tip) plus this STATUS commit.
- Local only, one commit ahead of origin: "Area 1 rework round 4 WIP" (three builder edits, never compiled or built; guard.sh blocks its push until a gate PASS).
Images used: 5 of 10 (refs + B3 sheet; baseline, R1, R2 and R3 sheets).
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\9bc72263-ffd8-4546-b30f-19d24fe64b2b\scratchpad\run6` (`sheets\R0_baseline_sheet.jpg`, `R1_sheet.jpg`, `R2_sheet.jpg`, `R3_sheet.jpg`; captures `base`, `r1`, `r2`, `r3`, logs `rN_log`, `gateN`; `sheet_round.py`). The 1280 px JPGs of every round are in the repo: `Reference/sessions/area1-rework/baseline|R1|R2|R3/`, so a sheet can be rebuilt after a reboot (`python sheet_round.py <S> R3 <dir of this round> <dir of the previous round> <repo root>`; it accepts JPG folders).

## Scores (strict /40, eye cameras)
| camera | R0 | R1 | R2 | R3 |
|---|---|---|---|---|
| Spawn | 14 | 18 | 21 | 21 |
| Postcard | 16 | 23 | 25 | 25 |
| West | 13 | 18 | 21 | 22 |
| Climb | 15 | 17 | 20 | 20 |
| BellYard | 9 | 11 | 16 | 16 |
| Balcony | 10 | 13 | 21 | 21 |
| Lake | 13 | 14 | broken (a belt tree trunk in front of the camera) | 15 |
Stop rule: 25/40 strict on the eye cameras, or 4 rounds (round 4 is the last).
Palette p10/p50/p90 (target .21-.25/.44-.50/.80-.86):
- R1: Spawn .29/.51/.70, Postcard .26/.45/.66, West .30/.49/.71, Climb .23/.45/.69, BellYard .06/.29/.59, Balcony .13/.51/.69, Lake .52/.66/.81.
- R3: Spawn .30/.53/.74, Postcard .26/.47/.71, West .30/.51/.77, Climb .20/.45/.73, BellYard .05/.24/.60, Balcony .16/.59/.73, Lake .53/.69/.84.
- R2: Spawn .29/.52/.73, Postcard .26/.46/.70, West .30/.50/.76, Climb .20/.44/.71, BellYard .05/.26/.59, Balcony .17/.58/.72, Lake .04/.19/.71 (blocked).

## What round 1 did (61bc6134, on origin)
Bell Hill built into the terrain (crag bank + wooded hillside north of the keep), far ranges raised, tree prefabs TR01-TR07 adopted from CC0 Poly Haven scans (`AreaTreesIntake`, d7713de7; TR07 unused so far), `HollowmereWild.cs` (great tree in an earth pit at (-15, -11), two churchyard trees, town trees, wall belt, crag boulders and ferns, 262 3D hill trees, 4394 far card trees, square grass drift), lot trees in `HollowmereRuinDressing`, the town's own Morning light in `HollowmereLook.TownMorning` (sun Euler(33, -68), sky copy `M_HM_Sky_Morning`, ambient, exposure +0.25, fog 0.0062 warmer), balcony camera moved.
R1 verdict: the forested hill behind the tower works. Still flat: lavender cut-out ridges, a pale spike of a mountain, bare striped hill ground and a tiled-looking crag (the steep rule painted the cobble layer over the whole hill), a wall belt too thin to read (49), only 7 town trees, a lot tree blocking the balcony view, dull highlights.

## What round 2 did (bff73c2e, on origin)
`HollowmereTerrain.Splat` (hill floor to grass, cobble cut 85% on the hill), `HollowmereRoad.MidHaze` (less haze and cooling, more noise), `HollowmereBackdrop.MountainHeight` 900 -> 700, `HollowmereWild` (town trees on a 4 m grid: 42; belt denser and taller: 84; 81 rock outcrops half sunk in the crag face; `Clear` got `laneGap`, `colliders`), `HollowmereRuinDressing` (no lot trees under the balcony), `HollowmereLook` (`Sun`, `Contrast` +8, parse key `sun`).
R2 verdict: the ring of trees round the square now carries Spawn, West and Balcony (the balcony view is open and reads as a town in a wooded valley); the outcrops make the bell yard's crag read as rock (stratified slabs, a bit large in texture); the hill floor is no longer a tan sheet. Still weak: the far ridges are a flat lavender band with a pale spike of a mountain over the tree line; highlights dull (p90 .70-.76); the church's north wall in the bell yard is black (p10 .05); the ruined rows are trees over rubble with no standing walls; the red trapdoor marker is loud. `HM_Overview_Ruined.png` was not shot in R2 (the filter `HM_Overview_` matched nothing; use `HM_Overview`).

## What round 3 did (85a941bd, on origin)
`HollowmereWild.WallBelt` (lake foreshore keep-clear back to x -45..60; belt trees 4.5 m from lane edges: 73 belt trees), `HollowmereLook.Morning` (`Sun` 1.38, `Ambient` 0.56), `HollowmereRoad.FarHaze`, the grass layer's `diffuseRemapMax` darker.
R3 verdict: the lake view is open again and its ridges read green-grey (15). Everything else is R2 within a point: the stronger sun moved p90 only .01-.02 (the tonemapper holds it; exposure is the lever, not the sun). **The lavender band is not the `FarRidge` annulus that round 3 tuned: it is `HollowmereBackdrop`'s `FarRange` layer** (`Cool = 1.4` at full amount gives a violet haze colour), still there in Spawn, West and Balcony, with the pale pink mountain over it. The lake's pale foreground is the worn-path terrain layer (the road out of the gate), not the grass.

## Round 4 (the last; edits written, committed locally as WIP, never compiled or built)
Edits: `HollowmereBackdrop.cs`: `FarRange` `Cool` 1.4 -> 1.05, `CoolAmt` 1.0 -> 0.7 (a blue-grey, one step past `HillRidge`); `M_BD_Mawrrow` cool 1.3 -> 1.0, key boost 2.0 -> 1.4, fill boost 2 -> 1.5 (a darker massif). `HollowmereTerrain.cs`: path layer `layers[2].diffuseRemapMax` (0.82, 0.78, 0.70).
Worth adding before the build, if the next session agrees (each is small):
- `HollowmereLook.Morning`: `Exposure` 0.25 -> about 0.38 to lift p90 toward .80 (p50 is at target on Postcard and Climb, over it on Balcony and Lake: try it first as a 1-minute variant, `SC_LOOK_VARIANTS="a:exposure=0.38;b:exposure=0.32,ambient=0.5"` with `SC_CAPTURE_NOBUILD=1`).
- BellYard (16, the lowest): the church's north wall is black (p10 .05). A soft fill for that yard (a shadowless point or spot light of low intensity near (10, 6.5, 30)) or a lighter wall material; and tone the red trapdoor marker down if it is art, not a gameplay beacon (check before touching).
- Standing ruin walls in the ruined rows would lift West and Spawn most, but that is asset work for a later batch, not this round.

## Next steps, in order
1. One implementer: build + capture R4 + sheet + commit + gate + push ("How to run a round"; previous round dir `r3`; the scratch still holds everything unless the PC rebooted). The WIP commit and the build commit push together.
2. Score R4 (image 6 of 10), fill the table. That ends the rounds (4 of 4) whatever the scores.
3. Landing, per the brief: the gate from step 1 counts if nothing changed after it; send the final sheet to the user with SendUserFile; board profile `merge-ready`, `needs: coordinator`; `get_usage`; complete "Run 6 lessons"; stop. Do not edit `CURRENT.md`.
4. For the Sonnet landing session (write this into its brief): the full `check`; `reviewer` on the branch diff (builders, Core `HollowmereLayout.BackHill`/`HillFoot`, `SaltwakeRoadPlan.FieldWeight`, the intake); a dedicated traversal case for the crag (no climb at the foot line, `SlopeLimit` 45), the tree trunks and the outcrops (the regression set passed in every gate, but nothing probes the new relief on purpose); the PL loop tests in `check`'s PlayMode tier; the scene size (57 MB, GitHub warns above 50 MB: fewer 3D hill trees past z 70, or the hill forest as one nested prefab).
5. Not done, for the user's eye at the review: no standing walls in the ruined rows; the bell yard's dark wall; the lake shore has no reeds or rocks from the LS kit; the capture light is Morning only (Evening and Midday keep the corner's looks and were not re-shot with the trees).

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
- Cost: first session start 5-hour 48% / weekly 75% / Fable 44%; restart 44% / 88% / 66% (the coordinator had quoted 0 / 82 / 57); at the pause 100% / 96% / 77%; the user then restored usage (0% / 58% / 0%). This restart: three rounds built and judged and the fourth written in about 4 hours; controller context 282k after round 2, about 300k at this handoff. Subagents: gate 79k, trees 267k, rounds 86k + 78k (gate only, after the limit cut one) + 83k tokens.
- What worked: STATUS as the only handoff (no wide re-reads; the restart needed about 20 greps); drafting new code outside `Assets/` while the gate ran; one implementer per round with the log lines to copy named in the brief; a sheet script written once and reused; position-hashed placement, so tuning one rule never moved the rest.
- The tree adoption was the big subagent cost (267k tokens, 29 minutes) and worth it: the scans' leaves had no alpha, and the agent's card-per-leaf-island method was its own find. Brief asset adoption with "say if the brief's method cannot work and pick another", which it did unprompted.
- Brief differently: give Fable the 5-hour budget at the start and a rule to write STATUS before each implementer launch, not after: the limit took the implementer mid-round and the handoff then had to be written inside a hold.
- guard.sh let the first push of a new branch through without a gate PASS, then blocked the next plain `git push`: a possible hole in the hook, for the coordinator.
- A capture filter with a trailing underscore (`HM_Overview_`) silently drops the camera: name filters without it.
- A keep-clear rule tuned blind (the lake foreshore in round 2) put a trunk in front of a camera and cost that camera a round: check every placement change against the capture camera positions before building.
- Tuning a backdrop colour blind costs a round when the wrong layer is tuned (round 3 changed `FarRidge`; the band was `FarRange`). Before tuning by eye, find which object draws the thing: one capture with the suspect layer disabled, or read its material colour.
