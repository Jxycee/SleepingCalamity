// Core/Town/LaneKitPlan.cs: the placement plan for Hollowmere's ring 2 lane kit (Q11; art contract Reference/hollowmere/lanes/CONTRACT.md). Pure C#, no engine
// references, so the Editor placer (HollowmereLanes) and the EditMode tests share one deterministic list of rows.
//
// Frame of a row: metres, +Z north, +X east, Y up; Yaw in degrees (Unity convention: local +Z = (sin yaw, cos yaw), local +X = (cos yaw, -sin yaw)); the origin is the
// piece's own pivot (see the contract's extents). Wall-like pieces have their seen (lane) face at local -Z, so +Z points away from the lane (up the bank, into the yard).
// Lane-aligned pieces (kerbs, gutters, grates) run along local X = the lane's direction of travel. Tilt is the pitch of local X in degrees, positive when +X rises
// (apply as Quaternion.Euler(0, Yaw, Tilt)). ScaleX stretches along local X. Steps: local +Z points uphill; Rise is the height of the tread above the one below.
// What goes where (all in-scope lanes: RingLane, WestStub, MarketStreet, EastStub, NorthEastStub, NorthWestGinnel, LowerLane up to the town wall line - 2 m):
//  - Steps on segments with |grade| >= 0.18 (landings at the segment ends and next to junctions), kerbs along ramps (0.05..0.18) and as flight cheeks,
//    a gutter along the right edge of every segment (grates where a run ends), garden walls along free lane edges (stairing down slopes, piers at gaps, a gate
//    in every run of 10 m or more, retaining walls where the bank beside the lane stands more than 0.8 m up), retaining walls for the terrace edges and the lot
//    cut/fill sides, lanterns about every 15 m on the walls. Ruined swaps in broken/choked variants by seeded hash; the layout never changes between presets.
// Authority: none (static plan data used at editor build time).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SleepingCalamity.Core
{
    public enum LanePiece : byte
    {
        GardenWall, GardenWallBroken, GardenWallEnd, RetainWall, RetainWallLow, RetainWallBreach, RetainButtress, LaneStep, LaneKerb, Gutter, GutterChoked, GutterGrate,
        WallLantern, WallLanternBroken, GatePier, GateLeaf, GateLeafBroken,
    }

    public enum LanePreset : byte { Ruined, Rebuilt, Both }

    public sealed class LaneKitRow
    {
        public string Id;
        public LanePiece Piece;
        public LanePreset Preset;
        public float X, Y, Z, Yaw, ScaleX = 1f, Tilt;
        public float Rise;      // steps: tread height above the tread below
        public string Lane;     // the lane (or "Terrace" / "Lot:<id>") the row belongs to

        public bool In(bool rebuilt) => Preset == LanePreset.Both || (rebuilt ? Preset == LanePreset.Rebuilt : Preset == LanePreset.Ruined);
        public bool IsWall => Piece == LanePiece.GardenWall || Piece == LanePiece.GardenWallBroken || Piece == LanePiece.RetainWall || Piece == LanePiece.RetainWallLow || Piece == LanePiece.RetainWallBreach;
        public bool IsRetaining => Piece == LanePiece.RetainWall || Piece == LanePiece.RetainWallLow || Piece == LanePiece.RetainWallBreach;
    }

    public static class LaneKitPlan
    {
        public const int Seed = 11011;
        public const float StepRun = 0.55f, StepVisible = 0.16f, FlightGrade = 0.18f, RampGrade = 0.05f;
        public const float GutterWidth = 0.4f, WallOffset = 0.6f, WallHalfThick = 0.25f;
        public const float JunctionClip = 2.6f, WallJunctionClip = 4f, Landing = 0.4f;
        public const float TownWallClear = 2f, LaneLimitE = TownWallLineE - TownWallClear - 0.5f;   // lane kit stops 2.5 m inside the wall line
        public const float TownWallLineE = HollowmereLayout.TownWallOffset;
        public const float BrokenGardenShare = 0.35f, BreachShare = 0.20f;

        public static readonly string[] InScope = { "RingLane", "WestStub", "MarketStreet", "EastStub", "NorthEastStub", "NorthWestGinnel", "LowerLane" };

        public static float Top(LanePiece p) => p == LanePiece.RetainWallLow ? 1.5f : 3.0f;   // coping height above the module origin

        // ------------------------------------------------------------------------------------------------ lot cut/fill heights (moved from HollowmereBuilder.CutAndFill)
        // The natural ground range just outside one lot side (2.3 m out from the lot edge, five samples along it).
        public static void LotSideHeights(Foundation f, LotSide s, out float hi, out float lo)
        {
            TownPoint dir = Foundation.SideDir(s), along = new TownPoint(-dir.Z, dir.X);
            float half = f.Side * 0.5f; hi = float.MinValue; lo = float.MaxValue;
            for (int k = -2; k <= 2; k++)
            {
                TownPoint l = dir * (half + 2.3f) + along * (k * half * 0.45f);
                TownPoint w = f.Footprint.ToWorld(l.X, l.Z);
                float y = HollowmereLayout.NaturalGround(w.X, w.Z);
                if (y > hi) hi = y; if (y < lo) lo = y;
            }
        }

        // ------------------------------------------------------------------------------------------------ geometry helpers
        sealed class Seg
        {
            public Lane Lane; public int LaneIdx, I; public TownPoint A, B, Dir, Right; public float Len, HA, HB, Grade, Hw, S0, S1, Cum; public bool JA, JB, Flight;
            public float H(float s) => HA + (HB - HA) * (s / Len);
            public TownPoint P(float s, float lat = 0f) => A + Dir * s + Right * lat;
        }

        public static bool InSquare(TownPoint p, float m = 0f) =>
            p.X > HollowmereLayout.SquareMinX - m && p.X < HollowmereLayout.SquareMaxX + m && p.Z > HollowmereLayout.SquareMinZ - m && p.Z < HollowmereLayout.SquareMaxZ + m;
        public static bool InKeep(TownPoint p, float m = 0f) => Math.Abs(p.X) < HollowmereLayout.KeepHalfWidth + m && p.Z > HollowmereLayout.KeepWallZ - m;
        static bool InKeepZone(TownPoint p) => Math.Abs(p.X) < HollowmereLayout.KeepHalfWidth + 1f && p.Z > HollowmereLayout.KeepWallZ - 7f;
        static bool TooCloseToTownWall(TownPoint p, float limit) => HollowmereLayout.PlateauDistance(p.X, p.Z) > limit;

        public static List<(TownPoint A, TownPoint B)> EdgeSegments(TownLayout t)
        {
            var list = new List<(TownPoint, TownPoint)>();
            foreach (PathEdge e in t.Edges) { PathNode a = t.Node(e.A), b = t.Node(e.B); if (a != null && b != null) list.Add((a.Pos, b.Pos)); }
            return list;
        }

        // Does the segment a-b, lengthened by `margin` at both ends, properly cross any path edge?
        public static bool CrossesEdge(List<(TownPoint A, TownPoint B)> edges, TownPoint a, TownPoint b, float margin)
        {
            TownPoint d = b - a; float l = d.Length; if (l < 1e-4f) return false;
            d = d * (1f / l);
            TownPoint a2 = a - d * margin, b2 = b + d * margin;
            foreach (var (p, q) in edges) if (TownLayout.SegmentsCross(a2, b2, p, q)) return true;
            return false;
        }

        static float Yaw(TownPoint v) => (float)(Math.Atan2(v.X, v.Z) * 180.0 / Math.PI);           // local +Z along v
        static float YawAlong(TownPoint v) => (float)(Math.Atan2(-v.Z, v.X) * 180.0 / Math.PI);       // local +X along v
        static float Round(float v) => (float)Math.Round(v, 3);

        sealed class Ctx
        {
            public TownLayout T; public List<LaneKitRow> Rows = new List<LaneKitRow>(); public List<(TownPoint A, TownPoint B)> Edges; public List<Seg> Segs = new List<Seg>();
            public List<Lane> Lanes = new List<Lane>(); public List<Mount> Mounts = new List<Mount>(); public List<Flight> Flights = new List<Flight>(); public int Counter;
            public float G(TownPoint p) => HollowmereLayout.Ground(T, p.X, p.Z);
        }
        sealed class Mount { public int LaneIdx; public float S, Prio, Y, Yaw; public TownPoint Pos; }
        sealed class Flight { public int LaneIdx; public float SHead, SFoot; }

        static LaneKitRow Add(Ctx c, LanePiece piece, LanePreset preset, string lane, TownPoint p, float y, float yaw, float scale = 1f, float tilt = 0f, float rise = 0f)
        {
            var r = new LaneKitRow { Id = "LK" + (c.Counter++).ToString("0000") + "_" + piece, Piece = piece, Preset = preset, Lane = lane, X = Round(p.X), Y = Round(y), Z = Round(p.Z), Yaw = Round(yaw), ScaleX = Round(scale), Tilt = Round(tilt), Rise = Round(rise) };
            c.Rows.Add(r);
            return r;
        }

        static void AddPair(Ctx c, LanePiece ruined, LanePiece rebuilt, string lane, TownPoint p, float y, float yaw, float scale = 1f, float tilt = 0f)
        {
            if (ruined == rebuilt) Add(c, ruined, LanePreset.Both, lane, p, y, yaw, scale, tilt);
            else { Add(c, ruined, LanePreset.Ruined, lane, p, y, yaw, scale, tilt); Add(c, rebuilt, LanePreset.Rebuilt, lane, p, y, yaw, scale, tilt); }
        }

        // ------------------------------------------------------------------------------------------------ the plan
        public static List<LaneKitRow> Plan(TownLayout t)
        {
            var c = new Ctx { T = t, Edges = EdgeSegments(t) };
            BuildSegments(c);
            Steps(c);
            KerbsAndGutters(c);
            GardenWalls(c);
            TerraceWalls(c);
            LotWalls(c);
            Lanterns(c);
            return c.Rows;
        }

        // ---- lane segments: valid range outside the square and inside the wall line, junction ends
        static void BuildSegments(Ctx c)
        {
            TownLayout t = c.T;
            var pointCount = new Dictionary<int, int>();
            foreach (string id in InScope)
            {
                Lane lane = HollowmereLayout.FindLane(t, id);
                int li = c.Lanes.Count; c.Lanes.Add(lane);
                float cum = 0f;
                for (int i = 1; i < lane.Points.Length; i++)
                {
                    TownPoint a = lane.Points[i - 1], b = lane.Points[i], d = b - a; float len = d.Length;
                    d = d * (1f / len);
                    var s = new Seg { Lane = lane, LaneIdx = li, I = i, A = a, B = b, Dir = d, Right = new TownPoint(d.Z, -d.X), Len = len, HA = lane.Heights[i - 1], HB = lane.Heights[i], Hw = lane.Width * 0.5f, Cum = cum };
                    s.Grade = (s.HB - s.HA) / len;
                    float lo = float.MaxValue, hi = float.MinValue;
                    for (float x = 0f; x <= len + 1e-4f; x += 0.1f)
                    {
                        TownPoint p = s.P(Math.Min(x, len));
                        if (InSquare(p) || TooCloseToTownWall(p, LaneLimitE)) continue;
                        lo = Math.Min(lo, x); hi = Math.Max(hi, x);
                    }
                    cum += len;
                    if (lo > hi) continue;
                    s.S0 = lo; s.S1 = Math.Min(hi, len);
                    s.JA = lo < 0.05f && IsJunction(t, a); s.JB = hi > len - 0.15f && IsJunction(t, b);
                    c.Segs.Add(s);
                }
            }
        }

        // A lane point where another lane, a stair or the square loop joins (three or more path edges meet there).
        static bool IsJunction(TownLayout t, TownPoint p)
        {
            foreach (PathNode n in t.Nodes)
            {
                if (TownPoint.Distance(n.Pos, p) > 0.3f) continue;
                int deg = 0;
                foreach (PathEdge e in t.Edges) if (e.A == n.Id || e.B == n.Id) deg++;
                return deg >= 3;
            }
            return false;
        }

        static float RangeA(Seg s, float clip) => s.S0 + (s.JA ? clip : 0f);
        static float RangeB(Seg s, float clip) => s.S1 - (s.JB ? clip : 0f);

        // ---- steps
        static void Steps(Ctx c)
        {
            foreach (Seg s in c.Segs)
            {
                if (Math.Abs(s.Grade) < FlightGrade) continue;
                float a = RangeA(s, JunctionClip), b = RangeB(s, JunctionClip);
                int n = (int)Math.Floor((b - a - 2f * Landing) / StepRun);
                if (n < 2) continue;
                s.Flight = true;   // round 3: no gutter runs along a stair flight
                int su = s.Grade > 0f ? 1 : -1;
                TownPoint u = s.Dir * su;
                float width = 2f * s.Hw, left = -s.Hw;
                int cols = (int)Math.Ceiling(width / 2f - 1e-3f);
                float scale = width / cols / 2f, rise = Math.Abs(s.Grade) * StepRun;
                for (int k = 0; k < n; k++)
                {
                    float dUp = Landing + k * StepRun, dDn = dUp + StepRun;
                    float sUp = su > 0 ? b - dUp : a + dUp, sDn = su > 0 ? b - dDn : a + dDn;
                    float y = s.H(sUp) - StepVisible;
                    for (int j = 0; j < cols; j++)
                    {
                        float lat = left + (j + 0.5f) * width / cols;
                        Add(c, LanePiece.LaneStep, LanePreset.Both, s.Lane.Id, s.P(sDn, lat), y, Yaw(u), scale, 0f, rise);
                    }
                }
                float sHead = su > 0 ? b - Landing : a + Landing, sFoot = su > 0 ? a + Landing : b - Landing;
                c.Flights.Add(new Flight { LaneIdx = s.LaneIdx, SHead = s.Cum + sHead, SFoot = s.Cum + sFoot });
            }
        }

        // ---- kerbs on ramps and flight cheeks, gutters on the right edge, grates at the run ends
        static void KerbsAndGutters(Ctx c)
        {
            foreach (Seg s in c.Segs)
            {
                float a = RangeA(s, JunctionClip), b = RangeB(s, JunctionClip), len = b - a;
                if (len < 0.8f) continue;
                int n = Math.Max(1, (int)Math.Ceiling(len / 2f - 1e-3f));
                float scale = len / n / 2f, tilt = (float)(Math.Atan(s.Grade) * 180.0 / Math.PI), yaw = YawAlong(s.Dir);
                bool kerb = Math.Abs(s.Grade) >= RampGrade && !s.Flight;   // round 3: the tilted kerb bars read as a black ribbon beside the stepped flight
                for (int i = 0; i < n; i++)
                {
                    float sm = a + (i + 0.5f) * len / n, h = s.H(sm);
                    if (kerb)
                    {
                        Add(c, LanePiece.LaneKerb, LanePreset.Both, s.Lane.Id, s.P(sm, -(s.Hw + 0.15f)), h, yaw, scale, tilt);
                        Add(c, LanePiece.LaneKerb, LanePreset.Both, s.Lane.Id, s.P(sm, s.Hw + 0.15f), h, yaw, scale, tilt);
                    }
                    if (!s.Flight) AddPair(c, LanePiece.GutterChoked, LanePiece.Gutter, s.Lane.Id, s.P(sm, s.Hw - GutterWidth * 0.5f), h, yaw, scale, tilt);
                }
                if (s.Flight) continue;
                // A run ends where the next segment of the lane does not continue it (square, junction, wall line): a grate at its downhill end.
                bool downIsB = s.Grade >= 0f;
                bool endsHere = downIsB ? (s.JB || s.S1 < s.Len - 0.15f || !HasNext(c, s)) : (s.JA || s.S0 > 0.05f || !HasPrev(c, s));
                if (endsHere)
                {
                    float se = downIsB ? b : a;
                    AddPair(c, LanePiece.GutterGrate, LanePiece.GutterGrate, s.Lane.Id, s.P(se, s.Hw - GutterWidth * 0.5f), s.H(se), yaw, 1f, tilt);
                }
            }
        }

        static bool HasNext(Ctx c, Seg s) => c.Segs.Exists(o => o.LaneIdx == s.LaneIdx && o.I == s.I + 1);
        static bool HasPrev(Ctx c, Seg s) => c.Segs.Exists(o => o.LaneIdx == s.LaneIdx && o.I == s.I - 1);

        // ---- garden walls, gates, retaining walls beside the lanes
        sealed class Cand { public Seg S; public float Sm, Len, Scale; public TownPoint Out, Pos; public bool Kept, Retain, Break; public float Rise; public int Index; }

        static bool WallPointBlocked(Ctx c, TownPoint p, Lane own)
        {
            foreach (Foundation f in c.T.Foundations) if (f.Footprint.Contains(p, 1.0f)) return true;
            foreach (Landmark l in c.T.Landmarks) if (l.Footprint.Contains(p, 0.5f)) return true;
            if (InSquare(p, 0.5f) || InKeepZone(p) || TooCloseToTownWall(p, LaneLimitE - 0.5f)) return true;
            foreach (Lane l in c.T.Lanes)
            {
                if (l == own) continue;
                if (HollowmereLayout.DistanceToPolyline(p, l.Points) < l.Width * 0.5f + 0.3f) return true;
            }
            return false;
        }

        static void GardenWalls(Ctx c)
        {
            for (int li = 0; li < c.Lanes.Count; li++)
                foreach (int side in new[] { 1, -1 })
                {
                    var cands = new List<Cand>();   // every piece along one lane edge in order; runs continue across segments that join end to end
                    Seg prev = null;
                    foreach (Seg s in c.Segs)
                    {
                        if (s.LaneIdx != li) continue;
                        float a = RangeA(s, WallJunctionClip), b = RangeB(s, WallJunctionClip), len = b - a;
                        bool joined = prev != null && prev.I == s.I - 1 && !prev.JB && !s.JA && prev.S1 > prev.Len - 0.15f && s.S0 < 0.05f;
                        prev = s;
                        if (len < 1.5f) { prev = null; continue; }
                        int n = Math.Max(1, (int)Math.Round(len / 2f));
                        float piece = len / n, scale = piece / 2f;
                        TownPoint outward = s.Right * side;
                        float lat = side * (s.Hw + WallOffset);
                        for (int i = 0; i < n; i++)
                        {
                            float sm = a + (i + 0.5f) * piece;
                            TownPoint pos = s.P(sm, lat);
                            bool blocked = false;
                            foreach (float dx in new[] { -1f, 0f, 1f })
                                foreach (float dl in new[] { -WallHalfThick, WallHalfThick })
                                    if (WallPointBlocked(c, s.P(sm + dx * scale, lat + side * dl), s.Lane)) blocked = true;
                            if (!blocked) blocked = CrossesEdge(c.Edges, s.P(sm - scale, lat), s.P(sm + scale, lat), 0.4f);
                            var cd = new Cand { S = s, Sm = sm, Len = piece, Scale = scale, Out = outward, Pos = pos, Kept = !blocked, Index = i, Break = i == 0 && !joined };
                            if (cd.Kept)
                            {
                                float up = c.G(s.P(sm, side * (s.Hw + 2.2f))), low = s.H(sm);
                                cd.Rise = up - low; cd.Retain = cd.Rise > 0.8f;
                            }
                            cands.Add(cd);
                        }
                    }
                    int start = 0;
                    while (start < cands.Count)
                    {
                        if (!cands[start].Kept) { start++; continue; }
                        int end = start;
                        while (end + 1 < cands.Count && cands[end + 1].Kept && !cands[end + 1].Break) end++;
                        EmitChain(c, cands, start, end, side);
                        start = end + 1;
                    }
                }
        }

        static void EmitChain(Ctx c, List<Cand> cands, int start, int end, int side)
        {
            Seg s = cands[start].S; string lane = s.Lane.Id;
            float chainLen = 0f; for (int i = start; i <= end; i++) chainLen += cands[i].Len;
            int gate = -1;
            if (chainLen >= 10f)
            {
                int mid = (start + end) / 2;
                for (int d = 0; d <= end - start && gate < 0; d++)
                    foreach (int k in new[] { mid - d, mid + d }) if (k >= start && k <= end && !cands[k].Retain && k > start && k < end) { gate = k; break; }
            }
            for (int i = start; i <= end; i++)
            {
                Cand cd = cands[i];
                float yaw = Yaw(cd.Out);
                TownPoint along = new TownPoint(cd.Out.Z, -cd.Out.X);   // local +X
                if (i == gate)
                {
                    EmitGate(c, cd, yaw, along, lane);
                    continue;
                }
                if (cd.Retain) EmitRetain(c, cd, yaw, lane);
                else
                {
                    float y = c.G(cd.Pos);
                    bool broken = TownWallPlan.Hash(Seed, 1000, c.Counter) < BrokenGardenShare;
                    AddPair(c, broken ? LanePiece.GardenWallBroken : LanePiece.GardenWall, LanePiece.GardenWall, lane, cd.Pos, y, yaw, cd.Scale);
                    AddMount(c, cd.S, cd.Sm, 1f, cd.Pos - cd.Out * WallHalfThick, y + 1.0f, yaw);
                }
                // Piers close a run at its ends and flank gaps (not next to a gate: its piers do that).
                if (i == start) Pier(c, cd, -1, yaw, along, lane);
                if (i == end) Pier(c, cd, 1, yaw, along, lane);
            }
        }

        static void Pier(Ctx c, Cand cd, int end, float yaw, TownPoint along, string lane)
        {
            // Local +X (`along`) may point either way along the lane; the pier sits 0.3 m inside the piece's end on the lane-direction side `end`.
            float sign = (along.X * cd.S.Dir.X + along.Z * cd.S.Dir.Z) >= 0f ? 1f : -1f;
            TownPoint p = cd.Pos + cd.S.Dir * (end * (cd.Len * 0.5f - 0.3f));
            Add(c, LanePiece.GardenWallEnd, LanePreset.Both, lane, p, c.G(p), yaw);
            if (sign == 0f) return;
        }

        static void EmitRetain(Ctx c, Cand cd, float yaw, string lane)
        {
            LanePiece low = cd.Rise <= 1.6f ? LanePiece.RetainWallLow : LanePiece.RetainWall;
            float top = Top(low), hl = cd.S.H(cd.Sm);
            float y = hl + Math.Min(cd.Rise, top) - top;
            LanePiece ruined = low == LanePiece.RetainWall && TownWallPlan.Hash(Seed, 2000, c.Counter) < BreachShare ? LanePiece.RetainWallBreach : low;
            AddPair(c, ruined, low, lane, cd.Pos, y, yaw, cd.Scale);
            AddMount(c, cd.S, cd.Sm, 2f, cd.Pos - cd.Out * 0.35f, hl + Math.Max(0.3f, Math.Min(1.6f, cd.Rise * 0.8f)), yaw);
        }

        static void EmitGate(Ctx c, Cand cd, float yaw, TownPoint along, string lane)
        {
            // Opening 1.4 m between the pier inner faces: pier centres +-1.0 from the centre, along the wall.
            TownPoint p1 = cd.Pos - along * 1.0f, p2 = cd.Pos + along * 1.0f;
            Add(c, LanePiece.GatePier, LanePreset.Both, lane, p1, c.G(p1), yaw);
            Add(c, LanePiece.GatePier, LanePreset.Both, lane, p2, c.G(p2), yaw);
            TownPoint hinge = cd.Pos - along * 0.7f;
            float roll = TownWallPlan.Hash(Seed, 3000, c.Counter);
            float hy = c.G(hinge);
            Add(c, LanePiece.GateLeaf, LanePreset.Rebuilt, lane, hinge, hy, yaw);
            if (roll < 0.6f) Add(c, LanePiece.GateLeafBroken, LanePreset.Ruined, lane, hinge, hy, yaw);
            AddMount(c, cd.S, cd.Sm, 3f, p1 - cd.Out * 0.3f, c.G(p1) + 1.8f, yaw);
        }

        // ---- terrace retaining walls (replace the greybox TerraceWall and LowerTerraceWall)
        static void TerraceWalls(Ctx c)
        {
            foreach (float offset in new[] { HollowmereLayout.TerraceOffset, HollowmereLayout.LowerTerraceOffset })
            {
                var run = HollowmereLayout.WallRun(c.T, HollowmereLayout.OffsetLine(offset, 16f), 1.0f, 1.0f);
                float sinceButtress = 0f, interval = 6f + 2f * TownWallPlan.Hash(Seed, 4000, 0);
                string name = "Terrace" + (offset == HollowmereLayout.TerraceOffset ? "" : "Lower");
                foreach (var (a, b) in run)
                {
                    TownPoint mid = (a + b) * 0.5f, d = b - a; float len = d.Length;
                    var nrm = new TownPoint(d.Z / len, -d.X / len);
                    TownPoint p1 = mid + nrm * 1.3f, p2 = mid - nrm * 1.3f;
                    float g1 = c.G(p1), g2 = c.G(p2);
                    TownPoint up = g1 >= g2 ? nrm : nrm * -1f;
                    float hi = Math.Max(g1, g2), step = hi - Math.Min(g1, g2);
                    if (step < 0.4f || WallSkip(c, mid, true)) { sinceButtress = 0f; continue; }
                    EmitModule(c, name, mid, up, hi, step, len, 5000, ref sinceButtress, ref interval);
                }
            }
        }

        // ---- lot cut/fill sides
        static void LotWalls(Ctx c)
        {
            foreach (Foundation f in c.T.Foundations)
                foreach (LotSide side in TownLayout.AllSides)
                {
                    LotSideHeights(f, side, out float hi, out float lo);
                    bool retain = hi > f.Y + 0.7f, fill = lo < f.Y - 0.7f;
                    if (!retain && !fill) continue;
                    TownPoint dir = Foundation.SideDir(side), along = new TownPoint(-dir.Z, dir.X);
                    float half = f.Side * 0.5f, radial = retain ? half + 1.4f : half + 0.65f, length = f.Side + 2.6f;
                    int n = (int)Math.Ceiling(length / 2f - 1e-3f);
                    float piece = length / n, step = retain ? hi - f.Y : f.Y - lo, none = 0f, interval = 1e9f;
                    for (int i = 0; i < n; i++)
                    {
                        TownPoint l = dir * radial + along * (-length * 0.5f + (i + 0.5f) * piece);
                        TownPoint mid = f.Footprint.ToWorld(l.X, l.Z);
                        TownPoint outward = f.Footprint.ToWorld(dir.X, dir.Z) - f.Footprint.ToWorld(0f, 0f);
                        TownPoint up = retain ? outward : outward * -1f;   // +Z toward the higher ground: out for a cut, in for a fill
                        if (WallSkip(c, mid, false)) continue;
                        TownPoint endA = mid - new TownPoint(up.Z, -up.X) * (piece * 0.5f), endB = mid + new TownPoint(up.Z, -up.X) * (piece * 0.5f);
                        if (CrossesEdge(c.Edges, endA, endB, 0.1f)) continue;
                        EmitModule(c, "Lot:" + f.Id, mid, up, retain ? f.Y + step : f.Y, step, piece, 6000, ref none, ref interval);
                    }
                }
        }

        // A terrace or lot-side wall piece is dropped inside the square, the keep, a landmark, another lot, or near the town wall line.
        static bool WallSkip(Ctx c, TownPoint p, bool terrace)
        {
            if (InSquare(p, 0.2f) || InKeep(p, 1.5f) || TooCloseToTownWall(p, LaneLimitE - 0.5f)) return true;
            foreach (Landmark l in c.T.Landmarks) if (l.Footprint.Contains(p, 0.2f)) return true;
            if (!terrace) { foreach (Foundation f in c.T.Foundations) if (f.Footprint.Contains(p, 0.1f)) return true; }
            return false;
        }

        // One retaining module on the line through `mid`: +Z toward the upper ground `up`, coping flush with `upperY`.
        static void EmitModule(Ctx c, string lane, TownPoint mid, TownPoint up, float upperY, float step, float len, int salt, ref float sinceButtress, ref float interval)
        {
            // A step taller than one module is built as a stack, each module's coping hidden 0.3 m into the one above; the top module takes the breach and the buttress.
            float rem = step, ub = upperY, yaw = Yaw(up), scale = len / 2f, y = 0f;
            LanePiece piece = LanePiece.RetainWall;
            for (int layer = 0; layer < 3 && (layer == 0 || rem > 0.3f); layer++)
            {
                piece = rem <= 1.6f ? LanePiece.RetainWallLow : LanePiece.RetainWall;
                float top = Top(piece); y = ub - top;
                LanePiece ruined = layer == 0 && piece == LanePiece.RetainWall && TownWallPlan.Hash(Seed, salt, c.Counter) < BreachShare ? LanePiece.RetainWallBreach : piece;
                AddPair(c, ruined, piece, lane, mid, y, yaw, scale);
                if (layer == 0)
                {
                    sinceButtress += len;
                    if (piece == LanePiece.RetainWall && sinceButtress >= interval)
                    {
                        Add(c, LanePiece.RetainButtress, LanePreset.Both, lane, mid, y, yaw);
                        sinceButtress = 0f; interval = 6f + 2f * TownWallPlan.Hash(Seed, salt + 1, c.Counter);
                    }
                }
                rem -= top - 0.3f; ub -= top - 0.3f;
            }
            // Lantern mount on the lane-facing (lower) face when a lane runs within 3 m.
            NearestLane(c, mid, out int li, out float ls, out float dist);
            if (li >= 0 && dist <= 3f + c.Lanes[li].Width * 0.5f)
                AddMount(li, c, ls, 2f, mid - up * 0.35f, upperY - step + Math.Max(0.3f, Math.Min(1.6f, step * 0.8f)), yaw);
        }

        // ---- lanterns
        static void AddMount(Ctx c, Seg s, float sm, float prio, TownPoint pos, float y, float yaw) =>
            c.Mounts.Add(new Mount { LaneIdx = s.LaneIdx, S = s.Cum + sm, Prio = prio, Pos = pos, Y = Math.Max(y, c.G(pos) + 0.9f), Yaw = yaw });
        static void AddMount(int li, Ctx c, float s, float prio, TownPoint pos, float y, float yaw) =>
            c.Mounts.Add(new Mount { LaneIdx = li, S = s, Prio = prio, Pos = pos, Y = Math.Max(y, c.G(pos) + 0.9f), Yaw = yaw });

        static void NearestLane(Ctx c, TownPoint p, out int li, out float s, out float dist)
        {
            li = -1; s = 0f; dist = float.MaxValue;
            foreach (Seg g in c.Segs)
            {
                TownPoint q = HollowmereLayout.ClosestOnSegment(p, g.A, g.B, out float tt);
                float d = TownPoint.Distance(p, q);
                if (d < dist) { dist = d; li = g.LaneIdx; s = g.Cum + tt * g.Len; }
            }
        }

        static void Lanterns(Ctx c)
        {
            for (int li = 0; li < c.Lanes.Count; li++)
            {
                var cand = c.Mounts.FindAll(m => m.LaneIdx == li);
                if (cand.Count == 0) continue;
                cand.Sort((x, y) => x.S != y.S ? x.S.CompareTo(y.S) : x.Prio.CompareTo(y.Prio));
                var picked = new List<Mount>();
                float total = HollowmereLayout.LaneLength(c.Lanes[li].Points);
                var targets = new List<float>();
                for (float s = 7.5f; s < total + 7.5f; s += 15f) targets.Add(s);
                foreach (Flight f in c.Flights) if (f.LaneIdx == li) { targets.Add(f.SHead); targets.Add(f.SFoot); }
                targets.Sort();
                foreach (float tgt in targets)
                {
                    Mount best = null; float bestScore = float.MaxValue;
                    foreach (Mount m in cand)
                    {
                        float d = Math.Abs(m.S - tgt);
                        if (d > 7f || picked.Exists(p => Math.Abs(p.S - m.S) < 6f && TownPoint.Distance(p.Pos, m.Pos) < 6f)) continue;
                        float score = d - m.Prio * 1.5f;
                        if (score < bestScore) { bestScore = score; best = m; }
                    }
                    if (best != null) picked.Add(best);
                }
                foreach (Mount m in picked)
                    AddPair(c, LanePiece.WallLanternBroken, LanePiece.WallLantern, c.Lanes[li].Id, m.Pos, m.Y, m.Yaw);
            }
        }

        // ------------------------------------------------------------------------------------------------ signature (tests)
        // Integer-quantised so the hash does not depend on the runtime's float formatting (Unity's Mono and .NET round "0.000" differently at ties).
        static long Q(float v, double k) => (long)Math.Round((double)v * k, MidpointRounding.AwayFromZero);

        public static string Signature(IList<LaneKitRow> rows)
        {
            var sb = new System.Text.StringBuilder();
            foreach (LaneKitRow r in rows)
                sb.Append(r.Piece).Append(':').Append((int)r.Preset).Append(':').Append(Q(r.X, 1000)).Append(':').Append(Q(r.Y, 1000)).Append(':')
                  .Append(Q(r.Z, 1000)).Append(':').Append(Q(r.Yaw, 10)).Append(':').Append(Q(r.ScaleX, 100)).Append(';');
            return sb.ToString();
        }

        public static uint Hash(string s)
        {
            unchecked { uint h = 2166136261u; foreach (char ch in s) { h ^= ch; h *= 16777619u; } return h; }
        }
    }
}
