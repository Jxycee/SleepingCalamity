# M7 loop integration and tuning: proposal (2026-11-02)

Status: **PROPOSAL, for the M7 design session with the user.** No code yet. Settled inputs: PHASE1 section 2 (M7 row), 6.4 (co-op authority and latency), 6.5 (starting numbers, P23 co-op scaling, P24 downed), P44 (2-player is the tuned case, 3-4 get a smoke run), P7 (no weapon wear, revisit after the M7 playtest), the M6 decisions Q1-Q6 (`m6-square-loop-proposal.md`, section 3a) and scope amendments A32-A36. This note does not reopen those. It covers what they leave open.

M7's job is not new features. It closes the phase: the whole loop must hold up in real co-op, the numbers must feel right to the user, and every walk and proof that earlier milestones deferred lands here.

## 1. The loop, as M7 has to prove it
One session, 2 players on two machines (or one editor plus Multiplayer Play Mode virtual players):
1. Host loads the world in Hollowmere; the client joins by Relay code and sees the same town, stock and baker status.
2. Both dive from the cistern stair. Floors 1-4 build identically from the host seed; enemies, chests, keys, gate, pieces, healing points and the stir meter run host-side; melee feels instant for the attacker (6.4).
3. Hurt players heal at banks and healing points; a downed player is revived by an ally or the dive fails when everyone is down (P24). A downed player can use nothing (BUGS 5, fixed).
4. Bank at the floor-2 checkpoint and the floor-4 heart; the baker is rescued; the party goes home; the haul moves to town stock.
5. One builder at a time places the bakery frame, parts and the baker; the wishes tick for both players; the service opens; the next dive starts with 3 loaves each.
6. Save, quit, reload: town, stock, residents, dive progress and each player's weapon growth are back. A rejoining player keeps their growth (A34).
7. Repeat for three runs of floors 1-4; the user says whether they felt samey (7.2).
Then the same session with 3 and 4 players as a smoke run: no desync or error in the log, the scaling numbers hold (P23).

## 2. What exists vs what's missing
| Area | Exists (main at `f0b9c338`) | Missing for M7 |
|---|---|---|
| Walks, automated | `DiveWalkQaTests` (M5 floors 1-4, host + client), `SquareLoopWalkQaTests` (M6 loop incl. save/reload), `DiveSpineNetworkTests`, `CellarContentsNetworkTests`, `TownSyncNetworkTests`, `BakerLoavesNetworkTests`, `SaveFlowTests`; `DiveBot` solo bot (SC_BOT-gated) | A 2-player walk that chains the full loop twice (second loop from the reloaded world); a 3-4 client variant; walks only use teleports, never real input or real latency |
| Network simulator | `CombatNetworkTests` already drives the Multiplayer Tools `NetworkSimulator` (150 ms RTT, loss percent configurable) for hit registration | The same preset under the dive and placement paths (stair, bank, go home, chest, piece, placement request, revive hold); the real M3 manual walk at 150 ms and 2% loss with the user |
| Multiplayer Play Mode | Package `com.unity.multiplayer.playmode` 3.0.0 and `multiplayer.tools` 2.2.12 are in the manifest; `Tools/unity.ps1` runs batch tests only | A documented, working MPPM recipe for 2-4 virtual players on the PC (which scene, the dev panel flags, Relay vs direct), and what a batch run can and cannot replace |
| Co-op scaling | Core `CoopScaling` (P23 life multipliers, extra enemy per room, strike caps 2p 3 / 3p 4 / 4p 5), revenge cap by players, enemy AI stages 1-4 (threat table, aggro/leash, ring slots) | Numbers measured at 3 and 4 players on real floors (is 4p winnable and not trivial); the host's CPU and bandwidth with 4 heroes plus a full room of enemies |
| Solo and 2p balance | JSON tuning loader (`StreamingAssets/Tuning`), solo-balance session in flight, DiveBot as the solo yardstick | A 2-player bot (or scripted pair) as the second yardstick; the tuning-notes list from the user's playtest applied through the loader (data first, code only if a number has no knob) |
| Playtest bugs | `Reference/qa/playtest/BUGS.md` triaged 2026-11-02: only solo balance open | A fresh round of that triage after the 2p playtest; the traversal and VoidEdge re-scan |
| Save and rejoin | Per-section world save, conservation pass, `PlayerKey` weapon growth, quit mid-dive = failed dive | Real-client proofs: rejoin after a crash, a 5th player refused, a subset of the recorded four playing, a client disconnect mid-dive, mid-placement (lock released?), mid-bank |
| Soak and perf | Soak tier (1000+ seed sweeps), floor-generation goldens | A long co-op soak (a 30-60 minute 4-bot dive loop with the log scanned for warnings, errors, desync and allocation growth); a frame-time budget at 4 players on the PC |
| Repetition check | EditMode metric over 1,000 seeds | The user's three-run "samey?" verdict |
| Weapon wear | None (P7) | The call whether the loop wants a field-repair beat, from the playtest |

## 3. Open questions for the user
**Q1. How do we run the 3-4 player smoke test?**
- A. **In-process bots plus MPPM for the real look.** A PlayMode soak spawns host + 1-3 `TestClient`s driven by bots (the DiveBot input path), runs a full loop and scans the log. The user then plays 2 players for real and one 4-player MPPM session for feel.
- B. MPPM only, by hand.
- C. Two PCs only, 2 players, 3-4 left to bots.
- **Recommend A.** Bots catch desync, errors and perf drift in minutes and are repeatable; MPPM is for the user's eyes. Trade-off: bots teleport or use scripted input, so latency feel is only checked by the user's walk and the simulator preset.

**Q2. What is the pass bar for "no desync"?**
- A. **No `NetworkVariable`/RPC warning or error in the log, and a state hash match.** Every N seconds the host publishes a hash of the replicated dive and town state; clients compare and log a mismatch; the smoke run fails on any mismatch or warning.
- B. The log is clean (no hash).
- C. The user eyeballs it.
- **Recommend A.** It makes "no desync" a number, and the hash costs a few lines in test-only code. Trade-off: the hash must cover the right fields; the risk-reviewer lists them.

**Q3. How is tuning applied?**
- A. **One playtest, one notes file, one tuning branch.** The user plays and says what felt wrong in plain words; Claude maps each note to a JSON tuning key (or reports "no knob" and adds one); changes land as a single reviewed data change, then a DiveBot / 2p-bot re-run proves the loop is still winnable.
- B. Tune live during the playtest with an in-game panel.
- C. Tune in code.
- **Recommend A,** plus a read-only debug overlay (dive time, damage taken, heals used, loaves eaten) so notes come with numbers. Trade-off: B is faster per tweak but needs a panel and risks untracked changes.

**Q4. What defines a winnable 2-player dive?**
- A. **A bar on the bot and on the user:** the 2-player bot clears floors 1-4 in at least 1 of 4 runs and fails floor 1 in fewer than 1 of 4; the user clears it within three tries.
- B. Whatever the user thinks.
- C. Match solo.
- **Recommend A,** with 2-player as the tuned case (P44). Trade-off: bot skill is not user skill, so the bar is a guard rail and the user's verdict wins.

**Q5. Do we decide weapon wear after the playtest?**
- A. **Yes: the playtest ends with one question (did the loop want a field-repair beat?); default is no.** Anything yes goes to `PARKING_LOT.md`, not into M7.
- B. Build the gentle DC1 repair curve now.
- C. Drop it.
- **Recommend A.** Phase 1 stops at floor 4 and one resident; M7 is not the place to start a new system.

**Q6. Which network conditions are the contract?**
- A. **150 ms RTT, 2% loss (the plan's number) must feel right; 300 ms / 5% must not break state, only feel worse.**
- B. 150 ms / 2% only.
- C. Add a Relay-region test.
- **Recommend A.** Trade-off: the second preset costs one more simulator run per walk; it finds ordering bugs the first never will.

## 4. Staged plan
| Stage | Work | Done when | Tests | Review | Sessions |
|---|---|---|---|---|---|
| **S0 Netsim under the loop** | Run the existing simulator preset (150 ms / 2% and 300 ms / 5%) under `DiveWalkQaTests` and `SquareLoopWalkQaTests`; a `NetCondition` helper in `PlayModeHarness`; fix any ordering or timeout bug it finds (waits get timeouts and paced frames, as the Grace test did) | Both walks pass under both presets, 3 runs in a row | PlayMode (the two walks, parameterised) | risk-reviewer on any netcode fix | 1 |
| **S1 Replicated-state hash + N-client harness** | Test-only state hash of dive and town state published by the host and compared on clients; `PlayModeHarness` helpers for 3-4 `TestClient`s | A 4-client connect and a hash match in a smoke PlayMode test | PlayMode: 4 clients join, one dive start, hash equal on all peers; a deliberate corruption fails the test | reviewer | 1 |
| **S2 Pair and quad bots** | Extend `DiveBot` to run N bots on N clients through the real input path (not teleports) in `Arena_Dive` and Hollowmere; 2p yardstick (Q4) | 2p bot reaches floor 4 under the bar; a 4-bot loop runs with a clean log and equal hashes | `SC_BOT`-gated PlayMode (not in `check`), plus a short fixed-seed fast twin that is | reviewer | 1-2 |
| **S3 Disconnect and rejoin proofs** | Scripted client drops during dive, bank, placement (lock must release), go home; rejoin keeps weapon growth; a 5th player is refused; a subset of the recorded four plays | Each case ends with a consistent world and no stuck lock | PlayMode host + clients per case | risk-reviewer (netcode and save) | 1 |
| **S4 Soak and perf** | 30-60 minute 4-bot loop on the PC; log scan for warnings, errors, mismatch; frame time and GC per phase; budget notes | No warnings, no hash mismatch, no unbounded growth, the 4-player frame time inside the budget (a number agreed in S4 from the 1p/2p baseline) | `SC_SOAK` PlayMode, report in `Reference/qa/m7/` | reviewer | 1 |
| **S5 Debug overlay and tuning notes template** | Read-only overlay (dive time, damage taken, heals, loaves, stir, per-room clear time), `docs/design/m7-tuning-notes.md` template mapping each note to a JSON key | Overlay shows in dev builds only; every existing tuning key is listed | EditMode: overlay model; no PlayMode | reviewer | 1 |
| **S6 The user's 2-player playtest** | The user plays the whole loop three times (two PCs or MPPM), the netsim walk at 150 ms / 2%, then one 4-player MPPM session | Notes file filled; the "samey?" verdict recorded; weapon-wear question answered | Manual, `sc-walk`; script in `USER_TODO.md` | user | 1 (script) + user time |
| **S7 Apply tuning** | Notes to JSON keys (data first), re-run S2 bots to prove the bars, re-triage `BUGS.md`, rescan traversal; fix what the playtest found | Notes closed or parked; bars still met; `check` green | EditMode for new keys; the S2 bot bars | reviewer; risk-reviewer for any netcode fix | 1-2 |
| **S8 Close the phase** | `docs/PHASE1.md` status, `CURRENT.md`, parking lot for deferred items, M10 in-game step handed over | Every row of the M7 "done when" cell is ticked with evidence | `check` and `soak` on the merge tip | branch-reviewer | 1 |

S0 and S1 are independent and can run together. S2 needs S1. S3 needs S1. S4 needs S2. S5 can run beside any. S6 needs S0 (so latency is not a surprise) and a first S2 result (so the user does not spend time on a loop the bot cannot clear). S7 needs S6. About **8-11 sessions**, plus the user's playtest time.

## 5. Risks
- **Solo balance and 2p balance are still moving.** The solo-balance session changes enemy tables; S2's bars should be set after it merges, and S7 retunes the 2p case on top.
- **Bots are not players.** They teleport or hold ideal lines; a clean bot soak does not prove feel. The user's walk (S6) is the real gate; the bot is a regression guard.
- **Flaky PlayMode under load.** `-nographics` frames take about a millisecond, so latency eats timing windows (the Grace test, fixed with paced frames). S0 and S3 must pace every latency-sensitive wait, put a timeout in each, and avoid `WaitForEndOfFrame`.
- **One PlayMode run per machine** (UDP 7790 lock): S2-S4 soaks block other PlayMode runs; schedule them when no check is queued, and keep the hours-long soak off the shared lock until the user is away.
- **MPPM is manual and PC-only.** It cannot run in the cloud and has its own scene-load quirks; the recipe is part of S6's script, and the in-process N-client harness is the cloud-safe substitute.
- **4-player scale is a smoke run, not a tuned case (P44).** Failing to feel great at 4 players is a note for later, not a blocker; a desync or error is a blocker.
- **Save and rejoin edge cases are many** (disconnect during bank, lock holder leaves, host quits mid-dive). S3 lists them; anything not listed is unproven and says so in the report.
- **Scope creep from the playtest.** Notes that need a new system (wear, decorations, more residents, better combat depth) go to `PARKING_LOT.md`; M7 applies tuning, fixes bugs and closes the phase.
