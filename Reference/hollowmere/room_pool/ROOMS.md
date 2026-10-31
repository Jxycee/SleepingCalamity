# Cellar room pool, Phase 1 (Q7)

Generated from `Assets/_Project/Core/Cellars/CellarRoomPool.cs`; the code is the source of truth. Format and legend: header of `CellarRoom.cs`. Row 0 is north. 4 m cells. All 21 rooms pass `CellarRoomPool.Validate()`.

Legend: `.` floor, `#` solid, `P` pillar, `D` door, `L` locked door, `s` tier-1 spawn, `S` tier-2 spawn, `C` chest slot, `K` key chest slot, `E` party start, `X` stair, `B` bank. Sockets are listed under each room (id: kind at cell, facing, variants).

| id | kind | theme | size | silhouette | doors | sockets | spawns | chests | lock / key |
|---|---|---|---|---|---|---|---|---|---|
| larder_pantry | Combat | Larder | 2x3 | closet | 2 | 1 | 2 | 1 | - |
| larder_cold_store | Combat | Larder | 3x4 | box | 2 | 4 | 2 | 1 | locked door |
| larder_hanging_hall | Combat | Larder | 4x4 | pillared-hall | 2 | 4 | 3 | 2 | - |
| larder_smoke_gallery | Combat | Larder | 5x3 | gallery | 2 | 4 | 3 | 2 | key chest |
| wine_cask_row | Combat | WineVault | 4x3 | box | 2 | 6 | 3 | 1 | locked door |
| wine_vault_l | Combat | WineVault | 4x4 | L-shape | 2 | 4 | 3 | 2 | key chest |
| wine_press_hall | Combat | WineVault | 5x4 | cross | 3 | 6 | 2 | 2 | key chest |
| wine_aisles | Combat | WineVault | 4x5 | aisles | 3 | 5 | 3 | 2 | - |
| coal_bunker | Combat | CoalCellar | 3x3 | box | 2 | 4 | 2 | 1 | locked door |
| coal_furnace_ring | Combat | CoalCellar | 4x4 | ring | 2 | 5 | 3 | 2 | key chest |
| coal_stoke_chicane | Combat | CoalCellar | 2x5 | chicane | 2 | 2 | 3 | 1 | - |
| coal_boiler_hall | Combat | CoalCellar | 5x4 | stepped | 3 | 5 | 2 | 2 | - |
| cellar_entry | Entry | Any | 4x4 | landing | 2 | 4 | 0 | 0 | - |
| cellar_exit | StairExit | Any | 4x4 | landing | 2 | 4 | 0 | 0 | - |
| cellar_checkpoint | Checkpoint | Any | 3x4 | nook | 2 | 5 | 0 | 1 | - |
| corridor_straight | Corridor | Any | 1x1 | - | 2 | 1 | 0 | 0 | - |
| corridor_corner | Corridor | Any | 1x1 | - | 2 | 1 | 0 | 0 | - |
| corridor_t | Corridor | Any | 1x1 | - | 3 | 1 | 0 | 0 | - |
| corridor_cross | Corridor | Any | 1x1 | - | 4 | 0 | 0 | 0 | - |
| corridor_dead_end | Corridor | Any | 1x1 | - | 1 | 1 | 0 | 0 | - |
| corridor_door | Corridor | Any | 1x1 | - | 2 | 1 | 0 | 0 | - |

## larder_pantry (Pantry closet)

Combat, Larder, 2x3 cells

```
sC
DD
.s
```

Doors: (0,1) West, (1,1) East

- larder_pantry.s1: Barrel at (0,2) facing North, variants Full/Stacked

## larder_cold_store (Cold store)

Combat, Larder, 3x4 cells

```
.D.
s..
C.s
.L.
```

Doors: (1,0) North, (1,3) South locked

- larder_cold_store.s1: Shelf at (0,0) facing South, variants Jars/Hams/Sacks
- larder_cold_store.s2: Shelf at (2,0) facing South, variants Jars/Hams/Sacks
- larder_cold_store.s3: Barrel at (0,3) facing North, variants Full/Stacked
- larder_cold_store.s4: Crate at (2,3) facing North, variants Stacked/Open/Broken

## larder_hanging_hall (Hanging hall)

Combat, Larder, 4x4 cells

```
..DC
.P.s
DsP.
..CS
```

Doors: (2,0) North, (0,2) West

- larder_hanging_hall.s1: Shelf at (0,0) facing South, variants Jars/Hams/Sacks
- larder_hanging_hall.s2: Shelf at (1,0) facing South, variants Jars/Hams/Sacks
- larder_hanging_hall.s3: Lantern at (0,3) facing East, variants Lit/Dim/Out
- larder_hanging_hall.s4: Roots at (1,3) facing North, variants Thin/Thick

## larder_smoke_gallery (Smoke gallery)

Combat, Larder, 5x3 cells

```
...K.
D.s.D
.s.SC
```

Doors: (0,1) West, (4,1) East

- larder_smoke_gallery.s1: Shelf at (0,0) facing South, variants Jars/Hams/Sacks
- larder_smoke_gallery.s2: Shelf at (2,0) facing South, variants Jars/Hams/Sacks
- larder_smoke_gallery.s3: Shelf at (4,0) facing South, variants Jars/Hams/Sacks
- larder_smoke_gallery.s4: Barrel at (0,2) facing North, variants Full/Stacked

## wine_cask_row (Cask row)

Combat, WineVault, 4x3 cells

```
CL..
.s.s
..DS
```

Doors: (1,0) North locked, (2,2) South

- wine_cask_row.s1: Rack at (2,0) facing South, variants Bottles/Casks/Empty
- wine_cask_row.s2: Rack at (3,0) facing South, variants Bottles/Casks/Empty
- wine_cask_row.s3: Barrel at (0,1) facing North, variants Full/Stacked
- wine_cask_row.s4: Barrel at (2,1) facing North, variants Full/Stacked
- wine_cask_row.s5: Barrel at (0,2) facing North, variants Full/Stacked
- wine_cask_row.s6: Lantern at (1,2) facing North, variants Lit/Dim/Out

## wine_vault_l (L vault)

Combat, WineVault, 4x4 cells

```
..##
D.##
s.sD
SCK.
```

Doors: (0,1) West, (3,2) East

- wine_vault_l.s1: Rack at (0,0) facing South, variants Bottles/Casks/Empty
- wine_vault_l.s2: Rack at (1,0) facing South, variants Bottles/Casks/Empty
- wine_vault_l.s3: Lantern at (1,1) facing West, variants Lit/Dim/Out
- wine_vault_l.s4: Crate at (3,3) facing North, variants Stacked/Open/Broken

## wine_press_hall (Press hall)

Combat, WineVault, 5x4 cells

```
#.D.#
..s..
D.S.D
#CK.#
```

Doors: (2,0) North, (0,2) West, (4,2) East

- wine_press_hall.s1: Rack at (1,0) facing South, variants Bottles/Casks/Empty
- wine_press_hall.s2: Rack at (3,0) facing South, variants Bottles/Casks/Empty
- wine_press_hall.s3: Barrel at (0,1) facing North, variants Full/Stacked
- wine_press_hall.s4: Barrel at (4,1) facing North, variants Full/Stacked
- wine_press_hall.s5: Barrel at (1,1) facing North, variants Full/Stacked
- wine_press_hall.s6: Lantern at (3,3) facing North, variants Lit/Dim/Out

## wine_aisles (Rack aisles)

Combat, WineVault, 4x5 cells

```
.D..
.#s.
D#.C
C#s.
S.D.
```

Doors: (1,0) North, (0,2) West, (2,4) South

- wine_aisles.s1: Rack at (3,0) facing South, variants Bottles/Casks/Empty
- wine_aisles.s2: Rack at (3,1) facing West, variants Bottles/Casks/Empty
- wine_aisles.s3: Rack at (3,3) facing West, variants Bottles/Casks/Empty
- wine_aisles.s4: Crate at (2,2) facing North, variants Stacked/Open/Broken
- wine_aisles.s5: Lantern at (3,4) facing North, variants Lit/Dim/Out

## coal_bunker (Coal bunker)

Combat, CoalCellar, 3x3 cells

```
.L.
D.s
SC.
```

Doors: (1,0) North locked, (0,1) West

- coal_bunker.s1: Chute at (0,0) facing South, variants Clear/Heaped
- coal_bunker.s2: Chute at (2,0) facing South, variants Clear/Heaped
- coal_bunker.s3: Crate at (1,1) facing North, variants Stacked/Open/Broken
- coal_bunker.s4: Lantern at (2,2) facing North, variants Lit/Dim/Out

## coal_furnace_ring (Furnace ring)

Combat, CoalCellar, 4x4 cells

```
..D.
.##s
s##.
CDKS
```

Doors: (2,0) North, (1,3) South

- coal_furnace_ring.s1: Chute at (0,0) facing South, variants Clear/Heaped
- coal_furnace_ring.s2: Chute at (1,0) facing South, variants Clear/Heaped
- coal_furnace_ring.s3: Lantern at (3,0) facing South, variants Lit/Dim/Out
- coal_furnace_ring.s4: Lantern at (0,1) facing East, variants Lit/Dim/Out
- coal_furnace_ring.s5: Crate at (3,2) facing North, variants Stacked/Open/Broken

## coal_stoke_chicane (Stoke chicane)

Combat, CoalCellar, 2x5 cells

```
sC
.#
DD
#.
Ss
```

Doors: (0,2) West, (1,2) East

- coal_stoke_chicane.s1: Lantern at (0,1) facing East, variants Lit/Dim/Out
- coal_stoke_chicane.s2: Chute at (1,3) facing West, variants Clear/Heaped

## coal_boiler_hall (Boiler hall)

Combat, CoalCellar, 5x4 cells

```
##D..
#..s.
D.S.D
C.C..
```

Doors: (2,0) North, (0,2) West, (4,2) East

- coal_boiler_hall.s1: Chute at (3,0) facing South, variants Clear/Heaped
- coal_boiler_hall.s2: Chute at (4,0) facing South, variants Clear/Heaped
- coal_boiler_hall.s3: Lantern at (1,1) facing East, variants Lit/Dim/Out
- coal_boiler_hall.s4: Crate at (4,1) facing North, variants Stacked/Open/Broken
- coal_boiler_hall.s5: Lantern at (1,3) facing North, variants Lit/Dim/Out

## cellar_entry (Stair landing)

Entry, Any, 4x4 cells

```
.D..
EE..
EE.D
....
```

Doors: (1,0) North, (3,2) East

- cellar_entry.s1: Lantern at (0,0) facing South, variants Lit/Dim/Out
- cellar_entry.s2: Lantern at (2,0) facing South, variants Lit/Dim/Out
- cellar_entry.s3: Barrel at (0,3) facing North, variants Full/Stacked
- cellar_entry.s4: Crate at (3,3) facing North, variants Stacked/Open/Broken

## cellar_exit (Stair hall)

StairExit, Any, 4x4 cells

```
.D..
....
D...
...X
```

Doors: (1,0) North, (0,2) West

- cellar_exit.s1: Lantern at (0,0) facing South, variants Lit/Dim/Out
- cellar_exit.s2: Lantern at (2,0) facing South, variants Lit/Dim/Out
- cellar_exit.s3: Crate at (0,3) facing North, variants Stacked/Open/Broken
- cellar_exit.s4: Barrel at (1,3) facing North, variants Full/Stacked

## cellar_checkpoint (Lamp nook)

Checkpoint, Any, 3x4 cells

```
.D.
.B.
..C
.D.
```

Doors: (1,0) North, (1,3) South

- cellar_checkpoint.s1: Lantern at (0,0) facing South, variants Lit/Dim/Out
- cellar_checkpoint.s2: Lantern at (2,0) facing South, variants Lit/Dim/Out
- cellar_checkpoint.s3: Shelf at (0,1) facing East, variants Jars/Hams/Sacks
- cellar_checkpoint.s4: Shelf at (2,1) facing West, variants Jars/Hams/Sacks
- cellar_checkpoint.s5: Crate at (0,2) facing North, variants Stacked/Open/Broken

## corridor_straight (Straight)

Corridor, Any, 1x1 cells, shape Straight, mask 5 (N1 E2 S4 W8)

Doors: (0,0) North, (0,0) South

- corridor_straight.s1: Lantern at (0,0) facing East, variants Lit/Dim/Out

## corridor_corner (Corner)

Corridor, Any, 1x1 cells, shape Corner, mask 3 (N1 E2 S4 W8)

Doors: (0,0) North, (0,0) East

- corridor_corner.s1: Lantern at (0,0) facing North, variants Lit/Dim/Out

## corridor_t (T junction)

Corridor, Any, 1x1 cells, shape T, mask 11 (N1 E2 S4 W8)

Doors: (0,0) North, (0,0) East, (0,0) West

- corridor_t.s1: Lantern at (0,0) facing North, variants Lit/Dim/Out

## corridor_cross (Crossing)

Corridor, Any, 1x1 cells, shape Cross, mask 15 (N1 E2 S4 W8)

Doors: (0,0) North, (0,0) East, (0,0) South, (0,0) West


## corridor_dead_end (Dead end)

Corridor, Any, 1x1 cells, shape DeadEnd, mask 1 (N1 E2 S4 W8)

Doors: (0,0) North

- corridor_dead_end.s1: Lantern at (0,0) facing North, variants Lit/Dim/Out

## corridor_door (Door piece)

Corridor, Any, 1x1 cells, shape Door, mask 5 (N1 E2 S4 W8)

Doors: (0,0) North, (0,0) South

- corridor_door.s1: Lantern at (0,0) facing East, variants Lit/Dim/Out

