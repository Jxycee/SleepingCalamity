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

        static Mesh _card; static Material _crow, _swallow, _gull, _butterfly, _bee;
        static Texture2D _smokeTex, _ringTex;

        public static void Build(Transform root, TownLayout t)
        {
            var life = new GameObject(RootName).transform; life.SetParent(root, false);
            BuilderUtil.EnsureFolder(TexFolder);
            Transform ruined = root.Find(HollowmereBuilder.RuinedRoot), rebuilt = root.Find(HollowmereBuilder.RebuiltRoot);
            if (HollowmereAmbientLife.Birds || HollowmereAmbientLife.Butterflies || HollowmereAmbientLife.Bees) { _card = EnsureCardMesh(); EnsureCardMaterials(); }

            var common = new List<AmbientCreatures.Flock>();
            if (HollowmereAmbientLife.Birds) BuildBirds(life, ruined, common, t);
            if (HollowmereAmbientLife.Butterflies || HollowmereAmbientLife.Bees) BuildInsects(life, common, t);
            Finish(life.gameObject, common);

            if (HollowmereAmbientLife.FishRings) BuildFishRings(life, t);
            if (HollowmereAmbientLife.ReedSway) BuildReedSway(life, root);
            BuildWindWaves(root);
            if (HollowmereAmbientLife.ChimneySmoke) BuildSmoke(ruined, rebuilt, t);
        }

        // ---------------------------------------------------------------------------------------------------------------- birds
        static void BuildBirds(Transform life, Transform ruined, List<AmbientCreatures.Flock> common, TownLayout t)
        {
            float tower = HollowmereLayout.KeepHeight + 17f;
            common.Add(Circle("BellTowerCrows", life, _crow, new Vector3(5.6f, tower, 24f), 7, 12.5f, 7f, 6.5f, 0.9f, 6.28f, 101));
            float sq = HollowmereLayout.Ground(t, 0f, -7f);
            common.Add(Circle("SquareSwallows", life, _swallow, new Vector3(0f, sq, -7f), 7, 20f, 9f, 12f, 0.34f, 6.28f, 102));
            common.Add(Circle("LakeGulls", life, _gull, new Vector3(HollowmereLayout.LakeCentre.X, HollowmereLayout.WaterY, HollowmereLayout.LakeCentre.Z), 6, 38f, 22f, 7f, 1.2f, 6.28f, 103));
            Vector3 valley = new Vector3(-95f, HollowmereLayout.Ground(t, -95f, 20f), 20f);
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

        static AmbientCreatures.Flock Circle(string name, Transform parent, Material mat, Vector3 centre, int count, float radius, float height, float speed, float span, float clump, int seed)
        {
            var f = new AmbientCreatures.Flock { Name = name, Move = AmbientCreatures.Motion.Circle, Centre = centre, Radius = radius, Height = height, Speed = speed, Wingspan = span, Clump = clump, Seed = seed };
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
                Name = name, Move = AmbientCreatures.Motion.Perch, Centre = new Vector3(p.x, HollowmereLayout.Ground(t, p.x, p.y), p.y), Spread = 7f, Wingspan = 0.9f,
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
            spots.Add(("SquareSW", new Vector2(-15f, -17f))); spots.Add(("KeepStairs", new Vector2(-24f, 14f))); spots.Add(("LakeShore", new Vector2(-18f, -84f)));
            int m = 0;
            foreach (Vector2 p in MeadowSpots(5)) spots.Add(("Meadow" + m++, p));
            int seed = 200;
            foreach (var s in spots)
            {
                float y = HollowmereLayout.Ground(t, s.P.x, s.P.y);
                bool meadow = s.Name.StartsWith("Meadow");
                if (HollowmereAmbientLife.Butterflies)
                {
                    var f = new AmbientCreatures.Flock { Name = "Butterflies_" + s.Name, Move = AmbientCreatures.Motion.Wander, Centre = new Vector3(s.P.x, y, s.P.y), Radius = meadow ? 4.5f : 3.2f, Height = 1.7f, Speed = 0.9f, Spread = meadow ? 8f : 5f, Wingspan = 0.1f, Seed = seed++ };
                    f.Cards = Cards(life, f.Name, _butterfly, meadow ? 6 : 4);
                    common.Add(f);
                }
                if (HollowmereAmbientLife.Bees)
                {
                    var f = new AmbientCreatures.Flock { Name = "Bees_" + s.Name, Move = AmbientCreatures.Motion.Wander, Centre = new Vector3(s.P.x, y, s.P.y), Radius = 1.3f, Height = 1.2f, Speed = 3.2f, Spread = 3.5f, Wingspan = 0.05f, Seed = seed++ };
                    f.Cards = Cards(life, f.Name, _bee, 4);
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

        static Transform[] Cards(Transform parent, string name, Material mat, int count)
        {
            var holder = new GameObject("Flock_" + name).transform; holder.SetParent(parent, false);
            var cards = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Card" + i); go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = _card;
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
            _crow = CardMat("M_HM_Bird_Crow", sh, new Color(0.075f, 0.075f, 0.085f), 1.15f, 13f, 0.5f);
            _swallow = CardMat("M_HM_Bird_Swallow", sh, new Color(0.10f, 0.11f, 0.15f), 1.3f, 26f, 0.4f);
            _gull = CardMat("M_HM_Bird_Gull", sh, new Color(0.62f, 0.63f, 0.62f), 0.8f, 7f, 0.3f);
            _butterfly = CardMat("M_HM_Butterfly", sh, new Color(0.60f, 0.56f, 0.40f), 0.85f, 38f, 0.9f);
            _bee = CardMat("M_HM_Bee", sh, new Color(0.17f, 0.12f, 0.05f), 1.0f, 140f, 0.3f);
        }

        static Material CardMat(string name, Shader sh, Color c, float belly, float flapSpeed, float flapAmp)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetColor("_Color", c); m.SetFloat("_Belly", belly); m.SetFloat("_FlapSpeed", flapSpeed); m.SetFloat("_FlapAmp", flapAmp); m.SetFloat("_Fold", 0f);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            return m;
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
                var go = new GameObject("Rings_" + (int)o.x); go.transform.SetParent(grp, false);
                go.transform.position = new Vector3(x, HollowmereLayout.WaterY + 0.04f, z);
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
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed++;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            }
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
            Material mat = ParticleMat("M_HM_Smoke", _smokeTex, new Color(0.60f, 0.58f, 0.56f, 0.34f));
            // the heroes' home chimney: both presets
            foreach (Transform preset in new[] { ruined, rebuilt })
            {
                if (preset == null) continue;
                Transform home = preset.Find("Landmarks/HeroesHome_Cistern");
                if (home != null && ChimneyTop(home, 0, true, out Vector3 top)) Plume(Holder(preset), "Smoke_HeroesHome", top, mat, 401);
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

        static void Plume(Transform parent, string name, Vector3 top, Material mat, int seed)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.position = top + Vector3.up * 0.15f; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // cone axis (+Z) points up
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            float life = HollowmereAmbientLife.SmokeLifetime;
            main.duration = 10f; main.loop = true; main.prewarm = true; main.startLifetime = life; main.startSpeed = 0.35f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.2832f);
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(HollowmereAmbientLife.SmokeRate * life) + 4; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = HollowmereAmbientLife.SmokeRate;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 5f; shape.radius = 0.12f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector2 w = HollowmereAmbientLife.WindDirection;
            vel.x = new ParticleSystem.MinMaxCurve(1.1f * w.x, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            vel.y = new ParticleSystem.MinMaxCurve(0.2f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            vel.z = new ParticleSystem.MinMaxCurve(1.1f * w.y, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 3.2f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.80f, 0.78f, 0.76f), 0f), new GradientColorKey(new Color(0.55f, 0.55f, 0.56f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.55f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.3f; noise.scrollSpeed = 0.15f;
            ps.useAutoRandomSeed = false; ps.randomSeed = (uint)seed;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
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
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a) * Mathf.Lerp(0.55f, 1.25f, n);
                    smoke[j * R + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                    float k = (d - 0.72f) / 0.07f, edge = Mathf.Clamp01((1f - d) / 0.12f);
                    float ra = Mathf.Exp(-k * k) * edge * Mathf.Lerp(0.55f, 1f, VN((i + 0.5f) / R, (j + 0.5f) / R, 5, 11));
                    ring[j * R + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(ra * 255f), 0, 255));
                }
            _smokeTex = ParticlePng(TexFolder + "/T_HM_SmokePuff.png", R, smoke);
            _ringTex = ParticlePng(TexFolder + "/T_HM_FishRing.png", R, ring);
        }

        static Texture2D ParticlePng(string path, int res, Color32[] px)
        {
            WritePng(path, res, px);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp)
            {
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp;
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
