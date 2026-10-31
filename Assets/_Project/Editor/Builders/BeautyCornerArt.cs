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
        public const int TextureCap = 2048;   // user rule 2026-10-30: the game targets 1440p, so no corner or lantern texture is imported above 2048 px
        static readonly string[] CapFolders = { "Assets/_Project/Content/Art", "Assets/_Project/Content/Textures/BeautyCorner" };
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

        public static Found FindIn(string folder) => Scan(folder);

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

        // ---- helpers for rows that need more than one folder or several materials (BeautyCornerGround, the facade) ----

        // The asset folder named exactly assetFolder (case-insensitive) under the batch folder, or null.
        public static string FindFolder(string batch, string assetFolder)
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot)) return null;
            foreach (string batchDir in AssetDatabase.GetSubFolders(ArtRoot))
            {
                if (!StartsWithBatch(Path.GetFileName(batchDir), batch)) continue;
                foreach (string assetDir in AssetDatabase.GetSubFolders(batchDir))
                    if (string.Equals(Path.GetFileName(assetDir), assetFolder, System.StringComparison.OrdinalIgnoreCase)) return assetDir;
            }
            return null;
        }

        public static string FirstFbx(string folder) =>
            AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.ToLowerInvariant().EndsWith(".fbx")).OrderBy(p => p).FirstOrDefault();

        // Sets the max import size of every texture under Content/Art and the derived BeautyCorner maps to TextureCap (only writes an importer that differs).
        public static void EnforceTextureCap()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", CapFolders.Where(AssetDatabase.IsValidFolder).ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null || ti.maxTextureSize <= TextureCap) continue;
                ti.maxTextureSize = TextureCap; ti.SaveAndReimport(); changed++;
            }
            Debug.Log("SC BEAUTY texture cap " + TextureCap + ": lowered " + changed + " importers");
        }

        // The ground under and around the cobble patch: B6's 2 m FarCobble tileable (UV metres / 2) with a low-frequency macro breakup so the repeat does not show:
        // a tileable grey noise (SurroundMacro.png, neutral 0.5) in URP Lit's detail-albedo slot (x2 mode), tiled once per MacroMetres. Null when the tileable is missing.
        public const float MacroMetres = 23f, FarTileMetres = 2f;
        public static Material SurroundGround()
        {
            string folder = FindFolder("B6", "FarCobble");
            if (folder == null) return null;
            Material m = MaterialFromSet(folder, "CobbleFar", "SurroundGround");
            string macroPath = "Assets/_Project/Content/Textures/BeautyCorner/Surround/SurroundMacro.png";
            var macro = AssetDatabase.LoadAssetAtPath<Texture2D>(macroPath);
            if (macro == null) return m;
            m.SetTexture("_DetailAlbedoMap", macro); m.SetFloat("_DetailAlbedoMapScale", 1f); m.EnableKeyword("_DETAIL_MULX2");
            m.SetTextureScale("_DetailAlbedoMap", Vector2.one * (FarTileMetres / MacroMetres));   // detail uv = base uv (1 per 2 m) x this
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        // The cellar lantern (B11) is only imported and given a material for now, it is not placed in the town corner.
        public static void EnsureCellarLantern()
        {
            const string folder = "Assets/_Project/Content/Art/Cellar/B11/CellarLantern";
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (LoadModel(folder, "CellarLantern.fbx") == null) return;   // textures: MaterialFromSet caps them at TextureCap
            MaterialFromSet(folder, "CellarLantern", "CellarLantern", emission: "_Emission.png");
        }

        // The FBX at folder/fileName (imported with the standard settings), or null when the file is missing.
        public static GameObject LoadModel(string folder, string fileName, bool keepMaterialSlots = false)
        {
            string path = folder + "/" + fileName;
            if (AssetImporter.GetAtPath(path) == null) return null;
            ImportModel(path, keepMaterialSlots);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // Material M_<matName> from folder/<prefix>_BaseColor.png, _Normal.png, _ORM.png (any may be missing). Same ORM unpack as the row materials.
        public static Material MaterialFromSet(string folder, string prefix, string matName, bool foliage = false, string emission = null)
        {
            var f = new Found { Folder = folder };
            string P(string suffix) { string p = folder + "/" + prefix + suffix; return AssetImporter.GetAtPath(p) != null ? p : null; }
            f.BaseColor = P("_BaseColor.png"); f.Normal = P("_Normal.png"); f.Orm = P("_ORM.png");
            if (f.BaseColor != null) ImportTexture(f.BaseColor, srgb: true, normal: false, alphaTransparency: foliage);
            if (f.Normal != null) ImportTexture(f.Normal, srgb: false, normal: true, alphaTransparency: false);
            if (f.Orm != null) ImportTexture(f.Orm, srgb: false, normal: false, alphaTransparency: false);
            Material m = BuildMaterial(f, matName, foliage);
            string em = emission != null ? P(emission) : null;
            if (em != null)
            {
                ImportTexture(em, srgb: true, normal: false, alphaTransparency: false);
                m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(em)); m.SetColor("_EmissionColor", Color.white); m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
            }
            return m;
        }

        // The one M_CrackWeeds material every foliage scatter shares (the corner's weeds, the square's edge tufts and weeds). The five B9 folders hold identical
        // atlases under different GUIDs, so building it from "the first folder that loaded" made the textures flip with call order and rewrote the .mat every build:
        // it always comes from Weed_A. Null when that folder is missing.
        public const string SharedFoliageFolder = "Weed_A";
        public static Material CrackWeedsMaterial()
        {
            string folder = FindFolder("B9", SharedFoliageFolder);
            return folder == null ? null : MaterialFromSet(folder, "T_B9_Foliage", "CrackWeeds", foliage: true);
        }

        // ---- the facade's multi-material setup (B4: shared B1 tileables + B3 decals, wear in the vertex colour) ----
        const string WearShaderName = "SleepingCalamity/WearLit";
        // slot name in Facade.fbx -> LookTest material (the imported B1/B3 tileables), tint (linear, as the generator's tile_mat tint), decal alpha (0 = opaque).
        // The B4 neighbour variants share the facade slots; their plaster is tinted and their shutters and doors use WearLit's paint mode (SC_PAINT) with the variant's paint colour.
        public static bool IsNeighbour(string folderName) => folderName != null && BeautyCornerTable.NeighbourLooks.ContainsKey(folderName);
        const float PaintLum = 0.17f;   // the generator's PAINT_LUM: mean linear luminance of the painted_wood albedo

        public static Dictionary<string, Material> BuildNeighbourMaterials(string folderName)
        {
            var look = BeautyCornerTable.NeighbourLooks[folderName];
            return BuildNeighbourMaterials(folderName, look.Plaster, look.Paint);
        }

        // Same, for any B4-kit house with its own plaster tint and shutter / door paint (linear): the neighbours and the townhouse kit family.
        public static Dictionary<string, Material> BuildNeighbourMaterials(string folderName, Color plasterTint, Color paintLinear)
        {
            Dictionary<string, Material> map = BuildFacadeMaterials();
            var look = (Plaster: plasterTint, Paint: paintLinear);
            Shader wear = Shader.Find(WearShaderName);
            Material plaster = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + folderName + "_FK_Plaster.mat", wear);
            plaster.CopyPropertiesFromMaterial(map["FK_Plaster"]);
            foreach (var k in map["FK_Plaster"].enabledKeywords) plaster.EnableKeyword(k.name);
            plaster.SetColor("_BaseColor", new Color(look.Plaster.r, look.Plaster.g, look.Plaster.b, 1f).gamma);
            plaster.SetColor("_Color", plaster.GetColor("_BaseColor")); plaster.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(plaster); map["FK_Plaster"] = plaster;
            foreach (string slot in new[] { "FK_Shutter", "FK_DoorPaint" })
            {
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + folderName + "_" + slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(map[slot]);
                foreach (var k in map[slot].enabledKeywords) m.EnableKeyword(k.name);
                m.EnableKeyword("_SC_PAINT");
                m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
                m.SetVector("_SpecColor", new Vector4(look.Paint.r / PaintLum, look.Paint.g / PaintLum, look.Paint.b / PaintLum, 1f));   // raw linear, SetVector skips the colour-space conversion
                m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m); map[slot] = m;
            }
            AssetDatabase.SaveAssets();
            return map;
        }

        // The B4 bell tower (BellTower/, sc_bell_tower.py) reuses the facade slots plus three of its own: grey rubble and the clock face are tinted B1 tileables
        // (the generator's tints); the bronze is a flat warm colour, brighter than the generator's iron x (2.1, 1.5, 0.75) (about (0.17, 0.10, 0.05)), so the bell
        // reads warm at the far-row distance. Flat like FK_Glass: a raw HDR tint on the iron tileable wrote different _Color bytes with and without graphics.
        public const string TowerFolder = "BellTower";
        public static bool IsTower(string folderName) => string.Equals(folderName, TowerFolder, System.StringComparison.OrdinalIgnoreCase);

        public static Dictionary<string, Material> BuildTowerMaterials()
        {
            Dictionary<string, Material> map = BuildFacadeMaterials();
            Shader wear = Shader.Find(WearShaderName);
            Material Tinted(string slot, string source, Color tint, float bumpScale)
            {
                Material src = LookTestBuilder.Mat(source);
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                m.SetColor("_BaseColor", tint.gamma); m.SetColor("_Color", m.GetColor("_BaseColor"));
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", src.GetFloat("_BumpScale") * bumpScale);
                m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m);
                return m;
            }
            map["BT_Rubble"] = Tinted("BT_Rubble", "sandstone", new Color(0.62f, 0.60f, 0.57f), 1.4f);   // grey so the pale quoins read
            map["BT_ClockFace"] = Tinted("BT_ClockFace", "plaster", new Color(0.96f, 0.93f, 0.86f), 0.4f);
            map["BT_Bronze"] = Flat("BT_Bronze", new Color(0.55f, 0.36f, 0.16f), 0.45f, wear);
            AssetDatabase.SaveAssets();
            return map;
        }

        static readonly (string Slot, string Source, Color Tint, float DecalAlpha)[] FacadeSlots =
        {
            ("FK_Plaster", "plaster", Color.white, 0f), ("FK_Oak", "oak_timber", Color.white, 0f), ("FK_OakDoor", "oak_timber", new Color(1.35f, 1.25f, 1.15f), 0f),
            ("FK_Sandstone", "sandstone", Color.white, 0f), ("FK_PlinthJoint", "sandstone", new Color(0.30f, 0.28f, 0.24f), 0f), ("FK_ChimneyStone", "sandstone", new Color(0.80f, 0.74f, 0.68f), 0f), ("FK_RoofTile", "roof_tile", Color.white, 0f),
            ("FK_Shutter", "painted_wood", new Color(0.57f, 0.73f, 0.28f), 0f), ("FK_DoorPaint", "painted_wood", new Color(0.50f, 0.66f, 0.25f), 0f),
            ("FK_Iron", "iron", Color.white, 0f),
            ("FK_Decal_crack", "crack", Color.white, 0.9f), ("FK_Decal_drip", "drip", Color.white, 0.35f), ("FK_Decal_moss", "moss", Color.white, 1.0f),
        };

        // slot name (".001" suffixes stripped) -> material. Opaque slots use SleepingCalamity/WearLit so the vertex colour "wear" darkens, mosses and polishes them.
        public static Dictionary<string, Material> BuildFacadeMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wearShader = Shader.Find(WearShaderName) ?? throw new System.InvalidOperationException("Shader not found: " + WearShaderName);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var map = new Dictionary<string, Material>();
            foreach (var s in FacadeSlots)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", s.DecalAlpha > 0f ? lit : wearShader);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                if (s.DecalAlpha > 0f) AlphaBlend(m);
                // The generator multiplies the tileable by Tint in linear space; Material.SetColor of a non-HDR colour is read as sRGB, so hand it the gamma value.
                Color tint = s.DecalAlpha > 0f ? new Color(1f, 1f, 1f, s.DecalAlpha) : s.Tint;
                m.SetColor("_BaseColor", s.DecalAlpha > 0f ? tint : new Color(Mathf.Clamp01(tint.r), Mathf.Clamp01(tint.g), Mathf.Clamp01(tint.b), 1f).gamma);
                if (s.Tint.r > 1f) m.SetColor("_BaseColor", Color.white);   // oak_lt brightens (1.35 x): the tileable is already light, keep it untinted instead of clipping
                // Set explicitly what a graphics-device import settles on, so a -nographics run (check, tests) leaves the same bytes: _Color mirrors _BaseColor and the
                // motion-vector pass stays enabled (a no-graphics import disables it and zeroes the _Color mirror).
                m.SetColor("_Color", m.GetColor("_BaseColor")); if (s.DecalAlpha <= 0f) m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m);
                map[s.Slot] = m;
            }
            map["FK_Glass"] = Flat("FK_Glass", new Color(0.05f, 0.075f, 0.095f), 0.9f, wearShader);
            map["FK_Leaf"] = Flat("FK_Leaf", new Color(0.07f, 0.13f, 0.025f), 0.4f, wearShader);
            map["FK_Bloom"] = Flat("FK_Bloom", new Color(0.45f, 0.04f, 0.03f), 0.5f, wearShader);
            AssetDatabase.SaveAssets();
            return map;
        }

        // Straight-alpha blend (SrcAlpha, OneMinusSrcAlpha), no depth write or shadows. Not the LookTest premultiply state: Unity's material validation strips its
        // _ALPHAPREMULTIPLY_ON keyword on the source material, and a premultiply blend without the keyword draws the decal as a pale rectangle.
        internal static void AlphaBlend(Material m)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 10f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_AlphaClip", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("ShadowCaster", false); m.renderQueue = 3000;
            // URP "preserve specular" adds the specular and sky reflection at full strength even where the alpha is 0, which draws every decal quad as a pale rectangle.
            m.SetFloat("_BlendModePreserveSpecular", 0f); m.SetFloat("_Smoothness", 0f); m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f); m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetTexture("_MetallicGlossMap", null); m.DisableKeyword("_METALLICSPECGLOSSMAP");
        }

        internal static Material Flat(string slot, Color linear, float smooth, Shader shader)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + slot + ".mat", shader);
            m.SetColor("_BaseColor", linear.gamma); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void ImportModel(string path) => ImportModel(path, false);

        // keepMaterialSlots: import the FBX's materials into the prefab (named by the FBX material) so the builder can read the slot names and assign its own per slot.
        static void ImportModel(string path, bool keepMaterialSlots)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(); dirty = true; } }
            if (keepMaterialSlots)
            {
                Set(mi.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, () => mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard);
                Set(mi.materialName, ModelImporterMaterialName.BasedOnMaterialName, () => mi.materialName = ModelImporterMaterialName.BasedOnMaterialName);
                Set(mi.materialSearch, ModelImporterMaterialSearch.Local, () => mi.materialSearch = ModelImporterMaterialSearch.Local);
                Set(mi.materialLocation, ModelImporterMaterialLocation.InPrefab, () => mi.materialLocation = ModelImporterMaterialLocation.InPrefab);
            }
            Set(mi.importAnimation, false, () => mi.importAnimation = false);
            Set(mi.animationType, ModelImporterAnimationType.None, () => mi.animationType = ModelImporterAnimationType.None);
            Set(mi.importCameras, false, () => mi.importCameras = false);
            Set(mi.importLights, false, () => mi.importLights = false);
            Set(mi.importBlendShapes, false, () => mi.importBlendShapes = false);
            Set(mi.generateSecondaryUV, false, () => mi.generateSecondaryUV = false);
            Set(mi.globalScale, 1f, () => mi.globalScale = 1f);
            if (!keepMaterialSlots) Set(mi.materialImportMode, ModelImporterMaterialImportMode.None, () => mi.materialImportMode = ModelImporterMaterialImportMode.None);
            Set(mi.isReadable, false, () => mi.isReadable = false);
            if (dirty) mi.SaveAndReimport();
        }

        internal static void ImportTexture(string path, bool srgb, bool normal, bool alphaTransparency)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool dirty = ti.textureType != type || ti.sRGBTexture != srgb || ti.maxTextureSize != TextureCap || ti.alphaIsTransparency != alphaTransparency
                || ti.mipmapEnabled != true || ti.anisoLevel < 8;
            if (!dirty) return;
            ti.textureType = type; ti.sRGBTexture = srgb; ti.maxTextureSize = TextureCap; ti.mipmapEnabled = true;
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
                m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");   // leaves in shade took a blue sheen from the sky probe
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
            // Rewrite only when the bytes differ. A timestamp test cannot be trusted: git restores the committed derived maps and the source ORM in the same second.
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(src, File.ReadAllBytes(srcAbs), false)) throw new System.IO.InvalidDataException("Cannot read " + ormPath);
            Color32[] px = src.GetPixels32();
            var ms = new Color32[px.Length]; var occ = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                ms[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g));
                occ[i] = new Color32(px[i].r, px[i].r, px[i].r, 255);
            }
            bool wroteMs = WritePng(msAbs, src.width, src.height, ms), wroteOcc = WritePng(occAbs, src.width, src.height, occ);
            Object.DestroyImmediate(src);
            if (!wroteMs && !wroteOcc && AssetImporter.GetAtPath(msPath) != null && AssetImporter.GetAtPath(occPath) != null) return msPath;
            AssetDatabase.ImportAsset(msPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(occPath, ImportAssetOptions.ForceUpdate);
            return msPath;
        }

        // Returns true when the file was written (new or different bytes).
        static bool WritePng(string abs, int w, int h, Color32[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels32(px);
            byte[] bytes = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            if (File.Exists(abs) && File.ReadAllBytes(abs).SequenceEqual(bytes)) return false;
            File.WriteAllBytes(abs, bytes);
            return true;
        }
    }
}
