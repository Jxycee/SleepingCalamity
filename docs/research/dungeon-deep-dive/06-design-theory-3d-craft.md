# 06 - Dungeon/level design theory and the craft of memorable hand-made 3D dungeons

Researcher note: this pass used one web-search round per topic (about 22 searches, 1 failed fetch: thealexandrian.net blocks WebFetch behind a bot check). Search-result summaries are secondary. Where I rely on my own background knowledge it is tagged [inference/background] and should be verified before it drives a decision. Confidence tags: [V] = seen in a fetched/search source this session; [B] = background knowledge, not re-verified; [I] = my inference.

---

## 1. Theory

### 1.1 Jaquaying / "xandering" the dungeon (Justin Alexander, The Alexandrian)
- Named after Jennell Jaquays (Caverns of Thracia, 1979; three entrances to level 1). [V: Wikipedia, Alexandrian search summaries]
- Core claims [V via secondary summaries]: (a) multiple entrances give a strategic choice before play begins and hidden entrances reward exploration; (b) real loops differ from branching: a branch is still functionally linear, and a hallway bending back on itself within a few metres is not a loop; (c) each extra connection between levels (stairs, shafts, chutes) creates new loops; (d) secret doors are best when they lead to other parts of the dungeon, not only to treasure; (e) players may miss content, which is acceptable; (f) "discontinuous levels" = you can reach a lower level from several non-adjacent upper spots, so the map is a graph, not a stack.
- Secondary source says Alexander later shifted the term to "xandering" (Nov 2023). [V weakly: Italian blog; not confirmed on his site]
- Primary URL: https://thealexandrian.net/wordpress/13085/roleplaying-games/jaquaying-the-dungeon (could not be fetched; part 5 at .../13132/...). 
- Key lesson for us: loops are not just shortcuts; they give choice of route and make retreat possible. [I]

### 1.2 Five Room Dungeon (Johnn Four, Roleplaying Tips)
- Structure [V]: 1 entrance + guardian; 2 puzzle or roleplay challenge; 3 trick/setback (some sources call it a red herring; the "red herring" label came from a low-quality page, treat as unverified); 4 climax/big conflict; 5 reward, revelation or plot twist (can be an event in room 4, not a place).
- Value: it is a pacing template (varied beats: gate, thinking, setback, peak, payoff). It is a tabletop linear structure; it is NOT a layout rule. Use it as a beat order for set-piece floors and for the sequence of "rooms you experience" along the critical path, not for graph topology. [I]

### 1.3 Kishotenketsu in Nintendo level design (Hayashida; Mark Brown's four steps)
- Hayashida (Mario Galaxy 2 / 3D Land / 3D World director) said in a Gamasutra interview that kishotenketsu (four-act structure from poems and four-panel comics) inspired his approach; the "introduction, development, twist, conclusion" breakdown of levels was popularised by Mark Brown's GMTK video, not stated in that form by Hayashida. He also stressed a single core concept per level. [V: search summaries; Gamasutra/gamedeveloper "the secret to mario level design", GMTK/MCV write-up]
- Practical meaning: a level (or a room) introduces one mechanic in a safe context, develops it with variants and escalation, twists it (new combination, changed constraint, danger), then closes with a short resolve (often a payoff). No fail-state teaching text; the layout teaches. [B/V]
- Applied to rooms: a "Kishoten room" pair or trio on the critical path can teach, escalate and twist a hazard (e.g. steam vents) across 3 rooms in sequence. For procedural floors, the generator can emit "teach-use-twist" triplets of one hazard on the main path (see section 5).

### 1.4 Mark Brown (GMTK) material
- Boss Keys series: game-by-game analyses of non-linear level design: Zelda dungeons, Dark Souls, Metroid, Hollow Knight, Silksong (Team Cherry episode). [V: existence only; no transcript retrieved]
- "The World Design of Hollow Knight" (2020, GMTK) is summarised on MetaFilter as an underground ant-nest of regions; critique that the end game is more scattered than Super Metroid. [V: https://metafilter.com/188536/The-World-Design-of-Hollow-Knight]
- GMTK "Why Nathan Drake doesn't need a compass" argues Naughty Dog guides by environment cues (see 1.6). [V: amara transcript exists]
- Not verified in detail: exact claims about how Hollow Knight teaches the map (Cornifer sells map; bench/pins; regions have distinct colour and music). [B]

### 1.5 Dan Taylor, "Ten Principles for Good Level Design" (GDC 2013)
Paraphrase from 80.lv summary [V secondary]: controlled chaos; mechanics drive the level; start from the emotion you want; tell story through space; give clear objectives without dictating method; introduce something new regularly; surprise via risk-taking and changes in aesthetics/pacing; risk/reward so the player sets their own difficulty; non-linearity, bidirectional routes, modularity, efficiency; empowerment and escapism. Taylor himself later called them guidelines, not laws.
- URL: https://80.lv/articles/gdc-ten-principles-for-good-level-design/ (secondary), GDC Vault/YouTube (primary, not watched).
- Relevant: "risk/reward so the player picks difficulty" matches Stir meter and optional chests; "bidirectional routes" matches loops.

### 1.6 Emilia Schatz / Naughty Dog: affordance and environmental language
- Secondary: 80.lv "Defining environment language for video games" attributes a taxonomy to Schatz: explicit cues (UI icons), defined cues (consistent visual language e.g. climbable ledges painted yellow-ish), cultural cues (a ladder looks like a ladder), inferred cues (learned rules). Explicit/defined cues break immersion and are the fallback. [V secondary: https://80.lv/articles/defining-environment-language-for-video-games/] Schatz quoted in the GMTK video: "very much a game in psychology". [V secondary]
- Not found: a GDC talk by Schatz on affordances; the material is from her CG Master Academy course and interviews. Do not cite as a GDC talk.
- For us: pick a small, consistent "affordance language" and keep it everywhere (see 5.4), e.g. bright warm-edged stones = climbable/pushable; ember glow = interactable.

### 1.7 Valve (Half-Life 2, Left 4 Dead) and Disney weenies
- Weenie = Disney's term (dog-training treat) for a visible landmark at the end of a view that pulls guests forward; Scott Rogers (GDC 2009 "Level Up"/Disney talk) brought it into game design, along with the castle-as-hub weenie, and noted Adventureland was built without one to keep a sense of exploration. [V: disneyblog.com weenie article, boingboing/Rogers slides, above-the-garage.com "Level layout, Disneyland style"]
- Valve: Valve Developer Community pages on L4D level design and visibility exist; I could not retrieve their text. Well-known practice [B]: L4D uses sightline-lit "safe room" light at the end of each map segment, a bright light and a path of lit rooms, plus flow distance metrics fed to the AI Director. A Patreon commentary argues Valve lighting choices all serve gameplay guidance [V weak]. Flow distance, as used for AI Director pacing, is relevant to our Stir meter and generator, but I did not verify exact numbers.
- God of War 2018's Lake of Nine is cited as a hub weenie [V weak: ingamenews]. 
- For us: one weenie per room (a lit bell, a big oven glow, a tall barrel stack) at the end of each sightline, and one per floor visible from the entry (the exit or the "centre").

### 1.8 Kevin Lynch, The Image of the City
- Five elements: paths, edges, districts, nodes, landmarks (landmarks are point references not entered; nodes are enterable strategic spots; edges are linear non-path boundaries). [V]
- Game test: a 2025 Wellesley study (Boakye, Tsien, Awosanya, Mawhorter, "The Image of the Open World Game") found only landmarks and edges came out strongly; NPCs were a new theme; paths less useful because no street names. [V: https://cs.wellesley.edu/~pmwh/mvmap/papers/open_world_elements/open_world_elements.html] This is open-world, not dungeons, so treat as weak.
- Mapping to dungeon: landmarks = tall set pieces and lit weenies; edges = cliffs, water channels, railings; districts = room sub-themes / chunk regions; nodes = junction rooms (3+ doors), checkpoint and loop crossroads; paths = corridors. [I]

### 1.9 Pacing graphs, breadcrumbing, light
- Intensity pacing: alternate high-intensity (combat), low (traversal, search, rest), with a rising baseline; Left 4 Dead's director is the extreme case [B]. Our Stir meter provides an inter-floor rising baseline; the within-floor shape is currently random.
- Breadcrumbing: pickups (coins, small items) placed along intended paths, lights placed along routes. Light draws the eye; bright = goal/safety, dark = danger or secrets. Widely accepted [B]; no single source fetched.

---

## 2. Hand-made 3D dungeons: craft and reception

### 2.1 Dark Souls 1 (interconnection) and its collapse
- Praised: the world is a folded graph; shortcuts (elevators, doors opened from the far side), no fast travel for much of the game, bonfires as checkpoints so shortcuts make sense. [V: thegamer.com "Dark Souls 1: FromSoftware's magnum opus of interconnected level design"]
- Criticised: second half. Lost Izalith was named by Miyazaki as his least favourite zone; Game Informer interview, also said the impact of Anor Londo's first reveal did not last through the experience (that is a lesser regret). Reviewers call the second half rushed or unfinished and the Bed of Chaos a gimmick boss; the "unfinished" claim is the reviewers', not Miyazaki's. [V: https://www.gameinformer.com/b/features/archive/2015/09/11/creating-worlds-hidetaka-miyazaki-s-favorite-zones.aspx]
- Lessons: (a) an interconnected hub-with-shortcuts structure is what people remember; (b) a late-game level that drops verticality, landmarks and shortcuts for flat arena sprawl is remembered as bad; (c) a show-stopping reveal (Anor Londo) gives "the room you remember" but must be backed by sustained quality.
- Sen's Fortress vs Lost Izalith: Sen's is a compact vertical gauntlet of readable traps with a loop-back; Izalith is lava sprawl with repeated enemies. [B: widely repeated; not individually sourced]

### 2.2 Elden Ring: legacy dungeons vs catacombs/caves
Evidence (player sentiment, treat as sentiment not data):
- Legacy dungeons (Stormveil, Raya Lucaria, Volcano Manor, Leyndell etc.) get the praise; the open world is described by some as setup for them; some say fast travel inside legacy dungeons undercuts tension. [V: Game Informer review https://gameinformer.com/review/elden-ring/absolutely-astonishing-adventure; Punished Backlog essay https://punishedbacklog.com/elden-ring-is-great-it-wouldve-been-even-better-without-the-open-world/; Steam threads]
- Catacombs/caves/tunnels: criticised as visually repetitive, differing mostly by end reward and boss; compared to Bloodborne chalice dungeons; same bosses reused (e.g. three Fallingstar Beasts, multiple repeats of other bosses); defenders say reuse is a cost of scale. [V: Steam community discussions, Backloggd reviews, search summary]
- Takeaways: repeated modular dungeons are tolerated when short, optional, and reward-driven; they are criticised when the only thing that changes is the boss and the loot. The boss reuse is more complained about than layout reuse. [I based on V]

### 2.3 Skyrim draugr crypts (Nordic ruins)
- Common critiques: most of ~186 dungeons feel alike, built from a modular kit with an archetypal layout; "impossible to get lost"; conspicuous loop-backs. [V: https://www.thegamer.com/skyrim-dungeons-need-variety/; gamesas / Steam forum threads]
- The end-of-dungeon shortcut that returns you to the entrance (a lever/door) is well known [B]; I could not find a design statement. Not verified as deliberate Bethesda doctrine.
- What it did right: the return shortcut is a good anti-fatigue device that respects dive-and-return, and it pairs with the "boss room, chest, shortcut" end beat. What players disliked is the sameness of the final beat and the lack of any unique structure.
- For us: a banked-return shortcut at the end of a floor is a feature we already have in effect (exit); make the final beat vary.

### 2.4 Resident Evil (mansion, RE2/3/Village maps)
- The Remake's map: unexplored rooms uncolored, rooms with items remaining orange, cleared green. RE2/3/Village: red = something still to do, blue = done. [V: S.T.A.R.S. manual evilresource.com; guides]
- RE2 remake's map is credited with reducing the "much-maligned" backtracking but not eliminating it. [V]
- Lesson: per-room "cleared/remaining" state on the map is a cheap, loved wayfinding and anti-frustration device. For us: map shows rooms with remaining chests/pieces/keys. Caveat: in Tormented Souls forum, the map is static and does not show leftover items/enemies, which players disliked. [V weak]

### 2.5 Metroid Prime, Hollow Knight/Silksong
- Not individually sourced this pass. Background [B]: Metroid Prime uses a full 3D wireframe map with doors coloured by lock type, scanning reveals lore and weaknesses, backtracking with new abilities. Hollow Knight: a map that must be bought and filled by resting at benches, so wayfinding is itself gameplay; regions have unique palettes, music and enemy sets, which is why they are memorable. GMTK's "World Design of Hollow Knight" backs the regional-identity point [V weak].
- For us: colour-coded door locks for key types; "region identity by sub-theme".

### 2.6 Kingdom Hearts world design complaints
- Deep Jungle: most criticised; unclear where to go next, vertical swing/climb navigation, camera; Destructoid ranks it worst, WatchMojo calls its level design the worst Square Enix has done. Atlantica: swimming controls and new control scheme. RPGFan lists Wonderland, Monstro and Atlantica as easy to get lost in. The camera is a series-wide complaint. [V: https://destructoid.com/?p=361603, watchmojo, rpgfan, thegamer]
- Hollow Bastion mixed; a minority complain.
- Lessons for a third-person action game: (a) do not change the control scheme in an area (no swim levels); (b) vertical mazes without a visible goal are the worst case; (c) camera in tight 3D spaces must be solved (with 4 co-op players, even more so); (d) give every confusing node a weenie.

### 2.7 Okami, Tomb Raider, God of War (2018), Ratchet
- God of War 2018 Muspelheim: six trials / five combat arenas climbing a volcano, hardest content in the game, optional; Niflheim: optional collect/puzzle realm. Mixed player opinion, no critic consensus on repetitiveness found. [V weak]
- Tomb Raider tombs: self-contained puzzle spaces with a single big mechanism, optional; reception strong in the 2013 reboot's "challenge tombs" [B].
- Okami and Ratchet: not covered. Mention only that small set-piece "challenge rooms" in a hub are cheap, loved content [B].

### 2.8 Persona 5 palaces (brief, other agent covers)
- Each palace has a distinct theme, a "treasure" goal, a safe room per section, and a one-time puzzle per area; shortcuts after solving puzzles. Strong theming helps memorability; palaces are mostly linear with a few loops. [B]

---

## 3. Wayfinding and orientation in modular/procedural 3D

- Warframe: missions use "tilesets" of predefined room tiles with compatible portals; forum comment: making every procedural room feel purposeful is hard and 'as long as the rooms seem to have a purpose they work'. [V: https://wiki.warframe.com/w/Tileset; https://forums.warframe.com/topic/366681-but-what-is-this-place-for]
- Returnal: reshuffled rooms per biome; fixed warp/hub as stable reference. [V weak]
- Deep Rock Galactic terrain scanner [B]: a scan pulse that reveals nearby ore/tunnels is an active orientation tool; also the 'molly'/drop pod as a landmark and the 'lighting flares' as player-made breadcrumbs. Not verified this pass.
- Practical stack for modular spaces [B/I]: (1) consistent colour code per sub-theme/theme band; (2) one distinct landmark prop per room type; (3) lit vs dark floor/ceiling; (4) sightlines from doors to a weenie; (5) map showing explored rooms and doors by lock; (6) a pulse/compass arrow to the exit or to key-carrier (optional, costs a resource). Full map vs minimap: full wireframe map with fog of war; minimap shows only nearby rooms. Co-op: shared map revealed by any player, team pings.
- Fog of war: reveal by walking; for dive-and-bank loops, show "unexplored door" markers to nudge completion.

---

## 4. Secrets and discovery

- Dark Souls illusory walls: Demon's Souls walls wobble as a hint; Dark Souls 1 dropped that; DS2 changed so hitting no longer opens them; DS3 has about 26 per a Vice/Broadly piece (count unverified). Players complain when no visual tell exists and the only hint is a player message; players troll with fake messages; Elden Ring sparked the "fake wall hit 50 times" panic. [V: https://broadly.vice.com/en/article/qjbm8p/be-wary-of-liar..., pushsquare, Steam threads] Lesson: secrets with zero tell are unfair; fake hints are a shared-fiction gag, not a design.
- Isaac: secret rooms are found by deduction from empty map pockets; guides disagree on adjacency details (some say they border the main path; one says they never border boss/other secret rooms; the "U-shaped empty pocket" heuristic is widely repeated). [V: boristhebrave.com dungeon-generation article; wiki; guides] Lesson: the map itself is the clue, rewarding thinking. Our grid has the same potential: a 14x14 grid with unused empty cells next to many rooms is a deduction space.
- Zelda bombable cracks: cracked wall texture + hollow sound = fair hint [B]. 
- Principle: three tell levels. Obvious (cracked wall visible), subtle (ring of light, a draught that moves candle flames, a mismatched stone), meta (map pocket). Always at least one tell; rewards scaled to tell subtlety.
- Co-op fit: "hearth marks" idea (2+ players push/brace) is a social lock; keep a solo route as parked.

---

## 5. Systemic and emergent variety

- BotW: the "chemistry engine" is a rule-based state calculator (physics = movement, chemistry = state); GDC 2017 "Change and Constant" by Fujibayashi, Dohta, Takizawa; "multiplicative gameplay"; element count disputed in sources (three rules vs five elements). Design goal: puzzle solutions that rhyme with the real world so players can solve by intuition. [V: gamesbeat, zeldauniverse, nintendowire]
- Immersive sims/Divinity surfaces [B]: surfaces (oil, water, fire, electricity) and their reactions give variety without hand-authored rooms. Hades traps [B]: spike/hazard rooms layered over the same arena.
- For us: a small element set (e.g. ember, damp/steam, oil/flour, cold) that interacts with combat gives the same 12 rooms different outcomes: flour dust + ember = flash fire, wet floor + lightning, oil spill slows. Feasible as server-side tile state; co-op friendly because effects are position-based and host authoritative. [I]
- Environmental hazards that reward positioning (not just damage) work better in co-op.

---

## 6. Environmental storytelling, set-pieces, "the room you remember"

- Storytelling by leftover traces: a half-eaten meal and a hastily barred door; a baker's apron on a skeleton by the oven; arrows of soot toward the exit. Dan Taylor's principle "tell stories through the space" [V secondary]; Schatz's environment language [V secondary].
- Memorable rooms share: a single strong silhouette, a unique colour/lighting key, scale contrast (a very large or very small room), an unmistakable set mechanic, and a payoff. Anor Londo is the canonical reveal but also the warning that a reveal alone does not carry a whole area. [V+I]
- Set-piece moments: scripted reveal (door opens onto a vast chamber), a collapse/transformation (floor drops, new route opens), a chase (hunter), a vista from a high point with the exit visible far below. In co-op, ensure every player gets camera control during reveals (no cutscenes locking cameras for long).

---

## 7. Cross-cutting patterns

1. The most-praised dungeons are graphs (loops, shortcuts, vertical links), not chains. [V: Alexander, Dark Souls reception]
2. Dungeons are remembered for a few rooms and for regions; sameness of the closing beat (Skyrim) or of the boss (Elden Ring) is what kills variety, more than layout reuse. [V+I]
3. Per-room state on the map (RE) and consistent affordance language (Naughty Dog) reduce frustration without UI clutter.
4. Navigation failures (KH Deep Jungle) come from vertical/curvy spaces with no visible goal and a bad camera.
5. Fair secrets need a tell; the map can be the tell.
6. Teach-escalate-twist-resolve (kishotenketsu) works at the room level; a short beat sequence works at the floor level (Five Room Dungeon).
7. Modular procedural spaces stay tolerable when each module has a clear purpose and identity (Warframe forum comment).

---

## 8. For Sleeping Calamity

Tags: cheap = Phase 1 size; later; big. Checks: real-time 3D third-person, co-op 2-4, authored rooms in a 14x14 grid, dive-and-bank loop, storybook tone.

### (a) Principles for hand-authored pool rooms
1. **One idea per room (core concept).** Each pool room states one sentence ("the flour-dust oven room", "the leaning shelves"). Name it in the room's metadata so the generator can avoid doubles. **cheap**
2. **Kishotenketsu inside the room:** introduce (door view shows the hazard safely), develop (combat uses it), twist (a second phase or a trigger changes the room), resolve (a calm corner with loot). Even a cheap version: entry sightline shows the hazard; first wave ignores it; second wave exploits it. **cheap**
3. **Every room has a weenie and a silhouette.** A tall or glowing prop visible from every door (a lit oven mouth, a giant ladle, a chandelier of onions). Rooms identifiable from a thumbnail. **cheap**
4. **Two routes inside a room at least:** a loop around a central obstacle so 4 players and kiting enemies do not deadlock. A flank route, a high ledge. **cheap**
5. **Co-op readability:** minimum 2 m clear lanes; no blind corners where a camera would clip; cover and sightlines scaled for 4 players; no 'only one can stand here' key spots unless it's a deliberate set piece. **cheap**
6. **Doors at distinct sides and heights,** so corridors can match by 4-bit mask without blocking; mark the "fighting space" away from the doors so spawn-in is not instantly overwhelming. **cheap**
7. **Affordance language fixed project-wide:** one consistent look for climbable, breakable, pushable, interactable, hazard (see 1.6). Maintain a palette list. **cheap**
8. **Optional secret in 1 of 4 rooms:** a cracked wall or hearth mark with at least one tell. Reward must be visible (a glint through the crack). **cheap**
9. **Environmental story in every room:** 3-5 props placed to tell a micro-story (dressing sockets, with fill chance) — consistent with the planned stretch idea. **cheap**
10. **Each room has a 'remembered thing'**: one stand-out feature (scale, light, sound). Avoid grey rooms that are only a rectangle of enemies. **cheap**
11. **Systemic hooks:** flammable, wet, oily surfaces tagged in the room so combat reacts. **later**
12. **Avoid** vertical swing/climb mazes and any control scheme change (KH lesson). **cheap (avoid)**

### (b) Principles for set-piece floors (fixed hand-authored, floor 4 = oven maze etc.)
1. **A whole-floor Five-Room-style beat list**: entrance guardian, puzzle/mechanic teach, setback (hunter or fire), climax (elite at the resident), reward/revelation (the rescued sleeper wakes and a shortcut back opens). **cheap** (matches the oven-maze design)
2. **Jaquays graph, not a maze chain:** at least two entrances into the climax area and one hidden or conditional route; one shortcut from the climax back to the start (Dark Souls elevator, Skyrim return lever) so banking after the boss is a short walk. **cheap**
3. **Show the goal early** (weenie): the sleeper's glow or a huge oven visible from the first room, then take the player round an indirect route. Creates tension and memorability. **cheap**
4. **Teach-use-twist across the floor:** introduce the floor's mechanic in a safe room, use it with enemies, twist with a changed rule (all ovens ignite). **cheap**
5. **One reveal moment** (Anor Londo style) but do not spend the rest of the floor; sustained quality in the second half is the lesson of Lost Izalith. **cheap**
6. **Co-op:** spacing so all players share the reveal; no solo-locking puzzles; any lever needing two people has a solo fallback (hearth marks parked rule). **cheap**
7. **Map & lock colours:** doors coloured by lock type; the set-piece floor fully mappable. **cheap**
8. **Difficulty scaling** applied to the elite only, not the traversal. **cheap**
9. **Fixed ending variation per band:** avoid "every dungeon ends the same" (Skyrim): each band's set piece closes differently (rescue, escape, collapse). **later** (3 bands)

### (c) Stitching a generated floor so it reads as a designed place
Current generator: 14x14 grid, 4-6 rooms, each linked to a random earlier room, 1-3 loops, 1-3 dead ends. Ideas, each with cost:

1. **Anchor the floor with a "centre" (memorable node).** Pick one pool room as the hub/centre (largest, tallest, with the weenie), placed near the grid centre, with 3-4 doors; the rest radiate. The hub is visible (via windows, balcony, glow) from at least two other rooms. Layout presets spine/ring/hub/wing (already parked) map straight to this. **cheap-to-later**
2. **Guarantee at least one meaningful loop that passes through the hub** and one that is a shortcut "lock" (door opened from the far side, one-way drop or lever): Dark Souls shortcut feel. Count remains 1-3, but classify them: loop type A (open), type B (one-way unlock). **cheap**
3. **Multiple entrance/exit options:** the exit may be reached from two directions; the key carrier's route varies. Jaquays: more than one way to the gate. **cheap**
4. **Vertical connection stand-ins:** because the grid is flat, fake vertical links with stairs/overlooks: a room with a balcony from which you see the room below/next (glass-less windows, grates). Visual loops are almost as good as real ones. **later**
5. **Landmark sightlines:** between rooms, use corridor segments with open arches or windows aligned so one weenie is visible from the previous room's door (a "sightline pair" tag). Place weenies at room ends facing doors. **cheap** (mask-matching already knows door positions)
6. **District identity:** 70% from the floor's sub-theme is good; add a 'floor palette key' (one dominant light colour per sub-theme) and a unique floor landmark visible in the hub. Fits per-floor atmosphere stretch goal. **cheap**
7. **Pacing along the path:** generator assigns a pacing tag to each room along the critical path: rhythm like high / low / medium / hub / high; low rooms are quiet (chest, healing, story). 1-2 'rest' rooms per floor. **cheap**
8. **Teach-use-twist hazard triplet** on the critical path: the generator, if the floor has a hazard, places hazard rooms in order teach (alone), use (with enemies), twist (with another modifier). **later**
9. **Secrets from the grid:** choose secret candidates among empty cells adjacent to 2+ rooms; add a cracked wall with a tell; the empty pocket is visible on the map, Isaac-style. Keep reward visible. Provide a solo route; in co-op optionally a push-together hearth mark. **cheap**
10. **Map as design tool:** wireframe map of room footprints with fog of war, doors coloured by lock, RE-style room state (items remain / done), chests and pieces counted in the "pieces left" counter. Multi-player: shared reveal. **cheap**
11. **Dead ends are valuable** when they hold a reward, a story vignette, or a secret; never an empty room. **cheap**
12. **The 'last room' variety:** the exit room has several variants (Skyrim sameness lesson) and a shortcut back to the entry when appropriate (bank point). **cheap**
13. **Fairness for the Stir meter:** the hunter (tier-2) should have telegraphed sounds/lights; with loops, the party can circle. Loops matter because chasers need a way to be evaded. **cheap**
14. **Camera safety:** each pool room reports minimum clearance for the camera; corridors of 4 m cell width are fine but avoid ceiling < 3 m; cameras clip is a KH-lesson. **cheap**

Conflicts with current design:
- Random "each room linked to a random earlier one" gives a tree plus 1-3 loops: acceptable, but a hub preset changes the room placement order. Presets already parked, so promoting them is a design change; recommend promoting `hub` and `ring` to Phase 1 because they give a 'centre'. Cost moderate.
- 70% sub-theme + 14x14 grid with 4-6 rooms means small floors; a central landmark must be cheap (one room), so use the hub room approach instead of landscape landmarks.
- Fixed set-piece floors vs the pool: fine.
- "Hearth marks" co-op secrets: keep optional per brief.
- Systemic element interaction (section 5) conflicts with Phase 1 scope; **later**.

---

## 9. Sources
Primary or near-primary:
- Justin Alexander, Jaquaying the Dungeon: https://thealexandrian.net/wordpress/13085/roleplaying-games/jaquaying-the-dungeon and part 5 https://thealexandrian.net/wordpress/13132/roleplaying-games/jaquaying-the-dungeon-part-5-jaquaying-for-fun-and-profit (primary; not fetched, bot wall)
- Game Informer, Miyazaki's favourite zones: https://www.gameinformer.com/b/features/archive/2015/09/11/creating-worlds-hidetaka-miyazaki-s-favorite-zones.aspx (primary-ish interview)
- Dan Taylor GDC 2013 talk on GDC Vault/YouTube (primary, not watched); summary https://80.lv/articles/gdc-ten-principles-for-good-level-design/ (secondary)
- Nintendo GDC 2017 BotW "Change and Constant" (primary; coverage https://gamesbeat.com/the-legend-of-zelda-breath-of-the-wild-makes-chemistry-just-as-important-as-physics/, https://nintendowire.com/news/2017/03/01/breath-wild-presentation-gdc-showcases-development-ideology-weird-early-concepts/)
- Hayashida/kishotenketsu: https://www.gamedeveloper.com/design/the-secret-to-i-mario-i-level-design ; GMTK write-up https://www.mcvuk.com/development/video-nintendos-level-design-secrets-in-four-steps ; https://www.nintendolife.com/news/2012/04/super_mario_3d_land_director_shares_level_design_inspirations (secondary, from primary interview)
- Warframe wiki tilesets: https://wiki.warframe.com/w/Tileset (primary wiki)
- Lynch applied to games: https://cs.wellesley.edu/~pmwh/mvmap/papers/open_world_elements/open_world_elements.html (research paper)
- Rogers/Disney weenies: https://boingboing.net/2009/03/27/gamedesign-lessons-f.html ; https://craphound.com/disneylandleveldesign.txt ; https://disneyblog.com/blog/the-weenie-walt-disneys-most-important-design-idea/ 
- Isaac generation: https://boristhebrave.com/2020/09/12/dungeon-generation-in-binding-of-isaac ; https://bindingofisaacrebirth.wiki.gg/wiki/Secret_Room
- RE manual: https://www.evilresource.com/resident-evil-remake/files/stars-manual

Secondary / sentiment:
- Elden Ring: https://gameinformer.com/review/elden-ring/absolutely-astonishing-adventure ; https://punishedbacklog.com/elden-ring-is-great-it-wouldve-been-even-better-without-the-open-world/ ; Steam threads (e.g. https://steamcommunity.com/app/1245620/discussions/0/3183486955461681051)
- Skyrim: https://www.thegamer.com/skyrim-dungeons-need-variety/ ; https://kotaku.com/an-incredibly-in-depth-look-at-the-making-of-skyrim-481342214 ; gamesas threads
- Dark Souls: https://www.thegamer.com/dark-souls-1-fromsoftwares-magnum-opus-of-interconnected-level-design/ ; Vice/Broadly illusory walls https://broadly.vice.com/en/article/qjbm8p/be-wary-of-liar-the-weird-history-behind-elden-rings-illusory-walls
- Kingdom Hearts: https://destructoid.com/?p=361603 ; https://www.watchmojo.com/articles/worst-kingdom-hearts-worlds ; https://classic.rpgfan.com/features/Retro_Encounter_Final_Thoughts/Kingdom-Hearts.html
- Schatz/affordances: https://80.lv/articles/defining-environment-language-for-video-games/
- Five Room Dungeon: https://bestiarumgames.com/blogs/news/better-ttrpg-dungeon-design-with-five-room-dungeon ; https://rpgbot.net/johnn-fourrss-5-room-dungeons-remastered-... (podcast notes)
- God of War trials: Wikipedia and Steam threads (weak)
- Hollow Knight world design: https://metafilter.com/188536/The-World-Design-of-Hollow-Knight

## 10. Open questions / not verified
- Exact content of the GMTK Boss Keys episodes (Hollow Knight, Silksong, Metroid Prime): not retrieved.
- Valve L4D design theory pages and flow-distance numbers: not retrieved.
- Skyrim's end-of-crypt shortcut as a deliberate design: no developer source.
- Metroid Prime, Okami, Ratchet, Tomb Raider, Deep Rock Galactic scanner, Returnal minimap details: only background knowledge, not researched.
- Dark Souls illusory wall counts (Vice article text was fragmentary).
- Isaac secret room adjacency rules: sources conflict.
- Whether Alexander renamed his term to "xandering": only an Italian blog.
- Recommended follow-ups: fetch GDC Vault for Dan Taylor, Alexander's essays via archive.org, and the Valve L4D wiki pages directly.
