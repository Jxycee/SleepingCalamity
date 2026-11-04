// Core/Rooms/CellarRoomsV2.cs: the Cellars pool (21 rooms) and the bakery set piece (6 rooms) as room data v2, plus the list of combat rooms that are small on purpose (rooms-v2 plan, Task 1; spec 11.5 F7).
// The floor generator still reads v1; this set wraps the very same CellarRoom objects (RoomDataV2.FromV1), so there is one source of geometry. Plan 3 reads these. Authority: none (static data).
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    // A combat room that does not hold 4 heroes + 8 enemies, with the party size it does hold (with its own authored spawns) and why it stays small.
    public sealed class SmallPartyRoom
    {
        public readonly string Id; public readonly int MaxHeroes; public readonly string Reason;
        public SmallPartyRoom(string id, int maxHeroes, string reason) { Id = id; MaxHeroes = maxHeroes; Reason = reason; }
    }

    public static class CellarRoomsV2
    {
        const string BakeryReason = "set piece with a lighter guard; reused only as plan 3's placeholder floor 3";

        static IReadOnlyList<RoomDataV2> pool, bakery, all;
        static IReadOnlyList<SmallPartyRoom> small;

        public static IReadOnlyList<RoomDataV2> Pool => pool ?? (pool = CellarRoomPool.All.Select(RoomDataV2.FromV1).ToList().AsReadOnly());
        public static IReadOnlyList<RoomDataV2> Bakery => bakery ?? (bakery = CellarBakeryPool.All.Select(RoomDataV2.FromV1).ToList().AsReadOnly());
        public static IReadOnlyList<RoomDataV2> All => all ?? (all = Pool.Concat(Bakery).ToList().AsReadOnly());

        public static IReadOnlyList<SmallPartyRoom> SmallParty => small ?? (small = new List<SmallPartyRoom>
        {
            new SmallPartyRoom("coal_stoke_chicane", 2, "the zigzag is the room; a third column erases it"),
            new SmallPartyRoom(CellarBakeryPool.MouthId, 4, BakeryReason),
            new SmallPartyRoom(CellarBakeryPool.ProofingId, 4, BakeryReason),
            new SmallPartyRoom(CellarBakeryPool.FlourId, 4, BakeryReason),
            new SmallPartyRoom(CellarBakeryPool.KneadingId, 4, BakeryReason),
            new SmallPartyRoom(CellarBakeryPool.AntechamberId, 4, BakeryReason),
        }.AsReadOnly());

        public static RoomDataV2 Find(string id) { foreach (RoomDataV2 r in All) if (r.Base.Id == id) return r; return null; }
    }
}
