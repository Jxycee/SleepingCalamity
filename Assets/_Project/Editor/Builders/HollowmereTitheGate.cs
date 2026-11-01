// The sealed tithe gate's art in Hollowmere (Wave 2; design sections 2, 3, 5; STORY 8.1-8.2): Art/Blender/town/sc_tithe_gate.py exports
// Content/Art/Town/Ring1/TitheGate/TitheGate.fbx (a T2 piece: the B4 kit's FK_* slots on the shared B1 tileables with the linear "wear" vertex
// colour for WearLit, the emissive TG_Breath slot, flat TG_Clay/Straw/Wax/Chalk and the B9 foliage atlas beside it).
// Placed on the TitheGate landmark row at the row's own height (HollowmereBuilder.BuildLandmark sets the transform), turned with ArtYaw 180
// (the FBX front is local +Z, the gate faces the square, -Z). The piece fills the keep wall's gap (world x 3.0..9.0) and overlaps the wall ends.
// It adds a small warm point light at SOCKET_Breath (the breath spill on the steps) and box colliders: the sealed gate and surround block,
// the threshold and two steps are walkable. Sealed in Phase 1: nothing opens. Same in both presets.
// Authority: none (editor-time scene building).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereTitheGate
    {
        public const string Folder = BeautyCornerArt.ArtRoot + "/Ring1/TitheGate";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        public const float ArtYaw = 180f;
        // The breath: warm amber (linear, as the generator's BREATH), HDR emission subtle against the midday exposure and readable at low sun.
        public static readonly Color BreathColor = new Color(1.0f, 0.30f, 0.06f);   // redder than the Blender preview: the corner grade pushes amber toward yellow-green
        public const float BreathEmission = 1.8f, BreathLightIntensity = 0.9f, BreathLightRange = 4f;

        // Places the art under g (the landmark's transform). False when the FBX is missing: the caller keeps its greybox.
        public static bool TryPlace(Transform g)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) return false;
            GameObject fbx = BeautyCornerArt.LoadModel(Folder, "TitheGate.fbx", keepMaterialSlots: true);
            if (fbx == null) return false;
            Dictionary<string, Material> slots = BuildMaterials();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, g);
            obj.name = "Art_TitheGate";
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.Euler(0f, ArtYaw, 0f);
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE tithe gate: no material for slot '" + slot + "' on " + r.name); mats[k] = BeautyCornerBuilder.PlaceholderMat("dark"); }
                }
                r.sharedMaterials = mats;
                if (r.name.StartsWith("Foliage")) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            Transform socket = FindDeep(obj.transform, "SOCKET_Breath");
            Vector3 lightPos = socket != null ? g.InverseTransformPoint(socket.position) : new Vector3(0f, 1.6f, -0.4f);
            LookTestBuilder.AddLight(g, "BreathLight", LightType.Point, lightPos + new Vector3(0f, -0.7f, -0.55f), Quaternion.identity, BreathColor, BreathLightIntensity, BreathLightRange, LightShadows.None);
            AddColliders(g);
            Debug.Log("SC HOLLOWMERE tithe gate art placed (" + Folder + "), slots=" + slots.Count);
            return true;
        }

        // The gate's review captures: Cap_HM_TitheGate in both presets at Midday and LowSun, plus a close 3/4 view (TG_Close_*), PNG 1920x1080.
        //   SC_CAPTURE_DIR=<dir> bash Tools/cloud/unity.sh exec --graphics --method SleepingCalamity.EditorTools.HollowmereTitheGate.CaptureGate --out <scratch>
        // Builds the scene first unless SC_CAPTURE_NOBUILD is set. Files: HM_TitheGate_<Preset>_<Light>.png, TG_Close_<Preset>_<Light>.png.
        public static void CaptureGate()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR to the folder for the PNGs");
            System.IO.Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) HollowmereBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new System.IO.FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset saved = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            GameObject closeGo = null;
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                Camera gateCam = null;
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.name == "Cap_HM_TitheGate") gateCam = c;
                if (gateCam == null) throw new System.InvalidOperationException("Cap_HM_TitheGate not found");
                closeGo = Object.Instantiate(gateCam.gameObject); closeGo.name = "TG_Close";   // scratch camera, never saved
                Vector3 eye = new Vector3(2.4f, 1.7f, 14.2f), at = new Vector3(5.9f, 2.2f, 19.3f);
                closeGo.transform.position = eye; closeGo.transform.rotation = Quaternion.LookRotation(at - eye, Vector3.up);
                Camera close = closeGo.GetComponent<Camera>(); close.fieldOfView = 50f;
                Camera[] cams = { gateCam, close };
                foreach (BeautyCornerBuilder.Preset light in new[] { BeautyCornerBuilder.Preset.Midday, BeautyCornerBuilder.Preset.LowSun })
                {
                    BeautyCornerBuilder.ApplyPreset(light);
                    foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                    {
                        HollowmereBuilder.ApplyPreset(preset);
                        foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                        LookTestCapture.WarmUp(cams);
                        LookTestCapture.Save(gateCam, System.IO.Path.Combine(dir, "HM_TitheGate_" + preset + "_" + light + ".png"));
                        LookTestCapture.Save(close, System.IO.Path.Combine(dir, "TG_Close_" + preset + "_" + light + ".png"));
                    }
                }
            }
            finally
            {
                if (closeGo != null) Object.DestroyImmediate(closeGo);
                QualitySettings.renderPipeline = saved;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { Transform f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();   // FK_Sandstone, FK_ChimneyStone, FK_PlinthJoint, FK_Iron on WearLit
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material breath = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_TG_Breath.mat", lit);
            breath.SetColor("_BaseColor", new Color(0.02f, 0.012f, 0.006f, 1f)); breath.SetColor("_Color", breath.GetColor("_BaseColor"));
            breath.SetFloat("_Smoothness", 0.1f); breath.SetFloat("_Metallic", 0f);
            breath.SetColor("_EmissionColor", BreathColor * BreathEmission); breath.EnableKeyword("_EMISSION");
            breath.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(breath);
            map["TG_Breath"] = breath;
            map["TG_Clay"] = Flat(lit, "TG_Clay", new Color(0.254f, 0.069f, 0.030f), 0.2f);
            map["TG_Straw"] = Flat(lit, "TG_Straw", new Color(0.479f, 0.323f, 0.086f), 0.1f);
            map["TG_Wax"] = Flat(lit, "TG_Wax", new Color(0.687f, 0.610f, 0.430f), 0.45f);
            map["TG_Chalk"] = Flat(lit, "TG_Chalk", new Color(0.723f, 0.672f, 0.552f), 0.05f);
            map["M_B9_Foliage"] = BeautyCornerArt.MaterialFromSet(Folder, "T_B9_Foliage", "TitheGate_Foliage", foliage: true);
            AssetDatabase.SaveAssets();
            return map;
        }

        // linear colour (as the generator's flat hexes), handed to SetColor as gamma like BeautyCornerArt.Flat
        static Material Flat(Shader lit, string key, Color linear, float smooth)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_HM_" + key + ".mat", lit);
            m.SetColor("_BaseColor", linear.gamma); m.SetColor("_Color", m.GetColor("_BaseColor"));
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // In the landmark's frame (yaw 0, +Z into the terrace, the gate front at z -0.245, the wall front face at z -0.6).
        static void AddColliders(Transform g)
        {
            var go = new GameObject("Colliders"); go.transform.SetParent(g, false);
            void B(string name, Vector3 c, Vector3 s) { var b = go.AddComponent<BoxCollider>(); b.center = c; b.size = s; }
            B("GateAndWall", new Vector3(0f, 2.6f, 0.2f), new Vector3(6.9f, 5.8f, 0.9f));
            B("PilasterL", new Vector3(-3.0f, 2.6f, -0.45f), new Vector3(0.9f, 5.8f, 1.05f));
            B("PilasterR", new Vector3(3.0f, 2.6f, -0.45f), new Vector3(0.9f, 5.8f, 1.05f));
            B("JambL", new Vector3(-2.1f, 2.6f, -0.55f), new Vector3(0.8f, 5.8f, 0.7f));
            B("JambR", new Vector3(2.1f, 2.6f, -0.55f), new Vector3(0.8f, 5.8f, 0.7f));
            B("Threshold", new Vector3(0f, 0.075f, -0.55f), new Vector3(3.96f, 0.75f, 0.86f));
            B("Step1", new Vector3(0f, 0.0f, -1.17f), new Vector3(5.24f, 0.6f, 0.38f));
            B("Step2", new Vector3(0f, -0.075f, -1.555f), new Vector3(5.96f, 0.45f, 0.39f));
        }
    }
}
