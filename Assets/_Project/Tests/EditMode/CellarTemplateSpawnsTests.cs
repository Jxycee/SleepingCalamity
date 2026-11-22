// Authority: none (pure EditMode tests). CellarTemplateSpawns (spawner wiring): the host's per-room spawn list from encounter templates.
// Catches: a template room larger than the live plan count (harder than today), more than MaxLiveEnemies in a wave, FightSpace overflow, two enemies on one cell in a wave, a spawn on a cell the plan
// calls illegal (chunk solid, chest, door), the gate-key carrier losing its cell, a kind off the roster or DoughWarden/Tier 2 in a Cellars room, waves deeper than 2, a changed golden pick.
// Does NOT cover the host sync (CellarContentsSync wave-1 timing, room-clear ordering); those are PlayMode/DiveBot and the 2-player walk.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarTemplateSpawnsTests
    {
        static IEnumerable<(CellarFloorLayout l, CellarFloorDressing d, CellarEncounterPlan p)> Floors(int seeds)
        {
            for (uint seed = 1; seed <= seeds; seed++)
                foreach (int floor in new[] { 2, 4, 6, 7 })
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    foreach (int extra in new[] { 0, 2 }) yield return (l, d, CellarEncounterPlan.BuildForDive(l, d, extra, false));
                }
        }

        [Test]
        public void Resolve_HoldsPlanCount_Caps_LegalCells_AndCarrier()
        {
            int templated = 0;
            foreach (var (l, d, p) in Floors(25))
                for (int r = 0; r < l.Rooms.Count; r++)
                    for (int players = 1; players <= 4; players++)
                    {
                        CellarTemplateRoom t = CellarTemplateSpawns.Resolve(l, d, p, r, players);
                        if (t == null) continue;
                        templated++;
                        string who = "seed " + p.Seed + " floor " + p.Floor + " room " + r + " players " + players + " " + t.TemplateId;
                        Assert.That(t.Spawns.Count, Is.LessThanOrEqualTo(p.CountIn(r) + CoopScaling.ExtraEnemiesPerRoom(players)), who + ": harder than the plan plus the party extras");
                        Assert.That(t.WaveCount, Is.LessThanOrEqualTo(2), who);
                        var legal = new HashSet<(int, int)>(CellarEncounterPlan.FreeCellsOf(l, d, r).Select(c => (c.X, c.Y)));
                        foreach (EncounterSpawn s in p.Spawns.Where(s => s.RoomIndex == r && s.Kind != EncounterKind.Hunter)) legal.Add((s.X, s.Y));
                        for (int w = 0; w < 2; w++)
                        {
                            var ws = t.InWave(w).ToList();
                            Assert.That(ws.Count, Is.LessThanOrEqualTo(GameMetrics.MaxLiveEnemies), who);
                            Assert.That(ws.Select(s => (s.Cell.X, s.Cell.Y)).Distinct().Count(), Is.EqualTo(ws.Count), who + ": shared cell");
                            foreach (TemplateSpawn s in ws) { Assert.That(legal, Does.Contain((s.Cell.X, s.Cell.Y)), who + ": illegal cell"); Assert.That(s.Kind, Is.Not.EqualTo(EnemyKind.DoughWarden), who); Assert.That((CellarFloorRoster.BitOf(s.Kind) & p.RosterMask) != 0 || p.RosterMask == 0 || EncounterBuilder.Remap(s.Kind, p.RosterMask) == s.Kind, who + ": off roster"); }
                        }
                        if (l.Carrier != null && p.Spawns.Any(s => s.RoomIndex == r && s.Kind == EncounterKind.Authored && s.X == l.Carrier.X && s.Y == l.Carrier.Y))
                            Assert.That(t.Spawns.Count(s => s.Carrier && s.Cell.X == l.Carrier.X && s.Cell.Y == l.Carrier.Y), Is.EqualTo(1), who + ": carrier lost its cell");
                    }
            Assert.That(templated, Is.GreaterThan(100), "the sweep must actually exercise templated rooms");
        }

        [Test]
        public void Resolve_ScalesWithPartySize_OneExtraEnemyPerExtraPlayer_WhereTheRoomHasSpace()
        {
            int grew = 0, rooms = 0;
            foreach (var (l, d, p) in Floors(35))   // 25 seeds gave 98 rooms once plan 5c shifted the twists; the sample size is incidental
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    CellarTemplateRoom solo = CellarTemplateSpawns.Resolve(l, d, p, r, 1);
                    if (solo == null) continue;
                    rooms++;
                    int prev = solo.Spawns.Count;
                    for (int players = 2; players <= 4; players++)
                    {
                        int n = CellarTemplateSpawns.Resolve(l, d, p, r, players).Spawns.Count;
                        Assert.That(n, Is.GreaterThanOrEqualTo(prev), "seed " + p.Seed + " floor " + p.Floor + " room " + r + " players " + players + ": fewer enemies than with one player fewer");
                        if (n > prev) grew++;
                        prev = n;
                    }
                }
            Assert.That(rooms, Is.GreaterThan(100));
            Assert.That(grew, Is.GreaterThan(rooms), "a bigger party must add enemies in most templated rooms");
        }

        // Probe (kept: fast): which live party sizes and latch extras reach a second wave in a templated room. CellarContentsSync's two-wave path is only live where this finds rooms.
        [Test]
        public void Resolve_WaveOneSurvives_ReportsWhichPartiesReachIt()
        {
            var counts = new Dictionary<string, int>(); int rooms = 0;
            for (uint seed = 1; seed <= 400; seed++)
                for (int floor = 2; floor <= 7; floor++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    foreach (int extra in new[] { 0, 1 })
                    {
                        CellarEncounterPlan p = CellarEncounterPlan.BuildForDive(l, d, extra, false);
                        for (int r = 0; r < l.Rooms.Count; r++)
                            for (int players = 1; players <= 4; players++)
                            {
                                CellarTemplateRoom t = CellarTemplateSpawns.Resolve(l, d, p, r, players);
                                if (t == null) continue;
                                string key = "players " + players + " extra " + extra;
                                if (!counts.ContainsKey(key)) counts[key] = 0;
                                rooms++;
                                if (t.InWave(1).Any()) { counts[key]++; Assert.That(t.InWave(0).Any(), key + ": wave 1 with no wave 0"); }
                            }
                    }
                }
            TestContext.WriteLine("WAVE1 PROBE rooms=" + rooms + " " + string.Join(" | ", counts.OrderBy(k => k.Key).Select(k => k.Key + " => " + k.Value)));
            UnityEngine.Debug.Log("WAVE1 PROBE rooms=" + rooms + " " + string.Join(" | ", counts.OrderBy(k => k.Key).Select(k => k.Key + " => " + k.Value)));
            Assert.That(rooms, Is.GreaterThan(100));
            // Tripwire: if this fails, a live party can now reach wave 1; drop the PlanOverride from the two-wave PlayMode tests and fix the CellarContentsSync.PlanOverride comment.
            Assert.That(counts.Values.Sum(), Is.EqualTo(0), "wave 1 is now reachable by a live 1-4 player party: " + string.Join(" | ", counts.OrderBy(k => k.Key).Select(k => k.Key + " => " + k.Value)));
        }

        [Test]
        public void Resolve_IsDeterministic()
        {
            foreach (var (l, d, p) in Floors(8))
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    CellarTemplateRoom a = CellarTemplateSpawns.Resolve(l, d, p, r, 3), b = CellarTemplateSpawns.Resolve(l, d, p, r, 3);
                    Assert.That(a == null, Is.EqualTo(b == null));
                    if (a != null) Assert.That(string.Join(";", a.Spawns.Select(s => s.Cell + "/" + s.Kind + "/" + s.Wave)), Is.EqualTo(string.Join(";", b.Spawns.Select(s => s.Cell + "/" + s.Kind + "/" + s.Wave))));
                }
        }

        [Test]
        public void Resolve_BakeryAndNonCombatRooms_KeepThePlanPath()
        {
            CellarFloorLayout l = CellarFloorGenerator.Layout(7, 3); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
            CellarEncounterPlan p = CellarEncounterPlan.BuildForDive(l, d, 0, false);
            for (int r = 0; r < l.Rooms.Count; r++)
                if (l.Rooms[r].Room.Kind != RoomKind.Combat || RoomEncounterTable.For(l.Rooms[r].Room.Id).Count == 0)
                    Assert.That(CellarTemplateSpawns.Resolve(l, d, p, r, 2), Is.Null, l.Rooms[r].Room.Id);
        }

        [Test]
        public void Resolve_NeverPlacesAnEnemyOnTheHuntersCell()
        {
            int hunterRooms = 0;
            for (uint seed = 1; seed <= 60; seed++)
                foreach (int floor in new[] { 2, 4, 6, 7 })
                {
                    CellarFloorLayout l = CellarFloorGenerator.Layout(seed, floor); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    CellarEncounterPlan p = CellarEncounterPlan.BuildForDive(l, d, 0, true);
                    if (p.Hunter == null) continue;
                    for (int players = 1; players <= 4; players++)
                    {
                        CellarTemplateRoom t = CellarTemplateSpawns.Resolve(l, d, p, p.Hunter.RoomIndex, players);
                        if (t == null) continue;
                        hunterRooms++;
                        foreach (TemplateSpawn s in t.Spawns)
                            Assert.That((s.Cell.X, s.Cell.Y), Is.Not.EqualTo((p.Hunter.X, p.Hunter.Y)), "seed " + seed + " floor " + floor + " players " + players + " wave " + s.Wave + ": template enemy on the hunter's cell");
                    }
                }
            Assert.That(hunterRooms, Is.GreaterThan(20), "the sweep must reach templated hunter rooms");
        }

        [Test]
        public void Resolve_AmbushRoom_UsesTheAmbushTemplateOnlyThere_AndRingCells()
        {
            int seen = 0;
            foreach (var (l, d, p) in Floors(300))
            {
                if (p.Twist != FloorTwist.Ambush) continue;
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    CellPos[] ring = r == p.AmbushRoom ? CellarAmbush.Cells(l, d, p, r) : null;
                    CellarTemplateRoom t = CellarTemplateSpawns.Resolve(l, d, p, r, 1, ring);
                    if (t == null) continue;
                    if (r != p.AmbushRoom) Assert.That(t.TemplateId, Is.Not.EqualTo(EncounterTemplates.AmbushId), "ambush template outside the ambush room");
                    else if (t.TemplateId == EncounterTemplates.AmbushId) seen++;
                }
            }
            Assert.That(seen, Is.GreaterThan(0), "the Ambush template must be reachable on an Ambush floor");
        }
    }
}
