# Music stems plan (spec step 5d)

Date: 2026-11-06. Docs only: **no audio is composed, recorded or generated here, and no code is written.** This plan says which stems the Undercroft needs and which game state switches each one, so a later audio pass and a later wiring pass have a contract. Everything marked **[PLACEHOLDER]** is a number or name to tune or rename; everything marked **[OPEN]** needs the user.

Sources: spec `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` section 7 (music line), section 5 (floors), 4.2 (bank room), 4.5 (stir); research `docs/research/dungeon-deep-dive/08-encounters-arenas-bosses-audio.md` section 6 and 7.6. Note the research table (7.6) is for an older 12-floor layout; this plan re-maps it to the signed-off 10 floors.

## 1. Principles (from the spec and research)
- One piece per gimmick half, one per set piece, a boss theme that is its own piece (not a remix of combat).
- **Stems, not more tracks**: the same piece sounds different as stems come in and drop out. Silence and ambient-only stretches are a feature, so music has weight when it returns.
- Music is **local per client**, driven by state the host already replicates (no new sync beyond one room-state byte, section 5). It never affects gameplay, so it carries no authority risk. Audio runs through the existing `AudioBus` (Music category, ducking, `ISettings` volumes); nothing here changes `AudioBus`'s rules.
- Original music only. No Dark Cloud tracks, names or motifs.

## 2. Pieces and where they play
Floors use the signed-off layout (spec section 5). Names are working labels **[PLACEHOLDER]**.

| Piece | Plays on | Mood | Built in |
|---|---|---|---|
| `water_explore` (the cold larders) | floors 1, 2, 4 (generated and authored water half, outside set pieces) | sparse plucked string, low pad, drips; small and warm-cold | Phase 1 dive (floors 1-7) |
| `bakery_set` | floor 3, the drowned bakery | dedicated piece; the oven's memory under the water; resolves warmly when the baker wakes | Phase 1 (stand-in set piece) |
| `dome_set` | floor 5, the cistern dome and the Brazen Ox | own piece, big and rhythmic; the Ox fight uses the set-piece's full-arrangement stems | Phase 1 (stand-in set piece) |
| `bank_room` | floor 5 bank room | near-silence: a warm held pad and the fire; no percussion; the push-on choice plays against it | Phase 1 |
| `heat_explore` (the coal cellars) | floors 6, 7 (and floor 8 outside its set piece's room) | different key from the water half; warmer, brass-and-bellows pulse; the bellows breathe | Phase 1 greybox gets the piece when it exists; before that the water piece plays |
| `feast_set` | floor 8, the feast hall | own piece; the braziers that never go out; the sunk feast | Floors 8-10 (roadmap row 5) |
| `roots_explore` | floor 9, the root wall | low drone, breathy woodwind; quiet, silence common | Floors 8-10 |
| `warden_boss` | floor 10, the Bell-Warden | its own piece, not a combat remix; the tolling is written into the tempo (the heroes fight between the rings); a closing tag on victory | Floors 8-10 |
| `town` | Hollowmere | outside this plan (town music is its own list) | n/a |

Two or three variants per exploration piece (the research playlist idea) **[OPEN]**: Phase 1 ships one per half; variants are the first thing to add if the user finds the dive samey.

## 3. Stem set
Each piece is authored as the same fixed stem layout, so one controller drives all of them. Stems are loop-aligned to a shared bar grid per piece.

| Stem | Role | Present in |
|---|---|---|
| `bed` | ambient bed: drips, creaks, hearth hum, wind. Always on while a floor is active. Drip density rises near water rooms (wayfinding). | every dive piece |
| `pad` | the exploration harmony: low pad and sparse plucked string (water), brass-and-bellows (heat), drone and breath (roots) | explore pieces, set pieces |
| `lead` | a melodic line; plays only sometimes (random combine or drop to bed-only), to keep exploration varied | explore pieces, set pieces |
| `drums` | percussion, rhythmic pulse | combat state, elite, boss |
| `bass` | low line that locks to the drums | combat state, elite, boss |
| `rise` | the "full arrangement" top layer used for elites, set-piece climaxes and the boss | set pieces, boss |
| `heart` | low heartbeat or drone that rises with stir (section 4); **not** piece-specific, one shared stem per half so it fits any piece | all pieces except `bank_room` |
| `tag` | one-shot stinger: the closing tag on a boss win, the "cleared" resolve | set pieces, boss |

Rough count **[PLACEHOLDER]**: 8 pieces x up to 6 looping stems, plus 2 stinger one-shots and 2 shared `heart` stems. Authoring size is for the audio pass to estimate, not this plan.

## 4. State to stem: where each switch happens
Inputs already exist in the dive code unless stated. The controller reads state; it never writes game state.

| State (source) | Stems on | Stems off | Timing |
|---|---|---|---|
| **Floor start** (floor entered; `DiveSession` floor + the floor's piece table) | `bed`, `pad` (+ `lead` per random roll) | all others | crossfade the piece over ~2 bars **[PLACEHOLDER]**; if a floor changes piece (e.g. floor 5 -> bank) use a short silence gap rather than a blend |
| **Explore** (no room hostile) | `bed`, `pad`, maybe `lead` | `drums`, `bass`, `rise` | `lead` rerolls each room, may stay off for a whole room |
| **Combat** (any enemy aware in the current room: the room-clear state `CellarRoomClear` goes live and the room has enemies) | adds `drums`, `bass` | none | on the next bar line |
| **Combat ends** (room clear) | back to explore stems; `tag` stinger optional on the clear of the last room on a floor | `drums`, `bass` | 2 bars after the last kill (research) **[PLACEHOLDER]** |
| **Elite** (a room has an elite; spec section 6 step 6) | all of combat plus `rise` | none | bar line after the elite is aggroed; `rise` drops with the elite's death |
| **Stir 40** (`DiveStir.ExtraEnemyThreshold`) | `heart` fades in, quiet | none | fade ~4 bars; latches for the half like stir does |
| **Stir 70** (`HunterThreshold`, the hunter appears) | `heart` louder; `pad` darkens (a filtered or detuned variant if the audio pass authors one **[OPEN]**) | none | on hunter spawn |
| **Stir 100** (`DeepClosesThreshold`, the collapse timer starts) | `heart` full; the closing theme replaces the explore piece for the rest of the floor **[OPEN: whether it is a new piece or just all stems + `heart`]** | `lead` | immediately on `BeginCollapse` |
| **Stir resets** (push on from the bank) | `heart` drops to off | `heart` | with the bank-to-floor-6 crossfade |
| **Set-piece room entered** (floors 3, 5, 8; the set-piece trigger at the room door) | the set piece's own piece starts: `bed`, `pad`, `lead` | the floor's explore piece | cue on entry, crossfade ~2 bars |
| **Set-piece climax** (the objective is live: floor 3 the water is routed, floor 5 the Ox fight begins, floor 8 the dampers are worked) | adds `drums`, `bass`, then `rise` at the encounter's peak | none | on the objective's start event |
| **Set-piece resolved** (baker wakes on 3; Ox cracked on 5; tally reached on 8) | `tag`, then `bed` + `pad` only | everything else | `tag` on the event; floor 3 resolves warm (the baker is safe from here, decision 26) |
| **Bank room** (floor 5, after the Ox) | `bank_room` piece: its pad and bed only | the dome's stems | silence gap, then the bank pad; stays through slot assignment and the push-on/go-home choice |
| **Push on** | nothing special; the next floor's explore piece fades in | `bank_room` | when the party steps on the floor-6 stair |
| **Boss floor** (floor 10, boss door open) | `warden_boss`: `bed`, `pad` until the boss aggros | explore piece | at the door |
| **Boss fight** | `drums`, `bass`, `rise`; tolling cues on the beat grid | none | at the boss's first attack |
| **Boss won** | `tag` closing stinger, then stems drop to `bed` + `pad`, then silence for the extra chest room / return | all combat stems | on the boss's death |
| **Boss retry** (paid retry, decision 25) | restart at the boss door state | | restart the piece from `bed` |
| **Wipe / failed dive** | everything fades out over ~1 s; `bed` only on the church/town side | all dive stems | on `DiveEnd.Failed` |
| **Downed (this player)** | `pad` low-passed, combat stems ducked **[OPEN]** | | optional, per-client; the party's state is unchanged |
| **Quiet floor** (a rare dark floor, if one is made) | `bed` only | everything else | fixed for the floor |

Notes:
- The bank room's silence is deliberate: the choice to push on is the dive's biggest decision (spec 4.2).
- Floors 6-7 are rough greybox in Phase 1 (decision 28). Until `heat_explore` exists they play the water half's piece plus `heart`; that is a stand-in, not a design.
- Floors 8-10 are not built yet (roadmap row 5); their rows are the contract for that plan.

## 5. Co-op: what is replicated
Music is local to each client. The controller needs only what each client can already see:
- floor number and phase (already replicated by `DiveHaulSync.Summary`);
- stir value (replicated for the HUD) -> thresholds computed locally against the same constants;
- **room state byte**: `explore / combat / elite / boss / cleared` per room. This is the only new item; the room-clear system is host-side, so the host replicates the byte for the room each player stands in (cheapest: one `NetworkVariable<byte>` per room, or per player "current room state"). **[OPEN: whether this byte already exists in `CellarRoomClear` replication; check before the wiring plan.]**
- set-piece and boss events as one-shot RPCs from the host (they already exist as objective events or will with the set pieces); restores and late joiners derive the current state, never replay stingers (`sc-netcode`: one-shots never replay).

Different players in different rooms hear different states; that is correct (one player in combat, another exploring). Authority for the byte: host-written, read by every client. No other music state is networked.

## 6. Wiring plan (for the later pass; do not build now)
Steps, each small enough for one subagent:
1. **Core `MusicStateModel`** (pure C#, tests in EditMode/`sc_core_tests`): takes floor, phase, room state, stir value, set-piece/boss events, `isDowned`; returns the target stem mask, the piece id, and the timing hints (next-bar, after-N-bars). Table-driven from section 4. Authority comment: none (local).
2. **Runtime `MusicController`** on the App root, beside `AudioBus`: owns one `AudioSource` per stem (looping), keeps them sample-aligned, applies fades, reads `MusicStateModel` each frame, follows `ISettings` for the Music category and ducking. No new bus rules.
3. **Data**: a `MusicCueTable` (JSON under `StreamingAssets`, like the tuning loader) mapping piece id to clip paths and bar lengths; the fade and bar timings from section 4 live there, not in code.
4. **Host: the room-state byte** (section 5) if it does not exist yet; `risk-reviewer` on this one step only.
5. **Playtest overlay**: show current piece, stem mask and state in the dev overlay so the user's playtest notes can name a stem.
6. **Tests**: EditMode for the model (every row of section 4); a PlayMode smoke that the controller never throws with all clips missing (AudioBus already warns once and stays silent for a missing clip); no test claims anything about how it sounds.

Sizes **[PLACEHOLDER]**: step 1 S, 2 M, 3 S, 4 S-M [net], 5 S, 6 S. About 2 sessions, after audio exists to hear.

## 7. What this plan does not decide
- The actual music (composition, instrumentation, tools): an audio pass, with its own brief and the user's taste. **[OPEN]**
- Whether to use middleware (FMOD, Wwise) or stay with Unity audio: the plan assumes plain Unity `AudioSource` stems through `AudioBus` as the cheap default; revisit only if bar-synced switching proves unreliable.
- Town and non-dive music; combat SFX (already handled by `AudioBus` and `SfxGenerator`).
- Dungeon-specific pieces for later dungeons (spec section 8): each gets its own list when designed.
