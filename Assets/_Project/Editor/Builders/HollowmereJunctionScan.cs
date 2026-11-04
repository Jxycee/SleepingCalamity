// Junction scan of the built Hollowmere scene (both presets): LARGE pieces that do not meet naturally. Survey only; the scene is opened, never saved.
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.HollowmereJunctionScan.Scan -OutDir <scratch>   (env SC_SCAN_DIR = report folder; SC_CAPTURE_NOBUILD=1 skips the build)
// Kinds per preset:
//   stair_angled   two flights whose ends lie within 2 m, ground-plane axes differing by > 12 deg (no bend or landing between them)
//   stair_butt     a flight end whose next 0.25 m (3 points across the width, 2 or more) lies inside a wall-class box
//   stair_dead     a flight end whose onward ground is > 0.7 m off the tread level and no other flight within 3 m (drop, or a bank)
//   wall_through   a wall that passes completely through another wall / building shell / stair piece (both ends outside the far faces)
//   wall_poke      a wall end that pokes > 0.05 m out past the far face of the other piece (and is embedded > 0.35 m)
//   wall_embed     a wall end embedded > 0.8 m without exiting (kept low in the ranking: usually hidden)
//   wall_overlap   near-parallel (< 15 deg) walls overlapping > 0.6 m
// Sources: LaneKitPlan rows (flights) + scene box colliders/mesh bounds (walls, buildings, stairs); keep flights from the Keep group (Step<n> children).
// Output in SC_SCAN_DIR: junctions_<preset>.tsv, junctions_ranked.md (top 25 over both presets), junction_cams.txt (suggested camera tuples for the top 10).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereJunctionScan
    {
        enum Cls { Wall, Building, Stair }

        // Oriented box: vertical = world Y; two horizontal axes A (half HA) and B (half HB).
        class Box
        {
            public string Name, Path; public Cls Cls; public Vector2 C; public Vector2 A, B; public float HA, HB, Y0, Y1; public string Lane;
            public Vector2 Long => HA >= HB ? A : B;
            public float HL => Mathf.Max(HA, HB);
            public float HT => Mathf.Min(HA, HB);
            public Vector2 Thin => HA >= HB ? B : A;
            public bool Inside(Vector2 p, float m = 0f)
            {
                Vector2 d = p - C;
                return Mathf.Abs(Vector2.Dot(d, A)) <= HA + m && Mathf.Abs(Vector2.Dot(d, B)) <= HB + m;
            }
        }

        class Flight
        {
            public string Id, Source, Lane; public Vector2 Bot, Top, Dir; public float BotY, TopY, Width; public int Steps;
        }

        class Fault
        {
            public string Kind, Preset, Name, Other, Info, Source; public Vector3 Pos; public float Size, Depth, Poke, Score;
        }

        static readonly List<Fault> Faults = new List<Fault>();
        static TownLayout _layout;

        public static void Scan()
        {
            string dir = Environment.GetEnvironmentVariable("SC_SCAN_DIR");
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("Set SC_SCAN_DIR to the report folder");
            Directory.CreateDirectory(dir);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SC_CAPTURE_NOBUILD"))) HollowmereBuilder.Build();
            Faults.Clear();
            _layout = HollowmereLayout.Create();
            List<LaneKitRow> rows = LaneKitPlan.Plan(_layout);
            var summary = new StringBuilder();
            EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Single);
            foreach (HollowmereBuilder.Preset preset in new[] { HollowmereBuilder.Preset.Ruined, HollowmereBuilder.Preset.Rebuilt })
            {
                HollowmereBuilder.ApplyPreset(preset);
                string p = preset.ToString();
                bool rebuilt = preset == HollowmereBuilder.Preset.Rebuilt;
                List<Box> boxes = CollectBoxes();
                List<Flight> flights = LaneFlights(rows, rebuilt);
                flights.AddRange(KeepFlights());
                int before = Faults.Count;
                var fl = new StringBuilder("id\tsource\tlane\tbotX\tbotY\tbotZ\ttopX\ttopY\ttopZ\tdirYaw\twidth\tsteps\n");
                foreach (Flight f in flights) fl.Append(f.Id + "\t" + f.Source + "\t" + f.Lane + "\t" + F(f.Bot.x) + "\t" + F(f.BotY) + "\t" + F(f.Bot.y) + "\t" + F(f.Top.x) + "\t" + F(f.TopY) + "\t" + F(f.Top.y) + "\t"
                    + F(Mathf.Atan2(f.Dir.x, f.Dir.y) * Mathf.Rad2Deg) + "\t" + F(f.Width) + "\t" + f.Steps + "\n");
                File.WriteAllText(System.IO.Path.Combine(dir, "flights_" + p + ".tsv"), fl.ToString());
                StairJunctions(p, flights, boxes);
                WallPiercing(p, boxes);
                foreach (Fault f in Faults.Skip(before)) f.Score = Score(f);
                Debug.Log("SC JUNCTION preset " + p + " walls=" + boxes.Count(b => b.Cls == Cls.Wall) + " buildings=" + boxes.Count(b => b.Cls == Cls.Building) + " stairs=" + boxes.Count(b => b.Cls == Cls.Stair)
                          + " flights=" + flights.Count + " faults=" + (Faults.Count - before));
                foreach (var g in Faults.Skip(before).GroupBy(f => f.Kind)) { Debug.Log("SC JUNCTION " + p + " " + g.Key + " count=" + g.Count()); summary.Append(p + " " + g.Key + " " + g.Count() + "\n"); }
                var tsv = new StringBuilder("kind\tpreset\tname\tother\tx\ty\tz\tdepth\tpoke\tsize\tscore\tinfo\n");
                foreach (Fault f in Faults.Skip(before).OrderByDescending(f => f.Score)) tsv.Append(Line(f)).Append('\n');
                File.WriteAllText(System.IO.Path.Combine(dir, "junctions_" + p + ".tsv"), tsv.ToString());
            }
            WriteRanked(dir, summary.ToString());
            Debug.Log("SC JUNCTION DONE dir=" + dir);
        }

        static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Line(Fault f) => f.Kind + "\t" + f.Preset + "\t" + f.Name + "\t" + f.Other + "\t" + F(f.Pos.x) + "\t" + F(f.Pos.y) + "\t" + F(f.Pos.z) + "\t" + F(f.Depth) + "\t" + F(f.Poke) + "\t" + F(f.Size) + "\t" + F(f.Score) + "\t" + f.Info;

        // ------------------------------------------------------------------------------------------------ boxes from the scene
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return "/" + string.Join("/", parts) + "/";
        }

        static Box FromBoxCollider(BoxCollider bc, string name, string path, Cls cls)
        {
            Transform t = bc.transform; Vector3 ls = t.lossyScale;
            Vector3 c = t.TransformPoint(bc.center), half = new Vector3(Mathf.Abs(bc.size.x * ls.x), Mathf.Abs(bc.size.y * ls.y), Mathf.Abs(bc.size.z * ls.z)) * 0.5f;
            return Build(name, path, cls, c, half, t.right, t.up, t.forward);
        }

        static Box FromBounds(Bounds lb, Transform t, string name, string path, Cls cls)
        {
            Vector3 ls = t.lossyScale;
            return Build(name, path, cls, t.TransformPoint(lb.center), new Vector3(lb.extents.x * Mathf.Abs(ls.x), lb.extents.y * Mathf.Abs(ls.y), lb.extents.z * Mathf.Abs(ls.z)), t.right, t.up, t.forward);
        }

        // Find the local axis that is vertical; the other two become A and B.
        static Box Build(string name, string path, Cls cls, Vector3 c, Vector3 half, Vector3 ax, Vector3 ay, Vector3 az)
        {
            Vector3[] axes = { ax.normalized, ay.normalized, az.normalized };
            float[] h = { half.x, half.y, half.z };
            int up = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[up].y)) up = i;
            int a = (up + 1) % 3, b = (up + 2) % 3;
            Vector2 A = new Vector2(axes[a].x, axes[a].z), B = new Vector2(axes[b].x, axes[b].z);
            // vertical extent: the vertical axis half plus the tilt of the horizontal ones (kerbs and ramps only; walls are upright)
            float hy = h[up] * Mathf.Abs(axes[up].y) + h[a] * Mathf.Abs(axes[a].y) + h[b] * Mathf.Abs(axes[b].y);
            if (A.sqrMagnitude < 1e-6f || B.sqrMagnitude < 1e-6f) return null;
            return new Box { Name = name, Path = path, Cls = cls, C = new Vector2(c.x, c.z), A = A.normalized, B = B.normalized, HA = h[a] * A.magnitude, HB = h[b] * B.magnitude, Y0 = c.y - hy, Y1 = c.y + hy };
        }

        static HashSet<Renderer> LowLods()
        {
            var set = new HashSet<Renderer>();
            foreach (LODGroup g in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                LOD[] lods = g.GetLODs();
                for (int i = 1; i < lods.Length; i++) if (lods[i].renderers != null) foreach (Renderer r in lods[i].renderers) if (r != null) set.Add(r);
            }
            return set;
        }

        static List<Box> CollectBoxes()
        {
            var list = new List<Box>();
            HashSet<Renderer> low = LowLods();
            var seen = new HashSet<Transform>();
            foreach (BoxCollider bc in UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!bc.enabled || bc.isTrigger) continue;
                Transform t = bc.transform; string path = PathOf(t), lower = path.ToLowerInvariant(), n = t.name.ToLowerInvariant();
                Cls? cls = ClassOf(lower, n);
                if (cls == null || cls == Cls.Building || !seen.Add(t)) continue;   // buildings come from their renderers below
                Box b = FromBoxCollider(bc, t.name, path, cls.Value);
                if (b != null) { if (b.Cls == Cls.Wall) b.Y0 = Mathf.Max(b.Y0, Ground(b.C)); list.Add(b); }   // only the part above the ground can be seen
            }
            foreach (MeshRenderer r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || low.Contains(r) || !r.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;
                Transform t = r.transform; string path = PathOf(t), lower = path.ToLowerInvariant(), n = t.name.ToLowerInvariant();
                Cls? cls = ClassOf(lower, n);
                if (cls == null || !seen.Add(t)) continue;
                Box b = FromBounds(mf.sharedMesh.bounds, t, t.name, path, cls.Value);
                if (b != null) list.Add(b);
            }
            return list;
        }

        // Wall class: lane garden / retaining walls (piers, buttresses, gate piers and ends excluded), town wall modules, keep wall segments.
        // Building: house shell renderers and landmark bodies. Stair: lane treads, keep treads and cores, wall-walk stair fittings.
        static Cls? ClassOf(string lower, string n)
        {
            if (lower.Contains("/lanes/gardenwalls/") && n.Contains("gardenwall") && !n.Contains("end")) return Cls.Wall;
            if (lower.Contains("/lanes/retaining/") && n.Contains("retainwall")) return Cls.Wall;
            if (lower.Contains("/townwall/") && (n.Contains("_straight") || n.Contains("_patched") || n.Contains("_crumbled"))) return Cls.Wall;
            if (lower.Contains("/keep/") && n.StartsWith("keepwall")) return Cls.Wall;
            if (lower.Contains("/lanes/steps/") && n.Contains("lanestep") && !n.Contains("tread")) return Cls.Stair;
            if (lower.Contains("/keep/") && (n.StartsWith("core") || n.StartsWith("step"))) return Cls.Stair;
            if (lower.Contains("/townwall/") && n.Contains("_stair")) return Cls.Stair;
            if ((n == "walls" || n == "shell" || n == "plinth") && !lower.Contains("/lod")) return Cls.Building;
            return null;
        }

        // ------------------------------------------------------------------------------------------------ flights
        static List<Flight> LaneFlights(List<LaneKitRow> rows, bool rebuilt)
        {
            var flights = new List<Flight>();
            List<LaneKitRow> steps = rows.Where(r => r.Piece == LanePiece.LaneStep && r.In(rebuilt)).ToList();
            var cur = new List<LaneKitRow>();
            void Flush()
            {
                if (cur.Count == 0) return;
                LaneKitRow f0 = cur[0];
                float yaw = f0.Yaw * Mathf.Deg2Rad; Vector2 u = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                float smax = cur.Max(r => Vector2.Dot(new Vector2(r.X, r.Z), u)), smin = cur.Min(r => Vector2.Dot(new Vector2(r.X, r.Z), u));
                var topRows = cur.Where(r => Vector2.Dot(new Vector2(r.X, r.Z), u) > smax - 0.1f).ToList();
                var botRows = cur.Where(r => Vector2.Dot(new Vector2(r.X, r.Z), u) < smin + 0.1f).ToList();
                Vector2 tc = new Vector2(topRows.Average(r => r.X), topRows.Average(r => r.Z)) + u * LaneKitPlan.StepRun, bc = new Vector2(botRows.Average(r => r.X), botRows.Average(r => r.Z));
                flights.Add(new Flight
                {
                    Id = "F:" + f0.Id, Source = "lane", Lane = f0.Lane, Bot = bc, Top = tc, Dir = u, BotY = botRows.Average(r => r.Y) + LaneKitPlan.StepVisible, TopY = topRows.Average(r => r.Y) + LaneKitPlan.StepVisible,
                    Width = topRows.Count * 2f * f0.ScaleX, Steps = cur.Count / Mathf.Max(1, topRows.Count)
                });
                cur.Clear();
            }
            foreach (LaneKitRow r in steps)
            {
                if (cur.Count > 0)
                {
                    LaneKitRow p = cur[cur.Count - 1];
                    float yaw = p.Yaw * Mathf.Deg2Rad; Vector2 u = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)), right = new Vector2(u.y, -u.x), d = new Vector2(r.X - p.X, r.Z - p.Z);
                    bool same = r.Lane == p.Lane && Mathf.Abs(Mathf.DeltaAngle(r.Yaw, p.Yaw)) < 1f && Mathf.Abs(Vector2.Dot(d, u)) <= 0.6f && Mathf.Abs(Vector2.Dot(d, right)) <= 2f * p.ScaleX * 6f + 0.1f;
                    if (!same) Flush();
                }
                cur.Add(r);
            }
            Flush();
            return flights.Where(f => f.Steps >= 2).ToList();
        }

        static List<Flight> KeepFlights()
        {
            var list = new List<Flight>();
            var seen = new HashSet<Transform>();
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Step") || t.parent == null || !seen.Add(t.parent) || !PathOf(t).ToLowerInvariant().Contains("/keep/")) continue;
                Transform g = t.parent; var steps = new List<Transform>();
                foreach (Transform c in g) if (c.name.StartsWith("Step") && c.gameObject.activeInHierarchy) steps.Add(c);
                if (steps.Count < 3) continue;
                steps.Sort((a, b) => a.position.y.CompareTo(b.position.y));
                Transform lo = steps[0], hi = steps[steps.Count - 1];
                Vector3 fwd = lo.forward; Vector2 u = new Vector2(fwd.x, fwd.z).normalized;
                float pitch = 0.367f * lo.lossyScale.z, rise = 0.222f * lo.lossyScale.y;
                list.Add(new Flight
                {
                    Id = "K:" + PathOf(g).Replace("/Hollowmere/", ""), Source = "keep", Lane = g.name, Bot = new Vector2(lo.position.x, lo.position.z), Top = new Vector2(hi.position.x, hi.position.z) + u * pitch,
                    Dir = u, BotY = lo.position.y, TopY = hi.position.y + rise, Width = 2f * lo.lossyScale.x, Steps = steps.Count
                });
            }
            return list;
        }

        static float Ground(Vector2 p) => HollowmereLayout.Ground(_layout, p.x, p.y);

        static void StairJunctions(string preset, List<Flight> flights, List<Box> boxes)
        {
            List<Box> walls = boxes.Where(b => b.Cls == Cls.Wall).ToList();
            foreach (Flight f in flights)
            {
                Vector2 right = new Vector2(f.Dir.y, -f.Dir.x);
                foreach (bool top in new[] { true, false })
                {
                    Vector2 end = top ? f.Top : f.Bot, onward = top ? f.Dir : -f.Dir; float endY = top ? f.TopY : f.BotY;
                    Vector3 pos = new Vector3(end.x, endY, end.y);
                    string endName = top ? "top" : "bottom";
                    float size = f.Width * (1f + 0.15f * f.Steps);
                    // neighbours
                    bool near = false;
                    foreach (Flight o in flights)
                    {
                        if (o == f) continue;
                        foreach (bool ot in new[] { true, false })
                        {
                            Vector2 e2 = ot ? o.Top : o.Bot; float y2 = ot ? o.TopY : o.BotY;
                            float d = Vector2.Distance(end, e2);
                            if (d > 3f || Mathf.Abs(y2 - endY) > 1.5f) continue;
                            near = true;
                            if (d > 2f || string.CompareOrdinal(f.Id, o.Id) > 0) continue;   // each pair once
                            float ang = Vector2.Angle(f.Dir, o.Dir); if (ang > 90f) ang = 180f - ang;
                            if (ang <= 12f) continue;
                            Faults.Add(new Fault { Kind = "stair_angled", Preset = preset, Name = f.Id + ":" + endName, Other = o.Id + ":" + (ot ? "top" : "bottom"), Pos = pos, Size = size, Depth = d, Poke = ang, Source = f.Source + "/" + o.Source,
                                                   Info = "yawDelta=" + F(ang) + " dist=" + F(d) + " dy=" + F(y2 - endY) + " lane=" + f.Lane + "/" + o.Lane });
                        }
                    }
                    // wall face
                    int hit = 0; Box hitBox = null;
                    foreach (float lat in new[] { -0.6f, 0f, 0.6f })
                    {
                        Vector2 q = end + onward * 0.25f + right * (lat * f.Width * 0.5f);
                        foreach (Box w in walls) if (w.Inside(q) && w.Y1 > endY + 0.3f && w.Y0 < endY + 0.6f) { hit++; hitBox = w; break; }
                    }
                    if (hit >= 2) Faults.Add(new Fault { Kind = "stair_butt", Preset = preset, Name = f.Id + ":" + endName, Other = hitBox.Name, Pos = pos, Size = size, Depth = hit, Source = f.Source, Info = "points=" + hit + "/3 lane=" + f.Lane });
                    // dead end
                    float ahead = Ground(end + onward * 1.2f), diff = ahead - endY;
                    if (!near && Mathf.Abs(diff) > 0.7f && f.Source == "lane")
                        Faults.Add(new Fault { Kind = "stair_dead", Preset = preset, Name = f.Id + ":" + endName, Other = diff < 0 ? "drop" : "bank", Pos = pos, Size = size * 0.7f, Depth = Mathf.Abs(diff), Source = f.Source, Info = "ground ahead " + F(diff) + " m vs tread lane=" + f.Lane });
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ wall piercing
        static void WallPiercing(string preset, List<Box> boxes)
        {
            var best = new Dictionary<string, Fault>();
            foreach (Box w in boxes)
            {
                if (w.Cls != Cls.Wall) continue;
                foreach (Box o in boxes)
                {
                    if (o == w || o.Cls == Cls.Stair && o.Path == w.Path) continue;
                    if ((o.C - w.C).sqrMagnitude > Sq(w.HL + o.HL + o.HT + 1f)) continue;
                    float vo = Mathf.Min(w.Y1, o.Y1) - Mathf.Max(w.Y0, o.Y0);
                    if (vo < 0.4f) continue;
                    Vector2 L = w.Long; float hL = w.HL;
                    // chord of the infinite centre line through o, in o's frame
                    Vector2 d = w.C - o.C; float c0 = -1e9f, c1 = 1e9f;
                    if (!Slab(Vector2.Dot(d, o.A), Vector2.Dot(L, o.A), o.HA, ref c0, ref c1) || !Slab(Vector2.Dot(d, o.B), Vector2.Dot(L, o.B), o.HB, ref c0, ref c1)) continue;
                    float t0 = Mathf.Max(c0, -hL), t1 = Mathf.Min(c1, hL), inside = t1 - t0;
                    if (inside < 0.05f) continue;
                    float ang = Vector2.Angle(L, o.Cls == Cls.Wall ? o.Long : o.Long); if (ang > 90f) ang = 180f - ang;
                    bool endA = c0 <= -hL && c1 >= -hL, endB = c0 <= hL && c1 >= hL;
                    string kind = null; float poke = 0f, depth = inside;
                    if (o.Cls == Cls.Wall && ang < 15f) { if (inside > 0.6f && (o.C - w.C).magnitude > 0.3f) kind = "wall_overlap"; }   // stacked retaining layers share their centre: by design
                    else if (!endA && !endB)
                    {
                        // both ends outside the chord: the wall enters one face and leaves the other. stubA/stubB = wall length past each face of o.
                        float stubA = c0 + hL, stubB = hL - c1, ms = Mathf.Min(stubA, stubB), Ms = Mathf.Max(stubA, stubB);
                        poke = ms;
                        if (ms > 0.4f) kind = "wall_through";
                        else if (ms > 0.05f && Ms > 0.4f) kind = "wall_poke";
                    }
                    else if (endA && endB) { if (inside > 1.6f) kind = "wall_embed"; }
                    else if (inside > 0.8f) kind = "wall_embed";
                    if (kind == null) continue;
                    float tcen = (t0 + t1) * 0.5f; Vector2 hitPt = w.C + L * tcen;
                    float gy = (Mathf.Max(w.Y0, o.Y0) + Mathf.Min(w.Y1, o.Y1)) * 0.5f;
                    float size = vo * Mathf.Max(0.3f, Mathf.Min(depth, 2.5f)) + (kind == "wall_through" ? vo * Mathf.Min(poke, 3f) : 0f);
                    var f = new Fault { Kind = kind, Preset = preset, Name = w.Name, Other = o.Name, Pos = new Vector3(hitPt.x, gy, hitPt.y), Size = size, Depth = depth, Poke = poke, Source = o.Cls.ToString(),
                                        Info = "angle=" + F(ang) + " vOverlap=" + F(vo) + " wallPath=" + Short(w.Path) + " otherPath=" + Short(o.Path) };
                    string key = string.CompareOrdinal(w.Path, o.Path) < 0 ? w.Path + "|" + o.Path : o.Path + "|" + w.Path;
                    if (!best.TryGetValue(key, out Fault prev) || f.Size > prev.Size) best[key] = f;
                }
            }
            // wall_poke: a wall END that leaves the far side of a thin wall / building: detect through the second pass on the end points
            foreach (Fault f in best.Values) Faults.Add(f);
        }

        static string Short(string p) { int i = p.IndexOf("/Hollowmere/", StringComparison.Ordinal); return i >= 0 ? p.Substring(i + 12) : p; }
        static float Sq(float v) => v * v;

        // Slab clip of the line x(t) = o + t*d against |x| <= h; false when the line misses.
        static bool Slab(float o, float d, float h, ref float t0, ref float t1)
        {
            if (Mathf.Abs(d) < 1e-6f) return Mathf.Abs(o) <= h;
            float a = (-h - o) / d, b = (h - o) / d;
            if (a > b) { float s = a; a = b; b = s; }
            t0 = Mathf.Max(t0, a); t1 = Mathf.Min(t1, b);
            return t0 <= t1;
        }

        // ------------------------------------------------------------------------------------------------ ranking
        static float DistToWalkway(Vector3 p)
        {
            var tp = new TownPoint(p.x, p.z);
            float best = float.MaxValue;
            foreach (Lane l in _layout.Lanes) best = Mathf.Min(best, HollowmereLayout.DistanceToPolyline(tp, l.Points));
            float dx = Mathf.Max(HollowmereLayout.SquareMinX - p.x, 0f, p.x - HollowmereLayout.SquareMaxX), dz = Mathf.Max(HollowmereLayout.SquareMinZ - p.z, 0f, p.z - HollowmereLayout.SquareMaxZ);
            return Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
        }

        static float Score(Fault f)
        {
            float w = 1f;
            switch (f.Kind)
            {
                case "stair_angled": w = 2f; break;
                case "wall_through": w = 2f; break;
                case "stair_butt": w = 1.6f; break;
                case "wall_overlap": w = 0.8f; break;
                case "stair_dead": w = 0.9f; break;
                case "wall_poke": w = 1.4f; break;
                case "wall_embed": w = 0.25f; break;
            }
            return f.Size * w / (1f + DistToWalkway(f.Pos) / 12f);
        }

        static void WriteRanked(string dir, string summary)
        {
            // one fault per (kind, name, other, rounded position) across presets: a fault in both presets is listed once with "both"
            var merged = new Dictionary<string, Fault>();
            var presets = new Dictionary<string, HashSet<string>>();
            foreach (Fault f in Faults)
            {
                string key = f.Kind + "|" + f.Name + "|" + f.Other + "|" + Mathf.Round(f.Pos.x) + "|" + Mathf.Round(f.Pos.z);
                if (!presets.TryGetValue(key, out var set)) presets[key] = set = new HashSet<string>();
                set.Add(f.Preset);
                if (!merged.TryGetValue(key, out Fault prev) || f.Score > prev.Score) merged[key] = f;
            }
            var ranked = merged.Select(kv => (Fault: kv.Value, Presets: string.Join("+", presets[kv.Key].OrderBy(x => x)))).OrderByDescending(x => x.Fault.Score).ToList();
            // collapse faults within 1.5 m of a better one (a long wall pierces many modules of the same neighbour)
            var top = new List<(Fault Fault, string Presets)>();
            foreach (var r in ranked)
            {
                if (top.Any(t => Vector3.Distance(t.Fault.Pos, r.Fault.Pos) < 1.5f)) continue;
                top.Add(r);
                if (top.Count >= 25) break;
            }
            var sb = new StringBuilder("# Hollowmere junction faults, top 25 by visibility\n\nScore = size * kind weight / (1 + distance to nearest lane or square / 12). Faults within 1.5 m of a better-ranked one are folded into it.\n\n## Counts (raw, per preset)\n```\n" + summary + "```\n\n");
            sb.Append("| # | kind | preset | piece | other | x | y | z | depth | poke/yaw | score | note |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
            int n = 1;
            foreach (var r in top)
            {
                Fault f = r.Fault;
                sb.Append("| ").Append(n++).Append(" | ").Append(f.Kind).Append(" | ").Append(r.Presets).Append(" | ").Append(f.Name).Append(" | ").Append(f.Other).Append(" | ").Append(F(f.Pos.x)).Append(" | ").Append(F(f.Pos.y)).Append(" | ")
                  .Append(F(f.Pos.z)).Append(" | ").Append(F(f.Depth)).Append(" | ").Append(F(f.Poke)).Append(" | ").Append(F(f.Score)).Append(" | ").Append(f.Info).Append(" |\n");
            }
            File.WriteAllText(System.IO.Path.Combine(dir, "junctions_ranked.md"), sb.ToString());
            // suggested capture tuples: stand 6 m off toward the nearest walkway (else south), eye 20 degrees above the fault point
            var cams = new StringBuilder();
            int ci = 1;
            foreach (var r in top.Take(10))
            {
                Fault f = r.Fault;
                Vector2 p = new Vector2(f.Pos.x, f.Pos.z), toward = new Vector2(0f, -1f);
                float bd = float.MaxValue;
                foreach (Lane l in _layout.Lanes)
                    for (int i = 1; i < l.Points.Length; i++)
                    {
                        TownPoint q = HollowmereLayout.ClosestOnSegment(new TownPoint(p.x, p.y), l.Points[i - 1], l.Points[i], out float tt);
                        float d = Vector2.Distance(p, new Vector2(q.X, q.Z));
                        if (d < bd && d > 1f) { bd = d; toward = (new Vector2(q.X, q.Z) - p).normalized; }
                    }
                Vector2 eye = p + toward * 6f;
                float eyeY = Mathf.Max(f.Pos.y + 6f * Mathf.Tan(20f * Mathf.Deg2Rad), Ground(eye) + 1.4f);
                cams.Append("(\"Cap_HM_J" + ci.ToString("00") + "\", new Vector3(" + F(eye.x) + "f, " + F(eyeY - Ground(eye)) + "f, " + F(eye.y) + "f), new Vector3(" + F(p.x) + "f, " + F(f.Pos.y) + "f, " + F(p.y) + "f), 55f),   // "
                           + f.Kind + " " + f.Name + " / " + f.Other + " (" + r.Presets + ")\n");
                ci++;
            }
            File.WriteAllText(System.IO.Path.Combine(dir, "junction_cams.txt"), cams.ToString());
        }
    }
}
