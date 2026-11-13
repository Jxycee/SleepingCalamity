# Dungeon 1: the Cellars (design)

**Status:** proposal from Fable Run 3, 2026-11-13. Not approved. The user approves this before any build session starts. Decisions are current direction, not locks.
**Builds on:** `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` (signed off), `docs/ROOM_METRICS.md`, `docs/STORY.md`, `docs/PHASE1.md` section 3, and the generator in `Assets/_Project/Core/Cellars/` (rooms v2, seven floors).
**References, in order:** the user's concepts C1-C8, Dough-warden and Rat-king (`Reference/private/concepts/`), then `Reference/hollowmere/dungeon_corner/` and `Reference/private/`.
**Assets:** codes in square brackets are rows in `docs/design/area1-dungeon1-needs.md`. "ours" means a piece under `Assets/_Project/Content/Art/Cellar/`.
**IP:** nothing of Dark Cloud's names, characters, text or assets.

## 1. Scope and names

"The Cellars" here means floors 1 to 7 of the Undercroft, the part Phase 1 builds (`UndercroftPlan.cs:11`). STORY and the spec call the whole ten-floor dungeon the Undercroft. Code and the concept sheet say Cellars. (Decision C3.)

What the code builds today:
| Floor | Kind | Content | Source |
|---|---|---|---|
| 1 | authored | entry, pantry, hanging hall, stair | `CellarFloorGenerator.SetPieces.cs:34-48` |
| 2 | generated | larder rooms | `CellarFloorGenerator.cs:80` |
| 3 | set piece | bakery: oven maze, calm antechamber, baker's hearth, stair | `SetPieces.cs:51-72` |
| 4 | generated | wine rooms | `CellarFloorGenerator.cs:80` |
| 5 | set piece | guardian arena, then the bank room; no stair | `SetPieces.cs:75-85` |
| 6 | generated | coal rooms | `CellarFloorGenerator.cs:80` |
| 7 | generated | coal rooms and the end cell | `CellarFloorLayout.cs:116` |

The concept sheet numbers its looks differently (larder 1, wine 2, coal 3, ovens 4). This design keeps the code's floor order and maps the looks by theme: C2 to floors 1-2, C7's ovens to floor 3 (cold) and floor 7 (lit), C3 to floor 4, C4 to floors 6-7, C5 to the bank room, C6 to the entry, C1 and C8 to corridors and combat rooms everywhere.

Fixed metrics (unchanged): 4 m cells on a 14 x 14 grid, walls 3.6 m, room vault crown 5.1 m, corridor crown 4.1 m, doors 2.4 x 2.8 m, combat rooms 2x5 to 5x4 cells.

Where the look stands today (`Reference/cellars/variety/floor_room.jpg`, `Reference/hollowmere/dungeon_corner/SHEET_dungeon_corner_unity.jpg`): the vault shape is right. The stones read about 1 m across (the concepts show 25 to 30 cm courses). Everything is one red-brown. Floors cannot differ, because one kit and one material set serve all seven.

## 2. Six ideas

1. **One descent, seven moods.** A depth gradient runs through everything: damp and candle-lit near the church, golden in the wine vaults, dry, black and ember-lit near the breath. Each floor has one landmark the player can name.
2. **Lamps are the path.** Gold pools mark the way from entry to stair. Unlit means optional or dangerous. The red breath always means "down".
3. **The town's things are down here.** Each floor shows something that belongs upstairs: the bakery's sign and ovens, casks with the inn's brand, the church's own stair. The player sees what they are taking home.
4. **Stone at the right size.** Rebuild the kit's surfaces at 1024 px/m with real courses, brick vaults and peeling limewash, as C1.
5. **Small life.** Rats that bolt from lamplight, moths at the lanterns, a drip into a puddle, dust in the light. None of it fights.
6. **A safe room that feels safe.** The bank room is the C5 nook: candle, bedroll, chests, a chalk mark, and two ways out told apart by light alone.

## 3. The depth gradient

A per-floor look table. Today there is none (one kit, values rolled per seed), so this is a proposed Presentation table, `CellarFloorLook` (Decision C6). Values are starting points for the lighting test corner. Every shadow stays warm: no teal, no green, nothing pure black (PHASE1 3.1).

| F | Name | Walls | Lamp tint | Ambient, top to bottom | Fog tint, density | Breath | Wet | Moss | Roots | Dust | Sound bed |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | Church Cellar | pale vault stone, 40% limewash, tan brick arches | candle #F2D58A | #3a2c26 to #2a1c18 | (0.16, 0.13, 0.11), 0.007 | 0.15 | 0.6 | 0.5 | thick at the entry | 40 | drips, wind in the church above |
| 2 | Cold Stores | 70% limewash, soot ceiling in the smoke gallery | candle | #3a2e2a to #2a1e1c | (0.15, 0.14, 0.13), 0.009 | 0.2 | 0.5 | 0.3 | thin | 30 | slow drips, hooks creaking |
| 3 | Drowned Bakery | wet dark brick, a flour-paste tide line at 0.5 m | sparse amber #E8A63C; one gold hearth | #34282a to #241a1c | (0.14, 0.12, 0.12), 0.011 | 0.25 | 1.0 | 0.2 | none | 20 | water lapping, wind in dead ovens |
| 4 | Wine Vaults | honey brick vaults, salt bloom | amber, the brightest floor | #40302a to #2e1e1a | (0.17, 0.13, 0.10), 0.006 | 0.35 | 0.7 | 0.1 | none | 35 | cask creak, far bubbling |
| 5 | Cistern Dome | dressed stone, iron sluices | amber and the red breath | #3c2a26 to #2c1a18 | (0.18, 0.12, 0.09), 0.010 | 0.6 | 0.4 | 0 | none | 20 | steam hiss, a deep hum |
| 6 | Coal Cellars | soot brick, coal face | ember #D2562A, a few oil lamps | #30221e to #1e1412 | (0.14, 0.09, 0.06), 0.012 | 0.75 | 0 | 0 | none | 80, as ash | far bellows, ticking metal |
| 7 | Stokehold | soot brick with glowing cracks | ember, furnace mouths | #38201a to #22120e | (0.18, 0.09, 0.05), 0.012 | 1.0 | 0 | 0 | one ember-veined root at the end door | 90 | furnace roar, rumble |

- "Breath" scales the red glow from grates and stairwells (`BreathGrate` exists in the kit).
- "Wet", "Moss" and "Dust" are 0 to 1 weights for the overlay rules in section 7.
- The existing dark-floor roll (`CellarAtmosphere.cs`) stays and multiplies lamp level on top.

Value bands: PHASE1 3.1 gives the Cellars 0.06-0.10 / 0.24-0.32 / 0.55-0.68; `sc-scene-look` gives 0.12-0.16 / 0.30-0.40 / 0.65-0.75. The concepts look closer to the brighter set. Proposal: the brighter band for lit rooms on floors 1 to 5, the darker band for floors 6 and 7 and for rolled dark floors. B0 measures C1 to C8 with `sc_ref_palette.py` and settles it. (Decision C5.)

## 4. Floor by floor

### Floor 1. The Church Cellar (authored)
- **Reads as:** the church's own store. Homely, a little damp, still safe-feeling.
- **Landmark:** the daylight shaft under the trapdoor (C6): cool light on wet steps, warm lamp beyond. The only daylight in the dungeon.
- **Set piece:** the Rat-king's court in the hanging hall. A throne of crates under the hook rail, a hoard of spoons and tin lids that glints. The concept gives him keys on his belt, so he is the floor's key carrier. (Decision C2.)
- **Also here (spec):** the sealed alcove with one pale sigil, the single non-warm light below the entry. A drain grate with the first red glow.
- **Palette, light:** pale stone, limewash, candle gold. Lamps on every route cell.
- **Surfaces:** roots through the entry vault, cobwebs on the stair, moss in the floor joints near the shaft.
- **Hooks:** `cellar_entry`, `larder_pantry`, `larder_hanging_hall`, `cellar_exit`; the `Carrier` kit field.

### Floor 2. The Cold Stores (generated, larder)
- **Reads as:** deeper, colder stores. White walls, hanging rows, quiet.
- **Landmark:** the smoke gallery: a soot-black vault with rows of hanging meat and a haze layer at 2.4 m. If the roll leaves it out, the hanging hall takes the landmark.
- **Palette, light:** limewash white against soot black. Fewer lamps than floor 1.
- **Surfaces:** damp, thin roots in ceiling corners, the last moss.
- **Hooks:** `larder_pantry`, `larder_cold_store`, `larder_hanging_hall`, `larder_smoke_gallery`.

### Floor 3. The Drowned Bakery (set piece)
- **Reads as:** the town's bakery, dragged down and drowned. Cold, grey, dripping. The one sad floor of the first half.
- **Landmark:** the Great Oven in the heart room, a beehive oven face 5 m across. The baker sleeps on its hearth.
- **Set piece:** every other oven is dead and wet, mouths black (C7's shapes with no fire). A grey tide line of flour paste runs along every wall at 0.5 m. Peels and trays lie where the water left them. The bakery's sign (the same piece as the town's `ShopSign_Bakery`) lies in the kneading hall.
- **Guardian (Decision C1):** the Dough-warden kneels before the Great Oven like a statue. It is the only thing in the bakery still burning; it took the fire into its chest. Beat it and the Great Oven relights, the room turns gold, and the baker wakes. The spec already asks for "a lighter guard than a boss" here.
- **Palette, light:** wet black stone, flour grey, dull brick, plum shadows. Almost no lamps; candle stubs on the kneading tables; one hearth glow. After the win, full gold.
- **Surfaces:** wet everywhere, drips in every room. No standing water in Phase 1 (`CellarPieceSpots.cs:56`); the wet look, tide line and sound carry "drowned". (Decision C7.)
- **Hooks:** `bakery_oven_mouth`, `bakery_proofing`, `bakery_flour_store`, `bakery_kneading_hall`, `bakery_flue_antechamber_calm`, `bakery_heart`; the `Baker` feature.

### Floor 4. The Wine Vaults (generated, wine)
- **Reads as:** the grandest and brightest floor. Long golden vaults (C3). The last comfortable place.
- **Landmark:** the Great Tun in the press hall, a cask face 4 m across with the inn's mark, its tap dripping into a red pool.
- **Palette, light:** honey brick, oak, wine red (#7a2230) as the accent, white salt bloom on the vaults. Cage lanterns on chains down the centre line. A wet floor that mirrors the lamps.
- **Surfaces:** wet floor, salt on the vaults, no moss. The first steam: floor grates that vent warm mist (look only).
- **Hooks:** `wine_cask_row`, `wine_vault_l`, `wine_press_hall`, `wine_aisles`; the Casks chunks.

### Floor 5. The Cistern Dome and the bank room (set piece)
- **Reads as:** the hinge of the dungeon. Water above, fire below.
- **Landmark:** the dome: a round hall ringed by sluice arches, with a central drain grate that glows red and steams.
- **Guardian:** per the signed-off spec this is the Brazen Ox (decision 46). Code places a "Warden" stand-in here. See Decision C1.
- **Then the bank room** (section 8).
- **Palette, light:** dressed grey-gold stone, black iron, the red glow from the grate, steam catching the lamplight. A rusted chandelier [C15] under the dome.
- **Hooks:** the guardian and bank cells (`CellarFloorLayout.cs:89-90`), `cellar_checkpoint`.

### Floor 6. The Coal Cellars (generated, coal)
- **Reads as:** work, soot and heat. The first floor that feels hostile.
- **Landmark:** the coal chute in the bunker: a dark shaft with a cone of coal beneath it and dust falling in the lamplight.
- **Path cue:** cart rails run from the entry toward the stair. Follow the rails.
- **Palette, light:** soot black-brown, dull red brick, ember orange. Oil lanterns far apart, furnace cracks. The darker value band. Ash falls instead of dust.
- **Surfaces:** dry. Soot streaks above every lamp and furnace. Coal grit on the floor.
- **Hooks:** `coal_bunker`, `coal_furnace_ring`, `coal_stoke_chicane`, `coal_boiler_hall`; the Rubble and Arch chunks; `CoalChute`.

### Floor 7. The Stokehold (generated, coal, the end)
- **Reads as:** right above the fire. Loud, red, everything glowing at the edges.
- **Landmark and end:** the Damper Door (section 9).
- **Set piece:** the furnace ring with its furnaces lit: C7's beehive ovens, burning. The cold ovens of floor 3, seen again alive.
- **Palette, light:** ember red-gold on black. Light from below through cracked floor tiles. Sparks.
- **Hooks:** the same coal rooms; `EndCell` (`CellarFloorLayout.cs:88`).

## 5. Room kit

Rebuild the surfaces of the existing kit (`Content/Art/Cellar/FloorKit/`, generators in `Art/Blender/cellars/`) and add the pieces the concepts need. No free kit on Fab fits: they are low-poly or Unreal-only (sheet C13).

| Piece | Status | Notes | Budget |
|---|---|---|---|
| Vault bay 4 x 4 m (VaultSegment), arch rib (ArchRib) | ours, re-texture | brick vault [C01] Old Brick Wall, stone rib every cell (C1) | 0.5-5k triangles, shared 2K sets |
| Groin bay with corner pillars | new | for rooms three or more cells wide (C8) | same |
| Pillar | ours, add a round drum with a capital | square pier on coal floors | same |
| Wall panels: plain ashlar, niche (NicheWall), limewashed, brick-infill arch, soot | ours and new variants | courses 25 to 30 cm; [C01] and ours vault_stone, limewash | same |
| End wall, door frames: arched oak door, iron grate gate, wide arch | ours and [C13] | door 2.4 x 2.8 m | M |
| Floor: flagstone tile in four variants, drain grate (BreathGrate), rail segment, cracked ember tile | ours and [C02], [C09] | 2 x 2 m tiles | shared 2K |
| Stair (CellarStair), ladder | ours, [T04] ladder | | M |
| Lights: wall cage sconce, chain cage lantern, oil lantern, candle clusters, furnace mouth | [C15], ours CellarLantern | section 6 | S |
| Larder set: stone shelf (LarderShelf), hook beam with hanging goods, crocks, sacks, barrels | ours and [C05], [C06], [C11] | | S and M |
| Wine set: cask on a cradle, bottle rack bay, cask rows, ladder, screw press, the Great Tun | ours and [C07] | press and tun are hero pieces | H for the two heroes |
| Coal set: coal heaps in three sizes, chute (CoalChute), cart, rails, furnace face, boiler drum | ours and [C08], [C09] | | M; boiler H |
| Bakery set: beehive oven (dead and lit), kneading table, proofing racks, peel, trays, flour sacks, troughs | ours, new, and [C10], [C11] | the oven is ours: nothing free fits (sheet C10) | oven H |
| Overlays: cobweb cards, roots, moss, drip and puddle decals, salt bloom, soot, limewash peel, flour dust, chalk marks | [C03], [C04], [C14], ours B3 trims | section 7 | decals, S |

### Dressing by room
| Room | Signature | Sockets and clutter | Lights |
|---|---|---|---|
| `cellar_entry` | stair foot; the daylight shaft on floor 1, a dark stair elsewhere; ladder | crates, one barrel | 1 sconce |
| `cellar_exit` | stair down with red light rising from the well | rail post, nothing in the way | 1 sconce, breath glow |
| `corridor_*` | a rib at each cell | 12% clutter chance as now; cobweb and one crate in dead ends | sconce every second cell on the route, none off it |
| `larder_pantry` | stone shelves on both long walls, crocks [C06] | sack row [C11], a rat hole in the brick end wall | 2 sconces |
| `larder_cold_store` | white walls, salt tubs, hanging game, a low mist card | barrels, crates | 1 dim lamp |
| `larder_hanging_hall` | a hook rail down the centre; goods [C05] hang above 2.2 m like a canopy | crate stacks at the edges | 2 sconces |
| `larder_smoke_gallery` | soot vault, smoking racks, haze at 2.4 m | an ember pan [T16] brazier | ember glow, 1 lamp |
| `wine_cask_row` | casks on cradles along one wall, bottle racks opposite [C07] | ladder | chain lanterns |
| `wine_vault_l` | a great arch at the bend, one burst cask and a wine pool | casks | chain lanterns |
| `wine_press_hall` | the screw press and the Great Tun | tubs, Casks chunks | chandelier |
| `wine_aisles` | rack aisles as cover (Casks chunks) | bottles | lanterns on the centre line |
| `coal_bunker` | heaps to wall height in the corners, the chute, dust fall | cart, rails | 1 oil lantern |
| `coal_furnace_ring` | furnace mouths around the central pier; cracked ember floor | bellows, coal | furnace light |
| `coal_stoke_chicane` | rails bending between heaps, a tipped cart [C09] (C4) | coal | 1 oil lantern |
| `coal_boiler_hall` | a riveted boiler drum, a fallen arch (Arch chunks), a steam leak | rubble | ember, steam glow |
| `bakery_*` | section 4, floor 3 | flour sacks, tables, racks | candle stubs |
| `cellar_checkpoint` | section 8 | | candle |

Combat rooms keep the room-card rule (spec section 7): a landmark, a loop around a central obstacle, a cover foothold inside the door. Dressing never enters the fight space; it sits on walls, in corners and above 2.2 m.

## 6. Light rules

- **Route lamps.** Warm lights go first to cells on the entry-to-stair route (`CellarFloorRoute`). Side rooms get one. Dead ends get none. (Proposal P2.)
- **Rhythm.** A lamp pool is 5 to 6 m across; leave 2 to 3 m of shade between pools in corridors.
- **Lamp:** gold #d6a24a at the flame, with the floor's tint from section 3. Keep the caps (6 lights a room, shadows on the nearest 2 to 4).
- **Breath:** red #c2412a from grates and stairwells, scaled by the floor's breath value. Never used for anything else.
- **Cool light** appears three times only: the daylight shaft on floor 1, the sigil alcove, and the way home in the bank room.
- **Fixtures:** cage sconce on walls (C1), cage lantern on a chain in rooms with a crown (C3, C8), oil lantern on coal floors (C4), candles in safe places (C5).
- **Motes:** dust in light cones only (exists); ash on floors 6 and 7; steam on floors 4 and 5.

## 7. Surface rules

| Overlay | Where | Rule |
|---|---|---|
| Wet | floors 1 to 5 by the wet value | puddle decals in low cells and along walls; raise smoothness and darken albedo to about 0.7 within one cell of a drip; a drip emitter under the vault crown, one per two rooms on floor 1 up to one per room on floors 3 and 4 |
| Moss | floors 1 and 2, a trace on 3 and 4 | olive and yellow only (#616e2e, #b6ab28), in floor joints and wall feet, always inside a lamp pool or the daylight shaft |
| Roots | floor 1 thick at the entry, floor 2 thin; one ember-veined root at the floor 7 door | on outer walls and vault corners only, never in the walking line (the `Roots` socket exists) |
| Cobwebs | every floor, fewer with depth | dead ends, door heads, behind shelves; none where lamps are |
| Salt bloom | floor 4 | white streaks on vault crowns |
| Soot | floors 2 (smoke gallery), 6, 7 | above lamps and furnace mouths; black ceilings |
| Flour | floor 3 | the tide line; drifts in the flour store |
| Chalk marks | Wren's mark at the bank room; small arrows near stairs on floor 1 | the same mark as her list wall in town |

All overlays are Presentation-only and seeded per floor. (Proposal P5.)

## 8. The bank room (floor 5)

Concept C5. Reached after the guardian.
- **The nook:** under a stair, timber-braced. A bedroll and pack [C12], a rough table with a candle and a scrap of map [C12], Wren's chalk mark on the plastered brick.
- **The bank:** chests [C12] against the wall, one per slot (solo 3; two players 2 each; three players 1 each and one extra; four players 1 each, as `BankSlots.cs`). In co-op each chest carries its owner's colour tag.
- **The door:** iron-bound, shut behind the party. Nothing follows them in.
- **Two ways out, told apart by light:** the stair up is washed in cool daylight (go home). The stair down glows red (push on).
- **Light and sound:** calm candle gold, no fog, no drone. The fight's music stops at the door.
- **Tuppence:** the cat waits on the bedroll and purrs. His collar keeps him safe (STORY). The cat means "safe". (Decision C4.)

## 9. The floor 7 end

The end cell becomes the **Damper Door**: an iron furnace door 3 m tall in the cell's far wall (the cracked door of C4 at full size).
- Glowing cracks; heat shimmer; cracked ember tiles for two cells in front of it.
- The rails end at its sill.
- One ember-veined root curls round the frame, the first hint of floor 9.
- **Opening it** (three beats): the cracks brighten, the door swings inward, white-gold light floods the cell. Cut to town in Evening light.
- It replaces the plain end column (`CellarFloorBuilder.Dive.cs:70`). No new rules: it is still the stand-in ending.

## 10. Ambient life and sound

| Thing | Floors | Behaviour |
|---|---|---|
| Rats [A04] | 1 to 4, six to ten a floor | run along wall feet between holes; freeze in lamplight; bolt when the player is within 4 m; never fight |
| Moths | every lit lamp | two or three cards circling |
| Cinder moths | 6 and 7 | orange specks at furnace cracks |
| Spiders | dead ends | one on a thread |
| Drips | 1 to 5 | a drop, a ring, a sound |
| Dust, ash, steam | section 6 | |
| Tuppence | bank room | section 8 |

Sound: one bed per floor (the table in section 3), plus emitters at drips, casks, hooks, furnaces and grates. Sources [S01]; most cellar ambience still has to be found or made.

## 11. Changes proposed to code

All small, none changes dive rules. Each is for the user to accept or refuse.
- **P1. `CellarFloorLook`:** a per-floor look table in Presentation (wall set, lamp tint, ambient, fog, breath, overlay weights, sound bed). Today seven floors share one kit and one material set.
- **P2. Route lamps:** warm lights prefer route cells.
- **P3. Rails on coal floors:** a trim kind that follows the route.
- **P4. One landmark socket per generated floor**, in its largest combat room (smoke gallery, Great Tun, coal chute, furnace ring).
- **P5. Overlays** (wet, moss, roots, cobwebs, salt, soot, vermin, drips): Presentation-only, seeded.
- **P6. A guardian cell on floor 3** if Decision C1 puts the Dough-warden there.
- **P7. Stone scale:** walls and floors at 1024 px/m with real courses.

Anything touching `Core/Cellars` needs the soak run before merge (CLAUDE.md).

## 12. Decisions for the user

- **C1. Where the Dough-warden stands.** The spec replaced it with the Brazen Ox on floor 5 (decision 46). The new concept (2026-11-13) brings it back as a grim oven guardian. Recommended: Dough-warden guards the Great Oven on floor 3 and the Ox keeps floor 5. Until an Ox exists, the floor 5 arena keeps using the Warden stand-in. Alternatives: Dough-warden on floor 5 in place of the Ox; or at the floor 7 door.
- **C2. The Rat-king is floor 1's key carrier.** STORY 85 says elites are fully demonic; the concept is storybook. Recommended: keep the concept and add ember eyes, so the dungeon's first face is whimsical with one wrong note.
- **C3. Name.** Use "the Cellars" for floors 1 to 7 in Phase 1 talk, and keep "Undercroft" for the whole dungeon.
- **C4. Tuppence waits at checkpoints.**
- **C5. Value bands** (section 3): which set is the target.
- **C6. The per-floor look table** (P1 to P5 follow from it).
- **C7. No standing water in Phase 1;** floor 3 reads "drowned" by wetness alone.

## 13. Not in this design
Floors 8 to 10, enemy rosters and tuning, the Brazen Ox's look, music, creature models (the two concepts go through the creature pipeline separately).
