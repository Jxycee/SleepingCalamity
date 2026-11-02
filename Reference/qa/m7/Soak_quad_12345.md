# Co-op soak, quad (4 players), seed 12345

**Verdict: NOT CLEAN as a soak (clean on log, hash, heap and objects; the run did not exercise repeated dive cycles).** The harness verdict line printed CLEAN because it only checked errors, hash, heap and object trend; the author downgraded it after reading the bot log:

- The host bot sat on floor 3 from 606 s to the end (1194 s, 66% of the run), alternating between "locked door (252, 1.4, -6)" and "stair (234, 0, -42)". Findings: "the door reads open (DoorOpen) but its gate object still blocks", then "no walkable path ... to locked door (PathPartial)" and a stuck spell against `Wall_12_2_East`, then give-ups on the stair. So only 2 dives started, 0 taken home, 1 failed (low HP), no bank, 0 enemies killed by the host bot, and floor 3 ran for 23 minutes with 0 enemies. Bank, go-home and a third dive cycle were not covered; the frame/GC rows for hub and floors 1, 2, 4 rest on 46 to 212 game seconds each.
- Cause is on the bot/floor-3 (seed 12345) side (DoorOpen with a blocking gate on the Spine locked door is a candidate real bug); not investigated further here, see the report. Nothing netcode-related was changed.

Game time 1800 s of 1800 s planned, 192 s wall (0.11 wall s per game s), Time.captureFramerate 60, host DiveBot + 3 FollowerBots.

## Dives

- Dives started (host bot left the hub into floor 1): 2; taken home: 0; failed: 1 (low HP 1, near a floor/traversal fault 0); floors left: 4; deepest floor 4
- Hero downs 6 (client 0: 3, client 1: 1, client 2: 2); out 3; revives 3; time downed 99 s
- Host bot: enemies killed 0, chests 13, pieces 4, stuck events 148, hearts banked: False

## Frame time per phase (real wall ms per frame, Stopwatch)

| Phase | Game s | Frames | Mean | p95 | p99 | Max | Managed alloc / frame |
|---|---:|---:|---:|---:|---:|---:|---:|
| hub | 46 | 2741 | 1.5 | 1.9 | 2.3 | 142.6 | 2300 B |
| floor 1 | 68 | 4091 | 2.3 | 3.4 | 5.8 | 177.0 | 3960 B |
| floor 2 | 92 | 5495 | 2.7 | 3.8 | 4.8 | 327.6 | 4244 B |
| floor 3 | 1381 | 82785 | 1.7 | 2.4 | 3.3 | 226.2 | 2458 B |
| floor 4+ | 212 | 12728 | 1.9 | 2.6 | 3.3 | 137.5 | 2518 B |
| **all** | 1797 | 107840 | 1.8 | 2.7 | 3.7 | 327.6 | |

Frame time is the whole loop of the one process: four peers' simulation, the bots, the hash and this sampler, uncapped (no vsync wait), software-free but with a real GPU path only if the editor is not -nographics. Managed alloc per frame is the sum of positive deltas of GC.GetTotalMemory(false) (a collection inside a frame hides some allocation, so it is a lower bound).

## Heap and object trend

- Managed heap after a forced collect: start 42.0 MB, end 46.2 MB (delta +4.2 MB); forced-collect samples every 120 s: 120s=46.7, 240s=47.5, 360s=48.1, 480s=48.1, 600s=44.3, 720s=45.6, 840s=45.3, 960s=45.6, 1080s=45.7, 1200s=45.7, 1320s=45.8, 1440s=45.8, 1560s=46.1, 1680s=46.2, 1800s=46.2; trend -0.05 MB/min
- Unforced heap (sawtooth) trend over 181 samples: +0.06 MB/min
- Host-side NetworkObjects: max 14, trend -0.070/min; enemies (all peers' copies) trend -0.294/min

| Game s | Floor | Heap MB (unforced) | NetworkObjects (host) | Enemies (host / all peers) |
|---:|---:|---:|---:|---:|
| 0 | 0 | 42.0 | 6 | 0 / 0 |
| 40 | 1 | 46.7 | 12 | 3 / 12 |
| 80 | 2 | 49.1 | 9 | 0 / 0 |
| 120 | 2 | 49.4 | 13 | 4 / 16 |
| 160 | 2 | 48.3 | 11 | 2 / 8 |
| 200 | 3 | 52.4 | 9 | 0 / 0 |
| 240 | 3 | 54.4 | 11 | 2 / 8 |
| 280 | 3 | 48.2 | 9 | 0 / 0 |
| 320 | 3 | 51.1 | 9 | 0 / 0 |
| 360 | 4 | 56.8 | 13 | 4 / 16 |
| 400 | 4 | 49.0 | 10 | 1 / 4 |
| 440 | 4 | 50.9 | 10 | 1 / 4 |
| 480 | 4 | 50.2 | 11 | 2 / 8 |
| 520 | 4 | 49.9 | 11 | 2 / 8 |
| 560 | 4 | 50.9 | 14 | 5 / 20 |
| 600 | 0 | 51.3 | 9 | 0 / 0 |
| 640 | 3 | 47.3 | 9 | 0 / 0 |
| 680 | 3 | 51.0 | 11 | 2 / 8 |
| 720 | 3 | 51.3 | 11 | 2 / 8 |
| 760 | 3 | 49.1 | 10 | 1 / 4 |
| 800 | 3 | 53.2 | 10 | 1 / 4 |
| 840 | 3 | 54.0 | 9 | 0 / 0 |
| 880 | 3 | 49.5 | 9 | 0 / 0 |
| 920 | 3 | 53.3 | 9 | 0 / 0 |
| 960 | 3 | 51.8 | 9 | 0 / 0 |
| 1000 | 3 | 49.6 | 9 | 0 / 0 |
| 1040 | 3 | 53.3 | 9 | 0 / 0 |
| 1080 | 3 | 54.1 | 9 | 0 / 0 |
| 1120 | 3 | 49.4 | 9 | 0 / 0 |
| 1160 | 3 | 52.8 | 9 | 0 / 0 |
| 1200 | 3 | 53.9 | 9 | 0 / 0 |
| 1240 | 3 | 49.4 | 9 | 0 / 0 |
| 1280 | 3 | 52.8 | 9 | 0 / 0 |
| 1320 | 3 | 53.9 | 9 | 0 / 0 |
| 1360 | 3 | 49.4 | 9 | 0 / 0 |
| 1400 | 3 | 52.9 | 9 | 0 / 0 |
| 1440 | 3 | 53.9 | 9 | 0 / 0 |
| 1480 | 3 | 49.4 | 9 | 0 / 0 |
| 1520 | 3 | 53.4 | 9 | 0 / 0 |
| 1560 | 3 | 51.8 | 9 | 0 / 0 |
| 1600 | 3 | 49.7 | 9 | 0 / 0 |
| 1640 | 3 | 53.1 | 9 | 0 / 0 |
| 1680 | 3 | 54.2 | 9 | 0 / 0 |
| 1720 | 3 | 49.7 | 9 | 0 / 0 |
| 1760 | 3 | 53.1 | 9 | 0 / 0 |
| 1800 | 3 | 54.1 | 9 | 0 / 0 |

## Hash (ReplicatedStateHash, every 10 s, agree on any frame within 5 s)

- Samples 179, agreed 179, mismatches 0

## Log (since the party was ready): 0 errors/exceptions (0 distinct), 0 warnings (0 distinct)

| Count | Kind | First line | Where (first occurrence) |
|---:|---|---|---|

Grouped by keyword: NullReference 0, NetworkVariable 0, Netcode 0, NGO 0, NetworkObject 0, RPC 0, Exception 0

## What this cannot show

- Loopback transport inside one process: no latency, loss, jitter, MTU or bandwidth limits (the QA walks under NetCondition cover those). Network cost is a memcpy here.
- One process runs all four peers, so frame time is the SUM of four peers' work plus the bots: a real peer would run roughly a quarter of the simulation but also render, and the host's share is in between. Frame time here is a relative regression signal, not a per-player FPS.
- The editor (not a player build) with the test runner, and likely -nographics: no rendering, UI, audio or GPU cost. Editor GC and Mono differ from IL2CPP builds.
- Bots, not people: no menus, inventory, town building, disconnect/rejoin or unusual input. Followers trail the host bot. Seed is pinned (12345), so floor layouts repeat per dive index, not per time.
- Fixed 60 Hz captureFramerate: no variable-delta bugs, no vsync pacing.

## Proposed frame-time budgets for the quad case (PROPOSALS, derived from this one run, not agreed)

- Observed overall p95 2.7 ms, p99 3.7 ms. Proposed regression budgets: p95 < 4 ms and p99 < 6 ms per phase (about 1.5x the observed values, to leave room for machine noise), measured by this soak on the same machine class.
- Managed allocation: budget against the table's B/frame per phase (propose 1.5x the observed hub and floor values), and a forced-collect heap slope of < 0.5 MB/min.
- Object count: host NetworkObjects must return to within a small margin of the hub baseline after each dive (the table above), trend < 0.05 per minute.

## Host bot log (tail)

      586.3s F0: goal: hub stair at (0.0, 0.0, 9.0)
      596.3s F0: goal: hub stair at (0.0, 0.0, 9.0)
      606.3s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
      606.9s F3: goal: chest 1 at (226.0, 0.4, -38.0)
      638.1s F3: goal: chest 0 at (214.0, 0.4, -42.0)
      643.6s F3: goal: key chest 2 at (246.0, 0.4, -30.0)
      711.2s F3: goal: locked door at (252.0, 1.4, -6.0)
      714.3s F3: goal: piece 1 at (214.0, 0.6, -46.0)
      744.3s F3: goal: locked door at (252.0, 1.4, -6.0)
      777.3s F3: goal: locked door at (252.0, 1.4, -6.0)
      810.4s F3: goal: locked door at (252.0, 1.4, -6.0)
      843.7s F3: goal: locked door at (252.0, 1.4, -6.0)
      846.3s F3: goal: stair at (234.0, 0.0, -42.0)
      876.2s F3: goal: locked door at (252.0, 1.4, -6.0)
      886.4s F3: goal: stair at (234.0, 0.0, -42.0)
      908.7s F3: goal: locked door at (252.0, 1.4, -6.0)
      926.4s F3: goal: stair at (234.0, 0.0, -42.0)
      941.2s F3: goal: locked door at (252.0, 1.4, -6.0)
      966.5s F3: goal: stair at (234.0, 0.0, -42.0)
      976.5s F3: goal: locked door at (252.0, 1.4, -6.0)
     1006.5s F3: goal: stair at (234.0, 0.0, -42.0)
     1016.6s F3: goal: locked door at (252.0, 1.4, -6.0)
     1046.6s F3: goal: stair at (234.0, 0.0, -42.0)
     1056.7s F3: goal: locked door at (252.0, 1.4, -6.0)
     1086.7s F3: goal: stair at (234.0, 0.0, -42.0)
     1096.8s F3: goal: locked door at (252.0, 1.4, -6.0)
     1126.8s F3: goal: stair at (234.0, 0.0, -42.0)
     1136.9s F3: goal: locked door at (252.0, 1.4, -6.0)
     1166.8s F3: goal: stair at (234.0, 0.0, -42.0)
     1176.9s F3: goal: locked door at (252.0, 1.4, -6.0)
     1206.9s F3: goal: stair at (234.0, 0.0, -42.0)
     1217.0s F3: goal: locked door at (252.0, 1.4, -6.0)
     1247.0s F3: goal: stair at (234.0, 0.0, -42.0)
     1257.1s F3: goal: locked door at (252.0, 1.4, -6.0)
     1287.1s F3: goal: stair at (234.0, 0.0, -42.0)
     1297.2s F3: goal: locked door at (252.0, 1.4, -6.0)
     1327.2s F3: goal: stair at (234.0, 0.0, -42.0)
     1337.3s F3: goal: locked door at (252.0, 1.4, -6.0)
     1367.3s F3: goal: stair at (234.0, 0.0, -42.0)
     1377.4s F3: goal: locked door at (252.0, 1.4, -6.0)
     1407.4s F3: goal: stair at (234.0, 0.0, -42.0)
     1417.5s F3: goal: locked door at (252.0, 1.4, -6.0)
     1447.5s F3: goal: stair at (234.0, 0.0, -42.0)
     1457.6s F3: goal: locked door at (252.0, 1.4, -6.0)
     1487.5s F3: goal: stair at (234.0, 0.0, -42.0)
     1497.6s F3: goal: locked door at (252.0, 1.4, -6.0)
     1527.6s F3: goal: stair at (234.0, 0.0, -42.0)
     1537.7s F3: goal: locked door at (252.0, 1.4, -6.0)
     1567.7s F3: goal: stair at (234.0, 0.0, -42.0)
     1577.8s F3: goal: locked door at (252.0, 1.4, -6.0)
     1607.8s F3: goal: stair at (234.0, 0.0, -42.0)
     1617.9s F3: goal: locked door at (252.0, 1.4, -6.0)
     1647.9s F3: goal: stair at (234.0, 0.0, -42.0)
     1658.0s F3: goal: locked door at (252.0, 1.4, -6.0)
     1688.0s F3: goal: stair at (234.0, 0.0, -42.0)
     1698.1s F3: goal: locked door at (252.0, 1.4, -6.0)
     1728.1s F3: goal: stair at (234.0, 0.0, -42.0)
     1738.2s F3: goal: locked door at (252.0, 1.4, -6.0)
     1768.1s F3: goal: stair at (234.0, 0.0, -42.0)
     1778.2s F3: goal: locked door at (252.0, 1.4, -6.0)

## Bot findings (20)

- host: **Fall** F2 at (219.2, -1.6, -36.7) (seed 12345, room wine_press_hall cell (3,4)): the hero dropped below the floor (y -1.6); last 10 s: 75.75s (232.2, 0.0, -42.5) cc on grounded True up True tp False stick 0.2,-1.0 goal chest 4 | 76.00s (232.4, 0.0, -43.7) cc on grounded True up True tp False stick 0.2,-1.0 goal chest 4 | 76.25s (232.6, 0.0, -44.8) cc on grounded True up True tp False stick 0.0,0.0 goal chest 4 | 76.50s (232.7, 0.0, -44.9) cc on grounded True up True tp False stick 0.0,0.0 goal chest 4 | 76.75s (232.7, 0.0, -44.9) cc on grounded True up True tp False stick 0.0,0.0 goal chest 4 | 77.00s (232.7, 0.0, -44.9) cc on grounded True up True tp False stick 0.0,0.0 goal chest 4 | 77.25s (232.6, 0.0, -44.7) cc on grounded True up True tp False stick -0.3,0.9 goal chest 0 | 77.50s (232.2, 0.0, -44.0) cc on grounded True up True tp False stick -0.3,0.9 goal chest 0 | 77.75s (231.5, 0.0, -43.2) cc on grounded True up True tp False stick -0.3,1.0 goal chest 0 | 78.00s (231.1, 0.0, -43.0) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 78.25s (230.6, 0.0, -42.1) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 78.50s (230.4, 0.0, -40.9) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 78.75s (230.1, 0.0, -39.7) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 79.00s (229.8, 0.0, -38.5) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 79.25s (229.6, 0.0, -37.4) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 79.50s (229.3, 0.0, -36.2) cc on grounded True up True tp False stick -0.2,1.0 goal chest 0 | 79.75s (228.6, 0.0, -35.4) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 80.00s (227.4, 0.0, -35.4) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 80.25s (226.2, 0.0, -35.4) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 80.50s (225.0, 0.0, -35.3) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 80.75s (223.9, 0.0, -35.3) cc on grounded True up True tp False stick -0.9,-0.4 goal chest 0 | 81.00s (223.1, 0.0, -36.2) cc on grounded True up True tp False stick -0.5,-0.9 goal chest 0 | 81.25s (222.5, 0.0, -37.2) cc on grounded True up True tp False stick -0.5,-0.9 goal chest 0 | 81.50s (221.8, 0.0, -38.3) cc on grounded True up True tp False stick -0.5,-0.9 goal chest 0 | 81.75s (221.2, 0.0, -39.3) cc on grounded True up True tp False stick -0.5,-0.9 goal chest 0 | 82.00s (220.6, 0.0, -40.3) cc on grounded True up True tp False stick -0.6,-0.8 goal chest 0 | 82.25s (219.6, 0.0, -40.6) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 82.50s (218.4, 0.0, -40.6) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 82.75s (217.2, 0.0, -40.6) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 83.00s (216.0, 0.0, -40.6) cc on grounded True up True tp False stick -1.0,0.0 goal chest 0 | 83.25s (214.9, 0.0, -40.3) cc on grounded True up True tp False stick -0.3,1.0 goal chest 0 | 83.50s (214.9, 0.0, -39.2) cc on grounded True up True tp False stick 0.0,1.0 goal chest 0 | 83.75s (214.9, 0.0, -38.0) cc on grounded True up True tp False stick 0.1,1.0 goal chest 0 | 84.00s (215.1, 0.0, -36.8) cc on grounded True up True tp False stick 0.2,1.0 goal chest 0 | 84.25s (215.5, 0.0, -35.7) cc on grounded True up True tp False stick 0.5,0.9 goal chest 0 | 84.50s (216.2, 0.0, -34.8) cc on grounded True up True tp False stick 0.2,0.1 goal chest 0 | 84.75s (216.5, 0.0, -34.6) cc on grounded True up True tp False stick 0.2,0.0 goal chest 0 | 85.00s (217.6, 0.0, -35.0) cc on grounded True up True tp False stick 0.2,-0.1 goal chest 0 | 85.25s (217.9, 0.0, -35.2) cc on grounded True up True tp False stick 0.0,0.2 goal chest 0 | 85.50s (218.7, 0.0, -35.8) cc on grounded True up True tp False stick -0.9,0.5 goal chest 0
- host: **Nav** F2 at (218.8, -7.8, -35.5) (seed 12345, room wine_press_hall cell (3,3)): the hero is off the walkable area (no NavMesh within 2 m)
- host: **Door** F2 at (241.5, 0.0, -31.6): the door reads open (DoorOpen) but its gate object still blocks
- host: **Prompt** F2 at (229.6, 0.0, -39.0): standing 3.0 m from the checkpoint bank but the prompt is 'None' ('')
- host: **Prompt** F2 at (229.6, 0.0, -39.1): standing 2.9 m from the checkpoint bank but the prompt is 'None' ('')
- host: **Nav** F3 at (242.7, 0.0, -24.4) (seed 12345, room larder_smoke_gallery cell (1,2)): no walkable path from the hero to locked door at (252.0, 1.4, -6.0) (PathPartial)
- host: **Door** F3 at (252.6, 0.0, -8.9): the door reads open (DoorOpen) but its gate object still blocks
- host: **Stuck** F3 at (252.5, 1.6, -12.4) (seed 12345, cell (13,3)): walking to stair at (234.0, 0.0, -42.0): moved 0.00 m in 2.5 s; capsule blocked by CellarFloorSync(Clone)/CellarFloor_s12345_f3/Walls/Wall_12_2_East (BoxCollider, 0.08 m)
- host: **Stuck** F4 at (225.2, 0.0, -19.9) (seed 12345, cell (6,4)): walking to chest 3 at (222.0, 0.4, -22.0): moved 0.00 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.00 m)
- host: **Unreachable** F4 at (225.2, 0.0, -19.9) (seed 12345, cell (6,4)): gave up on chest 3 at (222.0, 0.4, -22.0) after 4 stuck spells (last blocker PlayerCapsule(Clone) (CapsuleCollider, 0.00 m))
- host: **Stuck** F4 at (225.2, 0.0, -19.9) (seed 12345, cell (6,4)): walking to chest 4 at (226.0, 0.4, -30.0): moved 0.00 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.00 m)
- host: **Unreachable** F4 at (225.2, 0.0, -19.9) (seed 12345, cell (6,4)): gave up on chest 4 at (226.0, 0.4, -30.0) after 4 stuck spells (last blocker PlayerCapsule(Clone) (CapsuleCollider, 0.00 m))
- host: **SoftLock** F4 at (243.5, 0.0, -21.9): solo hero downed for 30 s and nothing happened (no revive, no dive fail, no return to the hub)
- host: **Stuck** F0 at (0.0, 0.0, -5.2): walking to hub stair at (0.0, 0.0, 9.0): moved 0.05 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.00 m)
- host: **Unreachable** F0 at (0.0, 0.0, -5.2): gave up on hub stair at (0.0, 0.0, 9.0) after 4 stuck spells (last blocker PlayerCapsule(Clone) (CapsuleCollider, 0.02 m))
- host: **Stuck** F0 at (0.0, 0.0, -5.1): walking to hub stair at (0.0, 0.0, 9.0): moved 0.02 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.03 m)
- host: **Unreachable** F0 at (0.0, 0.0, -5.1): gave up on hub stair at (0.0, 0.0, 9.0) after 4 stuck spells (last blocker PlayerCapsule(Clone) (CapsuleCollider, 0.05 m))
- host: **Stuck** F0 at (0.0, 0.0, -5.1): walking to hub stair at (0.0, 0.0, 9.0): moved 0.02 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.02 m)
- host: **Stuck** F3 at (224.5, 0.0, -28.6) (seed 12345, cell (6,7)): walking to stair at (234.0, 0.0, -42.0): moved 0.00 m in 2.5 s; capsule blocked by PlayerCapsule(Clone) (CapsuleCollider, 0.00 m)
- host: **Unreachable** F3 at (224.5, 0.0, -28.6) (seed 12345, cell (6,7)): gave up on stair at (234.0, 0.0, -42.0) after 4 stuck spells (last blocker PlayerCapsule(Clone) (CapsuleCollider, 0.00 m))
