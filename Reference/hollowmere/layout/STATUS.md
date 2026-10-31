# Hollowmere layout: status

Session: cloud, branch `claude/cool-ritchie-ofmy47`. Updated 2026-10-31.

State: DONE (ruin integration merged)
Progress: 100%
SC RESULT check PASS on d72bc47 (build PASS; test-edit 592/592; test-play 32 passed, 8 skipped, 0 failed). Merge commit 7a4661d (main 006a57a; identical to d72bc47 under Assets, Packages and ProjectSettings)
Sheet: Reference/hollowmere/layout/Hollowmere_Ruins_vs_Kit.jpg
Captures: Reference/hollowmere/layout/Hollowmere_Captures_Sheet.jpg

**Current task:** wire the ruin kit (`Content/Art/Town/Ruins/Ruin_{S,M,L}_{A,B,C}`) into HollowmereBuilder. In Ruined, every lot shows the ruin of its size class at its layout `RuinVariant` and `RuinTurn` (deterministic per lot id, stored in the layout data), on its pad. Footing uses the facade kit's materials, Foliage the B9 atlas, and FOOTPRINT is hidden. In Rebuilt, a lot with a building or garden has no ruin; the lots left empty keep theirs. Merge order (coordinator): after the bakery, then stalls; origin/main is merged in right before the merge.

| Item | State |
|---|---|
| Layout: `RuinVariant` = kit folder name, `RuinTurn` 0-3; test `Ruins_MatchTheKitAndTurn` | done (23 tests pass under `sc_core_tests`) |
| Builder: ruin placement and materials, greybox fallback, a ruin capture camera | done (e59b22b) |
| Captures in both presets, side by side with the ruin kit sheet | done: `Hollowmere_Ruins_vs_Kit.jpg` (kit sheet on top, in-engine ruins below), `captures/HM_Ruins_{Ruined,Rebuilt}.jpg`; all 9 variants load, no missing material slot |
| `check`, merge | done: main (bakery and bell tower) merged in, Hollowmere.unity regenerated with the builder (bakery on F02, B4 bell tower on the keep, ruins on every empty lot), check PASS, merged |

**Earlier (done):** Wave 0 greybox merged `1f6ff88`; terrain pass merged `1e20cb2`.

**Next:** none claimed. QUEUE.md claiming is PAUSED (usage pacing), so this session stops here. Q5 (builder churn) suits it when claiming reopens. The remaining churn is `HeroAnimator.controller` (+ .meta) on every cloud build; the ruin foliage material is now stable.

**Blockers:** none.
