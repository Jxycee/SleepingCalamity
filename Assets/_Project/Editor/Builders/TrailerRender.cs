// Map trailer renderer (repeatable): a ~75 s, 30 fps, 1920x1080 fly-through of Hollowmere (square Ruined, wipe to Rebuilt, Market Street up to the heroes' home, in at the
// door to the Undercroft stair) and then the Cellars test floor (fixed seed), encoded to MP4 with ffmpeg. Everything is deterministic: the camera route is a table of waypoints
// evaluated with Catmull-Rom splines at t = frame / 30, Midday light, particles simulated once to a fixed settled time, no Time.deltaTime.
//   powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TrailerRender.Render -OutDir <scratch>
// Output: <repo>/Reference/private/trailers/<yyyy-MM-dd>-<sha>/trailer.mp4 + key_01..key_06.png (gitignored). Frames go to %TEMP%\sc_trailer_frames (cleaned at start).
// Env knobs: SC_TRAILER_FPS_SKIP=N (render every Nth frame, encode at 30/N fps, file trailer_preview.mp4), SC_TRAILER_FRAMES="a-b" (frame range),
//   SC_TRAILER_CHECKONLY=1 (no rendering: only the per-frame camera clearance check), SC_CAPTURE_NOBUILD (reuse the built scenes), SC_FFMPEG (ffmpeg.exe path),
//   SC_TRAILER_OUT (output folder), SC_TRAILER_FRAMEDIR.
// Clearance: temporary MeshColliders on the scene's meshes (never saved); a camera inside or within 0.25 m of a mesh, or with a mesh within 0.4 m ahead, logs
//   "SC TRAILER CLEARANCE t=.. pos=..".
// A graphics run rewrites scenes and materials: restore them with git checkout, never commit them.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace SleepingCalamity.EditorTools
{
    public static class TrailerRender
    {
        const int Fps = 30, W = 1920, H = 1080;
        const float Total = 75f, CrossStart = 13.5f, CrossEnd = 15f, CutLane = 30f, CutCellar = 60f, CutRoom = 67.5f;
        const float DipHalf = 0.25f, FadeIn = 0.7f, FadeOut = 0.8f, WipeFeather = 0.35f;
        const string DownloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases (ffmpeg-master-latest-win64-gpl.zip) or https://www.gyan.dev/ffmpeg/builds/";

        // ------------------------------------------------------------------------------------------------ route
        // pos = (x, z) plus eye height above the ground at that spot; look = (x, z) plus height above the ground (Hollowmere), or absolute positions (Cellars).
        struct Wp
        {
            public float T; public Vector3 Pos, Look; public float Fov;
            public Wp(float t, float x, float z, float eye, float lx, float lz, float lh, float fov = 55f) { T = t; Pos = new Vector3(x, eye, z); Look = new Vector3(lx, lh, lz); Fov = fov; }
            public Wp(float t, Vector3 pos, Vector3 look, float fov) { T = t; Pos = pos; Look = look; Fov = fov; }
        }

        sealed class Shot
        {
            public string Name; public Wp[] Pts; public bool GroundRelative;
            public float Start => Pts[0].T; public float End => Pts[Pts.Length - 1].T;

            public void Eval(float t, Func<float, float, float> ground, out Vector3 pos, out Vector3 look, out float fov)
            {
                t = Mathf.Clamp(t, Start, End);
                int i = 0;
                while (i < Pts.Length - 2 && t > Pts[i + 1].T) i++;
                float u = Mathf.Clamp01((t - Pts[i].T) / Mathf.Max(1e-4f, Pts[i + 1].T - Pts[i].T));
                Wp p0 = Pts[Mathf.Max(i - 1, 0)], p1 = Pts[i], p2 = Pts[i + 1], p3 = Pts[Mathf.Min(i + 2, Pts.Length - 1)];
                pos = CatmullRom(p0.Pos, p1.Pos, p2.Pos, p3.Pos, u);
                look = CatmullRom(p0.Look, p1.Look, p2.Look, p3.Look, u);
                fov = Mathf.Lerp(p1.Fov, p2.Fov, u * u * (3f - 2f * u));
                if (GroundRelative) { pos.y += ground(pos.x, pos.z); look.y += ground(look.x, look.z); }
                // gentle deterministic walking sway
                pos.y += 0.025f * Mathf.Sin(t * 2f * Mathf.PI * 1.7f);
                look += new Vector3(0.12f * Mathf.Sin(t * 0.9f), 0.06f * Mathf.Sin(t * 1.3f + 1f), 0.12f * Mathf.Cos(t * 0.7f));
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        // Hollowmere: origin = the dry fountain (radius ~5.8 m basin), stalls at (4.5,4.5) (-7,-3) (5.5,-8.5), Market Street runs south from (0,-20.6) to (0,-49),
        // the heroes' home (cistern house, centre 12.5,11.25, 9 x 7.5) has its front door in the south face near x 12.85; the Undercroft stair is at (15, 12.5).
        static readonly Shot SquareShot = new Shot
        {
            Name = "square", GroundRelative = true,
            Pts = new[]
            {
                new Wp(0f,   -17f, -4f,   1.9f,   5f, -3f,  2.2f),
                new Wp(5f,   -15f, -12f,  1.9f,   2f,  0f,  2.6f),
                new Wp(10f,  -9f,  -18f,  1.9f,   4f, -2f,  2.6f),
                new Wp(15f,  0f,   -19f,  1.9f,   0f,  4f,  3.0f),
                new Wp(21f,  1f,   -14.5f, 1.9f,  -2f, 6f,  3.2f),
                new Wp(26f,  8f,   -14f,  1.9f,  -5f, -2f,  2.4f),
                new Wp(30f,  14f,  -9f,   1.9f,  -2f,  4f,  2.8f),
            }
        };
        static readonly Shot LaneShot = new Shot
        {
            Name = "lane", GroundRelative = true,
            Pts = new[]
            {
                new Wp(30f,  0.6f, -48f,  1.75f,  0f, -26f, 1.2f, 52f),
                new Wp(35f,  0.2f, -37f,  1.75f,  0f, -18f, 1.6f, 52f),
                new Wp(40f,  0f,   -24f,  1.75f,  1f, -8f,  2.0f, 54f),
                new Wp(44f,  3.5f, -15f,  1.8f,   9f, -2f,  2.0f, 55f),
                new Wp(48f,  9f,   -6.5f, 1.8f,   12.5f, 7.0f, 1.6f, 55f),
                new Wp(52f,  11.6f, 0f,   1.8f,   12.5f, 8.0f, 1.5f, 54f),
                new Wp(55f,  12.4f, 4.5f, 1.5f,  12.5f, 9.5f, 1.3f, 55f),
                new Wp(57.5f, 12.45f, 7.6f, 1.25f, 13.2f, 11.5f, 0.8f, 58f),
                new Wp(60f,  13.0f, 9.4f, 1.25f,   15f, 12.5f, 0.1f, 60f),
            }
        };

        // ------------------------------------------------------------------------------------------------ entry
        [MenuItem("SleepingCalamity/Art/Render Map Trailer")]
        public static void RenderMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Render();
        }

        public static void Render()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool checkOnly = Env("SC_TRAILER_CHECKONLY") == "1";
            int skip = Mathf.Max(1, int.TryParse(Env("SC_TRAILER_FPS_SKIP"), out int sk) ? sk : 1);
            int fA = 0, fB = Mathf.RoundToInt(Total * Fps) - 1;
            string range = Env("SC_TRAILER_FRAMES");
            if (!string.IsNullOrEmpty(range))
            {
                string[] ab = range.Split('-');
                if (ab.Length != 2 || !int.TryParse(ab[0], out fA) || !int.TryParse(ab[1], out fB)) throw new ArgumentException("SC_TRAILER_FRAMES must look like 100-400");
            }
            bool reduced = skip > 1 || !string.IsNullOrEmpty(range);
            string sha = Git(projectRoot, "rev-parse --short HEAD");
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string outDir = Env("SC_TRAILER_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(projectRoot, "Reference", "private", "trailers", date + "-" + sha);
            string frameDir = Env("SC_TRAILER_FRAMEDIR");
            if (string.IsNullOrEmpty(frameDir)) frameDir = Path.Combine(Path.GetTempPath(), "sc_trailer_frames");
            string ffmpeg = checkOnly ? null : FindFfmpeg();   // fail early, before the long render

            if (!checkOnly)
            {
                if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true);
                Directory.CreateDirectory(frameDir);
            }
            Directory.CreateDirectory(outDir);
            var frames = new List<int>();
            for (int f = fA; f <= fB; f++) if (f % skip == 0) frames.Add(f);
            Debug.Log("SC TRAILER start sha=" + sha + " frames=" + frames.Count + " skip=" + skip + " checkOnly=" + checkOnly + " out=" + outDir);

            bool noBuild = !string.IsNullOrEmpty(Env("SC_CAPTURE_NOBUILD"));
            if (!noBuild) { HollowmereBuilder.Build(); CellarFloorTestBuilder.Build(); }

            var stats = new Stats();
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            try
            {
                RenderHollowmere(frames, frameDir, checkOnly, stats);
                RenderCellars(frames, frameDir, checkOnly, stats);
            }
            finally
            {
                QualitySettings.renderPipeline = savedQuality;
                LookTestCapture.CleanProbes();
            }
            Debug.Log("SC TRAILER rendered frames=" + stats.Rendered + " clearanceWarnings=" + stats.Warnings);
            if (checkOnly) { Debug.Log("SC TRAILER DONE (check only)"); return; }

            // sequential names for ffmpeg, key frames, encode
            string seqDir = Path.Combine(frameDir, "seq"); Directory.CreateDirectory(seqDir);
            var seqIndex = new Dictionary<int, string>();
            int k = 0;
            foreach (int f in frames)
            {
                string src = FramePath(frameDir, f);
                if (!File.Exists(src)) { Debug.LogWarning("SC TRAILER missing frame " + f); continue; }
                string dst = Path.Combine(seqDir, "s_" + (k++).ToString("00000") + ".png");
                File.Move(src, dst); seqIndex[f] = dst;
            }
            float[] keyTimes = { 8f, 24f, 38f, 53f, 59f, 71f };   // ruined square, rebuilt square, lane, home, stair, cellar
            for (int i = 0; i < keyTimes.Length; i++)
            {
                int target = Mathf.RoundToInt(keyTimes[i] * Fps);
                int best = frames.Where(seqIndex.ContainsKey).OrderBy(f => Mathf.Abs(f - target)).FirstOrDefault();
                if (seqIndex.ContainsKey(best) && Mathf.Abs(best - target) <= Mathf.Max(skip, 2 * Fps))
                    File.Copy(seqIndex[best], Path.Combine(outDir, "key_" + (i + 1).ToString("00") + ".png"), true);
            }
            string mp4 = Path.Combine(outDir, reduced ? "trailer_preview.mp4" : "trailer.mp4");
            Encode(ffmpeg, seqDir, mp4, Mathf.Max(1, Fps / skip), sha, date);
            Debug.Log("SC TRAILER DONE " + mp4 + " frames=" + k + " seconds=" + (k * skip / (float)Fps));
        }

        sealed class Stats { public int Rendered, Warnings; }

        static string FramePath(string dir, int f) => Path.Combine(dir, "frame_" + f.ToString("00000") + ".png");
        static string Env(string key) => Environment.GetEnvironmentVariable(key);

        // ------------------------------------------------------------------------------------------------ Hollowmere
        static void RenderHollowmere(List<int> frames, string frameDir, bool checkOnly, Stats stats)
        {
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            if (!checkOnly) QualitySettings.renderPipeline = rp;
            EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
            TownLayout town = HollowmereLayout.Create();
            Func<float, float, float> ground = (x, z) => HollowmereLayout.Ground(town, x, z);
            Camera cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Cap_HM_Fountain")
                ?? throw new InvalidOperationException("Cap_HM_Fountain camera not found: build the Hollowmere scene first");
            HollowmereLook.Apply(BeautyCornerBuilder.Preset.Midday);

            var tex = checkOnly ? null : new Texture2D(W, H, TextureFormat.RGB24, false, false);
            var rt = checkOnly ? null : new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            try
            {
                foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
                {
                    bool ruined = preset == HollowmereBuilder.Preset.Ruined;
                    // Ruined pass: t < CrossEnd. Rebuilt pass: t >= CrossStart (the window CrossStart..CrossEnd blends over the ruined frames the first pass left).
                    var mine = frames.Where(f => { float t = f / (float)Fps; return t < CutCellar && (ruined ? t < CrossEnd : t >= CrossStart); }).ToList();
                    if (mine.Count == 0) continue;
                    HollowmereBuilder.ApplyPreset(preset);
                    if (!checkOnly)
                    {
                        foreach (ReflectionProbe probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                        SettleParticles();
                        LookTestCapture.WarmUp(new[] { cam });
                    }
                    var clear = new Clearance(); clear.Setup();
                    try
                    {
                        foreach (int f in mine)
                        {
                            float t = f / (float)Fps;
                            Shot shot = t < CutLane ? SquareShot : LaneShot;
                            shot.Eval(t, ground, out Vector3 pos, out Vector3 look, out float fov);
                            if (!clear.Check(t, pos, look)) stats.Warnings++;
                            if (checkOnly) continue;
                            Pose(cam, pos, look, fov);
                            RenderFrame(cam, rt, tex);
                            string path = FramePath(frameDir, f);
                            if (!ruined && t < CrossEnd)
                            {
                                float a = Mathf.Clamp01((t - CrossStart) / (CrossEnd - CrossStart));
                                a = a * a * (3f - 2f * a);
                                var ruinedTex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
                                ruinedTex.LoadImage(File.ReadAllBytes(path));
                                Color32[] rb = tex.GetPixels32(), ru = ruinedTex.GetPixels32();
                                for (int y = 0; y < H; y++)
                                    for (int x = 0; x < W; x++)
                                    {
                                        float w = Mathf.Clamp01((a * (1f + WipeFeather) - x / (float)W) / WipeFeather);   // wipe: rebuilt grows from the left edge
                                        w = w * w * (3f - 2f * w);
                                        int i = y * W + x;
                                        rb[i] = Color32.Lerp(ru[i], rb[i], w);
                                    }
                                UnityEngine.Object.DestroyImmediate(ruinedTex);
                                tex.SetPixels32(rb);
                            }
                            WriteFrame(tex, path, Fade(t));
                            stats.Rendered++;
                        }
                    }
                    finally { clear.Teardown(); }
                }
            }
            finally
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        // ------------------------------------------------------------------------------------------------ Cellars
        static void RenderCellars(List<int> frames, string frameDir, bool checkOnly, Stats stats)
        {
            var mine = frames.Where(f => f / (float)Fps >= CutCellar).ToList();
            if (mine.Count == 0) return;
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            if (!checkOnly) QualitySettings.renderPipeline = rp;
            EditorSceneManager.OpenScene(CellarFloorTestBuilder.ScenePath, OpenSceneMode.Single);
            CellarFloorLayout layout = CellarFloorGenerator.Generate(CellarFloorTestBuilder.Seed, CellarFloorTestBuilder.FloorNumber);   // the fixed seed
            Camera cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == "Floor_Room")
                ?? throw new InvalidOperationException("Floor_Room camera not found: build the Cellars test floor first");

            var (cp, ct) = CellarFloorTestBuilder.CorridorView(layout);
            var (rp0, rt0) = CellarFloorTestBuilder.RoomView(layout);
            Vector3 cDir = Flat(ct - cp), rDir = Flat(rt0 - rp0);
            float cLen = Vector3.Distance(Flat3(cp), Flat3(ct));
            var corridor = new Shot
            {
                Name = "corridor",
                Pts = new[] { new Wp(CutCellar, cp, ct, 70f), new Wp(CutCellar + 3.5f, cp + cDir * (cLen * 0.3f), ct, 70f), new Wp(CutRoom, cp + cDir * (cLen * 0.55f), ct, 68f) }
            };
            Vector3 side = Vector3.Cross(Vector3.up, rDir) * 0.6f;
            float rLen = Vector3.Distance(Flat3(rp0), Flat3(rt0));
            var room = new Shot
            {
                Name = "room",
                Pts = new[] { new Wp(CutRoom, rp0, rt0, 78f), new Wp(CutRoom + 3.75f, rp0 + rDir * (rLen * 0.15f) + side, rt0, 76f), new Wp(Total, rp0 + rDir * (rLen * 0.3f), rt0, 72f) }
            };
            Debug.Log("SC TRAILER cellar corridor pos=" + cp + " target=" + ct + " room pos=" + rp0 + " target=" + rt0);

            var tex = checkOnly ? null : new Texture2D(W, H, TextureFormat.RGB24, false, false);
            var rt = checkOnly ? null : new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            try
            {
                if (!checkOnly)
                {
                    foreach (ReflectionProbe probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)) LookTestCapture.RenderProbe(probe);
                    SettleParticles();
                    LookTestCapture.WarmUp(new[] { cam });
                }
                var clear = new Clearance(); clear.Setup();
                try
                {
                    foreach (int f in mine)
                    {
                        float t = f / (float)Fps;
                        (t < CutRoom ? corridor : room).Eval(t, null, out Vector3 pos, out Vector3 look, out float fov);
                        if (!clear.Check(t, pos, look)) stats.Warnings++;
                        if (checkOnly) continue;
                        Pose(cam, pos, look, fov);
                        RenderFrame(cam, rt, tex);
                        WriteFrame(tex, FramePath(frameDir, f), Fade(t));
                        stats.Rendered++;
                    }
                }
                finally { clear.Teardown(); }
            }
            finally
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }
        static Vector3 Flat3(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------------------------------------------ rendering helpers
        static void SettleParticles()
        {
            foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false; ps.randomSeed = (uint)(Mathf.Abs(ps.name.GetHashCode()) % 100000 + 1);   // fixed per system, never time-dependent
                ps.Simulate(14f, true, true);   // a settled cloud, once per scene
            }
        }

        static void Pose(Camera cam, Vector3 pos, Vector3 look, float fov)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
            cam.fieldOfView = fov;
        }

        static void RenderFrame(Camera cam, RenderTexture rt, Texture2D tex)
        {
            RenderTexture prev = RenderTexture.active;
            cam.targetTexture = rt; cam.enabled = true; cam.Render(); cam.enabled = false; cam.targetTexture = null;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
        }

        static void WriteFrame(Texture2D tex, string path, float fade)
        {
            if (fade < 0.999f)
            {
                Color32[] px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++) { Color32 c = px[i]; px[i] = new Color32((byte)(c.r * fade), (byte)(c.g * fade), (byte)(c.b * fade), 255); }
                tex.SetPixels32(px);
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        // 1 = full picture. Fade in at the start, out at the end, a short dip to black at each cut (Lane, Cellar corridor, Cellar room).
        static float Fade(float t)
        {
            float f = Mathf.Clamp01(t / FadeIn) * Mathf.Clamp01((Total - t) / FadeOut);
            foreach (float cut in new[] { CutLane, CutCellar, CutRoom }) f = Mathf.Min(f, Mathf.Clamp01(Mathf.Abs(t - cut) / DipHalf));
            return f * f * (3f - 2f * f);
        }

        // ------------------------------------------------------------------------------------------------ clearance
        sealed class Clearance
        {
            readonly List<MeshCollider> added = new List<MeshCollider>();
            readonly HashSet<Collider> mine = new HashSet<Collider>();
            int logged;

            public void Setup()
            {
                foreach (MeshFilter mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                    var r = mf.GetComponent<MeshRenderer>();
                    if (r == null || !r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                    string n = mf.name;
                    if (n.Contains("Shaft") || n.Contains("Ray") || n.Contains("Glow") || n.Contains("Decal") || n.Contains("Water") || n.Contains("Sky") || n.Contains("Fog")) continue;
                    if (r.bounds.size.magnitude < 0.6f || r.bounds.size.magnitude > 600f) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
                    added.Add(mc); mine.Add(mc);
                }
                Physics.SyncTransforms();
                Debug.Log("SC TRAILER clearance colliders=" + added.Count);
            }

            public void Teardown() { foreach (MeshCollider mc in added) if (mc != null) UnityEngine.Object.DestroyImmediate(mc); added.Clear(); mine.Clear(); }

            public bool Check(float t, Vector3 pos, Vector3 look)
            {
                string why = null;
                foreach (Collider c in Physics.OverlapSphere(pos, 0.25f)) if (mine.Contains(c)) { why = "inside " + c.name + " bounds " + c.bounds.min.ToString("0.0") + ".." + c.bounds.max.ToString("0.0"); break; }
                if (why == null)
                {
                    Vector3 fwd = (look - pos).normalized;
                    foreach (RaycastHit h in Physics.RaycastAll(pos, fwd, 0.4f)) if (mine.Contains(h.collider)) { why = "wall ahead " + h.collider.name + " " + h.distance.ToString("0.00") + " m"; break; }
                }
                if (why == null) return true;
                if (logged++ < 60) Debug.LogWarning("SC TRAILER CLEARANCE t=" + t.ToString("0.00") + " pos=" + pos.ToString("0.00") + " " + why);
                return false;
            }
        }

        // ------------------------------------------------------------------------------------------------ ffmpeg and git
        static string FindFfmpeg()
        {
            string env = Env("SC_FFMPEG");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            const string tools = @"C:\Dev\_tools\ffmpeg";
            if (Directory.Exists(tools))
            {
                string hit = Directory.GetFiles(tools, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault(p => p.Replace('\\', '/').Contains("/bin/"));
                if (hit != null) return hit;
            }
            foreach (string dir in (Env("PATH") ?? "").Split(Path.PathSeparator))
            {
                try { string p = Path.Combine(dir.Trim('"'), "ffmpeg.exe"); if (File.Exists(p)) return p; } catch { /* bad PATH entry */ }
            }
            throw new FileNotFoundException("ffmpeg not found. Set SC_FFMPEG, or unpack an official static Windows build into C:\\Dev\\_tools\\ffmpeg (so ...\\bin\\ffmpeg.exe exists): " + DownloadUrl);
        }

        static void Encode(string ffmpeg, string seqDir, string mp4, int fps, string sha, string date)
        {
            string text = sha + "  " + date;   // no colon: drawtext would need it escaped
            string vf = "drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='" + text + "':fontcolor=white@0.9:fontsize=24:x=w-tw-28:y=h-th-24:box=1:boxcolor=black@0.35:boxborderw=8";
            var psi = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string a in new[] { "-y", "-framerate", fps.ToString(), "-i", Path.Combine(seqDir, "s_%05d.png"), "-vf", vf, "-c:v", "libx264", "-crf", "16", "-pix_fmt", "yuv420p", mp4 }) psi.ArgumentList.Add(a);
            using (Process p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20 * 60 * 1000)) { try { p.Kill(); } catch { } throw new TimeoutException("ffmpeg timed out"); }
                if (p.ExitCode != 0) throw new InvalidOperationException("ffmpeg failed (" + p.ExitCode + "): " + err.Substring(Mathf.Max(0, err.Length - 1500)));
            }
        }

        static string Git(string workDir, string args)
        {
            try
            {
                var psi = new ProcessStartInfo("git", args) { WorkingDirectory = workDir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(15000); return o.Length > 0 ? o : "nogit"; }
            }
            catch { return "nogit"; }
        }
    }
}
