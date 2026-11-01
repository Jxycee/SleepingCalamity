// Authority: none (pure EditMode tests). M5 contents in Core: CellarLoot (rank roll + key chest), CellarPieceSpots, CellarRoomClear, CellarEnemyMix.
// Catches: a roll that differs between peers, a rank band that does not rise with the floor, a key chest that yields loot, a piece on a chest / spawn / bank / healing /
// locked-room / entry cell, a piece count that differs from DefaultPiecesPerFloor, a room that clears early or never.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarContentsTests
    {
        static readonly int[] Defaults = { 2, 2, 2 };   // = DiveHaulSync.DefaultPiecesPerFloor (the PlayMode contents test asserts the equality)

        [Test]
        public void Loot_IsDeterministic_AndItemsMatchTheirRank()
        {
            for (uint seed = 0; seed < 50; seed++)
                for (int floor = 1; floor <= 4; floor++)
                    for (int c = 0; c < 8; c++)
                    {
                        int a = CellarLoot.Roll(seed, floor, c, false), b = CellarLoot.Roll(seed, floor, c, false);
                        Assert.AreEqual(a, b);
                        Assert.AreEqual(CellarLoot.RollRank(seed, floor, c), CellarLoot.RankOf(a));
                        Assert.That(CellarLoot.RankOf(a), Is.InRange(1, 5));
                    }
        }

        [Test]
        public void Loot_TableHasAboutAdozenItems_WithEveryRank_AndNoReservedId()
        {
            Assert.That(CellarLoot.Items.Count, Is.InRange(10, 14));
            for (int r = 1; r <= 5; r++) Assert.That(CellarLoot.Items.Any(i => i.Rank == r), "rank " + r);
            Assert.That(CellarLoot.Items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(CellarLoot.Items.Count));
            Assert.That(CellarLoot.Items.Any(i => i.Id == CellarLoot.RoomKeyId || i.Id == CellarLoot.GateKeyId), Is.False);
            Assert.AreNotEqual(CellarLoot.RoomKeyId, CellarLoot.GateKeyId);
        }

        [Test]
        public void Loot_RankBandsRiseWithTheFloor()
        {
            var mean = new double[5]; var low = new double[5]; var high = new double[5];
            for (int floor = 1; floor <= 4; floor++)
            {
                int n = 0, sum = 0, lo = 0, hi = 0;
                for (uint s = 0; s < 600; s++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(0xC0FFEEu + s, floor);   // the real non-key chests of a real floor
                    for (int i = 0; i < l.Chests.Count; i++)
                    {
                        if (l.Chests[i].HoldsKey) continue;
                        int r = CellarLoot.RollRank(0xC0FFEEu + s, floor, i); sum += r; n++;
                        if (r <= 2) lo++; if (r >= 3) hi++;
                    }
                }
                mean[floor] = (double)sum / n; low[floor] = (double)lo / n; high[floor] = (double)hi / n;
            }
            Assert.That(mean[1], Is.InRange(1.2, 2.0), "floor 1 mean " + mean[1]);
            Assert.That(mean[2], Is.InRange(1.9, 2.8), "floor 2 mean " + mean[2]);
            Assert.That(mean[3], Is.InRange(2.7, 3.5), "floor 3 mean " + mean[3]);
            Assert.That(mean[4], Is.InRange(3.1, 4.1), "floor 4 mean " + mean[4]);
            Assert.That(mean[1], Is.LessThan(mean[2])); Assert.That(mean[2], Is.LessThan(mean[3])); Assert.That(mean[3], Is.LessThan(mean[4]));
            Assert.That(low[1], Is.GreaterThan(0.80), "floor 1 is mostly rank 1-2");
            Assert.That(high[4], Is.GreaterThan(0.7), "floor 4 is mostly rank 3+");
        }

        [Test]
        public void KeyChest_YieldsTheRoomKey_NotLoot()
        {
            for (uint seed = 0; seed < 60; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    if (l.KeyChestIndex < 0) continue;
                    for (int i = 0; i < l.Chests.Count; i++)
                    {
                        int item = CellarLoot.Roll(seed, floor, i, l.Chests[i].HoldsKey);
                        if (i == l.KeyChestIndex) Assert.AreEqual(CellarLoot.RoomKeyId, item);
                        else Assert.AreNotEqual(CellarLoot.RoomKeyId, item);
                    }
                }
        }

        [Test]
        public void PieceSpots_CountMatchesDefaults_AndCellsAreLegalAndDistinct()
        {
            for (uint seed = 0; seed < 120; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
                    CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    IReadOnlyList<CellPos> spots = CellarPieceSpots.Choose(l, d, Defaults[floor - 1]);
                    string tag = " seed " + seed + " floor " + floor;
                    Assert.AreEqual(Defaults[floor - 1], spots.Count, "count" + tag);
                    Assert.AreEqual(spots.Count, spots.Select(s => s.X * 100 + s.Y).Distinct().Count(), "distinct" + tag);
                    CellarEncounterPlan plan = CellarEncounterPlan.Build(l, d, 0, false);
                    CellPos? heal = CellarHealing.PointFor(l, d);
                    foreach (CellPos s in spots)
                    {
                        int room = l.RoomAt(s.X, s.Y);
                        Assert.That(room, Is.GreaterThanOrEqualTo(0), "in a room" + tag);
                        Assert.AreNotEqual(l.LockedRoomIndex, room, "locked room" + tag);
                        Assert.AreEqual(RoomKind.Combat, l.Rooms[room].Room.Kind, "room kind" + tag);
                        Assert.That(l.Chests.Any(c => c.X == s.X && c.Y == s.Y), Is.False, "chest" + tag);
                        Assert.That(plan.Spawns.Any(p => p.X == s.X && p.Y == s.Y), Is.False, "spawn" + tag);
                        Assert.That(l.EntryStarts.Any(p => p.X == s.X && p.Y == s.Y), Is.False, "entry" + tag);
                        if (l.BankCell.HasValue) Assert.That(l.BankCell.Value.X == s.X && l.BankCell.Value.Y == s.Y, Is.False, "bank" + tag);
                        if (l.StairExit.HasValue) Assert.That(l.StairExit.Value.X == s.X && l.StairExit.Value.Y == s.Y, Is.False, "stair" + tag);
                        if (heal.HasValue) Assert.That(heal.Value.X == s.X && heal.Value.Y == s.Y, Is.False, "heal" + tag);
                        if (l.Carrier != null) Assert.That(l.Carrier.X == s.X && l.Carrier.Y == s.Y, Is.False, "carrier" + tag);
                        Assert.That(l.Rooms[room].Room.CellAt(s.X - l.Rooms[room].X, s.Y - l.Rooms[room].Y), Is.EqualTo(CellKind.Floor), "floor cell" + tag);
                    }
                    var again = CellarPieceSpots.Choose(l, d, Defaults[floor - 1]);
                    for (int i = 0; i < spots.Count; i++) { Assert.AreEqual(spots[i].X, again[i].X); Assert.AreEqual(spots[i].Y, again[i].Y); }
                }
        }

        [Test]
        public void PieceSpots_Floor4HasNone()
        {
            for (uint seed = 0; seed < 10; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 4);
                Assert.AreEqual(0, CellarPieceSpots.Choose(l, CellarFloorDressing.Resolve(l), 2).Count);
            }
        }

        [Test]
        public void PieceId_MatchesDiveHaul()
        {
            var h = new DiveHaul(Defaults);
            for (int f = 1; f <= 3; f++) for (int i = 0; i < 2; i++) Assert.IsTrue(h.AddPiece(DiveHaul.PieceId(f, i)));
            Assert.AreEqual(0, h.PiecesLeft(1) + h.PiecesLeft(2) + h.PiecesLeft(3));
        }

        [Test]
        public void RoomClear_ClearsWhenTheLastOneDies_AndResets()
        {
            var c = new CellarRoomClear();
            Assert.IsFalse(c.IsCleared(2));
            c.Spawn(2); c.Spawn(2); c.Spawn(3);
            Assert.IsFalse(c.Died(2)); Assert.IsFalse(c.IsCleared(2));
            Assert.IsTrue(c.Died(2)); Assert.IsTrue(c.IsCleared(2)); Assert.IsFalse(c.IsCleared(3));
            Assert.IsFalse(c.Died(2), "no phantom death");
            Assert.AreEqual(1UL << 2, c.ClearedMask);
            c.Spawn(2); Assert.IsFalse(c.IsCleared(2), "a late spawn reopens the room");
            c.Reset(); Assert.AreEqual(0UL, c.ClearedMask); Assert.AreEqual(0, c.Spawned(2));
        }

        [Test]
        public void RoomClear_DespawnIsNotAKill_AndResetRoomAllowsAFreshSpawn()
        {
            var c = new CellarRoomClear();
            c.Spawn(2); c.Spawn(2);
            c.Despawned(2); Assert.AreEqual(1, c.Alive(2)); Assert.IsFalse(c.IsCleared(2), "a despawn never clears");
            c.Despawned(2); Assert.AreEqual(0, c.Alive(2)); Assert.IsFalse(c.IsCleared(2), "all despawned: still not cleared");
            Assert.IsFalse(c.Died(2), "nothing alive to die");
            c.Spawn(2); c.ResetRoom(2); Assert.AreEqual(0, c.Spawned(2)); Assert.AreEqual(0, c.Alive(2));
            c.Spawn(2); Assert.IsTrue(c.Died(2), "after a reset the room clears afresh");
            c.ResetRoom(2); Assert.AreEqual(0UL, c.ClearedMask, "ResetRoom also forgets the cleared bit");
        }

        [Test]
        public void EnemyMix_IsDeterministic_AndFloor1IsAllSlimes_Floor3HasRats()
        {
            bool rat3 = false;
            for (uint seed = 0; seed < 30; seed++)
                for (int r = 0; r < 8; r++)
                {
                    Assert.AreEqual(CellarEnemyMix.Tier1Kind(seed, 2, r, 1), CellarEnemyMix.Tier1Kind(seed, 2, r, 1));
                    Assert.AreEqual(SleepingCalamity.Core.Combat.EnemyKind.Slime, CellarEnemyMix.Tier1Kind(seed, 1, r, 0));
                    Assert.AreNotEqual(SleepingCalamity.Core.Combat.EnemyKind.RatKing, CellarEnemyMix.Tier1Kind(seed, 2, r, 0));
                    if (CellarEnemyMix.Tier1Kind(seed, 3, r, 0) == SleepingCalamity.Core.Combat.EnemyKind.RatKing) rat3 = true;
                }
            Assert.IsTrue(rat3);
        }
    }
}
