# Ground blend before/after diag (2026-11-22)
Before = area1-world/R19 (Run 8), after = R20 (tip ad721abe, Morning, Ruined, 1920x1080, capture without rebuild of the committed scenes).
Tool: Tools/art/sc_scene_diag.py (from the main checkout). HF share = spectrum high (>1/8 cyc/px), whole frame and per band far/mid/near.

| Camera | HF before | HF after | far b/a | mid b/a | near b/a | mean abs luma diff | pixels >12 |
|---|---|---|---|---|---|---|---|
| Spawn | 0.1488 | 0.1464 | .1073/.1061 | .1327/.1304 | .1828/.1813 | 2.1 | 1.8% |
| Orchard | 0.1465 | 0.1445 | .1924/.1905 | .1677/.1610 | .0902/.0977 | 5.9 | 5.7% |
| OrchardGap | 0.0496 | 0.0485 | .0370/.0363 | .0657/.0646 | .2028/.1783 | 2.6 | 3.2% |
| Meadow | 0.1539 | 0.1482 | .1057/.1029 | .1847/.1770 | .3927/.3928 | 2.6 | 3.6% |

Tiling and empty-cell figures are unchanged within noise (empty near OrchardGap 0.11 -> 0.12). Crops: R20/crops/*_before_after.jpg (before crops cut from R19 at the same pixel box as the R20 1:1 crops, template-matched).
