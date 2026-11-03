# 07 - 3D procgen from authored modules, and non-layout variety systems

Confidence tags: [V] verified from a fetched source this session; [S] seen only in a search snippet; [M] from my memory/inference, not verified. Primary developer talks (GDC vault, ExileCon, Bad North talk video) mostly could NOT be read here; I only got interviews, wikis, and summaries. See section 5.

## 1. Per game

### Kate Compton: "10,000 bowls of oatmeal" [V via Boris the Brave summary + search]
- Claim: a generator can emit endless outputs that differ in every number (each oat has a different position/rotation) yet the viewer perceives one thing: "oatmeal". Mathematical uniqueness (distinct bit patterns) is not perceptual uniqueness (a player can tell A from B at a glance, at play speed). Humans abstract fast: once they have the "description" (colour range, shape mix, size range), extra detail stops registering; too many free elements make each irrelevant.
- Her fix is to REMOVE freedom, not add content ("spice", per Boris the Brave 2026-07 summary):
  1. Few big decisions per run (pick a subset of palette/shape mix, generate everything from it).
  2. Discrete small sets (pick one of 2-4 values) rather than continuous ranges.
  3. Rare feature flags, each under ~10%; chance of at least one fires: 50% summed -> ~40%, 100% -> ~65%, 200% -> ~85%.
  4. Correlate properties (derive most shapes from 3 seeds) so patterns are visible.
  5. Vary spatially (different big decisions per region, blend between) and at different scales.
- Other commentary: Emily Short asks whether generated variation connects to anything mechanical (i.e. changes what the player does). CMU course reading: check output for dealbreakers at scale.
- Fit to us: our 4-6 rooms from a pool of 12, plus ~70% sub-theme bias, is already "few big decisions". The weak point is that variation inside a room (jitter, dressing) is oatmeal; what the player perceives is room identity, mechanics-changing events, and floor-level rolls.

### Warframe tilesets [V wiki.warframe.com/Tileset; criticism NOT found]
- Each tileset has 30+ tiles (rooms of varying size); 25 star-chart tilesets. Tile roles: Start, Connector (small corridor-ish), Intermediate (big combat), Objective, Dead end (single door, loot lockers), Exit. Doors/portals must be compatible.
- Map is a TREE (no loops). One route start -> objective -> exit; branches always end in dead ends. Each mission type x tileset has a developer-set template defining tile types, segment lengths, max branch depth. Duviri is the exception (custom nested layout).
- Remasters: Jupiter Gas City remastered in Update 25 (2019); Orokin/Galleon tilesets from 2013. I found no primary criticism write-up; community folklore (not verified): Warframe's complaint is that the same tileset repeats for many missions and tiles look alike, not the topology.
- Lessons: role-typed tiles (connector vs intermediate vs objective) + a per-mission template is a cheap, effective "grammar". Objective rooms are separate authored content keyed to mission type, so one tileset serves many mission types. [inference]

### Remnant / Remnant 2 [V partially; secondhand]
- CEO David Adams (IGN interview, relayed): biome order randomised at top level; each world has two (three with DLC) storylines, only one is in a given run; quests, events, minibosses, side quests are randomised; two players in same area can see different bosses.
- Community (Steam, not dev): prefab tiles carry entity nodes where items/mobs may spawn; dungeons are more fixed, randomness concentrated at entrances/event areas.
- Takeaway: the big perceived-variety lever is authored STORY/EVENT variants slotted into stable tile sets, not tile count. Co-op relevance: they handle 3 players with the host's world.
- Not found: tile counts, a Gunfire talk.

### Diablo III / IV [S/V mixed]
- D3: tiles are fixed-size/shape rooms or land masses that can hold elements; generator joins them; Rifts reuse tilesets with any monster population on any tileset (patch note); entrance area kept monster-free (secondary wiki).
- D4 beta (2023): heavy criticism that dungeons are "same rooms in random order", players posted screenshots of repeated tiles, unfavourable comparison with D2's distinct layouts (GamePro DE, PCGH forum, SI.com). Game director Joe Shely's reply: you'll level through many different dungeons, not repeat one. Blizzard's post-beta change list had a "Dungeon Layouts" section; gosugamers doubted it fully fixed it. Details/tile counts not found.
- Lesson: complaint was identifiability. Players recognise a room after 2-3 sightings; repeated tile sightings inside ONE dungeon are what trigger it. (inference from sentiment)

### Returnal [V Push Square/Press Start/Red Bull interviews]
- Harry Krueger: hybrid. Every area (treasure, combat, traversal, narrative) is hand-crafted; they are combined/connected freely between runs; contents (enemy mixes, counts, chests) are procedurally spawned. Six biomes, each a series of areas separated by doors; boss unlocks next biome; parasite items with positive+negative traits force risk decisions; unlock databank carries over. Review: finite rooms/enemies so repetition sets in. Room format and counts not found; GDC 2022 talk is about studio transformation, not generation.

### Deep Rock Galactic [V devtrackers.gg Ghost Ship post, community-reposted but dev-written content]
- 119 hand-crafted room templates, 10 biomes. Cave = chain of rooms joined by winding tunnels. Many templates contain internal randomizers (spheres that may hollow or add rock). Generator may warp, mirror, overlap templates by mission type/length/difficulty (Refinery merges rooms into big caverns).
- Template = shape clusters (spheres, half-spheres, planes) defining the carved volume; marked exit points become tunnel connections; a size cap globe.
- Biome applies surface noise + "debris" (flora, hazards, fauna, cobwebs) AND changes enemies, fog, lighting, cave shape.
- ~150,000 new caves/day; >500M caves since 2018 early access. Playtest every room with every class; rooms must survive random outcomes.
- Mutators [V wiki]: Warnings (harder, raise reward) max 2 per mission and Anomalies (change gameplay, neutral/beneficial) max 1; at least one mutator-free mission always on rotation.
- Lesson: they get perceived variety from biome (look + enemies + hazards + light as one bundle) and mutators, not from 119 rooms. Direct match for our per-floor atmosphere/dark-floor plan.

### Path of Exile [V GGG level-design interview thread]
- Random levels = pre-designed pieces assembled at random within limits: "pockets of designed layout and set pieces randomly distributed". Rooms have no fixed entrances; may be rotated/flipped, so designers author each piece for multiple configurations. Restrictions block bad combos (some slip through). Designers track average monsters/chests per area by running thousands of generated levels into a spreadsheet. Labyrinth rooms tagged by type so similar rooms don't stack. Merging tilesets caused lighting and height problems. Exploits: wall-gap jumps skipping key doors; movement skills skipping traps. Maintenance/tuning as much work as creation.
- Layouts: community wiki says waypoint/exits roughly fixed per layout; PoE2 zones show ~8 layouts (fan research) - i.e. a handful of layout seeds plus randomised fill. [S]
- Direct lessons: sample-N-thousand statistics (we already have Soak sweeps: extend to per-floor chest/enemy stats); tag rooms to prevent stacking similar ones.

### Spelunky (2) [V tinysubversions + GMTK transcript snippets]
- Each of 4 room types has 8-16 templates of 10x8 tiles; static tiles + probabilistic tiles (25% pushblock, 33% spikes) + 5x3 obstacle blocks drawn from their own template sets + random horizontal mirroring (mostly unnoticed since most rooms are symmetric). Spelunky 2 rooms/chunks can also be mirrored (fan NES port notes) [S]. Classic dark levels: never first/last level, at most one per area [V wiki]. Sp2 details beyond that not verified.
- Layered templates (obstacle blocks inside rooms) = what our "chunk regions" idea is.

### Townscaper / Bad North WFC (Stalberg) [V Game Developer article]
- Bad North: 300+ hand-built tiles (Maya -> Unity); hard + soft constraints; rules prevent identical/mirrored neighbours; some tiles span 2 cells to break grid look; navigability check, restore-state retry on failure; some levels still fail. Townscaper ~500 tiles would be needed to reskin; irregular quad grid hides the grid; windows cut with stencil to avoid seam cases; WFC struggles with long thin things (walls, pipes). Players built far larger than expected; "recipes" discovered.
- Not applicable directly: WFC wants hundreds of tiles; our scale is 4-6 rooms. But two details transfer: forbid identical neighbour repetition, and multi-cell pieces to hide the grid.

### Others touched
- Lethal Company [V wiki, thin]: exterior unique per moon; interior random (Facility ~50/50 Mineshaft, rare Manor); weather rolled daily affects only exterior. I found no source confirming the generator (it is widely said to use DunGen, [M] unverified). Pattern: interior type is a rare-variant roll with a rare "jackpot" (Manor).
- Hades Heat/Pact of Punishment [S]: opt-in ranked modifiers, higher Heat -> better rewards. Primary design commentary not found.
- Slay the Spire [S]: map choice with risk/reward; "?" events pool includes filler events that recur; StS2 redid events to force harder choices (PCGamesN). No details verified.
- Risk of Rain 2 stage variants, Dead Cells biome choice, Darkest Dungeon 2, Noita, Minecraft Dungeons, Torchlight, Unexplored 2, Caves of Qud, No Man's Sky: NOT researched in depth here; from memory [M]: RoR2 stages have 2-3 variants with different layout/hazard/lighting and a hidden "simulacrum/echo" stage; Dead Cells lets the player choose between 2-3 biome paths; Diablo champions/elites spawn with 1-4 random affixes (Fast, Shielding, Molten...) [M]; Mythic+ weekly affixes [M]. Treat as leads, not facts.

## 2. Cross-cutting patterns
1. Every shipped example spends authoring on a SMALL number of high-salience axes: biome bundle (look+enemy+hazard+light), objective/story variant, mutator, boss/miniboss swap. Tile count is secondary. Warframe 30+, DRG 119, Bad North 300 - but players of DRG/Warframe still read the biome/mission type first.
2. Perceived sameness comes from identifiable repeated rooms within ONE session (D4 beta). Counters: tag/forbid duplicates, per-run subsets, mirrored/rotated variants, internal randomizers (Spelunky probabilistic tiles, DRG randomizer spheres).
3. Tree-like topologies (Warframe, early D3) are common and criticised less than lack of identity; loops are a bonus.
4. Hand-crafted modules + procedural contents (Returnal, PoE) - contents (enemy mix, chests) carry replay variety.
5. Opt-in or rare modifiers with a reward attached (DRG warnings, Hades Heat, Returnal parasites) beat silent difficulty changes.
6. Designer process: run thousands of generations and look at statistics (PoE spreadsheet), playtest each room in all configurations/classes (DRG). Both match our Soak tier.
7. 3D-specific problems repeatedly mentioned: lighting and height when tilesets merge (PoE), navigability failures (Bad North), exploits via jumps/gaps (PoE). Our grid + flat floors + no jump avoids most.

## 3. Honest comparison: our approach vs shipped 3D games
- Smaller: 12 combat rooms + 3 sub-themes per band is about one-tenth of DRG (119) and 1/3 of Warframe per tileset. 4-6 rooms per floor from 12 means a player meets most of the pool within 3 floors. Repetition WILL be noticed by floor 4-6 unless we vary what happens in rooms, not just which rooms.
- Our advantages: 12 floors in 3 bands each with a hand-built set-piece floor (shipped games rarely get this). Co-op subset scaling. Floors are short and bank/stir pacing adds drama.
- Limits of the grid: 4 m cells and 4-bit corridor masks restrict to Manhattan topology; no vertical overlap (everything is one level), so no "see a place you'll visit later" sightlines (what makes DRG/Remnant feel 3D). Room-to-room matching by 4-bit mask means all connectors are identical doors, so the seams never differ. 14x14 floor with 4-6 rooms is sparse; much of the floor is corridor, which is the least identifiable content.
- What we cannot match: Remnant-style authored story branches, DRG's cave volumes, Bad North constraint solving. Do not try.

## 4. For Sleeping Calamity (ranked by perceived uniqueness per authoring cost)
Tags: cheap = Phase 1 sized; later; big.

1. **Per-floor "big decisions" bundle (Compton + DRG biome)** - cheap. Roll 3-4 discrete floor variables at seed: sub-theme (already), light state (normal / dim / rare dark), enemy roster subset (2 of 4 types, fixed per floor, shown as a signboard or flavour), hazard flavour (drips, steam, root creep). Keep rare variants under ~10% and correlate (dark floor -> glow-moths, fewer chests visible). Also act on DRG: bundle visuals + enemies + light so players read the floor as a "kind". Uses already-planned per-floor atmosphere + roster subsets. No conflict. Highest value per cost.
2. **Floor mutators (DRG Warnings/Anomalies, Hades Heat, Returnal parasites)** - cheap to medium. 0-1 per floor early, 2 per floor deeper. Warning = harder + shown reward; anomaly = rule twist (e.g., oven-warm floor: fire damage buff both ways; slick flagstone; echo hall: noise draws the hunter). Show on the stairs/door sign (matches "rewards shown on doors"). Cap and guarantee some clean floors (DRG always has mutator-free missions). Co-op: mutators scale with player count via existing difficulty rules. Conflict: the stir meter already adds pressure; keep mutators orthogonal (change rules, don't add raw enemies).
3. **Room-level variants inside the existing rooms (Spelunky probabilistic tiles + obstacle blocks, DRG randomizer spheres)** - cheap. Add 2-4 variants per room as mirror/rotate (check authored rooms for sidedness, mirrors break text/signs) plus 3-5 socket sets (planned "dressing sockets with fill chance"). This multiplies room identity ~4-8x for small cost. Add "chunk regions" (5x3-style cells inside rooms that pick from 3-4 mini-layouts: pillars, pit, barrel wall, brazier). Do chunk regions after sockets.
4. **Duplicate and similarity control** - cheap. Never show the same room twice in a floor unless pool exhausted; tag rooms (open arena / corridor fight / puzzle-ish / hazard) and avoid two same-tag rooms adjacent (PoE labyrinth tagging, Bad North no identical neighbours). Add a Soak metric: per-run "unique room count seen in floors 1-4" and per-floor tag histogram. Concrete numbers to track: chest/enemy count per floor mean+variance (PoE spreadsheet).
5. **Vignettes / events with pity counters (Remnant events, StS "?" rooms, Isaac)** - cheap each, medium total. A vignette is a small authored prop cluster + one choice: trapped NPC (rescue = a keepsake), wandering merchant, collapsed cart with a risk/reward chest, shrine that swaps a mutator. 8-12 events in the pool give variety more cheaply than 12 more combat rooms. Remnant's lesson: story variants slot into stable tiles. Co-op: choices must be host-resolved with a ping from all players. Avoid StS's filler events (ones that do nothing) - each must change state or give a choice.
6. **Objective/room-role grammar (Warframe tile roles)** - cheap on paper. Label rooms with roles (arena, connector, objective-holder, dead-end loot, set-piece). Already largely this (entry/exit/checkpoint). Add an "objective room" type that makes the floor goal differ: key carrier (current), defend-the-oven (hold position), escort-bearer, sealed vault (lever puzzle). 2-3 objective types per band is a big feel change; the exit key carrier alone repeats every floor. Medium authoring cost per objective; mid-priority but highest for "this floor plays different".
7. **Layout presets (spine/ring/hub/wing)** - cheap code, already planned. Do it, but expect low perceived impact: with identical connectors the player mostly can't see topology. Worth it for loop/dead-end distribution.
8. **Multi-level rooms (verticality)** - big. A room with a balcony/pit that is visible from a neighbouring room is the single biggest "3D" gain, but needs 2-level connector standards, nav and light work, camera issues for third-person and sightline-culling in co-op. Pilot with ONE pit room (drop from floor A -> lower part of floor) before committing. Probably "later".
9. **Connector standards beyond a door (types: wide arch, low crawl, barred gate, secret door)** - later. Gives seams visible variety and gates (movement tools, hearth marks). Needs 2-3 connector sockets per room face and mask expansion beyond 4 bits. Medium cost; good fit with parked "hearth marks".
10. **Elite affixes and mini-bosses (Diablo champions, Hades armoured elites)** - cheap-medium. 1-2 affixes on the key-carrier and a per-floor elite; one authored mini-boss per band besides set-piece. Reuses existing enemy assets with behaviour toggles. Works for solo and co-op; respect that the stir meter's tier-2 hunter already is this role.
11. **Daily/weekly seeds, challenge gauntlets** - later. Same seed from host already; a shared "this week's Hearthdeep seed" is easy but adds nothing until the loop is strong.
12. Avoid: WFC, noise caves, full story branching (Remnant) - authoring cost out of scope.

Where our approach is limited: perceived uniqueness is capped by rooms*variants; no amount of algorithm helps if only 12 rooms exist. A single tag-aware shuffle plus per-floor bundle plus mutators is the best value; invest in a 13th-20th room only after those.

## 5. Sources
Primary-ish:
- https://devtrackers.gg/deep-rock-galactic/p/8cd5ca9c-below-decks-at-ghost-ship-cave-generation-in-deep-rock-galactic (dev post, community repost)
- https://pathofexile.com/forum/view-thread/1654235 (GGG level designers interview)
- https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making (Stalberg)
- https://wiki.warframe.com/w/Tileset (wiki, secondary-primary)
- https://tinysubversions.com/spelunkyGen2/ (Spelunky Classic generator study)
- https://www.pushsquare.com/news/2021/02/interview_returnal_ps5_developer_housemarque_talks_story_replayability_and_value (Krueger)
- https://deeprockgalactic.wiki.gg/wiki/Warning, /Mutator, /Anomaly (wiki)
Secondary:
- https://www.boristhebrave.com/2026/07/05/oatmeal-spice/ (Compton summary)
- https://gamedeveloper.com/design/practical-procedural-generation-for-everyone- (Compton GDC summary, not read)
- https://wccftech.com/remnant-2-promises-to-take-procedural-generation-to-the-next-level/amp/ ; https://gamebanshee.com/9d648 ; https://shacknews.com/article/136051/gunfire-games-remnant-2-design-interview (Adams, not fully read)
- https://videogames.si.com/news/diablo-4-repetitive-dungeon-design ; https://www.gamepressure.com/newsroom/diablo-4s-repetitive-dungeons-were-designed-like-this/ze52eb ; https://www.nme.com/news/diablo-4-director-pushes-back-on-claims-of-repetitive-dungeons-3417417 ; https://www.gamepro.de/artikel/diablo-4-meinung-dungeons,3391852.html
- https://diablo.fandom.com/wiki/Nephalem_Rift ; https://www.rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards ; https://www.pcgamesn.com/slay-the-spire-2/events-redesign
- https://lethalcompany.wiki.gg/wiki/Moons

## 6. Open questions
- Compton's original essay and GDC talk text not read; only summaries.
- Warframe tile criticism/remaster rationale: no source found. Search the Warframe devstreams / Update 25 notes.
- Remnant tile counts and generator details; Returnal room counts/format; Path of Exile ExileCon talk "Procedural World Generation in Path of Exile" (Rhys Abraham); Bad North Everything Procedural 2018 talk video; D4 tile counts; Lethal Company generator (DunGen?). All unread.
- Baked vs realtime lighting, light probes, occlusion, navmesh for generated 3D levels: not found in sources; my inference: bake nothing per-room at runtime; use authored per-room baked lightmaps/probes inside prefab rooms (probe volumes carry with the prefab), realtime light only for per-floor atmosphere rolls, runtime NavMesh surface bake after assembly (or per-room navmesh stitched with links at doors). Verify with a Unity prototype.
- Seam hiding in 3D: sources only show Bad North (multi-cell tiles, irregular grid). Our fixed 4 m door standard plus door-frame collar props is the cheap hide [inference].
- Spelunky 2 special/dark level rules; Risk of Rain 2, Dead Cells, Darkest Dungeon 2, Noita, Minecraft Dungeons, Torchlight, Unexplored 2, Caves of Qud: unresearched.
