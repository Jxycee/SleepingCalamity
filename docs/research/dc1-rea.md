# Dark Cloud 1 (USA) - reverse-engineering notes for Sleeping Calamity

Research from the user's own copy of Dark Cloud 1 (SCUS-971.11, NTSC-U), approved for REA research: mechanics and numbers only. Everything below is in our own words. No decompiled code, game text or assets are reproduced; only the numbers needed. "(inferred)" marks conclusions not read directly from code. Function names are given demangled (`Class::Method`). Item and weapon references use numeric IDs, not in-game names.

## Summary
1. Weapons grow on a per-weapon ABS gauge earned from kills: next-level cost = base + level x increment (per weapon, base 5-125, increment 5-50, cap 999). Level-up is a manual menu action that adds +1 ATK/END/SPD/MAG, +1..3 max WHP and permanently absorbs whatever is attached (slot contents are consumed); starting weapons earn no ABS.
2. Durability is harsh: each hit removes (1.5 - 0.01 x END) x scale + 0.1 x extra from WHP; at 0 a normal weapon is deleted permanently, and even the starting weapon is reset to a level-0 "broken" form (all growth lost). Only an auto-repair item in the active slot saves it.
3. Damage = attack - monster defence (minimum 1), then one selected element (+0.4% per element point, +0.5% per MAG point, scaled by the monster's element %), anti-monster +1.5% per point, a per-character per-body-part %, half damage on frozen targets, rounded up. No crit roll in that path; a 1% instant-kill special exists.
4. Thirst drains 0.003 per frame x (1 + 0.2 x dungeon index) from a starting max of 30; at 0 you lose 1 HP every 120 frames. Springs refill fully.
5. Floors are procedural (16x16 room area, 2..N rooms of 3-4 cells joined to the nearest room); Atla are pre-assigned to random floors in the first or second half of each dungeon (max 8 per floor); one random eligible monster carries the gate key. Georama is scored per town by a hand-written rule function: 8-13 equally weighted conditions (area, distance, facing, river flood-fill, counts), each only counted once its building is complete.

---

## Setup (reusable)

| Item | Value |
|---|---|
| Target | `C:\Dev\_private\ps2\dc1\SCUS_971.11` (PS2 EE ELF, MIPS R5900, `.symtab` with ~4,180 GCC 2.x mangled C++ functions) |
| REA | Did **not** work: `open_binary` succeeds but every analysis call fails with "Windows Ghidra P0 accepts PE targets only" (Hopper is macOS-only, IDA not configured). |
| Ghidra headless | **Worked.** Ghidra 12.1.4 + ghidra-emotionengine-reloaded, language `r5900:LE:32:default`, JAVA_HOME `C:\Tools\jdk-21.0.12.1+1`. Import + auto-analysis about 15 minutes; full decompile export a further ~15 minutes. |
| Project | `C:\Dev\_private\ps2\dc1\ghidra\dc1` (project folder `C:\Dev\_private\ps2\dc1\ghidra`, project name `dc1`) |
| Import command used | `analyzeHeadless.bat C:\Dev\_private\ps2\dc1\ghidra dc1 -import C:\Dev\_private\ps2\dc1\SCUS_971.11 -processor r5900:LE:32:default -scriptPath C:\Dev\_private\ps2\dc1\scripts -postScript DecompAll.java <out.c> [nameRegex]` |
| Re-run without re-analysing | `analyzeHeadless.bat C:\Dev\_private\ps2\dc1\ghidra dc1 -process SCUS_971.11 -noanalysis -scriptPath C:\Dev\_private\ps2\dc1\scripts -postScript DecompAll.java C:/Dev/_private/ps2/dc1/decomp/subset.c "Regex"` |
| Outputs | `C:\Dev\_private\ps2\dc1\decomp\all.c` (decompiled C of all 4,254 functions, address order, `//==== name @ addr` headers), `decomp\syms.txt` (raw symtab dump) |
| Helper scripts | `C:\Dev\_private\ps2\dc1\scripts\`: `DecompAll.java` (Ghidra post-script: decompile all or regex-matched functions to one file), `fn.sh <regex>` (print matching functions from all.c), `syms.py` (symtab dump), `rd.py <hexaddr> [n] [fmt]` (read floats/ints from the ELF at a virtual address), `wl.py` (raw weapon-table rows, scratch only) |
| Decompile failures | 118 functions; nearly all are in the `title`/`dun` overlays (see Gaps) plus opening-cutscene code. |

Memory map: `main` segment 0x100000 (file offset 0x100). Two overlay segments, `title` and `dun`, both at 0x1dabd00 with **zero bytes in the ELF**; they load at runtime from `TITLE.BIN` / `DUN.BIN` inside `DATA.DAT` (string `\DATA.DAT;1` in the ELF). DATA.DAT is not in the dump folder (only DATA.HED/HD2), and per the brief it was not extracted. Several data tables (weapon list, Atla tables, enemy-layout tables) are initialised data inside `main`, so their numbers are readable directly.

---

## 1. Weapon growth

### Data layout (what the code reads)
Weapon base table `WeaponList` (in the ELF, 120 rows x 76 bytes; 118 real weapons). Per row: base max WHP, ATK, END, SPD, MAG; owner character; 6 attach-slot flags; 5 element values; 10 anti-monster values; base ABS cost; ABS cost increment per level; option bits; two build-up target bitmasks; ATK cap; MAG cap.

| Field | Min | Median | Max |
|---|---|---|---|
| Base max WHP | 5 | 55 | 99 |
| Base ATK | 1 | 42 | 212 |
| ATK cap (per weapon) | 5 | 78 | 350 |
| MAG cap (per weapon) | 5 | 46.5 | 300 |
| ABS cost at level 0 | 5 | 60 | 125 |
| ABS cost increase per level | 5 | 15.5 | 50 |
| Attach slots | 0 | 3 | 5 |

Slot flag pattern: slot 1 is a type-2 slot (never rolls "lucky", see below), later slots type 1. Most common layouts: 2 slots (29), 3 slots (67), 4 slots (12), 5 slots (4); 6 weapons have none.

Character starting ("default") weapons: per-character IDs {257, 299, 314, 331, 347, 363} are the *broken* base forms; the usable starting weapon is ID+1 (inferred from repair and break code; table at `@612` used in `BattleSubWeaponDmg`, and `GetDefaultWeaponNo`). Characters indexed 0-5 (order and identities inferred: 0 the hero, 1 and 3 and 5 ranged or magic users).

### ABS (weapon experience)
`CMonstorUnit::Step` (kill handling), `GetWeaponMaxExp`, `CDngStatusData::CheckDefaultWeapon`.

| Rule | Value |
|---|---|
| Who earns | Only the character who landed the kill, on their equipped weapon. **Starting (default) weapons earn nothing.** One story weapon (ID 0x10C) earns nothing until a story flag is set. |
| Amount | The monster's per-species ABS value (monster data, loaded by the overlay). |
| x2 | When a per-floor/unit mode flag is set (inferred: back floor; same flag disables gate-key carriers). |
| x1.2 | Weapon option bit 0x2000 (an "ABS up" style option). |
| Negative | While the player has a particular status (code 10), the kill *subtracts* ABS instead (floor 0). |
| Cap | ABS stops at the level cost; a "can level up" message fires. |
| Level cost | cost(L) = baseCost + L x increment, capped at 999 (if <= 0, 99). |

### Level-up (manual, from the weapon menu)
`CWeaponLevelUp::SetLevelUpValue`, `CWeaponLevelUp::SetLevelUpWeaponData`, `WeaponSelectKey` (menu), `WeaponMenuSelect` (commit), `ItemUseFunc` (item 0xB2).

| Step | Rule |
|---|---|
| Trigger | Player chooses level-up when ABS >= cost, or uses item 0xB2 (instant level-up powder) while ABS < cost (item consumed). Item 0xB2 refuses the default weapon and level >= 99. |
| Attachment absorb | Every attached item (ID > 0x50) in an open slot has its stats added to the weapon permanently: ATK and MAG capped at the weapon's caps, END/SPD capped at 99, each element and anti-monster value capped at 99. A slot flagged "lucky" adds x2. Then **all slots are emptied** (attachments are consumed). |
| Flat growth | +1 ATK (cap), +1 END (cap 99), +1 SPD (cap 99), +1 MAG (cap), max WHP +1..3 (uniform, cap 99). ABS resets to 0, level +1 (cap 99). |
| Lucky slot | After each level-up, each open non-type-2 slot rolls 1 in 50 (`rand % 50 == 5`); at most one slot becomes "lucky" (x2 for the next absorb). |
| Synthsphere bonus | Absorbed synthspheres (attachment ID 0x5A) also OR their stored option bits into the weapon and add to a per-weapon "spheres absorbed" counter. Opposite option pairs cancel (`CheckWeaponOptionStatus`: 0x2/0x4, 0x8/0x10, 0x100/0x200, 0x400/0x800). |

### Build-up (turning into another weapon)
`EnableBuildUpModel`, `CompareBuildUpModelData2`, `BuildMenuCompVolume` (short and char versions), `WeaponStatusBuildUp`, `SetWeaponBuildValue`, `CWeaponLevelUp::Step` (case 8), `IsNotBuildUpWeapon`.

| Rule | Value |
|---|---|
| Candidates | Each weapon has two bitmasks naming which of its owner's weapon list entries it can become (list index = default weapon ID + bit index). Weapons <= ID 0x100 and weapons with empty masks cannot build up. |
| Check | Uses the weapon's *current effective* stats (base + levels + current attachments). For each of 5 elements and 10 anti-monster stats: current >= target's base value. A negative target value means "current <= abs(value)" (an upper limit). ATK and MAG go through the same comparison (inferred: the decompiler hides the stack slots). END, SPD, WHP and level are **not** checked. |
| Extra | The menu only allows the build-up when **no attachments** are in the slots. |
| Result | Weapon ID becomes the target; level and ABS reset to 0; **ATK, MAG, elements and anti-monster values are kept**; END, SPD and max WHP are raised to the target's base if lower; the target's fixed option bits are added. |
| Special line | IDs 0x116/0x117 (a two-step sword line) build up by a fishing counter (number of a particular fish caught) instead of stats; the counter clears on use. |

### Spectrumize (weapon to synthsphere) and attachments
`WeaponStatusBreakEnable`, `CWeaponLevelUp::SetStatusBreak`, `CWeaponLevelUp::Step` (case 5), `createAttachVolume`, `GetAddAttachItem`, `AttachList` (50 attachment rows in the ELF).

| Rule | Value |
|---|---|
| Allowed | Weapon level >= 5. |
| Sphere value | 0.6 x (ATK, END, SPD, MAG, each element, each anti-monster), from the weapon's stats plus its current attachments (lucky slots x2), truncated. Carries the weapon's option bits (cancelling pairs removed). Stored as attachment ID 0x5A. |
| Weapon afterwards | A non-default weapon is deleted. A default weapon resets to its base stats at level 0. |
| Attachments | Generic attachment rows (ID > 0x50) hold 4 stat shorts, 5 element bytes, 10 anti-monster bytes. Items 0x5B-0x5E roll a grade on creation: sum of two 0..49 rolls; < 16 -> grade 3 (5.4%), < 31 -> grade 2 (14.4%), else grade 1 (80.2%) (what the grade multiplies is in the overlay/data, inferred: stat amount). |
| New weapon options | A newly created non-default weapon rolls 1% for option bit 0x200 and 1% for option bit 0x100 (`WepDataListToHaveCopy`). |
| Active element | A weapon uses **one** element at a time: on creation the highest element is selected; the player can switch to any element with value > 0, or to "none" if the weapon allows it (`WeaponMenuCheckEnableSetElem`). |
| Low-WHP weapon | Weapon ID 0x110 gets x1.5 on all displayed/used stats while current WHP <= 20% of max (`GetNowWeaponRate`). |

---

## 2. Weapon durability (WHP)

`BattleSubWeaponDmg` (main ELF), `CMonstorUnit::CheckDmg` (call sites of `SwordDmgCheck1`, which is in the overlay), `ItemUseFunc`.

| Rule | Value |
|---|---|
| Wear formula | WHP -= (1.5 - 0.01 x END) x A + 0.1 x B, where A is a scale and B a second wear input. Read in `BattleSubWeaponDmg`. |
| Option bits | Bit 0x100 doubles A (the "fragile" option), bit 0x200 halves A. The 0.1 x B term is unaffected. |
| Melee call sites | `CheckDmg` calls the overlay's `SwordDmgCheck1(1.0, monsterWear)` on a normal hit and `(0.1, monsterWear)` on a hit inside a monster's guard/deflect window; only for character indices 0, 2, 4 (the melee characters, inferred). `monsterWear` is a per-monster value. That `SwordDmgCheck1` applies the same formula is **(inferred)**: its body is in DUN.BIN. |
| Endurance | END 0 -> factor 1.5; END 50 -> 1.0; END 99 -> 0.51. |
| Default weapon | The broken base form never wears. |
| Warnings | Messages when WHP drops below 10% and below 5% of max. |
| At 0, with auto-repair | If item 0xB7 is in an **active-item slot**, it is consumed and WHP is fully restored. |
| At 0, normal weapon | **Deleted permanently** (slot emptied); the character re-equips their default weapon. |
| At 0, default weapon | Reverts to the broken base form, rebuilt from the table: **level, stats and attachments are all lost** (the struct is zeroed first). |
| Repair item 0xB1 | Restores current WHP to max. Used on the broken default weapon, it turns it back into the *base* starting weapon (not the grown one). |
| Instance fields | max WHP (short) and current WHP (float) per weapon. |

---

## 3. Damage formula (player hits monster)

`CMonstorUnit::CheckDmg`. The per-hit attack value is computed earlier, in the overlay (likely ATK x motion factor), and arrives on the hit's collision record.

| # | Step | Rule |
|---|---|---|
| 0 | Attack value | A = the hit's attack power (from the overlay). |
| 1 | Ranged distance | Character indices 1, 3, 5 (inferred: the ranged characters): distance <= 20 -> A x 1.5; distance >= 50 -> A x max(0.5, (150 - d)/100). |
| 2 | Defence | D = A - monster DEF. Character index 3 (inferred: the magic user) uses DEF / 2. If D <= 0, D = 1 (minimum 1). |
| 3 | Element | Only if the hit carries an element (fire, ice, thunder, wind, holy bits 1..0x10; anything else = slot 5): D = (D + D x (0.004 x weaponElem[e] + 0.005 x weaponMAG)) x monsterElemRate[e] / 100. If this zeroes a positive hit, the "no effect" feedback plays. |
| 4 | Anti-monster | If the hit uses the weapon's slayer table: D = D x (1 + 0.015 x slayer[monster family]). Slayer 99 -> x2.485. |
| 5 | Body part | D = D x partRate[character][hit zone] / 100 (per-monster table per character per hit zone; 0 = immune part). |
| 6 | Wrong tool | If the hit requires a specific monster family and this monster isn't it, D = 0. |
| 7 | Frozen | If the monster is frozen/stopped, D / 2. |
| 8 | Non-player source | If the hit has no player owner (items, traps, inferred), D x monster's item-damage rate / 100. |
| 9 | Rounding | Ceiling. |
| 10 | Drain option (0x400) | If D >= 100 and the player is alive: heal 1% of D. |
| 11 | Instant kill (0x1000) | 1% chance the hit deals the monster's full remaining HP, unless the monster is a boss type. |
| 12 | Steal (0x80) | 10% chance to steal the monster's steal item (once). |

Status effects on hit (rolled against the monster's status resistance % R): weapon option 0x20 -> 10% gate, then R% -> poison (180-frame ticks, each tick removes 10% of max HP); option 0x40 -> 4% gate, then R% -> stun 300 frames; element-field bits 0x100/0x200/0x800 (likely from attacks/items) -> poison, stun or another 180-frame status if rand < R; bit 0x1000 on the element field -> freeze 1800 frames.

No critical-hit roll exists in `CheckDmg`. Whether the overlay adds variance or crits when it builds A is unresolved (gap).

Allies: the six characters share this path; differences are (a) ranged distance scaling for indices 1/3/5, (b) half defence for index 3, (c) per-character body-part tables, (d) only 0/2/4 wear weapons via melee hits.

---

## 4. Thirst (dropping it; one table)

`CUserStatus::Step`, `CUserStatus::AddDrink`, `CDngStatusData::Initialize`, `HealingWater`, `ItemUseFunc`, `usedActiveItem`.

| Rule | Value |
|---|---|
| Drain per frame | 0.003 x (1 + 0.2 x dungeon index 0-6). x5 under one status (code 11); x0.8 with option bit 0x08; x2 with option bit 0x10. |
| Start / max | 30 (max and current). Item 0xB6 raises max by its value while max <= 90. |
| Duration (inferred, 60 Hz step) | 30 water = ~167 s in dungeon 1, ~83 s in dungeon 6. |
| At 0 | A timer adds 1/120 per frame; at 1 the character loses 1 HP (~1 HP per 2 s). |
| Refills | Springs / heal zones: full water and HP. Items with the "drink" flag: + item value (cap max). Kills with option bit 0x10 on the hit: +10. One healing item (0xAA) *costs* 20 water while healing 200 HP. |

---

## 5. Dungeon floors

`CDungeonMap::buildRandomMap`, `buildRoom`, `joinRoom`, `setRoomObject`, `setRoomDivid`, `setUnderDungeonStart`, `setStair`, `CDungeonMap::GetRoomLinkInfo`, `CDungeonMap::BuildCharaSpecialParts`, `CDungeonMap::SetTreasureBox`, `CMonstorUnit::SetKey`, `BtAtraListMake`, `BtAtraFloorCyoice`, `chkAtraFloor`; tables `MaxFloorTbl`, `CenterFloorTbl`, `noEntryTbl`, `AtraAppearData0-5`, `BtEnemyLayout00-06`, `BtUraEnemyLayout00-06`.

### Layout generation
| Step | Rule |
|---|---|
| Seed | A fresh seed per floor (rand / 100000), stored on the map so the floor can be rebuilt. |
| Grid | 20x20 cell map; rooms are placed in a 16x16 area. |
| Room size | Width and height each 3 or 4 cells (3 + rand 0..1); random position inside the area. |
| Placement | First two rooms (up to 64 tries), joined to each other; then add rooms up to the floor's target count (up to 512 tries each); each new room is joined by a corridor to the **nearest existing room** by centre distance (a greedy tree, no loops at this stage). |
| Dressing | `setRoomObject`: 30% chance per candidate cell for room objects; `setRoomDivid`: 40% chance a room gets an internal divider; `setUnderDungeonStart` picks the start among up to 60 candidate cells; `setStair` places the exit on a wall-adjacent room cell. |
| Extra links | 51% chance per floor of `GetRoomLinkInfo` adding cross links/special doors (inferred). |
| Character parts | `BuildCharaSpecialParts`: random chance to insert parts that need a specific ally's ability (separate rules for dungeons 1, 5 and 6). |
| Chests | Up to 24 treasure boxes per floor (`SetTreasureBox`), small and large variants. Contents: data/overlay (gap). |

### Floor counts and Atla
| Dungeon index | Floors (`MaxFloorTbl`) | Split floor (`CenterFloorTbl`) | Atla-free floors (`noEntryTbl`, 1-based) | Atla entries: fixed floor / random first half / random second half |
|---|---|---|---|---|
| 0 | 15 | 8 | 4, 8, 11 | 11 / 32 / 38 |
| 1 | 17 | 9 | 4, 9, 12 | 0 / 54 / 40 |
| 2 | 18 | 9 | 5, 9, 12 | 1 / 41 / 32 |
| 3 | 18 | 9 | 5, 9, 13 | 2 / 31 / 34 |
| 4 | 15 | 8 | 4, 8, 11 | 0 / 29 / 27 |
| 5 | 25 | 13 | 19-23 | 62 / 0 / 0 |
| 6 (bonus) | 100 (from its enemy table) | - | - | - |

Atla rule: each Atla entry names a fixed floor, or "any floor before the split floor", or "any floor from the split floor on". Random ones are assigned once (stored in the save) to a floor that has fewer than 8 Atla and isn't Atla-free. Max **8 Atla per floor**.

### Keys and monsters
| Rule | Value |
|---|---|
| Gate key | Each floor, a random living monster flagged "can carry" (and not already carrying a drop) gets the dungeon's key item (IDs 0xC3, 0xC9, 0xCA, 0xCB, 0xCC, 0xCE by dungeon). Dungeon index 1 instead hands out **three different keys** (196, 198, 205) to three monsters. Skipped when the floor has fewer than 2 monsters, when the per-floor mode flag is set (inferred: back floor), and on one scripted floor. After 9,999 failed picks the code forces eligibility. |
| Spawn table | Per floor (112 bytes): a header value plus up to 9 (species, weight %, variant) entries; weights sum to 100 (e.g. 20/50/30). Separate normal and back-floor tables per dungeon. Monster count per floor and stats are loaded by `BtLoadMonstor` in the overlay (gap). |
| Back floors | Entered through a script command (`_SET_URA_DUNGEON` sets map mode 2). How they unlock is in event script data (gap). They have their own enemy-layout tables. |

---

## 6. Georama (town building)

`CEditGround::RequestCheck`, per-town rule functions `CEditGround::NornRequest` (town 0), `MatatagiRequest` (1), `QueensRequest` (2), `MuskaRequest` (3), `YellowRequest` (4); helpers `CheckPartsRect`, `GetRectParts`, `GetRectDirParts`, `CheckRot`, `CEditArea::CheckRiverChain`, `CEditPartsInfo::CheckComplete`, `AnalyzeRequestPer`.

| Rule | Value |
|---|---|
| Parts | Up to 128 placed parts per town, 24 part kinds; the rule function gets the first instance of each kind. A town has up to 4 edit areas (grids). |
| Request slots | 24 booleans, one per part kind (a resident's house is a kind), plus a condition count per town: **8, 10, 10, 9, 13** for towns 0-4. |
| Completion gate | After the rules run, any request whose part kind isn't *complete* (all up to 6 sub-items placed: the house's own Atla pieces/resident) is cleared. |
| Town score | percent = (number of true requests / condition count) x 100, capped at 100. **Every condition has equal weight.** |
| Re-check | The whole set is recomputed from the current layout (no stored history). |

Condition vocabulary (all deterministic grid tests):
| Kind | Meaning | Example (numbers only) |
|---|---|---|
| Absolute area | Part K is (or is not) inside a fixed rectangle of grid cells in area N. | Town 0: kind 1 inside x10-13, y0-4. |
| Proximity | Part K is (or is not) within R cells of part J's footprint. | R = 1-4 typical. |
| In front of | Part K lies in a strip of depth R in front of part J, using J's rotation (`GetRectDirParts`). | R = 2-4, town 3 uses 14-cell strips. |
| Facing | Part K's rotation relative to part J's equals a set offset (`CheckRot`): face the same way, opposite, or 90 degrees. | Town 3 pairs houses facing shared features. |
| Absolute facing | Part K has a specific rotation. | Town 0 kind 0. |
| Count | At least N parts of a kind (or "houses", kinds < 7) within R cells. | Town 1: 4+ of kind 15 within 1 cell; town 0: 2+ houses within 4 cells. |
| River chain | 4-neighbour flood fill through river tiles connects two fixed cells, in each of 3 areas. | Town 1 (all three areas must connect). |
| Same/different area | Two parts are in the same edit area, or not. | Town 2. |
| Story flag | Condition also needs a game counter > 99. | Town 0 kind 5. |

---

## DC1 vs DC2

| Topic | DC1 (this file) | DC2 (`dc2-combat-weapons.md`, `dc2-dungeons-town.md`) |
|---|---|---|
| Level cost | base + level x increment (per weapon), cap 999 | grows by half of base per level |
| Level-up | Manual; +1 ATK/END/SPD/MAG, +1..3 max WHP; attachments absorbed and consumed | Automatic; +2/+3 ATK early then +1, +1 DUR, ~+1.7 WHP |
| ABS on default weapon | None | Yes |
| Build-up threshold | >= 100% of the target's base, negative = upper limit; no attachments allowed | >= 90% of target base + kill check |
| After build-up | Keeps ATK/MAG/elements; END/SPD/WHP raised to target base; level 0 | +10% of target base; level 0, keeps ABS % |
| Spectrumize | Level 5+, 0.6 x all stats including ATK; non-default weapon destroyed | Level 5+, 0.6 x DUR/attributes, full ATK |
| Lucky slot | 1/50 per level for a x2 absorb | none |
| WHP at 0 | Weapon destroyed (default weapon loses all growth) | 0 damage, -10% of max ABS |
| Wear | (1.5 - 0.01 x END) x scale + 0.1 x B | half the monster's wear value, -0.5% per DUR |
| Elements | One active element, +0.4%/point + 0.5%/MAG, x monster % | Sum over 8 attributes |
| Minimum damage | 1 | 0 |
| Crit | No roll seen; 1% instant-kill option | 10% x1.8 option |
| Thirst | Yes (0.003/frame scaled by dungeon) | Removed |
| Floors | Procedural rooms on a 20x20 grid | Hand-made rooms on a 14x14 grid from a floor graph |
| Gate key | Random eligible monster | First monster in the list |
| Georama score | Equal weight 1/N, N = 8-13, gated by building completion | Weighted 5-15% per request |

---

## Design takeaways for Sleeping Calamity

Cross-checked against `docs/design/dark-cloud-lessons.md`.

1. **Durability.** DC1's break rule is the root of the "babysitting" complaint: one empty gauge deletes a weapon, and even the starting weapon loses all its growth. Our rule already says wear never destroys. If wear stays, END-scaled wear (1.5 - 0.01 x END) is a good gentle curve, applied to output instead of to existence.
2. **Weapon tree.** Build-up checks are exact thresholds on element and slayer stats (plus upper limits), never on level. That makes a visible tree easy to build: show each target's thresholds against the weapon's current effective stats as progress bars. Drop "no attachments allowed", which is a hidden trap.
3. **Level-up absorb.** Absorbing attachments permanently at level-up is the most interesting DC1 mechanic: attachments are temporary until you commit. Keep the idea but preview the result, and make the 1-in-50 lucky slot visible (or drop it).
4. **Growth from use.** ABS = flat per-species value; no bonus for skill. Our "kills, combos, bosses" rule can reuse the per-species base plus skill multipliers. Don't copy "default weapon earns nothing".
5. **Damage.** Flat subtract with minimum 1 plus percentage layers is simple and readable; DC1's single active element is easier to read than DC2's 8-attribute sum. Use per-body-part % for weak points (DC1 has it per character), which supports "enemies need different answers".
6. **Georama.** Every condition is a deterministic grid predicate (area, distance, strip-in-front, facing, count, river flood fill). A live preview score is cheap: rerun the predicates on each placement. Equal weighting (1/N) is clear; show each wish in plain words with a live tick.
7. **Floors.** DC1's room-and-nearest-corridor generator is why floors felt samey: 3-4 cell boxes, no authored set pieces. Our hand-authored room pools fix this; borrow only the shape: seed per floor, tree join, then a 51% extra-link pass for loops.
8. **Collectibles.** The Atla split-floor rule (random within the first or second half, max 8 per floor, blocked on event floors) is a good way to spread town pieces without making them missable; add a per-floor "pieces left" counter.

---

## Gaps

- **Overlay code is missing.** `SwordDmgCheck1` (melee wear), `BtCheckDamageProc` (damage to the player, player defence), `BattleActionDrink`, `BtLoadMonstor` / `BtArrengeMonstor` (monster stats, count per floor, placement), the attack-value build (any random spread), back-floor unlock flow and chest contents all live in `DUN.BIN` inside DATA.DAT, which wasn't available or extracted. Next step if wanted: with the user's OK, pull `DUN.BIN` out of DATA.DAT (offsets via DATA.HED) and load it in Ghidra at 0x1dabd00 as an overlay block.
- **Data-file constants.** Item effect values (item data table via `GetItemData`), monster stats/ABS/wear, and georama part data (`CEditPartsInfo::Load`) come from data, not code.
- **Field meaning guesses.** The ABS x2 / no-gate-key mode flag (inferred back floor), the character index order, the "drink" item flag, and the attachment grade's effect are inferred.
- **Not resolved.** The "ma's key" / second key type from the brief wasn't found in main-ELF code; only gate keys (and dungeon 1's three keys) are.
- **Last town.** Only 5 town rule functions exist (towns 0-4); the remaining town has no request scoring in the ELF.
