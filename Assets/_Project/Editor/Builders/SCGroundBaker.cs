// Authority: none (editor build step). Ground-blend step (2026-11-22): a CPU top-down bake of the town's ground, so the ruin shader (SC_WearForwardPass, _SC_RUINSKIN) can melt stone
// into the real ground under it (height for the foot band, albedo/smoothness for the colour, a finite-difference normal for the crease). Neon's SCGroundBaker, adapted: no GPU (a
// Camera.Render is blank under -nographics), no .asset textures; raw .bytes written with File.WriteAllBytes (height R16, albedo RGBA32 with smoothness in A), read back by
// SleepingCalamity.Presentation.SCGroundBlendBinder. Deterministic: row-major loops, no randomness, bytes written only when they changed.
//   Heights: the Terrain's TerrainData (CPU interpolation), raised by any "street" collider the caller names (Collider.Raycast, so only those colliders count, never a wall or a prop).
//   Albedo: the Terrain's alphamap mix of its layers (diffuse x remap, the bright end of the remap is the tint the builder sets), the street collider's material otherwise.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class SCGroundBaker
    {
        public const int DefaultResolution = 512;
        const float FlatSmoothness = 0.15f;
        static readonly Color FallbackGround = new Color(0.25f, 0.25f, 0.22f, FlatSmoothness);

        public struct Result
        {
            public int Resolution;
            public Vector4 GroundBounds;       // (minX, minZ, 1/sizeX, 1/sizeZ)
            public Vector4 GroundHeightParams; // (minY, rangeY, 1/res, 1/res)
            public byte[] HeightBytes;         // R16, little endian, row 0 = south (min z), column 0 = west (min x)
            public byte[] AlbedoBytes;         // RGBA32, same layout; sRGB rgb, linear smoothness in a
            public int StreetTexels;           // texels whose height came from a street collider
        }

        /// <summary>Bakes the ground over [min, min + size] in world XZ into res x res texels (texel centres sampled). No files, no scene changes except a Physics sync.</summary>
        public static Result Compute(Terrain terrain, IList<Collider> streets, Vector2 min, Vector2 size, int res)
        {
            if (res < 2) throw new ArgumentOutOfRangeException(nameof(res));
            Physics.SyncTransforms();
            float sizeX = Mathf.Max(1e-3f, size.x), sizeZ = Mathf.Max(1e-3f, size.y);
            TerrainData td = terrain != null ? terrain.terrainData : null;
            Vector3 tPos = terrain != null ? terrain.transform.position : Vector3.zero;
            Vector3 tSize = td != null ? td.size : Vector3.one;
            float[,,] alpha = null; int aw = 0, ah = 0;
            TerrainLayer[] layers = Array.Empty<TerrainLayer>();
            if (td != null && td.terrainLayers != null && td.terrainLayers.Length > 0)
            {
                aw = td.alphamapWidth; ah = td.alphamapHeight;
                alpha = td.GetAlphamaps(0, 0, aw, ah);
                layers = td.terrainLayers;
            }
            var cpu = new Dictionary<Texture2D, Texture2D>(); var temp = new List<Texture2D>(); var meanCache = new Dictionary<Texture2D, Color>();
            Texture2D Readable(Texture2D src)
            {
                if (src == null) return null;
                if (cpu.TryGetValue(src, out Texture2D c)) return c;
                if (src.isReadable) return cpu[src] = src;
                string path = AssetDatabase.GetAssetPath(src);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    {
                        var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);   // values as stored (gamma); blended in gamma, like the sRGB result is stored
                        if (tmp.LoadImage(File.ReadAllBytes(path), false)) { temp.Add(tmp); return cpu[src] = tmp; }
                        UnityEngine.Object.DestroyImmediate(tmp);
                    }
                    else Debug.LogWarning("SC GROUNDBAKE texture '" + path + "' is not readable and not PNG/JPG: it falls back to a flat tint");
                }
                return cpu[src] = null;
            }

            int count = res * res;
            var heights = new float[count]; var albedo = new Color32[count];
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            float rayTop = (terrain != null ? tPos.y + tSize.y : 100f) + 50f;
            int streetTexels = 0;
            try
            {
                for (int y = 0; y < res; y++)
                {
                    float wz = min.y + (y + 0.5f) / res * sizeZ;
                    for (int x = 0; x < res; x++)
                    {
                        float wx = min.x + (x + 0.5f) / res * sizeX;
                        int idx = y * res + x;
                        float h = td != null ? tPos.y + td.GetInterpolatedHeight(Mathf.Clamp01((wx - tPos.x) / tSize.x), Mathf.Clamp01((wz - tPos.z) / tSize.z)) : 0f;
                        Color c = td != null && alpha != null ? SampleTerrain(wx, wz, tPos, tSize, alpha, aw, ah, layers, Readable) : FallbackGround;
                        if (streets != null)
                        {
                            var ray = new Ray(new Vector3(wx, rayTop, wz), Vector3.down);
                            for (int s = 0; s < streets.Count; s++)
                            {
                                Collider col = streets[s];
                                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                                if (col.Raycast(ray, out RaycastHit hit, rayTop + 200f) && hit.point.y >= h - 0.05f)
                                {
                                    h = hit.point.y; c = SampleStreet(col, hit, Readable, meanCache); streetTexels++;
                                }
                            }
                        }
                        heights[idx] = h;
                        if (h < minY) minY = h;
                        if (h > maxY) maxY = h;
                        albedo[idx] = new Color32(To8(c.r), To8(c.g), To8(c.b), To8(c.a));
                    }
                }
            }
            finally { foreach (Texture2D t in temp) UnityEngine.Object.DestroyImmediate(t); }

            if (float.IsInfinity(minY)) { minY = 0f; maxY = 1f; }
            float rangeY = Mathf.Max(0.01f, maxY - minY);
            var hb = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01((heights[i] - minY) / rangeY) * 65535f), 0, 65535);
                hb[i * 2] = (byte)(v & 0xFF); hb[i * 2 + 1] = (byte)(v >> 8);   // explicit little endian, no BlockCopy: the file must not depend on the host
            }
            var ab = new byte[count * 4];
            for (int i = 0; i < count; i++) { ab[i * 4] = albedo[i].r; ab[i * 4 + 1] = albedo[i].g; ab[i * 4 + 2] = albedo[i].b; ab[i * 4 + 3] = albedo[i].a; }
            return new Result
            {
                Resolution = res,
                GroundBounds = new Vector4(min.x, min.y, 1f / sizeX, 1f / sizeZ),
                GroundHeightParams = new Vector4(minY, rangeY, 1f / res, 1f / res),
                HeightBytes = hb, AlbedoBytes = ab, StreetTexels = streetTexels,
            };
        }

        /// <summary>Writes the two byte files (only when they changed), imports them as TextAssets and returns them.</summary>
        public static void Write(Result r, string heightPath, string albedoPath, out TextAsset height, out TextAsset albedo)
        {
            WriteIfChanged(heightPath, r.HeightBytes); WriteIfChanged(albedoPath, r.AlbedoBytes);
            AssetDatabase.ImportAsset(heightPath, ImportAssetOptions.ForceSynchronousImport); AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceSynchronousImport);
            height = AssetDatabase.LoadAssetAtPath<TextAsset>(heightPath); albedo = AssetDatabase.LoadAssetAtPath<TextAsset>(albedoPath);
            if (height == null || albedo == null) throw new InvalidOperationException("Ground bake files did not import as TextAssets: " + heightPath + ", " + albedoPath);
        }

        static void WriteIfChanged(string assetPath, byte[] bytes)
        {
            string abs = BuilderUtil.Abs(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            if (File.Exists(abs))
            {
                byte[] old = File.ReadAllBytes(abs);
                bool same = old.Length == bytes.Length;
                for (int i = 0; same && i < old.Length; i++) same = old[i] == bytes[i];
                if (same) return;
            }
            File.WriteAllBytes(abs, bytes);
        }

        static byte To8(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(v) * 255f), 0, 255);

        static Color SampleTerrain(float wx, float wz, Vector3 tPos, Vector3 tSize, float[,,] alpha, int aw, int ah, TerrainLayer[] layers, Func<Texture2D, Texture2D> readable)
        {
            float tx = Mathf.Clamp01((wx - tPos.x) / Mathf.Max(1e-3f, tSize.x)), tz = Mathf.Clamp01((wz - tPos.z) / Mathf.Max(1e-3f, tSize.z));
            int ax = Mathf.Clamp(Mathf.FloorToInt(tx * (aw - 1) + 0.5f), 0, aw - 1), az = Mathf.Clamp(Mathf.FloorToInt(tz * (ah - 1) + 0.5f), 0, ah - 1);
            Color acc = Color.clear; float wsum = 0f;
            int n = Mathf.Min(layers.Length, alpha.GetLength(2));
            for (int l = 0; l < n; l++)
            {
                float w = alpha[az, ax, l];   // alphamaps are [z, x, layer]
                if (w <= 0.001f || layers[l] == null) continue;
                TerrainLayer layer = layers[l];
                Color lc = new Color(0.25f, 0.28f, 0.18f, layer.smoothness);
                Texture2D tex = readable(layer.diffuseTexture);
                if (tex != null)
                {
                    float u = (wx - tPos.x + layer.tileOffset.x) / Mathf.Max(1e-3f, layer.tileSize.x), v = (wz - tPos.z + layer.tileOffset.y) / Mathf.Max(1e-3f, layer.tileSize.y);
                    Color s = tex.GetPixelBilinear(u - Mathf.Floor(u), v - Mathf.Floor(v));
                    // the layer's diffuse remap (the builder sets it to tame the meadow green and the path cream) is part of what the terrain shows
                    Vector4 lo = layer.diffuseRemapMin, hi = layer.diffuseRemapMax;
                    lc = new Color(Mathf.Lerp(lo.x, hi.x, s.r), Mathf.Lerp(lo.y, hi.y, s.g), Mathf.Lerp(lo.z, hi.z, s.b), layer.smoothness);
                }
                acc += lc * w; wsum += w;
            }
            return wsum > 1e-4f ? acc / wsum : FallbackGround;
        }

        // A street collider has no texture coordinate (a BoxCollider): its material's mean colour times the tint, smoothness clamped like the shader does.
        static Color SampleStreet(Collider col, RaycastHit hit, Func<Texture2D, Texture2D> readable, Dictionary<Texture2D, Color> means)
        {
            Renderer rend = col.GetComponent<Renderer>(); Material mat = rend != null ? rend.sharedMaterial : null;
            if (mat == null) return new Color(0.32f, 0.30f, 0.27f, 0.20f);
            Color tint = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            float smooth = mat.HasProperty("_Smoothness") ? Mathf.Min(mat.GetFloat("_Smoothness"), 0.28f) : 0.20f;
            Texture2D baseMap = mat.HasProperty("_BaseMap") ? readable(mat.GetTexture("_BaseMap") as Texture2D) : null;
            if (baseMap == null) return new Color(tint.r, tint.g, tint.b, smooth);
            if (col is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.isReadable && hit.textureCoord != Vector2.zero)
            {
                Vector2 uv = Vector2.Scale(hit.textureCoord, mat.GetTextureScale("_BaseMap")) + mat.GetTextureOffset("_BaseMap");
                Color s = baseMap.GetPixelBilinear(uv.x - Mathf.Floor(uv.x), uv.y - Mathf.Floor(uv.y));
                return new Color(s.r * tint.r, s.g * tint.g, s.b * tint.b, smooth);
            }
            if (!means.TryGetValue(baseMap, out Color mean))
            {
                Color sum = Color.black; const int N = 16;
                for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) sum += baseMap.GetPixelBilinear((i + 0.5f) / N, (j + 0.5f) / N);
                means[baseMap] = mean = sum / (N * N);
            }
            return new Color(mean.r * tint.r, mean.g * tint.g, mean.b * tint.b, smooth);
        }
    }
}
