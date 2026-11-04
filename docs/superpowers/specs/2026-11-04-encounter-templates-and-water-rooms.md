# Encounter templates and water rooms for the 12-room pool (spec step 5)

Date: 2026-11-04. Status: *proposal*, docs only. Serves step 5 of `2026-11-03-hearthdeep-dungeon-design.md` section 12 ("the water gimmick on the 12 rooms, bubbling variants for floor 4; encounter templates"). Reads sections 6 (floor recipe) and 7 (rooms and encounters), decisions 18 and 32, `docs/ROOM_METRICS.md`, `CellarRoomPool` / `CellarRoomsV2` / `CellarEncounterPlan`, and research note `08-encounters-arenas-bosses-audio.md` 7.1. Every number below is `(placeholder)` until a `sc-walk` playtest. The floor card and modifiers spec (`2026-11-04-floor-card-and-modifiers.md`) did not exist when this was written, so modifiers are not covered here.

## 1. Rules this respects
- **No respawn after leaving a room.** A room's encounter is spawned once, on first entry, from the pure plan. The host stores a per-room state (`Unspawned`, `Live`, `Cleared`). Leaving a live room freezes survivors where they stand; re-entering never spawns more. Waves are part of the one spawn (at most two deep) and finish inside the room.
- **Nothing missable.** Chests, key chests, the key carrier, valves, the pump point and the stair never sit on a cell that is under water or fire in any state (ROOM_METRICS gimmick rules). No loot or objective is gated on killing a particular enemy except the existing key carrier, who is always on a dry cell.
- **Full party fits.** Every combat room in the pool fits four heroes and eight enemies except `coal_stoke_chicane` (two heroes). Water must not break that: in every water state the room keeps at least 13 walkable cells, a short side of 3 walkable cells, and a walkable route between all doors. Wading (up to 50 cm) is walkable; only deep water removes cells. The chicane gets wade water only and keeps its two-hero tag.
- **Tier-1 enemies only** in the first dungeon (memory: first dungeon tier 1): Slime, Rat-king and Broom (`EnemyKind` 0-2). The Dough Warden is not used. The Brazen Ox is a floor-5 set piece, not part of this pool.
- **At most 8 live enemies with four players; at most 3 roles in a normal fight (2 on floors 1-2); waves at most 2 deep** (section 7).

## 2. Encounter templates

Roles: **R** = Rat-king (rusher), **S** = Slime (swarmer; **Sl** = slime lobber on a dais or behind cover), **B** = Broom (shield). Counts are total enemies by party size 1 / 2 / 3 / 4 `(placeholder)`. They replace "authored spawns plus `extraEnemiesPerRoom`" as the way a room's count is chosen; the authored `s` cells stay the first anchors.

| Template | Mix | Count 1 / 2 / 3 / 4 | Min room cells | Wants | Plays differently because |
|---|---|---|---|---|---|
| **Pack** | R only | 3 / 4 / 5 / 6 | 12 | open floor | Teaches rusher timing. One role, so it is also the breather on floor 1. |
| **Gel swarm** | S swarm, plus Sl in the 2-4 player counts | 4 / 5 / 6 / 8 (Sl: 0 / 1 / 2 / 2) | 14 | a pillar or ledge | Swarm against a lobber lane; the player uses cover. |
| **Shield wall** | B front, S behind | 3 / 4 / 5 / 6 (B: 1 / 1 / 2 / 2) | 14 | a chokepoint or door-width gap | Flank the shield; slimes block the retreat. |
| **Rush and bombard** | R, Sl | 4 / 5 / 6 / 7 (Sl: 1 / 2 / 2 / 3) | 14 | pillars | Two roles only; break sightlines. |
| **Guarded caster** | B front, Sl behind | 3 / 4 / 5 / 6 (B: 1 / 1 / 2 / 2) | 15 | a partition, solid or ledge | The shield blocks the route to the lobbers: the door-problem cure. |
| **Ambush** (rare) | R hidden in floor cracks | 2 / 3 / 4 / 4 | 15 | an empty-looking room | Enemy ambushes the player at the centre-line trigger. At most one per floor; never in a room with a deep-water state; this is the existing `FloorTwist.Ambush`. |

Template rules:
- Floors 1-2 use at most 2 roles; Pack, Gel swarm, Rush and bombard, Shield wall and Guarded caster all fit that. Ambush is floor 2+.
- A room may only draw a template whose Min room cells fit its walkable cells in the room's worst water state, and whose Wants it offers (table in section 3.3).
- The roster for the floor (`CellarFloorRoster`) still filters: a template is dropped if the floor's roster lacks one of its roles.
- Count above the live cap spills into the second wave (at most two deep): the first wave is at most 6 at four players, the rest spawn when it is down to 2 `(placeholder)`.
- The existing `S` (Tier2) glyph cells in key-chest and similar rooms: in the first dungeon they hold a Tier-1 enemy of the template's front role, not a Tier-2 (see Open questions).
- The elite (one per floor, one affix, from the second half) is outside this list; it replaces nothing here.

### 2.1 Spawn placement rule
Pure function of (layout, dressing, template, party size, seed), the same as `CellarEncounterPlan`: every peer derives the same cells; the host alone spawns. Own Rng stream (`cellars.encounter`, extended with the room index and template id).
1. **Candidate cells** = the room's floor cells that are not: a door, chest, key chest, feature, authored-dressing blocker, vignette cell, healing point, gate-key carrier cell, party start, stair, bank, baker, chunk solid (all existing filters), and not any cell that is deep or wading in any water state of the room (spawns stay dry in every state, so a flip never strands one).
2. **Entry door.** The entry door is the door nearest the party's route from the start (the one the party walks through). Remove every candidate within 3 cells (Manhattan, `(placeholder)`) of the entry door cell and of the cell inside it; this is the "keep spawns off the entry door" rule and also the room's door-problem fix. Room geometry that cannot leave enough cells (a 3x5 closet) relaxes the radius to 2, then to 1, never to 0.
3. **Anchors by role,** from the remaining candidates, ordered by walk distance from the entry door, farthest first, ties broken by the stream Rng:
   - lobbers (Sl) take the farthest cells, preferring a ledge or a cell behind a pillar or solid;
   - shields (B) take the cell nearest the middle of the room that is still at least 3 away from the entry door, between the entry and the lobbers;
   - rushers (R) and swarm (S) fill the rest, at least 2 cells apart where cells allow.
4. **Authored cells first.** Authored `s` spawns that pass step 1-2 are used first; those inside the entry exclusion move to the nearest legal cell (as chunk-solid spawns already do). The gate-key carrier stays keyed to its own cell.
5. **Too few cells.** Fewer enemies, never an illegal cell (existing rule). Report the shortfall to the overlay so a room that regularly runs short is resized.
6. **Trigger.** The encounter wakes when the party crosses the room's centre line or its second row of cells, not on the threshold (research section 2).
7. **After the room is left** nothing spawns (see section 1).

## 3. Water states for the 12 combat rooms

### 3.1 Water model used
- Surface heights in cm above the level-0 floor, one state index per region (`GimmickRegion.StateValues`): **dry** 0, **shallow (wade)** 40, **flooded (deep)** 80 `(placeholder)`. Under 50 cm walks, over blocks (ROOM_METRICS).
- Every region has two or three states, and every state changes something (the validator's rule).
- Cells in a water region never hold a door, chest, key chest, spawn, feature, valve or pump point.
- Warm tint on cellar water, no teal (PHASE1 P34, section 3.2).
- **Bubbling variant (floors 4-5 only):** a region's wade state gains a `bubbling` flag: steam vents on the cells and a damage tick for heroes standing in it `(placeholder 3 hp/s)`. Only wade cells carry it; deep cells still block. A dry route from every door to every other door must exist in every state.
- Rooms on the first-dungeon water half (floors 1-5) use every room in the pool, including the coal rooms; their heat states come later (section 3.2, step 7 of the build order).

### 3.2 Table: the 12 rooms
Size is cells wide x deep (4 m cells), walkable cells exclude solids and pillars. Doors counts include lockable ones.

| Room id | Size / walkable / doors | Shape | Water profile (states, default first) | Why it fits | Objective role |
|---|---|---|---|---|---|
| `larder_pantry` | 3x5 / 15 / 2 | closet | **Drip floor**: dry, shallow on the south two rows (0 / 40) | Smallest footprint in the pool, so wade only; no deep water can break the full-party minimum | none; breather room (Pack) |
| `larder_cold_store` | 3x5 / 15 / 2 (one lockable) | box | **Cold sump**: dry, flooded centre column pair (0 / 80), 2 deep cells | A box wide enough for 13 walkable cells even with 2 deep cells; a deep sump splits it into two lanes, which gives Guarded caster a partition | **valve room** (two-valve seal) |
| `larder_hanging_hall` | 4x4 / 14 / 2 | pillared hall | **Flooding sump** (pilot, already authored): dry, flooded (0 / 80), balcony reached by the stair | The only room with a stair and a pilot region; keep the pilot as the teaching room for the valve | **valve room**; the floor 1 teaching room |
| `larder_smoke_gallery` | 5x3 / 15 / 2 | long gallery | **Gallery wade**: dry, shallow along the south row, flooded a 2-cell channel in the middle row (0 / 40 / 80) | Long and shallow (3 deep): wade does not break the short side of 3; only a 2-cell channel is ever deep, leaving 13 walkable | none (key chest room; must stay clear of water) |
| `wine_cask_row` | 5x3 / 15 / 2 (one lockable) | box | **Cask flood**: dry, shallow floor between the racks (0 / 40) | Same long and shallow shape as the gallery; the lockable door means it must be full-party and never deep | **valve room** |
| `wine_vault_l` | 4x4 / 13 / 2 | L-shape | **Vault drip**: dry, shallow (0 / 40) on the foot of the L | Exactly 13 walkable cells, the minimum. Any deep cell would drop it below the full-party number, so it is wade only; key chest room | none |
| `wine_press_hall` | 5x4 / 16 / 3 | cross | **Press cistern**: dry, shallow, flooded (0 / 40 / 80) on the cross centre | Biggest cross: three states fit, the flooded centre makes four arms around a lake; three doors give a dry route in every state; key chest sits on a dry arm | **hold the pump** room |
| `wine_aisles` | 4x5 / 17 / 3 | aisles | **Aisle channels**: dry, shallow in the left aisle, flooded in the right aisle (0 / 40 / 80) | The solid wall column already splits it into two aisles; one aisle floods, the other stays dry, so there is always a fighting lane | **valve room** |
| `coal_bunker` | 3x5 / 15 / 2 (one lockable) | box | **Bunker seep**: dry, shallow (0 / 40) | Lockable, so full party and wade only | none |
| `coal_furnace_ring` | 4x4 / 14 / 2 | ring | **Ring moat**: dry, shallow (0 / 40) in the ring's outer cells | The ring is a loop around the solids; shallow keeps the loop walkable; key chest room | none |
| `coal_stoke_chicane` | 2x5 / 8 / 2 | chicane | **Chicane trickle**: dry, shallow (0 / 40) in the zigzag | 8 walkable cells and 2 heroes only; wade is the only state that cannot break the zigzag | none; two-hero tag stays |
| `coal_boiler_hall` | 5x4 / 17 / 3 | stepped | **Boiler basin**: dry, shallow, flooded (0 / 40 / 80) in the low step | Largest stepped room; the low step holds the water and the high step stays dry, three doors | **hold the pump** room |

Role summary (placeholder choices): valve rooms (two-valve seal) = cold store, hanging hall, cask row, aisles; pump rooms (hold the pump) = press hall, boiler hall. The generator draws two valve rooms per two-valve-seal floor and one pump room per hold-the-pump floor; if the layout has none, it re-rolls the objective (never silently skips it).

### 3.3 Which templates each room can draw
Cells walk in the worst (deepest) state; "Wants" is what the room offers.

| Room | Offers | Templates |
|---|---|---|
| `larder_pantry` | open, tiny | Pack |
| `larder_cold_store` | partition (deep sump), box | Guarded caster, Pack, Shield wall |
| `larder_hanging_hall` | pillars, balcony | Rush and bombard, Gel swarm, Guarded caster |
| `larder_smoke_gallery` | long lane | Pack, Shield wall, Ambush |
| `wine_cask_row` | racks | Pack, Shield wall, Gel swarm |
| `wine_vault_l` | corner | Shield wall, Rush and bombard |
| `wine_press_hall` | cross, large | any template; Rush and bombard, Gel swarm preferred |
| `wine_aisles` | partition (solid column) | Guarded caster, Shield wall, Rush and bombard, Ambush |
| `coal_bunker` | box | Pack, Gel swarm |
| `coal_furnace_ring` | loop with solids | Rush and bombard, Gel swarm |
| `coal_stoke_chicane` | 2-hero only | Pack (counts capped at 2 heroes' count: 3 / 4) |
| `coal_boiler_hall` | stepped, large | any template; Guarded caster preferred (the step is the dais) |

### 3.4 The two objectives on water
- **Two-valve seal.** Two valve Control sockets in two different valve rooms. The floor's stair door is a `ToolSeal` that opens when both valve regions are in their open state; each valve flips its own room's region only (a control acts on a region in its own room). On floor 1 the valve needs no tool; from floor 2 it needs the sluice key. No retry, no timer.
- **Hold the pump.** The pump socket sits on a dry cell of the pump room. While at least one hero stands within 2 cells `(placeholder)`, a 60 s `(placeholder)` hold progresses; it pauses when nobody is within and never resets. Each time the hold is half done, a second wave spawns from the template (waves two deep). Finishing drains the room's region and opens the stair door. Nothing is lost if the party leaves and comes back (no respawn, progress kept).
- Both objective rooms are the full-party rooms; neither is a lockable or key-chest room, so a locked floor can still draw its lockable and key rooms elsewhere.

## 4. Data fields each room would need (names only)
Per room (`RoomWaterData`, extends room data v2):
- `roomId`, `widthCells`, `depthCells`, `walkableCellCount`, `doorCount`, `lockable`, `keyChest`, `fullParty`, `maxHeroes`
- `waterRegionId`, `waterRegionCells`, `waterStateValuesCm`, `defaultWaterState`, `waterStateNames`
- `minWalkableCellsPerState`, `shortSideWalkablePerState`, `dryRouteBetweenDoors`
- `bubblingAllowed`, `bubblingCells`, `bubblingDamagePerSecond`, `bubblingFloorMin`, `bubblingFloorMax`
- `valveCapable`, `valveSocketId`, `valveTool`, `valveRegionId`, `valveOpenState`
- `pumpCapable`, `pumpSocketId`, `pumpHoldPoint`, `pumpHoldRadiusCells`, `pumpHoldSeconds`, `pumpDrainedState`
- `templateAllowList`, `spawnAnchorCells`, `lobberAnchorCells`, `shieldAnchorCells`, `ambushCellOk`, `entryDoorExclusionRadius`
- `dryCellsForSpawns` (computed: cells dry in every state)

Per template (`EncounterTemplate`):
- `templateId`, `name`, `roleCounts` (R, S, Sl, B), `countsByParty` (1 to 4), `liveCap`, `waveSizes`, `waveTrigger`, `minRoomCells`, `wants`, `floorMin`, `floorMax`, `maxPerFloor`, `ambushOnly`, `twist`

Per encounter instance (runtime, host):
- `roomIndex`, `templateId`, `partySize`, `state` (Unspawned / Live / Cleared), `spawnedCount`, `killedCount`, `waveIndex`

## 5. Open questions
1. **Tier-2 `S` cells.** Rooms such as `larder_smoke_gallery`, `wine_press_hall` and `coal_boiler_hall` author an `S` (Tier2) spawn. With tier-1 only in the first dungeon, do those cells hold a Tier-1 front-role enemy, or are they dropped? This proposal says Tier-1 front role.
2. **Where do the valves live on floors 2-5?** Floor 1 teaches with one authored valve. Is a valve room always dry-default (open to explore) or flooded-default (the player lowers the water to reach something)? Not decided here.
3. **Coal rooms in the water half.** The coal cellars are the heat half's theme (floors 6-8). Giving them water states makes them usable in floors 1-5; later they get heat states in a second region. Is that fine, or should floors 1-5 only draw the larder and wine rooms until heat rooms exist?
4. **Wade movement.** ROOM_METRICS says wade is walkable but not whether it slows the hero or enemies. This spec assumes no slow; add one only if a playtest wants it.
5. **Bubbling damage.** A damage tick on floors 4-5 wade cells is a new hazard. Value, and whether enemies are hurt too, need the user's call.
6. **Pump hold with no one near.** Pause or slowly keep going? Paused here so it never punishes a retreat.
7. **Count curve.** The counts are placeholder and rise about one enemy per extra player up to the live cap of 8; the existing `extraEnemiesPerRoom` curve and the square-root scaling in stir (decision 19) may want to drive them instead.
8. **Chicane.** Stays two-hero. If a floor needs it with three or four players, it should be resized, not removed.
9. **Floor-card and modifier interplay.** Not covered because that spec did not exist when this was written. A modifier that adds enemies must also obey the live cap and the entry exclusion.

Note (controller): the floor card and modifiers note now exists at `docs/superpowers/specs/2026-11-04-floor-card-and-modifiers.md`; reconcile modifier-to-room assignments there when plan 3 is written.
