# Dark Cloud 2 combat, weapon growth, durability, player stats - design research

Sources read (read-only): ChronicleTwo (PAL decomp, `ps2/src`, `ps2/include`) and OpenCloud (`code/engine`).
OpenCloud is Dark Cloud 2 only (its README says DC1 is "later"), so there is no Dark Cloud 1 coverage. Where OpenCloud overlaps (WHP wear, level-up, build-up check, spectrumize 0.6) it agrees with ChronicleTwo.
All text is our own words; only numbers and mechanics are recorded. "(inferred)" marks conclusions not read directly. Short names: ATK = weapon Attack stat, DUR = the weapon's second stat ("Durable"), WHP = weapon durability gauge, ABS = weapon experience gauge.

## Summary
1. Player damage to monsters = (ATK x per-attack multiplier - monster DEF) x per-weapon-type % x (1 + sum over 8 attributes of resist% x attr x 2/255), rounded up. Defence is subtracted FIRST, so attributes cannot rescue a hit that defence has zeroed.
2. Each weapon levels on its own ABS gauge (requirement grows by half of base per level): +2/+3 ATK in the first 5 levels, +1 after, +1 DUR, about +1.7 max WHP; cap level 99.
3. WHP drains per melee hit (half the monster's wear value, cut 0.5% per DUR point) and per ranged shot (1 / hits, cut 0.2% per DUR); at 0 WHP the weapon deals 0 damage, and the break costs 10% of max ABS. Repair kits fully restore.
4. Build-up needs ATK and all 8 attributes >= 90% of the target weapon's BASE values (plus a monster-kill check); the new weapon resets to level 0, keeps ABS %, and gains +10% of the target's base stats. Spectrumizing turns a level 5+ weapon into an attachment worth 60% of DUR/attributes and full ATK; applying it spends Synthesis points.
5. Player max HP only grows by item (+8, cap 255); defence only by item (+4, cap 128). Monster hits on the player add flat 1-3 chip damage that bypasses defence.

---

## 1. Damage formula

### 1a. Player/weapon attack hits a monster
Source: `ps2/src/monster.cpp` `CMonsterMan::CheckDamage` (main flow); `ps2/src/maintex.cpp` `SetDamageParam`; `ps2/src/actionchara.cpp` (damage-entry loop, ~line 3410); `ps2/src/userdata.cpp` `CBattleCharaInfo::RefreshParamater`; `ps2/src/menucommon.cpp` `GetDispVolumeForFloat`.

Order of operations (each step feeds the next):

| # | Step | Detail |
|---|------|--------|
| 0 | Attack input | `SetDamageParam` replaces the attack-table damage with the equipped weapon's ATK (slot 0 = melee, slot 1 = gun/magic). If that weapon's current WHP <= 0, ATK is forced to 0 (Max/Monica only; the monster-form character skips this check). Melee attacks then multiply by a per-attack `power_rate` and truncate to int (`actionchara.cpp` damage-entry loop). Monica's magic multiplies by a `scale` argument (`actscript.cpp` `ShotMonicaMagic`). |
| 0b | Power status | While the player has the Power status, ATK x1.5 for both slots (`RefreshParamater`). Lamb's Sword special case: ATK x1.5 at night, x0.5 by day (`CGameDataUsed::GetStatusParam(param, time)`). |
| 1 | Defence | D = ATK' - monster.defense (not clamped yet). |
| 1b | Ratio hits | Special flag 0x1000 replaces D with `max_life x (attack-table value %) x monster.ratio_damage_rate %`. Holy-water flag: D = 0 unless the target is one particular species (id 9). |
| 2 | Attack-type scaling | D x= ext_param[type]/100. Each monster has 12 percentages, one per attack type (Max melee, Max gun, laser, grenade, Monica melee, Monica magic, 4 ridepod kinds, monster attack, item/bomb). Index map is `vs_attk_index` in `monster.cpp`. |
| 3 | Attributes | For each of 8 attributes e: bonus_e = D x (attr_e x 2/255) x (monster.element_resist[e] / 100). D_final = D + sum(bonus_e) = D x (1 + sum(attr_e x resist_e / 12750)). attr_e is the weapon's total (base + level-ups + attachments). |
| 3b | Rain | If the weather is rain, each bonus_e is further multiplied by {0.6, 1.2, 1.4, 0.8, 1, 1, 1, 1}. Consistent with elements 0-3 being fire, cold, thunder, wind (inferred from the `WEAPON_ELEMENT_KIND` enum order). |
| 4 | Species bonuses | There is no separate anti-species code path. Attributes 4-7 go through the same formula as 0-3; a species bonus is a monster with a high `element_resist` in that slot (inferred). One hard-coded 1.2x exists for two specific monsters when a specific partner NPC is in the party. |
| 5 | Multi-hit split | D /= the attack's hit_count (multi-hit attacks divide damage across their hits). |
| 6 | Range falloff | Projectiles: full damage within range/2 of the origin, linear fall to 0 at range, 0 beyond. Melee uses range 10000, so no falloff. |
| 7 | Rounding | Ceiling (`GetDispVolumeForFloat`: whole+1 unless the fractional part < 0.00005). If result <= 0 it becomes 0 and the "no damage" counter increments. |
| 8 | Invulnerability | If the monster has an `muteki_time` timer, D = 0. A monster just hit ignores new hits for the attack's `stun_time` frames (`damage_time`). |
| 9 | Specials (only if D > 0) | Dark-coin flag 0x200: D x1.5 and costs the attacker 1% of max HP (if HP > 1). Critical flag 0x400: 10% chance (iRand(10)==0) of D x1.8. Drain flag 0x80: chance 25/hit_count % (min 1%) to heal 2% of D. Steal flag 0x10: chance 12/hit_count % (min 1%) tested against the average of two d100 rolls (extremes rarer); takes drop slot 0, or slot 1 with 20% chance; the monster's drops are then cleared. |
| 10 | Guard | A monster guards if its `guard_rate` > d100 (not while stunned, not if the attack ignores guard; a script flag can force guarding). Guarded: D x= the attack's `critical_rate`/100. (The field is documented as a crit scale but is used as the guard reduction for both monster and player.) |
| 11 | Apply | WHP wear is applied (melee only, see section 3), then life -= (int)D. Zero damage still wears the weapon and shows the guard/no-damage feedback. |

Notes:
- Critical hits: no innate crit chance was found. The only crit is the Bull's-Eye coin flag (10%, x1.8).
- Combos / charge attacks: there is no combo counter or charge multiplier in the C++. The multiplier is the `power_rate` argument that action scripts pass when registering each hit window (`actscript.cpp` damage-entry opcode, ~line 1003). The charge hold counter is `acumu_pad` (frames the button is held; `actionchara.cpp` ~3288) exposed to scripts. Actual numbers live in script data (see gaps). A `DAMAGE_HIT_CHARGED` flag exists but is documented as unread.
- Randomness: none in player-to-monster damage itself (deterministic) apart from the optional specials (crit, drain, steal, status procs).
- Monster rage ("gekirin"): each damaging hit lowers a counter by 1/hit_count; at 0 a non-boss monster's attack becomes 1.3x base for 900 steps.
- Stagger: monsters accumulate `stagger` from the attack's stagger value (reset window 60 steps) and flinch at their table threshold.

### 1b. Monster hits a player
Source: `ps2/src/actionchara.cpp` `CActionChara::CheckDamage`.

| Step | Maths |
|------|-------|
| Roll | D = hit.damage x (1 + rand[0, 0.15)) - player.defence; clamp to >= 0 |
| Multi-hit | D /= hit_count |
| Chip | D += 1 + rand[0, 2) (always added, so minimum chip is 1-3; defence cannot reduce it to 0) |
| Ratio hit | D = floor(maxHP x value%), never kills (D <= HP - 1) |
| Half-HP hit | D = floor(HP/2), min 1 |
| Guard | If the player is blocking: D x= critical_rate/100 of the attack |
| Round | Ceiling |
| Result | HP -= D; knockback speed 4 for 5 steps; hit-stun = stun_time x3 (x7 knockdown, x2 guarded) |
| Death | If HP <= 0 and a Resurrection Powder is equipped, HP is set to 50% instead |

No damage is taken while the player's `damage_time` or `muteki_time` is above 0.

## 2. Weapon growth

### 2a. How ABS is earned
Source: `ps2/src/runscript_opcodes.cpp` (monster death, ~lines 3400-3460); `ps2/src/maintex.cpp` `AddExpWeaponParam`; `ps2/src/userdata.cpp` `CBattleCharaInfo::AddAbs`; orb pickup in `ps2/src/dng_object.cpp` (`CPullItem`, ~line 1632).
- On a monster's death its `reward_exp` (per-monster table value) is split into orbs. Orb count by total exp: 1-5 -> 6 orbs, 6-49 -> 8, 50-199 -> 10, 200-499 -> 12, 500+ -> 16. Each orb carries exp / orb_count and gives that amount when collected.
- Experience-coin flag (0x800) on the killing hit: total exp x1.2.
- Credit goes to the weapon type of the LAST HIT (`last_hit_source`): melee -> main weapon slot; gun/magic -> sub slot; thrown items/bombs -> split 50/50 across both; ridepod -> ridepod ABS; generic named attacks -> main weapon. If the character collecting is not the one who owned the kill, orbs go to the current character's weapons (melee+gun split 50/50 for Max/Monica).
- Max's fishing rod never gains ABS.
- Breaking a weapon (section 3) removes 10% of max ABS (`AddAbsRate(-0.1)`).

### 2b. Level-up
Source: `userdata.cpp` `CGameDataUsed::IsLevelUp`, `LevelUp`, `CheckParamLimmit`.

| Item | Value |
|------|-------|
| Trigger | weapon level < 99 and ABS.now (ceiled) >= ABS.max |
| ATK gain | level (before increment) < 5: +2 if ATK < 100 else +3. Level >= 5: +1. Capped at the weapon's per-weapon `status_max` |
| DUR gain | +1 per level, capped at per-weapon max |
| WHP max gain | random pick from {1,1,1,1,1,2,2,2,3,3} (mean 1.7), cap 255. Current WHP is raised to keep the same fill ratio if that exceeds the current value |
| Synthesis (fusion) points | + the weapon's `fusion_point` per level (cap 999; 9999 for the fishing rod) |
| ABS reset | ABS.now = 0; new ABS.max = base + (base/2) x oldLevel, with INTEGER division of base/2; base = per-weapon `levelup_exp`. So levels 0 and 1 both need base, level 2 needs 1.5 base, level 3 needs 2 base; level L (>=1) needs base + (base/2)(L-1). Cap 99999 |
| Level cap | 99 |
| Partner bonus | If the active partner id (1, 0xF, 0x10 or 0x1A) matches the weapon class (Max melee, Monica melee, Max gun, Monica magic respectively): +1 extra Synthesis point and +1 to one random attribute slot that is below its per-weapon cap. Slot = rand(17) % 8 (slot 0 slightly favoured, 3/17 vs 2/17), up to 128 tries, fallback = first slot with room. Who the partner ids are is (inferred) - the code only tests ids. |

Per-weapon data (`CDataWeapon`: durability, levelup_exp, status[2], status_max[2], attribute[8], attribute_max[8], special, fusion points, build-up targets) is in game data tables, not in source (see gaps).

### 2c. Build-up
Source: `userdata.cpp` `CGameDataUsed::IsBuildUp`, `CheckBuildUpMonsterCondition`; `ps2/src/menusys.cpp` `BuildUpWeaponTrans`. OpenCloud `userdata.cc` `IsBuildUp` shows the same 0.9 test.
- A weapon has up to 3 build-up targets (`buildup_weapon[3]`) and up to 3 required monsters (`buildup_monster[3]`).
- Stat condition per target: for each of the 9 values (ATK + 8 attributes), own value >= 0.9 x the TARGET's BASE (starting) value. DUR and level are not tested. Each target is tested independently, so several can be open at once.
- Monster condition: each required monster must have a kill count > 0 in the monster book (negative entry = no requirement).
- Transform: weapon becomes the target item; level reset to 0; ABS keeps the same fill ratio but ABS.max = the new weapon's base `levelup_exp`; ATK += 0.1 x target base ATK; each attribute += 0.1 x target base attribute (truncated to integer); special flags merged via the cancel table; per-weapon limits re-applied. WHP and DUR are not modified by this function (inferred from absence). The name is replaced only if the player had not renamed it. A save flag (0x31) is set.

### 2d. Spectrumizing (weapon/item -> attachment) and applying it
Source: `userdata.cpp` `CGameDataUsed::ToSpectolTrans`; `menusys.cpp` `AfterSpectolFusion`, `CheckSpectolFusion`. OpenCloud `userdata.cc` ~1498 confirms the 0.6 factor.

Creation:

| Source | Result attachment |
|--------|-------------------|
| Weapon level < 5 | Synthesis cost = rand 1-4; ONE random bonus of rand 1-4 placed in one of 10 slots (8 attributes or the 2 base stats; `rand(10)`, slots 8-9 = ATK/DUR); no special flags |
| Weapon level >= 5 | cost = min(level, 20); ATK x1.0; DUR = floor(0.6 x DUR); each attribute floor(0.6 x attr); special flags copied; remembers the level |
| Attachment item stack of n | every stat/attribute x n; cost = n; special flags copied |
| Fish | attribute slot 7 = 2, cost 1 |
| Other items | stats from a per-item table (`etcitem_spectol_table`, 0x1A9 entries); cost = count |

Attachment stats cap at 999 each (`CheckParamLimmit`).

Applying to a weapon (needs weapon Synthesis points >= attachment cost; not on fishing rods):
- Attachment that came from a weapon: new ATK = max(weaponATK, attachATK) + min(...)/4 (integer division). Any other attachment: ATK += attachATK.
- DUR += attach DUR; attribute[i] += attach attribute[i], then clamped to 0..attribute_max of that weapon.
- Special flags merged; some opposing pairs cancel (money up/down, wear up/down, and others; table in `CheckWeaponAttribute`).
- Synthesis points -= cost. The UI reports how many of the 10 stats rose.
So attachments act as one-shot permanent stat injections bounded by per-weapon caps, not live-equipped modifiers.

## 3. Durability (WHP)

Source: `maintex.cpp` `calcWeaponParamWhp`, `calcWeaponParam2`; `userdata.cpp` `GetWHp`, `Repair`, `CBattleCharaInfo::AddWhp`; OpenCloud `engine/dng_main.cc` same-named functions (agree).

| Case | Wear per event |
|------|----------------|
| Melee hit (Max/Monica sword, ridepod punch/sword) | w = monster.whp x 0.5; w -= w x (0.005 x DUR); x1.3 if wear-up flag (0x20); x0.8 if wear-down flag (0x40). Applied even if the hit does 0 damage. |
| Ranged shot (Max gun, Monica magic), per shot | w = 1 - 1 x (0.002 x DUR); same two flag multipliers; w /= the attack's hit_count (bursts/multi-bullets) |
| Reaching 0 WHP | Gauge clamps at 0. Weapon deals 0 damage (`SetDamageParam`). When a hit takes WHP from >0 to 0 the weapon loses 10% of its max ABS (`AddAbsRate(-0.1)`); ABS below 1 snaps to 0. |
| Repair | Repair kit (separate kits for melee, gun, magic) adds 999, i.e. full. Ridepod arm: half of max. A "repair all" script op restores all weapons (999). |
| Max WHP | grows with level-ups (+1..3); hard cap 255 |
| Other | Some item effects halve current WHP (OpenCloud `userdata.cc` ~4670). |

Per-monster melee wear `monster.whp` is a u16 in the monster table; actual values not in source.
DUR also sets Monica's magic-sword charge counter: 0 charges if DUR < 32, else min(7, (DUR - 32)/16 + 3). Guarded hits of an element add to it (needs DUR > 30).

## 4. Player stats and status effects

### 4a. HP and defence
Source: `userdata.cpp` `CUserDataManager::Initialize` (~2767-2805), `GetDefenceVol`; `menucls1.cpp` item-use (~1105-1140); `MOS_CHANGE_PARAM` (~2470-2560).

| Stat | Value |
|------|-------|
| Starting HP | Max 32, Monica 48 |
| Starting defence | Max 4, Monica 8 |
| Max HP growth | only Fruit of Eden: +8, cap 255. No HP per level. |
| Defence growth | Potato Pie (Max, needs his melee weapon equipped) / Witch Parfait (Monica): +4, cap 128. Clothing adds nothing (no code found) |
| Ridepod defence | body part defence + 4 per shield kit |
| Monica monster-form | ATK = floor(base x (1 + 2 x level/98)), cap 999; DEF = base + 2 x min(15, level/6), cap 999; levels 0..98; ABS needed = 100 + 100 x level (+25 x (level-49) when level > 49); class change every 25 levels, max 3 |
| Regen | Regen coin: +1 HP every 125 (PAL) / 150 (NTSC) steps |
| Test loadout | sets Max to 48 HP, 8 defence |

### 4b. Status effects on the player
Source: `actionchara.cpp` `CheckDamage` (application), `userdata.cpp` `CBattleCharaInfo::StatusParamStep` (ticking). Unit = game step. `PAL_TIMING(pal, ntsc)` picks the PAL value in the PAL build.

| Status | Chance (per damaging hit whose attack carries the flag) | Duration / tick |
|--------|-----------------------------------------------------------|-----------------|
| Poison | 30% unless Antidote Amulet | Until cured. Every 100 steps (PAL) / 120 (NTSC): lose ceil(2% of max HP), never below 1 HP |
| Slow | 30%, not in rain, unless Anti-goo Amulet | 3600 steps |
| Stone | 50% unless Anti-petrify Amulet | 900 steps; blocks acting/switching |
| Stop | 50% unless Non-stop Amulet | 300 steps; blocks acting/switching |
| Curse | 50% unless Anti-curse Amulet | no timer found; until cured |
| Dry (thirst) | 50% | no timer found; blocks eating |
| Power (buff) | item-applied | ATK x1.5; timer in `status_time[0]` |

### 4c. Status effects on monsters
Source: `monster.cpp` `CheckDamage`, `CActiveMonster::CheckStatusAttr`.
- Proc chance per damaging hit = monster.status_chance / attack hit_count (%), blocked by per-monster resist flags; only if D > 0.
- Poison: every 120 steps lose 4% of max life (leaves at least 1); no timer found (persists).
- Stop: 300 steps; Stone: 900 steps; each hit on a stopped/stoned monster cuts 90 steps off the timer; mutually exclusive.
- Slow (goo): 1800 steps.

## Design takeaways for Sleeping Calamity

Worth adapting:
- Subtract-then-scale damage with attributes multiplying post-defence damage: elemental and species play stays meaningful but cannot trivialise defence.
- A per-weapon ABS gauge that levels each weapon individually, fed by what you killed with (items split credit) - encourages using the weapon you want to grow.
- Weapon wear with DUR as a mitigation stat, wear driven by the enemy (tough monsters wear more), and "break = lose a slice of progress, not the weapon" - a low-frustration loss.
- Build-up as an unlock gate on stats and on having met certain monsters; reset level but keep progress percentage is a nice carrot.
- Spectrumize/attachments as one-shot, cost-limited stat transfers; the 60% decay makes sacrificing a weapon a real choice.
- Flat chip damage on the player so defence is never total immunity.

Change:
- DC2 hides combo and charge multipliers in script data; expose them as explicit data with a visible multiplier and keep the whole formula in one tested C# function.
- Use an explicit species/tag multiplier rather than reusing attribute slots, so "anti-undead" and "fire" are different concepts in the UI.
- Add a proper crit stat instead of a coin-only 10% x1.8.
- The `critical_rate` field doing guard duty is confusing; name fields for what they do.
- Poison/curse/thirst with no timers can punish; add cures or timers.
- Max HP and defence being item-only is slow and opaque; consider a small visible level-based component.
- Integer divisions (base/2 per level) and ceil rounding create quirky breakpoints; use a documented float curve.

## Gaps / REA questions (need the game binary or disc data)
1. Per-weapon data tables (`CDataWeapon`, `CDataAttach`): base ATK/DUR/attribute, caps, levelup_exp, fusion points, build-up targets and monster requirements. The source only has the structures.
2. Per-monster tables (`BASE_MONSTER_TBL`): life, attack, defense, whp wear, reward_exp, status_chance, guard_rate, ext_param[12], element_resist[8], ratio_damage_rate. Needed for real damage numbers and to confirm that element_resist 100 is neutral (inferred).
3. `Damage_Param_Table` rows (per-attack damage, hit_count, stun_time, stagger, guard multiplier, element seeds). The table is in `colprim.cpp` with Shift-JIS names; numbers deliberately not copied here.
4. Combo and charge multipliers: `power_rate` values and charge thresholds are in action scripts (`acumu_pad` in code), not C++. Need the Max/Monica action-script data.
5. What attributes 4-7 are called and how the UI presents them; confirm 0-3 = fire/ice/thunder/wind.
6. `etcitem_spectol_table` contents and fish-attachment behaviour; the CheckWeaponAttribute cancel pairs in full.
7. Whether poison/curse/thirst on the player expire anywhere else (probably cure items only).
8. Whether clothing/armour affects defence (none found), and what partner ids 1/0xF/0x10/0x1A are.
9. Real step rate (60 vs 50 Hz) for status timers.
10. Dark Cloud 1: neither source covers it; DC1 maths would need a separate REA of the DC1 binary.
