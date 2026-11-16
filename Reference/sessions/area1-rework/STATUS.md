# Area 1 rework (Fable Run 6) STATUS

State: **rounds done (4 of 4), merge-ready for the coordinator; Fable stops here** (2026-11-16 about 18:00 Central). The whole first area changes, so the user looks before it merges. Brief: `side-session-notes/local_88bfb1c1-.../run6-fable-area1-brief.md`.
Archive: ready (once the landing session below is briefed).
Branch `claude/area1-rework`, worktree `.claude/worktrees/elated-dubinsky-d77725`.
- Origin: round 4 at aace1f69 (`gate` PASS 1859/1859, marker = that tip) plus this STATUS commit. Nothing local is unpushed.
- Final sheet (sent to the user): `Reference/sessions/area1-rework/R4/R4_sheet.jpg` (reference row, the seven eye cameras and the overview, round 3 as insets). Per-camera JPGs of every round: `baseline|R1|R2|R3|R4/`.
Images used: 6 of 10.
Scratch (a reboot clears it): `C:\Users\jayce\AppData\Local\Temp\claude\602ec13e15d252cb\9bc72263-ffd8-4546-b30f-19d24fe64b2b\scratchpad\run6` (`sheets\R0_baseline_sheet.jpg`, `R1_sheet.jpg`, `R2_sheet.jpg`, `R3_sheet.jpg`; captures `base`, `r1`, `r2`, `r3`, logs `rN_log`, `gateN`; `sheet_round.py`). The 1280 px JPGs of every round are in the repo: `Reference/sessions/area1-rework/baseline|R1|R2|R3/`, so a sheet can be rebuilt after a reboot (`python sheet_round.py <S> R3 <dir of this round> <dir of the previous round> <repo root>`; it accepts JPG folders).

## Scores (strict /40, eye cameras)
| camera | R0 | R1 | R2 | R3 | R4 (final) |
|---|---|---|---|---|---|
| Spawn | 14 | 18 | 21 | 21 | 22 |
| Postcard | 16 | 23 | 25 | 25 | 25 |
| West | 13 | 18 | 21 | 22 | 22 |
| Climb | 15 | 17 | 20 | 20 | 20 |
| BellYard | 9 | 11 | 16 | 16 | 17 |
| Balcony | 10 | 13 | 21 | 21 | 22 |
| Lake | 13 | 14 | broken (a belt tree trunk in front of the camera) | 15 | 15 |
Stopped on the round limit (4 of 4), not on the score: only Postcard reached 25. Mean 12.9 -> 20.4. The user reads our numbers 8-10 low.
Palette p10/p50/p90 (target .21-.25/.44-.50/.80-.86):
- R1: Spawn .29/.51/.70, Postcard .26/.45/.66, West .30/.49/.71, Climb .23/.45/.69, BellYard .06/.29/.59, Balcony .13/.51/.69, Lake .52/.66/.81.
- R4: Spawn .29/.53/.74, Postcard .26/.47/.71, West .30/.51/.77, Climb .20/.45/.73, BellYard .06/.26/.61, Balcony .16/.59/.72, Lake .53/.69/.84.
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

## What round 4 did (aace1f69, on origin)
`HollowmereBackdrop.cs`: `FarRange` `Cool` 1.4 -> 1.05, `CoolAmt` 1.0 -> 0.7; `M_BD_Mawrrow` cool 1.3 -> 1.0, key boost 2.0 -> 1.4, fill boost 2 -> 1.5. `HollowmereTerrain.cs`: path layer `diffuseRemapMax` (0.82, 0.78, 0.70). `HollowmereBuilder.BuildScene`: `BellYardFill`, a weak shadowless point light at (9, keep + 2.6, 30.2).
R4 verdict: the far range is blue-grey now, no longer lavender (Spawn and Balcony +1). The mountain is greyer but still a faceted pale spike. The bell yard fill barely shows (p10 .05 -> .06): intensity 5 at range 11 is too weak, or the wall material itself is near black. The lake foreground did not change (p50 .69): it is not the worn-path layer either; which layer or material draws the pale bank is still not found.

## What is still weak (for the user's eye, and for a later batch)
1. Ruined rows: trees over rubble, no standing walls. West and Spawn stay at 22 until the lots carry wall stubs with height.
2. Bell yard (17): the church's north wall is near black; the trapdoor marker is a loud red.
3. Lake (15): a pale flat bank in the foreground (cause not found: check the terrain splat near (-15, -80) and whether a road or shore mesh lies there), no reeds or shore rocks, the far shore a thin line.
4. The Mawrrow mountain reads as a low-poly spike at any colour: its mesh needs a broader, slumped shape (`HollowmereBackdrop`, the massif builder near `Mat("M_BD_Mawrrow"`), or more haze.
5. Highlights under target everywhere (p90 .71-.77 against .80-.86) with p50 on or over target: more exposure would wash the mid-tones, so this wants a tone curve change (shoulder), not exposure or sun.
6. The crag outcrops are 2-5 m copies of two boulders: fine from the walk, repetitive from the overview.
7. Only Morning was shot. Evening and Midday keep the corner's looks and were never captured with the trees and the hill.

## Next steps (not Fable)
1. Coordinator: the user looks at the sheet and the branch; merge only after the user's OK.
2. Sonnet landing session (its brief should carry this list):
   - full `check` on the tip (PlayMode included: the PL loop tests, `HollowmereRuinSeal`, stairs and traversal);
   - `reviewer` on the branch diff `origin/main...claude/area1-rework`: `Core/Town/HollowmereLayout.cs` (`HillFoot`, `BackHill`), `Core/Town/SaltwakeRoadPlan.cs` (`FieldWeight`), the builders `HollowmereWild`, `HollowmereTerrain`, `HollowmereRoad`, `HollowmereBackdrop`, `HollowmereLook`, `HollowmereRuinDressing`, `HollowmereBuilder`, and `AreaTreesIntake`;
   - a dedicated traversal case for the new relief: nobody climbs the crag from the foot line (`SlopeLimit` 45), no stuck pocket between an outcrop and the crag, every path node still reachable with the tree trunk capsules in (the regression set passed in all five gates, but nothing probes the hill on purpose);
   - a play walk of the loop in the built scene (spawn, build point, bell yard, dive, back): the great tree's pit at (-15, -11) has a low mesh collider, 42 town trees and 73 belt trees and shrubs stand with trunk capsules on the trees;
   - performance on the PC at 1440p from the balcony and the postcard views: 262 3D hill trees (LOD0 20k triangles each, LODGroup 0.35 / 0.12 / 0.02), about 160 more in and round the town, dense terrain grass on the hill now that its splat is grass;
   - the scene file is 57 MB (GitHub warns above 50 MB): fewer 3D hill trees past z 70, or the hill forest as one nested prefab;
   - TR07 is adopted but unused; `M_HM_Sky_Morning` and `HM_Morning_Profile` are new town-only assets; the `BellYardFill` light stays on in every light preset.
3. Then merge by `sc-merge` (the first area changes: ask first).

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
- Cost: first session start 5-hour 48% / weekly 75% / Fable 44%; restart 44% / 88% / 66% (the coordinator had quoted 0 / 82 / 57); at the pause 100% / 96% / 77%; the user then restored usage (0% / 58% / 0%). End of run (the user had restored usage to 0% / 58% / 0%): 26% / 62% / 6%, so rounds 3 and 4 with their judging and the landing cost about 4 points of weekly all-models. This restart: four rounds built and judged in about 5 hours; controller context 282k after round 2, 343k at the end (the coordinator chose finishing here over a restart for the last 3 turns). Subagents: gate 79k, trees 267k, rounds 86k + 78k (gate only, after the limit cut one) + 83k + 82k tokens.
- What worked: STATUS as the only handoff (no wide re-reads; the restart needed about 20 greps); drafting new code outside `Assets/` while the gate ran; one implementer per round with the log lines to copy named in the brief; a sheet script written once and reused; position-hashed placement, so tuning one rule never moved the rest.
- The tree adoption was the big subagent cost (267k tokens, 29 minutes) and worth it: the scans' leaves had no alpha, and the agent's card-per-leaf-island method was its own find. Brief asset adoption with "say if the brief's method cannot work and pick another", which it did unprompted.
- Brief differently: give Fable the 5-hour budget at the start and a rule to write STATUS before each implementer launch, not after: the limit took the implementer mid-round and the handoff then had to be written inside a hold.
- guard.sh let the first push of a new branch through without a gate PASS, then blocked the next plain `git push`: a possible hole in the hook, for the coordinator.
- A capture filter with a trailing underscore (`HM_Overview_`) silently drops the camera: name filters without it.
- A keep-clear rule tuned blind (the lake foreshore in round 2) put a trunk in front of a camera and cost that camera a round: check every placement change against the capture camera positions before building.
- Tuning a backdrop colour blind costs a round when the wrong layer is tuned (round 3 changed `FarRidge`; the band was `FarRange`). Before tuning by eye, find which object draws the thing: one capture with the suspect layer disabled, or read its material colour.
- Result against the goal: the town went from a bare summit to a town in a wooded valley (mean 12.9 -> 20.4, strict), and almost all of that came from rounds 1 and 2: landform, trees, light. Rounds 3 and 4 were tuning and moved the mean by about a point. Next time spend rounds 3 and 4 on the next structural item (standing ruin walls, the lake shore) rather than on colour and haze values.
- Three blind fixes missed (the lavender layer in round 3, the lake bank and the bell yard fill in round 4). Each would have been settled by a 1-minute capture-only probe (`SC_CAPTURE_NOBUILD=1`, one camera) before the full round. Put "probe first, then build" in the brief for any fix whose cause is a guess.
- The image budget (10) was not the limit; 6 were used. One sheet per round at 1500 px with the previous round as insets was enough to judge.
