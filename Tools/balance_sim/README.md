# balance_sim: combat and weapon-growth balance simulator

Python/torch fight simulator for the Hearthdeep dive (floors 1-7). Thousands of dives per case run on the GPU (CUDA graph, about 3 minutes for the default 6 cases). Built for Fable Run 5 (2026-11-15); reusable for every later area. Mechanics and numbers only, no Dark Cloud text or assets.

## Run

```
Tools\balance_sim\run.cmd [--config config.json] [--numbers data\game_numbers.json] [--out DIR] [--tag name] [--lanes N] [--dives N]
Tools\balance_sim\run.cmd --selftest
```

`run.cmd` uses `C:\AI\hairdeck\.venv\Scripts\python.exe` (torch 2.8 CUDA + matplotlib). CPU fallback works but is slow. Output (default `Tools/balance_sim/out/`, gitignored), per `--tag`:
- `<tag>_sheet.png`: ONE sheet, about 1260 px wide (TTK per floor and kind with target bands, mean TTK 1/2/4 players, room clear time, damage taken per hero, deaths per dive, weapon level after dive N, dive success, floor reached, skill comparison).
- `<tag>_metrics.csv` (long format: case, players, profile, floor, metric, kind, mean, p10, p90), `<tag>_metrics.json` (summary and per-target verdicts), `<tag>_config.json` (effective config and numbers).
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
| `enemies.<Kind>.hp/atk/def/speed` | `EnemyCatalog` (`Core/Combat/EnemyModel.cs`) static table | NO until QUEUE 53 |
| `enemyAttacks.<id>.*` | `AttackCatalog` static readonly `AttackData` | NO until QUEUE 53 |
| `heroAttacks.*`, `weapons.*` | `AttackCatalog`, `WeaponDefinitions` | code edit |
| `coop.*` | `CoopScaling` (`Core/Combat/EnemyRules.cs`) | code edit |
| `healing.*` | `HealTuning` (`Core/Cellars/HealTuning.cs`), `HeroVitals.LoafHeal` | code edit |
After editing C#, regenerate the JSON (above) and re-run: the sheet should reproduce the overridden run.

## Model summary and known limits
Time-stepped (dt 1/30 s), vectorised over dive lanes. Hero attack cycle normal1, normal2, finisher; damage by the exact `DamageFormula`. Skill profile = hit rate, uptime, dodge rate, guard rate, revive willingness. Enemy cooldown/recover from `enemyPacing`, strike cap by party size, rage after 7 hits. Weapon growth carried across floors and across N dives (ABS per kill, level cost, build-up). Heal points on eligible floors, loaves only with `bakeryBuilt`. Warden fight on floor 5 (tier 2).
Not modelled: stir, poise, riposte, hero stagger, positioning, charge swings, spells, tier-2 enemies in regular rooms. Dive-1 TTK has survivor bias. Style ABS uses assumed per-hit probabilities. All assumptions live under `assumptions` in `config.json`.
Not in the game (so not simulated): hero XP or level, hero HP growth, weapon durability, attachment drop source.
