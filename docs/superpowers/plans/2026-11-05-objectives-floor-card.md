# Objectives and Floor Card Implementation Plan (plan 5)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build spec section 12 step 4 into the live 7-floor dive: two new floor objectives (two-valve seal, hold the pump) beside the gate-key carrier, a named floor card on every floor, and two modifier slots (a hazard with a printed reward, and a free boon).

**Architecture:**
- Everything that decides *what* a floor is stays pure Core and a function of `(seed, floor)` (plus the deterministic layout), on its own `Rng` streams, so every peer computes the same objective, valve/pump cells, modifiers and card text locally. Nothing new is sent except live progress. The layout, dressing, atmosphere, loot-mix and floor goldens do not move; the roster layer hash and encounter-plan goldens on twist floors do move (Decision 3) and are re-pinned.
- Live objective state (valves turned, pump started and progress) is host-held and rides in `CellarContentsSync.ContentsState` (already the floor's host-written NetworkVariable). Completion sets the existing `ExitUnlocked`, which `DiveDirector` already wires to `ServerOpenStair`.
- The existing hidden floor twists (`FloorTwist` Ambush / Thin / Crowded, `CellarFloorRoster.TwistOf`) become modifiers printed on the card: `TwistOf` is derived from the new `FloorModifierSet`, so no second hidden roll remains.
- The card is an IMGUI HUD (the project's UI style; no floor map screen exists yet): a full card for a few seconds on arrival, then a compact corner panel (name, objective + progress, modifiers) that stays up, so nothing is missable without a new input binding.

**Tech Stack:** C# 9 (Unity 6000.6.5f1), NGO 2.13, NUnit 3, `Pcg32` + `Rng.Derive` (`Core/Random`), IMGUI.

**Spec:** `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` sections 6 (items 2, 5, 8), 7 (rules from the research), 12 step 4; design note `docs/superpowers/specs/2026-11-04-floor-card-and-modifiers.md` (all of it; numbers are placeholders).

## Global Constraints
- Host-authoritative. Only the host writes `NetworkVariable`s; clients ask by RPC and the host re-checks reach, liveness and floor. Every networked class keeps its class-level `// Authority:` comment (`sc-netcode`). No anti-cheat beyond normal host validation.
- Core stays pure C# (no `UnityEngine`); Core test files use only `System*`, `NUnit*`, `SleepingCalamity.Core*`, so `python3 Tools/cloud/sc_core_tests.py` runs them without Unity.
- Every new number is a `public const` marked `// placeholder`.
- New Core randomness uses new stream ids only: `cellars.objective`, `cellars.objective.cells`, `cellars.modifier`, `cellars.card.name`. Never draw from an existing stream (keeps `Golden_Seed12345_GeneratedFloors`, dressing, loot-mix and atmosphere goldens fixed).
- No required N-player action; every objective works solo; nothing missable; no hidden random penalty (every hazard is on the card with its reward); rooms cleared stay cleared; keys, pieces and people stay single shared host-held items; loot and gold per player (decision 39).
- At most 8 live enemies (`GameMetrics.MaxLiveEnemies`, `Core/Rooms/GameMetrics.cs:23`; no duplicate const); pump waves are trimmed to it and at most two waves are alive at once. Room spawns on entry, the ambush and the hunter are not newly capped by this plan (existing behaviour; noted in Deferred).
- Never edit `.prefab`/`.unity` YAML. Valve and pump markers are runtime greybox objects spawned by `CellarContentsSync` (no new prefab, no builder change). New `.cs` files get Unity-made `.meta` files from a Unity run (`unity.sh gate`), never hand-written GUIDs.
- Cloud: run Unity only via `bash Tools/cloud/unity.sh <cmd> --out <scratch>`; one Unity run at a time, in the session checkout (no extra worktree: disk). After any build, `git checkout -- Assets/_Project/Scenes Assets/_Project/Content/Materials` (Linux float churn) unless the task meant to change them.
- Avoid the files of in-flight PC sessions where possible: `BankChoice.cs`, `BankSlots.cs`, `PartyBags.cs`, `SuspendSave.cs`, `DiveSession.cs`, `DiveDirector.cs` bank code (`LocalBankScreen`, `DrawBankScreen`, `RequestBankRpc`, the `LocalInteract` bank branch), `DiveHaulSync` choice tick. Touch `DiveDirector.cs` only at the stair gate (`NetStairOpen`, `ServerTryStair`, `StairIsOpen`, `RefreshStairOpen`, the `RequireGateKey` wiring) and the locked-stair prompt text.
- Leave `CURRENT.md` alone. Branch: `claude/upbeat-maxwell-pnadr4`. Commit after each task.
- Test commands (cloud): Core `python3 Tools/cloud/sc_core_tests.py --where "class =~ <TestClass>"`; compile `python3 Tools/cloud/sc_cloud_compile.py`; gate `bash Tools/cloud/unity.sh gate --out <scratch>`; PlayMode `bash Tools/cloud/unity.sh test-play --filter <regex> --out <scratch>`; soak `bash Tools/cloud/unity.sh soak --out <scratch>`; full `check` only before the merge.

## Save impact: none (by design)
- Objective, valve/pump cells, modifiers and card text are functions of the dive seed and floor, which the dive already holds and the `suspend` section already restores.
- Live objective state is per floor and, like the gate key today (`DiveDirector` clears `_openedStairs` on resume), is not saved: a resumed dive starts floor 6 fresh.
- The Stirred +10 is not a new stir input: the host derives it from `(seed, current floor)` and sets a non-saved `DiveStir.ModifierBonus`, so `SuspendStirSave` is unchanged (floor 5, where the bank suspends, is calm, so the bonus is 0 there).
- No world-save section, field or fixture changes. The risk review checks this claim.

## Decisions this plan takes (placeholders; questions copied to STATUS.md)
1. **Two modifier slots (note section 3).** Slot A hazard + reward, slot B boon, either may be empty. Calm floors (`DiveFloorTable.IsCalmFloor`: 1, 3, 5, 6) have none. Floors 2 and 4: at most one slot fills (A 40%, B 35%, none 25%). Floor 7 (and any later generated floor >= 6): A 75% and B 60%, rolled independently. Never two from one family; a modifier already used earlier in the dive becomes empty (earlier floors are replayed from the seed, like `TwistOf`).
2. **The modifier list built now** (families in brackets):
   - Slot A hazards: **Crowded** [Enemies] = today's Crowded twist; reward +15 gold per room cleared, per present player. **Ambush** [Enemies] = today's ambush twist (the card names it, not the room); reward +40 gold per present player when the ambush room is cleared. **Stirred** [Stir] = +10 stir while the party is on this floor only (added after the sqrt-of-players scaling, never scaled again; it leaves with the floor; because `RewardValue` includes `Value` it counts like any stir while present); reward: every chest on the floor gives each player one extra roll. **Hurried** [Clock] = collapse multiplier 1.25 instead of 1.5 on this floor; reward +30 gold per present player when the party leaves the floor by its stair or exit. **Dim** [Light] = not rolled: slot A is Dim exactly when `CellarAtmosphere.IsDarkFloor(seed, floor)` is true on a generated floor (the existing at-most-one dark floor per dive, now printed on the card); it overrides the slot A roll, applies even on calm floor 6 (darkness already ships there), and on floors 2/4 leaves slot B empty. Reward +1 rank bonus on every chest roll on the floor. No atmosphere or dressing code changes.
   - Slot B boons: **Sparse** [Enemies] = today's Thin twist. **Warm Hands** [Healing] = healing points recharge in 15 s instead of 30. **Hushed** [Stir] = the stir hunter does not spawn on this floor.
   - Deferred: Brimming (needs the water gimmick, step 5), Fat Larder (needs elite affixes, step 5 encounter templates), Still Air (no ambient drips exist to silence). The note's "Warm Hands heals 25% more" became faster recharge because healing points already heal fully; Hurried's reward became gold because changing the push-on reward means editing `DiveSession`/`DiveStir` reward accounting, which another session is in.
3. **Twists fold into modifiers.** `CellarFloorRoster.TwistOf(seed, floor)` returns Crowded / Ambush / Thin when slot A is Crowded / Ambush or slot B is Sparse, else None. `FloorTwistTuning.AmbushPercent/ThinPercent/CrowdedPercent` are removed (their rates move into `FloorModifierTuning`). Encounter plans and goldens on twist floors move; re-pin them from the failure message and say so in the commit.
4. **Objective pick.** Pool for floors 2, 4, 6: GateKey, TwoValves, HoldPump (swarm is out of step 4). Floor 7 has no stair or carrier flow (`HasGateKeyStair(7)` is false), so its pool is TwoValves and HoldPump only, gating the stand-in ending door. Never the same as the previous floor's objective, where floor 1 is GateKey (taught), floor 3 is Rescue, floor 5 is Guardian (not in the pool). So floor 2 is TwoValves or HoldPump; 4 and 6 any of three; 7 differs from 6 (forced to the other one when 6 rolled a valve/pump objective). Equal weights. If a floor's layout can't host the picked objective (fewer than 2 eligible valve rooms, or no pump room), floors 2/4/6 fall back to GateKey and floor 7 to `Ending` (ungated, as today); the soak sweep counts fallbacks (measured 361 of 8000 floors after the Decision 5 relaxation, all with fewer than 2 eligible rooms; a fallback may repeat the previous objective, documented).
5. **Valve placement.** Valve and pump rooms are Combat rooms only (`CellarEncounterPlan.FreeCellsOf(layout, dressing, room)`, `CellarEncounterPlan.cs:204`, returns no cells for other kinds, which also excludes entry, exit and checkpoint rooms). Room graph distance from the entry room (BFS over `Links`, locked link excluded). Eligible: not the entry room, not the exit (stair) room, not the locked room, reachable without the locked link, has a free valve cell. Valve B = the eligible room with the greatest distance, preferring rooms with exactly one link (dead ends); valve A = the eligible room nearest the entry that is not adjacent to B (no shared link) and not B. A non-adjacent pair is preferred (B must have a non-adjacent partner); when no such pair exists, two distinct adjacent eligible rooms are allowed (B the farthest, A the nearest other), because layouts have 4-6 rooms and often only two eligible ones (measured 2026-11-06: 21% of floors fell back without this). Fallback only with fewer than 2 eligible rooms. Valve cell = the cell of `FreeCellsOf(...)` nearest the room centre that is also not a dead end (reuse, don't copy, its free rules). Ties by `cellars.objective.cells`.
6. **Pump placement.** The pump room is an eligible room (same rules) on or beside the entry-to-exit room path whose distance from the entry is in the middle third of that path (`d*3 >= len` and `d*3 <= 2*len`), nearest to `len/2`; the pump cell is its centre-most free cell. The pump ring radius is 4 m (placeholder).
7. **Valve hold** = 2 s (placeholder), revive-style: the owner reports "holding at valve i" every 0.2 s while the interact button is held within 1.8 m; the host accumulates while the last report is under 0.45 s old and resets to 0 when reports stop. A turned valve stays turned. A valve room is an ordinary combat room.
8. **Pump.** Started by one press (host checks reach 2.5 m and that the asker is up). Progress fills 1/75 per second (placeholder, not party-scaled) while at least one living hero is inside the ring; pauses (never resets) when none is; runs at 50% while any living pump-wave enemy is within 2.5 m of the pump. Three waves at progress 0, 0.35, 0.70; wave k spawns when its threshold is reached and fewer than two earlier waves still have living members. Wave size 2 + (players - 1), trimmed so live enemies on the floor stay <= 8. Within a wave enemies appear one at a time every 1.5 s (1.875 s solo). Wave enemies are Tier-1 from the floor roster, spawned at `CellarAmbush` ring cells of the pump room. At 100% the exit unlocks; the pump can't fail.
9. **Floor card** (note section 2): name, "Floor N", objective line with progress (`Valves 1/2`, `Pump 40%`, `Find the gate key`, `Key taken`), each modifier with its effect line and (hazards) reward line, and "Pieces left here: N" from `ContentsState.PieceCount` minus found. Shown full for 6 s on arrival (placeholder), then a compact corner panel that stays. Authored floors carry fixed cards (table in Task 2). Names: a hand-written table per `SubTheme` (Task 2), drawn from `cellars.card.name`, no repeat within a dive.
10. **No decline at the stair** (note open question 2): hazards are always taken.
11. **Modifier rewards are ordinary loot/gold** (note open question 6): they never count toward stir or the bank's unbanked-valuables input except as normal carried items.

## Review Focus
1. **Exit stays shut / opens twice.** Valves turned in either order by two players at once; pump finishing while a wave is alive; a host migration is not supported, but a client joining mid-floor must see valves turned and pump progress. Tests: Task 4 `TwoValves_BothTurned_UnlocksExit_AnyOrder`, `LateJoiner_SeesValveAndPumpState`.
2. **Gate-key code paths on non-key floors.** The carrier must not drop a gate key and `ServerArmFloor` must not open the stair at once on valve/pump floors. Test: Task 4 `ValveFloor_CarrierDropsNoKey_StairStaysShut`.
3. **Enemy cap.** Pump waves never push live enemies above 8. Test: Task 3 `PumpHold_WaveSizes_RespectLiveCap`, Task 4 `Pump_WavesTrimmedAtCap`.
4. **Determinism.** Objective, cells, modifiers and card name equal on host and client; generator goldens unchanged. Tests: Task 1/2 golden-unchanged and 2000-seed sweeps.
5. **Save claim.** No save schema change; a resume at floor 6 recomputes modifiers and objective.

---

### Task 1: Objective pick and placement (Core)

**Files:**
- Create: `Assets/_Project/Core/Cellars/FloorObjective.cs` (enum + `FloorObjectivePick` + `FloorObjectivePlan`)
- Modify: `Assets/_Project/Core/Cellars/CellarFloorValidator.cs` (objective check)
- Test: `Assets/_Project/Tests/EditMode/FloorObjectiveTests.cs`

**Interfaces:**
- Produces:
  - `public enum FloorObjective : byte { None = 0, GateKey = 1, TwoValves = 2, HoldPump = 3, Rescue = 4, Guardian = 5, Ending = 6 }`
  - `public static class FloorObjectiveTuning { public const float ValveHoldSeconds = 2f, ValveReachMeters = 1.8f, ReportInterval = 0.2f, ReportTimeout = 0.45f, PumpSeconds = 75f, PumpRingMeters = 4f, PumpStartReach = 2.5f, PumpSlowRadius = 2.5f, PumpSlowFactor = 0.5f, WaveStagger = 1.5f, SoloStaggerFactor = 1.25f; public const int WaveBase = 2, WaveCount = 3, MaxWavesAlive = 2; public static readonly float[] WaveThresholds = { 0f, 0.35f, 0.70f }; }` (all placeholder)
  - `public static class FloorObjectivePick { public static FloorObjective Rolled(uint seed, int floor); }` (the pick before layout fallback; floor 1 GateKey, 3 Rescue, 5 Guardian, generated floors from `cellars.objective`, previous-floor rule from Decision 4)
  - `public sealed class FloorObjectivePlan { public readonly FloorObjective Objective; public readonly int ValveRoomA, ValveRoomB, PumpRoom; /* -1 when unused */ public readonly CellPos ValveCellA, ValveCellB, PumpCell; public static FloorObjectivePlan For(CellarFloorLayout layout); public static FloorObjectivePlan For(CellarFloorLayout layout, CellarFloorDressing dressing); public ulong Hash(); }` (`For(layout)` calls `CellarFloorDressing.Resolve(layout)` itself; callers that already hold the dressing pass it. Uses `layout.Seed/Floor`, applies `Rolled`, places per Decisions 5-6, falls back per Decision 4)
  - `public static int FallbackCount` is NOT added; tests compute fallback as `For(l).Objective != Rolled(seed, floor)`.

- [ ] **Step 1: Write failing tests** in `FloorObjectiveTests` (namespace `SleepingCalamity.Tests.EditMode`, only Core usings):
  - `Rolled_FixedFloors`: floor 1 GateKey, 3 Rescue, 5 Guardian for seeds 1..50.
  - `Rolled_NeverRepeatsPreviousFloor`: seeds 1..2000, floors 2, 4, 6, 7: `Rolled(s,2) != GateKey`, `Rolled(s,7) != Rolled(s,6)`, `Rolled(s,7) != GateKey`, all in {GateKey, TwoValves, HoldPump}.
  - `Rolled_AllThreeAppear`: over seeds 1..300 each of the three appears on floor 4 at least 60 times.
  - `Plan_ValveRooms_DistinctNonAdjacentReachable`: for seeds 1..300 and floors 2,4,6,7 where `Objective == TwoValves`: A != B, no link joins A and B, neither is entry/exit/locked room, both cells are `CellKind.Floor` cells inside their rooms and not door cells, both reachable from `EntryStarts[0]` without the locked link (reuse the validator's reachability helper).
  - `Plan_PumpRoom_InMiddleThird`: same sweep for HoldPump: room distance d along the entry-to-exit room path satisfies `3*d >= len && 3*d <= 2*len` (or the beside-path rule), cell is a free floor cell.
  - `Plan_IsDeterministic`: `For(Layout(s,f)).Hash()` equal across two calls for 100 seeds.
  - `GeneratorGolden_Unchanged`: re-run the existing `CellarFloorGeneratorTests.Golden_Seed12345_GeneratedFloors` (no edit to it; just note it in the run).
  - `[Category("Soak")] Plan_FallbackRate_2000Seeds`: seeds 1..2000, floors 2,4,6,7: count fallbacks; print fallbacks and adjacent valve pairs; a fallback is allowed only if the layout has fewer than 2 eligible rooms, assert that.
- [ ] **Step 2: Run** `python3 Tools/cloud/sc_core_tests.py --where "class =~ FloorObjectiveTests"`: expect compile failure (types missing).
- [ ] **Step 3: Implement** `FloorObjective.cs`. Key code:

```csharp
public static FloorObjective Rolled(uint seed, int floor)
{
    if (floor == 1) return FloorObjective.GateKey;
    if (floor == 3) return FloorObjective.Rescue;
    if (floor == UndercroftPlan.BankFloor) return FloorObjective.Guardian;
    if (!CellarFloorGenerator.IsGeneratedFloor(floor)) return FloorObjective.None;
    FloorObjective prev = Rolled(seed, floor - 1);            // recursion ends at floor 1
    var pool = floor == DiveFloorTable.LastBuiltFloor        // floor 7: no stair, no carrier flow
        ? new[] { FloorObjective.TwoValves, FloorObjective.HoldPump }
        : new[] { FloorObjective.GateKey, FloorObjective.TwoValves, FloorObjective.HoldPump };
    var rng = new Pcg32(Rng.Derive(seed, "cellars.objective", (ulong)floor));
    int n = 0; var pick = new FloorObjective[pool.Length];
    foreach (var o in pool) if (o != prev) pick[n++] = o;
    return pick[rng.Range(0, n)];
}
```
  Placement: build a room adjacency list from `layout.Links` skipping `layout.LockedLinkIndex`; BFS from `layout.RoomAt(EntryStarts[0])`; the exit room is `layout.ExitRoomIndex()`. Free cells: `CellarEncounterPlan.FreeCellsOf(layout, dressing, room)` (Combat rooms only), minus dead-end cells. Also add `FloorObjectivePlan` to `CellarFloorValidator` as a check run for generated floors: the plan's cells must be reachable without the lock (one more error string; no change for GateKey).
- [ ] **Step 4: Run** the class, then `--where "class =~ CellarFloorGeneratorTests"` (goldens unchanged, validator sweep green).
- [ ] **Step 5: Commit** `plan5 task 1: floor objective pick and valve/pump placement (Core)`.

### Task 2: Modifiers and the floor card (Core)

**Files:**
- Create: `Assets/_Project/Core/Cellars/FloorModifiers.cs` (`FloorModifier`, `FloorModifierSet`, `FloorModifierTuning`)
- Create: `Assets/_Project/Core/Cellars/FloorCard.cs` (`FloorCard`, `FloorCardNames`, `FloorCardText`)
- Modify: `Assets/_Project/Core/Cellars/CellarFloorRoster.cs` (`TwistOf` derived; remove the three percent consts)
- Modify: `Assets/_Project/Core/Cellars/DiveStir.cs` (non-saved `ModifierBonus`)
- Test: `Assets/_Project/Tests/EditMode/FloorModifierTests.cs`, `FloorCardTests.cs`; update `CellarRosterTests.cs`, `CellarEncounterPlanTests.cs`, `CellarAmbushTests.cs` goldens/rates as they break

**Interfaces:**
- Produces:
  - `public enum FloorModifier : byte { None = 0, Crowded = 1, Ambush = 2, Stirred = 3, Hurried = 4, Dim = 5, Sparse = 20, WarmHands = 21, Hushed = 22 }`
  - `public enum ModifierFamily : byte { None, Enemies, Stir, Clock, Light, Healing }`; `public static ModifierFamily FamilyOf(FloorModifier m)` (Crowded/Ambush/Sparse Enemies; Stirred/Hushed Stir; Hurried Clock; Dim Light; WarmHands Healing)
  - `public static class FloorModifierTuning` (placeholders): `EarlyAPercent = 40, EarlyBPercent = 35` (floors 2, 4: one roll 0..99: <40 A, <75 B, else none), `LateAPercent = 75, LateBPercent = 60` (floors >= 6), `StirredBonus = 10f, HurriedCollapseMultiplier = 1.25f, WarmHandsRechargeSeconds = 15f, CrowdedGoldPerClear = 15, AmbushGold = 40, HurriedGold = 30, DimRankBonus = 1, StirredExtraRolls = 1`; hazard weights equal among Crowded, Ambush, Stirred, Hurried, Dim; boon weights equal among Sparse, WarmHands, Hushed.
  - `public readonly struct FloorModifierSet { public readonly FloorModifier A, B; public bool Has(FloorModifier m); public static FloorModifierSet For(uint seed, int floor); public ulong Hash(); }` — non-generated floors give (None, None). A generated floor with `CellarAtmosphere.IsDarkFloor(seed, floor)` gets A = Dim (floors 2/4: B None; floor 7: B rolled; floor 6: B None as a calm floor). Otherwise calm floors give (None, None) and other floors roll A from {Crowded, Ambush, Stirred, Hurried} and B from {Sparse, WarmHands, Hushed} on `cellars.modifier` index `floor`; same family as A makes B None; a modifier used on an earlier generated floor of the dive becomes None (replay floors 2..floor-1; Dim is exempt, `IsDarkFloor` already allows one per dive).
  - `CellarFloorRoster.TwistOf(seed, floor)`: `var m = FloorModifierSet.For(seed, floor); return m.A == FloorModifier.Crowded ? FloorTwist.Crowded : m.A == FloorModifier.Ambush ? FloorTwist.Ambush : m.B == FloorModifier.Sparse ? FloorTwist.Thin : FloorTwist.None;`
  - `DiveStir`: `public float ModifierBonus { get; set; }` (clamped >= 0, not part of `State`, not saved); `Value` becomes `clamp(base * sqrt(players) + ModifierBonus, 0, Max)`. `ResetForNewDive` and `OnPushOn` set it to 0. Update the `Value` formula comment at the top of the file.
  - `public static float StirredBonusFor(uint seed, int currentFloor)` on `FloorModifierSet`: `StirredBonus` when `For(seed, currentFloor).Has(Stirred)`, else 0 (only the floor the party is on).
  - `public sealed class FloorCard { public readonly string Name; public readonly int Floor; public readonly FloorObjective Objective; public readonly FloorModifierSet Modifiers; public static FloorCard For(uint seed, int floor, FloorObjective objective); }` (objective comes from `FloorObjectivePlan.For(layout).Objective`; the card does not regenerate layouts)
  - `public static class FloorCardText { public static string ObjectiveLine(FloorObjective o, int valvesTurned, float pumpProgress, bool pumpStarted, byte gateKey); public static string EffectLine(FloorModifier m); public static string RewardLine(FloorModifier m); public static string Title(FloorModifier m); }` (strings built from the tuning consts so numbers change in one place)
  - `FloorCardNames`: authored cards: floor 1 "The Undercroft Steps", floor 3 "The Drowned Bakery", floor 5 "The Cistern Dome". Generated: by `layout.Theme` (`SubTheme`), five names each, no repeat within a dive (replay earlier generated floors' picks):
    - Larder: The Smoking Larder, The Salt Shelves, The Cold Crocks, The Pickle Rows, The Hook Hall
    - WineVault: The Weeping Casks, The Corkline, The Drowned Racks, The Vinegar Walk, The Long Bung
    - CoalCellar: The Ember Chutes, The Sooted Stair, The Coal Throat, The Cinder Bins, The Black Hods
    - Bakery and Any: The Proving Rooms, The Flour Drifts, The Cold Ovens, The Crust Cellars, The Peel Racks
    `public static string NameFor(uint seed, int floor, SubTheme theme)`; the theme of an earlier floor comes from `CellarFloorGenerator.Layout(seed, f).Theme` (cheap enough; only on floor entry).
  - Objective lines: GateKey "Find the gate key" / "Gate key found: the way down is open"; TwoValves "Open both valves (n/2)"; HoldPump "Start the pump and hold it (n%)" / "Start the pump" before start; Rescue "Wake the baker"; Guardian "Beat the guardian, then reach the bank"; floor 7 any objective adds " to open the way out".
  - Effect/reward lines (Decision 2), e.g. Crowded: "More enemies in about half the rooms" / "Reward: +15 gold per room cleared, each". Dim: "The floor is dark" / "Reward: better chest finds". Hushed: "The hunter stays away this floor".

- [ ] **Step 1: Failing tests:**
  - `FloorModifierTests.CalmFloors_HaveNone` (1, 3, 5, 6 for 500 seeds).
  - `EarlyFloors_AtMostOneSlot` (floors 2, 4: never both A and B).
  - `NeverTwoOfOneFamily`, `NoModifierTwiceInADive` (2000 seeds, floors 2, 4, 7).
  - `Rates_RoughlyMatchTuning` (5000 seeds floor 2: A 40% ± 4, B 35% ± 4 before the repeat rule; floor 7 A 75% ± 4).
  - `TwistOf_FollowsModifiers` (TwistOf matches the mapping for 2000 seeds; Thin only with Sparse).
  - `StirredBonus_OnlyCurrentFloor`, `Dim_ExactlyOnDarkGeneratedFloors` (2000 seeds: `Has(Dim) == IsDarkFloor && IsGeneratedFloor`), `DiveStir_ModifierBonus_AddedAfterSqrt_ClampedAndClearedOnPushOn` (players 4, base 20: Value = 20*2+10 = 50; Max clamp; `OnPushOn` and `ResetForNewDive` zero it; `State` round trip unchanged).
  - `FloorCardTests.AuthoredFloors_FixedNames`, `GeneratedNames_NoRepeatInDive` (2000 seeds), `ObjectiveLines` (each objective/progress case exact string), `EveryModifier_HasTitleEffect_AndHazardsHaveReward`.
- [ ] **Step 2: Run** both classes: fail to compile.
- [ ] **Step 3: Implement.** Then run `CellarRosterTests`, `CellarEncounterPlanTests`, `CellarAmbushTests`, `CellarSpawnSafetyTests`, `DiveStir*`, `DiveSuspendTests`: re-pin `CellarFloorRoster.LayerHash` and twist-floor encounter goldens from the failure message (expected: Decision 3), change `CellarAmbushTests.cs:23` (pins `AmbushPercent == 12`) and other rate tests to read the twist rate through `FloorModifierSet`. Dressing and atmosphere goldens must NOT move; if one does, stop and report. `Editor/Traversal/TraversalScan.cs` and `Presentation/Cellars/CellarContentsSync.cs` reference `TwistOf`/`FloorTwistTuning`: keep `AmbushDelaySeconds` and `FirstTwistFloor` so they still compile; fix any reference to a removed const.
- [ ] **Step 4: Run** `python3 Tools/cloud/sc_core_tests.py` (whole Core suite) and `python3 Tools/cloud/sc_cloud_compile.py` (all assemblies OK).
- [ ] **Step 5: Commit** `plan5 task 2: floor modifiers (twists folded in), floor card text and names (Core)`.

### Task 3: Valve and pump rules (Core)

**Files:**
- Create: `Assets/_Project/Core/Cellars/FloorObjectiveState.cs` (`ValveHold`, `PumpHold`)
- Test: `Assets/_Project/Tests/EditMode/FloorObjectiveStateTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class ValveHold { public const int Count = 2; public int TurnedMask { get; } public int TurnedCount { get; } public bool AllTurned { get; } public float Progress(int valve); public void Report(int valve, float now); public bool Tick(float now, float dt); /* true when a valve turned this tick */ public void Reset(); }` — per valve: accumulate `dt` while `now - lastReport[v] <= ReportTimeout`, else progress 0; at `ValveHoldSeconds` the bit sets and stays.
  - `public sealed class PumpHold { public bool Started { get; } public float Progress { get; } public bool Done { get; } public int WavesSpawned { get; } public void Start(); public PumpTick Tick(float dt, bool heroInRing, bool enemyAtPump, int livingPerWave0, int livingPerWave1, int livingPerWave2); public static int WaveSize(int players, int liveEnemiesNow); public static float Stagger(int players); }` with `public readonly struct PumpTick { public readonly int StartWave; /* -1 none, else wave index to spawn now */ public readonly bool JustDone; }`.
  - Rules exactly as Decision 8. `WaveSize = max(0, min(WaveBase + players - 1, GameMetrics.MaxLiveEnemies - liveEnemiesNow))`. `Stagger(1) = WaveStagger * SoloStaggerFactor`, else `WaveStagger`. Progress never decreases; capped at 1.

- [ ] **Step 1: Failing tests:** `Valve_TurnsAfterHold`, `Valve_ResetsWhenReportsStop`, `Valve_StaysTurned`, `Valves_AnyOrder_AllTurned`, `Pump_NotStarted_NoProgress`, `Pump_FillsIn75s_WithHeroInRing` (tick 0.1 s × 750 → Done once, `JustDone` exactly once), `Pump_PausesWithoutHero_NeverResets`, `Pump_HalfRate_WhenEnemyAtPump`, `Pump_WavesAtThresholds_AtMostTwoAlive` (with wave 0 and 1 alive at progress 0.7, wave 2 waits; once wave 0 dies it starts), `PumpHold_WaveSizes_RespectLiveCap` (players 4, live 6 → 2; live 8 → 0; players 1, live 0 → 2), `Stagger_SoloIsSlower`.
- [ ] **Step 2: Run** fails; **Step 3: implement**; **Step 4: run** the class and the whole Core suite.
- [ ] **Step 5: Commit** `plan5 task 3: valve hold and pump hold rules (Core)`.

> **Implementer run A = Tasks 1-3** (Core only, no Unity needed; `sc_core_tests.py` + `sc_cloud_compile.py`). Then one `reviewer` pass. `unity.sh soak` runs once on the branch after run A because Core/Cellars changed (and again before the merge if Core/Cellars changes after).

### Task 4: Objectives live in the dive (host-authoritative)

**Files:**
- Modify: `Assets/_Project/Presentation/Cellars/CellarContentsSync.cs` (state, arm, ticks, RPCs, markers, carrier gating)
- Modify: `Assets/_Project/Presentation/Cellars/DiveDirector.cs` (stair gate only: `HasGateKeyStair` use at ~:508-510 becomes "floor has an objective gate"; the locked-stair prompt text at ~:829 reads the objective line)
- Test: `Assets/_Project/Tests/PlayMode/FloorObjectiveNetworkTests.cs` (new), adjust `CellarContentsNetworkTests.cs` where it assumed every generated floor is a key floor

**Interfaces:**
- Consumes: `FloorObjectivePlan.For`, `FloorObjectiveTuning`, `ValveHold`, `PumpHold` (Tasks 1, 3).
- Produces: `ContentsState` gains `public byte Objective; public byte ValveMask; public bool PumpStarted; public byte PumpPercent; /* 0-100 */` serialized and in `Equals`. Public read API on `CellarContentsSync`: `FloorObjective CurrentObjective`, `int ValvesTurned`, `float PumpProgress`, `bool PumpStarted`, `FloorObjectivePlan Plan` (the locally computed plan; host and clients equal).

Behaviour (Authority: host; add it to the class comment):
1. `ServerArmFloor` (~:506-518): compute the plan; write `Objective`. GateKey: today's code unchanged (including "no usable carrier opens the stair at once"). TwoValves/HoldPump: the carrier spawns as an ordinary enemy and never drops a key (`GateKey` stays 0); the stair stays shut until the objective completes. Every carrier-as-key-carrier assumption must check `Objective == GateKey`: the arm-time early open (~:518), the carrier skip/room reset in `SpawnRoom` (~:656), the "carrier could not spawn, so open the stair" case (~:666, would otherwise open valve/pump floors), and the death/key-drop path (~:704-712). Rescue/Guardian/authored floors: unchanged.
2. Valves: `LocalInteract` returns early when there is no rising edge (~:313-314); restructure so the held-valve report runs before that return (don't bolt it on after). The owner-side code adds: if `Objective == TwoValves`, the local hero is up, within `ValveReachMeters` of an unturned valve cell (cell centre at floor height) and `InteractHeld`, send `ValveHoldRpc(byte valve)` (`SendTo.Server`) every `ReportInterval`. Host: check the sender's hero is up, on this floor, within reach + 0.5 m; then `ValveHold.Report(valve, Time.time)`. Host `Update` ticks `ValveHold`; on a turn, set `ValveMask`; when `AllTurned`, set `ExitUnlocked = true` (the existing path opens the stair/exit). Gate-key press-edge logic must not also fire on the same press near a valve.
3. Pump: a press within `PumpStartReach` sends `StartPumpRpc()`; host checks up + reach, `PumpHold.Start()`, `PumpStarted = true`. Host ticks with `heroInRing` = any living hero within `PumpRingMeters` of the pump cell, `enemyAtPump` = any living pump-wave enemy within `PumpSlowRadius`; `StartWave` spawns `WaveSize(playersPresent, liveEnemiesOnFloor)` Tier-1 enemies (floor roster remap as today) at `CellarAmbush.Cells(...)` ring cells of the pump room, staggered by `Stagger(players)`, tagged with their wave index. `PumpPercent` updates at most when the whole percent changes (keeps the NetworkVariable quiet). `JustDone` sets `ExitUnlocked = true`.
4. Markers (all peers, presentation only, built with the floor; no NetworkObject): a valve = a greybox wheel (cylinder 0.6 m wide on a post) at each valve cell that turns 90° and changes colour when its `ValveMask` bit is set; the pump = a greybox box with a ring decal/line circle of `PumpRingMeters` radius, its colour following started/done. Interaction prompts in the existing prompt style: "Hold [Interact] to open the valve", "[Interact] start the pump when you're ready".
5. `DiveDirector`: keep `RequireGateKey`/`HasGateKeyStair` as they are (floors 2, 4, 6 are exactly the gated stairs; `ExitUnlocked` already drives `ServerOpenStair`). Change only: the locked-stair prompt (~:829) shows `FloorCardText.ObjectiveLine(...)` from `CellarContentsSync` instead of the gate-key-only text; on floor 7, `ServerTryFinish` (~:394-402) refuses (notice with the objective line) and the "Leave the Undercroft" prompt (~:830-831) shows the objective line while `ContentsSync.Objective` is TwoValves/HoldPump and `ExitUnlocked` is false. `IsExitUnlocked(7)` must stop returning true unconditionally for those objectives. No other DiveDirector edits.
6. Late join: all state is in `ContentsState`, so a joiner's markers and prompts follow it (valve bits, pump started/percent).

Tests (PlayMode, host + 1 client, existing dive test harness and `DiveDirector.SeedOverride`; find seeds with Task 1's `FloorObjectivePlan` in the test's `[OneTimeSetUp]` rather than hard-coding):
- `TwoValves_BothTurned_UnlocksExit_AnyOrder` (drive hold reports via the RPC path on host and client heroes; B then A).
- `Valve_HoldInterrupted_DoesNotTurn`.
- `ValveFloor_CarrierDropsNoKey_StairStaysShut` (kill the carrier; `GateKey == 0`, `NetStairOpen == false`).
- `Pump_Start_Fill_UnlocksExit` (use a test hook `CellarContentsSync.PumpSecondsOverrideForTests` = 6 s to keep it short; reset in TearDown).
- `Pump_WavesTrimmedAtCap` (pump waves never push live enemies above `GameMetrics.MaxLiveEnemies`; other spawn sources are out of scope).
- `Floor7_ValveOrPump_GatesFinish` (finish refused until done).
- `LateJoiner_SeesValveAndPumpState`.
- `Catches:` these prove the host rules and replication through `ContentsState`; they do not prove the owner's held-input sampling feel or marker visuals.

- [ ] **Step 1:** Write the PlayMode tests (fail). **Step 2:** implement 1-6. **Step 3:** `python3 Tools/cloud/sc_cloud_compile.py`, then `bash Tools/cloud/unity.sh gate --out <scratch>` (makes the `.meta` files; commit them), then `unity.sh test-play --filter "FloorObjectiveNetworkTests|CellarContentsNetworkTests|DiveSpineNetworkTests" --out <scratch>`. **Step 4: Commit** `plan5 task 4: two-valve seal and hold the pump in the live dive`.

### Task 5: Modifier effects

**Files:**
- Modify: `Assets/_Project/Presentation/Cellars/CellarContentsSync.cs` (Crowded/Ambush gold, Stirred extra rolls, Dim rank bonus, Warm Hands recharge, Hushed hunter)
- Modify: `Assets/_Project/Presentation/Cellars/DiveHaulSync.cs` (`FeedStir`: `stir.ModifierBonus = FloorModifierSet.StirredBonusFor(seed, floor)`; `TickCollapseStart`: Hurried passes `pathSec * HurriedCollapseMultiplier / DiveStir.CollapseMultiplier` so `CollapseSeconds` yields the 1.25x; Hurried gold on leaving the floor) — do not touch its bank choice tick or Summary choice fields
- Test: `Assets/_Project/Tests/PlayMode/FloorModifierNetworkTests.cs`; EditMode where the effect is a pure helper

Behaviour (host-authoritative; gold via the existing per-player `ServerAddGold` path; "present player" = connected hero on the floor):
- Crowded / Ambush / Sparse: encounter effects already come through `TwistOf` (Task 2). Add rewards: Crowded +15 gold per present player each time a room becomes cleared (`CellarRoomClear` transition); Ambush +40 gold per present player when the ambush room clears.
- Stirred: `FeedStir` sets `stir.ModifierBonus = FloorModifierSet.StirredBonusFor(seed, floor)`; each chest open also rolls `CellarLoot.RollChest(seed, floor, chestIndex + 1000 * k, playerSlot, rankBonus, ownsBoots)` for k = 1..`StirredExtraRolls` and adds it with the normal bag-full rule (a refused extra roll is just lost, toast "bag full").
- Dim: `rankBonus + DimRankBonus` in chest rolls on the floor (the darkness itself already exists: Decision 2).
- Hurried: collapse factor; +30 gold per present player when the stair/exit is taken from this floor (host, in the floor-leave path in `DiveHaulSync.ServerEnterFloor` or the stair callback; no `DiveDirector` bank code).
- Warm Hands: healing point recharge `WarmHandsRechargeSeconds` on this floor (`DiveHaulSync._healTuning` / `ServerSetHealTuning`, ~:661/:705).
- Hushed: `SpawnHunter` (`CellarContentsSync.cs` ~:677) is skipped on this floor even if latched (the latch itself stays; next floor it can come).
- Tests: `Stirred_AddsBonus_AndExtraRoll`, `Crowded_GoldPerClear_PerPlayer`, `Hurried_CollapseShorter_AndGoldOnLeave`, `Hushed_NoHunter`, `WarmHands_FasterRecharge`, `Dim_RankBonus` (unit-level where the host method can be called directly). `Catches:` host rewards and timers; not visuals of Dim.

- [ ] Steps: tests first (fail), implement, compile, `unity.sh test-play --filter "FloorModifierNetworkTests|DiveHaulNetworkTests|DiveHealingNetworkTests"`, commit `plan5 task 5: floor modifier effects and rewards`.

> **Implementer run B = Tasks 4-5** (`implementer`, effort high: networking). Then **one `risk-reviewer`** over Tasks 4-5 (net + the save-impact claim).

### Task 6: Floor card HUD

**Files:**
- Create: `Assets/_Project/UI/FloorCardHud.cs` (IMGUI, beside `StirMeterHud`)
- Modify: wherever `StirMeterHud` is added to the dive HUD object (builder or runtime `AddComponent`; follow the same path, builder only if it is how `StirMeterHud` is added, then `unity.sh build` and commit only the intended output)
- Test: `Assets/_Project/Tests/PlayMode/FloorCardHudTests.cs` (smoke: card text model)

Behaviour: reads `CellarFloorSync` (seed, floor), `CellarContentsSync` (objective, valves, pump, gate key, piece count/found) and builds `FloorCard.For(...)`; on each floor change shows the full card for 6 s (placeholder const) centred, then a compact panel under the stir meter: name, objective line with progress, one line per modifier (title + reward). Pieces line: "Pieces left here: N". UI never touches Unity Services or transports (`ArchitectureTests`). Text model in a pure helper `FloorCardText` (Task 2) so the PlayMode test only checks the HUD picks it up: `Hud_ShowsFullCardOnArrival_ThenPanel`, `Hud_ProgressFollowsValveMask`.

### Task 7: Bots play the new objectives

**Files:** the dive bot driver used by `DiveBotTests`, `CoopDiveBotTests`, `CoopDiveFastTests`, `CoopSoakTests` (find with `sc_lsp.py symbols DiveBot`). Merge `origin/main` first: the PC session `divebot-pace` is changing bot tests; keep edits to the bot's goal selection.

Behaviour: on a TwoValves floor the bot goes to the nearest unturned valve cell and holds interact until its bit sets, then the next; on HoldPump it walks to the pump, presses, stays inside the ring fighting until done; GateKey unchanged; then the stair as today. Co-op bots may split valves (nearest unturned per bot). If the bot cannot be taught within the run, add a test-only `DiveDirector.ObjectiveOverrideForTests` (GateKey) used by the bot tests and report it as an open item (not preferred).

Tests: the existing bot suites stay green: `unity.sh test-play --filter "DiveBotTests|CoopDiveFastTests"`; `CoopSoak_Quad_Seed12345` runs in `soak`.

> **Implementer run C = Tasks 6-7** (`implementer`, medium). Then `reviewer` on Task 6 and the controller's diff read on Task 7 if small.

### Finish
- [ ] Merge `origin/main` into the branch; `bash Tools/cloud/unity.sh check --out <scratch>` on the tip (PASS marker == HEAD); `bash Tools/cloud/unity.sh soak --out <scratch>` (Core/Cellars changed).
- [ ] `branch-reviewer` over the whole branch (networking + cross-assembly wiring).
- [ ] Merge per `sc-merge` (`--no-ff`, push main). Discard Linux float churn in `.unity`/`.mat`.
- [ ] STATUS.md: merged commit, check line, soak line, open questions, `Archive: ready`.

## Deferred (with reasons)
| Item | Why |
|---|---|
| Swarm objective, damper seal, hold the hearth | step 4 names only the two water objectives; heat twins wait for the heat half dressing |
| Brimming, Fat Larder, Still Air modifiers | need the water gimmick / elite affixes (step 5) / ambient audio |
| Floor map with the card re-openable from it | no map screen exists; the always-on corner panel covers "never missable" |
| Room-state water flip when a valve turns | the water gimmick is step 5 |
| Valve and pump rooms shown on a map once seen | no map screen yet; the card names the objective and the markers are visible in the room |
| Card line for pieces already found | the card shows pieces left; found-pieces history needs the map/recap |
| Capping room-entry spawns, ambush and hunter at 8 live | existing behaviour, not step 4; pump waves are capped |
| Hurried reward as 1.5x push-on stir reward | needs `DiveSession`/`DiveStir` reward accounting another session is changing |
