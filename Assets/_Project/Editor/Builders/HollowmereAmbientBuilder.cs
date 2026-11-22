// Hollowmere phase-1 ambient life, EFFECTS ONLY (no animal models, no people, no colliders): built into the town scene by HollowmereBuilder.BuildScene after the town atmosphere,
// so ProjectBuilder.RunAll covers it through the Hollowmere step. The on/off flags and look numbers live in SleepingCalamity.Presentation.HollowmereAmbientLife (the one tuning spot).
// What it builds (root HM_AmbientLife unless noted; AreaLook turns the root off in the cellar):
//   Birds        cards with the SC_BirdCard wing-flap shader, moved by AmbientCreatures: rooks perched in a ploughed field (west) and a hay field (east) that lift when the hero is within 15 m;
//                crows wheeling round the bell tower (both presets) and over the F03 ruins (Ruined root only); swallows over the square; gulls over the lake; high valley crows.
//   Butterflies / bees  wander cards over the garden lots (R2_07, R2_18, R2_22, R3_02), three town corners and a few scanned meadow spots outside the walls.
//   Fish rings   particle rings on the lake (Y = WaterY + 0.04) at a few patches over the deeper water.
//   Reed sway    AmbientReedSway on the lake's ShoreReeds / PoolReeds clumps (they were static meshes).
//   Wind waves   a tiling cloud + gust-band cookie on the Sun, slid with the wind by AmbientCloudShadows (cloud shadows drifting over the valley, bands crossing the grass and fields).
//   Chimney smoke  particle plumes above the heroes' home chimney in BOTH presets and above every rebuilt house chimney in the Rebuilt root only (the chimney part when the kit names one,
//                else the roof top of the building's bounds).
// Determinism: fixed seeds and hashes, assets rewritten only when their bytes differ. Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereAmbientBuilder
    {
        public const string RootName = "HM_AmbientLife";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string TexFolder = "Assets/_Project/Content/Textures/Hollowmere/Ambient";
        const string MeshPath = TexFolder + "/AmbientCard.asset";
        static readonly string[] GardenLots = { "R2_07", "R2_18", "R2_22", "R3_02" };

        static Mesh _card; static Material _lark, _pigeon, _crow, _swallow, _gull, _butterfly, _butterflyYellow, _butterflyOrange, _bee;
        static Texture2D _smokeTex, _ringTex;

        public static void Build(Transform root, TownLayout t)
        {
            var life = new GameObject(RootName).transform; life.SetParent(root, false);
            BuilderUtil.EnsureFolder(TexFolder);
            Transform ruined = root.Find(HollowmereBuilder.RuinedRoot), rebuilt = root.Find(HollowmereBuilder.RebuiltRoot);
            if (HollowmereAmbientLife.Birds || HollowmereAmbientLife.Butterflies || HollowmereAmbientLife.Bees) { _card = EnsureCardMesh(); EnsureCardMaterials(); EnsureWings(); }

            var common = new List<AmbientCreatures.Flock>();
            if (HollowmereAmbientLife.Birds) BuildBirds(life, ruined, common, t);
            if (HollowmereAmbientLife.Butterflies || HollowmereAmbientLife.Bees) BuildInsects(life, common, t);
            Finish(life.gameObject, common);

            if (HollowmereAmbientLife.FishRings) BuildFishRings(life, t);
            if (HollowmereAmbientLife.ReedSway) BuildReedSway(life, root);
            BuildWindWaves(root);
            if (HollowmereAmbientLife.ChimneySmoke) BuildSmoke(ruined, rebuilt, t);
            if (HollowmereAmbientLife.ClothSway) BuildClothSway(life, root);
            if (HollowmereAmbientLife.Animals) HollowmereAnimals.Build(ruined, t);
        }

        // T24: washing-line cloths (WashingLine.fbx parts named Cloth_*, hung -Y from the rope) sway in the wind via AmbientClothSway (transform pitch about the rope axis; no shader change).
        static void BuildClothSway(Transform life, Transform root)
        {
            // one AmbientClothSway per washing line (on the line's own root, referencing only its own children), so the line moves into its art scene whole and HollowmereCombine skips the cloths
            var byLine = new Dictionary<Transform, List<Transform>>(); var order = new List<Transform>();
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (!tr.name.StartsWith("Cloth_", System.StringComparison.Ordinal) || tr.GetComponent<MeshRenderer>() == null || tr.IsChildOf(life) || tr.parent == null) continue;
                if (!byLine.TryGetValue(tr.parent, out var l)) { byLine[tr.parent] = l = new List<Transform>(); order.Add(tr.parent); }
                l.Add(tr);
            }
            int n = 0;
            foreach (Transform line in order)
            {
                var sway = line.gameObject.AddComponent<AmbientClothSway>();
                sway.Cloths = byLine[line].ToArray();
                sway.Rest = sway.Cloths.Select(c => c.localRotation).ToArray();
                n += sway.Cloths.Length;
            }
            Debug.Log("SC HOLLOWMERE ambient: " + n + " washing-line cloths on " + order.Count + " lines sway");
        }

        // ---------------------------------------------------------------------------------------------------------------- birds
        static void BuildBirds(Transform life, Transform ruined, List<AmbientCreatures.Flock> common, TownLayout t)
        {
            float tower = HollowmereLayout.KeepHeight + 17f;
            common.Add(Circle("BellTowerCrows", life, _crow, new Vector3(5.6f, tower, 24f), 7, 12.5f, 7f, 6.5f, 0.9f, 6.28f, 101));
            float sq = HollowmereLayout.Ground(t, 0f, -7f);
            common.Add(Circle("SquareSwallows", life, _swallow, new Vector3(0f, sq, -7f), 7, 20f, 9f, 12f, 0.34f, 6.28f, 102));
            common.Add(Circle("LakeGulls", life, _gull, new Vector3(HollowmereLayout.LakeCentre.X, HollowmereLayout.WaterY, HollowmereLayout.LakeCentre.Z), 6, 38f, 22f, 7f, 1.2f, 6.28f, 103));
            // close flocks so the fixed cameras (heroes' home, lake shore, overview) always have birds in frame
            float home = HollowmereLayout.Ground(t, 12f, 10f);
            common.Add(Circle("HomeSwallows", life, _swallow, new Vector3(12f, home, 10f), 6, 10f, 13f, 9f, 0.34f, 6.28f, 108));
            common.Add(Circle("HomeCrows", life, _crow, new Vector3(2f, home, 14f), 3, 14f, 18f, 6f, 0.9f, 6.28f, 109));
            common.Add(Circle("ShoreGulls", life, _gull, new Vector3(-14f, HollowmereLayout.WaterY, -66f), 5, 16f, 9f, 6f, 1.2f, 6.28f, 110));
            common.Add(Circle("OverviewGulls", life, _gull, new Vector3(HollowmereLayout.LakeCentre.X + 6f, HollowmereLayout.WaterY, HollowmereLayout.LakeCentre.Z + 4f), 5, 22f, 20f, 6f, 1.2f, 6.28f, 111));
            // T11 life per district: larks over the meadow and the road, pigeons on the square's wall tops
            common.Add(Circle("MeadowLarks", life, _lark, new Vector3(-8f, HollowmereLayout.Ground(t, -8f, -76f), -76f), 3, 26f, 16f, 5f, 0.34f, 6.28f, 112));
            common.Add(Circle("RoadLarks", life, _lark, new Vector3(-84f, HollowmereLayout.Ground(t, -84f, -87f), -87f), 2, 22f, 17f, 5f, 0.34f, 6.28f, 113));
            AddRoofPigeons(common, life, ruined, t);
            Vector3 valley =new Vector3(-95f, HollowmereLayout.Ground(t, -95f, 20f), 20f);
            common.Add(Circle("ValleyCrows", life, _crow, valley, 8, 50f, 46f, 8f, 0.9f, 2.4f, 104));

            // rooks sitting in a ploughed field in the west and a hay field in the east: they lift when the hero comes within ScatterDistance
            AddPerched(common, life, t, "RooksWest", FieldKind.Ploughed, -136f, -60f, -150f, 110f, 12, 105);
            AddPerched(common, life, t, "RooksEast", FieldKind.Hay, 60f, 136f, -150f, 110f, 9, 106);

            // crows over the ruined lots: only while the town is still in ruins
            Foundation f03 = t.Foundations.FirstOrDefault(f => f.Id == "F03");
            if (f03 != null && ruined != null)
            {
                var holder = new GameObject(RootName + "_Ruined"); holder.transform.SetParent(ruined, false);
                var list = new List<AmbientCreatures.Flock> { Circle("RuinCrows", holder.transform, _crow, new Vector3(f03.Centre.X, f03.Y, f03.Centre.Z), 4, 8f, 12f, 5.5f, 0.9f, 6.28f, 107) };
                Finish(holder, list);
            }
        }

        // T11: pigeons sitting on the tops of the square's tall shells (F03, F05, F06): wall tops found by rays from above onto the lot's own colliders, three birds per lot
        // on three tops at least 3 m apart (one card each: every card is a GameObject, Transform and renderer in the 47 MB scene); they lift and wheel low over the square when the hero comes near, then settle again.
        static void AddRoofPigeons(List<AmbientCreatures.Flock> list, Transform parent, Transform ruined, TownLayout t)
        {
            Transform lots = ruined != null ? ruined.Find("Foundations") : null;
            if (lots == null) return;
            Physics.SyncTransforms();
            int seed = 114;
            foreach (string id in new[] { "F03", "F05", "F06" })
            {
                Transform lot = null;
                foreach (Transform c in lots) if (c.name.StartsWith(id + "_", System.StringComparison.Ordinal)) { lot = c; break; }
                if (lot == null) continue;
                Bounds? bb = null;
                foreach (Renderer r in lot.GetComponentsInChildren<Renderer>(true)) { if (r is MeshRenderer) bb = bb.HasValue ? Grow(bb.Value, r.bounds) : r.bounds; }
                if (!bb.HasValue) continue;
                Bounds b = bb.Value;
                var tops = new List<Vector3>();
                for (float x = b.min.x + 0.5f; x < b.max.x; x += 1.1f)
                    for (float z = b.min.z + 0.5f; z < b.max.z; z += 1.1f)
                        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(x, b.max.y + 3f, z), Vector3.down, b.size.y + 6f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (!h.collider.transform.IsChildOf(lot) || h.normal.y < 0.7f || h.point.y < b.max.y - 1.8f) continue;
                            tops.Add(h.point); break;
                        }
                var picked = new List<Vector3>();
                foreach (Vector3 p in tops.OrderByDescending(p => p.y + 0.4f * SaltwakeRoadPlan.Hash01((int)(p.x * 9f + p.z * 31f), seed)))
                {
                    if (picked.Any(q => (new Vector2(p.x, p.z) - new Vector2(q.x, q.z)).magnitude < 3f)) continue;
                    picked.Add(p);
                    if (picked.Count >= 3) break;
                }
                foreach (Vector3 p in picked)
                {
                    var f = new AmbientCreatures.Flock
                    {
                        Name = "Pigeons_" + id + "_" + list.Count, Move = AmbientCreatures.Motion.Perch, Centre = new Vector3(p.x, p.y, p.z), Spread = 1.2f, Wingspan = 0.55f * HollowmereAmbientLife.BirdScale,
                        AloftRadius = 14f, AloftHeight = 9f, Speed = 8f, Clump = 1.6f, Seed = seed++,
                    };
                    f.Cards = Cards(parent, f.Name, _pigeon, 1);
                    list.Add(f);
                }
            }
            Debug.Log("SC HOLLOWMERE ambient: " + list.Count(f => f.Name.StartsWith("Pigeons_")) + " pigeon roosts on the square's shells");
        }

        static AmbientCreatures.Flock Circle(string name, Transform parent, Material mat, Vector3 centre, int count, float radius, float height, float speed, float span, float clump, int seed)
        {
            var f = new AmbientCreatures.Flock { Name = name, Move = AmbientCreatures.Motion.Circle, Centre = centre, Radius = radius, Height = height, Speed = speed, Wingspan = span * HollowmereAmbientLife.BirdScale, Clump = clump, Seed = seed };
            f.Cards = Cards(parent, name, mat, count);
            return f;
        }

        static void AddPerched(List<AmbientCreatures.Flock> list, Transform parent, TownLayout t, string name, FieldKind kind, float x0, float x1, float z0, float z1, int count, int seed)
        {
            var hits = new List<Vector2>();
            for (int pass = 0; pass < 2 && hits.Count == 0; pass++)   // the wanted field kind first, any open field when the layout has none in that half
                for (float x = x0; x <= x1; x += 6f)
                    for (float z = z0; z <= z1; z += 6f)
                    {
                        if (!SaltwakeRoadPlan.OnTerrain(x, z, 14f) || HollowmereLayout.PlateauDistance(x, z) < 40f) continue;
                        FieldKind k = SaltwakeRoadPlan.Field(x, z, out float edge, out _);
                        if ((pass == 0 && k != kind) || edge < 10f) continue;
                        if (HollowmereLayout.Ground(t, x, z) < HollowmereLayout.WaterY + 3f) continue;
                        hits.Add(new Vector2(x, z));
                    }
            if (hits.Count == 0) { Debug.LogWarning("SC HOLLOWMERE ambient: no open field for " + name + " in x " + x0 + ".." + x1); return; }
            Vector2 p = hits.OrderBy(h => SaltwakeRoadPlan.Hash01((int)(h.x * 7f + h.y * 131f), seed)).First();
            var f = new AmbientCreatures.Flock
            {
                Name = name, Move = AmbientCreatures.Motion.Perch, Centre = new Vector3(p.x, HollowmereLayout.Ground(t, p.x, p.y), p.y), Spread = 7f, Wingspan = 0.9f * HollowmereAmbientLife.BirdScale,
                AloftRadius = 36f, AloftHeight = 32f, Speed = 9f, Clump = 1.6f, Seed = seed,
            };
            f.Cards = Cards(parent, name, _crow, count);
            list.Add(f);
        }

        // ---------------------------------------------------------------------------------------------------------------- butterflies and bees
        static void BuildInsects(Transform life, List<AmbientCreatures.Flock> common, TownLayout t)
        {
            var spots = new List<(string Name, Vector2 P)>();
            foreach (string id in GardenLots) { Foundation f = t.Foundations.FirstOrDefault(x => x.Id == id); if (f != null) spots.Add(("Garden_" + id, new Vector2(f.Centre.X, f.Centre.Z))); }
            // the lane side of each garden lot (door side, just outside the gate), where the paths run: a second flock that reads from the lane
            foreach (string id in GardenLots) { Foundation f = t.Foundations.FirstOrDefault(x => x.Id == id); if (f == null) continue; Vector3 o = Quaternion.Euler(0f, f.FacingYaw, 0f) * new Vector3(0f, 0f, -(f.Side * 0.5f + 2.5f)); spots.Add(("Path_" + id, new Vector2(f.Centre.X + o.x, f.Centre.Z + o.z))); }
            spots.Add(("SquareSW", new Vector2(-15f, -17f))); spots.Add(("KeepStairs", new Vector2(-24f, 14f))); spots.Add(("LakeShore", new Vector2(-18f, -84f)));
            int m = 0;
            foreach (Vector2 p in MeadowSpots(5)) spots.Add(("Meadow" + m++, p));
            int seed = 200;
            foreach (var s in spots)
            {
                float y = HollowmereLayout.Ground(t, s.P.x, s.P.y);
                bool meadow = s.Name.StartsWith("Meadow"), pathSpot = s.Name.StartsWith("Path_");
                if (HollowmereAmbientLife.Butterflies)
                {
                    int sp = seed % Wings.Length;   // T24: peacock, small tortoiseshell, cabbage white, brimstone: a real pinned-specimen photo per flock
                    var f = new AmbientCreatures.Flock { Name = "Butterflies_" + s.Name, Move = AmbientCreatures.Motion.Wander, Centre = new Vector3(s.P.x, y, s.P.y), Radius = meadow ? 4.5f : 2.6f, Height = 1.3f, Speed = 0.9f, Spread = meadow ? 8f : 3.5f, Wingspan = HollowmereAmbientLife.ButterflySpan, Tilt = 40f, Hover = HollowmereAmbientLife.ButterflyHover, HideWithin = 4.2f, Seed = seed++ };
                    f.Cards = Cards(life, f.Name, _wingMat[sp], pathSpot ? HollowmereAmbientLife.PathButterflies : meadow ? HollowmereAmbientLife.ButterfliesPerFlock + 2 : HollowmereAmbientLife.ButterfliesPerFlock, _wingMesh[sp]);
                    common.Add(f);
                }
                if (HollowmereAmbientLife.Bees && !pathSpot)
                {
                    var f = new AmbientCreatures.Flock { Name = "Bees_" + s.Name, Move = AmbientCreatures.Motion.Wander, Centre = new Vector3(s.P.x, y, s.P.y), Radius = 1.3f, Height = 1.1f, Speed = 3.2f, Spread = 4f, Wingspan = HollowmereAmbientLife.BeeSpan, Tilt = 40f, Jitter = HollowmereAmbientLife.BeeJitter, Seed = seed++ };
                    f.Cards = Cards(life, f.Name, _bee, HollowmereAmbientLife.BeesPerFlock);
                    common.Add(f);
                }
            }
        }

        // Open pasture outside the walls and away from the lake, spread at least 60 m apart, in a fixed hash order.
        static List<Vector2> MeadowSpots(int n)
        {
            var hits = new List<Vector2>();
            var lc = HollowmereLayout.LakeCentre;
            for (float x = -130f; x <= 130f; x += 10f)
                for (float z = -150f; z <= 110f; z += 10f)
                {
                    if (!SaltwakeRoadPlan.OnTerrain(x, z, 14f) || HollowmereLayout.PlateauDistance(x, z) < 34f) continue;
                    float lx = (x - lc.X) / (HollowmereLayout.LakeRadiusX + 14f), lz = (z - lc.Z) / (HollowmereLayout.LakeRadiusZ + 14f);
                    if (lx * lx + lz * lz < 1f) continue;
                    if (SaltwakeRoadPlan.Field(x, z, out float edge, out _) != FieldKind.Pasture || edge < 6f) continue;
                    hits.Add(new Vector2(x, z));
                }
            var picked = new List<Vector2>();
            foreach (Vector2 h in hits.OrderBy(h => SaltwakeRoadPlan.Hash01((int)(h.x * 3f + h.y * 71f), 77)))
            {
                if (picked.Any(p => Vector2.Distance(p, h) < 60f)) continue;
                picked.Add(h);
                if (picked.Count >= n) break;
            }
            return picked;
        }

        static Transform[] Cards(Transform parent, string name, Material mat, int count, Mesh mesh = null)
        {
            var holder = new GameObject("Flock_" + name).transform; holder.SetParent(parent, false);
            var cards = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Card" + i); go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh != null ? mesh : _card;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                cards[i] = go.transform;
            }
            return cards;
        }

        static void Finish(GameObject owner, List<AmbientCreatures.Flock> flocks)
        {
            if (flocks.Count == 0) return;
            var c = owner.AddComponent<AmbientCreatures>();
            c.Flocks = flocks.ToArray();
            c.Pose(37f, 0f, false);   // a settled frame for edit-mode captures
        }

        static Mesh EnsureCardMesh()
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            Mesh fresh = AmbientCreatures.BuildCardMesh();
            if (m == null) { AssetDatabase.CreateAsset(fresh, MeshPath); return fresh; }
            m.Clear(); m.vertices = fresh.vertices; m.uv = fresh.uv; m.triangles = fresh.triangles; m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            Object.DestroyImmediate(fresh);
            return m;
        }

        static void EnsureCardMaterials()
        {
            Shader sh = Shader.Find("SleepingCalamity/BirdCard") ?? throw new System.InvalidOperationException("Shader not found: SleepingCalamity/BirdCard");
            // T11: birds (cards of 1-3 m) fade out inside 8-26 m of any camera and carry a resting V; butterflies and bees (0.12-0.25 m) only inside 0.8-2.5 m
            _lark = CardMat("M_HM_Bird_Lark", sh, new Color(0.34f, 0.29f, 0.22f), 1.1f, 20f, 0.45f, null, 0.2f, 8f, 26f);   // T11: small brown larks over the meadow and the road
            _pigeon = CardMat("M_HM_Bird_Pigeon", sh, new Color(0.40f, 0.42f, 0.47f), 1.15f, 18f, 0.45f, new Color(0.22f, 0.23f, 0.27f, 1f), 0.18f, 5f, 16f);   // T11: roof pigeons, seen close, so they fade later than the high birds
            _crow = CardMat("M_HM_Bird_Crow", sh, new Color(0.15f, 0.15f, 0.17f), 1.15f, 13f, 0.5f, null, 0.22f, 8f, 26f);
            _swallow = CardMat("M_HM_Bird_Swallow", sh, new Color(0.17f, 0.18f, 0.23f), 1.3f, 26f, 0.4f, null, 0.22f, 8f, 26f);
            _gull = CardMat("M_HM_Bird_Gull", sh, new Color(0.62f, 0.63f, 0.64f), 0.8f, 7f, 0.3f, null, 0.22f, 10f, 30f);   // greyer than before (0.80): a pale flat card read as a grey gull sticker
            _butterfly = CardMat("M_HM_Butterfly", sh, new Color(1.6f, 1.55f, 1.35f), 0.8f, 22f, 0.95f, new Color(0.35f, 0.5f, 1.1f, 1f), 0.1f, 0.8f, 2.5f);
            _butterflyYellow = CardMat("M_HM_Butterfly_Yellow", sh, new Color(1.8f, 1.5f, 0.3f), 0.8f, 22f, 0.95f, new Color(0.9f, 0.35f, 0.08f, 1f), 0.1f, 0.8f, 2.5f);
            _butterflyOrange = CardMat("M_HM_Butterfly_Orange", sh, new Color(1.8f, 0.8f, 0.12f), 0.8f, 22f, 0.95f, new Color(0.25f, 0.1f, 0.05f, 1f), 0.1f, 0.8f, 2.5f);
            _bee = CardMat("M_HM_Bee", sh, new Color(0.55f, 0.36f, 0.05f), 1.0f, 140f, 0.3f, new Color(0.14f, 0.1f, 0.04f, 1f), 0f, 0.5f, 1.5f);
        }

        static Material CardMat(string name, Shader sh, Color c, float belly, float flapSpeed, float flapAmp, Color? tip = null, float dihedral = 0.22f, float fadeNear = 8f, float fadeFar = 26f)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetColor("_Color", c); m.SetFloat("_Belly", belly); m.SetFloat("_FlapSpeed", flapSpeed); m.SetFloat("_FlapAmp", flapAmp); m.SetFloat("_Fold", 0f); m.SetColor("_TipColor", tip ?? new Color(0f, 0f, 0f, 0f));
            m.SetFloat("_Dihedral", dihedral); m.SetFloat("_FadeNear", fadeNear); m.SetFloat("_FadeFar", fadeFar);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
        }


        // ---------------------------------------------------------------------------------------------------------------- T24 photo-textured butterflies
        // Four pinned-specimen photos (Wikimedia Commons, Museum of Toulouse "MHNT", CC BY-SA 4.0; see Content/Art/Town/AmbientLife/Wings/SOURCES.md), cut out with alpha, on a grid mesh with SC_WingCard.
        static readonly string[] Wings = { "peacock", "tortoise", "cabbage", "brimstone" };
        const string WingFolder = "Assets/_Project/Content/Art/Town/AmbientLife/Wings";
        static readonly Mesh[] _wingMesh = new Mesh[4];
        static readonly Material[] _wingMat = new Material[4];

        static void EnsureWings()
        {
            Shader sh = Shader.Find("SleepingCalamity/WingCard") ?? throw new System.InvalidOperationException("Shader not found: SleepingCalamity/WingCard");
            for (int i = 0; i < Wings.Length; i++)
            {
                string tp = WingFolder + "/T_Wing_" + Wings[i] + ".png";
                BeautyCornerArt.ImportTexture(tp, true, false, true, 1024);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp) ?? throw new System.IO.FileNotFoundException(tp);
                float aspect = tex.height / (float)tex.width;
                string mp = TexFolder + "/Wing_" + Wings[i] + ".asset";
                Mesh fresh = AmbientWingMesh.Build("Wing_" + Wings[i], aspect, 12, 4);
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                if (m == null) { AssetDatabase.CreateAsset(fresh, mp); m = fresh; }
                else { m.Clear(); m.vertices = fresh.vertices; m.uv = fresh.uv; m.triangles = fresh.triangles; m.RecalculateBounds(); m.RecalculateNormals(); EditorUtility.SetDirty(m); Object.DestroyImmediate(fresh); }
                _wingMesh[i] = m;
                Material mat = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_Wing_" + Wings[i] + ".mat", sh);
                mat.SetTexture("_MainTex", tex); mat.SetColor("_Tint", new Color(0.62f, 0.6f, 0.56f)); mat.SetFloat("_Cutoff", 0.5f); mat.SetFloat("_Translucency", Wings[i] == "cabbage" || Wings[i] == "brimstone" ? 0.45f : 0.35f);   // Pass A: tinted down so a cabbage white does not read as an unlit white card
                mat.SetFloat("_FlapSpeed", 22f); mat.SetFloat("_FlapAmp", 0.55f); mat.SetFloat("_Fold", 0f); mat.SetFloat("_Dihedral", 0.18f); mat.SetFloat("_FadeNear", 0.3f); mat.SetFloat("_FadeFar", 0.8f);
                mat.enableInstancing = false;
                EditorUtility.SetDirty(mat);
                _wingMat[i] = mat;
            }
        }

        // ---------------------------------------------------------------------------------------------------------------- fish rings
        static readonly Vector2[] RingPatches = { new Vector2(-34f, 3f), new Vector2(-12f, -7f), new Vector2(12f, 8f), new Vector2(34f, -4f), new Vector2(54f, 4f) };   // offsets from the lake centre

        static void BuildFishRings(Transform life, TownLayout t)
        {
            EnsureParticleTextures();
            Material mat = ParticleMat("M_HM_FishRing", _ringTex, new Color(0.86f, 0.90f, 0.90f, 0.55f));
            var grp = new GameObject("FishRings").transform; grp.SetParent(life, false);
            int seed = 301;
            foreach (Vector2 o in RingPatches)
            {
                float x = HollowmereLayout.LakeCentre.X + o.x, z = HollowmereLayout.LakeCentre.Z + o.y;
                if (HollowmereLayout.Ground(t, x, z) > HollowmereLayout.WaterY - 0.8f) continue;   // only over real depth
                var go = (GameObject)PrefabUtility.InstantiatePrefab(ParticlePrefab("Assets/_Project/Content/Prefabs/Hollowmere/HM_FishRing.prefab", () => MakeRing(mat, 301)), grp);
                go.name = "Rings_" + (int)o.x;
                go.transform.position = new Vector3(x, HollowmereLayout.WaterY + 0.04f, z);
                var rps = go.GetComponent<ParticleSystem>();
                rps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rps.randomSeed = (uint)seed++;
            }
        }

        static GameObject MakeRing(Material mat, int seed)
        {
            var go = new GameObject("HM_FishRing");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f; main.loop = true; main.prewarm = true; main.startLifetime = 3.4f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); main.startRotation = 0f;
            main.startColor = new Color(1f, 1f, 1f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 8; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = HollowmereAmbientLife.FishRingRate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(14f, 0f, 6f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 2.2f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }


        // ---------------------------------------------------------------------------------------------------------------- reed sway
        static void BuildReedSway(Transform life, Transform root)
        {
            var reeds = new List<Transform>();
            foreach (Transform tr in root.GetComponentsInChildren<Transform>(true))
                if (tr.parent != null && (tr.parent.name == "ShoreReeds" || tr.parent.name == "PoolReeds")) reeds.Add(tr);
            if (reeds.Count == 0) return;
            var go = new GameObject("ReedSway"); go.transform.SetParent(life, false);
            go.AddComponent<AmbientReedSway>().Reeds = reeds.ToArray();
        }

        // ---------------------------------------------------------------------------------------------------------------- wind waves (cloud shadows + gust bands)
        static void BuildWindWaves(Transform root)
        {
            Transform sun = root.Find("Lighting/Sun");
            if (sun == null) return;
            Light light = sun.GetComponent<Light>();
            var existing = sun.GetComponent<AmbientCloudShadows>();
            if (!HollowmereAmbientLife.WindWaves)
            {
                if (existing != null) Object.DestroyImmediate(existing);
                light.cookie = null;
                return;
            }
            Texture2D cookie = EnsureCookie();
            light.cookie = cookie; light.cookieSize2D = new Vector2(HollowmereAmbientLife.CookieSize, HollowmereAmbientLife.CookieSize);
            if (existing == null) sun.gameObject.AddComponent<AmbientCloudShadows>();
        }

        const int CookieRes = 256;
        const string CookiePath = TexFolder + "/T_HM_CloudGust_Cookie.png";

        static Texture2D EnsureCookie()
        {
            var px = new Color32[CookieRes * CookieRes];
            for (int j = 0; j < CookieRes; j++)
                for (int i = 0; i < CookieRes; i++)
                {
                    float u = i / (float)CookieRes, v = j / (float)CookieRes;
                    float c = 0.55f * VN(u, v, 3, 1) + 0.3f * VN(u, v, 6, 2) + 0.15f * VN(u, v, 12, 3);
                    float cloud = Smooth(0.46f, 0.66f, c);
                    float patch = VN(u + 0.31f, v + 0.67f, 3, 4);
                    float band = 0.5f + 0.5f * Mathf.Sin(6.2831853f * (8f * u + 6f * v) + 2.4f * (VN(u, v, 4, 5) - 0.5f) * 6.2831853f);
                    float light = 1f - HollowmereAmbientLife.CloudShadowStrength * cloud - HollowmereAmbientLife.GustBandStrength * band * (0.25f + 0.75f * patch);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(light * 255f), 0, 255);
                    px[j * CookieRes + i] = new Color32(b, b, b, 255);
                }
            Texture2D tex = WritePng(CookiePath, CookieRes, px);
            var imp = (TextureImporter)AssetImporter.GetAtPath(CookiePath);
            if (imp.textureType != TextureImporterType.Cookie || imp.wrapMode != TextureWrapMode.Repeat || imp.mipmapEnabled)
            {
                imp.textureType = TextureImporterType.Cookie; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath) ?? tex;
        }

        // ---------------------------------------------------------------------------------------------------------------- chimney smoke
        static void BuildSmoke(Transform ruined, Transform rebuilt, TownLayout t)
        {
            EnsureParticleTextures();
            Material mat = ParticleMat("M_HM_Smoke", _smokeTex, new Color(HollowmereAmbientLife.SmokeColour.r, HollowmereAmbientLife.SmokeColour.g, HollowmereAmbientLife.SmokeColour.b, HollowmereAmbientLife.SmokeAlpha));
            // the heroes' home chimney: both presets
            foreach (Transform preset in new[] { ruined, rebuilt })
            {
                if (preset == null) continue;
                Transform home = preset.Find("Landmarks/HeroesHome_Cistern");
                if (home != null && ChimneyTop(home, 0, true, out Vector3 top)) Plume(Holder(preset), "Smoke_HeroesHome", top, mat, 401);
            }
            // T24: the four living (the heroes) keep four fires in the Ruined preset: the home's cistern (above), its annex and the two lots nearest the home. (The docs name no four family-house lots yet; T25 may re-pick.)
            if (ruined != null)
            {
                Transform annex = ruined.Find("Landmarks/HeroesHome_Annex");
                if (annex != null && ChimneyTop(annex, 7, false, out Vector3 at)) Plume(Holder(ruined), "Smoke_HeroesAnnex", at, mat, 402);
                Transform rf = ruined.Find("Foundations");
                if (rf != null)
                {
                    var home = new Vector2(12.5f, 11.25f);
                    int s2 = 403;
                    foreach (Foundation f in t.Foundations.Where(x => x.Id != "F03" && x.Id != "F05" && x.Id != "F06").OrderBy(x => (new Vector2(x.Centre.X, x.Centre.Z) - home).sqrMagnitude).Take(2))
                    {
                        Transform lot = null;
                        foreach (Transform c in rf) if (c.name.StartsWith(f.Id + "_", System.StringComparison.Ordinal)) { lot = c; break; }
                        if (lot != null && ChimneyTop(lot, s2, false, out Vector3 lt)) Plume(Holder(ruined), "Smoke_Hero_" + f.Id, lt, mat, s2);
                        s2++;
                    }
                }
            }
            if (rebuilt == null) return;
            Transform foundations = rebuilt.Find("Foundations");
            if (foundations == null) return;
            int seed = 410;
            foreach (Foundation f in t.Foundations)
            {
                var kind = HollowmereLayout.RebuiltBuilding(f.Id);
                if (kind == HollowmereLayout.BuildingKind.Empty || kind == HollowmereLayout.BuildingKind.Garden || kind == HollowmereLayout.BuildingKind.ApprovedFacade) continue;
                Transform lot = null;
                foreach (Transform c in foundations) if (c.name.StartsWith(f.Id + "_", System.StringComparison.Ordinal)) { lot = c; break; }
                if (lot != null && ChimneyTop(lot, seed, false, out Vector3 top)) Plume(Holder(rebuilt), "Smoke_" + f.Id, top, mat, seed);
                seed++;
            }
            Transform facade = rebuilt.Find("Corner/Facade");   // the approved facade row (F01)
            if (facade != null && ChimneyTop(facade, 499, false, out Vector3 ft)) Plume(Holder(rebuilt), "Smoke_F01", ft, mat, 499);
        }

        static Transform Holder(Transform preset)
        {
            Transform h = preset.Find("HM_Smoke");
            if (h != null) return h;
            h = new GameObject("HM_Smoke").transform; h.SetParent(preset, false);
            return h;
        }

        // The chimney's top: the renderers named "chimney" when the building has them, else the roof top of the whole building's bounds (offset sideways by the seed;
        // the heroes' home sits at its west gable, where the art puts the stack).
        static bool ChimneyTop(Transform building, int seed, bool westGable, out Vector3 top)
        {
            Bounds? chim = null, all = null;
            foreach (MeshRenderer r in building.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.sharedMaterial == null || r.GetComponent<MeshFilter>() == null || r.GetComponent<MeshFilter>().sharedMesh == null) continue;
                Bounds b = r.bounds;
                all = all.HasValue ? Grow(all.Value, b) : b;
                if (r.name.IndexOf("chimney", System.StringComparison.OrdinalIgnoreCase) >= 0) chim = chim.HasValue ? Grow(chim.Value, b) : b;
            }
            top = default;
            if (chim.HasValue) { top = new Vector3(chim.Value.center.x, chim.Value.max.y, chim.Value.center.z); return true; }
            if (!all.HasValue) return false;
            Bounds a = all.Value;
            if (westGable) { top = new Vector3(a.min.x + 0.14f * a.size.x, a.max.y, a.center.z); return true; }
            float hx = SaltwakeRoadPlan.Hash01(seed, 1) - 0.5f, hz = SaltwakeRoadPlan.Hash01(seed, 2) - 0.5f;
            top = new Vector3(a.center.x + hx * a.size.x * 0.4f, a.max.y, a.center.z + hz * a.size.z * 0.3f);
            return true;
        }

        static Bounds Grow(Bounds a, Bounds b) { a.Encapsulate(b); return a; }

        // T09b scene diet: the 40 chimney plumes were 52 KB of particle YAML each (2.1 MB of the scene file). One prefab holds the plume; each scene object is an instance with its own
        // position and random seed. The prefab is created once (delete it to regenerate after changing the plume settings) and committed, so builds stay byte-identical.
        // One saved particle prefab per kind (created once from `make`, then reused and committed): the fish rings and pollen volumes were 50 KB of YAML each too.
        internal static GameObject ParticlePrefab(string path, System.Func<GameObject> make)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            BuilderUtil.EnsureFolder("Assets/_Project/Content/Prefabs/Hollowmere");
            GameObject tmp = make();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(tmp, path);
            Object.DestroyImmediate(tmp);
            return saved;
        }

        const string SmokePrefabPath = "Assets/_Project/Content/Prefabs/Hollowmere/HM_SmokePlume.prefab";

        static GameObject SmokePrefab(Material mat)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefabPath);
            if (existing != null) return existing;
            BuilderUtil.EnsureFolder("Assets/_Project/Content/Prefabs/Hollowmere");
            GameObject tmp = MakePlume("HM_SmokePlume", mat, 401);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(tmp, SmokePrefabPath);
            Object.DestroyImmediate(tmp);
            return saved;
        }

        static void Plume(Transform parent, string name, Vector3 top, Material mat, int seed)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(SmokePrefab(mat), parent);
            go.name = name;
            go.transform.position = top + Vector3.up * 0.15f; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // cone axis (+Z) points up
            var inst = go.GetComponent<ParticleSystem>();
            inst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            inst.randomSeed = (uint)seed;
        }

        static GameObject MakePlume(string name, Material mat, int seed)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            float life = HollowmereAmbientLife.SmokeLifetime;
            main.duration = 10f; main.loop = true; main.prewarm = true; main.startLifetime = life; main.startSpeed = 1.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.35f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.2832f);
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(HollowmereAmbientLife.SmokeRate * life) + 4; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = HollowmereAmbientLife.SmokeRate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4f; shape.radius = 0.08f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector2 w = HollowmereAmbientLife.WindDirection;
            vel.x = new ParticleSystem.MinMaxCurve(0.6f * w.x, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            vel.y = new ParticleSystem.MinMaxCurve(0.0f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            vel.z = new ParticleSystem.MinMaxCurve(0.6f * w.y, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 5.6f)));   // size 0.25-0.35 m -> about 1.4-2 m
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.87f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.3f; noise.scrollSpeed = 0.15f;
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        // ---------------------------------------------------------------------------------------------------------------- flame (T11: the brazier)
        const string FlamePrefabPath = "Assets/_Project/Content/Prefabs/Hollowmere/HM_BrazierFlame.prefab";

        /// <summary>T11: a real flame in place of the orange sphere: a few dozen additive teardrop cards that rise, narrow and cool from yellow to red (noise-licked), ember sparks and a thin smoke thread, as one prefab instance. Cost: 3 particle systems, at most 28 + 16 + 12 particles.</summary>
        public static GameObject BuildFlame(Transform parent, Vector3 pos)
        {
            EnsureParticleTextures();
            Texture2D flameTex = FlameTexture();
            Material flameMat = AdditiveMat("M_HM_Flame", flameTex, new Color(1.5f, 1.2f, 0.85f, 1f));
            Material sparkMat = AdditiveMat("M_HM_Ember", _smokeTex, new Color(2.2f, 1.2f, 0.5f, 1f));
            Material smokeMat = ParticleMat("M_HM_FlameSmoke", _smokeTex, new Color(0.35f, 0.33f, 0.32f, 0.32f));
            GameObject pf = ParticlePrefab(FlamePrefabPath, () => MakeFlame(flameMat, sparkMat, smokeMat));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.name = "BrazierFlame"; go.transform.position = pos;
            foreach (ParticleSystem ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return go;
        }

        static GameObject MakeFlame(Material flameMat, Material sparkMat, Material smokeMat)
        {
            var root = new GameObject("HM_BrazierFlame");
            root.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // cone axes (+Z) point up
            // flame body: teardrop cards, screen-up, rising 0.55 m and narrowing to nothing
            ParticleSystem flame = root.AddComponent<ParticleSystem>(); flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = flame.main;
            main.duration = 4f; main.loop = true; main.prewarm = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.85f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSize3D = true; main.startSizeX = new ParticleSystem.MinMaxCurve(0.3f, 0.46f); main.startSizeY = new ParticleSystem.MinMaxCurve(0.6f, 1.0f); main.startSizeZ = 1f;
            main.startRotation = 0f; main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 36; main.gravityModifier = 0f;
            var em = flame.emission; em.rateOverTime = 26f;
            var shape = flame.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4f; shape.radius = 0.09f;
            var sz = flame.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.75f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.12f)));
            var col = flame.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.62f, 0.18f), 0.3f), new GradientColorKey(new Color(0.85f, 0.22f, 0.05f), 0.75f), new GradientColorKey(new Color(0.3f, 0.05f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.6f, 0.65f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = flame.noise; noise.enabled = true; noise.strength = 0.22f; noise.frequency = 1.6f; noise.scrollSpeed = 1.8f; noise.damping = true;
            flame.useAutoRandomSeed = false; flame.randomSeed = 77u;
            var fr = root.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = flameMat; fr.renderMode = ParticleSystemRenderMode.Billboard; fr.shadowCastingMode = ShadowCastingMode.Off; fr.receiveShadows = false;

            // sparks: tiny embers lifting off, drifting with the wind
            var sparks = new GameObject("Embers"); sparks.transform.SetParent(root.transform, false);
            ParticleSystem sp = sparks.AddComponent<ParticleSystem>(); sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var sm = sp.main;
            sm.duration = 6f; sm.loop = true; sm.prewarm = true; sm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f); sm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f); sm.startColor = Color.white; sm.simulationSpace = ParticleSystemSimulationSpace.World; sm.maxParticles = 16; sm.gravityModifier = -0.04f;
            var se = sp.emission; se.rateOverTime = 5f;
            var ss = sp.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 22f; ss.radius = 0.16f;
            var sc = sp.colorOverLifetime; sc.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.08f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            sc.color = sg;
            var sn = sp.noise; sn.enabled = true; sn.strength = 0.6f; sn.frequency = 0.8f; sn.scrollSpeed = 0.6f;
            sp.useAutoRandomSeed = false; sp.randomSeed = 78u;
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.sharedMaterial = sparkMat; sr.renderMode = ParticleSystemRenderMode.Billboard; sr.shadowCastingMode = ShadowCastingMode.Off; sr.receiveShadows = false;

            // a thin smoke thread above the flame
            var smoke = new GameObject("Smoke"); smoke.transform.SetParent(root.transform, false); smoke.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            ParticleSystem sk = smoke.AddComponent<ParticleSystem>(); sk.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var km = sk.main;
            km.duration = 8f; km.loop = true; km.prewarm = true; km.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f); km.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            km.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f); km.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); km.startColor = Color.white;
            km.simulationSpace = ParticleSystemSimulationSpace.World; km.maxParticles = 12; km.gravityModifier = 0f;
            var ke = sk.emission; ke.rateOverTime = 2.4f;
            var ks = sk.shape; ks.shapeType = ParticleSystemShapeType.Cone; ks.angle = 6f; ks.radius = 0.1f;
            var kz = sk.sizeOverLifetime; kz.enabled = true; kz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 4.5f)));
            var kc = sk.colorOverLifetime; kc.enabled = true;
            var kg = new Gradient();
            kg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            kc.color = kg;
            var kn = sk.noise; kn.enabled = true; kn.strength = 0.3f; kn.frequency = 0.35f; kn.scrollSpeed = 0.2f;
            sk.useAutoRandomSeed = false; sk.randomSeed = 79u;
            var kr = smoke.GetComponent<ParticleSystemRenderer>();
            kr.sharedMaterial = smokeMat; kr.renderMode = ParticleSystemRenderMode.Billboard; kr.shadowCastingMode = ShadowCastingMode.Off; kr.receiveShadows = false;
            return root;
        }

        internal static Material AdditiveMat(string name, Texture2D tex, Color tint)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new System.InvalidOperationException("URP Particles/Unlit not found");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 1f);   // Additive: SrcAlpha / One
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 1f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        // A teardrop flame card: wide at the foot, a pointed licking tip, soft edges; a value noise bends the outline so no two layers match.
        static Texture2D FlameTexture()
        {
            const int R = 128;
            var px = new Color32[R * R];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float u = (i + 0.5f) / R, v = (j + 0.5f) / R;   // v = 0 at the foot
                    float bend = (VN(u, v, 4, 21) - 0.5f) * 0.22f * v + (VN(u, v, 8, 22) - 0.5f) * 0.08f;
                    float x = (u - 0.5f - bend) * 2f;
                    float width = Mathf.Pow(Mathf.Clamp01(1f - v), 0.8f) * (0.35f + 0.65f * Mathf.Sin(Mathf.Clamp01(v * 3.2f) * 1.5708f)) * 0.9f;
                    float d = Mathf.Abs(x) / Mathf.Max(0.02f, width);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    a *= Mathf.Clamp01(v * 10f);   // soft base
                    a *= Mathf.Lerp(0.8f, 1.15f, VN(u, v, 6, 23));
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                    px[j * R + i] = new Color32(255, 255, 255, b);
                }
            return ParticlePng(TexFolder + "/T_HM_Flame.png", R, px);
        }

        // ---------------------------------------------------------------------------------------------------------------- particle assets
        static Material ParticleMat(string name, Texture2D tex, Color tint)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new System.InvalidOperationException("URP Particles/Unlit not found");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint);
            // Surface Transparent, Blend Alpha (what the Particles inspector writes): SrcAlpha / OneMinusSrcAlpha, no depth write.
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureParticleTextures()
        {
            if (_smokeTex != null && _ringTex != null) return;
            const int R = 128;
            var smoke = new Color32[R * R]; var ring = new Color32[R * R];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float x = (i + 0.5f) / R * 2f - 1f, y = (j + 0.5f) / R * 2f - 1f, d = Mathf.Sqrt(x * x + y * y);
                    float n = 0.6f * VN((i + 0.5f) / R, (j + 0.5f) / R, 4, 9) + 0.4f * VN((i + 0.5f) / R, (j + 0.5f) / R, 8, 10);
                    float a = Mathf.Clamp01((0.85f - d) / 0.85f); a = a * a * (3f - 2f * a) * Mathf.Lerp(0.6f, 1.1f, n);   // radial falloff reaches 0 well inside the quad: no square edges
                    smoke[j * R + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                    float k = (d - 0.72f) / 0.07f, edge = Mathf.Clamp01((1f - d) / 0.12f);
                    float ra = Mathf.Exp(-k * k) * edge * Mathf.Lerp(0.55f, 1f, VN((i + 0.5f) / R, (j + 0.5f) / R, 5, 11));
                    ring[j * R + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(ra * 255f), 0, 255));
                }
            _smokeTex = ParticlePng(TexFolder + "/T_HM_SmokePuff.png", R, smoke);
            _ringTex = ParticlePng(TexFolder + "/T_HM_FishRing.png", R, ring);
        }

        internal static Texture2D ParticlePng(string path, int res, Color32[] px)
        {
            WritePng(path, res, px);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.alphaSource != TextureImporterAlphaSource.FromInput)   // T11: LookTestTextureImporter used to force alphaSource None
            {
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Writes the PNG only when its bytes changed (deterministic assets do not churn), imports it, and returns the loaded texture.
        static Texture2D WritePng(string path, int res, Color32[] px)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(path);
            if (!File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png)) { File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
            else if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------------------------------------------------------------------------------------------------------------- tileable value noise
        static float Smooth(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3f - 2f * x); }

        static float Lat(int i, int j, int n, int seed)
        {
            i = ((i % n) + n) % n; j = ((j % n) + n) % n;
            unchecked
            {
                uint h = (uint)(i * 374761393 + j * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        // Value noise on an n x n lattice that wraps at u, v = 1 (so the texture tiles).
        static float VN(float u, float v, int n, int seed)
        {
            u = (u - Mathf.Floor(u)) * n; v = (v - Mathf.Floor(v)) * n;
            int i = Mathf.FloorToInt(u), j = Mathf.FloorToInt(v);
            float fu = u - i, fv = v - j; fu = fu * fu * (3f - 2f * fu); fv = fv * fv * (3f - 2f * fv);
            float a = Mathf.Lerp(Lat(i, j, n, seed), Lat(i + 1, j, n, seed), fu), b = Mathf.Lerp(Lat(i, j + 1, n, seed), Lat(i + 1, j + 1, n, seed), fu);
            return Mathf.Lerp(a, b, fv);
        }
    }
}
