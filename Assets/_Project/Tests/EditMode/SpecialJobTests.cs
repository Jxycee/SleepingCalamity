// Assets/_Project/Tests/EditMode/SpecialJobTests.cs: tests for SleepingCalamity.Core.SpecialJobs / SpecialJobTuning (dive step 6 job 2: the special-room kinds' effects).
// Catches: a kind paying in the wrong room or for the wrong kind, an effect that changes which room/kind the floor picks (SpecialHash), a non-deterministic effect.
// NOT covered: the host wiring (CellarContentsSync.ApplySpecialJob, DiveHaulSync.ServerMendParty / ServerShrineStir): no EditMode path to a live host; needs a PlayMode case or the manual walk.
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class SpecialJobTests
    {
        static SpecialRoom Room(SpecialKind k, int room = 3) => new SpecialRoom(2, k, room);

        [Test]
        public void EachKind_HasItsOwnEffect_InItsRoomOnly()
        {
            Assert.That(SpecialJobs.OnClear(Room(SpecialKind.Quiet), 3).MendPercent, Is.EqualTo(SpecialJobTuning.MendPercent));
            Assert.That(SpecialJobs.OnClear(Room(SpecialKind.EliteDen), 3).ExtraRolls, Is.EqualTo(SpecialJobTuning.EliteDenRolls));
            Assert.That(SpecialJobs.OnClear(Room(SpecialKind.Treasury), 3).ExtraRolls, Is.EqualTo(SpecialJobTuning.TreasuryRolls));
            Assert.That(SpecialJobs.OnClear(Room(SpecialKind.Shrine), 3).StirDelta, Is.EqualTo(SpecialJobTuning.ShrineStir));
            foreach (SpecialKind k in new[] { SpecialKind.Quiet, SpecialKind.EliteDen, SpecialKind.Treasury, SpecialKind.Shrine })
            {
                Assert.That(SpecialJobs.OnClear(Room(k), 3).Any, Is.True, k.ToString());
                Assert.That(SpecialJobs.OnClear(Room(k), 2).Any, Is.False, "another room: " + k);
                Assert.That(SpecialJobs.OnClear(Room(k), -1).Any, Is.False, "no room: " + k);
            }
        }

        [Test]
        public void EachKind_DoesOnlyItsOwnJob()
        {
            SpecialJobEffect quiet = SpecialJobs.OnClear(Room(SpecialKind.Quiet), 3), den = SpecialJobs.OnClear(Room(SpecialKind.EliteDen), 3);
            SpecialJobEffect vault = SpecialJobs.OnClear(Room(SpecialKind.Treasury), 3), shrine = SpecialJobs.OnClear(Room(SpecialKind.Shrine), 3);
            Assert.That(quiet.ExtraRolls + (int)quiet.StirDelta, Is.EqualTo(0));
            Assert.That(den.MendPercent + (int)den.StirDelta, Is.EqualTo(0));
            Assert.That(vault.MendPercent + (int)vault.StirDelta, Is.EqualTo(0));
            Assert.That(shrine.MendPercent + shrine.ExtraRolls, Is.EqualTo(0));
        }

        [Test]
        public void NoSpecialRoom_NoEffect()
        {
            Assert.That(SpecialJobs.OnClear(null, 0).Any, Is.False);
            Assert.That(SpecialJobs.OnClear(new SpecialRoom(1, SpecialKind.None, -1), -1).Any, Is.False);
            Assert.That(SpecialJobs.OnClear(new SpecialRoom(1, SpecialKind.None, -1), 0).Any, Is.False);
        }

        [Test]
        public void Values_AreSmallForDungeonOne()
        {
            Assert.That(SpecialJobTuning.MendPercent, Is.InRange(1, 100));
            Assert.That(SpecialJobTuning.EliteDenRolls, Is.InRange(1, 3));
            Assert.That(SpecialJobTuning.TreasuryRolls, Is.InRange(1, 3));
            Assert.That(SpecialJobTuning.ShrineStir, Is.InRange(1f, 20f));
            Assert.That(SpecialJobTuning.ExtraRollChestBase, Is.GreaterThan(51000 + 64), "clear of the modifier roll indices (50000 + room + 1000 k)");
        }

        [Test]
        public void MendAmount_IsAPercentOfMaxHp_AtLeastOne()
        {
            Assert.That(SpecialJobs.MendAmount(100, 50), Is.EqualTo(50));
            Assert.That(SpecialJobs.MendAmount(1, 50), Is.EqualTo(1));
            Assert.That(SpecialJobs.MendAmount(0, 50), Is.EqualTo(0));
            Assert.That(SpecialJobs.MendAmount(100, 0), Is.EqualTo(0));
        }

        [Test]
        public void StirBonus_AppliesOnlyOnTheAltarsFloor_AndOnlyOnceTriggered()
        {
            Assert.That(SpecialJobs.StirBonusOnFloor(0, 12f, 2), Is.EqualTo(0f), "no altar cleared");
            Assert.That(SpecialJobs.StirBonusOnFloor(2, 12f, 2), Is.EqualTo(12f));
            Assert.That(SpecialJobs.StirBonusOnFloor(2, 12f, 3), Is.EqualTo(0f), "leaves with the floor");
        }

        // The jobs read the existing pick and never feed it: the room and kind of every floor are what SpecialRoomDive says, run twice, and the hash is a function of (floor, kind, room) alone.
        [Test]
        public void Effects_AreDeterministic_AndTheSpecialPickAndHashAreUnchanged()
        {
            for (uint seed = 1; seed <= 60; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor);
                    SpecialRoom a = SpecialRoomDive.Resolve(l);
                    ulong before = a.SpecialHash();
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        SpecialJobEffect e1 = SpecialJobs.OnClear(a, r), e2 = SpecialJobs.OnClear(a, r);
                        Assert.That(e1.Any, Is.EqualTo(e2.Any)); Assert.That(e1.ExtraRolls, Is.EqualTo(e2.ExtraRolls));
                        Assert.That(e1.Any, Is.EqualTo(a.Exists && r == a.RoomIndex), "pays only in the special room: seed " + seed + " floor " + floor + " room " + r);
                    }
                    SpecialRoom b = SpecialRoomDive.Resolve(l);
                    Assert.That(a.SpecialHash(), Is.EqualTo(before)); Assert.That(b.SpecialHash(), Is.EqualTo(before));
                    var h = Fnv1a64.Create(); h.AppendInt32LE(floor); h.AppendInt32LE((int)a.Kind); h.AppendInt32LE(a.RoomIndex);
                    Assert.That(before, Is.EqualTo(h.Value), "hash is still (floor, kind, room)");
                }
        }
    }
}
