Status: DONE
Archive: ready
Merged to main as edf1d82d (branch tip b971c69c). check PASS: EditMode 1625/1625, PlayMode 220 (199 pass, 0 fail, 21 skipped); soak 46/46; marker = tip. Main gained only non-Assets commits since, so no re-check.
Risk review (Opus): no blockers. Open should-fix follow-ups:
1. Party size does not change enemy count (CellarTemplateSpawns trims to plan count, which ignores party size); wave 1 survives only when plan count >= 4. Design call; left as-is for the dungeon-one difficulty guardrail.
2. Wave 1 spawns with no player-distance safety (CellarContentsSync TickWaveTwo).
3. Wave 1 dropped if wave 0 placed nothing; carrier-lost reset missing in TickWaveTwo (both theoretical).
4. No PlayMode test of the two-wave handoff; hunter cell not excluded from template cells.
Goldens: only new Choose_Golden pin; no existing golden changed.
Churn bug: cloud Unity runs rewrote BeautyCorner_Hollowmere/CellarFloorTest/Hollowmere .unity and M_HM_Surf_WornRun .mat; reverted, not committed.
