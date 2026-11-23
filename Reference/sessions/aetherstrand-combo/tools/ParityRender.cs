// Scratch harness (NOT game code): renders the web bust + AetherStrand hair in Unity URP with the web's cameras/lights.
// Env: SC_PARITY_OUT, SC_PARITY_GEO (PresetGeometry.json), SC_PARITY_RECIPE, SC_PARITY_BUST, SC_PARITY_RIG, SC_PARITY_TAG, SC_PARITY_SCALE, SC_PARITY_LEGACY
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ParityRender
{
    [Serializable] class BM { public string color; public float rough; public float metal; public float[] pos; public float[] nrm; public int[] idx; }
    [Serializable] class BL { public BM[] meshes; }
    [Serializable] class V3 { public float x, y, z; public Vector3 V => new Vector3(x, y, z); }
    [Serializable] class Rig { public string bg, keyC, fillC, rimC, skyC, gndC; public V3 keyD, fillD, rimD; public float keyI, fillI, rimI, hemiI; }

        // three.js ACESFilmicToneMapping (exposure 1.16) + sRGB, composited over the clear colour; 2x2 supersample to 512
    public static Texture2D ToneMapDownsample(Texture2D src, Color bgSrgb)
    {
        var px = src.GetPixels(); int W = src.width, N = W / 2;
        var outT = new Texture2D(N, N, TextureFormat.RGB24, false); var o = new Color[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            Color acc = Color.black;
            for (int j = 0; j < 2; j++) for (int i = 0; i < 2; i++)
            {
                Color c = px[(y * 2 + j) * W + (x * 2 + i)];
                Color tm = ACES(new Color(c.r, c.g, c.b), 1.16f);
                Color srgb = new Color(Lin2S(tm.r), Lin2S(tm.g), Lin2S(tm.b));
                acc += srgb;
            }
            acc *= 0.25f; acc.a = 1; o[y * N + x] = acc;
        }
        outT.SetPixels(o); outT.Apply(); return outT;
    }
    public static Color InverseBg(Color bgSrgb)
    {
        Color tgt = bgSrgb.linear; Color x = tgt;
        for (int it = 0; it < 200; it++)
        {
            Color t = ACES(x, 1.16f);
            x = new Color(Mathf.Max(0f, x.r - (t.r - tgt.r) * 0.8f), Mathf.Max(0f, x.g - (t.g - tgt.g) * 0.8f), Mathf.Max(0f, x.b - (t.b - tgt.b) * 0.8f), 0);
        }
        Debug.Log("PARITY inverse bg " + x + " -> " + ACES(x, 1.16f) + " target " + tgt);
        x.a = 1; return x;
    }
    static float Lin2S(float v) { v = Mathf.Clamp01(v); return v <= 0.0031308f ? v * 12.92f : 1.055f * Mathf.Pow(v, 1f / 2.4f) - 0.055f; }
    static Color ACES(Color c, float exposure)
    {
        float r = c.r * exposure / 0.6f, g = c.g * exposure / 0.6f, b = c.b * exposure / 0.6f;
        float r1 = 0.59719f * r + 0.35458f * g + 0.04823f * b, g1 = 0.07600f * r + 0.90834f * g + 0.01566f * b, b1 = 0.02840f * r + 0.13383f * g + 0.83777f * b;
        Func<float, float> fit = v => (v * (v + 0.0245786f) - 0.000090537f) / (v * (0.983729f * v + 0.4329510f) + 0.238081f);
        r1 = fit(r1); g1 = fit(g1); b1 = fit(b1);
        return new Color(Mathf.Clamp01(1.60475f * r1 - 0.53108f * g1 - 0.07367f * b1), Mathf.Clamp01(-0.10208f * r1 + 1.10813f * g1 - 0.00605f * b1), Mathf.Clamp01(-0.00327f * r1 - 0.07276f * g1 + 1.07602f * b1), 1);
    }

    public static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }
    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public static void SetupPipeline()
    {
        Directory.CreateDirectory("Assets/Parity");
        var data = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(data, "Assets/Parity/ParityRenderer.asset");
        var rp = UniversalRenderPipelineAsset.Create(data);
        AssetDatabase.CreateAsset(rp, "Assets/Parity/ParityURP.asset");
        AssetDatabase.SaveAssets();
        GraphicsSettings.defaultRenderPipeline = rp; QualitySettings.renderPipeline = rp;
        AssetDatabase.SaveAssets();
    }

    public static void Run()
    {
        try { Inner(); Debug.Log("PARITY RESULT PASS"); }
        catch (Exception e) { Debug.LogError("PARITY EXCEPTION " + e); }
    }

    static void Inner()
    {
        string outDir = Env("SC_PARITY_OUT", "C:/Dev/scratch/parity_out"); Directory.CreateDirectory(outDir);
        string geo = Env("SC_PARITY_GEO", "C:/Dev/scratch/web_out/halfup059/PresetGeometry.json");
        string recipe = Env("SC_PARITY_RECIPE", "C:/Dev/SleepingCalamity/.claude/worktrees/aetherstrand-parity/Reference/sessions/aetherstrand-combo/recipes/halfup059.json");
        string bustPath = Env("SC_PARITY_BUST", "C:/Dev/scratch/web_out/bust_wrapped.json");
        string tag = Env("SC_PARITY_TAG", "run");
        bool legacyNorm = Env("SC_PARITY_LEGACY", "0") == "1";
        float scale = float.Parse(Env("SC_PARITY_SCALE", "1"), System.Globalization.CultureInfo.InvariantCulture);

        ShaderUtil.allowAsyncCompilation = false;
        // URP must already be the active pipeline (assigning inside this call only takes effect next frame).
        if (GraphicsSettings.currentRenderPipeline == null) { Debug.LogError("PARITY no render pipeline asset active; run ParityRender.SetupPipeline first"); return; }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory("Assets/Parity");
        var root = new GameObject("BustRoot");
        var head = new GameObject("Head"); head.transform.SetParent(root.transform, false); head.transform.position = new Vector3(0, 0.44f, 0);

        var rig = JsonUtility.FromJson<Rig>(File.ReadAllText(Env("SC_PARITY_RIG", "C:/Dev/scratch/web_out/rig_studio.json")));
        var bl = JsonUtility.FromJson<BL>(File.ReadAllText(bustPath));
        var lit = Shader.Find(Shader.Find("Parity/BustLit") != null ? "Parity/BustLit" : "Universal Render Pipeline/Lit");
        Debug.Log("PARITY lit shader " + (lit == null ? "NULL" : lit.name + " supported=" + lit.isSupported));
        int bi = 0;
        foreach (var m in bl.meshes)
        {
            if (m.color == "#ffffff" && m.pos.Length <= 12 * 3) continue;
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            var v = new Vector3[m.pos.Length / 3];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(-m.pos[i * 3], m.pos[i * 3 + 1], m.pos[i * 3 + 2]);
            mesh.vertices = v;
            var idx = m.idx != null && m.idx.Length > 0 ? m.idx : null;
            if (idx == null) { idx = new int[v.Length]; for (int i = 0; i < idx.Length; i++) idx[i] = i; }
            mesh.triangles = idx; mesh.RecalculateNormals();
            var go = new GameObject("bust" + (bi++)); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mesh.RecalculateNormals();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", Hex(m.color)); mat.SetFloat("_Smoothness", 1f - m.rough); mat.SetFloat("_Metallic", m.metal);
            mat.SetFloat("_Cull", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        Type genT = Type.GetType("AetherStrand.Editor.AetherStrandMeshGenerator, AetherStrand.Editor");
        Type parT = Type.GetType("AetherStrand.Editor.HairSculptParams, AetherStrand.Editor");
        Type winT = Type.GetType("AetherStrand.Editor.AetherStrandSetupWindow, AetherStrand.Editor");
        var p = Activator.CreateInstance(parT);
        parT.GetField("overallScale").SetValue(p, scale);
        parT.GetField("headLocalOffset").SetValue(p, Vector3.zero);
        parT.GetField("bangsLength").SetValue(p, 1f);
        parT.GetField("layerLength").SetValue(p, 1f);
        parT.GetField("widthScale").SetValue(p, 1f);
        parT.GetField("thicknessScale").SetValue(p, 1f);
        parT.GetField("autoFitToHeadMesh").SetValue(p, false);
        var lf = parT.GetField("legacyAutoNormalize"); if (lf != null) lf.SetValue(p, legacyNorm);
        Directory.CreateDirectory("Assets/Parity");
        var matA = (Material)winT.GetMethod("CreateMaterialAsset", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { "Assets/Parity/Hair_" + tag + ".mat", recipe });
        matA.SetVector("_HeadCenterWS", new Vector4(0, 0.865f, -0.01f, 0));
        var smr = (SkinnedMeshRenderer)genT.GetMethod("GenerateAndAttach", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { root, p, geo, "Assets/Parity/Hair_" + tag + ".asset", matA });
        if (smr == null) throw new Exception("GenerateAndAttach returned null");
        var mb = smr.sharedMesh.bounds;
        Debug.Log("PARITY hair verts=" + smr.sharedMesh.vertexCount + " tris=" + (smr.sharedMesh.triangles.Length / 3) + " bounds c=" + mb.center + " s=" + mb.size);

        // web 3-light rig uniforms for the hair shader (colour = linear(hex) * intensity * web constant)
        Func<string, float, Vector4> LinC = (h, k) => { var c = Hex(h).linear * k; return new Vector4(c.r, c.g, c.b, 0); };
        Func<V3, Vector4> MDir = (d) => { var v = new Vector3(-d.x, d.y, d.z).normalized; return new Vector4(v.x, v.y, v.z, 0); };
        foreach (var kv in Env("SC_PARITY_MATSET", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pr = kv.Split('=');
            if (pr.Length == 2) matA.SetFloat(pr[0], float.Parse(pr[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var tn in new[] { "_GrooveAoMap", "_FlowIdMap", "_NormalMap", "_AlphaMap" })
        { var tx = matA.GetTexture(tn) as Texture2D; if (tx != null) tx.anisoLevel = int.Parse(Env("SC_PARITY_ANISO", "1")); }
        matA.SetFloat("_UseWebRig", 1f);
        matA.SetVector("_KeyDirW", MDir(rig.keyD)); matA.SetVector("_KeyColW", LinC(rig.keyC, rig.keyI * 0.42f));
        matA.SetVector("_FillDirW", MDir(rig.fillD)); matA.SetVector("_FillColW", LinC(rig.fillC, rig.fillI * 0.38f));
        matA.SetVector("_RimDirW", MDir(rig.rimD)); matA.SetVector("_RimColW", LinC(rig.rimC, rig.rimI * 0.42f));
        matA.SetVector("_AmbColW", LinC(rig.skyC, rig.hemiI * 0.48f));
        EditorUtility.SetDirty(matA); AssetDatabase.SaveAssets();
        Action<string, Vector3, string, float> Dir = (n, d, col, inten) =>
        {
            var g = new GameObject(n); var l = g.AddComponent<Light>(); l.type = LightType.Directional;
            l.color = Hex(col); l.intensity = inten / Mathf.PI; l.shadows = LightShadows.None;
            var dd = new Vector3(-d.x, d.y, d.z); g.transform.rotation = Quaternion.LookRotation(-dd.normalized);
        };
        Dir("key", rig.keyD.V, rig.keyC, rig.keyI); Dir("fill", rig.fillD.V, rig.fillC, rig.fillI); Dir("rim", rig.rimD.V, rig.rimC, rig.rimI);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        float hi = rig.hemiI / Mathf.PI;
        RenderSettings.ambientSkyColor = Hex(rig.skyC).linear * hi;
        RenderSettings.ambientEquatorColor = Color.Lerp(Hex(rig.skyC).linear, Hex(rig.gndC).linear, 0.5f) * hi;
        RenderSettings.ambientGroundColor = Hex(rig.gndC).linear * hi;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.08f; RenderSettings.fogColor = Hex(rig.bg);

        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 33; cam.aspect = 1; cam.nearClipPlane = 0.1f; cam.farClipPlane = 40;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = InverseBg(Hex(rig.bg)).gamma; cam.allowHDR = true; cam.allowMSAA = false;
        var ucd = camGo.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = false; ucd.antialiasing = AntialiasingMode.None;

        if (Env("SC_PARITY_DIAG", "0") == "1")
        {
            smr.enabled = false; int q = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                string[] sh = { "Universal Render Pipeline/Unlit", "Sprites/Default", "Hidden/Internal-Colored", "Universal Render Pipeline/Simple Lit" };
                var dm = new Material(Shader.Find(sh[q++ % sh.Length])); dm.color = Color.green; dm.SetColor("_BaseColor", Color.green); mr.sharedMaterial = dm;
                Debug.Log("PARITY diag " + mr.name + " -> " + dm.shader.name);
            }
        }
        if (Env("SC_PARITY_NOBUST", "0") == "1") foreach (var mr in root.GetComponentsInChildren<MeshRenderer>()) mr.enabled = false;
        var names = new[] { "FRONT", "BACK", "LEFT", "RIGHT" };
        var poss = new[] { new Vector3(0, .82f, 1.84f), new Vector3(0, .82f, -1.84f), new Vector3(1.86f, .82f, 0), new Vector3(-1.86f, .82f, 0) };
        var tgts = new[] { new Vector3(0, .77f, 0), new Vector3(0, .77f, -.02f), new Vector3(0, .77f, -.05f), new Vector3(0, .77f, -.05f) };
        var gxs = new[] { 0, 1, 0, 1 }; var gys = new[] { 1, 1, 0, 0 };
        var sheet = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        var rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { antiAliasing = int.Parse(Env("SC_PARITY_MSAA", "1")) };
        for (int k = 0; k < 4; k++)
        {
            cam.transform.position = new Vector3(-poss[k].x, poss[k].y, poss[k].z);
            cam.transform.LookAt(new Vector3(-tgts[k].x, tgts[k].y, tgts[k].z));
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var th = new Texture2D(1024, 1024, TextureFormat.RGBAHalf, false); th.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); th.Apply();
            var t = ToneMapDownsample(th, Hex(rig.bg));
            sheet.SetPixels(gxs[k] * 512, gys[k] * 512, 512, 512, t.GetPixels());
            File.WriteAllBytes(Path.Combine(outDir, tag + "_" + names[k] + ".png"), t.EncodeToPNG());
        }
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>()) Debug.Log("PARITY bustR " + mr.name + " vis=" + mr.isVisible + " b=" + mr.bounds.center + mr.bounds.size + " sh=" + mr.sharedMaterial.shader.name + " key=" + mr.sharedMaterial.GetVector("_KeyColW"));
        RenderTexture.active = null;
        File.WriteAllBytes(Path.Combine(outDir, tag + "_4view.png"), sheet.EncodeToPNG());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Parity/ParityScene.unity");
        Debug.Log("PARITY wrote " + Path.Combine(outDir, tag + "_4view.png"));
    }
}
