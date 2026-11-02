# Co-op soak, quad (4 players), seed 12345

**Verdict: NOT CLEAN as a clean soak, but the bot-side blockers are fixed (2026-11-02, pass 1 = this report, party NOT topped up).** Stuck events fell from 148 to 0, the "door reads open but gate blocks" finding is gone, and the 23-minute park on floor 3 became repeated full dives. Coverage still fails the harness check: 5 dives started, 0 taken home, 4 failed (party wipe on floors 3-4: 16 downs, 7 out), floor 3 = 74% of the run (revisited each dive; the bot spends ~100 s per visit on the optional locked-door goal and then dies on 3-4). Hash 179/179, 0 errors/warnings, heap +0.06 MB/min flat. A second pass with SC_SOAK_TOUGH=1 (party healed below 50%, to reach bank and go-home) was started but the Unity editor wedged near the end of the run (frozen process, no report written), so no verdict for the cycling coverage yet.

Game time 1800 s of 1800 s planned, 184 s wall (0.10 wall s per game s), Time.captureFramerate 60, host DiveBot + 3 FollowerBots.

## Dives

- Dives started (host bot left the hub into floor 1): 5; taken home: 0; failed: 4 (low HP 4, near a floor/traversal fault 0); floors left: 8; deepest floor 4
- Hero downs 16 (client 0: 5, client 1: 4, client 2: 3, client 3: 4); out 7; revives 9; time downed 268 s
- Host bot: enemies killed 0, chests 23, pieces 6, stuck events 0, hearts banked: False

## Frame time per phase (real wall ms per frame, Stopwatch)

| Phase | Game s | Frames | Mean | p95 | p99 | Max | Managed alloc / frame |
|---|---:|---:|---:|---:|---:|---:|---:|
| hub | 14 | 856 | 1.7 | 1.9 | 3.4 | 127.2 | 7623 B |
| floor 1 | 100 | 6025 | 1.7 | 2.6 | 3.2 | 119.1 | 3321 B |
| floor 2 | 110 | 6606 | 1.8 | 2.5 | 3.1 | 212.2 | 4519 B |
| floor 3 | 1326 | 79508 | 1.7 | 2.3 | 2.8 | 168.2 | 2874 B |
| floor 4+ | 247 | 14845 | 1.7 | 2.2 | 2.7 | 127.2 | 3481 B |
| **all** | 1797 | 107840 | 1.7 | 2.3 | 2.8 | 212.2 | |

Frame time is the whole loop of the one process: four peers' simulation, the bots, the hash and this sampler, uncapped (no vsync wait). Managed alloc per frame is the sum of positive deltas of GC.GetTotalMemory(false) (a collection inside a frame hides some allocation, so it is a lower bound).

## Heap and object trend

- Managed heap after a forced collect: start 40.7 MB, end 47.3 MB (delta +6.6 MB); forced-collect samples every 120 s: 120s=44.2, 240s=46.2, 360s=46.3, 480s=47.0, 600s=47.1, 720s=46.5, 840s=46.4, 960s=46.6, 1080s=46.8, 1200s=46.3, 1320s=47.7, 1440s=47.4, 1560s=47.0, 1680s=47.5, 1800s=47.3; trend +0.06 MB/min
- Unforced heap (sawtooth) trend over 181 samples: +0.08 MB/min
- Host-side NetworkObjects: max 14, trend -0.007/min; enemies (all peers' copies) trend -0.037/min

| Game s | Floor | Heap MB (unforced) | NetworkObjects (host) | Enemies (host / all peers) |
|---:|---:|---:|---:|---:|
| 0 | 0 | 40.7 | 6 | 0 / 0 |
| 40 | 1 | 44.9 | 9 | 0 / 0 |
| 80 | 1 | 46.8 | 9 | 0 / 0 |
| 120 | 2 | 46.8 | 11 | 2 / 8 |
| 160 | 2 | 47.0 | 11 | 2 / 8 |
| 200 | 2 | 49.6 | 11 | 2 / 8 |
| 240 | 3 | 54.0 | 11 | 2 / 8 |
| 280 | 3 | 48.9 | 12 | 3 / 12 |
| 320 | 3 | 48.4 | 11 | 2 / 8 |
| 360 | 3 | 51.6 | 11 | 2 / 8 |
| 400 | 3 | 48.1 | 9 | 0 / 0 |
| 440 | 3 | 47.8 | 9 | 0 / 0 |
| 480 | 4 | 55.3 | 12 | 3 / 12 |
| 520 | 4 | 48.3 | 14 | 5 / 20 |
| 560 | 4 | 49.7 | 11 | 2 / 8 |
| 600 | 4 | 49.7 | 11 | 2 / 8 |
| 640 | 3 | 51.4 | 10 | 0 / 0 |
| 680 | 3 | 50.9 | 12 | 3 / 12 |
| 720 | 3 | 52.9 | 11 | 2 / 8 |
| 760 | 3 | 47.4 | 11 | 2 / 8 |
| 800 | 3 | 50.4 | 11 | 2 / 8 |
| 840 | 3 | 51.3 | 11 | 2 / 8 |
| 880 | 3 | 48.6 | 11 | 2 / 8 |
| 920 | 3 | 51.1 | 11 | 2 / 8 |
| 960 | 3 | 52.6 | 11 | 2 / 8 |
| 1000 | 3 | 47.7 | 11 | 2 / 8 |
| 1040 | 3 | 50.4 | 9 | 0 / 0 |
| 1080 | 3 | 52.1 | 12 | 3 / 12 |
| 1120 | 3 | 47.9 | 11 | 2 / 8 |
| 1160 | 3 | 50.8 | 10 | 1 / 4 |
| 1200 | 3 | 50.3 | 9 | 0 / 0 |
| 1240 | 3 | 47.9 | 9 | 0 / 0 |
| 1280 | 4 | 53.9 | 9 | 0 / 0 |
| 1320 | 4 | 53.5 | 11 | 2 / 8 |
| 1360 | 4 | 48.5 | 12 | 3 / 12 |
| 1400 | 3 | 49.8 | 9 | 0 / 0 |
| 1440 | 3 | 54.9 | 11 | 2 / 8 |
| 1480 | 3 | 48.1 | 11 | 2 / 8 |
| 1520 | 3 | 49.0 | 9 | 0 / 0 |
| 1560 | 3 | 49.7 | 9 | 0 / 0 |
| 1600 | 3 | 50.4 | 11 | 2 / 8 |
| 1640 | 3 | 49.1 | 11 | 2 / 8 |
| 1680 | 3 | 50.3 | 11 | 2 / 8 |
| 1720 | 3 | 50.0 | 10 | 1 / 4 |
| 1760 | 3 | 50.5 | 9 | 0 / 0 |
| 1800 | 3 | 52.9 | 9 | 0 / 0 |

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

- Observed overall p95 2.3 ms, p99 2.8 ms. Proposed regression budgets: p95 < 4 ms and p99 < 5 ms per phase (about 1.5x the observed values, to leave room for machine noise), measured by this soak on the same machine class.
- Managed allocation: budget against the table's B/frame per phase (propose 1.5x the observed hub and floor values), and a forced-collect heap slope of < 0.5 MB/min.
- Object count: host NetworkObjects must return to within a small margin of the hub baseline after each dive (the table above), trend < 0.05 per minute.

## Host bot log (tail)

      431.0s F3: goal: locked door at (252.0, 1.4, -6.0)
      453.5s F3: goal: stair at (234.0, 0.0, -42.0)
      467.2s F3: left floor 3 after 254 s
      467.2s F4: floor 4 seed 12345 preset  rooms 7 chests 7 locked False
      467.7s F4: goal: chest 0 at (222.0, 0.4, -14.0)
      480.6s F4: goal: chest 3 at (222.0, 0.4, -22.0)
      506.6s F4: downed (HP 0)
      515.0s F4: back up after 8 s, floor 4, dive active True
      515.7s F4: goal: heart bank at (218.0, 0.0, -46.0)
      627.2s F4: the dive ended without going home (fail #1); HP 100, last HP 5
      627.2s F4: left floor 4 after 160 s
      627.2s F0: goal: hub stair at (0.0, 0.0, 9.0)
      630.1s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
      630.6s F3: goal: chest 1 at (226.0, 0.4, -38.0)
      659.6s F3: goal: chest 0 at (214.0, 0.4, -42.0)
      664.1s F3: goal: key chest 2 at (246.0, 0.4, -30.0)
      742.4s F3: goal: locked door at (252.0, 1.4, -6.0)
      777.8s F3: goal: piece 1 at (214.0, 0.6, -46.0)
      827.7s F3: goal: locked door at (252.0, 1.4, -6.0)
      860.8s F3: goal: locked door at (252.0, 1.4, -6.0)
      870.0s F3: goal: stair at (234.0, 0.0, -42.0)
     1024.7s F3: the dive ended without going home (fail #2); HP 100, last HP 9
     1024.7s F3: left floor 3 after 395 s
     1024.7s F0: goal: hub stair at (0.0, 0.0, 9.0)
     1027.7s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
     1028.2s F3: goal: chest 1 at (226.0, 0.4, -38.0)
     1054.2s F3: goal: chest 0 at (214.0, 0.4, -42.0)
     1058.7s F3: goal: key chest 2 at (246.0, 0.4, -30.0)
     1131.8s F3: goal: locked door at (252.0, 1.4, -6.0)
     1136.9s F3: goal: piece 1 at (214.0, 0.6, -46.0)
     1173.1s F3: goal: locked door at (252.0, 1.4, -6.0)
     1206.1s F3: goal: locked door at (252.0, 1.4, -6.0)
     1239.1s F3: goal: locked door at (252.0, 1.4, -6.0)
     1268.5s F3: goal: stair at (234.0, 0.0, -42.0)
     1279.8s F3: left floor 3 after 251 s
     1279.8s F4: floor 4 seed 12345 preset  rooms 7 chests 7 locked False
     1280.3s F4: goal: chest 0 at (222.0, 0.4, -14.0)
     1288.8s F4: goal: chest 3 at (222.0, 0.4, -22.0)
     1302.2s F4: goal: heart bank at (218.0, 0.0, -46.0)
     1346.5s F4: downed (HP 0)
     1367.5s F4: the dive ended without going home (fail #3); HP 100, last HP 0
     1367.5s F4: back up after 21 s, floor 4, dive active False
     1367.5s F4: left floor 4 after 87 s
     1367.5s F0: goal: hub stair at (0.0, 0.0, 9.0)
     1370.3s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
     1370.8s F3: goal: chest 1 at (226.0, 0.4, -38.0)
     1401.0s F3: goal: gate key at (218.0, 0.0, -50.0)
     1404.0s F3: goal: chest 0 at (214.0, 0.4, -42.0)
     1405.7s F3: goal: key chest 2 at (246.0, 0.4, -30.0)
     1428.1s F3: downed (HP 0)
     1517.1s F3: the dive ended without going home (fail #4); HP 100, last HP 0
     1517.1s F3: back up after 29 s, floor 3, dive active False
     1517.1s F3: left floor 3 after 146 s
     1517.1s F0: goal: hub stair at (0.0, 0.0, 9.0)
     1519.9s F3: floor 3 seed 12345 preset Spine rooms 5 chests 7 locked True
     1520.4s F3: goal: chest 1 at (226.0, 0.4, -38.0)
     1547.5s F3: goal: gate key at (218.0, 0.0, -50.0)
     1550.6s F3: goal: chest 0 at (214.0, 0.4, -42.0)
     1552.2s F3: goal: key chest 2 at (246.0, 0.4, -30.0)
     1694.8s F3: downed (HP 0)

## Bot findings (12)

- host: **Prompt** F2 at (229.6, 0.0, -39.0): standing 3.0 m from the checkpoint bank but the prompt is 'None' ('')
- host: **Prompt** F2 at (229.6, 0.0, -39.1): standing 2.9 m from the checkpoint bank but the prompt is 'None' ('')
- host: **Embedded** F3 at (237.1, 0.0, -18.4) (seed 12345, room larder_smoke_gallery cell (0,0)): the hero's capsule sits inside CellarFloor_s12345_f3/Dressing/Barrels_r2_larder_smoke_gallery.s4_Stacked/BarrelTop for over a second
- host: **Nav** F3 at (243.0, 0.1, -28.0) (seed 12345, room larder_smoke_gallery cell (1,3)): no walkable path from the hero to locked door at (252.0, 1.4, -6.0) (PathPartial)
- host: **Fall** F4 at (237.4, -1.6, -19.4) (seed 12345, cell (9,4)): the hero dropped below the floor (y -1.6); last 10 s: 516.92s (228.8, 0.0, -23.7) cc on grounded True up True tp False stick 0.9,-0.5 goal heart bank | 517.17s (229.8, 0.0, -24.2) cc on grounded True up True tp False stick 0.9,-0.5 goal heart bank | 517.42s (230.9, 0.0, -24.8) cc on grounded True up True tp False stick 0.9,-0.5 goal heart bank | 517.67s (232.0, 0.0, -25.2) cc on grounded True up True tp False stick 0.9,-0.3 goal heart bank | 517.92s (233.1, 0.0, -25.6) cc on grounded True up True tp False stick 0.9,-0.3 goal heart bank | 518.17s (234.3, 0.0, -26.0) cc on grounded True up True tp False stick 0.9,-0.3 goal heart bank | 518.42s (235.4, 0.0, -26.4) cc on grounded True up True tp False stick 0.9,-0.3 goal heart bank | 518.67s (236.5, 0.0, -26.9) cc on grounded True up True tp False stick 0.7,-0.7 goal heart bank | 518.92s (237.6, 0.0, -27.0) cc on grounded True up True tp False stick 1.0,0.1 goal heart bank | 519.17s (238.8, 0.0, -26.8) cc on grounded True up True tp False stick 1.0,0.1 goal heart bank | 519.42s (240.0, 0.0, -26.7) cc on grounded True up True tp False stick 1.0,0.1 goal heart bank | 519.67s (239.3, 0.0, -26.2) cc on grounded True up True tp False stick 0.2,-0.1 goal heart bank | 519.92s (237.5, 0.0, -25.5) cc on grounded True up True tp False stick 0.8,-0.5 goal heart bank | 520.17s (236.3, 0.0, -25.2) cc on grounded True up True tp False stick 0.8,0.6 goal heart bank | 520.42s (237.1, 0.0, -24.8) cc on grounded True up True tp False stick 0.5,-0.8 goal heart bank | 520.67s (237.8, 0.0, -24.6) cc on grounded True up True tp False stick 0.5,0.9 goal heart bank | 520.92s (238.5, 0.0, -24.6) cc on grounded True up True tp False stick 0.5,-0.9 goal heart bank | 521.17s (239.0, 0.0, -25.7) cc on grounded True up True tp False stick 0.1,-0.2 goal heart bank | 521.42s (239.2, 0.0, -26.0) cc on grounded True up True tp False stick 0.4,-0.9 goal heart bank | 521.67s (239.4, 0.0, -25.7) cc on grounded True up True tp False stick 0.3,-1.0 goal heart bank | 521.92s (239.5, 0.0, -23.9) cc on grounded True up True tp False stick -1.0,0.1 goal heart bank | 522.17s (239.5, 0.0, -22.5) cc on grounded True up True tp False stick -0.2,-0.1 goal heart bank | 522.42s (238.3, 0.0, -22.5) cc on grounded True up True tp False stick 1.0,-0.2 goal heart bank | 522.67s (238.4, 0.0, -22.6) cc on grounded True up True tp False stick 0.4,-0.9 goal heart bank | 522.92s (238.9, 0.0, -23.3) cc on grounded True up True tp False stick -0.6,0.8 goal heart bank | 523.17s (237.9, 0.0, -21.9) cc on grounded True up True tp False stick 1.0,-0.2 goal heart bank | 523.42s (236.9, 0.0, -21.0) cc on grounded True up True tp False stick 0.9,-0.3 goal heart bank | 523.67s (236.6, 0.0, -20.9) cc on grounded True up True tp False stick 0.4,-0.9 goal heart bank | 523.92s (237.2, 0.0, -21.9) cc on grounded True up True tp False stick 0.5,-0.9 goal heart bank | 524.17s (237.7, 0.0, -22.9) cc on grounded True up True tp False stick 0.4,-0.9 goal heart bank | 524.42s (238.4, 0.0, -23.7) cc on grounded True up True tp False stick 0.6,-0.8 goal heart bank | 524.67s (237.8, 0.0, -21.8) cc on grounded True up True tp False stick 0.0,0.0 goal heart bank | 524.92s (237.2, 0.0, -21.0) cc on grounded True up True tp False stick 0.9,0.5 goal heart bank | 525.17s (237.2, 0.0, -21.0) cc on grounded True up True tp False stick 1.0,0.1 goal heart bank | 525.42s (236.6, 0.0, -20.6) cc on grounded True up True tp False stick 0.5,-0.9 goal heart bank | 525.67s (236.6, 0.0, -20.6) cc on grounded True up True tp False stick 0.5,-0.9 goal heart bank | 525.92s (236.7, 0.0, -20.8) cc on grounded True up True tp False stick 0.1,-0.2 goal heart bank | 526.17s (236.8, 0.0, -21.1) cc on grounded True up True tp False stick 0.1,-0.2 goal heart bank | 526.42s (236.8, 0.0, -21.2) cc on grounded True up True tp False stick -0.1,1.0 goal heart bank | 526.67s (237.2, -0.8, -19.4) cc on grounded False up True tp False stick 0.8,-0.5 goal heart bank
- host: **Nav** F4 at (238.6, -17.9, -22.7) (seed 12345, room bakery_kneading_hall cell (0,0)): the hero is off the walkable area (no NavMesh within 2 m)
- host: **Embedded** F3 at (251.7, 0.1, -36.5) (seed 12345, cell (12,9)): the hero's capsule sits inside CellarFloorSync(Clone)/CellarFloor_s12345_f3/Walls/Wall_12_9_East for over a second
- host: **Fall** F3 at (251.9, -1.6, -34.3) (seed 12345, cell (12,8)): the hero dropped below the floor (y -1.6); last 10 s: 799.85s (247.4, 0.0, -47.4) cc on grounded True up True tp False stick 0.5,0.9 goal piece 1 | 800.10s (247.4, 0.0, -47.3) cc on grounded True up True tp False stick 0.5,0.9 goal piece 1 | 800.35s (247.4, 0.0, -47.2) cc on grounded True up True tp False stick -1.0,-0.2 goal piece 1 | 800.60s (247.8, 0.0, -46.7) cc on grounded True up True tp False stick 0.9,-0.5 goal piece 1 | 800.85s (247.9, 0.0, -46.5) cc on grounded True up True tp False stick 0.8,-0.5 goal piece 1 | 801.10s (248.3, 0.0, -46.2) cc on grounded True up True tp False stick -0.6,-0.8 goal piece 1 | 801.35s (248.9, 0.0, -45.2) cc on grounded True up True tp False stick 0.6,-0.8 goal piece 1 | 801.60s (249.5, 0.0, -43.6) cc on grounded True up True tp False stick 0.2,-1.0 goal piece 1 | 801.85s (250.1, 0.0, -42.4) cc on grounded True up True tp False stick 0.1,-1.0 goal piece 1 | 802.10s (250.2, 0.0, -43.3) cc on grounded True up True tp False stick 0.0,-1.0 goal piece 1 | 802.35s (249.9, 0.0, -44.4) cc on grounded True up True tp False stick -0.4,-0.9 goal piece 1 | 802.60s (249.9, 0.0, -44.2) cc on grounded True up True tp False stick 0.2,-1.0 goal piece 1 | 802.85s (250.2, 0.0, -42.5) cc on grounded True up True tp False stick 0.8,0.6 goal piece 1 | 803.10s (250.4, 0.0, -41.0) cc on grounded True up True tp False stick -0.8,0.6 goal piece 1 | 803.35s (249.7, 0.0, -41.1) cc on grounded True up True tp False stick 0.3,-1.0 goal piece 1 | 803.60s (249.3, 0.0, -42.1) cc on grounded True up True tp False stick 0.6,-0.8 goal piece 1 | 803.85s (250.0, 0.0, -40.7) cc on grounded True up True tp False stick 0.3,-0.9 goal piece 1 | 804.10s (250.8, 0.0, -39.1) cc on grounded True up True tp False stick 0.1,-1.0 goal piece 1 | 804.35s (251.3, 0.0, -38.7) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 804.60s (251.2, 0.0, -39.8) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 804.85s (250.8, 0.0, -40.9) cc on grounded True up True tp False stick 0.0,-1.0 goal piece 1 | 805.10s (251.4, 0.0, -40.2) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 805.35s (251.4, 0.0, -38.8) cc on grounded True up True tp False stick -1.0,0.3 goal piece 1 | 805.60s (251.4, 0.0, -37.8) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 805.85s (251.4, 0.0, -38.1) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 806.10s (251.4, 0.0, -37.1) cc on grounded True up True tp False stick -0.8,-0.6 goal piece 1 | 806.35s (251.4, 0.0, -36.6) cc on grounded True up True tp False stick -0.7,-0.7 goal piece 1 | 806.60s (251.2, 0.0, -36.9) cc on grounded True up True tp False stick 0.4,-0.9 goal piece 1 | 806.85s (251.3, 0.0, -37.3) cc on grounded True up True tp False stick 0.7,-0.7 goal piece 1 | 807.10s (251.4, 0.0, -38.2) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 807.35s (251.4, 0.0, -36.6) cc on grounded True up True tp False stick -0.1,-1.0 goal piece 1 | 807.60s (251.4, 0.0, -36.6) cc on grounded True up True tp False stick -1.0,0.0 goal piece 1 | 807.85s (251.0, 0.0, -36.6) cc on grounded True up True tp False stick -1.0,0.0 goal piece 1 | 808.10s (251.5, 0.1, -36.5) cc on grounded True up True tp False stick 0.1,1.0 goal piece 1 | 808.35s (251.7, 0.1, -36.5) cc on grounded True up True tp False stick 1.0,-0.1 goal piece 1 | 808.60s (251.7, 0.1, -36.5) cc on grounded True up True tp False stick -1.0,0.0 goal piece 1 | 808.85s (251.7, 0.1, -36.5) cc on grounded True up True tp False stick 1.0,0.2 goal piece 1 | 809.10s (251.7, 0.1, -36.5) cc on grounded True up True tp False stick 1.0,-0.1 goal piece 1 | 809.35s (251.7, 0.1, -36.5) cc on grounded True up True tp False stick 0.6,0.8 goal piece 1 | 809.60s (251.8, -1.3, -34.5) cc on grounded False up True tp False stick -0.2,-1.0 goal piece 1
- host: **Embedded** F3 at (236.6, 0.0, -18.3) (seed 12345, room larder_smoke_gallery cell (0,0)): the hero's capsule sits inside CellarFloorSync(Clone)/CellarFloor_s12345_f3/Dressing/Barrels_r2_larder_smoke_gallery.s4_Stacked for over a second
- host: **SoftLock** F3 at (239.1, 0.0, -27.8): solo hero downed for 30 s and nothing happened (no revive, no dive fail, no return to the hub)
- host: **Fall** F3 at (235.6, -1.6, -37.6) (seed 12345, cell (8,9)): the hero dropped below the floor (y -1.6); last 10 s: 1571.41s (236.6, 0.0, -30.9) cc on grounded True up True tp False stick 0.2,1.0 goal key chest 2 | 1571.66s (236.6, 0.0, -31.8) cc on grounded True up True tp False stick 0.0,1.0 goal key chest 2 | 1571.91s (236.8, 0.0, -31.3) cc on grounded True up True tp False stick -0.8,-0.6 goal key chest 2 | 1572.16s (236.6, 0.0, -32.3) cc on grounded True up True tp False stick 0.9,0.5 goal key chest 2 | 1572.41s (236.6, 0.0, -33.3) cc on grounded True up True tp False stick 0.7,0.7 goal key chest 2 | 1572.66s (236.7, 0.0, -33.4) cc on grounded True up True tp False stick 0.7,0.7 goal key chest 2 | 1572.91s (237.4, 0.0, -32.4) cc on grounded True up True tp False stick 0.6,0.8 goal key chest 2 | 1573.17s (237.7, 0.0, -31.4) cc on grounded True up True tp False stick 0.7,0.7 goal key chest 2 | 1573.42s (237.6, 0.0, -32.7) cc on grounded True up True tp False stick 0.4,0.9 goal key chest 2 | 1573.67s (237.4, 0.0, -33.3) cc on grounded True up True tp False stick 0.8,0.6 goal key chest 2 | 1573.92s (237.0, 0.0, -33.7) cc on grounded True up True tp False stick -0.1,1.0 goal key chest 2 | 1574.17s (237.2, 0.0, -33.4) cc on grounded True up True tp False stick 1.0,0.2 goal key chest 2 | 1574.42s (236.6, 0.0, -34.3) cc on grounded True up True tp False stick 0.1,1.0 goal key chest 2 | 1574.67s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 0.7,0.7 goal key chest 2 | 1574.92s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 0.7,0.7 goal key chest 2 | 1575.17s (237.4, 0.0, -34.8) cc on grounded True up True tp False stick 0.9,-0.3 goal key chest 2 | 1575.42s (237.8, 0.0, -34.9) cc on grounded True up True tp False stick -1.0,-0.1 goal key chest 2 | 1575.67s (237.1, 0.0, -35.0) cc on grounded True up True tp False stick -0.2,-1.0 goal key chest 2 | 1575.92s (236.7, 0.0, -35.4) cc on grounded True up True tp False stick 1.0,0.3 goal key chest 2 | 1576.18s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 0.1,1.0 goal key chest 2 | 1576.43s (236.6, 0.0, -35.2) cc on grounded True up True tp False stick 0.1,1.0 goal key chest 2 | 1576.68s (236.6, 0.0, -35.1) cc on grounded True up True tp False stick 0.0,0.2 goal key chest 2 | 1576.93s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 0.2,-1.0 goal key chest 2 | 1577.18s (236.6, 0.0, -35.2) cc on grounded True up True tp False stick 0.1,1.0 goal key chest 2 | 1577.43s (236.7, 0.0, -34.3) cc on grounded True up True tp False stick 0.1,-1.0 goal key chest 2 | 1577.68s (236.6, 0.0, -34.2) cc on grounded True up True tp False stick 0.1,0.2 goal key chest 2 | 1577.93s (236.9, 0.0, -34.2) cc on grounded True up True tp False stick -0.1,-1.0 goal key chest 2 | 1578.18s (236.9, 0.0, -34.2) cc on grounded True up True tp False stick -0.1,-1.0 goal key chest 2 | 1578.43s (236.7, 0.0, -34.2) cc on grounded True up True tp False stick 0.0,0.0 goal key chest 2 | 1578.68s (236.6, 0.0, -34.6) cc on grounded True up True tp False stick -1.0,0.1 goal key chest 2 | 1578.93s (236.6, 0.0, -35.1) cc on grounded True up True tp False stick 1.0,-0.3 goal key chest 2 | 1579.19s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 1.0,0.1 goal key chest 2 | 1579.44s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 1.0,0.1 goal key chest 2 | 1579.69s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 1.0,0.1 goal key chest 2 | 1579.94s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick 1.0,0.1 goal key chest 2 | 1580.19s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick -0.2,1.0 goal key chest 2 | 1580.44s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick -0.2,1.0 goal key chest 2 | 1580.69s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick -0.2,1.0 goal key chest 2 | 1580.94s (236.6, 0.0, -35.4) cc on grounded True up True tp False stick -0.2,1.0 goal key chest 2 | 1581.19s (235.7, -1.3, -37.4) cc on grounded False up True tp False stick 0.4,0.9 goal key chest 2
- host: **Nav** F3 at (231.7, -32.4, -40.7) (seed 12345, cell (7,10)): the hero is off the walkable area (no NavMesh within 2 m)
