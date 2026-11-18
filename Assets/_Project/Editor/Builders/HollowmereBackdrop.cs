// Hollowmere's horizon (T22 rebuild of QUEUE A12 / TOWN_VISUAL_REVIEW C3): real layered 3D distant terrain beyond the backdrop ring.
// Common practice for distant terrain at this scale (open-world games: low-poly heightfield shells with baked/macro-tiled albedo, ridged domain-warped noise, impostor canopy and crest
// fringes, aerial perspective by distance): picked as built-in heightfield meshes from ridged fbm, vertex-baked land cover, one tiling crown texture on the forest, instanced crest tree
// blobs and per-layer haze (SC Backdrop shader). Why: it is the proven recipe, costs one draw per 15-degree sector and needs no baked bitmap bigger than 2K.
//  1. Three heightfield layers around RingCentre: Near (500-1300 m, forested hills, crest trees), Mid (1150-2300 m, field and forest patchwork, valleys between spurs),
//     Far (2050-2990 m, the Mawrrow massif of rock, scree and pale ash). Each is a polar grid cut into 24 sectors (frustum culling), heights from domain-warped ridged noise under a
//     meandering crest band, lighter and bluer by distance (the shader grades by distance), valley floors darker (baked AO from height above the local floor).
//  2. Saltwake: a notch in all three layers at SaltwakeAzDeg (the road's direction from Eye_RoadOut) opens onto a flat marsh plain, an estuary with a pale water glint and a headland
//     with a lighthouse, all hazed. HollowmereRoad.RingHeight uses ApplyGap so the road and the ring ground fall away into the same plain (the bowl would hide the water otherwise).
//  3. Crest trees (12-tri blobs, deterministic) on the near and middle hills, the Mawrrow steam wisp, the ruined windmill on the near crest.
// Everything is a builder-made mesh, no colliders, nothing reachable. One tuning spot: HollowmereBackdropTuning. Deterministic (Mathf.PerlinNoise, System.Random with fixed seeds).
// Hooked by one line at the end of HollowmereRoad.Ring. Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereBackdropTuning
    {
        public static bool Terrain = true, Trees = true, Mountain = true, Wisp = true, Windmill = true, Saltwake = true;
        public static float MountainAzDeg = -118f, MountainDist = 2520f;   // azimuth = atan2(dz, dx) from RingCentre (north = 90, west = 180)
        public static float WindmillAzDeg = -62f, WindmillScale = 2.0f;
        public static float SaltwakeAzDeg = 186f, HeadlandOffsetDeg = 3f, HeadlandDist = 2640f, LighthouseScale = 2.4f;
        public static float WispAlpha = 0.07f;
    }

    public static class HollowmereBackdrop
    {
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string SpritePath = HollowmereRoad.Folder + "/Backdrop/T_Backdrop_Soft.png";
        const string CanopyPath = HollowmereRoad.Folder + "/Backdrop/T_Backdrop_Canopy.png";
        const float SeaY = -3f;
        const int Sectors = 24;

        struct Layer
        {
            public string Name; public float R0, Rc, R1, W, Amp, Lam, Meander, Tile, TreeH, TreeCell; public int Segs, Rings; public float Seed;
            public float HazeNear, HazeFar, D0, D1, Mist, Cool, CoolAmt, Patch; public Color HazeTint; public Vector3 Lift;
        }

        static readonly Layer[] Layers =
        {
            new Layer { Name = "NearHills", R0 = 520f, Rc = 900f, R1 = 1300f, W = 190f, Amp = 70f, Lam = 420f, Meander = 140f, Tile = 220f, TreeH = 8f, TreeCell = 8f, Segs = 1440, Rings = 90, Seed = 3f,
                HazeNear = 0.24f, HazeFar = 0.44f, D0 = 500f, D1 = 1300f, Mist = 0.10f, Cool = 0.95f, CoolAmt = 0.3f, Patch = 0f, HazeTint = new Color(0.98f, 1.0f, 1.02f), Lift = new Vector3(1f, 1f, 1f) },
            new Layer { Name = "MidHills", R0 = 1150f, Rc = 1700f, R1 = 2300f, W = 320f, Amp = 190f, Lam = 900f, Meander = 220f, Tile = 700f, TreeH = 20f, TreeCell = 20f, Segs = 1080, Rings = 80, Seed = 11f,
                HazeNear = 0.52f, HazeFar = 0.74f, D0 = 1200f, D1 = 2300f, Mist = 0.10f, Cool = 1.1f, CoolAmt = 0.6f, Patch = 0.25f, HazeTint = new Color(0.94f, 0.99f, 1.05f), Lift = new Vector3(0.95f, 1.04f, 1.18f) },
            new Layer { Name = "FarRange", R0 = 2050f, Rc = 2550f, R1 = 2990f, W = 300f, Amp = 240f, Lam = 1700f, Meander = 160f, Tile = 1400f, TreeH = 0f, TreeCell = 0f, Segs = 1080, Rings = 60, Seed = 23f,
                HazeNear = 0.74f, HazeFar = 0.88f, D0 = 1800f, D1 = 2990f, Mist = 0.05f, Cool = 1.05f, CoolAmt = 0.7f, Patch = 0.2f, HazeTint = new Color(0.91f, 0.98f, 1.08f), Lift = new Vector3(0.95f, 1.05f, 1.2f) },
        };

        // ------------------------------------------------------------------------------------------------ the height model
        static float PN(float x, float y) => Mathf.PerlinNoise(x + 1000f, y + 1000f);
        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }
        static float Rad(float deg) => deg * Mathf.Deg2Rad;
        static float Gauss(float a, float c, float s) { float d = Mathf.Atan2(Mathf.Sin(a - c), Mathf.Cos(a - c)) / s; return Mathf.Exp(-d * d); }

        // HollowmereRoad.RingHeight's hills for the far field (no terrain blend: every backdrop point is 370+ m outside the terrain).
        static float HillsBase(float x, float z)
        {
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            float r = Mathf.Sqrt((x - c.X) * (x - c.X) + (z - c.Z) * (z - c.Z));
            float hills = -16f + 0.055f * Mathf.Max(0f, r - 200f)
                + 18f * (Mathf.PerlinNoise(x * 0.0035f + 17.3f, z * 0.0035f + 4.1f) - 0.5f)
                + 6f * (Mathf.PerlinNoise(x * 0.011f + 3.7f, z * 0.011f + 29.9f) - 0.5f);
            return hills + HollowmereLayout.BackHill(x, z);
        }

        // Saltwake's notch: the road's direction. The floor is a flat marsh plain a little under the eye height of the low cameras, falling to the estuary beyond 1.7 km (sea at SeaY).
        static float GapAz => Rad(HollowmereBackdropTuning.SaltwakeAzDeg);
        // A low plain a little under the cameras' eye height out to 650 m, then rising gently (1:30), so every point farther out stands higher in the view than the one before (a flat estuary
        // at sea level would sit behind the tree belt, below the horizon, and never show). The "water" is painted on this slope (Albedo) and glinted by sprites.
        static float GapFloor(float r) => -2.5f + 0.035f * Mathf.Max(0f, r - 650f);
        public static float ApplyGap(float h, float x, float z)
        {
            if (!HollowmereBackdropTuning.Saltwake) return h;
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            float dx = x - c.X, dz = z - c.Z, r = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx);
            float gb = Smooth(0.10f, 0.85f, Gauss(a, GapAz, 0.20f)) * Smooth(250f, 650f, r);
            if (gb <= 0f) return h;
            float floor = GapFloor(r) + 1.4f * (PN(x * 0.012f + 4f, z * 0.012f + 8f) - 0.5f);
            return Mathf.Lerp(h, floor, gb);
        }

        struct S { public float H, Base, Rel, Massif, Gap; }

        static S Sample(int li, float x, float z)
        {
            Layer L = Layers[li];
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            float dx = x - c.X, dz = z - c.Z, r = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx), ca = dx / Mathf.Max(1f, r), sa = dz / Mathf.Max(1f, r), s = L.Seed;
            float g = HollowmereBackdropTuning.Saltwake ? Gauss(a, GapAz, 0.17f) : 0f;
            float baseH = ApplyGap(HillsBase(x, z), x, z);
            if (HollowmereBackdropTuning.Saltwake)   // the headland: a low spit of land with the lighthouse, inside the notch
            {
                float ha = GapAz + Rad(HollowmereBackdropTuning.HeadlandOffsetDeg), hr = HollowmereBackdropTuning.HeadlandDist;
                float lump = 0.7f + 0.6f * PN(x * 0.02f + 2f, z * 0.02f + 6f);
                baseH += Gauss(a, ha, 0.010f) * Mathf.Exp(-((r - hr) / 170f) * ((r - hr) / 170f)) * 45f * lump;
            }
            float rc = L.Rc + L.Meander * (PN(ca * 1.9f + s, sa * 1.9f + s * 0.4f) - 0.5f) * 2f;
            float u = (r - rc) / L.W;
            float env = Mathf.Max(0f, (Mathf.Exp(-u * u) - 0.04f) / 0.96f);
            float am = 0.45f + 0.8f * PN(ca * 1.4f + s * 2.1f, sa * 1.4f + s * 0.6f);
            float rg = Mathf.Clamp01(Ridged(x, z, L.Lam, s) * 1.5f);
            float amp = L.Amp * am * (1f - 0.93f * g);
            float h = baseH + amp * env * (0.30f + 0.70f * rg) + (PN(x / 55f + s, z / 55f) - 0.5f) * L.Amp * 0.07f * env * (1f - g);
            float massif = 0f;
            if (li == 2 && HollowmereBackdropTuning.Mountain)
            {
                float az0 = Rad(HollowmereBackdropTuning.MountainAzDeg);
                float jag = 0.9f + 0.2f * PN(ca * 40f + 7f, sa * 40f + 3f);
                float azk = Gauss(a, az0, 0.17f) * 100f + Gauss(a, az0 + 0.06f, 0.065f) * 250f * jag + Gauss(a, az0 - 0.12f, 0.065f) * 150f * jag;
                float rd = (r - HollowmereBackdropTuning.MountainDist) / 360f;
                massif = azk * Mathf.Exp(-rd * rd) * (0.82f + 0.3f * Ridged(x, z, 700f, 31f));
                h += massif;
            }
            h -= 3f * (1f - Smooth(L.R0, L.R0 + 160f, r));   // the foot tucks under the layer in front (or the ring ground)
            return new S { H = h, Base = baseH, Rel = (h - baseH) / Mathf.Max(1f, L.Amp), Massif = massif, Gap = g };
        }

        static float Ridged(float x, float z, float lam, float seed)
        {
            float wx = x + lam * 0.7f * (PN(x / (lam * 1.7f) + seed, z / (lam * 1.7f) + 3f) - 0.5f);
            float wz = z + lam * 0.7f * (PN(x / (lam * 1.7f) + seed + 9f, z / (lam * 1.7f) + 13f) - 0.5f);
            float f = 0f, amp = 1f, sum = 0f, fr = 1f / lam;
            for (int o = 0; o < 5; o++)
            {
                float n = 1f - Mathf.Abs(2f * PN(wx * fr + seed + o * 7.3f, wz * fr + seed * 0.7f + o * 3.1f) - 1f);
                f += amp * n * n; sum += amp; amp *= 0.5f; fr *= 2.07f;
            }
            return f / sum;
        }

        // 1 on the estuary: the notch from 1.8 km out, ragged shore (value noise on the threshold).
        static float WaterMask(float x, float z)
        {
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            float dx = x - c.X, dz = z - c.Z, r = Mathf.Sqrt(dx * dx + dz * dz), a = Mathf.Atan2(dz, dx);
            float side = Gauss(a, GapAz, 0.085f) + 0.35f * (PN(x * 0.006f + 3f, z * 0.006f + 5f) - 0.5f) + 0.12f * (PN(x * 0.02f, z * 0.02f) - 0.5f);
            return Smooth(0.55f, 0.65f, side) * Smooth(1800f, 1950f, r + 120f * (PN(x * 0.01f, z * 0.01f) - 0.5f));
        }

        static float Forest(int li, float x, float z, float rel, float nY)
        {
            float s = Layers[li].Seed;
            float p = PN(x * 0.0042f + s, z * 0.0042f + 1.7f) * 0.65f + PN(x * 0.012f + s * 2f, z * 0.012f + 9f) * 0.35f;
            float f = Smooth(0.36f, 0.50f, p);
            f = Mathf.Max(f, Smooth(0.5f, 0.85f, rel));
            f = Mathf.Max(f, 1f - Smooth(0.70f, 0.86f, nY));
            return f;
        }

        static Color Albedo(int li, float x, float z, S sm, float nY, float forest)
        {
            Layer L = Layers[li];
            float tone = PN(x / 80f + L.Seed, z / 80f + 4f);
            Color forestCol = Color.Lerp(new Color(0.07f, 0.13f, 0.05f), new Color(0.14f, 0.21f, 0.06f), tone) * (0.85f + 0.3f * PN(x / 22f, z / 22f));
            float ft = PN(x / 110f + 7f, z / 110f);
            Color fieldCol = Color.Lerp(new Color(0.27f, 0.36f, 0.13f), new Color(0.46f, 0.43f, 0.20f), Smooth(0.45f, 0.65f, ft)) * (0.9f + 0.2f * PN(x / 30f, z / 30f));
            Color col = Color.Lerp(fieldCol, forestCol, forest);
            if (li >= 1)
            {
                float rock = (1f - Smooth(0.60f, 0.80f, nY)) * Smooth(0.35f, 0.7f, sm.Rel) * (li == 2 ? 1f : 0.5f);
                float mk = sm.Massif / 360f;
                rock = Mathf.Max(rock, Smooth(0.10f, 0.45f, mk));
                Color rockCol = Color.Lerp(new Color(0.22f, 0.20f, 0.17f), new Color(0.33f, 0.30f, 0.26f), PN(x / 40f, z / 40f));   // scree lighter than the face
                col = Color.Lerp(col, rockCol, rock);
                col = Color.Lerp(col, new Color(0.30f, 0.28f, 0.26f), Smooth(0.78f, 0.97f, mk));   // pale ash on the summit
            }
            float water = HollowmereBackdropTuning.Saltwake ? WaterMask(x, z) : 0f;
            if (water > 0f) col = Color.Lerp(col, new Color(0.44f * (0.9f + 0.25f * PN(x * 0.004f, z * 0.03f)), 0.56f * (0.9f + 0.25f * PN(x * 0.004f, z * 0.03f)), 0.60f * (0.9f + 0.25f * PN(x * 0.004f, z * 0.03f)), 1f), water);
            float ao = Mathf.Lerp(0.60f, 1f, Smooth(0.10f, 0.65f, sm.Rel)); ao = Mathf.Lerp(ao, 1f, water);   // valley floors darker than the crests
            col *= ao;
            col.r *= L.Lift.x; col.g *= L.Lift.y; col.b *= L.Lift.z;
            col.a = 1f;
            return col;
        }

        // ------------------------------------------------------------------------------------------------ build
        static Vector3 mountainPeak;

        public static void Build(Transform ring)
        {
            Transform old = ring.Find("Landmarks");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Landmarks").transform; root.SetParent(ring, false);
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            int tris = 0;
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_BACKDROP_OFF")))   // capture "before" frames: the cameras only
            {
                AddHorizonCamera(root, "Cap_HM_HorizonSouth", -14f, -80f, -92f, 55f);
                AddHorizonCamera(root, "Cap_HM_HorizonWest", -125f, -40f, 183f, 55f);
                return;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Texture2D canopy = Canopy();
            if (HollowmereBackdropTuning.Terrain)
                for (int i = 0; i < Layers.Length; i++)
                {
                    Layer L = Layers[i];
                    Material m = Mat("M_BD_" + L.Name, Color.white, L.HazeNear, L.HazeFar, L.D0, L.D1, L.Mist, L.Cool, L.CoolAmt, patch: L.Patch, hazeTint: L.HazeTint, tex: i < 2 ? canopy : null, canopy: i < 2 ? 1f : 0f);
                    tris += BuildTerrain(root, i, m);
                    if (HollowmereBackdropTuning.Trees && L.TreeH > 0f)
                    {
                        Material tm = Mat("M_BD_" + L.Name + "Trees", Color.white, L.HazeNear * 0.75f, L.HazeFar * 0.8f, L.D0, L.D1, L.Mist * 0.5f, L.Cool, L.CoolAmt, hazeTint: L.HazeTint);
                        tris += BuildTrees(root, i, tm);
                    }
                }
            if (HollowmereBackdropTuning.Mountain) tris += BuildMountain(root, c);
            if (HollowmereBackdropTuning.Windmill) tris += BuildWindmill(root, c);
            if (HollowmereBackdropTuning.Saltwake) tris += BuildSaltwake(root, c);
            AddHorizonCamera(root, "Cap_HM_HorizonSouth", -14f, -80f, -92f, 55f);    // the horizon cameras: no play camera looks out over the wall, these show the backdrop
            AddHorizonCamera(root, "Cap_HM_HorizonWest", -125f, -40f, 183f, 55f);
            Debug.Log("SC HOLLOWMERE backdrop: " + tris + " tris in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s (terrain " + HollowmereBackdropTuning.Terrain + ", trees " + HollowmereBackdropTuning.Trees
                + ", mountain " + HollowmereBackdropTuning.Mountain + ", wisp " + HollowmereBackdropTuning.Wisp + ", windmill " + HollowmereBackdropTuning.Windmill + ", saltwake " + HollowmereBackdropTuning.Saltwake + ")");
        }

        static void AddHorizonCamera(Transform parent, string name, float x, float z, float azDeg, float fov)
        {
            Vector3 pos = new Vector3(x, HollowmereLayout.NaturalGround(x, z) + 7f, z);
            Vector3 dir = new Vector3(Mathf.Cos(Rad(azDeg)), 0f, Mathf.Sin(Rad(azDeg)));
            Camera cam = LookTestBuilder.AddCapture(parent, name, pos, pos + dir * 400f + Vector3.up * 22f, fov);
            cam.farClipPlane = HollowmereRoad.FarClip;
        }

        static int BuildTerrain(Transform parent, int li, Material m)
        {
            Layer L = Layers[li];
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            var rootT = new GameObject(L.Name).transform; rootT.SetParent(parent, false);
            int sp = L.Segs / Sectors, w = sp + 1, tris = 0;
            for (int s = 0; s < Sectors; s++)
            {
                var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color>(); var UV0 = new List<Vector2>(); var UV1 = new List<Vector2>(); var idx = new List<int>();
                for (int j = 0; j <= L.Rings; j++)
                {
                    float r = Mathf.Lerp(L.R0, L.R1, j / (float)L.Rings);
                    float eps = Mathf.Max(3f, r * 2f * Mathf.PI / L.Segs * 0.7f);
                    for (int i = 0; i <= sp; i++)
                    {
                        float a = (s * sp + i) * 2f * Mathf.PI / L.Segs;
                        float x = c.X + r * Mathf.Cos(a), z = c.Z + r * Mathf.Sin(a);
                        S sm = Sample(li, x, z);
                        float hx0 = Sample(li, x - eps, z).H, hx1 = Sample(li, x + eps, z).H, hz0 = Sample(li, x, z - eps).H, hz1 = Sample(li, x, z + eps).H;
                        Vector3 n = new Vector3(hx0 - hx1, 2f * eps, hz0 - hz1).normalized;
                        float forest = Forest(li, x, z, sm.Rel, n.y);
                        if (HollowmereBackdropTuning.Saltwake) forest *= 1f - WaterMask(x, z);
                        V.Add(new Vector3(x, sm.H, z)); N.Add(n); C.Add(Albedo(li, x, z, sm, n.y, forest));
                        UV0.Add(new Vector2(x / L.Tile, z / L.Tile)); UV1.Add(new Vector2(Mathf.Clamp01(forest * 0.85f + 0.15f), 0f));
                    }
                }
                for (int j = 0; j < L.Rings; j++)
                    for (int i = 0; i < sp; i++)
                    {
                        int a0 = j * w + i, b = a0 + 1, d = a0 + w, e = d + 1;
                        idx.AddRange(new[] { a0, e, d, a0, b, e });
                    }
                MakeMesh(rootT, L.Name + "_" + s.ToString("00"), V, N, C, UV0, UV1, idx, m);
                tris += idx.Count / 3;
            }
            return tris;
        }

        // ------------------------------------------------------------------------------------------------ crest trees
        static int BuildTrees(Transform parent, int li, Material m)
        {
            Layer L = Layers[li];
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            var rng = new System.Random(4001 + li * 17);
            var rootT = new GameObject(L.Name + "Trees").transform; rootT.SetParent(parent, false);
            var V = new List<Vector3>[Sectors]; var N = new List<Vector3>[Sectors]; var C = new List<Color>[Sectors]; var I = new List<int>[Sectors];
            for (int s = 0; s < Sectors; s++) { V[s] = new List<Vector3>(); N[s] = new List<Vector3>(); C[s] = new List<Color>(); I[s] = new List<int>(); }
            float cell = L.TreeCell;
            for (float r = L.R0 + 60f; r < L.R1 - 60f; r += cell)
            {
                int n = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * r / cell));
                for (int k = 0; k < n; k++)
                {
                    float a = (k + (float)rng.NextDouble()) * 2f * Mathf.PI / n, rr = r + ((float)rng.NextDouble() - 0.5f) * cell;
                    double roll = rng.NextDouble(), pick = rng.NextDouble(), hv = rng.NextDouble(), sv = rng.NextDouble();
                    float x = c.X + rr * Mathf.Cos(a), z = c.Z + rr * Mathf.Sin(a);
                    S sm = Sample(li, x, z);
                    if (sm.Gap > 0.45f || sm.H < SeaY + 4f) continue;
                    float e = 3f;
                    Vector3 nrm = new Vector3(Sample(li, x - e, z).H - Sample(li, x + e, z).H, 2f * e, Sample(li, x, z - e).H - Sample(li, x, z + e).H).normalized;
                    float forest = Forest(li, x, z, sm.Rel, nrm.y);
                    float crest = Smooth(0.30f, 0.80f, sm.Rel);
                    float p = forest * (0.25f + 0.75f * crest) * 0.9f * Smooth(0.30f, 0.62f, PN(x / 48f + 3f, z / 48f + 8f));   // groves, not a grid
                    if (nrm.y < 0.74f || roll > p) continue;
                    float size = L.TreeH * (0.55f + 0.95f * (float)hv * (float)hv + 0.2f * (float)hv);
                    bool conifer = pick < 0.15;
                    Vector3 pos = new Vector3(x, sm.H - 0.4f, z);
                    int sec = Mathf.Clamp(Mathf.FloorToInt(((a / (2f * Mathf.PI)) % 1f) * Sectors), 0, Sectors - 1);
                    AddTree(V[sec], N[sec], C[sec], I[sec], pos, size, conifer, (float)sv, PN(x / 70f, z / 70f), Layers[li].Lift);
                }
            }
            int tris = 0;
            for (int s = 0; s < Sectors; s++)
            {
                if (I[s].Count == 0) continue;
                MakeMesh(rootT, L.Name + "Trees_" + s.ToString("00"), V[s], N[s], C[s], null, null, I[s], m);
                tris += I[s].Count / 3;
            }
            return tris;
        }

        // A 12-triangle bipyramid crown (6 sides): conifer = tall and narrow, broadleaf = low and round. Normals bend from the crown centre so it shades like a lump.
        static void AddTree(List<Vector3> V, List<Vector3> N, List<Color> C, List<int> I, Vector3 p, float size, bool conifer, float rnd, float tone, Vector3 lift)
        {
            float ringY = conifer ? 0.24f : 0.50f, rad = size * (conifer ? 0.26f : 0.52f), baseY = conifer ? 0.0f : 0.12f, topY = conifer ? 1f : 0.86f;
            Color baseCol = conifer ? Color.Lerp(new Color(0.035f, 0.075f, 0.035f), new Color(0.06f, 0.11f, 0.045f), tone) : Color.Lerp(new Color(0.07f, 0.12f, 0.035f), new Color(0.13f, 0.18f, 0.05f), tone);
            baseCol *= (0.85f + 0.4f * rnd) * 0.6f;
            baseCol.r *= lift.x; baseCol.g *= lift.y; baseCol.b *= lift.z; baseCol.a = 1f;
            float rot = rnd * 6.28f;
            Vector3 centre = p + Vector3.up * size * (conifer ? 0.4f : 0.5f);
            int o = V.Count;
            Vector3 top = p + Vector3.up * size * topY, bot = p + Vector3.up * size * baseY;
            V.Add(top); V.Add(bot);
            for (int k = 0; k < 6; k++)
            {
                float a = rot + k * Mathf.PI / 3f;
                V.Add(p + new Vector3(Mathf.Cos(a) * rad, size * ringY, Mathf.Sin(a) * rad));
            }
            for (int k = 0; k < V.Count - o; k++) N.Add(((V[o + k] - centre).normalized * 0.5f + Vector3.up).normalized);
            C.Add(baseCol * 1.2f); C.Add(baseCol * 0.5f);
            for (int k = 0; k < 6; k++) C.Add(baseCol * (0.8f + 0.3f * Mathf.Sin(rot + k * 1.047f)));
            for (int k = 0; k < 6; k++)
            {
                int a0 = o + 2 + k, a1 = o + 2 + (k + 1) % 6;
                I.Add(o); I.Add(a1); I.Add(a0);          // upper cone (outward, counter-clockwise seen from outside)
                I.Add(o + 1); I.Add(a0); I.Add(a1);      // lower cone
            }
        }

        // ------------------------------------------------------------------------------------------------ the mountain
        static int BuildMountain(Transform parent, TownPoint c)
        {
            // the massif is part of the far layer (Sample li 2); only the steam wisp is placed here, on its summit
            float a = Rad(HollowmereBackdropTuning.MountainAzDeg) + 0.06f, r = HollowmereBackdropTuning.MountainDist;
            float x = c.X + r * Mathf.Cos(a), z = c.Z + r * Mathf.Sin(a);
            mountainPeak = new Vector3(x, Sample(2, x, z).H, z);
            if (HollowmereBackdropTuning.Wisp) BuildWisp(parent);
            return 0;
        }

        // The steam: a few huge soft warm sprites rising off the summit, very low alpha. Unlit, no fog (SleepingCalamity/BackdropWisp).
        static void BuildWisp(Transform parent)
        {
            var go = new GameObject("MawrrowSteam");
            go.transform.SetParent(parent, false);
            go.transform.position = mountainPeak + new Vector3(0f, 25f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.duration = 22f;
            main.startLifetime = 22f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(140f, 260f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.startColor = Color.white; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = 1.7f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(50f, 0f, 50f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(5f); vel.y = new ParticleSystem.MinMaxCurve(11f); vel.z = new ParticleSystem.MinMaxCurve(2f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(1f, 1.5f)));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.useAutoRandomSeed = false; ps.randomSeed = 4711u;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = WispMaterial("M_BD_Steam", new Color(1.0f, 0.90f, 0.78f, HollowmereBackdropTuning.WispAlpha), false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // ------------------------------------------------------------------------------------------------ windmill
        static int BuildWindmill(Transform parent, TownPoint c)
        {
            float a = Rad(HollowmereBackdropTuning.WindmillAzDeg), s = HollowmereBackdropTuning.WindmillScale;
            Layer L = Layers[0];
            float bestR = L.Rc, bestH = -1e9f;   // the highest point of the near crest on that bearing
            for (float rr = L.R0 + 150f; rr < L.R1 - 150f; rr += 4f) { float hh = Sample(0, c.X + rr * Mathf.Cos(a), c.Z + rr * Mathf.Sin(a)).H; if (hh > bestH) { bestH = hh; bestR = rr; } }
            Vector3 pos = new Vector3(c.X + bestR * Mathf.Cos(a), bestH - 1.5f, c.Z + bestR * Mathf.Sin(a));
            var mb = new MeshBuilder();
            mb.Frustum(new Vector3(0f, 0f, 0f), 4.6f, 3.0f, 17f, 8, 0f);                       // the tower
            mb.Tag = 1;                                                                        // A12 r1 (town-art-leftovers): cap, hub and sails are weathered timber, the tower whitewashed stone
            mb.Frustum(new Vector3(0f, 17f, 0f), 3.6f, 0.4f, 3.6f, 8, 0f);                     // the cap (cone), a little skewed by the ruin below
            mb.Box(new Vector3(0f, 18.2f, 3.0f), new Vector3(1.4f, 1.4f, 2.2f), Quaternion.identity);   // the hub housing
            float[] ang = { 18f, 108f, 198f, 288f };
            for (int k = 0; k < 4; k++)
            {
                Quaternion q = Quaternion.Euler(0f, 0f, ang[k]);
                bool broken = k == 2;
                float len = broken ? 5.5f : 15f;
                mb.Box(new Vector3(0f, 18.2f, 4.3f) + q * new Vector3(0f, len * 0.5f, 0f), new Vector3(0.7f, len, 0.5f), q);
                if (!broken) mb.Box(new Vector3(0f, 18.2f, 4.1f) + q * new Vector3(k == 3 ? 0.9f : 1.2f, 9.5f, 0f), new Vector3(k == 3 ? 1.4f : 2.4f, 8.5f, 0.25f), q);   // the lattice sail (one half gone)
            }
            Quaternion rot = Quaternion.Euler(0f, 90f - (a * Mathf.Rad2Deg + 180f), 0f);   // local +Z faces the town centre
            // A12 r1 (town-art-leftovers): it read as a near-black cut-out. Pale plaster tower, dark timber top, absolute vertex colours (white tint), no sun bake (the shader's key/sky fill lights it).
            var wv = mb.V.Select(p => p * s).ToList();
            var wc = new Color[wv.Count];
            Color plaster = new Color(0.55f, 0.52f, 0.47f), timber = new Color(0.18f, 0.15f, 0.13f);
            for (int i = 0; i < wc.Length; i++)
            {
                float foot = Mathf.Lerp(0.75f, 1f, Smooth(0f, 8f * s, wv[i].y));   // a little darker at the foot in the hill's shade
                Color b = mb.Tags[i] == 0 ? plaster * foot : timber;
                wc[i] = new Color(b.r, b.g, b.b, 1f);
            }
            var go = MakeObject(parent, "RuinedWindmill", wv, mb.I, Mat("M_BD_Windmill", Color.white, 0.25f, 0.38f, 500f, 1300f, 0.05f, 1.0f, 0.3f, 1.2f, 0.35f, 1.3f, 1f, 0f, 0f, 4f), null, null, wc);
            go.transform.position = pos;
            go.transform.rotation = rot;
            return mb.I.Count / 3;
        }

        // ------------------------------------------------------------------------------------------------ Saltwake: estuary, glint, headland lighthouse
        static int BuildSaltwake(Transform parent, TownPoint c)
        {
            int tris = 0;
            // the estuary is painted on the notch floor (Albedo/WaterMask); here only its glint and the lighthouse
            // the glint: sun on water, long thin additive bands facing the town
            Material gm = WispMaterial("M_BD_EstuaryGlint", new Color(1f, 0.95f, 0.80f, 0.32f), true);
            float[] gr = { 2050f, 2250f, 2450f, 2760f }, gaz = { -1.8f, 1.2f, -0.6f, 0.9f }, gw = { 300f, 420f, 360f, 520f };
            for (int k = 0; k < gr.Length; k++)
            {
                float a = GapAz + Rad(gaz[k]);
                float gx = c.X + gr[k] * Mathf.Cos(a), gz = c.Z + gr[k] * Mathf.Sin(a);
                Vector3 p = new Vector3(gx, Sample(2, gx, gz).H + 0.4f, gz);
                var q = new MeshBuilder(); float hw = gw[k] * 0.5f, hh = 5.5f + 2f * k;
                q.AddQuadFace(new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f), new Vector3(hw, hh * 2f, 0f), new Vector3(-hw, hh * 2f, 0f));
                var go = MakeObject(parent, "EstuaryGlint" + k, q.V, q.I, gm, q.UV);
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(0f, 90f - (a * Mathf.Rad2Deg + 180f), 0f);
                tris += 2;
            }

            // the lighthouse on the headland, in haze: a white tower and a keeper's house; the glint at the lantern
            float ha = GapAz + Rad(HollowmereBackdropTuning.HeadlandOffsetDeg), hr = HollowmereBackdropTuning.HeadlandDist;
            float lx = c.X + hr * Mathf.Cos(ha), lz = c.Z + hr * Mathf.Sin(ha);
            Vector3 pos = new Vector3(lx, Sample(2, lx, lz).H - 1.5f, lz);
            var mb = new MeshBuilder();
            mb.Frustum(new Vector3(0f, 0f, 0f), 4.6f, 2.8f, 34f, 8, 0f);
            mb.Frustum(new Vector3(0f, 34f, 0f), 3.6f, 3.6f, 1.0f, 8, 0f);                      // the gallery
            mb.Frustum(new Vector3(0f, 35f, 0f), 2.2f, 2.2f, 4.2f, 8, 0f);                      // the lantern room
            mb.Frustum(new Vector3(0f, 39.2f, 0f), 2.9f, 0.3f, 3.0f, 8, 0f);                    // the cap
            mb.Box(new Vector3(14f, 3f, 2f), new Vector3(14f, 6f, 8f), Quaternion.identity);     // the keeper's house beside it
            Quaternion face = Quaternion.Euler(0f, 90f - (ha * Mathf.Rad2Deg + 180f), 0f);
            var lh = MakeObject(parent, "SaltwakeLighthouse", mb.V, mb.I, Mat("M_BD_Lighthouse", new Color(0.74f, 0.72f, 0.66f), 0.50f, 0.70f, 1200f, 2990f, 0.04f, 1.1f, 0.6f));
            lh.transform.position = pos; lh.transform.rotation = face; lh.transform.localScale = Vector3.one * HollowmereBackdropTuning.LighthouseScale;
            tris += mb.I.Count / 3;
            var gq = new MeshBuilder(); gq.Quad(new Vector3(0f, 37f * HollowmereBackdropTuning.LighthouseScale, 1f), 70f);
            var gl = MakeObject(parent, "LighthouseGlint", gq.V, gq.I, WispMaterial("M_BD_Glint", new Color(1f, 0.93f, 0.72f, 0.6f), true), gq.UV);
            gl.transform.position = pos; gl.transform.rotation = face;
            return tris + 2;
        }

        // ------------------------------------------------------------------------------------------------ the forest crown texture
        // 2048 px, tiling: Worley crowns on a 64 x 64 torus (dome shading, dark gaps where crowns meet, light from the north-west), stored at 0.6 of its value (the shader multiplies by _CanopyGain 1.65).
        static Texture2D Canopy()
        {
            const int R = 2048, G = 64;
            var rng = new System.Random(77031);
            var fx = new float[G * G]; var fy = new float[G * G]; var ft = new float[G * G];
            for (int i = 0; i < G * G; i++) { fx[i] = 0.12f + 0.76f * (float)rng.NextDouble(); fy[i] = 0.12f + 0.76f * (float)rng.NextDouble(); ft[i] = 0.78f + 0.44f * (float)rng.NextDouble(); }
            var val = new float[R * R]; double sum = 0;
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float u = (i + 0.5f) * G / R, v = (j + 0.5f) * G / R;
                    int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                    float d1 = 9f, d2 = 9f, t1 = 1f, vx = 0f, vy = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy, wx = ((gx % G) + G) % G, wy = ((gy % G) + G) % G;
                            float px = gx + fx[wy * G + wx], py = gy + fy[wy * G + wx];
                            float dx = u - px, dy = v - py, d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < d1) { d2 = d1; d1 = d; t1 = ft[wy * G + wx]; vx = dx; vy = dy; }
                            else if (d < d2) d2 = d;
                        }
                    float dome = Mathf.Clamp01(1f - (d1 / 0.62f) * (d1 / 0.62f));
                    float edge = Smooth(0f, 0.22f, d2 - d1);
                    float light = 1f + 0.30f * Mathf.Clamp((-vx * 0.7f - vy * 0.7f) * 1.6f, -1f, 1f);
                    float grain = 0.92f + 0.16f * PN(i * 0.05f, j * 0.05f);
                    float vv = t1 * (0.72f + 0.36f * dome) * (0.72f + 0.28f * edge) * (1f + 0.5f * (light - 1f)) * grain;
                    val[j * R + i] = vv; sum += vv;
                }
            float mean = (float)(sum / (R * R));
            var px8 = new Color32[R * R];
            for (int i = 0; i < px8.Length; i++)
            {
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(val[i] / mean * 0.6f * 255f), 0, 255);
                px8[i] = new Color32(b, b, b, 255);
            }
            var tex = new Texture2D(R, R, TextureFormat.RGB24, false);
            tex.SetPixels32(px8); tex.Apply();
            BuilderUtil.EnsureFolder(HollowmereRoad.Folder + "/Backdrop");
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(CanopyPath);
            if (!File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png)) { File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(CanopyPath, ImportAssetOptions.ForceUpdate); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CanopyPath);
        }

        static void MakeMesh(Transform parent, string name, List<Vector3> v, List<Vector3> n, List<Color> col, List<Vector2> uv0, List<Vector2> uv1, List<int> idx, Material m)
        {
            var mesh = new Mesh { name = "BD_" + name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(col);
            if (uv0 != null) mesh.SetUVs(0, uv0);
            if (uv1 != null) mesh.SetUVs(1, uv1);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // ------------------------------------------------------------------------------------------------ helpers
        sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<int> I = new List<int>(); public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> Tags = new List<int>(); public int Tag;   // Tag = part id stamped on every vertex added after it is set (windmill: 0 tower, 1 timber)

            // Separate vertices per face (flat shading); winding counter-clockwise seen from outside.
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int o = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                Tags.Add(Tag); Tags.Add(Tag); Tags.Add(Tag); Tags.Add(Tag);
                UV.Add(new Vector2(0, 0)); UV.Add(new Vector2(1, 0)); UV.Add(new Vector2(1, 1)); UV.Add(new Vector2(0, 1));
                I.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }

            public void Frustum(Vector3 baseCentre, float r0, float r1, float h, int sides, float yawDeg)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (i / (float)sides * 360f + yawDeg) * Mathf.Deg2Rad, a1 = ((i + 1) / (float)sides * 360f + yawDeg) * Mathf.Deg2Rad;
                    Vector3 b0 = baseCentre + new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0), b1 = baseCentre + new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                    Vector3 t0 = baseCentre + new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1), t1 = baseCentre + new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                    Face(b1, b0, t0, t1);
                }
                // top cap (fan) so a flat-topped gallery is not open
                if (r1 > 0.5f)
                    for (int i = 0; i < sides; i++)
                    {
                        float a0 = (i / (float)sides * 360f + yawDeg) * Mathf.Deg2Rad, a1 = ((i + 1) / (float)sides * 360f + yawDeg) * Mathf.Deg2Rad;
                        Vector3 top = baseCentre + Vector3.up * h;
                        Face(top, top + new Vector3(Mathf.Cos(a1) * r1, 0f, Mathf.Sin(a1) * r1), top + new Vector3(Mathf.Cos(a0) * r1, 0f, Mathf.Sin(a0) * r1), top);
                    }
            }

            public void Box(Vector3 centre, Vector3 size, Quaternion rot)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) => centre + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Face(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));       // +Z
                Face(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));   // -Z
                Face(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));       // +X
                Face(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));   // -X
                Face(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1));       // +Y
                Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));   // -Y
            }

            public void AddQuadFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => Face(a, b, c, d);

            public void Quad(Vector3 centre, float size)
            {
                float h = size * 0.5f;
                Face(centre + new Vector3(-h, -h, 0f), centre + new Vector3(h, -h, 0f), centre + new Vector3(h, h, 0f), centre + new Vector3(-h, h, 0f));
            }
        }

        static GameObject MakeObject(Transform parent, string name, List<Vector3> v, List<int> idx, Material m, List<Vector2> uv = null, System.Func<Vector3, Vector3, Color> colour = null, Color[] vcols = null)
        {
            var mesh = new Mesh { name = "BD_" + name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (colour != null)   // vertex colours (the shader multiplies rgb into the tint, alpha adds haze): colour(position, normal)
            {
                var nrm = mesh.normals; var cols = new Color[v.Count];
                for (int i = 0; i < cols.Length; i++) cols[i] = colour(v[i], nrm[i]);
                mesh.SetColors(cols);
            }
            else if (vcols != null) mesh.SetColors(vcols);   // per-vertex colours baked by the caller (the windmill's parts)
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        // A SleepingCalamity/Backdrop material (same property set as HollowmereRoad.Backdrop; no texture, a flat tint).
        // A12 r1 (town-art-leftovers): the trailing key/fill args default to the shader's old behaviour, so ridges, cards and road are unchanged; always written so a regenerated .mat is never stale.
        static Material Mat(string name, Color tint, float hazeNear, float hazeFar, float d0, float d1, float mist, float cool, float coolAmt,
            float keyWrap = 0.7f, float keyBias = 0.15f, float keyBoost = 1f, float skyFillDir = 0f, float hazeTop = 0f, float hazeTopHeight = 0f, float fillBoost = 1f, float patch = 0f, Color? hazeTint = null, Texture2D tex = null, float canopy = 0f)
        {
            Shader sh = Shader.Find("SleepingCalamity/Backdrop") ?? Shader.Find("Universal Render Pipeline/Lit");
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetTexture("_BaseMap", tex != null ? tex : Texture2D.whiteTexture);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Canopy", canopy); m.SetFloat("_CanopyGain", 1.65f);
            m.SetFloat("_HazeNear", hazeNear); m.SetFloat("_HazeFar", hazeFar); m.SetFloat("_DistNear", d0); m.SetFloat("_DistFar", d1);
            m.SetColor("_HazeTint", hazeTint ?? new Color(0.98f, 1.0f, 1.03f)); m.SetFloat("_Patch", patch);
            m.SetFloat("_GroundMist", mist); m.SetFloat("_MistTop", -8f); m.SetFloat("_MistScale", 40f);
            m.SetFloat("_NoiseAmp", 0.04f); m.SetFloat("_NoiseLit", 0.03f);
            m.SetColor("_ShadowTint", new Color(0.30f, 0.38f, 0.48f));
            m.SetColor("_HazeCool", new Color(0.62f * cool, 0.8f, 1.2f * cool)); m.SetFloat("_HazeCoolAmt", coolAmt);
            m.SetFloat("_KeyWrap", keyWrap); m.SetFloat("_KeyBias", keyBias); m.SetFloat("_KeyBoost", keyBoost); m.SetFloat("_SkyFillDir", skyFillDir);
            m.SetFloat("_FillBoost", fillBoost); m.SetFloat("_HazeTop", hazeTop); m.SetFloat("_HazeTopHeight", hazeTopHeight);
            m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", (float)CullMode.Back);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material WispMaterial(string name, Color tint, bool additive)
        {
            Shader sh = Shader.Find("SleepingCalamity/BackdropWisp") ?? Shader.Find("Sprites/Default");
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetTexture("_BaseMap", SoftSprite());
            m.SetColor("_Tint", tint);
            m.SetFloat("_DstBlend", additive ? 1f : 10f);
            m.renderQueue = 3000;
            EditorUtility.SetDirty(m);
            return m;
        }

        // A 128 px radial falloff sprite (red = alpha), with a little noise so the plume is not a perfect disc. PNG asset, deterministic bytes.
        static Texture2D SoftSprite()
        {
            const int R = 128;
            var px = new Color32[R * R];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float x = (i + 0.5f) / R * 2f - 1f, y = (j + 0.5f) / R * 2f - 1f;
                    float d = Mathf.Sqrt(x * x + y * y);
                    float f = Mathf.Clamp01(1f - d); f = f * f * (3f - 2f * f);
                    f *= 0.75f + 0.5f * (Mathf.PerlinNoise(i * 0.07f + 3f, j * 0.07f + 8f) - 0.25f);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);
                    px[j * R + i] = new Color32(b, b, b, 255);
                }
            var tex = new Texture2D(R, R, TextureFormat.RGB24, false);
            tex.SetPixels32(px); tex.Apply();
            BuilderUtil.EnsureFolder(HollowmereRoad.Folder + "/Backdrop");
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(SpritePath);
            if (!File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png)) { File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath);
        }
    }
}
