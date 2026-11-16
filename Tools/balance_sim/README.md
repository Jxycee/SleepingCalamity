# balance_sim: combat and weapon-growth balance simulator

Python/torch fight simulator for the Hearthdeep dive (floors 1-7). Thousands of dives per case run on the GPU (CUDA graph, about 3 minutes for the default 6 cases). Built for Fable Run 5 (2026-11-15); reusable for every later area. Mechanics and numbers only, no Dark Cloud text or assets.

## Run

```
Tools\balance_sim\run.cmd [--config config.json] [--numbers data\game_numbers.json] [--out DIR] [--tag name] [--prev out\<oldtag>_metrics.csv] [--lanes N] [--dives N]
Tools\balance_sim\run.cmd --selftest
```

Relative paths (`--config`, `--numbers`, `--prev`, `--out`) resolve from the current working directory (run from the repo root). `--config`, `--numbers` and `--prev` are checked at startup: a missing file exits with an error before the simulation starts.

`run.cmd` uses `C:\AI\hairdeck\.venv\Scripts\python.exe` (torch 2.8 CUDA + matplotlib). CPU fallback works but is slow. Output (default `Tools/balance_sim/out/`, gitignored), per `--tag`:
- `<tag>_sheet.png`: ONE sheet, 1260 px wide, 3x3 panels: per-kind TTK solo dive 1 (per-kind bands), pooled TTK 1/2/4 players (floor 5 = Warden dives 1-3, band 40-80), room clear time, damage per hero per floor (bands), downs per hero per dive, weapon level after dive N, bank reach by dive, full clear by dive, mean floor reached. The header lists every FAIL (value and band), wrapped. With `--prev <metrics.csv>` every panel also draws the previous run, same colour, dashed and fainter, and the header names its tag.
- `<tag>_digest.txt`: text for reading instead of the CSV (about 90 lines): verdicts grouped per check and case (FAIL items with value and band), then one-line-per-case tables (TTK dive 1 per kind and floor, Warden `ttk_d1to3`, damage fraction, co-op ratios, bank reach / full clear / floors reached / downs per hero / weapon level at dives 1, 2, 3, 5, 10). With `--prev` the previous value follows each number in [brackets].
- `<tag>_metrics.csv` (long format: case, players, profile, floor, metric, kind, mean, p10, p90, n; dive metrics use floor 0 and kind `diveN`), `<tag>_metrics.json` (summary and per-target verdicts), `<tag>_config.json` (effective config and numbers).
Metrics: `ttk_all`, `ttk_d1`, `ttk_d1to3` (dives 1-3 pooled), `room_clear_*`, `dmg_taken_frac`, `deaths_per_dive` (party total), `downs_per_hero` (per hero), `weapon_level`, `dive_success` (= full clear, floor 7), `floors_reached`, `bank_reach` (fraction of lanes that cleared floor 5 in that dive).
The committed baseline on 2026-11-15 numbers is in `baseline/`.

## Where numbers come from (no hand copy)
`data/game_numbers.json` is exported from the live C# by the EditMode test `Assets/_Project/Tests/EditMode/BalanceExportTests.cs`. Regenerate after any tuning change: set `SC_BALANCE_EXPORT=1` and run `Tools\unity.ps1 test-edit -Filter BalanceExportTests`. Without the env var the same test is a drift guard: it fails if the committed file's tuning sections differ from the live data. The `floorStats` section (rooms and enemies per floor from the real floor generator, 200 seeds per floor) is exported but not compared.

## Change values to try
Edit `config.json` (every key has a `_comment`):
- `overrides`: dotted path into `game_numbers.json`, value absolute, `{"mult": x}` or `{"add": x}`. Examples: `"enemies.Slime.hp": {"mult": 1.2}`, `"weapons.axe.atk": 24`, `"growth.levelCostBase": 30`, `"coop.lifeMult.2": 2.0`, `"enemyAttacks.rat_lunge.mult": 0.9`. Unknown paths raise an error.
- `targets`: bands (ASSUMED, first draft) for regular-room TTK per floor, damage taken as a fraction of max HP, deaths per hero per dive, weapon level after dives 1/3/10.
- `skillProfiles`, `partySkillMix`, `cases`, `assumptions` (hit rate, uptime, dodge, guard, loaves, `bakeryBuilt`, build-up on/off, Broom block fraction, and so on), `run` (lanes, dives, seed).

## Map a chosen value set back to C#
| Override path prefix | C# tuning home | Tunable in data today |
|---|---|---|
| `growth.*`, `weaponGrowth` knobs | `WeaponGrowth` / `WeaponGrowthTuning` (`Core/Tuning/TuningSections.cs`), JSON section `weaponGrowth` | yes (StreamingAssets/Tuning/*.json) |
| `tierScale.*`, `enemies` knobs (soloAtk, soloLife, tier1Atk, tier2Life, tier2Def, cellarsMaxTier) | JSON section `enemies` | yes |
| `enemies.<Kind>.hp/atk/def/speed` | `EnemyCatalog` (`Core/Combat/EnemyModel.cs`) static table | NO, a separate later item (not part of the attack work) |
| `enemyAttacks.<id>.*` | JSON section `enemyAttacks` (`EnemyAttackTuning`, `Core/Tuning/TuningSections.cs`); values are written in place into the `AttackCatalog` enemy attacks | yes (StreamingAssets/Tuning/*.json) |
| `enemyPacing.*` | JSON section `enemyPacing` (`EnemyPacingTuning`; `AttackPacing` bases and tier profiles) | yes (StreamingAssets/Tuning/*.json) |
| `heroAttacks.*`, `weapons.*` | `AttackCatalog`, `WeaponDefinitions` | code edit |
| `coop.*` | `CoopScaling` (`Core/Combat/EnemyRules.cs`) | code edit |
| `healing.*` | `HealTuning` (`Core/Cellars/HealTuning.cs`), `HeroVitals.LoafHeal` | code edit |
After editing C#, regenerate the JSON (above) and re-run: the sheet should reproduce the overridden run.

### Applying an `enemyAttacks` / `enemyPacing` value set in the game
Put the values in any `StreamingAssets/Tuning/*.json` (or the mod folder), with the simulator's own key names; they apply as written:
```json
{
  "enemyAttacks": { "rat_lunge": { "mult": 0.9, "hitOnFrame": 30, "hitOffFrame": 38, "range": 1.4, "totalFrames": 64, "guardBreak": false } },
  "enemyPacing": { "cooldown": { "default": 1.2, "RatKing": 0.8, "Warden": 1.4 }, "recover": { "default": 0.35, "RatKing": 0.9 }, "tier2CooldownScale": 0.7 }
}
```
Name mapping (exporter name -> tuning key): `mult` = `multiplier`; `hitOnFrame` / `hitOffFrame` = the start / end of `hitOn: [start, end]`; `totalFrames`, `range`, `guardBreak`, `guardable` are the same. `enemyAttacks` also takes every `attacks` key (`halfAngleDeg`, `knockback`, `turn`, `lunge`, `superArmor`, ...). `enemyPacing` uses the exported names (`cooldown.default/RatKing/Warden`, `recover.default/RatKing`, `tier1Scale`, `tier1RecoverScale`, `tier2CooldownScale`, `tier2RecoverScale`, `tier1/2MaxAttackersPerHero`, `tier1/2MinStrikeGapSeconds`, `signatureEvery`, `signatureExtraRecoverSeconds`).
An attack that would break an enemy rule after the override (wind-up floor, glint 12-24 frames before the hitbox, lunge ends before the strike, reach against the body, 30-frame punish window) is rejected whole with a warning and keeps its defaults; the tell's own frames are not tunable, so move `hitOn` only within the glint lead. `test_nova` is not tunable. `BalanceExportTests` asserts the exporter's keys apply verbatim.

## Run 5 assumptions (under `assumptions`)
- `useFloorStatsKindShare`: true samples each regular enemy's kind from `floorStats.<floor>.kindShare` (Warden share dropped, renormalised over Slime/Broom/RatKing; the real generator) instead of `floorMix`.
- Bread ladder: a lane with the bakery gets `healing.loavesPerDive` loaves of `healing.loafHeal` at dive start; a lane without gets `breadLadderFreeLoaves` loaves of `breadLadderPoorLoafHeal`. The bakery flag is `bakeryBuilt`, or (`bakeryAfterFloor` > 0) from the dive after the lane first cleared that floor in any earlier dive, failed or not. 0 free loaves, `bakeryAfterFloor` 0 and `bakeryBuilt` false = the game as built (no loaves).
- `dodgeWindupRefSeconds` > 0: dodge chance = 1-(1-dodgeRate)^max(1, windup/ref), windup = `hitOnFrame / run.frameRate` of the landing attack. 0 = flat dodgeRate.
- Targets (`targets`, Run 5 schema): `ttkKindSoloDive1`, `partyTtkRatio`, `wardenTtkSecondsDives1to3`, `damageTakenFractionOfMaxHp`, `coopDamageRatio`, `bankReach`, `fullClear`, `floorsReachedDive1Min`, `downsPerHeroPerDiveMaxFromDive3`, `weaponLevelAfterDive`, `coopWeaponLevelRatioDive10`. A target with no data is a FAIL with value "no data". Per-kind TTK verdicts need 20 kills on that floor. `ttkKindSoloDive1` takes an optional `"floors": [1, 2, 3]`: only those floors are judged, others give no verdict.
- `rounds/*.json` are the configs of the balance rounds (r0 = game as built, r1a = structural changes, r1b = `config.json`).

## Model summary and known limits
Time-stepped (dt 1/30 s), vectorised over dive lanes. Hero attack cycle normal1, normal2, finisher; damage by the exact `DamageFormula`. Skill profile = hit rate, uptime, dodge rate, guard rate, revive willingness. Enemy cooldown/recover from `enemyPacing`, strike cap by party size, rage after 7 hits. Weapon growth carried across floors and across N dives (ABS per kill, level cost, build-up). Heal points on eligible floors. Bread ladder (below). Warden fight on floor 5 (tier 2 unless `tierScale.wardenTier` is 1, which makes it tier 1 in every respect: attack scale, life, defence, pacing).
Not modelled: stir, poise, riposte, hero stagger, positioning, charge swings, spells, tier-2 enemies in regular rooms. Dive-1 TTK has survivor bias. Style ABS uses assumed per-hit probabilities. All assumptions live under `assumptions` in `config.json`.
Time quantisation: dt 1/30 s rounds the game's 60 fps frame counts, so a hit lands up to 33 ms late and short attacks look slightly slower than in game.
Finisher-kill ABS bonus goes only when the killing blow (first hero, in slot order, whose cumulative damage crosses 0 hp within the tick) is a finisher hit. Solo (1 player) applies `tierScale.soloAtk` / `soloLife`; damage and life use one ceil with the 1e-4 epsilon, as the C#.
Not in the game (so not simulated): hero XP or level, hero HP growth, weapon durability, attachment drop source.
