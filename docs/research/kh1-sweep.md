# Kingdom Hearts 1: whole-game systems sweep

Phase 0 research, 2026-10-29. Covers the systems the user's own KH1 (USA) and its Final Mix (FM, in the HD remasters) contain beyond combat. Mechanics and numbers only, in our own words. No game code, text, data tables, art or audio is reproduced: a table is summarised by its shape plus two or three sample numbers.

**Already covered, not repeated here:** lock-on, camera, combo counting, Guard, Dodge Roll, revenge, i-frames, hitstop and the damage-formula claims are in `docs/research/kh-combat-camera.md`. This file extends that one.

Provenance tags (same as that note): **[wiki]** = khwiki.com page, **[guide]** = a commercial or fan walkthrough, **[forum]** = community claim, **[gen]** = widely known about the game but not checked against a source this session (treat as unverified), **[doc]** = OpenKH docs.

## Method and gaps
- **No KH1 decomp exists.** GitHub has decomps only for KH2FM, Chain of Memories (GBA) and 358/2 Days (DS), which matches `kh-combat-camera.md`.
- **OpenKH's KH1 pages** (`C:\Dev\_research\OpenKh\docs\kh1\`) cover only file types and name dictionaries (models, weapons, items, commands). They hold no mechanics data [doc].
- **Ghidra and the ISO were not used.** The wikis answered the design questions, and the missing numbers (the MP charge rate, the EXP value of Tech Points, summon durations) don't change any decision. If a number becomes load-bearing, the next step is a PCSX2 RAM watch, not the stripped ELF.
- **Conflicts found:**
  - Starting stats for the dream-weapon pairs differ between Giant Bomb and the khwiki walkthrough table. We used the khwiki one.
  - The Hades Cup unlock reads "defeat Behemoth" in one walkthrough and "seal the Hollow Bastion keyhole" in another.
  - Pages disagree on Ultima Weapon's strength (12 vs 14) between the original and FM.

---

## 1. Party AI and the Customize menu
- **How it works:**
  - Donald and Goofy are AI allies. A Customize screen sets per-ally frequency sliders for regular attacks, offensive magic, defensive magic and advanced magic, plus "when to use" thresholds for HP and MP items [forum]. Abilities are equipped per ally from their own AP pool [wiki].
  - Allies get KO'd and revive on a timer: Donald 80 s, Goofy 60 s in FM [wiki].
  - Allies only use items placed in their own slots. Players complained the AI burns potions and ethers early, so speedrunners strip allies' items [forum].
  - Each world swaps one ally for a local guest (Tarzan, Aladdin and others) [gen].
- **Useful for us? No.** Phase 1 has no AI companions: each hero plays alone, and co-op uses humans (PHASE1 1, dark-cloud-lessons "Combat and controls"). Two small ideas survive:
  - **Timed self-revive:** in solo, a downed hero could get one timed "second wind" per dive. FM's 60-80 s timers are far too long; ours would be about 5 s, if used at all.
  - **Ally-only item slots:** in co-op, a "give item to ally" action could replace KH's ally slots, but it isn't needed in Phase 1.

## 2. Abilities and AP
- **How it works [wiki]:**
  - Every ability costs AP to equip (1 to 5). Sora's max AP grows on level-up and with AP-Up items. Abilities can be toggled on and off freely.
  - There are three kinds:
    - **Action abilities** change your moveset: finishers, rush moves, Guard, Dodge Roll.
    - **Support abilities** are passives: Scan shows enemy HP, Treasure Magnet pulls drops in, Second Chance survives a lethal hit at 1 HP, Berserk raises attack at low HP, Jackpot and Lucky Strike improve drops, MP Haste and MP Rage speed MP gain, and Leaf Bracer stops hits from interrupting a Cure.
    - **Shared movement abilities** (High Jump, Glide, Mermaid Kick, Superglide) are free once earned, apply to the whole party, and come from world bosses or keyholes.
  - Equipping a stacking ability more than once stacks it (Combo Plus is covered in the combat note).
  - Some support abilities are "boons by opting out": EXP Zero (Proud mode only) blocks all EXP, and Encounter Plus raises the spawn rate.
  - FM retuned many costs, e.g. Trinity Limit 5 → 1 AP and Second Chance 2 → 5 AP [wiki]. The point to note: costs are a tuning dial, and they were revised after launch.
- **Useful for us? Maybe; the idea, not the AP pool.**
  - Our build layer is the weapon (attachments, build-up), so a second, character-level AP pool would double the menu load.
  - Two parts are worth taking:
    - **Movement unlocks as permanent, shared keys** (the High Jump / Glide model). A dungeon band's reward could be a traversal skill that opens shortcuts and secrets in earlier floors and towns, which fits dark-cloud-lessons' "shortcuts back to town".
    - **Opt-out boons for challenge players:** an EXP-Zero-style "no weapon growth" toggle, or a higher spawn rate for farming.
  - **Adapt:** weapon attachments already play the role of support abilities. Name a few classic passives as attachment effects: drop magnet, last-stand at 1 HP once per floor, low-HP attack bonus.

## 3. Level-up, stat growth and the dream-weapon choice
- **How it works [wiki]:**
  - At the start the player keeps one of three dream weapons (sword = power, shield = defence, rod = magic) and gives up another.
  - The kept weapon mostly **reorders the ability unlock list**. For example, the level-9 unlock is Combo Plus for the sword path, Scan for the rod and Guard for the shield. The rod path delays Guard until about mid-game.
  - The given-up weapon costs a small amount of one stat (about 2 STR, 2 AP or 1-2 DEF). The kept weapon nudges end stats: the rod gives +2 max MP by the end, the shield +1-2 item slots.
  - Max level is 100. HP gain is identical on every path (about 0.7 per level).
  - Three answers to a short quiz set the **EXP curve**:
    - The early curve is cheaper levels early and dearer late (level 2 costs 14 EXP vs 24 on the late curve).
    - The late curve is the reverse, and a middle curve sits between them.
    - All three curves converge on the same high-level total.
- **Useful for us? Yes, as onboarding.**
  - A cheap, playable "who are you" moment that tunes the first hours without locking power. Players remember it, and it changes little.
  - **Adapt:**
    - Each heirloom's first build-up could offer a two-way "lean": which branch of the weapon tree previews first, plus a small stat tilt. Re-routable at the workbench (P14), so it is never a trap.
    - Skip the hidden quiz-to-EXP-curve link. A hidden, permanent effect breaks our "no irreversible bad choice, show the preview" rule (dark-cloud-lessons).

## 4. Magic and MP
- **How it works:**
  - MP is a handful of **whole pips**: Sora has 2-3 at the start and about 8-10 by the end, up to the high teens with gear [wiki].
  - Each spell costs whole pips. Hitting enemies and collecting MP orbs fills a **charge gauge**, and each full gauge returns one pip [guide]. Ethers and save points also refill MP [wiki].
  - In KH1, max MP itself sets spell power (Cure's heal, Aero's duration). Later games split this into a separate Magic stat [wiki].
  - Spells level up when you collect gems or win cups (Fire → Fira → Firaga) [gen].
  - Three spells can be bound to quick shortcuts [gen].
  - FM's MP Rage turns 1/5 of the damage you take into MP [wiki].
  - Contrast with KH2: one bar that, once empty, recharges over time (in the combat note).
- **Useful for us? Yes, for Wren's staff and for any active skill.**
  - Pips plus a charge gauge fed by melee hits rewards fighting with spells woven in, not mashing then casting. It also reads at a glance in co-op.
  - **Adapt:**
    - Give the staff 3 "charge pips" that fill from landed hits (start at about 4 hits per pip, tuned in M3).
    - A heavy cast spends 1-3 pips.
    - Weapon growth raises the pip cap (a visible weapon-tree reward).
    - Avoid KH1's "max MP = spell power" coupling; keep power on the weapon's ATK and element.

## 5. Summons
- **How it works [wiki]:**
  - A summon needs **both allies present and conscious**. It replaces them for its duration and drains a summon MP bar until dismissed or empty.
  - Summons are earned as gems found in worlds and unlocked at a hub NPC.
  - The Cheer ability lengthens the bar.
  - Summons can't be called while flying or swimming.
- **Useful for us? No for Phase 1, maybe later.**
  - "The whole party must be up" is a nice co-op gate, but it fails solo.
  - Later idea: a hero's "heirloom awakening" that every present player triggers together, with a solo version on a longer charge.

## 6. Team attacks and Trinity Limit
- **How it works [wiki]:** Trinity Limit is a special move that spends all remaining party MP on one big attack. Its cost fell from 5 to 1 AP in FM. It is the only true combined attack in KH1.
- **Useful for us? Maybe (post-Phase 1).**
  - A co-op finisher fed by everyone's pips is a strong co-op moment.
  - **Adapt:** when 2+ players hit the same staggered (revenge-broken) elite within a short window, a joint finisher prompt opens. In solo the prompt opens on the stagger alone. Park it.

## 7. Item synthesis (the moogle workshop)
- **How it works [wiki]:**
  - The workshop sits in the hub, behind a co-op Trinity Mark.
  - Recipes come in lists. Each list unlocks after **N unique items made** (3, 9, 15, then all 24; FM uses six lists of six with gates at 3/9/15/21/30). The last list holds the best weapon, craftable once.
  - Materials are elemental shards, gems and stones dropped by specific enemies. Lucky Strike improves rare drops.
  - Crafting a set number of unique items also grants an ability (FM: Encounter Plus at 15).
  - Players praised the hunt and complained about the grind for rare materials [forum: RPGFan retro panel].
- **Useful for us? Yes, the unlock rule.** Synthesis is out of Phase 1 (PHASE1 1), but when it lands:
  - **Unlock new recipes by variety made, not by quantity**, which pushes players to try the whole list.
  - **Name every material's source in the journal** (dark-cloud-lessons: targeted sources, not fodder farming).
  - Make the endgame weapon a synthesis capstone that also counts as a weapon-tree build-up target.

## 8. Keyblades and keychains (weapon design)
- **How it works:**
  - One base weapon. Each **keychain** swaps the blade's look and stats (strength, MP bonus, crit rate, reach, recoil) [wiki].
  - Keychains are mostly a world's completion reward, themed on that world.
  - Trade-offs are small and legible:
    - a long-reach blade with a high crit rate but lower strength;
    - a blade with +2 MP;
    - a top-strength blade with -1 MP [guide].
  - Reach and recoil are hidden stats, and the wiki calls recoil "negligible" [wiki].
- **Useful for us? Yes, as a reference for a "theme per area" weapon reward.**
  - Our weapons grow rather than swap, but each town or dungeon band can grant an attachment or build-up target themed on that place.
  - **Adapt:** show reach and swing speed on the weapon card. KH1 hid them, and players learned them from wikis.

## 9. World design, hub and travel (Gummi ship)
- **How it works:**
  - A hub town (Traverse Town) holds the shops, synthesis, the summon unlock, the puppy-reward house and Merlin's magic [wiki].
  - Each Disney world is a set of linked rooms, closed by a boss and a **keyhole** seal (the "world complete" beat). Keyholes unlock new shared abilities and Trinity colours [wiki].
  - Several worlds can be done in any order mid-game [gen].
  - Save points sit about every 3-4 rooms. They heal fully, link to the ship, and can't be used with enemies near [wiki].
  - **Gummi ship [wiki]:**
    - Travel is an on-rails shooter between worlds, until a Warp-G for that route lets you teleport.
    - Ships are built from blocks in a garage, within a block-count limit.
    - Critics called the shooter slow and pointless and the building fun [guide][forum].
    - KH2 rebuilt it as scored missions with medals, unlocking the warp after the first clear [wiki].
- **Useful for us? The hub yes, the Gummi no.**
  - A single hub where every return pays out (claim milestone rewards, craft, rebuild) is already our town, and KH1 shows that it works.
  - Save points that heal and won't work with enemies nearby are our checkpoint rooms.
  - **Adapt:** travel between towns and dungeon bands is instant once reached (a warp at the town gate). If we ever add a travel activity, it's optional and pays into the main loop (dark-cloud-lessons: side content must feed the loop), never a forced gate.

## 10. Trinity Marks and exploration secrets
- **How it works [wiki]:**
  - Floor glyphs come in five colours, 46 marks in total.
  - Each colour is a different **party action**: a group jump shakes loose treasure, a group charge breaks a wall, a party ladder climbs a ledge, a group push moves a heavy object, and a group detect summons a chest.
  - Each colour unlocks at a story beat. Marks need **Sora with both Donald and Goofy in the party**.
  - Rewards are items, munny, puppies and shortcuts. A few marks are needed for the story; most are optional.
  - The journal counts uses, which helps completion.
  - One original-game mark became unreachable after a boss; FM moved it so nothing is missable [wiki].
- **Useful for us? Yes. The best co-op exploration idea in the game.**
  - **Adapt:** "hearth marks" in towns and dungeon rooms. Each colour asks the players present for a joint action (two heroes push, lift or brace).
  - **Rule:** solo and smaller groups must be able to trigger every mark, with a longer channel or a hero-specific tool, because solo worlds exist and difficulty scales to the players present (CLAUDE.md save rules). Co-op is faster and more fun, never the only way.
  - Colours unlock as the story opens, so early floors hold visible locks to come back to (fits "shortcuts back to town").
  - Nothing goes missable (FM's fix).

## 11. Enemy design (Heartless families)
- **How it works:**
  - A few silhouettes carry many variants.
  - The floating "wizard" family comes in **colour-coded element variants**: a red one casts fire and absorbs fire, one is a hopping blue ice variant, and a green one is a healer that resists most magic [wiki].
  - FM re-coloured almost every Heartless for its new content [wiki].
  - **Answer enemies:** the Large Body blocks all frontal physical hits with its belly, so you get behind it, use magic, or jump over with High Jump. When low on HP it shows an aura and rush-rams [wiki]. That is our sweeping broom exactly, which supports PHASE1 6.6.
  - Stats per enemy rise between visits (Large Body HP 160 → 300 after a story beat) [wiki]. That is the DC2 tier model.
  - **Respawns:** ordinary enemies return after you move 2 rooms away (1 with Encounter Plus) [wiki].
  - FM added rare enemies (mushroom-type) that respawn after 2 area changes and are puzzles more than fights [wiki][gen].
- **Useful for us? Yes.**
  - **Colour equals element on a shared silhouette** is a cheap readability rule: one model, a material swap, and a behaviour tweak. It fits our tier model.
  - Low-HP aura plus rush matches our Rage idea and confirms the telegraph.
  - Puzzle enemies (beat it only with a specific action) make a good optional floor event later.
  - **Avoid** the 2-room respawn (see "Things to avoid").

## 12. Boss design patterns
- **How it works [wiki][gen]:**
  - **Parts with their own HP:** Guard Armor's limbs and torso are separate targets. Each limb drops HP orbs when broken, and destroying limbs first is the intended route. A later fight reassembles the same parts into a harder form.
  - **Single weak point:** Behemoth can only be hurt on its horn, which you reach by climbing it [gen].
  - **Deflect the boss's own projectile back:** small ice shots from the Ice Titan [wiki].
  - **Arena hazards and set pieces** in several fights [gen].
  - Bosses award **permanent unlocks** (Dodge Roll from Guard Armor, Glide from a keyhole) [wiki].
- **Useful for us? Yes.**
  - Per-part HP and loot from breaking parts confirm DC1's per-part multipliers (PHASE1 6.1). For the Dough Warden: arms with their own HP that drop healing when broken, and a weaker or changed moveset once they're gone.
  - Re-using a boss's parts in a remixed form later is cheap content.
  - **Adapt for co-op:** part targets let players split roles (one on the arms, one on the core) without a class system.

## 13. Tech Points (skill bonus)
- **How it works [wiki]:** extra EXP-like points for skilful acts:
  - parrying or interrupting a melee hit;
  - deflecting a projectile back into its owner (one Ice Titan reflect is worth 24);
  - hitting an enemy with its elemental weakness;
  - landing a finisher on a weak spot (2 per finisher on one boss's head).
  - Tech Boost raises the gain.
- **Useful for us? Yes.** It maps directly onto weapon growth from use (PHASE1 6.3): skilful play feeds the weapon bond.
  - **Adapt:** bonus ABS on top of the kill value for a parry, a guard-break finisher, an element match and a weak-part finisher (start at +25% of the species value each, capped per kill). Show a small "Tech" pop so players learn the moves the game wants.

## 14. Menus and UX
- **How it works:**
  - A vertical command menu (Attack / Magic / Items / Special) cycled with the d-pad during combat [gen].
  - Three magic shortcuts on one modifier button [gen].
  - Limited item slots (3 to 8, growing with level) that refill from a stock between fights [wiki].
  - Scan shows enemy HP only as an equipped ability [wiki].
  - The FM journal adds "NEW" tags, completion stamps, and "????" for unseen enemies [wiki].
- **Useful for us? Partly.**
  - Take the journal's **completion feedback** ("NEW" badges, stamps, silhouettes for unseen entries) as cheap motivation.
  - Avoid menu-driven combat: cycling a list mid-fight was a common pain point [forum]. Put actions on buttons, keep quick slots, and show enemy HP by default (don't lock Scan behind a cost).

## 15. Difficulty modes
- **How it works [wiki]:**
  - Original release: Normal, plus Expert at **2x incoming damage**.
  - FM: Beginner gives starting gear (8 Power-Ups, 8 Defense-Ups, 4 AP-Ups, a status-proof accessory, an EXP boost). Proud doubles incoming damage and halves Gummi damage dealt.
  - Proud also unlocks the opt-out EXP Zero, and harder modes show a battle report at the end.
- **Useful for us? Partly.**
  - A single damage multiplier is cheap to ship but shallow. Our harder setting should also change enemy behaviour (more Rage, faster telegraphs, a tighter revenge cap), not only numbers.
  - "Easy = a starter gift" is a nice alternative to weaker enemies.
  - Stacking with co-op: difficulty scales to the players present (P23), so modes multiply on top of that.

## 16. Collectibles (Dalmatians, journal)
- **How it works [wiki][guide]:**
  - 99 puppies, **three per chest**, so 33 finds.
  - Rewards come at milestones (about every 9-12 puppies) and are **claimed in person at a hub house**, not on pickup. The ladder runs from spell gems through rare materials up to a top accessory, and finding all of them adds to a secret ending.
  - Some chests sit behind Trinity Marks.
  - The journal tracks them.
- **Useful for us? Yes.** It is our resident-return loop in miniature.
  - **Adapt:** lost keepsakes or "hearthstones" found in dungeon chests (bundled, so finds feel big), each with a named owner. Return them in town for milestone rewards and town wishes.
  - A tracker shows what's left per floor band (dark-cloud-lessons: nothing missable, a tracker shows what remains).

## 17. Mini-games and arena cups (Olympus)
- **How it works [wiki][guide]:**
  - Tournaments unlock with story progress. Phil Cup is 9 seeds.
  - Each cup has escalating modes: party, then solo, then a solo time trial (20 min on the Hades Cup). Each mode pays a different reward.
  - The **Hades Cup** runs 49 seeds counting down, with a **boss every 10th seed that sets a checkpoint**, so a loss resumes from the last one.
  - The cups' rewards include abilities, spells and keychains.
  - The Hundred Acre Wood holds gentle mini-games tied to collecting torn pages [wiki].
- **Useful for us? Yes, later.** An arena in a rebuilt town is a natural side activity that feeds the loop.
  - **Adapt:** a "proving ring" with ladders built from enemies already fought (cheap content).
  - Checkpoint every boss seed (the Hades model).
  - Optional rule variants (solo, timed, one weapon) each pay a distinct reward: an attachment or a build-up material.
  - In co-op, the ladder scales to the players present.

## 18. Camera and platforming
- **What happened:** reviewers then and now call the camera the game's biggest flaw. It sticks behind walls, uses shoulder-button rotation, and makes floaty jumps hard to judge. Platforming-heavy rooms (an early island, a tree-top area, swimming in an underwater world) are the most complained-about stretches [forum: RPGFan panel, TheGamer list, retrospectives].
- **Useful for us? Yes, as a warning.**
  - Camera collision and right-stick orbit are already planned (the combat note, PHASE1 6.5).
  - Keep precision platforming out of dungeons. Where we have vertical traversal, use ledge-grab and coyote time and keep the camera framing the landing.
  - No swim combat.

## 19. What players disliked (summary)
- **Camera and floaty jumping** (18) [forum].
- **Getting lost:** worlds with weak signposting and backtracking, and maze-like interiors (Wonderland, Monstro, the underwater world) [forum: RPGFan panel].
- **The Gummi ship:** a slow, forced shooter between worlds [guide].
- **Party AI** wastes items and dies [forum].
- **Grinding** for synthesis materials and rare drops [forum].
- **Menu-cycling** during fights [forum].
- **Missable content:** one Trinity Mark in the original release [wiki].
- **Praised:** secret bosses as the skill test, the cups, synthesis hunting, combat that deepens as abilities come in, and the hub [forum].

---

## Top 10 ideas worth taking (ranked)
1. **Co-op "hearth marks" (Trinity Marks):** a joint party action (push, lift, brace) opens secrets and shortcuts. Colours unlock with the story, so early floors hold visible locks to return to. Every mark must have a solo route (a longer channel or a hero tool), and nothing is missable.
2. **Tech bonus to weapon growth:** parry, deflect, weak-part finisher and element match pay extra ABS with a visible "Tech" pop. It teaches the intended answers and deepens the weapon bond.
3. **Pip magic with a hit-fed charge gauge** for Wren's staff and other active skills: 3 pips, refilled by landed hits. Weapon growth raises the cap.
4. **Boss parts with their own HP that drop healing when broken** (Guard Armor), for the Dough Warden. Later bosses reuse parts in remixed forms. Co-op players split targets naturally.
5. **Bundled collectibles claimed in town:** keepsakes found 3 to a chest, returned to named residents for milestone rewards. Ties exploration to "rebuilding home".
6. **Colour equals element on a shared silhouette, plus "answer" enemies** (the Large Body's frontal block confirms our broom). One model, many readable variants.
7. **Permanent shared traversal unlocks** from band bosses (High Jump / Glide model) that open shortcuts and secrets in earlier floors and towns.
8. **Synthesis recipe lists unlocked by variety crafted**, with every material's source named in the journal (when synthesis lands).
9. **Arena ladder in a rebuilt town:** a boss every N rounds sets a checkpoint, with solo, timed and one-weapon variants each paying a distinct reward. Scales to the players present.
10. **A starting "lean" choice per heirloom** that reorders which tree branch previews first plus a small stat tilt, shown openly and re-routable (dream-weapon choice minus its hidden parts).

## Things to avoid
- **Group-only gates** (summons and Trinity Marks need the full party). In our worlds, anything needing 2+ players must have a solo path.
- **Hidden permanent choices:** the quiz that silently sets the EXP curve, and the dream weapon's stat cost shown nowhere.
- **Enemies respawning after 2 rooms.** It conflicts with "floors stay cleared" (dark-cloud-lessons). Offer it only as an opt-in farming toggle, if at all.
- **A forced travel mini-game between areas** (the Gummi ship). Travel is instant once reached; any travel activity is optional and feeds the loop.
- **Menu-cycled combat commands,** and Scan locked behind an ability cost. Show enemy HP by default.
- **AI allies that spend shared consumables** (not relevant while we have no AI companions; keep it in mind if solo ever gets one).
- **Difficulty as only a damage multiplier** (Expert/Proud 2x). Our hard mode also changes behaviour.
- **Precision platforming, swim combat, and a camera without collision.**
- **Maze interiors with weak signposting.** Every room pool needs a landmark and a readable exit.
- **Missable collectibles** (the original release's Oogie mark). Everything stays reachable after its boss.
- **Coupling spell power to the MP pool** (KH1 max MP = spell strength). Keep power on the weapon.

## Sources
- KH wiki (khwiki.com): Level, Abilities_(KH), Trinity_Mark, Item_synthesis, Summon, Save_Point, Proud_Mode, MP, Tech_Points, Jiminy's_Journal, Kingdom_Hearts_Final_Mix, Keychain, Guard_Armor, Large_Body, Red_Nocturne, Green_Requiem, Encounter_Up, Gummi_Ship, Hades_Cup, Walkthrough:Kingdom_Hearts/Olympus_Coliseum, Dive_to_the_Heart, User:TheSilentHero/Walkthrough/Dive_to_the_Heart, Puppies.
- Guides: gamerguides.com KH Final Mix walkthrough (Olympus Coliseum, Dream Rod MP), PC Invasion and Prima (dream-weapon choice), destinyislands.com (puppy rewards), SuperCheats (magic gauge, via search summary), macmyths.com / gaming.net (keyblade stats).
- Forums and retrospectives: Steam KH 1.5+2.5 discussions (Donald Customize settings, ally items), RPGFan Retro Encounter final thoughts (classic.rpgfan.com), TheGamer "15 reasons" list (via search summary), Giant Bomb KH page (starting-stat table, conflicting).
- OpenKH KH1 docs: `C:\Dev\_research\OpenKh\docs\kh1\` (file types and dictionaries only).
