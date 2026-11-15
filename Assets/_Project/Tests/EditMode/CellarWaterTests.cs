// Tests/EditMode/CellarWaterTests.cs: the Cellars water policy (dive 5b). Core-only.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarWaterTests
    {
        [Test]
        public void Enabled_IsFalse_OnEveryDungeon1Floor()
        {
            Assert.That(CellarWater.EnabledInPhase1, Is.False);
            for (int f = 1; f <= 7; f++) Assert.That(CellarWater.Enabled(f), Is.False, "floor " + f);
        }

        [Test]
        public void Bubbling_IsFloors4And5()
        {
            for (int f = 1; f <= 10; f++) Assert.That(CellarWater.Bubbling(f), Is.EqualTo(f == 4 || f == 5), "floor " + f);
        }

        [Test]
        public void BrimmingHold_IsZero_OnDungeon1()
        {
            Assert.That(CellarWater.BrimmingHoldSeconds, Is.EqualTo(90f));
            for (int f = 1; f <= 7; f++) { Assert.That(CellarWater.BrimmingHold(f, true), Is.EqualTo(0f), "floor " + f); Assert.That(CellarWater.BrimmingHold(f, false), Is.EqualTo(0f)); }
        }

        [Test]
        public void StartState_IsDeterministic_AndInRange()
        {
            foreach (RoomDataV2 room in CellarWaterRooms.All)
                for (int seed = 1; seed <= 40; seed++)
                    for (int floor = 1; floor <= 5; floor++)
                    {
                        int[] a = CellarWater.StartState(seed, floor, 3, room), b = CellarWater.StartState(seed, floor, 3, room);
                        Assert.That(a, Is.EqualTo(b));
                        Assert.That(a.Length, Is.EqualTo(room.Regions.Count));
                        for (int i = 0; i < a.Length; i++) Assert.That(a[i], Is.InRange(0, room.Regions[i].StateCount - 1), room.Base.Id);
                    }
        }

        [Test]
        public void StartState_VariesAcrossSeeds()
        {
            RoomDataV2 room = CellarWaterRooms.Find("larder_pantry");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int seed = 1; seed <= 60; seed++) seen.Add(CellarWater.StartState(seed, 2, 0, room)[0]);
            Assert.That(seen.Count, Is.EqualTo(room.Regions[0].StateCount));
        }

        [Test]
        public void StartState_IsAlwaysAValidatedVector()
        {
            foreach (RoomDataV2 room in CellarWaterRooms.All)
                Assert.That(RoomValidatorV2.Validate(room), Is.Empty);   // every vector the start state can pick is covered by rules 6-9
        }
    }
}
