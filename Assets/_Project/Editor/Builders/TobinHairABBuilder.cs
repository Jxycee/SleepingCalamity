// Side-by-side hair trial (2026-11-22): two separate Tobin prefabs, so two solvers never drive the same bones.
//   HairAB/Tobin_HairDeck.prefab      TobinBase + the installed HairDeck cards (TobinHairMaterialBuilder) + TobinHairSpringChains (hair_<k>_<i> bones)
//   HairAB/Tobin_AetherStrand.prefab  TobinBase + AetherStrand locks (AetherHair/TobinAetherHair.fbx) + AetherStrandPhysics with 6 colliders (Hair_* bones)
// Both keep the same scalp shell and cap material. The AetherStrand side uses the plugin's headless API
// (AetherStrandSetupWindow.CreateMaterialAsset + AutoSetupPhysics, plugin v3.0.0) with AetherHair/Tobin_AetherStrand.json (the shipped look, Tobin's colours).
// Delete the HairAB prefabs after regenerating a hair FBX: an existing prefab keeps its structure (hair instance) and only gets settings re-applied.
// Rebuilds are idempotent: an existing prefab is edited in place (structure kept, settings re-applied), so two builds write the same bytes.
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinHairABBuilder.Run
// Trial only: not wired into ProjectBuilder.RunAll (the user picks a hair route from the A/B first).
using System.Linq;
using System.Text;
using AetherStrand.Editor;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairABBuilder
    {
        public const string Folder = "Assets/_Project/Content/Characters/TobinBase/HairAB";
        public const string AetherFolder = "Assets/_Project/Content/Characters/TobinBase/AetherHair";
        public const string AetherMatPath = AetherFolder + "/Materials/Tobin_AetherHair.mat";
        public const string AetherPresetPath = AetherFolder + "/Tobin_AetherStrand.json";
        public const string HairDeckPrefab = Folder + "/Tobin_HairDeck.prefab";
        public const string AetherPrefab = Folder + "/Tobin_AetherStrand.prefab";
        public const string AetherBonePrefix = "Hair_";
        public const float HeadHeight = 0.23f;   // chin to crown, metres (AutoSetupPhysics headHeightOverride)
        const string TexFolder = "Packages/com.aetherstrand.sculptedhair/Textures/";

        public static void Run() { Debug.Log("SC HAIRAB\n" + BuildAll()); }

        public static string BuildAll()
        {
            var log = new StringBuilder();
            TobinBaseMaterialBuilder.Build();
            BuildAetherMaterial(log);
            BuilderUtil.WritePrefab(HairDeckPrefab, root => BuildVariant(root, aether: false, log));
            BuilderUtil.WritePrefab(AetherPrefab, root => BuildVariant(root, aether: true, log));
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        public static Material BuildAetherMaterial(StringBuilder log = null)
        {
            BuilderUtil.EnsureFolder(AetherFolder + "/Materials");
            foreach (var t in new[] { "AetherStrand_GrooveAO", "AetherStrand_FlowIDShift", "AetherStrand_Normal", "AetherStrand_AlphaCard" })
                AssetDatabase.ImportAsset(TexFolder + t + ".png");
            var mat = AetherStrandSetupWindow.CreateMaterialAsset(AetherMatPath, AetherPresetPath);
            if (mat == null) throw new System.InvalidOperationException("AetherStrand CreateMaterialAsset returned null (shader missing?)");
            // The plugin binds textures with AssetDatabase.FindAssets by name; bind by package path when that finds nothing.
            foreach (var (prop, t) in new[] { ("_GrooveAoMap", "AetherStrand_GrooveAO"), ("_FlowIdMap", "AetherStrand_FlowIDShift"), ("_NormalMap", "AetherStrand_Normal"), ("_AlphaMap", "AetherStrand_AlphaCard") })
            {
                if (mat.GetTexture(prop) != null) continue;
                mat.SetTexture(prop, AssetDatabase.LoadAssetAtPath<Texture2D>(TexFolder + t + ".png"));
                log?.AppendLine("bound by path " + prop);
            }
            EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            log?.AppendLine("aether mat " + AetherMatPath + " shader " + mat.shader.name + " tex " + string.Join(",", new[] { "_GrooveAoMap", "_FlowIdMap", "_NormalMap", "_AlphaMap" }.Select(p => mat.GetTexture(p) ? mat.GetTexture(p).name : "NULL")));
            return mat;
        }

        static void BuildVariant(GameObject root, bool aether, StringBuilder log)
        {
            string tag = aether ? "aether" : "hairdeck";
            Transform tobinT = root.transform.Find("TobinBase"), hairT = root.transform.Find("Hair");
            if (tobinT == null || hairT == null)
            {
                // first build: structure (plain instances, not nested prefabs, so the hair rig can move under TobinBase's Head bone)
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                var baseSrc = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath);
                var hairSrc = AssetDatabase.LoadAssetAtPath<GameObject>(aether ? TobinAetherHairImporter.ModelPath : TobinHairMeshImporter.ModelPath);
                if (baseSrc == null || hairSrc == null) throw new System.IO.FileNotFoundException("TobinBase or hair FBX missing for " + tag);
                var tobin = Object.Instantiate(baseSrc, root.transform); tobin.name = "TobinBase";
                var hair = Object.Instantiate(hairSrc, root.transform); hair.name = "Hair";
                var hairAnim = hair.GetComponent<Animator>(); if (hairAnim != null) Object.DestroyImmediate(hairAnim);
                var anim = tobin.GetComponent<Animator>(); if (anim == null) anim = tobin.AddComponent<Animator>();
                anim.avatar = AssetDatabase.LoadAllAssetsAtPath(TobinBaseModelImporter.ModelPath).OfType<Avatar>().First();
                var realHead0 = anim.GetBoneTransform(HumanBodyBones.Head);
                var hairHead0 = hair.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                log.AppendLine(tag + " head delta mm " + ((realHead0.position - hairHead0.position).magnitude * 1000f).ToString("F2"));
                hairHead0.SetParent(realHead0, true);
                tobinT = tobin.transform; hairT = hair.transform;
            }
            var animator = tobinT.GetComponent<Animator>();
            var realHead = animator.GetBoneTransform(HumanBodyBones.Head);
            TobinBaseMaterialBuilder.Apply(tobinT.gameObject);
            foreach (var t in tobinT.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("BaseUnderlay")) t.gameObject.SetActive(true);

            if (!aether)
            {
                log.AppendLine("hairdeck mat " + TobinHairMaterialBuilder.Apply(hairT.gameObject));
                var chains = hairT.GetComponent<TobinHairSpringChains>(); if (chains == null) chains = hairT.gameObject.AddComponent<TobinHairSpringChains>();
                chains.head = realHead;
                int n = realHead.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(AetherBonePrefix, System.StringComparison.Ordinal));
                if (n != 0) throw new System.InvalidOperationException("HairDeck variant holds " + n + " Hair_ bones");
                return;
            }

            var hairSmr = hairT.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Tobin_AetherHair");
            hairSmr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(AetherMatPath);
            foreach (var r in hairT.GetComponentsInChildren<Renderer>(true)) if (r.name == "Tobin_HairShell") r.sharedMaterial = TobinHairMaterialBuilder.BuildCap();
            if (realHead.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("hair_", System.StringComparison.Ordinal)))
                throw new System.InvalidOperationException("AetherStrand variant holds HairDeck hair_ bones (two solvers)");

            // Head height H scales the 6 colliders. Passed explicitly: SkinnedMeshRenderer bounds of the head (the plugin's headMeshWorldBounds path) are
            // inflated in the editor (0.28 m wide), and v2.1's neck heuristic gave a cranium that swallowed the scalp.
            var phys = AetherStrandSetupWindow.AutoSetupPhysics(root, AetherBonePrefix, AetherPresetPath, HeadHeight, hairSmr);
            if (phys == null) throw new System.InvalidOperationException("AutoSetupPhysics returned null");
            int expected = realHead.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(AetherBonePrefix, System.StringComparison.Ordinal) && !t.parent.name.StartsWith(AetherBonePrefix, System.StringComparison.Ordinal));
            if (phys.hairRootBones.Any(t => !t.name.StartsWith(AetherBonePrefix, System.StringComparison.Ordinal))) throw new System.InvalidOperationException("AetherStrand bound a bone outside Hair_");
            if (phys.hairRootBones.Count != expected) throw new System.InvalidOperationException("roots " + phys.hairRootBones.Count + " != chains " + expected);
            if (phys.headTransform != realHead) throw new System.InvalidOperationException("AetherStrand head is " + (phys.headTransform ? phys.headTransform.name : "null"));
            if (phys.ComputeAudit().colliderCount != 6 || phys.colliders.Count != 6) throw new System.InvalidOperationException("AetherStrand colliders " + phys.colliders.Count + " != 6");
            phys.drawGizmos = false;
            EditorUtility.SetDirty(phys);
            var headBounds = tobinT.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.Contains("Head")).Select(r => r.bounds).FirstOrDefault();
            log.AppendLine("aether roots " + phys.hairRootBones.Count + " chains(rebuilt) " + phys.ComputeAudit().totalChains + " head " + realHead.name + " lossyScale " + realHead.lossyScale.ToString("F3")
                + " up " + realHead.up.ToString("F2") + " fwd " + realHead.forward.ToString("F2") + " headMeshBounds c " + headBounds.center.ToString("F3") + " ext " + headBounds.extents.ToString("F3"));
            foreach (var c in phys.colliders)
                log.AppendLine("  col " + c.name + " " + c.shape + " centreWS " + c.transform.TransformPoint(c.centerOffset).ToString("F3") + " r " + (c.radius * c.transform.lossyScale.x).ToString("F3") + " h " + c.capsuleHeight.ToString("F3"));
        }
    }
}
