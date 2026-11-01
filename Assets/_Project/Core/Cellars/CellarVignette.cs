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
//   - state-conditioned odds (procgen 5): each draw's probability is scaled by CellarVignetteTuning.Percent, a function of the floor depth, the floor's special-room kind
//     (SpecialRoomDive.KindFor: seed and floor only) and what earlier floors of this dive showed (family spacing, category fatigue); never of runtime player state.
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
                new CellarVignette("toppled_wine_rack", "Toppled wine rack with rats", VignetteCategory.Danger, 5, 1, true, false, false,
                    P("RackFrame", B, 0f, 0.35f, 0f, 2.6f, 0.3f, 0.9f, 0x5A3E26, 8f), P("Bottle1", Ca, 0.9f, 0.12f, 0.8f, 0.14f, 0.5f, 0.14f, 0x3E5A32, 80f),
                    P("Bottle2", Ca, -0.4f, 0.12f, 0.9f, 0.14f, 0.5f, 0.14f, 0x3E5A32, 100f), P("Bottle3", Ca, 0.3f, 0.12f, -0.9f, 0.14f, 0.5f, 0.14f, 0x2F4A5A, 70f),
                    P("Rat1", Ca, -1.0f, 0.1f, -0.6f, 0.18f, 0.22f, 0.18f, 0x4A4038, 90f), P("Rat2", Ca, 1.1f, 0.1f, 0.2f, 0.18f, 0.22f, 0.18f, 0x4A4038, 75f)),
                new CellarVignette("childs_drawing", "Child's drawing on the wall", VignetteCategory.Fluff, 3, 1, true, true, true,
                    P("Panel", B, 0f, 1.2f, -1.85f, 1.3f, 1.0f, 0.04f, 0xCDBE9A), P("Sun", Sp, -0.3f, 1.4f, -1.82f, 0.22f, 0.22f, 0.02f, 0xE0B040),
                    P("House", B, 0.25f, 1.05f, -1.82f, 0.34f, 0.3f, 0.02f, 0xA8503A), P("Stool", Cy, 0f, 0.2f, -1.2f, 0.4f, 0.4f, 0.4f, 0x6A5236)),
                new CellarVignette("collapsed_shelf", "Collapsed shelf over a cache", VignetteCategory.RiskReward, 5, 1, true, false, true,
                    P("Plank1", B, -0.3f, 0.45f, 0f, 2.2f, 0.12f, 0.5f, 0x5A3E26, 20f), P("Plank2", B, 0.4f, 0.3f, 0.2f, 1.9f, 0.12f, 0.5f, 0x5A3E26, -35f),
                    P("Jars", Cy, 1.1f, 0.15f, -0.9f, 0.3f, 0.3f, 0.3f, 0x8A7A58), P("Cache", B, 0f, 0.15f, 0f, 0.7f, 0.3f, 0.5f, 0xD6A24A)),
                new CellarVignette("sleeping_elite", "Sleeping elite", VignetteCategory.Danger, 8, 2, true, false, false,
                    P("Body", Ca, 0f, 0.35f, 0f, 0.6f, 1.8f, 0.6f, 0x8C2A22, 90f), P("Marker", Cy, 0f, 0.03f, 0f, 2.6f, 0.03f, 2.6f, 0xB84A2A),
                    P("Horn", Cy, 0.9f, 0.7f, 0f, 0.15f, 0.3f, 0.15f, 0xD8CFA8)),
                new CellarVignette("lantern_cache", "Lantern-oil cache", VignetteCategory.Reward, 7, 2, true, false, false,
                    P("Crate", B, 0f, 0.3f, 0f, 0.9f, 0.6f, 0.7f, 0x6A4A28), P("Oil1", Cy, 0.8f, 0.2f, 0.5f, 0.3f, 0.4f, 0.3f, 0xC8A040), P("Oil2", Cy, -0.8f, 0.2f, -0.4f, 0.3f, 0.4f, 0.3f, 0xC8A040),
                    P("Glint", Sp, 0f, 0.8f, 0f, 0.2f, 0.2f, 0.2f, 0xF2D58A)),

                // ---- procgen item 5: ten more greybox vignettes (premises are ours; one 4 m cell each; any combat room, WallHug ones need a wall cell) ----
                // A seep has pooled where the vault floor sagged; a leaking cask drips into it.
                new CellarVignette("flooded_alcove", "Flooded alcove", VignetteCategory.Fluff, 10, 1, true, false, false,
                    P("Puddle", Cy, 0f, 0.02f, 0f, 2.4f, 0.03f, 2.4f, 0x2F4650), P("Cask", Cy, 1.1f, 0.35f, -1.0f, 0.7f, 0.7f, 0.7f, 0x5A3E26), P("Drip", Sp, 1.1f, 0.05f, -0.4f, 0.12f, 0.12f, 0.12f, 0x6A8EA0)),
                // A burial-keeper's lectern still holds the ledger of names it was never allowed to finish.
                new CellarVignette("keepers_lectern", "Keeper's lectern", VignetteCategory.Fluff, 12, 1, true, false, true,
                    P("Stand", B, 0f, 0.6f, -1.3f, 0.5f, 1.2f, 0.5f, 0x4E3A28), P("Ledger", B, 0f, 1.25f, -1.3f, 0.7f, 0.08f, 0.5f, 0xC9B98E, 15f), P("Candle", Cy, 0.45f, 0.15f, -1.3f, 0.1f, 0.3f, 0.1f, 0xE8DFC0)),
                // A ring of pale caps glows where the cellar's roots came through; harmless, and pretty.
                new CellarVignette("pale_cap_ring", "Ring of pale caps", VignetteCategory.Fluff, 10, 1, true, false, false,
                    P("Cap1", Sp, 0.8f, 0.14f, 0f, 0.35f, 0.25f, 0.35f, 0xB9C8B0), P("Cap2", Sp, -0.8f, 0.12f, 0.2f, 0.3f, 0.22f, 0.3f, 0xB9C8B0), P("Cap3", Sp, 0.1f, 0.1f, 0.9f, 0.25f, 0.2f, 0.25f, 0xB9C8B0),
                    P("Cap4", Sp, -0.2f, 0.12f, -0.9f, 0.3f, 0.22f, 0.3f, 0xB9C8B0)),
                // A hand-cart lost a wheel on the way down and was left where it tipped.
                new CellarVignette("broken_handcart", "Broken handcart", VignetteCategory.Fluff, 12, 1, true, false, false,
                    P("Bed", B, 0f, 0.45f, 0f, 1.8f, 0.2f, 1.0f, 0x6A4A28, 10f), P("Wheel", Cy, 1.1f, 0.4f, 0.5f, 0.8f, 0.08f, 0.8f, 0x3A3028, 90f), P("Spill", Sp, -0.9f, 0.1f, -0.6f, 0.4f, 0.2f, 0.4f, 0x7C6A3E)),
                // A stack of crates hides a nest of cellar rats; one nudge brings them out.
                new CellarVignette("crate_stack_nest", "Crate stack with a nest", VignetteCategory.Danger, 14, 1, true, false, true,
                    P("CrateA", B, -0.5f, 0.35f, -1.2f, 0.9f, 0.7f, 0.9f, 0x6A4A28), P("CrateB", B, 0.5f, 0.35f, -1.2f, 0.9f, 0.7f, 0.9f, 0x5A3E26, 6f), P("CrateC", B, 0f, 1.05f, -1.2f, 0.9f, 0.7f, 0.9f, 0x6A4A28, -8f),
                    P("Nest", Sp, 0f, 0.1f, -0.4f, 0.5f, 0.2f, 0.5f, 0x4A4038)),
                // A cord across the passage runs to a bell; whoever trips it tells every sleeper on the floor.
                new CellarVignette("tripwire_bell", "Tripwire bell", VignetteCategory.Danger, 16, 2, true, false, false,
                    P("PostA", Cy, -1.5f, 0.4f, 0f, 0.12f, 0.8f, 0.12f, 0x4A4038), P("PostB", Cy, 1.5f, 0.4f, 0f, 0.12f, 0.8f, 0.12f, 0x4A4038), P("Cord", B, 0f, 0.2f, 0f, 3.0f, 0.02f, 0.02f, 0xA8987A),
                    P("Bell", Sp, 1.5f, 0.9f, 0f, 0.22f, 0.22f, 0.22f, 0xB89040)),
                // A pile of gnawed bones in a hollow; something large sleeps lightly nearby.
                new CellarVignette("bone_nest", "Nest of bones", VignetteCategory.Danger, 14, 3, true, false, false,
                    P("Hollow", Cy, 0f, 0.05f, 0f, 2.0f, 0.1f, 2.0f, 0x3A3532), P("BoneA", Ca, -0.4f, 0.2f, 0.2f, 0.12f, 0.7f, 0.12f, 0xD8CFA8, 70f), P("BoneB", Ca, 0.5f, 0.2f, -0.3f, 0.12f, 0.8f, 0.12f, 0xD8CFA8, 100f),
                    P("Skull", Sp, 0f, 0.25f, 0f, 0.3f, 0.3f, 0.3f, 0xE0D8B8)),
                // A cracked idol the old keepers feared: take the offering at its feet and it may not stay quiet about it.
                new CellarVignette("cracked_idol", "Cracked idol and offering", VignetteCategory.RiskReward, 16, 2, true, false, true,
                    P("Plinth", B, 0f, 0.3f, -1.3f, 0.8f, 0.6f, 0.8f, 0x6E6A60), P("Idol", Ca, 0f, 1.0f, -1.3f, 0.4f, 0.9f, 0.4f, 0x8A8470), P("Offering", B, 0f, 0.08f, -0.6f, 0.6f, 0.16f, 0.4f, 0xD6A24A)),
                // A larder hook line with cured joints, still good in the cold; the hooks are rusted shut around them.
                new CellarVignette("hanging_larder", "Hanging larder", VignetteCategory.Reward, 18, 2, true, false, true,
                    P("Rail", B, 0f, 1.7f, -1.7f, 2.4f, 0.08f, 0.08f, 0x3A3532), P("Joint1", Ca, -0.7f, 1.2f, -1.7f, 0.25f, 0.6f, 0.25f, 0x8C4A3A), P("Joint2", Ca, 0f, 1.2f, -1.7f, 0.25f, 0.6f, 0.25f, 0x8C4A3A),
                    P("Joint3", Ca, 0.7f, 1.2f, -1.7f, 0.25f, 0.6f, 0.25f, 0x8C4A3A)),
                // A mender's bench with a half-sewn kit: somebody meant to come back for it.
                new CellarVignette("menders_bench", "Mender's bench", VignetteCategory.Reward, 14, 2, true, false, true,
                    P("Bench", B, 0f, 0.45f, -1.4f, 1.6f, 0.1f, 0.6f, 0x6A4A28), P("Kit", B, -0.3f, 0.55f, -1.4f, 0.4f, 0.12f, 0.3f, 0x7C6A3E), P("Thread", Cy, 0.4f, 0.55f, -1.4f, 0.12f, 0.12f, 0.12f, 0xA83A3A)),
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
        public static VignetteFloor Select(uint seed, int floor) => Select(seed, floor, true);

        // conditioned=false skips the state-conditioned weights (CellarVignetteTuning) so a test can measure how far they shift the odds.
        public static VignetteFloor Select(uint seed, int floor, bool conditioned)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException(nameof(floor));
            IReadOnlyList<CellarVignette> pool = CellarVignettePool.All;
            int n = pool.Count;
            var misses = new int[n]; var seen = new bool[n]; int total = 0; VignetteFloor last = null;
            var catSeen = new int[4]; var prevFamilies = new HashSet<string>();   // dive state, all from earlier floors of this seed
            for (int f = 1; f <= floor; f++)
            {
                SpecialKind special = conditioned ? SpecialRoomDive.KindFor(seed, f, out _) : SpecialKind.None;
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
                    // probability = percent / (100 * odds) as a threshold on the 32-bit roll; percent 100 is 1/odds. A certain must-see ignores the percent.
                    int pct = conditioned && !certain[i] ? CellarVignetteTuning.Percent(v, f, special, catSeen, prevFamilies) : 100;
                    ulong threshold = ((1UL << 32) * (ulong)pct) / ((ulong)odds * 100UL);
                    if ((ulong)roll[i] < threshold) hit.Add(i);
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
                prevFamilies.Clear();
                foreach (CellarVignette c in chosen) { catSeen[(int)c.Category]++; prevFamilies.Add(CellarVignetteTuning.FamilyOf(c.Id)); }
                last = new VignetteFloor(f, chosen, forced);
            }
            return last;
        }
    }
}
