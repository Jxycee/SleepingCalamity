State: IN PROGRESS
Session: Cellars floor art 2: dense dressing and the corner's grade
Job: dress the generated Cellars rooms to the approved corner's density (B5 kit shelves, barrels, crates, rubble, jars) and match the corner's grade, lamp colour and fog (less orange)
Progress: 55% - code on claude/floor-art2-code a4dde79 (resolver + tests, placement, tuning block, combat-room clearance rule; test-edit 821/821, build PASS); round 1 captures running
Status: round 2 running (room clutter missing in round 1 view; retune; main-file fold)
Blocker: none (one coordinator decision below, not blocking the rounds)
Archive: no
Branch: claude/happy-ramanujan-41karq
Score: -
Sheet: -
SC RESULT: -
Merge commit: -

## Coordination with Q12 (claude/cellar-procgen)
- This job does NOT edit the room pool sockets or `CellarFloorDressing.Resolve`: Q12 pins `LayoutHash`/`DressingHash` for floors 1-3. Density comes from a new Core resolver `CellarFloorClutter` (own `Rng.Derive` stream, own hash, new file only) read by the Presentation art layer. Q12: please don't add a file of that name; tell us here if your layer must reserve cells clutter should avoid.

## Round 0 (measured)
Corner (approved sheet render panels) vs floor_art round 3, sc_ref_palette p10/p50/p90 and value-band share/sat (dark <.25, mid .25-.60, light >.60):
| capture | p10/p50/p90 | warm | dark/mid/light share | light sat |
|---|---|---|---|---|
| corner wide | .09/.23/.51 | +.21 | 54/38/6 | .42 |
| corner mid | .10/.23/.49 | +.24 | 54/38/6 | .53 |
| corner close | .11/.24/.55 | +.27 | 52/38/8 | .53 |
| floor room | .09/.26/.51 | +.23 | 47/48/3 | .57 |
| floor corridor | .15/.30/.55 | +.22 | 34/58/7 | .56 |
Diagnosis: values match, the distribution doesn't. The floor is a broad mid-tone tan wash (ambient 0.8 vs the corner's 0.23, plus a 4.0 fill per room) with few, more saturated lamp pools; the corner is deep grey-brown darks with local paler-gold pools. Targets: dark ~50-55%, mid <=42%, light >=5%, light sat <=0.50, p10 .09-.11, p50 .22-.25, p90 .48-.55.
Density: rooms show 1-2 socket pieces and greybox crate cubes; the corner frame holds a shelf, 3+ barrels, jars, a chute and coal in 4 m. The B5 kit has no standalone crate/jar/rubble pieces.

## Clutter kit (merged 6058730)
`Art/Blender/cellars/sc_cellar_clutter.py` -> `Content/Art/Cellar/B5/CellarClutter/CellarClutter.fbx`: Crate 1.6k, CrateStack 4.0k, JarCluster 1.4k, Rubble 1.4k, SackPile 0.9k (9.3k tris), CV_* materials only (all mapped), pivot bottom centre, front +X, wall side -X. SackPile reads stony (limewash stand-in): use sparingly.

## Merge order (coordinator)
Cellars branches touching CellarFloorBuilder.cs merge in order: M5 spine, M5 contents, Q12 procgen, then this job LAST. Builder edits stay in CellarFloorBuilder.Art.cs with at most one call line in CellarFloorBuilder.cs (code tip 8c5f174 has three one-liners: Clutter field, GoldLamp tuning, fog colour; to be folded into one call). Before the check: merge origin/main after those land, regenerate scenes with a -nographics build, never hand-merge .unity.

## DECISION FOR COORDINATOR: real floor kit was null
`CellarFloorKit` was not [Serializable], so CellarFloorSync.prefab's Kit never serialized. In play the floor had no materials or kit pieces (all greybox); only the CellarFloorTest scene showed the art. claude/floor-art2-code a4dde79 fixes it: [Serializable] kit with Pieces as parallel lists (+20 lines in CellarFloorBuilder.cs), a new CellarFloorKitBuilder step (ProjectBuilder.RunAll) that writes the shared kit onto CellarFloorSync.prefab, the prefab regenerated with -nographics. This exceeds the one-call-line rule and touches the prefab the spine wires. (a) keep it, merge last, resolve by rerunning the builders; or (b) the spine/contents owner takes it and I drop it. Default if no answer by my merge: (a). (A cross-session message to the coordinator was refused: cloud -> local needs the owner's trust approval.)

## Code step (a4dde79)
- Clutter rule: wall cells only, pushed to the wall face; never door / front-of-door / marker / socket cells; markers >=1.9 m clear; room centre open; door-to-door reachability; <=40% cover; 0-2 Rubble per room; corridors ~12% of straight cells, Rubble/Crate, >=2.5 m walkable. Encounter cells (CellarEncounterPlan stores only cells, so the standing point is the cell centre) allowed with every footprint >=1.0 m from the centre (tested over 1,000 seeds x floors 1-4; min exactly 1.00 m).
- Density: 3.52 rows per room (combat 2.05, was 0.54 under a blanket exclusion), per floor 1-4: 3.57/3.87/3.20/3.41. ClutterHash golden new; Layout/Dressing goldens unchanged.
- Tuning block `CellarArtTuning` (top of CellarFloorBuilder.Art.cs): ambient 0.8 -> 0.35; room fill 4.0 -> 1.8, (1.0,.76,.52) -> (1.0,.86,.70); lamp gold -> (1.0,.82,.58), lamp intensity x1.2; candle (1.0,.72,.44); fog warm offset x0.5, clamped within 0.03 of base.
- Usage: disk filled (four 2.9 GB agent worktrees + the Unity archive); cleared, now one persistent worktree /home/user/sc-art2 shared by the step agents.

## Round 1 (a4dde79, captures not committed)
| capture | p10/p50/p90 | warm | dark/mid/light | light sat |
|---|---|---|---|---|
| room | .09/.30/.59 | +.26 | 42/48/9 | .54 |
| corridor | .17/.35/.65 | +.23 | 23/62/14 | .54 |
Read: lamp pools stronger (light share up) but overall too bright and the corridor brick is a mid-tone wash; light sat down only .03. The Floor_Room view shows no clutter (unchanged dressing): being diagnosed. Corridor shows Rubble. Self score r1: not scored (density not visible).
