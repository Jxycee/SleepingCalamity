// Core/Cellars/CellarVignette.cs: authored set-pieces for the Undercroft Cellars (variety layer 4, research procedural-dungeons 3.4) and the selection that decides
// which ones a floor of a dive shows. A vignette is a small greybox prop group (one 4 m cell of a combat room) with a category (Cogmind's four kinds: fluff,
// reward, risk-versus-reward, danger), a base rarity "1 in N", the first floor it may appear on, and two flags: once per dive, and "must see" (escalating odds).
// Selection (VignetteDive.Select): for floors 1..N of one dive seed, replayed from the seed so floor N's answer is a pure function of (seed, N):
//   - each vignette that is eligible on the floor (floor >= MinFloor, not already seen if OncePerDive) rolls "1 in odds" on its own draw; the draws are made for
//     every vignette every floor, so the stream position never depends on the state;
//   - odds fall with every miss (Spelunky's falling counter): odds = Rarity - misses * PityStep, floored at 1; a MustSee vignette starts at MustSeeStart and so
//     reads 1/4, 1/3, 1/2, certain over four eligible floors (the Moai pattern); a hit resets the counter;
//   - a floor shows at most MaxPerFloor vignettes (the extra hits are dropped by a priority draw and count as misses; a must-see at certain odds outranks the draw),
//     so the average stays near the budget;
//   - floor GuaranteeFloor shows one by force if the dive has seen none yet, so no dive of floors 1-3 is bare.
// Placement is CellarFloorDressing's job (it needs the layout). Authority: none (static data; every peer picks the same).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum VignetteCategory : byte { Fluff = 0, Reward = 1, RiskReward = 2, Danger = 3 }

    public enum PropShape : byte { Box, Cylinder, Sphere, Capsule }

    // One primitive of a greybox prop group. Offsets are metres from the cell centre (Y is the height of the primitive's centre); Size is the full extent
    // (a Cylinder or Capsule uses SizeX as its diameter and SizeY as its height). Rgb is 0xRRGGBB. The group is turned by whole quarter turns when placed.
    public readonly struct VignetteProp
    {
        public readonly string Name; public readonly PropShape Shape; public readonly float X, Y, Z, SizeX, SizeY, SizeZ, YawDegrees; public readonly int Rgb;
        public VignetteProp(string name, PropShape shape, float x, float y, float z, float sx, float sy, float sz, int rgb, float yaw = 0f)
        { Name = name; Shape = shape; X = x; Y = y; Z = z; SizeX = sx; SizeY = sy; SizeZ = sz; Rgb = rgb; YawDegrees = yaw; }
    }

    public sealed class CellarVignette
    {
        public readonly string Id, Name; public readonly VignetteCategory Category;
        public readonly int Rarity;               // base odds 1 in Rarity on an eligible floor
        public readonly int MinFloor;             // first floor it may appear on
        public readonly bool OncePerDive, MustSee;
        public readonly bool WallHug;             // the group's back (local -Z, the south side) is turned to a wall of its cell, and it only stands in a cell that has one
        public readonly IReadOnlyList<VignetteProp> Props;
        public CellarVignette(string id, string name, VignetteCategory category, int rarity, int minFloor, bool oncePerDive, bool mustSee, bool wallHug, params VignetteProp[] props)
        { WallHug = wallHug; Id = id; Name = name; Category = category; Rarity = rarity; MinFloor = minFloor; OncePerDive = oncePerDive; MustSee = mustSee; Props = props; }
    }

    public static class CellarVignettePool
    {
        public static readonly IReadOnlyList<CellarVignette> All = Build().AsReadOnly();

        public static CellarVignette Find(string id) { foreach (CellarVignette v in All) if (v.Id == id) return v; return null; }

        static VignetteProp P(string name, PropShape s, float x, float y, float z, float sx, float sy, float sz, int rgb, float yaw = 0f) => new VignetteProp(name, s, x, y, z, sx, sy, sz, rgb, yaw);

        // Greybox only: boxes, cylinders and capsules in flat colours (the art pass swaps real props in by vignette id).
        static List<CellarVignette> Build()
        {
            const PropShape B = PropShape.Box, Cy = PropShape.Cylinder, Sp = PropShape.Sphere, Ca = PropShape.Capsule;
            return new List<CellarVignette>
            {
                new CellarVignette("abandoned_camp", "Abandoned adventurer camp", VignetteCategory.Fluff, 4, 1, true, false, false,
                    P("Bedroll", B, -0.9f, 0.1f, 0.5f, 1.8f, 0.2f, 0.8f, 0x6B5B45, 12f), P("FireRing", Cy, 0.7f, 0.1f, -0.2f, 0.9f, 0.2f, 0.9f, 0x3A3532),
                    P("Embers", Sp, 0.7f, 0.2f, -0.2f, 0.3f, 0.12f, 0.3f, 0x7A3B1E), P("Pack", B, -0.5f, 0.3f, -0.9f, 0.6f, 0.6f, 0.4f, 0x7C6A3E, -20f)),
                new CellarVignette("toppled_wine_rack", "Toppled wine rack with rats", VignetteCategory.Danger, 5, 1, false, false, false,
                    P("RackFrame", B, 0f, 0.35f, 0f, 2.6f, 0.3f, 0.9f, 0x5A3E26, 8f), P("Bottle1", Ca, 0.9f, 0.12f, 0.8f, 0.14f, 0.5f, 0.14f, 0x3E5A32, 80f),
                    P("Bottle2", Ca, -0.4f, 0.12f, 0.9f, 0.14f, 0.5f, 0.14f, 0x3E5A32, 100f), P("Bottle3", Ca, 0.3f, 0.12f, -0.9f, 0.14f, 0.5f, 0.14f, 0x2F4A5A, 70f),
                    P("Rat1", Ca, -1.0f, 0.1f, -0.6f, 0.18f, 0.22f, 0.18f, 0x4A4038, 90f), P("Rat2", Ca, 1.1f, 0.1f, 0.2f, 0.18f, 0.22f, 0.18f, 0x4A4038, 75f)),
                new CellarVignette("childs_drawing", "Child's drawing on the wall", VignetteCategory.Fluff, 3, 1, true, true, true,
                    P("Panel", B, 0f, 1.2f, -1.85f, 1.3f, 1.0f, 0.04f, 0xCDBE9A), P("Sun", Sp, -0.3f, 1.4f, -1.82f, 0.22f, 0.22f, 0.02f, 0xE0B040),
                    P("House", B, 0.25f, 1.05f, -1.82f, 0.34f, 0.3f, 0.02f, 0xA8503A), P("Stool", Cy, 0f, 0.2f, -1.2f, 0.4f, 0.4f, 0.4f, 0x6A5236)),
                new CellarVignette("collapsed_shelf", "Collapsed shelf over a cache", VignetteCategory.RiskReward, 5, 1, false, false, true,
                    P("Plank1", B, -0.3f, 0.45f, 0f, 2.2f, 0.12f, 0.5f, 0x5A3E26, 20f), P("Plank2", B, 0.4f, 0.3f, 0.2f, 1.9f, 0.12f, 0.5f, 0x5A3E26, -35f),
                    P("Jars", Cy, 1.1f, 0.15f, -0.9f, 0.3f, 0.3f, 0.3f, 0x8A7A58), P("Cache", B, 0f, 0.15f, 0f, 0.7f, 0.3f, 0.5f, 0xD6A24A)),
                new CellarVignette("sleeping_elite", "Sleeping elite", VignetteCategory.Danger, 8, 2, true, false, false,
                    P("Body", Ca, 0f, 0.35f, 0f, 0.6f, 1.8f, 0.6f, 0x8C2A22, 90f), P("Marker", Cy, 0f, 0.03f, 0f, 2.6f, 0.03f, 2.6f, 0xB84A2A),
                    P("Horn", Cy, 0.9f, 0.7f, 0f, 0.15f, 0.3f, 0.15f, 0xD8CFA8)),
                new CellarVignette("lantern_cache", "Lantern-oil cache", VignetteCategory.Reward, 7, 2, false, false, false,
                    P("Crate", B, 0f, 0.3f, 0f, 0.9f, 0.6f, 0.7f, 0x6A4A28), P("Oil1", Cy, 0.8f, 0.2f, 0.5f, 0.3f, 0.4f, 0.3f, 0xC8A040), P("Oil2", Cy, -0.8f, 0.2f, -0.4f, 0.3f, 0.4f, 0.3f, 0xC8A040),
                    P("Glint", Sp, 0f, 0.8f, 0f, 0.2f, 0.2f, 0.2f, 0xF2D58A)),
            };
        }
    }

    // The vignettes one floor of a dive shows, plus the counters after it (so a test can look at them).
    public sealed class VignetteFloor
    {
        public readonly int Floor; public readonly IReadOnlyList<CellarVignette> Chosen; public readonly bool Forced;
        public VignetteFloor(int floor, IReadOnlyList<CellarVignette> chosen, bool forced) { Floor = floor; Chosen = chosen; Forced = forced; }
    }

    public static class VignetteDive
    {
        public const int MaxPerFloor = 2, GuaranteeFloor = 3, MustSeeStart = 4, PityStep = 1;

        // The pick for floor `floor` (1-based) of the dive `seed`: replays floors 1..floor. Pure.
        public static VignetteFloor Select(uint seed, int floor)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException(nameof(floor));
            IReadOnlyList<CellarVignette> pool = CellarVignettePool.All;
            int n = pool.Count;
            var misses = new int[n]; var seen = new bool[n]; int total = 0; VignetteFloor last = null;
            for (int f = 1; f <= floor; f++)
            {
                var rng = new Pcg32(Rng.Derive(seed, "cellars.vignette", (ulong)f));
                var roll = new uint[n]; var prio = new uint[n];
                for (int i = 0; i < n; i++) { roll[i] = rng.NextUInt(); prio[i] = rng.NextUInt(); }
                var eligible = new bool[n]; var hit = new List<int>(); var certain = new bool[n];   // certain: a must-see whose odds have fallen to 1 (it outranks the cap)
                for (int i = 0; i < n; i++)
                {
                    CellarVignette v = pool[i];
                    eligible[i] = f >= v.MinFloor && !(v.OncePerDive && seen[i]);
                    if (!eligible[i]) continue;
                    int odds = v.MustSee ? MustSeeStart - misses[i] : v.Rarity - misses[i] * PityStep;
                    if (odds < 1) odds = 1;
                    certain[i] = v.MustSee && odds == 1;
                    if ((uint)(((ulong)roll[i] * (ulong)odds) >> 32) == 0) hit.Add(i);   // multiply-shift: true with probability 1/odds
                }
                bool forced = false;
                if (hit.Count == 0 && f == GuaranteeFloor && total == 0)
                {
                    int best = -1;
                    for (int i = 0; i < n; i++) if (eligible[i] && (best < 0 || prio[i] > prio[best])) best = i;
                    if (best >= 0) { hit.Add(best); forced = true; }
                }
                hit.Sort((a, b) => certain[a] != certain[b] ? (certain[a] ? -1 : 1) : prio[a] != prio[b] ? prio[b].CompareTo(prio[a]) : a.CompareTo(b));   // certain must-sees, then highest priority, ties by pool order
                var chosen = new List<CellarVignette>();
                for (int k = 0; k < hit.Count && k < MaxPerFloor; k++) chosen.Add(pool[hit[k]]);
                var isChosen = new bool[n]; for (int k = 0; k < hit.Count && k < MaxPerFloor; k++) isChosen[hit[k]] = true;
                for (int i = 0; i < n; i++)
                {
                    if (isChosen[i]) { misses[i] = 0; seen[i] = true; }
                    else if (eligible[i]) misses[i]++;
                }
                total += chosen.Count;
                last = new VignetteFloor(f, chosen, forced);
            }
            return last;
        }
    }
}
