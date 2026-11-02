# 02 - Isaac as a dungeon model, and Isaac-like structure in 3D

Confidence key: [V] verified in a fetched source; [S] snippet/secondary only; [I] my inference.

## PART A - The Binding of Isaac

### A1. Floor skeleton and special-room placement [V, wiki.gg Level Generation + Boris the Brave]
- Floorplan grows outward from the start; any cell that gained no neighbour is a "dead end". Dead ends are sorted by distance from start (farthest first) and special rooms are assigned to them in a fixed PRIORITY order. When dead ends run out, no more special rooms spawn. A failed roll returns the dead end to the queue.
- Minimum dead ends: 5 base, +1 on every floor after the first, +1 on XL floors, +2 in the Void. (Generation retries until enough exist.) [V]
- Priority list and odds (Repentance values):
  1 Boss (always, farthest dead end). 2 Super Secret (always, second-farthest). 3 Shop (guaranteed chapters 1-3). 4 Treasure (guaranteed chapters 1-3; XL floors get two). 5 Planetarium (1% base; rises if you skipped treasure rooms). 6 Dice or Sacrifice (~36% combined; Sacrifice more likely when hearts are full). 7 Library 1/20. 8 Curse room 1/4. 9 Mini-boss 1/4 (1/2 on floor 1). 10 Challenge (only if health >= max hearts; not floor 1; 50% floor 2; boss-challenge variant on the 2nd floor of a chapter). 11 Vault/Arcade (Arcade guaranteed with 5+ coins on 2nd floor of a chapter; becomes Vault ~2/5 with 2+ keys). 12 Bedroom (~1/50, ~22% when hurt). 13 Secret (always). 14 Ultra Secret (always in Repentance, but only reachable via Red Room tricks).
- Each room type switches OFF at a depth: treasure/shop/planetarium stop from chapter 4, arcade/vault from chapter 5, curse/sacrifice/challenge from chapter 6. [V]
- Several rooms read player STATE at generation time (health vs. max hearts, coins >= 5, keys >= 2, hearts low) - the floor is shaped by the player's current condition. [V] This is a strong, cheap idea: "the dungeon notices you".
- Secret Room: NOT a dead end. It is placed in an empty cell touching 3-4 rooms (weight 10-14, minus 3 / 6 for fewer neighbours), never next to boss/super secret/another secret. Boris: random search for a cell with >=3 neighbours and no dead-end neighbour, relaxing after 300 and 600 failed tries so it always succeeds. [V]
- Super Secret: a dead end connected to exactly one normal room, usually near boss/shop because of the farthest-first assignment. [V]
- Curse of the Labyrinth restricts special rooms to the six farthest dead ends. [V Boris]
- Design commentary (Boris): dead-end placement keeps special rooms off the main path, one door each; farthest dead end for boss gives natural progression; secret room is the opposite (tucked into crowded spots). Floorplan is separate from room contents, so rules tune without touching room pools. [V, commentary]

### A2. How players deduce secret rooms [V, wiki Secret Room]
- Rule of thumb: bomb the middle of a wall facing the gap in the minimap; look for cells that would touch 3-4 known rooms; in closets bomb the end wall. The entrance is never blocked by rocks, pits, spikes, or an enemy spawn point.
- Hints: the minimap's empty-cell pattern is itself the clue. Items add hints (Dog Tooth howl, a floating arrow item, X-ray) or reveal the room (maps) - revealing still requires opening it.
- Open method is a bomb; the wall stays open afterwards (changed from the Flash version). Moon card teleports in.
- Complaint evidence [S, Steam threads]: bomb scarcity = no secret rooms = no snowball; a daily-run softlock where a Super Secret room needed bombs the run never found; players want such skill-independent stucks fixed. Secret rooms sit behind a resource, so luck compounds.
- [I] The deduction works because the minimap is a clean grid of known cell shapes; it will not carry over to a 3D floor unless we give a similarly legible top-down map.

### A3. Room-clear loop and economy
- Doors lock on entry until all enemies die ("arena rule"); mini-bosses, challenge, boss challenge, curse (spiked door, damage on exit) and boss rooms use the same pattern with variations. [V]
- Room-clear drop table (base, wiki Room Clear Awards): nothing 22%, card/pill/trinket 8%, coin 15%, heart 15%, key 20%, bomb 15%, chest 5%. Luck shifts the "nothing" band; Hard mode removes hearts 66% (33% in Repentance). There is no "hearts only if hurt" rule on that page. [V]
- Four currencies: hearts (health), coins (shop), keys (locked treasure room from floor 2 on, vault, library, key-gated alt-path door), bombs (secret rooms, alt-path Mines, rock breaking). Keys/bombs are doubly pressured: they open the best rooms AND are random drops. McMillen's postmortem called them non-essential and randomly placed, never required for progress; they added structure without gating. [V, Game Developer 2011]
- Active-item charge per room cleared: the room count is the "clock" (bigger rooms give 2 charges) [S]. Not verified in detail.
- Devil/Angel deal [V, wiki Devil Room]: base chance only 1%, then additive: no red-heart damage against the boss +35%, no red-heart damage the whole floor +99%, blowing up a shopkeeper +10%, beggar +35%, items add more; multiplier x0.5 / x0.25 if you saw the door recently. Angel replaces Devil if you refused deals before; taking any deal stops angels for the run. Design lesson: the best reward is paid for by PLAY STYLE (flawless floor), not by a resource - gives skill a visible payoff and a built-in "do I risk it?" decision.
- Shops: item prices in coins; floor-1 shop free in Repentance. Treasure room: one pedestal item (rarely 2); it needs a key from floor 2 onward, so the key economy is "currency" that competes with shop/vault/library. [V]

### A4. Curses as floor modifiers [V, wiki Curse]
- Chance of a floor being cursed grows with progression/unlocks: new game 1/80 normal; after Mom 1/30; after later bosses 1/5 to 1/3. Each of six curses is equally likely. Lasts one floor.
- Darkness (dark map lit by player aura), Labyrinth (merges the chapter's two floors into one XL floor with two bosses, two treasure rooms), Lost (no map, +4 rooms), Unknown (hides health), Maze (entering rooms can send you elsewhere), Blind (item sprites hidden until picked up).
- Takeaways: the curses mostly remove INFORMATION (map, health, item identity) rather than add HP to enemies. Cheap to implement, strong "this floor is different" signal. Curse of Darkness already matches our "rare dark floor" stretch idea.

### A5. Chapter structure and alt path [V mixed, wiki + bamboogaming]
- Base run: 4 chapters x 2 floors (Basement, Caves, Depths, Womb) then branch Sheol/Cathedral then Dark Room/Chest; short floors (a floor is ~7-20 rooms; formula known). Repentance adds an ALT PATH replacing the first three chapters' later half: Downpour/Dross, Mines/Ashpit, Mausoleum/Gehenna, then Corpse with Mother as final boss.
- Alt-path doors cost something different each time: a key (to Downpour), explosions/bombs (Mines), two hearts sacrificed (Mausoleum). Knife pieces: piece 1 behind a mirror-world puzzle on Downpour II; piece 2 behind a minecart button puzzle and a chase on Mines II; assembled knife opens the flesh door after Mom to reach Corpse. [V]
- Design lesson: the alt path is a "different route with a toll", each toll = one of the existing resources (keys, bombs, health). It is also unlock-gated, so it appears late.
- Also: Mirror room, Mine cart room, Strange Door etc. are hand-made one-off "event rooms" attached to fixed floors, outside the generic special-room pool. [V]

### A6. Unlocks, challenges, content growth
- Unlocks tied to milestones: finishing each chapter/boss with each character adds items, characters, rooms. Layering gave long-term goals (McMillen postmortem). [V]
- 45 official challenges that grant unlocks; Greed mode with coin-donation unlocks. Greed/Greedier criticised as grindy by some players [S, Steam]. Repentance said by one reviewer to add the fewest "bad" unlocks of any version [S].
- Curse and room odds themselves grow with unlocks (curse chance climbs with milestones). [V]
- [I] Pool growth makes early runs readable and late runs deep; a downside is that new players see a thin game, and some unlocks are pure grind.

### A7. Co-op in Isaac [V, wiki Co-op]
- Local only until Repentance+ added online (4 players). P2 joins as a random "baby" for one heart container from P1; babies share stats but deal half damage, always fly, cannot pick up items/pickups (except special babies). Afterbirth: 4 local players. Repentance: P2-4 pick any unlocked character, ghost revive after the boss dies, shared coins/bombs/keys but separate hearts and items, boss item pedestals = one per player, players deal 20% less damage to bosses. Game over only when all are dead.
- Complaints [S, Steam]: a half-damage baby doesn't fill the missing 50% (net weaker), babies die to one hit late, can't collect or use bombs; co-op felt tacked on. Later patches rebalanced damage.
- [I] Isaac's room is a single-screen arena, so co-op simply shares the screen. It never had to solve camera, split progress, or lag-behind lock.

### A8. What McMillen / Himsl said [V, Game Developer postmortem 2011; S for the rest]
- Skeleton: a Zelda dungeon (keys, bombs, hearts, item per dungeon) proved a familiar frame for the roguelike conversion.
- Growth: each run's character visibly grows, via items, even though drops are random.
- Difficulty: no difficulty menu; the game adapts content as the player progresses, hands better items to skilled play (the 2011 postmortem frames this as the thing that made Isaac different).
- Scope: a one-week jam prototype + about three months part-time; 100 items stacking in combos outran testing - "some bugs took over 100,000 players weeks to find".
- Philosophy [S, Gamedev.net/Game Developer "Raw Meat"]: find the game's "core" (Isaac's was replayability) and strip everything else toward it.
- I did NOT find a GDC postmortem talk on Isaac itself; the written postmortems (2011 and 2012) are the primary sources.

### A9. Why Isaac is replayable (my synthesis, with evidence tags)
- Item synergy: big pool of items that stack and combine, so the build matters more than the layout. [V postmortem: 100 items, mostly stacking]
- Rooms are small and instantly readable; the minimap shows room outlines and unvisited neighbours so the player always picks a door with some knowledge. [I, consistent with wiki's placement rules]
- Short floors with a fixed sequence (find shop, treasure, boss) so a run is about 30-60 min with many decision points. [I]
- The layout only changes the ORDER of known special rooms; most of the variance is items and room pools. [I]
- Therefore, for Sleeping Calamity: the floor generator is the cheap part. The reasons Isaac replays (items) are not in our floor design at all.


## PART B - Room-based roguelites that went 3D (or 3D-ish)

Evidence quality warning: most of the web results were launch-era reviews, Steam-summary aggregators (Vaporlens, AI-generated) and Steam forum threads. Treat aggregator summaries as [S]. I could NOT find developer talks on camera/readability for Hades, Windblown or Ravenswatch.

### B1. Returnal (Housemarque, 2021; 3rd-person 3D shooter)
- Structure [V, Push Square interview Feb 2021]: "hybrid" generation. Treasure rooms, combat arenas, traversal connectors and narrative spaces are hand-built (lighting, props, audio polished); they are connected freely each run. Contents (enemy mixes, counts, elites, chest) are random per visit. Two acts of three biomes; each biome ends in a boss; killing the boss opens a persistent shortcut to the next biome in later runs. Permanent unlocks are mostly TOOLS (melee blade, hookshot) that open previously unreachable areas, plus a databank of weapons/artifacts that enlarges the pool. Runs always start with a level-zero pistol. One difficulty setting.
- Reviewer reading [S]: the world is hand-made rooms shuffled; finite rooms means repetition sets in; "secret rooms and hidden locks appear in the same places" (player feedback); one reviewer says checkpoints/shortcuts reduce repetition.
- Biggest player complaint [V, GameSpot/VGC/GameRevolution coverage]: no save-and-quit mid-run. A run can last hours; PS5 rest mode was the stand-in. Housemarque admitted it, later patched suspend. Community split between "prevents save-scumming" and "disrespects my time".
- Lesson [I]: Returnal's room pool is the same construction we use (authored rooms, randomised connection + contents). The pitfall is pool size: finite rooms, noticeable repetition. Time-respecting save/leave is a design requirement for a dive-and-bank game, which Dark Cloud style already satisfies with banking.

### B2. Hades / Hades II (isometric; Supergiant)
- Rooms are hand-designed and recombined; each biome has its own principles [V, Kotaku quoting Ed Gorinstein]: Tartarus = medium, almost completely walled-in chambers (forgiving for learners, wall-slam damage); small rooms early in a biome and larger later so scale tracks difficulty; Asphodel = lava archipelagos, so every enemy has a dedicated crossing method (hop, float, dash). Team rejected hidden gold in walls and big rooms with scattered doors because they hurt pace and the painted look.
- Hades II: no co-op, by Supergiant's own stated solo design intent [S, secondhand FAQ reports]. The best "feel" reference in this genre deliberately skips co-op, which says something about how hard readable arenas + co-op is.
- Camera: I could not find a primary source for the fixed isometric camera rationale. [I] A fixed camera means authors can guarantee that nothing hides an enemy and that every telegraph is visible; this is the opposite of a free third-person camera.

### B3. Ravenswatch (Passtech, top-down/iso 3D, 1-4 co-op)
- Each area = 18 min of exploration split into four days and four nights, then you must fight the boss [V, CGMagazine]; a forced timer; maps are randomly generated with points of interest [S]; heroes have distinct abilities (like our four heroes).
- Sentiment [S, Vaporlens + CGMag]: co-op is "the best way to play", solo feels poorer, some bosses near-impossible alone. #1 complaint is difficulty scaling in multiplayer (enemies too tanky, fights tedious), only three maps repeat the same POIs, thin meta-progression, a forced timer pushes rushing and lowers replay appeal. Random matchmaking unreliable.
- Lesson: our "difficulty scales to players present" must be tuned by NOT just inflating HP (the reviewers called that tedium). Prefer adding enemies/variants + pace rather than health sponges. [I]

### B4. Windblown (Motion Twin, 2024+, iso 3D, 1-3 co-op, EA)
- Praised: co-op ("seamless and fun"), speed, lighting/colour. Complaints [S, Vaporlens + a screenhype snippet]: too few biomes, repetitive layouts, multiplayer desync/lag/crashes in encounters and shops, flashy effects making attacks hard to read (unfair deaths), meta-progression grind, no true pause (acceptable in co-op).
- Lesson [I]: visual clutter + 3D camera = telegraph readability is the first thing players name. Plan for effect budgets and ground-marker telegraphs.

### B5. Wizard of Legend 2 (3D transition from 2D)
- Steam about 53% (about 2,400 reviews at time of the search), OpenCritic about 85 [S]. Praise: look, spell animations, co-op. Complaints [S, Vaporlens + one Steam demo thread]: performance, narrow/cluttered corridors, sparse generic maps, unclear boundaries (accidental falls), randomisation sometimes yields rooms too hard/too easy, effects hide boss patterns, shared-gold system poorly done, slow progression, "feels like Hades with less precise control" (single Steam user).
- This is the clearest cautionary tale for taking a precise 2D loop into 3D: tight corridors, edge-of-world falls, effects occlusion, and loss of aiming precision.

### B6. Gunfire Reborn (FPS, 1-4 co-op)
- Procedural levels in 3-4 acts each broken into 3-4 stages + boss [S]. Co-op is the most praised mode; solo is "less fun"; boss HP/damage scales with team size making fights longer [S, Co-optimus]. Complaints: generic level construction with little interactivity, too few maps, DLC characters unbalanced in co-op, matchmaking queues. One review says dead players have a long wait because respawn NPCs are rare [S].

### B7. Risk of Rain 2 (open 3D stages, 1-4 co-op)
- Stage = open map, find the teleporter, charge it while fighting the boss; difficulty rises with TIME not with room count; items are per-player pickups, chests/interactables shared [S, Steam].
- Complaints [S, Steam threads 2019-2024]: scaling too steep with players ("easy solo, extremely hard with 4"; "+30% per player too much"), chest prices rise per player so 4 players spend a lot per item, greedy players hogging pickups (responses: it's the friends), dead players waiting until all die, a 2024 patch with frame-rate-dependent gameplay and desync.
- Lesson: shared interactables + per-player loot is the standard split and it works; the unsolved problems are price scaling and time-based difficulty that ignores slower groups.

### B8. Elden Ring Nightreign (3 players, 3 days, shrinking circle)
- 3 days per expedition, a boss at the end of each day, about 45 minutes per run, 8 expeditions; the storm circle forces movement and makes players skip POIs [V, Game Informer]. Complaints [V/S]: unreliable matchmaking, poor solo scaling (hardest for solo), run-ruining randomness (missing the right weapon element), rigid success path, shrinking circle felt like an unfair disruption, no duos (3-stack or solo).
- Lesson: Our Stir meter (40/70/100) is a softer cousin of the shrinking circle. Nightreign shows the pressure clock is divisive when it prevents exploration. Make it a choice (bank now or push) rather than a rail.

### B9. Others (thin evidence)
- Ember Knights: Hades-like, 1-4 co-op, Steam about 92-95% positive (about 6,900 reviews) [S, Notebookcheck summary]. Reception hint: a familiar room loop + co-op, no reinvention, was enough. I could not verify its room-clear / co-op door rules.
- Witchfire (FPS roguelite with extraction): extraction at any time vs push on to lose currency = the same dive-and-bank tension as ours [S]. Complaints: repetitive mission structure, mostly small arenas, healing tied to herb farming.
- Roboquest: Steam about 95% (about 19k) [S]; complaints = early-access removed randomness, stale after 10+ hours, predictable levels, shortcut doors let you skip early areas.
- Ziggurat, Children of Morta, Soulstone Survivors, Rogue Legacy 2: NOT researched in depth; no verified claims here.

## PART C - Cross-cutting patterns on 3D room roguelites

1. The authored-room pool + random connection/contents recipe is universal (Returnal, Hades, Gunfire-ish). Everyone who ships it gets the same complaint after about 20 hours: too few rooms/biomes. [V/S]
2. Co-op is the most praised feature in every 3D room roguelite (Ravenswatch, Windblown, Gunfire, WoL2, Ember Knights) AND the most reported source of bugs (desync/lag, matchmaking). Netcode quality, not design, is the usual co-op killer. [S]
3. Difficulty scaling per player count is the #1 design complaint (Ravenswatch, RoR2, Gunfire boss HP, Nightreign solo, WoL2 shared gold). [S] Tanky HP inflation is the common failure.
4. Death in co-op has a standard menu (ghost, shared health, item revive, ally revive, clear-to-revive, countdown). Good practice per the Game Developer piece: gate revives on room clears, keep them scarce, avoid revive-at-full-HP exploits, use a countdown so waiting has an end, keep the dead involved. [V, Game Developer "Designing death in co-op roguelikes"; Bigosaur "SoaW death"]
5. Telegraph readability is the first 3D-specific problem players name (WoL2, Windblown: effects hide boss attacks). [S]
6. Fixed/iso camera is a design tool: Hades leans on it; the games with freer 3D camera (Returnal) rely on wide arenas. Tight corridors + free camera is the failure case (WoL2). [I plus S]
7. Time/pressure clocks (Ravenswatch day/night, RoR2 time-scaling, Nightreign circle) are divisive; players say they push rushing and hurt replay. [S]
8. Minimap: no source I fetched complained about Returnal's map (one review explicitly called it comprehensive). Isaac's minimap advantage (clean grid of known room shapes + visible gaps for deduction) disappears when rooms are irregular 3D spaces. [S/I]
9. Hades II (the genre's gold standard) shipping solo-only by design is itself evidence that co-op multiplies the cost of readable arena design. [S/I]
10. Room-clear lock in co-op: I found NO published treatment of the "one player lags behind a locked door" problem; only death/revive treatments and one itch devlog (Void Champions: bored waiting player vs unfair fight if the late player arrives). It is an open problem; solve it ourselves (see D).

## PART D - For Sleeping Calamity: "Isaac but 3D, but Dark Cloud"

Constraints recap: real-time 3D third person; co-op 2-4, any subset of recorded players, difficulty scales; generated 14x14 grid of 4 m cells, 4-6 authored rooms/floor; one gate key on an enemy; a key chest for a locked room on 50% of floors; 6-8 chests; pieces + counter; checkpoint after floor 2; banking; Stir meter. Floors are LONG (corridors between rooms) compared with Isaac's 7-20 one-screen rooms.

### D1. What an Isaac-like floor would be in our game
Isaac's structure is: (1) a graph of rooms with dead ends; (2) a priority list that assigns functions to dead ends; (3) state-aware odds; (4) a legible minimap; (5) a few resources that open doors. Our current generator already has (1). What Isaac adds is (2)-(5). Each Isaac system mapped:

| Isaac system | Verdict | Notes |
|---|---|---|
| Special rooms at dead ends, priority order, farthest first | **steal, cheap** | We already create 1-3 dead ends per floor with authored rooms. Put the elective rooms there: vault/treasure, healing fountain, shop-ish "peddler", challenge, secret. Use a fixed priority list so the best dead end (farthest) hosts the reward most worth the walk. Guarantee at least 1 elective dead end by lifting the dead-end minimum (Isaac forces 5+; ours 1-3 may be short; add a retry/graft rule). |
| State-aware roll odds (health >= max -> challenge/sacrifice more likely; coins >= 5 -> arcade; keys >= 2 -> vault) | **steal, cheap-to-later** | Use the host's party state at floor-generation time: low HP -> heal room likelier; many unbanked pieces (high Stir) -> a bank/escape room likelier or a "risk" room; full-HP party -> challenge room offered. Must be computed on the host and sent in the seed bundle so clients build the same floor (floor seed + a few state bytes). Caution: do NOT recompute from client state. |
| Arena rule: doors lock until room clear | **keep, but modify** | Already how we think of combat rooms. Under co-op the lock needs three rules (see D3). Do not lock the entire room; lock only the exits not the entry so a retreating player isn't trapped. |
| Room-clear drops (22% nothing, 15% coin/heart/bomb, 20% key, 5% chest) | **steal the idea, change numbers** | Our rooms are bigger and fewer, so per-room drops should be few but meaningful. Use a pity counter (parked "vignettes with pity counters" idea) rather than pure RNG. Isaac has no anti-flood; pity timers solve the "nothing in four rooms" feel. |
| Keys/bombs/coins as door currency | **partial, later** | Isaac's lesson (McMillen): keys/bombs/coins are *not required for progress*. DC1/DC2 already use a gate key. Adding a second "small key" currency for treasure/vault dead ends can work (the locked room whose key is in a chest is already this). Do NOT add bombs-as-currency in real-time 3D; the "bomb the wall" action is fine, but a run-ending softlock (Isaac's Super Secret needing a bomb the run never found; Steam daily-run complaint) must be avoided: always make a bomb-free route to anything required. |
| Secret rooms via bombing + minimap deduction | **adapt, big-ish** | The deduction game relies on a grid minimap with visible gaps; our 14x14 grid of 4 m cells already has this shape on the dungeon map if we show a floor map, so a *cell-gap deduction* is feasible: grid cells that touch 3+ known rooms/corridors without a room = secret candidate (use Isaac's own rule: a cell with 3+ neighbours). But our rooms are 3D with real geometry: reveal a *crack in the wall* (cracked-wall decal + rubble sound) instead of "bomb the middle of a wall". The parked "hearth marks" (co-op push/brace) are a better opener than a bomb: it makes secrets a co-op beat with a solo fallback. Keep the Isaac rule that the entrance is never blocked by obstacles. |
| Super Secret / Ultra Secret tiers | **skip** | Three secret tiers is Isaac's long-tail content. One secret tier is plenty for 12-floor dungeons. |
| Curses as floor modifiers | **steal, cheap** | The six curses mostly remove information (map, health, item identity) or reshape the floor (Labyrinth = merged XL floor, Maze = wrong room). Our stretch list has "rare dark floor". Cheap set for Phase 1+: Darkness (lantern radius), Lost (no floor map), Blind (chest contents hidden until opened), Labyrinth-lite (one larger floor, worth two floors toward pieces/Stir). Chance should grow with how many dives/unlocks the world has, as Isaac's does (1/80 early up to 1/5-1/3 late). One curse per floor, announced at the floor intro. NOT "Unknown" (hide HP) in co-op: five HUDs would need a rewrite. Maze (random room swap) breaks networked floors; skip. |
| Alt path with a toll (key / bombs / hearts) | **later, big** | The cleanest mapping: a branch from a band's set-piece floor that costs a different resource and leads to a rarer path (e.g., an optional "sealed stair" floor 4 -> 4b). Isaac only introduced it with Repentance after years. Matches parked "movement tools gating optional branches". Not Phase 1. Warn: Isaac's knife-piece chain (items from 3 floors needed to reach the true final) is the content-heavy part; copy only the toll idea. |
| Devil/Angel deal chance from play style (no-damage floor -> deal) | **steal, cheap-ish** | A bonus room that opens when the party clears a floor *without anyone being downed / with minimal damage*. Gives skill a visible payoff, fits co-op (shared goal) and the "keepsakes" idea. Make the flag the party's, host-computed, not per-player. Avoid the Isaac pitfall of a 1% base with +99% stacking; use a simple rule (no one downed, X% HP lost) and show it on the door. |
| Floor count/chapter structure (8 floors, short) | **already ours** | We have 12 floors in 3 bands; floors are far longer than Isaac's. Short floors are key to Isaac's feel; our floors with 4-6 rooms + corridors are the closest 3D analogue. Don't lengthen. |
| Unlock-driven content pool growth | **steal, later** | Grow the room pool, curse pool, loot pool and elective-room roster with dungeon completions. Tie growth to the *world*, not the player, because our multiplayer world has up to 4 recorded players and any subset can play. Also mitigates Returnal/Ravenswatch "too few rooms" complaints at launch. |
| Challenges (45 fixed-rule runs) | **later** | Matches parked "rule-twist challenge gauntlets". |
| Item synergy as the replay engine | **gap** | Isaac replays because of ~hundreds of stacking items. Our design has weapons that grow by use, not run-built synergy. Our floor generator alone will not drive replay (Returnal/Ravenswatch show floor shuffling only buys about 20 hours). The roster of elective rooms (D2) and weapon growth carry that weight. Not a floor problem. |
| Co-op as screen-sharing babies | **do not copy** | Isaac co-op's weaker baby, half damage, can't pick up, 20% boss-damage penalty are all patches over a single-screen design. We need full-strength players. |

### D2. Proposed elective-room roster for Phase 1.5 (all fit existing room-pool + dead-end machinery)
Each is an authored room (or dressing preset) dropped into a dead end by priority. **cheap** unless stated.
1. Vault/treasure room behind the locked door (exists as the key-chest locked room).
2. Mending spring/healing point room (we have healing points on about half of floors; make it a dead-end elective rather than a floor-wide roll).
3. Challenge hall: extra wave, doors lock, reward = chest. Offered only when the party is mostly healthy (Isaac rule). Rate: Isaac uses 1/2 on floor 2, none on floor 1.
4. Peddler/shop-like room: sells with run coins or town pieces' "banked tokens"? **later** (needs an economy decision).
5. Secret nook: cracked wall; guaranteed 1/floor from band 2. **later** for the full deduction mechanic.
6. Cursed altar: take a floor curse voluntarily for a better reward (Isaac's curse room is a different thing: damage on exit for item). A "pay with Stir" altar fits our system: +Stir for a reward. **cheap-later**; check it with the 40/70/100 thresholds.
7. Sleeping resident nook / keepsake room (parked keepsakes) **later**.

### D3. What breaks under real-time 3D co-op and the dive-and-bank loop
1. **Arena lock + lagging player (open problem, no published solution found).** Recommended rules [I]: (a) lock triggers when the *first* player crosses the threshold; (b) any player outside gets a "teleport-in / walk-in" window; late arrivals pass into the fight (the lock only blocks exits); (c) an unreachable (dead/downed/disconnected) player is excluded from the clear condition; (d) downed players revive on room clear and a short countdown rather than waiting for all to die (clear-to-revive + countdown per the Game Developer piece); (e) clear condition is "no hostile spawned by the room left", checked on the host, not per-client. This is **cheap** in design, but needs a risk review (host authority, subset-of-players handling, mid-fight disconnect).
2. **Bomb/key-as-currency + banked loot.** In Isaac, keys/bombs are consumed immediately and mostly irrelevant after the run. In our game the shared haul is held by the host and only safe once banked, so "spend key to open vault" adds a *second* sink for a shared resource. Keep keys per-floor (they expire at floor exit, like our gate key) so no inventory/bank accounting. **cheap.**
3. **Curse of Darkness/Lost/Blind in 3D co-op:** visual and HUD effects are per-client, deterministic and cheap. Curse of Maze (room swaps) and Labyrinth merges (a merged XL floor) change floor geometry or room routing and need extra host-synced state; skip Maze, do Labyrinth-lite only via the generator's room count/seed (cheap-ish).
4. **Secret discovery by deduction:** Isaac's minimap is the whole puzzle. In 3D we only have it if we ship a readable floor map. Returnal's players call its map comprehensive (one review). Plan a top-down grid map (we already have a 14x14 grid) that shows discovered cells and *unknown cell gaps*; without it, secrets are pixel hunting. If we add the map, "cell with 3+ known neighbours" is exactly Isaac's candidate rule. Co-op: share the explored map across players (sync discovered-cell bitmask).
5. **State-aware generation vs. seed-for-all-clients:** inputs must be part of the host seed bundle; per-player stats (individual HP) can't be used because clients disagree and subsets change between sessions. Use party-level or world-level numbers only.
6. **Scaling to players present:** the loudest 3D co-op complaint. Avoid pure HP scaling (Ravenswatch, Gunfire boss HP). Use enemy count per room (our Stir +1 enemy rule already does that), variant mix, and a cap; do not scale chest prices per player (RoR2 complaint).
7. **Per-player vs shared loot:** RoR2 splits it (pickups individual, interactables shared) and still has hogging complaints. In ours, chests feed the shared haul (host-held), so there's no hogging by design - an advantage. Isaac's Repentance gives one boss pedestal per player; consider "one reward per player" for personal gear rewards like weapon pieces (**later**).
8. **Time/pressure clocks:** our Stir meter is the clock. Reviewers found Nightreign's circle and Ravenswatch's timer rushed and un-fun; keep Stir *a choice* (bank vs push). Per Nightreign: avoid anything that forces skipping content you've already paid to generate.
9. **Pool size at launch:** every 3D shuffle roguelite is criticised for few biomes/rooms. We have 12 combat rooms + 3 sub-themes per band; plan content in the pool-growth order (D1 table) rather than all upfront.

### D4. Priority recommendation (my read)
1. **Do now (cheap):** priority-ordered elective rooms in dead ends (D1 row 1, D2 items 1-3); clear-to-revive + countdown + exit-lock-only arena rule for co-op (D3.1); floor curses Darkness/Lost/Blind driven by host seed (D1 curses); no-one-downed bonus door (D1 deal row); pity counters on drops.
2. **Next (later):** shared floor map with grid gaps + cracked-wall secret rooms, with "hearth mark" co-op opener; state-aware odds; pool growth per world; Stir altar.
3. **Big:** alt-path branch with a toll; challenge gauntlets; per-player rewards.
4. **Avoid:** bomb-gated required progress; per-player-HP-based generation; Maze curse; Unknown curse (HUD) in co-op; baby-style weak co-op; three tiers of secret rooms.
Conflicts with the current design: (a) "rewards shown on doors" (stretch) conflicts with Blind curse and the secret-room surprise, so Blind must hide door rewards for that floor; (b) our 50%-of-floors locked room is already a state-free vault; Isaac's vault needs 2 keys; keep ours; (c) healing points on about half of floors is rolled per floor, but state-aware logic should roll it from the party HP so a fresh party doesn't see it.

## Sources
Primary (developer-authored or direct data): 
- Isaac wiki.gg Level Generation (data-mined): https://bindingofisaacrebirth.wiki.gg/wiki/Level_Generation (primary-ish, community-datamined)
- Isaac wiki.gg Special room, Secret Room, Devil Room, Curse, Room Clear Awards, Co-op: https://bindingofisaacrebirth.wiki.gg/wiki/Special_room , /Secret_Room , /Devil_Room , /Curse , /Room_Clear_Awards , /Co-op (secondary community wiki)
- Game Developer, Postmortem: McMillen and Himsl's The Binding of Isaac (2011): https://gamedeveloper.com/business/postmortem-mcmillen-and-himsl-s-i-the-binding-of-isaac-i- (primary)
- Game Developer, Raw Meat tips from McMillen: https://gamedeveloper.com/design/raw-meat-game-design-tips-from-team-meat-s-edmund-mcmillen (primary; only seen as a search snippet)
- Boris the Brave, Dungeon Generation in Binding of Isaac: https://www.boristhebrave.com/2020/09/12/dungeon-generation-in-binding-of-isaac/ (secondary analysis)
- Push Square, Housemarque interview on Returnal: https://www.pushsquare.com/news/2021/02/interview_returnal_ps5_developer_housemarque_talks_story_replayability_and_value (primary interview)
- Unreal Engine interview, Returnal (Gregory Louden): https://www.unrealengine.com/en-US/developer-interviews/deciphering-the-narrative-driven-procedural-horror-of-returnal (primary; fetch blocked 403, seen as snippet only)
- Kotaku on Hades level design (Ed Gorinstein quotes): https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545 (secondary quoting primary)
- Game Developer, Designing death in co-op roguelikes: https://www.gamedeveloper.com/design/designing-death-in-co-op-roguelikes (primary design essay)
- Bigosaur, Son of a Witch death design: https://bigosaur.com/blog/165-soaw-death (primary dev blog)
- Void Champions devlog on the late/dead player dilemma: https://miguelseabra1999.itch.io/voidchampions/devlog/236037/eggsfrancisco-weekly-post-3 (primary, small)
Secondary (reviews, aggregators, forums):
- Game Informer, Nightreign review: https://gameinformer.com/review/elden-ring-nightreign/encapsulating-efficiency
- CGMagazine, Ravenswatch review: https://www.cgmagonline.com/review/game/ravenswatch-pc-review/
- Vaporlens summaries (AI-generated aggregates, weak): https://vaporlens.app/app/2193540/wizard_of_legend_2.md , /app/1911610/windblown.md , /app/2071280/ravenswatch.md , /app/1217060/gunfire_reborn.md
- Co-optimus Gunfire Reborn review: https://www.co-optimus.com/review/2458/gunfire-reborn-co-op-review.html (snippet only)
- Returnal save complaints: https://www.gamerevolution.com/news/680219-returnal-no-save-divides-community , https://www.videogameschronicle.com/news/housemarque-says-it-hasnt-decided-how-it-will-solve-returnals-lack-of-saves (snippets)
- Steam threads (Isaac secret rooms, bomb scarcity, daily-run softlock): https://steamcommunity.com/app/250900/discussions/0/3044978964799119154 , /613941122618101256
- Steam RoR2 threads on scaling/pricing/hogging: https://steamcommunity.com/app/632360/discussions/0/2141966124582714590 and related (snippets)
- Notebookcheck on Ember Knights: https://www.notebookcheck.net/More-than-just-a-Hades-clone-Co-op-roguelite-cheaper-than-ever-on-Steam.1279550.0.html
- Witchfire reviews (Shacknews, Press Start): https://www.shacknews.com/article/137248/witchfire-roguelite-preview (snippets)

## Open questions / not verified
- No GDC talk on Isaac found; only 2011/2012 Game Developer postmortems. The 2012 piece's content (blasphemous success) was not fetched.
- Isaac minimap reveal rules (adjacent-room silhouettes, special-room icons visible before entering, secret rooms hidden) are from general knowledge; the wiki Minimap page 404'd. [I]
- Isaac "active item charges per room cleared" not verified (only a snippet about big rooms giving 2 charges).
- Hades/Hades II camera and door-reward-preview rationale: no primary source found. Hades II has no co-op: secondhand FAQ report only.
- Windblown, Ravenswatch, Gunfire, WoL2: sentiment mostly from AI-generated Vaporlens aggregates and snippets; no full Steam review sampling. Windblown "no pause" is from a reviewer snippet.
- No published material found on the co-op arena-lock/lagging-player problem; the D3.1 rules are my design proposal.
- Ziggurat, Children of Morta, Soulstone Survivors, Rogue Legacy 2, Gungeon room-clear specifics: not researched here.
- Isaac secret room placement ordering is disputed on the wiki talk page (priority vs farthest-first) [S].
