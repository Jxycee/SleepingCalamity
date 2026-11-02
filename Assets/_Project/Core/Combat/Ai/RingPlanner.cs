// Core/Combat/Ai/RingPlanner.cs: spacing rings, shared angular slots, strafe flip and separation (enemy AI stage 4, docs/research/enemy-ai.md 3.1-3.4, 10.5).
// Pure C# (plain floats, no UnityEngine). Authority: host. CombatHost owns ONE planner for the whole host (like TargetLedger): slots are per hero, and a hero
// can be worked by enemies of several rooms. Nothing here is replicated.
// A slot is an ANGLE around the hero (world yaw, so it moves with the hero), on a ring whose radius is the enemy's preferred ring. Enemies that hover on one
// hero share the ring: each keeps its own angle (the free slot nearest its approach angle when it first claims), the angles are pushed apart to at least
// 360/max(N,4) degrees (times SlotGapFactor), and at most half of the front arc (the half-width around the hero's facing) is used. Each hovering enemy
// circles by drifting its slot angle at its circle speed and flips direction on a seeded timer. Separate from the slots, a soft push keeps ANY two enemies
// that report positions apart by their separation radius. No per-tick allocation: members are created once per enemy and reused.
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.Core.Combat
{
    public enum EnemyRole : byte { Rusher = 0, Skirmisher = 1, Ranged = 2, Heavy = 3 }

    // All the placeholder numbers of ring planning in one place (doc 10.2 defaults). Tune here, not at the call sites. P9 Placeholder.
    public sealed class RingTuning
    {
        public const float DefaultHoverRing = 3.0f, DefaultCircleSpeedScale = 0.45f, DefaultFlipMinSeconds = 1.5f, DefaultFlipMaxSeconds = 3.0f;

        public float FrontHalfArcDeg = 60f;     // the arc toward the hero's facing that is kept (half) clear so the player sees the near group
        public float FrontMaxFraction = 0.5f;   // at most this share of the front arc's slot capacity may be used
        public float SlotGapFactor = 0.75f;     // slots are at least 360/max(N,4) * this apart (under 1 so a full ring with half its front arc closed still packs)
        public int MaxPerRing = 6;              // more enemies than this on one hero spill to the next ring out
        public float RingStep = 1.6f;           // metres between a ring and the next one out
        public float StaleSeconds = 1.0f;       // an enemy that stopped reporting for this long no longer holds a slot
        public float SlotFollowRange = 1.5f;    // the slot only drifts (circles) while its enemy is within this of it, so it never runs away
        public float SeparationGain = 3.0f;     // weight of the separation push against the move toward the slot
        public float MinRingMeters = 1.8f;      // a room-clamped ring never shrinks below this (unless the room needs less to hold the slot)
        public float BlockedSeconds = 0.5f;     // an enemy that moved less than BlockedMeters over this long counts as blocked (a collider on the way)
        public float BlockedMeters = 0.15f;

        public static readonly RingTuning Default = new RingTuning();
    }

    public struct RingRequest
    {
        public int SelfId, HeroId;
        public float HeroX, HeroZ;
        public float FrontX, FrontZ;        // the hero's facing (flat); zero: no front arc rule
        public float SelfX, SelfZ;
        public float Ring;                  // preferred ring radius (m)
        public float MaxRing;               // outermost ring a spill may reach (the enemy's attack reach); 0: no cap. Past it the slots pack tighter instead
        public float CircleSpeed;           // m/s along the ring
        public float FlipMin, FlipMax;      // strafe flip interval (s)
        public float SeparationRadius;      // m
        public float InitialSign;           // +1 / -1; 0 draws it from the member's own seeded stream
        public double Now;
        public float Dt;
        public Func<float, float, bool> InBounds;   // optional: is (x, z) inside the enemy's room? A slot outside is pulled in toward the hero
    }

    public struct RingResult
    {
        public float SlotX, SlotZ;          // world point to hover at
        public float SepX, SepZ;            // separation push (unit-ish; 0 when nobody is too close)
        public float Sign;                  // current circling direction (+1 / -1)
        public float Angle;                 // the slot's world angle (radians)
        public float RingRadius;            // the ring actually used (after spill and room clamp)
    }

    public sealed class RingPlanner
    {
        private sealed class Member
        {
            public int Id, Hero;
            public long Order;              // claim order on the hero: the earlier claimer keeps the contested slot
            public float X, Z, Sep;
            public double Seen = double.NegativeInfinity;       // last report of any kind (separation)
            public double SlotSeen = double.NegativeInfinity;   // last report as a hoverer (holds a slot)
            public float Angle, Pref, Eff, Sign = 1f;
            public float ChkX, ChkZ; public double ChkAt = double.NegativeInfinity; public bool Blocked;   // progress check: no movement for BlockedSeconds frees the slot to drift
            public double FlipAt;
            public Pcg32 Rng;
        }

        private const float TwoPi = (float)(Math.PI * 2.0), Pi = (float)Math.PI, Deg = (float)(Math.PI / 180.0);

        private readonly RingTuning _t;
        private readonly ulong _seed;
        private readonly Dictionary<int, Member> _byId = new Dictionary<int, Member>();
        private readonly List<Member> _all = new List<Member>();
        private long _seq;

        public RingPlanner(ulong seed = 0x51A7UL, RingTuning tuning = null) { _seed = seed; _t = tuning ?? RingTuning.Default; }

        public int Count => _all.Count;
        public RingTuning Tuning => _t;

        public void Remove(int id)
        {
            if (!_byId.TryGetValue(id, out Member m)) return;
            _byId.Remove(id);
            _all.Remove(m);
        }

        public void Clear() { _byId.Clear(); _all.Clear(); }

        // Test hooks.
        public bool TryGetAngle(int id, out float angle) { bool ok = _byId.TryGetValue(id, out Member m); angle = ok ? m.Angle : 0f; return ok; }
        public bool TryGetSign(int id, out float sign) { bool ok = _byId.TryGetValue(id, out Member m); sign = ok ? m.Sign : 0f; return ok; }

        private Member Get(int id, double now)
        {
            if (_byId.TryGetValue(id, out Member m)) return m;
            m = new Member { Id = id, Rng = new Pcg32(unchecked(_seed + (ulong)(uint)id * 0x9E3779B97F4A7C15UL), (ulong)(uint)id) };
            _byId[id] = m;
            _all.Add(m);
            return m;
        }

        private static float Wrap(float a)
        {
            while (a > Pi) a -= TwoPi;
            while (a <= -Pi) a += TwoPi;
            return a;
        }

        private bool Slotting(Member m, double now) => now - m.SlotSeen <= _t.StaleSeconds;

        // Position report without a slot (approaching, striking): joins the separation. Returns the push.
        public void Observe(int selfId, float x, float z, float sepRadius, double now, out float sepX, out float sepZ)
        {
            Member m = Get(selfId, now);
            m.X = x; m.Z = z; m.Sep = sepRadius; m.Seen = now;
            Separation(m, now, out sepX, out sepZ);
        }

        // One hovering enemy's tick: claim or keep its slot, circle, flip, resolve against the others, and return the point to move to plus the separation push.
        public RingResult Plan(in RingRequest q)
        {
            Member m = Get(q.SelfId, q.Now);
            bool fresh = m.Hero != q.HeroId || !Slotting(m, q.Now) || m.Order == 0;
            m.X = q.SelfX; m.Z = q.SelfZ; m.Sep = q.SeparationRadius; m.Seen = q.Now;
            if (fresh)
            {
                m.Hero = q.HeroId;
                m.Order = ++_seq;
                m.Angle = (float)Math.Atan2(q.SelfZ - q.HeroZ, q.SelfX - q.HeroX);   // the free slot nearest where it came from
                m.ChkAt = q.Now; m.ChkX = q.SelfX; m.ChkZ = q.SelfZ; m.Blocked = false;
                m.Sign = q.InitialSign != 0f ? (q.InitialSign < 0f ? -1f : 1f) : (m.Rng.NextFloat() < 0.5f ? -1f : 1f);
                m.FlipAt = q.Now + m.Rng.Range(q.FlipMin, Math.Max(q.FlipMin + 0.001f, q.FlipMax));
            }
            m.SlotSeen = q.Now;
            m.Pref = q.Ring;

            if (q.Now >= m.FlipAt)
            {
                m.Sign = -m.Sign;
                m.FlipAt = q.Now + m.Rng.Range(q.FlipMin, Math.Max(q.FlipMin + 0.001f, q.FlipMax));
            }

            // Which ring: members past MaxPerRing (by claim order) spill outward.
            int rank = 0;
            for (int i = 0; i < _all.Count; i++)
            {
                Member o = _all[i];
                if (o == m || o.Hero != m.Hero || !Slotting(o, q.Now) || o.Order >= m.Order) continue;
                if (Math.Abs(o.Pref - m.Pref) < _t.RingStep * 0.5f) rank++;
            }
            float ring = m.Pref + (rank / Math.Max(1, _t.MaxPerRing)) * _t.RingStep;
            if (q.MaxRing > 0f) ring = Math.Max(m.Pref, Math.Min(ring, q.MaxRing));   // out of reach there is no point: the ring packs tighter (smaller gap below) instead
            m.Eff = ring;

            // Circle: the slot drifts along the ring while its enemy keeps up.
            float sx = q.HeroX + (float)Math.Cos(m.Angle) * ring, sz = q.HeroZ + (float)Math.Sin(m.Angle) * ring;
            float fx = sx - q.SelfX, fz = sz - q.SelfZ;
            if (q.Now - m.ChkAt >= _t.BlockedSeconds)
            {
                float cx = q.SelfX - m.ChkX, cz = q.SelfZ - m.ChkZ;
                m.Blocked = cx * cx + cz * cz < _t.BlockedMeters * _t.BlockedMeters && fx * fx + fz * fz > 0.09f;   // not moving and not yet at the slot
                m.ChkAt = q.Now; m.ChkX = q.SelfX; m.ChkZ = q.SelfZ;
            }
            if (m.Blocked || fx * fx + fz * fz <= _t.SlotFollowRange * _t.SlotFollowRange)   // a blocked enemy keeps its slot drifting so it finds another way round
                m.Angle = Wrap(m.Angle + m.Sign * (q.CircleSpeed / Math.Max(0.5f, ring)) * q.Dt);

            // Peers on this ring band.
            int n = 1;
            for (int i = 0; i < _all.Count; i++)
            {
                Member o = _all[i];
                if (o == m || o.Hero != m.Hero || !Slotting(o, q.Now) || Math.Abs(o.Eff - ring) >= _t.RingStep * 0.5f) continue;
                n++;
            }
            float chord = 2f * (float)Math.Asin(Math.Min(1f, q.SeparationRadius / (2f * Math.Max(0.5f, ring))));   // radians for a chord of one separation radius
            float gap = Math.Max(TwoPi * _t.SlotGapFactor / Math.Max(n, 4), chord);
            // Resolve against the EARLIER claimers (they keep their angles, so a crowd settles in claim order and nothing tugs): keep at least `gap` from each,
            // and stay out of the front arc once half its capacity is taken. A few passes, because leaving the arc can land next to another slot.
            bool hasFront = q.FrontX * q.FrontX + q.FrontZ * q.FrontZ > 1e-6f;
            float front = hasFront ? (float)Math.Atan2(q.FrontZ, q.FrontX) : 0f;
            float half = _t.FrontHalfArcDeg * Deg;
            int allowed = (int)Math.Floor(((int)Math.Floor(2f * half / gap) + 1) * _t.FrontMaxFraction);
            bool wall = false;   // the front arc is full for this member: it is a forbidden zone
            if (hasFront)
            {
                int inArc = 0;
                for (int i = 0; i < _all.Count; i++)
                {
                    Member o = _all[i];
                    if (o == m || o.Hero != m.Hero || !Slotting(o, q.Now) || o.Order >= m.Order || Math.Abs(o.Eff - ring) >= _t.RingStep * 0.5f) continue;
                    if (Math.Abs(Wrap(o.Angle - front)) < half) inArc++;
                }
                wall = inArc >= allowed;
            }
            m.Angle = Resolve(m, ring, gap, wall, front, half, q.Now);

            // Slot point, pulled toward the hero until it is inside the room (when the room is known).
            float r = ring;
            float px = q.HeroX + (float)Math.Cos(m.Angle) * r, pz = q.HeroZ + (float)Math.Sin(m.Angle) * r;
            if (q.InBounds != null)
            {
                for (int i = 0; i < 4 && !q.InBounds(px, pz) && r > _t.MinRingMeters; i++)
                {
                    r = Math.Max(_t.MinRingMeters, r * 0.8f);
                    px = q.HeroX + (float)Math.Cos(m.Angle) * r; pz = q.HeroZ + (float)Math.Sin(m.Angle) * r;
                }
                // Still outside (a corner, a bay): the nearest in-room point on the line toward the hero, even closer than the minimum ring.
                for (int i = 0; i < 12 && !q.InBounds(px, pz) && r > 0.3f; i++)
                {
                    r = Math.Max(0.3f, r - 0.3f);
                    px = q.HeroX + (float)Math.Cos(m.Angle) * r; pz = q.HeroZ + (float)Math.Sin(m.Angle) * r;
                }
            }

            var res = new RingResult { SlotX = px, SlotZ = pz, Sign = m.Sign, Angle = m.Angle, RingRadius = r };
            Separation(m, q.Now, out res.SepX, out res.SepZ);
            return res;
        }

        // Keeps a step from carrying the enemy into another one: for every reporting neighbour that the step (unit direction x speed x dt) would bring inside the
        // separation radius, the part of the direction that points at the neighbour is removed (the enemy slides around it). The push in Plan/Observe only
        // pulls apart what already overlaps; this stops head-on crossings from overlapping in the first place. Direction is renormalised; returns false when
        // nothing is left to move.
        public bool Slide(int selfId, double now, float speed, float dt, ref float mx, ref float mz)
        {
            if (!_byId.TryGetValue(selfId, out Member m)) return true;
            float step = speed * dt;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _all.Count; i++)
                {
                    Member o = _all[i];
                    if (o == m || now - o.Seen > _t.StaleSeconds) continue;
                    float min = Math.Max(m.Sep, o.Sep);
                    float dx = m.X - o.X, dz = m.Z - o.Z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < 1e-8f) continue;
                    float nx = m.X + mx * step - o.X, nz = m.Z + mz * step - o.Z;
                    if (nx * nx + nz * nz >= min * min || nx * nx + nz * nz >= d2) continue;   // stays clear, or moves away
                    float d = (float)Math.Sqrt(d2), ux = dx / d, uz = dz / d;
                    float toward = mx * ux + mz * uz;
                    if (toward >= 0f) continue;
                    mx -= toward * ux; mz -= toward * uz;
                }
            }
            float mag = (float)Math.Sqrt(mx * mx + mz * mz);
            if (mag < 1e-3f) { mx = 0f; mz = 0f; return false; }
            mx /= mag; mz /= mag;
            m.X += mx * step; m.Z += mz * step;   // the neighbours who plan after this one see where it is going, not where it was
            return true;
        }

        // The valid angle nearest the member's own: its own if it is free; else the spot `gap` beside an earlier claimer (either side) or just outside the full
        // front arc (the wall), whichever is valid and closest. Valid = at least `gap` from every earlier claimer on this ring band and outside the wall.
        // No valid spot (an over-full ring): the roomiest candidate.
        private float Resolve(Member m, float ring, float gap, bool wall, float front, float half, double now)
        {
            float best = m.Angle, bestMove = float.MaxValue, fallback = m.Angle, fallbackNearest = -1f;
            int count = _all.Count;
            int total = 1 + 2 * count + 2;
            for (int c = 0; c < total; c++)
            {
                float cand;
                if (c == 0) cand = m.Angle;
                else if (c <= 2 * count)
                {
                    Member o = _all[(c - 1) >> 1];
                    if (!Earlier(o, m, ring, now)) continue;
                    cand = Wrap(o.Angle + ((c & 1) == 1 ? gap : -gap));
                }
                else
                {
                    if (!wall) continue;
                    cand = Wrap(front + ((c & 1) == 1 ? 1f : -1f) * (half + 0.5f * Deg));
                }
                if (wall && Math.Abs(Wrap(cand - front)) < half) continue;
                float nearest = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    Member o = _all[i];
                    if (Earlier(o, m, ring, now)) nearest = Math.Min(nearest, Math.Abs(Wrap(cand - o.Angle)));
                }
                if (nearest < gap - 1e-4f)
                {
                    if (nearest > fallbackNearest) { fallbackNearest = nearest; fallback = cand; }   // over-full ring: the roomiest spot is the best of a bad lot
                    continue;
                }
                float move = Math.Abs(Wrap(cand - m.Angle));
                if (move < bestMove) { bestMove = move; best = cand; }
            }
            return bestMove < float.MaxValue ? best : fallback;
        }

        private bool Earlier(Member o, Member m, float ring, double now)
            => o != m && o.Hero == m.Hero && o.Order < m.Order && Slotting(o, now) && Math.Abs(o.Eff - ring) < _t.RingStep * 0.5f;

        // Soft push away from every other reporting enemy closer than the larger separation radius, proportional to the overlap.
        private void Separation(Member m, double now, out float sepX, out float sepZ)
        {
            sepX = 0f; sepZ = 0f;
            for (int i = 0; i < _all.Count; i++)
            {
                Member o = _all[i];
                if (o == m || now - o.Seen > _t.StaleSeconds) continue;
                float min = Math.Max(m.Sep, o.Sep);
                float dx = m.X - o.X, dz = m.Z - o.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 >= min * min) continue;
                if (d2 < 1e-6f)
                {
                    // exactly stacked: split along a fixed per-order direction so the pair separates in opposite ways
                    float a = (m.Id ^ o.Id) * 2.399963f + (m.Id > o.Id ? 0f : Pi);
                    sepX += (float)Math.Cos(a); sepZ += (float)Math.Sin(a);
                    continue;
                }
                float d = (float)Math.Sqrt(d2);
                float push = (min - d) / min;
                sepX += dx / d * push; sepZ += dz / d * push;
            }
        }
    }
}
