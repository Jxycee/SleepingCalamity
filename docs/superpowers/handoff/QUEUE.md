# Work queue (any hour)

One queue for day and night (user, 2026-11-06; replaces `overnight-queue.md`). Built from a full open-work scan on 2026-11-06 (spec section 12, PARKING_LOT, BUGS, triage notes, session STATUS files, decisions-for-review).

## Rules
- Take the top open item in **Ready** that nothing else is touching, plus the related items that land with it: one M item with its S follow-ups, or 3-5 related S items, per fresh session (`session-usage.md`, "The game comes first"). Tick items in the merge commit that lands them, not in a commit of their own.
- Size: **S** = under ~1 h, docs/tools/small code, at most one check. **M** = a few hours, one implementer run + review + check. **L** = multi-task plan, risk review, or art rounds.
- Overnight: anything may run, but start with S items, and check usage (`get_usage`: weekly vs the even line, 5 h window) before starting an M or L. Workday break still stops everything.
- `[U]` = needs Unity (PC: one PlayMode run at a time). `[A]` = art/Blender. `[net]` = networking/save, needs `risk-reviewer`.
- Mark done as `- [x]` with the merge commit; never delete lines in the same pass. Refill when fewer than ~10 Ready items are open. The board counts open `- [ ]` lines for its idle alarm.
- Sources are cited so a session can read the original note first.

## Ready: dive build (critical path after the playtest)
- [x] **M** Dungeon spec step 5a: encounter templates for the 12 v2 rooms (spec `2026-11-03-hearthdeep-dungeon-design.md` section 12 step 5; `docs/ROOM_METRICS.md`, `CellarRoomsV2`) - write plan first, then code [U] (`4dee555f`)
- [x] **M** Wire the host spawner to encounter templates (after spec 5a merges; `EncounterBuilder`, Ambush hook = `CellarEncounterPlan.AmbushRoom == roomIndex`; first add a golden template-pick test for a fixed (seed, floor, room) and a comment that the solo cap uses `Base.Spawns.Count` while live plan counts vary by twist: 5a reviewer nits 1, 4) [U][net] (`edf1d82d`)
- [ ] **S** Spawner follow-ups (risk review `Reference/sessions/spawner-wiring/STATUS.md`): wave 1 has no player-distance safety (`CellarContentsSync` TickWaveTwo); wave 1 dropped when wave 0 placed nothing + missing carrier-lost reset; PlayMode test of the two-wave handoff; exclude the hunter cell from template cells [U][net]
- [ ] **L** Spec step 5b: water gimmick states on the 12 rooms (flood/drain), validator coverage via `RoomValidatorV2` [U]
- [ ] **M** Spec step 5c: deferred floor modifiers Brimming, Fat Larder, Still Air (`Reference/sessions/plan5-floor-card/STATUS.md:12`) [U]; dungeon-1 guardrail: keep them light and readable (user 2026-11-06)
- [x] **S** Dungeon-1 modifier difficulty check: DiveBot solo runs with plan 5 modifiers on vs off; if they add much difficulty, soften (e.g. floor 7 one hazard at most, Stirred's latch ends at the next stair). Guardrail: user 2026-11-06, dungeon one must not be extremely complex or overly difficult [U] (`5d2dcf8c`, results `Reference/sessions/dungeon1-difficulty/RESULTS.md`: modifiers on/off within noise, party-size fix `e9e7d6db`)
- [x] **S** Floor 3 deaths: plain DiveBot (no healing) dies on F3 on every seed, modifiers on or off (49/30/33 fails); seed 7 logs 'took damage with no enemy within 4 m' on F3. Find the damage source (hazard, hitbox reach or bug) and judge it against the dungeon-one guardrail [U] (`e59bbfde`: not a bug, F3 rat_king set piece = 252 of 331 hits; proposal F3 mix 20/50/30 to 30/50/20 waits on the user)
- [x] **S** Coop SoftLock finding on F3: 'solo hero downed 30 s and nothing happened' (CoopBot_Pair_Seed12345); the downed player should be revived or the dive should fail [U][net] (`e59bbfde`: out heroes stand up when the party takes the stair; risk review no blockers)
- [ ] **S** Live 2-4 player bot run on the party-size fix (`e9e7d6db`): enemy count rises with players, no hash mismatches [U][net]
- [x] **S** Spec step 5d: music stems plan (docs: which stems, where they switch; no audio yet) (`141a79a4`)
- [ ] **M** Spec step 6: jobs for dead ends, collapse fairness, crash saver if simple (section 12 step 6) - plan first [U]
- [x] **S** Floors 8-10 of the Undercroft have no plan row (spec :233 says 10 floors; plan 3 built 7): write the roadmap row (docs) (`141a79a4`)
- [ ] **M** Secret-idol paid boss retry (spec Q3 / decision 25): no build row found; confirm in code, plan + build if missing [U][net]
- [ ] **S** Confirm `RoomValidatorV2` checks every gimmick state (spec Q5 F7); close or file the gap [U]
- [x] **S** Rewrite `m7-proposal` sections 1 and 4 for the 7-floor dive (triage notes :7) (docs) (`141a79a4`)
- [ ] **S** DiveBot Tough Seed12345 run: bar needs 2 of 3 seeds finished (triage :67) [U]

## Ready: bugs and test health
- [ ] **M** BUGS item 1: solo dives unwinnable for the bot (fails in 40-70 s, never past floor 2); re-measure on main since DiveBot pace `235164ee`, then tune or close (`Reference/qa/playtest/BUGS.md:36`) [U]
- [ ] **S** BUGS item 18a: gate key dropped inside a wall has no fallback (`BUGS.md:93`) [U][net]
- [ ] **S** BUGS item 18b: piece spheres uncollectable when haul piece count differs from floor arm count (`BUGS.md:94`); may be stale after plan 3 (`dive-flow/STATUS.md:12`) [U]
- [ ] **S** `DiveSpineNetworkTests` floor-7 host-hero drift (~2 m/s after Move, unexplained) + `Floor5_RealButtonPushOn` flaked once (`plan5-floor-card/STATUS.md:24-26`) [U]
- [x] **S** plan5 nits: "Find the gate key" on an opened key floor; pieces line duplicates `StirMeterHud`; follower bots don't split valves (`plan5-floor-card/STATUS.md:27`) [U] (`b76f862b`; follower-bot valve split moved to its own M item)
- [ ] **M** Co-op follower bots split valves: FollowerBot has no goal pathing; needs nav + a hold loop (plan5-nits STATUS) [U]
- [x] **S** Floor-7 valve reach 1.8 m / 0.6 s hold lost to knockback: tuning note or fix (`plan5-floor-card/STATUS.md:25`) [U] (`b76f862b`: hold survives a <1 s blip, ReportTimeout 0.6 -> 1.0)
- [ ] **S** Flaky `DiveWalkQaTests.Walk_Floor1ToTheGeneratedPieceFloor_..._Descend(Rtt300Loss5)`: 40 s timeout waiting for the client hero, passed on re-run (plan5-nits PC check, 2026-11-06) [U][net]
- [ ] **S** `StairHandoverTests` should destroy `TestHubGround` in teardown (PARKING_LOT :56) [U]
- [ ] **S** Test gap: host's own bag saved and reloaded (PARKING_LOT :83) [U][net]
- [x] **S** combat-polish follow-ups: parry-during-grace test; HurtGrace values into `TuningSections`; dead `HeroAnimMap.IsHeavyHurt` / `HurtGrace.Knockdown` (`combat-polish/STATUS.md:13-16`) [U] (`0e8ace4a`; attack-id tuning split out as its own M item)
- [ ] **S** traversal-2 reviewer follow-ups: VoidEdge ray reaches 42 m; lane tolerance 0.7 vs 0.2 m; EditMode guard for `HollowmereRuinSeal` (`traversal-2/STATUS.md:36-38`) [U]
- [x] **S** `TuningSections.cs:101` points at a TODO that no longer exists; enemy attack-id tuning not tunable (code) (`0e8ace4a`; attack-id tuning split out as its own M item)
- [ ] **M** Enemy attack ids tunable: AttackCatalog enemy attacks are static readonly AttackData held by reference (EnemyDefinition.Attacks, AllEnemyAttacks, EnemyAttackById, AI/tell rules); needs a registry or replace-in-place + EnemyAttackRules validation (combat-tuning-followups STATUS, Skipped) [U]
- [ ] **M** Builder bug: MotionVectors pass flips between graphics and `-nographics` builds (CURRENT Inbox) [U]

## Ready: per-player loot and save follow-ups (PARKING_LOT 2026-11-03)
- [x] **S** Close stale entries after checking code: satchel placement (:65, `LostSatchel.ShouldPlaceOn` exists), legacy DiveHaul retire (:68, class gone), per-player gold source (:78), stir thresholds (:75, :79, `DiveStir`), generator v1 vs v2 (`rooms-v2/STATUS.md:4`) (docs) (`605d2ba0`; enemy-kill gold, stash view, generator v2 switch stay open in PARKING_LOT)
- [ ] **S** Stale "shared haul" comments (2 left: `AssetPaths.cs:18`, `PrefabBuilder.cs:38`; PHASE1 7.2 done `605d2ba0`) - bundle with the next Assets change, it needs a check (PARKING_LOT :80)
- [ ] **S** HUD "N home" undercounts pieces after reload; `Summary.BankedPieces` comment wrong (:81) [U]
- [ ] **M** Bag robustness bundle: sticky `PlayerBagSync._bagId` re-resolve (:71); one bad value drops a whole bag entry (:72); satchel vs `PlayerBag.MaxCarried` (:73) [U][net]
- [ ] **M** Bank slot edge cases: party shrinks mid-floor (:70); late joiner lifts slots above `BankSlots.TotalSlots` (:76); 3-player extra-slot owner not saved (:66) [U][net]
- [ ] **M** Per-player enemy drops via `DiveHaulSync.ServerAddItem` (:64) [U][net]
- [ ] **M** Dev-only bank keys: confirm push-on / go-home have real input everywhere (CURRENT Inbox; bank-input `dc8a20f2` may have closed it) [U]

## Ready: multiplayer readiness (M7)
- [ ] **M** 3-4 player smoke with bots + MPPM (triage Q1) [U][net]
- [ ] **M** State-hash "no desync" pass bar: add per-player bags and the 7-floor stir to the hash (triage Q2) [U][net]
- [ ] **S** Network conditions test set at 150 ms / 2% and 300 ms / 5% (triage Q6 default; user may change) [U][net]

## Ready: tools and process
- [x] **S** `sc_core_tests.py` false green on `Core.Combat` (`8d823e23`: 105 of 157 EditMode files, 1205 tests pass)
- [x] **S** `unity.ps1 exec` false FAIL on player builds (`cd4eec4e`)
- [ ] **S** Verify the exec fix on the next player build (it was not re-run after `cd4eec4e`)
- [x] **S** `Reference/hollowmere/QUEUE.md` stale: Q7, Q9, Q10, Q12-Q15 say unclaimed but are DONE (docs) (`605d2ba0`)
- [ ] **S** `sc-walk` documents WalkDriver helpers that don't exist: port them or fix the skill (CURRENT blockers)
- [ ] **M** Legacy FishGame asset tools rework (`sc-asset-craft` section 0b)
- [ ] **S** churn follow-ups: `M_CrackWeeds.mat` commit, PC run of `sc_churn_check.sh` (`hollowmere/churn/STATUS.md:18-19`) [U]
- [ ] **S** Linux float churn in 3 scenes + `M_HM_Surf_WornRun.mat`: find the builder that writes non-deterministic floats (cloud-tasker STATUS) [U]
- [ ] **S** `StallLodCapture` restore in `finally`; delete stale `M_LK_StepNosing.mat` (town-art-leftovers STATUS :15) [U]

## Ready: Hollowmere art
- [x] **M** Regenerate Inn / Cottage / Townhouse FBX with the fixed kit UVs; RetainWallLow `LK0727`/`LK0669` sandstone 2.53 (CURRENT, kit UV regen `bc520027`) [A][U] (`eb2ee73d`, uv_stretch 4 -> 0)
- [ ] **L** Wide junction survey: every unnatural join, ~60-camera outside sweep (CURRENT Next 3; town-art-leftovers :31) [A][U]
- [ ] **M** Traversal islands (17.7 m2, 32.5 m2), stuck pockets, corners R2_03_C3 / R2_08_C3 collider probe, Blocked +3 near lower gate (traversal-2 :41, stairs-traversal-3 :18) [U]
- [ ] **M** Ivy / moss embedding, wall-breach weeds, stall-prop overlaps, uv_density x4 (town-art-leftovers :18) [A][U]
- [ ] **M** Town review 2 (CURRENT Next 3) [A][U]
- [ ] **S** Stairs: A9 still reads as a ramp; black sliver on the Terrace flight edge; textured stone nosing (town-surfaces :4, stairs-traversal-3 :25) [A][U]
- [ ] **M** Square leftovers: Market Street stub pale, dusk row dark, Rebuilt planters thin, HM_HomeDoor recapture, low bench (`hollowmere/square/STATUS.md:12`) [A][U]
- [ ] **L** Ring 2 L lots still greybox boxes (`hollowmere/ring2/STATUS.md:19`) [A][U]
- [ ] **S** Polish nits: scarecrow colour + hat-brim shadow, weir broken-foam shader, StairFlight duplicate UVs, dark streaky bank under the west wall (polish/polish2/polish3 STATUS) [A][U]
- [ ] **M** town-visual-polish leftovers A1, A2, A6, A8-A14, hanging ivy, annex board (STATUS :7) [A][U]
- [ ] **S** Far tree line / hedge for low cameras (`FarHedge` unused; PARKING_LOT :52) [A][U]
- [ ] **S** road2 LowSun: R3_02 house face and Lower Gate bank saturate orange (PARKING_LOT :53) [U]
- [ ] **L** Saltwake road 2: parked after round 1 (`hollowmere/road2/STATUS.md`) [A][U]
- [ ] **S** Butterflies and bees faint at eye height (ambient-life STATUS :3; 1 round left) [U]

## Ready: Cellars art
- [ ] **M** B5 cellar kit 29/40 to 32 (`hollowmere/dungeon_corner/STATUS.md:9`) [A][U]
- [ ] **S** Dust-motes ParticleSystem prefab (`CellarFloorBuilder.cs:8`, :416) [U]
- [ ] **S** B11 cellar lantern wear and rust (PARKING_LOT :49) [A]
- [ ] **S** B6 plaza ground jitter / wet band / moss rocks; B7 props polish (PARKING_LOT :50-51) [A]

## Ready: rigged characters and animals (from `docs/research/2026-11-06-open-rigged-assets.md`)
- [x] **S** Verify licences before adopting: Quaternius QAL (Bestiary kit), UAL2 wording, Ultimate Animated Animals roster, MPFB2 rig options, Mixamo FAQ, UniRig skinning release (research note "Still to verify") (`b3b146e9`)
- [ ] **M** Ambient life phase 2 without waiting on user models: Quaternius Farm/Animated Animals (CC0) restyled, plus rooks on a Mesh2Motion bird rig (`Reference/sessions/ambient-life/STATUS.md`) [A][U]
- [ ] **M** Gleaner prototype: Bestiary kit humanoid on the UAL rig + UAL2 clips, sheaf mask as prop (after QAL check) [A][U]
- [ ] **M** Rig the Cellars tier-1 enemies (jam-jar slime, broom, rat-king) with small bone rigs (UniRig or hand) so they animate instead of sliding primitives [A][U]
- [ ] **M** Wren's cat Tuppence: own mesh on a borrowed quadruped rig (Mesh2Motion fox / Quaternius cat), follow-Wren via Animation Rigging look-at [A][U][net]
- [x] **S** Townsfolk idle-work loops: map UAL2 farming/fishing/carry clips to baker, smith, innkeeper, Saltwake fisherman; list gaps to generate with HY-Motion via Tencent's HF Space (local 12 GB GPU can't run it) or MoMask locally (personal-use licence bar, user 2026-11-06) (`b3b146e9`, `docs/research/2026-11-06-townsfolk-clip-map.md`)
- [ ] **M** MPFB2 hero/townsfolk base test: one CC0 body on `game_engine` rig through `HeroAnimator.controller` [A][U]

## Ready: design notes (docs only, placeholders marked)
- [x] **S** Gold sink proposal: what gold buys (triage :52; tuning blocked without it) (`5854759a`, `docs/superpowers/specs/2026-11-06-gold-economy-proposal.md`)
- [x] **S** Enemy-kill purse, stash view, town emptying (PARKING_LOT :77, triage C1) (`5854759a`, `docs/superpowers/specs/2026-11-06-gold-economy-proposal.md`)
- [ ] **S** Co-op hearth marks design note (PARKING_LOT :15)
- [ ] **S** Movement tools (glide, high jump) gating optional branches (PARKING_LOT :26)
- [ ] **S** Rule-twist challenge gauntlets from existing rooms (PARKING_LOT :32)
- [ ] **S** Keepsakes for named residents; permanent capacity from story beats / superbosses (PARKING_LOT :31, :33)
- [ ] **S** Residents travelling between towns (PARKING_LOT :59)
- [ ] **S** Food/water craftables + cooking (PARKING_LOT :60) - proposal for the user

## Waiting on the user (don't start; ask when they're around; plain bullets so the board doesn't count them)
- Playtest `C:\Dev\SleepingCalamity-builds\playtest-2026-11-06\SleepingCalamity.exe` (includes plan 5); notes in `docs/playtest/NOTES.md`
- Story book read + bible sign-off; bible Qs 6, 7, 8, 13, 15, 16 touch dive text (triage :24); 5 and 20 touch rules
- Rename placeholder floor and NPC names (spec Q7; decisions-for-review S1-S8)
- Bag-rule decisions: chest with a full bag (PARKING_LOT :69); save-and-quit at a stair (:74); absent player's bag through push-on/fail (:82); bank-room picks UI vs auto-fill (:67)
- Gold economy decisions 1-5 (`docs/superpowers/specs/2026-11-06-gold-economy-proposal.md`, recommended defaults given)
- Failed dive keeps half of gold? (triage :50; proposal Q4 says keep settled decision 7); weapon wear (triage Q5, default no); network conditions numbers (Q6)
- Co-op F10 proposal (spec section 4.7, decision 40); bank slots in a 4-player party (spec Q6)
- decisions-for-review: A6 PHASE1 lock (51 decisions), A24 cellar mood, A34 WeaponsSave wiring (must-fix before M7), A2 OoT 2026 screens check now due
- USER_TODO: Mixamo clips (blocks M2 animation), 2-player Relay walk (M1, M2, M5, M6, M7), Tobin concept (M10), corner approvals (M9)
- Standing on the other hero's head: block or allow (USER_TODO :13)
- Trial ramenvr/autopilot smoke tester (CURRENT Inbox)
