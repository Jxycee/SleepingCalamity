// Capture-only: renders each Hollowmere market stall (Produce, Cloth, Tools) in both presets at the camera distance of every LOD transition, once with LOD i forced and
// once with LOD i+1 forced from the same pose, so the pop between two LODs can be diffed. Also one LOD0 close frame per stall. Not wired into ProjectBuilder.RunAll; it
// changes nothing in the scene file (the scene is opened, never saved).
//   unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.StallLodCapture.CaptureAll -OutDir <scratch>   (env SC_CAPTURE_DIR = PNG folder, SC_CAPTURE_NOBUILD=1 skips the build)
// Transition distance (Unity's LODGroup maths): relative height h = worldSize / (2 d tan(fov/2)) * lodBias, so d = worldSize / (2 tan(fov/2) h) / lodBias,
// worldSize = LODGroup.size * the group's largest lossy scale axis, fov = 50 (the vertical FOV; the 1920x1080 frame has the same vertical extent).
// Files: <Type>_<Preset>_T<i>_LOD<i>.png, <Type>_<Preset>_T<i>_LOD<i+1>.png, <Type>_<Preset>_Close_LOD0.png.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class StallLodCapture
    {
        const float Fov = 50f, EyeHeight = 1.65f, CloseDistance = 3.5f;
        static readonly string[] Types = { "Produce", "Cloth", "Tools" };

        public static void CaptureAll()
        {
            string dir = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-scCaptureDir") dir = args[i + 1];
            if (string.IsNullOrEmpty(dir)) dir = System.Environment.GetEnvironmentVariable("SC_CAPTURE_DIR");
            if (string.IsNullOrEmpty(dir)) throw new System.InvalidOperationException("Set SC_CAPTURE_DIR (or pass -scCaptureDir) to the folder for the PNGs");
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) HollowmereBuilder.Build();
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = rp;
            var forced = new List<LODGroup>();
            Camera cam = null;
            try
            {
                EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
                HollowmereLook.Apply(BeautyCornerBuilder.Preset.Midday);
                cam = LookTestBuilder.AddCapture(null, "Cap_StallLod", Vector3.zero, Vector3.forward, Fov);
                foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                {
                    HollowmereBuilder.ApplyPreset(preset);
                    if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("SC_CAPTURE_NOPROBES")))
                        foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                    LookTestCapture.WarmUp(new[] { cam });
                    Physics.SyncTransforms();
                    foreach (string type in Types)
                    {
                        string prefix = "Stall_" + type + "_" + preset;
                        LODGroup lg = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                            .FirstOrDefault(g => g.gameObject.activeInHierarchy && PathOf(g.transform).Contains("/" + prefix + "/"));
                        if (lg == null) { Debug.LogWarning("SC STALL LOD " + type + " " + preset + " not found (" + prefix + ")"); continue; }
                        forced.Add(lg);
                        CaptureStall(cam, lg, type, preset.ToString(), prefix, dir);
                    }
                }
            }
            finally
            {
                foreach (LODGroup g in forced) if (g != null) g.ForceLOD(-1);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
                HollowmereBuilder.ApplyPreset(HollowmereBuilder.Preset.Ruined);
                HollowmereLook.Apply(BeautyCornerBuilder.Preset.Midday);
                QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC CAPTURE DONE " + dir);
        }

        static string PathOf(Transform t)
        {
            string p = "";
            for (; t != null; t = t.parent) p = "/" + t.name + p;
            return p + "/";
        }

        static int Tris(Renderer[] rs) => rs == null ? 0 : rs.Sum(r => r != null && r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0);

        static void CaptureStall(Camera cam, LODGroup lg, string type, string preset, string prefix, string dir)
        {
            LOD[] lods = lg.GetLODs();
            Transform stall = lg.transform.root == lg.transform ? lg.transform : FindStallRoot(lg.transform, prefix);
            Vector3 ls = lg.transform.lossyScale;
            Debug.Log("SC STALL LOD " + type + " " + preset + " tree: lg=" + PathOf(lg.transform) + " stallRoot=" + PathOf(stall) + " nodes=" + string.Join(";", stall.GetComponentsInChildren<Transform>(true).Select(t => t.name + (t.GetComponent<Renderer>() != null ? "[R]" : "") + (t.GetComponent<LODGroup>() != null ? "[LG]" : "") + (t.gameObject.activeSelf ? "" : "[off]"))));
            float worldSize = lg.size * Mathf.Max(ls.x, Mathf.Max(ls.y, ls.z));
            float bias = QualitySettings.lodBias, tanHalf = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            Bounds b0 = default; bool hasB = false;
            Renderer[][] sets = new Renderer[lods.Length][];
            bool broken = false;   // the group's own renderer references are gone (null): ForceLOD then hides nothing, so the LOD children are switched by hand
            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] own = lods[i].renderers == null ? new Renderer[0] : lods[i].renderers.Where(r => r != null).ToArray();
                if (own.Length == 0) broken = true;
                sets[i] = own.Length > 0 ? own : lg.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_LOD" + i)).ToArray();
            }
            Debug.Log("SC STALL LOD " + type + " " + preset + " group renderers per LOD=" + string.Join("/", lods.Select(l => l.renderers == null ? -1 : l.renderers.Count(r => r != null))) + " children per LOD=" + string.Join("/", sets.Select(x => x.Length)) + " broken=" + broken);
            foreach (Renderer r in sets[0]) if (r != null) { if (!hasB) { b0 = r.bounds; hasB = true; } else b0.Encapsulate(r.bounds); }
            Vector3 center = b0.center;
            Vector3 front = stall.forward; front.y = 0f; front.Normalize();   // the counter faces local +Z
            float baseY = stall.position.y;
            var dists = new List<float>();
            for (int i = 0; i < lods.Length - 1; i++) dists.Add(worldSize / (2f * tanHalf * lods[i].screenRelativeTransitionHeight) / bias);
            Debug.Log("SC STALL LOD " + type + " " + preset + " size=" + lg.size.ToString("F2") + " worldSize=" + worldSize.ToString("F2")
                + " heights=" + string.Join("/", lods.Select(l => l.screenRelativeTransitionHeight.ToString("F3"))) + " bias=" + bias.ToString("F2")
                + " dist=" + string.Join("/", dists.Select(d => d.ToString("F1"))) + " tris=" + string.Join("/", sets.Select(x => Tris(x))));

            // close LOD0 frame
            PlaceCamera(cam, stall, center, front, baseY, CloseDistance, out float closeAz);
            Show(lg, sets, broken, 0);
            LookTestCapture.Save(cam, Path.Combine(dir, type + "_" + preset + "_Close_LOD0.png"));
            for (int i = 0; i < lods.Length - 1; i++)
            {
                PlaceCamera(cam, stall, center, front, baseY, dists[i], out float az);
                Debug.Log("SC STALL LOD " + type + " " + preset + " T" + i + " dist=" + dists[i].ToString("F1") + " azimuth=" + az.ToString("F0") + " cam=" + cam.transform.position.ToString("F2"));
                Show(lg, sets, broken, i);
                LookTestCapture.Save(cam, Path.Combine(dir, type + "_" + preset + "_T" + i + "_LOD" + i + ".png"));
                Show(lg, sets, broken, i + 1);
                LookTestCapture.Save(cam, Path.Combine(dir, type + "_" + preset + "_T" + i + "_LOD" + (i + 1) + ".png"));
            }
            lg.ForceLOD(-1);
            foreach (Renderer[] set in sets) foreach (Renderer r in set) r.enabled = true;
        }

        static void Show(LODGroup lg, Renderer[][] sets, bool broken, int lod)
        {
            lg.ForceLOD(lod);
            if (broken) for (int k = 0; k < sets.Length; k++) foreach (Renderer r in sets[k]) r.enabled = k == lod;
        }

        static Transform FindStallRoot(Transform t, string prefix)
        {
            for (Transform p = t; p != null; p = p.parent) if (p.name == prefix) return p;
            return t;
        }

        // Camera at `dist` (horizontal) in front of the stall, eye EyeHeight above the stall's ground, looking at the stall's centre. Nudges the azimuth in 20 degree steps
        // until nothing but the stall itself blocks the line from the centre to the camera.
        static void PlaceCamera(Camera cam, Transform stall, Vector3 center, Vector3 front, float baseY, float dist, out float azimuth)
        {
            azimuth = 0f;
            float[] tries = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 80f, -80f, 100f, -100f, 140f, -140f, 180f };
            Vector3 pos = default;
            foreach (float az in tries)
            {
                Vector3 dir = Quaternion.Euler(0f, az, 0f) * front;
                pos = new Vector3(center.x, baseY + EyeHeight, center.z) + dir * dist;
                azimuth = az;
                Vector3 from = center, to = pos;
                RaycastHit[] hits = Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore);
                if (!hits.Any(h => !h.transform.IsChildOf(stall))) break;
            }
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(center - pos, Vector3.up);
        }
    }
}
