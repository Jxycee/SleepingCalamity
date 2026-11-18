// B0 lighting-corner intake helpers (editor time, authority: none). Everything here is deterministic: no random seeds, files are rewritten only when the bytes differ.
//  - Megascans-style 2K sets from Art/Source/Fab/<set>/ (BaseColor, Normal, Roughness, AO jpg) become Assets/_Project/Content/Textures/B0Fab/<prefix>/<prefix>_{albedo,normal,ms,occ}
//    (the suffixes LookTestTextureImporter already imports correctly: sRGB albedo, normal map, linear ms = R metallic 0 / A smoothness = 1 - roughness, linear occlusion; 2048 max).
//  - The K01 sky HDRIs: import settings, the sun's position found in the pixels (so the skybox can be turned to the preset's sun), the horizon colour for the fog.
//  - The cloud-shadow cookies for the sun (tiling value noise on an integer lattice, so the tile wraps exactly).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class B0Fab
    {
        const string SrcRoot = "Art/Source/Fab/";
        public const string TexRoot = "Assets/_Project/Content/Textures/B0Fab/";
        public const string SkyFolder = "Assets/_Project/Content/Art/Town/B0/K01";
        public const string MaterialsFolder = "Assets/_Project/Content/Materials/B0";

        public sealed class TexSet { public Texture2D Albedo, Normal, Ms, Occ; }

        // ------------------------------------------------------------------------------------------------ Megascans sets
        // Null when the source folder or one of its maps is missing (the caller then keeps its old material).
        public static TexSet Megascans(string srcName, string prefix)
        {
            string srcDir = BuilderUtil.Abs(SrcRoot + srcName);
            if (!Directory.Exists(srcDir)) return null;
            string Find(string tail) => Directory.GetFiles(srcDir, "*_" + tail + ".jpg").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
            string bc = Find("BaseColor"), nr = Find("Normal"), ro = Find("Roughness"), ao = Find("AO");
            if (bc == null || nr == null || ro == null) return null;
            string dir = TexRoot + prefix; BuilderUtil.EnsureFolder(dir);
            string a = dir + "/" + prefix + "_albedo.jpg", n = dir + "/" + prefix + "_normal.jpg", ms = dir + "/" + prefix + "_ms.png", occ = dir + "/" + prefix + "_occ.png";
            var written = new List<string>();
            if (CopyIfDifferent(bc, BuilderUtil.Abs(a))) written.Add(a);
            if (CopyIfDifferent(nr, BuilderUtil.Abs(n))) written.Add(n);
            Color32[] rough = Decode(ro, out int w, out int h);
            var msPx = new Color32[rough.Length];
            for (int i = 0; i < rough.Length; i++) msPx[i] = new Color32(0, 0, 0, (byte)(255 - rough[i].r));
            if (BeautyCornerArt.WritePng(BuilderUtil.Abs(ms), w, h, msPx)) written.Add(ms);
            var occPx = new Color32[rough.Length];
            Color32[] aoPx = ao != null ? Decode(ao, out int aw, out int ah) : null;
            if (aoPx != null && aoPx.Length != occPx.Length) aoPx = null;
            for (int i = 0; i < occPx.Length; i++) { byte v = aoPx != null ? aoPx[i].r : (byte)255; occPx[i] = new Color32(v, v, v, 255); }
            if (BeautyCornerArt.WritePng(BuilderUtil.Abs(occ), w, h, occPx)) written.Add(occ);
            foreach (string p in written) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            if (written.Count > 0) Debug.Log("SC B0 intake " + srcName + ": wrote " + written.Count + " files under " + dir);
            return new TexSet
            {
                Albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(a), Normal = AssetDatabase.LoadAssetAtPath<Texture2D>(n),
                Ms = AssetDatabase.LoadAssetAtPath<Texture2D>(ms), Occ = AssetDatabase.LoadAssetAtPath<Texture2D>(occ),
            };
        }

        // Puts the set on a URP Lit / WearLit material (the ms/occ convention of BeautyCornerArt.BuildMaterial). tiles = repeats per UV unit.
        public static void Apply(Material m, TexSet s, float tiles)
        {
            m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            m.SetTexture("_BaseMap", s.Albedo);
            m.SetTexture("_BumpMap", s.Normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", s.Ms); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_OcclusionMap", s.Occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            foreach (string prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) { m.SetTextureScale(prop, Vector2.one * tiles); m.SetTextureOffset(prop, Vector2.zero); }
            EditorUtility.SetDirty(m);
        }

        // T01 square cobbles (Quixel scan, 2 x 2 m): a Lit material, 1 repeat per 2 m (the corner's ground boxes take UV = metres / 2). Null while the set is absent.
        public static Material CobbleGround()
        {
            TexSet s = Megascans("T01_cobblestone_floor", "T01_cobble");
            if (s == null) return null;
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_T01_Cobble.mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found"));
            Apply(m, s, 1f);
            // round 3: the cobbles read flat grey-pink under the hazy sky ambient: stronger relief and AO, a warmer albedo
            m.SetFloat("_BumpScale", 1.6f); m.SetFloat("_OcclusionStrength", 1f);
            Color warm = new Color(1.0f, 0.90f, 0.76f, 1f); m.SetColor("_BaseColor", warm); m.SetColor("_Color", warm);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // [C15] Wall_Sconce (CC-BY 4.0, Get Dead Entertainment; credit it on the credits screen): FBX plus 4K PBR pngs. The albedo and normal keep their 4K files and import at 2048;
        // metallic + roughness are packed into one 2K ms map (R metallic, A smoothness). Null while the source folder is absent.
        public const string SconceFolder = "Assets/_Project/Content/Art/Cellar/B0/C15_wall_sconce";
        public static GameObject Sconce(out Material mat)
        {
            mat = null;
            string src = BuilderUtil.Abs(SrcRoot + "C15_wall_sconce/");
            if (!Directory.Exists(src)) return null;
            BuilderUtil.EnsureFolder(SconceFolder); BuilderUtil.EnsureFolder(MaterialsFolder);
            string fbx = SconceFolder + "/Wall_Sconce.fbx", bc = SconceFolder + "/C15_BaseColor.png", nr = SconceFolder + "/C15_Normal.png";
            string msDir = TexRoot + "C15_sconce"; BuilderUtil.EnsureFolder(msDir); string ms = msDir + "/C15_sconce_ms.png";
            var written = new List<string>();
            if (CopyIfDifferent(src + "source/Wall_Sconce_FBX.fbx", BuilderUtil.Abs(fbx))) written.Add(fbx);
            if (CopyIfDifferent(src + "textures/Wall_Sconce_BaseColor_4k.png", BuilderUtil.Abs(bc))) written.Add(bc);
            if (CopyIfDifferent(src + "textures/Wall_Sconce_Normal_4k.png", BuilderUtil.Abs(nr))) written.Add(nr);
            Color32[] metal = Decode(src + "textures/Wall_Sconce_Metallic_4k.png", out int w, out int h), rough = Decode(src + "textures/Wall_Sconce_Roughness_4k.png", out int rw, out int rh);
            if (rw != w || rh != h) throw new InvalidDataException("C15 metallic and roughness maps differ in size");
            int ow = w / 2, oh = h / 2; var packed = new Color32[ow * oh];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    int m = 0, r = 0;
                    for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { int i = (2 * y + dy) * w + 2 * x + dx; m += metal[i].r; r += rough[i].r; }
                    packed[y * ow + x] = new Color32((byte)((m + 2) / 4), 0, 0, (byte)(255 - (r + 2) / 4));
                }
            if (BeautyCornerArt.WritePng(BuilderUtil.Abs(ms), ow, oh, packed)) written.Add(ms);
            foreach (string p in written) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            BeautyCornerArt.ImportTexture(bc, srgb: true, normal: false, alphaTransparency: false);
            BeautyCornerArt.ImportTexture(nr, srgb: false, normal: true, alphaTransparency: false);
            GameObject model = BeautyCornerArt.LoadModel(SconceFolder, "Wall_Sconce.fbx");
            Material m2 = LookTestBuilder.Ensure(MaterialsFolder + "/M_C15_Sconce.mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found"));
            m2.SetColor("_BaseColor", new Color(0.50f, 0.46f, 0.42f)); m2.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bc));
            m2.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nr)); m2.SetFloat("_BumpScale", 1f); m2.EnableKeyword("_NORMALMAP");
            m2.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m2.SetFloat("_Metallic", 0.8f); m2.SetFloat("_Smoothness", 0.55f); m2.EnableKeyword("_METALLICSPECGLOSSMAP");   // round 3: dark iron, not a blown-out mirror plate
            EditorUtility.SetDirty(m2); AssetDatabase.SaveAssetIfDirty(m2);
            mat = m2;
            return model;
        }

        internal static bool CopyIfDifferent(string src, string dst)
        {
            byte[] b = File.ReadAllBytes(src);
            if (File.Exists(dst) && File.ReadAllBytes(dst).SequenceEqual(b)) return false;
            File.WriteAllBytes(dst, b); return true;
        }

        static Color32[] Decode(string file, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(t, File.ReadAllBytes(file), false)) throw new InvalidDataException("Cannot read " + file);
            w = t.width; h = t.height; Color32[] px = t.GetPixels32(); UnityEngine.Object.DestroyImmediate(t);
            return px;
        }

        // ------------------------------------------------------------------------------------------------ sky HDRIs
        public sealed class SkyInfo { public float SunU, SunV, SunElevation, SunLuminance; public Color HorizonLinear; }
        static readonly Dictionary<string, SkyInfo> skyCache = new Dictionary<string, SkyInfo>();

        // 2K lat-long HDR: cubemap-free sky for Skybox/Panoramic. Texture2D, linear, mipmaps, 2048 max, repeat in U and clamp in V (BC6H).
        public static Texture2D ImportSky(string fileName)
        {
            string path = SkyFolder + "/" + fileName;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return null;
            // T26: a sky opts into 4K by its file name without extension in B1TownIntake.FourK / SC_INTAKE_4K (default 2K; the source must really be 4K).
            bool k4 = B1TownIntake.IsFourK(Path.GetFileNameWithoutExtension(fileName)); int max = k4 ? 4096 : 2048;
            if (k4) Debug.Log("SC INTAKE 4K " + Path.GetFileNameWithoutExtension(fileName));
            bool dirty = ti.textureShape != TextureImporterShape.Texture2D || ti.sRGBTexture || ti.mipmapEnabled || ti.maxTextureSize != max
                || ti.wrapModeU != TextureWrapMode.Repeat || ti.wrapModeV != TextureWrapMode.Clamp || ti.textureCompression != TextureImporterCompression.CompressedHQ || ti.filterMode != FilterMode.Bilinear;
            if (dirty)
            {
                ti.textureType = TextureImporterType.Default; ti.textureShape = TextureImporterShape.Texture2D; ti.sRGBTexture = false; ti.mipmapEnabled = false;   // T09: no mips: the panorama's u wrap picks the smallest mip on one column and drew a dashed grey vertical line in the sky
                ti.maxTextureSize = max; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.isReadable = false;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Where the sun is in the pixels (brightest pixel, as a lat-long u/v) and the colour of the sky within 3 degrees of the horizon outside the sun's 30 degree azimuth band.
        public static SkyInfo ProbeSky(string fileName)
        {
            if (skyCache.TryGetValue(fileName, out SkyInfo c)) return c;
            float[] px = ReadHdr(BuilderUtil.Abs(SkyFolder + "/" + fileName), out int w, out int h);
            int best = 0; float bestL = -1f;
            for (int i = 0; i < w * h; i++)
            {
                float l = 0.2126f * px[i * 3] + 0.7152f * px[i * 3 + 1] + 0.0722f * px[i * 3 + 2];
                if (l > bestL) { bestL = l; best = i; }
            }
            var info = new SkyInfo { SunU = ((best % w) + 0.5f) / w, SunV = ((best / w) + 0.5f) / h, SunLuminance = bestL };
            info.SunElevation = 90f - info.SunV * 180f;
            double r = 0, g = 0, b = 0; int n = 0;
            int y0 = Mathf.RoundToInt((0.5f - 3f / 180f) * h), y1 = Mathf.RoundToInt((0.5f + 3f / 180f) * h);
            for (int y = y0; y < y1; y++)
                for (int x = 0; x < w; x++)
                {
                    float du = Mathf.Abs(((x + 0.5f) / w) - info.SunU); du = Mathf.Min(du, 1f - du);
                    if (du * 360f < 30f) continue;
                    int i = y * w + x; r += px[i * 3]; g += px[i * 3 + 1]; b += px[i * 3 + 2]; n++;
                }
            info.HorizonLinear = new Color((float)(r / n), (float)(g / n), (float)(b / n));
            skyCache[fileName] = info;
            Debug.Log("SC B0 sky " + fileName + " sun u=" + info.SunU.ToString("F4") + " v=" + info.SunV.ToString("F4") + " elevation=" + info.SunElevation.ToString("F1") + " lum=" + bestL.ToString("F0") + " horizon(linear)=" + info.HorizonLinear);
            return info;
        }

        // Skybox/Panoramic turns its geometry by _Rotation about Y (angle measured from +X toward +Z) and reads the texture along the unturned direction, with the
        // texture's longitude = atan2(z, x) = (0.5 - u) * 2 pi. So the sun, at local azimuth phiS, is drawn at phiS + rotation; we want the sun's own azimuth atan2(-fwd.z, -fwd.x).
        public static float SkyRotationDegrees(SkyInfo sky, Quaternion sunRotation)
        {
            Vector3 toSun = -(sunRotation * Vector3.forward);
            float target = Mathf.Atan2(toSun.z, toSun.x) * Mathf.Rad2Deg;
            float local = (0.5f - sky.SunU) * 360f;
            return Mathf.Repeat(target - local, 360f);
        }

        // Fog colour from the horizon: the horizon hue at the start colour's brightness, halfway to the start colour (the table's start values).
        public static Color HorizonFog(SkyInfo sky, Color start)
        {
            float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            Color startLin = start.linear;
            float k = Lum(startLin) / Mathf.Max(1e-4f, Lum(sky.HorizonLinear));
            Color matched = new Color(sky.HorizonLinear.r * k, sky.HorizonLinear.g * k, sky.HorizonLinear.b * k).gamma;
            Color fog = Color.Lerp(start, matched, 0.5f); fog.a = 1f;
            return fog;
        }

        // Radiance .hdr (new-style RLE or flat), top row first. RGB floats.
        static float[] ReadHdr(string abs, out int w, out int h)
        {
            byte[] d = File.ReadAllBytes(abs); int pos = 0;
            string Line() { int s = pos; while (d[pos] != (byte)'\n') pos++; string t = System.Text.Encoding.ASCII.GetString(d, s, pos - s); pos++; return t; }
            while (Line().Length > 0) { }
            string[] res = Line().Split(' ');
            if (res.Length != 4 || res[0] != "-Y" || res[2] != "+X") throw new InvalidDataException("Unsupported HDR orientation in " + abs + ": " + string.Join(" ", res));
            h = int.Parse(res[1]); w = int.Parse(res[3]);
            var rgb = new float[w * h * 3]; var line = new byte[w * 4];
            for (int y = 0; y < h; y++)
            {
                if (w >= 8 && w < 32768 && d[pos] == 2 && d[pos + 1] == 2 && (d[pos + 2] & 0x80) == 0)
                {
                    pos += 4;
                    for (int c = 0; c < 4; c++)
                    {
                        int x = 0;
                        while (x < w)
                        {
                            int count = d[pos++];
                            if (count > 128) { count -= 128; byte v = d[pos++]; for (int k = 0; k < count; k++) line[(x++) * 4 + c] = v; }
                            else for (int k = 0; k < count; k++) line[(x++) * 4 + c] = d[pos++];
                        }
                    }
                }
                else { Buffer.BlockCopy(d, pos, line, 0, w * 4); pos += w * 4; }
                for (int x = 0; x < w; x++)
                {
                    int e = line[x * 4 + 3]; int o = (y * w + x) * 3;
                    if (e == 0) continue;
                    float f = (float)Math.Pow(2.0, e - 136);
                    rgb[o] = line[x * 4] * f; rgb[o + 1] = line[x * 4 + 1] * f; rgb[o + 2] = line[x * 4 + 2] * f;
                }
            }
            return rgb;
        }

        // ------------------------------------------------------------------------------------------------ cloud-shadow cookies
        // 256 px tiling grey map for the sun's cookie: 1 in clear sky, 1 - dip under a cloud. Periodic value noise (3 octaves on an integer lattice) so the 220 m tile wraps.
        public static Texture2D CloudCookie(string name, float dip)
        {
            string dir = TexRoot + "Sky"; BuilderUtil.EnsureFolder(dir);
            string path = dir + "/CloudCookie_" + name + ".png";
            const int n = 256; var px = new Color32[n * n];
            int[] periods = { 4, 8, 16 }; float[] amps = { 0.55f, 0.30f, 0.15f };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0f;
                    for (int o = 0; o < 3; o++) v += amps[o] * Periodic(x / (float)n * periods[o], y / (float)n * periods[o], periods[o], o);
                    float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.62f, v));
                    byte b = (byte)Mathf.RoundToInt((1f - dip * cloud) * 255f);
                    px[y * n + x] = new Color32(b, b, b, 255);
                }
            if (BeautyCornerArt.WritePng(BuilderUtil.Abs(path), n, n, px) || AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Periodic(float x, float y, int period, int octave)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0 % period, y0 % period, octave), b = Hash((x0 + 1) % period, y0 % period, octave);
            float c = Hash(x0 % period, (y0 + 1) % period, octave), d = Hash((x0 + 1) % period, (y0 + 1) % period, octave);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        static float Hash(int x, int y, int o)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + o * 1442695041) + 0x9E3779B9u;
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
