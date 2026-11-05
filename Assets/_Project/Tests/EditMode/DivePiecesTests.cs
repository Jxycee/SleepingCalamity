// Authority: none (pure EditMode tests). Tests for DivePieces + PieceCatalog.Specs (plan 3 task 5, decision 10).
// Catches: a random piece landing on an authored or set-piece floor, a spec table that misses a catalogue id, a piece that is not safe the moment it is found, a person counted as a piece.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class DivePiecesTests
    {
        static DivePieces Make(ulong seed = 42) => new DivePieces(PieceDepths.Assign(seed, PieceCatalog.Specs));

        [Test]
        public void Specs_CoverEveryCatalogueId_BakerFixedOnFloor3()
        {
            var ids = PieceCatalog.Specs.Select(s => s.Id).ToList();
            foreach (PieceInfo p in PieceCatalog.All) Assert.That(ids, Does.Contain(p.Id), "piece " + p.Id);
            Assert.That(ids, Does.Contain(PieceCatalog.BakerId));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            Assert.That(PieceCatalog.Specs.Single(s => s.Id == PieceCatalog.BakerId).FixedFloor, Is.EqualTo(3));
            foreach (PieceSpec s in PieceCatalog.Specs.Where(s => s.Id != PieceCatalog.BakerId)) Assert.That(s.FixedFloor, Is.EqualTo(0), "piece " + s.Id + " is random");
            Assert.That(PieceCatalog.Specs.Where(s => s.SecondHalf).Select(s => s.Id), Is.EquivalentTo(new[] { 300, 301 }));
        }

        [Test]
        public void PeopleLeft_BakerOnFloor3()
        {
            DivePieces p = Make();
            Assert.That(p.PeopleLeft(3), Is.EqualTo(1));
            Assert.That(p.PiecesLeft(3), Is.EqualTo(0), "the baker is a person, not a piece");
            Assert.That(p.Found(PieceCatalog.BakerId), Is.True);
            Assert.That(p.PeopleLeft(3), Is.EqualTo(0));
            Assert.That(p.Found(PieceCatalog.BakerId), Is.False, "found twice");
        }

        [Test]
        public void DivePieces_PickIsSafeAtOnce()
        {
            DivePieces p = Make();
            int id = PieceCatalog.All[0].Id;
            int floor = p.DepthOf(id);
            int before = p.PiecesLeft(floor);
            Assert.That(p.Pick(id), Is.True);
            Assert.That(p.IsFound(id), Is.True);
            Assert.That(p.PiecesLeft(floor), Is.EqualTo(before - 1));
            Assert.That(p.OnFloor(floor).Contains(id), Is.False);
            Assert.That(p.Pick(id), Is.False);
            Assert.That(p.Pick(999), Is.False, "unknown id");
        }

        [Test]
        public void Counts_AddUpToTheCatalogue_AndRestoreKeepsOnlyKnownIds()
        {
            DivePieces p = Make(7);
            int pieces = 0, people = 0;
            for (int f = 1; f <= UndercroftPlan.BossFloor; f++) { pieces += p.PiecesLeft(f); people += p.PeopleLeft(f); }
            Assert.That(pieces, Is.EqualTo(PieceCatalog.All.Count));
            Assert.That(people, Is.EqualTo(1));
            p.Restore(new[] { 100, 400, 12345 });
            Assert.That(p.FoundIds, Is.EquivalentTo(new[] { 100, 400 }));
        }

        [Test]
        public void ForWorld_MatchesAssign_AndNeverPutsAPieceOnFloor1()
        {
            for (ulong seed = 1; seed <= 100; seed++)
            {
                DivePieces p = DivePieces.ForWorld(seed);
                Assert.That(p.OnFloor(1), Is.Empty, "seed " + seed);
                IReadOnlyDictionary<int, int> d = PieceDepths.Assign(seed, PieceCatalog.Specs);
                foreach (var kv in d) Assert.That(p.DepthOf(kv.Key), Is.EqualTo(kv.Value));
            }
        }
    }
}
