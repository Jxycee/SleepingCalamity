// Area 1 nature set intake (editor time, authority: none). The N01..N07 Poly Haven models (Art/Staging/area1-world/nature, moved into
// Assets/_Project/Content/Art/Town/Adopted/<code>_<name>/ in T08) already carry packed textures (<code>_albedo, _normal, _ms, _occ) and an FBX with meshes <code>_LOD0/1/2.
// This makes what the T11a / T12 intake made: M_<code> (URP Lit; alpha clip + two-sided for the flower clumps N06, N07) under Content/Materials/B1 and a prefab
// <folder>/<code>.prefab with a LODGroup (N01-N05, one renderer per LOD mesh; N06/N07 are single 72/96 triangle clumps). Pivot is the base centre (done in Blender).
// Run: Tools\unity.ps1 exec -Method SleepingCalamity.EditorTools.NatureIntake.Run
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class NatureIntake
    {
        const string Root = "Assets/_Project/Content/Art/Town/Adopted/";
        const string MatFolder = "Assets/_Project/Content/Materials/B1";
        static readonly string[] Folders = { "N01_cliff_slab", "N02_angular_rock", "N03_scree_cluster", "N04_stump", "N05_fallen_log", "N06_dandelion", "N07_meadow_spike" };
        static readonly float[][] LodHeights =
        {
            new[] { 0.30f, 0.10f, 0.02f }, new[] { 0.30f, 0.10f, 0.02f }, new[] { 0.30f, 0.10f, 0.02f }, new[] { 0.30f, 0.06f }, new[] { 0.30f, 0.06f }, null, null,
        };

        [MenuItem("SleepingCalamity/Art/Adopt Nature Set (N01-N07)")]
        public static void Run()
        {
            BuilderUtil.EnsureFolder(MatFolder);
            for (int i = 0; i < Folders.Length; i++) Adopt(Folders[i].Split('_')[0], Root + Folders[i], LodHeights[i]);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("SC NATURE intake done");
        }

        static void Adopt(string code, string dst, float[] lodHeights)
        {
            bool flower = lodHeights == null;
            string fbx = code + ".fbx", albedo = dst + "/" + code + "_albedo." + (flower ? "png" : "jpg"), normal = dst + "/" + code + "_normal." + (flower ? "png" : "jpg");
            string ms = dst + "/" + code + "_ms.png", occ = dst + "/" + code + "_occ.png";
            AssetDatabase.ImportAsset(dst + "/" + fbx, ImportAssetOptions.ForceUpdate);
            GameObject model = BeautyCornerArt.LoadModel(dst, fbx) ?? throw new InvalidOperationException("model did not import: " + dst + "/" + fbx);
            BeautyCornerArt.ImportTexture(albedo, srgb: true, normal: false, alphaTransparency: flower);
            BeautyCornerArt.ImportTexture(normal, srgb: false, normal: true, alphaTransparency: false);
            bool hasMs = File.Exists(BuilderUtil.Abs(ms)), hasOcc = File.Exists(BuilderUtil.Abs(occ));
            if (hasMs) BeautyCornerArt.ImportTexture(ms, srgb: false, normal: false, alphaTransparency: false);
            if (hasOcc) BeautyCornerArt.ImportTexture(occ, srgb: false, normal: false, alphaTransparency: false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MatFolder + "/M_" + code + ".mat", lit);
            m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo)); m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            if (hasMs) { m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            else { m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", 0.15f); }
            if (hasOcc) { m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occ)); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (flower)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
                m.SetFloat("_AlphaToMask", 1f); m.doubleSidedGI = true; m.enableInstancing = true;
            }
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);

            var rootGo = new GameObject(code);
            try
            {
                var inst = UnityEngine.Object.Instantiate(model); inst.name = "Model";
                foreach (var old in inst.GetComponentsInChildren<LODGroup>(true)) UnityEngine.Object.DestroyImmediate(old);
                inst.transform.SetParent(rootGo.transform, false);
                var rends = inst.GetComponentsInChildren<Renderer>(true).OrderBy(r => r.name, StringComparer.Ordinal).ToList();
                foreach (var r in rends)
                {
                    var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int s = 0; s < slots.Length; s++) slots[s] = m;
                    r.sharedMaterials = slots;
                    if (flower) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (!flower)
                {
                    var lods = new List<LOD>();
                    for (int l = 0; l < lodHeights.Length; l++)
                    {
                        var r = rends.FirstOrDefault(x => x.name.EndsWith("_LOD" + l, StringComparison.Ordinal)) ?? throw new InvalidOperationException(code + " has no LOD" + l + " (" + string.Join(",", rends.Select(x => x.name)) + ")");
                        lods.Add(new LOD(lodHeights[l], new[] { r }));
                    }
                    var lg = rootGo.AddComponent<LODGroup>(); lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds();
                }
                Bounds b = rends[0].bounds; foreach (var r in rends.Where(x => x.name.EndsWith("_LOD0", StringComparison.Ordinal) || flower)) b.Encapsulate(r.bounds);
                Debug.Log("SC NATURE " + code + " renderers=" + rends.Count + " size=(" + b.size.x.ToString("F2") + ", " + b.size.y.ToString("F2") + ", " + b.size.z.ToString("F2") + ") min.y=" + b.min.y.ToString("F3"));
                PrefabUtility.SaveAsPrefabAsset(rootGo, dst + "/" + code + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(rootGo); }
        }
    }
}
