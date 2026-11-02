// World traversal scan: shared types. The hero capsule read from the real prefab, the issue record, the space description a scan runs over, and the source
// index that names the builder file and line most likely to have placed an offending object. Editor-only; nothing here runs in the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    // The hero's collision numbers, taken from the PlayerCapsule prefab's CharacterController (never hard-coded), plus the comfort margin for "fits".
    public sealed class HeroCapsule
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/PlayerCapsule.prefab";
        public const float ComfortMargin = 0.1f;   // an opening "fits" when it is at least the capsule diameter plus this
        public float Radius, Height, StepOffset, SlopeLimit, SkinWidth, JumpHeight;
        public int Layer;
        public float Diameter => Radius * 2f;
        public float FitWidth => Diameter + ComfortMargin;

        public static HeroCapsule FromPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("TraversalScan: hero prefab missing at " + PrefabPath);
            CharacterController cc = prefab.GetComponent<CharacterController>();
            if (cc == null) throw new InvalidOperationException("TraversalScan: no CharacterController on " + PrefabPath);
            Vector3 s = prefab.transform.lossyScale;
            float horiz = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            return new HeroCapsule
            {
                Radius = cc.radius * horiz, Height = cc.height * Mathf.Abs(s.y), StepOffset = cc.stepOffset, SlopeLimit = cc.slopeLimit,
                SkinWidth = cc.skinWidth, JumpHeight = MovementMath.JumpHeight, Layer = prefab.layer,
            };
        }

        public override string ToString() =>
            $"radius {Radius:0.00} m, height {Height:0.00} m, step offset {StepOffset:0.00} m, slope limit {SlopeLimit:0}°, skin {SkinWidth:0.00} m, jump {JumpHeight:0.0} m; fits = width >= {FitWidth:0.00} m";
    }

    public enum IssueKind
    {
        Unreachable,        // a target the hero can't walk to from the spawn
        VoidEdge,           // walkable floor that ends with no wall and no ground below: the hero can walk off the level
        StairToNowhere,     // a stair whose top or bottom landing has nowhere to go, or that isn't walkable at all
        Stuck,              // a pocket the hero can drop into but not climb (or jump) out of
        Blocked,            // an intended path the capsule doesn't fit through at all
        Hole,               // no ground under an intended path / floor cell
        Narrow,             // an opening on an intended path narrower than the capsule diameter plus the margin
        Step,               // a step on an intended path higher than the step offset
        Slope,              // ground on an intended path steeper than the slope limit
        Island,             // walkable floor at ground level cut off from the main area
        WalkThrough,        // a visible prop or wall with no collider where the hero walks
        InvisibleBlocker,   // a collider with nothing visible blocking open-looking ground
        ClutterInDoorway,   // dressing or clutter inside a doorway, on a stair or a door's threshold
        TightGap,           // a gap that looks passable between walkable areas but is narrower than the capsule
        OversizedCollider,  // a collider far bigger than its mesh
        FloorMismatch,      // the walkable floor's collider and its visible mesh disagree in height
    }

    public sealed class TraversalIssue
    {
        public string Scene, Variant; public int Seed = -1, Floor;
        public Vector3 Pos; public IssueKind Kind;
        public float Measured, Limit; public string Unit = "m";
        public string Detail = "", Source = "", Object = "", Group = "", Plan = "";
        public int Samples = 1;

        public static int Severity(IssueKind k)
        {
            switch (k)
            {
                case IssueKind.Unreachable: return 100;
                case IssueKind.VoidEdge: return 96;
                case IssueKind.StairToNowhere: return 92;
                case IssueKind.Stuck: return 90;
                case IssueKind.Blocked: return 85;
                case IssueKind.Hole: return 75;
                case IssueKind.Narrow: return 65;
                case IssueKind.Step: return 55;
                case IssueKind.Island: return 50;
                case IssueKind.WalkThrough: return 45;
                case IssueKind.InvisibleBlocker: return 42;
                case IssueKind.Slope: return 40;
                case IssueKind.ClutterInDoorway: return 38;
                case IssueKind.TightGap: return 25;
                case IssueKind.OversizedCollider: return 20;
                default: return 15;
            }
        }

        // Rank within the whole report: kind first, then how far past the limit it is, then how many samples it covers.
        public float Score
        {
            get
            {
                float over = Limit > 0f ? Mathf.Clamp01(Mathf.Abs(Measured - Limit) / Mathf.Max(0.05f, Limit)) : 0.5f;
                return Severity(Kind) * 100f + over * 50f + Mathf.Min(Samples, 40);
            }
        }

        public string Where => Seed >= 0 ? $"{Scene} F{Floor} seed {Seed}" : $"{Scene} {Variant}";
    }

    // A thing that must be reachable from the spawn.
    public sealed class TraversalTarget
    {
        public string Name, Kind; public Vector3 Pos; public float Reach = 1.2f; public bool BehindLock;
        public bool Reached; public float NearestGap = float.PositiveInfinity;   // horizontal distance to the nearest main-mesh point (diagnostic)
    }

    // An intended walking line (lane centre, corridor centre, door crossing) and the ground height the builder meant it to have.
    public sealed class IntendedPath
    {
        public string Name; public Vector3 A, B; public float Width;   // width: the lane width the plan gives (0 = unknown)
        public float TrimA, TrimB;                                      // metres left unchecked at each end (a closed door the line ends against)
    }

    // A spot that must stay free of dressing: a doorway, a stair, a door threshold.
    public sealed class KeepClear
    {
        public string Name; public Vector3 Pos; public float Radius;
    }

    // Everything a scan of one playable space needs. Built by TraversalSpaces from the town layout or a Cellars floor plan.
    public sealed class ScanSpace
    {
        public string Scene, Variant; public int Seed = -1, Floor;
        public Bounds Area;                      // world bounds the grid and the NavMesh bake cover
        public Vector3? Spawn;                   // null: the largest walkable component counts as the main area
        public readonly List<TraversalTarget> Targets = new List<TraversalTarget>();
        public readonly List<IntendedPath> Paths = new List<IntendedPath>();
        public readonly List<KeepClear> Clear = new List<KeepClear>();
        public readonly List<GameObject> Locks = new List<GameObject>();   // closed doors: disabled for the "open" pass that checks targets behind them
        public Func<float, float, float> ExpectedGround;                   // the height the builder meant the walkable ground to have at (x, z)
        public Func<float, float, bool> ExpectedWalkable;                  // true where the plan means the hero to walk (lanes, room floors, corridors)
        public Func<Vector3, string> Describe;                             // a plan-level location for a world point (room id and cell, lane), optional
        public Transform Root;                                             // the hierarchy the scan's object checks look at
        public string Name => Seed >= 0 ? $"{Scene}_F{Floor}_s{Seed}" : $"{Scene}_{Variant}";
    }

    // Finds the builder file and line that most likely placed an object, by searching the builder sources for the object's base name.
    public static class TraversalSources
    {
        static readonly string[] Roots =
        {
            "Assets/_Project/Editor/Builders", "Assets/_Project/Presentation/Cellars", "Assets/_Project/Presentation/Town",
            "Assets/_Project/Core/Cellars", "Assets/_Project/Core/Town",
        };
        static List<(string file, string[] lines)> s_files;
        static readonly Dictionary<string, string> s_cache = new Dictionary<string, string>();
        static readonly Regex Trailer = new Regex(@"(\s*\(Clone\)|[_ ]?\(?\d+\)?|_[a-z]?\d+(_[a-z]?\d+)*)$");

        public static string HierarchyPath(Transform t, int depth = 6)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < depth; i++, t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        public static string Find(Transform t)
        {
            for (int i = 0; t != null && i < 6; i++, t = t.parent)
            {
                string hit = FindName(t.name);
                if (hit != null) return hit;
            }
            return "";
        }

        static string FindName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            if (s_cache.TryGetValue(raw, out string cached)) return cached;
            Load();
            string result = null;
            var names = new List<string>();
            string n = raw;
            for (int i = 0; i < 4; i++) { names.Add(n); string m = Trailer.Replace(n, ""); if (m == n || m.Length < 3) break; n = m; }
            foreach (string name in names)
            {
                if (name.Length < 3) continue;
                foreach (string probe in new[] { "\"" + name + "\"", "\"" + name + "_", "\"" + name + " ", "\"" + name })
                {
                    foreach (var (file, lines) in s_files)
                        for (int l = 0; l < lines.Length; l++)
                            if (lines[l].Contains(probe)) { result = file + ":" + (l + 1); goto done; }
                }
            }
        done:
            s_cache[raw] = result;
            return result;
        }

        static void Load()
        {
            if (s_files != null) return;
            s_files = new List<(string, string[])>();
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            foreach (string root in Roots)
            {
                string dir = Path.Combine(project, root);
                if (!Directory.Exists(dir)) continue;
                foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    s_files.Add((Path.GetRelativePath(project, f).Replace('\\', '/'), File.ReadAllLines(f)));
            }
        }
    }
}
