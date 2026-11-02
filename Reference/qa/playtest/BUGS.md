# Alpha playtest bug list (2026-11-01)

How this was found:
- **Bot.** `DiveBot` (`Assets/_Project/Tests/PlayMode/Bot/`) plays real solo dives in Arena_Dive on the pad: the same input path a player uses, real prompts, real CharacterController. 7 runs: seeds 12345, 7, 31 and 404 in normal mode, and seeds 12345, 7 and 31 in "tough" mode (the host tops HP up below 50% so the run reaches floors 3-4). Run it with `SC_BOT=1 SC_BOT_OUT=<dir> bash Tools/cloud/unity.sh test-play --filter DiveBotTests --out <scratch>`.
- **Code read-throughs.** Three of them: dive flow and haul, floor contents and loot, combat/enemies/hero.
- **Traversal scan.** It now also checks for edges where the hero can walk off the level.

Confidence labels:
- **seen**: the bot hit it in play.
- **verified**: I traced the code path myself.
- **traced**: a reader traced it and I didn't re-check every step.
- **likely / speculative**: as stated.

Collision and layout bugs from the traversal scan (rebuilt buildings, chests and dressing with no colliders, the town-wall stairs to nowhere, and so on) are in `Reference/qa/traversal/FIXLIST.md` and are not repeated here.

## Status on main (triaged 2026-11-02)

- **FIXED, combat 76038dd7:** 6 parry farm, 7 buffered attack after dodge, 8 elite lock release, 15 dodge while guarding, 16 pre-Spawn NetworkVariable warnings, and the wall half of 2 (melee line of sight, owner + victim + host).
- **FIXED, enemy AI stages 3-4 (53890ee8, 5263d4c7):** the aggro/leash half of 2 (room-scoped aggro 12 m, leash 20 m, walk home).
- **FIXED, Cellars interaction 64bc2e99 + e8771574:** 12 ambush spawns, 13 range checks through walls, 18 gate key reachability, piece spheres vs piece ids, hunter respawn distance.
- **FIXED, dive flow d6154e73:** 3 floors 1-2 lost, 4 bank spam, 5 downed hero actions, 9 chest farming (fresh seed per dive), 10 same seed, 11 locked stair prompt, 14 short revive.
- **MITIGATED:** 18 healing point spent on a same-seed retry: every dive now draws a fresh seed and the point is keyed on (seed, floor), so a retry gets a ready point. Only a fixed `SeedOverride` (tests) replays the old case.
- **FIXED, Hollowmere traversal 056f80bd / 575d3e90:** 17 world edge (Saltwake road end collider, VoidEdge probe now measures real void). Re-confirm on the next scan.
- **OPEN:** 1 solo balance (solo-balance session, DiveBot still shows normal solo unwinnable). The dive-flow review's piece-spot cap worry is theoretical: `CellarContentsTests.PieceSpots_CountMatchesDefaults_AndCellsAreLegalAndDistinct` asserts every default floor gets all its spots across seeds; only a test that adds extra pieces beyond the defaults can hit it.
- **Left by design:** 18 one press teleports the whole party.

## Blocker / major

1. **Solo dives are close to unwinnable without help: the bot fails a dive every 40-70 s and never gets past floor 2** (major, seen).
   - Normal runs: seed 12345 failed 8 dives in 600 s, seed 7 failed 15, seed 31 failed 14, seed 404 failed 13 (re-run). Lowest HP 2%. Seed 12345 took 861 damage in 64 hits.
   - The same bot policy clears wave 1 of the combat arena (BotPlaytestTests).
   - Likely causes, both in item 2: every spawned enemy chases from any distance, and attacks ignore walls.
   - Every solo death fails the dive on the same tick (`DiveDirector.WipeTick`: one hero, all down). The hero is never visibly downed: the bot polled every frame and never saw `IsUp == false`.
   - Owner: combat / enemy AI.
2. **Enemies have no aggro range, leash or line of sight; attacks go through walls both ways** (major, verified for targeting, traced for hits).
   - `Enemy.PickTarget` (`Enemy.cs:312-328`) takes every standing hero in the world, and `AiBrain.PickTarget` picks the nearest one. Enemies steer straight at the hero with no NavMesh (`Enemy.cs:340-356`), so they grind on walls toward a hero in another room.
   - Melee hit checks are range + cone + height only, with no wall test, both for the hero's swings (`HeroCombat.cs:382-412`) and for incoming hits (`HeroCombat.cs:525-542`, `HitGeometry.cs:43-52`). Only the bow checks walls.
   - Repro: open a room so it spawns, step into the corridor, and get hit through the doorframe.
   - Owner: enemy AI.
3. **After the floor-2 checkpoint is banked, floors 1-2 are gone for good (the session, and since M6 S2 the world save: `DiveState.RestoreProgress` restores `CheckpointReached`)** (major, seen + verified).
   - In tough seed 7, the second dive after going home started on floor 3.
   - Cause: `DiveRoute.StartFloor(CheckpointReached)` (`DiveDirector.cs:106`, `DiveRoute.cs:11`). `CheckpointReached` is never cleared, and there is no floor choice.
   - This contradicts PHASE1 7.2 ("nothing is missable"; a return dive *may* start at the checkpoint). Any piece or chest missed on floors 1-2 is lost.
   - Owner: dive flow.
4. **Bank spam is an unlimited free full heal and stir reset** (major, verified).
   - `DiveState.BankAtPoint` has no "already banked / nothing to bank" guard. `DiveHaulSync.ServerBank` heals everyone (`:171-178`) and calls `Stir.OnBank` on every press.
   - The floor-2 prompt "Bank the haul at the checkpoint" never goes away.
   - Repro: stand at the checkpoint mid-fight and tap interact.
   - Owner: dive flow.
5. **A downed hero can still press stair, bank and go home, and so can a teammate starting a revive near them** (major, verified).
   - There is no downed input context: `InputContextRouter.InteractLive` is Free or LockOn. `DiveDirector.LocalInteract` (`:425-462`) never checks the local hero's vitals, and the host's `ServerTryStair`, `ServerTryBank` and `ServerTryGoHome` don't check that the asker is up.
   - Revive is a hold of the same button (`HeroCombat.cs:622-633`), and its first frame is the press edge that `DiveDirector` acts on. The stair prompt reaches 3.5 m and revive reaches 3 m.
   - Repro (co-op): a teammate goes down by the stair; you start the revive and the whole party descends instead.
   - Owner: dive flow.
6. **Parry growth can be farmed without limit on one enemy** (major, verified).
   - `CombatRewards.OnParry` (`CombatRewards.cs:115-124`) falls back to `abs = 1` once the per-enemy ledger cap pays 0, then applies the x1.5 tech bonus. Every parry keeps paying.
   - Repro: dagger, one slime, parry every attack.
   - Owner: combat (weapon growth).

## Minor

7. **A buffered attack press survives a dodge or a hit, so the hero swings on its own** (verified).
   - `AttackMachine.Interrupt` (`AttackMachine.cs:98-107`) does not clear `_buffered`, and an idle machine with a live buffer begins Hit1 (`:163`).
   - Repro: press attack early during a combo, then roll; a Hit1 starts when the roll ends.
8. **A hard lock on an elite never drops** (verified).
   - `TargetLock` (`:53-58`) skips the range and line-of-sight drops for elites. The camera stays glued to the Dough Warden through walls and rooms, and every swing turns toward it (`HeroCombat.cs:133-137`).
9. **Chest loot can be farmed on return dives** (seen + traced).
   - Chests re-arm closed on every arm (`CellarContentsSync.cs:312,334`) and the roll is fixed per seed, floor and chest. In the first bot run, 24 re-entries of floor 1 gave 103 chest opens.
   - Each dive after the checkpoint replays floors 3-4 with the same seed (item 3, and the seed is kept per session, `DiveDirector.cs:99-104`), so the same floor 3-4 chests can be looted and banked at the heart every return dive.
10. **Every dive in a session uses the same seed** (verified; may be intended): `_seed` is picked only on the first haul (`DiveDirector.cs:99-104`). Return dives replay identical floors.
11. **The stair prompt shows "Take the stair down" while the exit is still locked**, and every press is refused (verified, `DiveDirector.cs:443`). Only the 3 s refusal notice tells the player why.
12. **Ambush enemies can spawn right on the entering player** (likely).
    - The ambush ring is the far half of the room's free cells from the room centre (`CellarAmbush.cs:55-66`), which includes the cells inside the door. Nothing keeps spawns away from players.
    - Related: if the player leaves during the 0.75 s delay, the ambush still springs in the empty room (`CellarContentsSync.cs:442-446`).
13. **Range checks ignore walls** (verified). Chest 2.5 m, gate key 1.5 m and door 3 m are all flat distances (`CellarContentsSync.cs:530-587`). A chest by a wall can be opened from the next room, and once the room key is held, the locked door auto-opens for a player in the adjacent room.
14. **Revive started with under 3 s of downed timer always fails** (traced). `PlayerVitals.Tick` runs the downed timer down in the same tick as revive progress. Also: the revive hold always picks the first downed hero in reach (`HeroCombat.cs:622-636`).
15. **Holding the dagger guard eats the dodge press** (verified; may be intended): `AllowsDodge` requires `!_guarding` (`HeroCombat.cs:116`).
16. **Enemies write networked values before they are spawned: 318 NGO warnings per 25 minutes** (seen).
    - The warning is "NetworkVariable is written to, but doesn't know its NetworkBehaviour yet".
    - Cause: `CombatHost.SpawnEnemy` calls `Enemy.ServerSeed` (writes `NetMaxHp` and `NetHp`, `Enemy.cs:167-168`) before `instance.Spawn` (`CombatHost.cs:433-436`). It floods the log, and it is worth confirming that clients get the seeded HP.
17. **Hollowmere: you can walk off the edge of the world** (seen by the traversal scan).
    - Terrain edge at x ≈ -150: VoidEdge 228 samples around (-150, -19.9, -97) and 76 around (-150, -18.5, -33), in both presets. The walkable ground ends with nothing below.
    - Related: the Saltwake road's last node sits there (FIXLIST item 10).
    - The Cellars have none (200 floors checked).
18. **Speculative, from the readers:**
    - The gate key drops where the carrier dies, with no reachability fallback if the corpse ends up in a wall (`CellarContentsSync.cs:516,530`).
    - Piece spheres can't be collected if the haul's piece count differs from the floor's arm count (`CellarContentsSync.cs:363-364,551`).
    - The healing point stays spent on a same-seed retry (`DiveHaulSync.cs:219-221`).
    - The hunter respawns at its cell the tick it despawns, with no distance check from players (`CellarContentsSync.cs:487-496`).
    - One player's press teleports the whole party with no ready check (`DiveDirector.cs:299-323`; probably by design).

## Checked and fine

- **Presses:** double presses on stair, bank and go home advance once; prompt radii sit inside the host's accept radius.
- **Loot and keys:** chest-open races are host-serialised; the locked door needs the room key on both paths; keys and chests reset on every arm.
- **Pieces:** piece ids stay stable across a fail; piece, chest and heal cells exclude each other and chunk solids.
- **Enemies:** attack tokens don't leak.
- **Bot path:**
  - In tough mode the hub stair, chests, the gate key, the locked door, pieces, the checkpoint, the stair, the floor-4 heart bank and go home all worked on the pad.
  - No interact press failed, no pickup failed while standing on it, and the hero never ended up inside geometry.
  - After the bot's NavMesh timing fix, no falls out of the floor.

Not covered: 2-4 player sessions with real clients (the bot is solo), the town hub (M6 S3, not on main yet), animation and feel.
