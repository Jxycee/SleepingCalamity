# 06b - Craft and Zelda deep pass (wave 2): theory sources, metroidvania guidance, comparison dungeons, co-op puzzles, secrets

Extends 06-design-theory-3d-craft.md and 01-zelda.md (not repeated). Tags: [V] = read in a fetched page or search result this session; [B] = background, not re-verified; [I] = my inference.

Effort honesty: about 35 WebSearch calls and 25 WebFetch reads. The session-wide WebSearch budget ran out before the last planned searches (Genshin/Portal 2/Diablo-style co-op puzzles, Valve "weenie" talks, more L4D commentary). Those gaps are listed in section 10 and are NOT filled with guesses. Blocked or unusable this pass: GDC Vault, 80.lv (no list, summary only), the amara Boss Keys transcripts (403), Valve developer wiki (403), zeldadungeon.net (403), broadly.vice.com (not in the network allowlist), Game Informer's Death's Door page gave only part. reddit was not used.

---

## 1. Theory sources, now verified

### 1.1 Jaquaying / xandering (Justin Alexander)
- [V] Alexander coined "jaquaying" in 2010 and in November 2023 deprecated it in favour of "xandering" (search summary of his site; his own pages now say "xandering"). Named for Jennell Jaquays (1956-2024).
- [V] Fetched Alexandrian page (the Caverns of Thracia analysis): Thracia has three separate entrances to level 1; from level 1 there are two conventional ways down and eight unconventional/secret ones; players can interrogate locals to learn secret routes; every room has one or more exits and the choice among them shapes the adventure; the contrast case is a linear published module where choices only lead down one pre-set path. Level 2 is where it "gets really interesting" (connections between levels; he points to a separate addendum and a "Melan diagram" for visualising flow).
- [V] Alexander's later xandering-examples essay (part 5): xandering changes the space around existing encounters without changing the adventure's core; cheap to do; the examples are connecting tunnels, a secret door at the bottom of a pit trap, a water link between two areas, a teleporter; the author warns not to apply every technique at once: pick one and push it.
- [V] Sly Flourish's checklist (a widely used digest of the essay): asymmetric non-linear layout; multiple entrances (also a stealth-vs-force choice; with one entrance every fight in the first room can alert the whole place); loops that are physically big enough to matter (a hallway twisting around itself in a small space adds complexity, not interest); multiple start-to-finish paths; secret doors/hallways/chambers; shortcuts that skip rooms or link back to earlier areas. Red flags: one entrance into a big room with eight single-door rooms (clear them one at a time), tight loops, traps at dead ends.
- [V] Jaquays' own modules: Caverns of Thracia (Judges Guild, 1979) and Dark Tower (designed fall/winter 1978, published 1979 or 1980: sources disagree). Obituaries credit her with an "open, interlocking, looping" style. Dark Tower is described as two towers connected at many points (tightly interconnected towers and dungeons), Thracia as the one with three entrances, factions and a coherent setting.
- Gap: I could not read the original Alexandrian parts 1-4 (the dungeon-levels-connections addendum). The "discontinuous levels" point in file 06 stays [B].
- Takeaway for a floor generator [I]: the measurable properties are (1) entrance count, (2) loop size (graph cycle length in rooms, not metres), (3) secret/conditional edges, (4) one-way shortcuts. Our generator controls (2)-(4) already; (1) is the one lever not exposed.

### 1.2 Dan Taylor, Ten Principles (GDC 2013) - now fully listed
Sources: summaries of the talk by three independent bloggers plus Gamedeveloper "Part 2" [V secondary]; the talk itself is on the GDC Vault/Internet Archive (not watched). Taylor modelled the list loosely on Dieter Rams; he later said it is a starting point, not the only way.
1. Fun to navigate: clear visual language (lighting, geometry) for the main path, with verticality, secondary paths, hidden areas. Not always obvious: Modern Warfare 2 favela is a deliberate maze for tension. Mirror's Edge red route.
2. Does not rely on words: leave a gap for the player to fill; environment tells the story (BioShock).
3. Tells the player what to do, not how: short clear objectives, multiple ways; do not punish improvisation.
4. Constantly teaches: introduce new patterns (Koster's Theory of Fun); Zelda dungeons teach an item then test it on the boss. Keep old mechanics fresh via modifiers.
5. Surprises: avoid a smooth intensity ramp; his learn/play/challenge/surprise loop. Dead Space 2's Ishimura withholds the expected attack for ~15 minutes. Risky surprises get greyboxed and playtested early.
6. Empowers the player: visible consequences.
7. Easy, medium and hard at once: no up-front difficulty select; instead risk/reward routes (Burnout marks shortcuts with yellow barricades; Dishonored layered paths). The main path suits moderate players, optional risk routes challenge skilled ones.
8. Efficient: modular, mechanics-driven encounters; two-way routes so art is seen twice (Halo 3 returns through the same desert in a tank); optional collectibles are cheap playtime, but non-linear levels should guide with implicit goals, not filler collectibles.
9. Creates emotion: start from the intended emotion, then choose spatial metrics (tight to open = liberation; height = persecution or hope).
10. Driven by mechanics: level as "gameplay delivery system"; reuse and twist core mechanics (Arkham City Riddler challenges reuse existing gadgets).
Relevance [I]: #7 and #8 match our dive-and-bank model exactly (Stir meter as player-chosen risk; chunks as modular encounters). #5 argues against a perfectly even Stir ramp: schedule a deliberate quiet floor.

### 1.3 Valve
- [V] Valve GDC 2006 deck (Speyrer, Jacobson), read in full: "playtesting as production": build 15 minutes of rough gameplay, playtest, use results to set next week's priorities; 1-2 week increments; "done" as soon as watching playtests was no longer painful; simulate the player "in their living room" (no hints, no answers, no rewards); observers include designers; question playtesters little, learn from what they do not experience; late playtesting is less valuable because it is too late for substantive change; small independent design teams per chapter, each with a sandbox. Valve developer wiki: each HL2 level has a theme or purpose; sketch the main route before detail.
- [V] L4D lighting: Valve environment designer Randy Lundeen said an earlier darker version meant playtesters could not see zombie silhouettes in mid/background, so they were repeatedly mobbed, which was frightening but frustrating (no information to react to). The fix was light-coloured fog: less realistic, but players could see attackers and anticipate. Lesson: in horror-lit spaces readability of threats beats realism. The Valve L4D wiki also frames lighting as a design factor (classic horror techniques) and navigation flow nodes from safe room to safe room [V from search summaries; page itself 403].
- Not found: any Valve statement using "weenie"/landmark vocabulary. The Disney "weenie" lineage in file 06 stands, and attributing it to Valve is [B]. Not verified: L4D flow-distance numbers.

### 1.4 Mark Brown (GMTK) conclusions - what I could verify
- [V] Silksong world design (GMTK Substack, "The World Design of Hollow Knight: Silksongs"): three-act "concertina" structure: act 1 mostly linear with optional skips, act 2 open (three destinations in any order), act 3 a mix. Zones each have their own palette and mood; the Citadel's areas are more alike but each has a gimmick (gears, box puzzles). Each area starts unmapped until you buy its map from Shakra; the map fills in when you rest at a bench; open paths show where to go next, enabling "map-poking" (check unexplored exits until one works), but secret walls are not on the map, which undercuts that. Backtracking: the main path rarely needs it; returning with new abilities mostly unlocks optional content, and he praises that as rewarding casuals and completionists alike. Gating: key/lock metaphor; early abilities open few nearby locks so progress is "just around the corner"; one ability (Cling Grip) opens many routes; alternate routes let players bypass certain bosses. Guidance: map icons for key items, NPC clues, cutscenes explaining skipped fights; Team Cherry philosophy: a stuck player should go elsewhere; he criticises corpse-running for pulling players back to the place they died and criticises hidden prerequisites for the true ending.
- [V] Hollow Knight episode (via search summaries and a student paper citing it): the game hints at upcoming areas by placing a room that carries that zone's visual identity ahead of time; early game is linear; soft locks (expensive items, strong enemies, hard-to-reach paths) let order vary a little (the Lantern purchase example); one-way doors show their lever on the far side and stay open once opened, and the blocked boss path routes the player the long way past Cornifer.
- [V] Brown's recurring thesis (search summaries): good non-linear design helps the player build a mental map via backtracking and branching; Boss Keys later moved from Zelda to Metroidvania/Dark Souls/Metroid Prime. A transcript fragment says of Metroid Prime that it nailed the lock-and-key interconnected structure but "we might learn more from its mistakes" [V weak, secondary]. The details of that episode remain unretrieved.
- [V] A university analysis of Hollow Knight (HSLU "Metroidvania Analysis", reading Brown): Key-usage hints (the first key is shown on the exact enemy that blocked the path; the second is followed by obstacles that need it immediately); trap-door ability locks (the area can be entered but not left until the wall-jump is found); lit torches mark the route to a reward; symbols tied to an ability (crystals mean the crystal-dash) appear in foreign zones to signal a secret; map shows what is unexplored; not every secret is on the map, so revisits with new abilities find more; boss-room map symbols let players choose to go or avoid.

### 1.5 Super Metroid guidance (Hugo Bille, "The Invisible Hand of Super Metroid") [V, fetched]
- Opening: a wall blocks the right, so the player learns to go left; a linear Ceres prologue teaches mechanics first.
- Teach by lock-in: a room that requires running traps the player until they learn it; three creatures demonstrate the wall-jump; a pit corridor shows a charged jump via an enemy.
- Soft gates: blocked paths usually have a back door; one-way shafts close old areas and focus the player; chained roadblocks.
- Funnel: early Norfair is limited to a few rooms, tunnels/shafts reduce getting lost in act 1; the Brinstar map room sits where the player will likely find it and reveals Kraid's lair.
- Hints: stand-out blocks, disconnected ground, partial ceilings signal hidden rooms; a recycled enemy frozen in lava teaches the Ice Beam; a statue room foreshadows bosses; a shattered containment room tells a story with no text. Secrets sit where curious players look; revisiting reveals new content.
- Caveat from reviews: some points are impossible without tedious searching or a walkthrough [V weak].

### 1.6 Zelda cracks as the original fair secret
- [V] howtomakeanrpg digest: the cracked wall appears before bombs, so it foreshadows a key the player has not got; players try the sword and lamp first, then move on; the open question nags until bombs arrive and they return unprompted. Lessons: show the lock before the key; drip-feed locks; hide locks in plain sight; keys with multiple uses; level keys sparingly.
- [V] Zelda Wiki: cracked walls recur under several names; in some games sword-tapping tells which are bombable. I found NO source for a "hollow sound" cue; it stays [B] and should be treated as unverified.

---

## 2. Comparison dungeons and areas

### 2.1 Metroid Prime
- [V] Retro Studios (MIGS 2007 via Game Developer): Prime has "not one repeated environment"; Super Metroid was the "bible"; the Scan Visor was pushed by Nintendo late in development, Retro initially found it boring and made scans collectable and used them to teach enemy weaknesses; scan is liked in Japan, disliked in the West; Prime 2 lost players ("players got lost too easily", constant dark-world damage "too damn hard") and Retro concluded Western players want clearer direction.
- [V] Player sentiment (GameSpot/Giant Bomb user reviews, fan sites): the map is rotatable, every room named, doors on the map colour-coded by the weapon that opens them; but doors outside the selected room are hidden, and the top-down view makes doors hard to read; a multi-layered section confuses the map. Backtracking is the main complaint ("too much even for a fan"), and the 12-Artifact hunt near the end is the sharpest ("key items rather than fun upgrades", structured revisits via clue scans). Missable scans (some are lost after you break all missile doors etc.) frustrate completionists. Metroid Prime 4 reportedly moved to a unified map and no fast travel [V, one review].
- Lesson [I]: colour-coded locks on the map, yes; a late fetch-quest that needs many revisits is the thing players resent; map hiding doors outside the selected room is the thing to avoid.

### 2.2 Hollow Knight / Silksong map-buying
- [V] Cornifer (wiki): you must buy the incomplete area map to start mapping; maps price from 30 Geo (Crossroads) to 150 (Fog Canyon, Queen's Gardens); Iselda sells the same maps at a higher price (Cornifer's prices are 75% of hers); you find him by scattered papers and his humming (he is silent in Deepnest because he is afraid); he leaves an area after certain progress points; a bench rest updates the map; secrets are not on the map.
- [V] Reception: Steam threads from 2017 complain that the map-buying system is bad if you miss him, or that the map won't update; a 2024 thread titled "Getting lost simulator"; a Spanish review calls finding Cornifer tedious at times. Silksong's Shakra moves region to region and sells map, compass and quill together, so fewer return trips [V guides], and maps can be found as pickups.
- Design intent [V Brown]: each area starts blank so exploration is real; humming/paper trail is a sound-and-breadcrumb wayfinding for the cartographer himself.
- Lesson [I]: "buy or find the floor plan" is a good dive-and-bank hook (a map pickup as loot). The complaint is from missing the vendor and from the map needing a rest point; we can fix both by making the map auto-update at checkpoints and sold in the hub/preparation screen.

### 2.3 Persona 5 palaces
- [V] Palaces are fully authored, not random (Twinfinite: credited as a smart change for the series); each palace has a gimmick/puzzle and the final boss has an exploitable gimmick; palaces have teleport points back to the entrance; they take several hours and are designed to be finished in one visit; later ones are longer, not more complex; puzzles are mostly "highlight mode, interact with highlighted object" (one code puzzle that needs algebra from slips was called tedious rather than challenging); stealth is thin, sneak attacks give the first turn; palaces are almost entirely linear with little branching (Prismatic Tyrant); reception: AV Club "too damn long"; reviewers say the middle/late palaces (Madarame, Kaneshiro, Okumura) are weaker than Kamoshida's; Mementos is repetitive procedural filler for some, fine for others [V weak].
- Not verified: safe rooms and calling cards as design (Atlus statements not found).
- Lesson [I]: tight authored theming is memorable; length without new mechanics is not.

### 2.4 God of War (2018): Niflheim and Muspelheim
- [V Wikipedia] Niflheim: optional realm of poisonous fog with a "maze-like structure of rewards"; Muspelheim: six trials climbing a volcano toward a Valkyrie. [V weak, blogs/Steam] Muspelheim trials called repetitive when they are timed waves or avoidance conditions; Niflheim called a roguelike-ish grind (collect X before the timer ends, "Mist Echoes"); the sequel is said to have improved the equivalent content; a Gameluster review says 2018's Muspelheim was "a tower of unique combat scenarios" while Ragnarok leaned on gimmicks. Layout randomisation in Niflheim is NOT confirmed by any source I read.
- Lesson [I]: optional challenge realms are valued as endgame; a "do the same task under a new limiter" structure is what reads as grind.

### 2.5 Tomb Raider (2013)
- [V weak: user reviews and blogs] optional tombs are short (about 5 minutes each, one puzzle apiece, around 45 minutes total in one review), considered too easy and few, yet many players said they wanted more rather than fewer; counts quoted (7, 8, 9) disagree.
- Lesson [I]: short single-idea optional rooms are appreciated; the complaint was quantity and difficulty, not format. Matches our "one idea per room".

### 2.6 Okami
- [V weak: reviews] puzzles too easy and brush mechanics over-used in dungeon puzzles; dungeon sizes uneven (some barely dungeons, some long); backtracking complaints but a few save points inside dungeons and fewer obtuse puzzles were praised; pacing disputed (slow vs great).

### 2.7 Death's Door
- [V] Game Informer Afterwords with the developers: early game is guided, level design unlocks shortcuts over time as a mastery mechanic; deeper exploration is saved for later; no map by design: Foster dislikes maps because players consult them instead of learning the space, and maps make secrets too easy; Dark Souls is the model (players memorise landmarks); they admit the isometric camera makes it harder; the game avoids forcing backtracking and optional upgrades are reached through the central hub; the post-credits secret content ("fine if most players never find it") was planned late. They greyboxed each level with ProBuilder first.
- [V] One review says lack of a map made backtracking harder in the Mushroom Dungeon.
- Interesting note for us: this is the same toolset (ProBuilder greybox) we use.

### 2.8 Tunic
- [V] Gamedeveloper interview with the Tunic team: "content for no one" (secrets hidden so well the team is satisfied that they merely exist); one bombable wall changes how the player sees every wall ("every other wall becomes a question"); manual pages hold essential information in obvious places and less essential in hard places, to control how much the player learns; the glyph language makes the player feel like an outsider; guidance by isometric camera, positioning and letting players explore; "soft" or "firm" gates rather than hard locks, doors left slightly open; early on too many open doors sent players astray, so they tuned it to a "gentle touch" (a Game Developer editor note says some details in this interview were later found to be incorrect).
- [V] Reviews: manual praised as a modern NES-booklet; criticisms: randomised non-numbered page order, "requires way too much outside knowledge", puzzles you cannot even recognise as puzzles, risk of getting stuck.
- Lesson [I]: a physical-artifact-as-hint-system is a lovely idea for a storybook game (a baker's recipe book whose pages hint at a floor's secrets) but must not be required for progression.

### 2.9 Hob
- [V] Gamedeveloper interview (Patrick Blank): the game guides with subtle lighting and camera direction rather than markers; cliffs and depth show distant areas you will reach later; puzzles are environmental and the art was built as modular puzzle pieces; world shifts happen in real time without cutscenes so players keep control; vistas are spread out so the player isn't always looking at the ground. 80.lv on Hob's level design: designers limited custom cameras and shaped areas to suit the default camera instead, with a foliage dither effect for occlusion; mechanics are introduced via environmental hints (a grapple point is understood once you get the glove, then you return to earlier areas); each glove zone is built around its ability (Warp Pads visible as soon as you enter the electric zone); playtesters are not told secrets exist, and secrets players miss reveal problem areas; begin with basic shapes and add placeholder combat to test whether spaces feel cramped, open or vertical.
- [V] Reviews: level design is the best part, compared to ALttP; criticisms: areas look alike, players get lost; the map looks like areas are close when paths are much more twisted (KeenGamer); camera and combat weaker.

### 2.10 Minishoot' Adventures
- [V] Reviews: seamless handcrafted world, each area flows into the next, shortcuts unlock; the dungeons are linear and shown as the Zelda influence; but caves/dungeons have no map and the overworld map is weak early; map fragments reveal dungeon entrances. Lesson: people forgive no map in a world, not in dungeons they must revisit after a break.

### 2.11 Ys VIII / IX
- [V weak] Ys VIII: the Underground Shrine is a gated dungeon opened by a character (Dana) ability and her "virtue" stat; Ys IX: Steam comments say less freedom and dungeons limited by palette. Nothing about their maps verified. I could not find Falcom design commentary. Treat as unresearched.

### 2.12 Hyper Light Drifter
- [V] Alex Preston: team wants to be "subtle and implicit", "being explicit can ruin the mystery"; no spoken text, no numbers on currency; NPCs show images of destinations that appear as map markers; secrets are often marked by a small ground symbol or a platform slightly out of view; reviews praise even small secrets as rewarding; critics (Giant Bomb, Ludus Novus) say it explains too little of the systems.

### 2.13 Echoes of Wisdom
- [V] Reviews: about seven full dungeons with small keys, boss key, map; critics split. Game Informer's reviewer found dungeons "superfluous and without challenge" because a few favourite clones (echoes) bypass most tasks, and said they missed solving a defined puzzle with a specific item; later dungeons offered fewer surprises; NWR/Kotaku-type reviews: most platforming can be solved by building bridges from echoes. The review I fetched did not mention rifts or the Still World; I found no developer interview. Rifts as a design (consumed-by-rift world, small alternate-dimension challenge rooms) remain [B].
- Lesson [I]: if a hero has a "solve anything" tool, authored puzzles become optional. For us, 4 heroes with distinct abilities means per-room puzzles must have a defined intended tool but never a lockout when another hero tries something else.

---

## 3. Puzzles in action RPGs and co-op

### What I found [V]
- Gamedeveloper "Effective Co-Op Design": complex puzzles can still work in co-op; busywork (many pieces to turn) feels good when shared and tedious solo; give each player a separate sub-puzzle on larger boards to reduce collisions; co-op levels may be harder because more minds make dead ends less likely; players who are not active feel sidelined, which the author counted as a flaw, so every player's contribution was made necessary; same abilities for everyone but puzzles that fail if everyone does the same thing force role-shifting; design for a specified player count (it simplified levels), noting scaling cost; keep player-vs-player collision so movement needs coordination.
- [V weak] Student/dev write-ups: waiting while the other solves is the main co-op puzzle frustration; one dev disliked that the whole co-op campaign could be beaten by one player while others stood idle and added barriers and role awards; a UEFN tutorial says test whether one person can solve the puzzle alone, and split critical information or actions; a pure-co-op article says a failed turn should leave the waiting player something to think about; Solvimus is described as built on the "waiting for your partner" tension.
- [V] From wave 1 (01-zelda): Tri Force Heroes critique and Four Swords taxonomy (symmetrical / asymmetrical / uniqueness / dependency).
- Not found: Portal 2 co-op designer statements, Genshin/Diablo-style puzzle pacing, or any measured "puzzle length that keeps action-RPG flow". I will not invent a figure. The only measured numbers I have: Tomb Raider 2013 tombs ~5 minutes each (user reports), Persona 5 palaces several hours, Valve's playtest increments 1-2 weeks.

### Inferences [I] for action-RPG co-op puzzle design
1. A puzzle should take about as long as a room fight (1-3 minutes) and have a visible physical cause and effect, because the player's combat rhythm is the baseline.
2. Parallel sub-tasks beat serial ones: put one lever per player on one machine (2 or more levers, any can be thrown by any player) so no one waits.
3. Anti-wait: the solver gets a "ping and mark" that others can act on (carry the crate, hold the plate, kite the guard), and the room's enemies keep applying pressure so waiting players have a job. Make waiting players useful by having a second, non-puzzle task (hold a flank).
4. Puzzle input must degrade: required count k = min(2, players present). Never an exact N.
5. Puzzle state must be host-authoritative and cheap to sync (a few bools).
6. Never gate loot for "solved by player X" so one player gets the reward; the reward is shared.

## 4. Secrets and fair hinting

[V] findings:
- Demon's Souls walls wobble when hit; Dark Souls 1 removed the tell; DS2 changed to an interact button (not hitting) and players complained about walls with no difference from normal walls, while others noted some had a different texture; DS3 returned to hitting; the Elden Ring "50-hit wall" panic was probably a bug (Kotaku/PC Gamer), not intent. Dark Souls support for "illusory wall ahead" player messages is itself a hint channel that trolls corrupt.
- Elden Ring/Dark Souls messages: preset-phrase vocabulary forces indirect, cryptic hints ("be wary of up"); messages can be rated and the writer gets a health boost; false messages (a fake ledge) and trolling exist; designers called it a "simple system" for "new ways to play" (Famitsu interview); there is no source that the trolling was intentional design.
- Super Metroid/Zelda: hints come from sight (disconnected blocks, cracks), recycled enemies, and showing the lock before the key.
- Tunic: "one bombable wall makes every wall a question"; Hyper Light Drifter/Hob: small ground symbols, out-of-view platforms; Hob playtesters are not told secrets exist.
- Hollow Knight: secret walls are not on the map; an ability-linked symbol (crystals) appears in foreign zones as a hint.

Tell ladder [I, with the above as evidence]: (1) visible lock (crack, grate, glint), (2) environmental motif that rhymes with the verb (draught moving flame, mismatched mortar, a soot-free patch), (3) player-authored hint (ping, "mark" from co-op), (4) meta tell (the map pocket). Every secret gets at least rung 1 or 2. Rewards scale with the subtlety.

Co-op pings as social hinting [I]: Elden Ring's messages are asynchronous, limited vocabulary, and rated. For 2-4 players we already have live teammates; the useful part is the limited vocabulary (a small icon set: "careful", "look here", "secret?", "treasure", "danger below"), which prevents text chat reliance and makes hints legible across a language barrier. Persist a "mark" on the floor for the party's remaining run, not across parties (no UGC moderation, no trolls). Cost: **cheap**. Optional later: ghost marks from a previous dive on the same seed ("a previous party left a mark here") as a purely friendly Easter egg [I].

---

## 5. Cross-cutting patterns (new)

1. Players forgive no map when the world is a single memorable space (Death's Door, Dark Souls), not when the area is a repeated, revisited dungeon (Minishoot caves, Death's Door's Mushroom Dungeon). Our floors are different every run, so we need a map.
2. The most-complained map failures are about access, not content: buying from a hidden vendor (Cornifer), updating only at rest points, hiding doors outside the selected room (Prime), a map that misleads about distance (Hob).
3. Late fetch-quests that force structured revisits (Prime Artifacts, Silksong bell quest) are the most-resented use of backtracking.
4. Teach by lock-in, demonstration and foreshadowing, never by text (Super Metroid, Hob's Warp Pads, Hollow Knight's key-usage hints).
5. Short, single-idea optional rooms are loved but must be several and not trivial (Tomb Raider 2013, Okami).
6. Powerful universal tools make authored puzzles optional (Echoes of Wisdom); every-puzzle-needs-the-item is the opposite complaint (TP Spinner). The middle is "intended tool plus fair alternatives".
7. Length without new mechanics reads as padding (Persona 5 later palaces, GoW Muspelheim trials, Niflheim grind).
8. Readability beats realism (L4D fog fix, Taylor #1).
9. Every secret needs rung 1 or 2 of the tell ladder.
10. The best advice on process is Valve's: playtest rough, small increments, watch don't ask.

---

## 6. For Sleeping Calamity

Tags: **cheap** = Phase 1 size, **later**, **big**. Checked against: 3D third-person real-time, co-op 2-4 with any subset, procedural floors from authored rooms, dive-and-bank, storybook tone.

### 6.1 Authored rooms (the pool)
1. **Room card with one-sentence concept and a "teach/use/twist" beat.** (Taylor #4, Hayashida via file 06.) Metadata fields: concept, hazard verb, tool used (hero ability or shared), intended solve time. **cheap**
2. **Show the lock before the key** (Zelda crack): the room's puzzle element is visible from the door, while its solving tool may be found elsewhere on the floor. For example, a sealed hatch visible on floor 1 whose key is a hearth-mark tool found on floor 3. **cheap** for the sealed-door, **later** for cross-floor.
3. **Per-room "verb sign"**: the ability motif is attached to the thing (flame-lit glyph = fire hero can ignite; frost-crack = cold). Use it consistently like Hollow Knight's crystal symbols and Hob's glove zones. Also fits Schatz-style fixed language from file 06. **cheap**
4. **Ability puzzles accept a fair alternative.** Each puzzle room has an intended tool plus a second route (a pushed crate, a thrown flask). Never lock out other heroes (Echoes lesson, TP lesson). **cheap**
5. **Puzzle size.** Authored puzzles 1-3 minutes, one idea, no serial chains inside a combat room; combat and puzzle in the same room use the Hob rule: the arena is the puzzle's context, not an interruption. Optional challenge rooms may be 5 minutes (Tomb Raider tombs number). [I] **cheap**
6. **Threat readability.** Light the encounter space and silhouettes (L4D fog lesson); use darkness for corridors and secrets, not for enemy spawn areas. **cheap**
7. **Greybox and playtest each pool room with placeholder enemies before art** (Hob, Death's Door ProBuilder, Valve). A room that fails playtest never ships to the pool. **cheap**
8. **At least one room per sub-theme is a "vista" room**: a high ledge view down on another room or the exit (Hob vista points, Taylor #9). **cheap-to-later**
9. **Quiet rooms by design**: each floor includes 1 low-intensity room (Taylor #5: peaks and valleys; Valve rhythm). **cheap**
10. **Chest rooms and dead ends hold a micro-story** (file 06). Unchanged.

### 6.2 Set-piece floors (4, 8, 12 + boss)
1. **Floor mechanic chosen from the band's verb**, taught in a safe vestibule (Super Metroid lock-in), used with enemies, twisted. Example for oven maze: a heat gauge on floor 4 that opens/closes dampers. **cheap**
2. **Graph, not chain** (Jaquays): at least 2 entrances into the climax area, one conditional/secret route, one return shortcut to the entrance that stays open once opened (Hollow Knight one-way lever, Dark Souls elevator). A floor that is cleared stays cleared, so the shortcut must be persisted in the host's floor state. **cheap**
3. **Show the sleeper early, route indirect** (Hob "cliffs and depth show where you'll go"). **cheap**
4. **Trap-door ability lock for the mini-boss arena** (HK Mantis Claw example): the elite's room can be entered but exited only after the floor's tool is found, ensuring the player has the verb before the boss. In co-op, the lock applies to the party. **later**
5. **No late fetch-quest** (Prime Artifacts): collectibles at set-piece floors are done in one pass; "pieces left" counter is the cure. **cheap**
6. **One reveal; sustain quality**: keep floor length near generated floors' time, not 3x (Persona 5 palaces, Lost Izalith). **cheap**
7. **Different ending per band** (Skyrim sameness): rescue / escape / collapse. **later**
8. **Playtest cadence**: build 15 minutes of rough, playtest, reprioritise (Valve). Apply to each set piece before polish. **cheap**

### 6.3 Puzzle/gimmick per band (proposal sketches; names are placeholders, not locked)
- Rule of thumb [I]: one band gimmick, one tool, 3-5 pool-room variants, and one cross-room link per floor (from file 01), with the gimmick state a single host-owned flag.
- Band 1 (cellars, heat/flour): damp/dry floors, dampers and ovens. Puzzle verbs: open/close damper, redirect steam, ignite flour. Co-op k-of-N: two vents to hold open (solo: weighted plate or a timer).
- Band 2 (e.g. cisterns, water level): rises/lowers per floor from one wheel in the entry room (Water Temple lesson: one-touch). Co-op: wheel can be turned by anyone; a second wheel speeds it.
- Band 3 (e.g. deep roots, light/dark): carried lanterns reveal hidden walls; a "wick" resource. Co-op: lanterns stack range.
- Each tool is reused in later bands for optional branches (TP Spinner complaint).
- Echoes lesson: no hero ability can skip a puzzle outright; an ability may shorten it (fire hero lights both braziers at once).

### 6.4 Secrets
1. **Tell ladder rule**: every secret has rung 1 or 2; reward is visible (a glint through the crack). **cheap**
2. **Generator picks secret candidates from empty grid cells adjacent to 2+ rooms** (Isaac-style map deduction from file 06), plus a "map pocket" tell. **cheap**
3. **Show-the-lock-first secrets**: a visible sealed alcove on an early floor opens with a tool found later (Zelda crack). Persist in save so the player returns. **later**
4. **Tool-glyph secrets** (HK crystal motif): the same glyph as a hero ability on a wall means "this hero can open this". **cheap**
5. **Co-op hearth mark** stays as parked: always a solo route; the co-op route gives a faster/larger reward only if it does not out-reward the solo route (file 01 rule). **later**
6. **Pings, not chat**: a small icon vocabulary of party marks (see section 4). Marks persist for the dive, not across parties. **cheap**
7. **Secrets are not on the map until found** (Hollow Knight), but the cracked room's empty pocket is visible, so the map gives a deduction clue. **cheap**
8. **Never** a no-tell secret, never an N-hit wall, never required for progress.

### 6.5 Maps
1. **Auto-updating floor map, wireframe, rooms revealed on entering a door or seeing the room through a window.** No vendor, no rest needed (HK lesson). **cheap**
2. **Per-room state** (RE rule from file 06): unseen / has loot or pieces left / cleared. Combine with Prime's colour-coded doors by lock type, all doors shown even in unselected rooms (Prime complaint). **cheap**
3. **Map as loot**: a "floor plan" pickup reveals the layout at the start of a floor (Super Metroid map room, Hollow Knight map purchase). Without it, the map still fills in as you go. Price/shop version in the hub before a dive. **cheap**
4. **Map shows distance honestly**: draw corridors to scale or at least in order (Hob complaint). **cheap**
5. **Shared map reveal in co-op**; each player's visited rooms are the union. **cheap**
6. **Pieces-left counter replaces the compass.** Done.
7. **Optional "bell" ping from a hero ability** that shows nearby secrets (cost: a stamina bar or shared cooldown). **later**
8. **Map-poking ("check unexplored exits")**: show unexplored door markers; secrets are not shown. Matches Silksong behaviour and nudges completion. **cheap**

### 6.6 Conflicts / cautions
- A map that always updates is more generous than Hollow Knight/Death's Door. Reason: floors are regenerated each dive, so there is no mental-map benefit to withholding it.
- "Show the lock before the key" across floors needs per-world persistence of sealed alcoves; it is a save-data section (risk-reviewer territory).
- Per-band gimmick state is networked; keep it one host-owned flag per floor.
- Intended-tool puzzles with 4 heroes means designing 4 solutions per room; budget by giving each puzzle one intended hero verb and one generic fallback.
- Puzzle lengths in 6.1 #5 and the co-op rules in section 3 are my inferences; no source measured them.

---

## 7. Sources
Primary or near-primary:
- Alexandrian (Caverns of Thracia / xandering): https://thealexandrian.net/wordpress/13085/roleplaying-games/jaquaying-the-dungeon ; https://thealexandrian.net/wordpress/13132/roleplaying-games/jaquaying-the-dungeon-part-5-jaquaying-for-fun-and-profit
- Jaquays biographical: https://en.wikipedia.org/wiki/Jennell_Jaquays ; https://en.wikipedia.org/wiki/The_Caverns_of_Thracia ; https://gamasutra.com/design/obituary-jennell-jaquays-game-designer-passed-away-at-age-67
- Valve GDC 2006 HL2 design process (PDF): https://cdn.fastly.steamstatic.com/apps/valve/2006/GDC2006_HL2DesignProcess.pdf
- L4D lighting: https://shacknews.com/article/56603/left-4-dead-developer-illuminates
- GMTK Silksong world design: https://gmtk.substack.com/p/the-world-design-of-hollow-knight
- Hollow Knight wiki (Cornifer): https://hollowknight.wiki/w/Cornifer
- Retro Studios MIGS 2007: https://www.gamedeveloper.com/game-platforms/migs-2007-retro-studios-on-the-journey-of-i-metroid-prime-i-
- Death's Door developers: https://www.gameinformer.com/afterwords/2021/09/22/afterwords-deaths-door
- Tunic team interview: https://www.gamedeveloper.com/design/designing-content-for-no-one-an-interview-with-the-team-behind-tunic
- Hob level design: https://80.lv/articles/the-level-design-of-hob ; https://gamedeveloper.com/design/inside-the-ever-shifting-levels-of-runic-games-i-hob-i-
- Hyper Light Drifter: https://gameinformer.com/games/hyper_light_drifter/b/pc/archive/2016/03/22/playing-the-opening-hours-of-hyper-light-drifter%27s-mysterious-world
- Dan Taylor talk (not watched): https://gdcvault.com/play/1017803 ; https://archive.org/details/GDC2013Taylor_20151117

Secondary:
- Taylor summaries: https://roderickomen.wordpress.com/2013/04/01/164/ ; https://www.gamedeveloper.com/design/ten-principles-of-good-level-design-part-2- ; https://amysgamedevelopmentblog.wordpress.com/2017/09/22/good-level-design/ ; https://80.lv/articles/gdc-ten-principles-for-good-level-design/ (no list)
- Sly Flourish checklist: https://slyflourish.com/simpler_jaquay_style_maps.html
- Super Metroid: https://gamedeveloper.com/design/the-invisible-hand-of-super-metroid
- Zelda lock and key: https://howtomakeanrpg.com/r/a/zelda-lock-and-key.html ; Zelda Wiki: https://zeldawiki.wiki/wiki/Weak_Wall
- HSLU Hollow Knight analysis (PDF, student work): https://blog.hslu.ch/games/files/2021/11/Metroidvania_Analysis.pdf
- Dark Souls interconnection: https://www.thegamer.com/dark-souls-1-fromsoftwares-magnum-opus-of-interconnected-level-design/
- Persona 5: https://twinfinite.net/2017/04/persona-5s-dungeon-design-is-a-smart-change-for-the-series/ ; https://prismatictyrant.substack.com/p/persona-5-retrospective-part-4 ; https://www.avclub.com/persona-5-is-too-damn-long-1798262317
- God of War 2018: https://en.wikipedia.org/wiki/God_of_War_(2018_video_game) ; https://gameluster.com/review-god-of-war-ragnarok-the-nature-of-an-end/ ; https://superjumpmagazine.com/god-of-war-left-me-unenthused
- Tomb Raider tombs: https://worthplaying.com/article/2013/3/8/reviews/88422-ps3-review-tomb-raider/ ; https://steamcommunity.com/app/203160/discussions/0/846947320682989295
- Okami reviews: https://www.onderhond.com/blog/okami-review ; https://archive.rpgamer.com/games/other/ps2/okami/reviews/okamirdrev1.html
- Minishoot reviews: https://godisageek.com/?p=292448 ; https://www.gameinformer.com/review/minishoot-adventures/a-link-between-genres
- Echoes of Wisdom: https://gameinformer.com/review/the-legend-of-zelda-echoes-of-wisdom/pulled-in-two-directions ; https://nintendoworldreport.com/review/68832/the-legend-of-zelda-echoes-of-wisdom-switch-review
- Metroid Prime player sentiment: https://www.gamespot.com/metroid-prime/user-reviews/2200-113798/ ; https://omegametroid.com/?p=7567
- Co-op: https://www.gamedeveloper.com/design/effective-co-op-design
- Elden Ring messages: https://www.superjumpmagazine.com/the-genius-communication-of-elden-ring/ ; https://epiloguegaming.com/rhetorical-analysis-of-player-messages-in-elden-ring/
- Illusory walls: https://www.pushsquare.com/news/2022/03/elden-ring-community-shocked-by-new-fake-wall-revelation ; https://www.pcgamer.com/uk/this-50-hit-elden-ring-illusory-wall-is-probably-a-bug/ ; https://darksouls.fandom.com/wiki/Illusory_Wall_(Dark_Souls_III) ; https://steamcommunity.com/app/236430/discussions/0/558756256212317762
- Hollow Knight map sentiment: https://steamcommunity.com/app/367520/discussions/0/598518313861026452

## 8. Gaps carried forward
- Boss Keys episodes on Metroid Prime and Dark Souls: still only snippets. The Hollow Knight/Silksong episodes are covered via Substack and secondary sources, not the transcript.
- Taylor: principle titles verified from three summaries; talk not watched.
- Valve: "weenie"/landmark use by Valve not found; L4D flow numbers not found.
- Persona 5 safe rooms/calling cards, GoW Niflheim layout randomisation, Ys VIII/IX dungeon design, Echoes of Wisdom rifts, Portal 2 and Genshin co-op puzzles: not verified (search budget exhausted).
- Zelda "hollow sound" cue: no source found.
- All puzzle-duration and co-op degradation rules above are [I].
