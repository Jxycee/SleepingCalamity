# Dive Rules in Core: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the signed-off Hearthdeep dive rules as pure Core classes with EditMode tests, without wiring them into the running game.

**Architecture:** Eight small, independent classes in `Assets/_Project/Core/Cellars/`, each with its own test file in `Assets/_Project/Tests/EditMode/`. They sit beside the old 4-floor rules (`DiveRoute`, `DiveState`, `StirMeter`, `DiveHaul`), which the live dive keeps using until plan 3 of `docs/superpowers/plans/2026-11-03-dive-build-roadmap.md` wires the new classes in and retires the old ones. No engine types, no networking, no save changes in this plan.

**Tech Stack:** C# 9 (Unity 6000.6), NUnit 3, the project's `Pcg32` RNG (`Assets/_Project/Core/Random/Pcg32.cs`: `new Pcg32(ulong seed, ulong stream)`, `int Range(int minInclusive, int maxExclusive)`).

## Global Constraints
- Spec: `docs/superpowers/specs/2026-11-03-hearthdeep-dungeon-design.md` (signed off 2026-11-03). Decision numbers below refer to its section 2.
- Core is pure C#: no `UnityEngine`, no MonoBehaviour (`CLAUDE.md`; `ArchitectureTests` enforces it). Namespace `SleepingCalamity.Core`; tests in namespace `SleepingCalamity.Tests.EditMode`.
- Test files may use only `System*`, `NUnit*` and `SleepingCalamity.Core`, so `python3 Tools/cloud/sc_core_tests.py` picks them up and runs them without Unity.
- Every new file starts with a one-line header comment: its path, what it does, and the spec section or decision it implements.
- Numbers marked *placeholder* are tuning starting points; keep them as `public const` so later JSON tuning can replace them.
- Unity needs a `.meta` next to every new `.cs` file. Task 9 generates them with a Unity run; never hand-write GUIDs.
- Don't touch `DiveRoute.cs`, `DiveState.cs`, `StirMeter.cs`, `StirTuning.cs`, `DiveHaul.cs` or anything in `Presentation/` in this plan.
- Branch: work on a fresh branch off `main` (for example `claude/dive-rules-core`). Commit after each task. Merge per `sc-merge` after review and a green `check`.

Test command for every task (cloud or PC with .NET 8): `python3 Tools/cloud/sc_core_tests.py --where "class =~ <TestClass>"`. It prints NUnitLite's summary and exits 0 when all pass.

---

### Task 1: The Undercroft floor plan

**Files:**
- Create: `Assets/_Project/Core/Cellars/UndercroftPlan.cs`
- Test: `Assets/_Project/Tests/EditMode/UndercroftPlanTests.cs`

**Interfaces:**
- Produces: `enum UndercroftFloorKind { Authored, Generated, SetPiece, Boss }`; `static class UndercroftPlan` with `const int FirstFloor = 1, BankFloor = 5, BossFloor = 10, Phase1LastFloor = 7`; `bool IsFloor(int floor)`; `UndercroftFloorKind KindOf(int floor)`; `bool IsFirstHalf(int floor)`; `int NextFloor(int floor, int lastBuiltFloor)` (0 = no stair: the end of the built dive).

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.UndercroftPlan (spec 2026-11-03 section 5: the 10-floor Undercroft).
using System;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class UndercroftPlanTests
    {
        [Test]
        public void Kinds_MatchTheSignedOffFloorTable()
        {
            Assert.That(UndercroftPlan.KindOf(1), Is.EqualTo(UndercroftFloorKind.Authored));
            foreach (int f in new[] { 2, 4, 6, 7 }) Assert.That(UndercroftPlan.KindOf(f), Is.EqualTo(UndercroftFloorKind.Generated), $"floor {f}");
            foreach (int f in new[] { 3, 5, 8, 9 }) Assert.That(UndercroftPlan.KindOf(f), Is.EqualTo(UndercroftFloorKind.SetPiece), $"floor {f}");
            Assert.That(UndercroftPlan.KindOf(10), Is.EqualTo(UndercroftFloorKind.Boss));
        }

        [Test]
        public void KindOf_ThrowsOutsideTheDungeon()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UndercroftPlan.KindOf(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => UndercroftPlan.KindOf(11));
            Assert.That(UndercroftPlan.IsFloor(0), Is.False);
            Assert.That(UndercroftPlan.IsFloor(10), Is.True);
        }

        [Test]
        public void Halves_SplitAtTheBankFloor()
        {
            Assert.That(UndercroftPlan.IsFirstHalf(1), Is.True);
            Assert.That(UndercroftPlan.IsFirstHalf(5), Is.True, "the bank floor closes the first half");
            Assert.That(UndercroftPlan.IsFirstHalf(6), Is.False);
            Assert.That(UndercroftPlan.IsFirstHalf(10), Is.False);
        }

        [Test]
        public void NextFloor_StopsAtTheLastBuiltFloor()
        {
            Assert.That(UndercroftPlan.NextFloor(1, UndercroftPlan.Phase1LastFloor), Is.EqualTo(2));
            Assert.That(UndercroftPlan.NextFloor(6, UndercroftPlan.Phase1LastFloor), Is.EqualTo(7));
            Assert.That(UndercroftPlan.NextFloor(7, UndercroftPlan.Phase1LastFloor), Is.EqualTo(0), "Phase 1 ends after floor 7 (stand-in ending)");
            Assert.That(UndercroftPlan.NextFloor(9, UndercroftPlan.BossFloor), Is.EqualTo(10));
            Assert.That(UndercroftPlan.NextFloor(10, UndercroftPlan.BossFloor), Is.EqualTo(0));
            Assert.That(UndercroftPlan.NextFloor(0, UndercroftPlan.BossFloor), Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ UndercroftPlanTests"`
Expected: build error, "The name 'UndercroftPlan' does not exist in the current context".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/UndercroftPlan.cs: the Undercroft's floor table (spec 2026-11-03 section 5; decisions 2, 26, 28). Pure C#.
// Floor 1 authored, 2/4/6/7 generated, 3/5/8/9 set pieces, 10 the boss. The bank closes the first half on floor 5. Phase 1 builds to floor 7.
using System;

namespace SleepingCalamity.Core
{
    public enum UndercroftFloorKind { Authored, Generated, SetPiece, Boss }

    public static class UndercroftPlan
    {
        public const int FirstFloor = 1, BankFloor = 5, BossFloor = 10, Phase1LastFloor = 7;

        public static bool IsFloor(int floor) => floor >= FirstFloor && floor <= BossFloor;

        public static UndercroftFloorKind KindOf(int floor) => floor switch
        {
            1 => UndercroftFloorKind.Authored,
            2 or 4 or 6 or 7 => UndercroftFloorKind.Generated,
            3 or 5 or 8 or 9 => UndercroftFloorKind.SetPiece,
            10 => UndercroftFloorKind.Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(floor), floor, "the Undercroft has floors 1-10"),
        };

        public static bool IsFirstHalf(int floor) => IsFloor(floor) && floor <= BankFloor;

        // The floor the stair leads to, or 0 at the end of the built dive (the boss floor, or the Phase 1 stand-in ending).
        public static int NextFloor(int floor, int lastBuiltFloor) => IsFloor(floor) && floor < lastBuiltFloor ? floor + 1 : 0;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ UndercroftPlanTests"`
Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/UndercroftPlan.cs Assets/_Project/Tests/EditMode/UndercroftPlanTests.cs
git commit -m "Core: UndercroftPlan floor table (10 floors, bank on 5, Phase 1 to floor 7)"
```

---

### Task 2: The one-time checkpoint and floor pick

**Files:**
- Create: `Assets/_Project/Core/Cellars/DiveCheckpoint.cs`
- Test: `Assets/_Project/Tests/EditMode/DiveCheckpointTests.cs`

**Interfaces:**
- Consumes: `UndercroftPlan.FirstFloor`, `UndercroftPlan.BankFloor` (Task 1).
- Produces: `sealed class DiveCheckpoint` with `bool Held`, `void OnWentHomeFromBank()`, `bool CanStartOn(int floor, bool bossBeaten)`, `bool TryStart(int floor, bool bossBeaten)`, `void Restore(bool held)`.

Rules (decisions 13, 31, 38): with no checkpoint a dive starts on floor 1. Going home from the bank leaves the checkpoint; the next dive may start on any floor from 1 to the bank, and starting uses the checkpoint up whatever floor is picked. Once the boss is beaten, starting at the bank is always allowed (the tithe-gate shortcut) and doesn't use the checkpoint.

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.DiveCheckpoint (spec decisions 13, 31, 38).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveCheckpointTests
    {
        [Test]
        public void WithoutACheckpoint_OnlyFloorOne()
        {
            var cp = new DiveCheckpoint();
            Assert.That(cp.CanStartOn(1, false), Is.True);
            Assert.That(cp.CanStartOn(3, false), Is.False);
            Assert.That(cp.CanStartOn(5, false), Is.False);
            Assert.That(cp.TryStart(5, false), Is.False);
        }

        [Test]
        public void GoingHomeFromTheBank_AllowsAnyFloorUpToTheBank_Once()
        {
            var cp = new DiveCheckpoint();
            cp.OnWentHomeFromBank();
            Assert.That(cp.Held, Is.True);
            for (int f = 1; f <= UndercroftPlan.BankFloor; f++) Assert.That(cp.CanStartOn(f, false), Is.True, $"floor {f}");
            Assert.That(cp.CanStartOn(6, false), Is.False, "never past the bank");
            Assert.That(cp.TryStart(3, false), Is.True);
            Assert.That(cp.Held, Is.False, "starting uses the checkpoint up");
            Assert.That(cp.CanStartOn(3, false), Is.False, "the next dive starts on floor 1 again");
        }

        [Test]
        public void StartingOnFloorOne_AlsoUsesTheCheckpoint()
        {
            var cp = new DiveCheckpoint();
            cp.OnWentHomeFromBank();
            Assert.That(cp.TryStart(1, false), Is.True);
            Assert.That(cp.Held, Is.False);
        }

        [Test]
        public void AfterTheBoss_TheBankIsAlwaysOpen_WithoutUsingTheCheckpoint()
        {
            var cp = new DiveCheckpoint();
            Assert.That(cp.TryStart(UndercroftPlan.BankFloor, true), Is.True);
            cp.OnWentHomeFromBank();
            Assert.That(cp.TryStart(UndercroftPlan.BankFloor, true), Is.True);
            Assert.That(cp.Held, Is.True, "the shortcut doesn't spend the checkpoint");
            Assert.That(cp.CanStartOn(3, true), Is.True, "the held checkpoint still allows a floor pick");
        }

        [Test]
        public void Restore_BringsBackTheSavedState()
        {
            var cp = new DiveCheckpoint();
            cp.Restore(true);
            Assert.That(cp.CanStartOn(4, false), Is.True);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveCheckpointTests"`
Expected: build error, "The type or namespace name 'DiveCheckpoint' could not be found".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/DiveCheckpoint.cs: the halfway bank as a one-time checkpoint with a floor pick (spec decisions 13, 31, 38). Pure C#.
// Going home from the bank leaves it; the next dive may start on floors 1 to the bank and spends it whatever floor it picks. After the boss the bank
// is always open (the tithe-gate shortcut) and starting there spends nothing. The world save stores Held (plan 3).
namespace SleepingCalamity.Core
{
    public sealed class DiveCheckpoint
    {
        public bool Held { get; private set; }

        public void OnWentHomeFromBank() => Held = true;

        public void Restore(bool held) => Held = held;

        public bool CanStartOn(int floor, bool bossBeaten)
        {
            if (floor == UndercroftPlan.FirstFloor) return true;
            if (bossBeaten && floor == UndercroftPlan.BankFloor) return true;
            return Held && floor > UndercroftPlan.FirstFloor && floor <= UndercroftPlan.BankFloor;
        }

        // Starts a dive on `floor`. False (nothing changes) when that floor isn't allowed.
        public bool TryStart(int floor, bool bossBeaten)
        {
            if (!CanStartOn(floor, bossBeaten)) return false;
            bool shortcut = bossBeaten && floor == UndercroftPlan.BankFloor;
            if (!shortcut) Held = false;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveCheckpointTests"`
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/DiveCheckpoint.cs Assets/_Project/Tests/EditMode/DiveCheckpointTests.cs
git commit -m "Core: DiveCheckpoint (one-time halfway checkpoint, floor pick, post-boss shortcut)"
```

---

### Task 3: Bank slots and what can be banked

**Files:**
- Create: `Assets/_Project/Core/Cellars/BankSlots.cs`
- Test: `Assets/_Project/Tests/EditMode/BankSlotsTests.cs`

**Interfaces:**
- Produces: `enum DiveItemKind { Weapon, Usable, Gear, Large, Satchel, Key, Progress }`; `static class BankSlots` with `bool IsBankable(DiveItemKind kind)`, `int TotalSlots(int players)`, `int SlotsFor(int players, int recordedSlot, int extraSlotOwner)`, `int PickExtraSlotOwner(ulong diveSeed, IReadOnlyList<int> presentSlots, int previousOwner)`. Recorded slots are the world's player indices 0-3; `-1` means "none".

Rules (decisions 4, 5, 34): solo 3 slots; 2 players 2 each; 3 players 1 each plus one extra for a player the host picks from the dive seed, never the same player twice in a row; 4 players 1 each. Weapons, usables, gear, larger items and the lost satchel are bankable; keys and progress items never. Gold and bread aren't items here.

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.BankSlots (spec decisions 4, 5, 34).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BankSlotsTests
    {
        [Test]
        public void Bankable_WeaponsUsablesGearLargeAndTheSatchel_NeverKeysOrProgress()
        {
            foreach (var k in new[] { DiveItemKind.Weapon, DiveItemKind.Usable, DiveItemKind.Gear, DiveItemKind.Large, DiveItemKind.Satchel })
                Assert.That(BankSlots.IsBankable(k), Is.True, k.ToString());
            Assert.That(BankSlots.IsBankable(DiveItemKind.Key), Is.False);
            Assert.That(BankSlots.IsBankable(DiveItemKind.Progress), Is.False);
        }

        [Test]
        public void Totals_ByPartySize()
        {
            Assert.That(BankSlots.TotalSlots(1), Is.EqualTo(3));
            Assert.That(BankSlots.TotalSlots(2), Is.EqualTo(4));
            Assert.That(BankSlots.TotalSlots(3), Is.EqualTo(4));
            Assert.That(BankSlots.TotalSlots(4), Is.EqualTo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => BankSlots.TotalSlots(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BankSlots.TotalSlots(5));
        }

        [Test]
        public void PerPlayer_ThreePlayersGetOneEach_PlusTheExtraForItsOwner()
        {
            Assert.That(BankSlots.SlotsFor(1, 0, -1), Is.EqualTo(3));
            Assert.That(BankSlots.SlotsFor(2, 1, -1), Is.EqualTo(2));
            Assert.That(BankSlots.SlotsFor(3, 2, 2), Is.EqualTo(2));
            Assert.That(BankSlots.SlotsFor(3, 0, 2), Is.EqualTo(1));
            Assert.That(BankSlots.SlotsFor(4, 3, -1), Is.EqualTo(1));
        }

        [Test]
        public void ExtraSlot_IsDeterministic_AndNeverTheSameOwnerTwiceInARow()
        {
            var present = new List<int> { 0, 2, 3 };
            for (ulong seed = 1; seed <= 500; seed++)
            {
                int a = BankSlots.PickExtraSlotOwner(seed, present, 2);
                Assert.That(a, Is.Not.EqualTo(2), $"seed {seed}");
                Assert.That(present, Does.Contain(a));
                Assert.That(BankSlots.PickExtraSlotOwner(seed, present, 2), Is.EqualTo(a), "same seed, same pick");
            }
        }

        [Test]
        public void ExtraSlot_WithNoPreviousOwner_CanPickAnyone()
        {
            var present = new List<int> { 0, 1, 2 };
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 200; seed++) seen.Add(BankSlots.PickExtraSlotOwner(seed, present, -1));
            Assert.That(seen, Is.EquivalentTo(present));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ BankSlotsTests"`
Expected: build error, "The name 'BankSlots' does not exist in the current context".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/BankSlots.cs: bank slots by party size and what may be banked (spec decisions 4, 5, 34). Pure C#.
// Solo 3; 2p 2 each; 3p 1 each plus one extra for a player picked from the dive seed (never the previous owner; the world save keeps it, plan 3); 4p 1 each.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum DiveItemKind { Weapon, Usable, Gear, Large, Satchel, Key, Progress }

    public static class BankSlots
    {
        public const ulong ExtraSlotStream = 0xB5;   // keeps this pick independent of other rolls on the same dive seed

        public static bool IsBankable(DiveItemKind kind) => kind != DiveItemKind.Key && kind != DiveItemKind.Progress;

        public static int TotalSlots(int players) => players switch
        {
            1 => 3,
            2 or 3 or 4 => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(players), players, "1-4 players"),
        };

        public static int SlotsFor(int players, int recordedSlot, int extraSlotOwner) => players switch
        {
            1 => 3,
            2 => 2,
            3 => recordedSlot == extraSlotOwner ? 2 : 1,
            4 => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(players), players, "1-4 players"),
        };

        // The 3-player extra slot's owner: a seeded pick among the present players, skipping the previous owner (-1 = none).
        public static int PickExtraSlotOwner(ulong diveSeed, IReadOnlyList<int> presentSlots, int previousOwner)
        {
            var candidates = new List<int>();
            foreach (int s in presentSlots) if (s != previousOwner) candidates.Add(s);
            if (candidates.Count == 0) candidates.AddRange(presentSlots);
            var rng = new Pcg32(diveSeed, ExtraSlotStream);
            return candidates[rng.Range(0, candidates.Count)];
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ BankSlotsTests"`
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/BankSlots.cs Assets/_Project/Tests/EditMode/BankSlotsTests.cs
git commit -m "Core: BankSlots (slots by party size, 3-player extra slot pick, bankable kinds)"
```

---

### Task 4: Failure: dive gold and the lost satchel

**Files:**
- Create: `Assets/_Project/Core/Cellars/LostSatchel.cs`
- Test: `Assets/_Project/Tests/EditMode/LostSatchelTests.cs`

**Interfaces:**
- Produces: `static class DiveGold` with `int KeptOnFail(int earnedThisDive)`; `sealed class LostSatchel` with `int Floor`, `IReadOnlyList<int> Items`; `sealed class SatchelSlot` (one per player) with `LostSatchel Waiting`, `LostSatchel Carried`, `void OnFail(int floor, IReadOnlyList<int> unbankedItems)`, `bool ShouldPlaceOn(int floor)`, `bool PickUp()`, `IReadOnlyList<int> EmptyOutsideTheDungeon()`, `void Restore(LostSatchel waiting)`.

Rules (decisions 7, 33-35, 39): a failed dive keeps half the gold earned in it, rounded down; gold already owned isn't touched (the caller only passes this dive's earnings). The unbanked loot becomes one satchel at the floor depth where the player fell. It is one item, can't be opened in the dungeon, and turns back into its items only outside it (home from the bank, the end, or banked), into town storage. Failing again while carrying it loses it for good. One satchel per player at a time; a new failure with items replaces a waiting satchel. *Interpretation (flag in review):* a failure with nothing unbanked leaves an already waiting satchel where it is.

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.DiveGold and LostSatchel/SatchelSlot (spec decisions 7, 33-35).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class LostSatchelTests
    {
        [Test]
        public void DiveGold_HalvesWhatTheDiveEarned_RoundingDown()
        {
            Assert.That(DiveGold.KeptOnFail(101), Is.EqualTo(50));
            Assert.That(DiveGold.KeptOnFail(0), Is.EqualTo(0));
            Assert.That(DiveGold.KeptOnFail(-5), Is.EqualTo(0));
        }

        [Test]
        public void Failing_LeavesTheUnbankedItemsAsOneSatchelAtThatDepth()
        {
            var slot = new SatchelSlot();
            slot.OnFail(6, new List<int> { 11, 12, 12 });
            Assert.That(slot.Waiting, Is.Not.Null);
            Assert.That(slot.Waiting.Floor, Is.EqualTo(6));
            Assert.That(slot.Waiting.Items, Is.EqualTo(new[] { 11, 12, 12 }));
            Assert.That(slot.ShouldPlaceOn(6), Is.True);
            Assert.That(slot.ShouldPlaceOn(4), Is.False);
        }

        [Test]
        public void PickedUp_ItTurnsBackIntoItemsOnlyOutsideTheDungeon()
        {
            var slot = new SatchelSlot();
            slot.OnFail(4, new List<int> { 7 });
            Assert.That(slot.PickUp(), Is.True);
            Assert.That(slot.Waiting, Is.Null);
            Assert.That(slot.Carried, Is.Not.Null);
            Assert.That(slot.ShouldPlaceOn(4), Is.False, "it isn't placed again once carried");
            Assert.That(slot.EmptyOutsideTheDungeon(), Is.EqualTo(new[] { 7 }));
            Assert.That(slot.Carried, Is.Null);
            Assert.That(slot.EmptyOutsideTheDungeon(), Is.Empty);
        }

        [Test]
        public void FailingWhileCarryingIt_LosesItForGood_AndLeavesTheNewHaul()
        {
            var slot = new SatchelSlot();
            slot.OnFail(4, new List<int> { 7 });
            slot.PickUp();
            slot.OnFail(6, new List<int> { 9 });
            Assert.That(slot.Carried, Is.Null);
            Assert.That(slot.Waiting.Items, Is.EqualTo(new[] { 9 }), "the old satchel's items are gone");
        }

        [Test]
        public void ANewFailure_ReplacesAWaitingSatchel_UnlessItDroppedNothing()
        {
            var slot = new SatchelSlot();
            slot.OnFail(2, new List<int> { 1 });
            slot.OnFail(7, new List<int> { 2 });
            Assert.That(slot.Waiting.Floor, Is.EqualTo(7));
            slot.OnFail(3, new List<int>());
            Assert.That(slot.Waiting.Floor, Is.EqualTo(7), "an empty failure leaves the waiting satchel where it is");
        }

        [Test]
        public void PickUp_FailsWhenNothingWaits()
        {
            Assert.That(new SatchelSlot().PickUp(), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ LostSatchelTests"`
Expected: build error, "The name 'DiveGold' does not exist in the current context".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/LostSatchel.cs: what a failed dive costs (spec decisions 7, 33-35, 39). Pure C#.
// DiveGold: half the dive's earnings kept. SatchelSlot (one per player): the unbanked haul waits as one satchel at the depth where the player fell;
// it opens only outside the dungeon (into town storage); failing while carrying it loses it; one satchel at a time.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class DiveGold
    {
        public static int KeptOnFail(int earnedThisDive) => Math.Max(0, earnedThisDive) / 2;
    }

    public sealed class LostSatchel
    {
        public int Floor { get; }
        public IReadOnlyList<int> Items { get; }

        public LostSatchel(int floor, IEnumerable<int> items)
        {
            Floor = floor;
            Items = new List<int>(items).AsReadOnly();
        }
    }

    public sealed class SatchelSlot
    {
        private static readonly IReadOnlyList<int> None = new List<int>().AsReadOnly();

        public LostSatchel Waiting { get; private set; }
        public LostSatchel Carried { get; private set; }

        public void Restore(LostSatchel waiting) { Waiting = waiting; Carried = null; }

        // The player's dive failed on `floor` with these items unbanked (the satchel itself never counts as one).
        public void OnFail(int floor, IReadOnlyList<int> unbankedItems)
        {
            Carried = null;
            if (unbankedItems != null && unbankedItems.Count > 0) Waiting = new LostSatchel(floor, unbankedItems);
        }

        public bool ShouldPlaceOn(int floor) => Waiting != null && Waiting.Floor == floor;

        public bool PickUp()
        {
            if (Waiting == null) return false;
            Carried = Waiting;
            Waiting = null;
            return true;
        }

        // The carried satchel left the dungeon (home from the bank, the end, or banked in a slot): its items, for town storage.
        public IReadOnlyList<int> EmptyOutsideTheDungeon()
        {
            IReadOnlyList<int> items = Carried?.Items ?? None;
            Carried = null;
            return items;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ LostSatchelTests"`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/LostSatchel.cs Assets/_Project/Tests/EditMode/LostSatchelTests.cs
git commit -m "Core: DiveGold and the lost satchel (one per player, opens outside the dungeon)"
```

---

### Task 5: Momentum and the paid boss retry

**Files:**
- Create: `Assets/_Project/Core/Cellars/DiveMomentum.cs`
- Test: `Assets/_Project/Tests/EditMode/DiveMomentumTests.cs`

**Interfaces:**
- Consumes: `UndercroftPlan.FirstFloor`, `BankFloor`, `BossFloor` (Task 1).
- Produces: `sealed class DiveMomentum` with `bool Unbroken`, `bool PushedOn`, `void OnDiveStart(int startFloor)`, `void OnPushOn()`, `int ChestRankBonus(int floor)`, `bool EndChestRoomOpens(bool bossBeaten)`; `enum RetryPrice { None, Idol, Gold }`; `sealed class BossRetry` with `const int GoldPrice`, `bool UsedThisDive`, `bool IdolActivated`, `void OnDiveStart()`, `void ActivateIdol()`, `RetryPrice PriceFor(int floor, int gold)`, `bool TryRetry(int floor, ref int gold)`.

Rules (decisions 25, 32, 36, 37): a party that walked down to the bank this dive and pushes on gets one chest rank more on every second-half floor. An extra chest room opens after the boss only for an unbroken dive (started on floor 1, pushed on, beat the boss). One retry per dive, at the boss only (never the floor-5 guardian): free if the party activated the hidden idol this dive, otherwise it costs gold (*placeholder* 500).

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.DiveMomentum and BossRetry (spec decisions 25, 32, 36, 37).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveMomentumTests
    {
        [Test]
        public void PushingOnAfterWalkingToTheBank_RaisesSecondHalfChests()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(1);
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(0), "not before pushing on");
            m.OnPushOn();
            Assert.That(m.ChestRankBonus(4), Is.EqualTo(0), "first-half floors never get it");
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(1));
            Assert.That(m.ChestRankBonus(10), Is.EqualTo(1));
        }

        [Test]
        public void StartingAtTheBank_StartsCold()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(UndercroftPlan.BankFloor);
            m.OnPushOn();
            Assert.That(m.PushedOn, Is.False);
            Assert.That(m.ChestRankBonus(7), Is.EqualTo(0));
        }

        [Test]
        public void TheEndChestRoom_NeedsAnUnbrokenDiveFromFloorOne()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(3);
            m.OnPushOn();
            Assert.That(m.ChestRankBonus(6), Is.EqualTo(1), "a floor-pick start still earns momentum");
            Assert.That(m.EndChestRoomOpens(true), Is.False, "but not the unbroken-dive room");

            m.OnDiveStart(1);
            m.OnPushOn();
            Assert.That(m.EndChestRoomOpens(false), Is.False);
            Assert.That(m.EndChestRoomOpens(true), Is.True);
        }

        [Test]
        public void BossRetry_OnlyAtTheBoss_OncePerDive()
        {
            var r = new BossRetry();
            r.OnDiveStart();
            int gold = 1000;
            Assert.That(r.PriceFor(UndercroftPlan.BankFloor, gold), Is.EqualTo(RetryPrice.None), "no retry for the floor-5 guardian");
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.True);
            Assert.That(gold, Is.EqualTo(1000 - BossRetry.GoldPrice));
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.False, "once per dive");
            r.OnDiveStart();
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, gold), Is.EqualTo(RetryPrice.Gold), "a new dive gets a new retry");
        }

        [Test]
        public void BossRetry_TheIdolPaysFirst_GoldIsTheFallback()
        {
            var r = new BossRetry();
            r.OnDiveStart();
            r.ActivateIdol();
            int gold = 0;
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, gold), Is.EqualTo(RetryPrice.Idol));
            Assert.That(r.TryRetry(UndercroftPlan.BossFloor, ref gold), Is.True);
            Assert.That(gold, Is.EqualTo(0), "the idol costs no gold");

            r.OnDiveStart();
            Assert.That(r.IdolActivated, Is.False, "the idol is found again each dive");
            Assert.That(r.PriceFor(UndercroftPlan.BossFloor, BossRetry.GoldPrice - 1), Is.EqualTo(RetryPrice.None), "too little gold and no idol");
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveMomentumTests"`
Expected: build error, "The type or namespace name 'DiveMomentum' could not be found".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/DiveMomentum.cs: why push on, and the paid boss retry (spec decisions 25, 32, 36, 37). Pure C#.
// Momentum: walk down to the bank and push on, and every second-half chest rolls one rank higher; an unbroken dive (from floor 1) also opens the
// extra chest room after the boss. BossRetry: one per dive at the boss only; the idol found this dive pays, gold is the fallback.
namespace SleepingCalamity.Core
{
    public sealed class DiveMomentum
    {
        private bool _walkedToBank;

        public bool Unbroken { get; private set; }
        public bool PushedOn { get; private set; }

        public void OnDiveStart(int startFloor)
        {
            Unbroken = startFloor == UndercroftPlan.FirstFloor;
            _walkedToBank = startFloor < UndercroftPlan.BankFloor;
            PushedOn = false;
        }

        // The party chose "push on" at the bank. A dive that started at the bank starts cold.
        public void OnPushOn() { if (_walkedToBank) PushedOn = true; }

        public int ChestRankBonus(int floor) => PushedOn && floor > UndercroftPlan.BankFloor ? 1 : 0;

        public bool EndChestRoomOpens(bool bossBeaten) => bossBeaten && Unbroken && PushedOn;
    }

    public enum RetryPrice { None, Idol, Gold }

    public sealed class BossRetry
    {
        public const int GoldPrice = 500;   // placeholder

        public bool UsedThisDive { get; private set; }
        public bool IdolActivated { get; private set; }

        public void OnDiveStart() { UsedThisDive = false; IdolActivated = false; }

        public void ActivateIdol() => IdolActivated = true;

        public RetryPrice PriceFor(int floor, int gold)
        {
            if (floor != UndercroftPlan.BossFloor || UsedThisDive) return RetryPrice.None;
            if (IdolActivated) return RetryPrice.Idol;
            return gold >= GoldPrice ? RetryPrice.Gold : RetryPrice.None;
        }

        // Pays and spends the dive's retry. False (nothing changes) when no retry is available.
        public bool TryRetry(int floor, ref int gold)
        {
            RetryPrice price = PriceFor(floor, gold);
            if (price == RetryPrice.None) return false;
            if (price == RetryPrice.Gold) gold -= GoldPrice;
            else IdolActivated = false;
            UsedThisDive = true;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveMomentumTests"`
Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/DiveMomentum.cs Assets/_Project/Tests/EditMode/DiveMomentumTests.cs
git commit -m "Core: DiveMomentum and BossRetry (second-half chest luck, end chest room, idol or gold retry)"
```

---

### Task 6: Stir per half

**Files:**
- Create: `Assets/_Project/Core/Cellars/DiveStir.cs`
- Test: `Assets/_Project/Tests/EditMode/DiveStirTests.cs`

**Interfaces:**
- Consumes: `UndercroftPlan.IsFloor` (Task 1).
- Produces: `sealed class DiveStir` with constants `PerFloorCleared = 10f`, `PerUnbankedValuable = 5f`, `Max = 100f`, `ExtraEnemyThreshold = 40f`, `HunterThreshold = 70f`, `DeepClosesThreshold = 100f`, `CollapseMultiplier = 1.5f`, `CollapseMinSeconds = 60f`, `RewardRankStep = 35f`; properties `int PlayersPresent`, `int UnbankedValuables`, `float Value`, `float RewardValue`, `int RewardRankBonus`, `bool ExtraEnemyLatched`, `bool HunterLatched`, `bool CollapsePending`, `bool CollapseRunning`, `float CollapseSecondsLeft`; methods `bool OnFloorCleared(int floor)`, `void BeginCollapse(float pathSecondsToStair)`, `void OnStairTaken()`, `void OnPushOn()`, `void ResetForNewDive()`, `bool Tick(float dt)`; static `float CollapseSeconds(float pathSecondsToStair)`.

Rules (decision 19, section 4.5 with red-team F2): value = (10 × floors cleared this half + 5 × unbanked valuables carried) × √players, clamped to 0-100. Latches at 40 (one more enemy per room) and 70 (the hunter) stay on for the half. At 100 the deep closes once per half: the host measures the path time to this floor's stair and starts a countdown of max(60 s, 1.5 × path); taking the stair in time stops it; reaching 0 fails the dive. Pushing on from the bank resets the danger side (floors, latches, collapse) but carries the reward side: `RewardValue` = carried + current, and every 35 reward points add one chest rank (*placeholders*).

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.DiveStir (spec decision 19, section 4.5, red-team F2).
using System;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveStirTests
    {
        [Test]
        public void Value_FloorsAndValuables_TimesTheSquareRootOfPlayers()
        {
            var s = new DiveStir();
            s.OnFloorCleared(1);
            s.OnFloorCleared(2);
            s.UnbankedValuables = 2;
            Assert.That(s.Value, Is.EqualTo(30f).Within(0.001f));
            s.PlayersPresent = 4;
            Assert.That(s.Value, Is.EqualTo(60f).Within(0.001f), "x2 for 4 players, not x1.75");
            Assert.That(s.OnFloorCleared(2), Is.False, "a floor counts once");
        }

        [Test]
        public void Latches_StayOnForTheHalf()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 8;
            Assert.That(s.ExtraEnemyLatched, Is.True);
            Assert.That(s.HunterLatched, Is.False);
            s.UnbankedValuables = 0;
            Assert.That(s.ExtraEnemyLatched, Is.True, "latched even after banking items away");
        }

        [Test]
        public void TheDeepCloses_AimsAtThisFloorsStair_OncePerHalf()
        {
            Assert.That(DiveStir.CollapseSeconds(20f), Is.EqualTo(60f), "never under 60 s");
            Assert.That(DiveStir.CollapseSeconds(80f), Is.EqualTo(120f));

            var s = new DiveStir();
            s.UnbankedValuables = 20;
            Assert.That(s.CollapsePending, Is.True);
            s.BeginCollapse(80f);
            Assert.That(s.CollapseRunning, Is.True);
            Assert.That(s.CollapseSecondsLeft, Is.EqualTo(120f));
            s.OnStairTaken();
            Assert.That(s.CollapseRunning, Is.False);
            Assert.That(s.CollapsePending, Is.False, "escaped once: no second collapse this half");
        }

        [Test]
        public void Tick_FailsWhenTheCountdownRunsOut()
        {
            var s = new DiveStir();
            s.UnbankedValuables = 20;
            s.BeginCollapse(10f);
            Assert.That(s.Tick(59f), Is.False);
            Assert.That(s.Tick(1.5f), Is.True);
            Assert.That(s.CollapseRunning, Is.False);
        }

        [Test]
        public void PushingOn_ResetsTheDanger_ButCarriesTheReward()
        {
            var s = new DiveStir();
            for (int f = 1; f <= 5; f++) s.OnFloorCleared(f);
            s.UnbankedValuables = 4;
            Assert.That(s.Value, Is.EqualTo(70f).Within(0.001f));
            Assert.That(s.RewardRankBonus, Is.EqualTo(2));
            s.UnbankedValuables = 0;   // banked at the bank room
            s.OnPushOn();
            Assert.That(s.Value, Is.EqualTo(0f));
            Assert.That(s.HunterLatched, Is.False);
            Assert.That(s.RewardValue, Is.EqualTo(50f).Within(0.001f), "the reward side starts where the first half left it (value when pushing on)");
            s.OnFloorCleared(6);
            Assert.That(s.RewardValue, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void ResetForNewDive_ClearsEverything()
        {
            var s = new DiveStir();
            s.OnFloorCleared(1);
            s.OnPushOn();
            s.ResetForNewDive();
            Assert.That(s.RewardValue, Is.EqualTo(0f));
            Assert.That(s.Value, Is.EqualTo(0f));
        }
    }
}
```

Note for the implementer: in `PushingOn_ResetsTheDanger_ButCarriesTheReward` the valuables are set to 0 before `OnPushOn`, so the carried reward is 5 floors × 10 = 50.

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveStirTests"`
Expected: build error, "The type or namespace name 'DiveStir' could not be found".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/DiveStir.cs: the stir meter for the 10-floor dive (spec decision 19, section 4.5, red-team F2). Pure C#; time comes in as Tick(dt).
// Value = clamp((PerFloorCleared x floors cleared this half + PerUnbankedValuable x valuables carried) x sqrt(players), 0, Max).
// Latches hold for the half. At DeepClosesThreshold the host calls BeginCollapse with the path time to this floor's stair; once per half.
// Pushing on resets the danger side and carries the reward side (RewardValue). Replaces StirMeter in plan 3.
using System;

namespace SleepingCalamity.Core
{
    public sealed class DiveStir
    {
        public const float PerFloorCleared = 10f, PerUnbankedValuable = 5f, Max = 100f;               // placeholders
        public const float ExtraEnemyThreshold = 40f, HunterThreshold = 70f, DeepClosesThreshold = 100f;
        public const float CollapseMultiplier = 1.5f, CollapseMinSeconds = 60f, RewardRankStep = 35f;

        private int _floorMask;   // floors cleared this half (bit f-1)
        private int _players = 1, _valuables;
        private bool _extra, _hunter, _collapseUsed, _running;
        private float _secondsLeft, _rewardCarry;

        public int PlayersPresent { get => _players; set { _players = Math.Max(1, value); Refresh(); } }
        public int UnbankedValuables { get => _valuables; set { _valuables = Math.Max(0, value); Refresh(); } }

        public float Value
        {
            get
            {
                int floors = 0;
                for (int m = _floorMask; m != 0; m >>= 1) floors += m & 1;
                float raw = (PerFloorCleared * floors + PerUnbankedValuable * _valuables) * (float)Math.Sqrt(_players);
                return Math.Max(0f, Math.Min(Max, raw));
            }
        }

        public float RewardValue => Math.Min(Max, _rewardCarry + Value);
        public int RewardRankBonus => (int)(RewardValue / RewardRankStep);
        public bool ExtraEnemyLatched { get { Refresh(); return _extra; } }
        public bool HunterLatched { get { Refresh(); return _hunter; } }
        public bool CollapsePending => !_collapseUsed && !_running && Value >= DeepClosesThreshold;
        public bool CollapseRunning => _running;
        public float CollapseSecondsLeft => _running ? _secondsLeft : 0f;

        public static float CollapseSeconds(float pathSecondsToStair) => Math.Max(CollapseMinSeconds, CollapseMultiplier * pathSecondsToStair);

        public bool OnFloorCleared(int floor)
        {
            if (!UndercroftPlan.IsFloor(floor)) return false;
            int bit = 1 << (floor - 1);
            if ((_floorMask & bit) != 0) return false;
            _floorMask |= bit;
            Refresh();
            return true;
        }

        public void BeginCollapse(float pathSecondsToStair)
        {
            if (!CollapsePending) return;
            _running = true;
            _collapseUsed = true;
            _secondsLeft = CollapseSeconds(pathSecondsToStair);
        }

        public void OnStairTaken() { _running = false; _secondsLeft = 0f; }

        public void OnPushOn()
        {
            _rewardCarry = RewardValue;
            _floorMask = 0;
            _extra = _hunter = _collapseUsed = _running = false;
            _secondsLeft = 0f;
        }

        public void ResetForNewDive()
        {
            OnPushOn();
            _rewardCarry = 0f;
            _valuables = 0;
        }

        // True when the countdown just reached 0: the host fails the dive.
        public bool Tick(float dt)
        {
            if (!_running) return false;
            _secondsLeft -= dt;
            if (_secondsLeft > 0f) return false;
            _running = false;
            _secondsLeft = 0f;
            return true;
        }

        private void Refresh()
        {
            float v = Value;
            if (v >= ExtraEnemyThreshold) _extra = true;
            if (v >= HunterThreshold) _hunter = true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ DiveStirTests"`
Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/DiveStir.cs Assets/_Project/Tests/EditMode/DiveStirTests.cs
git commit -m "Core: DiveStir (stir per half, sqrt player scaling, collapse aimed at this floor's stair, carried reward)"
```

---

### Task 7: Piece and people depths (Atla-style)

**Files:**
- Create: `Assets/_Project/Core/Cellars/PieceDepths.cs`
- Test: `Assets/_Project/Tests/EditMode/PieceDepthsTests.cs`

**Interfaces:**
- Consumes: `UndercroftPlan.KindOf`, `IsFirstHalf`, `BankFloor`, `BossFloor` (Task 1).
- Produces: `readonly struct PieceSpec { int Id; int FixedFloor; bool SecondHalf; }` with constructor `PieceSpec(int id, int fixedFloor, bool secondHalf)` (FixedFloor 0 = a random floor in its half); `static class PieceDepths` with `const ulong Stream`, `IReadOnlyList<int> CandidateFloors(bool secondHalf)`, `IReadOnlyDictionary<int, int> Assign(ulong worldSeed, IReadOnlyList<PieceSpec> pieces)`.

Rules (decision 10, section 4.4): when the world is created each piece or person gets a fixed floor, or a random floor in the first or second half; the world save stores the result (plan 3). Random picks only land on authored or generated floors: set-piece floors hold only their own rescue, and the boss floor holds none. Fixed floors may be any floor (the baker on floor 3 is fixed). Floors are rebuilt every dive, but a piece's depth never changes.

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.PieceDepths (spec decision 10, section 4.4).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class PieceDepthsTests
    {
        [Test]
        public void Candidates_AreTheNonSetPieceFloorsOfEachHalf()
        {
            Assert.That(PieceDepths.CandidateFloors(false), Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(PieceDepths.CandidateFloors(true), Is.EqualTo(new[] { 6, 7 }));
        }

        [Test]
        public void Assign_KeepsFixedFloors_AndPutsRandomOnesInTheirHalf()
        {
            var pieces = new List<PieceSpec>
            {
                new PieceSpec(1, 3, false),      // the baker: fixed on floor 3
                new PieceSpec(2, 0, false),
                new PieceSpec(3, 0, true),
            };
            for (ulong seed = 1; seed <= 300; seed++)
            {
                IReadOnlyDictionary<int, int> d = PieceDepths.Assign(seed, pieces);
                Assert.That(d[1], Is.EqualTo(3));
                Assert.That(PieceDepths.CandidateFloors(false), Does.Contain(d[2]), $"seed {seed}");
                Assert.That(PieceDepths.CandidateFloors(true), Does.Contain(d[3]), $"seed {seed}");
            }
        }

        [Test]
        public void Assign_IsDeterministicPerWorldSeed_AndVariesAcrossSeeds()
        {
            var pieces = new List<PieceSpec>();
            for (int i = 0; i < 6; i++) pieces.Add(new PieceSpec(10 + i, 0, false));
            Assert.That(PieceDepths.Assign(42, pieces), Is.EqualTo(PieceDepths.Assign(42, pieces)));
            var floorsSeen = new HashSet<int>();
            for (ulong seed = 1; seed <= 50; seed++) foreach (int f in PieceDepths.Assign(seed, pieces).Values) floorsSeen.Add(f);
            Assert.That(floorsSeen, Is.EquivalentTo(new[] { 1, 2, 4 }));
        }

        [Test]
        public void Assign_RejectsBadSpecs()
        {
            Assert.Throws<ArgumentException>(() => PieceDepths.Assign(1, new List<PieceSpec> { new PieceSpec(1, 11, false) }));
            Assert.Throws<ArgumentException>(() => PieceDepths.Assign(1, new List<PieceSpec> { new PieceSpec(1, 0, false), new PieceSpec(1, 0, true) }));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ PieceDepthsTests"`
Expected: build error, "The type or namespace name 'PieceSpec' could not be found".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/PieceDepths.cs: which floor depth each piece and person lives on, decided once per world (spec decision 10, section 4.4; DC1 Atla style). Pure C#.
// Fixed floors are kept; random ones land on an authored or generated floor of their half (never a set piece or the boss). Deterministic per world seed.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct PieceSpec
    {
        public readonly int Id;
        public readonly int FixedFloor;    // 0 = a random floor in its half
        public readonly bool SecondHalf;   // which half a random piece lands in

        public PieceSpec(int id, int fixedFloor, bool secondHalf) { Id = id; FixedFloor = fixedFloor; SecondHalf = secondHalf; }
    }

    public static class PieceDepths
    {
        public const ulong Stream = 0xA71A;   // keeps piece depths independent of other rolls on the world seed

        public static IReadOnlyList<int> CandidateFloors(bool secondHalf)
        {
            var floors = new List<int>();
            for (int f = UndercroftPlan.FirstFloor; f <= UndercroftPlan.BossFloor; f++)
            {
                if (UndercroftPlan.IsFirstHalf(f) == secondHalf) continue;
                UndercroftFloorKind kind = UndercroftPlan.KindOf(f);
                if (kind == UndercroftFloorKind.Authored || kind == UndercroftFloorKind.Generated) floors.Add(f);
            }
            return floors;
        }

        public static IReadOnlyDictionary<int, int> Assign(ulong worldSeed, IReadOnlyList<PieceSpec> pieces)
        {
            var rng = new Pcg32(worldSeed, Stream);
            IReadOnlyList<int> first = CandidateFloors(false), second = CandidateFloors(true);
            var result = new SortedDictionary<int, int>();
            foreach (PieceSpec p in pieces)
            {
                if (result.ContainsKey(p.Id)) throw new ArgumentException($"piece {p.Id} appears twice", nameof(pieces));
                if (p.FixedFloor != 0)
                {
                    if (!UndercroftPlan.IsFloor(p.FixedFloor)) throw new ArgumentException($"piece {p.Id}: floor {p.FixedFloor} is outside the Undercroft", nameof(pieces));
                    result[p.Id] = p.FixedFloor;
                    continue;
                }
                IReadOnlyList<int> pool = p.SecondHalf ? second : first;
                result[p.Id] = pool[rng.Range(0, pool.Count)];
            }
            return result;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ PieceDepthsTests"`
Expected: `Passed: 4, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/PieceDepths.cs Assets/_Project/Tests/EditMode/PieceDepthsTests.cs
git commit -m "Core: PieceDepths (Atla-style floor depths per world seed)"
```

---

### Task 8: The bread ladder

**Files:**
- Create: `Assets/_Project/Core/Cellars/BreadLadder.cs`
- Test: `Assets/_Project/Tests/EditMode/BreadLadderTests.cs`

**Interfaces:**
- Produces: `static class BreadLadder` with `const int SurvivorsHeal = 15, BakerHeal = 30, HealPerUpgrade = 10, StackCap = 10, FreeLoavesPerDive = 3`; `int HealPerLoaf(bool bakerHome, int bakerUpgrades)`; `int LoavesAtDiveStart(int extraBaked)`.

Rules (decisions 20, 27): before the baker is home the free loaves are the survivors' poor bread (15 HP, half today's 30 HP loaf); after, his bread heals 30, and each baker upgrade adds 10 (*placeholders*). 3 free loaves per dive (3-5 to tune), plus extra the baker baked from gathered materials, capped at a stack of 10. Potions don't exist yet, so the "a basic potion never beats the best bread" rule waits for the potion work (YAGNI).

- [ ] **Step 1: Write the failing test**

```csharp
// Tests for SleepingCalamity.Core.BreadLadder (spec decisions 20, 27).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BreadLadderTests
    {
        [Test]
        public void SurvivorsBread_IsWeakerThanTheBakers()
        {
            Assert.That(BreadLadder.HealPerLoaf(false, 0), Is.EqualTo(15));
            Assert.That(BreadLadder.HealPerLoaf(false, 3), Is.EqualTo(15), "upgrades need the baker");
            Assert.That(BreadLadder.HealPerLoaf(true, 0), Is.EqualTo(30));
        }

        [Test]
        public void BakerUpgrades_RaiseTheHeal()
        {
            Assert.That(BreadLadder.HealPerLoaf(true, 2), Is.EqualTo(50));
            Assert.That(BreadLadder.HealPerLoaf(true, -1), Is.EqualTo(30));
        }

        [Test]
        public void Carried_IsFreePlusExtra_CappedAtTen()
        {
            Assert.That(BreadLadder.LoavesAtDiveStart(0), Is.EqualTo(3));
            Assert.That(BreadLadder.LoavesAtDiveStart(4), Is.EqualTo(7));
            Assert.That(BreadLadder.LoavesAtDiveStart(20), Is.EqualTo(BreadLadder.StackCap));
            Assert.That(BreadLadder.LoavesAtDiveStart(-2), Is.EqualTo(3));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ BreadLadderTests"`
Expected: build error, "The name 'BreadLadder' does not exist in the current context".

- [ ] **Step 3: Write minimal implementation**

```csharp
// Core/Cellars/BreadLadder.cs: bread as the free, upgradable heal (spec decisions 20, 27; section 4.8). Pure C#.
// Survivors' bread before the baker, his bread after, +HealPerUpgrade per baker upgrade; free loaves plus baked extras, capped at StackCap.
// Replaces HeroVitals.LoafHeal and BakerService's loaf count in plan 3.
using System;

namespace SleepingCalamity.Core
{
    public static class BreadLadder
    {
        public const int SurvivorsHeal = 15, BakerHeal = 30, HealPerUpgrade = 10;   // placeholders
        public const int StackCap = 10, FreeLoavesPerDive = 3;                      // free loaves: 3-5 to tune

        public static int HealPerLoaf(bool bakerHome, int bakerUpgrades) =>
            bakerHome ? BakerHeal + HealPerUpgrade * Math.Max(0, bakerUpgrades) : SurvivorsHeal;

        public static int LoavesAtDiveStart(int extraBaked) => Math.Min(StackCap, FreeLoavesPerDive + Math.Max(0, extraBaked));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python3 Tools/cloud/sc_core_tests.py --where "class =~ BreadLadderTests"`
Expected: `Passed: 3, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Core/Cellars/BreadLadder.cs Assets/_Project/Tests/EditMode/BreadLadderTests.cs
git commit -m "Core: BreadLadder (survivors' bread, baker's bread, upgrades, stack of 10)"
```

---

### Task 9: Whole-suite run, Unity metas and the gate

**Files:**
- Create (generated by Unity, never by hand): the `.meta` file beside each of the 16 new `.cs` files.

- [ ] **Step 1: Run every pure Core test**

Run: `python3 Tools/cloud/sc_core_tests.py`
Expected: exit 0, `Failed: 0`, and the totals include the 38 new tests (4 + 5 + 5 + 6 + 5 + 6 + 4 + 3).

- [ ] **Step 2: Compile the whole project without Unity**

Run: `python3 Tools/cloud/sc_cloud_compile.py --csproj && python3 Tools/cloud/sc_cloud_compile.py`
Expected: every assembly compiles with 0 errors (the new files are only referenced by their tests).

- [ ] **Step 3: Let Unity import the files and run the fast gate**

Cloud: `bash Tools/cloud/setup.sh --wait && bash Tools/cloud/unity.sh gate --out <scratchpad>/gate` (PC: `Tools\unity.ps1 gate -OutDir <scratchpad>\gate`).
Expected: exit code 0 and `PASS` (compile + EditMode, Soak excluded); the import writes the 16 `.meta` files. If there is no Unity licence in the session, stop here and report that the metas and gate are still owed; don't hand-write metas.

- [ ] **Step 4: Commit the metas**

```bash
git add Assets/_Project/Core/Cellars/*.meta Assets/_Project/Tests/EditMode/*.meta
git status --short   # only the 16 new .meta files should be staged
git commit -m "Core dive rules: Unity metas for the new files"
```

- [ ] **Step 5: Review and merge**

Send the branch to the `reviewer` agent (pure Core, no networking or save) with the spec path and this plan; fix findings; then follow `sc-merge`: merge origin/main, one `check`, merge commit into main, push.

---

## Self-review (done while writing)
- **Spec coverage (plan 1's scope):** floor plan (5), checkpoint and floor pick (13, 31, 38), bank slots and bankable kinds (4, 5, 34), gold and the satchel (7, 33-35), momentum (36, 37), boss retry (25, 32), stir per half (19, 4.5, F2), piece depths (10, 4.4), bread ladder (20, 27). Deliberately left to later plans: per-player bags and loot rolls (39, plan 2), wiring, saves, the church entrance and the stand-in ending (plan 3), the metrics gym (plan 4), stair suspend and crash saver (24, 4.6), mixed-power growth (40), co-op extras (41-43), the 3-player pick's world-save field (plan 3).
- **Placeholders:** none; every number marked *placeholder* is a named constant.
- **Names:** `UndercroftPlan`, `UndercroftFloorKind`, `DiveCheckpoint`, `DiveItemKind`, `BankSlots`, `DiveGold`, `LostSatchel`, `SatchelSlot`, `DiveMomentum`, `RetryPrice`, `BossRetry`, `DiveStir`, `PieceSpec`, `PieceDepths`, `BreadLadder`; checked against each task's Interfaces block. None clashes with an existing Core type (checked: `DiveRoute`, `DiveState`, `DiveHaul`, `StirMeter`, `StirTuning`, `HealTuning`, `CellarLoot` stay as they are).
