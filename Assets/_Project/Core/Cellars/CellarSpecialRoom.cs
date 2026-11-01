// Core/Cellars/CellarSpecialRoom.cs: the one capped special room of a Cellars floor (procgen item 1c, research procedural-dungeons takeaways): a tag on an existing combat room,
// "one of N kinds, at most once per dive per kind, may be none". Greybox only: the tag names a kind; what each kind does (an elite den's extra elite, a treasury's chests...) is later work.
// Which kind a floor gets is a dive-wide rule replayed from the seed over floors 1..N (like VignetteDive.Select), on its own stream Rng.Derive(seed, "cellars.special", f):
//   - per floor three draws are always made (chance roll, kind pick, room pick), so the stream position never depends on the state;
//   - the floor gets a special room when the roll is under the floor's percent; the kind is picked from the kinds not yet used in the dive and allowed on that floor. A kind drawn
//     on a floor whose layout has no eligible room is spent anyway (so the kind sequence needs no earlier layout and floor N's answer stays a pure function of (seed, N));
//   - the room is a combat room (never entry, exit or checkpoint), not the locked leaf, not the room that holds the sleeping-elite vignette; Arc and Branch rooms are
//     preferred over Spine rooms (CellarFloorRoute), and only when none is eligible does a Spine room take it.
// Floor 4 (the fixed bakery) has none. All placeholder numbers live in the Tuning block below. SpecialHash() is the cross-peer proof. Authority: none (every peer derives the same).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum SpecialKind : byte { None = 0, EliteDen = 1, Treasury = 2, Shrine = 3, Quiet = 4 }

    public sealed class SpecialRoom
    {
        public readonly int Floor; public readonly SpecialKind Kind; public readonly int RoomIndex;   // RoomIndex -1 when Kind is None
        public SpecialRoom(int floor, SpecialKind kind, int roomIndex) { Floor = floor; Kind = kind; RoomIndex = roomIndex; }
        public bool Exists => Kind != SpecialKind.None;

        public ulong SpecialHash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE(Floor); h.AppendInt32LE((int)Kind); h.AppendInt32LE(RoomIndex);
            return h.Value;
        }
    }

    public static class SpecialRoomDive
    {
        // ---- Tuning (PLACEHOLDERS: first guesses to be tuned by the user; none of them is tested for feel) ----
        // Percent chance per floor (index = floor, floors 1-3) that the floor gets a special room.
        public static readonly int[] FloorPercent = { 0, 40, 55, 55 };
        // The kinds in draw order and the first floor each may appear on. Add a kind by appending (changes picks, not the hashes of other layers).
        public static readonly (SpecialKind Kind, int MinFloor)[] Kinds =
        {
            (SpecialKind.EliteDen, 2), (SpecialKind.Treasury, 1), (SpecialKind.Shrine, 1), (SpecialKind.Quiet, 1),
        };

        // The kind floor `floor` of the dive `seed` gets (None when the roll misses or no kind is left), replaying floors 1..floor. Pure.
        public static SpecialKind KindFor(uint seed, int floor, out uint roomDraw)
        {
            if (floor < 1) throw new ArgumentOutOfRangeException(nameof(floor));
            var used = new bool[Kinds.Length]; SpecialKind result = SpecialKind.None; roomDraw = 0;
            for (int f = 1; f <= floor; f++)
            {
                var rng = new Pcg32(Rng.Derive(seed, "cellars.special", (ulong)f));
                int roll = rng.Range(0, 100); uint kindDraw = rng.NextUInt(); uint room = rng.NextUInt();
                result = SpecialKind.None; roomDraw = room;
                if (f > 3 || roll >= FloorPercent[f]) continue;
                var open = new List<int>(); for (int i = 0; i < Kinds.Length; i++) if (!used[i] && f >= Kinds[i].MinFloor) open.Add(i);
                if (open.Count == 0) continue;
                int pick = open[(int)(kindDraw % (uint)open.Count)]; used[pick] = true; result = Kinds[pick].Kind;
            }
            return result;
        }

        public static SpecialRoom Resolve(CellarFloorLayout layout, CellarFloorRoute route = null, CellarFloorDressing dressing = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var none = new SpecialRoom(layout.Floor, SpecialKind.None, -1);
            if (layout.IsStub) return none;
            SpecialKind kind = KindFor(layout.Seed, layout.Floor, out uint roomDraw);
            if (kind == SpecialKind.None) return none;
            route = route ?? CellarFloorRoute.Resolve(layout);
            int eliteRoom = -1;
            if (VignetteDive.Select(layout.Seed, layout.Floor).Chosen.Any(v => v.Id == "sleeping_elite"))
            {
                dressing = dressing ?? CellarFloorDressing.Resolve(layout);
                foreach (PlacedVignette pv in dressing.Vignettes) if (pv.Vignette.Id == "sleeping_elite") eliteRoom = pv.RoomIndex;
            }
            var off = new List<int>(); var spine = new List<int>();
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                if (layout.Rooms[r].Room.Kind != RoomKind.Combat || r == layout.LockedRoomIndex || r == eliteRoom) continue;
                if (route.RoleOf(r) == RoomRole.Spine) spine.Add(r); else off.Add(r);
            }
            List<int> pool = off.Count > 0 ? off : spine;
            return pool.Count == 0 ? none : new SpecialRoom(layout.Floor, kind, pool[(int)(roomDraw % (uint)pool.Count)]);
        }
    }
}
