# Hollowmere layout: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

State: IN PROGRESS (ruin integration)
Progress: 90% (ready; waiting for the bakery to merge first)
SC RESULT: pending (the last run passed on 8d86098; it runs again after the bakery merge, on the final tip)
Sheet: Reference/hollowmere/layout/Hollowmere_Ruins_vs_Kit.jpg
Captures: Reference/hollowmere/layout/Hollowmere_Captures_Sheet.jpg

**Current task:** wire the ruin kit (`Content/Art/Town/Ruins/Ruin_{S,M,L}_{A,B,C}`) into HollowmereBuilder. In Ruined, every lot shows the ruin of its size class at its layout `RuinVariant` and `RuinTurn` (deterministic per lot id, stored in the layout data), on its pad. Footing uses the facade kit's materials, Foliage the B9 atlas, and FOOTPRINT is hidden. In Rebuilt, a lot with a building or garden has no ruin; the lots left empty keep theirs. Merge order (coordinator): after the bakery, then stalls; origin/main is merged in right before the merge.

| Item | State |
|---|---|
| Layout: `RuinVariant` = kit folder name, `RuinTurn` 0-3; test `Ruins_MatchTheKitAndTurn` | done (23 tests pass under `sc_core_tests`) |
| Builder: ruin placement and materials, greybox fallback, a ruin capture camera | done (e59b22b) |
| Captures in both presets, side by side with the ruin kit sheet | done: `Hollowmere_Ruins_vs_Kit.jpg` (kit sheet on top, in-engine ruins below), `captures/HM_Ruins_{Ruined,Rebuilt}.jpg`; all 9 variants load, no missing material slot |
| `check`, merge | `check` PASS on 8d86098 (build PASS; test-edit 592/592; test-play 32 passed, 8 skipped, 0 failed). After it: a fix so the shared ruin foliage material builds from one fixed folder (it flipped between the byte-identical atlas copies). Next: merge origin/main after the bakery lands, regenerate Hollowmere.unity with the builder if it conflicts, `check` again, merge, then State DONE |

**Earlier (done):** Wave 0 greybox merged `1f6ff88`; terrain pass merged `1e20cb2`.

**Blockers:** none.
