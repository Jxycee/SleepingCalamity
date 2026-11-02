# Inside the room: encounters, arenas, traps, elites, bosses, dungeon audio

Wave 2 note, 2026-11-02. Extends (does not repeat) `docs/research/enemy-ai.md` (tokens, rings, roles table, difficulty-by-player-count table, telegraphs) and `docs/research/combat-depth.md` (poise, openings, guard, perfect dodge). Roster assumed: Rat-king (rusher), Slime (swarmer/lobber), Broom (shield), 2 tiers each, plus Dough Warden elite (from enemy-ai.md).

Confidence tags: **[V]** verified from a fetched/searched source (URL in Sources); **[I]** my inference or prior knowledge, not checked this session.

Effort honesty: about 24 searches and 10 successful fetches. Search results were thin for several asks (Hades wave counts, GoW/DMC/Bayonetta arena talks, KH1 room design, Returnal, DS aggro ranges, P3 per-block track list). Reddit, GameFAQs blocked and not circumvented. Gaps are listed at the end rather than filled with guesses.

---
## 1. Encounter composition

### 1.1 What the sources say
- **Few types at once.** Level Design Book table: 1 enemy type for a tutorial/rest beat, 2 for a regular encounter, 3 for a complex one; at 4 split into two factions (2v2) so it stays readable; 5+ is a free-for-all where players only track a subset. [V] Headcounts are not given there.
- **Roles overlap differently.** Critpoints: give each enemy a different way to control space (line, cluster, close flail) and pair them so they cover different options at different times (ranged+melee, fast rusher+slow wide hitbox). With turn-taking group AI, one much tougher enemy can beat three weak ones. Same-type enemies can still have different roles via position. [V] (This backs combat-depth.md rank 5, "fewer, tougher".)
- **Doom 2016.** 16 archetypes, each readable at a glance; ranged enemies hold distance, others lurk to lunge; arenas medium/large, asymmetrical, vertical; per-attack-type token pools that vary with difficulty, enemies request/release tokens and can steal them; cover logic repurposed so enemies stand in "exposed" spots near cover, not hidden ones. "Combat chess": the player chooses the kill order. [V] Eternal fights can mix 7+ types, and a critic notes that this strains attention when combined with heavy mobility. [V, secondary blog]
- **Doom arena taxonomy.** "Jungle gym" (distinct floors, obstacles blunt long-range damage, separate skirmishes) vs "canyon" (open, long sightlines). Both are needed for variety; Eternal's opening leaned canyon. [V, one critic]
- **Door problem** (Game Developer): players retreat to the entry door and turn the fight into a shooting gallery. Fixes without changing enemy count or room size: a strong foothold inside, partitions so no spot sees everything, height difference that leashes enemies, a reward in the room, a one-way drop; layer several. Convex corners are good cover, dead ends repel. [V]
- **Ambush rules** (Level Design Book): player ambushes enemy (hidden reinforcements join), enemy ambushes player after a mid-field trigger (do not make it very hard), vista with one-way entrance as the "fair fight". If the layout is unclear, players fight from the doorway and the arena goes unused. [V]
- **Waves.** Hades rooms spawn enemies in several waves, the next wave starting when the last dies; elite rooms (skull on door in Hades 2) are usually a single wave of armoured enemies or a mini-boss. [V, secondary guides; Hades 2 more than Hades 1] Hades "Jury Summons" raises room enemy counts +20% per rank up to +60%, so count is a tunable knob. [V]
- **Diablo 3 packs.** Champion packs: 3 or 5 of the same type, all with the same affixes and no leader; rare packs: one leader plus minions. Affix count rises with level (1/2/3/4 at 1-29/30-49/50-59/60+), at most 1 defensive and 1 crowd-control affix, 2-3 offensive; some pairs are banned. Affixes that need the pack to cooperate (Fire Chains, Health Link) are what make a champion pack feel like a puzzle. Affixes players hate: Waller (walls you off), Shielding (lone survivor untargetable), Juggernaut. [V] Lesson: affixes that remove player agency are the unpopular ones; affixes that add a position/target decision are the loved ones. [I, reading of sources]
- **Dark Souls.** Ambush encounters should be rare and learnable; DS2's habit of adding enemies for difficulty is a recurring complaint; a few reviewers/players say ambushes discourage exploration. [V, weak: forum + one review] Telegraph example: a smoke cloud warning of an off-camera shockwave. [V]

### 1.2 Cross-cutting
1. Variety comes from **composition x space x state**, not roster size: same 3 enemies, different mix, different room, different modifier.
2. Readability rule of thumb across sources: two roles in a normal fight, three in a set piece; never four+ without splitting them into two groups.
3. Counts scale cheaply (Pact of Punishment +60%, our +1/+2/+3 per player) but extra bodies must not stack tokens beyond the global strike cap already in enemy-ai.md 6.3.
4. Rooms are most interesting when something about the **room** changes the fight (height, hazard, partition), not the enemy.

---
## 2. Arena geometry for third-person melee [mostly I, with V door/jungle-gym backing]

Our rooms are on a 4 m grid. Camera behind the hero is the real constraint: a melee fight needs the camera to see the hero and the attacker at once, and wall or pillar clipping is worse in a dungeon than in a field. Suggested rules (all **[I]**, to be validated with `sc-walk` playtests):

| Rule | Value / reason |
|---|---|
| Combat footprint | Smallest combat room 3x3 cells (12x12 m) for solo; a room that will hold 4 heroes + 6-8 enemies wants 4x4 (16x16 m) or an L/T that totals 12+ cells. Strike ring radii in enemy-ai.md are a few metres; 4 heroes each need a ring plus dodge-roll room. |
| Free floor | At least 60% of floor cells walkable-with-no-obstacle; obstacles in clusters of 1 cell, never lines longer than 2 cells (stops the camera boxing in). |
| Pillars | 1-3 per combat room, 1 cell wide, spaced at least 2 cells apart so a hero can circle one and a 4-player party can pass. Pillars block lobber lines and break sightlines (Critpoints "include a pillar to circle", Doom jungle-gym damage damping). |
| Door fix | Combat starts when the party crosses the room's centre line or the second row of cells, not on the threshold; door closes (or enemies get a leash tether) only on the arena variants where a closure fits the fiction. Put a half-height foothold (crate stack, barrel row) 1 cell inside the entry so the first cover is inside the room. |
| Height | Max one step level (0.5-1 m ledge) in most rooms; a raised 1-2 cell dais for lobbers makes the lobber a position target (leashing: melee enemies can't follow without stepping up). Avoid anything needing jump (no jump button in DC; check our hero kit). |
| Chokepoints | One 1-cell corridor mouth is useful for a tank/riposte fight but it must have a second exit or loop, else one hero holds it solo (shooting-gallery problem). Keep hand-authored loops on dead-end rooms. |
| Camera | Ceiling at least 5 m (storybook cellar vaults can curve higher); no low arches inside the combat footprint; wall-hugging side cells accepted as lost space. |
| Per player count | Same room, different **density**: do not resize rooms. 1 player: 2-3 enemies (tier-1 mix) + 1 spawn point per 6 cells; 4 players: +3 enemies per existing rule, spawn in 2-3 telegraphed batches so the screen is never entered with 8 live bodies. Rooms authored for 4 (large) play fine with 1 because fewer enemies means open ground; the reverse fails. So author for 4, populate for N. |

Spawn telegraph: enemies climb out of floor holes / drop from ceiling vents with a 1 s glow ring and no attack until landed (Doom has tokens, Hades has spawn "pulses"; both let the player see where). [I, mechanism; the existence of Hades spawn telegraphs is common knowledge not fetched]

---
## 3. Traps and hazards

### 3.1 What I found
- **Spelunky.** Arrow traps fire at anything entering their detection zone, enemies included; each holds one arrow, then is harmless; thrown objects can set them off from outside the zone; webs block them. Yu: look at a trap that worked and ask why it worked and how else to use it. Yu also says players need not kill everything: lure enemies into traps. Early-level arrow damage was kept hard despite designer pushback; a patch to Spelunky 2 later toned down spawn rates in the first areas after player feedback. [V, community wikis + interview + press]
- **Silksong gauntlet (Hollow Knight wiki).** Pressure plates the hero or enemies can step on release rail-guided spikes that can kill enemies. [V, wiki]
- **Terraria community guidance.** Traps as supplementary damage/CC in boss arenas; funnelling enemies with conveyors. [V, wiki/guide level]
- **Hades** has spike traps and enemy-affecting hazards; I could not source details this session. [I]
- **Dark Cloud**: trap and floor-effect details live in the repo's decomp notes, not re-checked. [not verified here]

### 3.2 Fairness rules distilled (my synthesis, [I] from the above plus general practice)
1. Telegraph by shape, sound and a "primed" look (cracks, glints, pipes hiss) at least 0.6 s before danger; same visual vocabulary on every trap.
2. Hazards hurt **enemies and heroes alike** and are *visible in the room before the fight*; this is the strongest interaction hook (lure, knock-back, shield-bash into it).
3. One-shot or cooldown traps (Spelunky's one arrow) reward learning without becoming a permanent tax.
4. Damage is a fixed number, not a percentage kill, in the dive-and-bank economy: a hazard must never be the thing that loses a haul.
5. Hazards do not stack with a full arena token budget (a room with a hazard has one fewer enemy or a smaller wave).
6. Host-authoritative: trap state is a short replicated timer/phase (like enemy tells), damage resolved on host.

---
## 4. Mini-bosses and elites

### 4.1 Sources
- **Isaac champions.** Base 5% per eligible enemy (20% hard, 75% in the Void), most with 2x health and ~15% larger, with a colour = behaviour mapping. Examples: explode on death, invulnerable until others die, regenerate other enemies while alive, split in two on death, 6x health "crown" that tags the others, rainbow combining traits at ~20x rarer; some enemy types can't be champions; champions drop a pickup of their colour on normal difficulty. [V]
- **Etrian FOEs.** Visible, non-random, fixed-route enemies much tougher than normal; the player learns the route and avoids or ambushes; some are slow (move once per two steps), some pause every move so there is a window; passive ones exist. The fear works because the threat is seen and learnable, not rolled. [V]
- **Diablo packs.** Leader+minions (rare) vs identical pack (champion); affix budget by category. See 1.1. [V]
- **Hades.** Skull-marked elite rooms in Hades 2, usually a single wave of armoured foes or a mini-boss; Pact "Middle Management" adds an elite skill to mini-boss fights, "Benefits Package" adds perks to armoured enemies. Elite fights are faster than normal rooms. [V, secondary]
- **Slay the Spire elites** are a deliberate opt-in risk trade (extra relic and gold) and the route choice around them is the main strategic layer. [V, player sources]

### 4.2 Takeaways for us
- Mark elites **before** entry (door sign) and give them a **visible, learnable behaviour** (FOE-like) so risk is chosen.
- Variation through **one affix on a known body** is cheaper than new bodies: Isaac and Diablo both do it. With one elite (Dough Warden) an affix table gives it a new fight per floor.
- Affixes that remove agency (Waller, Shielding on lone survivor) were the disliked ones; keep affixes as extra *decisions*.

---
## 5. Boss floors

### 5.1 Sources
- **Zelda dungeon->item->boss.** The item found in the dungeon is used through it and against its boss, so the boss is a final exam of that item. Aonuma: pick the dungeon's style, then the item and how it interacts with Link's surroundings; he also put the boss door in sight on entry as a "bait". Twilight Princess: a boss reworked repeatedly when its item (Spinner) changed (secondhand). [V]
- **Shadow of the Colossus.** 48 → 24 → 16 colossi; "better to leave players a little unsatisfied than to thin out the game"; each defeat method must not overlap with another; where two used similar tactics they were merged; early colossi can be beaten without terrain, later ones require it; boss and arena designed together (a colossus form implied the arena it needs; tests failing changed both); players do not backtrack to gather boss tools. [V, interview transcript site]
- **Hades.** Bosses rotate (e.g. the three Furies take turns as the first-biome boss), open with voiced dialogue, comment when you repeat; Extreme Measures changes the fight (Furies attack together; the Hydra's arena changes). The article credits randomization plus player-chosen difficulty for the modularity. Specific phases/attack lists not covered by the source. [V, thin]
- **Dark Cloud boss reception.** RPGFan (DC1): bosses impressive in rendering and animation but the boss music is a slight remix of combat music; dungeons repetitive. RPGamer on DC2: multiple-enemy and boss fights are more fun than the trash. Den of Geek (on DC1's final boss): players who neglected ranged characters found it nearly impossible and abused awkward positioning; another critic says the final boss is the only real resistance and fights menus and camera. I found nothing on DC2 individual bosses. [V]
- **Co-op scaling.** Common patterns: flat HP per extra player (Elden Ring co-op, reported +60% per player), Diablo 3 trimmed per-player monster HP to a flat 75% bump to keep co-op rewarding, scaling by **minion count** for summoning bosses (V Rising), scaling only when more than one player is engaged. A Klei thread argues for scaling boss *power/pattern* not just HP. [V, forum/community level]

### 5.2 Principles for Sleeping Calamity [I unless tagged]
1. A boss is a **final exam of the band's rules** (the band's traps, the shield or lobber pattern the party has learned), not a new rule set. That is cheap with a small roster: the boss is a large body of a known role plus one new verb.
2. **One fight, one gimmick, one arena** (SotC). 12 floors / 4 bosses beats many weak ones; reserve unique art budget accordingly. Also: don't gate the boss behind a tool hunted elsewhere.
3. **Set-piece floors at band end** (already planned) are mini-bosses with a puzzle element (oven maze + elite). The final boss floor can echo them.
4. **Phases by HP third** keep the host's state small: phase index is a single replicated byte; each phase adds one attack, not a new moveset.
5. **Co-op scaling for bosses:** HP per enemy-life multiplier (existing 1.0/1.7/2.3/2.8) *plus* more adds in the arena rather than more boss attack speed; one extra "stagger-the-boss" mechanic needing 2+ heroes (brace/push) with a solo fallback matches the parked hearth-mark idea.
6. **Rematch/variants (Hades-style):** a post-clear "heated" variant of the boss arena changing one rule (second boss body, hazard on, adds) gives replay value for few assets. Defer until after Phase 1.
7. **Boss door in sight early** (Aonuma's bait): the boss floor's door visible from the previous floor's exit.

---
## 6. Dungeon music and audio

### 6.1 Sources
- **RPGFan on DC1:** music is high quality and fits each area, but dungeon music tires over hours, combat music is forgettable from overuse, and boss music is a light remix of the combat track; "constant repetition and lack of diversity" drags the score. [V]
- **Hades (FMOD, via Gameplay.co):** about 2.5 hours of music; per-region playlists (4 Tartarus, 3 Asphodel, 3 Elysium pieces); a fixed intro piece with separate drum, bass and "everything else" stems; drums and bass on during combat, drums drop out afterwards; out of combat guitar and bass randomly play together, bass alone or neither, sometimes silence; pieces move through sections (pads/pulse → full arrangement → rock section used for mini-boss and boss) via a "section dial" checked at measure markers after a couple of chambers; a closing tag plays when a boss fight ends; a new piece starts at the biome midpoint. Stated aim: lengthen the music's shelf life. [V]
- **Persona 3 Tartarus:** six blocks (Thebel, Arqa, Yabbashah, Tziah, Harabah, Adamah) each with distinct visuals, and each block has its own track, though wiki notes say later tracks are variations on the first theme (e.g. Arqa like Thebel with louder synth percussion). I could not get an official per-block list. [partly V]
- **Breath of the Wild:** piano-led sparse score, silence, famous tunes withheld until key moments; Iwata said how much BGM to use was a central question from the start. [V, secondary]
- **Adaptive terms:** vertical layering = add/remove parallel stems; horizontal resequencing = switch segments on state; many games combine them: resequencing for big state changes, layering for intensity. [V]

### 6.2 Cross-cutting
- The fix for "same few tracks" is **not more tracks alone**: it is (a) one track per band so the player hears change by depth, (b) stems that drop in and out with state so the same track sounds different, (c) deliberate **silence** and ambient beds so music has weight when it returns, (d) boss/elite music that is its own piece, not a remix.
- Ambient beds are wayfinding: dripping cistern = water ahead, creaking roots = root band, distant oven roar = floor 4. [I]

---
## 7. For Sleeping Calamity (each tagged cheap / later / big)

### 7.1 Encounter templates for a small roster [cheap]
Roster: R = Rat-king (rusher), S = Slime (swarmer/lobber), B = Broom (shield), W = Dough Warden (elite). t1/t2 = tier. Base counts are solo; add +1/+2/+3 per player as existing, cap simultaneous strikes per enemy-ai.md.

| Template | Roles | Base (solo) | Space it wants | Why it plays differently |
|---|---|---|---|---|
| **Pack** | R t1 x3 | 3 | open room | Learn rusher timing; turns at tokens |
| **Gel swarm** | S t1 x5 (3 swarm, 2 lobbers on dais) | 5 | pillars + dais | Swarm vs sniper lane; pillar use |
| **Shield wall** | B t1 x2 + S t1 x2 | 4 | chokepoint | Flank the shield; slimes block retreat |
| **Rush and bombard** | R x2 + S lobber x2 | 4 | pillars | Two roles only; break sightlines |
| **Guarded caster** | B x1 front + S lobber x2 behind | 3 | partition | Door-problem cure: shield blocks the route to lobbers |
| **Mixed tier** | R t2 x1 + R t1 x2 | 3 | open | One tough duel plus fodder (kill-order choice) |
| **Ambush** (rare) | R x2 hidden in floor cracks | 2 | empty-looking room | Enemy-ambushes-player at centre-line trigger; never combined with a hazard; max 1 per floor |
| **Reinforced** | Pack + second wave after clear | 3+2 | open | Waves 2 deep; second wave spawns from holes with telegraph |
| **Hunter** (stir 70) | one t2 roams | 1 | corridors | FOE-like: its route visible on the floor |
| **Elite arena** | W + 2 t1 adds | 3 | large | See 7.3 |

Rules: max 2 roles in a floor 1-2 room, 3 from floor 3; no more than 8 live enemies at 4 players; the final enemy in a wave drops the key if it is the key carrier (existing loop); waves are at most 2 deep.

### 7.2 Arena rules for 4 m grid rooms [cheap/later]
See section 2 table. Additions: author each combat room with a **size tag** (M 3x3, L 4x4+) and let the generator fill density by player count; place at least one foothold object inside 1 cell of the entry; no combat room with a single exit unless it holds a loop (generator already makes loops); every room has a clear lane from entry to a pillar. [cheap]

### 7.3 Elite and mini-boss principles [cheap -> later]
- Elites are **visible and marked** (sign over door, a distinct silhouette). Hunters roam on a known loop.
- Variation by **one affix** on the Dough Warden (flour cloud that blinds the camera slightly; heat-stoked: attacks leave embers; stuffed: spawns a slime at 50% HP; sticky: slows). Cap one affix per elite, none that remove agency (no wall-off, no invulnerability while others live). **later**
- Elite rooms: single wave, no more than 2 adds, one hazard-free arena so tells stay readable.
- Reward: a visible chest or bank-point shortcut on the door tells risk and payoff (matches parked "rewards shown on doors").
- Elite chance on generated floors: 1 per floor from band 2 on; Isaac's 5% shows rare is fine, but we have a fixed set-piece elite per band so keep random elite rolls to <= 1 per floor.

### 7.4 Trap ideas fitting cellars/cisterns/roots [cheap/later]
All hurt enemies and heroes, are visible in the room before the fight, telegraph 0.6+ s, deal fixed damage, and can be triggered by either side.
- **Cellars:** barrel rollers (a barrel pair rolls along a marked rut when a plate is stepped on; knocks over a row of enemies); hanging sack of flour drops on a rope when cut (blinds/slows those below); loose-brick spikes in the floor on a slow rhythm (one-arrow style: primed, fires, resets after 6 s).
- **Cisterns:** valve wheel that floods a lane (slows everything, extinguishes oven embers); drain grates that flush at a rhythm (push, no damage, lures enemies away); rising water in the final wave.
- **Roots:** root snaps shut on anything stepping on a bulb (roots reward lure kills); spore pods (cloud that confuses enemy targeting but not heroes); thorn wall that retracts after the bulb is hit.
- **Co-op hearth mark tie-in** (parked idea): a heavy lever needing two heroes opens a shortcut; always a solo route. **big**
- Spelunky lessons: allow thrown objects/lures to trigger from safety; webs/barriers (a Broom's shield) can block them; one-shot where possible.
- Rule: hazard rooms have fewer enemies; no hazard overlaps a pillar-only safe zone.

### 7.5 Boss-floor principles for the Cellars [later/big]
- Boss door visible from floor 11's exit. Band-end set pieces escalate: floor 4 oven maze + Warden; floor 8 cistern with water control; floor 12 root-boss.
- Boss is **a giant of a known role** (e.g. a huge Rat-king for the rusher band, a Broom matriarch for shield) with one new verb and phases by thirds; reuse tells and token logic from enemy-ai.md.
- Arena authored with the boss (SotC): arena plan drawn from the boss's reach; adds spawn from holes; 2-player stagger mechanic with a solo fallback.
- Rematch variant later: "heated" arena with one extra rule, shown on the door.

### 7.6 Music plan per band [cheap -> later]
Principles: one distinct piece per band + one per set-piece floor + one boss piece (not a remix of combat); stems for state; silence is a feature.

| Band | Floors | Ambient bed | Exploration | Combat | Notes |
|---|---|---|---|---|---|
| Cellars (1-4) | 1-3 | drips, wood creak, distant hearth hum | sparse plucked string + low pad | add percussion + bass stem (Hades-style) | warm, small |
| Cellars set piece | 4 | oven roar, bread crackle | dedicated piece | elite theme as rock-section equivalent (all stems) | cue when entering maze |
| Cisterns (5-8) | 5-7 | water echo, valves | water-pitched keys, long reverb | percussion in rhythmic pulse | different key from Cellars |
| Cisterns set piece | 8 | | own piece | elite theme variant | |
| Roots (9-11) | 9-11 | creaks, wind in tunnel | low drone + breathy woodwind | heavier | quiet; silence common |
| Boss floor | 12 | | | boss piece with closing tag on victory (Hades) | |

Mechanics: states = `explore`, `combat`, `elite/boss`, `cleared`; combat adds drum + bass stems at the next bar, removes them 2 bars after the last kill; explore guitar/bass-equivalent stems randomly combine or drop to ambient only (Hades); a section dial advances after N rooms on the floor; one piece per floor chosen from the band playlist of 2-3 pieces, so repeated dives vary; stir meter 40/70/100 adds a low heartbeat/drone stem and at 100 swaps in a closing theme; quiet floors (rare dark floor) play ambient only. Ambient beds are also wayfinding: drip density grows near water rooms. Co-op: music is local per client, driven by the host's replicated room state (a byte: explore/combat/elite). [I]

---
## 8. Open questions
- Hades 1 wave counts per room and enemy cap; GoW 2018, DMC, Bayonetta arena rules; KH1 room structure; Returnal waves; DS aggro radius: not found in public sources this session.
- Persona 3 per-block track names and whether each block's track is unique or a variation (wiki says variation); worth checking an official track list.
- DC1/DC2 individual boss reception beyond the final boss: sources thin.
- Hades trap behaviour against enemies: not sourced.
- BotW composer's own statements beyond secondary summaries.

## Sources
Primary-ish: Game Developer "The door problem of combat design" (https://www.gamedeveloper.com/design/the-door-problem-of-combat-design); GDC Doom talk listing (https://gdconf.com/news/see-doom-devs-break-push-forward-combat-design-gdc-2018); Game Developer "Cyber demons: the AI of DOOM 2016" (https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-); Level Design Book encounters (https://book.leveldesignbook.com/process/combat/encounter); Gameplay.co Hades music (https://gameplay.co/hades-game-music-sound-design-darren-korb-supergiant-games/); Shadow of the Colossus developer interview transcript (https://glitterberri.com/extended-developer-interview/making-of-the-colossi/); Iwata Asks OoT3D (https://www.nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-The-Legend-of-Zelda-Ocarina-of-Time-3D/Vol-3-Original-Development-Staff-Part-2/3-Making-the-Bosses-and-Enemies/3-Making-the-Bosses-and-Enemies-216367.html); Aonuma Kotaku interview via https://www.zeldadungeon.net/wiki/Interview:Kotaku_February_17th_2015.
Secondary: Adam Atomic on Doom Eternal (https://blog.adamatomic.com/post/613311014289768448/design-of-doom-eternal); Critpoints (https://critpoints.net/2017/04/17/designing-for-multiple-enemies/); Maxroll Diablo 3 affixes (https://maxroll.gg/d3/resources/elite-affixes); Isaac wiki champions (https://bindingofisaacrebirth.wiki.gg/wiki/Champion); Game Developer Hades boss fights (https://www.gamedeveloper.com/design/boss-fights-will-never-be-the-same); RPGFan Dark Cloud review (https://www.rpgfan.com/review/dark-cloud-2/); RPGamer DC2 (https://rpgamer.com/review/dark-cloud-2-review-3/); Den of Geek boss cheese (https://www.denofgeek.com/?p=1014119); Spelunky wiki/Destructoid (https://spelunky.fandom.com/wiki/Arrow_Trap_(Classic)); Silksong gauntlet (https://www.hollowknight.wiki/w/Enemy_Gauntlets_(Silksong)/The_Slab_1); Persona 3 Arqa (https://megamitensei.fandom.com/wiki/Arqa_Block), Tartarus (https://megatenwiki.com/wiki/Tartarus); Hades Pact (https://www.rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards); Etrian FOE pages via search (https://en.wikipedia.org/wiki/Etrian_Odyssey_(video_game)); BotW score (https://www.zeldadungeon.net/musical-musings-listening-closely-to-the-emptiness-of-breath-of-the-wilds-hyrule-field-day/); Splice adaptive music (https://splice.com/blog/adaptive-music-video-games/); co-op scaling forum threads (Steam Elden Ring, Klei forum).
