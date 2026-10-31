State: IN PROGRESS
Session: Wave 2: Kit family 3, tall jettied townhouse (M)
Progress: 50%
Sheet: Reference/hollowmere/townhouse/SHEET_r1_four_sides.jpg
Blocker: none

Score: r1 26.5/40 (silhouette 3.5, proportion 3.5, form 3.5, surface 3, colour 3.5, wear 3, set 3.5, glass/metal 3); target 31.5 (first + 5)
Tris: A 34,317 / B 34,383 / C 34,007 (budget 35k; Frame + Part_Door 1,558 + Part_Shutters 3,456)
SC RESULT: no check yet
Merge commit: n/a

## Log
- 2026-10-31: started. Reading the contract (spec 5.2/5.4), the B4 facade kit, HollowmereLayout/Builder. Unity editor still downloading in the background.
- Generator Art/Blender/town/sc_townhouse.py runs: 3 seeded variants, all four sides framed from the B4 kit (imported as a library), Frame + Part_Door + Part_Shutters + SOCKET_* + FOOTPRINT. First cut 76k tris; budget pass now about 34-35k. First proof renders in progress.
- Checked before building: the B4 kit already holds every piece (kit pieces, vbuild variant layout); no outside generator needed. Reused it rather than writing new pieces.
- r1 sheet: 3 variants x four corners beside Bernkastel/Rothenburg/Quedlinburg refs and the approved corner. Frame-only render (FRAME_ONLY_Townhouse_A_r1.jpg): open dark doorway, bare window frames, reads as waiting for parts.
- r1 worst three: (1) lean too subtle for storybook whimsy, (2) roof cap thin over the narrow walls, (3) plaster too clean at mid distance; A and C read alike. Round 2 running.
- Integration written: BuildingKind.Townhouse on F06 + F07 (south side, M, facing the fountain; F01 facade, F02 bakery, F09 inn untouched), HollowmereTownhouse.cs places the FBX on the footing at the pad height, FOOTPRINT collider + class validation. Unity editor still downloading for the check.
