// Core/Cellars/CellarWater.cs: the policy for standing water in the Cellars (dive 5b). Pure data and functions; authority: none. Dungeon 1 (the Cellars) keeps water OFF in
// Phase 1 (DC7, docs/design/dungeon1-cellars.md: no standing water), so Enabled is false on every floor and the Brimming hold is a no-op there. The water room variants
// (CellarWaterRooms) are built and validated anyway. A later dungeon that turns water on must add a host-authoritative room-water sync (sc-netcode): the host owns each
// room's sump state, clients only mirror it.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarWater
    {
        public const bool EnabledInPhase1 = false;   // DC7 (user 2026-11-13): no standing water in Phase 1
        public const float BrimmingHoldSeconds = 90f;
        public const int BubblingFirstFloor = 4, BubblingLastFloor = 5;

        // Water rooms exist on the first-half floors (1-5) once enabled.
        public static bool Enabled(int floor) => EnabledInPhase1 && UndercroftPlan.IsFirstHalf(floor);

        // Floors 4-5 show bubbling water (a presentation cue). Independent of Enabled: it only says where bubbling would show.
        public static bool Bubbling(int floor) => floor >= BubblingFirstFloor && floor <= BubblingLastFloor;

        // The Brimming modifier holds a room's water up for this long; 0 while water is off.
        public static float BrimmingHold(int floor, bool brimming) => Enabled(floor) && brimming ? BrimmingHoldSeconds : 0f;

        // One state index per region of the room, on the stream "cellars.water" (no other stream moves). Every region draws once per call so a room change never shifts a later draw.
        public static int[] StartState(int seed, int floor, int roomIndex, RoomDataV2 room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var rng = new Pcg32(Rng.Derive(unchecked((uint)seed), "cellars.water", (ulong)(floor * 1000 + roomIndex)));
            var state = new int[room.Regions.Count];
            for (int i = 0; i < state.Length; i++) state[i] = rng.Range(0, Math.Max(1, room.Regions[i].StateCount));
            return state;
        }
    }
}
