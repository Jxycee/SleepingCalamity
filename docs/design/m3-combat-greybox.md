# M3 combat greybox: how it is built and how to play it

Status: built 2026-10-30 on branch `m3-combat`. Numbers are P9 Placeholders (tune from the playtest). Plan: `docs/PHASE1.md` 6.1 to 6.6.

## Where things live
- **Core (pure, tested):** `Assets/_Project/Core/Combat/`: `AttackData` (frame windows), `AttackCatalog` (the four heirloom sets and the enemy attacks, as data),
  `AttackMachine` (input buffer, combo/finisher windows, charge, hitstop clock), `DamageFormula`, `GuardMath` (guard, dodge i-frames with leniency, broom front,
  hitstop budget), `EnemyRules` (revenge, rage, co-op scaling, boss parts, downed/revive), `EnemyModel` (host hit resolution), `CombatRewards` (calls the M4
  rules), `ArenaWaves`. Sounds: `Core/Audio/CombatSfx.cs`.
- **Gameplay:** `Gameplay/Combat/`: `HeroCombat` (owner: swings, hit detection, judging enemy hits; RPCs), `HeroVitals` (host HP, downed, revive), `Enemy` (host AI,
  replicated attack clock), `CombatHost` (host rules, growth, healing drops, waves), `HealingDrop`, `CombatEvents` (local cosmetic events).
- **Presentation:** `Presentation/Combat/`: `EnemyVisual` (telegraph, flash, rage), `HeroWeaponVisual` (held weapon, swing, shield, bow, downed pose), `CombatFx`
  (sparks, damage numbers, swing footprint, sounds), `CombatHud` (debug HUD). `HeroCameraRig.Kick` is the camera kick.
- **Scene and prefabs:** built by `SceneBuilder.BuildCombatArena` (Arena_Combat) and `PrefabBuilder` (EnemySlime, EnemyRatKing, EnemyBroom, EnemyDoughWarden,
  HealingDrop; the hero gained HeroVitals, HeroCombat, HeroWeaponVisual).

## The hit flow (6.4)
1. The attacker's client runs the swing (`AttackMachine`), tests its hitbox against the enemy copies it can see, and on contact plays the spark, sound, hitstop
   (its own attack clock only, never `Time.timeScale`) and camera kick at once.
2. It sends `SubmitHitServerRpc(swing id, attack index, enemy, part, its position and yaw)`.
3. The host (`CombatHost.ServerPlayerHitsEnemy`) checks: attacker up, enemy alive, the attack exists on that weapon, one hit per target per swing, a spell costs a
   pip once per swing. It then applies damage, stagger, revenge, rage, parts, ABS and the kill credit, and broadcasts the result (number, sounds) to everyone and
   the ABS report to the attacker.
4. Enemy attacks: the host runs the AI and replicates the attack serial, id and origin. Each client runs the same telegraph/hitbox clock on its own copy; the
   victim's client judges the hit against its dodge (i-frames), guard or parry and tells the host, which re-checks only that the enemy is performing that attack.

## Numbers changed from 6.5 / 6.6 (and why)
- Dodge i-frames: frames 5-18 of 30 as planned, and the 100 ms owner leniency is applied **either side** (so the whole first 0.4 s of the 0.5 s roll is safe; a
  reaction dodge never loses to the 5-frame start-up).
- Finisher bonus: `1.6 x (1 + 0.10 x prior connected hits)`, multiplicative; hits that missed do not count.
- Element: weapon Fire x 2% on a fire-weak enemy (slime, broom). Tag: weapon Pest x 3% on a pest (rat-king). The staff's spell counts as Fire 10.
- Rage: every landed hit by any player counts; 7th hit telegraphs 0.8 s, then +30% attack for 15 s; the count restarts. Not used by the elite.
- Charge: tap starts hit 1 at once (no latency); keep holding 0.6 s and release for the x2.0 charge (so a charge follows one swing).
- Input buffer 0.25 s, plus the time to the swing's cancel frame (the heavy axe cancels later than 0.25 s).
- Revenge uses each attack's own value: hit 1 and 2 = 1, finisher 3, charge 4, air 1.5 / 3.5, bow shot 2, staff spell 4.
- Warden: parts are the rolling-pin arm (right flank, 70 HP, x1.5 weak point) and the dough crown (above ground swings: jump attacks and arrows, 70 HP). Each
  drops one healing pickup (+25 HP) when broken. Revenge fills break the Warden free with a circle attack. Its slam and pound break guards.
- Healing: +20 HP to every hero at each wave clear (arena only), on top of the Warden's drops.
- Hero in co-op: life of enemies x1.7 for two (a 40 HP slime has 68), one extra enemy per extra player.
- Spell pips come from M4 (`SpellPips`: 3, one per 4 landed hits); the staff's right-click spends one.
- Not applied yet: hero abilities (M4 `HeroAbilityBook`) have no combat effect in M3; Combo Plus and the like wait for M7 tuning.

## Controls (keyboard and mouse; the HUD shows them, F1 hides)
LMB attack (2 hits then finisher; hold 0.6 s then release for the charge), RMB: dagger guard (raise it just before a hit = parry), bow aim (then LMB shoots),
staff spell. Left Ctrl dodge, Space jump (attack in the air for the air combo), Shift sprint, Tab lock-on (Q/R switch), E hold near a downed ally to revive.
Debug: F5 cycles the four heirlooms (axe, staff, dagger and shield, bow and sword), F6 clears the current wave.

## How to play the 5-minute arena
1. In the editor: menu **SleepingCalamity > Dev > Combat Arena** (tick it once; it is remembered), then **Auto Solo** if you want to skip the dev panel, and press
   Play from the Bootstrap scene. Or in the dev panel (top-left in the Lobby) tick "Start in the combat arena (M3)" and press Solo. Or start the player with
   `-dev-combat -dev-solo`.
2. Wave 1 arrives after 4 s: four slimes. Wave 2: slimes and rat-kings. Wave 3: brooms and rat-kings. Wave 4: the Dough Warden and two slimes. Waves 5 and up
   repeat these at tier 2 (life x3, attack x2.3, defence x3.3). A wave takes 40 s to 2 min; four waves is about five minutes.
3. Try each heirloom (F5) against the same wave. What to judge: hit feel (hitstop, sound, camera kick), the combo rhythm, dodge timing and how late a roll can be,
   guard and parry on the dagger, the broom (front blocks 80%: flank it, or finish with the axe), the Warden (read the telegraph, break the arm and crown for
   healing, watch the revenge bar).
4. Two players: the host starts a multiplayer dev-direct world and the second instance joins (Multiplayer Play Mode or a second build): `-dev-combat` on the host.
   Enemy life and count scale with the party. Downed players are revived by holding E for 3 s.

## Network simulator
`Tests/PlayMode/CombatNetworkTests.Netsim_150msRtt_2PercentLoss_...` drives the Multiplayer Tools `NetworkSimulator` component (it works in PlayMode, in the
editor). The simulator delays each packet at the send and the receive stage of both peers, so a per-packet 37 ms gives about 148 ms RTT (75 ms gave 300). For
the manual walk: add a NetworkSimulator component to the App object, pick "37 ms, 2% loss" (custom preset), and play the two-player arena; hits on the attacker's
screen must play instantly (spark, hitstop, sound) and the damage number must follow within about one round trip.
