State: IN PROGRESS
Session: Wave 2: Kit family 3, tall jettied townhouse (M)
Progress: 75%
Sheet: Reference/hollowmere/townhouse/SHEET_r3_four_sides.jpg
Blocker: none

Score: r3 29/40 (silhouette 4, proportion 4, form 4, surface 3.5, colour 3.5, wear 3.5, set 3.5, glass/metal 3); r1 26.5; target 31.5 not reached, stopped at 3 rounds for usage pacing
Tris: A 34,265 / B 32,346 / C 34,531 (budget 35k; Part_Door 1,558, Part_Shutters 3,456)
SC RESULT: no check yet
Merge commit: n/a

## Log
- 2026-10-31: started. Reading the contract (spec 5.2/5.4), the B4 facade kit, HollowmereLayout/Builder. Unity editor still downloading in the background.
- Generator Art/Blender/town/sc_townhouse.py runs: 3 seeded variants, all four sides framed from the B4 kit (imported as a library), Frame + Part_Door + Part_Shutters + SOCKET_* + FOOTPRINT. First cut 76k tris; budget pass now about 34-35k. First proof renders in progress.
- Checked before building: the B4 kit already holds every piece (kit pieces, vbuild variant layout); no outside generator needed. Reused it rather than writing new pieces.
- r1 sheet: 3 variants x four corners beside Bernkastel/Rothenburg/Quedlinburg refs and the approved corner. Frame-only render (FRAME_ONLY_Townhouse_A_r1.jpg): open dark doorway, bare window frames, reads as waiting for parts.
- r1 worst three: (1) lean too subtle for storybook whimsy, (2) roof cap thin over the narrow walls, (3) plaster too clean at mid distance; A and C read alike. Round 2 running.
- Integration written: BuildingKind.Townhouse on F06 + F07 (south side, M, facing the fountain; F01 facade, F02 bakery, F09 inn untouched), HollowmereTownhouse.cs places the FBX on the footing at the pad height, FOOTPRINT collider + class validation. Unity editor still downloading for the check.
- r2 (pushed): stronger lean (x 1.4-2.2 cm/m, outward tip 2.8-4 cm/m), eave overhang 1.0 m, grime x1.15-1.4, C ochre wash stronger. Sheet SHEET_r2_four_sides.jpg. Not re-scored: superseded by the coordinator's r1 review.
- r3 plan (written, not applied yet; resumes on the coordinator's word): B side-gabled (eave to the street, gables on the sides, mirror_roof about the house centre); C log-store lean-to (kit_leanto narrowed to stay inside the foundation) + a 0.006 rad/m twist; jetty 0.42-0.58 with curved bracket ends at every post line; storey heights 2.0-2.45 / 1.9-2.3; sway-backed ridge (sag 0.32); a ledged back door with step on every variant; an external stone chimney breast and stack over the back bay of one side wall; budget relief by side plaster grid 0.45 and front lattice 0.22.
- sc_facade_kit.py change is additive only (a __main__ guard, defaults untouched): the facade and bakery export unchanged.
- Unity: editor still downloading; check not run yet. HollowmereTownhouse.cs.meta will be committed after the first Unity import.
- Resumed: merged origin/main (6762a0f; kept the bakery placement and the shared kit guard). r3 applied: A front gable 34,265 / B side gables 32,346 / C front gable + lean-to + twist 34,531 tris. r3 sheet rendering. Unity editor still downloading.
- r3 exported to Assets/_Project/Content/Art/Town/Ring1/Townhouse/Townhouse_{A,B,C}/ with SOURCES.md. Sheet SHEET_r3_four_sides.jpg. Next visible gains if a round 4 is wanted: plaster value variation that survives distance, glass that catches the sky, a shorter stack on A/C.
- Unity editor in (download had stalled at 04:45; setup.sh --wait restarted it). Cloud C# compile PASS. unity.sh check running; captures (Cap_HM_Townhouses, Cap_HM_TownhouseSide) after.
