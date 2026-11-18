// T26 judging extras for HollowmereCapture (editor only, no scene content):
//   1:1 crops beside every eye frame: <frame>_crop_wall.jpg (where a near-vertical surface meets ground in the lower half) and <frame>_crop_ground.jpg (a ground patch in the lower third),
//   both 640x360 pixels of the 1920x1080 frame, no scaling. Picked by raycasting a pixel grid from the camera against the scene's colliders (decals and plants have none, so the walls and
//   ground that count are the town's solid ones). SC_CAPTURE_CROPS=0 turns them off.
//   SC_CAPTURE_MOTION=1 (with SC_CAPTURE_ONLY): <frame>_motion.jpg, 4 frames over 2 s of game time (birds, butterflies, reeds, smoke) as a 2x2 grid of half-size tiles, in reading order.
//   SC_CAPTURE_MILESTONE=1: SC_RANDOM_EYES (default 3) seeded random eye cameras on walkable ground inside the wall (eye 1.65 m, level gaze), seed SC_RANDOM_SEED (default 26),
//   files HM_Rand_<n>_<preset>.png, and every named camera also at Evening (the one dusk preset the capture code has; there is no overcast preset).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereCaptureExtras
    {
        const int CropW = 640, CropH = 360;

        public static bool CropsOn => System.Environment.GetEnvironmentVariable("SC_CAPTURE_CROPS") != "0";
        public static bool MotionOn => System.Environment.GetEnvironmentVariable("SC_CAPTURE_MOTION") == "1";
        public static bool MilestoneOn => System.Environment.GetEnvironmentVariable("SC_CAPTURE_MILESTONE") == "1";
        public static bool IsEye(Camera c) => c.name.StartsWith("Cap_HM_Eye_") || c.name.StartsWith("Cap_HM_Rand_");

        // ------------------------------------------------------------------------------------------------ crops
        struct Hit { public bool Ok; public float Dist; public Vector3 Normal; public Vector3 Point; public Collider Col; }

        static Hit[,] Grid(Camera cam, int cols, int rows, int y0, int step)
        {
            var g = new Hit[cols, rows];
            Transform t = cam.transform; float th = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), aspect = (float)LookTestCapture.Width / LookTestCapture.Height;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float px = (c + 0.5f) * step, py = y0 + r * step;
                    float nx = px / LookTestCapture.Width * 2f - 1f, ny = 1f - py / LookTestCapture.Height * 2f;
                    Vector3 dir = (t.forward + t.right * (nx * th * aspect) + t.up * (ny * th)).normalized;
                    if (Physics.Raycast(t.position, dir, out RaycastHit h, 60f, ~0, QueryTriggerInteraction.Ignore))
                        g[c, r] = new Hit { Ok = true, Dist = h.distance, Normal = h.normal, Point = h.point, Col = h.collider };
                }
            return g;
        }

        // Pixel centres (in screen coordinates, y from the top) of the wall-foot and the ground patch for this camera.
        public static void PickCrops(Camera cam, out Vector2 wall, out Vector2 ground)
        {
            Physics.SyncTransforms();
            const int step = 30, cols = LookTestCapture.Width / step;
            int y0 = LookTestCapture.Height / 2, rows = (LookTestCapture.Height - y0) / step;
            Hit[,] g = Grid(cam, cols, rows, y0, step);
            wall = new Vector2(LookTestCapture.Width * 0.5f, LookTestCapture.Height * 0.72f); ground = new Vector2(LookTestCapture.Width * 0.5f, LookTestCapture.Height * 0.86f);
            float bestW = float.MaxValue, bestG = float.MaxValue;
            for (int r = 1; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    Hit lo = g[c, r], up = g[c, r - 1];
                    float px = (c + 0.5f) * step, py = y0 + r * step, cx = Mathf.Abs(px - LookTestCapture.Width * 0.5f) / (LookTestCapture.Width * 0.5f);
                    // wall foot: ground below, a near-vertical surface right above it, a collider taller than 2.2 m (a wall, not a crate), both within a metre of each other in depth, 2.5-16 m out; nearest wins, the centre breaks ties
                    if (lo.Ok && up.Ok && lo.Normal.y > 0.8f && up.Normal.y < 0.35f && up.Col.bounds.size.y > 2.2f && Mathf.Abs(lo.Dist - up.Dist) < 1.2f && lo.Dist >= 2.5f && lo.Dist <= 16f)
                    {
                        float s = lo.Dist + cx * 6f;
                        if (s < bestW) { bestW = s; wall = new Vector2(px, py - step * 0.5f); }
                    }
                    // ground patch: lower third, open ground 2-9 m out, flat; nearest to the centre column
                    if (py >= LookTestCapture.Height * 2f / 3f && lo.Ok && lo.Normal.y > 0.9f && lo.Dist >= 2f && lo.Dist <= 9f)
                    {
                        float s = cx * 10f + Mathf.Abs(lo.Dist - 4f);
                        if (s < bestG) { bestG = s; ground = new Vector2(px, py); }
                    }
                }
        }

        static void WriteCrop(Texture2D frame, Vector2 centre, string path)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(centre.x) - CropW / 2, 0, LookTestCapture.Width - CropW);
            int yTop = Mathf.Clamp(Mathf.RoundToInt(centre.y) - CropH / 2, 0, LookTestCapture.Height - CropH);
            int y = LookTestCapture.Height - yTop - CropH;   // texture rows run bottom-up
            var crop = new Texture2D(CropW, CropH, TextureFormat.RGB24, false, false);
            crop.SetPixels(frame.GetPixels(x, y, CropW, CropH)); crop.Apply();
            File.WriteAllBytes(path, crop.EncodeToJPG(95));
            Object.DestroyImmediate(crop);
        }

        // Renders cam to <frame>.png and, for an eye camera, the two crops beside it.
        public static void SaveWithCrops(Camera cam, string pngPath)
        {
            if (!CropsOn || !IsEye(cam)) { LookTestCapture.Save(cam, pngPath); return; }
            PickCrops(cam, out Vector2 wall, out Vector2 ground);
            string stem = pngPath.Substring(0, pngPath.Length - 4);
            LookTestCapture.Save(cam, pngPath, tex => { WriteCrop(tex, wall, stem + "_crop_wall.jpg"); WriteCrop(tex, ground, stem + "_crop_ground.jpg"); });
            Debug.Log("SC CAPTURE crops " + cam.name + " wall " + wall + " ground " + ground);
        }

        // ------------------------------------------------------------------------------------------------ motion strip
        public static void SaveMotion(Camera cam, string jpgPath)
        {
            const float t0 = 37f, span = 2f; const int frames = 4;   // 37 s is the builder's settled pose
            var creatures = Object.FindObjectsByType<AmbientCreatures>(FindObjectsSortMode.None);
            var reeds = Object.FindObjectsByType<AmbientReedSway>(FindObjectsSortMode.None);
            var smoke = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            foreach (ParticleSystem ps in smoke) ps.Simulate(14f, true, true);
            int w = LookTestCapture.Width / 2, h = LookTestCapture.Height / 2;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false, false);
            float dt = span / (frames - 1);
            for (int i = 0; i < frames; i++)
            {
                float t = t0 + i * dt;
                if (i > 0) foreach (ParticleSystem ps in smoke) ps.Simulate(dt, true, false);
                foreach (AmbientCreatures c in creatures) c.Pose(t, i > 0 ? dt : 0f, false);
                foreach (AmbientReedSway r in reeds) r.Pose(t);
                int tile = i;
                LookTestCapture.Save(cam, jpgPath, tex =>
                {
                    Color32[] px = tex.GetPixels32(); var o = new Color32[w * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            Color32 a = px[2 * y * tex.width + 2 * x], b = px[2 * y * tex.width + 2 * x + 1], c2 = px[(2 * y + 1) * tex.width + 2 * x], d = px[(2 * y + 1) * tex.width + 2 * x + 1];
                            o[y * w + x] = new Color32((byte)((a.r + b.r + c2.r + d.r + 2) / 4), (byte)((a.g + b.g + c2.g + d.g + 2) / 4), (byte)((a.b + b.b + c2.b + d.b + 2) / 4), 255);
                        }
                    int sx = (tile % 2) * w, sy = (1 - tile / 2) * h;   // reading order, texture rows bottom-up
                    sheet.SetPixels32(sx, sy, w, h, o);
                }, writeFile: false);
            }
            sheet.Apply();
            File.WriteAllBytes(jpgPath, sheet.EncodeToJPG(92));
            Object.DestroyImmediate(sheet);
            foreach (AmbientCreatures c in creatures) c.Pose(t0, 0f, false);
            Debug.Log("SC CAPTURE wrote " + jpgPath + " (4 frames over " + span + " s)");
        }

        // ------------------------------------------------------------------------------------------------ milestone random eyes
        // Creates (in the open scene, never saved) Cap_HM_Rand_<n> cameras at seeded walkable spots inside the town wall.
        public static List<Camera> MakeRandomEyes()
        {
            var made = new List<Camera>();
            int want = int.TryParse(System.Environment.GetEnvironmentVariable("SC_RANDOM_EYES"), out int n) && n > 0 ? n : 3;
            int seed = int.TryParse(System.Environment.GetEnvironmentVariable("SC_RANDOM_SEED"), out int s) ? s : 26;
            var rng = new System.Random(seed);
            Physics.SyncTransforms();
            for (int attempt = 0; attempt < 4000 && made.Count < want; attempt++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * 70f, z = (float)(rng.NextDouble() * 2 - 1) * 60f;
                if (HollowmereLayout.PlateauDistance(x, z) > HollowmereLayout.TownWallOffset - 4f) continue;   // inside the wall, clear of the wall itself
                if (!Physics.Raycast(new Vector3(x, 90f, z), Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.93f) continue;
                Vector3 eye = hit.point + Vector3.up * 1.65f;
                if (Physics.OverlapSphere(eye, 0.45f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;   // not inside a prop or a wall
                float yaw = (float)rng.NextDouble() * 360f; Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                if (Physics.Raycast(eye, dir, 3.5f, ~0, QueryTriggerInteraction.Ignore)) continue;   // not facing a wall at arm's length
                Camera cam = LookTestBuilder.AddCapture(null, "Cap_HM_Rand_" + made.Count, eye, eye + dir * 10f, 60f);   // same camera set-up as the builder's own
                cam.farClipPlane = HollowmereRoad.FarClip;
                made.Add(cam);
                Debug.Log("SC CAPTURE random eye " + cam.name + " pos " + eye.ToString("F2") + " yaw " + yaw.ToString("F0") + " seed " + seed);
            }
            return made;
        }
    }
}
