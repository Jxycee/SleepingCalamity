# Kingdom Hearts II Final Mix: whole-game sweep for useful systems

Phase 0 research, 2026-10-29. The user asked to "go through all of KH2 to determine if there is anything useful". Mechanics and numbers only, in our own words. No game code, text, tables or assets are reproduced; where KH2 has a table we describe its shape (level count, total, growth rate), not its rows.

This extends `kh-combat-camera.md`, which already covers combat windows, lock-on, camera, combos, hitstop, the revenge gauge, combat reaction commands, dodge/guard i-frames, MP charge, the enemy HP formula and basic party AI. Those are not repeated here.

Provenance tags (same as the combat note): **[doc]** OpenKH file-format doc (`C:\Dev\_research\OpenKh\docs\kh2\`), **[decomp]** symbol in the KH2FM WIP decomp (`C:\Dev\_research\kh2\functions.tsv`; cited only as proof a system exists, not of how it behaves), **[wiki]** khwiki.com, **[guide]** a commercial or fan guide, **[review]** press reviews.

Verdict key: **yes** = take it, **maybe** = later or with changes, **no** = skip.

---

## 1. Drive Forms and the drive gauge (top relevance: growth by use)

**How it works [wiki][doc].**
- Sora fuses with partners into five forms (FM adds a sixth, Limit Form, plus the random Anti Form). Each form has its own moveset and **its own level, 1 to 7**, stored in a form-level table (Fmlv: form id, level, ability granted at that level, EXP to next) [doc `00battle.md` Fmlv]. Summons share the same table [doc]; `YS::SUMMON_LEVEL::AddExp` exists [decomp].
- **Each form levels from a different verb**, earned while in that form (per common sources; the fetched wiki text did not state it) [wiki Drive_Form]:
  - Valor (strength form): +1 per hit landed on any enemy, even a hit that does nothing.
  - Wisdom (magic form): +1 per Heartless (one enemy family) defeated.
  - Limit (FM): +1 per limit-finisher reaction command used.
  - Master (all-round form): drive orbs picked up (small 1, big 3).
  - Final (strongest form): +1 per Nobody (the other enemy family) defeated.
- **Curve shape [wiki Valor_Form]:** about 2,200 to 2,500 EXP from level 1 to 7 (the wiki lists two sets; which one is FM is unconfirmed). The cost per level grows from under 100 to several hundred, roughly 1.2 to 1.5x the previous step. In hits, that is a long but visible grind that finishes around the end of the story.
- **Level cap tied to story:** the max form level starts at 3 and rises by 1 per new form unlocked; the final form lifts every form's cap to 7 [wiki].
- **Each level gives** +1 form-gauge bar (duration) and an ability: a growth ability (see 2), a combat ability, or an "Auto-form" reaction command. Some rewards apply to **base Sora, not just the form**: Valor's levels give base Sora Combo Plus twice [wiki].
- **Drive gauge [wiki Drive_Gauge]:** 3 bars at start, up to 7 (base) or 9 (FM), raised only by boss and superboss rewards. It fills by dealing damage, drive orbs, items, and faster during MP charge (`YS::SHEET::mpdrive` [decomp]). Costs: two forms 3 bars, two forms 4, the final form 5, a summon 3. It refills fully on reaching a safe room or a room with the party.
- **Form gauge while transformed:** 3 bars +1 per form level (max 9). Each bar lasts 10 s, half speed out of combat; a support ability multiplies drain by 0.8 per copy (0.8 / 0.64 / 0.512). Attacks also drain it by a per-attack "form value", only 1/5 of that against normal enemies (so boss fights burn forms faster). Drive orbs add 0.2 s / 0.6 s.
- **Anti Form [wiki Antiform]:** a hidden counter: +1 per form use, -10 per final-form use, -4 per Anti. Chance of turning into the weak, uncontrollable Anti form: 0% at 0-4 points, 10% at 5-9, 25% at 10+. Multipliers for important fights (x2 scripted, x4 rival-boss fights, x10 the last boss), x0 when certain allies are present. `YS::PARTY::is_drive_berserk` [decomp].

**Useful for us? YES, the single most useful KH2 system.** It is weapon growth by use, already in our P15 rules, but KH2 shows two upgrades:
1. **Different growth verbs per weapon.** Our heirlooms already have different jobs (PHASE1 6.6). Let each one gain a share of its experience from its own verb on top of the shared kill ABS: the axe from finishers and guard breaks, the dagger from hits landed and parries, the staff from multi-target sweeps (enemies hit per swing), the bow from weak-point and long-range hits. This turns "use the weapon" into "play the weapon's way" without fodder.
2. **Growth that leaks back to the base character.** Form levels unlock abilities that base Sora keeps one tier weaker. For us: a weapon's growth milestones unlock a hero ability that stays when the hero switches weapon (the "weapon bond" becomes permanent mastery, the way DC2 did with weapon skills but visibly).
- **Keep the verbs off kill credit.** KH2's family-kill verbs would bring back last-hit competition, which P12's shared credit removes; use hit, finisher, parry and multi-hit verbs only.
- **Adapt:** host keeps per-weapon counters per player; verbs are tags on hit/kill events the host already resolves (PHASE1 6.4), so no new trust. Per-level costs grow ~1.25x per level and show on the weapon tree as a bar with the verb named ("Finishers: 41/60"). Keep **level caps tied to story progress**, as KH2 does: cheap protection against over-grinding one weapon early.
- **Anti Form: NO** as a hidden random punishment (breaks "visible systems"). If we want a risk mechanic, make it visible: the stir meter already plays that role.
- **Drive gauge refill on reaching a safe room: YES.** Matches our checkpoint and healing-point rule; any "special gauge" resets at checkpoints so players never hoard.

## 2. Growth abilities: movement unlocked by progression

**How it works [wiki Growth_ability][doc].** Five movement abilities, one per form, each with levels 1-3 plus a MAX only the form itself has: High Jump (hold jump for more height), Quick Run (dash; hold to extend), Dodge Roll (FM; i-frames and reflects projectiles), Aerial Dodge (a mid-air jump that also blocks), Glide (hold to glide; speed rises per level). Base Sora gets level N when the form reaches level 3/5/7. They cost 2 or 3 AP per level, so players can turn them off. Character jump values live in the player data [doc `preferences.md`]. KH2 used them only lightly for optional chests; FM's Cavern of Remembrance (section 9) is built around them.

**Useful? YES, with care.** It gives exploration a progression axis beyond stats, and it is the cheapest way to make revisited dungeon floors feel different ("I can reach that ledge now").
- **Adapt:** a small set of hero traversal tools, each tied to a hero or a weapon milestone (a climbing hook, a long hop, a glide cloak, a dash). Rooms in the pool carry **traversal tags** ("needs dash", "needs glide") on optional branches only; the floor generator never puts the critical path behind a tool the party lacks. In co-op, one player with the tool can open a shortcut (a ladder, a rope) for everyone, so the party isn't blocked by its weakest member.
- **Watch out:** our floors are generated, so traversal gates must be authored into room tags, not placed by hand per floor.

## 3. Limits, party limits, summons

**How it works [wiki Limit][doc].**
- **Party limit:** spends all of Sora's MP (MP charge frozen until it ends); the third party member vanishes; Sora and the partner are briefly invincible; a limit gauge runs while the player chains attack or reaction-command presses; a final hit unlocks after enough earlier hits; a "Stop" command ends it early. Each partner has one or two, all 3 AP. A support ability lets an AI partner offer its limit as a reaction command [wiki]. Data: limit table (character, summon, "group 3 needs all characters alive", command, spawned object) [doc `00battle.md` Limt]; `YS::LIMIT::reset_hit_counter`, `YS::FRIEND::can_limit` [decomp].
- **FM Limit Form:** four limits on face buttons at a fixed 60-80 MP each instead of all MP [wiki].
- **Summons:** cost 3 drive bars, replace both partners, have their own level (1-7) and their own limits paid from a summon gauge [wiki][doc Sumn, Fmlv].

**Useful? MAYBE (team attacks) / NO (summons).**
- A KH2 limit removes a party member, which is fine with AI partners and **wrong for co-op**: you can't make a friend vanish.
- **Adapt as an opt-in team attack:** player A starts a team move from a shared co-op gauge (fills from everyone's hits); nearby players get a prompt; each player who joins presses their own inputs during the limit gauge and adds a hit to the chain; more joiners unlock the finisher. Nobody is forced out. Solo uses an AI-free version (the hero's own super). The limit-gauge idea ("press during a timed window, finisher after N hits") is reusable as is. Post-Phase 1.
- **Summons: NO.** They replace the party; in co-op that means taking control away from friends.

## 4. Level-up, stats and the "route" choice

**How it works [doc `00battle.md` Lvup, Plrp, Bons][wiki].**
- 99 levels per character. Each level row holds EXP to next, strength, magic, defense, AP, and **three ability columns**: which one Sora gets depends on a sword/shield/staff choice made at the very start of the game (also shifting at which levels he gains things).
- **HP, MP, drive bars, and item/accessory/armour slots do not come from levels.** They come from **"bonus levels"**: a per-event reward table that pays out after bosses and set fights (HP +5, MP +10, a slot, a drive bar, or an ability) [doc Bons]. So max HP growth is tied to story beats, not grinding.
- World difficulty: each world's enemy level is the **sum of the progress-flagged entries** that are on (Btlv) [doc]. A world gets stronger as story flags flip, so revisits automatically scale.

**Useful?**
- **Bonus levels: YES.** Rewarding a boss or a rescue with a permanent capacity bump (an attachment slot, a carry slot, a gauge bar) is cleaner than XP. For us: each rescued resident, set-piece floor or optional boss grants one named capacity bump, shown in the journal.
- **Btlv-style band difficulty: YES.** Enemy level per dungeon band = sum of active progress entries. Cheap, data-driven, and makes revisits stay relevant. Fits our tier rule (6.5) and co-op scaling (P23) as separate multipliers.
- **Early irreversible route choice: NO** as is (dark-cloud-lessons: no irreversible choices). If we ever want a "what kind of hero are you" choice, make it re-routable like build-ups (P14).

## 5. Abilities and AP

**How it works [wiki Ability_Points][wiki].** Abilities are items you equip against an AP budget. Types: action (new moves: finishers, aerial moves, reaction-command triggers), growth (movement, section 2), support (passives: more drops, MP haste, combo changes, "lucky" drops, damage control). AP rises with levels and permanent AP-boost items, cap 199. Equipping and unequipping is free and instant at any time from the menu. Negative abilities exist (fewer combo hits) and players use them by choice.

**Useful? YES, as the shape of our weapon attachments.** A free-to-swap budget with no penalty encourages experiments; negative abilities that help a style ("shorter combo, faster finisher") give experts a toy.
- **Adapt:** weapons carry an attachment budget; attachments can be removed freely until absorbed at level-up (P13). Consider letting players toggle off learned movement or combo abilities (KH2 lets you disable your own growth abilities; speedrunners and challenge players rely on it).

## 6. Item synthesis (Moogle shop)

**How it works [doc `mixdata.md`][guide gamerguides].**
- Recipes (found as items) list up to six ingredients with counts and a rank C/B/A/S. Some recipes start locked behind **free development** levels 1-3 [doc].
- **The shop levels up from synthesis EXP**, which is the sum of the ingredients' rank EXP [doc MIEX]. Nine levels, 1,980 EXP total; cost per level grows from 40 to 650 [guide]. Levels unlock new materials, **halve the material counts for a recipe rank**, and at level 8 open free development. The guide notes that **making one of every recipe lands exactly at max level**: the curve was tuned to the content.
- **Collector's goals** [doc MICO]: "hand over N of a material type/rank" or "collect N unique materials", rewarding an item or a shop upgrade.
- Materials come in four tiers per element family (shard, stone, gem, crystal), dropped by enemies and found in chests. Some recipes have a "+" version using one extra rare material. The ultimate weapon is a rank-S recipe of 20 units across six ingredients.

**Useful? YES (post-Phase 1, synthesis is out of Phase 1).**
- A crafter NPC who **levels with what you bring**, halves costs as they level, and unlocks experimental crafts later, fits a rebuilt town perfectly: a town resident's shop grows with the town.
- "Make one of everything lands exactly at max" is a good tuning rule: the crafter's curve is sized to the recipe list, so completionists never grind.
- Collector's goals solve the "materials pile up with no use" problem. Name targeted sources in the journal (dark-cloud-lessons).
- **Avoid:** KH2's random recipe drops and obscure material sources needed guides. Every recipe shows where its materials come from.

## 7. Optional superbosses and challenge content (FM)

**How it works [wiki Absent_Silhouette][wiki Lingering_Will][wiki cups].**
- **Absent Silhouettes:** five optional rematches placed as portals inside story worlds after certain beats. Each pays a rare material, a recipe, a bonus level, and **a permanent capacity reward per character** (a slot, +1 drive bar, MP, HP). Beating one unlocks a harder "data" version at a hub reached through the optional cavern [wiki].
- **Data rematches and Lingering Will:** much harder, repeatable fights against story bosses at a central terminal. Lingering Will (revenge cap 25, see the combat note) is the reference superboss: dense combos with readable gaps, a guardable projectile that can be reflected, a telegraphed berserk state (a ground rune and a colour change), and a "wrong choice kills" grab. The FM additions answered the base game's "too easy" reviews [review].
- **Coliseum cups:** 10-round gauntlets, each with a **rule twist**: solo only with a damage-point counter that fails you at zero, a restricted drive gauge with cheaper limits, a faster gauge for solo play. A 50-round "paradox" cup cycles the earlier rule sets every 10 rounds with a boss every tenth [wiki Hades_Paradox_Cup].

**Useful? YES.**
- **Rule-twist challenge floors**: a dungeon side door (or a "back floor", see the DC1 notes) runs a short gauntlet with one rule: no healing points, one weapon only, a shared damage budget for the party, no lock-on. Cheap content from existing rooms and enemies; great for co-op.
- **Superboss rematches that pay permanent capacity**, not just loot (fits "side content feeds the main loop"). Optional, placed after the story beat, with an instant retry.
- **Readable superboss design rules** we take from Lingering Will: every fast string has a gap; the strongest attacks have counterplay (guard reflects the beam); phase changes are telegraphed by both a visual and a ground marker.

## 8. Enemy design: two families, telegraphs

**How it works [wiki Nobody][wiki].** Heartless are instinctive swarmers with varied silhouettes and bright eyes; Nobodies are grey, agile, "strategic", teleport and dodge, and share one emblem. Each family drives a different form's growth (section 1). Per-group reaction immunities and super-armour windows are data [doc, see the combat note]. Data-driven drop tables: each enemy has counts of HP, MP, money and drive orbs plus up to three item drops with a percentage (Przt) [doc]; `YS::PRIZETABLE`, `trap_prize_vacuum` [decomp].

**Useful? YES.**
- **Two or three enemy families per dungeon with one readable trait each** (colour, emblem, movement style), so players learn "family X = dodge, family Y = swarm". Combined with growth verbs, families also steer which weapon a player brings.
- **Drop tables with orb counts plus three item slots** is the right shape for our enemy data; keep it per species and tier.
- **Orb auto-vacuum** after a fight (KH2 has prize vacuum hooks): pull dropped orbs to players after a fight ends, so nobody walks back for pickups. In co-op, every player gets their own copy of HP/MP orbs (host spawns per player).

## 9. Cavern of Remembrance (FM optional dungeon) vs our dives

**How it works [wiki Cavern_of_Remembrance].** An optional multi-area dungeon off a late hub. Areas: a cave with a ledge needing high jump or aerial dodge; a vertical mineshaft crossed by pipes whose openings need **max-level** growth abilities (a coloured orb beside each opening shows which one); an engine area where switching on machines at the bottom opens the way; conveyor and steam-jet rooms; a final glide section; a hall of three wave fights; then the superboss terminal. A hidden passage loops back to the hub save point as a shortcut. Few saves inside, long first traversal, trivial repeat traversal.

**Compare to our dives.** The cavern is a hand-made traversal test that unlocks as you grow; our floors are generated from room pools for repetition. Useful pieces:
- **Colour-coded hints at each gate** ("this opening needs the glide") so the player knows what to come back with. **YES.**
- **One-way shortcut from the deep end back to the hub**: matches our "shortcuts back to town" rule. **YES.**
- **Wave-fight hallway before the boss** as a set-piece floor type. **YES** (our floor 4 set-piece can use it).
- **Long first traversal with no checkpoints: NO** (dark-cloud-lessons).

## 10. Reaction commands beyond combat (environment use)

**How it works [doc `03system.md` Cmd][wiki].** Commands are data entries with flags (cursor, land-only, battle-only, "require", drive-only, per-form disables), a camera mode (watch, lock-on, both), a receiver (player, target, both) and an action. Reaction commands use the same table, so one contextual button handles talking, opening, pushing objects, riding things, throwing debris, launching off enemies, and world-specific gimmicks. A table (Rcct) of unknown use sits beside it [doc].

**Useful? YES.** One contextual button driven by a data table (prompt name, valid states, camera mode, who it affects) covers chests, levers, carrying town pieces, reviving a downed ally and boss counters. **Adapt:** the prompt goes to the nearest eligible player; in co-op, a "both players" receiver flag allows two-person actions (lift a portcullis together). **Avoid KH2's flaw:** pressing early is free and the prompt does the work, so players mash the button [review RPGFan]. For combat prompts, require timing or a risk; for environment prompts, mashing is fine.

## 11. Minor systems (looked at lightly)

| System | How it works (short) | Useful? | Note |
|---|---|---|---|
| World structure and revisits | Each Disney world is visited twice; the second visit opens new rooms and has higher enemy levels (Btlv sum) | **maybe** | Revisit with new story flags fits our dungeon bands; don't re-run the same path |
| Gummi ship | Optional rail-shooter missions between worlds, scored, with ship building | **no** | Called a "thin mechanic" in reviews [review Pixel Empire]; not our genre |
| Minigames | Many per-world minigames with scored missions (`14mission.md`, `MISSION_SCORE`) [doc][decomp]; FM made the 100 Acre ones harder | **maybe** | Scored side activities only if they feed materials or town wishes (dark-cloud-lessons) |
| Puzzle pieces (FM) | Crowns hidden in rooms give pieces of picture puzzles; completing a picture gives a reward [doc `15jigsaw.md`] | **maybe** | A per-dungeon "relic set" collectible, tracked in the journal, reward on completion |
| Jiminy's journal | Sub-files: worlds, story, character files, enemy list, limit/minigame records, quests, puzzles, report collection; a completion % [doc `jiminy.md`] | **yes** | Our journal (post-Phase 1) should mirror this: every system, every collectible, a tracker so nothing is missable |
| Menus and shortcuts | Command menu, 4 magic shortcuts on a held button, item slots per character | **maybe** | Quick-slots for consumables; we don't need a command menu for melee |
| Party AI settings | Per-partner behaviour sliders and ability toggles | **no** | We have no AI partners; co-op is players |
| Critical mode (FM) | Damage taken x2, damage dealt x1.25, bonus-level HP/MP gains smaller, 75% EXP, but the game **starts with 50 AP, gains +3 per AP increase instead of +2, and grants extra starting abilities** [wiki Critical_Mode] | **yes** | A hard mode that gives you more tools while hurting more is better than pure stat inflation |
| Magic | Each spell has three tiers raised by story items, not by use; MP charge is in the combat note; a spell cast as a finisher plays its own motion (Magc) [doc] | **maybe** | Wren's staff spells could tier up through build-ups instead of story gates |
| Skateboard / riding | Board in town hubs, hits gave form EXP in JP | **no** | Out of scope |

**Final Mix additions, where covered:** Limit Form and the Dodge Roll growth ability (1, 2, 3), the Cavern of Remembrance (9), Absent Silhouettes, data rematches and Lingering Will (7), Critical mode and puzzle pieces (11), the Paradox cups (7), harder 100 Acre minigames (11). Together they were FM's answer to the "too easy" complaint: harder optional content and a harder mode, not a harder story.

## 12. What players disliked (base KH2)

- **Too easy, mashable** [review RPGamer, Retroware]: even the higher difficulty felt light. FM's answer was optional superbosses and Critical mode, not a harder story.
- **Reaction commands do the work** [review RPGFan]: early presses carry no penalty.
- **Little reason to use drive forms** beyond spectacle [review RPGamer]: forms are temporary and gated behind a gauge, so players forget them. (Growth by use and permanent leak-back to base Sora fixed some of this.)
- **Linear worlds**, less platforming than KH1 [review]; side content scattered and needing guides (material sources, Anti points, synthesis goals).
- **Gummi ship** felt bolted on [review].

---

## Top 10 ideas worth taking (ranked)

1. **Per-weapon growth verbs.** Each heirloom gains part of its experience from its own playstyle (axe finishers, dagger hits and parries, staff multi-hits, bow weak-point shots), shown on the weapon tree. From Drive Form EXP.
2. **Growth that leaks back to the hero.** Weapon milestones unlock a hero ability that stays when switching weapons (a weaker tier than on the weapon). From form levels feeding base Sora.
3. **Traversal tools unlocked by progression, tagged in room pools.** Optional branches need a tool; one player with it opens a shortcut for the party; colour-coded gate hints. From growth abilities and the Cavern.
4. **Bonus levels: permanent capacity from story beats and optional bosses** (a slot, a gauge bar, max HP), listed in the journal. From the bonus table and Absent Silhouettes.
5. **Rule-twist challenge gauntlets** built from existing rooms (no healing, one weapon, shared damage budget), good for co-op. From the Coliseum cups.
6. **Band difficulty as a sum of progress entries.** Revisited bands rise as the story advances; a separate multiplier from co-op scaling. From Btlv.
7. **A crafter who levels with what you bring**, halves costs as they level, and whose curve is sized so making each recipe once reaches max; collector's goals for spare materials. From the Moogle shop.
8. **One contextual button from a data table** (prompt, valid states, camera, receiver incl. "both players"), timing-gated in combat. From the command table.
9. **Optional superboss rematches with readable rules** (every string has a gap, strongest attack has counterplay, phase changes telegraphed by visual plus ground marker), instant retry. From Lingering Will.
10. **Hard mode that adds tools as well as pain** (more starting attachment budget plus higher incoming damage). From Critical mode.

## Things to avoid

- **Hidden random punishments** (Anti Points): any risk system must be visible (our stir meter).
- **Irreversible early build choices** (the sword/shield/staff route).
- **Mechanics that remove a player** (party limits and summons dropping a partner).
- **Prompts that reward mashing** in combat.
- **Temporary power players forget to use** (forms behind a gauge with no reason to engage); growth must be permanent and visible.
- **Content that needs a guide**: random recipe drops, opaque material sources, unexplained counters.
- **Long optional dungeons with no checkpoints**, and bolted-on genres (Gummi ship).

## Gaps

- The two Valor EXP sets on the wiki are not labelled; which is FM is unconfirmed. Exact per-level rows for the other forms, the summon levels and the bonus-level table were not pulled (and would not be copied).
- No disc extraction and no Ghidra work this pass: the form, synthesis and drop tables could be read from KH2.IDX/IMG with OpenKH tools if exact shapes matter later.
- Drive gauge fill per hit, limit-gauge length and summon gauge costs not found.
- Gummi ship, minigames, menus and world revisits were looked at lightly; no numbers are claimed for them.
- Synthesis EXP per material rank: fan sources disagree; only the moogle curve shape is used.

## Sources

- OpenKH docs: `docs/kh2/file/type/00battle.md` (Fmlv, Lvup, Bons, Btlv, Plrp, Limt, Sumn, Przt), `03system.md` (Cmd, Rcct), `mixdata.md`, `jiminy.md`, `15jigsaw.md`, `14mission.md` - https://openkh.dev
- KH2FM decomp symbols (`C:\Dev\_research\kh2\functions.tsv`): `YS::SUMMON_LEVEL::AddExp`, `YS::SHEET::mpdrive`, `YS::SHEET::start_drive`, `YS::LIMIT::reset_hit_counter`, `YS::FRIEND::can_limit`, `YS::PARTY::is_drive_berserk`, `YS::PRIZETABLE`, `YS::trap_prize_vacuum_ca`, `YS::JIGSAW::can_get`, `YS::MISSION_SCORE` - https://github.com/govanify/kh2
- khwiki: https://www.khwiki.com/Drive_Form, /Drive_Gauge, /Valor_Form, /Antiform, /Growth_ability, /Ability_Points, /Limit, /Critical_Mode, /Cavern_of_Remembrance, /Absent_Silhouette, /Lingering_Will, /Nobody, /Hades_Paradox_Cup, https://khwiki.com/Cerberus_Cup, https://khwiki.com/Goddess_of_Fate_Cup
- Fandom: https://kingdomhearts.fandom.com/wiki/Titan_Cup, https://kingdomhearts.fandom.com/wiki/Pain_%26_Panic_Paradox_Cup
- Synthesis guide: https://www.gamerguides.com/kingdom-hearts-hd-25-remix/guide/kingdom-hearts-ii-final-mix/side-quests/item-synthesis
- Reviews: https://archive.rpgamer.com/games/kh/kh2/reviews/kh2strev6.html, https://www.rpgfan.com/review/kingdom-hearts-ii/, https://articles.retroware.com/2021/03/09/the-history-of-kingdom-hearts-part-2-the-rise-of-the-series-and-a-contentious-end-to-a-saga/, https://www.thepixelempire.net/kingdom-hearts-ii-ps2-review.html
