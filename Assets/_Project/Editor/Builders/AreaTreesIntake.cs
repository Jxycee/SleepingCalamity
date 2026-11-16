// Area 1 tree intake (editor time, authority: none). Turns the Blender adopt output of Art/Blender/town/sc_trees_adopt.py, already committed under
//   Assets/_Project/Content/Art/Town/Trees/<code>/  (<code>.fbx with meshes <code>_LOD0/1/2, <code>_bark_*|<code>_leaf_* textures: albedo, normal, ms, occ)
// into the placement prefabs Content/Art/Town/Trees/<code>/<code>.prefab and the materials Content/Materials/Trees/M_<code>_Bark|Leaves.mat (URP Lit).
// Codes: TR01 small broadleaf 6 m, TR02 big broadleaf 13 m, TR03 medium broadleaf 9 m, TR04 conifer 11 m, TR05 tall shrub 2.2 m, TR06 low shrub 0.9 m, TR07 low bush 0.7 m.
// Prefab: unrotated unit-scale root, pivot at the trunk base (the meshes are modelled that way), one LODGroup (0.35 / 0.12 / 0.02 cull), children LOD0..LOD2 with
// a MeshFilter + MeshRenderer each (the FBX's own auto LODGroup is not instantiated). Trees get one CapsuleCollider around the trunk; shrubs none.
// Deterministic: files and assets are rewritten only when different. Run: menu SleepingCalamity/Art/Adopt Town Trees, or Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.AreaTreesIntake.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class AreaTreesIntake
    {
        const string Root = "Assets/_Project/Content/Art/Town/Trees/";
        const string MatFolder = "Assets/_Project/Content/Materials/Trees";
        public static readonly string[] Codes = { "TR01", "TR02", "TR03", "TR04", "TR05", "TR06", "TR07" };
        static readonly HashSet<string> Shrubs = new HashSet<string> { "TR05", "TR06", "TR07" };
        static readonly float[] LodHeights = { 0.35f, 0.12f, 0.02f };

        [MenuItem("SleepingCalamity/Art/Adopt Town Trees (TR01-TR07)")]
        public static void Run()
        {
            int made = 0; var failed = new List<string>();
            foreach (string code in Codes)
            {
                try { Adopt(code); made++; }
                catch (Exception e) { failed.Add(code + ": " + e.Message); Debug.LogError("SC TREES FAIL " + code + ": " + e); }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("SC TREES DONE prefabs=" + made + " failed=" + failed.Count);
            if (failed.Count > 0) throw new InvalidOperationException("Tree intake failures: " + string.Join("; ", failed));
        }

        static void Adopt(string code)
        {
            string dir = Root + code; bool shrub = Shrubs.Contains(code);
            string fbx = dir + "/" + code + ".fbx";
            if (AssetImporter.GetAtPath(fbx) == null) throw new InvalidOperationException("missing " + fbx);
            BuilderUtil.EnsureFolder(MatFolder);

            // textures
            string leafAlbedo = Tex(dir, code + "_leaf_albedo.png"), leafNormal = Tex(dir, code + "_leaf_normal.jpg"), leafMs = Tex(dir, code + "_leaf_ms.png");
            ImportTex(leafAlbedo, srgb: true, normal: false, alpha: true); ImportTex(leafNormal, false, true, false); ImportTex(leafMs, false, false, false);
            Material leaves = MakeMaterial("M_" + code + "_Leaves", leafAlbedo, leafNormal, leafMs, null, leaf: true);
            Material bark = null;
            if (!shrub)
            {
                string ba = Tex(dir, code + "_bark_albedo.jpg"), bn = Tex(dir, code + "_bark_normal.jpg"), bm = Tex(dir, code + "_bark_ms.png"), bo = Tex(dir, code + "_bark_occ.png");
                ImportTex(ba, true, false, false); ImportTex(bn, false, true, false); ImportTex(bm, false, false, false); ImportTex(bo, false, false, false);
                bark = MakeMaterial("M_" + code + "_Bark", ba, bn, bm, bo, leaf: false);
            }

            // model
            GameObject model = BeautyCornerArt.LoadModel(dir, code + ".fbx");
            if (model == null) throw new InvalidOperationException("model did not import: " + fbx);
            var meshes = new Mesh[3];
            for (int k = 0; k < 3; k++)
            {
                string want = code + "_LOD" + k;
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name == want || mf.sharedMesh != null && mf.sharedMesh.name == want) { meshes[k] = mf.sharedMesh; break; }
                if (meshes[k] == null) throw new InvalidOperationException("mesh not found: " + want);
            }

            Material[] slots = shrub ? new[] { leaves } : new[] { bark, leaves };
            var root = new GameObject(code);
            try
            {
                var lodRenderers = new Renderer[3][];
                for (int k = 0; k < 3; k++)
                {
                    int sub = meshes[k].subMeshCount;
                    if (sub != slots.Length) throw new InvalidOperationException(code + "_LOD" + k + " has " + sub + " submeshes, expected " + slots.Length);
                    var go = new GameObject("LOD" + k); go.transform.SetParent(root.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[k];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = slots;
                    bool shadows = k < 2 || !shrub;
                    mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                    lodRenderers[k] = new Renderer[] { mr };
                    Debug.Log("SC TREES LOD " + code + " LOD" + k + " tris=" + Tris(meshes[k]) + " localRot=" + go.transform.localRotation.eulerAngles + " localScale=" + go.transform.localScale);
                }
                var lg = root.AddComponent<LODGroup>();
                var lods = new LOD[3];
                for (int k = 0; k < 3; k++) lods[k] = new LOD(LodHeights[k], lodRenderers[k]);
                lg.SetLODs(lods); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();

                Bounds b = meshes[0].bounds;
                string col = "none";
                if (!shrub)
                {
                    var cap = root.AddComponent<CapsuleCollider>();
                    float r = TrunkRadius(meshes[0], b);
                    float h = Mathf.Min(4f, b.max.y);
                    cap.direction = 1; cap.radius = r; cap.height = h; cap.center = new Vector3(TrunkCenter.x, h * 0.5f, TrunkCenter.z);
                    col = "capsule r=" + r.ToString("F3") + " h=" + h.ToString("F2") + " c=(" + cap.center.x.ToString("F2") + ", " + cap.center.y.ToString("F2") + ", " + cap.center.z.ToString("F2") + ")";
                }
                Debug.Log("SC TREES BOUNDS " + code + " height=" + b.max.y.ToString("F3") + " minY=" + b.min.y.ToString("F3") + " size=(" + b.size.x.ToString("F2") + ", " + b.size.y.ToString("F2") + ", " + b.size.z.ToString("F2") + ") lodGroups="
                    + root.GetComponentsInChildren<LODGroup>(true).Length + " collider=" + col);
                PrefabUtility.SaveAsPrefabAsset(root, dir + "/" + code + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Vector3 TrunkCenter;

        // Trunk radius at 1 m: the bark (submesh 0) vertices between 0.8 and 1.2 m above the pivot; their mean distance from their own centre.
        static float TrunkRadius(Mesh m, Bounds b)
        {
            var v = m.vertices; var tris = m.GetTriangles(0);
            var used = new HashSet<int>(tris);
            var ring = new List<Vector3>();
            foreach (int i in used) if (v[i].y > 0.8f && v[i].y < 1.2f) ring.Add(v[i]);
            if (ring.Count < 3) { TrunkCenter = new Vector3(0, 0, 0); return 0.15f; }
            Vector3 c = Vector3.zero; foreach (var p in ring) c += p; c /= ring.Count;
            float sum = 0; foreach (var p in ring) sum += new Vector2(p.x - c.x, p.z - c.z).magnitude; sum /= ring.Count;
            TrunkCenter = new Vector3(c.x, 0, c.z);
            return Mathf.Max(0.1f, sum);
        }

        static int Tris(Mesh m) { int n = 0; for (int i = 0; i < m.subMeshCount; i++) n += (int)(m.GetIndexCount(i) / 3); return n; }

        static string Tex(string dir, string file)
        {
            string p = dir + "/" + file;
            if (AssetImporter.GetAtPath(p) == null) throw new InvalidOperationException("missing texture " + p);
            return p;
        }

        // BeautyCornerArt.ImportTexture sets type/sRGB/2048 cap/mips/aniso; the leaf albedo additionally keeps alpha coverage through the mips (distant crowns do not thin out).
        static void ImportTex(string path, bool srgb, bool normal, bool alpha)
        {
            BeautyCornerArt.ImportTexture(path, srgb, normal, alpha);
            if (!alpha) return;
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti.mipMapsPreserveCoverage && Mathf.Approximately(ti.alphaTestReferenceValue, 0.4f)) return;
            ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f; ti.SaveAndReimport();
        }

        static Material MakeMaterial(string name, string albedo, string normal, string ms, string occ, bool leaf)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MatFolder + "/" + name + ".mat", lit);
            m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo)); m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", leaf ? 0.25f : 0.5f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (occ != null) { m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occ)); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (leaf)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.4f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
                m.SetFloat("_AlphaToMask", 1f); m.doubleSidedGI = true;
            }
            m.enableInstancing = true;
            MaterialValidation.Validate(m);   // same bytes as the final MaterialValidation.SettleAll of BuildAll
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }
    }
}
