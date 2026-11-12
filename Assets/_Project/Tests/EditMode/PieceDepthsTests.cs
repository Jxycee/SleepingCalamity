// Tests/EditMode/PieceDepthsTests.cs: tests for SleepingCalamity.Core.PieceDepths (spec decision 10, section 4.4).
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class PieceDepthsTests
    {
        [Test]
        public void Candidates_AreTheGeneratedFloorsOfEachHalf()
        {
            Assert.That(PieceDepths.CandidateFloors(false), Is.EqualTo(new[] { 2, 4 }), "floor 1 is authored, 3 and 5 are set pieces");
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
            Assert.That(floorsSeen, Is.EquivalentTo(new[] { 2, 4 }));
        }

        [Test]
        public void Assign_NeverPutsRandomPiecesOnFloor1()
        {
            for (ulong seed = 1; seed <= 200; seed++)
                foreach (var kv in PieceDepths.Assign(seed, PieceCatalog.Specs))
                    if (kv.Key != PieceCatalog.BakerId) Assert.That(kv.Value, Is.Not.EqualTo(1), $"seed {seed} piece {kv.Key}");
        }

        [Test]
        public void Assign_FirstHalfRandomUsesFloors2And4Only()
        {
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 200; seed++)
                foreach (PieceSpec s in PieceCatalog.Specs)
                {
                    if (s.FixedFloor != 0 || s.SecondHalf) continue;
                    int f = PieceDepths.Assign(seed, PieceCatalog.Specs)[s.Id];
                    Assert.That(f, Is.EqualTo(2).Or.EqualTo(4), $"seed {seed} piece {s.Id}");
                    seen.Add(f);
                }
            Assert.That(seen, Is.EquivalentTo(new[] { 2, 4 }));
        }

        [Test, Category("Soak")]
        public void FrameFloor_Share_Over1000Seeds()
        {
            int onFloor2 = 0;
            for (ulong seed = 1; seed <= 1000; seed++)
                if (PieceDepths.Assign(seed, PieceCatalog.Specs)[PieceCatalog.FrameId] == 2) onFloor2++;
            TestContext.WriteLine($"PL-1 frame (id 100) on floor 2: {onFloor2}/1000 seeds ({onFloor2 / 10.0:0.0}%), on floor 4: {1000 - onFloor2}/1000");
            Assert.That(onFloor2, Is.GreaterThan(0));
        }

        [Test]
        public void Frame_IsOnFloor2_ForEveryNewWorld()
        {
            for (ulong seed = 1; seed <= 200; seed++)
                Assert.That(PieceDepths.Assign(seed, PieceCatalog.Specs)[PieceCatalog.FrameId], Is.EqualTo(2), $"seed {seed}");
        }

        [Test]
        public void SavedAssignment_IsKept_OnLoad()
        {
            var dive = new DiveSave { PieceDepths = new List<PieceDepthSave> { new PieceDepthSave { Id = PieceCatalog.FrameId, Floor = 4 } } };
            for (ulong seed = 1; seed <= 50; seed++)
                Assert.That(TownSaveRules.ResolvePieceDepths(dive, seed)[PieceCatalog.FrameId], Is.EqualTo(4), $"seed {seed}");
        }

        [Test]
        public void OtherPieces_StillSpreadOverFloors2And4()
        {
            var seen = new HashSet<int>();
            for (ulong seed = 1; seed <= 200; seed++)
            {
                IReadOnlyDictionary<int, int> d = PieceDepths.Assign(seed, PieceCatalog.Specs);
                foreach (int id in new[] { PieceCatalog.OvenId, PieceCatalog.DoorId, PieceCatalog.SignId })
                {
                    Assert.That(d[id], Is.EqualTo(2).Or.EqualTo(4), $"seed {seed} piece {id}");
                    seen.Add(d[id]);
                }
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 2, 4 }));
        }

        [Test]
        public void Assign_RejectsBadSpecs()
        {
            Assert.Throws<ArgumentException>(() => PieceDepths.Assign(1, new List<PieceSpec> { new PieceSpec(1, 11, false) }));
            Assert.Throws<ArgumentException>(() => PieceDepths.Assign(1, new List<PieceSpec> { new PieceSpec(1, 0, false), new PieceSpec(1, 0, true) }));
        }
    }
}
