// Tests/EditMode/EncounterBuilderTests.cs: EncounterBuilder, EncounterValidator and the room x party x floor x seed sweep (encounter templates plan, Tasks 3-5). Core-only (EnemyKind fully qualified).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public class EncounterBuilderTests
    {
        static IEnumerable<RoomDataV2> CombatRooms => RoomEncounterTable.RoomIds.Select(CellarRoomsV2.Find);
        const int AllKinds = CellarFloorRoster.SlimeBit | CellarFloorRoster.RatKingBit | CellarFloorRoster.BroomBit;

        static string Dump(RoomEncounter e) => e == null ? "null" : e.TemplateId + ":" + string.Join(";", e.Spawns.Select(s => s.Cell + "/" + s.Kind + "/w" + s.Wave));

        [Test]
        public void SeedKey_CastsBeforeShifting()
        {
            Assert.That(EncounterBuilder.SeedKey(3, 5), Is.EqualTo((3UL << 32) | 5UL));
            Assert.That(EncounterBuilder.SeedKey(200, int.MaxValue), Is.EqualTo((200UL << 32) | (uint)int.MaxValue));
        }

        [Test]
        public void Build_IsDeterministic_PerSeed()
        {
            foreach (RoomDataV2 r in CombatRooms)
                for (int players = 1; players <= 4; players++)
                    Assert.That(Dump(EncounterBuilder.Build(r, null, 77, 3, 2, players, AllKinds)), Is.EqualTo(Dump(EncounterBuilder.Build(r, null, 77, 3, 2, players, AllKinds))), r.Base.Id);
        }

        [Test]
        public void Build_DifferentSeeds_PickMoreThanOneTemplate()
        {
            RoomDataV2 r = CellarRoomsV2.Find("coal_boiler_hall");
            var ids = new HashSet<string>();
            for (uint s = 1; s <= 60; s++) ids.Add(EncounterBuilder.Build(r, null, s, 3, 0, 1, AllKinds).TemplateId);
            Assert.That(ids.Count, Is.GreaterThan(1));
            Assert.That(ids, Does.Not.Contain("ambush"), "Ambush needs the ambush room");
        }

        [Test]
        public void Build_LiveCount_IsMonotoneInPlayers_AndCapped()
        {
            foreach (RoomDataV2 r in CombatRooms)
                for (uint seed = 1; seed <= 20; seed++)
                    foreach (string tid in RoomEncounterTable.For(r.Base.Id))
                    {
                        int prev = 0;
                        for (int players = 1; players <= 4; players++)
                        {
                            RoomEncounter e = EncounterBuilder.Build(r, tid, seed, 3, 1, players, AllKinds, tid == "ambush");
                            Assert.That(e.PeakLive, Is.GreaterThanOrEqualTo(prev), r.Base.Id + " " + tid);
                            Assert.That(e.PeakLive, Is.LessThanOrEqualTo(GameMetrics.MaxLiveEnemies));
                            prev = e.PeakLive;
                        }
                    }
        }

        [Test]
        public void PerPlayerAdds_NeverExceedCoopScaling()
        {
            foreach (RoomDataV2 r in CombatRooms)
                foreach (string tid in RoomEncounterTable.For(r.Base.Id))
                {
                    int solo = EncounterBuilder.Build(r, tid, 5, 3, 0, 1, AllKinds, true).LiveIn(0);
                    for (int players = 2; players <= 4; players++)
                        Assert.That(EncounterBuilder.Build(r, tid, 5, 3, 0, players, AllKinds, true).LiveIn(0) - solo, Is.LessThanOrEqualTo(CoopScaling.ExtraEnemiesPerRoom(players)), r.Base.Id + " " + tid);
                }
        }

        [Test]
        public void SoloLiveCount_IsAtMostTheAuthoredPlanCountPlusOne()
        {
            foreach (RoomDataV2 r in CombatRooms)
                for (int floor = 1; floor <= 7; floor++)
                    for (uint seed = 1; seed <= 20; seed++)
                    {
                        RoomEncounter e = EncounterBuilder.Build(r, null, seed, floor, 0, 1, AllKinds);
                        Assert.That(e.PeakLive, Is.LessThanOrEqualTo(r.Base.Spawns.Count + 1), r.Base.Id + " floor " + floor + " seed " + seed);
                    }
        }

        [Test]
        public void Floors1To2_NeverRollReinforcedOrMoreThanTwoKinds()
        {
            foreach (RoomDataV2 r in CombatRooms)
                for (int floor = 1; floor <= 2; floor++)
                    for (uint seed = 1; seed <= 40; seed++)
                    {
                        RoomEncounter e = EncounterBuilder.Build(r, null, seed, floor, 0, 4, AllKinds, seed % 3 == 0);
                        Assert.That(e.WaveCount, Is.EqualTo(1));
                        Assert.That(e.TemplateId, Is.Not.EqualTo("reinforced"));
                        Assert.That(e.Spawns.Select(s => s.Kind).Distinct().Count(), Is.LessThanOrEqualTo(2));
                    }
        }

        [Test]
        public void Ambush_OnlyInTheAmbushRoom_NeverInAHazardRoom()
        {
            foreach (RoomDataV2 r in CombatRooms)
                for (uint seed = 1; seed <= 30; seed++)
                {
                    Assert.That(EncounterBuilder.Build(r, null, seed, 4, 0, 1, AllKinds, false).TemplateId, Is.Not.EqualTo("ambush"), r.Base.Id);
                    RoomEncounter amb = EncounterBuilder.Build(r, null, seed, 4, 0, 1, AllKinds, true);
                    if (RoomEncounterTable.For(r.Base.Id).Contains("ambush")) Assert.That(amb.TemplateId, Is.EqualTo("ambush"), r.Base.Id);
                    else Assert.That(amb.TemplateId, Is.Not.EqualTo("ambush"), r.Base.Id);
                }
            foreach (string id in RoomEncounterTable.RoomIds.Where(i => RoomEncounterTable.For(i).Contains("ambush")))
            {
                RoomDataV2 room = CellarRoomsV2.Find(id);
                Assert.That(EncounterBuilder.FreeCells(room).Count - room.Base.Spawns.Count, Is.GreaterThanOrEqualTo(3), id + ": Ambush only in empty-looking rooms");
                Assert.That(room.Regions, Is.Empty, id);
            }
            // a room with a hazard region never takes Ambush even when it is the Ambush room
            RoomDataV2 pantry = CellarRoomsV2.Find("larder_pantry");
            RoomDataV2 wet = RoomDataV2.Author(pantry.Base, null, new[] { "w..", "...", "...", "...", "..." }, new[] { new GimmickRegion("w", 'w', GimmickKind.Water, 0, 100) }, null, null, null, RoomAffordance.None);
            Assert.That(EncounterBuilder.IsLegal(EncounterTemplates.Find("ambush"), wet, 4, true), Is.False);
            Assert.That(EncounterBuilder.IsLegal(EncounterTemplates.Find("ambush"), pantry, 4, true), Is.True);
        }

        [Test]
        public void SmallPartyRoom_HoldsItsGroupAtMaxHeroes()
        {
            RoomDataV2 r = CellarRoomsV2.Find("coal_stoke_chicane");
            Assert.That(EncounterBuilder.HeldHeroes(r.Base.Id, 4), Is.EqualTo(2));
            foreach (string tid in RoomEncounterTable.For(r.Base.Id))
            {
                RoomEncounter two = EncounterBuilder.Build(r, tid, 9, 3, 0, 2, AllKinds), four = EncounterBuilder.Build(r, tid, 9, 3, 0, 4, AllKinds);
                Assert.That(Dump(four), Is.EqualTo(Dump(two)), tid);
                Assert.That(EncounterValidator.Validate(r, four, AllKinds), Is.Empty);
            }
        }

        [Test]
        public void RosterRemap_NeverLeavesTheRoster_NorUsesDoughWarden()
        {
            int slimeOnly = CellarFloorRoster.SlimeBit;
            foreach (RoomDataV2 r in CombatRooms)
            {
                RoomEncounter e = EncounterBuilder.Build(r, null, 3, 2, 0, 3, slimeOnly);
                Assert.That(e.Spawns.All(s => s.Kind == SleepingCalamity.Core.Combat.EnemyKind.Slime), Is.True, r.Base.Id);
            }
            Assert.That(EncounterBuilder.Remap(SleepingCalamity.Core.Combat.EnemyKind.Broom, CellarFloorRoster.RatKingBit), Is.EqualTo(SleepingCalamity.Core.Combat.EnemyKind.RatKing));
            Assert.That(EncounterBuilder.Remap(SleepingCalamity.Core.Combat.EnemyKind.Broom, AllKinds), Is.EqualTo(SleepingCalamity.Core.Combat.EnemyKind.Broom));
        }

        [Test]
        public void ReservedCells_KeepTheirSpawn()
        {
            foreach (RoomDataV2 r in CombatRooms)
            {
                var reserved = r.Base.Spawns.Take(2).Select(s => new CellPos(s.X, s.Y)).ToList();
                for (int players = 1; players <= 4; players++)
                {
                    RoomEncounter e = EncounterBuilder.Build(r, null, 11, 3, 0, players, AllKinds, false, reserved);
                    foreach (CellPos c in reserved) Assert.That(e.Spawns.Any(s => s.Wave == 0 && s.Cell.X == c.X && s.Cell.Y == c.Y), Is.True, r.Base.Id + " " + c);
                }
            }
        }

        [Test]
        public void AuthoredSpawnCells_AreUsedFirst()
        {
            RoomDataV2 r = CellarRoomsV2.Find("wine_aisles");
            RoomEncounter e = EncounterBuilder.Build(r, "rush_bombard", 1, 3, 0, 1, AllKinds);
            Assert.That(e.Spawns.Count(s => s.OnAuthoredCell), Is.EqualTo(System.Math.Min(e.Spawns.Count, r.Base.Spawns.Count)));
        }

        [Test]
        public void SetPiece_AndUnknownRooms_HaveNoEncounter()
        {
            Assert.That(EncounterBuilder.Build(CellarRoomsV2.Find("bakery_heart"), null, 1, 4, 0, 1, AllKinds), Is.Null);
        }

        [Test]
        public void ToExtraCells_ListsWaveZeroCells()
        {
            RoomEncounter e = EncounterBuilder.Build(CellarRoomsV2.Find("coal_boiler_hall"), "reinforced", 1, 3, 0, 2, AllKinds);
            Assert.That(EncounterBuilder.ToExtraCells(e).Count, Is.EqualTo(e.LiveIn(0)));
            Assert.That(e.LiveIn(1), Is.EqualTo(2));
        }

        // ---- validator: one negative per rule ----

        static RoomEncounter Good() => EncounterBuilder.Build(CellarRoomsV2.Find("larder_cold_store"), "pack", 1, 3, 0, 1, AllKinds);
        static RoomDataV2 Cold => CellarRoomsV2.Find("larder_cold_store");
        static RoomEncounter With(RoomEncounter e, IEnumerable<PlannedSpawn> spawns, int floor = -1, string template = null, int? held = null)
            => new RoomEncounter(e.RoomId, template ?? e.TemplateId, floor < 0 ? e.Floor : floor, e.RoomIndex, e.Players, held ?? e.HeldHeroes, spawns);
        static PlannedSpawn At(int x, int y, SleepingCalamity.Core.Combat.EnemyKind k = SleepingCalamity.Core.Combat.EnemyKind.RatKing, int wave = 0, SpawnTier tier = SpawnTier.Tier1) => new PlannedSpawn(new CellPos(x, y), k, tier, wave, "t", false);
        static void Expect(List<string> errs, string fragment) => Assert.That(errs.Any(m => m.Contains(fragment)), Is.True, fragment + " not reported in:\n" + string.Join("\n", errs));

        [Test]
        public void Validator_AcceptsTheBuilderOutput() => Assert.That(EncounterValidator.Validate(Cold, Good(), AllKinds), Is.Empty);

        [Test]
        public void Validator_FlagsEachRule()
        {
            const SleepingCalamity.Core.Combat.EnemyKind R = SleepingCalamity.Core.Combat.EnemyKind.RatKing, S = SleepingCalamity.Core.Combat.EnemyKind.Slime, B = SleepingCalamity.Core.Combat.EnemyKind.Broom;
            RoomEncounter g = Good();
            Expect(EncounterValidator.Validate(Cold, With(g, g.Spawns, template: "nope"), AllKinds), "unknown template");
            RoomDataV2 hall = CellarRoomsV2.Find("larder_hanging_hall");   // pillar at (1,1)
            var inHall = new RoomEncounter(hall.Base.Id, "gel_swarm", 3, 0, 1, 1, new[] { At(1, 1, S), At(0, 0, S), At(1, 0, S), At(0, 2, S) });
            Expect(EncounterValidator.Validate(hall, inHall, AllKinds), "not a standable floor cell");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(1, 0), At(0, 1), At(2, 2) }), AllKinds), "door cell");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1), At(0, 1), At(2, 2) }), AllKinds), "two spawns on one cell");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1), At(2, 2), At(1, 1, tier: SpawnTier.Tier2) }), AllKinds), "above tier 1");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1), At(2, 2), At(1, 1, SleepingCalamity.Core.Combat.EnemyKind.DoughWarden) }), AllKinds), "above tier 1");
            Expect(EncounterValidator.Validate(Cold, g, CellarFloorRoster.SlimeBit), "off the floor roster");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1, R), At(2, 2, S), At(1, 1, B) }, floor: 2), AllKinds), "more than 2 kinds");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1), At(2, 2), At(1, 1, wave: 1) }, floor: 2), AllKinds), "one wave only");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(0, 1), At(2, 2), At(1, 1, wave: 2) }), AllKinds), "out of range");
            Expect(EncounterValidator.Validate(Cold, With(g, new[] { At(1, 1, wave: 1), At(1, 2, wave: 1) }), AllKinds), "wave 0 is empty");
            var nine = new List<PlannedSpawn>(); foreach (var c in EncounterBuilder.FreeCells(Cold).Take(9)) nine.Add(At(c.X, c.Y));
            Expect(EncounterValidator.Validate(Cold, With(g, nine), AllKinds), "live in wave 0");
            RoomDataV2 chicane = CellarRoomsV2.Find("coal_stoke_chicane");   // 8 floor cells, 2 heroes: 6 enemies need 9
            var six = EncounterBuilder.FreeCells(chicane).Select(c => At(c.X, c.Y)).ToList(); six.Add(six[0]);   // 5 free cells + a repeat = 6
            Expect(EncounterValidator.Validate(chicane, new RoomEncounter(chicane.Base.Id, "guarded_caster", 3, 0, 2, 2, six), AllKinds), "FightSpace");
            Expect(EncounterValidator.Validate(Cold, With(g, g.Spawns, template: "ambush"), AllKinds, false), "outside the Ambush room");
            RoomDataV2 wet = RoomDataV2.Author(Cold.Base, null, new[] { "w..", "...", "...", "...", "..." }, new[] { new GimmickRegion("w", 'w', GimmickKind.Water, 0, 100) }, null, null, null, RoomAffordance.None);
            Expect(EncounterValidator.Validate(wet, With(g, g.Spawns, template: "ambush"), AllKinds, true), "hazard region");
            // the room must have what the template wants: the ring has no open 3x3
            RoomDataV2 ring = CellarRoomsV2.Find("coal_furnace_ring");
            var ringPack = new RoomEncounter(ring.Base.Id, "pack", 3, 0, 1, 1, new[] { At(0, 0), At(1, 0), At(0, 1) });
            Expect(EncounterValidator.Validate(ring, ringPack, AllKinds), "wants");
            // guardrails: a template whose solo group is over authored + 1 (gel swarm 4 in the 2-spawn cold store)
            var gel = new RoomEncounter(Cold.Base.Id, "gel_swarm", 3, 0, 1, 1, new[] { At(0, 1, S), At(2, 2, S), At(1, 1, S), At(1, 3, S) });
            Expect(EncounterValidator.Validate(Cold, gel, AllKinds), "authored spawn count");
            var tooManyAdds = new RoomEncounter(Cold.Base.Id, "pack", 3, 0, 1, 1, new[] { At(0, 1), At(2, 2), At(1, 1), At(1, 3), At(2, 3) });
            Expect(EncounterValidator.Validate(Cold, tooManyAdds, AllKinds), "ExtraEnemiesPerRoom");
        }

        // ---- sweep (Task 5): every pool combat room x players 1-4 x floors 1-7 x seeds ----

        static void Sweep(int seeds)
        {
            var errs = new List<string>(); int built = 0;
            foreach (RoomDataV2 r in CombatRooms)
                for (int floor = 1; floor <= 7; floor++)
                    for (uint seed = 1; seed <= (uint)seeds; seed++)
                    {
                        int roster = CellarFloorRoster.RosterMask(seed, floor);
                        bool ambushRoom = seed % 4 == 0;
                        for (int players = 1; players <= 4; players++)
                        {
                            RoomEncounter e = EncounterBuilder.Build(r, null, seed, floor, (int)(seed % 12), players, roster, ambushRoom);
                            if (e == null) { errs.Add(r.Base.Id + " floor " + floor + " seed " + seed + ": no legal template"); continue; }
                            built++;
                            errs.AddRange(EncounterValidator.Validate(r, e, roster, ambushRoom));
                            if (e.PeakLive > r.Base.Spawns.Count + 1 + CoopScaling.ExtraEnemiesPerRoom(e.HeldHeroes)) errs.Add(r.Base.Id + ": live " + e.PeakLive + " over plan count + 1 + extras");
                            if (errs.Count > 20) { Assert.Fail(string.Join("\n", errs)); }
                        }
                    }
            Assert.That(errs, Is.Empty, string.Join("\n", errs.Take(20)));
            Assert.That(built, Is.EqualTo(12 * 7 * seeds * 4));
        }

        [Test] public void Sweep_Fast_AllRoomsPartiesFloors_AreValidatorClean() => Sweep(50);

        [Test, Category("Soak")] public void Sweep_Soak_AllRoomsPartiesFloors_AreValidatorClean() => Sweep(1000);
    }
}
