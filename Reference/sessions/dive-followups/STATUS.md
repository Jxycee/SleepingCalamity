State: paused: user gaming (2026-11-14)
Branch: claude/dive-followups (from origin/main)

Done (commits fa3d9813, dabc5e09, 90c56ad7; gate PASS EditMode 1781/1781, soak 46/46, touched PlayMode classes pass):
- Item 1 spawner: wave 1 uses the 4 m ambush player-distance rule (all-or-nothing, retried per host tick, 8 s wait cap added by the implementer: drop if wait-forever preferred); wave 1 queued when wave 0 placed nothing; carrier-lost reset in TickWaveTwo; hunter cell excluded from template cells; PlayMode two-wave handoff test (uses new PlanOverride hook).
- Item 4: StairHandoverTests destroys TestHubGround in UnityTearDown.
- Unverified: wave-0-placed-nothing path and carrier-lost reset have no tests.
- Decision for user: wave 1 never occurs in live play (party size does not change enemy count; latch gives 1 extra, wave 1 needs 2+). Handoff is tested but dormant.

Next:
1. Items 2 and 3 (DiveSpineNetworkTests floor-7 drift + Floor5_RealButtonPushOn; DiveWalkQaTests Rtt300Loss5 40 s timeout): implementer ae5cf5f1a17d7b751 was investigating when paused; check its commits.
2. Gate, soak (Core/Cellars touched), risk-reviewer (Opus) over branch, full check on tip (SC MARKER = HEAD), sc-merge into fresh origin/main (pre-approved 2026-11-14); merge commit ticks QUEUE lines 24, 43, 47, 48 and adds a CURRENT.md Now line.
Archive: no
