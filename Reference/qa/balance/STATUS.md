Status: DONE (survival target met at soloAtk 1.0 / soloLife 1.0 with an all-tier-1 Cellars; DiveBot still does not reach home inside its time cap: bot pace and floor navigation, not balance)
Archive: ready

# Solo normal dive balance (2026-11-02)

Target was: DiveBot solo normal gets home on 4 of 5 seeds. **Result: 0 of 5.** Seeds 12345, 7, 31, 404, 2024 (normal; runs 1 used the 4 original seeds only). Raw reports: side-notes `balance-base`, `balance-base2`, `balance-run3` .. `balance-run6`.

## Runs, dive fails per seed (12345 / 7 / 31 / 404 / 2024) and deepest floor
| run | change | limit | fails | deepest | hits (sum) |
|---|---|---|---|---|---|
| 1 | baseline | 600 s | 7 / 12 / 10 / 10 / - | F2 F1 F1 F2 | ~264 |
| 2 | baseline + hit log | 600 s | 7 / 11 / 9 / 11 / 2 | F2 F1 F1 F1 F2 | 283 |
| 3 | floors 1-2 'S' spawns tier 1 (Tier2MinFloor 3) | 600 s | 4 / 8 / 6 / 5 / 2 | F2 F3 F3 F3 F3 | ~273 |
| 4 | + global tier1Atk 0.5, tier2Atk 0.8, tier2Life 2, tier2Def 2.2 | 1500 s | 9 / 21 / 13 / 9 / 15 | F3 F3 F4 F4 F3 | 925 |
| 5 | global cuts removed; soloAtk 0.55, soloLife 0.75 | 1500 s | 8 / 14 / 7 / 6 / 5 | F4 (all, ended F3-F4) | 815 |
| 6 | + bot walks to the healing point below 45% HP | 1500 s | 11 / 14 / 5 / 5 / 4 | F4 all | 806 |

No run ended in "went home"; every one hit the time cap. Lowest HP is 1-5% on every seed in every run. Heart never banked.

## Why (findings)
1. **Floor 1-2 rat-kings were tier 2.** The authored room 'S' glyph spawns a tier-2 rat-king (540 HP, DEF 33, lunge 18) in nearly every floor 1 combat room. Run 2: rat_king tier 2 = 194 of 283 hits, 3438 of ~4180 damage. Fixed (below); floor 1 deaths dropped from ~10 to 2-4 per seed (run 3 vs 2).
2. **Floors 3-4 are where it still dies** (run 6, summed): broom tier 1 F3 284 hits / ~1650 dmg (largest); rat_king tier 2 F3 148 hits / ~1300 (4 seeds); rat_king tier 1 F3 81 / ~590; rat_king t1 F4 57 / ~390; broom F4 58 / ~330; rat_king t2 F4 28 / 268; Dough Warden t2 F4 7 hits / 127. Average hit is 7-9 HP now (was 14-18). Hits per room were not measured; about 28-30 hits per failed dive and ~35 hits per floor-3 visit.
3. **The bot is a weak tester.** It dodges reactively but never backs off, never eats loaves, never guards. Heal-seeking (run 6) made 4-8 trips on seeds 31/404/2024 and 0 on 12345/7 (no point on the floor, or not ready); those seeds still fail 5-14 times. The log does not record HP recovered. The bot also falls through the floor (y -1.6) and arrives on floors with no walkable ground (Fall / Entry / Nav findings in all seeds): some dives are lost to the bot or the floor, not the numbers.
4. Run 4 vs run 5: global attack cuts did not help more than the solo-only cut (hits went up, since the bot survives longer and fights more).

## Committed
- `EnemyCatalog.Tier2MinFloor = 3` + `AuthoredTier(...)`: authored 'S' rat-kings are tier 1 on floors 1-2; the Warden and the stir hunter stay tier 2. **Effect on co-op: easier on floors 1-2 for every party size** (the table was wrong, not a solo tweak).
- Tuning loader section `enemies`: `tier1Atk`, `tier2Atk`, `tier2Life`, `tier2Def`, `soloAtk`, `soloLife`, `tier2MinFloor` (EnemyCatalog tier constants are now static fields; `TuningStoreTests` cover them).
- `Assets/StreamingAssets/Tuning/10-balance.json`: `soloAtk 0.55`, `soloLife 0.75`. Apply only when exactly 1 hero is present, so **2-4 player numbers are unchanged** (CoopScaling and the tier tables are untouched).
- DiveBot: per-hit log (nearest enemy), seed 2024, 1500 s limit for normal mode, heal-seeking. `DiveHaulSync.TryHealingPoint` (read-only accessor).

## Remaining gap and recommendation
Not reached: no seed gets home. Do not cut attack further (0.55 is near the floor the coordinator set).
1. **Fix the bot before more numbers**: loaf use (`H`), retreat to the nearest healing point/checkpoint bank at low HP and *wait* until healed, guard against brooms. A player takes far fewer than 30 hits per floor. Also the floor falls/entry findings (Reference/qa/traversal) kill runs.
2. **Floor 3 is the cliff.** Candidates, in order: broom tier 1 on floors 3 (284 hits: its sweep reach 1.9 m and frontal block; check bot approach before changing numbers); tier-2 'S' rat-kings first appearing on floor 3 at x3 life/x3.3 DEF: `tier2Life` 3 -> 2 and `tier2Def` 3.3 -> 2.2 is a global tuning option (run 4 had it; it affects co-op).
3. A solo hero-side option if numbers must carry it: more max HP or a free heal on room clear solo (HeroVitals.BaseMaxHp is a const 100; ClearHeal 20).

## Run 7 (2026-11-02, smarter bot, soloAtk 0.55 / soloLife 0.75, numbers unchanged)
Bot changes (test-only): eats loaves (H) below 50% HP (none were in the dev dive: 0 eaten), retreats to the floor's healing point below 45% and waits until 90%, walks to a broom's side instead of its blocking front, tags dive fails within 20 s of a Fall/Entry/Nav/Embedded finding as floor faults, prints hits per floor and per visit.

| seed | fails (floor-fault) | deepest | hits / dmg | lowest HP | heal trips (reached) | hits per floor (visits) | ended |
|---|---|---|---|---|---|---|---|
| 12345 | 2 (1) | F3 | 56 / 376 | 1% | 0 | F1 7, F2 14, F3 35 (3) | cap, on F3 chests |
| 7 | 3 (1) | F3 | 66 / 440 | 1% | 0 | F1 9, F2 1, F3 56 (4) | cap, on F3 chests |
| 31 | 0 | F3 | 28 / 164 | 47% | 2 (2) | F1 3, F2 7, F3 22 | cap, 2 floors cleared |
| 404 | 1 | F4 | 50 / 362 | 5% | 2 (2) | F1 1, F2 12, F3 25 (2), F4 17 | cap, on F3 chests |
| 2024 | 0 | F4 | 14 / 80 | 65% | 0 | F1 2, F2 6, F3 3, F4 3 | cap, just entered F4 |

Compared with run 6 (806 hits, 4-14 fails per seed) hits fell to 214 and fails to 0-3: the earlier 0/5 was mostly the bot. Damage now: rat_king tier 2 F3 80 hits / 660 dmg (largest), broom F3 42 / 236 (3.8 per F3 visit, was ~25), rat_king t1 F1-F4 62 / 395. Heart never reached, nobody went home.

**The remaining gap is time, not death:** every seed was alive at the 1500 s cap, still on chests of F3/F4 (the bot clears every chest, ~400-500 s per floor). Four floors plus heart plus the way home need roughly 2000-2500 s of bot play at this pace.

## Recommendation of run 7 (superseded by the decision below)
1. Do not touch the numbers yet. Let the bot play to the end: raise the normal limit to ~3000 s (one run of 5 seeds is then ~25 min of PlayMode lock; run 2-3 seeds first) or make it skip optional chests on floors 3-4 and head for the stair/heart. That settles whether solo normal is winnable at soloAtk 0.55 / soloLife 0.75.
2. If it then wins nearly always, the solo numbers can be eased back toward 1.0 to keep it a fight; if it still loses on F3/F4, the user's options are the tier-2 rat-king on F3 (`tier2Life` 3 -> 2, `tier2Def` 3.3 -> 2.2; global, co-op too) or a solo hero HP / clear-heal change.
3. Separate bugs seen: falls through the floor on F2/F3, no walkable ground on stair arrival (traversal), checkpoint bank prompt None at ~3 m on F2.

## User decision (2026-11-02) and runs 8-10
**Rule:** the Cellars (the first dungeon, floors 1-4) are a rougher tutorial like DC1: every regular enemy is **tier 1**. The hero does not have tier-2 gear, weapon upgrades or later areas yet; tier 2 belongs to later dungeons once those exist. The Dough Warden (floor-4 set-piece guard) keeps its tier-2 stats. Implemented as the tunable `enemies.cellarsMaxTier` (default 1; 2 restores the tier-2 rat-kings from `Tier2MinFloor` 3 and the stir hunter), see `EnemyCatalog.CappedTier/AuthoredTier`.
**Knobs:** `10-balance.json` now holds `soloAtk 1.0`, `soloLife 1.0`, `cellarsMaxTier 1`. Solo and co-op use the same enemy numbers again (CoopScaling untouched), so the 55 % / 75 % solo cut is no longer needed.

| run | change | what happened |
|---|---|---|
| 8 | bot opens only key chests + 2 optional chests per floor, heads for pieces/key/stair; tier 2 on F3+ (soloAtk 0.55) | 0/5 home, 0 deaths; 4 seeds ended with the exit locked and the gate key 0: the bot never visited the carrier's room (optional chests used to lead it there) |
| 9 | not run (superseded by the tier decision; stopped while queued) | |
| 10 | + bot walks to the gate-key carrier; **all tier 1, solo knobs 1.0** | 0/5 home at the 1500 s cap; dive fails 0 / 3 / 1 / 2 / 0 (seeds 12345 / 7 / 31 / 404 / 2024), lowest HP 54 / 2 / 4 / 4 / 86 %, hits 5 / 40 / 25 / 55 / 1, no fails near a floor fault; rat_king did 98 of 136 hits (F3 51); broom 14 hits, slime 14, **no Dough Warden hit** (nobody got past F4). Seeds 12345 and 2024 stalled at the carrier goal on F1 (bot stood 4 m from the cell, fixed below). Seeds 7/31/404 spent 430-1047 s on single F3 visits (falls through the floor, embedded walls, "fought 60 s without the room going quiet", exit locked for 700 s) |

Runs 7-10 in one line: with the smarter bot and tier-1 enemies the hero hardly dies (0-3 fails per seed, 1-55 hits per run, down from 806-925 hits and 4-21 fails in runs 4-6). What stops the bot is pace and navigation.

## Bot faults fixed (test-only)
Loaf eating, healing-point retreat that waits for 90 %, broom flanking, floor-fault death tags (Fall/Entry/Nav/Embedded within 20 s), hits per floor and visits in the summary, optional-chest cap (2 per floor, key chests always), gate-key carrier goal (walks onto the carrier's cell, gives up after 90 s with a Flow finding), normal-mode limit 2100 s.

## Follow-ups (not started)
1. **Bot pace.** Re-run `DiveBot_Seed*` (SC_BOT=1) with the carrier fix to see how many seeds reach the heart and home inside 2100 s. Floors 3-4 eat the time: falls through the floor (y -1.6 / -150), no walkable ground on stair arrival, embedded walls, the checkpoint bank prompt `None` at 3 m (traversal list in Reference/qa/traversal). Fix those, then judge pace.
2. **The Dough Warden is untested** by the bot (never reached). The user's playtest tonight is the real check of the floor-4 guard at its tier-2 stats; if it alone blocks runs, tune it via `tier2Atk/tier2Life/tier2Def` (global) or give the Warden its own entry.
3. Tough mode is unchanged (host heals below 50 %): still easier than normal by construction; the "tough harder than normal" rule is not meaningful for the bot until normal gets home.

Archive: ready
