State: paused: user gaming (2026-11-14)
Branch: claude/dive-followups (from origin/main)

Done (commits fa3d9813, dabc5e09, 90c56ad7; gate PASS EditMode 1781/1781, soak 46/46, touched PlayMode classes pass):
- Item 1 spawner: wave 1 uses the 4 m ambush player-distance rule (all-or-nothing, retried per host tick, 8 s wait cap added by the implementer: drop if wait-forever preferred); wave 1 queued when wave 0 placed nothing; carrier-lost reset in TickWaveTwo; hunter cell excluded from template cells; PlayMode two-wave handoff test (uses new PlanOverride hook).
- Item 4: StairHandoverTests destroys TestHubGround in UnityTearDown.
- Unverified: wave-0-placed-nothing path and carrier-lost reset have no tests.
- Decision for user: wave 1 never occurs in live play (party size does not change enemy count; latch gives 1 extra, wave 1 needs 2+). Handoff is tested but dormant.

Flake findings so far (implementer ae5cf5f1a17d7b751, no commits, worktree clean at 90c56ad7):
- 2a floor-7 drift NOT reproduced: hero locomotion inert in the rig (no input service, HeroLocomotion.Update returns early, no _cc.Move). Unpinned real Floor7_ValveOrPump_GatesFinish passes both cases (TwoValves, HoldPump); only ~0.0002 m/frame x drift on valve 1 (0.05 to 0.11 m over 735 frames) with 5 live enemies. Not 2 m/s. Proposal: no game change; pin may be removable from the test.
- 2b Floor5_RealButtonPushOn: unverified hypothesis: FakeInputService swapped into ServiceRoots.App and teardown does not restore the real one, plus Seconds(0.7) arming timing. Needs repeat runs.
- 3 DiveWalkQaTests Rtt300Loss5: not started. Look for "[start] the client never received its hero" (line 65); stress 5-10x with CPU load.

Next:
0. On resume: repeat-run DiveSpineNetworkTests (Floor5_* repeated) 3-5x; fix teardown input restore if confirmed; stress DiveWalkQa Rtt300Loss5; gate.
1. Items 2 and 3 (DiveSpineNetworkTests floor-7 drift + Floor5_RealButtonPushOn; DiveWalkQaTests Rtt300Loss5 40 s timeout): implementer ae5cf5f1a17d7b751 was investigating when paused; check its commits.
2. Gate, soak (Core/Cellars touched), risk-reviewer (Opus) over branch, full check on tip (SC MARKER = HEAD), sc-merge into fresh origin/main (pre-approved 2026-11-14); merge commit ticks QUEUE lines 24, 43, 47, 48 and adds a CURRENT.md Now line.
Archive: no
