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
2. Wave 1 arrives after 4 s: two slimes. Wave 2: three slimes and a rat-king. Wave 3: brooms and rat-kings. Wave 4: the Dough Warden and two slimes. Waves 5 and up
   repeat these at tier 2 (life x3, attack x2.3, defence x3.3). A wave takes 40 s to 2 min; four waves is about five minutes.
3. Try each heirloom (F5) against the same wave. What to judge: hit feel (hitstop, sound, camera kick), the combo rhythm, dodge timing and how late a roll can be,
   guard and parry on the dagger, the broom (front blocks 80%: flank it, or finish with the axe), the Warden (read the telegraph, break the arm and crown for
   healing, watch the revenge bar).
4. Two players: the host starts a multiplayer dev-direct world and the second instance joins (Multiplayer Play Mode or a second build): `-dev-combat` on the host.
   Enemy life and count scale with the party. Downed players are revived by holding E for 3 s.

## Readability pass (branch `m3-readability`, 2026-10-30)
First playtest of the arena: "very hard to progress in the waves; not sure when I'm hitting something or how they hit me from a distance; the bow does not
work sometimes (no arrow fires at all)". Everything below is a P9 Placeholder; all numbers are data (`AttackCatalog`, `EnemyCatalog`, `ArenaWaves`, `CombatSfx`).

### The bow bug (root cause)
Three causes, all hidden because a shot had no visual at all:
1. **The shot's hitbox is one 60 Hz frame (frames 4-5)** and the owner only tested the attack clock once per rendered frame. Under 60 fps (the editor, any hitch)
   the clock stepped over the window, so `FireShot` never ran while the shot's sound had already played at the press. Fixed in Core: `AttackMachine.HitboxSwept`
   covers every frame the clock passed this tick; `HeroCombat` uses it for all attacks. Test: `BowReadabilityTests` (red before the fix at dt = 1/35 and slower)
   and a PlayMode shot at 20 fps.
2. **A click during the previous shot's 20-frame recovery was thrown away** (fast clicking fired every other shot). `AttackMachine.RequestSpecial` buffers the press
   (0.25 s plus the time to the cancel frame) while the aim button is held.
3. **The ray flew over short enemies**: it left at 1.4 m along the hero's facing (a slime is 1.0 m tall, a rat-king 1.2 m) and aimed at `Targetable.AimPoint`, which sits
   above a slime. Now: from 1.3 m, along the camera's aim, snapped to the enemy nearest the crosshair within 10 degrees (plus its body width) at its body centre, a
   0.25 m thick cast. A drawn arrow (a bright streak, 70 m/s) now flies on every shot, hit or miss, for every peer's bow.

### Your hits read
| What | Old | New |
|---|---|---|
| Enemy flash on a landed hit | on the HP change, a round trip later, fade 0.17 s | at contact (the attacker's client), full white 0.08 s then fades 0.12 s |
| Enemy reaction | none | squash 18-40% and a shove 0.18-0.53 m away from the attacker over 0.2 s (heavy hits more) |
| Hit spark | size 0.7 / 1.3 (finisher), 0.14 s | 1.1 / 2.0, 0.22 s |
| Damage numbers | 18 pt (crit 26), 1.1 s, no outline | 26 pt, weak point 32, finisher or charge 38 (orange), crit 46 (gold, "!"), 1.6 s, dark outline, a pop on appearing |
| Screen hit marker | none | a white X at screen centre for 0.18 s (gold and bigger on a finisher or charge) |
| Hit sound volume | Hit 0.8, HitHeavy 0.95, HitCrit 0.9 | all 1.0 |
| Swing whoosh volume | 0.5 | 0.3 (a hit is always louder than a swing) |
| Whiff | same as any swing | a new dull `Combat_Whiff` (0.3) when the hit window ends with no hit; the swing arc stays a dim blue smudge, and turns warm and strong on a connected swing |
| Hitstop frames (normal / finisher / charge) | axe 4/10/10, staff 3/8/9, dagger 3/8/8, bow-sword 4/9/9; spell 6, shot 3 | axe 6/14/14, staff 5/12/12, dagger 4/11/11, bow-sword 5/12/12; spell 8, shot 4 |
| Hitstop budget | 12 frames per 0.5 s | 16 (so a 14-frame finisher is never clipped) |

The recorded Kenney clips replace the generated hit and whoosh sounds by file, but the cue's volume comes from the recipe, so the volume edits apply to them too.

### Their hits read
- **Telegraph footprint** (`Core/Combat/Telegraph.cs`, drawn by `EnemyVisual`): built from the same `AttackData` and the same `HitGeometry` rules as the hit test, so the
  drawn area is where the hero's centre gets hit (range grown by the hero's radius, a cone's edge widened by the body's slack; `TelegraphTests` prove it point by point). It
  fills from the centre outward up to the strike frame, turns solid red while the hitbox is live, and fades 8 frames later. A lunge or hop is drawn around its **landing
  spot** with a lane to it (before, the disc sat at the enemy's own spot and travelled with it). The lunge now always ends before the hitbox opens, a rule
  (`EnemyAttackRules`) tested for every attack.
- **The glob lob** has a visible ball that flies in an arc and lands on the strike frame; lob reach 8.5 m -> 6.5 m.
- **Reach vs look** (`EnemyDefinition.VisualReach` = how far the greybox body extends in front; the rule is range <= VisualReach + 0.6, tested). Range in metres and wind-up
  (frames before the hitbox opens, 60 per second), old -> new:

| Attack | Range | Wind-up | Lunge |
|---|---|---|---|
| Slime hop (circle) | 1.5 -> 1.1 | 30 -> 34 | 2.2 m, ends at the strike |
| Slime glob (lob circle) | 1.6 -> 1.3 | 42 -> 46 | none |
| Rat-king lunge (cone) | 2.3 -> 1.45 (half angle 40 -> 42) | 16 -> 28 | 3.4 m; the old hit window ended with the lunge (about 5.7 m of reach from the start), now the lunge ends before it opens |
| Rat-king tail spin (circle) | 2.5 -> 1.45 | 28 -> 30 | none |
| Broom sweep (cone) | 3.1 -> 2.4 | 32 -> 36 | none |
| Broom poke (cone) | 2.2 -> 1.9 | 22 -> 28 | 1.2 m, now ends before the strike |
| Warden slam / flour sweep / oven-door pound | 3.6 / 3.8 / 4.4 -> 3.0 each | unchanged (56 / 40 / 72) | none |
| Warden break free | 4.0 -> 3.0 | 18 -> 30 | none |

  The AI's "start the attack" reach is now `Range + 0.7 x lunge + 0.2` for every shape (circles used to ignore their lunge).
- **When the hero is hurt:** a red wash and a bright band on the screen edge on the side the hit came from (relative to the camera, 0.55 s), the hurt sound at full
  volume, and a 0.25 s flinch (the body kicks back 18 degrees and dips 8 cm). With the Animator hero (Y Bot) the flinch is the clips' Hurt / HurtHeavy, driven by `HeroCombat.HurtSerial`; `HeroFlinch` (added to the hero at runtime) is only the
  mannequin fallback and does nothing while an `AnimatorHeroDriver` is working.

### Difficulty and HUD
- Tier 1 enemy attack x0.65 (`EnemyCatalog.Tier1Atk`): slime 12 -> 7.8, rat-king 18 -> 11.7, broom 15 -> 9.75, Warden 30 -> 19.5 (the 6.6 table values stay the base). A
  slime hop did 11 damage to the 100 HP hero, now 8. **Tier 2 is unchanged** (the table value x2.3), so the step from wave 4 to wave 5 is now x3.5: expected, tune after the retest.
- Wave 1: four slimes -> **two slimes**. Wave 2: two slimes + two rat-kings -> **three slimes + one rat-king**. Waves 3 and 4 unchanged.
- HUD: a big "Wave N - X left" line (34 pt, outlined) at the top centre; the hero's HP bar is 380 x 36 (was 280 x 22) with a large number, green, then amber, then a
  pulsing red below 30%. The target enemy's bar moved down under the wave line.

## Playtest checklist (answer yes or no after the next arena session)
1. Can you tell every time you land a hit (flash, number, sound, the X)?
2. Do you see every enemy attack coming before it lands (the filling ground footprint)?
3. Can you tell, from the ground marker alone, where an attack will land and how far it reaches, including a lunge or a lobbed glob?
4. Does the bow fire every time you click while aiming, even clicking fast?
5. Can you clear waves 1-3 with each heirloom (axe, staff, dagger and shield, bow and sword)?
6. Does dodging through an attack feel fair (never hit when you rolled, never safe when you did not)?
7. Does the dagger's parry window (the first 6 frames of a raised guard) feel learnable?
8. When you are hurt, do you know which side it came from and how much HP you have left?

**Retest result (user, 2026-10-30, on `main` after `1c44f60`): yes to all 8.** Notes: hits feel like hitting something even though the axe sits crooked in
the hands (grip to fix). The ground footprints helped, but the real game should read enemy attacks without indicators: telegraph through the enemy's own wind-up
(pose, anticipation, sound, a glint) and keep the footprint as an optional assist at most.

## Enemy polish: tells from the body, pacing (branch `m3-enemy-polish`, 2026-10-30)
User notes after the retest: enemy attacks must read without ground indicators (wind-up pose, anticipation, sound, glint); retune the wave 4 to 5 step with
`docs/research/combat-timings.md`. Everything is a P9 Placeholder, all numbers are data.

### How an attack is staged (Core `EnemyTell`, `TellMath`; drawn by `EnemyVisual`)
Studied from the research: early DC1 and KH enemies give 0.65-0.7 s from motion start to damage, KH1's first enemy holds its first pose 8 ticks, KH2's single
big strikes 1.2-1.3 s. Every enemy attack now goes **anticipate** (lean back / crouch / coil / rise to a peak) -> **hold** (still, shivering) -> **commit**
(the attacking part flashes white-hot with a star flare and a metallic "shing", 12-24 frames before the hitbox) -> a 6-frame snap into the **strike**
(overshoot forward, stretch) -> eased **recovery**. Each attack plays its own wind-up sound as it starts. Rules (`EnemyAttackRules`, tested): the glint leads
the hitbox by 12-24 frames, the enemy stops turning and has not started its lunge before the glint (where it faces at the glint is where it strikes), wind-up
at least 27 frames for small enemies, 54 for the elite's own attacks, 36 for its break-free; attack frames after the hitbox plus the AI's recover time at least 30.

| Attack | Wind-up (frames) | Pose | Glint part, lead | Sound |
|---|---|---|---|---|
| Slime hop | 34 | crouch 35%, shiver | lid, 18 | squelch (pitched up) |
| Slime glob | 46 | lean back 16, glob swells on the lid, thrown at the glint | lid, 20 | squelch |
| Rat-king lunge | 28 | rears back 22, crouch, shiver | head (crown), 14 | scratch |
| Rat-king tail spin | 30 | coils 75 the other way | tail, 14 | scratch |
| Broom sweep | 36 | twists back 60, leans 10 | bristles, 14 | wooden creak |
| Broom poke | 28 | draws back 20 | handle, 14 | creak (higher) |
| Warden slam | 56 | leans back 22, rises 0.25 m | rolling pin, 16 | low groan |
| Warden flour sweep | 40 -> **54** | coils 80 | rolling pin, 16 | groan |
| Warden oven-door pound | 72 | leans back, crouch, rises 0.35 m | whole body, 16 | groan |
| Warden break free | 30 -> **36** | crouch 30%, hard shiver | whole body, 14 | groan |
| **Warden dough quake (new signature)** | **90** (1.5 s) | rises 0.6 m, leans back 26, long trembling hold; crown glows hotter through the hold | crown, 18 | groan, then a **bell toll** at the peak |

Recovery: rat-king lunge and spin 18/16 -> 26/22 frames after the hit. The quake is chosen every 3 Warden attacks (never by the random roll), takes its
hero's attack token alone (nothing else starts during its tell), and leaves the Warden stuck 1 s extra: the reward for reading it. It is a 3 m ring (guard
break, knockback 6, x2.0).

The glob always shows in flight and casts a shadow that grows at its landing spot (a shadow, not a marker). **The ground footprint is an assist, off by
default** (`CombatAssists.GroundTelegraph`, saved per machine; **F2** in the arena toggles it, the F1 help shows its state).

### Pacing: attack tokens (Core `AttackPacing`, `AttackTokens`; host only, nothing replicated)
Before starting an attack the host asks for a token on its target hero: at most **2** enemies winding up or striking at one hero, and two enemies' strikes
on the same hero open at least a gap apart (**0.5 s** tier 1, **0.35 s** tier 2). A refused enemy hovers at the edge of its reach and strafes, asking again
every 0.2-0.45 s (KH's hovering crowd; DC1 never has more than 3 monsters near each other). A token lapses when its hit window closes (so it cannot leak) and
is released early on stagger, parry, death and despawn. The Warden's revenge break-free needs none.

### The wave 4 to 5 step
Against the 100 HP, DEF 10 hero tier 2 at x2.3 meant a 26-point slime hop and a 113-point Warden pound (a one-shot); DC1's early enemies take 19-46% per hit
(typically 25%). Tier 2 attack is now **x1.0** of the 6.6 table (a x1.54 step, not x3.5): slime hop 8 -> 11, rat-king spin 14 -> 22, broom sweep 11 -> 19,
Warden slam 27 -> 41, pound 32 -> 50. Tier 2 presses through pacing instead: cooldowns x0.7, recover x0.8, the tighter strike gap, and **mixed rosters**
(wave 5: 2 slimes, a rat-king, a broom; 6: 2 rat-kings, 2 slimes, a broom; 7: 2 brooms, a rat-king, 2 slimes; 8: the Warden, a rat-king, a broom). Life x3
and defence x3.3 are unchanged. Waves 1-4 keep their rosters and cooldowns.

### Checks
- EditMode: `EnemyTellTests` (tells honest, floors, punish window, signature, stage sampling, glint, crossing at low fps), `AttackTokensTests` (count, gap,
  lapse, release, exclusive, tier 2), updated `ArenaWavesTests` and `EnemyModelTests`.
- PlayMode: `CombatArenaTests.Solo_AttackTokens_KeepACrowdToTwoAttackers_AndTheirStrikesApart` (four slimes on an idle hero).
- Bot (`SC_BOT=1`): `Bot_TierTwo_WaveFive_StaysReadable`; the report now lists strikes, the closest two strikes, most winding up at once, most waiting, and
  hits taken within 0.5 s of another.
- Captures: `TellCaptureTests` (Explicit, `-Graphics`) writes every attack's tell frame by frame with the footprint off.

### Playtest checklist (answer yes or no; play with F2 off, the default)
1. Can you tell every enemy attack is coming from the enemy alone (its pose, hold and wind-up sound), with no ground marker?
2. Does the glint and its "shing" work as your cue to roll or guard (never too late, never so early it means nothing)?
3. Does the Warden's dough quake feel like an event: the rise, the bell, the glowing crown, the hold, then a window to punish it?
4. Do waves 5-8 feel harder through the mix and pace of enemies, without hits you could not see or answer?
5. Do enemies waiting their turn read as waiting (circling at the edge), not as broken AI?

## Network simulator
`Tests/PlayMode/CombatNetworkTests.Netsim_150msRtt_2PercentLoss_...` drives the Multiplayer Tools `NetworkSimulator` component (it works in PlayMode, in the
editor). The simulator delays each packet at the send and the receive stage of both peers, so a per-packet 37 ms gives about 148 ms RTT (75 ms gave 300). For
the manual walk: add a NetworkSimulator component to the App object, pick "37 ms, 2% loss" (custom preset), and play the two-player arena; hits on the attacker's
screen must play instantly (spark, hitstop, sound) and the damage number must follow within about one round trip.
