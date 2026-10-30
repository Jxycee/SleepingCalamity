# Combat readability and feel timings (Dark Cloud 1, Kingdom Hearts 1)

Research for M3 tuning after the first arena play (2026-10-30: "can't tell when I hit", "can't see how enemies hit me from a distance", "waves too hard"). Measured from the user's own Dark Cloud 1 copy (SCUS-971.11) with the Ghidra project and data extraction set up in `dc1-rea.md`. Mechanics and numbers only, in our own words. Characters and monsters are named by index or table row, never by in-game name. Function names and addresses are from the decompile; data files are paths inside the disc's `DATA.DAT` (see `dc1-rea.md` for the extractor).

Status: **static route done** (DC1). **Emulator route** (PCSX2 + PINE) pending: the user installed PCSX2 with their own BIOS on 2026-10-30; numbers marked "emu" in the gaps list are the next measurements. KH1's executable has no symbol table (only stripped `main` + VU overlays), so KH1 needs the emulator; nothing new for KH1 here.

## Units and the tick
- **Tick.** All DC1 timers count once per game-loop step. The loop (`main` mode switch, then `MGEndFrame` @ 0x12d100) waits in `WaitVSync__Fi` @ 0x12d070 for the vblank counter (`vcount`, bumped in `VSyncCallBack__Fi`) to change, and its profiler prints load as a % of one 262-line NTSC field. So the logic is capped at **60 Hz, one step per field**, dropping to 30 only when a frame overruns. Seconds below assume 60 Hz (confidence medium-high; the first emulator check confirms it by watching a timer count down against frame advance).
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

## Recommended M3 values
Ours today (main, `af71ed0`): 60 Hz frames, hero `PlayerVitals` 100 HP / DEF 10, no invulnerability after being hit (`HeroCombat` only applies a 0.25 s `HurtStagger`), hitstop 3-10 frames with a 12-frame cap, enemy hit-on frames 16-72.

1. **Add post-hit invulnerability (top fix for "too hard").** New constants next to `DodgeTiming` in `GuardMath` (e.g. `HurtInvuln`): **heavy (attack knockback >= 3 or guard-break) 1.2 s, light 0.8 s, multi-hit tick 0.15 s.** DC1 uses 2.67 / 1.33 / 0.13 s; ours is faster and co-op, so shorter, but it must outlast the hurt reaction by ~0.3 s. The victim's client already judges enemy hits (`HeroCombat` line ~458), so it is one more `IsInvulnerable` check there; no host change.
2. **Show the invulnerability.** A colour pulse on the hero for the same duration (DC1: red tint, one sine half-cycle over the whole window) plus a 0.4 s flicker. Players read it as "safe now".
3. **Heavy hits knock down, light hits don't.** Enemy attacks with `knockback >= 3` (`BroomSweep`, the three Warden slams, `WardenBreakFree`): knock-down **0.9 s** (DC1 2.2 s is too long for us), invulnerable 0.3 s past it. Light hits: keep `HurtStagger` 0.25 s (DC1 15 ticks = 0.25 s, a match), and **skip the stagger when the move stick is held** (DC1 rule).
4. **Wind-up floor for every enemy attack: 0.45 s (27 frames) for small enemies, 0.9 s for heavy and elite.** In `AttackCatalog`: `RatLunge` hit-on 16 -> **28** (total 40 -> 52), `BroomPoke` 22 -> **28**, `WardenBreakFree` 18 -> **36**, `WardenSweep` 40 -> **54**. Leave `SlimeHop` 30, `SlimeGlob` 42, `RatSpin` 28, `BroomSweep` 32, `WardenSlam` 56, `WardenPound` 72. DC1's early median is 0.65 s and its heavy blows are never under 0.55 s.
5. **Shrink enemy reach to "body + a limb" (the "hit from a distance" complaint).** DC1 hit spheres are 1.2-1.5x the body radius. Ours: slime 1.5 m on a 0.55 m body (2.7x), rat lunge 2.3 m plus a 3.4 m slide (4.6x before the slide), broom 3.1 m (5.6x). Set range ~= radius + 0.7 m: `SlimeHop` 1.2, `RatLunge` 1.2 with lunge 2.0 m, `RatSpin` 1.4, `BroomSweep` 1.9, `BroomPoke` 1.8 with lunge 0.8; Warden attacks 2.6-3.2 (radius 1.0). Where an attack must reach far, the telegraph must show the reach (ground decal of the real shape: the readability branch's job).
6. **Recovery is the reward.** Give every enemy attack recovery >= 0.5 s after the hit window (DC1: 14-100 ticks, long after heavy ones). `RatLunge` currently has 16 frames after its window: raise `TotalFrames` to keep ~30 frames after the new hit-off.
7. **Enemy hit flash: two layers.** A white flash for **4 frames** on impact, then a red fade of **~25 frames** (DC1's red ramp, peak at frame 12, is too slow alone to mark the instant of contact). White-only flash when a hit is blocked or does nothing (DC1 uses white for "no effect"; our broom's bristles and guarded hits).
8. **Keep hitstop, it is not from DC1.** DC1 has none; our KH-style 3-10 frames with the 12-frame cap stays. If the user still can't tell hits, raise normal hits to 5 and finishers to 10-12 before anything else; DC1's other cues (rumble 12-22 frames, shake 15-25 frames when *you* are hit) are worth copying for the victim side.
9. **Waves: fewer at once.** DC1 never has more than 3 monsters near each other. `ArenaWaves`: wave 1 **3 slimes** (was 4); spawn each wave in a trickle **1.5 s apart**; and an attack token so at most **2 enemies are winding up at once** (per player). The per-extra-player enemy stays.
10. **Don't raise or lower damage yet.** DC1 early enemies take 19-46% of starting HP per hit (typical 25%); ours take ~11-22% (Warden pound ~49%). Our deaths come from overlapping attacks and no invulnerability, not from damage. Retest after 1, 4, 5 and 9.

Also worth knowing, not for M3: DC1 swings chain only at the end of each swing (no early cancel), a press anywhere in a swing queues the next, and the finisher has a full second of recovery; our input buffer plus cancel frames is already more responsive. Being hit refills DC1's combo gauge, a small comeback rule.

## Gaps (next: emulator)
- **emu: tick rate** (confirm 60 Hz by watching the invulnerability timer 0x1dc44a0 drop 160 -> 0 against frame advance).
- **emu: KH1** hitstop frames, enemy flash frames, dodge-roll i-frames, early telegraphs, knockback and early damage as % of HP (no symbols: needs PINE memory watching or frame-stepped captures).
- Monster flinch duration after a hit (script entry 110 picks a reaction motion through a random branch the walker can't resolve) and what reads the hero's push values; frame-step one floor-1 fight.
- Row 97's wind-up motion and row 2's sideways-knock meaning; DC1 dodge (DC1 has no roll for character 0, only guard: not measured).
- Hurt flicker trigger site (`setUnitDamageColor` argument) not traced.
