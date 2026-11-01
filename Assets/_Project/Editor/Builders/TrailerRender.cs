// Map trailer renderer (repeatable): a ~75 s, 30 fps, 1920x1080 smooth cinematic fly-through, each place shown once, in this order:
//   Ruined square sweep -> feathered wipe to Rebuilt -> Rebuilt square sweep (the same flight, flown back, so the wipe compares like with like) -> Keep Lane glide toward
//   the heroes' home -> door, ivy, lit interior -> Undercroft stair entrance and descent -> Cellars corridor dolly -> Cellars room, fade out.
// Everything is deterministic: each shot is a waypoint spline resampled by arc length (constant speed) under a trapezoid ease-in/out, then low-pass smoothed
// (position, look-at target and fov) before it is posed at t = frame / 30. Midday light, particles simulated once to a fixed settled time, no Time.deltaTime, no walk bob.
//   powershell -NoProfile -ExecutionPolicy Bypass -File Tools\unity.ps1 exec -Graphics -Method SleepingCalamity.EditorTools.TrailerRender.Render -OutDir <scratch>
// Output: <main checkout>/Reference/private/trailers/<yyyy-MM-dd>-<sha>/trailer.mp4 + key_01..key_06.png (gitignored). Frames go to %TEMP%\sc_trailer_frames (cleaned at start).
// Env knobs: SC_TRAILER_FPS_SKIP=N (render every Nth frame, encode at 30/N fps, file trailer_preview.mp4), SC_TRAILER_FRAMES="a-b" (frame range),
//   SC_TRAILER_CHECKONLY=1 (no rendering: only the per-frame camera clearance check), SC_CAPTURE_NOBUILD (reuse the built scenes), SC_FFMPEG (ffmpeg.exe path),
//   SC_TRAILER_OUT (output folder), SC_TRAILER_FRAMEDIR.
// Clearance: temporary MeshColliders on the scene's meshes (never saved); a camera inside or within 0.25 m of a mesh, or with a mesh within 0.4 m ahead, logs
//   "SC TRAILER CLEARANCE t=.. pos=..". Per-segment smoothness is logged as "SC TRAILER SEGMENT" (max per-frame translation and rotation).
// The stair segment adds a warm lamp that rides with the camera (runtime only, destroyed after; the scene asset is untouched).
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
        // Timeline (seconds). Ruined sweep 0..FDur, wipe CrossStart..CrossEnd (camera holds), Rebuilt sweep CrossEnd..CutLane (the same flight in reverse).
        const float FDur = 12.2f, CrossStart = 12.2f, CrossEnd = 13.8f, CutLane = 26f, CutStair = 47f, CutCellar = 60f, CutRoom = 67f, Total = 74.5f;
        const float DipHalf = 0.25f, FadeIn = 0.7f, FadeOut = 0.9f, WipeFeather = 0.35f;
        const string DownloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases (ffmpeg-master-latest-win64-gpl.zip) or https://www.gyan.dev/ffmpeg/builds/";

        // ------------------------------------------------------------------------------------------------ route
        // pos = (x, z) plus eye height above the ground at that spot; look = (x, z) plus height above the ground (Hollowmere), or absolute positions (stair, Cellars).
        struct Wp
        {
            public Vector3 Pos, Look; public float Fov;
            public Wp(float x, float z, float eye, float lx, float lz, float lh, float fov = 52f) { Pos = new Vector3(x, eye, z); Look = new Vector3(lx, lh, lz); Fov = fov; }
            public Wp(Vector3 pos, Vector3 look, float fov) { Pos = pos; Look = look; Fov = fov; }
        }

        struct Pose { public Vector3 Pos, Look; public float Fov; }

        sealed class Shot
        {
            public string Name; public Wp[] Pts; public bool GroundRelative;
            public float Duration;
            public float Ramp = 0.16f;                             // fraction of the shot spent easing in, and again easing out
            public float PosSigma = 0.30f, LookSigma = 0.80f, FovSigma = 1.0f;   // low-pass (gaussian, seconds)
            public float Length;                                   // metres of camera path, set by Bake

            Vector3 EvalPos(float g, Func<float, float, float> ground)
            {
                int i = Mathf.Min((int)g, Pts.Length - 2); float u = g - i;
                Vector3 p = CatmullRom(Pts[Mathf.Max(i - 1, 0)].Pos, Pts[i].Pos, Pts[i + 1].Pos, Pts[Mathf.Min(i + 2, Pts.Length - 1)].Pos, u);
                if (GroundRelative) p.y += ground(p.x, p.z);
                return p;
            }

            public Pose[] Bake(Func<float, float, float> ground)
            {
                int n = Mathf.RoundToInt(Duration * Fps) + 1, segs = Pts.Length - 1;
                const int S = 48;
                var gs = new List<float>(); var cum = new List<float>();
                Vector3 prev = default; float len = 0f;
                for (int i = 0; i < segs; i++)
                    for (int k = 0; k <= S; k++)
                    {
                        if (i > 0 && k == 0) continue;
                        float g = i + k / (float)S; Vector3 p = EvalPos(g, ground);
                        if (gs.Count > 0) len += Vector3.Distance(prev, p);
                        prev = p; gs.Add(g); cum.Add(len);
                    }
                Length = len;
                var pos = new Vector3[n]; var look = new Vector3[n]; var fov = new float[n];
                int c = 1;
                for (int j = 0; j < n; j++)
                {
                    float target = Trap(j / (float)(n - 1), Ramp) * len;
                    while (c < cum.Count - 1 && cum[c] < target) c++;
                    float span = Mathf.Max(1e-5f, cum[c] - cum[c - 1]);
                    float gp = Mathf.Lerp(gs[c - 1], gs[c], Mathf.Clamp01((target - cum[c - 1]) / span));
                    int i = Mathf.Min((int)gp, segs - 1); float u = gp - i;
                    pos[j] = EvalPos(gp, ground);
                    look[j] = CatmullRom(Pts[Mathf.Max(i - 1, 0)].Look, Pts[i].Look, Pts[i + 1].Look, Pts[Mathf.Min(i + 2, Pts.Length - 1)].Look, u);
                    if (GroundRelative) look[j].y += ground(look[j].x, look[j].z);
                    fov[j] = Mathf.Lerp(Pts[i].Fov, Pts[i + 1].Fov, u * u * (3f - 2f * u));
                }
                pos = Smooth(pos, PosSigma * Fps); look = Smooth(look, LookSigma * Fps); fov = Smooth(fov, FovSigma * Fps);
                var res = new Pose[n];
                for (int j = 0; j < n; j++) res[j] = new Pose { Pos = pos[j], Look = look[j], Fov = fov[j] };
                return res;
            }
        }

        // Trapezoid velocity profile: ease in over the first r, constant speed, ease out over the last r. x and result in 0..1.
        static float Trap(float x, float r)
        {
            r = Mathf.Clamp(r, 0.001f, 0.49f); float v = 1f / (1f - r);
            if (x < r) return v * x * x / (2f * r);
            if (x > 1f - r) return 1f - v * (1f - x) * (1f - x) / (2f * r);
            return v * (x - r / 2f);
        }

        static Vector3[] Smooth(Vector3[] a, float sigma)
        {
            if (sigma < 0.5f) return a;
            int rad = Mathf.CeilToInt(sigma * 3f); var w = new float[2 * rad + 1]; float sum = 0f;
            for (int k = -rad; k <= rad; k++) { w[k + rad] = Mathf.Exp(-k * k / (2f * sigma * sigma)); sum += w[k + rad]; }
            var r = new Vector3[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                Vector3 acc = Vector3.zero;
                for (int k = -rad; k <= rad; k++) acc += a[Mathf.Clamp(i + k, 0, a.Length - 1)] * w[k + rad];
                r[i] = acc / sum;
            }
            return r;
        }

        static float[] Smooth(float[] a, float sigma)
        {
            var v = new Vector3[a.Length];
            for (int i = 0; i < a.Length; i++) v[i] = new Vector3(a[i], 0f, 0f);
            v = Smooth(v, sigma);
            return v.Select(x => x.x).ToArray();
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        // Hollowmere: origin = the dry fountain (~5.8 m basin), square x -20..20, z -21..7 (Market Street leaves at (0,-20.6)); the heroes' home (cistern house, centre 12.5,11.25,
        // 9 x 7.5) fronts onto the square's north-east corner with its door in the south face near x 12.85; Keep Lane runs east from (18.6,6.1) to (45.5,12).
        // One arc round the south side of the fountain, rising to a crane high point mid-arc and settling again. The Rebuilt pass flies it back.
        static readonly Shot SquareShot = new Shot
        {
            Name = "square", GroundRelative = true, Duration = FDur, Ramp = 0.18f,
            Pts = new[]
            {
                new Wp(-17f,  -4f,   3.6f,   2f,  1f,   2.2f, 54f),
                new Wp(-15f,  -11f,  4.6f,   1f,  0.5f, 2.2f, 52f),
                new Wp(-9.5f, -16f,  5.6f,   0f,  0f,   2.4f, 50f),
                new Wp(0f,    -18f,  6.2f,   0f,  1f,   2.6f, 50f),
                new Wp(9.5f,  -16f,  5.6f,   0f,  0f,   2.4f, 50f),
                new Wp(15f,   -11f,  4.6f,  -1f,  0.5f, 2.2f, 52f),
                new Wp(17f,   -4f,   3.6f,  -2f,  1f,   2.2f, 54f),
            }
        };

        // Keep Lane from its east end, west toward the home, a long descending glide into the square's north-east corner, then the door, in, and on toward the stair.
        // Starts over the lane's far end (not the square), so the square is never revisited.
        static readonly Shot HomeShot = new Shot
        {
            Name = "lane+home", GroundRelative = true, Duration = CutStair - CutLane, Ramp = 0.12f,
            Pts = new[]
            {
                new Wp(44f,   12.2f, 4.6f,  30f,   11.4f, 2.6f, 50f),
                new Wp(36f,   11.0f, 4.0f,  22f,   10.5f, 2.4f, 50f),
                new Wp(27f,   11.5f, 3.4f,  19.5f, 9f,    2.2f, 50f),
                new Wp(21.5f, 9.5f,  3.0f,  15f,   4f,    2.0f, 52f),
                new Wp(19.4f, 5.0f,  2.8f,  13.5f, 6.5f,  1.8f, 52f),
                new Wp(16.2f, 1.6f,  2.2f,  12.8f, 7.2f,  1.6f, 54f),
                new Wp(13.4f, 3.4f,  1.7f,  12.85f, 7.4f, 1.5f, 56f),
                new Wp(12.85f, 5.9f, 1.55f, 12.85f, 9.0f, 1.4f, 58f),
                new Wp(12.85f, 8.8f, 1.5f,  14.2f, 12.0f, 1.0f, 58f),
                new Wp(13.4f, 10.6f, 1.45f, 15.0f, 12.5f, 0.2f, 58f),
            }
        };

        // Stair scene (absolute positions): from inside the house through the doorway, down flight A, round the turns, down flight C, into the vault's entry tunnel.
        static readonly Shot StairShot = new Shot
        {
            Name = "stair", Duration = CutCellar - CutStair, Ramp = 0.14f, LookSigma = 1.0f,
            Pts = new[]
            {
                new Wp(new Vector3(3.0f, 10.7f, 1.0f),  new Vector3(3.2f, 8.9f, -4.2f), 62f),
                new Wp(new Vector3(3.1f, 10.6f, -1.8f), new Vector3(3.2f, 8.0f, -6.0f), 62f),
                new Wp(new Vector3(3.2f, 10.4f, -3.9f), new Vector3(3.0f, 7.2f, -6.4f), 62f),
                new Wp(new Vector3(3.2f, 9.5f, -5.2f),  new Vector3(2.2f, 6.7f, -6.6f), 62f),
                new Wp(new Vector3(2.9f, 8.7f, -6.3f),  new Vector3(0.8f, 6.3f, -6.6f), 62f),
                new Wp(new Vector3(1.9f, 7.6f, -6.6f),  new Vector3(0.0f, 5.2f, -6.1f), 62f),
                new Wp(new Vector3(0.6f, 6.5f, -6.5f),  new Vector3(0.1f, 4.3f, -4.6f), 62f),
                new Wp(new Vector3(0.0f, 5.6f, -5.6f),  new Vector3(0.1f, 2.6f, -2.5f), 62f),
                new Wp(new Vector3(0.0f, 4.3f, -4.0f),  new Vector3(0.2f, 2.2f, 2.0f),  62f),
                new Wp(new Vector3(0.0f, 2.8f, -2.4f),  new Vector3(0.2f, 1.8f, 4.0f),  62f),
                new Wp(new Vector3(0.15f, 2.1f, -0.6f), new Vector3(0.2f, 1.5f, 5.0f),  62f),
            }
        };

        sealed class Plan
        {
            public Pose[] Poses; public string[] Seg; public int Count;
            public Pose At(int f) => Poses[Mathf.Clamp(f, 0, Count - 1)];
        }

        static int Cut(float t) => Mathf.RoundToInt(t * Fps);

        static Plan BuildPlan(TownLayout town, CellarFloorLayout layout)
        {
            Func<float, float, float> ground = (x, z) => HollowmereLayout.Ground(town, x, z);
            Pose[] sq = SquareShot.Bake(ground), home = HomeShot.Bake(ground), stair = StairShot.Bake(null);

            var (cp, ct) = CellarFloorTestBuilder.CorridorView(layout);
            var (rp0, rt0) = CellarFloorTestBuilder.RoomView(layout);
            Vector3 cDir = Flat(ct - cp), rDir = Flat(rt0 - rp0);
            float cLen = Vector3.Distance(Flat3(cp), Flat3(ct)), rLen = Vector3.Distance(Flat3(rp0), Flat3(rt0));
            Vector3 side = Vector3.Cross(Vector3.up, rDir) * 0.6f;
            var corridor = new Shot { Name = "corridor", Duration = CutRoom - CutCellar, Ramp = 0.2f, Pts = new[] { new Wp(cp, ct, 70f), new Wp(cp + cDir * (cLen * 0.3f), ct, 70f), new Wp(cp + cDir * (cLen * 0.6f), ct, 68f) } };
            var room = new Shot { Name = "room", Duration = Total - CutRoom, Ramp = 0.2f, Pts = new[] { new Wp(rp0, rt0, 78f), new Wp(rp0 + rDir * (rLen * 0.15f) + side, rt0, 76f), new Wp(rp0 + rDir * (rLen * 0.3f), rt0, 72f) } };
            Pose[] cor = corridor.Bake(null), rm = room.Bake(null);
            Debug.Log("SC TRAILER cellar corridor pos=" + cp + " target=" + ct + " room pos=" + rp0 + " target=" + rt0);

            int count = Mathf.RoundToInt(Total * Fps) + 1;
            var plan = new Plan { Poses = new Pose[count], Seg = new string[count], Count = count };
            int fCross0 = Cut(CrossStart), fCross1 = Cut(CrossEnd), fLane = Cut(CutLane), fStair = Cut(CutStair), fCellar = Cut(CutCellar), fRoom = Cut(CutRoom);
            for (int f = 0; f < count; f++)
            {
                if (f < fCross0) { plan.Poses[f] = sq[Mathf.Min(f, sq.Length - 1)]; plan.Seg[f] = "ruined sweep"; }
                else if (f < fCross1) { plan.Poses[f] = sq[sq.Length - 1]; plan.Seg[f] = "wipe hold"; }
                else if (f < fLane) { plan.Poses[f] = sq[Mathf.Clamp(sq.Length - 1 - (f - fCross1), 0, sq.Length - 1)]; plan.Seg[f] = "rebuilt sweep"; }
                else if (f < fStair) { plan.Poses[f] = home[Mathf.Min(f - fLane, home.Length - 1)]; plan.Seg[f] = "lane+home"; }
                else if (f < fCellar) { plan.Poses[f] = stair[Mathf.Min(f - fStair, stair.Length - 1)]; plan.Seg[f] = "stair"; }
                else if (f < fRoom) { plan.Poses[f] = cor[Mathf.Min(f - fCellar, cor.Length - 1)]; plan.Seg[f] = "cellar corridor"; }
                else { plan.Poses[f] = rm[Mathf.Min(f - fRoom, rm.Length - 1)]; plan.Seg[f] = "cellar room"; }
            }
            // Smoothness report: the biggest frame-to-frame camera move and turn inside each segment.
            foreach (string seg in plan.Seg.Distinct())
            {
                float maxStep = 0f, maxRot = 0f, sumStep = 0f; int n = 0, first = -1, last = -1;
                for (int f = 1; f < count; f++)
                {
                    if (plan.Seg[f] != seg || plan.Seg[f - 1] != seg) continue;
                    if (first < 0) first = f - 1; last = f;
                    float d = Vector3.Distance(plan.Poses[f].Pos, plan.Poses[f - 1].Pos);
                    float a = Quaternion.Angle(Rot(plan.Poses[f]), Rot(plan.Poses[f - 1]));
                    maxStep = Mathf.Max(maxStep, d); maxRot = Mathf.Max(maxRot, a); sumStep += d; n++;
                }
                Debug.Log("SC TRAILER SEGMENT " + seg + " t=" + (first / (float)Fps).ToString("0.0") + ".." + (last / (float)Fps).ToString("0.0") + " frames=" + n
                    + " maxStep=" + maxStep.ToString("0.000") + " m (" + (maxStep * Fps).ToString("0.00") + " m/s) maxRot=" + maxRot.ToString("0.000") + " deg (" + (maxRot * Fps).ToString("0.0") + " deg/s) pathMetres=" + sumStep.ToString("0.0"));
            }
            return plan;
        }

        static Quaternion Rot(Pose p) => Quaternion.LookRotation(p.Look - p.Pos, Vector3.up);

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
            if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(MainCheckoutRoot(projectRoot), "Reference", "private", "trailers", date + "-" + sha);   // the main checkout, so videos outlive a worktree
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

            TownLayout town = HollowmereLayout.Create();
            Plan plan = BuildPlan(town, CellarFloorGenerator.Generate(CellarFloorTestBuilder.Seed, CellarFloorTestBuilder.FloorNumber));   // the fixed seed

            bool noBuild = !string.IsNullOrEmpty(Env("SC_CAPTURE_NOBUILD"));
            if (!noBuild) { HollowmereBuilder.Build(); UndercroftStairBuilder.Build(); CellarFloorTestBuilder.Build(); }

            var stats = new Stats();
            RenderPipelineAsset savedQuality = QualitySettings.renderPipeline;
            try
            {
                RenderHollowmere(frames, frameDir, checkOnly, stats, plan);
                RenderSimple("stair", UndercroftStairBuilder.ScenePath, "Cap_Stair_Top", Cut(CutStair), Cut(CutCellar), true, frames, frameDir, checkOnly, stats, plan);
                RenderSimple("cellars", CellarFloorTestBuilder.ScenePath, "Floor_Room", Cut(CutCellar), int.MaxValue, false, frames, frameDir, checkOnly, stats, plan);
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
            float[] keyTimes = { 6.5f, 20.5f, 37f, 44.5f, 53f, 66f };   // ruined square, rebuilt square, lane, home door, stair, cellar
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
        static void RenderHollowmere(List<int> frames, string frameDir, bool checkOnly, Stats stats, Plan plan)
        {
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(BeautyCornerBuilder.RpAssetPath) ?? throw new FileNotFoundException(BeautyCornerBuilder.RpAssetPath);
            if (!checkOnly) QualitySettings.renderPipeline = rp;
            EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
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
                    // Ruined pass: t < CrossEnd. Rebuilt pass: CrossStart <= t < CutStair (the window CrossStart..CrossEnd blends over the ruined frames the first pass left).
                    var mine = frames.Where(f => { float t = f / (float)Fps; return t < CutStair && (ruined ? t < CrossEnd : t >= CrossStart); }).ToList();
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
                            Pose ps = plan.At(f);
                            if (!clear.Check(t, ps.Pos, ps.Look)) stats.Warnings++;
                            if (checkOnly) continue;
                            Place(cam, ps.Pos, ps.Look, ps.Fov);
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

        // ------------------------------------------------------------------------------------------------ stair and Cellars
        // A scene rendered through the look-test stack with one fixed camera moved by the plan. lamp = a warm point light riding with the camera (runtime only).
        static void RenderSimple(string label, string scenePath, string camName, int f0, int f1, bool lamp, List<int> frames, string frameDir, bool checkOnly, Stats stats, Plan plan)
        {
            var mine = frames.Where(f => f >= f0 && f < f1).ToList();
            if (mine.Count == 0) return;
            var rp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(LookTestBuilder.RpAssetPath) ?? throw new FileNotFoundException(LookTestBuilder.RpAssetPath);
            if (!checkOnly) QualitySettings.renderPipeline = rp;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Camera cam = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.name == camName)
                ?? throw new InvalidOperationException(camName + " camera not found: build the " + label + " scene first");

            GameObject lampGo = null;
            if (lamp)
            {
                lampGo = new GameObject("TrailerLamp") { hideFlags = HideFlags.DontSave };
                var l = lampGo.AddComponent<Light>();
                l.type = LightType.Point; l.color = new Color(1f, 0.76f, 0.5f); l.intensity = 2.2f; l.range = 6.5f; l.shadows = LightShadows.None;
            }
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
                        Pose ps = plan.At(f);
                        if (!clear.Check(t, ps.Pos, ps.Look)) stats.Warnings++;
                        if (checkOnly) continue;
                        Place(cam, ps.Pos, ps.Look, ps.Fov);
                        if (lampGo != null) lampGo.transform.position = ps.Pos + Vector3.up * 0.2f - cam.transform.forward * 0.4f;
                        RenderFrame(cam, rt, tex);
                        WriteFrame(tex, FramePath(frameDir, f), Fade(t));
                        stats.Rendered++;
                    }
                }
                finally { clear.Teardown(); }
            }
            finally
            {
                if (lampGo != null) UnityEngine.Object.DestroyImmediate(lampGo);
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

        static void Place(Camera cam, Vector3 pos, Vector3 look, float fov)
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

        // 1 = full picture. Fade in at the start, out at the end, a short dip to black at each cut (Lane, Stair, Cellar corridor, Cellar room).
        static float Fade(float t)
        {
            float f = Mathf.Clamp01(t / FadeIn) * Mathf.Clamp01((Total - t) / FadeOut);
            foreach (float cut in new[] { CutLane, CutStair, CutCellar, CutRoom }) f = Mathf.Min(f, Mathf.Clamp01(Mathf.Abs(t - cut) / DipHalf));
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
            foreach (string a in new[] { "-y", "-framerate", fps.ToString(), "-i", Path.Combine(seqDir, "s_%05d.png"), "-vf", vf, "-c:v", "libx264", "-preset", "slow", "-crf", "26", "-pix_fmt", "yuv420p", "-movflags", "+faststart", mp4 }) psi.ArgumentList.Add(a);
            using (Process p = Process.Start(psi))
            {
                string err = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(20 * 60 * 1000)) { try { p.Kill(); } catch { } throw new TimeoutException("ffmpeg timed out"); }
                if (p.ExitCode != 0) throw new InvalidOperationException("ffmpeg failed (" + p.ExitCode + "): " + err.Substring(Mathf.Max(0, err.Length - 1500)));
            }
        }

        // The checkout that owns the .git folder (the repo root itself when not in a linked worktree).
        static string MainCheckoutRoot(string projectRoot)
        {
            string common = Git(projectRoot, "rev-parse --git-common-dir");
            if (common == "nogit") return projectRoot;
            string gitDir = Path.GetFullPath(Path.IsPathRooted(common) ? common : Path.Combine(projectRoot, common));
            return Path.GetDirectoryName(gitDir.TrimEnd('\\', '/'));
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
