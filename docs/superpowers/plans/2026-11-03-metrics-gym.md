# Metrics Gym and Room Data v2: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lock the game's real measurements (hero, camera, doors, jumps, stairs, water, fight space) in Core and in one builder-made gym scene, then freeze the room data v2 contract with a state-aware validator and one converted pilot room, before any new room is authored.

**Architecture:** Everything new lives in `Assets/_Project/Core/Rooms/` (pure C#, one class per file) so EditMode tests and `Tools/cloud/sc_core_tests.py` cover it. `GameMetrics` reads the real constants (`MovementMath`, `EnemyModel`, `CellarFloorLayout`) and mirrors the few that live in Presentation, pinned by a test. `MovementProbe` simulates the hero's dodge and run-jump at 60 Hz with the same `MovementMath` calls `HeroLocomotion` makes. Room data v2 wraps a v1 `CellarRoom` (unchanged) with layers; the floor generator keeps reading v1, so nothing under `Core/Cellars/` changes and no soak is needed. The gym is laid out as pure data (`MetricsGymLayout`) and instantiated by one Editor builder wired into `ProjectBuilder.RunAll`.

**Tech Stack:** C# 9 (Unity 6000.6.5f1), NUnit 3, Unity CharacterController (one PlayMode test), editor builders (`BuilderUtil`, `MaterialLibrary`).

**Spec:** `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` (sections 3.2, 5-7, 11.5 F7) and roadmap row 4 in `docs/superpowers/plans/2026-11-03-dive-build-roadmap.md`. Research: `docs/research/dungeon-deep-dive/10-red-team.md` (F7, section 3 "Room format", move 3), `08-encounters-arenas-bosses-audio.md` section 2 (arena geometry).

## Global Constraints
- Core is pure C#: no `UnityEngine`. Namespace `SleepingCalamity.Core`; tests in `SleepingCalamity.Tests.EditMode`.
- Core-only test files use only `System*`, `NUnit*` and `SleepingCalamity.Core` (so `python Tools/cloud/sc_core_tests.py` runs them). A test that needs Presentation or Editor goes in its own file.
- Every new file starts with a one-line header comment: its path, what it does, and the spec section it serves (match `CellarRoom.cs`'s style).
- Values marked *placeholder* are `public const` tuning starting points.
- Don't touch: anything under `Assets/_Project/Core/Cellars/` (floor generation reads it), plan 1's files (`UndercroftPlan`, `DiveCheckpoint`, `BankSlots`, `LostSatchel`, `DiveMomentum`, `DiveStir`, `PieceDepths`, `BreadLadder` and their tests), `DiveRoute`, `DiveState`, `StirMeter`, `DiveHaul`.
- Scenes only through editor builders wired into `ProjectBuilder.RunAll`; never hand-edit `.unity`/`.prefab`. Builders must be deterministic (same bytes on two runs).
- Unity only through `C:\Dev\SleepingCalamity\.claude\worktrees\q12-town-look-5967e9\Tools\unity.ps1 <cmd> -OutDir <scratchpad>`. `.meta` files come from a Unity run (Task 7), never hand-written.
- Branch `claude/metrics-gym-aac07e`. Commit after each task.

Core test command: `python Tools/cloud/sc_core_tests.py --where "class =~ <TestClass>"` (needs .NET 8; if it fails on this PC's .NET 10, use `Tools/unity.ps1 test-edit` instead and don't debug the runner).

## Measured facts this plan builds on (scan of 2026-11-03)
- Hero (`Core/Player/MovementMath.cs:13-23`): walk 2.2, jog 4.8, run 7.0 m/s; ground accel 40, air accel 6; gravity -22; jump height 1.2 m; dodge 7.0 m/s for 0.5 s, applied as an instant constant velocity (`HeroLocomotion.cs:141-147`); capsule h 1.8, r 0.35, step 0.4, slope 45°, camera pivot 1.5. Sprint needs `grounded` (`HeroLocomotion.cs:136`), so in the air the target speed falls to jog and a run-jump slows: a run-jump is about 3.6 m, less than one 4 m cell. No fall damage, no vault, no crouch.
- Camera (`Presentation/Hero/HeroCameraRig.cs:17`, `HeroLocomotion.cs:17`): boom 5.0 free / 6.5 lock, FOV 50, pitch 15° default (2-55).
- Cellars (`Core/Cellars/CellarFloorLayout.cs:68`, `Presentation/Cellars/CellarFloorBuilder.cs:65`, `CellarFloorBuilder.Art.cs:51`): 14x14 grid of 4 m cells; walls 3.6, doors 2.4 x 2.8; vault crown 5.1 (room) / 4.1 (corridor).
- Enemies (`Core/Combat/EnemyModel.cs:97-108`): radii 0.5-1.0; rat reach 1.45 (`AttackCatalog.cs:104`); aggro 12, leash 20 (`ThreatTable.cs:16`). Spec section 7: at most 8 live enemies with four players.
- No cellar stair, ledge or water-depth constants exist yet: this plan defines them.

## Decisions this plan makes (from the measurements)
- **Height level = 200 cm.** One level up is a clear barrier (above the 1.6 m jump-plus-step bound, confirmed by the Task 8 PlayMode test) and needs a stair; one level down is a free drop (no fall damage). A stair is one cell: 10 steps of 20 cm rise and 40 cm run (26.6°, under the 45° slope limit and the 0.4 m step offset). The validator allows levels 0-1 for now; the contract stores any level.
- **Water:** a region's state value is its surface height in cm above level 0. Depth = surface − cell floor. Depth ≤ 0 dry, 1-50 cm wade (walkable), over 50 cm deep (blocks walking; no swimming).
- **Heat:** state value 0 = off, 1 = burning (the region's cells block walking).
- **A hero on a cell that turns deep or burning is moved to the nearest standable cell** (runtime rule for plan 3+); the validator guarantees one exists next to every such cell in every state.
- **Fight capacity is a computed property, not a validation error.** Current small rooms stay legal; the doc lists which fit a full party.
- **Doors stay on level 0 and connect to the generator as today.** The v2 door type is carried but the generator ignores it until plan 3; the seam rule (both sides take the stricter type) is plan 3's.
- **Tool verbs frozen now** (spec 3.2; names are placeholders): `Sluice, Bellows, Blast, Smoke, Star, Tide`.

## Review Focus
1. A hero standing in a region when it floods or ignites: every deep-capable or burnable cell needs a standable neighbour in every state (Task 4 test `DeepCell_WithNoStandableNeighbour_IsAnError`).
2. A player who left the room in the "wrong" state (co-op: someone else pulled the valve): validation from every reachable (cell, state) pair, not only the start state (Task 4 test `BalconyTrap_IsASoftlock`).
3. A chest, key, door, spawn or feature that goes under water or fire in some state (Task 4 test `ChestThatGoesDeep_IsAnError`).
4. A room file from a newer build (version 3) loaded by this build: rejected, not misread (Task 4 test `UnknownVersion_IsRejected`).
5. Metrics drifting from the code they mirror (Task 1 pin test, Task 6 doc drift test).

---

### Task 1: GameMetrics and the movement probe

**Files:**
- Create: `Assets/_Project/Core/Rooms/MovementProbe.cs`, `Assets/_Project/Core/Rooms/GameMetrics.cs`
- Test: `Assets/_Project/Tests/EditMode/GameMetricsTests.cs` (Core-only), `Assets/_Project/Tests/EditMode/GameMetricsPinTests.cs` (references Presentation)

**Interfaces:**
- Produces: `static class MovementProbe { const float Hz = 60f; float DodgeDistance(); float JumpApex(); float RunJumpDistance(float runSpeed = MovementMath.RunSpeed); }`
- Produces: `readonly struct MetricRow { string Key; float Value; string Unit; string Source; }` and `static class GameMetrics` with:
  - mirrors: `CameraFreeBoom = 5.0f, CameraLockBoom = 6.5f, CameraFov = 50f, CameraPitchDeg = 15f, DoorWidth = 2.4f, DoorHeight = 2.8f, WallHeight = 3.6f, RoomVaultCrown = 5.1f, CorridorVaultCrown = 4.1f`
  - design values: `const int LevelHeightCm = 200, StairRiseCm = 20, StairRunCm = 40, WadeMaxCm = 50, MaxLiveEnemies = 8, FullPartyHeroes = 4, MaxValidatedLevel = 1`; `const float WideArchWidth = 3.6f` (*placeholder*); `int StepsPerLevel => LevelHeightCm / StairRiseCm` (10); `float CellMeters => CellarFloorLayout.CellMeters`; `float MaxClimbableLedge => MovementMath.JumpHeight + MovementMath.StepOffset` (1.6, the analytic bound Task 8 measures)
  - `IReadOnlyList<MetricRow> Rows()`: one row per value above plus the hero values read from `MovementMath` (walk, jog, run, capsule h/r, step, slope, jump height, dodge speed/duration, pivot) and the probe results (`dodge_distance`, `run_jump_distance`, `jump_apex`). Keys are snake_case and unique. Source is `file:line` or `"MovementProbe"`/`"design (plan 4)"`.
  - `string MarkdownTable()`: `| Key | Value | Unit | Source |` header plus one line per row, values formatted `0.##` with invariant culture.

- [ ] **Step 1: Write the failing Core test** `GameMetricsTests`:

```csharp
[Test] public void Dodge_IsConstantSpeedForItsDuration() =>
    Assert.That(MovementProbe.DodgeDistance(), Is.EqualTo(MovementMath.DodgeSpeed * MovementMath.DodgeDuration).Within(0.15f));
[Test] public void JumpApex_MatchesJumpHeight() =>
    Assert.That(MovementProbe.JumpApex(), Is.EqualTo(MovementMath.JumpHeight).Within(0.05f));
[Test] public void RunJump_IsShorterThanOneCell()
{
    float d = MovementProbe.RunJumpDistance();
    Assert.That(d, Is.InRange(3.0f, 4.0f));            // sprint drops in the air: about 3.6 m
    Assert.That(d, Is.LessThan(GameMetrics.CellMeters));
}
[Test] public void Boots_RaiseRunJumpByLessThanHalfAMetre() =>   // spec 5: +5% run speed must not open new gaps
    Assert.That(MovementProbe.RunJumpDistance(MovementMath.RunSpeed * 1.05f) - MovementProbe.RunJumpDistance(), Is.InRange(0f, 0.5f));
[Test] public void OneLevel_IsAboveTheClimbBound() =>
    Assert.That(GameMetrics.LevelHeightCm / 100f, Is.GreaterThan(GameMetrics.MaxClimbableLedge + 0.3f));
[Test] public void Stair_FitsTheController()
{
    Assert.That(GameMetrics.StairRiseCm / 100f, Is.LessThanOrEqualTo(MovementMath.StepOffset));
    Assert.That(Math.Atan2(GameMetrics.StairRiseCm, GameMetrics.StairRunCm) * 180 / Math.PI, Is.LessThan(MovementMath.SlopeLimit));
    Assert.That(GameMetrics.StepsPerLevel * GameMetrics.StairRunCm, Is.LessThanOrEqualTo(GameMetrics.CellMeters * 100));
}
[Test] public void Rows_HaveUniqueKeysAndSources() { /* keys unique, non-empty Unit and Source, contains "dodge_distance", "run_jump_distance", "level_height" */ }
```

- [ ] **Step 2: Run** `python Tools/cloud/sc_core_tests.py --where "class =~ GameMetricsTests"`. Expected: compile failure (types missing).
- [ ] **Step 3: Implement `MovementProbe`.** Fixed step `dt = 1/Hz`. Dodge: a `DodgeState` (`Core/Player/DodgeState.cs`), `TryStart(0,1)`, then per step `Tick(dt)` and add `DodgeSpeed * dt` while `Active` (the order `HeroLocomotion.Tick` uses: tick, then move). Jump: `vy = JumpVelocity(JumpHeight, Gravity)`, `y += VerticalStep(ref vy, Gravity, TerminalVelocity, dt)` until `y <= 0`; apex = max y. Run-jump: start `vx = runSpeed`; each step in the air `Accelerate(ref vx, ref vz, JogSpeed, 0, AirAccel, dt)` (sprint needs ground) and `x += vx * dt`; stop when y returns to 0.
- [ ] **Step 4: Implement `GameMetrics`** with the interface above.
- [ ] **Step 5: Write `GameMetricsPinTests`** (own file): each mirror equals its source: `HeroCameraRig.FreeBoom/LockBoom/Fov`, `CellarFloorBuilder.DoorWidth/DoorHeight/WallHeight`, `CellarFloorBuilder.RoomSpring + VaultRise` = 5.1 and `CorridorSpring + VaultRise` = 4.1 (check `CellarFloorBuilder.Art.cs:84` for the real crown formula and use it). Read the camera pitch default from `HeroLocomotion.cs:17`; if it is private, pin it with a comment naming the line instead.
- [ ] **Step 6: Run** the Core test (PASS); the pin test runs in Task 7's Unity run.
- [ ] **Step 7: Commit** `feat(core): GameMetrics and MovementProbe (plan 4 task 1)`.

---

### Task 2: Fight space

**Files:**
- Create: `Assets/_Project/Core/Rooms/FightSpace.cs`
- Test: `Assets/_Project/Tests/EditMode/FightSpaceTests.cs`

**Interfaces:**
- Consumes: `MovementProbe.DodgeDistance()`, `MovementMath.CapsuleRadius`, `GameMetrics.CellMeters`.
- Produces: `static class FightSpace { const float EnemyBubbleRadius = 2.0f /* placeholder: tier-1 reach 1.45 + body 0.55 */, UsableFloorFraction = 0.8f; float HeroBubbleRadius { get; } /* dodge/2 + capsule = 2.1 */; int MinShortSide(int heroes) /* 3 when heroes >= 3, else 2 */; int RequiredFloorCells(int heroes, int enemies); bool Fits(CellarRoom room, int heroes, int enemies); (int w, int h) SmallestRectangle(int heroes, int enemies) }`
- `RequiredFloorCells = ceil((heroes·π·HeroBubbleRadius² + enemies·π·EnemyBubbleRadius²) / (CellMeters² · UsableFloorFraction))`. `Fits` = `room.FloorCount >= Required && min(W,H) >= MinShortSide`. `SmallestRectangle` = smallest area with `w <= h`, `w >= MinShortSide`, `w·h >= Required` (ties: smaller h).

- [ ] **Step 1: Failing tests:**

```csharp
[Test] public void FullParty_NeedsThirteenCells() => Assert.That(FightSpace.RequiredFloorCells(4, 8), Is.EqualTo(13));
[Test] public void FullParty_SmallestRoomIs3x5() => Assert.That(FightSpace.SmallestRectangle(4, 8), Is.EqualTo((3, 5)));
[Test] public void Solo_SmallestRoomIs2x3() => Assert.That(FightSpace.SmallestRectangle(1, 3), Is.EqualTo((2, 3)));
[Test] public void PoolRoomsThatFitAFullParty()
{
    var fit = CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat && FightSpace.Fits(r, 4, 8)).Select(r => r.Id).OrderBy(s => s);
    Assert.That(fit, Is.EqualTo(new[] { "coal_boiler_hall", "larder_hanging_hall", "larder_smoke_gallery", "wine_aisles", "wine_press_hall" }));
}
[Test] public void EveryPoolCombatRoom_FitsSolo() => /* all 12 combat rooms Fits(r, 1, 3) */;
```
(If the pinned list differs, recount `FloorCount` by hand from `CellarRoomPool.cs` before changing the test; report the difference.)

- [ ] **Step 2: Run** `--where "class =~ FightSpaceTests"`, expect FAIL. **Step 3: Implement.** **Step 4: Run**, expect PASS.
- [ ] **Step 5: Commit** `feat(core): FightSpace capacity rule (plan 4 task 2)`.

---

### Task 3: Room data v2 contract

**Files:**
- Create: `Assets/_Project/Core/Rooms/RoomDataV2.cs` (the room record and its parts), `Assets/_Project/Core/Rooms/EnemyHabitat.cs`
- Test: `Assets/_Project/Tests/EditMode/RoomDataV2Tests.cs`

**Interfaces:**
- Consumes: `CellarRoom`, `CellPos`, `CellSide`, `FightSpace.Fits`, `GameMetrics`.
- Produces:
  - `enum DoorType : byte { Plain, WideArch, Gate, Locked, ToolSeal, Secret }` (no crawl: the hero has no crouch)
  - `enum ToolVerb : byte { None, Sluice, Bellows, Blast, Smoke, Star, Tide }`
  - `enum GimmickKind : byte { Water, Heat }`; `enum SocketPurpose : byte { Control, Alcove }`
  - `[Flags] enum RoomAffordance { None = 0, OpenFloor = 1, Water = 2, Ledge = 4, HighCeiling = 8, FullParty = 16 }`
  - `sealed class DoorV2 { int DoorIndex /* into Base.Doors */; DoorType Type; int Level; ToolVerb Tool; string GateRegion; int GateOpenState; }`
  - `sealed class GimmickRegion { string Id; char Letter; GimmickKind Kind; IReadOnlyList<int> StateValues; int StateCount => StateValues.Count; IReadOnlyList<CellPos> Cells /* filled by Author */; }`
  - `sealed class StairCell { int X, Y; CellSide Up; }` (the cell is at its own level; the neighbour on `Up` is one level higher)
  - `sealed class ToolSocket { string Id; int X, Y; CellSide Facing; SocketPurpose Purpose; ToolVerb Tool; string Region; }` (a Control cycles its region to `(state + 1) % StateCount`)
  - `sealed class RoomDataV2 { const int SchemaVersion = 2; int Version; CellarRoom Base; IReadOnlyList<DoorV2> Doors /* one per Base.Doors entry */; IReadOnlyList<GimmickRegion> Regions; IReadOnlyList<StairCell> Stairs; IReadOnlyList<ToolSocket> Sockets; RoomAffordance AuthoredTags; int LevelAt(int x, int y); GimmickRegion RegionAt(int x, int y) /* null if none */; int DepthCm(int x, int y, IReadOnlyList<int> state) /* water: surface - level·LevelHeightCm, else 0 */; bool Burning(int x, int y, IReadOnlyList<int> state); bool Standable(int x, int y, IReadOnlyList<int> state) /* Floor, depth <= WadeMaxCm, not burning */; RoomAffordance Affordances { get; } /* derived | AuthoredTags */ }`
  - `static RoomDataV2 FromV1(CellarRoom room)`: version 2, all levels 0, doors `Locked` where `CellDoor.Locked` else `Plain`, level 0, no regions, stairs or sockets.
  - `static RoomDataV2 Author(CellarRoom baseRoom, string[] levelRows, string[] regionRows, GimmickRegion[] regions, StairCell[] stairs, ToolSocket[] sockets, DoorV2[] doorOverrides, RoomAffordance authoredTags, int version = SchemaVersion)`: `levelRows` one digit per cell, row 0 north (null = all 0); `regionRows` a region's `Letter` or `.` per cell (null = none); `doorOverrides` replace the `FromV1` door with the same `DoorIndex`. Throws `ArgumentException` on a malformed grid (wrong row count or width, unknown letter or character); rule checks belong to the validator.
  - Derived affordances: `Water` if any Water region; `Ledge` if any level > 0; `FullParty` if `FightSpace.Fits(Base, 4, 8)`; `OpenFloor` if some 3x3 window is all Floor at one level with no region. `HighCeiling` is authored only.
  - `static class EnemyHabitat { RoomAffordance Needs(EnemyKind kind); bool Fits(EnemyKind kind, RoomDataV2 room) }`: Slime, RatKing, Broom need `None`; DoughWarden needs `OpenFloor`. (The Brazen Ox rename is a later plan's; use the current enum.)

- [ ] **Step 1: Failing tests:** `FromV1_KeepsBaseAndMarksTheLockedDoor` (cold store: its `L` door → `DoorType.Locked`, others `Plain`, all levels 0, `Version == 2`); `Author_ParsesLevelsAndRegions` (a hand-built 3x3 base with a level row `"001"`, a region `w` over two cells with values `{0, 80}`: `LevelAt`, `RegionAt`, `DepthCm` at state `{1}` = 80 on level 0, 0 on a level-1 cell under the same surface would be `80-200 <= 0`); `Standable_WadeVsDeep` (50 cm standable, 51 not); `Heat_BurningBlocks`; `Author_ThrowsOnBadGrid` (short row, unknown letter); `Affordances_AreDerived` (Water/Ledge from the hand room; `FullParty` true for `wine_press_hall` via `FromV1`, false for `larder_pantry`); `ToolVerbs_AreFrozen` (`Enum.GetNames(typeof(ToolVerb))` equals the list above, so adding a verb is a deliberate test change); `EnemyHabitat_WardenNeedsOpenFloor`.
- [ ] **Step 2: Run** `--where "class =~ RoomDataV2Tests"`, FAIL. **Step 3: Implement.** **Step 4: Run**, PASS.
- [ ] **Step 5: Commit** `feat(core): room data v2 contract (plan 4 task 3)`.

---

### Task 4: State-aware validator

**Files:**
- Create: `Assets/_Project/Core/Rooms/RoomValidatorV2.cs`
- Test: `Assets/_Project/Tests/EditMode/RoomValidatorV2Tests.cs`

**Interfaces:**
- Consumes: Task 3 types, `CellarRoom.Validate()`, `CellarRoom.Step`, `GameMetrics.MaxValidatedLevel`.
- Produces: `static class RoomValidatorV2 { List<string> Validate(RoomDataV2 room, IReadOnlyCollection<ToolVerb> partyTools = null); List<string> ValidateAll(IEnumerable<RoomDataV2> rooms); int StateCount(RoomDataV2 room) /* product of region state counts */ }`. Messages start with the room id, like `CellarRoom.Validate`.

Rules, in order (stop after 1 or 2 fail):
1. `Version == SchemaVersion`, else `"<id>: room data version N is not supported (this build reads 2)"`.
2. `Base.Validate()` errors pass through; `Doors.Count == Base.Doors.Count` and each `DoorIndex` appears once.
3. Shape: levels in 0..`MaxValidatedLevel`; every door on level 0; a Gate names an existing region and a state in range; a ToolSeal names a tool other than `None`; region ids unique, 2..4 states each, a Heat region's values are 0/1; a Water value is in 0..`LevelHeightCm·(MaxValidatedLevel+1)`.
4. Stairs: on a Floor cell, the `Up` neighbour is in bounds, Floor and exactly one level higher; the stair cell carries no door, spawn, chest, feature, dressing socket, tool socket or chunk region.
5. Tool sockets: ids unique; on a Floor cell with no other mark (as rule 4); a Control names a region in this room (F7 b); a wall-mounted socket's back is the room edge, a Solid or a Pillar (the `DressingSocket` rule).
6. Marks never blocked: every door, chest, spawn, feature and tool-socket cell is standable in every state vector (no key under water, no spawn in fire).
7. Escape: every cell that is not standable in some state has an orthogonal neighbour that is standable in that state (the "move to the nearest standable cell" rule has a target).
8. Each region matters: for each region there are two of its states, with the other regions fixed, that differ in the standable set (one room plays two ways, spec 7).
9. No softlock (F7 c). Search the graph of nodes (cell, state vector). Edges: walk to an orthogonal neighbour when both are standable in the state and either the levels match, the move goes down any number of levels (a drop), or it climbs one level from a stair cell across its `Up` side (and back down the same way); at a Control socket's cell whose `Tool` is `None` or in `partyTools`, step to the state with that region cycled, keeping the cell if it stays standable (otherwise this edge does not exist). Barrels and crates (`DressingSocket` kinds Barrel, Crate) block their cells as in v1. Starting nodes: every door cell in every state vector where it is standable. Requirement: from every node reachable from any start, every door, chest, feature and Control cell is reachable in some state; and every Gate door is reachable in a state where its region is in `GateOpenState`. Report the first failing (cell, state, target) as `"<id>: softlock: from (x,y) in state [a,b] the door (x,y N) cannot be reached"`.

- [ ] **Step 1: Failing tests** (build small rooms with `CellarRoom.Parse` + `RoomDataV2.Author`):
  - `EveryPoolRoom_UpgradesAndValidates`: `ValidateAll(CellarRoomPool.All.Select(RoomDataV2.FromV1))` is empty, and the same for `CellarBakeryPool` rooms.
  - `UnknownVersion_IsRejected`: `Author(..., version: 3)` → one error containing `"version 3"`.
  - `DoorAboveLevelZero_IsAnError`, `LevelTwo_IsNotValidatedYet`.
  - `StairOnAChest_IsAnError`, `StairWithoutAHigherNeighbour_IsAnError`.
  - `ControlForAnotherRoomsRegion_IsAnError`.
  - `ChestThatGoesDeep_IsAnError` (chest in a Water region with values `{0, 80}`).
  - `DeepCell_WithNoStandableNeighbour_IsAnError` (a region cell walled in by pillars and other region cells).
  - `RegionThatChangesNothing_IsAnError` (values `{0, 30}`: wade both ways).
  - `BalconyTrap_IsASoftlock`: a level-1 balcony reachable only by a stair, the only Control on the balcony, the stair cell in a Water region `{0, 260}`: from a door in the flooded state the Control can't be reached, so the validator reports `softlock`.
  - `ToolControl_NeedsTheTool`: the same room valid with the control `Tool = Sluice` only when `partyTools` holds `Sluice`.
- [ ] **Step 2: Run** `--where "class =~ RoomValidatorV2Tests"`, FAIL. **Step 3: Implement** (breadth-first search; state vectors are small: at most 4 regions of 4 states). **Step 4: Run**, PASS.
- [ ] **Step 5: Commit** `feat(core): state-aware room validator v2 (plan 4 task 4)`.

---

### Task 5: The pilot room (Hanging hall, v2)

**Files:**
- Create: `Assets/_Project/Core/Rooms/PilotRoomsV2.cs`
- Test: `Assets/_Project/Tests/EditMode/PilotRoomsV2Tests.cs`

**Interfaces:**
- Consumes: Tasks 3-4.
- Produces: `static class PilotRoomsV2 { RoomDataV2 HangingHall { get; } IReadOnlyList<RoomDataV2> All { get; } }`.

The pilot re-authors `larder_hanging_hall` (same id, name, kind, theme, silhouette, size 4x4, door cells N (2,0) and W (0,2), 2 chests, 2 pillars, spawns s s S). The pool's own copy stays untouched for the generator; the pilot moves one pillar and one chest so the stair and the water fit, and drops the chunk region (red-team: every authored room is migrated, so the pilot shows what a migration costs). Grid (x east, y south):

```
v1 rows      levels   regions
"..DC"       "0001"   "...."
".P.s"       "0001"   "..w."
"Ds.."       "0001"   "..w."
".CPS"       "0001"   "...."
```
- Balcony: column x = 3 at level 1 (2 m): chest (3,0), spawns (3,1) s and (3,3) S. The lobber dais the research asks for.
- Stair: (2,2), `Up = East`, to (3,2).
- Region `w` "sump", `GimmickKind.Water`, cells (2,1) and (2,2), states `{0, 80}`: drained (A) or flooded 80 cm (B). Flooded drowns the stair, so the balcony and its chest are reachable only in A; the N door still links to the W door around the west side in both states.
- Tool socket `pilot_hall.valve`: Control, `ToolVerb.None`, region `w`, cell (0,3), facing East (back on the west edge).
- Dressing sockets (`SocketSpec`, the pool's variant names and fill chances): Shelf (0,0) S, Shelf (1,0) S, Roots (0,1) E, Lantern (3,2) W.
- Authored tags: none (Water, Ledge are derived).

- [ ] **Step 1: Failing tests:** `Pilot_Validates` (`RoomValidatorV2.Validate(PilotRoomsV2.HangingHall)` empty); `Pilot_KeepsThePoolRoomsSeams` (id, kind, theme, silhouette, size and door cells equal `CellarRoomPool.Find("larder_hanging_hall")`'s); `Pilot_IsMultiLevelAndTwoState` (`Affordances` has `Water | Ledge`; `StateCount == 2`); `Pilot_FloodCutsOffTheBalcony` (in state `{1}` `(2,2)` is not standable; in `{0}` it is); `Pilot_FitsAFullParty` (`FightSpace.Fits(pilot.Base, 4, 8)`: 14 floor cells).
- [ ] **Step 2: Run** `--where "class =~ PilotRoomsV2Tests"`, FAIL. **Step 3: Implement.** **Step 4: Run**, PASS. If the validator rejects the grid, fix the grid only where a rule really demands it and record why in the file header.
- [ ] **Step 5: Commit** `feat(core): pilot room v2, the hanging hall (plan 4 task 5)`.

---

### Task 6: Gym layout and the room authors' doc

**Files:**
- Create: `Assets/_Project/Core/Rooms/MetricsGymLayout.cs`, `docs/ROOM_METRICS.md`
- Test: `Assets/_Project/Tests/EditMode/MetricsGymLayoutTests.cs`, `Assets/_Project/Tests/EditMode/RoomMetricsDocTests.cs`

**Interfaces:**
- Consumes: `GameMetrics`, `MovementProbe`, `FightSpace`, `PilotRoomsV2`.
- Produces: `enum GymShape : byte { Box, Capsule, Marker, PilotRoom }`; `readonly struct GymPiece { string Lane; string Name; GymShape Shape; float X, Y, Z /* centre, metres, y up */; float SX, SY, SZ /* size; Capsule: SX = diameter, SY = height */; string Label; int PilotState /* PilotRoom only */; }`; `static class MetricsGymLayout { IReadOnlyList<GymPiece> Build(); IReadOnlyList<string> Lanes { get; } }`.

Lanes (one per strip along +Z, strips 10 m apart on X, a 0.1 m floor slab per lane; Marker = a thin 0.05 m post with a label):
1. `run`: markers at `RunSpeed·k`, k = 1..3 ("1 s run").
2. `dodge`: marker at `DodgeDistance()`.
3. `gaps`: pits of widths 2.0, 3.0, `RunJumpDistance() − 0.2`, `RunJumpDistance() + 0.3`, 4.0 (one cell) m.
4. `ledges`: blocks of heights `StepOffset`, 1.0, `JumpApex()`, `MaxClimbableLedge`, `LevelHeightCm/100`.
5. `stair`: one cell (4 m) of `StepsPerLevel` steps rising `StairRiseCm`, onto a 4x4 m level-1 deck.
6. `doors`: two wall frames, openings `DoorWidth` and `WideArchWidth`, both `DoorHeight`, walls `WallHeight`; a vault-crown bar at `RoomVaultCrown`.
7. `water`: two 4x4 m pools, depths `WadeMaxCm/100` and 1.0 m (labelled wade / deep).
8. `camera`: a hero capsule and a boom bar from the pivot (`CameraPivotHeight`) back `CameraFreeBoom` at `CameraPitchDeg`, under a ceiling slab at `RoomVaultCrown`.
9. `fight`: walls around `SmallestRectangle(4, 8)` cells (12 x 20 m), 4 hero capsules (r 0.35, h 1.8) and 8 enemy capsules (r 0.5, h 1.2) spread inside.
10. `pilot`: two `PilotRoom` pieces, `PilotState` 0 and 1, side by side (the builder greyboxes `PilotRoomsV2.HangingHall` there).

- [ ] **Step 1: Failing tests** `MetricsGymLayoutTests`: `Lanes_AreTheTen`; `RunMarkers_AreOneSecondApart` (Z gaps = `RunSpeed`); `Ledges_IncludeTheClimbBoundAndOneLevel`; `FightRoom_IsTheSmallestFullPartyRoom` (wall span = 12 x 20 m; 4 + 8 capsules; no two capsules overlap); `Lanes_DoNotOverlap` (per-lane XZ bounds disjoint); `Build_IsDeterministic` (two builds equal).
- [ ] **Step 2: Write `docs/ROOM_METRICS.md`** for room authors (one page, short sentences): the grid (4 m cells, 14x14); the hero (run 7 m/s, dodge 3.5 m, run-jump about 3.6 m: a one-cell pit is never jumpable, a 3 m gap is; boots must not change this); doors (2.4 x 2.8, wide arch 3.6, door on level 0); height (level = 2 m, one stair cell per level, balconies are lobber daises, drops are free, max level 1 for now); water (wade ≤ 50 cm, deep blocks, no swimming) and heat; the gimmick rules (controls act on their own room's region; every state must change something; no softlock from any state; a hero on a cell that floods is moved to the nearest dry cell); fight space (full party needs 13 floor cells and a 3-cell short side, smallest 3x5; the pool rooms that fit: the five from Task 2; the rest are solo/duo rooms); room data v2 in one paragraph (fields, version 2, `FromV1`, `RoomValidatorV2`, pilot `PilotRoomsV2.HangingHall`); the gym scene path. End with the generated table between `<!-- metrics:begin -->` and `<!-- metrics:end -->`.
- [ ] **Step 3: Write `RoomMetricsDocTests.DocTable_MatchesGameMetrics`**: find the repo root by walking up from `Directory.GetCurrentDirectory()` until `docs/ROOM_METRICS.md` exists; the text between the markers (trimmed, `\r\n` → `\n`) equals `GameMetrics.MarkdownTable()`; on failure the message prints the expected table to paste.
- [ ] **Step 4: Implement `MetricsGymLayout`**; paste the table into the doc; run both test classes, PASS.
- [ ] **Step 5: Commit** `feat(core): metrics gym layout and ROOM_METRICS doc (plan 4 task 6)`.

---

### Task 7: The gym scene builder

**Files:**
- Create: `Assets/_Project/Editor/Builders/MetricsGymBuilder.cs`, `Assets/_Project/Editor/Builders/RoomGreybox.cs`, `Assets/_Project/Presentation/Metrics/MetricsGymRecord.cs`
- Modify: `Assets/_Project/Editor/Builders/ProjectBuilder.cs` (one `Step` after `UndercroftStair`), `Assets/_Project/Editor/Builders/AssetPaths.cs` (`MetricsGymScene = ScenesFolder + "/MetricsGym.unity"`), and any scene inventory the validators or tests keep (grep `Validators.cs:79`, `:201`, `:265` and `Tests/` for the scene lists before adding).
- Test: `Assets/_Project/Tests/EditMode/MetricsGymSceneTests.cs`

**Interfaces:**
- Consumes: `MetricsGymLayout.Build()`, `GameMetrics.Rows()`, `PilotRoomsV2.HangingHall`, `RoomDataV2.Standable/LevelAt/DepthCm`.
- Produces: `public static class MetricsGymBuilder { public static void Build(); }`; `public static class RoomGreybox { public static GameObject Build(RoomDataV2 room, IReadOnlyList<int> state, Vector3 origin, Transform parent); }`; `public sealed class MetricsGymRecord : MonoBehaviour { public string[] Keys; public float[] Values; public string[] Units; }`.

- [ ] **Step 1: Failing test** `MetricsGymSceneTests.Scene_RecordsTheMetrics`: the scene asset exists at `AssetPaths.MetricsGymScene`; open it additively with `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)`, find one `MetricsGymRecord` whose `Keys`/`Values` equal `GameMetrics.Rows()` (values within 1e-4), find one root per lane named `Lane_<lane>`, then close the scene (`EditorSceneManager.CloseScene(scene, true)`) in a `finally`.
- [ ] **Step 2: Implement** `MetricsGymBuilder.Build()` on the `DiveArenaBuilder` model: new empty scene, a directional light and a camera, one root per lane, each `GymPiece` as a primitive via `BuilderUtil.EnsurePrimitive` (Box → Cube, Capsule → Capsule, Marker → thin Cube) with materials from `MaterialLibrary.Get` (existing greybox names only, so `MaterialValidationTests` stays green), a `TextMesh` label only if another builder already uses one (otherwise put the label in the object name), the `MetricsGymRecord` filled from `GameMetrics.Rows()`, and the two `PilotRoom` pieces through `RoomGreybox.Build` (a 4 m floor tile per Floor cell raised to `LevelAt·LevelHeightCm/100`, pillars and solids as 3.6 m blocks, a ramp per stair, a translucent-less water slab at `DepthCm` per region cell, a small marker per chest, spawn and control). Save with `BuilderUtil.SaveScene(AssetPaths.MetricsGymScene)`. Not in Build Settings. No randomness.
- [ ] **Step 3: Wire** `Step(errors, "MetricsGym", MetricsGymBuilder.Build);` after the `UndercroftStair` line in `ProjectBuilder.RunAll`, and register the scene where Step 1's grep found inventories.
- [ ] **Step 4: Run** `Tools\unity.ps1 build -OutDir <scratch>` (generates the scene and every new `.meta`), then run it again and confirm `git status` shows no changed `.unity`/`.mat` (determinism). Then `Tools\unity.ps1 test-edit -OutDir <scratch>`: all EditMode tests PASS, including Task 1's pin test and Step 1's test.
- [ ] **Step 5: Commit** the builder, record, scene, metas and wiring: `feat(editor): metrics gym scene builder (plan 4 task 7)`.

---

### Task 8: Ledge and stair proof in PlayMode, then the gate

**Files:**
- Test: `Assets/_Project/Tests/PlayMode/HeroClimbMetricsTests.cs`

**Interfaces:**
- Consumes: `MovementMath`, `GameMetrics`.

- [ ] **Step 1: Write the test.** Build geometry at runtime (cubes with BoxColliders on the `Ground` layer, as other PlayMode tests do): a CharacterController with the hero's numbers (`CapsuleHeight`, `CapsuleRadius`, `StepOffset`, `SlopeLimit`, `SkinWidth`, centre y 0.9) driven at a fixed 60 Hz step by a minimal copy of `HeroLocomotion`'s run-and-jump step (`Accelerate`, `GroundedPush`, `JumpVelocity`, `VerticalStep`, `CharacterController.Move`): run at `RunSpeed` toward a ledge and jump 0.3 m before it. Tests:
  - `MaxClimbableLedge_IsBelowOneLevel`: try ledges from 0.4 m to 2.0 m in 0.1 m steps; the highest the hero ends up standing on is ≥ `JumpHeight` and < `LevelHeightCm/100 − 0.2`. `Debug.Log` the measured value as `SC METRIC max_ledge=<m>`.
  - `Stair_ClimbsOneLevel`: walking (no jump) up a stair of `StepsPerLevel` boxes, each `StairRiseCm` high and `StairRunCm` deep, reaches the deck at 2.0 m within 3 s.
  - `OneLevelWall_BlocksARunJump`: a 2.0 m wall stops the hero (end height < 0.5 m).
  Every wait loop has a frame cap; no `WaitForEndOfFrame` (`-nographics`).
- [ ] **Step 2: Run** `Tools\unity.ps1 test-play -OutDir <scratch>` (it queues on the PlayMode port lock). Expected: PASS. If the measured max ledge disagrees with `MaxClimbableLedge` by more than 0.2 m, note the measured value in `ROOM_METRICS.md` prose (the analytic bound stays the constant).
- [ ] **Step 3: Run** `Tools\unity.ps1 gate -OutDir <scratch>`, PASS. Commit `test(playmode): hero climb metrics (plan 4 task 8)` and push the branch.

---

### Task 9 (controller): merge
- [ ] Merge `origin/main` into the branch (builder output conflicts: take main's side, one `build`, commit). Run one `Tools\unity.ps1 check`; confirm `SC MARKER` equals HEAD. No soak: nothing under `Core/Cellars/` changes.
- [ ] Merge per `sc-merge` (`git merge --no-ff` of the checked tip into a fresh `origin/main`; the Assets freeze was lifted 2026-11-03). Set the board profile `Archive: ready`.

## Self-review (done while writing)
- Roadmap row 4 coverage: hero, boom, run and dodge (Task 1), door widths (1, 6), smallest 4+8 fight room (2, 6, 7), stair and ledge heights (1, 8), water levels (3, 6); v2 contract: height levels, door types, gimmick-state layers, tool sockets, habitat tags, version field (3); F7 state-aware validator (4); one pilot (5); gym scene by a builder (7); doc for room authors (6).
- Names are consistent across tasks: `MovementProbe.DodgeDistance/RunJumpDistance/JumpApex`, `GameMetrics.Rows/MarkdownTable/LevelHeightCm/WadeMaxCm/MaxValidatedLevel`, `FightSpace.Fits/SmallestRectangle`, `RoomDataV2.FromV1/Author/Standable`, `RoomValidatorV2.Validate`, `PilotRoomsV2.HangingHall`, `MetricsGymLayout.Build`.
- Open by design: the generator, seam rule and runtime eject wait for plan 3; level 2+ and a v2 room pool wait for room authoring.
