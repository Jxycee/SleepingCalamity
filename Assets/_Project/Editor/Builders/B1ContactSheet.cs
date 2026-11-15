// B1 contact-sheet renders (editor time, authority: none). One 3/4-view PNG per adopted prefab under Content/Art/Town/Adopted/<code>/<code>.prefab, lit by the B0 Morning preset.
// Opens the BeautyCorner scene for its rig (sun, sky, post volume), hides its dressing, never saves anything. Needs a graphics device:
//   Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.B1ContactSheet.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = the PNG folder; SC_CAPTURE_ONLY = comma list of codes)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class B1ContactSheet
    {
        const int Size = 640;
        const string Root = "Assets/_Project/Content/Art/Town/Adopted";

        public static void CaptureAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            string only = System.Environment.GetEnvironmentVariable("SC_CAPTURE_ONLY");
            var want = string.IsNullOrEmpty(only) ? null : only.Split(',').Select(s => s.Trim()).ToList();

            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset saved = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            try
            {
                EditorSceneManager.OpenScene(BeautyCornerBuilder.ScenePath, OpenSceneMode.Single);
                // Hide all the corner's dressing (renderers, terrains, LOD groups), keep only its lights, sky, post volume and probes.
                foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) r.enabled = false;
                foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.enabled = false;
                foreach (LODGroup g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None)) g.enabled = false;
                foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.enabled = false;
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (l.type != LightType.Directional && l.name != "ShadeFill") l.enabled = false;
                BeautyCornerBuilder.ApplyPreset(BeautyCornerBuilder.Preset.Morning);

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ground.name = "CS_Ground";
                ground.transform.position = new Vector3(0, -0.05f, 0); ground.transform.localScale = new Vector3(40f, 0.05f, 40f);
                Object.DestroyImmediate(ground.GetComponent<Collider>());
                var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.SetColor("_BaseColor", new Color(0.42f, 0.40f, 0.34f)); gm.SetFloat("_Smoothness", 0.1f);
                ground.GetComponent<Renderer>().sharedMaterial = gm;

                var camGo = new GameObject("CS_Cam"); var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 28f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.Skybox; cam.enabled = false;
                foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);

                var codes = Directory.GetDirectories(BuilderUtil.Abs(Root)).Select(Path.GetFileName).OrderBy(s => s, System.StringComparer.Ordinal).ToList();
                int n = 0;
                foreach (string code in codes)
                {
                    if (want != null && !want.Contains(code)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + code + "/" + code + ".prefab");
                    if (prefab == null) { Debug.LogWarning("SC B1 SHEET no prefab " + code); continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    try
                    {
                        Bounds b = Bnds(inst);
                        Vector3 c = b.center;
                        float radius = b.extents.magnitude;
                        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                        Vector3 dir3 = Quaternion.Euler(18f, 35f, 0f) * new Vector3(0, 0, -1f);
                        camGo.transform.position = c + dir3 * dist; camGo.transform.LookAt(c);
                        Render(cam, null); Render(cam, Path.Combine(dir, code + ".png"));
                        n++;
                    }
                    finally { Object.DestroyImmediate(inst); }
                }
                Debug.Log("SC B1 SHEET DONE " + n + " " + dir);
            }
            finally
            {
                QualitySettings.renderPipeline = saved;
                LookTestCapture.CleanProbes();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // never leave the modified corner scene open
            }
        }

        static Bounds Bnds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            RenderTexture prev = RenderTexture.active;
            cam.targetTexture = rt; cam.enabled = true; cam.Render(); cam.enabled = false; cam.targetTexture = null;
            for (int i = 0; i < 6; i++) { EditorApplication.QueuePlayerLoopUpdate(); System.Threading.Thread.Sleep(30); }
            if (path != null)
            {
                cam.targetTexture = rt; cam.enabled = true; cam.Render(); cam.enabled = false; cam.targetTexture = null;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
