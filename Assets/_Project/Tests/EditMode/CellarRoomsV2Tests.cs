// Tests/EditMode/CellarRoomsV2Tests.cs: the Cellars pool and bakery rooms as room data v2 (rooms-v2 plan, Task 1). Core-only (EnemyKind is written fully qualified so sc_core_tests.py picks the file up).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarRoomsV2Tests
    {
        static string Describe(RoomDataV2 r) => $"{r.Base.Id} ({r.Base.Width}x{r.Base.Height}, {r.Base.FloorCount} floor cells)";

        static bool FitsFullParty(RoomDataV2 r) => FightSpace.Fits(r.Base, GameMetrics.FullPartyHeroes, GameMetrics.MaxLiveEnemies);

        [Test]
        public void Pool_IsWrappedByReference()
        {
            Assert.That(CellarRoomsV2.Pool.Count, Is.EqualTo(CellarRoomPool.All.Count));
            for (int i = 0; i < CellarRoomPool.All.Count; i++) Assert.That(ReferenceEquals(CellarRoomsV2.Pool[i].Base, CellarRoomPool.All[i]), Is.True, CellarRoomPool.All[i].Id);
            Assert.That(CellarRoomsV2.Bakery.Count, Is.EqualTo(CellarBakeryPool.All.Count));
            for (int i = 0; i < CellarBakeryPool.All.Count; i++) Assert.That(ReferenceEquals(CellarRoomsV2.Bakery[i].Base, CellarBakeryPool.All[i]), Is.True, CellarBakeryPool.All[i].Id);
            Assert.That(CellarRoomsV2.All.Count, Is.EqualTo(28));
            Assert.That(CellarRoomsV2.All.Select(r => r.Base.Id).Distinct().Count(), Is.EqualTo(28));
        }

        [Test]
        public void Find_ReturnsTheWrapper_OrNull()
        {
            Assert.That(ReferenceEquals(CellarRoomsV2.Find("larder_pantry").Base, CellarRoomPool.Find("larder_pantry")), Is.True);
            Assert.That(ReferenceEquals(CellarRoomsV2.Find("bakery_heart").Base, CellarBakeryPool.Find(CellarBakeryPool.HeartId)), Is.True);
            Assert.That(CellarRoomsV2.Find("nope"), Is.Null);
        }

        [Test]
        public void EveryRoom_PassesTheValidator()
        {
            List<string> errors = RoomValidatorV2.ValidateAll(CellarRoomsV2.All);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        [Test]
        public void EveryRoom_IsVersion2_WithNoExtras()
        {
            foreach (RoomDataV2 r in CellarRoomsV2.All)
            {
                Assert.That(r.Version, Is.EqualTo(RoomDataV2.SchemaVersion), r.Base.Id);
                Assert.That(r.Regions, Is.Empty, r.Base.Id);
                Assert.That(r.Stairs, Is.Empty, r.Base.Id);
                Assert.That(r.Sockets, Is.Empty, r.Base.Id);
                Assert.That(r.AuthoredTags, Is.EqualTo(RoomAffordance.None), r.Base.Id);
                for (int x = 0; x < r.Base.Width; x++) for (int y = 0; y < r.Base.Height; y++) Assert.That(r.LevelAt(x, y), Is.EqualTo(0), r.Base.Id);
            }
        }

        [Test]
        public void Doors_AreLockedOnTheLDoor_ElsePlain()
        {
            foreach (RoomDataV2 r in CellarRoomsV2.All)
            {
                Assert.That(r.Doors.Count, Is.EqualTo(r.Base.Doors.Count), r.Base.Id);
                for (int i = 0; i < r.Doors.Count; i++)
                    Assert.That(r.Doors[i].Type, Is.EqualTo(r.Base.Doors[i].Locked ? DoorType.Locked : DoorType.Plain), $"{r.Base.Id} door {i}");
            }
        }

        [Test]
        public void EveryCombatRoom_FitsAFullParty_OrIsListedSmall()
        {
            var listed = new HashSet<string>(CellarRoomsV2.SmallParty.Select(s => s.Id));
            var bad = CellarRoomsV2.All.Where(r => r.Base.Kind == RoomKind.Combat && !FitsFullParty(r) && !listed.Contains(r.Base.Id)).Select(Describe).ToList();
            Assert.That(bad, Is.Empty, "combat rooms too small for 4 heroes + 8 enemies and not listed: " + string.Join("; ", bad));
        }

        [Test]
        public void SmallPartyList_HasNoRoomThatFits()
        {
            foreach (SmallPartyRoom s in CellarRoomsV2.SmallParty)
            {
                RoomDataV2 r = CellarRoomsV2.Find(s.Id);
                Assert.That(r, Is.Not.Null, s.Id);
                Assert.That(s.Reason, Is.Not.Empty, s.Id);
                Assert.That(FitsFullParty(r), Is.False, s.Id + " now fits a full party: remove it from SmallParty");
                Assert.That(FightSpace.Fits(r.Base, s.MaxHeroes, r.Base.Spawns.Count), Is.True, s.Id + " does not fit " + s.MaxHeroes + " heroes and its own spawns");
            }
        }

        [Test]
        public void FullPartyTag_MatchesTheList()
        {
            var listed = new HashSet<string>(CellarRoomsV2.SmallParty.Select(s => s.Id));
            foreach (RoomDataV2 r in CellarRoomsV2.All.Where(x => x.Base.Kind == RoomKind.Combat))
                Assert.That((r.Affordances & RoomAffordance.FullParty) != 0, Is.EqualTo(!listed.Contains(r.Base.Id)), Describe(r));
        }

        [Test]
        public void PoolCombatRooms_ThatFitAFullParty()
        {
            string[] expected = CellarRoomPool.All.Where(r => r.Kind == RoomKind.Combat && r.Id != "coal_stoke_chicane").Select(r => r.Id).OrderBy(s => s).ToArray();
            Assert.That(expected.Length, Is.EqualTo(11));
            string[] actual = CellarRoomsV2.Pool.Where(r => r.Base.Kind == RoomKind.Combat && FitsFullParty(r)).Select(r => r.Base.Id).OrderBy(s => s).ToArray();
            Assert.That(actual, Is.EqualTo(expected), "pool combat rooms that do not fit: " + string.Join("; ", CellarRoomsV2.Pool.Where(r => r.Base.Kind == RoomKind.Combat && !FitsFullParty(r)).Select(Describe)));
        }

        [Test]
        public void EverySpawn_CanHostTheKindsItRolls()
        {
            // Floors 1-3: a Tier2 spawn is always a RatKing, Tier1 comes from CellarEnemyMix.Tier1Kind (Slime, Broom or RatKing); none needs open floor.
            // The Dough Warden appears only in the bakery antechamber (CellarFloorLayout.cs, CellarContentsSync.cs).
            var tier1 = new[] { SleepingCalamity.Core.Combat.EnemyKind.Slime, SleepingCalamity.Core.Combat.EnemyKind.Broom, SleepingCalamity.Core.Combat.EnemyKind.RatKing };
            foreach (RoomDataV2 r in CellarRoomsV2.Pool.Where(x => x.Base.Kind == RoomKind.Combat))
                foreach (SleepingCalamity.Core.Combat.EnemyKind k in tier1)
                    Assert.That(EnemyHabitat.Fits(k, r), Is.True, r.Base.Id + " cannot host " + k);
            Assert.That(EnemyHabitat.Fits(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden, CellarRoomsV2.Find(CellarBakeryPool.AntechamberId)), Is.True);
        }
    }
}
