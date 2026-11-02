// Hollowmere's horizon (QUEUE A12 / TOWN_VISUAL_REVIEW C3): real silhouette meshes beyond the backdrop ring, built on top of HollowmereRoad's ring, mid hills and far ridge.
//  1. Three ragged ridge layers (r 760 / 1750 / 2450 m from RingCentre): polar strips whose crest is low-frequency hills + ragged noise + tree-clump bumps, each layer
//     lighter and bluer (SleepingCalamity/Backdrop shader: its own haze by distance, no scene fog), so the valley wall reads as depth, not one card band.
//  2. ONE dark mountain massif on the horizon (the place Mawrrow sleeps: a long slumped shape, two summits and a saddle) with a faint warm steam wisp (particles, low alpha).
//  3. A ruined windmill on the nearest ridge (dark, in haze), 4. a far lighthouse with a faint glint on the middle ridge to the west (toward Saltwake).
// Everything is a builder-made mesh, no colliders, nothing reachable (all of it stands 700+ m from the playable terrain). One tuning spot with an on/off per piece:
// HollowmereBackdropTuning. Deterministic (Mathf.PerlinNoise, fixed seeds). Hooked by one line at the end of HollowmereRoad.Ring.
// Authority: none (editor-time scene building).
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
        public static bool Ridges = true, Mountain = true, Wisp = true, Windmill = true, Lighthouse = true;
        public static float MountainAzDeg = -118f, MountainDist = 2600f, MountainHeight = 560f;   // azimuth = atan2(dz, dx) from RingCentre (north = 90, west = 180); the far ridges hide its lower 7 degrees
        public static float WindmillAzDeg = -62f, LighthouseAzDeg = 186f, WindmillScale = 2.0f, LighthouseScale = 1.8f;
        public static float WispAlpha = 0.07f;
    }

    public static class HollowmereBackdrop
    {
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string SpritePath = HollowmereRoad.Folder + "/Backdrop/T_Backdrop_Soft.png";

        struct Layer
        {
            public string Name; public float R0, Rc, R1, Foot, Base, Big, Rag, Clump; public int Segs; public float Seed;
            public Color Tint; public float HazeNear, HazeFar, D0, D1, Mist, Cool, CoolAmt;
        }

        static readonly Layer[] Layers =
        {
            new Layer { Name = "TreeRidge", R0 = 700f, Rc = 760f, R1 = 840f, Foot = -8f, Base = 24f, Big = 14f, Rag = 6f, Clump = 11f, Segs = 1440, Seed = 3f,
                Tint = new Color(0.12f, 0.18f, 0.09f), HazeNear = 0.62f, HazeFar = 0.74f, D0 = 500f, D1 = 1300f, Mist = 0.10f, Cool = 0.95f, CoolAmt = 0.3f },
            new Layer { Name = "HillRidge", R0 = 1400f, Rc = 1750f, R1 = 2100f, Foot = -20f, Base = 80f, Big = 50f, Rag = 14f, Clump = 14f, Segs = 1440, Seed = 11f,
                Tint = new Color(0.17f, 0.24f, 0.19f), HazeNear = 0.72f, HazeFar = 0.82f, D0 = 1200f, D1 = 2200f, Mist = 0.10f, Cool = 1.1f, CoolAmt = 0.6f },
            new Layer { Name = "FarRange", R0 = 2150f, Rc = 2450f, R1 = 2750f, Foot = -20f, Base = 200f, Big = 110f, Rag = 22f, Clump = 14f, Segs = 1440, Seed = 23f,
                Tint = new Color(0.22f, 0.28f, 0.31f), HazeNear = 0.84f, HazeFar = 0.90f, D0 = 1800f, D1 = 2900f, Mist = 0.05f, Cool = 1.4f, CoolAmt = 1.0f },
        };

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
            if (HollowmereBackdropTuning.Ridges) for (int i = 0; i < Layers.Length; i++) tris += BuildLayer(root, c, i);
            if (HollowmereBackdropTuning.Mountain) tris += BuildMountain(root, c);
            if (HollowmereBackdropTuning.Windmill) tris += BuildWindmill(root, c);
            if (HollowmereBackdropTuning.Lighthouse) tris += BuildLighthouse(root, c);
            AddHorizonCamera(root, "Cap_HM_HorizonSouth", -14f, -80f, -92f, 55f);    // the horizon cameras: no play camera looks out over the wall, these show the backdrop
            AddHorizonCamera(root, "Cap_HM_HorizonWest", -125f, -40f, 183f, 55f);
            Debug.Log("SC HOLLOWMERE backdrop: " + tris + " tris (ridges " + HollowmereBackdropTuning.Ridges + ", mountain " + HollowmereBackdropTuning.Mountain + ", wisp "
                + HollowmereBackdropTuning.Wisp + ", windmill " + HollowmereBackdropTuning.Windmill + ", lighthouse " + HollowmereBackdropTuning.Lighthouse + ")");
        }

        static void AddHorizonCamera(Transform parent, string name, float x, float z, float azDeg, float fov)
        {
            Vector3 pos = new Vector3(x, HollowmereLayout.NaturalGround(x, z) + 7f, z);
            Vector3 dir = new Vector3(Mathf.Cos(Rad(azDeg)), 0f, Mathf.Sin(Rad(azDeg)));
            Camera cam = LookTestBuilder.AddCapture(parent, name, pos, pos + dir * 400f + Vector3.up * 22f, fov);
            cam.farClipPlane = HollowmereRoad.FarClip;
        }

        // ------------------------------------------------------------------------------------------------ ridge layers
        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }
        static float Rad(float deg) => deg * Mathf.Deg2Rad;

        // Knolls (a landmark's seat on a ridge): layer index, azimuth, sigma (rad), height.
        static float Knoll(int layer, float a)
        {
            float k = 0f;
            if (layer == 0 && HollowmereBackdropTuning.Windmill) k += Gauss(a, Rad(HollowmereBackdropTuning.WindmillAzDeg), 0.03f) * 18f;
            if (layer == 1 && HollowmereBackdropTuning.Lighthouse) k += Gauss(a, Rad(HollowmereBackdropTuning.LighthouseAzDeg), 0.022f) * 20f;
            return k;
        }
        static float Gauss(float a, float c, float s) { float d = Mathf.Atan2(Mathf.Sin(a - c), Mathf.Cos(a - c)) / s; return Mathf.Exp(-d * d); }

        static float Top(int li, float a)
        {
            Layer L = Layers[li];
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a), s = L.Seed;
            float big = L.Big * Mathf.PerlinNoise(ca * 1.7f + s, sa * 1.7f + s * 0.6f);
            float mid = L.Big * 0.35f * (Mathf.PerlinNoise(ca * 6f + s * 2f, sa * 6f + 5f) - 0.5f);
            float rag = L.Rag * (Mathf.PerlinNoise(ca * 38f + s, sa * 38f + 1.3f) - 0.5f);
            // tree-clump bumps: round-topped lumps where a mid-frequency noise passes a threshold, with fine jitter on top (the tree tips)
            float cl = Smooth(0.52f, 0.72f, Mathf.PerlinNoise(ca * 75f + s * 3f, sa * 75f + 9f));
            float tips = (Mathf.PerlinNoise(ca * 230f + s, sa * 230f + 4f) - 0.5f) * 0.45f * L.Clump * cl;
            return L.Base + big + mid + rag + L.Clump * cl + tips + Knoll(li, a);
        }

        static int BuildLayer(Transform parent, TownPoint c, int li)
        {
            Layer L = Layers[li];
            var v = new List<Vector3>(); var idx = new List<int>();
            float[] rs = { L.R0, L.Rc, L.R1 };
            for (int j = 0; j < 3; j++)
                for (int i = 0; i <= L.Segs; i++)
                {
                    float a = i * 2f * Mathf.PI / L.Segs;
                    float top = Top(li, a);
                    float y = j == 0 ? L.Foot : j == 1 ? top : Mathf.Lerp(top, L.Foot, 0.55f);
                    v.Add(new Vector3(c.X + rs[j] * Mathf.Cos(a), y, c.Z + rs[j] * Mathf.Sin(a)));
                }
            int w = L.Segs + 1;
            for (int j = 0; j < 2; j++)
                for (int i = 0; i < L.Segs; i++)
                {
                    int a = j * w + i, b = a + 1, d = a + w, e = d + 1;
                    idx.AddRange(new[] { a, e, d, a, b, e });
                }
            Material m = Mat("M_BD_" + L.Name, L.Tint, L.HazeNear, L.HazeFar, L.D0, L.D1, L.Mist, L.Cool, L.CoolAmt);
            MakeObject(parent, L.Name, v, idx, m);
            return idx.Count / 3;
        }

        // ------------------------------------------------------------------------------------------------ the mountain
        static Vector3 mountainPeak;

        static int BuildMountain(Transform parent, TownPoint c)
        {
            float az = Rad(HollowmereBackdropTuning.MountainAzDeg), H = HollowmereBackdropTuning.MountainHeight;
            float cx = c.X + HollowmereBackdropTuning.MountainDist * Mathf.Cos(az), cz = c.Z + HollowmereBackdropTuning.MountainDist * Mathf.Sin(az);
            // local axes: radial (away from the town) and tangential (along the horizon)
            Vector2 rad = new Vector2(Mathf.Cos(az), Mathf.Sin(az)), tan = new Vector2(-rad.y, rad.x);
            const int N = 64; const float Half = 700f;
            float[] hs = new float[(N + 1) * (N + 1)];
            var v = new List<Vector3>();
            float best = 0f;
            for (int j = 0; j <= N; j++)
                for (int i = 0; i <= N; i++)
                {
                    float lt = (i / (float)N * 2f - 1f) * Half, lr = (j / (float)N * 2f - 1f) * Half;   // tangential, radial offsets from the centre
                    // two summits along the horizon (the long slumped body): a tall one and a lower shoulder, an elliptical footprint (wide along the horizon)
                    float h = H * Bump(lt, lr, 150f, 0f, 430f, 260f) + 0.58f * H * Bump(lt, lr, -300f, 40f, 360f, 240f);
                    float x = cx + tan.x * lt + rad.x * lr, z = cz + tan.y * lt + rad.y * lr;
                    float ridged = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.004f + 2f, z * 0.004f + 8f) * 2f - 1f);
                    h *= 0.72f + 0.45f * ridged + 0.06f * (Mathf.PerlinNoise(x * 0.02f, z * 0.02f) - 0.5f);
                    hs[j * (N + 1) + i] = h;
                    if (h > best) { best = h; mountainPeak = new Vector3(x, h - 30f, z); }
                    v.Add(new Vector3(x, h - 30f, z));
                }
            var idx = new List<int>();
            for (int j = 0; j < N; j++)
                for (int i = 0; i < N; i++)
                {
                    int a = j * (N + 1) + i, b = a + 1, d = a + N + 1, e = d + 1;
                    if (hs[a] <= 0f && hs[b] <= 0f && hs[d] <= 0f && hs[e] <= 0f) continue;
                    idx.AddRange(new[] { a, d, e, a, e, b });
                }
            Material m = Mat("M_BD_Mawrrow", new Color(0.07f, 0.08f, 0.11f), 0.50f, 0.64f, 2000f, 3000f, 0.04f, 1.3f, 0.8f);
            MakeObject(parent, "MawrrowMountain", v, idx, m);
            int tris = idx.Count / 3;
            if (HollowmereBackdropTuning.Wisp) BuildWisp(parent);
            return tris;
        }

        static float Bump(float t, float r, float ct, float cr, float rt, float rr)
        {
            float f = 1f - Mathf.Sqrt(((t - ct) / rt) * ((t - ct) / rt) + ((r - cr) / rr) * ((r - cr) / rr));
            return f <= 0f ? 0f : 0.4f * f + 0.6f * f * f * (3f - 2f * f);
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
            Vector3 pos = new Vector3(c.X + L.Rc * Mathf.Cos(a), Top(0, a) - 1.5f, c.Z + L.Rc * Mathf.Sin(a));
            var mb = new MeshBuilder();
            mb.Frustum(new Vector3(0f, 0f, 0f), 4.6f, 3.0f, 17f, 8, 0f);                       // the tower
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
            var go = MakeObject(parent, "RuinedWindmill", mb.V.Select(p => p * s).ToList(), mb.I, Mat("M_BD_Windmill", new Color(0.06f, 0.065f, 0.07f), 0.55f, 0.66f, 500f, 1300f, 0.05f, 1.0f, 0.3f));
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 90f - (a * Mathf.Rad2Deg + 180f), 0f);   // local +Z faces the town centre
            return mb.I.Count / 3;
        }

        // ------------------------------------------------------------------------------------------------ lighthouse
        static int BuildLighthouse(Transform parent, TownPoint c)
        {
            float a = Rad(HollowmereBackdropTuning.LighthouseAzDeg);
            Layer L = Layers[1];
            Vector3 pos = new Vector3(c.X + L.Rc * Mathf.Cos(a), Top(1, a) - 2f, c.Z + L.Rc * Mathf.Sin(a));
            var mb = new MeshBuilder();
            mb.Frustum(new Vector3(0f, 0f, 0f), 4.2f, 2.6f, 34f, 8, 0f);
            mb.Frustum(new Vector3(0f, 34f, 0f), 3.4f, 3.4f, 1.0f, 8, 0f);                      // the gallery
            mb.Frustum(new Vector3(0f, 35f, 0f), 2.2f, 2.2f, 4.2f, 8, 0f);                      // the lantern room
            mb.Frustum(new Vector3(0f, 39.2f, 0f), 2.9f, 0.3f, 3.0f, 8, 0f);                    // the cap
            var go = MakeObject(parent, "FarLighthouse", mb.V, mb.I, Mat("M_BD_Lighthouse", new Color(0.62f, 0.60f, 0.57f), 0.72f, 0.82f, 1200f, 2200f, 0.08f, 1.1f, 0.6f));
            go.transform.position = pos; go.transform.localScale = Vector3.one * HollowmereBackdropTuning.LighthouseScale;
            var tris = mb.I.Count / 3;
            // the glint: one additive sprite at the lantern, facing the town
            var q = new MeshBuilder(); q.Quad(new Vector3(0f, 37f * HollowmereBackdropTuning.LighthouseScale, 1f), 60f);
            var gl = MakeObject(parent, "LighthouseGlint", q.V, q.I, WispMaterial("M_BD_Glint", new Color(1f, 0.93f, 0.72f, 0.6f), true), q.UV);
            gl.transform.position = pos;
            gl.transform.rotation = Quaternion.Euler(0f, 90f - (a * Mathf.Rad2Deg + 180f), 0f);
            go.transform.rotation = gl.transform.rotation;
            return tris + 2;
        }

        // ------------------------------------------------------------------------------------------------ helpers
        sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>(); public readonly List<int> I = new List<int>(); public readonly List<Vector2> UV = new List<Vector2>();

            // Separate vertices per face (flat shading); winding counter-clockwise seen from outside.
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int o = V.Count; V.Add(a); V.Add(b); V.Add(c); V.Add(d);
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

            public void Quad(Vector3 centre, float size)
            {
                float h = size * 0.5f;
                Face(centre + new Vector3(-h, -h, 0f), centre + new Vector3(h, -h, 0f), centre + new Vector3(h, h, 0f), centre + new Vector3(-h, h, 0f));
            }
        }

        static GameObject MakeObject(Transform parent, string name, List<Vector3> v, List<int> idx, Material m, List<Vector2> uv = null)
        {
            var mesh = new Mesh { name = "BD_" + name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v);
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        // A SleepingCalamity/Backdrop material (same property set as HollowmereRoad.Backdrop; no texture, a flat tint).
        static Material Mat(string name, Color tint, float hazeNear, float hazeFar, float d0, float d1, float mist, float cool, float coolAmt)
        {
            Shader sh = Shader.Find("SleepingCalamity/Backdrop") ?? Shader.Find("Universal Render Pipeline/Lit");
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/" + name + ".mat", sh);
            m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_HazeNear", hazeNear); m.SetFloat("_HazeFar", hazeFar); m.SetFloat("_DistNear", d0); m.SetFloat("_DistFar", d1);
            m.SetColor("_HazeTint", new Color(0.98f, 1.0f, 1.03f));
            m.SetFloat("_GroundMist", mist); m.SetFloat("_MistTop", -8f); m.SetFloat("_MistScale", 40f);
            m.SetFloat("_NoiseAmp", 0.04f); m.SetFloat("_NoiseLit", 0.03f);
            m.SetColor("_ShadowTint", new Color(0.30f, 0.38f, 0.48f));
            m.SetColor("_HazeCool", new Color(0.62f * cool, 0.8f, 1.2f * cool)); m.SetFloat("_HazeCoolAmt", coolAmt);
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
