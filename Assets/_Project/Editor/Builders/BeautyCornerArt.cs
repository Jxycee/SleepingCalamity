// Finds, imports and materials the beauty-corner art that the Blender sessions drop into Assets/_Project/Content/Art/Town/<Batch>/<Asset>/ (M9).
// Convention per asset folder: one FBX plus <name>_BaseColor.png, <name>_Normal.png, <name>_ORM.png (R occlusion, G roughness, B metallic; BaseColor alpha = foliage cut-out).
//
// ORM to URP Lit, the simplest correct path: URP Lit has no ORM input, it reads metallic from the R channel and smoothness from the A channel of one map
// (_MetallicGlossMap) and occlusion from the G channel of another (_OcclusionMap). So an editor step unpacks the ORM once into two derived PNGs under
// Assets/_Project/Content/Textures/BeautyCorner/<asset>/ : <asset>_ms.png (R = metallic, A = 1 - roughness) and <asset>_occ.png (R=G=B = occlusion).
// Their import settings (linear, alpha kept) come from LookTestTextureImporter by suffix. They are regenerated only when the ORM is newer. No shader graph needed.
// The FBX is imported with no animation, no cameras or lights, no materials (ours are assigned), no lightmap UVs, scale 1.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class BeautyCornerArt
    {
        public const string ArtRoot = "Assets/_Project/Content/Art/Town";
        const string DerivedRoot = "Assets/_Project/Content/Textures/BeautyCorner";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/BeautyCorner";

        public sealed class Found { public string Folder, Fbx, BaseColor, Normal, Orm; }

        static string Norm(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        // Looks for the row's art. Batch folder: its name starts with the batch code. Asset folder: Names match exactly (normalised), else contain.
        public static Found Find(BeautyCornerTable.Row row)
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot)) return null;
            foreach (string batchDir in AssetDatabase.GetSubFolders(ArtRoot))
            {
                string batchName = Path.GetFileName(batchDir);
                if (!StartsWithBatch(batchName, row.Batch)) continue;
                string best = null; int bestScore = 0;
                foreach (string assetDir in AssetDatabase.GetSubFolders(batchDir))
                {
                    string n = Norm(Path.GetFileName(assetDir));
                    foreach (string alias in row.Names)
                    {
                        string a = Norm(alias);
                        int score = n == a ? 2 : (n.Contains(a) ? 1 : 0);
                        if (score > bestScore) { bestScore = score; best = assetDir; }
                    }
                }
                if (best == null) continue;
                if (bestScore == 1) Debug.LogWarning("SC BEAUTY row " + row.Id + ": no exact folder name match under " + batchDir + ", using the contains-match " + best);
                Found f = Scan(best);
                if (f != null) return f;
            }
            return null;
        }

        static bool StartsWithBatch(string folder, string batch)
        {
            if (!folder.StartsWith(batch, System.StringComparison.OrdinalIgnoreCase)) return false;
            return folder.Length == batch.Length || !char.IsDigit(folder[batch.Length]);   // B1 must not match B10
        }

        static Found Scan(string dir)
        {
            var f = new Found { Folder = dir };
            foreach (string guid in AssetDatabase.FindAssets("", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(p)) continue;
                string lower = p.ToLowerInvariant();
                if (lower.EndsWith(".fbx") && f.Fbx == null) f.Fbx = p;
                else if (lower.EndsWith("_basecolor.png")) f.BaseColor = p;
                else if (lower.EndsWith("_normal.png")) f.Normal = p;
                else if (lower.EndsWith("_orm.png")) f.Orm = p;
            }
            return f.Fbx == null ? null : f;
        }

        // Applies import settings (only writes the importer when something differs) and builds the material. Returns the FBX asset to instantiate.
        public static GameObject Prepare(Found f, string assetName, bool foliage, out Material material)
        {
            ImportModel(f.Fbx);
            if (f.BaseColor != null) ImportTexture(f.BaseColor, srgb: true, normal: false, alphaTransparency: foliage);
            if (f.Normal != null) ImportTexture(f.Normal, srgb: false, normal: true, alphaTransparency: false);
            if (f.Orm != null) ImportTexture(f.Orm, srgb: false, normal: false, alphaTransparency: false);
            material = BuildMaterial(f, assetName, foliage);
            return AssetDatabase.LoadAssetAtPath<GameObject>(f.Fbx);
        }

        static void ImportModel(string path)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(); dirty = true; } }
            Set(mi.importAnimation, false, () => mi.importAnimation = false);
            Set(mi.animationType, ModelImporterAnimationType.None, () => mi.animationType = ModelImporterAnimationType.None);
            Set(mi.importCameras, false, () => mi.importCameras = false);
            Set(mi.importLights, false, () => mi.importLights = false);
            Set(mi.importBlendShapes, false, () => mi.importBlendShapes = false);
            Set(mi.generateSecondaryUV, false, () => mi.generateSecondaryUV = false);
            Set(mi.globalScale, 1f, () => mi.globalScale = 1f);
            Set(mi.materialImportMode, ModelImporterMaterialImportMode.None, () => mi.materialImportMode = ModelImporterMaterialImportMode.None);
            Set(mi.isReadable, false, () => mi.isReadable = false);
            if (dirty) mi.SaveAndReimport();
        }

        static void ImportTexture(string path, bool srgb, bool normal, bool alphaTransparency)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool dirty = ti.textureType != type || ti.sRGBTexture != srgb || ti.maxTextureSize < 4096 || ti.alphaIsTransparency != alphaTransparency
                || ti.mipmapEnabled != true || ti.anisoLevel < 8;
            if (!dirty) return;
            ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = 4096; ti.mipmapEnabled = true;
            ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear;
            ti.alphaSource = alphaTransparency ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = alphaTransparency;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        static Material BuildMaterial(Found f, string assetName, bool foliage)
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + assetName + ".mat", lit);
            m.SetColor("_BaseColor", Color.white);
            if (f.BaseColor != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(f.BaseColor));
            if (f.Normal != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(f.Normal)); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (f.Orm != null)
            {
                string dir = DerivedRoot + "/" + assetName;
                string ms = UnpackOrm(f.Orm, dir, assetName, out string occ);
                m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ms)); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(occ)); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP");
            }
            if (foliage)   // alpha-clip cut-out from the BaseColor alpha, double sided
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = 2450;
            }
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // ORM -> (_ms: R metallic, A smoothness = 1 - roughness) + (_occ: occlusion in RGB). Skipped when both outputs are newer than the ORM.
        static string UnpackOrm(string ormPath, string dir, string assetName, out string occPath)
        {
            BuilderUtil.EnsureFolder(dir);
            string msPath = dir + "/" + assetName + "_ms.png"; occPath = dir + "/" + assetName + "_occ.png";
            string srcAbs = BuilderUtil.Abs(ormPath), msAbs = BuilderUtil.Abs(msPath), occAbs = BuilderUtil.Abs(occPath);
            bool fresh = File.Exists(msAbs) && File.Exists(occAbs)
                && File.GetLastWriteTimeUtc(msAbs) >= File.GetLastWriteTimeUtc(srcAbs) && File.GetLastWriteTimeUtc(occAbs) >= File.GetLastWriteTimeUtc(srcAbs);
            if (!fresh)
            {
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (!ImageConversion.LoadImage(src, File.ReadAllBytes(srcAbs), false)) throw new System.IO.InvalidDataException("Cannot read " + ormPath);
                Color32[] px = src.GetPixels32();
                var ms = new Color32[px.Length]; var occ = new Color32[px.Length];
                for (int i = 0; i < px.Length; i++)
                {
                    ms[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g));
                    occ[i] = new Color32(px[i].r, px[i].r, px[i].r, 255);
                }
                WritePng(msAbs, src.width, src.height, ms); WritePng(occAbs, src.width, src.height, occ);
                Object.DestroyImmediate(src);
            }
            AssetDatabase.ImportAsset(msPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(occPath, ImportAssetOptions.ForceUpdate);
            return msPath;
        }

        static void WritePng(string abs, int w, int h, Color32[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels32(px);
            File.WriteAllBytes(abs, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }
    }
}
