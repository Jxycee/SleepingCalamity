// Core/Rooms/CellarWaterRooms.cs: water-authored room data v2 variants of the pool's combat rooms (dive 5b). Each variant wraps the SAME CellarRoom object the pool holds
// (same id, size, doors, spawns, chests, sockets) and adds one sump region 'w' of 2-4 floor cells that never hold a door, spawn or chest, plus (where the room has a free
// wall-backed cell) a Control socket with no tool needed. States are surface heights in cm: 0 drained, 40 wade (standable, below WadeMaxCm 50), 80 flooded (not standable).
// Every variant passes RoomValidatorV2 (rules 1-9). coal_stoke_chicane has no variant: every free cell of that one-wide path is a chokepoint, so no sump can be flooded
// without cutting a door off from the chest (see Unsuitable). Phase 1 keeps water switched off (CellarWater.EnabledInPhase1); this data is built and validated ahead of it.
// Authority: none (static data).
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public static class CellarWaterRooms
    {
        public const char SumpLetter = 'w';
        public const string SumpId = "sump";

        // Pool combat rooms that cannot carry a sump (checked by CellarWaterRoomsTests: the 11 variants plus these cover all 12).
        public static readonly IReadOnlyList<string> Unsuitable = new[] { "coal_stoke_chicane" };

        static IReadOnlyList<RoomDataV2> all;
        public static IReadOnlyList<RoomDataV2> All => all ?? (all = Build());

        public static RoomDataV2 Find(string id) => All.FirstOrDefault(r => r.Base.Id == id);

        static RoomDataV2 Water(string id, int[] states, (int x, int y)[] sump, (int x, int y, CellSide facing)? valve)
        {
            CellarRoom room = CellarRoomPool.Find(id);
            var rows = new string[room.Height];
            for (int y = 0; y < room.Height; y++)
            {
                var chars = new char[room.Width];
                for (int x = 0; x < room.Width; x++) chars[x] = sump.Any(c => c.x == x && c.y == y) ? SumpLetter : '.';
                rows[y] = new string(chars);
            }
            var sockets = valve.HasValue
                ? new[] { new ToolSocket(id + ".valve", valve.Value.x, valve.Value.y, valve.Value.facing, SocketPurpose.Control, ToolVerb.None, SumpId) }
                : null;
            return RoomDataV2.Author(room, null, rows, new[] { new GimmickRegion(SumpId, SumpLetter, GimmickKind.Water, states) }, null, sockets, null, RoomAffordance.None);
        }

        static IReadOnlyList<RoomDataV2> Build()
        {
            const CellSide N = CellSide.North, E = CellSide.East, W = CellSide.West;
            int[] drain = { 0, 80 }, wade = { 0, 40, 80 };
            return new List<RoomDataV2>
            {
                Water("larder_pantry", wade, new[] { (1, 3), (1, 4) }, (0, 3, E)),
                Water("larder_cold_store", drain, new[] { (1, 1), (1, 2) }, (0, 3, E)),
                Water("larder_hanging_hall", wade, new[] { (2, 1), (1, 3) }, (3, 2, W)),
                Water("larder_smoke_gallery", drain, new[] { (3, 1), (1, 0) }, null),
                Water("wine_cask_row", drain, new[] { (3, 0), (4, 0) }, (4, 1, W)),
                Water("wine_vault_l", drain, new[] { (1, 1), (1, 2) }, (2, 0, CellSide.South)),
                Water("wine_press_hall", drain, new[] { (1, 0), (3, 0) }, null),
                Water("wine_aisles", wade, new[] { (0, 0), (0, 1) }, (1, 4, N)),
                Water("coal_bunker", wade, new[] { (1, 3), (1, 4) }, (0, 4, E)),
                Water("coal_furnace_ring", drain, new[] { (2, 2), (3, 2) }, null),
                Water("coal_boiler_hall", drain, new[] { (2, 1), (1, 2) }, null),
            }.AsReadOnly();
        }
    }
}
