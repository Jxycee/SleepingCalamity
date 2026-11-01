// Core/Cellars/CellarAtmosphere.cs: the per-floor atmosphere roll of the Undercroft Cellars (variety layer 3, research procedural-dungeons 3.3). Pure data: one roll
// per (seed, floor) from its own stream ("cellars.atmosphere"), so two visits to the same sub-theme differ in how lit, foggy and dusty they feel without moving a
// single wall. Everything is integer percentages (no float in a decision); the Presentation builder turns them into lamp counts, fog density and tint.
// Dark floor: about 1 in 12, never floor 1, never twice in a dive (floor N is dark only if no earlier floor of the dive was; the earlier rolls are replayed from
// the seed, each on its own stream, so the answer is still a pure function of (seed, floor)). Fog tint is warm only: red and green rise, blue never does.
// Odd light: one combat room of the floor gets a tinted lamp patch (amber, ember or candle; all warm, no teal).
// Authority: none (static data derived from the dive seed; every peer rolls the same atmosphere).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum OddLightKind : byte { None = 0, Amber = 1, Ember = 2, Candle = 3 }

    public sealed class CellarAtmosphere
    {
        public const int DarkFloorOneIn = 12, FirstDarkFloor = 2;
        public const int LampMin = 75, LampMax = 110, DarkLamp = 30;       // percent of the base lamp count and intensity
        public const int FogMin = 85, FogMax = 135, DarkFog = 170;          // percent of the scene's base fog density
        public const int DustMax = 100, DustVisible = 40;                   // DustPercent at or above DustVisible turns the dust motes on

        public readonly int Floor;
        public readonly bool Dark;
        public readonly int LampPercent;      // scales the lantern sockets kept (CellarFloorDressing) and every lamp's intensity
        public readonly int FogPercent;       // scales the base fog density
        public readonly int FogWarmPercent;   // 0..100: how far the fog colour is pushed toward warm (see FogTintOffset)
        public readonly int DustPercent;      // 0..100: how much dust hangs in the air; the builder shows motes from DustVisible up
        public readonly OddLightKind OddLight;
        public readonly int OddLightRoom;     // room index of the lit patch, -1 when the floor has no combat room

        CellarAtmosphere(int floor, bool dark, int lamp, int fog, int warm, int dust, OddLightKind odd, int oddRoom)
        { Floor = floor; Dark = dark; LampPercent = lamp; FogPercent = fog; FogWarmPercent = warm; DustPercent = dust; OddLight = odd; OddLightRoom = oddRoom; }

        public bool DustVisibleHere => DustPercent >= DustVisible;

        // The warm shift added to the fog colour (red a little more than green, blue untouched, so it only ever warms).
        public void FogTintOffset(out float r, out float g, out float b) { float k = FogWarmPercent / 100f; r = 0.06f * k; g = 0.02f * k; b = 0f; }

        // The odd-light patch's colour (warm only), as 0xRRGGBB; 0 when there is none.
        public int OddLightRgb
        {
            get
            {
                switch (OddLight)
                {
                    case OddLightKind.Amber: return 0xE8A63C;
                    case OddLightKind.Ember: return 0xD2562A;
                    case OddLightKind.Candle: return 0xF2D58A;
                    default: return 0;
                }
            }
        }

        static bool RollDark(uint seed, int floor) => new Pcg32(Rng.Derive(seed, "cellars.dark", (ulong)floor)).Range(0, DarkFloorOneIn) == 0;

        // True for the one floor of a dive (at most) that is dark. A pure function of (seed, floor): earlier floors are replayed.
        public static bool IsDarkFloor(uint seed, int floor)
        {
            if (floor < FirstDarkFloor) return false;
            for (int f = FirstDarkFloor; f < floor; f++) if (RollDark(seed, f)) return false;
            return RollDark(seed, floor);
        }

        // Rolls the floor's atmosphere. The layout supplies the rooms the odd light may sit in (combat rooms); the numbers depend only on (seed, floor).
        public static CellarAtmosphere Roll(CellarFloorLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            uint seed = layout.Seed; int floor = layout.Floor;
            var rng = new Pcg32(Rng.Derive(seed, "cellars.atmosphere", (ulong)floor));
            bool dark = IsDarkFloor(seed, floor);
            // every draw is made whatever the branch, so the stream position never depends on the dark roll
            int lamp = rng.Range(LampMin, LampMax + 1), fog = rng.Range(FogMin, FogMax + 1), warm = rng.Range(0, 101), dust = rng.Range(0, DustMax + 1);
            int oddKind = rng.Range(1, 4), oddPick = rng.Range(0, 1 << 20);
            if (dark) { lamp = DarkLamp; fog = DarkFog; dust = Math.Max(dust, 60); }
            var combat = new List<int>();
            for (int i = 0; i < layout.Rooms.Count; i++) if (layout.Rooms[i].Room.Kind == RoomKind.Combat) combat.Add(i);
            int room = combat.Count == 0 ? -1 : combat[oddPick % combat.Count];
            return new CellarAtmosphere(floor, dark, lamp, fog, warm, dust, room < 0 ? OddLightKind.None : (OddLightKind)oddKind, room);
        }

        public void AppendTo(ref Fnv1a64 h)
        {
            h.AppendInt32LE(Dark ? 1 : 0); h.AppendInt32LE(LampPercent); h.AppendInt32LE(FogPercent); h.AppendInt32LE(FogWarmPercent);
            h.AppendInt32LE(DustPercent); h.AppendInt32LE((int)OddLight); h.AppendInt32LE(OddLightRoom);
        }
    }
}
