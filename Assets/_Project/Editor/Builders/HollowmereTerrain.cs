// The Hollowmere ground as a Unity Terrain (terrain pass, 2026-10-31). Heights come from the layout (HollowmereLayout.Ground: the square's shelf, the
// keep terrace, the lane terraces, every lot pad, the wall bank and ditch, the lake basin, the stream ravine), written with TerrainData.SetHeights, so
// every pad and doorstep is exact. The open hillsides and lake banks may carry erosion from the official Terrain Tools package (com.unity.terrain-tools
// 5.3.3: Perlin noise, then its hydraulic and thermal eroders, fixed seed): Bake() runs those on the GPU in a graphics run and writes the result to a
// 16-bit RAW in the repo (RawPath); Build() blends that RAW in only where ProtectMask is 0, so pads, lanes, walls, the shelf, the bank and the ditch keep
// their designed heights. The build itself needs no GPU and runs the same with or without the RAW (without it the ground is the designed heights alone).
//   Bake:   bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereTerrain.Bake --out <scratch>
// The Terrain Tools eroders and noise are internal to that package, so Bake reaches them by reflection, pinned to 5.3.3 (Packages/manifest.json).
// Layers: grass and dirt (flat greybox tints) and the B1 cobble tileable under the lanes and the square, painted from the layout and the slope.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereTerrain
    {
        public const int Res = 513, SplatRes = 512;
        public static readonly Vector3 Origin = new Vector3(-150f, -24f, -170f);
        public static readonly Vector3 Size = new Vector3(300f, 44f, 300f);
        public const string Folder = "Assets/_Project/Content/Terrain/Hollowmere";
        public const string DataPath = Folder + "/Hollowmere_TerrainData.asset";
        public const string RawPath = Folder + "/Hollowmere_Eroded_513.r16";   // 16-bit little-endian, Res x Res, row 0 = south edge, column 0 = west edge
        const int Seed = 1031;
        const float NoiseMetres = 1.4f;   // peak-to-peak height of the Terrain Tools noise on the open ground before erosion

        static float WorldX(int i) => Origin.x + i * Size.x / (Res - 1);
        static float WorldZ(int j) => Origin.z + j * Size.z / (Res - 1);

        // ------------------------------------------------------------------------------------------------ heights
        // Normalised designed heights, [row = z, column = x] as TerrainData.SetHeights takes them.
        public static float[,] Designed(TownLayout t)
        {
            var h = new float[Res, Res];
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                    h[j, i] = Mathf.Clamp01((HollowmereLayout.Ground(t, WorldX(i), WorldZ(j)) - Origin.y) / Size.y);
            return h;
        }

        // 1 where the designed height must hold (the shelf and the keep, every lot and landmark with its yard, every lane corridor, the wall bank and the
        // ditch), 0 on the open hillsides, the lake banks and the ravine, with a soft 2-3 m edge.
        public static float ProtectMask(TownLayout t, float x, float z)
        {
            var p = new TownPoint(x, z);
            float e = HollowmereLayout.PlateauDistance(x, z), m = 0f;
            if (z < HollowmereLayout.KeepWallZ + 14f) m = Mathf.Max(m, 1f - Smooth(4.5f, 7f, e));          // the shelf, its terrace walls, the keep
            m = Mathf.Max(m, 1f - Smooth(1.5f, 3.5f, Mathf.Abs(e - 29.5f) - 4.5f));                           // the bank (26-29.5) and the ditch (31)
            foreach (Foundation f in t.Foundations) m = Mathf.Max(m, 1f - Smooth(1.0f, 3.5f, Outside(f.Footprint, p)));
            foreach (Landmark l in t.Landmarks) m = Mathf.Max(m, 1f - Smooth(1.0f, 3.5f, Outside(l.Footprint, p)));
            foreach (Lane lane in t.Lanes) m = Mathf.Max(m, 1f - Smooth(0.75f, 3.5f, HollowmereLayout.DistanceToPolyline(p, lane.Points) - lane.Width * 0.5f));
            return m;
        }

        static float Outside(Footprint fp, TownPoint p)
        {
            TownPoint l = fp.ToLocal(p);
            float ox = Mathf.Max(0f, Mathf.Abs(l.X) - fp.HalfX), oz = Mathf.Max(0f, Mathf.Abs(l.Z) - fp.HalfZ);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }

        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        // Designed heights with the baked erosion blended in where the mask allows (no RAW: the designed heights).
        public static float[,] Final(TownLayout t, out bool eroded)
        {
            float[,] h = Designed(t);
            float[,] raw = ReadRaw();
            eroded = raw != null;
            if (raw == null) return h;
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                {
                    float m = ProtectMask(t, WorldX(i), WorldZ(j));
                    h[j, i] = Mathf.Lerp(raw[j, i], h[j, i], m);
                }
            return h;
        }

        static float[,] ReadRaw()
        {
            string abs = BuilderUtil.Abs(RawPath);
            if (!File.Exists(abs)) return null;
            byte[] b = File.ReadAllBytes(abs);
            if (b.Length != Res * Res * 2) { Debug.LogWarning("SC HOLLOWMERE terrain RAW has the wrong size, ignored: " + RawPath); return null; }
            var h = new float[Res, Res];
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                {
                    int k = (j * Res + i) * 2;
                    h[j, i] = (b[k] | (b[k + 1] << 8)) / 65535f;
                }
            return h;
        }

        // ------------------------------------------------------------------------------------------------ the terrain object
        public static Terrain Build(Transform parent, TownLayout t)
        {
            BuilderUtil.EnsureFolder(Folder);
            TerrainLayer[] layers = { Layer("Grass", Flat("hm_grass", new Color32(108, 124, 66, 255)), null, 4f), Layer("Dirt", Flat("hm_dirt", new Color32(112, 92, 70, 255)), null, 4f), CobbleLayer() };
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null) { data = new TerrainData(); AssetDatabase.CreateAsset(data, DataPath); }
            data.heightmapResolution = Res;
            data.size = Size;
            data.alphamapResolution = SplatRes;
            data.baseMapResolution = 512;
            data.terrainLayers = layers;
            float[,] h = Final(t, out bool eroded);
            data.SetHeights(0, 0, h);
            data.SetAlphamaps(0, 0, Splat(t, data));
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("SC HOLLOWMERE terrain res=" + Res + " eroded=" + (eroded ? "yes (" + RawPath + ")" : "no (designed heights; run HollowmereTerrain.Bake to add Terrain Tools erosion)"));

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Origin + new Vector3(0f, -0.02f, 0f);   // the corner's surround sat 2 cm down so the B6 setts read above it
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 220f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return terrain;
        }

        static Material TerrainMaterial()
        {
            const string path = "Assets/_Project/Content/Materials/Hollowmere/M_HM_Terrain.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit") ?? throw new InvalidOperationException("URP Terrain/Lit shader not found");
            Material m = LookTestBuilder.Ensure(path, shader);
            EditorUtility.SetDirty(m);
            return m;
        }

        // Splat weights per texel: cobble on the square, the keep top and the lanes; dirt on bare lots, steep banks, the ditch and the lake shore; grass elsewhere.
        static float[,,] Splat(TownLayout t, TerrainData data)
        {
            var a = new float[SplatRes, SplatRes, 3];
            for (int j = 0; j < SplatRes; j++)
                for (int i = 0; i < SplatRes; i++)
                {
                    float u = (i + 0.5f) / SplatRes, v = (j + 0.5f) / SplatRes;
                    float x = Origin.x + u * Size.x, z = Origin.z + v * Size.z;
                    float grass = 1f, dirt = 0f, cobble = 0f;
                    if (IsPaved(t, x, z)) { cobble = 1f; grass = 0f; }
                    else
                    {
                        float steep = data.GetSteepness(u, v), y = data.GetInterpolatedHeight(u, v) + Origin.y;
                        dirt = Mathf.Max(Smooth(24f, 38f, steep), 1f - Smooth(HollowmereLayout.WaterY + 0.3f, HollowmereLayout.WaterY + 1.8f, y));
                        var p = new TownPoint(x, z);
                        foreach (Foundation f in t.Foundations) if (f.Footprint.Contains(p, 0.5f)) { dirt = Mathf.Max(dirt, 0.55f); break; }
                        dirt = Mathf.Clamp01(dirt + 0.18f * (Mathf.PerlinNoise(x * 0.11f + 13f, z * 0.11f + 7f) - 0.5f));
                        grass = 1f - dirt;
                    }
                    a[j, i, 0] = grass; a[j, i, 1] = dirt; a[j, i, 2] = cobble;
                }
            return a;
        }

        static bool IsPaved(TownLayout t, float x, float z)
        {
            if (x >= HollowmereLayout.SquareMinX && x <= HollowmereLayout.SquareMaxX && z >= HollowmereLayout.SquareMinZ && z <= HollowmereLayout.SquareMaxZ) return true;
            if (z > HollowmereLayout.KeepWallZ + 0.6f && z < HollowmereLayout.KeepWallZ + 12f && Mathf.Abs(x) < HollowmereLayout.KeepHalfWidth - 1f) return true;
            var p = new TownPoint(x, z);
            foreach (Lane lane in t.Lanes) if (HollowmereLayout.DistanceToPolyline(p, lane.Points) < lane.Width * 0.5f) return true;
            return false;
        }

        static TerrainLayer Layer(string name, Texture2D albedo, Texture2D normal, float tile)
        {
            string path = Folder + "/HM_" + name + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
            layer.diffuseTexture = albedo; layer.normalMapTexture = normal; layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0.05f; layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static TerrainLayer CobbleLayer()
        {
            const string dir = "Assets/_Project/Content/Textures/B1_town/cobble/";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "cobble_albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "cobble_normal.png");
            return Layer("Cobble", albedo != null ? albedo : Flat("hm_cobble", new Color32(128, 120, 110, 255)), normal, 2f);
        }

        // A small flat-tint albedo for the greybox grass and dirt (no B1 grass or soil tileable exists yet), with a little value noise so it is not dead flat.
        static Texture2D Flat(string name, Color32 c)
        {
            string path = Folder + "/" + name + "_albedo.png";
            if (!File.Exists(BuilderUtil.Abs(path)))
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                var rng = new System.Random(Seed);
                for (int k = 0; k < px.Length; k++)
                {
                    float f = 0.92f + 0.16f * (float)rng.NextDouble();
                    px[k] = new Color32((byte)Mathf.Min(255, c.r * f), (byte)Mathf.Min(255, c.g * f), (byte)Mathf.Min(255, c.b * f), 255);
                }
                tex.SetPixels32(px); File.WriteAllBytes(BuilderUtil.Abs(path), tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------------------------------------ the erosion bake (graphics run)
        public static void Bake()
        {
            TownLayout t = HollowmereLayout.Create();
            float[,] designed = Designed(t);
            Assembly tt = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.TerrainTools.Editor")
                ?? throw new InvalidOperationException("Terrain Tools (com.unity.terrain-tools) is not loaded");
            if (!SystemInfo.supportsComputeShaders) throw new InvalidOperationException("Terrain Tools erosion needs compute shaders; this run has none (use --graphics)");

            var desc = new RenderTextureDescriptor(Res, Res, RenderTextureFormat.RFloat, 0) { enableRandomWrite = true, sRGB = false };
            var heightRT = new RenderTexture(desc); heightRT.Create();
            var noiseRT = new RenderTexture(desc); noiseRT.Create();
            var outRT = new RenderTexture(desc); outRT.Create();
            try
            {
                // 1. Terrain Tools noise (NoiseSettings defaults: fractal Perlin), moved by the seed and scaled to about 40 m features.
                Type nsType = tt.GetType("UnityEditor.TerrainTools.NoiseSettings", true);
                var ns = ScriptableObject.CreateInstance(nsType);
                FieldInfo trf = nsType.GetField("transformSettings");
                object tr = trf.GetValue(ns);
                tr.GetType().GetField("translation").SetValue(tr, new Vector3(Seed * 0.37f, 0f, Seed * 0.61f));
                tr.GetType().GetField("scale").SetValue(tr, new Vector3(7.5f, 7.5f, 7.5f));
                trf.SetValue(ns, tr);
                tt.GetType("UnityEditor.TerrainTools.NoiseUtils", true).GetMethod("Blit2D", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { nsType, typeof(RenderTexture) }, null)
                    .Invoke(null, new object[] { ns, noiseRT });
                float[,] noise = Read(noiseRT);
                float mean = 0f; foreach (float f in noise) mean += f; mean /= noise.Length;

                // 2. Designed heights plus the noise on the open ground only.
                var start = new float[Res, Res];
                for (int j = 0; j < Res; j++)
                    for (int i = 0; i < Res; i++)
                    {
                        float m = ProtectMask(t, WorldX(i), WorldZ(j));
                        start[j, i] = designed[j, i] + (1f - m) * (noise[j, i] - mean) * NoiseMetres / Size.y;
                    }
                Write(heightRT, start);

                // 3. Terrain Tools hydraulic erosion (its thermal pass runs inside it), default settings.
                Type erType = tt.GetType("UnityEditor.TerrainTools.Erosion.HydraulicEroder", true);
                object eroder = Activator.CreateInstance(erType, true);
                var inputs = (System.Collections.Generic.Dictionary<string, RenderTexture>)erType.GetProperty("inputTextures").GetValue(eroder);
                inputs["Height"] = heightRT;
                Vector2 texel = new Vector2(Size.x / (Res - 1), Size.z / (Res - 1));
                erType.GetMethod("ErodeHeightmap").Invoke(eroder, new object[] { outRT, Size, new Rect(0, 0, Res, Res), texel, false });
                float[,] eroded = Read(outRT);

                // 4. Keep the designed heights where they must hold, write the RAW.
                var bytes = new byte[Res * Res * 2];
                float maxDelta = 0f;
                for (int j = 0; j < Res; j++)
                    for (int i = 0; i < Res; i++)
                    {
                        float m = ProtectMask(t, WorldX(i), WorldZ(j));
                        float v = Mathf.Clamp01(Mathf.Lerp(eroded[j, i], designed[j, i], m));
                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(v - designed[j, i]) * Size.y);
                        ushort q = (ushort)Mathf.RoundToInt(v * 65535f);
                        int k = (j * Res + i) * 2; bytes[k] = (byte)(q & 0xff); bytes[k + 1] = (byte)(q >> 8);
                    }
                BuilderUtil.EnsureFolder(Folder);
                File.WriteAllBytes(BuilderUtil.Abs(RawPath), bytes);
                AssetDatabase.ImportAsset(RawPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("SC HOLLOWMERE terrain bake wrote " + RawPath + " max change vs designed=" + maxDelta.ToString("0.00") + " m");
            }
            finally
            {
                heightRT.Release(); noiseRT.Release(); outRT.Release();
            }
        }

        static float[,] Read(RenderTexture rt)
        {
            RenderTexture prev = RenderTexture.active;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, false, true);
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Color[] px = tex.GetPixels();
            UnityEngine.Object.DestroyImmediate(tex);
            var h = new float[rt.height, rt.width];
            for (int j = 0; j < rt.height; j++) for (int i = 0; i < rt.width; i++) h[j, i] = px[j * rt.width + i].r;
            return h;
        }

        static void Write(RenderTexture rt, float[,] h)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RFloat, false, true);
            var px = new Color[Res * Res];
            for (int j = 0; j < Res; j++) for (int i = 0; i < Res; i++) px[j * Res + i] = new Color(h[j, i], 0f, 0f, 1f);
            tex.SetPixels(px); tex.Apply();
            Graphics.Blit(tex, rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
