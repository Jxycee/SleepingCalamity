// Authority: none (pure EditMode tests). Core/Town/HollowmereLayout: the general lot clearance rule (lots keep off the town wall and the lane surface).
using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereLotClearanceTests
    {
        static float DistToSegment(TownPoint p, TownPoint a, TownPoint b)
        {
            float vx = b.X - a.X, vz = b.Z - a.Z, wx = p.X - a.X, wz = p.Z - a.Z;
            float t = Mathf.Clamp01((wx * vx + wz * vz) / (vx * vx + vz * vz));
            return TownPoint.Distance(p, new TownPoint(a.X + vx * t, a.Z + vz * t));
        }

        [Test]
        public void EveryRing2Lot_KeepsClearOfTheTownWallBank()
        {
            TownLayout t = HollowmereLayout.Create();
            int n = 0;
            foreach (Foundation f in t.Foundations)
            {
                if (f.Ring != 2) continue;
                float inner = float.MaxValue, outer = 0f;
                foreach (TownPoint k in f.Footprint.Corners())
                {
                    float d = HollowmereLayout.PlateauDistance(k.X, k.Z);
                    inner = Math.Min(inner, d); outer = Math.Max(outer, d);
                }
                // a lot wholly outside the wall is not held to the inner limit; a lot straddling the wall line is a gate lot (the lower gate's R2_23) that no setback can clear
                if (inner < HollowmereLayout.TownWallOffset && outer <= HollowmereLayout.TownWallOffset)
                    Assert.That(outer, Is.LessThanOrEqualTo(HollowmereLayout.LotWallLimit + 0.05f), f.Id + " reaches the town wall bank");
                n++;
            }
            Assert.That(n, Is.GreaterThan(10));
        }

        [Test]
        public void EveryRing2Lot_StaysOffTheLaneSurface()
        {
            TownLayout t = HollowmereLayout.Create();
            foreach (Foundation f in t.Foundations)
            {
                if (f.Ring != 2) continue;
                foreach (Lane lane in t.Lanes)
                {
                    if (lane.Ring != 2 || lane.Id.EndsWith("Stub", StringComparison.Ordinal)) continue;   // stubs branch off the lane the lots stand on
                    for (int i = 1; i < lane.Points.Length; i++)
                        foreach (TownPoint k in f.Footprint.Corners())
                            Assert.That(DistToSegment(k, lane.Points[i - 1], lane.Points[i]), Is.GreaterThanOrEqualTo(lane.Width * 0.5f - 0.05f), f.Id + " on lane " + lane.Id);
                }
            }
        }

        [Test]
        public void HeroesHomeDoorSpan_NeverProducesNaN()
        {
            SleepingCalamity.EditorTools.HeroesHomeArt.DoorSpan(float.NaN, 0f, 10f, 1.6f, out float a, out float c);
            Assert.That(a, Is.EqualTo(4.2f).Within(1e-4f)); Assert.That(c, Is.EqualTo(5.8f).Within(1e-4f));
            SleepingCalamity.EditorTools.HeroesHomeArt.DoorSpan(3f, 0f, 10f, 1.6f, out a, out c);
            Assert.That(a, Is.EqualTo(2.2f).Within(1e-4f)); Assert.That(c, Is.EqualTo(3.8f).Within(1e-4f));
        }

        [Test]
        public void NoLotIdIsSpecialCasedInTheLayout()
        {
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Core/Town/HollowmereLayout.cs"));
            Assert.IsFalse(Regex.IsMatch(src, "\\.Id\\s*==\\s*\"[A-Z]+[0-9_]+\""), "a lot id is compared to a literal in HollowmereLayout");
        }
    }
}
