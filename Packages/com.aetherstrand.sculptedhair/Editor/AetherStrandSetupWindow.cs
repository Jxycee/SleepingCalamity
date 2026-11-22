#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    [Serializable]
    public class AetherStrandPresetData
    {
        public string schemaVersion = "2.1";
        public string presetId = "parted_shag_medium";
        public ShaderPreset shader = new ShaderPreset();
        public PhysicsPreset physics = new PhysicsPreset();

        [Serializable]
        public class ShaderPreset
        {
            public string rootColor = "#26180f";
            public string baseColor = "#4c3322";
            public string tipColor = "#61432d";
            public string primarySpecColor = "#d9ba9c";
            public string secondarySpecColor = "#8c5d3a";
            public float primaryShift = 0.08f;
            public float primaryPower = 84f;
            public float primaryIntensity = 0.34f;
            public float secondaryShift = -0.15f;
            public float secondaryPower = 32f;
            public float secondaryIntensity = 0.20f;
            public float rimIntensity = 0.28f;
            public float rimPower = 3.4f;
            public float rootAO = 0.46f;
            public float grooveDepth = 0.68f;
            public float strandVariation = 0.22f;
            public float alphaClip = 0.34f;
            public float headCenterBlend = 0.30f;
        }

        [Serializable]
        public class PhysicsPreset
        {
            public float stiffness = 0.62f;
            public float gravity = 4.4f;
            public float windStrength = 0.55f;
            public float windTurbulence = 0.24f;
            public float windAzimuth = 30f;
            public float damping = 0.095f;
            public float inertia = 0.88f;
            public float bendingSmoothness = 0.52f;
            public float collisionMarginMm = 11f;
            public float rootAnchor = 0.62f;
            public int solverSubsteps = 3;
            public int solverIterations = 4;
        }
    }

    /// <summary>
    /// Interactive EditorWindow + headless batchmode static API for script-built Unity pipelines.
    /// </summary>
    public class AetherStrandSetupWindow : EditorWindow
    {
        private GameObject _characterRoot;
        private string _bonePrefix = "Hair_";
        private TextAsset _presetJson;

        [MenuItem("Tools/AetherStrand/Sculpted Hair Setup")]
        public static void OpenWindow()
        {
            var win = GetWindow<AetherStrandSetupWindow>("AetherStrand Setup");
            win.minSize = new Vector2(380, 290);
        }

        private void OnGUI()
        {
            GUILayout.Label("AetherStrand — Sculpted Volumetric Hair Toolkit (URP)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create a sculpted hair material or auto-bind Hair_ bone chains + 6 head/body colliders on any Humanoid character.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            _characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", _characterRoot, typeof(GameObject), true);
            _bonePrefix = EditorGUILayout.TextField("Hair Bone Prefix", _bonePrefix);
            _presetJson = (TextAsset)EditorGUILayout.ObjectField("Preset JSON (Optional)", _presetJson, typeof(TextAsset), false);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Create Hair Material Asset", GUILayout.Height(30)))
            {
                string jsonPath = _presetJson != null ? AssetDatabase.GetAssetPath(_presetJson) : null;
                var mat = CreateMaterialAsset("Assets/AetherStrand_Hair_Mat.mat", jsonPath);
                if (mat != null) EditorGUIUtility.PingObject(mat);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                if (GUILayout.Button("Auto-Setup Physics & 6 Colliders on Character", GUILayout.Height(30)))
                {
                    string jsonPath = _presetJson != null ? AssetDatabase.GetAssetPath(_presetJson) : null;
                    AutoSetupPhysics(_characterRoot, _bonePrefix, jsonPath);
                }
            }
        }

        /// <summary>
        /// Headless-safe material creator for editor build scripts (no modal dialogs).
        /// Updates in place if asset already exists to preserve GUIDs.
        /// </summary>
        public static Material CreateMaterialAsset(string outputAssetPath, string presetJsonPath = null)
        {
            Shader s = Shader.Find("AetherStrand/SculptedHair_URP");
            if (s == null)
            {
                Debug.LogError("[AetherStrand] Shader 'AetherStrand/SculptedHair_URP' not found.");
                return null;
            }

            AetherStrandPresetData preset = LoadPreset(presetJsonPath);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(outputAssetPath);
            Material mat = existing != null ? existing : new Material(s);
            if (existing != null) mat.shader = s;

            ApplyShaderPreset(mat, preset.shader);

            TryBindTexture(mat, "_GrooveAoMap", "AetherStrand_GrooveAO");
            TryBindTexture(mat, "_FlowIdMap", "AetherStrand_FlowIDShift");
            TryBindTexture(mat, "_NormalMap", "AetherStrand_Normal");
            TryBindTexture(mat, "_AlphaMap", "AetherStrand_AlphaCard");

            if (existing == null)
            {
                string dir = Path.GetDirectoryName(outputAssetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(mat, outputAssetPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            return mat;
        }

        /// <summary>
        /// Headless-safe physics + 6-collider setup scaled to measured head height H.
        /// Discovers all hair chains starting with bonePrefix (case-insensitive) under characterRoot.
        /// </summary>
        public static AetherStrandPhysics AutoSetupPhysics(
            GameObject characterRoot,
            string bonePrefix = "Hair_",
            string presetJsonPath = null,
            float headHeightOverride = 0f,
            Renderer hairRenderer = null)
        {
            if (characterRoot == null) return null;

            AetherStrandPresetData preset = LoadPreset(presetJsonPath);
            var physics = characterRoot.GetComponent<AetherStrandPhysics>() ?? characterRoot.AddComponent<AetherStrandPhysics>();
            physics.hairRenderer = hairRenderer;

            var animator = characterRoot.GetComponentInChildren<Animator>();
            Transform headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            Transform neckBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
            Transform chestBone = animator != null && animator.isHuman
                ? (animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest))
                : null;
            Transform shoulderL = animator != null && animator.isHuman
                ? (animator.GetBoneTransform(HumanBodyBones.LeftShoulder) ?? animator.GetBoneTransform(HumanBodyBones.LeftUpperArm))
                : null;
            Transform shoulderR = animator != null && animator.isHuman
                ? (animator.GetBoneTransform(HumanBodyBones.RightShoulder) ?? animator.GetBoneTransform(HumanBodyBones.RightUpperArm))
                : null;

            if (headBone != null)
            {
                physics.headTransform = headBone;

                // Estimate head height H (~0.23m default on MetaPerson)
                float H = headHeightOverride > 0.05f ? headHeightOverride : 0.23f;
                if (headHeightOverride <= 0.05f && neckBone != null)
                {
                    float neckToHead = Vector3.Distance(headBone.position, neckBone.position);
                    if (neckToHead > 0.04f && neckToHead < 0.20f)
                        H = Mathf.Clamp(neckToHead * 2.5f, 0.18f, 0.28f);
                }

                physics.headCenterLocalOffset = new Vector3(0f, 0.50f * H, -0.03f * H);
                physics.colliders.Clear();

                // 6-collider medium-layered set scaled to H
                AddOrUpdateCollider(headBone, "AS_Col_Cranium", HairColliderShape.Sphere, new Vector3(0f, 0.52f * H, -0.04f * H), 0.42f * H, 0f, Vector3.up, physics);
                AddOrUpdateCollider(headBone, "AS_Col_FaceJaw", HairColliderShape.Capsule, new Vector3(0f, 0.28f * H, 0.15f * H), 0.30f * H, 0.36f * H, new Vector3(0f, 1f, -0.15f).normalized, physics);
                if (neckBone != null)
                    AddOrUpdateCollider(neckBone, "AS_Col_Neck", HairColliderShape.Capsule, new Vector3(0f, 0.21f * H, 0.01f * H), 0.24f * H, 0.42f * H, Vector3.up, physics);
                if (chestBone != null)
                    AddOrUpdateCollider(chestBone, "AS_Col_UpperChest", HairColliderShape.Capsule, new Vector3(0f, 0.15f * H, -0.05f * H), 0.42f * H, 0.90f * H, Vector3.right, physics);
                if (shoulderL != null)
                    AddOrUpdateCollider(shoulderL, "AS_Col_ShoulderL", HairColliderShape.Capsule, new Vector3(0f, 0.18f * H, 0f), 0.26f * H, 0.55f * H, Vector3.up, physics);
                if (shoulderR != null)
                    AddOrUpdateCollider(shoulderR, "AS_Col_ShoulderR", HairColliderShape.Capsule, new Vector3(0f, 0.18f * H, 0f), 0.26f * H, 0.55f * H, Vector3.up, physics);
            }

            // Deterministic discovery of all hair chain roots
            var roots = new List<Transform>();
            var allTransforms = characterRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                bool matches = t.name.StartsWith(bonePrefix, StringComparison.OrdinalIgnoreCase);
                bool parentMatches = t.parent != null && t.parent.name.StartsWith(bonePrefix, StringComparison.OrdinalIgnoreCase);
                if (matches && !parentMatches) roots.Add(t);
            }
            roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            physics.hairRootBones = roots;

            ApplyPhysicsPreset(physics, preset.physics);
            physics.RebuildChains();
            EditorUtility.SetDirty(physics);
            return physics;
        }

        private static AetherStrandPresetData LoadPreset(string presetJsonPath)
        {
            if (!string.IsNullOrEmpty(presetJsonPath) && File.Exists(presetJsonPath))
            {
                try
                {
                    var parsed = JsonUtility.FromJson<AetherStrandPresetData>(File.ReadAllText(presetJsonPath));
                    if (parsed != null) return parsed;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AetherStrand] Failed to parse preset JSON '{presetJsonPath}': {ex.Message}");
                }
            }
            return new AetherStrandPresetData();
        }

        private static void ApplyShaderPreset(Material mat, AetherStrandPresetData.ShaderPreset p)
        {
            if (ColorUtility.TryParseHtmlString(p.rootColor, out var cRoot)) mat.SetColor("_RootColor", cRoot);
            if (ColorUtility.TryParseHtmlString(p.baseColor, out var cBase)) mat.SetColor("_BaseColor", cBase);
            if (ColorUtility.TryParseHtmlString(p.tipColor, out var cTip)) mat.SetColor("_TipColor", cTip);
            if (ColorUtility.TryParseHtmlString(p.primarySpecColor, out var cSpec1)) mat.SetColor("_PrimarySpecColor", cSpec1);
            if (ColorUtility.TryParseHtmlString(p.secondarySpecColor, out var cSpec2)) mat.SetColor("_SecondarySpecColor", cSpec2);

            mat.SetFloat("_PrimaryShift", p.primaryShift);
            mat.SetFloat("_PrimaryPower", p.primaryPower);
            mat.SetFloat("_PrimaryIntensity", p.primaryIntensity);
            mat.SetFloat("_SecondaryShift", p.secondaryShift);
            mat.SetFloat("_SecondaryPower", p.secondaryPower);
            mat.SetFloat("_SecondaryIntensity", p.secondaryIntensity);
            mat.SetFloat("_RimIntensity", p.rimIntensity);
            mat.SetFloat("_RimPower", p.rimPower);
            mat.SetFloat("_RootAO", p.rootAO);
            mat.SetFloat("_GrooveDepth", p.grooveDepth);
            mat.SetFloat("_StrandVariation", p.strandVariation);
            mat.SetFloat("_HeadCenterBlend", p.headCenterBlend);
            mat.SetFloat("_UseAlphaClip", 1f);
            mat.SetFloat("_AlphaClip", p.alphaClip);
            mat.EnableKeyword("_USE_ALPHA_CLIP");
        }

        private static void ApplyPhysicsPreset(AetherStrandPhysics physics, AetherStrandPresetData.PhysicsPreset p)
        {
            physics.stiffness = p.stiffness;
            physics.gravity = p.gravity;
            physics.damping = p.damping;
            physics.headInertia = p.inertia;
            physics.bendingSmoothness = p.bendingSmoothness;
            physics.collisionMargin = p.collisionMarginMm * 0.001f;
            physics.rootAnchor = p.rootAnchor;
            physics.solverSubsteps = Mathf.Clamp(p.solverSubsteps, 1, 6);
            physics.solverIterations = Mathf.Clamp(p.solverIterations, 1, 6);
            physics.fallbackWindStrength = p.windStrength;
            physics.fallbackTurbulence = p.windTurbulence;
            physics.fallbackWindAzimuthDeg = p.windAzimuth;
        }

        private static void TryBindTexture(Material mat, string prop, string fileToken)
        {
            string[] guids = AssetDatabase.FindAssets(fileToken + " t:Texture2D");
            if (guids.Length > 0)
            {
                Array.Sort(guids, StringComparer.Ordinal);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (tex != null) mat.SetTexture(prop, tex);
            }
        }

        private static void AddOrUpdateCollider(
            Transform parentBone,
            string childName,
            HairColliderShape shape,
            Vector3 offset,
            float radius,
            float height,
            Vector3 axis,
            AetherStrandPhysics physics)
        {
            Transform existing = parentBone.Find(childName);
            GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
            if (existing == null)
            {
                go.transform.SetParent(parentBone, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }

            var col = go.GetComponent<AetherStrandCollider>() ?? go.AddComponent<AetherStrandCollider>();
            col.shape = shape;
            col.centerOffset = offset;
            col.radius = radius;
            col.capsuleHeight = height;
            col.capsuleAxis = axis;
            physics.colliders.Add(col);
        }
    }
}
#endif
