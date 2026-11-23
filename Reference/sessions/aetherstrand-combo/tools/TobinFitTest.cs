// Scratch harness: attach web hair to Tobin (TobinBase.fbx) with the plugin's head fit and render 4 views with the web studio rig.
// Env: SC_TOBIN_RECIPE, SC_TOBIN_GEO, SC_TOBIN_TAG, SC_TOBIN_FIT (1 = autoFit, 0 = raw web scale), SC_PARITY_OUT, SC_PARITY_RIG
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class TobinFitTest
{
    [Serializable] class V3 { public float x, y, z; public Vector3 V { get { return new Vector3(x, y, z); } } }
    [Serializable] class Rig { public string bg, keyC, fillC, rimC, skyC, gndC; public V3 keyD, fillD, rimD; public float keyI, fillI, rimI, hemiI; }

    public static void Run()
    {
        try { Inner(); Debug.Log("TOBIN RESULT PASS"); } catch (Exception e) { Debug.LogError("TOBIN EXCEPTION " + e); }
    }

    static void Inner()
    {
        if (GraphicsSettings.currentRenderPipeline == null) { Debug.LogError("TOBIN no URP asset"); return; }
        string outDir = ParityRender.Env("SC_PARITY_OUT", "C:/Dev/scratch/parity_out"); Directory.CreateDirectory(outDir);
        string tag = ParityRender.Env("SC_TOBIN_TAG", "tobin");
        string recipe = ParityRender.Env("SC_TOBIN_RECIPE", "C:/Dev/SleepingCalamity/.claude/worktrees/aetherstrand-parity/Reference/sessions/aetherstrand-combo/recipes/halfup059.json");
        string geo = ParityRender.Env("SC_TOBIN_GEO", "C:/Dev/scratch/web_out/halfup059/PresetGeometry.json");
        bool fit = ParityRender.Env("SC_TOBIN_FIT", "1") == "1";
        var rig = JsonUtility.FromJson<Rig>(File.ReadAllText(ParityRender.Env("SC_PARITY_RIG", "C:/Dev/scratch/web_out/rig_studio.json")));

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tobin/TobinBase.fbx");
        var tobin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        tobin.transform.position = Vector3.zero; tobin.transform.rotation = Quaternion.identity;
        Transform head = null; foreach (var t in tobin.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") head = t;

        Type genT = Type.GetType("AetherStrand.Editor.AetherStrandMeshGenerator, AetherStrand.Editor");
        Type parT = Type.GetType("AetherStrand.Editor.HairSculptParams, AetherStrand.Editor");
        Type winT = Type.GetType("AetherStrand.Editor.AetherStrandSetupWindow, AetherStrand.Editor");
        var p = Activator.CreateInstance(parT);
        parT.GetField("overallScale").SetValue(p, 1f);
        parT.GetField("headLocalOffset").SetValue(p, Vector3.zero);
        parT.GetField("bangsLength").SetValue(p, 1f); parT.GetField("layerLength").SetValue(p, 1f);
        parT.GetField("widthScale").SetValue(p, 1f); parT.GetField("thicknessScale").SetValue(p, 1f);
        parT.GetField("autoFitToHeadMesh").SetValue(p, fit);
        parT.GetField("legacyAutoNormalize").SetValue(p, false);
        var matA = (Material)winT.GetMethod("CreateMaterialAsset", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { "Assets/Parity/Hair_" + tag + ".mat", recipe });
        Func<string, float, Vector4> LinC = (h, k) => { Color c; ColorUtility.TryParseHtmlString(h, out c); c = c.linear * k; return new Vector4(c.r, c.g, c.b, 0); };
        Func<V3, Vector4> MDir = (d) => { var v = new Vector3(-d.x, d.y, d.z).normalized; return new Vector4(v.x, v.y, v.z, 0); };
        matA.SetFloat("_UseWebRig", 1f);
        matA.SetVector("_KeyDirW", MDir(rig.keyD)); matA.SetVector("_KeyColW", LinC(rig.keyC, rig.keyI * 0.42f));
        matA.SetVector("_FillDirW", MDir(rig.fillD)); matA.SetVector("_FillColW", LinC(rig.fillC, rig.fillI * 0.38f));
        matA.SetVector("_RimDirW", MDir(rig.rimD)); matA.SetVector("_RimColW", LinC(rig.rimC, rig.rimI * 0.42f));
        matA.SetVector("_AmbColW", LinC(rig.skyC, rig.hemiI * 0.48f));
        var smr = (SkinnedMeshRenderer)genT.GetMethod("GenerateAndAttach", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { tobin, p, geo, "Assets/Parity/Hair_" + tag + ".asset", matA });
        if (smr == null) throw new Exception("GenerateAndAttach null");
        Debug.Log("TOBIN hair bounds(world) " + smr.bounds.center + " size " + smr.bounds.size);

        Color bg; ColorUtility.TryParseHtmlString(rig.bg, out bg);
        Action<string, V3, string, float> Dir = (n, d, col, inten) =>
        {
            var g = new GameObject(n); var l = g.AddComponent<Light>(); l.type = LightType.Directional;
            Color c; ColorUtility.TryParseHtmlString(col, out c); l.color = c; l.intensity = inten / Mathf.PI; l.shadows = LightShadows.None;
            var dd = new Vector3(-d.x, d.y, d.z); g.transform.rotation = Quaternion.LookRotation(-dd.normalized);
        };
        Dir("key", rig.keyD, rig.keyC, rig.keyI); Dir("fill", rig.fillD, rig.fillC, rig.fillI); Dir("rim", rig.rimD, rig.rimC, rig.rimI);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        Color sky, gnd; ColorUtility.TryParseHtmlString(rig.skyC, out sky); ColorUtility.TryParseHtmlString(rig.gndC, out gnd);
        float hi = rig.hemiI / Mathf.PI;
        RenderSettings.ambientSkyColor = sky.linear * hi; RenderSettings.ambientEquatorColor = Color.Lerp(sky.linear, gnd.linear, 0.5f) * hi; RenderSettings.ambientGroundColor = gnd.linear * hi;
        RenderSettings.fog = false;

        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 33; cam.aspect = 1; cam.nearClipPlane = 0.02f; cam.farClipPlane = 20;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = ParityRender.InverseBg(bg).gamma; cam.allowHDR = true; cam.allowMSAA = false;
        var ucd = camGo.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = false;

        // web cameras (pivot-local, y offset 0.44) rescaled around the head bone by the head fit's mean scale
        float ks = 0.34f; // ~ Tobin/web head size ratio, used for framing only
        Vector3 hc = head.position + head.up * 0.06f; // framing centre ~ head middle
        Vector3 fwd = tobin.transform.forward, right = tobin.transform.right;
        var views = new[] { new Vector3(0, 0.38f, 1.84f), new Vector3(0, 0.38f, -1.84f), new Vector3(1.86f, 0.38f, 0), new Vector3(-1.86f, 0.38f, 0) };
        string[] names = { "FRONT", "BACK", "LEFT", "RIGHT" }; int[] gx = { 0, 1, 0, 1 }, gy = { 1, 1, 0, 0 };
        var sheet = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        var rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        for (int k = 0; k < 4; k++)
        {
            Vector3 v = views[k];
            // web +x is the character's left (viewer right when facing); Unity character faces +z with right = +x, so mirror x
            Vector3 pos = hc + (right * (-v.x) + Vector3.up * (v.y - 0.38f) + fwd * v.z) * ks;
            cam.transform.position = pos; cam.transform.LookAt(hc);
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var th = new Texture2D(1024, 1024, TextureFormat.RGBAHalf, false); th.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); th.Apply();
            var tt = ParityRender.ToneMapDownsample(th, bg);
            sheet.SetPixels(gx[k] * 512, gy[k] * 512, 512, 512, tt.GetPixels());
        }
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outDir, tag + "_4view.png"), sheet.EncodeToPNG());
        Debug.Log("TOBIN wrote " + Path.Combine(outDir, tag + "_4view.png"));
    }
}
