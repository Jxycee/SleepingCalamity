# Room metrics for room authors

One page. Read it before you author a room. The table at the end is generated from `GameMetrics` and a test keeps it honest. The same numbers are laid out in the metrics gym scene (laid out by `MetricsGymLayout`, built by `MetricsGymBuilder`: ten lanes you can walk).

## The grid
- A room is a grid of 4 m cells. A floor is 14 x 14 cells.
- Walls are 3.6 m tall. A room's vault crown is 5.1 m, a corridor's 4.1 m.

## The hero
- Run is 7 m/s. A dodge covers 3.5 m.
- A run-jump covers about 3.6 m. A 3 m gap can be jumped. A one-cell (4 m) pit never can.
- Boots must not change this. If a boot changes the jump, change the room rules first.
- The hero has no crouch, vault or swim, and no code deals fall damage, so this plan treats a drop of any height as free.

## Doors
- A plain door is 2.4 m wide and 2.8 m tall. A wide arch is 3.6 m wide.
- Doors stay on level 0.

## Height
- One level is 2 m. A level up is a wall to a jumping hero, so it needs a stair. The highest ledge a running jump climbs is 1.5 m, measured with the real controller (`HeroClimbMetricsTests`; the table's 1.6 m is the jump-plus-step bound).
- A stair is one cell: 10 steps, each 20 cm up and 40 cm deep.
- A balcony is a lobber's dais. Reach it by the stair, or leave it for the gimmick.
- The validator allows levels 0 and 1 for now.

## Water and heat
- A water region has a surface height in cm. Depth is the surface minus the cell floor.
- Up to 50 cm is a wade: walkable. Deeper blocks walking. There is no swimming.
- Heat is off (0) or burning (1). Burning cells block walking.

## Gimmick rules
- A control acts on a region in its own room.
- Every state of a region must change something.
- No softlock from any state: a party that left the room in the other state must still get out.
- Chests, keys, doors, spawns and features may not go under water or fire in any state.
- A hero on a cell that floods or ignites will be moved to the nearest standable cell (a runtime rule for the new dive, not built yet). The validator already makes sure one is next to every such cell.
- A room file with a version this build does not know is rejected.

## Fight space
- A full party (4 heroes, up to 8 enemies) needs 13 floor cells and a short side of 3 cells. The smallest room is 3 x 5 cells.
- Five pool rooms fit a full party: Hanging hall, Smoke gallery, Press hall, Rack aisles and Boiler hall. The rest are solo or duo rooms.
- This is a computed tag (`FullParty`), not an error. A small room is legal.

## Room data v2
A v2 room wraps the old `CellarRoom` and adds level rows, gimmick regions (water or heat, each with its state values), stair cells, tool sockets and typed doors. The schema version is 2. `RoomDataV2.FromV1` upgrades an old room with no extras. `RoomValidatorV2` checks every reachable (cell, state) pair. The pilot is `PilotRoomsV2.HangingHall`: two levels and two water states. The floor generator still reads the old rooms.

## The gym
The gym scene is `Assets/_Project/Scenes/MetricsGym.unity` (built by `MetricsGymBuilder`; not in Build Settings). Walk it to feel a number before you use it.

## The table
<!-- metrics:begin -->
| Key | Value | Unit | Source |
|---|---|---|---|
| walk_speed | 2.2 | m/s | Core/Player/MovementMath.cs:13-23 |
| jog_speed | 4.8 | m/s | Core/Player/MovementMath.cs:13-23 |
| run_speed | 7 | m/s | Core/Player/MovementMath.cs:13-23 |
| capsule_height | 1.8 | m | Core/Player/MovementMath.cs:13-23 |
| capsule_radius | 0.35 | m | Core/Player/MovementMath.cs:13-23 |
| step_offset | 0.4 | m | Core/Player/MovementMath.cs:13-23 |
| slope_limit | 45 | deg | Core/Player/MovementMath.cs:13-23 |
| jump_height | 1.2 | m | Core/Player/MovementMath.cs:13-23 |
| dodge_speed | 7 | m/s | Core/Player/MovementMath.cs:13-23 |
| dodge_duration | 0.5 | s | Core/Player/MovementMath.cs:13-23 |
| camera_pivot_height | 1.5 | m | Core/Player/MovementMath.cs:13-23 |
| dodge_distance | 3.5 | m | MovementProbe |
| run_jump_distance | 3.58 | m | MovementProbe |
| jump_apex | 1.2 | m | MovementProbe |
| max_climbable_ledge | 1.6 | m | design (plan 4) |
| camera_free_boom | 5 | m | Presentation/Hero/HeroCameraRig.cs:17 |
| camera_lock_boom | 6.5 | m | Presentation/Hero/HeroCameraRig.cs:17 |
| camera_fov | 50 | deg | Presentation/Hero/HeroCameraRig.cs:17 |
| camera_pitch | 15 | deg | Gameplay/Player/HeroLocomotion.cs:17 |
| cell_size | 4 | m | Core/Cellars/CellarFloorLayout.cs:68 |
| door_width | 2.4 | m | Presentation/Cellars/CellarFloorBuilder.cs:65 |
| door_height | 2.8 | m | Presentation/Cellars/CellarFloorBuilder.cs:65 |
| wall_height | 3.6 | m | Presentation/Cellars/CellarFloorBuilder.cs:65 |
| room_vault_crown | 5.1 | m | Presentation/Cellars/CellarFloorBuilder.Art.cs:51,84 |
| corridor_vault_crown | 4.1 | m | Presentation/Cellars/CellarFloorBuilder.Art.cs:51,84 |
| level_height | 200 | cm | design (plan 4) |
| stair_rise | 20 | cm | design (plan 4) |
| stair_run | 40 | cm | design (plan 4) |
| steps_per_level | 10 | steps | design (plan 4) |
| wade_max | 50 | cm | design (plan 4) |
| max_live_enemies | 8 | enemies | design (plan 4) |
| full_party_heroes | 4 | heroes | design (plan 4) |
| max_validated_level | 1 | level | design (plan 4) |
| wide_arch_width | 3.6 | m | design (plan 4) |
<!-- metrics:end -->
