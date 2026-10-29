# Dark Cloud 1 (USA) - reverse-engineering notes for Sleeping Calamity

Research from the user's own copy of Dark Cloud 1 (SCUS-971.11, NTSC-U), approved for REA research: mechanics and numbers only. Everything below is in our own words. No decompiled code, game text or assets are reproduced; only the numbers needed. "(inferred)" marks conclusions not read directly from code. Function names are given demangled (`Class::Method`). Item and weapon references use numeric IDs, not in-game names.

## Summary
1. Weapons grow on a per-weapon ABS gauge earned from kills: next-level cost = base + level x increment (per weapon, base 5-125, increment 5-50, cap 999). Level-up is a manual menu action that adds +1 ATK/END/SPD/MAG, +1..3 max WHP and permanently absorbs whatever is attached (slot contents are consumed); starting weapons earn no ABS.
2. Durability is harsh: each hit removes (1.5 - 0.01 x END) x scale + 0.1 x extra from WHP; at 0 a normal weapon is deleted permanently, and even the starting weapon is reset to a level-0 "broken" form (all growth lost). Only an auto-repair item in the active slot saves it.
3. Damage = attack - monster defence (minimum 1), then one selected element (+0.4% per element point, +0.5% per MAG point, scaled by the monster's element %), anti-monster +1.5% per point, a per-character per-body-part %, half damage on frozen targets, rounded up. No crit roll in that path; a 1% instant-kill special exists.
4. Thirst drains 0.003 per frame x (1 + 0.2 x dungeon index) from a starting max of 30; at 0 you lose 1 HP every 120 frames. Springs refill fully.
5. Floors are procedural (16x16 room area, 2..N rooms of 3-4 cells joined to the nearest room); Atla are pre-assigned to random floors in the first or second half of each dungeon (max 8 per floor); one random eligible monster carries the gate key. Georama is scored per town by a hand-written rule function: 8-13 equally weighted conditions (area, distance, facing, river flood-fill, counts), each only counted once its building is complete.
6. **No damage randomness anywhere** (all six characters' attack builders read). Player hit power = effective weapon ATK including attachments (x1.8 on the hero's combo finisher; ATK/2 + MAG/2 for the magic and gun users); monster hit power is a fixed per-attack number in its script. Damage to the player = monster power - character DEF, minimum **0**. Monsters per floor: exactly 15 (8 on a back floor).
7. Monsters scale steeply by dungeon: median HP ~30 -> ~500 across dungeon indices 0-5 (thousands in the bonus dungeon), median attack 30 -> ~107, but monster DEF stays tiny (0-25). ABS per kill 2-60. Back floors are a second copy of the current floor with their own spawn tables, x2 ABS, no gate key, and no map/crystal chests.

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
| Decompile failures | 118 functions in the first pass; nearly all were the unmapped `title`/`dun` overlays. `dun` is now resolved (see below); `title` (TITLE.BIN, same method) was not needed. |

Memory map: `main` segment 0x100000 (file offset 0x100). Two overlay segments, `title` and `dun`, both at 0x1dabd00 with **zero bytes in the ELF**. Several data tables (weapon list, Atla tables, enemy-layout tables, **monster table**) are initialised data inside `main`, so their numbers are readable directly.

### Overlay and data extraction (second pass)
| Item | Value |
|---|---|
| Where DUN.BIN lives | **Not in DATA.DAT.** `LoadOverlay` opens `cdrom0:\DUN.BIN;1` directly; `DUN.BIN` (100,224 bytes) and `TITLE.BIN` are plain ISO root files. Extract with `7z e "<iso>" DUN.BIN TITLE.BIN -oC:\Dev\_private\ps2\dc1`. |
| Overlay format | CodeWarrior overlay: header magic `MWo3`, load address 0x1dabd00 at +0x08, bss size at +0x14, static-ctor range at +0x18/+0x1c, code from +0x80. The whole file (header included) loads at 0x1dabd00, so file offset = address - 0x1dabd00; already absolute (no relocation needed). bss runs to 0x1f06b00. |
| Ghidra load | Ghidra's ELF import already maps 0x2a2380-0x1f06b00 as a zero-filled `heap` block. `scripts\FillDun.java <DUN.BIN> <out.c> <syms>` writes DUN.BIN's bytes into that block, applies the `dun`-section symbols, disassembles and decompiles. Output `decomp\dun.c` (161 of 162 functions). Symbols from `scripts\dunsyms.py <ELF> dun` (filters `.symtab` by section index, so `title` symbols at the same addresses are excluded). Some `title` function entries also sit inside the range, so a few `dun` functions are split by stray `title` names (read neighbours). Pre-change project backup: `ghidra_backup_pre_dun\`. |
| DATA.HED format | 80-byte records, 5,103 entries: name[64] (NUL-terminated, backslash paths like `dun\monstor\x.stb`), then four little-endian u32: **byte offset in DATA.DAT**, size, start sector (offset/2048), sector count. `DATA.HD2` holds the same 4 numbers in 32-byte records with a name-pool offset instead of the inline name. |
| Extractor | `scripts\hed.py list DATA.HED` / `hed.py x DATA.HED DATA.DAT <outdir> <regex>` (run with `python -I`). DATA.DAT (1.73 GB) is extracted to `C:\Dev\_private\ps2\dc1\DATA.DAT`; pulled files in `extract\` (`dun\monstor\*.stb`, `dun\*.cfg`, `dun\script\d0N\event.stb`). |
| Script format (`.stb`) | Header `STB`, +0x08 code offset, +0x0c entry-table offset, +0x10 entry count; entries are (entry id, offset). Bytecode is 12-byte instructions (op, a, b) but functions are interleaved with 32-byte headers, so each function has its own alignment (scan all three 4-byte phases). Op 3 = push immediate (a: 1 int, 2 float, 3 string offset from the code base), op 0x15 = call a native command with `a` stack slots (first slot = command id). Command-id tables (func, id) in the ELF at 0x269c50 (306 event commands), 0x26b310 (67 dungeon-event commands), 0x2918a0 (91 monster commands). Helpers: `scripts\stbsim.py` (symbolic stack walk, lists a command's immediate arguments), `scripts\stbctx.py` (calls around a command), `scripts\montiers.py` (monster table x spawn tables x script attack powers, prints ranges). |

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
| Amount | The monster's per-species ABS value (`MonstorTable`, 0-60, median 3-9 per dungeon; section 3b). |
| x2 | On a back floor (confirmed: the flag is `BtUraDongeon`; the same flag disables gate-key carriers). |
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
| Melee call sites | `CheckDmg` calls the overlay's `SwordDmgCheck1(1.0, monsterWear)` on a normal hit and `(0.1, monsterWear)` on a hit inside a monster's guard/deflect window; only for character indices 0, 2, 4 (the melee characters, inferred). **Confirmed from DUN.BIN:** `SwordDmgCheck1` passes both straight to `BattleSubWeaponDmg` (same formula, A = 1.0 or 0.1, B = monster wear), then handles the break effects and the low-WHP weapon 0x110's power-up cue. |
| Monster wear (B) | Per-species value in the monster table (row +0x66). Range 0-100; most early monsters 0-10, medians by dungeon index 0-5: 6/0/0/10/20/10, bonus dungeon 10-100. So a normal hit costs (1.5 - 0.01 x END) + 0.1 x B: END 30 on a B 10 monster = 1.2 + 1.0 = 2.2 WHP; a guarded hit still pays the full 0.1 x B term (0.12 + 1.0). Hard-shelled monsters (B 40-60) roughly triple wear per hit. |
| Endurance | END 0 -> factor 1.5; END 50 -> 1.0; END 99 -> 0.51. |
| Ranged/magic wear | Casters wear their weapon per shot, not per hit: the magic user at scale 0.8 (1.8 on a charged cast), the gun user at 0.5. |
| Default weapon | The broken base form never wears. |
| Warnings | Messages when WHP drops below 10% and below 5% of max. |
| At 0, with auto-repair | If item 0xB7 is in an **active-item slot**, it is consumed and WHP is fully restored. |
| At 0, normal weapon | **Deleted permanently** (slot emptied); the character re-equips their default weapon. |
| At 0, default weapon | Reverts to the broken base form, rebuilt from the table: **level, stats and attachments are all lost** (the struct is zeroed first). |
| Repair item 0xB1 | Restores current WHP to max. Used on the broken default weapon, it turns it back into the *base* starting weapon (not the grown one). |
| Instance fields | max WHP (short) and current WHP (float) per weapon. |

---

## 3. Damage formula (player hits monster)

`CMonstorUnit::CheckDmg`. The per-hit attack value is built when the attack's hit volume is spawned (`ToanKey_Play`, `UngagaKey_Play`, `GoroKey_Play` etc., one per character, plus `basic_damage`) and arrives on the hit's collision record.

| # | Step | Rule |
|---|---|---|
| 0 | Attack value | Melee and slingshot: A = the weapon's **effective ATK** (base + levels + current attachments, lucky slots x2, the low-WHP weapon's x1.5; built by `WeaponAllValueSet`), the same for every swing of a combo. The hero's 5th (finisher) combo hit x1.8; one melee ally's long special x1.5. Magic and gun users: A = ATK/2 + MAG/2, the magic user's charged casts x1.5 / x2.2. A player status (bit 8, 1800 frames) doubles A. Combo steps differ only in hit radius and active frames. `basic_damage` attaches the slayer table, active element and character index to the hit. All six characters' attack builders were read (`ToanKey_Play`, `UngagaKey_Play`, `GoroKey_Play`, `BattleActionPlay_Jinn`, `BattleActionShotRuby`, `BattleActionPlay_Ozumond`). |
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

**No variance and no crits (all six characters checked).** Neither the attack-value builds nor `CheckDmg` roll anything for the amount (the overlay's other `rand()` calls are a +-8 screen shake after a hit); the only random rolls on a hit are the 1% instant kill, the 10% steal and the status procs. The same weapon on the same monster part always deals the same number.

### Damage to the player (monster hits character)
`BtCheckDamageProc` (DUN.BIN), `_SET_DMG_COL` / `_SET_DMG_PARA` (monster script commands), `BtSetStatusErr`, `BtStatusErrStep`, `CDngStatusData::Initialize`.

| Rule | Value |
|---|---|
| Monster attack power | Set per attack by the monster's script: `SET_DMG_COL(frame, radius, startFrame, endFrame)` opens a hit volume on a skeleton frame for a motion window, `SET_DMG_PARA(power, statusFlags, hitType[, knockAngle])` gives it a power. Fixed integers (e.g. one species uses 30 and 35 for its two attacks). Doubled while the monster is in its 1800-frame status (the one that also halves damage it takes). |
| Formula | damage = power - DEF[character], **minimum 0** (not 1). DEF is doubled while the player has status bit 8. |
| Player DEF | Per character, starting 3, 1, 12, 23, 38, 46 (character order 0-5; later joiners start higher). Raised only by per-character defence items 0x88-0x8D: + (base + rand % range), cap 99. Starting max HP: 70, 60, 100, 90, 110, 100. |
| Flag 0x80000 | Damage = half the character's max HP instead (percentage attack). |
| Hit types | Type 3 = heavy blow: knock-back, and a stagger state if damage < current HP (otherwise a down state); types 2/4 = light hit. Invulnerability window after a hit: 160 frames (type 3), 80 (type 2), 8 (type 4, multi-hit attacks). |
| Status bit 8 | Odd one: it doubles both the player's attack and DEF for 1800 frames, yet monsters inflict it (flag 0x1000). Identity unresolved (a "berserk"-style state, inferred). Likewise the monster's 1800-frame state (labelled "frozen" above) both halves damage taken and doubles damage dealt. |
| Status procs | One 0-99 roll per hit, compared per flag: 0x1000 -> status bit 8 at 65%; 0x100 -> stop/freeze (bit 4, 300 frames) at 65% unless an active-slot charm item 0x84 absorbs it (one charge used); 0x200 -> poison (bit 0x10) at 65% unless immune or charm 0x87; 0x400 -> bit 0x20 (charm 0x85); 0x800 -> bit 0x40 (charm 0x86); 0x100000 -> stop always. Statuses block each other (stop clears the others). |
| Poison on the player | 4% of max HP every 180 frames. |
| Money theft | Flag 0x40000: 20% per hit, if the player holds more than 10 money, the monster takes one fifth of it; killing that monster drops the stolen sum. |
| Monster attack ranges (script `SET_DMG_PARA` power, 269 attack entries from 138 of 141 species) | by dungeon index 0: 16-45 (median 30), 1: 12-68 (38), 2: 42-84 (69), 3: 45-102 (71), 4: 8-125 (90), 5: 75-175 (107), 6 (bonus): 85-200 (131). Versus starting DEF 1-46 and a DEF cap of 99, mid-game monsters hit for roughly 30-100. |

Allies: the six characters share this path; differences are (a) ranged distance scaling for indices 1/3/5, (b) half defence for index 3, (c) per-character body-part tables, (d) only 0/2/4 wear weapons via melee hits.

---

## 3b. Monsters (stats, drops, AI)

`MonstorTable` (main ELF, 167 rows x 156 bytes), `CMonstorUnit::SetupBaseModel` / `SetupViewMonstor` (copy a row into the live monster), `BtLoadMonstor` (DUN.BIN), `ArrangementPos`, `CMonstorUnit::Step` (kill), `SelectAttachi`, per-species script `dun\monstor\<name>.stb`.

### Row layout (what each species defines)
Model file names (body + up to 3 parts), script name, max HP, family (0-9: which anti-monster stat applies; family 8 = mimic), 5 element damage rates % (fire, ice, thunder, wind, holy), body radius, DEF, wear value, ABS per kill, money amount and money-drop chance %, type (0 normal, 2 boss, 3/4 mimic variants), name id, steal item, "can carry a drop / gate key" flag, damage-from-items %, status susceptibility %, fixed drop item, and a 6-entry per-character damage % (the body-part table's default). **Attack power is not in the row**: it lives in the species script (section 3, damage to the player).

### Per dungeon (normal floors; species that appear in that dungeon's spawn tables)
| Dungeon index | Species | HP min-max (median) | DEF | ABS/kill | Wear B | Attack power |
|---|---|---|---|---|---|---|
| 0 | 14 | 12-250 (29) | 0-10 (2) | 2-10 (3) | 0-40 (6) | 16-45 (30) |
| 1 | 19 | 50-700 (60) | 0-12 (0) | 3-20 (3) | 0-10 (0) | 12-68 (38) |
| 2 | 18 | 80-700 (200) | 0-10 (5) | 0-30 (9) | 0-30 (0) | 42-84 (69) |
| 3 | 15 | 150-2000 (500) | 0-10 (5) | 4-40 (5) | 0-50 (10) | 45-102 (71) |
| 4 | 14 | 80-2000 (465) | 0-12 (5) | 0-50 (8) | 0-60 (20) | 8-125 (90) |
| 5 | 25 | 80-3200 (525) | 0-25 (3) | 0-60 (6) | 0-60 (10) | 75-175 (107) |
| 6 (bonus, 100 floors) | 56 | 1500-50000 (5000) | 8-30 (20) | 5-35 (25) | 10-100 (10) | 85-200 (131) |

Maxima are bosses/mini-bosses; the medians are the typical floor monster. Back-floor spawn lists use mostly the same species band per dungeon with lower HP ceilings (e.g. dungeon index 4 back floors 270-750). Shape: HP grows ~x15 from dungeon index 0 to 5 while DEF barely moves, so weapon ATK growth (not DEF penetration) is what keeps pace; ABS per kill stays almost flat (median 3-9), so later weapons level slower relative to monster toughness.

| Field | Distribution over all 167 rows |
|---|---|
| Element rates | 100 is the norm (507 of 835 values); weaknesses mostly 110-150 (a few 200), resistances 0-80, two entries -50 (heals). Weakness most often holy, then fire, thunder, wind; resistance most often fire, then ice. |
| Status susceptibility % | Mostly 50-70; 26 species immune (0), 7 fully susceptible (100). |
| Item damage % | 50-100 (75 rows at 100). |
| Per-character damage % | 100 almost everywhere; 0 marks immunity to a character (145 entries: e.g. flying enemies immune to melee characters), 150-200 marks a favoured character. |
| Families | 10 families; family 9 (47 rows) is the catch-all (bonus-dungeon, objects). |

### Drops on kill
Only if the monster has the "can carry" flag and isn't a boss:
1. If it holds an assigned drop (the gate key), that drops.
2. Else 10% chance of its fixed drop item.
3. Else `SelectAttachi`: 71% chance of an attachment: 30% of those (when some element after fire beats the running maximum, i.e. a non-fire weakness) the gem for that element (IDs 0x52-0x55; the fire gem 0x51 can't come from this path), otherwise the anti-monster gem for its family (0x6F-0x78). **Caveat:** in both the decompile and the disassembly this drop only happens when the monster's kill-owner field is -1, while ABS is only granted when that same field equals the active character. Read literally, attachments drop only from non-character kills (items, traps); that contradicts play experience, so the field's meaning (or a reset we didn't find) is unresolved.
4. If nothing dropped: roll its money chance (0-80%, median 30%); amount = base + rand(0..base/2) (base 0-35), x2 / x0.5 with weapon options 0x02 / 0x04.
Money stolen by the monster is always returned on death. ABS: the species value, x2 on back floors (confirmed below), x1.2 with option 0x2000.

### Placement
Each floor gets **15 monsters (8 on a back floor)**, fewer only if placement gives up after 65,000 tries, placed at random free spots at least 25 units from any other monster, with at most 2 others within 480 units. The species is picked **uniformly** from the floor's loaded spawn list (the per-entry weight field isn't used on this path); species of type 2 (boss) and 4 (a mimic variant) are placed at most once per spawn-list slot. Then the gate key is assigned (section 5).

### AI (monster scripts)
Each species runs a bytecode script with fixed entry points: **1** spawn/init, **100** think (restarted whenever the previous program finishes, otherwise resumed each frame), **110** got hit, **120** died, **50** mimic wake-up (20 scripts have it). While the monster is stunned the script is suspended. The command vocabulary (91 commands) is a small action set: distance and facing to the player (`GET_DISTANCE`, `CHK_USER_INNER_PRODUCT`), move/rotate/cancel, play motion and test motion frame, random int/float, open damage volumes and set their power, fire shots, guard windows (`SET_GUARD_FRAME`, `GUARD_SEARCH`, which produce the 0.1-wear guarded hits), lock-on distance, invulnerable/alpha/fall flags, and per-monster globals. So DC1 AI is a hand-scripted loop per species (typically "face player, approach until in range, roll for one of 1-3 attacks, recover"), not a shared state machine (the "typical loop" is inferred from the command mix, not traced per species).

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
| Chests | See "Chests" below. |

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
| Spawn table | Per floor (112 bytes): a header value plus up to 9 (species, weight %, variant) entries; weights sum to 100 (e.g. 20/50/30). Separate normal and back-floor tables per dungeon. `BtLoadMonstor` loads every listed species for the floor; placement then picks among them uniformly (section 3b), so the weight field looks unused on that path. |
| Back floors | **Confirmed:** the per-unit flag that doubles ABS and skips the gate key is `BtUraDongeon` (copied into the monster unit by `CleanViewMonstor`), i.e. the back floor. A back floor is a **second map of the same floor** (`UraDungeonMap`, own monster unit, own item set, own lighting/fog lines `URA_*` in the floor config). Entry: a map part of kind 77 (the `o11` family, a different prop per dungeon: cart, door, etc.) carries a `ura_2` marker; touching it runs the dungeon's event script, which plays a short camera/fade cutscene and calls `_SET_URA_DUNGEON`; the dungeon loop then swaps maps (the same command swaps back). Differences on the back map: spawn list from the back-floor layout, 8 monsters, ABS x2, no gate key, chests drawn from the back-floor item pools and no map/crystal chests. |

### Chests
`CDungeonMap::buildEventData`, `PresetSmallItemNo_Get`, `SetTreasureBox`, `GetRoomLinkInfo`, chest-open code in the dungeon menu (`BtGetTreasurebox*`).

| Rule | Value |
|---|---|
| Count | 4 + rand(0..3) chests per floor (24 slots max, shared with scripted chests). |
| Size | Each chest: 88% small, 12% large; the first two are always small. |
| Main-floor freebies | On the main (not back) floor the first two small chests are always the **floor map** (item 0xE9: reveals the minimap) and the **crystal** (item 0xEA: sets a minimap flag, inferred to show items/Atla). Neither goes into the inventory; both are per-floor flags. |
| Small chest contents | A random item from the floor's pool with ID 0x51-0x100 (attachments, gems, consumables). |
| Large chest contents | A random item with ID > 0x100 (**weapons**), plus a trap id 0-5 (read back by `_GET_ITEM_TRAPID`). |
| Pools | `ItemPutListPtr[dungeon + 7 x isBackFloor]`: per-floor lists (up to ~128 IDs) with a fallback list for "before the split floor" (key 0x100) and "after" (key 0xFF). A per-dungeon rarity table (`ItemSetRateTbl`) gives each item a 0-100 threshold; a candidate is accepted when a d100 roll beats it (rejection sampling). Weapons the player already owns get their threshold lowered by 10 (one owned) or 20 (2+) (as decompiled; this makes duplicates *more* likely, which may be a decompiler sign issue). |
| Special chest | `GetRoomLinkInfo` (51% of floors) puts a chest holding item 0xD8 + dungeon index in a room reached through an ally-ability door (inferred from the flood fill over door types). |
| Trap circles | After chests: 3 rolls at 21% each for a floor trap circle. |
| Mimics | Family-8 monsters of type 3/4 register a mimic event at their spawn point (they sit as chests and wake via script entry 50). |

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
| Minimum damage | 1 to monsters, 0 to the player | 0 |
| Crit / variance | None: damage is fully deterministic; 1% instant-kill option; hero finisher x1.8 | 10% x1.8 option |
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
9. **Monsters.** DC1 scales monster HP ~x15 across the story while DEF stays near zero and damage is deterministic; fights get longer, not more interesting. Wear per hit is mostly a flat per-species number (0.1 x B), which is a cleaner lever than END: we can mark "hard" enemies (high B) visibly instead of hiding it. Script entry points (spawn / think / hit / die / wake) are a good minimal AI contract for our own enemy behaviours.

---

## Gaps

- **Back-floor unlock condition.** How a floor gets the back-door part (kind 77 with the `ura_2` marker) is not traced: the part comes from the floor's part set in `dun\d0Nmain_*.cfg` and the random map builder, and the door's event function in `dun\script\d0N\event.stb` may add a condition (nearby events test game flags and `ITEM_USE_WINDOW` item IDs, but those weren't tied to the back-door function). A full per-function STB disassembler would settle it.
- **Status identities.** Player status bit 8 (doubles ATK and DEF, monster-inflicted) and the monster's 1800-frame state (halves damage taken, doubles damage dealt) contradict their first-pass labels; meanings unconfirmed. The minimap crystal's exact effect is inferred.
- **Attack coverage.** `SET_DMG_PARA` powers were recovered for 138 of 141 species scripts (269 distinct attack entries) by a symbolic stack walk over all three code alignments; attacks whose power is computed at run time from script variables would be missed. Matches were sanity-checked (plausible powers, flags, hit types) but not traced per function.
- **Item data.** Item effect values (`GetItemData`), the chest rarity table's exact sign, the attachment grade's effect, and georama part data (`CEditPartsInfo::Load`) were not read.
- **Attachment drops.** The kill-owner test on the attachment drop (section 3b, drops step 3) reads as "non-character kills only"; unresolved.
- **Field meaning guesses.** The character index order and the "drink" item flag are still inferred.
- **Not resolved.** The "ma's key" / second key type from the brief wasn't found; only gate keys (and dungeon index 1's three keys) are.
- **Last town.** Only 5 town rule functions exist (towns 0-4); the remaining town has no request scoring in the ELF.
