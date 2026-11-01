// Authority: none (pure EditMode tests). Procgen item 1 (Core/Cellars: CellarFloorRoute, CellarSpecialRoom, CellarDoorHints) and the greybox markers in CellarFloorBuilder.
// Catches: a route, special room or hint set that differs between peers or depends on the order floors are asked in, a golden that drifts, an arc that touches the Spine or the
// locked room, a key arc that does not hold the key chest, a special kind repeated within a dive, a special room on the locked leaf, a checkpoint, the sleeping-elite room or floor 4,
// a door hint that disagrees with the layout, and a builder that draws markers the Core data does not have. Spread numbers are written to procgen_stats.txt in the temp folder.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarProcgenTests
    {
        const int Seeds = 1200;

        static ulong[] Hashes(uint seed, int floor)
        {
            CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor);
            CellarFloorRoute r = CellarFloorRoute.Resolve(l); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
            SpecialRoom s = SpecialRoomDive.Resolve(l, r, d); CellarDoorHints h = CellarDoorHints.Resolve(l, s, d);
            return new[] { r.RouteHash(), s.SpecialHash(), h.HintsHash() };
        }

        [Test]
        public void Procgen_SameSeedTwice_AndCallOrderDoesNotMatter()
        {
            foreach (uint seed in new uint[] { 0, 1, 7, 12345, 99999, 0xC0FFEE12 })
            {
                var fwd = new ulong[4][]; var back = new ulong[4][];
                for (int f = 1; f <= 4; f++) fwd[f - 1] = Hashes(seed, f);
                for (int f = 4; f >= 1; f--) back[f - 1] = Hashes(seed, f);
                for (int f = 0; f < 4; f++) { Assert.AreEqual(fwd[f], back[f], "seed " + seed + " floor " + (f + 1)); Assert.AreEqual(fwd[f], Hashes(seed, f + 1)); }
            }
        }

        [Test]
        public void Procgen_GoldenHashes_Seed12345()
        {
            // Fresh goldens (new layers, no earlier value to move). LayoutHash/DressingHash stay pinned by CellarFloorGeneratorTests / CellarVarietyTests.
            ulong[][] expect =
            {
                new ulong[] { 690935452282621397UL, 8845299293798975808UL, 13704606209555056737UL },     // route, special, hints (hints re-pinned 2026-10-31: first-reached rooms only, deliberate change; route/hints goldens moved with item 2 layout presets, deliberate)
                new ulong[] { 12317519853489886730UL, 8103913021369237331UL, 8535885715987663944UL },
                new ulong[] { 10929967913840331367UL, 9026194201199058082UL, 7482022743495240998UL },
            };
            var sb = new StringBuilder();
            for (int f = 1; f <= 3; f++) { ulong[] g = Hashes(12345, f); sb.Append("floor " + f + ": " + g[0] + "UL, " + g[1] + "UL, " + g[2] + "UL\n"); }
            for (int f = 1; f <= 3; f++)
            {
                ulong[] g = Hashes(12345, f);
                for (int k = 0; k < 3; k++) Assert.AreEqual(expect[f - 1][k], g[k], "floor " + f + " hash " + k + "\n" + sb);
            }
        }

        [Test]
        public void Floor4_AllLayersEmpty()
        {
            foreach (uint seed in new uint[] { 1, 12345, 777 })
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, 4);
                CellarFloorRoute r = CellarFloorRoute.Resolve(l);
                Assert.IsTrue(r.IsEmpty); Assert.AreEqual(CycleRole.None, r.Cycle); Assert.AreEqual(0, r.Spine.Count);
                Assert.IsFalse(SpecialRoomDive.Resolve(l).Exists);
                Assert.AreEqual(0, CellarDoorHints.Resolve(l).Hints.Count);
            }
        }

        [Test]
        public void Route_Invariants_AndArcRoleMix()
        {
            var cycle = new int[4, 4]; var floors = new int[4]; int arcsTotal = 0, spineTotal = 0, bad = 0;
            for (uint seed = 0; seed < Seeds; seed++)
                for (int f = 1; f <= 3; f++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f); CellarFloorRoute r = CellarFloorRoute.Resolve(l);
                    string at = "seed " + seed + " floor " + f;
                    Assert.AreEqual(l.Rooms.Count, r.Roles.Count, at);
                    Assert.AreEqual(RoomKind.Entry, l.Rooms[r.Spine[0]].Room.Kind, at); Assert.AreEqual(RoomKind.StairExit, l.Rooms[r.Spine[r.Spine.Count - 1]].Room.Kind, at);
                    CellarRoomGraph g = CellarRoomGraph.Build(l);
                    for (int i = 0; i + 1 < r.Spine.Count; i++) Assert.IsTrue(g.Adjacent(r.Spine[i]).Contains(r.Spine[i + 1]), at + " spine hop");
                    Assert.IsTrue(l.EntryToExitDistance() > 0, at);
                    for (int i = 0; i < r.Roles.Count; i++)
                    {
                        Assert.AreEqual(r.Spine.Contains(i), r.Roles[i] == RoomRole.Spine, at + " room " + i);
                        Assert.AreEqual(i == l.LockedRoomIndex, r.Roles[i] == RoomRole.Locked, at + " room " + i);
                    }
                    foreach (RouteArc a in r.Arcs)
                    {
                        Assert.IsTrue(a.From != a.To, at);
                        if (a.From < l.Rooms.Count) Assert.AreEqual(RoomRole.Spine, r.Roles[a.From], at);
                        if (a.To < l.Rooms.Count) Assert.AreEqual(RoomRole.Spine, r.Roles[a.To], at);
                        foreach (int room in a.Rooms) Assert.AreEqual(RoomRole.Arc, r.Roles[room], at);
                    }
                    Assert.AreEqual(r.Arcs.Count == 0, r.Cycle == CycleRole.None, at); Assert.AreEqual(r.Cycle == CycleRole.None, r.ChosenArc < 0, at);
                    if (r.Cycle == CycleRole.KeyArc) { Assert.IsTrue(l.HasLockedRoom && r.Chosen.Rooms.Contains(l.KeyChest.RoomIndex), at + " key arc"); }
                    cycle[f, (int)r.Cycle]++; floors[f]++; arcsTotal += r.Arcs.Count; spineTotal += r.Spine.Count; if (r.Cycle == CycleRole.None) bad++;
                }
            var sb = new StringBuilder("ROUTE (" + Seeds + " seeds)\n");
            for (int f = 1; f <= 3; f++)
                sb.AppendLine("floor " + f + ": None " + Pct(cycle[f, 0], floors[f]) + " Plain " + Pct(cycle[f, 1], floors[f]) + " KeyArc " + Pct(cycle[f, 2], floors[f]) + " ShortcutBack " + Pct(cycle[f, 3], floors[f]));
            sb.AppendLine("mean arcs/floor " + (arcsTotal / (3.0 * Seeds)).ToString("F2") + " mean spine rooms " + (spineTotal / (3.0 * Seeds)).ToString("F2") + " None overall " + Pct(bad, 3 * Seeds));
            Report(sb.ToString());
        }

        [Test]
        public void SpecialRoom_Spread_Over1000Seeds()
        {
            var has = new int[4]; var kinds = new int[4, 5]; int spineOnly = 0, dives = 0, none = 0, dived = 0;
            for (uint seed = 0; seed < Seeds; seed++)
            {
                var seen = new List<SpecialKind>();
                for (int f = 1; f <= 3; f++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f); CellarFloorRoute r = CellarFloorRoute.Resolve(l); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    SpecialRoom s = SpecialRoomDive.Resolve(l, r, d); string at = "seed " + seed + " floor " + f;
                    Assert.AreEqual(s.SpecialHash(), SpecialRoomDive.Resolve(l).SpecialHash(), at + " null args resolve alike");
                    if (!s.Exists) { Assert.AreEqual(-1, s.RoomIndex, at); continue; }
                    has[f]++; kinds[f, (int)s.Kind]++;
                    Assert.IsFalse(seen.Contains(s.Kind), at + " kind repeated: " + s.Kind); seen.Add(s.Kind);
                    PlacedRoom p = l.Rooms[s.RoomIndex];
                    Assert.AreEqual(RoomKind.Combat, p.Room.Kind, at); Assert.AreNotEqual(l.LockedRoomIndex, s.RoomIndex, at); Assert.AreNotEqual(RoomRole.Locked, r.RoleOf(s.RoomIndex), at);
                    Assert.IsFalse(d.Vignettes.Any(v => v.Vignette.Id == "sleeping_elite" && v.RoomIndex == s.RoomIndex), at + " elite room");
                    if (r.RoleOf(s.RoomIndex) == RoomRole.Spine)
                    {
                        spineOnly++;
                        for (int i = 0; i < l.Rooms.Count; i++)
                            if (l.Rooms[i].Room.Kind == RoomKind.Combat && i != l.LockedRoomIndex && r.RoleOf(i) != RoomRole.Spine && !d.Vignettes.Any(v => v.Vignette.Id == "sleeping_elite" && v.RoomIndex == i))
                                Assert.Fail(at + ": Spine room chosen while off-path room " + i + " was eligible");
                    }
                }
                dived++; dives += seen.Count; if (seen.Count == 0) none++;
            }
            var sb = new StringBuilder("SPECIAL (" + Seeds + " seeds)\n");
            for (int f = 1; f <= 3; f++)
                sb.AppendLine("floor " + f + ": has special " + Pct(has[f], Seeds) + " EliteDen " + Pct(kinds[f, 1], Seeds) + " Treasury " + Pct(kinds[f, 2], Seeds) + " Shrine " + Pct(kinds[f, 3], Seeds) + " Quiet " + Pct(kinds[f, 4], Seeds));
            sb.AppendLine("specials per dive " + (dives / (double)dived).ToString("F2") + ", dives with none " + Pct(none, dived) + ", placed on a Spine room " + Pct(spineOnly, has[1] + has[2] + has[3]));
            Report(sb.ToString());
            Assert.Greater(has[1] + has[2] + has[3], Seeds / 2, "some specials appear");
        }

        [Test]
        public void SpecialKinds_NeverRepeatInADive_AndDoNotNeedLayouts()
        {
            var any = new int[4];
            for (uint seed = 0; seed < 5000; seed++)
            {
                var seen = new List<SpecialKind>();
                for (int f = 1; f <= 3; f++)
                {
                    SpecialKind k = SpecialRoomDive.KindFor(seed, f, out _);
                    if (k == SpecialKind.None) continue;
                    any[f]++;
                    Assert.IsFalse(seen.Contains(k), "seed " + seed + " floor " + f); seen.Add(k);
                    if (k == SpecialKind.EliteDen) Assert.GreaterOrEqual(f, 2, "elite den min floor");
                }
            }
            Report("KINDS (5000 seeds, no layouts): a kind is drawn on floor 1 " + Pct(any[1], 5000) + ", floor 2 " + Pct(any[2], 5000) + ", floor 3 " + Pct(any[3], 5000) + " (percent knobs 40/55/55)\n");
        }

        [Test]
        public void DoorHints_AgreeWithTheLayout()
        {
            int hints = 0, withFlag = 0, notable = 0, keyHints = 0, specialHints = 0, rewardHints = 0;
            for (uint seed = 0; seed < Seeds; seed++)
                for (int f = 1; f <= 3; f++)
                {
                    CellarFloorLayout l = CellarFloorGenerator.Generate(seed, f); CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                    SpecialRoom s = SpecialRoomDive.Resolve(l, null, d); CellarDoorHints h = CellarDoorHints.Resolve(l, s, d); string at = "seed " + seed + " floor " + f;
                    CellarRoomGraph graph = CellarRoomGraph.Build(l);
                    for (int ri = 0; ri < l.Rooms.Count; ri++)   // a door has a hint exactly when something other than its own room lies beyond it
                        for (int di = 0; di < l.Rooms[ri].Room.Doors.Count; di++)
                            Assert.AreEqual(graph.DoorNeighbours(ri, di).Count > 0, h.Find(ri, di) != null, at + " door " + ri + "/" + di);
                    foreach (FloorLink k in l.Links)   // every link end that reaches another room has a hint
                    {
                        if (k.RoomB >= 0) { Assert.IsNotNull(h.Find(k.RoomA, k.DoorA), at + " link end A"); Assert.IsNotNull(h.Find(k.RoomB, k.DoorB), at + " link end B"); }
                    }
                    int lockedHints = 0;
                    foreach (DoorHint x in h.Hints)
                    {
                        Assert.IsTrue(x.Neighbours.Count > 0 && l.DoorIsOpen(x.RoomIndex, x.DoorIndex), at);
                        foreach (int nb in x.Neighbours)
                        {
                            Assert.AreNotEqual(x.RoomIndex, nb, at);
                            Assert.IsTrue(graph.DoorNeighbours(x.RoomIndex, x.DoorIndex).Contains(nb), at + " first-reached room is on the door's network");   // a subset of the whole network
                        }
                        bool keyThere = l.KeyChest != null && x.Neighbours.Contains(l.KeyChest.RoomIndex);
                        Assert.AreEqual(keyThere, x.Has(DoorHintFlags.Key), at + " key flag");
                        Assert.AreEqual(l.Chests.Count(c => x.Neighbours.Contains(c.RoomIndex)), x.ChestCount, at); Assert.AreEqual(x.ChestCount > 0, x.Has(DoorHintFlags.Chests), at);
                        Assert.AreEqual(s.Exists && x.Neighbours.Contains(s.RoomIndex), x.Has(DoorHintFlags.Special), at);
                        Assert.AreEqual(x.Neighbours.Any(n => l.Rooms[n].Room.Kind == RoomKind.StairExit), x.Has(DoorHintFlags.Exit), at);
                        Assert.AreEqual(x.Neighbours.Any(n => l.Rooms[n].Room.Kind == RoomKind.Checkpoint), x.Has(DoorHintFlags.Checkpoint), at);
                        if (x.Has(DoorHintFlags.Locked)) { lockedHints++; Assert.IsTrue(x.Neighbours.Contains(l.LockedRoomIndex), at + " locked hint points at the locked room"); }
                        hints++; if (x.Flags != DoorHintFlags.None) withFlag++; if (x.Notable) notable++; if (x.Has(DoorHintFlags.Key)) keyHints++; if (x.Has(DoorHintFlags.Special)) specialHints++; if (x.Has(DoorHintFlags.RewardVignette)) rewardHints++;
                    }
                    Assert.AreEqual(l.HasLockedRoom ? 1 : 0, lockedHints, at + " exactly one locked-door hint");
                }
            Assert.Less(notable * 100.0 / hints, 75.0, "door icons (notable hints) must not mark nearly every door");
            Assert.Less(withFlag * 100.0 / hints, 95.0, "first-reached hints must flag clearly fewer doors than the whole-network version (97%)");
            Report("HINTS (" + Seeds + " seeds x floors 1-3): " + hints + " door hints, flagged " + Pct(withFlag, hints) + ", notable (icon) " + Pct(notable, hints) + ", key " + Pct(keyHints, hints) + ", special " + Pct(specialHints, hints) + ", reward vignette " + Pct(rewardHints, hints) + "\n");
        }

        [Test]
        public void Builder_DrawsTheSpecialMarkerAndOneIconPerHintedDoor()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int specials = 0;
            foreach (var (seed, floor) in new (uint, int)[] { (12345, 1), (12345, 2), (12345, 3), (7, 2), (99, 3), (4242, 1), (12345, 4) })
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(seed, floor); CellarFloorBuild b = CellarFloorBuilder.Build(l, null);
                Transform g = b.Root.Find(CellarFloorBuilder.ProcgenGroup);
                int icons = b.DoorHints.Hints.Count(x => x.Notable);
                Assert.AreEqual(icons, b.DoorHintIcons); Assert.AreEqual(icons, Enumerable.Range(0, g.childCount).Count(i => g.GetChild(i).name.StartsWith("DoorHint_")));
                foreach (DoorHint x in b.DoorHints.Hints.Where(x => x.Notable)) Assert.IsNotNull(g.Find("DoorHint_r" + x.RoomIndex + "_d" + x.DoorIndex));
                Assert.AreEqual(b.Special.Exists ? 1 : 0, b.SpecialMarkers);
                if (b.Special.Exists) { specials++; Assert.IsNotNull(g.Find("SpecialRoom_" + b.Special.Kind + "_r" + b.Special.RoomIndex)); }
                if (floor == 4) { Assert.AreEqual(0, b.DoorHintIcons); Assert.AreEqual(0, b.SpecialMarkers); }
                Object.DestroyImmediate(b.Root.gameObject);
            }
            Assert.Greater(specials, 0, "at least one sample floor shows a special room");
        }

        static string Pct(int n, int of) => (100.0 * n / System.Math.Max(1, of)).ToString("F1") + "%";

        static void Report(string text)
        {
            Debug.Log(text);
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "procgen_stats.txt"), text + "\n"); } catch (IOException) { }
        }
    }
}
