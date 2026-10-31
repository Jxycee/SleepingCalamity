State: DONE
Session: Wave 1: Inn (L, frame + parts)
Progress: 100%
Sheet: Reference/hollowmere/inn/SHEET_inn_r4.jpg
Blocker: none

- Art APPROVED by the coordinator (round 4, ~30/40). Art final (round 4 of max 4): self-review 31.5/40 (round 4: sign board 1.3x, set 4.5). Round 3 = 31/40 (silhouette 4.5, proportion 3.5, form 4, surface 3.5, colour 4, wear 3.5, set 4, metal/emissive 4); round 1 = 26 -> target 31 reached. r4 sheet: tql/left/close/sign re-rendered, the other views unchanged from r3 except the sign.
- Round 2 fixes: painted-plank grain across the posts -> ox-blood stained oak; mossy porch roof (wear keyed to the porch eave); body widened to 9 m; steeper porch roof. Round 3: sign bracket turned out past the corner (was along the wall).
- Tris: 49,890 (Frame 45,132 / Part_Door 1,150 / Part_Sign 1,488 / Part_Porch 1,904 / Part_Lantern 216), cap 50k. FOOTPRINT 9.96 x 8.97 m (L max 10.5); whole asset x -5.43..5.50, y -5.07..5.46 inside the 11 m foundation.
- Export + integration: Inn.fbx on F09 (L) at its pad height via HollowmereInnArt.cs + a builder row (d79c9a0; check PASS there: build, edit 591/591, play 32 pass). Merged origin/main (bakery, ruins, stalls) at 6d92db0; check re-running on the tip.
- sc_facade_kit.py: only the `if __name__ == '__main__'` import guard, byte-identical to the bell tower / bakery branches (additive).
- Unity captures: not taken (no GPU in this cloud session; HollowmereCapture.CaptureAll needs --graphics): a PC batch job.
SC RESULT check PASS (2a5200e, after merging main with the tithe gate; build PASS, edit 592/592, play 32 pass / 0 fail / 8 skip). An earlier tip run failed once on CombatArenaTests.Solo_DaggerGuard_Reduces_AndAParryStopsTheHit (real-time parry window, unrelated); passed on both re-runs.
- Merge: 2c436ed on main (tree identical to the checked 2a5200e, main had not moved, so no second check).
