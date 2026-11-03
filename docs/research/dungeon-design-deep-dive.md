# Dungeon design deep dive: what the Hearthdeep should be (2026-11-02)

Research only. Nothing here changes `docs/PHASE1.md` or the code until the user decides. The evidence notes behind every claim are in `docs/research/dungeon-deep-dive/` (index at the end). Confidence tags carry over from the notes: **[V]** read in a source, **[S]** secondary or snippet, **[I]** inference.

User's brief: study the dungeons we are building against Dark Cloud 1 and 2, Zelda, Binding of Isaac "if it were 3D" and anything else useful. We are not one game: take parts and make our own thing, and fix what people disliked in Dark Cloud.

## 0. Method and limits

- Two waves of research agents (7 + 6) covered: Zelda and Zelda-likes; Isaac and 3D room roguelites; Dark Cloud player sentiment and the JRPG crawler family (Persona, Mystery Dungeon, Azure Dreams, Etrian, Stardew); hub-and-dive loops and extraction tension; co-op dungeons and scaling; level-design theory and hand-made 3D craft; 3D modular procgen and Unity tech; encounters, arenas, bosses and music; dungeon UX, rescues, revisits and difficulty options.
- Already in the repo and not redone: `procedural-dungeons.md` (Brogue, Spelunky, Diablo 1, PMD, Hades, Unexplored, Gungeon, Cogmind), `dc1-rea.md` and `dc2-dungeons-town.md` (decomp internals), `genre-landscape.md`.
- **The code was checked first** (`dungeon-deep-dive/00-code-state.md`). Main already implements most of the earlier procgen recommendations: layout presets, route arcs, a capped special room, door reward hints, Hades-style loot-mix steering, vignettes with pity and state-conditioned odds, chunk regions, atmosphere and the rare dark floor, roster subsets and floor twists (ambush, thin, crowded), healing points, a downed state with hold-to-revive, wipe = everyone down. This document recommends only what is **not** built yet.
- Stopped because sources began repeating across agents (the same handful of conclusions arrived from Zelda, Isaac, Persona, Diablo IV, Deep Rock and Compton independently), and the session's 200-search web budget was spent. reddit.com is blocked by the cloud proxy; GameFAQs, GDC Vault and several wikis returned 403. Gaps are listed in section 9.

## 1. The five findings that matter most

1. **Nobody remembers a Dark Cloud dungeon.** Across four fetched ResetEra threads, reviews and retrospectives, no one names a favourite or hated DC1/DC2 dungeon; they remember the loop, the weapons and the town, and the dungeons only as "the same pattern", with the Demon Shaft as a joke about 100 floors [V, 03b]. The top complaints, by count of sources: sameness (~24), length/padding (~14), weapon breaking (~10), attrition meters, forced single-hero floors, key-carrier hunting, RNG medals [V, 03, 03b]. Memorable dungeons are the open opportunity of the genre; the direct competitors found (Emberville, Aeruta, Somewhen) are 2D and solo [S, 03b].
2. **Sameness is perceptual, and layout barely moves it.** Kate Compton: a generator can make outputs that differ mathematically yet all feel like the same bowl of oatmeal; what counts is perceptual uniqueness, bought by a few big discrete decisions per output [V, 07b]. The evidence lines up: Persona 3 Reload added teleporters, rare chests and optional doors around Tartarus and critics still call the floors generic [V, 03]; Rogue Galaxy and Rune Factory's hand-built dungeons drew the same sameness complaint [V, 03b]; Diablo IV players objected to tedious repeated objectives more than to reused tiles, and Blizzard raised event odds from 10% to 60% because 10% was invisible [S, 07b]; Deep Rock players read the biome and mutator first, not the cave [V, 07]. With 12 combat rooms and 4-6 per floor, a player has seen most of the pool by floor 3 [I, 07]. **Change what happens on a floor, not where its walls are.**
3. **A dungeon is a machine with state; independent rooms are not a dungeon.** Zelda dungeons are remembered for one gimmick explored fully and a state change (water level, flipped tower) [V, 01]. Link's Awakening's Chamber Dungeons, the series' one "rooms from a pool" experiment, were received lukewarmly precisely because self-contained rooms in any order have no interlocking puzzles [V, 01]. Our generator has the same shape. Every floor needs at least one dependency between rooms.
4. **Co-op rules are where frustration hides.** In 12 co-op games compared, one player's death never costs the team its loot; total loss comes only from a full wipe or a timeout [V/S, 04b]. Almost none migrate the host; when the host leaves, clients lose progress, and that is the most consistent player complaint [S, 04b]. Valve's Left 4 Dead director slides say outright: no invisible leashes and no teleporting stragglers; make players stick together through enemies, shared resources and helpless teammates, and pace in waves (build up, peak 3-5 s, fade, relax 30-45 s) [V, 05].
5. **Combat and content carry replay; floors do not.** Isaac replays on hundreds of stacking items, not on its floors [V, 02]. 3D room roguelites that only shuffle rooms (Returnal, Ravenswatch, Gunfire Reborn) get about 20 hours before "too few rooms" complaints [S, 02]. Weak combat turns the same room into a chore (Rune Factory 5 / Azuma) [S, 03b]. For us, weapon growth, encounter design and events must do the replay work.

## 2. The model: what kind of dungeon the Hearthdeep is

We are not Zelda, Isaac or Dark Cloud. The research points to a layered hybrid, each layer borrowed from the game that does it best:

| Layer | Borrowed from | What it is for us |
|---|---|---|
| **Band** (4 floors) | Zelda dungeon | One band **gimmick** (a single host-owned state such as heat, water level or light), one shared **tool**, taught on generated floors and examined in the authored **set-piece floor** (4, 8, 12) and the boss. Each band ends differently (rescue, escape, collapse) so endings never repeat (Skyrim lesson). |
| **Floor** (generated) | Isaac + Deep Rock + Stardew | A **named bundle** of big decisions (sub-theme, light, roster, hazard flavour, objective type, at most one mutator) announced on the stairs; **elective rooms** in dead ends by priority; one **cross-room dependency**; a readable **floor map**. |
| **Room** (authored) | Level-design craft, Doom, Hollow Knight | **One idea** per room with a teach/use/twist beat, a landmark ("weenie"), a loop around a central obstacle, an encounter template, a foothold inside the door. |
| **Dive** | Dark Cloud + Deep Rock + L4D | Bank-or-push tension (our stir meter), co-op rules where one death never costs the haul, and a pacing layer that **breathes** instead of only ratcheting. |
| **Home** | Dave the Diver, Hades, Cult of the Lamb | The haul turns into something visible and usable at once (a lit lamp, a resident, a new verb), and the town changes the next dive. |

"Isaac, but 3D, but Dark Cloud" in one sentence: Isaac's **floor grammar** (a graph whose dead ends get jobs by priority, state-aware odds, a minimap you can deduce from, floor curses that remove information) on top of authored 3D rooms, inside Zelda-style **bands**, wrapped in Dark Cloud's **dive-and-rebuild loop**. What we must not take from Isaac: its co-op (half-strength "baby" players, a single screen), bomb-gated progress, and per-player state in generation [V, 02].

## 3. Built vs. missing

| Area | On main | Missing (this document's recommendations) |
|---|---|---|
| Layout | presets, route arcs, chunks, loops, dead ends | cross-room dependency, band gimmick states, secrets, vista links |
| Floor identity | atmosphere, dark floor, roster subset, twist | a **named** bundle shown on entry, mutators with stated rewards, objective variety |
| Rewards | door hints, special room (greybox), loot mix, vignettes | elective-room jobs for dead ends, events with choices, no-one-downed bonus |
| Navigation | none | **floor map** with fog, room state, lock colours, shared reveal |
| Co-op | downed + revive, wipe = all down | fail floor and protected piece, dropped piece on death, room-lock rules, gather-to-exit, retreat ping, sublinear scaling table, solo self-revive, host-loss mirroring |
| Pacing | stir latches 40/70/100, 90 s collapse | collapse scaled to distance, hunter that expires or can be shaken, stir relief, director-lite room spawns |
| In-room | authored spawns, ambush ring | encounter templates, arena rules, traps, elite affixes |
| Audio | (not checked) | per-band music with combat stems |
| Home | M6 town loop | three-beat return ceremony, town upgrades that change the dive |

## 4. Recommendations

Tags: **cheap** (Phase 1 sized), **later**, **big**. Ordered within each group by value for cost. Evidence file in brackets.

### 4.1 Design rules (no build cost; adopt now)
1. **No required door, floor or puzzle needs one specific hero.** Hero abilities open optional branches or shorten puzzles only. DC1 forced-hero floors and DC2's red/blue seals are among the repeat complaints; Tri Force Heroes' exact-three requirement was its most criticised feature [03b, 01].
2. **Never delete a weapon or its growth.** Durability, if it exists, means "needs mending" [03].
3. **No required N-player action.** Write co-op puzzles as "k of the players present" with a mechanical solo stand-in (a block on a plate, a timer), not a copy-controlled clone. The co-op route may be faster but must not out-reward the solo route by much [01, 05].
4. **No bomb- or consumable-gated required progress** (Isaac's Super Secret softlock complaints) [02].
5. **Every secret has a visible tell and a visible reward.** Dark Souls 2's no-tell walls drew backlash; Isaac's secrets work because the map is the clue [06, 06b].
6. **Side goals are deterministic and never missable.** DC2's RNG fishing medals, mimic-ruined time trials and missable photos are its most-hated extras [03].
7. **One idea per authored room**, written on a room card (concept, hazard verb, intended tool, solve time, teach/use/twist) [06, 06b].
8. **Modifiers are shown before entry, readable in the room, never random mid-fight, never hero-punishing**, at most one per floor, with a stated reward or stir trade. Mythic+ and Diablo IV both retired the affixes that broke these rules [05].
9. **Keep town management out of the dive.** Dave the Diver's prototype failed when restaurant and diving ran at the same time [V, 04b].

### 4.2 Cheap, high value
1. **Floor map.** It is missing, and five threads need it (secrets, room state, elective rooms, co-op orientation, revisit reasons). Fog of war by cell, rooms revealed on entry, every door shown with its lock colour (Metroid Prime hid doors and drew complaints), Resident Evil-style room state (unseen / loot left / done), always-visible counters for pieces, chests and sleepers left, a warning when leaving with any left (Pikmin), and a shared reveal in co-op. Regenerated floors give no reason to withhold it; Hollow Knight's complaints were about access (buying it, updating only at benches), not content [06b, 09, 02].
2. **Objective variety beyond the key carrier.** Today every floor ends the same way: hunt the carrier. Add 2-3 objective types per band (key carrier; hold the hearth or oven against waves; a sealed vault opened by a lever or brazier sequence across two rooms). This is also the cross-room dependency from finding 3. Telegraph the carrier (a glint, and a ping after about 60 s or when few enemies are left) so it is never hunted blind [07b, 03, 01].
3. **Name the floor bundle and add mutators with rewards.** The atmosphere, roster and twist rolls already exist; correlate them into a named kind ("the smoking larder", "the dark cellar") shown on the stairs card with the floor's counts. Add 0-1 mutator per floor, 0-2 deeper, with guaranteed clean floors (Deep Rock always has mutator-free missions). Candidates: **Stirred floor** (+10 stir, double chest rolls), **Cold hearth** (no healing point, +1 revive on bank), **Swarm** (kill all to open the stair, Stardew's infested floors), **Quiet** (a breather reward floor). Isaac's information curses (no map, chests hidden, small light) fit; its Maze and Unknown curses do not fit networked co-op [07, 07b, 05, 02, 03].
4. **A band gimmick as one host-owned flag, toggled one-touch.** For example: Cellars dampers and ovens (heat), Cisterns water level from one wheel, Roots lanterns revealing walls. Pool rooms carry a state-A/state-B variant. The Water Temple's worst complaint was the menu swap for Iron Boots, which the 3DS version fixed with quick-assign; the gimmick control must be a single interaction [01, 06b].
5. **Elective rooms in dead ends, by priority.** Isaac sorts dead ends farthest-first and assigns jobs in a fixed order [V, 02]. Ours: vault (exists as the locked room), mending spring, challenge hall (offered when the party is healthy), stir altar (take stir for a reward). The special-room tag already exists; this gives its kinds real effects.
6. **Death and failure rules.** Keep "fail only when everyone is down" (built). Add: a downed hero who bleeds out drops their carried piece where they fall for anyone to pick up; a failed dive keeps about 25% of unbanked coins and materials (Deep Rock's verified failure payout) and weapon growth earned; each present player saves one unbanked piece chosen at the moment of loss (Dave the Diver's chosen item); each failed dive advances one resident story beat (Hades' "take the sting out of failure") [04b]. Conflicts with the current rule "a failed dive loses the unbanked haul" (section 5).
7. **A collapse clock that is fair.** 90 s is far tighter than Deep Rock's 5-minute pod. Scale it to the path time to the nearest bank point x1.5 with a 60 s floor; telegraph from about stir 85; mark the nearest bank point on the map; end early when every living player stands at the bank point [04, 04b].
8. **A stir meter that breathes.** Pressure that only rises becomes dread. Let the tier-2 hunter be shaken or expire (Shiren's Behemoth vanishes; Etrian FOEs are visible and avoidable), let banking or a lit lamp lower stir, and add the L4D micro-layer later (next room's spawns +/-1 by the party's recent damage, with a guaranteed quiet stretch after a peak) [03b, 05].
9. **Scaling by players present, sublinear, count and HP together.** Proposal to tune: enemy count 1.0 / 1.45 / 1.85 / 2.2, enemy HP 1.0 / 1.15 / 1.3 / 1.45, damage flat, bosses mostly HP (about 1 / 1.7 / 2.2 / 2.6, in line with Monster Hunter's curve). Solo gets one self-revive per dive (Nightreign) and slightly fewer enemies (Deep Rock's compensation for losing the robot). Stir's player factor should grow with the square root of players, not linearly. Pure HP inflation is the loudest scaling complaint in 3D co-op roguelites [05, 02].
10. **Room locks without leashes.** Lock the exits only, never the entry; a short "wait for the party" ring at the door; late players walk in; downed or disconnected players are not required for the clear; dead ends never lock. Leave the floor through a **gather-to-exit** ring that ignores fallen players (FFXIV's cairn), so nobody leaves friends behind. Add a **retreat ping** any player can raise. No published fix exists for the lagging-player arena problem, so this needs a risk review [02, 05, 04].
11. **Encounter templates and arena rules.** One enemy type for a breather, two for a normal fight, three for a complex one; pair roles that cover each other (rusher + lobber, shield + caster); at most 8 live enemies at four players; waves at most two deep. Fix the door problem (players fight from the entry) with a cover foothold inside the room, partitions and a reward deeper in, not more enemies. The agent drafted ten templates for our roster (`08`, 7.1) [08].
12. **Elites with one affix.** One elite per floor from band 2, marked before the fight; one affix on a known body (Diablo's budget: at most one defensive, one crowd-control); never wall-off or "invulnerable while others live" [08].
13. **Music per band with combat stems.** RPGFan's Dark Cloud complaint is exactly the same few tracks for hours, with a boss theme that remixes the combat one [V, 08]. Hades: a playlist per biome, drum and bass stems that come in for combat and drop out after, random stem combinations, sometimes silence, a closing tag on victory. One piece per band, one per set piece, a real boss theme; stir thresholds add a low drone [08].
14. **Floor time budget.** Generated floor 4-7 min solo (up to +25% at four players), set piece 8-10, a band 20-30 min, with a bank point between bands so co-op sessions land at 30-90 min. Add it to the dive walk tests [09, 03].
15. **Return ceremony in three skippable beats (8-12 s).** The town changes in view; pieces are delivered per carrier with one resident line; a new verb or shop item appears at once. Credit every present player, including the fallen. Batch when there are many pieces (Cult of the Lamb's chore complaints are the warning) [04b].
16. **Town upgrades that change the dive.** A lamp-keeper adds collapse seconds, a cartographer marks bank points on the map, a smith adds mending, a shop sells an escape item that always works (Moonlighter's paid pendant failing when you couldn't pay was disliked; DC1's escape powder just worked) [04, 09].

### 4.3 Later
1. **Secrets from the grid.** Candidates are empty cells touching 2+ rooms, with a crack and a glint; the empty pocket shows on the map as the clue; the co-op "hearth mark" opens it faster, a solo route always exists [02, 06, 06b].
2. **Vignettes grow into events with a choice.** Each must change state or offer a trade (a trapped resident, a wandering peddler, a cart with a risky chest, a shrine that swaps the mutator). Slay the Spire makes about 22% of its map unknown rooms [V wiki, 07b].
3. **Sleeper mini-beats.** Between set-piece rescues, one or two generated floors per band hold a lighter sleeper vignette: carrying them home slows you until banked. Pikmin 4 ends most caves with a castaway and warns if you leave one [09].
4. **Named variants on repeat dives** (Risk of Rain 2 stage variants with a log entry): after a band is cleared once, later dives may roll a flooded wing or an opened door with its own journal entry [07b].
5. **State-aware elective odds** from party-level numbers in the host seed bundle (hurt party: mending spring likelier; healthy party: challenge hall). Never per-player stats: clients disagree and the subset changes [02].
6. **Pool size: 20+ rooms per band before shipping a band**, theme-folder dressing variants first (same geometry, 2-3 prop and spawn sets), then more geometry. Hades' Gorinstein said making rooms modular cost more than making more rooms, so pilot chunk-heavy rooms on one room before scaling [03b, 07b].
7. **Knowledge that survives failure** (Shiren's bestiary): enemy notes and per-band map knowledge persist, so repeats feel like progress [03b].
8. **Band tools stay useful in later bands** for optional branches (Twilight Princess' Spinner went unused after its dungeon) [01].
9. **Traps that hurt both sides**, visible before the fight, triggerable from safety (Spelunky): barrel ruts, flour sacks, rhythm spikes; Cisterns valves and flushing grates; Roots snap-bulbs [08].
10. **Host loss.** Mirror the unbanked haul to each client on pickup and floor clear; if the host drops, clients keep banked items, the fail floor and their saved piece. Nobody in the genre does this well, which makes it an opportunity; it touches save data, so `risk-reviewer` [04b].
11. **Difficulty and assists.** Per-player assists affect only that player (damage taken, lock-on, map reveal); world assists (enemy HP, stir rate) are host-set and shown to all. Hades' God Mode (20% resistance, +2% per death, capped at 80%) is the gentle-ramp model; name options neutrally, as Celeste learned [09].
12. **Weekly rule-twist seed** (parked gauntlets): host seed plus a modifier set, unlimited practice [09].

### 4.4 Big
1. **One multi-level pilot room** (a balcony or pit seen from a neighbour). It is the largest "feels 3D" gain and the grid does not support it; prove it in one room first [07, 06].
2. **Connector variety** (arch, barred gate, crawl, secret door) beyond the identical 4-bit door [07].
3. **An alternate path with a toll** from a set-piece floor (Isaac Repentance) [02].
4. **A post-game** that is a scaling ladder or rule-twist dives, never 100 bare floors [03, 09].
5. **Physical carrying** of pieces (Lethal Company, REPO), cosmetic first with the host count as the truth [04].

### 4.5 Avoid
Bare floor counts as content; RNG-gated or missable side goals; forced hero swaps; hard N-player requirements; bomb-gated progress; Isaac-style weak co-op partners; per-player HP in generation; the Maze and Unknown curses in co-op; value loss on hit (REPO; damage is constant in a combat game); random mid-fight affixes; any approach that needs baked lighting per generated layout (section 6); map vendors or bench-only map updates; late fetch quests that force revisits (Metroid Prime's Artifact hunt is the most resented backtracking) [all].

## 5. Conflicts with the current plan (decisions for the user)

| # | Current | Research suggests | Why |
|---|---|---|---|
| 1 | A failed dive loses the whole unbanked haul | Keep 25% of coins and materials, weapon growth, and one saved piece per player | Every comparable co-op game softens loss [04b] |
| 2 | Stir player factor linear, x(1 + 0.25 x (players - 1)) | Square-root growth | Linear speeds four-player dives into the hunter and collapse [05] |
| 3 | 90 s collapse | Distance-scaled, 60 s minimum, early close | Fairness, and the DC time-trial anxiety players disliked [04, 03] |
| 4 | Host disconnect ends the session and the unbanked haul | Mirror the haul to clients | Most consistent co-op complaint; save-data change [04b] |
| 5 | Key carrier is the only floor objective | 2-3 objective types per band | Diablo IV evidence on repeated objectives [07b] |
| 6 | Room pool of 12 combat rooms | 20+ per band before a band ships | Pool seen within 3 floors [07, 03b] |
| 7 | PHASE1 3.5 names additive room scenes + APV as the lighting fallback | Drop it: APV baking sets cannot cover shuffled layouts | Section 6 [07b] |
| 8 | Healing point is a flat 51% roll | Make it an elective dead-end room, likelier when the party is hurt | Isaac's state-aware rooms [02] |

## 6. Unity notes for generated floors (to prototype, not decided)
- **Lighting:** baked occlusion and APV baking sets suit fixed scenes, not shuffled rooms; light probes cannot be created at runtime; Enlighten realtime GI is on its way out. Realtime lights with no lightmaps keep the atmosphere roll and the dark floor free. Per-room prefab lightmaps are the fallback, with seams at doors [V, 07b].
- **Culling:** GPU Resident Drawer with GPU occlusion handles runtime objects (Forward+, no MaterialPropertyBlock, no `OnBecameVisible`, not with static batching); room-graph culling by door mask is the cheap backup [V, 07b].
- **NavMesh:** per-room surfaces baked in the editor, joined by links at the fixed door positions, host-only [I, 07b].
- **Generation:** spread instantiation over frames behind the floor card; seed each room from (floor seed, cell) so a late joiner can rebuild one room (Noita) [V/I, 07b].

## 7. Measuring sameness
Extend the Soak tier: a floor fingerprint (room ids from entry, light state, roster, vignette tags, objective, mutator, entry-to-exit distance) with a cap on the top-1 share; a minimum route difference between floors of one dive; dry-run counters for the longest stretch with no vignette, mutator or dark floor. The human test: show testers eight floor screenshots from the fixed cameras and ask which two were the same floor, then later ask them to name one memorable thing per floor (Compton's test) [07b]. This makes M7's "three runs, did it feel samey" check concrete.

## 8. Suggested order if the user wants to act
1. Adopt the rules in 4.1 into PHASE1 (doc change only).
2. Decide the conflicts in section 5.
3. Build in this order: floor map, death and fail rules, objective variety with one cross-room dependency, named bundle plus mutators, scaling table and room-lock rules, encounter templates. Then the band gimmick for the Cellars (dampers and ovens), elective rooms, music stems, collapse fairness.
4. Plan the Cisterns band (floors 5-8) around a gimmick and a tool from day one.

## 9. Gaps and how to close them
- No Reddit or GameFAQs text (proxy block); per-dungeon DC1/DC2 memories are thin. A PC session can read those boards.
- Not read: Mark Brown's Boss Keys transcripts and charts, Compton's GDC talk text, the Path of Exile and Bad North talks, Hades' room scripts, Isaac's minimap reveal rules, Persona 3 Reload's per-block music list, published studio sameness telemetry.
- Inferences to playtest: every number in the scaling table, the room-lock rules, puzzle lengths, the floor time budget and the fail-floor share.
- Another research round needs a larger web-search budget (`CLAUDE_CODE_MAX_WEB_SEARCHES_PER_SESSION`, the user's setting).

## Evidence notes (`docs/research/dungeon-deep-dive/`)
| File | Topic |
|---|---|
| `brief.md` | The shared brief and rules the agents followed |
| `00-code-state.md` | What main implements today |
| `01-zelda.md`, `06b-craft-and-zelda-deep.md` | Zelda dungeons, Chamber Dungeons, co-op Zelda, Zelda-likes, maps, secrets, band gimmicks |
| `02-isaac-3d-roguelites.md` | Isaac's floor grammar, curses, secrets; Returnal, Hades, Ravenswatch, Windblown, Risk of Rain 2, Nightreign |
| `03-dc-jrpg-crawlers.md`, `03b-dc-sentiment-deep.md` | Dark Cloud sentiment, ranked complaints, what fans want; Persona, Mystery Dungeon, Azure Dreams, Etrian, Stardew |
| `04-hub-loop-extraction.md`, `04b-hub-loop-extraction-deep.md` | Hub and dive loops, extraction tension, co-op loss and host-loss rules, return ceremony |
| `05-coop-dungeons.md` | FFXIV deep dungeons, WoW Delves and Mythic+, Diablo IV, Deep Rock, L4D director, scaling, revives, room locks |
| `06-design-theory-3d-craft.md` | Jaquaying, five-room dungeon, kishotenketsu, Dark Souls, Elden Ring, Skyrim, Resident Evil, wayfinding |
| `07-3d-procgen-variety.md`, `07b-procgen-deep.md` | Compton, Warframe, Deep Rock, Path of Exile, Remnant, Risk of Rain 2, Torchlight; Unity lighting, culling and NavMesh |
| `08-encounters-arenas-bosses-audio.md` | Encounter composition, arenas, traps, elites, bosses, music |
| `09-dungeon-ux-people-meta.md` | Maps, escape items, floor length, rescues, revisits, post-game, assists |
