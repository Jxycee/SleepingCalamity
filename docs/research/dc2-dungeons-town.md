# Dark Cloud 2 research: dungeons, enemies, Georama, camera, side activities

Research notes for Sleeping Calamity (original action RPG, spiritual successor). Mechanics and numbers only, in our own words. No code, data tables or game text copied.

## Summary

1. DC2 floors are NOT one big random generator. A hand-authored floor graph (per dungeon, 9 to 39 floors, branching "passages") decides what each floor is. Most floors are then built at load from 4 to 6 small hand-made rooms, joined by corridors, on a 14x14 tile grid (cell 320 or 400 units). A few floors use whole fixed layouts.
2. Enemy and loot design is a fixed spawn list per floor (at most 24 enemies, no respawn found). The "first monster in the list carries the gate key" rule, a 50% locked-room-with-key-chest roll, 8 chests per floor with a rank-filtered item pick, and "kill everything to clear the floor" are the core loop.
3. Enemy stats come in 4-tier families (grade 0..3, life about 1x / 3x / 12x / 59x). Damage is a simple flat subtraction (attack minus defence), scaled by per-attack-type percentages and per-element percentages. Rage (enrage after N hits, +30% attack for 15 s) and weapon-wear-per-hit are the personality hooks. Experience goes to the weapon, not the character.
4. Georama is scored by hand-written per-town functions that set up to 26 boolean conditions; each town has 9 to 11 "request" entries, each needs all of 1 to 4 conditions and is worth 5 to 15 percent (always sums to 100). Parts are crafted from items, then placed from stock under a per-town polygon and draw-memory budget.
5. Camera is a spring-follow orbit camera (distance 100 to 160, height -15 to 40, 1/8 catch-up per frame, collision probes). Lock-on is nearest-enemy within 400 (hold within 300), cycles by distance priority, bosses never drop lock. OpenCloud (reimplementation) adds almost nothing: it is mostly stubs and has no DC1 code.

## Sources and coverage

| Source | What it gave | Limits |
|---|---|---|
| ChronicleTwo `ps2/src/*.cpp` (matching PAL decomp) | Everything below, including the full monster stat table (`monster.cpp`, `base_monster_define`, 343 rows) | Monster AI is bytecode in game data, not in source. Item, weapon and chest tables live in data files not all extracted here. |
| ChronicleTwo `port/gameplay/**/*.json` | Extracted data: floor graphs (`menu/dngmap`), room presets (`dungeon/cfg_file`), spawn lists (`mos_place*`), chest tables (`tbox_d0*`), Georama requests (`geo*.cfg.json`) | Some text fields are Japanese or localised; only numbers and structure used here. |
| OpenCloud `code/engine` | Confirms module split and script-call names only. `monster_func.cc` has 143 `todo` stubs; fishing, photo, camera are near-empty | No DC1 code at all (README says DC1 is "later down the road"). Nothing numeric beyond ChronicleTwo. |

DC1 is therefore not covered by either source. Everything here is DC2.

Source key used below: `file::function` inside `ChronicleTwo/ps2/src/` unless stated.

---

## 1. Dungeon floors

### 1.1 Two layers

1. Floor graph (the dungeon "map" screen): `dngfloor.cpp::CDngFloorManager::LoadDataTable`, `AnalyzeFile`, tags `GI`, `RT`, `RI`, `RI_LINK`, `RI_OP`, `RF_INFO`. Data: `port/gameplay/menu/dngmap/dmap{0..6}.cfg.json` (graph) and `dflr{0..6}.cfg.json` (per-floor properties).
2. Per-floor layout (what you walk through): `automap.cpp::CAutoMapGen::Build`, `RandomMapMainProc`, `CreatRoom`, `RoomLink`, `CreatDoorRoom`, `SetHealingPointIndex`, `SetInOutPartsIndex`. Driven from `dng_event.cpp::LoadDungeonMapFile` which loads a room-preset file (`dungeon/cfg_file/<name>.cfg`) and a generation-flag word.

The graph is a grid of cells that are either a floor ("room", carries floor id, an `order` number, 4 neighbour links, a tile-texture number) or a passage ("root", a connector with a type 0..3 and a shape). A floor's outgoing passages (to floors of higher `order`) become the exits placed inside that floor's generated layout: the exit mask is `1 << passage_type`, up to 4 exits. (`dngfloor.cpp::GetDngMapNextRoot`, `GetDngMapNextFloorID`.) This is how branching and "pick your next floor" works.

### 1.2 Floor graph sizes (from the extracted data)

| Dungeon idx | Floors | Passages | Tagged floors (start / sub / boss / exit) | Passage types used | Floors with geostone | Floors with fishing | Fast-clear time range (units below) |
|---|---|---|---|---|---|---|---|
| 0 | 9 | 12 | 1 / 1 / 1 / 1 | 0 only | 0 | 0 | 9000 to 10800 |
| 1 | 16 | 19 | 1 / 2 / 1 / 0 | 0, 1, 3 | 8 | 12 | 14400 to 18000 |
| 2 | 25 | 56 | 1 / 4 / 0 / 0 | 0 to 3 | 18 | 4 | 23400 to 29700 |
| 3 | 21 | 70 | 1 / 2 / 1 / 0 | 0, 1, 3 | 14 | 9 | 19200 to 25200 |
| 4 | 23 | 93 | 1 / 2 / 1 / 0 | 0, 1, 3 | 16 | 0 | 24600 to 29700 |
| 5 | 28 | 69 | 1 / 2 / 1 / 0 | 0, 1, 2 | 18 | 0 | 23400 to 29100 |
| 6 | 39 | 93 | 1 / 6 / 1 / 0 | 0, 1, 2 | 0 | 0 | 31200 to 48000 |

Notes: "sub" and "boss" options only shift the map-screen icon (default offset y = -26 for those two). Every floor has the Sphida (golf) challenge flag set in all dungeons except dungeon 3 (11 of 21). Seals: a floor can be sealed so that it only applies when a given character is in the party and a clear flag has not been set (`IsSealFloor`), a "come back with the right character" gate. A special floor id 100 ("Wonder Forest") has a hard-wired answer and exit mask 1.

### 1.3 Layout generator (`RandomMapMainProc`)

Constants:

| Item | Value |
|---|---|
| Grid | 14 x 14 cells normally; 20 x 16 when the "fixed start" flag is set |
| Cell size | 320 x 320 world units in most dungeons, 400 x 400 in dungeon files d05 and d06 (set by a `GRID_SIZE` command in the preset file) |
| Room presets per file | 20 (random-floor files), up to 25 in d04, up to 64 supported |
| Preset room size | 2x3 up to 6x4 cells typically (some 4x6), each cell is a (part number, attribute) pair; -1 = empty |
| Max rooms per floor | 8 (array size), actually built 4 to 6 |
| Seed | `srand(iRand(0xFFFF))`, i.e. a 16-bit seed, printed in debug builds. Not exposed to save data in the source. |

Algorithm in plain steps:

1. Pick a seed, clear the grid.
2. Place room 0: either preset 0 pinned at the grid origin ("fixed start" mode) or a random preset at a random cell (retry until it fits).
3. Place room 1 at random position/preset until it fits, then carve a corridor 0 to 1 (`RoomLink`).
4. Target room count `goal = 4 + iRand(3)` (4, 5 or 6). Repeat: try up to 128 random placements for the next room; on success link it to a random earlier room (never room 0 in fixed-start mode) and count it. Bail out after 128 failed rounds.
5. Add 1 to 3 extra random corridors between random room pairs (this creates loops).
6. Add 1 to 3 "dummy roots" (dead-end side paths, ids 50+), then 3 corridor stubs (`CreatTermParts`) at random road cells with an open neighbour.
7. Convert occupancy to concrete tile pieces by matching each cell's kind and 4-bit neighbour-link mask against a 277-entry piece table (`SetPartsIndex`). Pieces are named families: corridor ("way"), room, door/entrance, step, part, dead-end.
8. Optionally place way-in/way-out dead-end pieces (+4 offset), then exits per the floor graph mask (+8, +12, +16, +20 for passage types 0 to 3). Each exit picks a random dead-end cell.
9. Door room: 50% of floors get nothing; otherwise find rooms with exactly one entrance (an entrance with the stepped-door variant counts as 999 so is excluded) and turn a random one into a locked door room.
10. Healing point: 51% chance (`iRand(100) < 51`) to replace one random second-family room tile with the healing-point tile (unless the no-healing flag).
11. `IndexToPartsPlace` instantiates the map parts.

Room placement validity (`CreatRoom`): the room's rectangle plus a one-cell margin on all four sides must be empty. Preset pick: reject presets with `fixed > 0`; accept preset if `rate >= iRand(100)`; loop until accepted. Rate values in the data: 40, 50, 60, 80, 100 (random files). Corridors (`RoomLink`) head from the side of room A that faces room B (compare |dx| vs |dy| of room centres) through a room-edge cell that is not a part/healing tile, walking towards B. (The loop in `CreatRoom` indexes the preset array with -1 when the first test rejects; quirk of the original, harmless in the reimplementation if you just retry.)

Generation modes (flag word, `automap.hpp`): dummy tree surround (0x1), whole-preset floor (0x2), dummy mountain (0x4), fixed 20x16 start (0x8), no in/out (0x10), no healing (0x20), fixed whole floor from preset 0 (0x40). Whole-preset mode picks a preset by floor number: floor 5 and floor 11 use preset 0; floors below 19 otherwise pick 1 to 19 at random; floors 19 and up pick 0 to 9 (inferred meaning: these are the special "preset-floor" dungeon, likely the late optional dungeon). Which dungeon uses which flag is set by event script data, not in this source.

Fixed layouts: d03f01, d03f02, d03f03 and `_d03f03` use only `fixed = 1` presets with 12 to 30 cell sides (up to 30 x 20 cells, about 9600 x 6400 units). `mstage0..8` are single fixed rooms from 3x5 to 15x15 cells (inferred: boss or event arenas).

### 1.4 Monster placement

Source: `dng_event.cpp::LoadMonsterFile`, `AutoSetMonster`, `CreatMonsterFloorInfo`; data `mos_place{0..6}.cfg.json`.

| Item | Value |
|---|---|
| Spawn list | A fixed list per dungeon and floor: pairs (monster id, parameter). 1 to 24 entries seen (6 to 39 floors per dungeon). Hard cap 32 entries, 24 live at once |
| Entry parameter | Passed to the monster as `locate_param`. Stored per monster, meaning is script defined (inferred: variant/group/behaviour number). |
| Where | Random walkable flat cell (`SearchMapFlatPosition`), at least 520 units from the floor's entry point, and not overlapping chests or circles (`CheckObjectPutArea`, 40 radius). Retries forever until placed |
| Mimics | Monster ids in the mimic range are not spawned as monsters; they become chests (below) |
| Respawn | None found. Monsters are placed once at floor load. The floor clears when live monsters plus mimic chests reaches 0 (`dng_main.cpp`, event `DNG_EVENT_FLOOR_CLEAR`). (Gap: scripts could spawn extras via `CREATE_MONSTER`; not visible.) |
| Activation | A monster is only drawn and simulated inside a 800-unit radius (`clip_dist`), hysteresis +30; fades over 20 frames; at most the nearest N ranked by distance get priority (`CheckView(rank_limit)`) |

### 1.5 Chest and item placement

Source: `dng_event.cpp::AutoSetTreasureBox`, `PickupRandomItem`, `CreatTresuarBoxInfo`; data `tbox_d01..d08.cfg.json`.

Per floor: 8 chests, then mimics, then circles, then geostone/stones, then key chest.

| Chest slot | Contents |
|---|---|
| 0 | Fixed item: a "magic crystal" (flags 0x41) |
| 1 | Fixed item: a dungeon map (flags 0x41) |
| 2 to 7 | Random (rules below) |
| Min distance from entry | 320 units; 60 units from other chests; random yaw |

Random chest rules (slots 2 to 7):

- Roll r1 = d100: r1 <= 92 gives base kind 1 (93%); 93 to 96 gives kind 2 (4%); 97 to 99 gives kind 4 (3%). OR in a random one of three look/size flags (1/3 each).
- Roll r2 = d100: r2 <= 94 gives 0x40 (95%, plain), 95 or 96 gives 0x200 (2%, special variant, meaning unknown), 97 to 99 gives 0x80 "two items" (3%).
- Item pick uses a "rank" system: every item entry in the chest table has a rank 1 to 100 (low = junk, high = rare; observed ranks 5, 15, 20, 60, 70, 90, 100). Picking draws a group from the floor's group list, then a random item in the group, and accepts it only if its rank passes the filter; otherwise redraws.
  - Normal: wanted minimum rank = `(d100 + d100) / 2` (triangular, mean 50), accept rank >= that.
  - Kind 2 chest: accept rank <= 30 (cheap item).
  - Two-item chest: first item rank >= 60, second item rank <= 50.
- Quantity jitter: base quantity n; if 3 <= n < 10, add -1, 0 or +1; if n >= 10, quantity = 0.8n + rand(0.4n) + 0.5 (80% to 120%).
- Table shape (d01): `floor_start` number, a list of floors each naming 3 to 4 group ids, groups each with 3 to 11 items (item id, count 1 to 4, rank). A floor's candidate rank range is rescanned so filters are clamped inside min and max rank for that floor.

Other floor objects:

| Object | Rule |
|---|---|
| Mimic chests | One per mimic entry in the spawn list: placed away from entry (320) on free ground, chest flags `0x141` |
| Random circles ("lucky or unlucky circles") | Count: +1 at 24%, +1 at 19%, +1 at 9% (so 0 to 3). Kind 0: grants (weapon absorb bonus 10%, full heal and status cure, repair both weapons). Kind 1: hurts (halve HP, or weapon damage). Ridepod and monster form get nothing. Placement retries 100 times |
| Geostone | One floor object if the floor's flag is set and not yet found. Needed for the town (Georama) progression |
| Random stones | Up to 12 environmental pickups; random cells |
| Key-door key chest | If a locked door room exists: a one-item chest holding the key item, placed outside the door room |
| Healing point | A room tile (51% of floors). After use, `HEALING_POINT_COOLDOWN` = 1800 frames before it recharges (about 30 s at 60 fps) |

### 1.6 Floor keys / gate keys

Source: `dng_event.cpp::GetGateKeyIndex`, `GetKeyDoorIndex`, `AutoSetMonster`; `monster.cpp::ThinkHost` (death drop); `userdata.cpp::CheckItemDngKey`.

- Each of 7 dungeons has a gate key item id and a door key item id (gate: 337, 339, 341, 342, 344, 348, 350; door key: 338, 340, none, 343, 346, 349, 351). One dungeon switches to different items from floor 17.
- The monster in spawn slot 0 gets `gate_key` set. When its death fade completes it throws that item into the world. (Inferred: this is the "kill the key carrier" loop, which opens the floor exit; the exit gate event is script data.)
- All dungeon-key items are deleted from the bag on every floor load (`CheckItemDngKey`), so keys are strictly per floor.
- Back floors: not found as a mechanism. The nearest things are the "sub" floors on the graph (bonus branches), the Sphida (golf) challenge per floor, and the Wonder Forest special floor id 100.

### 1.7 Floor medals and challenges

Source: `dngfloor.cpp::IsClearMostFastDestroy`, `IsClearPractice`; `userdata.cpp::AddYarikomiMedal` (counter clamps 0 to 999).

| Challenge | Rule |
|---|---|
| Fast clear | `elapsed = (play_time - floor_start_time) * 6 / 5`. First time elapsed < par: +1 medal and flag. Later: improve the record only. Par per floor in the data: 9000 to 48000 (units look like 60 Hz frames; 9300 is about 155 s; inferred) |
| Practice (a restriction challenge) | 7 kinds: (0) clear before a timer reaches a parameter; (1) clear using thrown items only; (2) clear using only one chosen weapon/character action (parameter picks which of Max melee, Max gun, Monica melee, Monica magic); (3) clear using the ridepod only; (4) clear in monster form only; (5) leave the floor without healing; (6) never satisfiable. Which kinds apply depends on a difficulty/context table; clear gives +1 medal the first time, otherwise "again" without reward |
| Sphida (golf) | Optional on floors, prize table of 3 items (one per rank) per floor |
| Fishing | Optional floor flag with a record size per floor |

Medal is a persistent currency (cap 999), spent elsewhere (not traced).

---

## 2. Enemies

### 2.1 Stat table shape

Source: `gamedata.hpp::BASE_MONSTER_TBL` (0xB8 bytes), data in `monster.cpp::base_monster_define[344]` (343 real rows, ids up to 629).

Fields per monster kind: id, grade (0..3 within a family, -1 for unique), name, model file, script file (AI), drop-gift-type, sound bank, life, "can the player transform into it" id (0..10), weapon exp reward, money reward, weapon wear per hit (`whp`), rage hit count (`gekirin_num`), guard chance (%), dodge chance vs two attack modes (%), attack, defence, stagger threshold, boss flag, sword-trail count, element response[8] (%), damage-by-attack-type[12] (%), flag bits (no flinch 0x1, no knockback 0x4), lift flag (cannot be picked up), linked next kind, 3 drop item ids, immune-to-status bit mask, status chance (%), ratio-damage rate (%), area number, encyclopedia index, share-model flag.

Family structure: 64 families of 4 palette-swapped tiers (grade 0 to 3). Grade -1 and ids 500+ are one-offs (bosses and event enemies).

| Tier (grade) | Life (median / min / max) | Attack (median) | Defence (median) | Weapon exp (median / max) | Money (median / max) |
|---|---|---|---|---|---|
| 0 | 150 / 10 / 910 | 25 | 15 | 22 / 182 | 18 / 123 |
| 1 | 553 / 50 / 1900 | 59 | 49 | 109 / 200 | 88 / 147 |
| 2 | 1850 / 200 / 10000 | 122 | 76 | 200 / 300 | 112 / 160 |
| 3 | 8700 / 1000 / 20000 | 157 | 110 | 400 / 600 | 110 / 160 |

Within a family life scales about 1 : 2.9 : 11.8 : 58.8 (median), attack 1 : 2.3 : 4.8 : 6.2, defence 1 : 3.3 : 5.1 : 7.3. Typical boss: life 10000 to 20000 (one at 50000), attack 145 to 190, defence about 85 to 115, stagger threshold 3 to 4, 400 to 500 exp.

Other medians: guard chance 0% for 281 of 343 monsters (others 3 to 20%); dodge chance 0% for 289; rage count 5 to 10 (median 7; 0 for 41 monsters); status chance 100% for 306, a few at 5% (resistant); stagger threshold 0 to 5 (mean 2; 0 = flinch on every hit; two large outliers are used as immunity markers); weapon wear per hit 1 to 4 (higher on late tiers).

### 2.2 Damage maths (`monster.cpp::CMonsterMan::CheckDamage`)

Plain steps for a hit on a monster:

1. `base = hit_attack_power - monster.defence` (flat subtraction, can go negative).
2. Ratio attacks (some specials): `base = max_life * (move_percent/100) * (monster.ratio_rate/100)`.
3. `base *= attack_type_rate[kind] / 100` where `kind` maps Max sword, Max gun, laser, grenade, Monica sword, Monica magic, ridepod punch/sword/gun/other, monster form, thrown item to the 12 percentage slots.
4. For each of 8 element channels e: `extra += element_resp[e]/100 * base * element_power[e] / 127.5` (hit element power is 0 to 255). Add `extra` to `base`. In rain weather the first four channels are multiplied by 0.6, 1.2, 1.4, 0.8. (Element channel order inferred: fire, ice, thunder, wind, then four others.) Note the table value acts as additive weakness percent, not resistance.
5. Special multipliers: x1.2 for a couple of party-member vs monster-family pairs.
6. Multi-hit attacks divide damage by their hit count.
7. Range falloff for area hits: full damage within half the radius, linear fade to 0 at full radius.
8. Round; 0 or below counts as a "no damage" hit; invulnerability frames zero it out.
9. Status: each status chance = `monster.status_chance / hit_count` compared to d100 (monsters with the matching immunity bit never get it). Poison lasts 120 steps (periodic life loss), stop freezes for 300 frames, stone is a longer grey freeze, slow halves speed. A frozen monster loses 90 frames of freeze per further hit.
10. Guard: `guard_chance > d100` guards (not while stunned); can be forced by script or masked off.
11. Stagger: each non-guarded hit adds the move's stagger value; when total >= monster threshold, play flinch. Threshold 0 flinches always. Total decays after 60 frames.
12. Knockdown moves bypass stagger unless the monster has the no-knock flag. Stun moves (120 frames of dizzy) override and make guard impossible.
13. Rage ("gekirin"): each damaging hit takes 1/hit_count from the rage counter. At 0, rage lasts 900 frames: non-boss attack becomes 1.3x; then counter resets. Bosses have no rage counter (set to -1).
14. Drain/steal weapon properties: drain heals 2% of damage with chance `25/hit_count`% (min 1); steal pulls a drop item with chance `12/hit_count`% (min 1) judged against `(d100+d100)/2`.

### 2.3 AI states

Monster AI is a bytecode script per monster (`dungeon/monster/<name>.stb`), not in source. What the engine fixes are program entry codes: init 100, main 200, status effect 400, damage react 500, knockdown 600, guard 700, dodge attack-mode-0 800, dodge attack-mode-1 900, dead 1000, grabbed 1100, landed after throw 1200, dizzy 1400.

Engine-side decisions made every frame (`ThinkHost`): ranking by distance to the player, visibility, death fade (alpha -3 of 128 per frame, about 43 frames), dodge: if the target player is in a "murderous" attack mode with timer > 0 and this monster is the target, roll d100 against dodge chance 0 or 1 and request the dodge program. Target distance feeds lock-on. Monsters can be thrown, lifted, photographed and turned into pets.

Gap: chase / flee / ranged behaviours are all in the .stb scripts. We only know the hook points.

### 2.4 Drops and rewards (`runscript_opcodes.cpp`, dead-handler functions)

| Reward | Rule |
|---|---|
| Money | `total = reward_money * (1 + 0.3 if party member 17 + 0.3 if weapon wealth bonus - 0.3 if weapon less-money malus)`. 70% of total as one large coin; the rest split into 5 small coins (the remainder from the large coin is added to keep integer maths) |
| Weapon exp | `exp` from table (x1.2 if the killing weapon has the absorb-up property). Spawned as 6/8/10/12/16 collectible orbs for exp <6 / >=6 / >=50 / >=200 / >=500, each carrying `exp/orb_count`. Orbs credit the weapon that made the kill (character index + weapon source stored on the orb). There is no character level in this game; progression is weapons and parts |
| Item drop 1 | `roll = d100`; drops if `roll % 6 == 0` (17% of kills). Slot 1 instead of slot 0 if `roll < 20` and slot 1 exists (4 of the 17 values), so about 13% slot 0 and 4% slot 1 |
| Item drop 2 | slot 2 with 25% (`d100 % 4 == 0`) or always for one party member |
| Badge drop | Special: the kill must have used holy water (for a small set of ids) or a badge-drop weapon property; drops the "transform badge" |
| Gift pack | A throw-in item combo (3 matching thrown items) can pacify certain monster types (table of 16 type -> item -> count) |

Drop slots per monster: 225 have slot 0, 210 slot 1, 186 slot 2 set.

### 2.5 Spawn and respawn

See 1.4. Fixed list, placed at floor load, no timed respawn. Total live cap 24. Re-entering a floor reloads the list.

Gaps for REA: AI scripts, group aggro rules, whether monsters wake by proximity, any scripted reinforcements, the spawn-parameter semantics.

---

## 3. Town rebuilding (Georama)

Source: `editanalyze.cpp::AnalyzeSharlot / AnalyzeStera / AnalyzeBenietio / AnalyzeHeim / AnalyzeMoonFlower`; `editdata.cpp::CEditData::Analize / Analyze / GetAnalyzePercent / GetAnalyzeFlag`, `CEditMap::CultureAnalyze*`; `editparts.hpp::CEditPartsInfo`; `editmode.cpp` placement loop; `editmenu.cpp` crafting menu. Data: `geo{0..6}.cfg.json` (5 town definitions inside each file).

### 3.1 Scoring model

There are no per-resident personality requirements and no generic scoring function. Instead:

- Each of 5 towns has a hand-coded analysis function that runs when the player ends an edit session (and at load). It fills `condition[0..63]` (booleans) and an optional `target[]` array that chains a condition to another request entry.
- A data file defines, per town, up to 16 "requests" (entries). Each request has a message, a percent value, and up to 8 condition numbers. A request is satisfied only if all its conditions are true (a condition can itself point to another request, so requests nest; recursion limit 64).
- Town completion % = sum of percent values of satisfied requests. Every town's requests sum to exactly 100.
- The result is read by the town UI and by scripts (`event_func.cpp`: returns `GetAnalyzePercent` to scripts), which drive story gates.

| Town (code name) | Conditions defined | Requests | Request percent values | Poly budget | Draw-memory budget |
|---|---|---|---|---|---|
| Sharlot (geo 0) | 18 | 9 | 10 x7, 15 x2 | 4000 | 48000 |
| Stera (geo 1) | 20 | 10 | 10 each | 6000 | 50000 |
| Benietio (geo 2) | 22 | 9 | 15 x2, 10 x7 | 6000 | 54000 |
| Heim (geo 3) | 26 | 11 | 5, 5, then 10 x9 | 6000 | 54000 |
| Moon Flower (geo 4) | 12 | 10 | 10 each | 4000 | 48000 |

### 3.2 Condition kinds actually used (the "vocabulary")

All are checks over placed-part counts, positions, colours and residents:

| Kind | Examples with numbers |
|---|---|
| Count of a part type | river cells >= 15 (counted in a radius of 350 around trees with a cap of 6 per tree, summing to 15 or more); trees >= 10, >= 15; torches >= 4; pier pieces >= 8; fence cells >= 15 inside a house's territory; pots >= 10; windmill >= 1; specific stone-lamp parts >= 8 |
| Geometry | three named trees in a line: tree B between A and C, within 800 units of A and within 100 units of the line; four plateau heights equal (`GroundBalance`); houses at height >= 125 (3 blocks) or >= 167 (4 blocks) with a resident |
| Adjacency and containment | a part inside a house's "territory" or attached as a "child" part (chimney, crane, arm, fence, boat near an iron house within a radius) |
| Colour | a house painted one of 8 palette colours, matched within a colour-distance of 2 to the palette (distinct from its default colour); some requests demand a specific colour or a pair of colours (red, blue, purple, green houses) |
| Residents | a given resident lives in any house, in a house of a given colour/part, or "N residents total >= 2/3"; `CheckLiveNPC(character, house)` |
| Story or world flags | bit flags set by dungeon events (boss killed, quest done, monster destroyed) |
| Culture points | thresholds 20, 30, 40, 50, 60, 70, 80, 100 (varies by town) |
| Future-state flags | result of other requests ("something restored in the future") - the `target[]` chaining |

Culture points (`CultureAnalyzeParts`): each part definition carries (kind, value). Kind 6 "alone" counts its own value; kind 1 "main" counts its value plus values of all kind-2 "territory" parts in its footprint and all kind 3 / 4 "child" parts attached to it; others contribute 0 on their own. Town culture = sum over all placed parts.

Completing a request: effect is purely informational/gating here (percent plus unlocked story events and "future" map changes). No gold or item is awarded in this source; rewards are in event scripts (gap).

### 3.3 Parts, cost and budget

Part definition (`CEditPartsInfo`): attribute bits (no-remove, quarter-turn rotation, river piece, line/fence piece, wall mount, burnable, no-refund, any-height, no-stock-destroy), culture (kind, value), weight on ground, `max_num` placeable, geostone that unlocks it, number of paintable colours, polygon cost triple, up to 4 material (item, count) pairs, placement tolerance (default: at least 98% of the base must rest on ground), collision shapes.

Rules:

- Crafting: pick a part and a quantity; consumes `material.count x quantity` of up to 4 material items; adds to stock (`AddBuildPartsNum`). Items must be owned; menu shows shortfall.
- Placing: from stock, one at a time, rotate in steps (quarter turns for some), magnet snap toggle, undo with Cancel. Removing returns the part to stock unless flagged no-refund.
- Hard budgets while placing: `total_polygons + part.polyn[0] <= town max_polygons` and `draw_mem + part.polyn[2] <= town max_draw_mem` (table above). Hitting the budget ends placement mode.
- `max_num` caps how many of that part exist.
- Geostones (found in dungeon floors, 1.5) unlock part families.

---

## 4. Camera and lock-on

### 4.1 Free (orbit) camera: `cameracontrol.cpp::CCameraControl`, `mg_camera.cpp`

| Parameter | Value |
|---|---|
| Min / max horizontal distance to target | 100 / 160 |
| Height above target (current) | clamped -15 to +40 |
| Height rest range | eased back into -15 to 20 when the stick is idle: 1/20 of the gap per frame |
| Height offset added by distance | linear from 18 (at min distance) to 10 (at max distance) |
| Ground clearance | 25 above floor; camera stays 20 below ceiling |
| Position catch-up speed | 8: each frame pos moves `(target - pos)/8`; snaps when error < 0.1. Look-at point uses the same speed |
| Orbit input | right stick X: `turn = 0.05 rad per frame * stick`; L1/R1 hold: +/-0.05 rad per frame; right stick Y: `2 * stick` height units per frame; optional reverse |
| Rotation compensation | `turn *= min_dist / actual_dist` so closer cameras rotate slower in world arc |
| Swing-behind | L2 or Cancel button sets a target yaw = player yaw + pi; eases with an angle interpolation until within 0.1 rad |
| Wall avoidance | `CheckCollision` pushes camera off walls. `AutoMove`: if a sphere (radius 4) ray from target to camera hits geometry, rotate the camera around the target in alternating +/- steps of 0.01636 rad (about 0.94 degrees), up to 32 steps each way (+/- 30 degrees), take the first clear spot. Skipped if the player is actively orbiting |
| Eye (first person) mode | Exists: follow switched off, view yaw/pitch driven directly (`dng_main.cpp::InitEyeCamera`, `EyeCamera`); the exact trigger is script/gun related (inferred) |
| Script hooks | Camera control parameters (distance min/max, heights) can be overridden by event scripts (`SET_CAMERA_CTRL_PARAM1/2`, `RESET_CAMERA_CTRL_PARAM`); -99999.9 means "leave unchanged" |

Player movement speed: 3.0 units per frame free, 1.8 while locked on (`HumanMoveIF`).

### 4.2 Lock-on (`actionchara.cpp::DistCheck_Action2`, `Check_LockOn`, `RockOn_TargetSel`, `StepLockOn`)

Two modes (a per-floor setting): NEAREST (default) and SELECT.

NEAREST:
- Not locked: every frame the "candidate" target is the live, visible, not-held, lock-on-allowed monster with the smallest distance, within 400 units. A boss anywhere on the floor always qualifies and overrides distance ties.
- Pressing the lock button with a candidate turns lock-on on (lock sound).
- Locked: stays valid while the target is alive and within 300 units (hysteresis; bosses are valid at any distance). If it becomes invalid, the engine retargets to the nearest within 400 and stays locked; if none, lock-on drops.
- Lock button pressed again while locked: if the target is behind the player (dot product of facing and target direction < -0.2) drop lock; otherwise advance to the next monster in distance-priority order (rank + 1), wrapping.

SELECT:
- Cycle through monster slots in index order, skipping dead, held or no-lock monsters.

Locked-on movement: move speed drops from 3.0 to 1.8 units per frame, and many action states branch on the lock flag (aim and facing; inferred). Bosses use a screen-bottom life gauge.

---

## 5. Side activities

### 5.1 Fishing (`fishing.cpp`)

Core loop: pick a cast point in a fishing zone, cast with power, wait for a bite, hook, reel battle, land or lose.

Bite selection (`GetUkiWaitTime`):
- Each zone has up to 8 fish entries with a base weight (`rate`) and a wait bias. Per fish: bait affinity (4 levels: none = x0, low = x0.5, normal = x1, high = x1.5) and time-of-day affinity (4 bands, same multipliers). Weighted random pick among the fish present; a "nothing bites" entry is reduced by `1 - 0.5 * rod_stat4_rate`.
- Wait time (frames): base 240, extra 240 (halved for the lure rod); with bias b clamped to [-2, 2]: if b >= 0 wait = 240/(1+b) else 240*(1-b); then add `rand * extra`, extra scaled likewise.
- After the bite, nibble delay by bait affinity: none = abort; low = 100 to 160; normal = 50 to 100; high = 30 to 60 frames; pull window 30 frames.

Fish generation:
- Size is random between per-fish min and max (scaled by cast distance), with a floor of half the minimum. Length scale = size / base size * 1.05, capped at 4. Width factor 1.0 to 1.3 (random, capped). Weight = width * size * weight_rate. Pull strength = pull_rate * size / 80 * width, then divided by `1 + 2 * (max(rod_power - 10, 0) / 90)`. Score value `fishing_point = width * point_rate * size` (x2 with a lure).

Reel battle (`LineTensionStep`, per frame):
- Fish energy `vigour` in [-1, 1]; pull = pull_strength * (1 + 0.5 * vigour).
- Tension change per frame: rests at -0.004; the player's input activity count raises it: from 5 to 20 activity it ramps to +0.006; above 20 it jumps to 0.2 (scaled by pull). Rod hold-up state adds `+0.03 * pull` and drains vigour 0.02; rod lowered state subtracts 0.008. Reeling (positive) multiplies tension by 0.9. Reel-out (negative) doubles pull and reduces vigour by `0.05 * 3 * rod_stat1/100`. Wind adds `0.03 * pull`. Tension clamped to [min_tension, 1.0]; min_tension creeps up 0.0001 per frame (a soft time limit).
- Win when fish distance < minimum length; lose when tension reaches 1.0 (after at least 100 battle frames) or the line is cut.
- Stick > 0.8 deflection decides left/right/up rod direction; the player counters the fish's swim direction.

There is also a fish-breeding/racing layer (`gyorace.cpp`, `userdata.cpp` fish stats: five stats battle, stamina, boost, endurance, tenacity; aquarium) not read in depth. Breeding: new fish get +0..3 or +0..2 to those stats on offspring.

### 5.2 Photography and invention (`photo.cpp`, `inventmn.cpp`)

- Photograph: the player takes a screenshot-style picture; the engine records the nearest monster/object within 300 units (any distance for bosses) whose bounding box overlaps the frame (screen test +/-50 pixels), plus subject id. Distance to the subject is read from the depth buffer in a small patch (8x8 pixels) at screen centre.
- "Ideas" (neta): each unique subject photographed adds an idea. Experience: 2 per ordinary idea, 5 per "scoop" (rare event subject id >= 1000, up to 9999). Photographer level = total_exp / 100, displayed +1. Up to 512 recorded subjects. Shutter count is a stored currency capped at 99999.
- Invention: a recipe table maps three idea ids to one item and a list of material (item, count) pairs. Combining three ideas that exactly match a recipe yields that item (a partial match of two flags a "close" hint). Crafting then consumes materials and builds items; the same recipe pattern is used for Georama parts (3.3) and weapons.

### 5.3 Sphida (golf) mini-game (`sphida.cpp`)

- Placed on a floor: pin at least 60 units from the player and chests/circles; ball at least 200 units from the pin.
- Power gauge: a cursor charges 0 to 40 ticks (power = ticks/40) and, if the player has not committed, runs back down (fails at 0 with "no power"). After the power is set it runs the cursor back towards an impact point; stopping within 1 tick = best, error bands scale with a "safe level" (default 2): inner band 0.5 x safe, outer 1.5 x safe; past -7 ticks = late miss.
- 6 clubs numbered 9 to 14, each with three numbers (38 to 50, 0.2 to 4.0, and a 1 to 6 value; meanings not traced, probably reach, loft and a tier). Rewards: three prizes per floor by rank/par.

---

## Design takeaways for Sleeping Calamity

Worth adapting:

1. Two-layer floors: a hand-authored dungeon graph (branches, sub floors, sealed floors that need a specific character class, special floors) plus small hand-made room pieces assembled by a simple placer with a seed. Cheap to author, endless variation. Our version should expose and save the seed so co-op players get identical floors and a run can be replayed.
2. Room-piece assembly rules worth copying: preset room with a rate weight; rectangle plus margin overlap test; connect each new room to a random earlier one; add 1 to 3 loops; side dead ends; matching a tile piece by 4-bit neighbour mask. This runs in milliseconds and has no runtime failure other than retry caps.
3. Per-floor "kill the key carrier" and "locked room with the key in a chest elsewhere" produce a tiny puzzle per floor with no authored content. The 50% chance for the lock room keeps it from becoming predictable.
4. Medal-style challenges (fast clear par per floor, restriction runs, no-heal) give replay value for the cost of one counter each. The restriction list maps well onto our weapon and ability set.
5. Rank-filtered loot (items have a value rank; the pick demands rank >= a triangular roll) is a neat way to bias without weighted tables, and per-floor group lists give regional loot identity. Quantity jitter of +/-20% hides counts.
6. Weapon-centred progression (experience orbs credit the weapon that scored the kill, durability wear per monster hit) ties the economy of the whole game to the combat loop; also makes the tier families (same model, 4 tiers, life x59) very cheap to produce.
7. Georama-like town goals: concrete, checkable, small-number conditions ("N trees near a river", "a house on a plateau with a resident", "a colour match") plus a percent-weighted list per town. Easy to build as data (condition ids plus request list) with code-defined checks.
8. Camera: orbit with distance-based height, spring catch-up, and rotate-around-obstacle probing is simple and robust. Lock-on with hysteresis (acquire 400, hold 300, bosses unconditional) feels fine.

Change or avoid:

- Co-op and difficulty scaling are not in the source. Scale per player count by multiplying life only (tier ratios give a natural ladder: 2 players about 1.7x, 3 about 2.3x, 4 about 2.8x - our tuning, not DC2), keep attack unchanged but add a defence floor so damage never reaches 0. Spawn count: raise the 24-live cap and the list length (not the unit stats) to avoid kill-time bloat.
- The flat `attack - defence` subtraction makes damage collapse or explode between tiers. Prefer a ratio form (`attack * 100/(100+defence)`) so tier gaps stay readable and small hits never round to zero.
- Fixed monster lists with no respawn plus kill-all clear is fine for a small group; for 4 players add an optional reinforcement wave on a timer instead of larger lists.
- Shared lock-on: each player needs their own target. The DC2 tie to a single `target_no` and one camera will not work. Provide per-player cameras or a shared encounter camera mode.
- The door-room lock uses a single global slot; with co-op make the key an item that any player can carry and allow a teammate to open the door.
- Hidden 16-bit seed only: record 32-bit plus floor id.
- The town is analysed with 5 giant hand-written functions. Replace with a data-driven condition language (count, adjacency, height, colour, resident) so designers can author them.
- Material-driven Georama crafting plus polygon budgets: keep the budget idea (it is a useful design constraint) but express it as "points" instead of polygons.
- Weapon exp pickups need a magnet in co-op so the last-hit player is not the only recipient; consider splitting by contribution.

---

## Gaps / REA questions (game binary and data not in either source)

1. Monster AI (.stb scripts): states, aggro radius, chase and flee conditions, group behaviour, scripted reinforcements, what `locate_param` means per monster.
2. Which dungeon uses which generation flag (event scripts), and the exact fixed layouts for floors that are not random; how the preset-floor number mapping relates to real dungeons.
3. Chest flag meanings (0x2, 0x4, 0x8/0x10/0x20, 0x200) and their visuals; the full item rank tables for all 8 chest files (only the d01 shape was inspected).
4. Gate key usage: which event consumes it and exactly what the gate does; whether the key carrier is always spawn slot 0 of a floor.
5. Floor time unit for the fast-clear par (assumed 60 Hz frames), and the medal spend list.
6. Georama: the rewards for completing requests (event scripts), the full part tables (cost, `max_num`, polygon costs, culture values), how towns unlock parts via geostones.
7. Element channels 4 to 7 identities; which status mask bits mean what; `unk_*` table fields.
8. Fishing: per-fish parameter table (min and max size, pull rate, point rate, affinity matrices), rod and bait stat tables, record sizes; fish racing and breeding rules (`gyorace.cpp` not read).
9. Photography: how subjects become ideas (`neta`) in detail and the recipe table values; scoops list; anything about film limits.
10. Dungeon 3 and others: how many monsters per floor is configured in practice versus the list cap; whether any floor spawns monsters from chests beyond mimics.
11. DC1: neither source covers it; any DC1 reference would have to come from RE of its binary or other sources.
12. Camera: values for special camera modes (boss cams, cutscene cams) and event-script overrides; any aim-assist in gun mode.
13. Co-op: none of this exists in DC2; all multiplayer design is ours.
