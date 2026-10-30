# Combat readability and feel timings (Dark Cloud 1, Kingdom Hearts 1 and 2)

Research for M3 tuning after the first arena play (2026-10-30: "can't tell when I hit", "can't see how enemies hit me from a distance", "waves too hard"). Measured from the user's own Dark Cloud 1 copy (SCUS-971.11) with the Ghidra project and data extraction set up in `dc1-rea.md`. Mechanics and numbers only, in our own words. Characters and monsters are named by index or table row, never by in-game name. Function names and addresses are from the decompile; data files are paths inside the disc's `DATA.DAT` (see `dc1-rea.md` for the extractor).

Status: **DC1 done, static and live.** Static numbers came from the decompile; the user's PCSX2 (own BIOS) then confirmed them live by reading the same timers every frame over PINE in a first-dungeon fight (table "Live checks" below). **KH1 and KH2 measured live (2026-10-30)**, section "Kingdom Hearts 1 and 2" below: RAM addresses found by watching values change over PINE (both executables are stripped), then scripted fights. The private measurement kit (PINE reader, RAM dumper, change logger, scripted inputs, save states, a warp into any DC1 dungeon and any KH2 room) is described in `C:\Dev\_private\ps2\emu\README.md`, outside the repo; every KH address is in `C:\Dev\_private\ps2\kh1\NOTES.md` and `...\kh2fm\NOTES.md`.

## Units and the tick
- **Tick.** All DC1 timers count once per game-loop step. The loop (`main` mode switch, then `MGEndFrame` @ 0x12d100) waits in `WaitVSync__Fi` @ 0x12d070 for the vblank counter (`vcount`, bumped in `VSyncCallBack__Fi`) to change. **Live: the 80-tick invulnerability ran out in exactly 80 vblanks, at 59.7 vblanks/s, every time: one step per field, 60 Hz.**
- **Motion frames.** Every character's and monster's animations sit on one long timeline per model. Each model's `info.cfg` (inside its `.chr` pack) lists `KEY start, end, speed` per motion id; the motion advances `speed` timeline frames per tick, and a script's `_SET_MOTION(id, speed)` can override the speed (-1 = the cfg value). Hitboxes are opened on timeline frame ranges, so **ticks = timeline frames / speed**.
- **Distance.** No metres. Reach and knockback are given as ratios to the victim's or attacker's own size: the monster table's body radius (row +0x60), the script's `_SET_BODY_COL` radius, and `BODY_SIZE` in `info.cfg` (17 for character 0, 10-22 for early monsters).

## Measured numbers

| # | What | Value (ticks at 60 Hz) | Source | Method | Confidence |
|---|---|---|---|---|---|
| 1 | Player invulnerability after a heavy hit (hit type 3) | **160** (2.67 s) | `BtCheckDamageProc` @ 0x1dbafd0 (DUN.BIN) sets the timer at 0x1dc44a0; `MoveChara` @ 0x1db08a0 counts it down; the hit test is skipped entirely while it is non-zero | decompile | high |
| 2 | ... after a light hit (type 2) | **80** (1.33 s) | same | decompile | high |
| 3 | ... after one hit of a multi-hit attack (type 4) | **8** (0.13 s) | same | decompile | high |
| 4 | Player hurt colour pulse | Red ambient tint, one sine half-cycle over **160 / 80 / 10** ticks (heavy / light / multi), peak red at the midpoint: it lasts as long as the invulnerability | `setUnitAmbientAnime` @ 0x1dc1000 (speed arg), `unitAmbientAnime` @ 0x1dc1050 (count += pi / speed per tick) | decompile + constants (0x2a18a0 = pi) | high |
| 5 | Player hurt brightness flicker | 24 ticks, brightness 128 +- 80 over 1.5 sine cycles (step pi/8 to 3 pi) | `setUnitDamageColor` @ 0x1dc0f20, used by `Draw_MainUnit` | decompile + constants 0x2a1dc4/0x2a1dc8 | medium (trigger site not traced) |
| 6 | Screen shake when the player is hit | **15** ticks (heavy) / **25** (light), random +-8 offset re-rolled every tick | timer 0x1dc45c4 set in `BtCheckDamageProc`, counted in `motionDrive` @ 0x1db7450 (offset 0x1dc45c6) | decompile | high (amplitude unit unknown) |
| 7 | Pad vibration on player hit | heavy: strength 230 for 22 ticks; light: 220 for 12 | `SetVibration(pad, 1, s, t)` in `BtCheckDamageProc` | decompile | medium (arg meaning inferred) |
| 8 | Heavy hit that doesn't kill: knock-down | Player turned to face the attacker, launched along the attacker-to-player direction at 1.0 unit/tick, slowing by 0.018 per tick per axis: about **28 units, 1.6x the hero's BODY_SIZE**; down for the whole knock-down motion (key 6, 295-335 at 0.3) = **133** ticks (2.2 s). The 160-tick invulnerability outlasts it by 27 | `BtCheckDamageProc`, `unitBlowActionRot` @ 0x1dc0e80, `MoveChara` (blow decay 0x2a1d24 = 0.018); `dun\mainchara\c01d.chr` info.cfg | decompile + data | high (distance) / medium (duration: end test reads key 6's end) |
| 9 | Light hit on the player | If the stick is pushed >= 0.8 the hero **keeps moving** (stays in the free state); otherwise a flinch of key 4 (285-291 at 0.4) = **15** ticks (0.25 s) | `BtCheckDamageProc` (stick test vs 0x2a1bcc = 0.8) | decompile + data | high |
| 10 | Being hit also | Cancels the current action and held item, drops lock-on (heavy), and **refills the combo gauge to 100** | `BtCheckDamageProc` | decompile | high |
| 11 | Guarding | Hits in the guard state do **no damage**: a light hit pushes the hero back by half the hit-to-hero offset, a heavy one by 0.1 unit/tick decaying (a nudge) | `BtCheckDamageProc` (state 6 / action 9 branches) | decompile | high |
| 12 | Hitstop / hit pause | **None.** No timer freezes or slows motion in the player-hits-monster path (`CMonstorUnit::CheckDmg` @ 0x1d9f10), the monster-hits-player path (`BtCheckDamageProc`) or the motion step (`motionDrive`). DC1 sells hits with flash, sound, number, shake and rumble only | three functions read end to end | decompile | high |
| 13 | Monster hit flash | Tint ramps 0 -> 1 at 0.08 per tick and back: **~25 ticks**, peak at tick 12-13. **Red** (255,0,0) when the hit did damage, **white** when it did nothing (immune part, wrong tool, zero) | `CheckDmg` writes the colour/rate at unit +0x1e500..0x1e53c; `CMonstorUnit::PalletStep` @ 0x1d8570 runs the ramp; rate 0x2a1d88 = 0.08 | decompile | high |
| 14 | Monster re-hit lockout | Got-hit script (entry 110) sets the monster invulnerable for **8-9** ticks and cancels its move (`_STATUS_SET_MUTEKI 8/9`, `_SET_MOVE_CANSEL`), then rolls 0-99 for its reaction | `dun\monstor\e06a.stb`, `e60a.stb` entry 110 | script walk | high (value) / low (reaction branch) |
| 15 | Monster knockback susceptibility | Row +0x98: 1.0 most early rows, 0.8 / 0.7 / 0.6 sturdier ones, 0.0 the boss and one large row | `MonstorTable` @ 0x27fb00 | data | low (label inferred, reader not traced) |
| 16 | Monster stun slows it | While a monster's status timer (+0x1e3e4, the 300-tick stun) runs, every motion plays at **half speed** | `_SET_MOTION` @ 0x1e1710 | decompile | high |
| 17 | Hero combo, per swing (character 0) | 5 swings. Swing 1: hit opens at tick **17**, live 10, done at 33. Swing 2: 10 / 7 / 27. Swing 3: 13 / 7 / 30. Swing 4: 13 / 10 / 33. Finisher: 20 / 13 / **93** (60 ticks of recovery). Full string 216 ticks (3.6 s) | `ToanKey_Play` @ 0x241690 (hit frames 825-828, 833-835, 842-844, 851-854, 862-866); `dun\mainchara\c01d.chr` keys 36-40 (820-884, speed 0.3) | decompile + data | high |
| 18 | Hero combo rules | A press anywhere during a swing queues the next; the chain happens only at the swing's **last frame** (no early cancel). Each extra swing costs 25 from a 0-100 gauge (needs >= 20). The gauge refills 0.25 per tick x max(1, SPD/30), never while swinging. Stopping after swing 1 locks attacks for 15 ticks. Holding the button through swing 1 turns it into the charge state | `ToanKey_On` @ 0x241450, `ToanKey_Play`, `motionDrive` | decompile | high |
| 19 | Hero hit power and push per swing | Hit sphere radius 2.8, 5.3, 6.2, 6.2, 6.2 (on the blade point); push strength 1.2, 1.2, 1.5, 2.0, 3.0 (second value 0.2, 0.2, 0.3, 0.4, 0.3); finisher damage x1.8; small forward lunge at the start of swing 1 | `ToanKey_Play` (`Set`, `SetKickBack`), constants 0x2a1c68.. | decompile + constants | high (values) / low (how the monster uses the push) |
| 20 | Early-enemy attack timings | See the table below: **wind-up 19-87 ticks (0.3-1.45 s), median ~40 (0.65 s)**; active 2-60; recovery 14-100 | scripts `dun\monstor\<row>.stb` (`_SET_DMG_COL frame, radius, start, end`, `_SET_DMG_PARA power, flags, type`), models' `info.cfg` keys | script walk + data | medium-high |
| 21 | Early-enemy reach | Hit sphere radius / body-collision radius = **1.2-1.5** (8.5/6, 10/7, 8.9/6.5, 8/5.5, 7/7, 8/10). The sphere sits on a limb bone, so real reach is that plus the bone's offset | same | data | high (ratio) |
| 22 | Early-enemy damage vs starting HP | Power 16-35 minus character 0's DEF 3 = **13-32 of 70 HP (19-46%) per hit**; typical floor-1 attacker 17-18 (25%), so 4-5 hits from full | scripts above; `CDngStatusData::Initialize` (HP 70, DEF 3) | data | high |
| 23 | Crowd density | Each monster is placed at least 25 units from any other and with **at most 2 others within 480 units**: fights are 1-3 monsters at a time, 15 per floor | `CMonstorUnit::ArrangementPos` @ 0x1d7fc0 | decompile (from `dc1-rea.md`) | high |

### Live checks (PCSX2 + PINE, first dungeon floor 1, 2026-10-30)
Method: a cleared-game save warped to floor 1; every monster's HP set to 60000 and the hero's DEF to 0 so fights last and hits register; timers read every frame (each sample tagged with the vblank count). Logs in the private kit. Opponent: table row 1 (attack A, light). Heavy-blow rows were forced by setting that monster's hit type to 3 in memory, so the engine's own heavy path ran.

| What | Static | Live | Note |
|---|---|---|---|
| Tick rate | 60 Hz (inferred) | **60 Hz** (80 ticks = 80 vblanks) | high |
| I-frames, light / heavy | 80 / 160 | **80 / 160** | 6 + 4 hits |
| Screen shake, light / heavy | 25 / 15 | **24 / 14** visible | first tick is the set |
| Light flinch | 15 | **12** | motion blending eats ~3 |
| Hit while walking (stick held) | no flinch | **no flinch**, keeps walking | confirmed |
| Heavy knock-down | 133 | **128** | |
| Heavy push | ~28 units | **22-24 units over ~48 ticks** (~1.4x the hero's BODY_SIZE) | launch speed 1.0/tick |
| Hitstop | none | **none**: hero's motion advanced 0.3 frames every tick straight through the hit frames | |
| Monster flinch (row 1) | key 8 = 50 | **50** | starts the same tick as the hit, cancels its attack |
| Monster re-hit lockout | 8-9 | **8** | |
| Monster red flash | ~25 (one ramp) | **23** (the got-hit script adds a second, faster pulse at 0.2/tick on top) | |
| Row 1 attack A, whole motion | 48 | **48** | |
| Row 1 re-attack interval while in reach | - | **106 ticks** (1.77 s) between hits | new |
| Hero swings 1-4 | 33 / 27 / 30 / 33 | **32 / 25 / 28 / 32** | finisher not timed (chain ended) |
| Damage (DEF 0) | power 20 | **20 per hit** (10% of this save's 200 HP; 29% of a new game's 70) | |

### Early-enemy attacks (dungeon index 0, floors 1-7)
Rows from `BtEnemyLayout00` (0x2917b0) and `MonstorTable`. Wind-up = ticks from the start of the attack motion to the first live hitbox frame; when the hitbox motion starts live, the motion played just before it is the wind-up and is added. Speeds are the ones the script passes to `_SET_MOTION` (else the cfg default).

| Row | HP | Body radius | Attack | Hit type | Power | Hit radius | Wind-up | Active | Recovery |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 23 | 6 | A (key 13 at 0.42) | light | 20 | 8.5 | **29** | 5 | 14 |
| 1 | | | B (key 16 at 0.42) | light | 21 | 8.5 | **19** | 7 | 21 |
| 1 | | | guard window (timeline 100-117) | | | | | | |
| 3 | 23 | 7 | lunge (key 13 at 0.5-0.56) | heavy | 22 | 10 | **34-38** | 2 | 18-20 |
| 52 | 12 | 6.5 | rush (key 18 at 0.35, then key 19 live throughout) | light | 17 | 8.9 | **60** | 60 | 87 |
| 52 | | | body contact while moving (key 0) | light + poison | 16 | 7.9 | 0 (always live) | | |
| 2 | 38 | 5 | A (key 12-14 at 0.2), sideways knock 100 deg | heavy | 25 | 8 | **58** | 11 | 31 |
| 2 | | | B (key 16 at 0.2 then key 17 at 0.24) | heavy | 26 | 8 | **87** | 6 | 44 |
| 96 | 13 | 7 | slam (key 13 at 0.35, then key 14 live) | heavy | 35 | 7 | **43** | 25 | 100 |
| 97 | 15 | 10 | spin (key 11 at 0.3) | light | 35 | 8 | 10 in-motion (preceded by an unresolved wind-up motion) | 37 | 3 |

Patterns: the fast, short-reach pokes are light (80 ticks of invulnerability, no knock-down); every heavy blow has a wind-up of at least ~35 ticks (0.6 s) and the slowest ones are the most damaging. Recovery after a big attack is long (44-100 ticks): that is the punish window.

## Kingdom Hearts 1 and 2 (live, PCSX2 + PINE, 2026-10-30)
Same method as the DC1 live checks: the user's own discs (KH1 NA SLUS-203.70; KH2 Final Mix JP SLPM-666.75), RAM read every poll (every 1-2 vblanks) over PINE, inputs scripted, enemy HP raised so fights last. Enemies are named by type order and motion id, never by in-game name; "hero" is the player character.

Setups:
- **KH1 fight:** New Game on Normal, first fight of the game (hero level 1, HP 18, after taking the sword and giving up the shield), 5 copies of the first enemy type (HP 10). A save state is taken as soon as they spawn.
- **KH1 roll:** a downloaded Expert-mode save, hero level 41 (the roll isn't available at the start).
- **KH2 fight:** a downloaded clear save (hero level 99, made on the hardest mode), with the difficulty byte switched to Standard for the runs, warped into a room of world 0x02 that spawns the basic grey enemy type of the second faction (HP 152 at that save's enemy level). The enemies are late-game strength, so their damage is not early-game damage (see the table).
- **Units:** vblanks at 60 Hz. KH1's game logic steps every 2 vblanks (30 Hz), so its timings are even numbers. KH2's numbers are ±2 frames, because the polls land every 2 vblanks. Knockback is in raw game units, with no scale found.

| What | KH1 | KH2 | Note |
|---|---|---|---|
| Hitstop on the attacker | **none**: the hero's motion clock advances every tick through every contact frame | **none**: the hero's animation advances 1 frame per frame through every contact; the game clock never stalls against vsync | high |
| Hitstop on the victim | **none**: the flinch motion starts and advances on the next tick | **~4 frames**: every hit reaction holds its first pose for 4 frames, vs 0-2 for every other motion start (attacks, idles, recoveries) | KH2 medium (±2) |
| Hero invulnerability after being hit | **~75-80 vblanks (1.25-1.33 s)**. Idle among 5 enemies, hits never landed closer than 80 apart (49 hits). Of 26 strikes from one enemy attack, all 14 that connected less than 80 vblanks after a hit did nothing, and all 12 outside that window landed | **none found**: a multi-hit enemy attack lands its ticks 12 frames apart. The roll's flag (next row) is never set by being hit. Single-hit protection is not proven either way | KH1 high (timer field not located); KH2 medium |
| Hero hurt reaction | light hit: hurt motion 70 vblanks, but control returns after one tick (the player can act through it) | light hit: hurt motion 72 frames | |
| Dodge roll | 38 vblanks (0.63 s), motion length 38 | **52 frames** plus a 26-frame recovery motion. A flag bit is set for **frames 0-34**, the probable i-frame window (2/3 of the roll). Holding it from outside doesn't stick, because the game rewrites it every frame, so the bit is unconfirmed | KH1 high (length) / no i-frame data; KH2 medium |
| Enemy reaction to a light hit | flinch motion 42 vblanks (0.7 s); a hit-reaction flag holds for 12 vblanks (probably the hit blink, unconfirmed) | flinch motions 52-72 frames; a big hit launches (motion 36 frames) | |
| Knockback | light hit: push 11.4 units/tick, ×0.64 each tick, so ~30 units over ~12 vblanks; combo finisher launches the enemy 530-790 units | light hit: 95-340 units over 40 frames (enemy movement included) | raw units |
| Early-enemy telegraph (motion start to first damage) | first enemy type: **42 vblanks (0.7 s)**. 8 of them a held first pose, then contact at motion clock 34; motion 86 total, so ~44 of recovery | basic grey type: multi-hit attack **40 frames (0.67 s)** to the first of 4 ticks (40/68/80/102 of 128); single strike **72-78 frames (1.2-1.3 s)** of a 160-frame motion; a 120-frame variant **62** | high |
| Damage vs hero HP | first enemy: **1 per hit = 5.6% of 18 HP** (its 70-vblank attack: 2 = 11%). The hero kills it in 3 hits (4, 4, then a finisher 8-17 of its 10 HP) | late-game strength only: Standard, enemy STR 43. Against hero DEF 150: 2 per multi-hit tick, 4-5 per single hit. Against DEF forced to 2: 5 per tick, 10-15 per single. Early-game % not measured | KH1 high; KH2 not comparable |

What this means for us:
- **Hitstop:** neither KH game freezes the attacker. KH2 pauses only the victim, for about 4 frames; KH1 doesn't pause at all. So our 3-10 frame hitstop is not taken from KH. If we keep it, keep it short and on the victim: about 4 frames for a normal hit.
- **Invulnerability after being hit:** KH1 gives ~1.3 s, DC1 1.33 s for a light hit, and KH2 nothing obvious. Two of three reference games agree on ~1.3 s. That backs recommendation 1, and puts our proposed 0.8 s light / 1.2 s heavy on the short side of the references.
- **Telegraphs:** the early KH enemies give 0.67-0.7 s before the first damage, the same as DC1's early median of 0.65 s. Single big strikes give 1.2-1.3 s. This backs the wind-up floors in recommendation 4.
- **Roll:** KH2's roll is ~0.87 s with about 0.57 s of probable i-frames at the start. KH1's is 0.63 s. That backs our ~30-frame roll with an early i-frame window (`kh-combat-camera.md` recommendation 8).

## Recommended M3 values
Ours today (main, `af71ed0`): 60 Hz frames, hero `PlayerVitals` 100 HP / DEF 10, no invulnerability after being hit (`HeroCombat` only applies a 0.25 s `HurtStagger`), hitstop 3-10 frames with a 12-frame cap, enemy hit-on frames 16-72.

1. **Add post-hit invulnerability (top fix for "too hard").** New constants next to `DodgeTiming` in `GuardMath` (e.g. `HurtInvuln`): **heavy (attack knockback >= 3 or guard-break) 1.2 s, light 0.8 s, multi-hit tick 0.15 s.** DC1 uses 2.67 / 1.33 / 0.13 s; ours is faster and co-op, so shorter, but it must outlast the hurt reaction by ~0.3 s. The victim's client already judges enemy hits (`HeroCombat` line ~458), so it is one more `IsInvulnerable` check there; no host change.
2. **Show the invulnerability.** A colour pulse on the hero for the same duration (DC1: red tint, one sine half-cycle over the whole window) plus a 0.4 s flicker. Players read it as "safe now".
3. **Heavy hits knock down, light hits don't.** Enemy attacks with `knockback >= 3` (`BroomSweep`, the three Warden slams, `WardenBreakFree`): knock-down **0.9 s** (DC1's measured 2.1 s is too long for us), invulnerable 0.3 s past it. Light hits: keep `HurtStagger` 0.25 s (DC1 15 ticks = 0.25 s, a match), and **skip the stagger when the move stick is held** (DC1 rule).
4. **Wind-up floor for every enemy attack: 0.45 s (27 frames) for small enemies, 0.9 s for heavy and elite.** In `AttackCatalog`: `RatLunge` hit-on 16 -> **28** (total 40 -> 52), `BroomPoke` 22 -> **28**, `WardenBreakFree` 18 -> **36**, `WardenSweep` 40 -> **54**. Leave `SlimeHop` 30, `SlimeGlob` 42, `RatSpin` 28, `BroomSweep` 32, `WardenSlam` 56, `WardenPound` 72. DC1's early median is 0.65 s and its heavy blows are never under 0.55 s.
5. **Shrink enemy reach to "body + a limb" (the "hit from a distance" complaint).** DC1 hit spheres are 1.2-1.5x the body radius. Ours: slime 1.5 m on a 0.55 m body (2.7x), rat lunge 2.3 m plus a 3.4 m slide (4.6x before the slide), broom 3.1 m (5.6x). Set range ~= radius + 0.7 m: `SlimeHop` 1.2, `RatLunge` 1.2 with lunge 2.0 m, `RatSpin` 1.4, `BroomSweep` 1.9, `BroomPoke` 1.8 with lunge 0.8; Warden attacks 2.6-3.2 (radius 1.0). Where an attack must reach far, the telegraph must show the reach (ground decal of the real shape: the readability branch's job).
6. **Recovery is the reward.** Give every enemy attack recovery >= 0.5 s after the hit window (DC1: 14-100 ticks, long after heavy ones). `RatLunge` currently has 16 frames after its window: raise `TotalFrames` to keep ~30 frames after the new hit-off.
7. **Enemy hit flash: two layers.** A white flash for **4 frames** on impact, then a red fade of **~25 frames** (DC1's red ramp, peak at frame 12, is too slow alone to mark the instant of contact). White-only flash when a hit is blocked or does nothing (DC1 uses white for "no effect"; our broom's bristles and guarded hits).
8. **Keep hitstop, it is not from DC1.** DC1 has none (confirmed live), and neither does KH1; KH2 pauses only the victim, for ~4 frames (section above). Our 3-10 frames with the 12-frame cap stays, but it is our own choice, not a KH copy, so keep normal hits near 4 frames and on the victim. If the user still can't tell hits, raise normal hits to 5 and finishers to 10-12 before anything else; DC1's other cues (rumble 12-22 frames, shake 15-25 frames when *you* are hit) are worth copying for the victim side.
9. **Waves: fewer at once.** DC1 never has more than 3 monsters near each other. `ArenaWaves`: wave 1 **3 slimes** (was 4); spawn each wave in a trickle **1.5 s apart**; and an attack token so at most **2 enemies are winding up at once** (per player). The per-extra-player enemy stays.
10. **Don't raise or lower damage yet.** DC1 early enemies take 19-46% of starting HP per hit (typical 25%); ours take ~11-22% (Warden pound ~49%). Our deaths come from overlapping attacks and no invulnerability, not from damage. Retest after 1, 4, 5 and 9.

Also worth knowing, not for M3: DC1 swings chain only at the end of each swing (no early cancel), a press anywhere in a swing queues the next, and the finisher has a full second of recovery; our input buffer plus cancel frames is already more responsive. Being hit refills DC1's combo gauge, a small comeback rule.

## Gaps
- **KH1 and KH2 (left open after the live runs)**: KH1's invulnerability timer field and its roll i-frames; whether KH2's roll bit really is invulnerability; KH2 single-hit protection; KH2 damage at early-game enemy strength (needs an early save or the enemy level table); the enemy hit flash in both (KH1 has a 12-vblank hit-reaction flag, unconfirmed as the flash); a knockback scale in either game.
- Guard was not confirmed live (holding R1 with the hero facing away still took a flinch; the guard needs facing and was not set up).
- Monster flinch was measured for row 1 only (50 ticks); other rows' flinch motions are in their `info.cfg` but the script picks them by a random branch.
- What reads the hero's push values (row +0x98 susceptibility), row 97's wind-up motion, row 2's sideways knock, the hurt flicker trigger site.
