# M7 tuning notes (playtest to key)

Where to edit: drop a `*.json` file in `Assets/StreamingAssets/Tuning/` (files load in name order, a later value wins; `10-balance.json` is the current one) or in the user's mod tuning folder. Each file is one object whose top-level keys are sections. Bad keys or out-of-range values are skipped and logged once as `[Tuning] ...`. Host and clients must load the same files. Loader: `Core/Tuning/TuningStore.cs`; sections and defaults: `Core/Tuning/TuningSections.cs`.

Example (the current file):

```json
{ "enemies": { "soloAtk": 1.0, "soloLife": 1.0, "cellarsMaxTier": 1 } }
```

Complaint tags: **hard** = too hard, **many** = enemies too many, **heals** = healing, **loaves**, **stir**, **flow** = pacing / feel of a dive.

## A. JSON knobs (section.key)

Values below are the code defaults; `10-balance.json` currently sets `enemies.soloAtk` 1.0, `enemies.soloLife` 1.0, `enemies.cellarsMaxTier` 1 (all equal to the defaults).

| Key path | Current value | What it changes | Answers |
|---|---|---|---|
| `enemies.soloAtk` | 1.0 | Multiplier on enemy attack damage when ONE hero is present (co-op untouched). Lower = less damage taken solo. | hard |
| `enemies.soloLife` | 1.0 | Multiplier on enemy life when ONE hero is present. Lower = fights end sooner solo. | hard, flow |
| `enemies.tier1Atk` | 0.65 | Attack multiplier of tier 1 enemies (floors 1-2, and all Cellars regulars) on top of the base table. | hard |
| `enemies.tier2Atk` | 1.0 | Attack multiplier of tier 2 (Warden, hunter, rat-kings from `tier2MinFloor`). | hard |
| `enemies.tier2Life` | 3.0 | Life multiplier of tier 2. | hard, flow |
| `enemies.tier2Def` | 3.3 | Defence multiplier of tier 2 (how much of your hit is soaked). | hard, flow |
| `enemies.cellarsMaxTier` | 1 | Highest tier a regular Cellars enemy may be; 2 brings back tier-2 rat-kings. The Warden stays tier 2 regardless. | hard |
| `enemies.tier2MinFloor` | 3 | First floor where the authored rat-king spawns are tier 2 (only matters when `cellarsMaxTier` is 2). | hard |
| `poise.resetSeconds` | 1.0 | Seconds without a hit before an enemy's poise meter resets. | flow |
| `poise.steadySeconds` | 2.0 | How long a staggered/steady state lasts. | flow |
| `poise.perExtraPlayer` | 0.5 | Extra poise-timing per additional player. | flow |
| `weaponGrowth.finisherMultiplier` | 1.25 | Bonus on weapon growth credit for finisher kills. | flow |
| `weaponGrowth.creditWindowSeconds` | 10.0 | How long after a hit a kill still credits that weapon. | flow |
| `weaponGrowth.flatAtkFromLevel` | 5 | Flat attack per weapon level. Higher = hero outgrows enemies faster. | hard |
| `weaponGrowth.buildUpThreshold` | 0.9 | Fraction of growth needed before build-up is offered. | flow |
| `weaponGrowth.buildUpBonus` | 0.1 | Bonus given by a build-up. | hard |
| `targeting.softConeHalfAngleDeg` | 60 | Half-angle of the soft lock cone. | flow |
| `targeting.softRange` | 6 | Soft lock range (m). | flow |
| `targeting.heightBand` | 2 | Soft lock vertical band (m). | flow |
| `targeting.acquireRange` | 10 | Hard lock pick-up range (m). | flow |
| `targeting.releaseRange` | 13 | Hard lock keeps out to this (m); must be >= acquireRange. | flow |
| `targeting.losGraceSeconds` | 1.0 | Seconds a lock survives losing sight. | flow |
| `targeting.hardHeightBand` | 4 | Hard lock vertical band (m). | flow |
| `targeting.facingWeight` | 0.6 | How much facing beats distance when picking a target. | flow |
| `targeting.flickOn` | 0.75 | Stick deflection that starts a target flick. | flow |
| `targeting.flickOff` | 0.35 | Deflection that ends it; must be <= flickOn. | flow |
| `targeting.flickConeHalfAngleDeg` | 60 | Cone used by the flick. | flow |
| `targeting.eliteReleaseRange` | 26 | Lock release range vs elites/bosses; must be >= releaseRange. | flow |
| `targeting.eliteLosGraceSeconds` | 4 | Sight grace vs elites/bosses. | flow |
| `heroCombat.knockbackScale` | 1.0 | Scales knockback taken. | hard, flow |
| `heroCombat.hurtStagger` | 0.25 | Seconds the hero is staggered when hit. Lower = less chain-hit pain. | hard |
| `heroCombat.shotHeight` | 1.3 | Height shots start at. | flow |
| `heroCombat.shotAssistDeg` | 10 | Aim assist cone of shots. | flow |
| `heroCombat.shotRadius` | 0.25 | Shot hit radius. | flow |
| `attacks.<id>.<field>` | see below | Per hero attack. Ids: `axe_1 axe_2 axe_f axe_c`, `staff_1 staff_2 staff_f staff_c staff_s`, `dag_1 dag_2 dag_f dag_c`, `bow_1 bow_2 bow_f bow_c bow_s` (`Core/Combat/AttackCatalog.cs`). Fields: ints `totalFrames cancelFrom hitstopFrames`; windows `[start,end]` `hitOn comboOpen finisherAllowed iFrames superArmor turn lunge`; floats `multiplier range halfAngleDeg heightBand knockback revenge cameraKick poise turnDegPerSec lungeMeters`; bools `guardBreak guardable fire`. Example: `"attacks": {"axe_1": {"multiplier": 1.2}}`. | Hero damage multiplier, reach, i-frames, recovery. `multiplier` up = fights shorter; `iFrames` wider = easier dodging. | hard, flow |

Enemy attack ids (`slime_hop`, `rat_lunge`, `warden_slam`, ...) are NOT tunable yet; they are logged as unknown.

## B. No knob yet (code default only, edit the source)

| Item | Current value | What it changes | Answers | Where |
|---|---|---|---|---|
| Hero max HP | 100 | Life pool of every hero. | hard | `Core/Combat/EnemyRules.cs:137` (`PlayerVitals.BaseMaxHp`) |
| Hero defence | 10 | Flat damage soak. | hard | `Core/Combat/EnemyRules.cs:138` |
| Loaf heal | 30 HP | HP restored per loaf eaten. | loaves, heals | `Gameplay/Combat/HeroVitals.cs:87` |
| Loaves per player at dive start | 3 (needs a complete bakery, baker moved in) | How many loaves you carry down. | loaves | `Core/Town/Placement/BakerService.cs:30` |
| Healing point chance | 51 % per floor 1-3 | How often a floor has a free full heal. | heals | `Core/Cellars/HealTuning.cs:12` |
| Healing point recharge | 30 s | How soon it can be reused. | heals | `Core/Cellars/HealTuning.cs:12` |
| Healing point radius | 1.6 m | Stand-on distance. | heals | `Core/Cellars/HealTuning.cs:12` |
| Bank / checkpoint heal | full heal to max | Heals everyone on banking (floor-4 bank too). | heals | `Core/Cellars/HealTuning.cs:3` (rule in header; no number) |
| Stir per floor cleared | 10 | Stir gained for each floor cleared. | stir, many | `Core/Cellars/StirTuning.cs:19` |
| Stir per unbanked piece | 5 | Stir while carrying pieces. | stir | `Core/Cellars/StirTuning.cs:19` |
| Stir per extra player | +25 % each | Stir multiplier in co-op. | stir | `Core/Cellars/StirTuning.cs:19` |
| Stir max | 100 | Clamp. | stir | `Core/Cellars/StirTuning.cs:19` |
| Stir extra-enemy latch | 40 | At this stir every room gets extra enemies. | stir, many | `Core/Cellars/StirTuning.cs:20` |
| Extra enemies once latched | 1 per room | Size of that bump. | stir, many | `Core/Cellars/StirTuning.cs:21` |
| Stir hunter latch | 70 | Rat-king hunter released. | stir, hard | `Core/Cellars/StirTuning.cs:20` |
| Stir deep-closes latch | 100 | Starts the countdown. | stir, flow | `Core/Cellars/StirTuning.cs:20` |
| Deep-closes countdown | 90 s | Time to reach a bank before the dive fails. | stir, flow | `Core/Cellars/StirTuning.cs:20` |
| Co-op enemy life by players | 1.0, 1.0, 1.7, 2.3, 2.8 (index = players) | Enemy life in co-op. | hard, many | `Core/Combat/EnemyRules.cs:87` |
| Co-op extra enemies | +1 per extra player | Enemies per room. | many | `Core/Combat/EnemyRules.cs:90` |
| Global strike cap | solo none; 3, 4, 5 for 2, 3, 4 players | Enemy attacks under way at once. | hard | `Core/Combat/EnemyRules.cs:94` |
| Warden/boss part weak-point multiplier | 1.5, drop heals 25 | Weak-point damage and the heal a broken part drops. | heals, hard | `Core/Combat/EnemyRules.cs:110` |
| Enemy base tables, enemy attack timings | per enemy | Base life/atk/def/pacing. | hard | `Core/Combat/EnemyModel.cs`, `Core/Combat/AttackCatalog.cs:98+` (`EnemyTuning` only scales tiers) |
| Pieces per floor | 2, 2, 2 | Bakery pieces to find. | flow | `Presentation/Cellars/DiveHaulSync.cs:29` |
| Rooms and enemies per room | generator + encounter plan | Room count and roster. | many | `Core/Cellars/CellarEncounterPlan.cs`, `CellarFloorGenerator.cs` |

## How to map a note to a key

1. Read the dev overlay (F4 in the editor or a development build; off until pressed) at the moment of the complaint: dive time, damage taken, heals used, loaves left, stir, floor, room clear times.
2. "Too hard": damage taken high early -> `enemies.soloAtk` / `tier1Atk` down; fights drag (long room times) -> `soloLife` down or `attacks.*.multiplier` up; hits feel unfair -> `heroCombat.hurtStagger` down, wider `iFrames`.
3. "Too many enemies": room clear times long and stir above 40 -> stir latch/step (section B); otherwise rooms/encounter plan (section B, no knob).
4. "Heals / loaves": heals used and loaves left tell you if you ran dry. Loaf strength and count are section B (no knob yet).
5. "Stir": watch stir against floors cleared and carried pieces; thresholds are section B.
6. "Flow": dive time per floor and room times; `targeting.*`, `poise.*`, `heroCombat.*` for combat feel.
7. Change ONE key, restart the game (tuning loads at startup), replay the same seed if you can.

## Playtest notes (fill in)

| Date | Floor / room | What happened (overlay numbers) | Complaint tag | Key I want changed | New value |
|---|---|---|---|---|---|
| | | | | | |
| | | | | | |
| | | | | | |
| | | | | | |
