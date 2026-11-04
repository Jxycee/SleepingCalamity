// Authority: none (pure EditMode tests). Procgen 3: per-floor enemy roster subset and one floor twist (CellarFloorRoster, CellarEncounterPlan.BuildForDive).
// Catches: an empty or off-eligible roster, a floor-1/4 roster that is not the full set, a rolled kind that is off the roster, a twist on floor 1/4, one twist kind twice in a dive,
// twist shares far from the placeholders, a Thin floor that empties a room / drops the gate-key carrier, a Crowded floor with no extra, an Ambush plan with no room, peers building
// different plans, and the layer moving the layout / dressing / existing mix goldens.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarRosterTests
    {
        const int Seeds = 2000;

        [Test]
        public void Roster_NeverEmpty_OnlyEligible_FullOnTheSetPieceFloors()
        {
            for (uint seed = 0; seed < Seeds; seed++)
                for (int floor = 1; floor <= 7; floor++)
                {
                    int m = CellarFloorRoster.RosterMask(seed, floor), e = CellarFloorRoster.EligibleMask(floor);
                    Assert.AreNotEqual(0, m, "seed " + seed + " floor " + floor);
                    Assert.AreEqual(0, m & ~e, "off-eligible kind, seed " + seed + " floor " + floor);
                    if (floor == 1 || floor == 3 || floor == 5) Assert.AreEqual(e, m);
                }
        }

        [Test]
        public void Roster_EachEligibleKindAppearsOnAReasonableShareOfFloors()
        {
            foreach (int floor in new[] { 2, 4, 6, 7 })
            {
                int[] hits = new int[3]; int[] bits = { CellarFloorRoster.SlimeBit, CellarFloorRoster.BroomBit, CellarFloorRoster.RatKingBit };
                for (uint seed = 0; seed < Seeds; seed++)
                {
                    int m = CellarFloorRoster.RosterMask(seed, floor);
                    for (int k = 0; k < 3; k++) if ((m & bits[k]) != 0) hits[k]++;
                }
                for (int k = 0; k < 3; k++)
                {
                    if ((CellarFloorRoster.EligibleMask(floor) & bits[k]) != 0)
                        Assert.That(hits[k] / (double)Seeds, Is.InRange(0.6, 0.95), "floor " + floor + " kind bit " + bits[k] + " share " + hits[k] / (double)Seeds);
                    else Assert.AreEqual(0, hits[k]);
                    TestContext.WriteLine("roster floor " + floor + " bit " + bits[k] + " share " + hits[k] / (double)Seeds);
                }
            }
        }

        // The pre-roster pick, copied from origin/main (CellarEnemyMix.Tier1Kind before procgen 3): weights and stream pinned here so a drift in the new code fails.
        static EnemyKind OldPick(uint seed, int floor, int room, int index)
        {
            int[][] weights = { new[] { 100, 0, 0 }, new[] { 60, 40, 0 }, new[] { 20, 50, 30 }, new[] { 30, 40, 30 } };
            int[] w = weights[System.Math.Max(1, System.Math.Min(4, floor)) - 1];
            var rng = new Pcg32(Rng.Derive(seed, "cellars.mix", ((ulong)floor << 32) | ((uint)room << 8) | (uint)index));
            int roll = rng.Range(0, w[0] + w[1] + w[2]);
            return roll < w[0] ? EnemyKind.Slime : roll < w[0] + w[1] ? EnemyKind.Broom : EnemyKind.RatKing;
        }

        [Test]
        public void Mix_NeverRollsOffRoster_AndFullRosterMatchesTheOldPick()
        {
            for (uint seed = 0; seed < 300; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    int mask = CellarFloorRoster.RosterMask(seed, floor);
                    for (int room = 0; room < 6; room++)
                        for (int i = 0; i < 4; i++)
                        {
                            EnemyKind k = CellarEnemyMix.Tier1Kind(seed, floor, room, i, mask);
                            Assert.AreNotEqual(0, CellarFloorRoster.BitOf(k) & mask, "kind " + k + " off roster " + mask);
                            Assert.AreEqual(OldPick(seed, floor, room, i), CellarEnemyMix.Tier1Kind(seed, floor, room, i), "old pick, seed " + seed + " floor " + floor);
                            Assert.AreEqual(OldPick(seed, floor, room, i), CellarEnemyMix.Tier1Kind(seed, floor, room, i, CellarFloorRoster.EligibleMask(floor)));
                        }
                }
        }

        [Test]
        public void Twist_NeverOnTheSetPieces_AtMostOnePerKindPerDive_SharesNearPlaceholders()
        {
            var count = new int[4]; int floors = 0;
            for (uint seed = 0; seed < 5000; seed++)
            {
                var used = new HashSet<FloorTwist>();
                for (int floor = 1; floor <= 7; floor++)
                {
                    FloorTwist t = CellarFloorRoster.TwistOf(seed, floor);
                    if (floor == 1 || floor == 3 || floor == 5) { Assert.AreEqual(FloorTwist.None, t); continue; }
                    floors++; count[(int)t]++;
                    if (t != FloorTwist.None) Assert.IsTrue(used.Add(t), "twist " + t + " twice in dive " + seed);
                }
            }
            // The first generated floor sits at the placeholders; later ones lose the repeats, so the overall share is a little under.
            Assert.That(count[(int)FloorTwist.Ambush] / (double)floors, Is.InRange(0.08, 0.14), "ambush (procgen 3b ambush, deliberate)");
            Assert.That(count[(int)FloorTwist.None] / (double)floors, Is.InRange(0.58, 0.72), "none");
            Assert.That(count[(int)FloorTwist.Thin] / (double)floors, Is.InRange(0.08, 0.14), "thin");
            Assert.That(count[(int)FloorTwist.Crowded] / (double)floors, Is.InRange(0.08, 0.14), "crowded");
            TestContext.WriteLine("twist shares of the generated floors: none " + count[0] / (double)floors + " ambush " + count[1] / (double)floors + " thin " + count[2] / (double)floors + " crowded " + count[3] / (double)floors);
        }

        [Test]
        public void Twist_AndRoster_AreDeterministic()
        {
            for (uint seed = 0; seed < 200; seed++)
                for (int floor = 1; floor <= 4; floor++)
                {
                    Assert.AreEqual(CellarFloorRoster.TwistOf(seed, floor), CellarFloorRoster.TwistOf(seed, floor));
                    Assert.AreEqual(CellarFloorRoster.LayerHash(seed, floor), CellarFloorRoster.LayerHash(seed, floor));
                }
        }

        [Test]
        public void Layer_GoldenHashes_Seed12345()
        {
            var got = new ulong[8];
            foreach (int f in new[] { 1, 2, 4, 6, 7 }) got[f] = CellarFloorRoster.LayerHash(12345, f);
            string all = got[1] + "UL, " + got[2] + "UL, " + got[4] + "UL, " + got[6] + "UL, " + got[7] + "UL";
            Assert.AreEqual(9929646806074584996UL, got[1], "floor 1; actual " + all); Assert.AreEqual(1000385178204227360UL, got[2], "floor 2; actual " + all); Assert.AreEqual(5465015992139406178UL, got[4], "floor 4; actual " + all);
            Assert.AreEqual(12528100526218511281UL, got[6], "floor 6; actual " + all); Assert.AreEqual(856329369820389952UL, got[7], "floor 7; actual " + all);
        }

        static List<(uint seed, int floor)> TwistedFloors(FloorTwist want)
        {
            var list = new List<(uint, int)>();
            for (uint seed = 0; seed < 3000 && list.Count < 120; seed++)
                foreach (int floor in new[] { 2, 4, 6, 7 })
                    if (CellarFloorRoster.TwistOf(seed, floor) == want) list.Add((seed, floor));
            return list;
        }

        [Test]
        public void Plan_Thin_DropsOnePerRoom_MinOne_KeepsCarrier()
        {
            int checkedFloors = 0;
            foreach (var (seed, floor) in TwistedFloors(FloorTwist.Thin))
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                foreach (int extra in new[] { 0, 1 })
                {
                    CellarEncounterPlan thin = CellarEncounterPlan.BuildForDive(l, d, extra, false), plain = CellarEncounterPlan.Build(l, d, extra, false);
                    Assert.AreEqual(FloorTwist.Thin, thin.Twist);
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        int p = plain.CountIn(r), t = thin.CountIn(r);
                        Assert.AreEqual(p > 1 ? p - 1 : p, t, "room " + r + " seed " + seed + " floor " + floor);
                        if (p > 0) Assert.GreaterOrEqual(t, 1);
                    }
                    if (l.Carrier != null && plain.Spawns.Any(s => s.Kind == EncounterKind.Authored && s.X == l.Carrier.X && s.Y == l.Carrier.Y))
                        Assert.IsTrue(thin.Spawns.Any(s => s.Kind == EncounterKind.Authored && s.X == l.Carrier.X && s.Y == l.Carrier.Y), "carrier dropped, seed " + seed);
                }
                checkedFloors++;
            }
            Assert.Greater(checkedFloors, 20);
        }

        [Test]
        public void Plan_Thin_WithStirExtras_KeepsEveryExtra()
        {
            int checkedRooms = 0;
            foreach (var (seed, floor) in TwistedFloors(FloorTwist.Thin))
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan thin = CellarEncounterPlan.BuildForDive(l, d, 1, false), plain = CellarEncounterPlan.Build(l, d, 1, false);
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    int pe = plain.Spawns.Count(s => s.RoomIndex == r && s.Kind == EncounterKind.Extra), te = thin.Spawns.Count(s => s.RoomIndex == r && s.Kind == EncounterKind.Extra);
                    bool removableAuthored = plain.Spawns.Any(s => s.RoomIndex == r && s.Kind == EncounterKind.Authored
                        && !(l.Carrier != null && l.Carrier.X == s.X && l.Carrier.Y == s.Y) && !(l.WardenCell.HasValue && l.WardenCell.Value.X == s.X && l.WardenCell.Value.Y == s.Y));
                    if (pe > 0 && plain.CountIn(r) > 1 && removableAuthored) { Assert.AreEqual(pe, te, "extras lost, room " + r + " seed " + seed + " floor " + floor); checkedRooms++; }
                }
            }
            Assert.Greater(checkedRooms, 10);
        }

        [Test]
        public void Plan_Crowded_AddsAtLeastOneExtra_NoSharedCells()
        {
            int checkedFloors = 0;
            foreach (var (seed, floor) in TwistedFloors(FloorTwist.Crowded))
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan crowd = CellarEncounterPlan.BuildForDive(l, d, 0, false), plain = CellarEncounterPlan.Build(l, d, 0, false);
                Assert.Greater(crowd.Spawns.Count, plain.Spawns.Count, "seed " + seed + " floor " + floor);
                Assert.AreEqual(crowd.Spawns.Count, crowd.Spawns.Select(s => (s.X, s.Y)).Distinct().Count());
                for (int r = 0; r < l.Rooms.Count; r++) Assert.LessOrEqual(crowd.CountIn(r), plain.CountIn(r) + 1);
                checkedFloors++;
            }
            Assert.Greater(checkedFloors, 20);
        }

        [Test]
        public void Plan_Ambush_NamesACombatRoom_SpawnsUnchanged()
        {
            int checkedFloors = 0;
            foreach (var (seed, floor) in TwistedFloors(FloorTwist.Ambush))
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                CellarEncounterPlan a = CellarEncounterPlan.BuildForDive(l, d, 1, false), plain = CellarEncounterPlan.Build(l, d, 1, false);
                Assert.AreEqual(plain.Spawns.Count, a.Spawns.Count);
                Assert.AreEqual(FloorTwist.Ambush, a.Twist);
                Assert.That(a.AmbushRoom, Is.InRange(0, l.Rooms.Count - 1)); Assert.AreEqual(RoomKind.Combat, l.Rooms[a.AmbushRoom].Room.Kind);
                checkedFloors++;
            }
            Assert.Greater(checkedFloors, 20);
        }

        [Test]
        public void Plan_PlainBuildCarriesNoTwist_SetPiecesNone_PeersAgree()
        {
            for (uint seed = 0; seed < 60; seed++)
                for (int floor = 1; floor <= 7; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellarEncounterPlan a = CellarEncounterPlan.BuildForDive(l, d, 1, true), b = CellarEncounterPlan.BuildForDive(l, d, 1, true);
                    Assert.AreEqual(a.Spawns.Count, b.Spawns.Count); Assert.AreEqual(a.Twist, b.Twist); Assert.AreEqual(a.RosterMask, b.RosterMask); Assert.AreEqual(a.AmbushRoom, b.AmbushRoom);
                    Assert.AreEqual(FloorTwist.None, CellarEncounterPlan.Build(l, d, 1, true).Twist);
                    if (floor == 1 || floor == 3 || floor == 5) Assert.AreEqual(FloorTwist.None, a.Twist);
                }
        }
    }
}
