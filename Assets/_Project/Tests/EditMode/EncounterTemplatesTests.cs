// Tests/EditMode/EncounterTemplatesTests.cs: the eight encounter templates and the room table (encounter templates plan, Tasks 1-2). Core-only; EnemyKind is fully qualified so sc_core_tests.py picks the file up.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class EncounterTemplatesTests
    {
        [Test]
        public void Templates_AreTheEightResearchOnes_WithUniqueIds()
        {
            Assert.That(EncounterTemplates.All.Count, Is.EqualTo(8));
            Assert.That(EncounterTemplates.All.Select(t => t.Id).Distinct().Count(), Is.EqualTo(8));
            foreach (EncounterTemplate t in EncounterTemplates.All) Assert.That(EncounterTemplates.Find(t.Id), Is.SameAs(t));
            Assert.That(EncounterTemplates.Find("elite_arena"), Is.Null);
            Assert.That(EncounterTemplates.Find("hunter"), Is.Null);
        }

        [Test]
        public void Roles_AreTierOne_AndWavesAtMostTwoDeep()
        {
            foreach (EncounterTemplate t in EncounterTemplates.All)
            {
                Assert.That(t.WaveCount, Is.InRange(1, EncounterTemplate.MaxWaves), t.Id);
                foreach (EncounterRole r in t.Roles)
                {
                    Assert.That(r.Kind, Is.Not.EqualTo(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden), t.Id);
                    Assert.That(r.Count, Is.GreaterThan(0), t.Id);
                    Assert.That(r.Wave, Is.InRange(0, 1), t.Id);
                }
                foreach (var k in t.PerPlayerAdds) Assert.That(k, Is.Not.EqualTo(SleepingCalamity.Core.Combat.EnemyKind.DoughWarden), t.Id);
                Assert.That(t.BaseInWave(0), Is.GreaterThan(0), t.Id);
            }
        }

        [Test]
        public void SoloBaseCounts_MatchTheResearchTable_WithTheTwoDocumentedCuts()
        {
            var expected = new System.Collections.Generic.Dictionary<string, int>
            { ["pack"] = 3, ["gel_swarm"] = 4 /* research 5 */, ["shield_wall"] = 4, ["rush_bombard"] = 4, ["guarded_caster"] = 3, ["mixed_tier"] = 3, ["ambush"] = 2, ["reinforced"] = 5 };
            foreach (var kv in expected) Assert.That(EncounterTemplates.Find(kv.Key).BaseCount, Is.EqualTo(kv.Value), kv.Key);
            Assert.That(EncounterTemplates.Find("reinforced").BaseInWave(0), Is.EqualTo(3));
            Assert.That(EncounterTemplates.Find("reinforced").BaseInWave(1), Is.EqualTo(2));
        }

        [Test]
        public void PerPlayerAdds_NeverExceedCoopScaling_AndTheEightCap()
        {
            foreach (EncounterTemplate t in EncounterTemplates.All)
            {
                Assert.That(t.PerPlayerAdds.Count, Is.LessThanOrEqualTo(SleepingCalamity.Core.Combat.CoopScaling.ExtraEnemiesPerRoom(4)), t.Id);   // +1 per extra player
                Assert.That(t.SoloPeak + t.PerPlayerAdds.Count, Is.LessThanOrEqualTo(GameMetrics.MaxLiveEnemies), t.Id);
                foreach (var k in t.PerPlayerAdds) Assert.That(t.Kinds, Does.Contain(k), t.Id + ": an add must not introduce a new kind");
            }
        }

        [Test]
        public void Floors1To2_AllowOneWaveAndTwoKindsOnly()
        {
            foreach (EncounterTemplate t in EncounterTemplates.All)
            {
                bool ok = t.LegalOnFloor(1);
                Assert.That(t.LegalOnFloor(2), Is.EqualTo(ok), t.Id);
                if (ok) { Assert.That(t.IsMultiWave, Is.False, t.Id); Assert.That(t.DistinctKinds, Is.LessThanOrEqualTo(2), t.Id); }
                Assert.That(t.LegalOnFloor(3), Is.True, t.Id);
            }
            Assert.That(EncounterTemplates.Find("reinforced").LegalOnFloor(2), Is.False);
            Assert.That(EncounterTemplates.Find("mixed_tier").LegalOnFloor(1), Is.False);
            Assert.That(EncounterTemplates.Find("pack").LegalOnFloor(1), Is.True);
        }

        [Test]
        public void Table_CoversTheTwelvePoolCombatRooms_WithTwoOrThreeKnownTemplates()
        {
            var combat = CellarRoomsV2.Pool.Where(r => r.Base.Kind == RoomKind.Combat).Select(r => r.Base.Id).ToList();
            Assert.That(combat.Count, Is.EqualTo(12));
            Assert.That(RoomEncounterTable.RoomIds.OrderBy(x => x), Is.EqualTo(combat.OrderBy(x => x)));
            foreach (string id in combat)
            {
                var ts = RoomEncounterTable.For(id);
                Assert.That(ts.Count, Is.InRange(2, 3), id);
                Assert.That(ts.Distinct().Count(), Is.EqualTo(ts.Count), id);
                foreach (string t in ts) Assert.That(EncounterTemplates.Find(t), Is.Not.Null, id + " -> " + t);
            }
            Assert.That(RoomEncounterTable.For("bakery_heart"), Is.Empty, "set pieces keep their authored guard");
        }

        [Test]
        public void EveryTemplate_ReachesAtLeastOneRoom()
        {
            foreach (EncounterTemplate t in EncounterTemplates.All)
                Assert.That(RoomEncounterTable.RoomIds.Any(id => RoomEncounterTable.For(id).Contains(t.Id)), Is.True, t.Id);
        }

        [Test]
        public void TableEntries_FitTheirRoom_AffordanceAndSoloGuardrail()
        {
            foreach (string id in RoomEncounterTable.RoomIds)
            {
                RoomDataV2 room = CellarRoomsV2.Find(id);
                int authored = room.Base.Spawns.Count;
                foreach (string tid in RoomEncounterTable.For(id))
                {
                    EncounterTemplate t = EncounterTemplates.Find(tid);
                    Assert.That((room.Affordances & t.Wants), Is.EqualTo(t.Wants), id + " lacks what " + tid + " wants");
                    Assert.That(t.SoloPeak, Is.LessThanOrEqualTo(authored + 1), id + " / " + tid + ": solo live may exceed today's plan count by at most 1");
                }
            }
        }
    }
}
