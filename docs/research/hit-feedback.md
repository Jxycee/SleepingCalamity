# Hit feedback ("game feel"): hitstop, shake, rumble, flashes, VFX, audio, accessibility

Date: 2026-11-01. Written in our own words: mechanics and numbers only. Builds on `combat-timings.md` (DC1: no hitstop, shake 15/25 ticks when the hero is hit, white flash for no-effect), `combat-depth.md`, and `kh-combat-camera.md` section 4 (KH2 pauses only the victim, about 4 frames). It does not repeat them.

Source quality: the Sources list marks what was fetched and read and what is only a pointer. The talks by Vlambeer, Jonasson/Purho and Eiserloh were not readable from here as text; their numbers below come from implementations that cite them, or from common practice, and are marked. Treat every number as a start value to tune in the walk.

## 1. What we already have (read first)

| Piece | Where | State |
|---|---|---|
| Hitstop, attacker | `AttackData.HitstopFrames`, `AttackMachine.Freeze`, `HitstopBudget` | Done. Freezes the attacker's own attack clock. Budget caps stacked frames (16 per 0.5 s). PHASE1 start: 4 frames normal, 10 finisher. Heavy hits were raised to 14 in the readability pass. |
| Hitstop, victim | none | Enemy has a flash and a squash/shove, no freeze. |
| Camera kick | `AttackData.CameraKick` (0..1) -> `CombatEvents.CameraKick` -> `HeroCameraRig.Kick` | Done, but it is a **FOV kick** that decays with unscaled time. It is not a shake: no offset, no rotation, no noise. |
| Hit flash | `Enemy` `FlashHoldSeconds 0.08`, `FlashFadeSeconds 0.12`, `HitReactSeconds 0.2` | Done, networked via the attack results, drawn in `EnemyVisual`. |
| Hit sparks, numbers | `CombatFx` (spark, floating damage numbers) | Done. |
| Sound | `Core/Audio/CombatSfx` plus `CombatCue` enum | Cues exist (Swing, HitNormal, HitFinisher, HitCrit, Blocked, Parry, Dodge, PlayerHurt, EnemyDie...). Layering and pitch variance not checked here. |
| Rumble | `IInputService.SetRumble(low, high)`, `PlayerInputAdapter` | The method exists and is cleared on disable. **No combat code calls it.** |
| Events | `CombatEvents` (static, local, cosmetic; Swing, LocalHit, Cue, Number, CameraKick, HeroHurt, HurtFrom...) | The right seam. Every peer raises them for what it sees. |
| Accessibility toggles | none found | Needed (section 10). |

So the gap is not "add juice from scratch". It is: a proper shake (trauma), victim-side hitstop, rumble, layered feedback per event from data, a screen-space punch on big moments, and settings to turn parts off.

## 2. Hitstop (hit pause)

### 2.1 What the sources say

| Source | Point |
|---|---|
| Sakurai's Famitsu column on hitstop (translated, read) | A brief freeze on a hit makes it feel strong. Stronger attacks freeze longer. There is a cap, no matter how much damage. Both fighters freeze for the same time, but the attacker also vibrates a little (most 2D fighters shake only the victim). Grounded characters shake side to side and airborne ones up and down so feet do not sink into the floor. The shake amplitude scales with camera distance and tapers off before the freeze ends. The flinch pose eases into the hurt pose over 4 frames. Hurtboxes stay put so hits do not randomly whiff. Projectiles get less freeze; electric attacks get more. Some moves are tuned per hit area (sweet spot longer). |
| Smash wiki, hitlag (read via search summary; fan-maintained) | Freeze length is a function of damage, with a cap of 20 frames in Melee and 30 from Brawl on. A 15% hit is about 8 to 10 frames in the older games and 15 in Ultimate. |
| Monster Hunter community notes (search summaries; not primary) | The big charged finisher of a heavy weapon has the longest pause. Reported example: first hit of a string 14 frames and later hits 7. Capcom said it reduced weapon hitstop in Wilds. Sharpness reportedly changes the hit effect, not the pause length. Treat all as anecdotes. |
| Our own research | DC1 has none; KH1 none; KH2 about 4 frames on the victim only. Our hitstop is a design choice, not a DC copy. |

### 2.2 Rules we should use

- Scale by weapon weight and by attack: light 3, normal 4 to 5, heavy 8 to 10, finisher 10 to 14 frames at 60 Hz. Cap total per 0.5 s (existing 16).
- **Attacker and victim are different clocks.** The attacker's own animation and the victim's flinch freeze for the same short time, but the game world, the camera, other players and enemies keep running. Never freeze the whole `Time.timeScale` in co-op: one player's hit would stutter everyone else. The existing per-machine freeze is correct.
- Add the Sakurai-style micro-shake during the freeze: 1 to 2 cm sideways jitter on the victim (alternating sign every frame), tapering to zero. It tells the eye "frozen, not crashed".
- Let a freeze be cancelled by the attacker's next input after the hit frame (buffer already works through hitstop).
- Cut the pause on multi-hit moves: first hit full, later hits 40 to 50% (the MH anecdote and common practice), or the swing turns into a stutter.
- Weak or glancing hits (guarded, blocked, no damage): 0 to 2 frames, white flash only.
- Killing blow: add 2 to 4 frames to the normal value, then let the death anim run in slow motion for a moment (section 8).

### 2.3 Victim hitstop

Add a victim hold: the enemy's pose holds its first hit frame for 3 to 4 frames (matches KH2's measured 4) before the flinch plays. Armoured enemies skip it. This is presentation only: the host AI state does not wait. Show it by freezing the visual's animator speed, not the AI.

## 3. Camera shake

### 3.1 Trauma model (Eiserloh, GDC 2016, via implementations)

The talk itself was not readable as text. Implementations that follow it agree on this:

- `trauma` is a number 0 to 1 that events add to and that **decays linearly** over time.
- The visible shake is `trauma` raised to a power (2 or 3), so small hits barely move the camera and big ones jump.
- The motion itself is **smooth noise** (Perlin or similar), not white random, so it looks like a physical shake. Sample it at a fixed frequency.
- Shake is applied as **rotation** (yaw, pitch, roll) and a small offset. In 3D, keep offsets near zero or tiny because translation pushes the camera into walls; rotation does not.

One engine docs page (Godot camera system, read) lists defaults you can start from:

| Setting | Their default | Ours to start |
|---|---|---|
| Exponent | 2 | 2 |
| Noise frequency | 14 Hz | 12 to 16 Hz |
| Decay | 0.9 trauma per second | 1.5 for hits, 0.7 for big events (per-event, below) |
| Max rotation at trauma 1 | (3, 3, 2) degrees | (2.0, 2.0, 1.5) degrees for a third-person boom camera |
| Max offset at trauma 1 (3D) | 0 | 0 to 0.05 m |
| Octaves | 2 | 2 |

With exponent 2, trauma 0.5 shows only 25% of the max, and 0.3 shows 9%. So per-event trauma has to be larger than it looks.

### 3.2 Per-event trauma (common practice start values)

| Event | Trauma add | Notes |
|---|---|---|
| Normal hit landed | 0.10 to 0.15 | barely visible; the main cue is hitstop |
| Heavy hit | 0.25 | |
| Finisher or crit | 0.35 | |
| Hero hit (unguarded) | 0.35 to 0.45 | victim side is stronger than attacker side (DC1: shake only when you are hit) |
| Hero hit, guarded | 0.15 | |
| Perfect parry | 0.2 | plus a hit pause, sharp |
| Enemy slam / quake near the hero | 0.4 to 0.6 scaled by distance | scale by `1 - dist / 12 m` |
| Elite death | 0.5 | |
| Level-up, ability pop | 0.0 | use light and sound, not shake |

Cap total trauma at 1. Do not stack a shake add faster than every 0.05 s from the same source.

### 3.3 Other camera feedback

| Technique | Use |
|---|---|
| FOV kick (we have it) | Keep for finishers and charge release: -2 to -4 degrees on strike, recover in 0.15 s. Not for every hit. |
| Push-in / pull-back | Tiny dolly on a finisher: 0.1 to 0.2 m over 0.1 s. |
| Directional kick | Small camera push away from the hit direction (about 0.05 m, 80 ms). Gives direction when the hero is hit. |
| Co-op | Shake and FOV are **local only**: a client shakes only for hits on its own hero and its own attacks (and big room events scaled by distance). Never shake a camera for an ally's small hit. |

Cinemachine 3 route: add a `CinemachineImpulseSource` per event and a `CinemachineImpulseListener` on the camera, or drive our own trauma component and apply it after Cinemachine in the late update. Our own small component is easier to unit test (pure function of trauma and time) and to bind to the settings slider. Impulse is the fallback if the boom camera gets stacked effects.

## 4. Controller rumble

Facts from the Input System docs (read): `Gamepad.current.SetMotorSpeeds(low, high)` takes 0 to 1 for each of two motors; on most pads the left motor is a low-frequency heavy rumble and the right is a high-frequency light buzz. `InputSystem.PauseHaptics()`, `ResumeHaptics()` and `ResetHaptics()` pause and clear. Support is limited: Xbox and PS4 pads on Windows, plus console-specific packages for consoles. A DualShock 4's rumble and light bar share one command channel, so rapid mixed calls can drop. No rumble on WebGL or DualShock 3.

Design rules (common practice):

- Low motor = weight (hits, slams). High motor = sharpness (cuts, crits, parry clash, UI ticks).
- Keep pulses short: 60 to 150 ms for hits, 200 to 400 ms for big ones. Constant long rumble numbs the player and drains battery.
- Use a small state: `rumbleLow = max(active pulses)` with each pulse decaying linearly, not a queue of `SetMotorSpeeds` calls. One call per frame at most.
- Always send zero when the app loses focus, the game pauses, or the hero dies (`PlayerInputAdapter` already clears on disable).
- Rumble is local to the player who owns the pad. A player's rumble fires on their own hits and being hit, not on allies'.
- Keep a master slider (0 to 100%) and an off switch.

Start table (low / high, duration):

| Event | Low | High | Duration |
|---|---|---|---|
| Normal hit landed | 0.0 | 0.25 | 60 ms |
| Heavy hit | 0.35 | 0.35 | 100 ms |
| Finisher / crit | 0.6 | 0.5 | 160 ms |
| Hero hit | 0.7 | 0.3 | 180 ms |
| Hero hit, guarded | 0.3 | 0.1 | 80 ms |
| Parry | 0.2 | 0.7 | 80 ms |
| Slam near the hero | 0.8 x scale | 0.2 | 300 ms |
| Level-up | 0.0 | 0.4 | pulse 2 x 80 ms |
| Death | 0.8 | 0.0 | 400 ms |

DC1 for reference: rumble 12 to 22 frames on being hit (`combat-timings.md`), which is 200 to 370 ms; ours are in range.

## 5. Flashes and rim flashes

| Layer | Value | Note |
|---|---|---|
| White flash on the victim | 4 frames at full, then fade (we have 0.08 s hold, 0.12 s fade) | Keeps. |
| Colour fade after the flash | red or event colour, about 25 frames (from `combat-timings.md` rec. 7) | Keeps. |
| Rim / fresnel flash | Add a rim term to the enemy shader driven by the same flash value, so the silhouette lights even in dark rooms | Cheap, reads from a distance, works on URP Lit with a shader graph add. |
| Blocked or no-effect | White flash only, no colour | DC1 rule, adopted. |
| Hero hit | Short screen-edge flash from the hit direction (`HurtFrom` exists) at 20 to 30% opacity, 0.2 s | Directional, not full screen. |
| Low HP | Slow vignette pulse | Not a flash; keep below 2 Hz. |

Implementation: one `MaterialPropertyBlock` value `_HitFlash` per renderer, set from a `FeedbackFlash` helper. Do not swap materials (breaks batching and GPU instancing).

## 6. Sparks and VFX layering

Layers for a hit, in order of the time they show (common practice):

| # | Layer | Timing | Notes |
|---|---|---|---|
| 1 | Impact flash (a bright billboard or shape) | frame 0, 2 to 3 frames | the "stamp" of the hit; scale by weight |
| 2 | Directional slash / streak | frame 0 to 4 | follows the swing direction, tells which way it came |
| 3 | Sparks / chips / dust | frame 0 to 20 | material-based: stone dust, goo splat, wood chips, metal sparks |
| 4 | Smear / ring distortion | frame 0 to 6 | optional, finishers only |
| 5 | Lingering residue | up to 60 frames | goo splat, scorch, drip. Keep within budget. |

Budget: pooled particle systems, at most 3 live impact bursts per hero, 12 per screen; finisher VFX may exceed for 0.3 s. Everything is local, spawned from `CombatEvents.LocalHitLanded`/`Cue`.

Material tint by the enemy species: slime = jam splash, rat = fur puff and dust, broom = straw and splinters, Warden = flour puff. This also gives free variety without new animations.

## 7. Damage numbers: yes or no

Our code already pops numbers (`CombatFx`, crit, blocked, heavy). Keep them, with rules:

| Rule | Why |
|---|---|
| Yes for an action RPG with growth (weapon levels, ABS) | Players read numbers to judge growth; DC1 shows them. |
| Colour code: white normal, yellow crit, orange heavy, grey blocked, green heal, red on the player | Already in `DamageNumber`. |
| Merge rapid hits: sum numbers within 0.25 s into one popping figure | Stops number spam in swarms. |
| Number budget: at most 8 live per screen; drop the oldest of the lowest weight | Readability and cost. |
| Own hits at full size; allies' hits at 60% size and 60% alpha | Co-op clutter. |
| Setting: Off / Mine only / All | Accessibility and taste. |

## 8. Audio layering

Common practice (not from a single fetched source): build each hit as 3 layers.

| Layer | Length | Job | Variance |
|---|---|---|---|
| Transient | 10 to 40 ms | The click or crack that says "contact" | 3 to 5 variants, pitch +-4% |
| Body | 80 to 200 ms | The material (goo splat, wood knock, thud) | 3 variants, pitch +-6% |
| Tail | 200 to 600 ms | Room and weight: low thump or short reverb tail | 1 to 2 variants, volume scaled by weight |

More rules:

- Never play the same clip twice in a row (use a shuffle bag). Randomise pitch a little and volume +-1 dB.
- Weight scales the low layer, not just the volume: heavy hits add a sub thump (50 to 90 Hz) and drop the transient pitch 1 to 2 semitones.
- Crit and finisher add a bright accent layer (a ring or chime) so the player hears the difference, not only sees it.
- Duck the music 2 to 3 dB for 150 ms on a finisher; duck ambience on a slam.
- Limit voices: max 6 impact sounds per 100 ms; drop the quietest.
- Placeholders are CC0 packs (PHASE1 6.6). Hit sound is half the feel, so spend effort on the transient layer first.
- Hitstop interaction: play the sound at frame 0 of the freeze, not after it.
- Co-op: your own hits play at full volume and centred; allies' hits are spatialised and 3 to 6 dB lower.

## 9. Time slow, screen punches, level-ups

### 9.1 Time slow on finishers

- Slow the **presentation**, not the simulation: slow the animators and particles of the attacker and the victim to 0.2 to 0.4 for 0.08 to 0.15 s, then ramp back over 0.1 s. The host AI and other players do not slow.
- Only on: the last enemy of a room, an elite finisher, a perfect parry into a counter. Not on every kill.
- Single player may slow the whole time scale for 0.1 s on a boss death; in co-op skip it, or slow only the camera-local effects.

### 9.2 Chromatic and radial punch in URP

Official docs (URP, 6000.0): the Volume system lets a global volume override post effects such as Chromatic Aberration, Vignette and Lens Distortion; blending between volumes is weighted. I could not fetch the exact parameter table, so verify ranges in the editor. Practical pattern:

- One global Volume holds the "punch" overrides at weight 0. A `FeedbackPostFx` component raises the weight on an event and decays it (animation curve), so no per-frame allocation.
- Chromatic Aberration intensity: 0.2 to 0.5 peak on a heavy hit or hero hit, 0.1 s attack and 0.25 s decay. Keep it off the whole-screen for ordinary hits.
- Radial punch: a short Lens Distortion dip (-0.1 to -0.2) or a custom fullscreen radial-blur pass centred on the hit point's screen position, 0.1 to 0.15 s. A custom pass costs a fullscreen blit; use the Render Graph path of URP 17 and only enable it while active.
- Vignette: a 0.1 punch on hero hit, plus the existing directional hurt flash.
- All of it is local, capped, and hidden when the "reduce flashes" setting is on.
- Cost: chromatic aberration and distortion are cheap fullscreen passes. Radial blur is not; budget with the existing quality-first stance, but the ceiling is the 1440p target in the project notes.

### 9.3 Level-ups and rewards

No shake. Use a 0.3 s light bloom pulse, a rising chime, a ring burst, and a brief tick of rumble on the high motor. Keep the gameplay running (co-op!). The HUD shows the number.

## 10. Knockback and hit reactions

| Aspect | Rule |
|---|---|
| Direction | Along the attacker-to-victim line, flattened; for sweeps along the swing tangent. |
| Distance | By weapon weight: light 0.2 m, normal 0.4 m, heavy 0.8 m, finisher 1.5 m (start values). Our `HitResult.Knockback` already exists in metres and respects armour. |
| Easing | Ease-out over 0.15 to 0.25 s (we do `_knock -= step` with decay: fine). Never instant teleport. |
| Squash | Scale along the hit axis 0.85 for 3 frames, spring back with a small overshoot (0.2 s total). `HitReact` already drives a squash and shove; scale by `HitHeavy`. |
| Armoured / committed enemy | No flinch (existing). Give a tiny flash plus a spark plus a 1 frame shake so the player knows the hit counted. |
| Pain escalation (Doom 2016, reported) | Small reactions for small damage, flinch on poise break, stagger state on break, with a clear window for a finisher. We have poise and an "open" window already. |
| Launcher / heavy | A short upward pop (0.3 m) for finisher kills, then ragdoll-lite fall. Optional. |
| Death | Hold the last hit pose for 3 frames, then fall/pop with a puff. Drop timing sync to the reward (`CombatRewards`). |
| Hero hit reaction | `HeroFlinch` exists. Keep the player's control returning fast (DC1 and KH return control within a tick). Do not lock input during the reaction beyond the existing 0.25 s stagger. |

## 11. Hit feedback per game (what to learn)

| Game | What is reported | Use |
|---|---|---|
| Vlambeer "Art of Screenshake" (Nijman, talk 2013) | Stack many small effects on one action: kick the camera back, add to a decaying shake value, sound, muzzle flash, recoil, bigger bullets, screen flash, enemy knockback and a short pause. No fetched transcript; one secondhand blog gives "kick back 6 px, add 4 to the shake value, shake decays fast, capped". | Layer small effects; cap and decay shake. |
| Jonasson and Purho "Juice it or lose it" (2012) | Takes a plain Breakout clone and adds only feedback (tweens, particles, sound, shake) to make it feel alive. Counter-talk (Kelly, GDC Europe) says it has weaknesses. | Juice supports clarity; do not hide state behind effects. |
| Sakurai (Smash) | Hitstop by power, with a cap; attacker vibrates; victim ease into hurt. | Section 2. |
| Hades | A tiny slow-motion on hit is reported by a reviewer. No developer breakdown found. | Short presentation slow on specific hits only. |
| Doom (2016) | Pain escalation; glory kill makes the player invulnerable during the animation. | Finisher cinematics must not let allies be hurt unfairly; in co-op, keep the finisher short. |
| Monster Hunter | Longer pause on heavy finishers, shorter on repeated hits (anecdotal). | Section 2.2. |
| God of War (2018), DMC | No primary breakdown found. Known by play: heavy hits have strong camera and sound weight, small hits stay light. | Use contrast: most hits light, a few big. |

## 12. Accessibility and settings

| Setting | Values | Effect |
|---|---|---|
| Camera shake | 0 to 100% (default 100) | Scales trauma add. 0 also disables FOV kick and push-in. |
| Reduce flashes | Off / On | Disables screen-edge flash, caps white flash to 1 frame at 60% brightness, turns off chromatic and vignette punches, no strobing finishers. Keep under 3 flashes per second (general photosensitivity guidance). |
| Rumble | 0 to 100% and Off | Scales both motors. |
| Hitstop | Full / Reduced / Off | Reduced = half; Off = 0 for the attacker. Never changes damage or timing of the hit. |
| Damage numbers | Off / Mine / All | Section 7. |
| Hit sounds | Master + "Impacts" slider | via the mixer. |
| Motion | "Reduce motion" master that sets shake 25%, kick off, slow-mo off | One switch for people who want less. |

Rules: every setting is local, stored in the existing settings object (`PlayerInputAdapter` already listens to `_settings.Changed`), applied at the feedback layer only. None of them may change gameplay timing, damage or networking. Test that toggling a setting leaves host state identical.

## 13. Recommended design for Sleeping Calamity

### 13.1 Where it lives

- **Presentation**, in a new folder `Presentation/Feedback/`: `FeedbackDirector` (MonoBehaviour, one per client scene) subscribes to `CombatEvents` and turns each event into a feedback play from data.
- Core holds the pure parts: `TraumaShake` (trauma, decay, sample to angles), `PulseRumble` (pulse mixer), `HitstopPolicy` (frames by weight, multi-hit falloff), `FlashCurve`. EditMode-testable, no UnityEngine.
- Local-only cosmetic: nothing here is a NetworkVariable, RPC or host state. The only networked inputs are the events that already sync (attack results, `HeroHurt`, `NetAttackSerial`). Every peer plays its own feedback for what it sees; the attacker's client plays its own hit instantly (existing rule in PHASE1 6.4).
- Existing `HitstopBudget` stays. `HeroCameraRig.Kick` becomes one of the consumers (FOV part).

### 13.2 Event to feedback table (start values; frames at 60 Hz)

Weight classes: L light, N normal, H heavy, F finisher / charge.

| Event | Hitstop (atk / victim) | Shake trauma | Rumble low / high, ms | Flash | VFX | SFX layers |
|---|---|---|---|---|---|---|
| Hit L | 3 / 2 | 0.08 | 0 / 0.2, 50 | white 3f | small spark, dust | click + body |
| Hit N | 4 / 3 | 0.12 | 0 / 0.25, 60 | white 4f, red fade | spark + streak, material burst | click + body |
| Hit H | 8 / 5 | 0.25 | 0.35 / 0.35, 100 | white 4f, red fade, rim | big flash + streak + burst | click + body + low thump |
| Hit F / charge | 12 / 8 | 0.35 | 0.6 / 0.5, 160 | white 5f, rim, mild screen punch | flash, ring, burst, residue | all 3 + accent + sub |
| Crit (adds) | +2 | +0.1 | +0.1 / +0.2 | yellow tint | extra star burst | accent chime |
| Guarded / blocked (enemy side) | 1 / 1 | 0.05 | 0.1 / 0.1, 40 | white only 2f | grey sparks | metal / wood clack |
| Whiff | 0 | 0 | 0 | none | trail only | swing swoosh |
| Kill | +3 / hold 3f | 0.30 | 0.4 / 0.3, 120 | white then dissolve | puff + drop pop | thump + pop |
| Elite kill | 12 / 12 | 0.50 | 0.8 / 0.3, 300 | white, vignette | large burst | all + sub, music duck 150 ms |
| Hero hit | 0 (do not freeze the victim's own controls) | 0.40 | 0.7 / 0.3, 180 | screen-edge directional, 0.2 s | blood-free puff, hit sign | thud + vocal grunt |
| Hero hit, guarded | 1 | 0.15 | 0.3 / 0.1, 80 | small rim | shield sparks | block clack |
| Parry | 4 | 0.20 | 0.2 / 0.7, 80 | bright white flash 3f | ring clash | high clang + accent |
| Dodge (perfect) | 0 | 0 | 0 / 0.15, 40 | slight desaturation 0.1 s (optional) | whoosh trail | whoosh |
| Enemy slam near hero | 0 | 0.5 x `1 - d/12` | 0.8 x scale / 0.2, 300 | dust | ground crack, dust | boom + tail |
| Level-up / ABS pop | 0 | 0 | 0 / 0.4, 2 x 80 | bloom pulse | ring, motes | chime rise |
| Down | 0 | 0.30 | 0.8 / 0, 400 | vignette | fade | low sting |
| Revive | 0 | 0 | 0 / 0.3, 100 | warm bloom | motes | warm sting |

Victim hitstop is a presentation hold of the enemy's animator; the host AI never waits.

### 13.3 Data asset shape

One `HitFeedbackProfile` ScriptableObject per profile (not per attack), referenced by id from `AttackData`/`CueId`, so the Core attack data stays free of UnityEngine types:

```
HitFeedbackProfile
  id                string        // "hit_normal", "hit_finisher", "hero_hurt"...
  hitstopAttackerFrames int
  hitstopVictimFrames   int
  multiHitFalloff       float     // 0.5 = later hits at 50%
  trauma                float
  traumaDecayPerSec     float
  fovKick               float     // existing CameraKick, 0..1
  rumble                RumblePulse[]   // low, high, ms, curve
  flash                 FlashSpec       // white frames, tint, fade, rim
  postFx                PostFxSpec      // chroma, vignette, distort, ms, curve
  vfx                   VfxRef[]        // prefab ids, per material tag
  sfx                   SfxLayer[]      // clip bank id, delay ms, gain dB, pitch range, voice limit
  timeSlow              SlowSpec        // scale, ms (presentation only)
  accessibilityGroup    Flags     // Shake | Flash | Rumble | Hitstop | TimeSlow, which toggles scale it
```

`AttackData.HitstopFrames` and `CameraKick` stay for now (Core owns weights); the profile is chosen by weight class plus material tag. Later, those two fields become the defaults the profile can override.

### 13.4 Test plan

EditMode (Core, no scene):

| Test | Checks |
|---|---|
| Trauma | Add clamps at 1; decay linear; shake output = `trauma^2` scaled; zero at zero; deterministic for a seed. |
| Shake setting | Slider 0 gives exactly zero output; 50% halves the add. |
| Hitstop policy | Frames by weight; multi-hit falloff; global cap; setting Off returns 0; Reduced halves. |
| Rumble mixer | Max of overlapping pulses; decays; never above 1; zero on stop; slider scales. |
| Flash curve | Peak, hold frames, fade; reduced-flash caps brightness and frame count. |
| Data | Every `CombatCue` and weight class maps to a profile; no profile has an empty SFX group where one is required. |
| Strobe guard | A scripted burst of events produces at most 3 flashes per second with reduce-flash on. |

PlayMode (a few): a hit event raises the camera rig's rotation within 1 frame and returns to rest within the decay time; a fake input service records a rumble call on hero hit and zero on pause; changing a setting leaves enemy HP and phase identical (feedback does not touch host state). Walk (manual, `sc-walk`): record a short sheet per weight class with the volume and shake at default; the user judges feel. Network smoke: client A hits at 150 ms RTT, feedback fires on A at once, on B only the allowed distant ones.

### 13.5 Staged build

| Stage | Work | Done when |
|---|---|---|
| 1 | `TraumaShake` in Core + camera rig consumer (rotation shake after Cinemachine). Wire `HeroHurt`, `LocalHit` heavy/finisher. Slider. | Hero hit and finisher shake; tests green. |
| 2 | `PulseRumble` + `IInputService.SetRumble` calls from the director; zero on pause, death, focus loss; slider. | Pad rumbles on hit and hero hit; off works. |
| 3 | `HitFeedbackProfile` assets for the table above; `FeedbackDirector` replaces ad hoc calls in `CombatFx` for hit/hurt. Keep old paths until parity. | One data change retunes a weight class. |
| 4 | Victim hitstop hold + micro-shake; multi-hit falloff in `HitstopPolicy`. | Enemies hold the pose 3 to 4 frames; no host change. |
| 5 | Flash polish: rim term in the enemy shader, `_HitFlash` property block, reduce-flash cap. | Silhouette flashes in a dark room. |
| 6 | Audio layers and pitch variance via the mixer; voice limit; music duck on finisher. | Three-layer hits; no repeat in a row. |
| 7 | `FeedbackPostFx` volume (chromatic, vignette) + optional radial punch; presentation time slow on elite kills. | Punch visible; off with Reduce flashes; frame cost measured. |
| 8 | Accessibility panel: all toggles in 12; automation tests; a walk sheet at 1 and 4 players. | Every toggle verified; user walk notes. |

Stage 1 and 2 give the largest gain per hour: the game has hitstop and a FOV kick but no shake or rumble.

### 13.6 Risks

- Over-juice hides telegraphs. Cap total effect on the hero's screen; check the "can I still see the enemy's tell" case in every walk.
- Cinemachine ordering: apply our shake after the brain, or it is overwritten.
- Shared hits in co-op: shake and rumble from allies must be gated by distance and ownership.
- Photosensitivity: the reduce-flash switch must be easy to find (first-run and in pause).
- Post-FX passes cost in the 1440p quality target; measure before stage 7.

## Sources

Fetched and read:
- Sakurai's Famitsu column on hitstop, translated and discussed (Source Gaming): https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/
- Unity Input System, Gamepad (SetMotorSpeeds, PauseHaptics, supported devices and caveats): https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Gamepad.html
- Trauma shake defaults (exponent, decay, noise frequency, rotation and offset limits), a Godot camera system doc that follows the Eiserloh model: https://www.strayspark.studio/docs/godot-camera-system/noise-and-shake
- Doom (2016) pain escalation, Game Developer (secondhand): https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-

Found by search, snippets only (anecdotal or fan-maintained):
- Smash hitlag formulas and caps: https://www.ssbwiki.com/Hitlag
- Monster Hunter hitstop discussion and the Wilds reduction: https://deltiasgaming.com/?p=113987 (fetch failed; snippet only) and https://gamingbolt.com/monster-hunter-wilds-weapon-hitstops-will-be-closer-to-what-people-are-looking-forward-to/amp
- Game Developer page for Eiserloh's talk (description only, no formulas): https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math
- "Juice it or lose it" overview pages: https://www.gamedeveloper.com/design/video-is-your-game-juicy-enough-
- Vlambeer "The Art of Screenshake" mentions and a secondhand note on its numbers: https://infovore.org/tag/feel/ (not the talk itself)
- Hades slow-motion remark in a review: https://www.pcgamer.com/uk/hades-is-already-a-killer-combat-game-in-early-access

Pointers, not read (check before relying):
- Squirrel Eiserloh, "Math for Game Programmers: Juicing Your Cameras With Math", GDC 2016 (GDC YouTube channel).
- Jan Willem Nijman, "The Art of Screenshake" (INDIGO Classes 2013).
- Martin Jonasson and Petri Purho, "Juice it or Lose it" (2012).
- Unity URP post-processing, Chromatic Aberration page (6000.0 docs): http://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-chromatic-aberration.html
- Cinemachine impulse (CinemachineImpulseSource / Listener) in the Cinemachine 3 manual.

Not found: primary breakdowns of God of War, Devil May Cry or Hades hit feedback numbers; exact Monster Hunter frame data. Our own measured DC1 and KH numbers are in `combat-timings.md` and `kh-combat-camera.md`.
