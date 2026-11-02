// Hollowmere town surfaces (visual review A2 and A11): flat, collider-free, shadow-free soft decals over the cobbles, one shared soft-blob texture and a few tinted materials.
//   - Macro variation on the square (A2): the B1 cobble and the setts repeat in a radial fan with one tone. Large soft tone patches (damp, sandy wear, mossy grey), small stains,
//     darker damp strips along the square's edges where the buildings hug it, and pale worn runs along the walking lines break the repeat. Hash-placed, so builds are identical.
//   - Contact AO (A11): a soft dark footprint under every sack stack, crate, barrel and stall standing on paved ground, one decal per prop under that prop's preset root
//     (so Ruined/Rebuilt toggling still works). Static after the build, no colliders, no shadow casting.
// Called by HollowmereBuilder after the clutter is placed (the props exist) and before the foliage.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereSurfaces
    {
        const string TexFolder = "Assets/_Project/Content/Textures/Hollowmere/Surfaces", MatFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string TexPath = TexFolder + "/T_HM_SoftBlob.png";
        const float DecalLift = 0.075f;   // above the cobble bed: the setts stand a few cm proud, taller ones show through like grit in the wash
        static readonly Regex PropName = new Regex("sack|crate|barrel|stall|cask|keg", RegexOptions.IgnoreCase);

        static Texture2D _blob;
        static Material _damp, _sand, _moss, _stain, _wear, _ao;

        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            EnsureAssets();
            int macro = SquareMacro(set, t);
            int ao = ContactAo(set, ruined, rebuilt, t);
            Debug.Log("SC HOLLOWMERE surfaces squareDecals=" + macro + " contactAo=" + ao);
        }

        // ------------------------------------------------------------------------------------------------ assets
        static void EnsureAssets()
        {
            BuilderUtil.EnsureFolder(TexFolder); BuilderUtil.EnsureFolder(MatFolder);
            const int R = 128;
            var px = new Color32[R * R];
            for (int j = 0; j < R; j++)
                for (int i = 0; i < R; i++)
                {
                    float x = Mathf.Abs((i + 0.5f) / R * 2f - 1f), y = Mathf.Abs((j + 0.5f) / R * 2f - 1f);
                    float d = Mathf.Pow(Mathf.Pow(x, 3f) + Mathf.Pow(y, 3f), 1f / 3f);   // a rounded square
                    float s = Mathf.Clamp01((1f - d) / 0.65f);
                    float a = s * s * (3f - 2f * s);
                    px[j * R + i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(TexPath);
            bool fresh = !File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png);
            if (fresh) File.WriteAllBytes(abs, png);
            if (fresh || AssetImporter.GetAtPath(TexPath) == null) AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(TexPath);
            if (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || imp.mipmapEnabled)
            {
                imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            _blob = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            _damp = DecalMat("HM_Surf_Damp", new Color(0.02f, 0.017f, 0.012f), 0.34f);
            _sand = DecalMat("HM_Surf_Wear", new Color(0.62f, 0.52f, 0.38f), 0.20f);
            _moss = DecalMat("HM_Surf_Moss", new Color(0.18f, 0.22f, 0.13f), 0.17f);
            _stain = DecalMat("HM_Surf_Stain", new Color(0.04f, 0.03f, 0.02f), 0.24f);
            _wear = DecalMat("HM_Surf_WornRun", new Color(0.66f, 0.56f, 0.42f), 0.26f);
            _ao = DecalMat("HM_Surf_ContactAO", new Color(0.015f, 0.012f, 0.01f), 0.6f);
            AssetDatabase.SaveAssets();
        }

        static Material DecalMat(string name, Color rgb, float alpha)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_" + name + ".mat", lit);
            m.SetTexture("_BaseMap", _blob); m.SetColor("_BaseColor", new Color(rgb.r, rgb.g, rgb.b, alpha)); m.SetColor("_Color", m.GetColor("_BaseColor"));
            m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            LookTestBuilder.MakeTransparent(m);
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        // A flat quad lying on the ground at world (x, y, z), w by d metres, yawed; visual only.
        static GameObject Flat(Transform parent, string name, Material m, float x, float y, float z, float w, float d, float yaw, int order)
        {
            GameObject go = LookTestBuilder.Prim(parent, name, PrimitiveType.Quad, Vector3.zero, new Vector3(w, d, 1f), m);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(90f, yaw, 0f));
            go.transform.localScale = new Vector3(w, d, 1f);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.sortingOrder = order;
            return go;
        }

        static float H(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)a * 73856093u ^ (uint)b * 19349663u ^ (uint)salt * 83492791u ^ 0x9E3779B9u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ------------------------------------------------------------------------------------------------ A2: the square
        static int SquareMacro(Transform set, TownLayout t)
        {
            Transform g = Group(set, "SquareMacro");
            float x0 = HollowmereLayout.SquareMinX, x1 = HollowmereLayout.SquareMaxX, z0 = HollowmereLayout.SquareMinZ, z1 = HollowmereLayout.SquareMaxZ;
            int n = 0;
            float Y(float x, float z) => HollowmereLayout.Ground(t, x, z) + DecalLift;
            // tone patches: 8 m cells, two per cell, sizes 5-11 m by 3-7 m, random yaw; the order stack keeps the larger ones under the smaller
            int cx = 0;
            for (float cellX = x0; cellX < x1; cellX += 8f, cx++)
            {
                int cz = 0;
                for (float cellZ = z0; cellZ < z1; cellZ += 8f, cz++)
                    for (int k = 0; k < 2; k++)
                    {
                        float px = cellX + 8f * H(cx, cz, 10 + k), pz = cellZ + 8f * H(cx, cz, 20 + k);
                        float pick = H(cx, cz, 30 + k);
                        Material m = pick < 0.45f ? _damp : pick < 0.75f ? _sand : _moss;
                        Flat(g, "Tone" + n, m, px, Y(px, pz), pz, 5f + 6f * H(cx, cz, 40 + k), 3f + 4f * H(cx, cz, 50 + k), 360f * H(cx, cz, 60 + k), 1);
                        n++;
                    }
            }
            // small stains and a few pale scuffs, 1.2-3 m, scattered over the whole square
            for (int i = 0; i < 46; i++)
            {
                float px = Mathf.Lerp(x0, x1, H(i, 1, 70)), pz = Mathf.Lerp(z0, z1, H(i, 2, 71));
                float s = 1.2f + 1.8f * H(i, 3, 72);
                Flat(g, "Stain" + i, i % 4 == 0 ? _sand : _stain, px, Y(px, pz), pz, s * (1f + H(i, 4, 73)), s, 360f * H(i, 5, 74), 2);
                n++;
            }
            // damp strips along the edges (where the buildings and walls stand): 1.1-1.9 m wide, 3-6 m long, with gaps
            var edges = new[] { (x0, z0, x1, z0, 0f, 1f), (x0, z1, x1, z1, 0f, -1f), (x0, z0, x0, z1, 1f, 0f), (x1, z0, x1, z1, -1f, 0f) };   // from, to, inward normal
            int e = 0;
            foreach (var (ax, az, bx, bz, nx, nz) in edges)
            {
                float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                float dx = (bx - ax) / len, dz = (bz - az) / len, s = 0f;
                int k = 0;
                while (s < len - 1f)
                {
                    float seg = 3f + 3f * H(e, k, 80), wid = 1.1f + 0.8f * H(e, k, 81), gap = 0.5f + 3f * H(e, k, 82);
                    float mid = s + seg * 0.5f, off = 0.55f + 0.3f * H(e, k, 83);
                    float px = ax + dx * mid + nx * off, pz = az + dz * mid + nz * off;
                    float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg + 90f;   // the quad's long (local x) axis along the edge
                    if (H(e, k, 84) > 0.2f) { Flat(g, "Damp" + e + "_" + k, _damp, px, Y(px, pz), pz, seg, wid, yaw, 3); n++; }
                    s += seg + gap; k++;
                }
                e++;
            }
            // pale worn runs where feet go: from the Market Street mouth, the West Lane mouth and the heroes' home door to the middle of the square
            var runs = new[] { (0f, -20.5f, 1f, -6f), (-19.5f, -9f, -3f, -5f), (11f, 5f, 3f, -1f) };
            int r = 0;
            foreach (var (ax, az, bx, bz) in runs)
            {
                float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                float mx = (ax + bx) * 0.5f, mz = (az + bz) * 0.5f, yaw = Mathf.Atan2(bx - ax, bz - az) * Mathf.Rad2Deg + 90f;
                Flat(g, "Run" + r, _wear, mx, Y(mx, mz), mz, len, 2.4f + 0.6f * H(r, 0, 90), yaw, 2);
                Flat(g, "RunCore" + r, _wear, mx, Y(mx, mz), mz, len * 0.7f, 1.0f, yaw, 3);
                n += 2; r++;
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ A11: contact AO
        static int ContactAo(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            var roots = new[] { set, ruined, rebuilt };
            var groups = new Dictionary<Transform, List<Renderer>>();
            foreach (Transform root in roots)
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "FOOTPRINT" || !(r is MeshRenderer)) continue;
                    Transform prop = null;
                    Transform c = r.transform;
                    for (int up = 0; up < 4 && c != null && !roots.Contains(c); up++, c = c.parent)
                        if (PropName.IsMatch(c.name)) { prop = c; break; }
                    if (prop == null) continue;
                    if (!groups.TryGetValue(prop, out List<Renderer> list)) groups[prop] = list = new List<Renderer>();
                    list.Add(r);
                }
            int count = 0;
            foreach (var kv in groups.OrderBy(kv => PathOf(kv.Key), System.StringComparer.Ordinal))
            {
                Bounds b = kv.Value[0].bounds;
                foreach (Renderer r in kv.Value) b.Encapsulate(r.bounds);
                float ground = HollowmereLayout.Ground(t, b.center.x, b.center.z);
                if (b.size.x > 7f || b.size.z > 7f || b.min.y > ground + 0.35f || b.min.y < ground - 1f) continue;   // a container, a prop on a table, or a hole
                if (!HollowmereTerrain.IsPaved(t, b.center.x, b.center.z)) continue;
                Transform root = kv.Key; while (root.parent != null && !roots.Contains(root.parent)) root = root.parent;
                Transform owner = root.parent != null ? root.parent : set;
                Flat(Group(owner, "ContactAO"), "AO_" + kv.Key.name, _ao, b.center.x, b.min.y + 0.03f, b.center.z, Mathf.Min(6f, b.size.x * 1.35f + 0.4f), Mathf.Min(6f, b.size.z * 1.35f + 0.4f), 0f, 4);
                count++;
            }
            return count;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name + "#" + t.GetSiblingIndex());
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
