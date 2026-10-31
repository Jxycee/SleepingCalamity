# Hollowmere layout: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

State: IN PROGRESS (terrain pass)
Progress: 50%
Sheet: Reference/hollowmere/layout/Hollowmere_Map_Town.jpg

**Wave 0 greybox:** merged to main as `1f6ff88` (`check` PASS on main).

**Terrain pass (user feedback 2026-10-31):** heights are in the layout data (`Foundation.Y`, `Landmark.Y`, `Lane.Heights`, path node `Y`; metres relative to the square). The square shelf is 0 and the keep terrace +4. The Ring Lane is one 3 m terrace down on the flanks (-3.5) and spirals down to a second terrace on the south (-6.5). The town wall stands on a raised bank with a dry ditch outside it (-9 to -12). The lake is a basin with water at -12 (bed -16.5), the stream runs in a ravine, and the south slope falls to the lake. Every lot is a flat pad at its lane's height (cut and fill walls in the builder). Lanes are relaxed to 17 deg or less. Validation adds a 20 deg rule (stairs exempt) and requires doorsteps at the lot's height. The keep wall gets buttresses, planters with ivy and a second (west) stair. Foundations have not moved.

| Item | State |
|---|---|
| Heights + validation + tests (22) | done; `sc_core_tests` PASS |
| Elevation-shaded map | done (`Hollowmere_Map_Town.jpg`, `Hollowmere_Map_Square.jpg`) |
| Builder: terrain mesh, terrace walls, cut and fill, keep wall breakup, low-angle cameras | written, compiles; Unity build and captures next |
| `check`, merge | not yet |

**Blockers:** none.
