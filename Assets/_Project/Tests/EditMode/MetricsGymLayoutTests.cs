// Tests/EditMode/MetricsGymLayoutTests.cs: the metrics gym laid out as pure data, ten lanes of real measurements (metrics gym plan, Task 6). Core-only.
using System;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class MetricsGymLayoutTests
    {
        static GymPiece[] Lane(string lane) => MetricsGymLayout.Build().Where(p => p.Lane == lane).ToArray();

        [Test] public void Lanes_AreTheTen() =>
            Assert.That(MetricsGymLayout.Lanes, Is.EqualTo(new[] { "run", "dodge", "gaps", "ledges", "stair", "doors", "water", "camera", "fight", "pilot" }));

        [Test] public void EveryLane_HasPieces()
        {
            foreach (string l in MetricsGymLayout.Lanes) Assert.That(Lane(l), Is.Not.Empty, l);
        }

        [Test] public void RunMarkers_AreOneSecondApart()
        {
            var m = Lane("run").Where(p => p.Shape == GymShape.Marker).OrderBy(p => p.Z).ToArray();
            Assert.That(m.Length, Is.EqualTo(3));
            for (int i = 0; i < m.Length; i++) Assert.That(m[i].Z, Is.EqualTo(MovementMath.RunSpeed * (i + 1)).Within(0.001f));
            Assert.That(m[1].Z - m[0].Z, Is.EqualTo(MovementMath.RunSpeed).Within(0.001f));
        }

        [Test] public void DodgeMarker_IsOneDodgeAway() =>
            Assert.That(Lane("dodge").Single(p => p.Shape == GymShape.Marker).Z, Is.EqualTo(MovementProbe.DodgeDistance()).Within(0.001f));

        [Test] public void Gaps_StraddleTheRunJump()
        {
            var floors = Lane("gaps").Where(p => p.Name.StartsWith("floor")).OrderBy(p => p.Z).ToArray();
            var gaps = new float[floors.Length - 1];
            for (int i = 0; i < gaps.Length; i++) gaps[i] = (floors[i + 1].Z - floors[i + 1].SZ / 2) - (floors[i].Z + floors[i].SZ / 2);
            Assert.That(gaps.Length, Is.EqualTo(5));
            Assert.That(gaps[0], Is.EqualTo(2.0f).Within(0.001f)); Assert.That(gaps[1], Is.EqualTo(3.0f).Within(0.001f));
            Assert.That(gaps[2], Is.EqualTo(MovementProbe.RunJumpDistance() - 0.2f).Within(0.001f));
            Assert.That(gaps[3], Is.EqualTo(MovementProbe.RunJumpDistance() + 0.3f).Within(0.001f));
            Assert.That(gaps[4], Is.EqualTo(GameMetrics.CellMeters).Within(0.001f));
        }

        [Test] public void Ledges_IncludeTheClimbBoundAndOneLevel()
        {
            var h = Lane("ledges").Where(p => p.Name.StartsWith("ledge")).Select(p => p.SY).ToArray();
            Assert.That(h, Has.Some.EqualTo(GameMetrics.MaxClimbableLedge).Within(0.001f));
            Assert.That(h, Has.Some.EqualTo(GameMetrics.LevelHeightCm / 100f).Within(0.001f));
            Assert.That(h, Has.Some.EqualTo(MovementMath.StepOffset).Within(0.001f));
        }

        [Test] public void Stair_IsOneCellOfTenSteps()
        {
            var steps = Lane("stair").Where(p => p.Name.StartsWith("step")).OrderBy(p => p.Z).ToArray();
            Assert.That(steps.Length, Is.EqualTo(GameMetrics.StepsPerLevel));
            Assert.That(steps.Last().SY, Is.EqualTo(GameMetrics.LevelHeightCm / 100f).Within(0.001f));
            Assert.That(steps.Sum(s => s.SZ), Is.EqualTo(GameMetrics.CellMeters).Within(0.001f));
            Assert.That(Lane("stair").Single(p => p.Name == "deck").SY, Is.EqualTo(GameMetrics.LevelHeightCm / 100f).Within(0.001f));
        }

        [Test] public void Water_HasWadeAndDeepPools()
        {
            var pools = Lane("water").Where(p => p.Name.StartsWith("pool")).OrderBy(p => p.Z).ToArray();
            Assert.That(pools.Select(p => p.SY), Is.EqualTo(new[] { GameMetrics.WadeMaxCm / 100f, 1.0f }).Within(0.001f));
            Assert.That(pools.All(p => p.SX == 4f && p.SZ == 4f), Is.True);
        }

        [Test] public void FightRoom_IsTheSmallestFullPartyRoom()
        {
            var fight = Lane("fight");
            var w = fight.Where(p => p.Name.StartsWith("wall")).ToDictionary(p => p.Name);
            float spanX = (w["wall_east"].X - w["wall_east"].SX / 2) - (w["wall_west"].X + w["wall_west"].SX / 2);
            float spanZ = (w["wall_north"].Z - w["wall_north"].SZ / 2) - (w["wall_south"].Z + w["wall_south"].SZ / 2);
            Assert.That(FightSpace.SmallestRectangle(4, 8), Is.EqualTo((3, 5)));
            Assert.That(spanX, Is.EqualTo(12f).Within(0.001f)); Assert.That(spanZ, Is.EqualTo(20f).Within(0.001f));
            var caps = fight.Where(p => p.Shape == GymShape.Capsule).ToArray();
            Assert.That(caps.Count(c => c.Name.StartsWith("hero")), Is.EqualTo(4));
            Assert.That(caps.Count(c => c.Name.StartsWith("enemy")), Is.EqualTo(8));
            var floor = fight.Single(p => p.Name == "floor");
            foreach (var c in caps)
            {
                Assert.That(c.X - c.SX / 2, Is.GreaterThanOrEqualTo(floor.X - floor.SX / 2), c.Name); Assert.That(c.X + c.SX / 2, Is.LessThanOrEqualTo(floor.X + floor.SX / 2), c.Name);
                Assert.That(c.Z - c.SX / 2, Is.GreaterThanOrEqualTo(floor.Z - floor.SZ / 2), c.Name); Assert.That(c.Z + c.SX / 2, Is.LessThanOrEqualTo(floor.Z + floor.SZ / 2), c.Name);
            }
            for (int i = 0; i < caps.Length; i++)
                for (int j = i + 1; j < caps.Length; j++)
                {
                    float d = (float)Math.Sqrt((caps[i].X - caps[j].X) * (caps[i].X - caps[j].X) + (caps[i].Z - caps[j].Z) * (caps[i].Z - caps[j].Z));
                    Assert.That(d, Is.GreaterThan((caps[i].SX + caps[j].SX) / 2), caps[i].Name + " vs " + caps[j].Name);
                }
        }

        [Test] public void Pilot_HasBothStates()
        {
            var p = Lane("pilot").Where(x => x.Shape == GymShape.PilotRoom).OrderBy(x => x.X).ToArray();
            Assert.That(p.Select(x => x.PilotState), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test] public void Lanes_DoNotOverlap()
        {
            var b = MetricsGymLayout.Lanes.Select(l =>
            {
                var ps = Lane(l);
                return (l, minX: ps.Min(p => p.X - p.SX / 2), maxX: ps.Max(p => p.X + p.SX / 2), minZ: ps.Min(p => p.Z - p.SZ / 2), maxZ: ps.Max(p => p.Z + p.SZ / 2));
            }).ToArray();
            for (int i = 0; i < b.Length; i++)
                for (int j = i + 1; j < b.Length; j++)
                {
                    bool apart = b[i].maxX <= b[j].minX || b[j].maxX <= b[i].minX || b[i].maxZ <= b[j].minZ || b[j].maxZ <= b[i].minZ;
                    Assert.That(apart, Is.True, b[i].l + " overlaps " + b[j].l);
                }
        }

        // MetricsGymBuilder reuses objects by "<name> | <label>", so two pieces with the same key in one lane would collapse into one.
        [Test] public void PieceKeys_AreUniquePerLane()
        {
            var dup = MetricsGymLayout.Build().GroupBy(p => (p.Lane, p.Label.Length > 0 ? p.Name + " | " + p.Label : p.Name)).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.That(dup, Is.Empty);
        }

        [Test] public void Build_IsDeterministic() => Assert.That(MetricsGymLayout.Build(), Is.EqualTo(MetricsGymLayout.Build()));
    }
}
