// Core/Town/TownLayout.cs: the town-layout model behind the rebuild loop (design docs/superpowers/specs/2026-10-30-hollowmere-mass-production-design.md
// sections 2-5, PHASE1 7.1 / P26). Pure C#, no engine references, so the Editor builder (HollowmereBuilder), the EditMode tests and later M6 placement
// all read the same rows.
//
// Frame: metres, +Z north, +X east, Y up; yaw in degrees clockwise seen from above, the Unity convention (forward = (sin yaw, cos yaw)).
//  - A Foundation is a square old lot of one size class (S 6 m, M 8 m, L 11 m). Its own frame turns with Yaw; Facing names the door side in that frame.
//    It has a doorstep node just outside the middle of each side and a corner node off each corner, linked round the lot, so a building can face any
//    of the four turns and still have a doorstep on the graph.
//  - A Landmark is a fixed piece with its own footprint (no size class): it blocks the path graph like any building.
//  - Lanes are spine polylines; PathNodes and PathEdges are the NPC path graph M6 uses to refuse buildings and break decorations.
// Validate() is the rule set the builder fails the build on: no foundation overlaps another foundation, a landmark or a path node; no path edge or node
// is inside a foundation or landmark footprint; no path edge is steeper than MaxGrade (20 deg) unless it is a stair; ids are unique and edges name real
// nodes; every doorstep of every foundation sits at the lot's height and reaches the graph's root.
// Authority: none (static layout data; M6 runs its placement checks host-side over these rows).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum FoundationSize : byte { S = 0, M = 1, L = 2 }

    // A side of a foundation in its own frame: North = local +Z.
    public enum LotSide : byte { North = 0, East = 1, South = 2, West = 3 }

    public enum LandmarkKind : byte { Fountain, Stall, BellTower, TitheGate, HeroesHome, Mill, TownGate, KeepHall }

    public enum PathNodeKind : byte { Loop, Lane, Doorstep, Corner, Door, Stair }

    public readonly struct TownPoint
    {
        public readonly float X, Z;
        public TownPoint(float x, float z) { X = x; Z = z; }
        public static TownPoint operator +(TownPoint a, TownPoint b) => new TownPoint(a.X + b.X, a.Z + b.Z);
        public static TownPoint operator -(TownPoint a, TownPoint b) => new TownPoint(a.X - b.X, a.Z - b.Z);
        public static TownPoint operator *(TownPoint a, float s) => new TownPoint(a.X * s, a.Z * s);
        public float Length => (float)Math.Sqrt(X * X + Z * Z);
        public static float Distance(TownPoint a, TownPoint b) => (a - b).Length;
        public override string ToString() => "(" + X.ToString("0.##") + ", " + Z.ToString("0.##") + ")";
    }

    // An oriented rectangle on the ground: centre, half extents along its own X and Z, yaw.
    public readonly struct Footprint
    {
        const float Eps = 1e-3f;   // touching edges are not an overlap
        public readonly TownPoint Centre; public readonly float HalfX, HalfZ, Yaw;
        public Footprint(TownPoint centre, float sizeX, float sizeZ, float yaw) { Centre = centre; HalfX = sizeX * 0.5f; HalfZ = sizeZ * 0.5f; Yaw = yaw; }

        public TownPoint AxisX { get { double r = Yaw * Math.PI / 180.0; return new TownPoint((float)Math.Cos(r), (float)-Math.Sin(r)); } }
        public TownPoint AxisZ { get { double r = Yaw * Math.PI / 180.0; return new TownPoint((float)Math.Sin(r), (float)Math.Cos(r)); } }

        public TownPoint ToWorld(float lx, float lz) => Centre + AxisX * lx + AxisZ * lz;
        public TownPoint ToLocal(TownPoint p) { TownPoint d = p - Centre, ax = AxisX, az = AxisZ; return new TownPoint(d.X * ax.X + d.Z * ax.Z, d.X * az.X + d.Z * az.Z); }

        public TownPoint[] Corners() => new[] { ToWorld(-HalfX, -HalfZ), ToWorld(HalfX, -HalfZ), ToWorld(HalfX, HalfZ), ToWorld(-HalfX, HalfZ) };

        // Strictly inside (a point on the edge is outside).
        public bool Contains(TownPoint p, float margin = 0f)
        {
            TownPoint l = ToLocal(p);
            return Math.Abs(l.X) < HalfX + margin - Eps && Math.Abs(l.Z) < HalfZ + margin - Eps;
        }

        // Separating-axis test on the four edge normals; shapes that only touch do not overlap.
        public bool Overlaps(Footprint o)
        {
            foreach (TownPoint axis in new[] { AxisX, AxisZ, o.AxisX, o.AxisZ })
            {
                Project(this, axis, out float a0, out float a1); Project(o, axis, out float b0, out float b1);
                if (a1 <= b0 + Eps || b1 <= a0 + Eps) return false;
            }
            return true;
        }

        static void Project(Footprint f, TownPoint axis, out float min, out float max)
        {
            min = float.MaxValue; max = float.MinValue;
            foreach (TownPoint c in f.Corners()) { float d = c.X * axis.X + c.Z * axis.Z; if (d < min) min = d; if (d > max) max = d; }
        }

        // Does the segment pass through the interior? Clip it against the local box (Liang-Barsky), shrunk by Eps so grazing an edge is allowed.
        public bool SegmentCrosses(TownPoint a, TownPoint b)
        {
            TownPoint la = ToLocal(a), lb = ToLocal(b);
            float t0 = 0f, t1 = 1f, dx = lb.X - la.X, dz = lb.Z - la.Z, hx = HalfX - Eps, hz = HalfZ - Eps;
            if (!Clip(-dx, la.X + hx, ref t0, ref t1) || !Clip(dx, hx - la.X, ref t0, ref t1)) return false;
            if (!Clip(-dz, la.Z + hz, ref t0, ref t1) || !Clip(dz, hz - la.Z, ref t0, ref t1)) return false;
            return t1 - t0 > 1e-5f;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-9f) return q > 0f;
            float r = q / p;
            if (p < 0f) { if (r > t1) return false; if (r > t0) t0 = r; }
            else { if (r < t0) return false; if (r < t1) t1 = r; }
            return true;
        }
    }

    public sealed class Foundation
    {
        public const float DoorstepOffset = 0.6f;   // doorstep and corner nodes sit this far outside the lot edge
        public string Id; public int Ring; public FoundationSize Size; public TownPoint Centre; public float Yaw; public LotSide Facing; public string RuinVariant;
        public int RuinTurn;   // 0-3 quarter turns of the ruin on the lot (in the lot's own frame); the ruin is switched off once a building stands here
        public float Y;   // ground height of the lot (the pad the builder flattens)

        public static float SideLength(FoundationSize s) => s == FoundationSize.S ? 6f : s == FoundationSize.M ? 8f : 11f;
        // The largest building body that fits (design 5.1); the rest of the lot is yard.
        public static float MaxBuilding(FoundationSize s) => s == FoundationSize.S ? 5f : s == FoundationSize.M ? 7.5f : 10.5f;

        public float Side => SideLength(Size);
        public Footprint Footprint => new Footprint(Centre, Side, Side, Yaw);
        public string DoorstepId(LotSide side) => Id + "_" + side;
        public string CornerId(int i) => Id + "_C" + i;   // 0 SW, 1 SE, 2 NE, 3 NW in the lot frame

        public static TownPoint SideDir(LotSide s) => s == LotSide.North ? new TownPoint(0f, 1f) : s == LotSide.East ? new TownPoint(1f, 0f) : s == LotSide.South ? new TownPoint(0f, -1f) : new TownPoint(-1f, 0f);
        public TownPoint DoorstepPoint(LotSide s) { TownPoint d = SideDir(s) * (Side * 0.5f + DoorstepOffset); return Footprint.ToWorld(d.X, d.Z); }
        public TownPoint CornerPoint(int i)
        {
            float h = Side * 0.5f + DoorstepOffset;
            return i == 0 ? Footprint.ToWorld(-h, -h) : i == 1 ? Footprint.ToWorld(h, -h) : i == 2 ? Footprint.ToWorld(h, h) : Footprint.ToWorld(-h, h);
        }
        // World yaw a building on this lot faces when its door is on Facing (local -Z of the building art turned to the door side).
        public float FacingYaw => Yaw + (Facing == LotSide.North ? 180f : Facing == LotSide.East ? 270f : Facing == LotSide.South ? 0f : 90f);
    }

    public sealed class Landmark
    {
        public string Id, Name; public LandmarkKind Kind; public Footprint Footprint; public float Y; public float Height;
        public string DoorNode;   // the path node in front of its door, null for none
    }

    public sealed class Lane
    {
        public string Id, Name; public int Ring; public float Width; public TownPoint[] Points;
        public float[] Heights;   // ground height of each point (the relaxed lane profile); the lane surface is linear between points
    }

    public sealed class PathNode
    {
        public string Id; public TownPoint Pos; public float Y; public PathNodeKind Kind;
    }

    public readonly struct PathEdge
    {
        public readonly string A, B;
        public PathEdge(string a, string b) { A = a; B = b; }
    }

    public sealed class TownLayout
    {
        public readonly List<Foundation> Foundations = new List<Foundation>();
        public readonly List<Landmark> Landmarks = new List<Landmark>();
        public readonly List<Lane> Lanes = new List<Lane>();
        public readonly List<PathNode> Nodes = new List<PathNode>();
        public readonly List<PathEdge> Edges = new List<PathEdge>();
        public string RootNode;   // reachability is measured from here (the fountain loop)
        public const float MaxGrade = 0.364f;   // tan 20 deg: the steepest walkable path edge; an edge between two Stair nodes is a stair and may be steeper

        public PathNode Node(string id) { foreach (PathNode n in Nodes) if (n.Id == id) return n; return null; }
        public Foundation Foundation(string id) { foreach (Foundation f in Foundations) if (f.Id == id) return f; return null; }
        public Landmark Landmark(string id) { foreach (Landmark l in Landmarks) if (l.Id == id) return l; return null; }

        public int Count(int ring, FoundationSize size) { int n = 0; foreach (Foundation f in Foundations) if (f.Ring == ring && f.Size == size) n++; return n; }

        public Dictionary<string, List<string>> Adjacency()
        {
            var adj = new Dictionary<string, List<string>>();
            foreach (PathNode n in Nodes) if (!adj.ContainsKey(n.Id)) adj[n.Id] = new List<string>();
            foreach (PathEdge e in Edges)
            {
                if (adj.TryGetValue(e.A, out List<string> a) && adj.TryGetValue(e.B, out List<string> b)) { a.Add(e.B); b.Add(e.A); }
            }
            return adj;
        }

        public HashSet<string> ReachableFrom(string root)
        {
            var seen = new HashSet<string>();
            Dictionary<string, List<string>> adj = Adjacency();
            if (root == null || !adj.ContainsKey(root)) return seen;
            var q = new Queue<string>(); q.Enqueue(root); seen.Add(root);
            while (q.Count > 0) foreach (string n in adj[q.Dequeue()]) if (seen.Add(n)) q.Enqueue(n);
            return seen;
        }

        // Every broken rule as one line (empty = valid). The builder fails the build on any line; the EditMode tests feed it broken layouts.
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            foreach (Foundation f in Foundations) if (!ids.Add(f.Id)) errors.Add("duplicate id " + f.Id);
            foreach (Landmark l in Landmarks) if (!ids.Add(l.Id)) errors.Add("duplicate id " + l.Id);
            var nodeIds = new HashSet<string>();
            foreach (PathNode n in Nodes) if (!nodeIds.Add(n.Id)) errors.Add("duplicate path node " + n.Id);

            for (int i = 0; i < Foundations.Count; i++)
            {
                Foundation f = Foundations[i]; Footprint fp = f.Footprint;
                if (f.Ring < 1 || f.Ring > 3) errors.Add(f.Id + ": ring " + f.Ring + " is not 1-3");
                for (int j = i + 1; j < Foundations.Count; j++)
                    if (fp.Overlaps(Foundations[j].Footprint)) errors.Add(f.Id + " overlaps foundation " + Foundations[j].Id);
                foreach (Landmark l in Landmarks)
                    if (fp.Overlaps(l.Footprint)) errors.Add(f.Id + " overlaps landmark " + l.Id);
            }

            foreach (PathNode n in Nodes)
            {
                foreach (Foundation f in Foundations) if (f.Footprint.Contains(n.Pos)) errors.Add("path node " + n.Id + " is inside foundation " + f.Id);
                foreach (Landmark l in Landmarks) if (l.Footprint.Contains(n.Pos)) errors.Add("path node " + n.Id + " is inside landmark " + l.Id);
            }

            foreach (PathEdge e in Edges)
            {
                PathNode a = Node(e.A), b = Node(e.B);
                if (a == null || b == null) { errors.Add("path edge " + e.A + "-" + e.B + " names a missing node"); continue; }
                if (e.A == e.B) { errors.Add("path edge " + e.A + " loops to itself"); continue; }
                float run = TownPoint.Distance(a.Pos, b.Pos), rise = Math.Abs(a.Y - b.Y);
                bool stair = a.Kind == PathNodeKind.Stair && b.Kind == PathNodeKind.Stair;
                if (!stair && rise > MaxGrade * run + 0.05f) errors.Add("path edge " + e.A + "-" + e.B + " is too steep: rises " + rise.ToString("0.0") + " m over " + run.ToString("0.0") + " m");
                foreach (Foundation f in Foundations) if (f.Footprint.SegmentCrosses(a.Pos, b.Pos)) errors.Add("path edge " + e.A + "-" + e.B + " crosses foundation " + f.Id);
                foreach (Landmark l in Landmarks) if (l.Footprint.SegmentCrosses(a.Pos, b.Pos)) errors.Add("path edge " + e.A + "-" + e.B + " crosses landmark " + l.Id);
            }

            HashSet<string> reach = ReachableFrom(RootNode);
            if (RootNode == null || Node(RootNode) == null) errors.Add("root node " + (RootNode ?? "(none)") + " is missing");
            foreach (Foundation f in Foundations)
                foreach (LotSide s in AllSides)
                {
                    string id = f.DoorstepId(s);
                    PathNode n = Node(id);
                    if (n == null) errors.Add(f.Id + ": no doorstep node on its " + s + " side");
                    else if (!reach.Contains(id)) errors.Add(f.Id + ": the " + s + " doorstep is not reachable from " + RootNode);
                    else if (TownPoint.Distance(n.Pos, f.DoorstepPoint(s)) > 0.05f) errors.Add(f.Id + ": the " + s + " doorstep node is not at the middle of that side");
                    else if (Math.Abs(n.Y - f.Y) > 0.05f) errors.Add(f.Id + ": the " + s + " doorstep is not at the lot's height");
                }
            foreach (Landmark l in Landmarks)
                if (l.DoorNode != null && !reach.Contains(l.DoorNode)) errors.Add(l.Id + ": door node " + l.DoorNode + " is not reachable from " + RootNode);
            return errors;
        }

        // Do two segments properly cross (touching at an end point does not count)?
        public static bool SegmentsCross(TownPoint a, TownPoint b, TownPoint c, TownPoint d)
        {
            float d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }
        static float Cross(TownPoint o, TownPoint a, TownPoint b) => (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);

        public static readonly LotSide[] AllSides = { LotSide.North, LotSide.East, LotSide.South, LotSide.West };
    }
}
