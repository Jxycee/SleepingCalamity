#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    [Serializable]
    internal class PresetShaderDto
    {
        public string rootColor;
        public string baseColor;
        public string tipColor;
        public string primarySpecColor;
        public string secondarySpecColor;
        public float primaryShift;
        public float primaryPower;
        public float primaryIntensity;
        public float secondaryShift;
        public float secondaryPower;
        public float secondaryIntensity;
        public float rimIntensity;
        public float rimPower;
        public float rootAO;
        public float grooveDepth;
        public float strandVariation;
    }

    [Serializable]
    internal class PresetPhysicsDto
    {
        public float stiffness;
        public float gravity;
        public float windStrength;
        public float windTurbulence;
        public float windAzimuth;
        public float damping;
        public float inertia;
        public float bendingSmoothness;
        public float collisionMarginMm;
        public float rootAnchor;
        public int solverSubsteps;
    }

    [Serializable]
    internal class PresetRecipeDto
    {
        public string presetId;
        public PresetShaderDto shader;
        public PresetPhysicsDto physics;
    }

    public class AetherStrandSetupWindow : EditorWindow
    {
        private GameObject _characterRoot;
        private Renderer _hairRenderer;
        private string _bonePrefix = "Hair_";
        private string _materialOutputPath = "Assets/AetherStrand_layered_swept_long_Mat.mat";
        private string _presetJsonPath = "";
        private float _headHeightOverride = 0f;

        [MenuItem("Tools/AetherStrand/Hair Studio Setup")]
        public static void OpenWindow()
        {
            var win = GetWindow<AetherStrandSetupWindow>("AetherStrand Setup");
            win.minSize = new Vector2(380, 340);
        }

        private void OnGUI()
        {
            GUILayout.Label("AetherStrand — Sculpted Hair Toolkit (Unity 6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Baked preset: Layered Swept Long (URP).\n" +
                "Create/update the hair material in-place (preserving GUIDs) or auto-bind Hair_ bone chains + 6 scaled body colliders.",
                MessageType.Info
            );

            EditorGUILayout.Space(8);
            _characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", _characterRoot, typeof(GameObject), true);
            _hairRenderer = (Renderer)EditorGUILayout.ObjectField("Hair Renderer (Optional)", _hairRenderer, typeof(Renderer), true);
            _bonePrefix = EditorGUILayout.TextField("Hair Bone Prefix", _bonePrefix);
            _materialOutputPath = EditorGUILayout.TextField("Material Output Path", _materialOutputPath);
            _presetJsonPath = EditorGUILayout.TextField("Preset JSON Path (Opt)", _presetJsonPath);
            _headHeightOverride = EditorGUILayout.FloatField("Head Height Override (0=Auto)", _headHeightOverride);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Create / Update Hair Material Asset", GUILayout.Height(30)))
            {
                Material mat = CreateMaterialAsset(_materialOutputPath, string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath);
                if (mat != null) EditorGUIUtility.PingObject(mat);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                if (GUILayout.Button("Auto-Setup Physics & 6 Scaled Colliders", GUILayout.Height(30)))
                {
                    AutoSetupPhysics(
                        _characterRoot,
                        _bonePrefix,
                        string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath,
                        _headHeightOverride,
                        _hairRenderer
                    );
                }
            }
        }

        /// <summary>
        /// Headless-safe material creator/updater. Updates existing assets in-place to preserve GUIDs.
        /// Never displays modal dialogs.
        /// </summary>
        public static Material CreateMaterialAsset(
            string outputPath = "Assets/AetherStrand_layered_swept_long_Mat.mat",
            string presetJsonPath = null)
        {
            Shader s = Shader.Find("AetherStrand/SculptedHair_URP");
            if (s == null)
            {
                Debug.LogError("[AetherStrand] Shader 'AetherStrand/SculptedHair_URP' not found. Ensure AetherStrand_Hair.shader is imported.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = "Assets/AetherStrand_layered_swept_long_Mat.mat";

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // In-place update preserves existing asset GUIDs across scenes and prefabs
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(s);
            }
            else
            {
                mat.shader = s;
            }

            // Apply baked default look values first
            mat.SetColor("_RootColor", new Color(0.035f, 0.039f, 0.047f, 1f));
            mat.SetColor("_BaseColor", new Color(0.145f, 0.153f, 0.169f, 1f));
            mat.SetColor("_TipColor", new Color(0.298f, 0.314f, 0.341f, 1f));
            mat.SetColor("_PrimarySpecColor", new Color(0.784f, 0.800f, 0.824f, 1f));
            mat.SetColor("_SecondarySpecColor", new Color(0.361f, 0.376f, 0.408f, 1f));
            mat.SetFloat("_StrandVariation", 0.280f);
            mat.SetFloat("_PrimaryShift", 0.050f);
            mat.SetFloat("_PrimaryPower", 64.0f);
            mat.SetFloat("_PrimaryIntensity", 0.360f);
            mat.SetFloat("_SecondaryShift", -0.140f);
            mat.SetFloat("_SecondaryPower", 24.0f);
            mat.SetFloat("_SecondaryIntensity", 0.220f);
            mat.SetFloat("_RimIntensity", 0.480f);
            mat.SetFloat("_RimPower", 2.70f);
            mat.SetFloat("_RootAO", 0.740f);
            mat.SetFloat("_GrooveDepth", 0.780f);
            mat.SetFloat("_UseAlphaClip", 1f);
            mat.SetFloat("_AlphaClip", 0.34f);
            mat.SetFloat("_DitherStrength", 0.0f);
            mat.SetFloat("_HeadCenterBlend", 0.15f);
            mat.EnableKeyword("_USE_ALPHA_CLIP");

            // Optional JSON recipe override
            PresetRecipeDto recipe = TryLoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.shader != null)
            {
                ApplyColorHex(mat, "_RootColor", recipe.shader.rootColor);
                ApplyColorHex(mat, "_BaseColor", recipe.shader.baseColor);
                ApplyColorHex(mat, "_TipColor", recipe.shader.tipColor);
                ApplyColorHex(mat, "_PrimarySpecColor", recipe.shader.primarySpecColor);
                ApplyColorHex(mat, "_SecondarySpecColor", recipe.shader.secondarySpecColor);
                if (recipe.shader.primaryPower > 0f) mat.SetFloat("_PrimaryPower", recipe.shader.primaryPower);
                if (recipe.shader.secondaryPower > 0f) mat.SetFloat("_SecondaryPower", recipe.shader.secondaryPower);
                mat.SetFloat("_PrimaryShift", recipe.shader.primaryShift);
                mat.SetFloat("_PrimaryIntensity", recipe.shader.primaryIntensity);
                mat.SetFloat("_SecondaryShift", recipe.shader.secondaryShift);
                mat.SetFloat("_SecondaryIntensity", recipe.shader.secondaryIntensity);
                mat.SetFloat("_RimIntensity", recipe.shader.rimIntensity);
                if (recipe.shader.rimPower > 0f) mat.SetFloat("_RimPower", recipe.shader.rimPower);
                mat.SetFloat("_RootAO", recipe.shader.rootAO);
                if (recipe.shader.grooveDepth > 0f) mat.SetFloat("_GrooveDepth", recipe.shader.grooveDepth);
                mat.SetFloat("_StrandVariation", recipe.shader.strandVariation);
            }

            TryBindTexture(mat, "_GrooveAoMap", "AetherStrand_GrooveAO");
            TryBindTexture(mat, "_FlowIdMap", "AetherStrand_FlowIDShift");
            TryBindTexture(mat, "_NormalMap", "AetherStrand_Normal");
            TryBindTexture(mat, "_AlphaMap", "AetherStrand_AlphaCard");

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, outputPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AetherStrand] {(isNew ? "Created" : "Updated in-place")} hair material at '{outputPath}'.");
            return mat;
        }

        /// <summary>
        /// Headless-safe deterministic physics & 6-collider auto-setup (Cranium, Face/Jaw, Neck, Upper Chest, L/R Shoulders).
        /// Never displays modal dialogs.
        /// </summary>
        public static AetherStrandPhysics AutoSetupPhysics(
            GameObject characterRoot,
            string bonePrefix = "Hair_",
            string presetJsonPath = null,
            float headHeightOverride = 0f,
            Renderer hairRenderer = null,
            Bounds? headMeshWorldBounds = null)
        {
            if (characterRoot == null)
            {
                Debug.LogError("[AetherStrand] AutoSetupPhysics requires a non-null characterRoot.");
                return null;
            }

            if (string.IsNullOrEmpty(bonePrefix)) bonePrefix = "Hair_";

            AetherStrandPhysics physics = characterRoot.GetComponent<AetherStrandPhysics>();
            if (physics == null)
                physics = characterRoot.AddComponent<AetherStrandPhysics>();

            if (hairRenderer == null)
            {
                Renderer[] renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hairRenderer = renderers[i];
                        break;
                    }
                }
            }
            physics.hairRenderer = hairRenderer;

            Animator animator = characterRoot.GetComponentInChildren<Animator>();
            Transform headBone = null;
            Transform neckBone = null;
            Transform chestBone = null;
            Transform leftShoulderBone = null;
            Transform rightShoulderBone = null;

            if (animator != null && animator.isHuman)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                            ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                            ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                leftShoulderBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)
                                   ?? animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                rightShoulderBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm)
                                    ?? animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            }

            if (headBone == null)
            {
                foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase))
                    {
                        headBone = t;
                        break;
                    }
                }
            }
            if (headBone == null) headBone = characterRoot.transform;

            physics.headTransform = headBone;

            // Character anatomical basis in world space (immune to arbitrary bone local axis conventions)
            Vector3 upWS = characterRoot.transform.up.sqrMagnitude > 1e-6f ? characterRoot.transform.up.normalized : Vector3.up;
            Vector3 fwdWS = characterRoot.transform.forward.sqrMagnitude > 1e-6f ? characterRoot.transform.forward.normalized : Vector3.forward;
            Vector3 rightWS = Vector3.Cross(upWS, fwdWS);
            if (rightWS.sqrMagnitude > 1e-6f) rightWS.Normalize();
            else rightWS = Vector3.right;
            fwdWS = Vector3.Cross(rightWS, upWS).normalized;

            // Estimate anatomical head height (meters) for proportional collider scaling
            float headHeight = headHeightOverride;
            if (headHeight <= 0.01f && headMeshWorldBounds.HasValue)
            {
                float h = headMeshWorldBounds.Value.size.y;
                if (h > 0.06f) headHeight = Mathf.Clamp(h, 0.14f, 0.45f);
            }
            if (headHeight <= 0.01f)
            {
                if (headBone != null && neckBone != null)
                {
                    float neckToHead = Vector3.Distance(headBone.position, neckBone.position);
                    if (neckToHead > 0.02f) headHeight = Mathf.Clamp(neckToHead * 2.0f, 0.16f, 0.28f);
                }
                if (headHeight <= 0.01f && hairRenderer != null)
                {
                    float h = hairRenderer.bounds.size.y * 0.72f;
                    if (h > 0.08f) headHeight = Mathf.Clamp(h, 0.16f, 0.35f);
                }
                if (headHeight <= 0.01f) headHeight = 0.23f;
            }

            // Compute Cranium and Face/Jaw dimensions in world space (fit inside actual skull bounds when provided)
            Vector3 craniumCenterWS;
            float craniumRadiusWS;
            Vector3 faceCenterWS;
            float faceRadiusWS;
            float faceHeightWS;

            if (headMeshWorldBounds.HasValue)
            {
                Bounds hb = headMeshWorldBounds.Value;
                craniumRadiusWS = Mathf.Max(0.03f, Mathf.Min(hb.extents.x, hb.extents.z) * 0.84f);
                float craniumTopY = hb.max.y - craniumRadiusWS * 1.04f;
                float craniumY = Mathf.Lerp(hb.center.y, craniumTopY, 0.55f);
                craniumCenterWS = new Vector3(hb.center.x, craniumY, hb.center.z) - fwdWS * (craniumRadiusWS * 0.06f);

                faceRadiusWS = craniumRadiusWS * 0.78f;
                faceHeightWS = Mathf.Max(0.02f, hb.extents.y * 0.55f);
                faceCenterWS = new Vector3(hb.center.x, hb.center.y - hb.extents.y * 0.28f, hb.center.z) + fwdWS * (craniumRadiusWS * 0.10f);
            }
            else
            {
                craniumRadiusWS = 0.36f * headHeight;
                craniumCenterWS = headBone.position + upWS * (0.52f * headHeight) - fwdWS * (0.02f * headHeight);

                faceRadiusWS = 0.29f * headHeight;
                faceHeightWS = 0.26f * headHeight;
                faceCenterWS = headBone.position + upWS * (0.28f * headHeight) + fwdWS * (0.01f * headHeight);
            }

            // Create / update 6 world-space-computed analytical colliders converted to each bone's local space
            physics.colliders.Clear();

            // 1. Cranium Sphere
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                headBone,
                "AetherStrand_Col_Cranium",
                HairColliderShape.Sphere,
                craniumCenterWS,
                craniumRadiusWS,
                0f,
                upWS
            ));

            // 2. Face / Jaw Capsule
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                headBone,
                "AetherStrand_Col_FaceJaw",
                HairColliderShape.Capsule,
                faceCenterWS,
                faceRadiusWS,
                faceHeightWS,
                upWS
            ));

            // 3. Neck Capsule
            Transform neckParent = neckBone != null ? neckBone : headBone;
            Vector3 neckCenterWS = neckBone != null
                ? neckBone.position + upWS * (0.02f * headHeight) - fwdWS * (0.02f * headHeight)
                : (headMeshWorldBounds.HasValue
                    ? new Vector3(headMeshWorldBounds.Value.center.x, headMeshWorldBounds.Value.min.y - 0.08f * headHeight, headMeshWorldBounds.Value.center.z) - fwdWS * (0.04f * headHeight)
                    : headBone.position - upWS * (0.08f * headHeight) - fwdWS * (0.04f * headHeight));
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                neckParent,
                "AetherStrand_Col_Neck",
                HairColliderShape.Capsule,
                neckCenterWS,
                0.16f * headHeight,
                0.34f * headHeight,
                upWS
            ));

            // 4. Upper Chest Capsule
            Transform chestParent = chestBone != null ? chestBone : characterRoot.transform;
            Vector3 chestCenterWS = chestBone != null
                ? chestBone.position + upWS * (0.08f * headHeight) - fwdWS * (0.02f * headHeight)
                : neckCenterWS - upWS * (0.34f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                chestParent,
                "AetherStrand_Col_UpperChest",
                HairColliderShape.Capsule,
                chestCenterWS,
                0.22f * headHeight,
                0.44f * headHeight,
                rightWS
            ));

            // 5. Left Shoulder Capsule
            Transform leftParent = leftShoulderBone != null ? leftShoulderBone : chestParent;
            Vector3 leftCenterWS = leftShoulderBone != null
                ? leftShoulderBone.position + upWS * (0.04f * headHeight)
                : chestCenterWS - rightWS * (0.42f * headHeight) + upWS * (0.04f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                leftParent,
                "AetherStrand_Col_LeftShoulder",
                HairColliderShape.Capsule,
                leftCenterWS,
                0.165f * headHeight,
                0.32f * headHeight,
                rightWS
            ));

            // 6. Right Shoulder Capsule
            Transform rightParent = rightShoulderBone != null ? rightShoulderBone : chestParent;
            Vector3 rightCenterWS = rightShoulderBone != null
                ? rightShoulderBone.position + upWS * (0.04f * headHeight)
                : chestCenterWS + rightWS * (0.42f * headHeight) + upWS * (0.04f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                rightParent,
                "AetherStrand_Col_RightShoulder",
                HairColliderShape.Capsule,
                rightCenterWS,
                0.165f * headHeight,
                0.32f * headHeight,
                rightWS
            ));

            // Deterministic sorted chain discovery using strict Ordinal comparison (never matches legacy lowercase prefixes)
            var roots = new List<Transform>();
            foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(bonePrefix, StringComparison.Ordinal) &&
                    (t.parent == null || !t.parent.name.StartsWith(bonePrefix, StringComparison.Ordinal)))
                {
                    roots.Add(t);
                }
            }
            roots.Sort((a, b) => string.Compare(GetHierarchyPath(a), GetHierarchyPath(b), StringComparison.Ordinal));
            physics.hairRootBones = roots;

            // Apply baked defaults (fallbackWindStrength defaults to 0f for indoor stillness), then optional JSON override
            physics.stiffness = 0.74f;
            physics.gravity = 3.60f;
            physics.damping = 0.100f;
            physics.headInertia = 0.84f;
            physics.bendingSmoothness = 0.64f;
            physics.collisionMargin = 0.0090f;
            physics.rootAnchor = 0.66f;
            physics.solverSubsteps = 3;
            physics.fallbackWindStrength = 0f;
            physics.fallbackTurbulence = 0.24f;
            physics.fallbackWindAzimuthDeg = 28.0f;

            PresetRecipeDto recipe = TryLoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.physics != null)
            {
                if (recipe.physics.stiffness > 0f) physics.stiffness = recipe.physics.stiffness;
                if (recipe.physics.gravity >= 0f) physics.gravity = recipe.physics.gravity;
                if (recipe.physics.damping > 0f) physics.damping = recipe.physics.damping;
                physics.headInertia = recipe.physics.inertia;
                physics.bendingSmoothness = recipe.physics.bendingSmoothness;
                if (recipe.physics.collisionMarginMm > 0f) physics.collisionMargin = recipe.physics.collisionMarginMm * 0.001f;
                if (recipe.physics.rootAnchor > 0f) physics.rootAnchor = recipe.physics.rootAnchor;
                physics.solverSubsteps = Mathf.Clamp(recipe.physics.solverSubsteps > 0 ? recipe.physics.solverSubsteps : 3, 1, 6);
                physics.fallbackWindStrength = recipe.physics.windStrength;
                physics.fallbackTurbulence = recipe.physics.windTurbulence;
                physics.fallbackWindAzimuthDeg = recipe.physics.windAzimuth;
            }

            physics.RebuildChains();
            EditorUtility.SetDirty(physics);
            Debug.Log($"[AetherStrand] Auto-setup complete on '{characterRoot.name}': {roots.Count} bone chains, {physics.colliders.Count} colliders (headHeight={headHeight:F3}m, craniumR={craniumRadiusWS:F3}m).");
            return physics;
        }

        private static float GetUniformLossyScale(Transform t)
        {
            if (t == null) return 1f;
            Vector3 s = t.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return avg > 1e-5f ? avg : 1f;
        }

        private static AetherStrandCollider EnsureNamedColliderWorldSpace(
            Transform parent,
            string childName,
            HairColliderShape shape,
            Vector3 centerWS,
            float worldRadius,
            float worldHeight,
            Vector3 axisWS)
        {
            Transform existing = parent.Find(childName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            float parentScale = GetUniformLossyScale(parent);
            Vector3 localCenter = parent.InverseTransformPoint(centerWS);
            Vector3 localAxis = parent.InverseTransformDirection(axisWS.sqrMagnitude > 1e-6f ? axisWS.normalized : Vector3.up);
            if (localAxis.sqrMagnitude > 1e-6f) localAxis.Normalize();
            else localAxis = Vector3.up;

            AetherStrandCollider col = go.GetComponent<AetherStrandCollider>();
            if (col == null) col = go.AddComponent<AetherStrandCollider>();
            col.shape = shape;
            col.centerOffset = localCenter;
            col.radius = Mathf.Max(0.0001f, worldRadius / parentScale);
            col.capsuleHeight = Mathf.Max(0f, worldHeight / parentScale);
            col.capsuleAxis = localAxis;
            EditorUtility.SetDirty(col);
            return col;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        private static PresetRecipeDto TryLoadRecipeJson(string presetJsonPath)
        {
            if (string.IsNullOrWhiteSpace(presetJsonPath) || !File.Exists(presetJsonPath)) return null;
            try
            {
                string raw = File.ReadAllText(presetJsonPath);
                return JsonUtility.FromJson<PresetRecipeDto>(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AetherStrand] Could not parse preset JSON '{presetJsonPath}': {ex.Message}");
                return null;
            }
        }

        private static void ApplyColorHex(Material mat, string prop, string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                mat.SetColor(prop, parsed);
            }
        }

        private static string ResolvePackageRootPath()
        {
            // Resolve package root relative to this Editor MonoScript (../Textures/<name>.png)
            string[] scriptGuids = AssetDatabase.FindAssets("AetherStrandSetupWindow t:MonoScript");
            Array.Sort(scriptGuids, StringComparer.Ordinal);
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (!string.IsNullOrEmpty(scriptPath) && scriptPath.EndsWith("AetherStrandSetupWindow.cs", StringComparison.Ordinal))
                {
                    string editorDir = Path.GetDirectoryName(scriptPath);
                    string pkgRoot = string.IsNullOrEmpty(editorDir) ? null : Path.GetDirectoryName(editorDir);
                    if (!string.IsNullOrEmpty(pkgRoot))
                    {
                        return pkgRoot.Replace('\\', '/');
                    }
                }
            }
            return "Packages/com.aetherstrand.sculptedhair";
        }

        private static void TryBindTexture(Material mat, string prop, string fileToken)
        {
            Texture2D tex = null;

            // 1. Package-scoped resolution relative to MonoScript path (../Textures/<name>.png)
            string pkgRoot = ResolvePackageRootPath();
            if (!string.IsNullOrEmpty(pkgRoot))
            {
                string scopedPath = $"{pkgRoot}/Textures/{fileToken}.png";
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(scopedPath);
            }

            // 2. Direct UPM package path fallback
            if (tex == null)
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Packages/com.aetherstrand.sculptedhair/Textures/{fileToken}.png");
            }

            // 3. Project-wide AssetDatabase search fallback
            if (tex == null)
            {
                string[] guids = AssetDatabase.FindAssets(fileToken + " t:Texture2D");
                if (guids.Length > 0)
                {
                    Array.Sort(guids, StringComparer.Ordinal);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (tex != null) mat.SetTexture(prop, tex);
        }
    }
}
#endif
