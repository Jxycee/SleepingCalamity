State: planning (gate on fresh main running)
Branch: claude/dive-5b-water (from origin/main 366d0126)
Archive: not yet

## Plan 5b (2026-11-15)
Findings: the 12 pool combat rooms are `RoomDataV2.FromV1` wrappers (no regions); only the pilot hanging hall has a sump. The generator and spawner read v1 `CellarRoom`; v2 feeds tests, encounters and the greybox. `RoomValidatorV2` already loops every state vector (rules 6-9).
Conflict: DC7 (`dungeon1-cellars.md`, user 2026-11-13) = no standing water in Phase 1; spec section 5/7 = water gimmick floors 1-5. `dungeon1-cellars.md` wins: states are built and validated, switched OFF on dungeon 1.
1. `Core/Rooms/CellarWaterRooms.cs`: water-authored v2 variant of each of the 12 pool combat rooms: one sump region (letter w) with states {drained 0, wade 40 or flooded 80+}, a valve Control socket on a dry wall cell, stairs/levels only if the room needs a dry route. Same id/geometry as the pool room (shared CellarRoom object).
2. Every variant passes `RoomValidatorV2` over all state vectors (rules 1-9), fits-party and encounter-table checks keep passing; golden test lists each room's states and standable-cell counts per state.
3. `Core/Cellars/CellarWater.cs` (pure policy): `Enabled(dungeon, floor)` = false for dungeon 1 (all 7 floors), true for later dungeons floors 1-5; `Bubbling(dungeon, floor)` floors 4-5; `StartState(seed, floor, roomIndex)` deterministic via its own named stream; `BrimmingHoldSeconds` = 90 (spec), applied only when Enabled (dungeon 1: no-op, Brimming keeps its heal/loot effect).
4. Tests: EditMode for validator on all 12 variants, per-state reach, policy off on dungeon 1, determinism per seed, Brimming no-op. Soak (Core/Cellars touched) run before merge.
5. Out of scope, noted for the coordinator: runtime water mesh/replication and valve interaction wiring. Dungeon 1 has water off, so there is no networked state this step; a later dungeon must add a host-authoritative room-water sync (sc-netcode) when it turns water on. Presentation: `RoomGreybox` already draws water per state.
Review: `reviewer` (pure Core), then `sc-merge`.
