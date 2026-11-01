State: IN PROGRESS
Session: Cellars floor art 2: dense dressing and the corner's grade
Job: dress the generated Cellars rooms to the approved corner's density (B5 kit shelves, barrels, crates, rubble, jars) and match the corner's grade, lamp colour and fog (less orange)
Progress: 30% - clutter kit merged (6058730); Core CellarFloorClutter resolver + tests WIP on claude/floor-art2-code a488068 (unverified)
Status: resumed (coordinator RESUME); floor-code step running (finish resolver + tests from a488068, placement, tuning block, test-edit). Next: round 1 captures.
Blocker: none
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
