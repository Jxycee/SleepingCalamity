# 01 - Zelda-family dungeon design and Zelda-likes

Confidence tags: [V] = verified from a fetched/searched source this session; [R] = recalled from my own knowledge, not re-verified; [I] = my inference.
Honest caveat: many pages 403'd (amara transcripts of Boss Keys, zeldadungeon.net debates, gamespot). Boss Keys findings below are mostly [R] plus thin [V] snippets. Chamber Dungeon rules, ALBW design, Tri Force Heroes rationale, Water Temple fixes are [V].

## 1. The Zelda dungeon as a machine

### Locks and keys (Mark Brown, Boss Keys)
- [V] Brown frames dungeons as keys and locks: small keys = many keys for many locks; the Nightmare/boss key = one key for one lock; the dungeon item = one key for many locks. (amara transcript snippet via search.)
- [V] Brown built a mapping notation for dungeon layouts and judged dungeons by branching, player choice and backtracking; he said all Zelda dungeons fall into three broad complexity categories (zeldadungeon interview snippet). [R] The three groupings are roughly: linear (a chain), "hub/branching with choice", and rich lock-graphs with backtracking. I could not verify exact category names or his charts.
- [R] His recurring conclusions: (a) the best dungeons make the player hold a mental map and return to places with new tools (Link's Awakening is his model of varied locking techniques and backtracking, [V] confirmed via roomescapeartist); (b) Majora's Mask uses the physical space itself (rotation, Z-axis) as the puzzle, hard to do and satisfying [V]; (c) OoT is praised for setting variety but criticised for frequent linearity [V]; (d) Twilight Princess is nearly all-linear but works because it leans into that (each dungeon a tightly scripted sequence) [V snippet]; (e) A Link Between Worlds is almost fully non-linear, with pros and cons [V]; (f) Minish Cap shows bad hint delivery and examines linearity vs choice [V]; (g) Zelda 1 has poorly tuned small key counts and players skipping required items [V].
- [R] Brown's well-known point on linearity: a linear dungeon is easier to pace and teach, a non-linear one gives agency but risks lost players and a flatter difficulty curve. No verified ranking found; I could not retrieve his "best dungeon" ordering. Do not cite one.

### The parts
- Small keys: consumable, local currency; the key count per dungeon is a hidden constraint. Zelda 1 criticism: too few/too many keys lets players skip content [V]. Randomizer community note: logic assumes the worst-case spend of keys, because a key spent on the wrong door can soft-lock (OoTMM wiki) [V].
- Boss key: one lock, gates the boss; usually placed behind the dungeon's hardest section, so it is the dungeon's "climax token" [R].
- Dungeon item: teaches a verb in safe rooms, opens paths, then is tested by the boss ("teach, use, test"). [R] Well-known design pattern; [V] that fans label the boss-item tie as a key quality (zeldadungeon "best use of a dungeon item in a boss fight" debate title).
- Map and compass: information economy. [R] Map shows layout, compass shows chests/boss; found mid-dungeon so players choose between exploring blind and spending time to buy information. In ALttP the dungeon's compass shows chests count, so a player knows when a dungeon is "done".
- Central room / hub: [R] many dungeons orbit a landmark room (Forest Temple twisted hallway, Water Temple's tower, Stone Tower, Snowpeak mansion hub) that players return to, giving orientation and a spatial "thread" for backtracking.
- Dungeon-wide state changes: Water Temple water levels, Stone Tower Temple flip, Great Bay temple current directions, Deku Palace/Woodfall water, Ikana (rotation), Wind Waker Earth/Wind Temple scaffolding [R]. They turn a static graph into a stateful puzzle; the cost is the cost of moving state (Water Temple, below).
- Backtracking inside a dungeon: [V] Link's Awakening is praised for locking variety plus backtracking; [V] Phantom Hourglass/Spirit Tracks tower backtracking is a split complaint (see co-op section).

## 2. Era by era

- Zelda 1 [V/R]: 9 dungeons, mostly branching with doors that open by key, bomb or room-clear; any order for several. Criticised: key counts, optionality of items, little signposting. Replayed as "open and bare".
- ALttP [R]: first pattern of item-gated dungeon plus big key, map, compass, a bow-boss pairing. Dark World dungeons can be partly done in two or three orders but items gate them. [V] Aonuma's team believed ALttP allowed multi-dungeon play but replaying it showed it did not (Iwata Asks).
- Link's Awakening [V]: Brown's favourite for lock variety. 2019 remake keeps layouts.
- OoT [V/R]: dungeons are real 3D mazes. Water Temple: the Iron Boots swapping through the pause menu was the most-cited gripe; Aonuma apologised and the 3DS version made boots a quick-assign item, added wall markings and brightened the temple. Dissent: reputation for tedium survives; some still call it a favourite. The remake (Switch 2, 2026) shows Forest Temple graphics and camera changes, time passing inside dungeons, a boss-rematch hall; whether dungeons are redesigned is NOT confirmed (reports conflict; Engadget says original dungeons, not a freeform reimagining) [V snippets, unverified].
- Majora's Mask [V]: four big dungeons, each built around a spatial gimmick (Z-axis, rotation, water flow); fans praise the rotation gimmick as unforgettable.
- Wind Waker [R]: two dungeons (Earth/Wind Temples) reused the "need the whole path to be reshaped by the item" idea; Forsaken Fortress revisits; known for cut dungeon content (the Triforce chart fetch quest replaced them) [R only].
- Twilight Princess [V]: strongest dungeon theming by fan consensus (Snowpeak Ruins house-theme platforming, Forest Temple as best first dungeon debate). Complaint: items such as the Spinner are useful in one dungeon then barely again; Slingshot replaced by Bow soon after; GameCube two-item limit made it worse. Defenders: new mechanics are introduced fast and used well while they're active.
- Skyward Sword [R]: dungeons plus Silent Realms; items more reused; Brown/fans praise Lanayru Mining Facility's time-shift state change.
- ALBW [V]: seven late dungeons in any order; rental items from Ravio return only to shop on game over; Aonuma/Shikata wanted stuck players to do something else; rental motivated rupee spending. Design cost: no item-from-dungeon-N unlocks dungeon N+1, so the dungeons lose the "teach" arc [I, confirmed in the Iwata Asks in outline]. Brown: almost non-linear, pros and cons [V].
- BotW [V]: Divine Beasts criticised for lack of distinct themes, repeated structure; defenders like them. Shrines: short, samey (I did not verify "samey" directly; common sentiment [R]).
- TotK [V]: temples seen as improvement over Divine Beasts, more integrated into the world, but "don't sniff the best of the older games"; Nintendo confirmed "classic dungeons return" before release. [R] Sage abilities complaint.
- Echoes of Wisdom [V]: ~7 full dungeons; reviewers split: IGN praises crafted dungeons and item progression, Destructoid/Eurogamer call them a letdown, VGChartz says emergent play (echoes) clashes with old-school dungeon design; WayTooManyGames: powerful echoes make combat a formality. [R] Echoes let you bypass authored puzzles, which is exactly the "authored puzzle vs freedom" problem.

## 3. Link's Awakening (2019) Chamber Dungeons: a Zelda "procedural" experiment
- [V] Player builds a dungeon by placing room tiles on a heart-shaped grid; need an entrance, a Nightmare (boss) room, a chest holding the Nightmare Key, all doors must connect; room contents not customisable; rooms are lifted from existing dungeons; reward rupees scales with room count; Dampe sets restrictions per challenge; Shadow Link amiibo variant. 
- [V] Reception: lukewarm, "one of the more disappointing features". Kotaku: rooms come from dungeons you already cleared so you do the same things in a different order; self-contained rooms in any order mean no interlocking puzzles; players began optimising for speed over cleverness because challenges are easy; payoff weak. Aonuma said Chamber Dungeons were a reason he revisited the game and was open to a "Zelda Maker" if it succeeded.
- [I] Lesson: Rooms that don't depend on each other lose what makes a Zelda dungeon: state carried between rooms (a lever here changes a room there), items found earlier, orientation. Room-shuffle without a lock-graph is a gauntlet, not a dungeon. This is almost exactly what our 4-6-rooms-from-a-pool generator risks.

## 4. Co-op Zelda
- Four Swords [R]: four Links, same-colour switches, lifting/throwing each other, rupee-competition meta-score.
- Four Swords Adventures [V partial]: designed to be playable solo; blog taxonomy of multiplayer puzzles: symmetrical (everyone does the same thing in different places, "less interesting"), asymmetrical (different roles), uniqueness (each player has different abilities, temporary or permanent), dependency (physical joining, e.g. carry another, strongest teamwork feel) (Blue Renga). [R] solo handled by one player hopping between positions or the puzzle needing only "enough" weight.
- Tri Force Heroes [V]: exactly 3 players for the intended game; critics: no 2-player mode, can't fill a slot with AI, solo mode uses two doppels you swap manually (chore; totem and timing puzzles harder alone), dropouts lose the level, communication limited to scripted emotes. Shikata's reasons for 3: when one player lags the other two must regroup; with 4 the party splits into two teams of two; totems are easier to form with three; "triangular relationship" concept.
- Spirit Tracks [V]: Tower of Spirits, Zelda directs the possessed Phantom, dual-character puzzles; praised as a puzzle high point; backtracking in the tower is a split complaint (some say fixed vs Phantom Hourglass, some say still repeats). [I] Single-player pseudo co-op is cheap when one controller handles two avatars, but that does not scale to 4 humans.
- [I] How N-player puzzles degrade: puzzles keyed to "exactly N bodies" fail in N-1; solo-mode substitutes (doppels, phantoms) are the reported complaint; puzzles keyed to "at least k bodies, k small" or "any one player plus time" degrade gracefully.

## 5. What fans say makes a dungeon good [V partly]
- zeldadungeon.net writers: puzzles are core; variety among dungeons defines the series; atmosphere with a bit of sinister surprise; a stand-out gimmick (MM rotation); distinctive setting (Snowpeak house theme); multiple visits across the game is a good thing; fan top-10 lists weigh layout, difficulty, boss, item, puzzles, and music. [R] consensus also: landmark central room, boss tied to dungeon item.
- Randomizers [V]: OoTR keeps fixed numbers of keys/maps/compasses per dungeon and randomises which chests hold them; settings ladder from "keys removed" to "keys anywhere in the world". Practical meaning: the key count constraint is what keeps lock graphs sound; shuffling keys without logic produces dead ends.

## 6. Zelda-likes
- Ever Oasis [V]: town building plus Zelda-style dungeons (keys, puzzles, bosses). Reviews split: some call dungeons a strongest element, close to 3D Zelda; RPGFan: dungeons the most uneven part, puzzles clash with a central mechanic (the party-swap), design forces too many trips back to town; Vooks: puzzles easy and below Zelda standards. Relevant to us: the town loop that sends players back mid-dungeon was a named complaint. Our bank-or-lose haul and checkpoint floor avoid forced trips, but the "left a gate half-done" problem is the same.
- TUNIC [V partial]: Shouldice deliberately avoided the traditional puzzle-dungeon structure, drawing on Fez-style hidden secrets; mystery and discovery as a pillar. [R] Dungeons are gated by finding keys in the world and by learning the manual; hidden-in-plain-sight secrets.
- Death's Door [V partial, listicle only]: "Zelda-like dungeon design" tight combat, heavy mood; [R] each of three dungeons is a lock-graph around a big door/gate with shortcuts and a boss; short and well paced.
- Hyper Light Drifter, Minishoot' Adventures [V weak]: only listicle-level evidence; Minishoot is metroidvania-style ability gating. Okami, Hob, Oceanhorn, Ys: no sources retrieved, nothing claimed. [R] Hob's strength is world-as-machine with a state-changing world, not discrete dungeons.

## 7. Cross-cutting patterns
1. A dungeon is a small state machine: items and keys are state, rooms are views. Chamber Dungeons failed by removing state.
2. Gimmick per dungeon (MM spaces, TP Snowpeak, Water Temple levels) is the main thing fans remember. BotW's missing themes are its most-cited failure.
3. The dungeon item is a teaching device; TP's one-dungeon items and ALBW's rentals both show the cost of weakening the item-to-dungeon-to-boss link.
4. Linear structure is easy to pace and teach; non-linearity gives agency but weakens the teach arc and the difficulty curve.
5. Co-op scales badly when designed for exact N; the complaints (TFH) are about hard N requirement and clumsy substitutes.
6. State-change gimmicks need cheap controls (Water Temple lesson: a hostile change interface is a bigger complaint than the puzzle).

## 8. For Sleeping Calamity
Answering the main question first.

**Can Zelda-style authored lock-key structure coexist with procedural floors and 2-4 player co-op?** Yes, on three layers, because lock-key logic only needs a graph, not hand-drawn geometry:

1. **Authored shell, generated filler (cheap to later).** Keep the three set-piece floors (4, 8, 12 / boss) fully authored as the dungeon's "Zelda moments": a landmark central room, one gimmick used fully, a dungeon item/tool taught there. Generated floors in the band prepare for it using the band's gimmick in smaller doses. Mirrors Zelda's rhythm (teach, test, climax) and keeps the authoring cost bounded.
2. **Generated lock-key cycles (cheap).** Our graph already links each room to an earlier one plus loops. Add a Zelda lock pass: pick a loop or branch, place a lock (key door, gate lever, gimmick-state door) on the path to the exit or to a treasure room, and place its key at a node reachable without the lock (standard "lock and key" ordering as in Dormans-style mission graphs, [R]). Already have: gate key on a carrier enemy, locked room with chest key. Extend to a second lock kind per band (e.g. a lever sequence), ordered so the key is always on the near side. Tag **cheap** for key-in-chest; **later** for item-style locks.
3. **Band gimmick (later).** Each band gets one dungeon-wide rule that changes rooms (water level, heat, light/dark floor, a door-flip) toggled by a single readable object in the entry room or on checkpoint floors. Use Water Temple lesson: toggling must be one-touch, never menu-heavy. Rooms in the pool get gimmick-aware variants (a flag on the room template, state A and B), keeping room logic authored and generation just assembling.

**Dungeon item / tool**: A DC-style game already has weapons; replace "item opens paths" with hero-specific abilities (four heroes) and a few shared tools per band. Do not put the tool in only one dungeon (TP Spinner complaint): each band's tool must keep use in later bands (**later**; conflicts with parked "movement tools gate optional branches" only in a good way: use them for optional branches in every later band).

**Map/compass economy (cheap)**: Floor map revealed at start would remove exploration; a cheap Zelda-like split: map shows layout only after the first checkpoint/healing point or after a map pickup, compass-like "pieces left" counter already exists. Rewards on doors (planned) acts as the compass.

**Chamber Dungeon warning (avoid)**: Do not make room order a free permutation of independent rooms. Add at least one inter-room dependency per floor (a lever or valve in one room changes another room, a visible key in one room whose door is in another). Else floors become Chamber-style gauntlets. **cheap** (one cross-room link per floor).

**Co-op puzzles scale to party size (cheap to later)**:
- Prefer Four Swords' "dependency" and "uniqueness" over "symmetrical" puzzle types; but write each as "k of N players", where k=1 solo works with a mechanical stand-in (weighted plate with a pushable block, timed lever) and k=2 adds optional speed or reward. This matches the parked "hearth marks" idea with always-a-solo-route.
- Never hard-require N players (TFH criticism). Don't substitute with a swap-controlled copy; use a mechanical stand-in or an AI-less alternate route instead.
- Subset-of-players-present: puzzle data stores "required = min(2, playersPresent)" style rules so 2-of-4 saves still pass. Check that the alternate solo route does not out-reward the co-op one so co-op stays attractive.
- Avoid 2x2 split: Shikata's observation that four players split into two teams of two is a real risk for spread-out rooms; keep puzzle spaces small enough for one camera.

**Boss tied to dungeon gimmick/tool (later)**: The band boss uses the band's gimmick and requires the band tool; matches fan criteria. The boss floor is authored anyway.

**Town loop**: Ever Oasis complaint of forced trips back to town. Keep "dive, bank, return" optional; checkpoint after floor 2 and per-band set-piece floor serve the same purpose.

**Conflicts with current design**: (a) Non-linear any-order bands (ALBW-style) would conflict with the fixed three-band structure and with difficulty scaling per band; keep bands linear, make inside-floor order flexible. (b) Echoes-of-Wisdom lesson: powerful hero abilities can trivialise authored puzzles; give puzzle rooms checks that ignore broad damage abilities. (c) Dungeon-wide state changes need networked state (host-authoritative); keep the gimmick a single host-owned flag.

## 9. Sources
- Boss Keys summary, Room Escape Artist (secondary): https://roomescapeartist.com/2017/09/10/boss-keys-analysis-zelda-dungeons/
- Boss Keys series listing/transcripts (primary, could not fetch): https://amara.org/subtitles/sA8tGCZJvrxP/en-gb/1/download/The%20Legend%20of%20Zelda%20Links%20Awakenings%20dungeon%20design%20%20Boss%20Keys.en-gb.txt ; https://amara.org/subtitles/d7gY3BK6O78V/en-gb/1/download/The%20Legend%20of%20Zelda%20Twilight%20Princess%20dungeon%20design%20%20Boss%20Keys.en-gb.txt
- Zelda Dungeon interview with Mark Brown (primary, 403): https://www.zeldadungeon.net/we-interviewed-mark-brown-of-game-makers-toolkit/
- Nerdist on MM episode: https://nerdist.com/article/legend-of-zelda-majoras-mask-dungeons-deconstructed-in-boss-keys/
- Kotaku, Chamber Dungeons (secondary): https://kotaku.com/zelda-link-s-awakenings-chamber-dungeons-are-a-big-dis-1838038985
- Zelda wiki, Chamber Dungeon: https://zeldawiki.wiki/wiki/Chamber_Dungeon
- Iwata Asks ALBW part 6 (primary): https://www.nintendo.com/en-gb/Iwata-Asks/Iwata-Asks-The-Legend-of-Zelda-A-Link-Between-Worlds/The-Legend-of-Zelda-A-Link-Between-Worlds/6-Rethinking-the-Unquestioned-/6-Rethinking-the-Unquestioned--832156.html
- Siliconera on ALBW non-linear dungeons: https://www.siliconera.com/zelda-producer-tells-link-worlds-non-linear-dungeons-work/
- Water Temple / 3DS: https://themarysue.com/?p=20861 ; https://zeldauniverse.net/2011/05/14/ocarina-of-time-3ds-improved-water-temple/ ; https://www.zeldadungeon.net/?p=10285 (secondary)
- Tri Force Heroes: https://nintendoeverything.com/zelda-tri-force-heroes-miiverse-messages-from-aonuma-and-shikata-why-3-players-instead-of-4/ (primary quotes); https://www.nintendolife.com/news/2015/09/preview_fabulous_fashion_and_questionable_game_design_in_the_legend_of_zelda_tri_force_heroes ; https://www.rpgsite.net/review/4631-the-legend-of-zelda-tri-force-heroes-review
- Multiplayer puzzle taxonomy: https://bluerenga.blog/2005/02/17/designing-multiplayer-puzzles/
- Spirit Tracks reception: https://zeldauniverse.net/2010/02/12/its-the-puzzles-that-make-spirit-tracks-special/ ; https://www.nintendoworldreport.com/review/20507/the-legend-of-zelda-spirit-tracks-nintendo-ds
- TP items debate: https://www.zeldadungeon.net/daily-debate-did-the-limited-use-of-items-in-twilight-princess-bother-you/
- Divine Beasts / TotK: https://www.zeldadungeon.net/daily-debate-how-would-you-turn-the-divine-beasts-into-better-dungeons/ ; https://www.denofgeek.com/games/tears-of-the-kingdom-what-happened-to-the-divine-beasts ; https://zeldauniverse.net/2023/05/11/nintendo-confirms-classic-dungeons-return-in-tears-of-the-kingdom/
- Echoes of Wisdom reviews: https://destructoid.com/?p=608157 ; https://waytoomany.games/2024/12/30/review-the-legend-of-zelda-echoes-of-wisdom/ ; https://gameinformer.com/review/the-legend-of-zelda-echoes-of-wisdom/pulled-in-two-directions
- OoT remake: https://engadget.com/2252620/switch-2-remake-of-legend-of-zelda-ocarina-of-time-arrives-november-5 ; https://www.shacknews.com/article/150646/the-legend-of-zelda-ocarina-of-time-remake-day-night-mechanic (the latter URL title may belong to 2013 or 2026; unclear)
- Fan criteria: https://www.zeldadungeon.net/?p=197323 ; https://zeldadungeon.net/?p=112553 (403, snippets only)
- Randomizers: https://ootrandomizer.com ; https://ootmm.wiki.gg/wiki/Small_Key_Shuffle
- Ever Oasis: https://www.rpgfan.com/review/ever-oasis/ ; https://vooks.net/ever-oasis-3ds-review ; https://www.zeldadungeon.net/inspired-by-zelda-save-your-new-town-in-ever-oasis/
- Tunic: https://www.purexbox.com/features/interview-tunic-creator-talks-zelda-dark-souls-and-the-influences-that-shaped-his-game ; https://www.gamedeveloper.com/design/designing-content-for-no-one-an-interview-with-the-team-behind-tunic (note: Game Developer posted an accuracy correction)

## 10. Open questions
- Exact content of Brown's three dungeon categories, his linearity charts per game, and any ranking: not retrieved (amara transcripts 403; only snippets).
- Whether the 2026 OoT remake changes dungeon layouts or the Water Temple: conflicting, unconfirmed.
- Four Swords Adventures solo scaling rules (per-puzzle) and Spirit Tracks mechanics detail: not verified.
- Okami, Hob, Oceanhorn, Ys, Hyper Light Drifter, Minishoot' dungeons: no usable sources; Death's Door/TUNIC only partly sourced.
- Echoes of Wisdom rift criticism specifically: not found.
- Recommend a follow-up pass pulling the YouTube transcripts of Boss Keys (OoT, MM, ALBW, TP, LA) with a transcript tool.
