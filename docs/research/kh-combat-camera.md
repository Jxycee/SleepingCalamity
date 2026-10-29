# Kingdom Hearts 1 / 2 combat, lock-on and camera: research notes

Phase 0 research, 2026-10-29. Mechanics and numbers only, in our own words. No game code, data tables, text or assets are reproduced. The sources are the OpenKH docs, the KH2FM WIP decomp (symbol names and small functions), fan wikis, and a short byte-level look at the user's own KH2FM ELF. Local clones (read-only, outside the repo): `C:\Dev\_research\OpenKh`, `C:\Dev\_research\kh2`.

## Summary
1. KH2 combat is **driven by animation data**. Each attack animation carries frame-range windows: hitbox on, combo-input open, finisher allowed, reaction command allowed, "can't be hit" (i-frames), and "turn toward lock target at speed X". The code mostly reads those flags. This is the main pattern to copy.
2. Targeting has **two layers**. A soft auto-target (a range, a height band and a timer, plus an attack-assist cone that steers swings) runs all the time. A hard lock-on (toggle, acquire range, release range) sits on top. The camera has a separate, shorter "lock radius" while locked.
3. Combos are **counted, not timed**: about 2 normal hits plus a finisher by default. Abilities add or remove hits (Combo Plus / Negative Combo). Finishers are tagged in the attack data and get their own damage bonuses.
4. Bosses resist stunlock with a **hidden "revenge" gauge**. Every hit adds a fixed amount (opener 1, finisher 3, big spells 4 to 4.5), and when the gauge passes the boss's cap (Lingering Will: 25) the boss breaks free.
5. Gaps: no hitstop numbers, no i-frame durations, and no vanilla camera distances. Those values load from data files at runtime, and we have no KH2 disc data. No KH1 decomp exists that we could find.

Units, inferred: the engine runs at 60 fps (OpenKH anb doc), so fields measured in time are frames at 60 Hz. Distances look like about 1 unit = 1 cm: Sora's jump height is about 185 and his hang height about 150. So "1200" is about 12 m. This is an inference, not documented.

Provenance tags: **[doc]** = OpenKH file-format doc. **[ex]** = a value from OpenKH's mod-patch *example* file (plausibly vanilla, unverified). **[wiki]** = fan wiki. **[elf]** = our own read of SLPM_666.75 (its sha256 matches the decomp's target). **[forum]** = community claim.

---

## 1. Lock-on

**Rule (KH1 and KH2) [wiki]:** with no lock-on, attacks auto-target the nearest valid enemy. Tapping R1 hard-locks one target and the camera follows it. KH1 cycles targets with R2/L2. KH2 holds R2 and flicks the right stick toward the next target. Lock-on stays on until toggled off or until the target leaves the release range. In KH1, while locked, room exits don't trigger.

**Soft lock (auto-target) [doc]:** the player preference block has an "auto lock-on" group: range, min and max height relative to the player, a time value, a height adjust, and an "inner adjust" that weights candidates by facing (a dot-product threshold). That group sits next to an **attack-assist** group that defines the swing's target search: radius, min and max height, horizontal angle (the cone), vertical angle and range, a separate "S range" and "U range", and air variants. The search decides who a swing homes onto and how far the player lunges ("attack distance to speed" in the system block [doc], example 0.1 [ex]). Doc: `OpenKh/docs/kh2/file/type/preferences.md` (Plyr).

**Hard lock [doc][ex]:** the system block holds a single "lock-on range" and a separate "lock-on release range". The example file sets **both to 1200** (about 12 m). That would mean no hysteresis, i.e. a target at the edge flickers in and out. Unverified as vanilla. Doc: preferences.md (Sstm). The decomp also exposes a per-character "lock-on ratio" and a "lock-out range" getter: `YS::STATUS::GetLockOnRatio / GetLockOutRange` (`kh2/src/yasui/libys/status.cpp`). Those values are runtime globals, so the data isn't in the ELF.

**Lock-on and animation [doc]:** attack animations carry a "turn to lock-on target" window, both as a range trigger and as a single-frame trigger, each with a turn-speed parameter. A "turn to target" trigger handles the soft-target case. So homing during a swing is authored per animation, not hard-coded. Source: `OpenKh/docs/kh2/file/anb/anb.md` (Motion Triggers).

**Code structure [decomp symbols]:** `YS::LOCKON` has start(target), auto_lock and a set/reset player. `PLAYER::lockon_end`. `dk::LockOn` is the HUD reticle. AI and event scripts can hide the reticle (`trap_lockon_hide`). Source: `kh2/functions.tsv`.

## 2. Third-person camera

**Structure [decomp][elf]:** one object-follow camera (`dk::ObjCamera`) with swappable behaviours: target camera (lock-on), talk, reaction-command, event, scope/first-person, cannon, book, light-cycle, dangle. The follow camera stores a live `radius` (distance), plus a `defaultRadius` and a separate **`defaultLockRadius`**. Lock-on swaps in its own boom length, and two small init functions reset the radius to one or the other (`kh2/src/harata/libdk/objcamera.*`). In the ELF, a preset loader at 0x140e30 copies a 6-float row (radius, lock radius and four others) from a table indexed by preset, then calls the radius init [elf]. That table lives in BSS, so it fills at runtime from data. The decomp also has a per-battle camera data getter (`YS::BATTLE::GetCameraData`), so camera presets change per area or fight.

**Collision [decomp]:** the camera has a collision pass (`ObjCamera::collision`). The lock camera adds its own `updateCollision`, an eye-collision check, and a "visible object" check (it keeps the target on screen and pulls in past walls). `kh2/src/harata/libdk/targetcamera.hpp`.

**Player options [decomp][wiki]:** a config switch between **auto** and **manual** camera (`Tz::Config::isCameraAuto`), right-stick camera, zoom in and out, and left/right controls (`dk::CameraConfig`). KH1 used L2/R2 to rotate and had no right-stick orbit; KH2 added the right stick. Camera shake has a "no kick" toggle (`dk::Camera::setNoKick`), so impacts can kick the camera.

**Numbers:** none recovered. Distances, heights and auto-rotate speeds are in data. See Gaps.

## 3. Combo structure

**Rules [wiki]:**
- Ground and air combos are a **count of hits**. Each Negative Combo removes one hit, and two of them (with no Combo Plus) leave **only the finisher**. So the base is about **2 normal hits + 1 finisher** on the ground and in the air (KH2).
- Combo Plus / Air Combo Plus add one normal hit each and stack. KH1 and KH2 both cost 1 AP.
- The finisher is the last hit. It is a distinct move, usually knockback or launch, often guard-piercing. Some finishers are ability-gated choices (e.g. a leap that breaks guard, or a shot that knocks the target down).
- **Combo Boost [forum]:** ground finisher damage gains +10% per hit in the combo, i.e. (1 + normal hits) x 10%, and stacks linearly. That matches the system block's "combo damage up" example value **10** [ex]. The same block's "reaction damage up" is **50** [ex], i.e. +50% reaction-command damage from its boost ability.

**Data model [doc]:**
- Every hitbox references an **attack-parameter entry**: type (normal, pierce-armor, guard, special, cure), power, element, team mask, enemy reaction class, two knockback strengths (the distance scales with the victim's weight), hit interval (re-hit every N frames for multi-hit), a reflect/guard response, revenge damage, and a combo group. A "kind" flag marks combo finisher, air combo finisher or reaction command. Doc: `00battle.md` (Atkp).
- Which attack a character picks next comes from a **party-attack table**. Each entry has a motion, a next motion, jump and speed limits, a near/far distance band, a low/high height band, an inner cone (min/max), a blend time and a distance adjust. The same table selects both "which swing fits this target's distance and height" and party AI attacks. Doc: `00battle.md` (Ptya).
- The **combo-input window** and the **finisher-allowed window** are frame ranges on the animation (anb range triggers "allow combo" and "allow combo finisher"). A hitbox trigger can carry a combo id. The "weapon trail" is a window too. Doc: `anb.md`.
- **Magic in combos:** a spell has separate ground, finisher and air motions with blend times (`00battle.md`, Magc), and a spell cast as the finisher plays its finisher motion.

## 4. Hitstop / hit pause

Not found in any doc or decomp symbol. No "hitstop / slow / freeze" names exist. There's only hit-mark positioning and a vibration table (`Vbrt` in 00battle, and a "trigger vibration" frame trigger on attack animations). Hitstop is likely a per-attack or per-reaction value we haven't identified, or it's baked into the victim's reaction animations. See Gaps.

Outside reference (not KH): an IEICE 2023 study of 3D action games found **0.1 to 0.2 s** hitstop and **0.2 to 0.4 s** slow motion felt best on a blow-away hit (https://ken.ieice.org/ken/paper/20230315OCSt/eng/).

## 5. Enemy reactions

- **Reaction class per attack [doc]:** each attack entry says whether the victim flinches, gets knocked back, launched and so on, and carries two knockback strengths scaled by the victim's weight. The system block holds global blow physics: blow speed, blow-up, brakes, a minimum XZ, "hit back" distances (example 200, and 200 for the small version [ex]), and an air-blow stop. `preferences.md` (Sstm).
- **Per-group immunities [doc]:** an enemy "pattern" group decides how it reacts to special effects (immune, drawn in, flinch, knocked out, flinch-plus-instant-revenge). A global "stop" list can disable damage reactions per entity. `00battle.md` (Patn, Stop).
- **Super armour [doc]:** the "pierce armor" attack type and the "Pattern enable/disable" anb triggers switch armour on and off per animation window.
- **Revenge (anti-stunlock) [wiki]:** a hidden per-boss gauge. Hits add fixed amounts. Ground opener 1, follow-up 1, ground finisher 3, air hits 1.5, air finisher 3.5, launcher or guard-break moves 3, a big burst finisher 6. Mid-tier spells add 2 to 4.5, and guarding adds 0. When the gauge reaches the boss's cap (**Lingering Will = 25**), the boss does a set counter or teleport and the gauge resets. The cap can change per phase. Source: https://www.khwiki.com/Forum:Notes_41_(Revenge_Value) and https://www.khwiki.com/Revenge_Value. The attack data has a "revenge damage" field and the animations have a "revenge check" frame trigger [doc], which confirms the gauge is data-driven per hit.
- **Reaction commands [wiki][doc]:** a contextual Triangle prompt. An animation window ("allow RC") or a hitbox-attached "reaction command (self)" trigger enables it, so an enemy's attack animation can open a counter window for the player. Most RCs accept an early press with no penalty (only three KH2 RCs punish early input). Limit and form RCs need an equipped ability, a drive-gauge cost and low HP. https://www.khwiki.com/Reaction_Command

## 6. Dodge and guard i-frames

- **Dodge Roll:** KH1 is a 1 AP ability (Square + direction). In KH2 base game it is only a reaction command. KH2FM makes it a growth ability with 4 levels from Limit Form, and higher levels roll farther [wiki]. The form-ability block has a "dodge roll star time" field (star = invincibility) alongside "air slide star time" [doc]. The example values are 0.7 and 0.5 [ex], with **unknown unit**. They sit among multipliers, so they're probably not frame counts. https://www.khwiki.com/Dodge_Roll
- **Guard:** costs 2 AP in both games [wiki]. KH1: one blocking swing that blocks frontal hits only. If the weapon's "recoil" beats the attack's, Sora recovers fast enough to counter or deflect projectiles; otherwise he recoils longer. KH2: invulnerable to applicable attacks *during a successful guard animation*. Sora's spin start can block some hits from behind, and it reflects some attacks back. Limit Form's guard starts faster, lasts longer and recovers sooner [wiki]. Each attack entry carries a "reflect self / reflect other" response (reflect, guard, nothing) and a reflect range and angle [doc]. So which hits can be guarded is set per attack. https://www.khwiki.com/Guard
- **I-frames as animation windows [doc]:** the "hitbox off (can't be hit)" range trigger turns invulnerability on for a frame range, and AI has a "disable invincibility" trigger. Roll, guard and RC invulnerability are authored the same way.

## 7. MP and damage formula

- **KH2 enemy stats [doc]:** enemy HP = base HP x level-table HP%, rounded up (`(hp*pct+99)/100`). Each enemy has a damage cap and a minimum damage. Elemental weaknesses are percentages (100 = normal, 200 = double, 0 = immune). An attack's critical adjust can halve damage or zero it. `00battle.md` (Lvpm, Enmp).
- **KH2 physical formula [forum, unverified]:** roughly (strength + weapon strength) x attack power factor, minus defense. Not confirmed from data or decomp.
- **KH1 [forum, unverified]:** x = strength minus defense (plus technique and critical corrections). Damage equals x when x is small (below 8), grows faster above that, and has a floor of about half the attack's added value. https://gamefaqs.gamespot.com/boards/200737-kingdom-hearts-hd-i5-plus-ii5-remix/75189033
- **MP [ex][wiki]:** KH2 MP recharges in a time-based "MP charge" state after the bar empties, instead of per-spell regen. The haste tiers cut that charge time by 25 / 50 / 75% (example values 0.25 / 0.5 / 0.75 [ex]). Spells also feed the drive gauge ("MP to drive" and "MP drive rate" fields [doc]).

## 8. Party AI (cheap pass)

- **KH1 [wiki]:** the player sets how often Donald draws from offensive, defensive and support magic. Situational AI picks the actual spell. Goofy and Donald also have behaviour settings (aggression, item use).
- **KH2 [doc][decomp]:** party members run bytecode AI scripts (`OpenKh/docs/kh2/file/ai/`) and pick attacks from the same party-attack table, gated by distance and height bands (section 3). A `FriendPersonality` class makes item and heal decisions (e.g. `hpItem`), and `Friend::is_party` checks membership (`kh2/functions.tsv`). Per-character movement tuning (walk 2.0, run 8.0 per frame, turn about 15 degrees per frame [ex]) shares one schema with the player.

---

## Design takeaways for Sleeping Calamity

1. **Author combat as animation events.** Put hitbox-on, combo-window, finisher-window, cancel-window, i-frame, turn-to-target (with turn speed) and super-armour windows on each attack clip as data (a ScriptableObject timeline or animation events into Core). Core logic reads the flags; designers tune feel without code. The same windows drive enemies and companions.
2. **Use two targeting layers.**
   - The soft target is always on: a cone (start around 60 degrees half-angle), a range band and a height band, weighted by facing and distance. Each swing homes and lunges toward the soft target.
   - The hard lock is a toggle with **hysteresis**: release range at least 1.25x acquire range (e.g. about 10 m acquire, 12 to 13 m release), plus a short grace timer before dropping a target that breaks line of sight. Do this unlike the example's 1200/1200.
   - Switching is a right-stick flick that picks the nearest target in the flick direction.
3. **Give the lock camera its own boom length.** Store a free radius and a lock radius per area preset (KH2 swaps presets per area and fight). Keep both the player and the target in frame, and add camera collision pull-in with a target-visibility check. Use Cinemachine: a third-person follow camera, plus a target group for lock-on and a Deoccluder.
4. **Count combos, don't time them.** Base 2 hits + finisher on the ground and in the air. Weapon growth (our Dark Cloud layer) can add hits or change the finisher, like Combo Plus. The finisher carries knockback, a revenge-value spike and a damage bonus that scales with combo length (about +10% per prior hit is a good starting number).
5. **Make a revenge gauge mandatory for co-op.**
   - Keep one gauge per boss **on the host**, summed across all players' hits, with per-hit values in attack data (opener 1, finisher 3, heavy 4 to 6) and a cap per boss phase.
   - **Scale the cap with player count**, e.g. cap x (1 + 0.5 x (players - 1)), or bosses break free nonstop with 4 players.
   - Without it, four players stunlock any boss forever.
6. **Keep hitstop cosmetic and local.** Start at about 3 to 6 frames at 60 Hz for normal hits and 8 to 12 for finishers. Apply it on the attacker's own client (animation speed dip plus a camera kick) and on the victim's presentation. **Never freeze the host simulation** or other players' time. With 4 players, cap stacked hitstop so a flurry from teammates doesn't stutter your view.
7. **Lock-on is owner-side; hit resolution is host-side.**
   - Each player's soft and hard lock runs on their own client (instant camera response) and is sent to the host as an intent ("I'm targeting X").
   - The host resolves hits with the attack data (host authority per our netcode rules), and reactions and revenge come back from the host.
   - Predict the local swing's homing on the owner so latency doesn't make swings drift.
8. **Author i-frames and guard per attack.** Roll i-frames as an animation window (start with about 10 to 15 frames of a roughly 30-frame roll; tune by feel). Guard uses a per-attack "guardable / reflectable / unblockable" flag, as KH2's attack data does. A host-side check of the dodge window needs some latency tolerance. Let the owner's dodge state win inside a small window (about 100 ms); this is not anti-cheat, so be lenient.
9. **Reaction-command-style contextual prompts** come cheap with the same window system. An enemy animation opens an "RC allowed" window on players in range. That suits co-op prompts on bosses and stays optional for Phase 1.

## Gaps

- **No KH1 decompilation found.** The handoff's "WIP decomp" is the KH2FM one (`github.com/govanify/kh2`). Searches found decomps only for Chain of Memories (GBA) and 358/2 Days (DS). The KH1 ELF strings give only HUD names.
- **Camera numbers:** default radius, lock radius, height and auto-rotate speed load at runtime into a BSS table (KH2FM ELF, loader at 0x140e30, table 0x4573f8). They need the game's data files (KH2.IDX/IMG, not on disk) or an emulator RAM read. We stopped here per the brief.
- **Hitstop:** no data field or code symbol identified. The next step would be frame-stepping footage in PCSX2, or finding the attack-to-reaction path in Ghidra.
- **I-frame durations** for roll and guard: only a field of unknown unit (0.7 / 0.5 example values). Not measured.
- **Vanilla data values:** the [ex] numbers come from OpenKH's patch example and are probably, but not certainly, vanilla. Two of them (MP Haste 25/50/75 and the +10% combo-boost step) match community-known effects.
- **Damage formulas** for KH1 and KH2 are community claims, unverified against data or decomp.
- **Soft-target cone angles and ranges** (the Plyr attack-assist values) are unknown. Only the field list is known.
- KH1 party AI and camera have only wiki-level detail.

## Sources

- OpenKH docs: `C:\Dev\_research\OpenKh\docs\kh2\file\type\00battle.md`, `preferences.md`, `docs\kh2\file\anb\anb.md`, `docs\kh2\file\ai\`, `docs\tool\GUI.ModsManager\listpatch_information.md` (example values) - https://github.com/OpenKH/OpenKh, https://openkh.dev
- KH2FM decomp: `C:\Dev\_research\kh2\functions.tsv`, `src\harata\libdk\objcamera.hpp`, `targetcamera.hpp`, `cameraconfig.hpp`, `src\yasui\libys\lockon.cpp`, `status.cpp` - https://github.com/govanify/kh2
- KH2FM ELF (user's copy, hash matches the decomp target): preset loader 0x140e30.
- Wikis: https://www.khwiki.com/Lock-On, /Combo_Plus, /Negative_Combo, /Guard, /Dodge_Roll, /Reaction_Command, /Revenge_Value, /Forum:Notes_41_(Revenge_Value)
- Forums: GameFAQs KH2 Negative Combo / Combo Boost threads (https://gamefaqs.gamespot.com/boards/915410-kingdom-hearts-ii/68274376), and the KHFM damage thread above.
- Hitstop study: https://ken.ieice.org/ken/paper/20230315OCSt/eng/
