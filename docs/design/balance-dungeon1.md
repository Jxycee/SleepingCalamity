# Balance of the first dungeon (the Cellars), Run 5

Decided 2026-11-15, with leeway: this is the current direction, not a lock. If a playtest or a better idea says a value is wrong, raise it with the reason. Source: `Reference/sessions/run5-balance/STATUS.md` (four simulator rounds), `Tools/balance_sim/` (the simulator and its final outputs). The values are pinned by `BalanceDungeon1Tests`.

## The game as built (round 1, r0)
A solo player with the average skill profile never reached the floor-5 bank: 0% at every dive, and floor 2.1 on average on dive 1. Two players reached it 4% of the time at dive 10. Four players reached it 32% to 72% of the time, with 4 downs per hero per dive. The Rat-king took 14 s to kill solo on floor 1 and is over half the enemies on floors 4-7. The Warden was tier 2: life x3 and defence x3.3 in front of the starting axe.

## Targets (revised as the rounds went on)
Basis: Dark Cloud tradition, the Cellars as a rough tutorial with tier-1 enemies, solo and co-op both viable. All bands are for average skill with the axe unless a case is named.
- T1 Regular enemies die fast. Solo dive 1: Slime 2-4 s, Broom 5-8 s, Rat-king 6-10 s, judged on floors 1-3 (the starting axe). Co-op time per enemy 0.6-1.5 times solo (was 0.6-1.2: a party has revives, a solo hero has the assist).
- T2 The Warden is a fight, not a wall: 40-80 s at any party size.
- T3 Damage per hero per floor, as a fraction of max HP: f1 0.10-0.30, f2 0.20-0.45, f3 0.35-0.70, f4 0.25-0.55, f5 0.40-0.90, f6 and f7 0.25-0.60. Co-op per-hero damage 0.7-2.0 times solo (was 0.7-1.25, same reason as T1).
- T4 The bank (floor 5 cleared) is the real goal. Solo average: dive 1 15-45%, dive 3 40-70%, dive 10 65-90%. Novice solo: dive 1 at most 15%, dive 10 at least 3% (a fixed novice profile never improves). Expert: dive 1 70-100% (was 55-90%: the skill gap is large by nature). Two and four players (average) started at dive 1 15-60%, dive 3 40-85%, dive 10 65-95%; revised for four players to 40-85% / 60-98% / 75-100% (holding four players lower would need more than 1.5 downs each, and T6 wins), and dive 3 up to 100%.
- T5 The full clear (floor 7 from the entrance) is earned: solo average dive 1 at most 25%, dive 10 35-85% (was 75%); two players up to 92%. The simulator has no stir, which adds enemies on exactly floors 5-7, so the real number sits lower.
- T6 Downs in co-op: at most 1.5 per hero per dive from dive 3 on.
- T7 Weapon level after dive 1 / 3 / 10 solo: 1-3 / 3-6 / 8-14. Co-op per-hero level at dive 10 up to 2.2 times solo (co-op rooms hold more enemies and last about twice as long, so growth per minute of combat is equal).
- Tier rule: regular Cellars enemies stay tier 1, and the Warden moves to tier 1 too.

## Chosen values
| Value | Was | Now | Why |
|---|---|---|---|
| `tier1Atk` | 0.65 | 0.5 | tier-1 hits of 5-12 instead of 7-14 on 100 health |
| `soloAtk`, `soloLife` | 1, 1 | 0.6, 0.75 | a solo hero has no revive and takes every hit |
| Broom life, Rat-king life | 160, 180 | 130, 140 | Rat-king took 14 s solo and is over half of floors 4-7 |
| Warden tier, life | 2, 600 | 1, 2000 | tier 2 put a 3.3x defence wall in front of the starting axe; 49-52 s now (`wardenTier` knob, 2 restores the old fight) |
| Warden attack multipliers (slam, pound, quake, sweep) | 1.5, 1.8, 2.0, 1.1 | 2.1, 2.5, 2.8, 1.5 | hits that matter (20-38 in co-op), fewer of them |
| Warden cooldown | 1.4 s | 2.4 s | pays for the bigger hits |
| Co-op life (2 / 3 / 4 players) | 1.7, 2.3, 2.8 | 1.5, 2.3, 3.0 | smooths the step from two to three players |
| Weapon level cost per level | 20 | 40 | level 12.5 after ten dives instead of 17; the first level-up still lands on floor 1 (base stays 40) |

The C# defaults are the source of truth; `Assets/StreamingAssets/Tuning/10-balance.json` carries only `soloAtk` and `soloLife`, set to the same numbers.

## The final curves (round 4, `Tools/balance_sim/final/r4_sheet.png`)
As designed (3 free loaves of poor bread before the baker, his bread after). Bank reach at dives 1 / 3 / 10: solo 14% / 68% / 84%, two players 25% / 78% / 87%, three 55% / 95% / 98%, four 79% / 98% / 100%. Full clear at dive 10: 80% / 82% / 97% / 100%. Fights: Slime 2.3 s, Broom 5.8 s, Rat-king 8.0 s solo on dive 1; Warden 49-52 s at every party size. Damage per floor 0.17 to 0.49 solo, 0.18 to 0.66 in co-op. Downs per hero from dive 3: 0.9 or less. Weapon level solo 2.5 / 5.9 / 12.5; at dive 10, 14.8 / 17.5 / 19.4 for two / three / four players.

As built today, without the free loaves (r4n): bank reach solo 3% / 56% / 82%, two players 13% / 66% / 89%, four players 69% / 98% / 100%. Dive 1 is rougher, then it converges.

## Misses left, not tuned away
- Solo dive 1 bank reach 14.5% against 15%.
- Co-op floor 4 damage 0.56-0.57 against 0.55.
- The Warden's damage per hero with three or four players: 0.38 and 0.32 against 0.40.
- Three players' bank reach at dive 3: 95.3% against 95%.
- Novice solo stays at 0%: a fixed novice profile takes about three times an average player's damage, and weapon growth alone cannot close that. The design's help (extra baked loaves, baker upgrades) is not in the simulator.

## Two earlier decisions this changes
- The Warden keeps its tier-2 stats (2026-11-02, the Cellars tier rule). It is tier 1 now: tier 2 meant life x3 and defence x3.3 against a starting axe, which made the fight a wall. Its life went up to 2000 to compensate, and its hits and cooldown were reshaped. `"wardenTier": 2` puts the tier-2 scaling back (life x3, defence x3.3, tier-2 pacing) on top of the new life, attack and cooldown values; the Warden's base life is not tunable in data, so the exact old fight needs a code edit.
- The 2026-10-30 "fewer, tougher enemies" life numbers for the Broom (160) and Rat-king (180): now 130 and 140, because the Rat-king took 14 s and carried too much of floors 4-7. The slime stays at 80.

## What the simulator does not model
- Stir, poise, riposte, hero stagger, positioning, charge swings, spells, tier-2 enemies in regular rooms. Stir adds enemies on floors 5-7, so real floors 5-7 are harder than simulated.
- Extra baked loaves and baker upgrades, the design's help for weaker players.
- Dodge chance rising with the wind-up is an assumption, and the Warden numbers lean on it.
- Hero XP or level, hero HP growth, weapon durability and the attachment drop source (not in the game).
- The floor-3 guardian of the design is not in the game yet.
- Dive-1 kill times have survivor bias, and the time step (1/30 s) makes short attacks look slightly slower than in game.

## Follow-ups
- Wire the bread ladder's free loaves (owned by plan 3); until then the first dive is the rougher "as built" curve.
- Playtest the Warden solo (1500 life solo, attack 9 before the move multiplier, 2.4 s cooldown).
- After any combat change, re-run the simulator: regenerate `Tools/balance_sim/data/game_numbers.json` (`SC_BALANCE_EXPORT=1`, `BalanceExportTests`), then `Tools\balance_sim\run.cmd`.
- `CombatDepthTests.ARatKing_TakesAboutThreeAxeOpenings` (the 2026-10-30 "fewer, tougher enemies" intent) fails: a solo Rat-king (140 x 0.75 = 105 life) now takes 2 axe openings, not 3 to 4. Left unchanged for a decision; T1 (Rat-king 6-10 s solo) is met with the new life.
- Anything else that keyed off the Warden's tier is listed in the landing report: the Warden now drops loot as a tier-1 enemy (one rank less than the floor's chest rank instead of one more).
