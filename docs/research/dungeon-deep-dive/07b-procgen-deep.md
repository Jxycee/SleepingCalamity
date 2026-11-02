# 07b - Procgen deep dive (wave 2): oatmeal sources, more games, Unity tech for generated 3D floors, measuring sameness

Tags: [V] read from a fetched page this session; [S] search snippet only; [M] memory/inference, not verified.
Effort and limits, stated plainly: about 30 WebSearch calls were answered before the shared 200-call search budget ran out (the last 6 planned searches, on sameness metrics, telemetry, Unexplored 2, Monster Train and Returnal room counts, returned nothing). About 40 WebFetch reads succeeded. I did not work around the block. Items below marked "NOT FOUND" are real gaps, not claims of absence. Wave-1 file `07-3d-procgen-variety.md` and repo `docs/research/procedural-dungeons.md` sections 2-3 were read first; nothing here repeats their per-game internals.

## 1. Kate Compton's oatmeal argument, from primary-ish copies

### 1.1 What she actually said [V: galaxykate0.tumblr.com "So you want to build a generator" (her blog, 2016), GDC summary on Game Developer, Emily Short's blog]
- The problem: 10,000 bowls of oatmeal, each mathematically unique (every oat in a different place), still read as "a lot of oatmeal". She also uses 18,446,744,073,709,551,616 possible planets as the same point. Counting outputs is the wrong metric.
- Her two bars:
  - **Perceptual differentiation** (easy): this item is not identical to the last one. A glance at a row of trees says whether they are suspiciously uniform. Enough for background content.
  - **Perceptual uniqueness** (hard): an individual item stands out as characterful, like a named character rather than a crowd extra. This is what she says the real metric is, and it is hard to reach.
- Practical rules from the essay: write down what makes an output good and bad; mark deal-breakers as hard constraints; use generate-and-test to reject violators (fails if passing content is rare or constraints are many, then use a solver or a more conservative generator, which shrinks the space); aim for a deliberately chosen expressive range, not one perfect output repeated. She credits Gillian Smith with naming the expressive-range problem.
- GDC summary [V]: a smaller set of really distinct items beats a million near-identical ones; repeating trees is often fine; test "would you write fanfic about this generated object?"; five steps (understand the design space, enumerate constraints, understand the process, pick a method, iterate); structure random output with category hierarchies (e.g. medium objects clustering beside large ones); Spore planets were improved by generating many seeds, keeping the good ones and discarding the rest ("you don't need to do it right, just do what works").
- "Spice" (Boris the Brave, 2026-07-05, summarising her) [V]: fewer bigger decisions per run; discrete 2-4 value choices; rare feature flags each under 10% (flags summing to 50% give about 40% chance at least one fires, 100% gives 65%, 200% gives 85%); correlate properties (the example derives most shapes from 3 random shapes); vary spatially with different big decisions per region and blend between them (Voronoi or recursive subdivision), and vary scale of decisions.
- Emily Short's addition [V: emshort.blog 2016-09-21]: variation matters only if it connects to play. Decorative variation (names, haiku, food) gets looked past quickly. She does not demand new verbs for every variant; small persistent state changes the player notices beat big branching. Her implied tests: does it change with something the player did, would the player notice, does it add something a status readout would not. Also: procgen does not replace authoring, it is a way of designing content and is often as much labour.

### 1.2 The book "Procedural Generation in Game Design" (Short and Adams, eds., CRC Press 2017) [V table of contents from routledge.com; chapter contents NOT read, publisher page only]
Chapters: 1 When and Why to Use Procedural Generation (Darren Grey); 2 Managing Output: Boredom versus Chaos (Kenny Backus); 3 Aesthetics in Procedural Generation (Liam Welton); 4 Designing for Modularity (Jason Grinblat); 5 Ethical Procedural Generation (Michael Cook); 6 Level Design I: Case Study (Chris Chung); 7 Level Design II: Handcrafted Integration (Jim Shepard); 8 Level Design III: Architecture and Destruction (Evan Hahn); 9 Cyclic Generation (Joris Dormans); 10 Worlds (Mark R. Johnson); 11 Puzzles (Danny Day); 12 Procedural Logic (Ben Kane); 13 Artificial Intelligence (Johnson); 14 Procedural Enemy Waves (Wyatt Cheng); 15-17 generative art/toys/audio (Schmidt, Compton, Zgeb); 18-21 narrative (Kybartas, Grinblat, Tuffs, Short); 22 Understanding the Generated (Gillian Smith); 23 Content Tools Case Study (Kepa Auwae); 24 Automated Game Tuning (Aaron Isaksen); 25 Generating Rules (Cook); 26 Algorithms and Approaches (Brian Bucklew); 27 Meaning (Johnson).
- The chapter titled "Maintaining Designer Control" and a Tanya Short "ages of procedural content" chapter named in the task are NOT in this table of contents. Tanya Short and Tarn Adams are the editors; Short's own contribution I could not locate. Do not cite either as a book chapter.
- Chapters most relevant to us by title (inference from titles only, unread): 2 (boredom vs chaos is exactly the oatmeal tension), 7 (Shepard, handcrafted integration = our authored rooms), 9 (Dormans, cyclic generation; the Unexplored technique already in the repo), 14 (Cheng, enemy waves; Diablo's director), 22 (Smith, understanding the generated = expressive range), 24 (Isaksen, automated tuning = Soak-style stats). Worth buying or finding in a library for the controller; not obtainable here.

## 2. More games (only what I could verify)

### Minecraft Dungeons [S/V partial]
- Official dev blog "Random vs fixed pieces" (title confirmed by search, page itself 503'd on two fetches): levels are built room by room, not block by block; many rooms are random, but some rooms are fixed and never change, and those act as landmarks. Designer Christian Berg: level designers specify how many rooms a level has and can request specific rooms or room types (e.g. trapped ones). Designers authored rooms in vanilla Minecraft then tested them in the third-person Dungeons view [S via Windows Central].
- Community format notes [V, unofficial: Dokucraft/Dungeons-Level-Format]: a level is a JSON file listing tiles (cuboids of blocks) from named object groups; tiles carry connection data; the file also sets monster spawns, objectives and resource packs. Tile counts NOT found.
- Takeaway: "fixed landmark pieces among random pieces, with the designer asking for N rooms and for named types" is exactly our entry/exit/checkpoint + pool model and a per-floor "request" list.

### Torchlight 1/2/Frontiers [V Runic Q&A 2011-05-20; S for Frontiers]
- TorchED 2 [V]: reusable "level chunks" (e.g. catacombs) shared by several dungeons; "layout links" place props and spawners into chunks; each link has theme-named folders, and a dungeon's ruleset picks a theme so only matching folders become active. Result: two dungeons share geometry but differ in props and spawners. Feature tags: designers mark many candidate spots, then the ruleset says how many fill (example: 100 candidate chest spots, 3-5 chests picked). "Chests and breakables are no longer randomly spawned": they sit where they make sense, with designer-controlled counts. Torchlight 1 had 7 tilesets [S]; TL2 moved to themed tilesets placed in regions so they feel grounded; almost every landmark has a miniboss [V].
- Frontiers [S]: pitched as procedurally generated zones to fight MMO-style dungeon burnout; no technical write-up found.
- Takeaway: the "theme folder" idea is the cheapest content multiplier I found: the same room geometry with 2-3 prop/spawn sets selected per floor sub-theme. Our "dressing sockets with variants" is the same concept; make the selection key the floor theme and bind enemy spawn tables to it.

### Hades (rooms) [V Kotaku, DigiPen on Ed Gorinstein]
- Gorinstein explored modular room layouts first; it clashed with the painterly art style, and options inside one room were "pretty limited". Quote gist: it was more effort to make a room modular than to make more rooms and get variety from combat and enemies. Rooms are recombined every run; smaller chambers early in a biome, larger ones later; biome rules shape enemies (Asphodel islands give enemies movement tools); exploration extras were cut for pacing and replaced by small discovery points (urns, wells, fishing spots, gates). No counts found.
- Takeaway: strong support for our "more hand-made rooms plus encounter variety" over heavy in-room modularity. Counter-evidence for the chunk-region idea: modularity costs more than new rooms at Supergiant's polish bar. Our authoring stack is procedural Blender/Unity builders, so the cost calculus is different; test with one room before committing.
- Hades II [S]: two paths (route choice) instead of one; nothing on room generation.

### Risk of Rain 2 [V devtrackers patch note, wiki.gg Stages; S forum]
- Official note: "Stage Variants is a new system that allows stages to have multiple layouts" (introduced in the Hidden Realms update, tested first on Titanic Plains and Distant Roost, the stages seen most). Wiki: loop-only variants are mostly cosmetic but each has its own name, spawn pool and log entry, some open new areas (Viscous Falls = night Verdant Falls with a new cave opening; Disturbed Impact opens some houses; Iron Auroras opens the drill interior; Golden Dieback changes haze/leaves/mushrooms). Each stage slot has 2-4 stage options (stage 1 lists 5, stage 2 lists 5, stage 3 lists 9 across DLC), so the player sees one of several bundles per slot.
- Environment logs (the Void Fields and Secret Stage logs were added in the same update) [V]: a collectible log entry per environment, with each variant getting its own entry. That is a reward for seeing variants.
- Takeaway: two ideas. (1) A "variant" is a named bundle (layout tweak + light + spawn pool) with its own collectible entry, shown only on a later pass (loops); we can unlock variants on repeat dives of a band. (2) Variants are cheap when the base layout is reused and only 1-2 openings/doors change.

### Darkest Dungeon 2 [V wiki.gg Roads; S reviews]
- Not a room generator: a region is nodes joined by roads; five road types (combat, hazardous = -1 armor, rough = -1 wheel, oblivion tear = +1 loathing, safe); nodes are shops, services and encounters; each region gets a modifier (32 in the pool) and a goal (13), both shown at the inn before choosing; regions per expedition depend on the Confession; Watchtower reveals the rest of the region (scouting/hidden info). No generator internals found.
- Takeaway: "show the floor's modifier and goal on the door before entering" is validated as a player-facing decision. Matches our "rewards shown on doors".

### Slay the Spire [V wiki.gg, fan-maintained]
- Each act: 17 floors, up to 6 nodes a floor, 1-3 links up/down; fixed floors (floor 1 easy fights, floor 9 treasure, floor 15 rest, 16 boss); base weights normal 53%, elite 8%, rest 12%, shop 5%, unknown 22%; unknown rooms are resolved on entry with pity-style weights (a type's weight drops to its base when it appears, others rise) and reset per act; 3 bosses possible per act; a seed fixes the act maps and bosses. 
- Takeaway: the "?" room with rising weights is the same pity counter pattern as wave 1 recommends; 22% unknown share is a usable reference for our vignette frequency (about 1 in 4-5 rooms).

### Diablo IV [V Gamepressure/NME, Gematsu snippet, S for rest]
- Blizzard (Joe Shely): the similarity of dungeons is intentional, each ties to its place in the world; 100+ dungeons (later quarter update: 150+ combining handcrafted and procedural); different objectives per dungeon (clear all, objective items). Art update: tile-sets reused across dungeons mixed with props, interactives and lighting, and a feature to blend two tile-sets inside one dungeon [S]. A German outlet claims 75 tile sets with two categories and 25 with six categories (unverified, third party).
- Post-beta changes [S, April 2023]: backtracking reduced in multiple dungeons; dungeon event spawn chance raised from 10% to 60%; objective-item carrying reworked. Blizzard said these followed feedback "and data", but no telemetry figure was published.
- Takeaways: (a) players complained about backtracking and tedious carry objectives more than about tile reuse. Our key carrier + gate key is a carry objective, so watch it. (b) Events at 10% were too rare to register; 60% was Blizzard's fix. For us a 1-in-10 vignette on a 4-floor band means the player might never see one: use floor pity rather than flat low odds.

### Remnant 2 [V Shacknews, S rest]
- Principal level designer Cindy To cites procedural generation as a replayability source; no tile or room counts in any source found. Earlier note about biome order and event randomisation (wave 1) stands. NOT FOUND: tile counts, any Gunfire GDC talk.

### Warframe tileset remasters [V Devstream notes via search; S]
- Gas City remaster (Update 25 era; Devstream 114 and 123 notes): goal was to bring an old tileset to the same material/lighting standard as newer ones and improve performance; Earth remaster added vegetation and volumetric lighting. No page describes the Galleon or Corpus ship remasters; no criticism write-up. NOT FOUND.
- Takeaway: old tile sets are revisited for materials and lighting, not topology. Cheap to plan for us: keep room geometry and light rigs separable so a later lighting pass does not touch layouts.

### Lethal Company [S]
- Unity HDRP (Acerola breakdown summary: PBR, volumetrics, low internal resolution to reach 100+ FPS on mid-range hardware). Interiors generated at runtime on entering, causing freezes on weak CPUs (third-party performance guide). Mods describe 130+ room tiles for custom interiors. DunGen use by the base game still unverified.
- Takeaway: a shipped Unity co-op game generated interiors at load time and accepted a hitch; cover it with a loading beat. We already generate from a host seed before the floor starts.

### Noita [V Noita wiki, Game Developer on Wang tiles]
- Biomes are generated from Wang tile templates (each pixel colour maps to a material) and then pixel scenes (hand-made scenes) are stamped on top; guaranteed chests exist as pixel scenes; some biomes get seed-based biome modifiers. Wang-tile background [V, Game Developer 64 Pixels Later]: two edge types without rotation need 16 tiles, three types 64, four types 256; per-tile PRNG seeded from coordinates plus the global seed so result does not depend on exploration order; fixed-seed regions let designers hand-edit by storing only diffs.
- Takeaway for structure only: authored "scenes" stamped over a generated base is the vignette model. The coordinate-seeded per-tile RNG trick is directly useful for networking: derive every room's RNG from (floorSeed, cellX, cellY) so clients and late joiners reproduce a room without replaying the whole generation order.

### Caves of Qud [V Game Developer, Qud wiki]
- Dungeons, wilderness and part of the history and lore are generated; towns are mostly static, with a hand-written lore backbone around the generated parts (pairing static areas with a procedural world, in the Omega/ADOM tradition). 5 sultans' histories generated each game; history is told as biased, conflicting accounts (wiki gloss). Grinblat's GDC 2018 talk is members-only; the mechanism for turning history into playable content NOT read.
- Takeaway: history as content is out of Phase 1 scope. The usable part: a generated "who lived here" fact per floor that names the keepsake/vignette cast (e.g. which resident's mug sits in the oven maze) gives free narrative coherence if the same fact drives dressing, a note, and one vignette. Tag: later.

### Spelunky 2 [V PlayStation Blog quote via search, wiki]
- Derek Yu: core generation is the same as Spelunky 1, a safe path to the exit built from room templates. Additions: a back layer reachable through hidden doors, fluid physics; post-launch tuning of spawn rates in the first two levels (1-1, 1-2). Yu also said the algorithm does not make the most natural caves and players learn to recognise repeating patterns [S].
- Takeaway: even Spelunky, with endless runs, relied on rule changes (liquid, layers) more than on more rooms. Spawn-rate tuning of early floors was a post-launch fix: expose per-floor spawn weights as data.

### Not obtained
Unexplored 2 devlogs, Monster Train, Returnal room counts, Dungeon of the Endless internals (only an Endless Dungeon blog on station room design: props sized to fixed slots, "what's behind the door" as the design goal), Hades II generator, Diablo IV tile counts (official), Remnant 2 counts. All hit the search budget; leave them as open.

## 3. Unity tech for generated 3D floors (project is Unity 6000.6.5f1, URP 17.6)

### 3.1 Lighting
Verified facts:
- **Enlighten**: the Enlighten *baked* GI backend is gone (upgrade replaces it with the Progressive Lightmapper). The 6.0 upgrade guide (6000.6 docs copy, updated 2026-10-30) says Enlighten *precomputed Realtime GI* "is still available and is supported up until Unity 6" [V]. Practical reading: do not build on it, and the GPU Resident Drawer docs require Realtime GI with Enlighten to be disabled anyway [V]. Auto-generate lighting is removed: bake explicitly via Lightmapping.Bake / BakeAsync [V].
- **No official runtime-bake path for generated levels**. Every source I found describes editor-time baking. Options people actually use [V forum threads]:
  1. *Bake lightmaps into room prefabs and reassign at runtime* (open-source PrefabLightmapping by Ayfel, stores lightmap index/scale-offset on the prefab). Caveats: remove the static flag on objects if you instantiate at runtime (static batching rewrites UVs); lightmaps in AssetBundles misrender on Android in some reports; seams at room doors because each room is baked alone. One poster's seam fix: blend lightmap pixels across matching doorway planes by distance. A Unity forum comment (bgolus): the choices are pre-baked/extracted lightmaps or realtime lighting.
  2. *Light probes*: "You can't create new probes at runtime" (Unity forum, Pema-Malling). Workarounds: assign baked probe data per room then retetrahedralize (`LightmapSettings.lightProbes.bakedProbes` then `LightProbes.Tetrahedralize()`; order matters in one reporter's experience; the sync call is CPU-heavy, a TetrahedralizeAsync exists [V Unity API docs via search]), or `LightProbeUsage.CustomProvided` with `MaterialPropertyBlock.CopySHCoefficientArraysFrom`, "might take some engineering".
  3. *Adaptive Probe Volumes (APV)*: Baking Sets must bake all scenes that load together; one baking set loaded at a time, a scene belongs to one set; additively loaded scenes in different combinations are a known pain, with reports of APV not loading on additive/script loads in 6000.0.22 and of a unique baking set needed per scene combination [V]. APV data can be streamed (disk and GPU streaming) or loaded from AssetBundles/Addressables, but not both at once [V docs]. Merging probes from different APVs at runtime is reported as impossible [S]. Conclusion: APV fits fixed scenes and open-world cells, not "shuffle 5 rooms from 12 into a random grid" (12-choose-5 orderings at 4 rotations is far too many combinations to bake sets for).
  4. *Realtime lights*: reports of heavy cost with 50+ rooms of realtime lights [S, old forum]. URP Forward+ removes the per-object light limit (Per Object Limit setting ignored); the per-camera visible-light cap still applies and includes the main light [V docs]. Shadow-casting lights remain the real cost (shadow atlas); I found no authoritative limit figure.
- Shipped-game evidence: none found for how a Unity shipped game lights generated interiors. Lethal Company is HDRP with volumetrics and low-resolution output, so it leans on realtime lights and fog [S]. I found no postmortem. Treat as a gap.

Recommendation for our 14x14 grid (inference, to be proven by a prototype):
- Do not bake per floor. Bake each *room prefab* once (lightmap + its own probe group, if you keep the static flag off at spawn and use PrefabLightmapping-style assignment) or, simpler and probably enough for a stylised look, **use no lightmaps at all**: realtime, mostly unshadowed point lights per room, one or two shadowed lights, strong ambient/probe-free fill from a per-floor ambient colour, and baked-looking AO painted into textures/vertex colours (our asset pipeline already bakes PBR sets). Dynamic characters then need only a per-room ambient tint, which sidesteps probe retetrahedralization.
- The per-floor atmosphere roll (dark floor, fog tint, dim lights) is trivially compatible with realtime lights and incompatible with baked lightmaps (baked lights cannot change at runtime). If we want the "rare dark floor", realtime is the choice.
- Test that Forward+ with 6-10 active shadowless point lights per room at 4-6 rooms visible is inside budget; shadowed lights should be the exception (one per room, distance-faded).

### 3.2 NavMesh
- DunGen's Unity NavMesh adapter [V docs] offers four modes: pre-baked per tile (fastest, needs NavMeshLink at doorways), add-if-missing, always re-bake per tile, and one full-dungeon bake (seamless, "can be much slower on large layouts"). Doors toggle a NavMeshLink enabled state via the door's IsOpen. Unity manual advice: use smaller NavMesh tile size if baking at runtime to cap memory [S]. No benchmark found.
- Recommendation: per-room NavMeshSurface baked in the editor on each room prefab, joined at doors with NavMeshLinks (placed from the 4-bit mask positions). Our rooms are 4 m cells on a regular grid with identical door positions, so link endpoints are known without calculation. Rotations need the surface baked in a rotation-independent way (NavMesh data is positioned with the prefab transform; rotate the surface with the prefab and verify). Fallback if links misbehave: one runtime bake over the assembled floor on the host only. Clients in a host-authoritative game do not need AI nav at all if enemies are host-simulated, so the bake cost only matters on the host (and solo).
- Gap: I could not verify prefab-baked NavMesh data surviving rotation/instantiation in 6000.6 AI Navigation; that is a 1-hour prototype.

### 3.3 Occlusion / culling
- Built-in baked occlusion culling is "not suitable" when scene geometry is generated at runtime [V Unity manual 6000.x]; the compute function is editor-only [S forum]. Dynamic objects can be occludees, not occluders [V].
- **GPU Resident Drawer + GPU occlusion culling** [V 6000.6 docs, S forum]: needs Forward+ or Deferred+ renderer, compute shader support, SRP Batcher on; Enlighten Realtime GI must be off; Probe Atlas Blending used by default with Forward+. Supports runtime-created and changed objects ("if you create or change GameObjects each frame, the drawer updates"). A forum reply states GPU occlusion culling is fully dynamic, needs no static flag and no bake. Renderer restrictions: no MaterialPropertyBlock, no Use Proxy Volume or Anchor Override, no OnWillRenderObject/OnBecameVisible/OnBecameInvisible scripts, not under an Animator, materials must support BatchRendererGroup, at most 128 materials in the check; BatchRendererGroup shader variants must be kept (longer builds). Unsupported: animated LOD cross-fade, Light.shadowMatrixOverride.
  - Implication: our room prefabs and props can use it if they are plain MeshRenderers with SRP-Batcher-compatible URP Lit materials and no MPB. Characters (Animator) are exempt/normal. This is the lowest-effort culling option and fits generated content.
- Cheaper backup, because our floor is a grid with doors: **room-graph culling**. Each cell knows its neighbours via the mask; enable renderers/lights only for the current room, rooms adjacent through *open* doors, and a depth of 1-2 beyond (player count matters in co-op: union of all players' visible sets). Forum users report this "portal-ish" approach gives a few draw calls at a time. Also `StaticBatchingUtility.Combine` on the floor root after generation was reported as the main win in a forum thread [S]; it conflicts with lightmap assignment, GPU Resident Drawer and per-room toggling, so choose one: do not use static batching with the GRD path.

### 3.4 Batching and instancing
- With GRD, runtime-placed prefabs batch via BatchRendererGroup without Combine; the SRP Batcher is the baseline. Keep per-room material count low (shared atlases per sub-theme) to stay under the 128-material check and keep draws down.

### 3.5 Load-time and co-op
- Generate on the host from the seed, instantiate rooms over a few frames behind a loading beat (Lethal Company freezes are evidence that doing it in one frame hurts). Instantiate by cell order so all clients agree; use coordinate-seeded RNG per room (Noita Wang-tile trick above) so a late joiner can rebuild any single room.

## 4. How studios measure "feels samey"
What I could verify (thin, because the targeted searches were cut off):
- Compton: perceptual differentiation vs uniqueness; the "write fanfic" smell test; constraints plus generate-and-test; brute-force sampling and human curation of seeds (Spore) [V].
- Short: does the variation connect to play and change with player action [V].
- Gillian Smith's expressive range analysis (credited by Compton) [V name only]: plot generated levels on metrics (the standard pair in the original paper is linearity and leniency [M]) and read the histogram for coverage and clustering. The paper itself I could not fetch (404/unavailable); the repo's earlier research already recommends fingerprints. NOT verified from source here.
- PoE level designers: run thousands of generated levels, put average monsters and chests per area into a spreadsheet [V wave 1]. DRG: playtest every room with every class [V wave 1].
- Blizzard (Diablo IV): changed dungeon event odds 10% to 60% and reduced backtracking "based on feedback and data" [S]; the data was not published. This suggests two cheap measurable signals: how often a vignette fires per dive, and backtracking distance.
- NOT FOUND: any studio-published recognition test or telemetry definition of "samey" (the searches I planned for this were blocked by the search budget).

Concrete sameness checks for us (inference, ordered by cost):
1. Soak-tier: per seed, extract a floor fingerprint (set of room ids in order from entry, light state, roster subset, vignette tags, objective type, entry-exit distance) and report distinct fingerprints and the top-1 share; flag any fingerprint above a threshold (wave 1 suggested roster-set share under 30%).
2. Pairwise "route string" edit distance for floors in one dive; require a minimum distance between floors 1-4 of the same dive (a cheap duplicate guard).
3. Dry-run counters: longest run of floors with no vignette, no mutator, no dark floor across 1,000 simulated 12-floor dives.
4. Human recognition test (5 minutes, no telemetry needed): show 6 testers 8 screenshots from 8 generated floors (taken from fixed cameras the repo already has) and ask "which two were the same floor?" and "which floor was this one?" after a delay. If testers cannot tell floors apart, differentiation fails; if they can match floors by a landmark, uniqueness is working. Compton's fanfic test as a fallback: can a tester name one memorable thing about each floor?
5. In-game telemetry later (optional, solo-only logging): per floor, time-to-first-fight, backtrack distance, whether the party opened the optional locked room, vignette interaction rate.

## 5. For Sleeping Calamity: merged ranking (wave 1 + wave 2)
Constraints restated honestly. Our grid is 14x14 cells of 4 m, 4-6 rooms per floor from a pool of 12, 4-bit door masks, single level height, rotations in 4 ways, identical doors. That means: topology is nearly invisible to the player; a room is identified by its interior; the corridors between rooms are the least recognisable content; the player meets most of the 12-room pool inside 3 floors; the only strong repetition fix inside the grid is to change what happens, not where walls are. None of the ideas below require leaving the grid, except where marked.

Tags: cheap (Phase 1 sized), later, big.

1. **Per-floor bundle of big decisions (Compton + DRG biome + RoR2 variants + Torchlight themes)** - cheap, highest value. Roll at floor seed: sub-theme, light state (normal / dim / rare dark, at most 1 in 12 and never floor 1), enemy roster subset, prop/spawn theme folder (same room geometry, different dressing set and spawner table), hazard flavour. Correlate them so the bundle reads as one named thing (dark floor = glow-moths + fewer lanterns + less chest sparkle). Realtime lighting (section 3.1) keeps this free. Show the bundle name on the door (DD2 and Warframe-style pre-entry info; rewards-on-doors already planned). Wave 1 rank 1; wave 2 evidence: RoR2 variants (named bundle plus its own log entry), Torchlight theme folders, DD2 region modifier shown before choosing.
2. **Objective variants beyond key-carrier (Warframe roles, Minecraft Dungeons "request room types", Diablo IV objective types)** - medium. Wave 1 rank 6, raised: Diablo IV players complained about tedious carry objectives and backtracking, so the repeated key-carrier hurts most. 2-3 objective types per band (key carrier, defend the oven/hearth, sealed vault with lever or brazier lighting). Co-op: host resolves, scaled by player count.
3. **Floor mutators with visible reward (DRG, Hades Heat, DD2 modifiers)** - cheap to medium. Wave 1 rank 2. Keep orthogonal to the stir meter; guarantee some clean floors; shown on the door.
4. **Vignettes and events with floor pity, not flat low odds (StS unknown rooms, Diablo IV event odds 10 to 60%, Spelunky/Brogue pity)** - cheap each. Evidence: StS gives about 22% of nodes as unknowns with adjusting weights; Blizzard raised event chance to 60% because 10% was invisible. Use a floor-level rising weight, tag fluff/reward/risk/danger, once-per-kind per floor. Add the coordinate-seeded RNG so clients reproduce them.
5. **Room-level variants: sockets with fill chance, mirror/rotate, prop theme folders** - cheap. Wave 1 rank 3. Hades evidence argues against *heavy* chunk modularity (more effort than new rooms at their art bar); keep chunk regions after sockets and pilot with one room. The cheapest multiplier of all is the theme-folder switch (same geometry, 2-3 prop/spawn sets), rank above chunk regions.
6. **Duplicate and similarity guards plus Soak fingerprints (PoE tags/stat sheet; section 4)** - cheap. Wave 1 rank 4, plus the pairwise route-string guard and the 5-minute human recognition test.
7. **Roster subset + spawn weights as data (PMD cap, Spelunky 2 early-floor spawn tuning)** - cheap, partly in rank 1; expose per-floor spawn weights so early floors can be tuned after playtests without code.
8. **Named variants unlocked on repeat dives (RoR2 loop variants + environment logs)** - cheap-medium, new. After a band is cleared once, later dives roll an alternative layout tweak (one extra door open, a flooded wing) with its own collectible log entry. Fits the town-rebuilding collection loop; also gives a reason to dive again. Solo/co-op neutral because it is host seed.
9. **Layout presets (spine/ring/hub/wing) + distance bound** - cheap code, low perceived impact (wave 1 rank 7). Keep, but do it after 1-8.
10. **Connector variety (arch, barred gate, low crawl, secret)** - later, and expensive because it breaks the identical-door 4-bit assumption. Wave 1 rank 9.
11. **Elite affixes / per-band miniboss (Torchlight "landmark minibosses", Diablo champions)** - cheap-medium. Wave 1 rank 10.
12. **Multi-level rooms** - big, unchanged (wave 1 rank 8). Not supported by the grid; pilot one pit room.
13. **History-driven floor facts (Qud)** - later. One generated "who lived here" fact per floor that drives dressing, a note and a vignette.
14. **Avoid:** WFC, noise caves, heavy in-room modularity before one pilot, any approach that needs baked lighting per generated combination (APV baking sets, per-floor lightmaps).

Unity-specific decisions to make early (each is a prototype, not a doc exercise), in priority order:
- (a) Lighting: realtime lights and no lightmaps for rooms (recommended above); confirm Forward+ cost with real room dressing. If a baked look is wanted later, use per-room prefab lightmaps, not APV.
- (b) Culling: enable GPU Resident Drawer + GPU occlusion culling only after checking our materials and props meet its list (SRP-Batcher-compatible URP Lit, no MPB, no OnBecameVisible); otherwise room-graph culling by door mask.
- (c) NavMesh: per-room baked surfaces with NavMeshLinks at doors vs one runtime bake on the host; host-only requirement keeps cost off clients.
- (d) Generation hitch: spread instantiation over frames behind a loading beat.

## 6. Sources
Primary or near-primary:
- https://galaxykate0.tumblr.com/post/139774965871/so-you-want-to-build-a-generator (Compton, read)
- https://gamedeveloper.com/design/practical-procedural-generation-for-everyone- (Compton GDC summary, read)
- https://emshort.blog/2016/09/21/bowls-of-oatmeal-and-text-generation/ (Short, read)
- https://www.routledge.com/Procedural-Generation-in-Game-Design/Short-Adams/p/book/9781498799195 (TOC, read)
- https://www.runicgames.com/blog/2011/05/20/level-design-q-a/ (Torchlight II, read)
- https://devtrackers.gg/risk-of-rain/p/d3ec5f04-early-access-hidden-realms-content-update-december-17-2019 (RoR2 patch note, read)
- https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer (read)
- https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity6.md (read)
- https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/lighting/probevolumes/streaming (read)
- https://dungen-docs.aegongames.com/2.18/pathfinding/unity-navmesh/ (read)
- https://docs.unity3d.com/6000.6/Documentation/Manual/OcclusionCulling.html (snippet via search)
- https://www.gamedeveloper.com/design/seeded-exploration-of-wang-tiled-environments (read)
- https://www.gamedeveloper.com/design/tapping-into-the-potential-of-procedural-generation-in-caves-of-qud (read)
Secondary:
- https://www.boristhebrave.com/2026/07/05/oatmeal-spice/
- https://kotaku.com/hades-level-design-is-less-random-than-it-seems-1845254545 ; https://digipen.edu/showcase/news/eduardo-gorinstein-delivers-challenge-and-fun-hades
- https://riskofrain2.wiki.gg/wiki/Stages ; https://darkestdungeon.wiki.gg/wiki/Roads ; https://slaythespire.wiki.gg/wiki/Map_Generation ; https://noita.wiki.gg/wiki/World_generation ; https://wiki.cavesofqud.com/wiki/Caves_of_Qud/Videos
- https://www.gamepressure.com/newsroom/diablo-4s-repetitive-dungeons-were-designed-like-this/ze52eb ; https://gamingbolt.com/diablo-4-will-improve-dungeon-layouts-classes-ui-and-more-based-on-beta-feedback ; https://80.lv/articles/blizzard-presented-changes-to-diablo-4-made-based-on-feedback (search snippets)
- https://shacknews.com/article/136051/gunfire-games-remnant-2-design-interview
- https://80.lv/articles/how-to-achieve-lethal-company-s-graphics-with-unity-hdrp
- https://github.com/Dokucraft/Dungeons-Level-Format (unofficial) ; https://www.minecraft.net/pt-pt/article/the-road-dungeons (title via search, page 503)
- https://discussions.unity.com/t/baked-lighting-on-prefabs-dynamically-generated-level/799924 ; /t/probe-volumes-for-additive-levels/918696 ; /t/gpu-occlusion-culling-and-scene-loading-questions/1541672 ; /t/computing-occlusion-culling-at-runtime-what-do-i-miss/933744 ; https://github.com/unitycoder/PrefabLightmapping

## 7. Open questions
- Short and Adams chapter text; "Maintaining Designer Control" and "ages of procedural content" not located as chapters (possibly talks or other publications).
- Smith and Whitehead expressive-range paper not read; the metrics named (linearity, leniency) are from memory.
- Any studio telemetry or recognition-test definition of "samey": not found (search budget exhausted).
- Unexplored 2, Monster Train, Returnal room counts, Dungeon of the Endless internals, Hades II generator, Diablo IV tile counts, Remnant 2 counts, Grinblat's Qud history talk: not obtained.
- Unity: no shipped-game postmortem on lighting generated 3D floors; per-prefab NavMesh data under rotation in 6000.6; Forward+ visible-light cap default figure; whether GPU occlusion culling holds up with our actual materials. All need one prototype each.
