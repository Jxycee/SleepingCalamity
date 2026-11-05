// Core/Cellars/CellarFloorGenerator.SetPieces.cs: the hand-placed Undercroft floors 1, 3 and 5 (plan 3 task 4; spec section 5). Pure C#, no engine references.
// CellarFloorGenerator is partial so these builders can use its private nested Builder (PlaceFixed / LinkFixed / Assemble). They draw nothing from the dive seed:
// every seed gives the same floor, the seed only rides on the layout. Authority: none (static; every peer builds the same floor).
//   Floor 1 (authored): entry -> pantry (teaching room) -> hanging hall (gate-key carrier) -> stair hall. Larder, Spine, no water, three dead-end chests.
//   Floor 3 (stand-in): the taken bakery's oven maze (CellarBakeryPool): the ring of four oven rooms, the CALM antechamber (no guardian), the baker's hearth behind it and a stair hall.
//   Floor 5 (stand-in): the guardian arena (the Dough Warden stands in for the Brazen Ox), then the bank room (the lamp nook) behind it. No stair: the bank room is the way on.
// Floor 7's EndCell is added by CellarFloorLayout itself (it is generated, not placed here).
using System;

namespace SleepingCalamity.Core
{
    // The public face of the set-piece builders: UndercroftSetPieces.Build(seed, floor) for floors 1, 3, 5. CellarFloorGenerator.Layout is the entry point the runtime calls.
    public static class UndercroftSetPieces
    {
        public const int AuthoredFloor = 1, BakeryFloor = 3, GuardianFloor = 5;

        public static CellarFloorLayout Build(uint seed, int floor)
        {
            switch (floor)
            {
                case AuthoredFloor: return CellarFloorGenerator.BuildAuthoredFloor(seed);
                case BakeryFloor: return CellarFloorGenerator.BuildBakeryFloor(seed);
                case GuardianFloor: return CellarFloorGenerator.BuildGuardianFloor(seed);
                default: throw new ArgumentOutOfRangeException(nameof(floor), "the set pieces are floors 1, 3 and 5");
            }
        }
    }

    public static partial class CellarFloorGenerator
    {
        const CellSide N = CellSide.North, E = CellSide.East, S = CellSide.South, W = CellSide.West;

        // Floor 1: four rooms in a chain, spread over the grid so the walk from the party start to the stair is long. The fixed rng only picks the three dead ends.
        internal static CellarFloorLayout BuildAuthoredFloor(uint seed)
        {
            var b = new Builder(new Pcg32(1111));
            b.Reset();
            int entry = b.PlaceFixed(Special(RoomKind.Entry), 0, 0, 0);
            int pantry = b.PlaceFixed(CellarRoomPool.Find("larder_pantry"), 0, 6, 0);
            int hall = b.PlaceFixed(CellarRoomPool.Find("larder_hanging_hall"), 0, 10, 6);
            int exit = b.PlaceFixed(Special(RoomKind.StairExit), 2, 3, 8);
            b.LinkFixed(entry, E, pantry, W);
            b.LinkFixed(pantry, E, hall, N);
            b.LinkFixed(hall, W, exit, E);
            const int deadEnds = 3;   // 3 room chests (pantry 1, hall 2) + 3 dead-end chests = the 6 a floor needs
            for (int i = 0; i < deadEnds; i++) if (b.AddDeadEnd() == null) throw new InvalidOperationException("floor 1: no room for a dead end");
            return b.Assemble(seed, UndercroftSetPieces.AuthoredFloor, false, SubTheme.Larder, b.CarrierIn(hall), -1, deadEnds, LayoutPreset.Spine);
        }

        // Floor 3: the oven maze. The ring (mouth, proofing, flour, kneading) is the loop; the calm antechamber is the hearth's only approach and also holds the way to the stair hall.
        internal static CellarFloorLayout BuildBakeryFloor(uint seed)
        {
            var b = new Builder(new Pcg32(3333));
            b.Reset();
            int entry = b.PlaceFixed(Special(RoomKind.Entry), 0, 0, 0);
            int mouth = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.MouthId), 0, 5, 0);
            int proof = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.ProofingId), 0, 9, 0);
            int flour = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.FlourId), 0, 5, 5);
            int knead = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.KneadingId), 0, 9, 5);
            int ante = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.AntechamberCalmId), 0, 6, 9);
            int heart = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.HeartId), 0, 0, 9);
            int exit = b.PlaceFixed(Special(RoomKind.StairExit), 0, 10, 10);
            b.LinkFixed(entry, E, mouth, W);
            b.LinkFixed(mouth, E, proof, W);
            b.LinkFixed(mouth, S, flour, N);
            b.LinkFixed(proof, S, knead, N);
            b.LinkFixed(flour, E, knead, W);
            b.LinkFixed(knead, S, ante, N);
            b.LinkFixed(ante, W, heart, E);
            b.LinkFixed(ante, E, exit, W);
            return b.Assemble(seed, UndercroftSetPieces.BakeryFloor, true, SubTheme.Bakery, null, preset: null);
        }

        // Floor 5: entry, then the arena (the original antechamber; its Tier2 spawn is the guardian), then the bank room. The bank room's only link is the arena.
        internal static CellarFloorLayout BuildGuardianFloor(uint seed)
        {
            var b = new Builder(new Pcg32(5555));
            b.Reset();
            int entry = b.PlaceFixed(Special(RoomKind.Entry), 0, 0, 0);
            int arena = b.PlaceFixed(CellarBakeryPool.Find(CellarBakeryPool.AntechamberId), 0, 6, 6);
            int bank = b.PlaceFixed(Special(RoomKind.Checkpoint), 0, 6, 0);
            b.LinkFixed(entry, E, arena, W);
            b.LinkFixed(arena, N, bank, S);
            return b.Assemble(seed, UndercroftSetPieces.GuardianFloor, true, SubTheme.Bakery, null, preset: null);
        }
    }
}
