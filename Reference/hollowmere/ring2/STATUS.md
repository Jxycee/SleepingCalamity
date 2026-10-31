# Q3 ring 2 fill and the play-position captures: status

State: DONE (merged to main d87f6bf)
Session: Bell tower: bake, integrate, check, merge
Job: Q3 Ring 2 fill and the play-position captures (cloud session, branch claude/brave-keller-3d91gk)
Progress: 100%
Sheet: Reference/hollowmere/ring2/ring2_sheet.jpg
SC RESULT: SC RESULT check PASS on 09be625 (test-edit 607/607, test-play 32 passed / 8 skipped / 0 failed); the merge's Assets equal the checked tip
Merge commit: d87f6bf
Blocker: none

Result:
- HollowmereLayout.AssignRing2 (pure C#, seed 20261031): 12 free ring-2 lots -> 8 kit homes + 4 ruins (33%; R2_10, R2_15, R2_17, R2_21), around the fixed lots
  R2_01 smithy, R2_03 schoolhouse (Q2), R2_08 Workshop_B, R2_09 StoneCottage_B (Q1). Variants: Townhouse_A x3, Townhouse_B x2, Workshop_C x2, Workshop_B x1;
  no duplicate variant within 22 m. Placers (cottage, workshop, townhouse) read variant and turn from the data. Tests incl. a golden test and a seed-sensitivity case.
- Captures: the four play-position cameras (heroes' home entry, fountain, tithe gate, lane mouth) in Ruined and Rebuilt at Midday and LowSun; merged with main's
  SC_CAPTURE_SUN / SC_CAPTURE_ONLY options (default run unchanged).
- Review (reviewer agent): no blocking findings; golden test added; the empty share is measured over the fill pool (about 25% over all ring-2 lots).
- Weak / open: the play cameras mostly show the square (ring 2 reads in the overview); the L lots are still greybox boxes; no stone cottage is drawn from the
  pool with this seed (the only free S lot came up as a ruin); the Ruined fountain frame has a blown-out white ground (pre-existing, not from Q3).

History:
Started 2026-10-31. Plan: a seeded ring-2 assignment in HollowmereLayout drawing on the merged kit families (cottage A-C, townhouse, workshop A-C) with
variants and turns at pad heights, some lots left empty with ruins; the design-7 capture set (heroes' home entry, fountain, tithe gate, lane mouth) in Ruined
and Rebuilt at midday and low sun; a contact sheet beside the approved corner. Implementation on a Sonnet implementer; check and merge here.
