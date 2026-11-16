// Import setup for the rigged cellar enemies (Slime, RatKing, Broom FBX under Content/Art/Enemies), applied from code and idempotent: an importer is
// only re-imported when a setting differs, and an AnimatorController is only rebuilt when its signature changes (Unity gives every state a random id on
// each rebuild, so a rebuild in place would rewrite the file).
//   Model        Generic rig built from the file, no root motion, compression off, no cameras/lights/blend shapes.
//   Clips        one per FBX take, named by the suffix after '|' ("Armature|Attack_slime_hop" -> "Attack_slime_hop"); Idle and Move loop, the rest play once.
//   Materials    the FBX's own material names (M_Slime_*, RK_*, M_Broom_*) are remapped to URP Lit materials built here under Content/Materials/Enemies.
//                Broom maps: BaseColor + Normal as is, ORM repacked to a metallic/occlusion/smoothness map. Glass is transparent, ember is emissive amber.
//   Controller   <Enemy>.controller beside the FBX: a state per clip (Idle default; every Attack_* state at speed 0, placed by EnemyAnimatorDriver).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class EnemyArtBuilder
    {
        public const int Version = 1;
        public const string Root = "Assets/_Project/Content/Art/Enemies";
        public const string MaterialsFolder = "Assets/_Project/Content/Materials/Enemies";

        public sealed class Spec
        {
            public EnemyKind Kind;
            public string Name;
            public string Folder => Root + "/" + Name;
            public string Fbx => Folder + "/" + Name + ".fbx";
            public string Controller => Folder + "/" + Name + ".controller";
        }

        public static readonly Spec[] Specs =
        {
            new Spec { Kind = EnemyKind.Slime, Name = "Slime" },
            new Spec { Kind = EnemyKind.RatKing, Name = "RatKing" },
            new Spec { Kind = EnemyKind.Broom, Name = "Broom" },
        };

        public static Spec SpecOf(EnemyKind kind) => Specs.FirstOrDefault(s => s.Kind == kind);

        static readonly Color Ember = new Color(1.0f, 0.42f, 0.08f);
        const float EmberStrength = 2.5f;
        static readonly Color FaintEmission = new Color(0.003f, 0.003f, 0.003f);   // keeps the _EMISSION keyword on (ShaderGUI validation drops it for black); a block adds the hit tint

        // base colour, metallic, smoothness, emission (HDR, null = none), alpha (< 1 = transparent). Names are the FBX materials; unknown ones fall back to the FBX's colour.
        struct Look { public Color Base; public float Metal, Smooth; public Color? Emit; public float Alpha; }
        static Look L(float r, float g, float b, float metal, float smooth, Color? emit = null, float alpha = 1f) =>
            new Look { Base = new Color(r, g, b), Metal = metal, Smooth = smooth, Emit = emit, Alpha = alpha };

        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            { "M_Slime_Glass", L(0.93f, 0.95f, 0.88f, 0f, 0.96f, null, 0.28f) },
            { "M_Slime_Jam", L(0.22f, 0.012f, 0.03f, 0f, 0.55f) },
            { "M_Slime_Lid", L(0.42f, 0.31f, 0.16f, 0.9f, 0.60f) },
            { "M_Slime_Eye", L(0.96f, 0.92f, 0.80f, 0f, 0.90f) },
            { "M_Slime_Pupil", L(0.03f, 0.02f, 0.025f, 0f, 0.95f) },
            { "M_Slime_Iris", L(0.62f, 0.34f, 0.07f, 0f, 0.88f) },
            { "M_Slime_Fruit", L(0.46f, 0.035f, 0.05f, 0f, 0.78f) },
            { "M_Slime_Label", L(0.80f, 0.70f, 0.50f, 0f, 0.15f) },
            { "M_Slime_Puddle", L(0.16f, 0.008f, 0.02f, 0f, 0.94f) },
            { "RK_Fur", L(0.17f, 0.145f, 0.125f, 0f, 0.10f) },
            { "RK_Skin", L(0.46f, 0.27f, 0.26f, 0f, 0.45f) },
            { "RK_Cloak", L(0.27f, 0.26f, 0.10f, 0f, 0.05f) },
            { "RK_Rope", L(0.40f, 0.30f, 0.17f, 0f, 0.10f) },
            { "RK_Brass", L(0.55f, 0.38f, 0.12f, 1f, 0.65f) },
            { "RK_RustTin", L(0.42f, 0.42f, 0.40f, 0.9f, 0.45f) },
            { "RK_Spoon", L(0.62f, 0.62f, 0.60f, 1f, 0.68f) },
            { "RK_EmberEye", L(0.05f, 0.02f, 0.01f, 0f, 0.70f, Ember) },
            { "RK_Bone", L(0.78f, 0.70f, 0.52f, 0f, 0.60f) },
            { "RK_eye", L(0.05f, 0.02f, 0.01f, 0f, 0.70f, Ember) },
            { "RK_pupil", L(0.02f, 0.01f, 0.01f, 0f, 0.80f) },
            { "M_Broom_Ember", L(0.05f, 0.02f, 0.01f, 0f, 0.65f, Ember) },
        };

        public static void Build()
        {
            BuilderUtil.EnsureFolder(MaterialsFolder);
            foreach (Spec spec in Specs)
            {
                if (!File.Exists(BuilderUtil.Abs(spec.Fbx))) { Debug.LogWarning("SC ENEMYART missing " + spec.Fbx); continue; }
                BuildOne(spec);
            }
            AssetDatabase.SaveAssets();
        }

        static void BuildOne(Spec spec)
        {
            AssetDatabase.ImportAsset(spec.Fbx, ImportAssetOptions.ForceSynchronousImport);
            ModelImporter mi = AssetImporter.GetAtPath(spec.Fbx) as ModelImporter;
            if (mi == null) throw new InvalidOperationException("No ModelImporter for " + spec.Fbx);

            // 1. model + material mode, imported once so the file's material names exist
            bool dirty = false;
            Set(ref dirty, mi.animationType, ModelImporterAnimationType.Generic, v => mi.animationType = v);
            Set(ref dirty, mi.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => mi.avatarSetup = v);
            Set(ref dirty, mi.importAnimation, true, v => mi.importAnimation = v);
            Set(ref dirty, mi.animationCompression, ModelImporterAnimationCompression.Off, v => mi.animationCompression = v);
            Set(ref dirty, mi.importCameras, false, v => mi.importCameras = v);
            Set(ref dirty, mi.importLights, false, v => mi.importLights = v);
            Set(ref dirty, mi.importBlendShapes, false, v => mi.importBlendShapes = v);
            Set(ref dirty, mi.optimizeGameObjects, false, v => mi.optimizeGameObjects = v);   // keep the bone transforms: the presentation code looks tell parts up by name
            Set(ref dirty, mi.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, v => mi.materialImportMode = v);
            Commit(mi, spec.Fbx, dirty);

            // 2. materials, remapped by FBX material name
            ConfigureTextures(spec);
            var names = new SortedSet<string>(StringComparer.Ordinal);
            var embedded = new Dictionary<string, Material>();
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(spec.Fbx).OfType<Material>()) { names.Add(m.name); embedded[m.name] = m; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx);
            if (model != null)   // the materials the meshes use now (already remapped ones keep the file's name)
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials) if (m != null) names.Add(m.name);
            dirty = false;
            foreach (var kv in mi.GetExternalObjectMap().ToArray())   // a regenerated FBX can drop or rename materials: forget the old remaps
                if (kv.Key.type == typeof(Material) && !names.Contains(kv.Key.name)) { mi.RemoveRemap(kv.Key); dirty = true; }
            foreach (string name in names)
            {
                embedded.TryGetValue(name, out Material source);
                Material mat = EnsureMaterial(spec, name, source);
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                if (!mi.GetExternalObjectMap().TryGetValue(id, out UnityEngine.Object current) || current != mat) { mi.AddRemap(id, mat); dirty = true; }
            }
            Commit(mi, spec.Fbx, dirty);

            // 3. clips: one per take, renamed to the suffix after '|', Idle and Move loop
            ModelImporterClipAnimation[] defaults = mi.defaultClipAnimations;
            if (defaults != null && defaults.Length > 0)
            {
                bool anyBar = defaults.Any(c => c.takeName.Contains('|'));
                var next = new List<ModelImporterClipAnimation>();
                foreach (ModelImporterClipAnimation clip in defaults)
                {
                    if (anyBar && !clip.takeName.Contains('|')) continue;   // the exporter's stray default take
                    ModelImporterClipAnimation c = clip;
                    c.name = ClipName(c.takeName);
                    c.loopTime = c.loopPose = c.name == "Idle" || c.name == "Move";
                    c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = false;
                    c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                    c.wrapMode = c.loopTime ? UnityEngine.WrapMode.Loop : UnityEngine.WrapMode.Once;
                    next.Add(c);
                }
                next = next.OrderBy(c => c.name, StringComparer.Ordinal).ToList();
                if (!SameClips(mi.clipAnimations, next)) { mi.clipAnimations = next.ToArray(); Commit(mi, spec.Fbx, true); }
            }

            BuildController(spec);
        }

        public static string ClipName(string takeName)
        {
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }

        static bool SameClips(ModelImporterClipAnimation[] have, List<ModelImporterClipAnimation> want)
        {
            if (have == null || have.Length != want.Count) return false;
            foreach (ModelImporterClipAnimation w in want)
            {
                ModelImporterClipAnimation h = have.FirstOrDefault(c => c.takeName == w.takeName);
                if (h == null || h.name != w.name || h.loopTime != w.loopTime || h.loopPose != w.loopPose || h.firstFrame != w.firstFrame || h.lastFrame != w.lastFrame) return false;
            }
            return true;
        }

        static void Set<T>(ref bool dirty, T have, T want, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(have, want)) return;
            apply(want);
            dirty = true;
        }

        static void Commit(AssetImporter importer, string path, bool dirty)
        {
            if (!dirty) return;
            importer.SaveAndReimport();
            Debug.Log("SC ENEMYART imported " + path);
        }

        // ---- materials ----

        static void ConfigureTextures(Spec spec)
        {
            foreach (string file in Directory.GetFiles(BuilderUtil.Abs(spec.Folder), "*.png").OrderBy(f => f, StringComparer.Ordinal))
            {
                string path = spec.Folder + "/" + Path.GetFileName(file);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
                if (ti == null) continue;
                bool dirty = false;
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool data = path.EndsWith("_ORM.png", StringComparison.Ordinal) || path.EndsWith("_MOS.png", StringComparison.Ordinal);
                Set(ref dirty, ti.textureType, normal ? TextureImporterType.NormalMap : TextureImporterType.Default, v => ti.textureType = v);
                Set(ref dirty, ti.sRGBTexture, !(normal || data), v => ti.sRGBTexture = v);
                Set(ref dirty, ti.maxTextureSize, 1024, v => ti.maxTextureSize = v);
                Set(ref dirty, ti.mipmapEnabled, true, v => ti.mipmapEnabled = v);
                Commit(ti, path, dirty);
            }
        }

        // The texture set a material uses: "M_Broom_Iron" -> Broom_Iron, "M_RatBody" -> RatKing_Body (the set whose key matches ignoring case, underscores and "king").
        static string TextureStem(Spec spec, string material)
        {
            string stem = material.StartsWith("M_", StringComparison.Ordinal) ? material.Substring(2) : material;
            string Norm(string x) => x.Replace("_", "").Replace("King", "").ToLowerInvariant();
            if (File.Exists(BuilderUtil.Abs(spec.Folder + "/" + stem + "_BaseColor.png"))) return stem;
            foreach (string file in Directory.GetFiles(BuilderUtil.Abs(spec.Folder), "*_BaseColor.png").OrderBy(f => f, StringComparer.Ordinal))
            {
                string t = Path.GetFileName(file);
                t = t.Substring(0, t.Length - "_BaseColor.png".Length);
                if (Norm(t) == Norm(stem)) return t;
            }
            return stem;
        }

        static Texture2D Tex(Spec spec, string stem, string key) => AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Folder + "/" + stem + "_" + key + ".png");

        // The ORM map (R occlusion, G roughness, B metallic) as the packed map URP Lit reads: R metallic, G occlusion, A smoothness. Written once per ORM change.
        static Texture2D PackedMos(Spec spec, string stem)
        {
            string orm = BuilderUtil.Abs(spec.Folder + "/" + stem + "_ORM.png");
            if (!File.Exists(orm)) return null;
            string mosPath = spec.Folder + "/" + stem + "_MOS.png";
            string mosAbs = BuilderUtil.Abs(mosPath);
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!src.LoadImage(File.ReadAllBytes(orm), false)) return null;
                Color32[] px = src.GetPixels32();
                var packed = new Color32[px.Length];
                for (int i = 0; i < px.Length; i++) packed[i] = new Color32(px[i].b, px[i].r, 0, (byte)(255 - px[i].g));
                var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, true);
                dst.SetPixels32(packed);
                byte[] png = dst.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(dst);
                if (!File.Exists(mosAbs) || !File.ReadAllBytes(mosAbs).SequenceEqual(png))
                {
                    File.WriteAllBytes(mosAbs, png);
                    AssetDatabase.ImportAsset(mosPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(src); }
            ConfigureTextures(spec);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(mosPath);
        }

        static Material EnsureMaterial(Spec spec, string name, Material source)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new InvalidOperationException("Shader not found: Universal Render Pipeline/Lit");
            string path = MaterialsFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;

            string stem = TextureStem(spec, name);
            Texture2D baseMap = Tex(spec, stem, "BaseColor");
            Look look;
            if (!Looks.TryGetValue(name, out look))
            {
                Color c = new Color(0.6f, 0.6f, 0.6f);
                if (source != null) c = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : c;
                bool ember = name.IndexOf("ember", StringComparison.OrdinalIgnoreCase) >= 0;
                look = L(c.r, c.g, c.b, 0f, 0.4f, ember ? Ember : (Color?)null);
                if (baseMap == null && !Looks.ContainsKey(name)) Debug.LogWarning("SC ENEMYART no look for material " + name + ": using the FBX colour");
            }

            m.SetColor("_BaseColor", baseMap != null ? Color.white : look.Base);
            m.SetTexture("_BaseMap", baseMap);
            Texture2D normalMap = Tex(spec, stem, "Normal");
            m.SetTexture("_BumpMap", normalMap);
            if (normalMap != null) { m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1f); } else m.DisableKeyword("_NORMALMAP");
            Texture2D mos = baseMap != null ? PackedMos(spec, stem) : null;
            m.SetTexture("_MetallicGlossMap", mos);
            m.SetTexture("_OcclusionMap", mos);
            if (mos != null)
            {
                m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.EnableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_OcclusionStrength", 1f);
            }
            else
            {
                m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.DisableKeyword("_OCCLUSIONMAP");
                m.SetFloat("_Metallic", look.Metal); m.SetFloat("_Smoothness", look.Smooth);
            }

            Color emit = look.Emit.HasValue ? look.Emit.Value * EmberStrength : FaintEmission;
            m.SetColor("_EmissionColor", emit);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;

            bool transparent = look.Alpha < 1f;
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.SrcAlpha : 1f);
            m.SetFloat("_DstBlend", transparent ? (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha : 0f);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (transparent) { m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000; m.SetColor("_BaseColor", new Color(look.Base.r, look.Base.g, look.Base.b, look.Alpha)); }
            else { m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1; }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- controller ----

        public static AnimationClip[] Clips(Spec spec) =>
            AssetDatabase.LoadAllAssetsAtPath(spec.Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(c => c.name, StringComparer.Ordinal).ToArray();

        static void BuildController(Spec spec)
        {
            AnimationClip[] clips = Clips(spec);
            if (clips.Length == 0) { Debug.LogWarning("SC ENEMYART no clips in " + spec.Fbx); return; }
            string signature = "v" + Version + "|" + string.Join(",", clips.Select(c => c.name + ":" + c.length.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ":" + c.isLooping));
            AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.Controller);
            if (ctrl != null && AssetImporter.GetAtPath(spec.Controller) is AssetImporter existing && existing.userData == signature) return;
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(spec.Controller);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            foreach (ChildAnimatorState s in sm.states.ToArray()) sm.RemoveState(s.state);
            var idle = clips.FirstOrDefault(c => c.name == EnemyAnimMap.Idle) ?? clips[0];
            int i = 0;
            foreach (AnimationClip clip in clips)
            {
                AnimatorState st = sm.AddState(clip.name, new Vector3(260f, 60f * i++, 0f));
                st.motion = clip;
                st.writeDefaultValues = true;
                st.speed = clip.name.StartsWith(EnemyAnimMap.AttackPrefix, StringComparison.Ordinal) ? 0f : 1f;   // attack states are placed by the combat clock
                if (clip == idle) sm.defaultState = st;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            AssetImporter importer = AssetImporter.GetAtPath(spec.Controller);
            if (importer != null) { importer.userData = signature; importer.SaveAndReimport(); }
            Debug.Log("SC ENEMYART controller " + spec.Controller + " states=" + clips.Length);
        }

        // The model for a kind, or null when its FBX is not in the project.
        public static GameObject Model(EnemyKind kind)
        {
            Spec spec = SpecOf(kind);
            return spec == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx);
        }

        public static RuntimeAnimatorController ControllerOf(EnemyKind kind)
        {
            Spec spec = SpecOf(kind);
            return spec == null ? null : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(spec.Controller);
        }
    }
}
