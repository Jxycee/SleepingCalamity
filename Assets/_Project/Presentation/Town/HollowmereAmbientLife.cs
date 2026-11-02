// Authority: none (local presentation, every peer animates its own copy; nothing here is networked or saved).
// The ONE tuning spot for Hollowmere's phase-1 ambient life (effects only, no animal models). Each effect has an on/off flag; HollowmereAmbientBuilder reads them
// when it builds the scene, so flipping a flag and running the Hollowmere build adds or drops that effect. The numbers below are the look knobs.
// Style: PHASE1 section 3 (muted daytime colour, subtle, nothing cartoon). Cost: no colliders, no per-frame allocations (AmbientCreatures pools its cards at build time).
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class HollowmereAmbientLife
    {
        // ---- on/off, one line per effect
        public static bool Birds = true;          // valley rooks (lift when the hero comes near), crows round the bell tower and the ruins, swallows over the square, gulls over the lake
        public static bool Butterflies = true;    // pale wings over the meadows, the garden and the square planters
        public static bool Bees = true;           // dark specks working the same patches
        public static bool FishRings = true;      // slow rings on the lake
        public static bool ReedSway = true;       // the lake and pool reeds lean with the gusts
        public static bool WindWaves = true;      // drifting cloud shadows plus gust bands crossing the grass and fields (a sun light cookie scrolled with the wind)
        public static bool ChimneySmoke = true;   // heroes' home in both presets, every rebuilt house in Rebuilt only

        // ---- wind (shared by reeds, smoke and the cookie)
        public static readonly Vector2 WindDirection = new Vector2(0.8f, 0.6f).normalized;   // towards +x/+z
        public const float WindSpeed = 4f;                 // m/s the cloud shadows and gust bands travel
        public const float CloudShadowStrength = 0.34f;    // 0..1 light removed under the darkest cloud
        public const float GustBandStrength = 0.07f;       // 0..1 light modulation of the fine bands that read as wind waves through the grass
        public const float CookieSize = 240f;              // metres per cookie tile

        // ---- birds
        public const float ScatterDistance = 15f;          // a perched flock lifts when the hero comes this close
        public const float AloftSeconds = 28f;             // how long a lifted flock wheels before it settles again
        public const float FlockCullDistance = 260f;       // a flock further than this from the camera is not animated

        public const float BirdScale = 2.3f;               // wingspan multiplier for every bird card (cards, no models: they must read at distance)
        public const float ButterflySpan = 0.34f;          // metres wing to wing (0.15 was a speck at 10 m eye height; Q town-surfaces round 1)
        public const float BeeSpan = 0.12f;
        public const int ButterfliesPerFlock = 9, BeesPerFlock = 7;   // town corners and garden lots (meadows add 2 butterflies)
        public const int PathButterflies = 7;              // the extra flock on the lane side of each garden lot, where the paths are

        // ---- smoke
        public const float SmokeRate = 3.2f;               // particles per second per chimney
        public const float SmokeLifetime = 9f;
        public const float SmokeAlpha = 0.45f;             // peak alpha of a puff
        public static readonly Color SmokeColour = new Color(0.68f, 0.66f, 0.62f);   // light warm grey

        // ---- fish rings
        public const float FishRingRate = 0.22f;           // rings per second per patch

        // ---- reeds
        public const float ReedLeanDegrees = 2.2f;
    }
}
