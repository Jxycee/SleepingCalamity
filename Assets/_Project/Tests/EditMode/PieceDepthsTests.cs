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
