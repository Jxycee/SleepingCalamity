// Tests/EditMode/CellarEnemyDropTests.cs: CellarLoot.RollEnemyDrop, the per-player enemy-drop roll (host rolls once per hitter on a real death).
// Catches: a roll that is not deterministic per (seed, floor, kill, slot), every player getting the same roll, gold / boots / key ids leaking out of the enemy table, silly odds, and a dropped item
// not surviving the bag -> world-save round trip (a drop is an ordinary PlayerBag.Add).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class CellarEnemyDropTests
    {
        [Test]
        public void RollEnemyDrop_IsDeterministic()
        {
            for (int slot = 0; slot < 4; slot++)
                for (int key = 0; key < 50; key++)
                    Assert.That(CellarLoot.RollEnemyDrop(4242u, 2, key, slot), Is.EqualTo(CellarLoot.RollEnemyDrop(4242u, 2, key, slot)));
        }

        [Test]
        public void RollEnemyDrop_SlotsCanDiffer()
        {
            int differ = 0;
            for (int key = 0; key < 400; key++)
                if (CellarLoot.RollEnemyDrop(777u, 3, key, 0) != CellarLoot.RollEnemyDrop(777u, 3, key, 1)) differ++;
            Assert.That(differ, Is.GreaterThan(20), "two players do not share one roll");
        }

        [Test]
        public void RollEnemyDrop_NeverGoldBootsOrKeys_AndOnlyTableItems()
        {
            var table = new HashSet<int>();
            foreach (LootItem i in CellarLoot.Items) table.Add(i.Id);
            for (int n = 0; n < 4000; n++)
            {
                int id = CellarLoot.RollEnemyDrop((uint)(1 + n * 7919), 1 + n % 4, n % 97, n % 4, 1 + n % 2);
                if (id == CellarLoot.EnemyDropNoItem) continue;
                Assert.That(table.Contains(id), Is.True, "id " + id + " is a table item");
                Assert.That(id, Is.Not.EqualTo(CellarLoot.GoldId).And.Not.EqualTo(CellarLoot.BootsId).And.Not.EqualTo(CellarLoot.RoomKeyId).And.Not.EqualTo(CellarLoot.GateKeyId));
            }
        }

        [Test]
        public void RollEnemyDrop_OddsAreAboutOneInN()
        {
            int drops = 0, total = 20000;
            for (int n = 0; n < total; n++) if (CellarLoot.RollEnemyDrop(9001u + (uint)(n / 64), 1 + n % 4, n % 64, n % 4) != CellarLoot.EnemyDropNoItem) drops++;
            double rate = drops / (double)total, want = 1.0 / CellarLoot.EnemyDropOneIn;
            Assert.That(rate, Is.InRange(want * 0.8, want * 1.2), "drop rate " + rate);
        }

        [Test]
        public void RollEnemyDrop_SlotOutOfRange_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CellarLoot.RollEnemyDrop(1u, 1, 0, 4));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CellarLoot.RollEnemyDrop(1u, 1, 0, -1));
        }

        [Test]
        public void Tier2_PaysAtLeastAsMuchRankAsTier1_OnTheSameRoll()
        {
            for (int key = 0; key < 300; key++)
            {
                int a = CellarLoot.RollEnemyDrop(31u, 3, key, 0, 1), b = CellarLoot.RollEnemyDrop(31u, 3, key, 0, 2);
                Assert.That(a == CellarLoot.EnemyDropNoItem, Is.EqualTo(b == CellarLoot.EnemyDropNoItem), "the tier moves the rank, not whether it drops");
                if (a != 0) Assert.That(CellarLoot.RankOf(b), Is.GreaterThanOrEqualTo(CellarLoot.RankOf(a)));
            }
        }

        [Test]
        public void DroppedItem_SurvivesBagGoHome_AndTheWorldSaveRoundTrip()
        {
            int drop = 0;
            for (int key = 0; key < 200 && drop == 0; key++) drop = CellarLoot.RollEnemyDrop(55u, 2, key, 0);
            Assert.That(drop, Is.Not.EqualTo(0), "premise: some kill drops");
            var bags = new PartyBags();
            PlayerKey pk = PlayerKey.Local("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            PlayerBag bag = bags.For(pk.ToString());
            Assert.That(bag.Add(drop), Is.True);
            Assert.That(bag.Carried, Is.EqualTo(new[] { drop }));
            bag.GoHome();
            var keyOf = new Dictionary<string, PlayerKey> { { pk.ToString(), pk } };
            BagsSave save = BagsSaveRules.Capture(bags, keyOf, false, 0);
            var back = new PartyBags();
            BagsSaveRules.Restore(save, back);
            Assert.That(back.TryGet(pk.ToString(), out PlayerBag restored), Is.True);
            Assert.That(restored.Stash, Is.EqualTo(new Dictionary<int, int> { { drop, 1 } }));
        }
    }
}
