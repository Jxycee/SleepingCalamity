State: merged to main; Archive: ready
Started: 2026-11-14
Branch: claude/mp-readiness-tests
Job: QUEUE.md "Ready: multiplayer readiness (M7)", the 3-4 player smoke (triage Q1) and the network-conditions set (triage Q6). Tests only; no game code, no ReplicatedStateHash, no DiveWalkQaTests changes.

## What landed (all under Assets/_Project/Tests/PlayMode/Mp/)
- `MpPartyRig.cs`: shared rig. Host + 1-3 in-process TestClients (no scenes), optional NetworkSimulator, real DiveDirector, one hero per peer at the hub stair, log tap (errors, warnings), helpers for floor-built / arrival / back-home waits and the client trapdoor and stair presses (real RPCs, retried).
- `NetConditionCoreLoopTests.cs` (item 1):
  - `CoreLoop_ClientDrivenDive_StateAgreesAtEveryStep_UnderLatency` [150/2, 300/5], check tier: client trapdoor press, piece + item + damage, client stair press, abandon back to the hub, second dive; `ReplicatedStateHash.AssertAllEqual` after each step; no error, no Netcode warning.
  - `QuadCoreLoop_UnderLatency` [150/2, 300/5] and `Rejoin_MidDive_UnderLatency_HashAgrees` [150/2, 300/5]: slow tier, `SC_MP=1`, `[Category("Slow")]`.
- `PartyScaleSmokeTests.cs` (item 2):
  - `Party_EntersDive_ScalesEnemies_ChangesFloor_ReturnsToHub` [2, 3, 4 players], check tier, no latency: live enemy count in a template combat room == plan base + (players - 1) on the host, every peer sees the enemies, floor change by client stair press, return to the hub, hash agreement at each step, clean log.
  - `PartyUnderLatency_...` [3, 4] x [150/2, 300/5]: slow tier (`SC_MP=1`).

## Not covered
Plan-driven rooms (bakery set pieces, Tier 2, warden) get no party scaling in the spawner and are not asserted; ambush rooms; enemy life scaling; real melee; walking bots (CoopDiveBotTests / CoopSoakTests stay the soak); real relay; scene-managed clients; MPPM (virtual players are extra editor processes, not available under the batchmode wrapper); enemies and transforms in the hash. The 300/5 stair hand-over leg is covered by a one-token edit of StairHandoverTests' [Values] (passes).

## Check
Gate PASS (EditMode 1780). PlayMode, check tier: total 13, passed 5, skipped 8 (the slow tier). With SC_MP=1: 13/13 passed (slowest case 65 s). StairHandoverTests 3/3 passed including 300/5.
Archive: ready
