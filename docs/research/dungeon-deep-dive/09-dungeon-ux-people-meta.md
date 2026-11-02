# 09 - Dungeon UX, people in dungeons, meta structure, accessibility (wave 2)

Effort note: about 38 searches and 10 successful fetches. Under the 40/15 target; the search tool returns summaries, so many claims below are from secondary summaries. Confidence tags: [V] verified in a fetched/returned source, [S] single or secondary source, [I] my inference. Reddit/GameFAQs blocked; no workarounds tried. Nothing found on DC1/DC2 per-floor minutes or on a DC in-dungeon map beyond what is below.

## 1. Dungeon UX and quality of life

### Maps
- **Zelda map/compass/keys.** Original dungeons each held a map, compass, small keys, an item and a boss [V, Zelda wiki summaries]. Link's Awakening's compass chimes when a key room is entered and marks chests [S]. Aonuma: dungeons were maze-like so the compass was needed to locate the boss; shorter dungeons make it unnecessary; BotW dungeon map is a 3D model with required locations marked [V, Game Informer/Nintendo Everything via search]. Takeaway: the map/compass pair is "two tiers of help", earned per dungeon, and it shrinks in importance as layouts get readable.
- **Isaac.** Minimap fills as rooms are entered; items layer help: Treasure Map shows layout without special rooms, Compass shows room icons without full layout, Blue Map reveals secrets; all three together = full floor [V, wiki.gg]. Help is modular and found, not given. No dev intent quote found.
- **Returnal.** Housemarque built a 3D map plus HUD minimap because vertical levels made a compass or flat 2D map insufficient, and a precise, good-looking map for random levels was hard to build cheaply [V, PlayStation Blog]. Map shows explored areas, which doors were entered, side-path door markers, and has teleporters for fast travel [S, reviews]. Stated principle: remove distraction from second-to-second play.
- **Metroid Prime.** 3D rotating map, consulted constantly; rooms downloaded at map stations [S]. Prime 4 reportedly drops fast travel, so players backtrack (review note) [S].
- **Hollow Knight.** No map at start; maps are bought from Cornifer, incomplete, filled in on resting; pins and compass are optional purchases; the pin for unrescued grubs is a late unlock. Analysis: players choose how much navigation help they want; map open does not pause the game [V, Superjump + wiki]. This is the closest model for "show remaining collectibles": remaining-rescue pins are an earned layer.
- **Hades** has no map (rooms are linear choices with door rewards shown on doors) [I from prior wave notes, not re-verified]. **DC1/DC2**: map powder reveals unexplored parts of the floor map; floors have a screen map [S, StrategyWiki/fandom]. Floor key is dropped by a specific monster [V].
- **Dead Cells** Assist Mode includes "full map reveal ahead of time" as an option [V, wiki + press]. A good precedent: reveal-all is an assist toggle, not default.

### Floor transitions / loading
- Diablo 4 stated design: no loading screens between dungeon levels; in practice players got stuck loading at launch, and Blizzard said in March 2026 it is improving load speeds [S, PCGamesN, MP1st]. Lesson: promise of seamless is fragile; fast, predictable transitions matter more than ideological seamlessness.
- Pikmin 2 sublevels are random but follow a layout scheme; leaving a sublevel warns if treasure/castaways remain; Pikmin 4 warns about uncollected castaways/treasure on exit [V, Pikmin Wiki]. Excellent "you're leaving something behind" UX.
- [I] For us: stairs/door transition with a floor-intro card hides generation. Co-op needs everyone in range (or a short "party gathers" gate) before descending; show pieces left, rescues left, chests left on the intro/outro card.

### Escape / return items
- **DC1:** Escape Powder exits the dungeon via command; mayor replaces it early on; also found as treasure [V, StrategyWiki]. Repair Powder fixes broken weapons (three types) [S].
- **PMD/Mystery Dungeon:** can't return to higher floors; only finish, objective, or Escape Orb leaves [V, StrategyWiki/MD wiki]. Escape item = safety valve that costs the run's remaining potential.
- **Moonlighter:** pendant warps out for a gold fee; reviewers note it fails if you lack the gold and the cost was poorly explained; death keeps only "pocket" items, restart clears pack; 20-slot pack keeps runs short and low stakes [V/S, Steam threads, reviews]. Lesson: never let the escape cost block the escape; explain it.
- **Stardew:** Mines have elevators and a craftable Staircase; Skull Cavern has no elevator and restarts at floor 1 each visit; holes drop several floors [V, wiki]. Shortcut options keep long dives optional.
- **Shiren 6:** rescue is a limited resource (3 per adventure), unlocked only post-game, so help "feels like a reward" [V, Siliconera].

### Inventory pressure, auto-pickup, merchants, checkpoints
- Moonlighter shows slot limits create decisions but also UI complaints ("laborious" sorting) [S]. Moonlighter 2 ties backpack to gold per run [S].
- Our banking loop already supplies pressure (unbanked haul risk). [I] Avoid slot Tetris; pressure should come from risk, not UI chores. Auto-pickup of pieces/currency, manual for gear.
- Hades-style in-run merchants and Sisyphus-type NPC choices (health/currency/darkness) are one-per-run encounters [V, Fextralife via search]. Cheap pattern for a "wandering helper" room.
- Pikmin 2 timer stops underground, Pikmin 4 underground time at 1/6 speed [V]: a dungeon that doesn't punish the player for thoroughness.

### Floor length and session length
- No reliable per-floor times found for DC1/DC2 (searches failed). DC2 reviews call dungeons too long / padded by length [V, RPGamer: "Too-long dungeons" listed as a con; DC1 "dungeon padding" criticism]. DC1 Demon Shaft: 100 floors, difficulty step every 20, no story, "one long grind" [S].
- Hades: successful run about 30 minutes [S, GameBanshee review]; one Q&A says 15-20 [weak]; speedrun record about 5.5 min [S]. Hades II runs 30-60 min [S]. Risk of Rain 2 runs 20-40 min, rarely over an hour [S]. Roguelike blogs: runs from minutes up to about 90 min; suspend/save-and-quit is a genre tradition; Dungeon of the Endless added co-op save after launch [S, forum/CoOptimus].
- Co-op session length: only a weak figure found (about 2.2 h couch co-op, unverified origin). No solid dataset. [I] Plan 30-90 min per dive, with a hard cap on a single "sitting" via checkpoints.
- Math [I]: 12 floors in 90 min = about 7 min per floor including transitions and set-pieces; target 4-7 min for generated floors, 8-10 for set-piece floors, boss 5-8. Band of 4 floors then = 20-30 min, so a session = 1-3 bands. Solo can run faster; scaling enemy count with players already lengthens kills, so check that 4-player floor time stays within +25%.

## 2. People in dungeons

- **Pikmin 4 castaways:** most caves end in a rescue on the final sublevel; the whole game is framed as a rescue; exit warns if castaways remain [V, Pikmin Wiki + Nintendo summary]. Strongest match: each dungeon end beat = a person.
- **Cult of the Lamb:** dungeons are where animals are found/freed and become base followers; follower count gates progress and the dungeon/base halves develop together [S, Wikipedia, reviews]. Design lesson: rescues feed the town directly.
- **PMD rescue:** two senses: client missions inside dungeons, and the Friend Rescue/SOS system where a fainted team asks for help (password/QR/Wi-Fi), rescuer must have cleared the dungeon, thank-you mail with item [V, Bulbapedia]. Super Mystery Dungeon "expeditions" are job-style missions [S].
- **Shiren rescue:** 3 per adventure, post-game, rewards points/trophy [V]. Scarcity makes help meaningful.
- **Dead Cells:** run-reshuffled NPC positions carry backstory fragments (2016 Japanese preview) [S]; Collector converts cells into permanent unlocks [S]. No evidence of captive-rescue as a mechanic (my search did not find one).
- **Hades:** per-region special encounters (Sisyphus, Eurydice, Patroclus) each with once-per-run interaction; they give boons/resources and carry relationships between runs [S]. Dev design intent not found.
- **Darkest Dungeon / Spelunky damsel:** nothing usable found on captive design; Spelunky's damsel drew misogyny criticism and the game lets you swap to a male character or dog [S, Techprincess/Destructoid]. Avoid damsel framing; our sleepers are residents, not trophies.
- **Environmental storytelling:** generic guides: notes, props, frozen moments (a child's drawing) imply former inhabitants and let players infer [S]. [I] for us: each rescue room should hold the sleeper's trade tools and a "what they were doing" tableau, which the town later rebuilds as their shop.

## 3. Meta structure across a dungeon's life

- **Revisits:** DC floors stay cleared (our rule). The Game Developer article: forced backtracking is "a cheap way to lengthen games"; only revisit when the world has changed meaningfully or a revisit adds emotional weight; keep extras optional [V, gamedeveloper.com]. Players describe 100% hunts as tedious when items are gated behind a later tool (Arkham City, Metroid Prime Corruption examples) [S, reviews/forums].
- **Mitigations seen:** Pikmin exit warnings; Hollow Knight pins for remaining grubs; Isaac maps; shortcuts disguise backtracking (Nintendojo comment) [S].
- **Refresh models:** Stardew Mines have optional dangerous variants (Shrine of Challenge) [V]. DC1 floors can be repeated with restock and re-randomised layout [S, search summary of fandom wiki]. Pikmin 2 regenerates on game restart [V].
- **Post-game:** Demon Shaft (DC1, 100 floors, unlocked after clearing, no story) [S]; Isaac Void: mixed-rooms floor, Delirium final boss, complaints that the route forces you through "bad content" and that access is random [S, wiki + forum]; Shiren 1 Final Puzzle 99 floors with no item carry-in, level reset to 1 each run; Shiren 3 post-game mixes reset and level-keeping dungeons; Shiren 6 post-game megadungeon with unidentified items [S].
- **Daily/weekly seeds:** Game Developer survey of daily runs: firm rules are only shared seed and daily change; one-attempt dailies raise stakes but can be gamed by watching videos; Dead Cells allows unlimited tries keeping top score (Bénard), about 7,000 runs/day; Caveblazers' daily built in a weekend; players treat daily as a puzzle with a best line [V]. Isaac: 52 curated dailies a year with restrictions, plus unscored practice [S]. Spelunky: one attempt per day, criticised by some [S].
- **Achievements/speedrun support:** a shared seed is the minimum for speedrun/leaderboard parity; assists in Dead Cells keep achievements but hide leaderboard score [V]. Hades God Mode doesn't lock achievements [S].
- **NG+:** not researched in enough depth; no findings.

## 4. Accessibility and difficulty options

- **Hades God Mode:** 20% starting damage resistance, +2% per death, cap 80%; toggle anytime; Kasavin said a standard easy mode didn't fit the roguelike structure, and increments are small enough to give confidence while learning; not available in the top difficulty (Hell Mode) [V, Inverse/CanIPlayThat]. Banking of resistance after toggling off is disputed between sources.
- **Celeste Assist:** options for speed, stamina, air dashes, invincibility; originally named "Cheat Mode", renamed because "judgmental"; achievements still allowed; in-game text recommends first playthrough without it; designer said it "breaks the game" from his view but doesn't ruin intent [V, Vice/Wikipedia summaries].
- **Dead Cells Assist (June 2022, with AbleGamers):** enemy health/damage, trap damage, parry window, trap speed, auto-hit, lives 1-infinite, map reveal; keeps achievements, hides daily leaderboard score; plus Continue mode, no-blood, font/UI options [V, several].
- **Returnal:** options inside regular settings, not hidden in a separate menu; colourblind palettes, subtitle styling, remap preview [V, PS Blog].
- **Co-op interaction:** Borderlands 4 gives each player their own difficulty (mostly player damage taken and loot, enemy HP unchanged) [V, Xbox Wire/Gearbox via search]. Helldivers 2 and Overcooked use one shared level; Overcooked assist applies to the whole group [S]. Resident Evil 6 requires players to agree [S]. Academic notes: selectable/dynamic difficulty in co-op can hurt advanced players' experience [S, IPSJ]. Xbox guidance: expose individual mechanics, not just presets [S].

## Cross-cutting patterns
1. Help is layered and earned (Isaac items, Hollow Knight purchases, Zelda map/compass); full reveal is an assist toggle.
2. Every game with a safety exit makes it reliable and explained (Moonlighter negative; PMD orb positive).
3. Rescue scarcity or structure (Shiren 3, Pikmin one per cave end) makes each person a beat.
4. Accessibility works best as per-mechanic toggles that keep achievements and progression, and which are separate from the competitive layer (Dead Cells leaderboard hidden).
5. Revisit frustration comes from forced returns without change; optional returns with visible reasons are accepted.

## For Sleeping Calamity

Map design
- Floor map as a pausable-free overlay (Hollow Knight/Returnal style), fog of war by cell. Rooms revealed on entry; corridors by walking. **cheap**
- Layered help, earned per dungeon: base = explored cells and doors; "Hearth lantern"/map item reveals layout; compass marks chests, pieces and the sleeper. Full reveal also as an assist toggle. Matches current "rewards shown on doors". **cheap** (base), **later** (item layers)
- Always show counters on the map frame: pieces left, chests left, sleepers left, key status. Warn on exit if remaining (Pikmin). **cheap**
- 14x14 flat generated floors suit a 2D map; use 3D map only if verticality is added. Co-op: all players share the explored map (host-owned state). **cheap**

Floor length targets
- Generated floor: 4-7 min solo, +25% max at four players; set-piece floor 8-10; boss 5-8. A band of 4 = 20-30 min; a full 12-floor dive about 80-100 min, split into 3 bands with a checkpoint/bank between them so 30-60 min sessions are natural. Test with timed bot/playtest runs. **cheap** to set as budget, **later** to tune.
- Offer suspend/resume of a co-op dive (host save at band end at least), since Dungeon of the Endless had to bolt on co-op save. Interacts with banking and Stir; define that save-and-quit keeps the Stir value. **later**
- Floor intro card (name, band, pieces/sleepers/chests counts). Descent gate waits for living players within a radius; downed players are carried along. **cheap**

Escape items
- Escape Powder-equivalent that always works, free to use but ends the dive with the unbanked haul at risk only if not banked at a bank point (or banks the haul at a penalty); never gated by currency (Moonlighter lesson). Starter stock given by the mayor, restocked free. **cheap**
- Escape is a party action (host or vote), not per player, unless "leave alone" is allowed with only your carried share. **later**
- Repair-style item only if weapon durability exists; otherwise skip. **later**
- Optional shortcut shafts (Stardew holes) to the band start after clearing. **later**

Rescue / NPC beats
- Each band floor 4 = sleeper rescue (already planned). Add a lighter "stirring sleeper" mini-beat on 1-2 generated floors per band: a small room vignette where a resident sleeps, with a carry-home prompt that costs a visible burden (slower, can't dash) until banked, aligned with Stir risk. **cheap**/**later**
- Wandering helper room (Hades-style once-per-dive choice: heal, pieces hint, currency) as one of the 12 combat-room pool alternatives. **later**
- Environmental storytelling: every rescued sleeper's room contains tools and a tableau of what they were doing; keepsake pickups return to residents (parked idea). Avoid damsel framing; sleepers are rested, not helpless props. **cheap**
- Pikmin-style: a rescue is the dungeon's last beat and its payoff shows in town the next morning. **cheap** (content), conflict: none.

Revisit rules
- Floors stay cleared (keep). Revisit reasons must be visible: map pin for uncollected pieces/chests, bank-limit "pieces left". Never gate a collectible behind a later tool without a map pin and a reason (Game Developer guidance). Movement-tool gated branches are optional and flagged. **cheap**
- Optional "Stir the floor" revisit mode re-rolls a cleared floor for loot with a new seed (DC1 restock model) for farming, not required for 100%. **later**

Post-game and challenge
- Post-game dungeon: a themed deep shaft with fixed difficulty steps every N floors, no story, level/gear carry-in rule chosen per mode (DC1 Demon Shaft / Shiren). **big**
- Rule-twist gauntlets (parked) as weekly seeds: host seed + modifier set + unlimited practice, one scored attempt per week, leaderboard optional (Dead Cells unlimited vs Spelunky single attempt). Our host-provided seed makes this cheap to prototype. **later**
- Speedrun support: seed display, fixed-seed practice entry, deterministic generation (already planned). **cheap**
- NG+: unresearched; defer. **later**

Accessibility and difficulty in co-op
- Assist options (God Mode style, per-mechanic): damage taken, enemy health, trap damage, parry/dodge window, auto-aim/lock-on, map reveal, Stir rate, infinite lives. Keep progression and achievements; hide only competitive seed scores. **cheap** (a few) / **later** (full list).
- Hades-style gentle ramp: optional "Lullaby" resistance +x% per failed dive, capped. **cheap**
- Co-op: assists are per player where they only affect that player (damage taken, aim, map overlay, lives); world-affecting ones (enemy health, Stir rate) are host-set and shown to everyone; difficulty scaling to present players stays separate from assists. Borderlands 4 model for player-side; Overcooked model for world-side. **cheap** to specify now.
- Put options in the standard settings (Returnal), name them neutrally (Celeste), and show a first-run note without moralising.

## Sources (P primary, S secondary)
- S https://caniplaythat.com/2021/08/11/hades-god-mode-explained-by-supergiant-games (Kasavin via Inverse) 
- S https://www.inverse.com/gaming/hades-god-mode-interview
- S https://www.vice.com/en/article/d3w887/celeste-difficulty-assist-mode
- S https://blogs.tuni.fi/playlab/features/too-steep-a-hill-to-die-on-celeste-assist-mode-and-accessibility/
- P(blog) https://blog.playstation.com/?p=348960 (Returnal UX)
- S https://www.superjumpmagazine.com/getting-lost-by-design-in-hollow-knight/
- S https://hollowknight.wiki/w/Map_and_Quill_(Hollow_Knight)
- S https://www.siliconera.com/shiren-wanderer-tower-fortune-dice-fate-rescues-feel-like-reward/
- S https://www.gamedeveloper.com/design/backtracking-and-non-essential-areas
- S https://www.gamedeveloper.com/design/the-24-hour-ticket-examining-daily-runs-
- S https://www.pikminwiki.com/cave
- S https://deadcells.wiki.gg/wiki/Assist_Mode ; https://gamedeveloper.com/design/dead-cells-devs-drop-surprise-accessibility-update ; https://fextralife.com/dead-cells-devs-want-upcoming-accessibility-options-to-remain-tough-but-fair/
- S https://strategywiki.org/wiki/Dark_Cloud/Items ; https://darkcloud.fandom.com/wiki/Dungeon
- S https://rpgamer.com/review/dark-cloud-2-review-3/ (dungeon length criticism)
- S https://mobile.mysterydungeonwiki.com/wiki/Rescue_Team_DX:Escape_Orb ; https://bulbapedia.bulbagarden.net/wiki/Friend_Rescue
- S https://bindingofisaacrebirth.wiki.gg/wiki/Treasure_Map ; https://bindingofisaacrebirth.wiki.gg/wiki/The_Void ; https://bindingofisaacrebirth.wiki.gg/wiki/Daily_Challenges
- S https://stardewvalleywiki.com/Skull_cavern ; https://stardewvalleywiki.com/The_Mines
- S https://www.pcgamesn.com/diablo-4/no-loading-screens
- S Moonlighter: https://steamcommunity.com/app/606150/discussions/0/4768721849831072479 ; https://uxdesign.cc/ux-in-video-games-part-1-moonlighter-17c7fe166c12
- S https://nintendoeverything.com/aonuma-on-how-nintendo-approached-making-dungeons-in-zelda-breath-of-the-wild/ ; https://zeldauniverse.net/2017/02/10/dungeons-havent-escaped-the-overhauling-of-zeldas-conventions/
- S https://news.xbox.com/en-us/2025/08/12/how-borderlands-4-built-for-co-op-ground-up/
- S https://www.co-optimus.com/article/13098/dungeon-of-the-endless-now-has-a-co-op-save-feature.html
- weak https://voxbooster.com/blog/couch-co-op-statistics-2026 (unverified co-op session figure)

## Open questions
- Real per-floor times for DC1/DC2 (none found); real co-op session length data.
- Dead Cells prisoner/captive mechanic (probably not a thing); Darkest Dungeon captives (none found).
- Developer intent for Isaac minimap, Hades NPC encounters, Pikmin 4 cave castaway placement (Nintendo "Ask the Developer Vol. 10" would be the primary source to read).
- New Game+ and achievement-tied-to-dungeon patterns not researched.
- Whether Hades God Mode resistance persists when toggled off (sources disagree).
