State: DONE
Session: Q14. Decorations batch 2: market and lane clutter
Progress: 100%
Sheet: Reference/hollowmere/clutter/clutter_engine_sheet.jpg (Blender set: clutter_sheet_r3.jpg)
Blocker: none
Archive: ready

Branch: claude/blissful-goldberg-prbbeg (tip 690694a)
Score: art r1 27 -> r2 28.5 -> r3 29/40 (agent self-score); coordinator ~27/40 APPROVED (Blender sheet and in-engine sheet). Stopped at round 3 (gain < 1.5).
Tris: props 994-2712 (Well 2634), signs 580-1340, washing-line parts 36-384; all within CONTRACT budgets. In scene: 155,752 tris for all clutter.
SC RESULT: SC RESULT check PASS on 690694a (build PASS, test-edit 726/726, test-play 33 passed / 8 skipped / 0 failed), SC MARKER 690694ac299dd5b9dcbf67293634a5762a80081b; origin/main merged right before the check.
Merge commit: 302c607

## Result
- Art: `Art/Blender/town/sc_clutter.py` (Gen, slot materials on shared B1 tileables, no bakes, FBX path_mode STRIP): Handcart, CrateStack, SackStack,
  RainBarrel, FlowerBox, Well, each with a ruined look (_Broken / Well_Ruined); ShopSign_Bakery/Inn/Smithy/Workshop/Tailor/Grocer + ShopSign_Broken;
  WashingLine parts (rope, hook, peg, 5 cloths, 2 torn rags). Export: `Assets/_Project/Content/Art/Town/Clutter/` (21 FBX, SOURCES.md). Contract: CONTRACT.md.
- Unity: Core `HollowmereClutter` builds the plan from the layout: Rebuilt 52 ground rows (Well 1, RainBarrel 19, CrateStack 8, FlowerBox 16,
  Handcart 3, SackStack 5) + 6 trade signs (Workshop on R2_08/R2_13/R2_19/R2_23, Tailor F06, Grocer F07; none where the building art carries its own sign)
  + 3 washing lines (R2_11-R2_12, R2_19-R2_20, R2_04-R2_05, 6.3-7.2 m); Ruined 15 broken rows (every kind, Well_Ruined at the well's spot).
  Rows stay outside lots (clear of the Q6 garden), off the keep terrace, the beauty corner, the square dressing and the Q11 lane kit.
- Path graph: `Validate` rejects any footprint (inflated by the clearance) that covers a path node or crosses a path edge; `HollowmereClutterTests`
  (14) includes negative tests (row on a node, row across an edge, row on a lane step) plus counts, signs, well, washing-line clearance.
- Editor `HollowmereClutter.cs` places it all (real art, M_CL_* materials, box colliders), camera `Cap_HM_Clutter` (the R2_19-R2_20 washing line).

## Open (later polish, not blocking)
- Sacks are smooth pillows (no burlap weave or stitching), flower clumps read as pastel blobs, washing-line cloth pale in Blender and near black when backlit in Unity.
- The Ruined capture shows almost no clutter near the camera: a tipped cart or split barrel in view would sell the ruin.
- Cap_HM_Clutter now frames the washing line, not R2_19's crates and sign; the crates and sign were seen in the previous framing (engine sheet).
- One ring-2 crate stack (R2_13) was dropped because Q11's lane steps took its spot; nothing nearby passes the rules.

## Usage waste
Rough shares of this session's spend:
1. About 30%: the Unity editor download stalled in the container (~45 min idle before any in-engine work), then a disk-full episode (worktrees with
   their own Library folders filled the allowance) and a container restart that killed a running check. Fix: run in-engine steps in the main checkout,
   not per-agent worktrees (each Unity worktree costs ~3-6 GB), and check `setup.sh --check` at session start so a stalled editor download is caught early.
2. About 25%: three extra check/merge rounds because main kept moving under Assets (Q11 lane kit, polish merge, cellar art) while each 40-minute
   check ran, plus one fix round (a crate stack on Q11's new lane steps that the plan didn't know about). Fix: a coordinator merge slot per session.
3. About 20%: the usage pause mid-start: two agents were spawned, then paused, then replaced by fresh ones that re-read the same context.
4. About 10%: one wasted camera capture (the first Cap_HM_Clutter framing missed the washing line; the span coordinates should have been printed
   from the plan before choosing the camera), and an agent that re-read a stale capture PNG.
The rest was ordinary work (contract, generator, plan, reviews, merge).
