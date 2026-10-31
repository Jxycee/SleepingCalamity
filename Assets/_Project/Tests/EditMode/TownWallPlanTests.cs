// Authority: none (pure EditMode tests). Core/Town/TownWallPlan: the deterministic module sequence for Hollowmere's town wall (Q8).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class TownWallPlanTests
    {
        static List<(TownPoint A, TownPoint B)> Pieces(TownLayout t) =>
            HollowmereLayout.WallRun(t, HollowmereLayout.OffsetLine(HollowmereLayout.TownWallOffset, 16f), 0.5f);

        static List<WallSlot> Plan(bool rebuilt)
        {
            TownLayout t = HollowmereLayout.Create();
            return TownWallPlan.Plan(Pieces(t), (x, z) => HollowmereLayout.Ground(t, x, z), rebuilt);
        }

        static string Sig(List<WallSlot> p) => string.Join(",", p.Select(s => s.Piece + ":" + s.Run + ":" + s.Index + ":" + s.Centre.X.ToString("0.000") + ":" + s.Centre.Z.ToString("0.000")));

        [Test]
        public void Sequence_IsDeterministic()
        {
            Assert.AreEqual(Sig(Plan(false)), Sig(Plan(false)));
            Assert.AreEqual(Sig(Plan(true)), Sig(Plan(true)));
        }

        [Test]
        public void Ruined_BreachAndCrumbledSharesAreInRange()
        {
            List<WallSlot> mods = Plan(false).Where(s => TownWallPlan.IsModule(s.Piece)).ToList();
            Assert.That(mods.Count, Is.GreaterThan(30), "enough modules for shares to mean something");
            float breach = mods.Count(s => s.Piece == WallPiece.Breach) / (float)mods.Count, crumbled = mods.Count(s => s.Piece == WallPiece.Crumbled) / (float)mods.Count;
            Assert.That(breach, Is.InRange(0.05f, 0.11f), "breach share");
            Assert.That(crumbled, Is.InRange(0.11f, 0.19f), "crumbled share");
            Assert.AreEqual(0, mods.Count(s => s.Piece == WallPiece.Patched), "no patched wall in Ruined");
        }

        [Test]
        public void Ruined_VariantsAreSpreadOut()
        {
            List<WallSlot> mods = Plan(false).Where(s => TownWallPlan.IsModule(s.Piece)).OrderBy(s => s.Index).ToList();
            for (int i = 1; i < mods.Count; i++)
                Assert.IsFalse(TownWallPlan.IsRuinVariant(mods[i].Piece) && TownWallPlan.IsRuinVariant(mods[i - 1].Piece), "two ruined modules touch at " + i);
        }

        [Test]
        public void Rebuilt_HasNoBreachesOrCrumbled_AndPatchesTheRuinedBreaches()
        {
            List<WallSlot> ruined = Plan(false).Where(s => TownWallPlan.IsModule(s.Piece)).OrderBy(s => s.Index).ToList();
            List<WallSlot> rebuilt = Plan(true).Where(s => TownWallPlan.IsModule(s.Piece)).OrderBy(s => s.Index).ToList();
            Assert.AreEqual(ruined.Count, rebuilt.Count);
            Assert.AreEqual(0, rebuilt.Count(s => TownWallPlan.IsRuinVariant(s.Piece)));
            for (int i = 0; i < ruined.Count; i++)
                if (ruined[i].Piece == WallPiece.Breach) Assert.AreEqual(WallPiece.Patched, rebuilt[i].Piece, "module " + i);
                else if (ruined[i].Piece != WallPiece.Crumbled) Assert.AreEqual(ruined[i].Piece, rebuilt[i].Piece, "module " + i);
            Assert.That(rebuilt.Count(s => s.Piece == WallPiece.Patched), Is.GreaterThan(0));
        }

        [Test]
        public void Modules_CoverEachRunWithinTheStretch()
        {
            TownLayout t = HollowmereLayout.Create();
            var runs = TownWallPlan.Runs(Pieces(t));
            List<WallSlot> mods = TownWallPlan.Plan(Pieces(t), (x, z) => 0f, false).Where(s => TownWallPlan.IsModule(s.Piece)).ToList();
            for (int r = 0; r < runs.Count; r++)
            {
                float len = TownWallPlan.Length(runs[r]);
                List<WallSlot> m = mods.Where(s => s.Run == r).OrderBy(s => s.Start).ToList();
                if (len < TownWallPlan.ModuleLength * (1f - TownWallPlan.MaxStretch)) { Assert.AreEqual(0, m.Count, "run " + r + " too short for a module"); continue; }
                Assert.That(m.Count, Is.GreaterThan(0), "run " + r);
                foreach (WallSlot s in m) Assert.That(s.Stretch, Is.InRange(0.9f - 1e-4f, 1.1f + 1e-4f), "run " + r + " stretch");
                for (int i = 1; i < m.Count; i++) Assert.AreEqual(m[i - 1].Start + m[i - 1].Length, m[i].Start, 1e-3f, "run " + r + " gap or overlap at " + i);
                float covered = m.Sum(s => s.Length), rem = len - covered;
                Assert.That(rem, Is.GreaterThanOrEqualTo(-1e-3f), "run " + r + " overshoots");
                Assert.That(rem, Is.LessThan(TownWallPlan.ModuleLength * 0.9f), "run " + r + " leaves more than a module uncovered");
            }
        }

        [Test]
        public void Fit_UsesStretchOrLeavesARemainder()
        {
            TownWallPlan.Fit(8f, out int n, out float s, out float rem);
            Assert.AreEqual((2, 1f, 0f), (n, s, rem));
            TownWallPlan.Fit(4.3f, out n, out s, out rem);
            Assert.AreEqual(1, n); Assert.AreEqual(0f, rem, 1e-4f); Assert.AreEqual(4.3f / 4f, s, 1e-4f);
            TownWallPlan.Fit(6f, out n, out s, out rem);   // 4.4 is the longest one module gets: 1.6 m stay open
            Assert.AreEqual(1, n); Assert.AreEqual(1.1f, s, 1e-4f); Assert.AreEqual(1.6f, rem, 1e-3f);
            TownWallPlan.Fit(3f, out n, out s, out rem);
            Assert.AreEqual(0, n);
        }

        [Test]
        public void EveryRunHasAnEndAtEachOpening()
        {
            TownLayout t = HollowmereLayout.Create();
            var pieces = Pieces(t);
            var runs = TownWallPlan.Runs(pieces);
            List<WallSlot> plan = TownWallPlan.Plan(pieces, (x, z) => 0f, false);
            for (int r = 0; r < runs.Count; r++)
            {
                bool hasModules = plan.Any(s => s.Run == r && TownWallPlan.IsModule(s.Piece));
                List<WallSlot> ends = plan.Where(s => s.Run == r && s.Piece == WallPiece.End).ToList();
                if (!hasModules) { Assert.AreEqual(0, ends.Count, "run " + r); continue; }
                Assert.AreEqual(2, ends.Count, "run " + r + " ends");
                TownPoint a = runs[r][0], b = runs[r][runs[r].Count - 1];
                Assert.That(ends.Min(e => TownPoint.Distance(e.Centre, a)), Is.LessThanOrEqualTo(TownWallPlan.ModuleLength * 0.5f), "run " + r + " start jamb");
                Assert.That(ends.Min(e => TownPoint.Distance(e.Centre, b)), Is.LessThanOrEqualTo(TownWallPlan.ModuleLength * 0.5f), "run " + r + " end jamb");
            }
            Assert.That(plan.Count(s => s.Piece == WallPiece.End), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Fittings_ButtressesAreOutsideOnDownhill_StairsAreSpaced()
        {
            TownLayout t = HollowmereLayout.Create();
            var pieces = Pieces(t);
            List<WallSlot> plan = TownWallPlan.Plan(pieces, (x, z) => HollowmereLayout.Ground(t, x, z), false);
            foreach (WallSlot b in plan.Where(s => s.Piece == WallPiece.Buttress))
            {
                float inside = HollowmereLayout.Ground(t, b.Centre.X - b.Outward.X * 3f, b.Centre.Z - b.Outward.Z * 3f), outside = HollowmereLayout.Ground(t, b.Centre.X + b.Outward.X * 3f, b.Centre.Z + b.Outward.Z * 3f);
                Assert.That(inside - outside, Is.GreaterThanOrEqualTo(TownWallPlan.DownhillDrop - 1e-3f), "buttress " + b.Index);
            }
            var stairs = plan.Where(s => s.Piece == WallPiece.Stair).OrderBy(s => s.Index).ToList();
            for (int i = 1; i < stairs.Count; i++)
                if (stairs[i].Run == stairs[i - 1].Run) Assert.That(stairs[i].Start - stairs[i - 1].Start, Is.GreaterThanOrEqualTo(TownWallPlan.StairMin - 4.5f), "stairs " + i);
        }

        [Test]
        public void Gate_SitsBetweenItsPiersFacingOut()
        {
            TownPoint c; float yaw; TownPoint o;
            TownWallPlan.GatePlacement(new TownPoint(-24.6f, -64.6f), new TownPoint(-18f, -64.6f), out c, out yaw, out o);
            Assert.AreEqual(-21.3f, c.X, 1e-3f); Assert.AreEqual(-64.6f, c.Z, 1e-3f);
            Assert.AreEqual(-1f, o.Z, 1e-3f); Assert.AreEqual(180f, System.Math.Abs(yaw), 1e-2f);
            TownWallPlan.GatePlacement(new TownPoint(59.2f, -44.1f), new TownPoint(54f, -47.9f), out c, out yaw, out o);
            Assert.That(o.X, Is.GreaterThan(0.4f), "East Gate faces east, away from the town");
        }
    }
}
