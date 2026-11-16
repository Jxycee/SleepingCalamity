// B8: the per-floor look table (Presentation/Cellars/CellarFloorLook, design dungeon1-cellars section 3), the builder wiring that shows it (lamp tint, fog, wall material instances,
// overlays) and the route lamps (Core/Cellars/CellarFloorWarmLights: Spine rooms first, one lamp in a side room, none in dead ends).
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class CellarFloorLookTests
    {
        // The section 3 table, typed independently of the code under test: lamp, ambient top, ambient bottom, fog tint, fog density, breath, wet, moss, dust percent.
        static readonly (string Lamp, string Top, string Bottom, float Fr, float Fg, float Fb, float Fd, float Breath, float Wet, float Moss, float Dust)[] Spec =
        {
            ("#F2D58A", "#3a2c26", "#2a1c18", 0.16f, 0.13f, 0.11f, 0.007f, 0.15f, 0.6f, 0.5f, 40),
            ("#F2D58A", "#3a2e2a", "#2a1e1c", 0.15f, 0.14f, 0.13f, 0.009f, 0.20f, 0.5f, 0.3f, 30),
            ("#E8A63C", "#34282a", "#241a1c", 0.14f, 0.12f, 0.12f, 0.011f, 0.25f, 1.0f, 0.2f, 20),
            ("#E8A63C", "#40302a", "#2e1e1a", 0.17f, 0.13f, 0.10f, 0.006f, 0.35f, 0.7f, 0.1f, 35),
            ("#E8A63C", "#3c2a26", "#2c1a18", 0.18f, 0.12f, 0.09f, 0.010f, 0.60f, 0.4f, 0.0f, 20),
            ("#D2562A", "#30221e", "#1e1412", 0.14f, 0.09f, 0.06f, 0.012f, 0.75f, 0.0f, 0.0f, 80),
            ("#D2562A", "#38201a", "#22120e", 0.18f, 0.09f, 0.05f, 0.012f, 1.00f, 0.0f, 0.0f, 90),
        };

        static float L1(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        [Test]
        public void AllSevenFloorsAreDefined_AndOutOfRangeClamps()
        {
            Assert.AreEqual(7, CellarFloorLook.Count);
            for (int f = 1; f <= 7; f++) { Assert.AreEqual(f, CellarFloorLook.For(f).Floor); Assert.IsNotEmpty(CellarFloorLook.For(f).Name); }
            Assert.AreSame(CellarFloorLook.For(1), CellarFloorLook.For(0)); Assert.AreSame(CellarFloorLook.For(1), CellarFloorLook.For(-5));
            Assert.AreSame(CellarFloorLook.For(7), CellarFloorLook.For(8)); Assert.AreSame(CellarFloorLook.For(7), CellarFloorLook.For(99));
        }

        [Test]
        public void ValuesMatchTheDesignTable()
        {
            for (int f = 1; f <= 7; f++)
            {
                CellarFloorLookRow r = CellarFloorLook.For(f); var s = Spec[f - 1]; string at = "floor " + f;
                Assert.AreEqual(0f, L1(CellarFloorLook.Hex(s.Lamp), r.LampTint), 1e-4f, at + " lamp");
                Assert.AreEqual(0f, L1(CellarFloorLook.Hex(s.Top), r.AmbientTop), 1e-4f, at + " ambient top");
                Assert.AreEqual(0f, L1(CellarFloorLook.Hex(s.Bottom), r.AmbientBottom), 1e-4f, at + " ambient bottom");
                Assert.AreEqual(0f, L1(new Color(s.Fr, s.Fg, s.Fb), r.FogTint), 1e-4f, at + " fog tint");
                Assert.AreEqual(s.Fd, r.FogDensity, 1e-6f, at + " fog density");
                Assert.AreEqual(s.Breath, r.Breath, 1e-6f, at + " breath"); Assert.AreEqual(s.Wet, r.Wet, 1e-6f, at + " wet"); Assert.AreEqual(s.Moss, r.Moss, 1e-6f, at + " moss");
                Assert.AreEqual(s.Dust / 100f, r.Dust, 1e-6f, at + " dust");
            }
            Assert.AreEqual(0.0f, CellarFloorLook.Hex("#000000").r); Assert.AreEqual(1f, CellarFloorLook.Hex("#ffffff").g);
            Assert.Throws<System.ArgumentException>(() => CellarFloorLook.Hex("F2D58A"));
        }

        [Test]
        public void EveryShadowIsWarm_NothingTealOrPureBlack_AndWeightsAreUnitRange()
        {
            for (int f = 1; f <= 7; f++)
            {
                CellarFloorLookRow r = CellarFloorLook.For(f); string at = "floor " + f;
                foreach (Color c in new[] { r.AmbientTop, r.AmbientBottom, r.AmbientEquator, r.FogTint, r.LampTint })
                {
                    Assert.Greater(c.r, c.g, at + " not red-dominant (cold or green): " + c); Assert.Greater(c.r, c.b, at + " not red-dominant (teal or blue): " + c);
                }
                foreach (Color c in new[] { r.AmbientTop, r.AmbientBottom, r.FogTint }) Assert.Greater(c.r + c.g + c.b, 0.1f, at + " near black");
                Assert.Greater(Luma(r.AmbientTop), Luma(r.AmbientBottom), at + " top brighter than bottom");
                foreach (float w in new[] { r.Limewash, r.Breath, r.Wet, r.Moss, r.Roots, r.Dust, r.Soot, r.Salt }) Assert.That(w, Is.InRange(0f, 1f), at);
                Assert.That(Mathf.Max(r.WallTint.r, Mathf.Max(r.WallTint.g, r.WallTint.b)), Is.LessThanOrEqualTo(1f), at + " wall tint is a multiplier (1 per channel at most)");
                Assert.Greater(r.LampScale, 0f);
            }
        }

        [Test]
        public void FloorsAreDistinguishable_ThumbnailLevel()
        {
            // every pair differs overall in wall, lamp and fog together
            for (int a = 1; a <= 7; a++)
                for (int b = a + 1; b <= 7; b++)
                {
                    CellarFloorLookRow x = CellarFloorLook.For(a), y = CellarFloorLook.For(b);
                    float d = L1(x.WallTint, y.WallTint) + L1(x.LampTint, y.LampTint) + L1(x.FogTint, y.FogTint) + Mathf.Abs(x.Limewash - y.Limewash) * 0.5f;
                    Assert.Greater(d, 0.15f, "floors " + a + " and " + b + " look alike: " + d);
                }
            // the overview floors 1, 4 and 6: walls and lamps clearly apart in every pair; the fog of floor 6 apart from both, floors 1 and 4 by density
            int[] three = { 1, 4, 6 };
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                {
                    CellarFloorLookRow x = CellarFloorLook.For(three[i]), y = CellarFloorLook.For(three[j]);
                    Assert.Greater(L1(x.WallTint, y.WallTint), 0.30f, "wall " + x.Floor + " vs " + y.Floor);
                    Assert.Greater(L1(x.LampTint, y.LampTint), 0.20f, "lamp " + x.Floor + " vs " + y.Floor);
                    // floors 1 and 4 have near-identical fog in the design table (0.16,0.13,0.11 vs 0.17,0.13,0.10): they differ by wall and lamp; fog is checked against floor 6
                    if (x.Floor == 6 || y.Floor == 6) Assert.Greater(L1(x.FogTint, y.FogTint) + Mathf.Abs(x.FogDensity - y.FogDensity) * 20f, 0.10f, "fog " + x.Floor + " vs " + y.Floor);
                }
            // floor 1 is pale limewashed stone with candle gold; floor 4 the honey brick under the brightest lamps; floor 6 soot brick under ember
            CellarFloorLookRow f1 = CellarFloorLook.For(1), f4 = CellarFloorLook.For(4), f6 = CellarFloorLook.For(6);
            Assert.Greater(Luma(f1.WallTint), Luma(f6.WallTint) + 0.3f); Assert.GreaterOrEqual(f1.Limewash, 0.4f - 1e-4f);
            Assert.Greater(f4.LampTint.r - f4.LampTint.b, 0.4f, "honey brick under amber lamps");   // B8 round 2: the wall tint is a multiplier over the green-biased CV_Brick base and cut in red to leave honey, no longer amber itself; the amber check moves to the lamp
            Assert.That(CellarFloorLook.For(4).LampScale, Is.EqualTo(Enumerable.Range(1, 7).Max(f => CellarFloorLook.For(f).LampScale)), "the Wine Vaults are the brightest floor");
            Assert.Greater(f6.LampTint.r - f6.LampTint.b, 0.5f, "ember"); Assert.Greater(f6.Soot, 0.5f);
        }

        // ------------------------------------------------------------------------------------------------ builder wiring
        static CellarFloorKit FogKit() => new CellarFloorKit { FogBaseDensity = 0.007f, FogBaseColor = new Color(0.16f, 0.13f, 0.11f) };

        [Test]
        public void Builder_AppliesTheFloorLook_LampTintFogAndWallVariants()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool fog = RenderSettings.fog; float dens = RenderSettings.fogDensity; Color fc = RenderSettings.fogColor;
            var built = new System.Collections.Generic.Dictionary<int, CellarFloorBuild>();
            try
            {
                var colours = new System.Collections.Generic.Dictionary<int, (Color wall, Color lamp, Color fog, float density)>();
                foreach (int floor in new[] { 1, 6 })
                {
                    CellarFloorLayout layout = CellarTestFloors.Layout(12345, floor);
                    CellarFloorBuild b = CellarFloorBuilder.Build(layout, null, FogKit()); built[floor] = b;
                    Material wall = b.Root.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).First(m => m != null && m.name == "CellarLook_f" + floor + "_Wall");
                    Light lamp = b.Root.Find(CellarFloorBuilder.LampGroup).GetComponentsInChildren<Light>(true).First();
                    colours[floor] = (wall.color, lamp.color, RenderSettings.fogColor, RenderSettings.fogDensity);
                    CellarFloorLookRow look = CellarFloorLook.For(floor);
                    StringAssert.Contains("CellarLook_f" + floor, wall.name);
                    Assert.AreEqual(0f, L1(look.FogTint, RenderSettings.fogColor), 0.12f, "floor " + floor + " fog tint (atmosphere offset stays small)");
                    Assert.AreEqual(look.FogDensity, b.FogDensity / (b.Dressing.Atmosphere.FogPercent / 100f), 1e-5f, "floor " + floor + " fog density from the look");
                    Assert.Less(L1(Color.Lerp(CellarArtTuning.LampGold, look.LampTint, CellarFloorLook.LampTintWeight), lamp.color), 1e-4f, "floor " + floor + " lamp colour");
                }
                Assert.Greater(L1(colours[1].wall, colours[6].wall), 0.15f, "wall instances differ per floor");
                Assert.Greater(L1(colours[1].lamp, colours[6].lamp), 0.15f, "lamp tints differ per floor");
                Assert.Greater(L1(colours[1].fog, colours[6].fog), 0.03f, "fog differs per floor");
            }
            finally
            {
                foreach (CellarFloorBuild b in built.Values) if (b.Root != null) Object.DestroyImmediate(b.Root.gameObject);
                RenderSettings.fog = fog; RenderSettings.fogDensity = dens; RenderSettings.fogColor = fc;
            }
        }

        [Test]
        public void Overlays_AreSeededAndFollowTheWeights()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material Card() => new Material(lit);
            CellarFloorKit Kit() => new CellarFloorKit { DecalDamp = Card(), DecalLeak = Card(), DecalMoss = Card(), DecalMossClump = Card() };
            int Count(int floor, out string names)
            {
                CellarFloorBuild b = CellarFloorBuilder.Build(CellarTestFloors.Layout(12345, floor), null, Kit());
                try { names = string.Join(",", b.Root.Find(CellarFloorBuilder.OverlayGroup)?.GetComponentsInChildren<Transform>(true).Select(t => t.name) ?? new string[0]); return b.Overlays; }
                finally { Object.DestroyImmediate(b.Root.gameObject); }
            }
            int w1 = Count(1, out string n1), w1b = Count(1, out string n1b), w6 = Count(6, out _);
            Assert.AreEqual(w1, w1b, "same seed and floor: same overlays"); Assert.AreEqual(n1, n1b);
            Assert.Greater(w1, 0, "the wet, mossy Church Cellar gets overlays"); Assert.AreEqual(0, w6, "the dry, mossless Coal Cellars get none");
            // no asset, no overlay
            CellarFloorBuild none = CellarFloorBuilder.Build(CellarTestFloors.Layout(12345, 1), null, new CellarFloorKit());
            try { Assert.AreEqual(0, none.Overlays); Assert.IsNull(none.Root.Find(CellarFloorBuilder.OverlayGroup)); }
            finally { Object.DestroyImmediate(none.Root.gameObject); }
            // the hash is stable and spread
            Assert.AreEqual(CellarFloorBuilder.Rand01(1, 1, 3, 4, 5), CellarFloorBuilder.Rand01(1, 1, 3, 4, 5));
            float mean = 0; for (int i = 0; i < 400; i++) mean += CellarFloorBuilder.Rand01(12345, 1, i % 20, i / 20, 1) / 400f;
            Assert.That(mean, Is.InRange(0.4f, 0.6f));
        }

        // ------------------------------------------------------------------------------------------------ route lamps
        [Test]
        public void WantFor_SpineKeepsTheRoll_ArcGetsOne_DeadEndsNone()
        {
            Assert.AreEqual(2, CellarFloorWarmLights.WantFor(RoomRole.Spine, 2, 2)); Assert.AreEqual(1, CellarFloorWarmLights.WantFor(RoomRole.Spine, 2, 1));
            Assert.AreEqual(0, CellarFloorWarmLights.WantFor(RoomRole.Spine, 0, 2));
            Assert.AreEqual(1, CellarFloorWarmLights.WantFor(RoomRole.Arc, 2, 2)); Assert.AreEqual(1, CellarFloorWarmLights.WantFor(RoomRole.Arc, 0, 2));
            Assert.AreEqual(0, CellarFloorWarmLights.WantFor(RoomRole.Arc, 2, 0));
            Assert.AreEqual(0, CellarFloorWarmLights.WantFor(RoomRole.Branch, 2, 2, 1), "dead end"); Assert.AreEqual(1, CellarFloorWarmLights.WantFor(RoomRole.Branch, 2, 2, 2), "side room");
            Assert.AreEqual(0, CellarFloorWarmLights.WantFor(RoomRole.Locked, 2, 2, 3));
        }

        [Test]
        public void RouteLamps_FollowTheRoute_AcrossSeeds()
        {
            int spineLit = 0, spineRooms = 0, rows = 0, near = 0, arcLit = 0, arcRooms = 0;
            for (uint seed = 0; seed < CellarTestFloors.FastSeeds; seed++)
                for (int floor = 1; floor <= 3; floor++)
                {
                    CellarFloorLayout l = CellarTestFloors.Layout(seed, floor); if (l.IsStub) continue;
                    CellarFloorRoute route = CellarFloorRoute.Resolve(l); CellarFloorWarmLights w = CellarTestFloors.WarmLights(seed, floor);
                    bool routed = route.Spine.Count > 0;
                    for (int r = 0; r < l.Rooms.Count; r++)
                    {
                        int n = w.CountInRoom(r); string at = "seed " + seed + " floor " + floor + " room " + r;
                        if (!routed) continue;
                        RoomRole role = route.RoleOf(r);
                        int doors = Enumerable.Range(0, l.Rooms[r].Room.Doors.Count).Count(d => l.DoorIsOpen(r, d));
                        if (role == RoomRole.Locked || (role == RoomRole.Branch && doors <= 1)) Assert.AreEqual(0, n, at + " is a dead end");
                        if (role == RoomRole.Arc || role == RoomRole.Branch) { Assert.LessOrEqual(n, 1, at); if (doors >= 2 || role == RoomRole.Arc) { arcRooms++; if (n == 1) arcLit++; } }
                        if (role == RoomRole.Spine) { spineRooms++; if (n > 0) spineLit++; }
                    }
                    foreach (WarmLightRow row in w.Rows)
                    {
                        if (!routed || route.RoleOf(row.RoomIndex) != RoomRole.Spine) continue;
                        PlacedRoom p = l.Rooms[row.RoomIndex]; rows++;
                        for (int d = 0; d < p.Room.Doors.Count; d++)
                            if (l.DoorIsOpen(row.RoomIndex, d) && System.Math.Abs(p.DoorCell(d).X - row.CellX) + System.Math.Abs(p.DoorCell(d).Y - row.CellY) <= CellarFloorWarmLights.RouteReach) { near++; break; }
                    }
                }
            TestContext.WriteLine("route lamps: spine " + spineLit + "/" + spineRooms + " lit, arc " + arcLit + "/" + arcRooms + " lit, " + near + "/" + rows + " spine lights within reach of an open door");
            Assert.Greater(spineRooms, 50); Assert.Greater(spineLit, 0); Assert.Greater(arcLit, 0, "side rooms get a lamp");
            Assert.Greater(near * 100 / System.Math.Max(1, rows), 50, "spine lamps sit by the route's doors first");
        }
    }
}
