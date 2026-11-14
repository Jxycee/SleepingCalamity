// B1 town intake (editor time, authority: none). Turns the Fab/Megascans mesh folders in Art/Source/Fab/<code>_<name>/ into project assets:
//   Assets/_Project/Content/Art/Town/Adopted/<code>/  FBX copies, packed textures (<code>_albedo, _normal, _ms, _occ; 2048 max) and the prefab <code>.prefab
//   Assets/_Project/Content/Materials/B1/M_<code>.mat  URP Lit (alpha clip + two-sided for plants)
// Props: the single Mid FBX. Plants (T12a-e): VarA LOD0..LOD2 under a LODGroup. L04 (FBX only, its texture was deleted at 4096 px): flat URP materials from the FBX's own slot colours.
// Megascans FBX are in cm; the importer's file scale brings them to metres, and the bounds are logged per code ("SC B1 BOUNDS"). The prefab root is the pivot: bottom centre of the LOD0 bounds.
// Opacity goes into the albedo alpha (alpha clip); Translucency and Billboard maps are ignored. Deterministic: sorted order, files rewritten only when bytes differ.
// Run: menu SleepingCalamity/Art/Adopt Town Intake (B1), or Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.B1TownIntake.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class B1TownIntake
    {
        const string SrcRoot = "Art/Source/Fab/";
        const string DstRoot = "Assets/_Project/Content/Art/Town/Adopted/";
        const string MatFolder = "Assets/_Project/Content/Materials/B1";
        const int Cap = 2048;
        static readonly HashSet<string> Skip = new HashSet<string> { "T01", "T01b", "T01c", "T03", "T15a", "T15b", "C01", "C02", "C15" };
        static readonly HashSet<string> Plants = new HashSet<string> { "T12a", "T12b", "T12c", "T12d", "T12e" };
        // Props whose source is far off real size: the prefab is scaled so the LOD0 bounds are this tall (m).
        static readonly Dictionary<string, float> TargetHeight = new Dictionary<string, float> { { "T11b", 1.5f } };
        const string L04Pick = "Composition_7";
        static readonly float[] LodHeights = { 0.5f, 0.2f, 0.05f };

        [MenuItem("SleepingCalamity/Art/Adopt Town Intake (B1)")]
        public static void Run()
        {
            string root = BuilderUtil.Abs(SrcRoot);
            var dirs = Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal).ToList();
            int made = 0; var failed = new List<string>();
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir); string code = name.Split('_')[0];
                if (Skip.Contains(code)) continue;
                if (Directory.GetFiles(dir, "*.fbx").Length == 0) continue;
                try { Adopt(code, dir); made++; }
                catch (Exception e) { failed.Add(code + ": " + e.Message); Debug.LogError("SC B1 FAIL " + code + ": " + e); }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("SC B1 DONE prefabs=" + made + " failed=" + failed.Count);
            if (failed.Count > 0) throw new InvalidOperationException("B1 intake failures: " + string.Join("; ", failed));
        }

        static void Adopt(string code, string srcDir)
        {
            string dst = DstRoot + code; BuilderUtil.EnsureFolder(dst); BuilderUtil.EnsureFolder(MatFolder);
            bool plant = Plants.Contains(code);
            var fbxFiles = Directory.GetFiles(srcDir, "*.fbx").OrderBy(f => f, StringComparer.Ordinal).ToList();
            var lodSrc = new List<string>();
            if (plant)
            {
                for (int l = 0; l < 3; l++)
                {
                    string f = fbxFiles.FirstOrDefault(x => Regex.IsMatch(Path.GetFileName(x), "_VarA_LOD" + l + @"\.fbx$"));
                    if (f != null) lodSrc.Add(f);
                }
                if (lodSrc.Count == 0) throw new InvalidOperationException("no VarA LOD fbx");
            }
            else lodSrc.Add(fbxFiles[0]);

            // models
            var models = new List<GameObject>();
            for (int i = 0; i < lodSrc.Count; i++)
            {
                string file = plant ? code + "_LOD" + i + ".fbx" : code + ".fbx";
                string dstPath = dst + "/" + file;
                if (B0Fab.CopyIfDifferent(lodSrc[i], BuilderUtil.Abs(dstPath)) || AssetImporter.GetAtPath(dstPath) == null) AssetDatabase.ImportAsset(dstPath, ImportAssetOptions.ForceUpdate);
                GameObject m = BeautyCornerArt.LoadModel(dst, file, keepMaterialSlots: code == "L04");
                if (m == null) throw new InvalidOperationException("model did not import: " + dstPath);
                models.Add(m);
            }

            // materials
            Material[] mats;
            if (code == "L04") mats = null;   // built from the instantiated slots below
            else mats = new[] { BuildMaterial(code, srcDir, dst, plant) };

            // prefab
            var rootGo = new GameObject(code);
            try
            {
                var inst = new List<GameObject>();
                for (int i = 0; i < models.Count; i++)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(models[i]);
                    g.name = plant ? "LOD" + i : "Model";
                    g.transform.SetParent(rootGo.transform, false);
                    inst.Add(g);
                }
                if (code == "L04")
                {
                    // The FBX is a layout sheet of ~64 pots on a grid: keep one pot (plain instance, not a prefab link, so the others can go).
                    var single = UnityEngine.Object.Instantiate(models[0]); single.name = "Model";
                    single.transform.SetParent(rootGo.transform, false);
                    UnityEngine.Object.DestroyImmediate(inst[0]);
                    inst[0] = single;
                    var all = single.GetComponentsInChildren<Renderer>(true).ToList();
                    if (!all.Any(r => r.name == L04Pick)) throw new InvalidOperationException("L04 part not found: " + L04Pick);
                    foreach (var r in all) if (r != null && r.name != L04Pick) UnityEngine.Object.DestroyImmediate(r.gameObject);
                }
                if (TargetHeight.TryGetValue(code, out float th))
                {
                    float k = th / BoundsOf(inst[0]).size.y;
                    foreach (var g in inst) g.transform.localScale = Vector3.one * k;
                }
                Bounds b = BoundsOf(inst[0]);
                Vector3 off = new Vector3(-b.center.x, -b.min.y, -b.center.z);
                foreach (var g in inst) g.transform.localPosition = off;
                if (code == "L04") mats = FlatSlotMaterials(code, inst[0]);
                foreach (var g in inst)
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int s = 0; s < slots.Length; s++) slots[s] = mats.Length == 1 ? mats[0] : mats[Mathf.Min(s, mats.Length - 1)];
                        r.sharedMaterials = slots;
                    }
                if (plant)
                {
                    var lg = rootGo.AddComponent<LODGroup>();
                    var lods = new LOD[inst.Count];
                    for (int i = 0; i < inst.Count; i++)
                        lods[i] = new LOD(i == inst.Count - 1 ? Mathf.Min(LodHeights[i], 0.05f) : LodHeights[i], inst[i].GetComponentsInChildren<Renderer>(true));
                    lg.SetLODs(lods); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
                }
                Bounds fin = BoundsOf(rootGo);
                Debug.Log("SC B1 BOUNDS " + code + " size=(" + fin.size.x.ToString("F3") + ", " + fin.size.y.ToString("F3") + ", " + fin.size.z.ToString("F3") + ") m, min.y=" + fin.min.y.ToString("F3") + " lods=" + inst.Count);
                PrefabUtility.SaveAsPrefabAsset(rootGo, dst + "/" + code + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(rootGo); }
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) throw new InvalidOperationException("no renderers");
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // L04 ships no usable texture: one flat URP Lit per FBX slot, colour from the FBX material.
        static Material[] FlatSlotMaterials(string code, GameObject inst)
        {
            var seen = new List<string>(); var list = new List<Material>();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found");
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                foreach (var src in r.sharedMaterials)
                {
                    string n = src != null ? src.name : "default";
                    if (seen.Contains(n)) continue; seen.Add(n);
                    Color c = new Color(0.45f, 0.5f, 0.3f);
                    if (src != null && src.HasProperty("_Color")) c = src.GetColor("_Color");
                    Material m = LookTestBuilder.Ensure(MatFolder + "/M_" + code + "_" + Sanitize(n) + ".mat", lit);
                    m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Smoothness", 0.3f); m.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
                    list.Add(m);
                }
            if (list.Count == 0) throw new InvalidOperationException("L04 has no slots");
            return list.ToArray();
        }

        static string Sanitize(string s) => Regex.Replace(s, "[^A-Za-z0-9_]", "_");

        // ------------------------------------------------------------------------------------------------ textures and material
        static string Find(string dir, string tail) =>
            Directory.GetFiles(dir, "*.jpg").Where(f => !Path.GetFileName(f).Contains("Billboard") && Path.GetFileName(f).EndsWith("_2K_" + tail + ".jpg", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();

        static Material BuildMaterial(string code, string srcDir, string dst, bool plant)
        {
            string bc = Find(srcDir, "BaseColor"), nr = Find(srcDir, "Normal"), ro = Find(srcDir, "Roughness"), ao = Find(srcDir, "AO"), me = Find(srcDir, "Metalness"), op = Find(srcDir, "Opacity");
            if (bc == null || nr == null || ro == null) throw new InvalidOperationException("missing BaseColor/Normal/Roughness");
            bool alpha = plant && op != null;
            var written = new List<string>();
            string albedo = dst + "/" + code + "_albedo." + (alpha ? "png" : "jpg"), normal = dst + "/" + code + "_normal.jpg", ms = dst + "/" + code + "_ms.png", occ = dst + "/" + code + "_occ.png";

            Color32[] c = Decode(bc, out int w, out int h);
            Color32[] rough = Decode(ro, out int rw, out int rh);
            if (rw != w || rh != h) throw new InvalidDataException("map size mismatch");
            if (alpha)
            {
                Color32[] o = Decode(op, out int ow, out int oh);
                if (ow != w || oh != h) throw new InvalidDataException("opacity size mismatch");
                var px = new Color32[c.Length];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(c[i].r, c[i].g, c[i].b, o[i].r);
                if (WritePacked(albedo, w, h, px)) written.Add(albedo);
            }
            else if (B0Fab.CopyIfDifferent(bc, BuilderUtil.Abs(albedo)) || AssetImporter.GetAtPath(albedo) == null) written.Add(albedo);
            if (B0Fab.CopyIfDifferent(nr, BuilderUtil.Abs(normal)) || AssetImporter.GetAtPath(normal) == null) written.Add(normal);

            Color32[] met = me != null && !plant ? Decode(me, out int mw, out int mh) : null;
            if (met != null && met.Length != rough.Length) met = null;
            var msPx = new Color32[rough.Length];
            for (int i = 0; i < msPx.Length; i++) msPx[i] = new Color32(met != null ? met[i].r : (byte)0, 0, 0, (byte)(255 - rough[i].r));
            if (WritePacked(ms, w, h, msPx)) written.Add(ms);
            if (ao != null)
            {
                Color32[] a = Decode(ao, out int aw, out int ah);
                if (a.Length == rough.Length)
                {
                    var op2 = new Color32[a.Length];
                    for (int i = 0; i < op2.Length; i++) op2[i] = new Color32(a[i].r, a[i].r, a[i].r, 255);
                    if (WritePacked(occ, w, h, op2)) written.Add(occ);
                }
                else ao = null;
            }
            foreach (string p in written) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            BeautyCornerArt.ImportTexture(albedo, srgb: true, normal: false, alphaTransparency: alpha);
            BeautyCornerArt.ImportTexture(normal, srgb: false, normal: true, alphaTransparency: false);
            BeautyCornerArt.ImportTexture(ms, srgb: false, normal: false, alphaTransparency: false);
            if (ao != null) BeautyCornerArt.ImportTexture(occ, srgb: false, normal: false, alphaTransparency: false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_" + code + ".mat", lit);
            m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo)); m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (ao != null) { m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occ)); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (alpha)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
                m.SetFloat("_AlphaToMask", 1f); m.doubleSidedGI = true;
            }
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // Writes the PNG only when the bytes differ (or the file is missing).
        static bool WritePacked(string assetPath, int w, int h, Color32[] px)
        {
            string abs = BuilderUtil.Abs(assetPath);
            bool wrote = BeautyCornerArt.WritePng(abs, w, h, px);
            return wrote || AssetImporter.GetAtPath(assetPath) == null;
        }

        // Decodes a jpg to RGBA32; halves it (2 x 2 box) until it fits the 2048 cap, so nothing above 2048 reaches the project.
        static Color32[] Decode(string file, out int w, out int h)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(t, File.ReadAllBytes(file), false)) throw new InvalidDataException("Cannot read " + file);
            w = t.width; h = t.height; Color32[] px = t.GetPixels32(); UnityEngine.Object.DestroyImmediate(t);
            while (w > Cap || h > Cap)
            {
                int nw = w / 2, nh = h / 2; var o = new Color32[nw * nh];
                for (int y = 0; y < nh; y++)
                    for (int x = 0; x < nw; x++)
                    {
                        int r = 0, g = 0, b = 0, a = 0;
                        for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { var p = px[(2 * y + dy) * w + 2 * x + dx]; r += p.r; g += p.g; b += p.b; a += p.a; }
                        o[y * nw + x] = new Color32((byte)((r + 2) / 4), (byte)((g + 2) / 4), (byte)((b + 2) / 4), (byte)((a + 2) / 4));
                    }
                px = o; w = nw; h = nh;
            }
            return px;
        }
    }
}
