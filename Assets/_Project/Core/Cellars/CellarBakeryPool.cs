// Core/Cellars/CellarBakeryPool.cs: the rooms of the bakery set piece, the taken bakery's oven maze (PHASE1 7.2), now floor 3 of the dive. Authored as ASCII like the pool
// (legend in CellarRoom.cs) but kept OUT of CellarRoomPool.All, so the generated floors never draw them. Floor 3 is placed by UndercroftSetPieces (CellarFloorGenerator.SetPieces.cs):
// the pool's entry room, four oven-maze rooms joined in a ring (the loop), the calm antechamber (stair hall beside it) and the heart. Floor 5's guardian arena is the
// original antechamber (its Tier2 spawn is the stand-in guardian).
// Authority: none (static data).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class CellarBakeryPool
    {
        public const string MouthId = "bakery_oven_mouth", ProofingId = "bakery_proofing", FlourId = "bakery_flour_store", KneadingId = "bakery_kneading_hall",
            AntechamberId = "bakery_flue_antechamber", AntechamberCalmId = "bakery_flue_antechamber_calm", HeartId = "bakery_heart";

        public static readonly IReadOnlyList<CellarRoom> All = Build().AsReadOnly();

        public static CellarRoom Find(string id) { foreach (CellarRoom r in All) if (r.Id == id) return r; return null; }

        const SocketKind Lantern = SocketKind.Lantern, Shelf = SocketKind.Shelf, Rack = SocketKind.Rack, Crate = SocketKind.Crate, Barrel = SocketKind.Barrel;

        static SocketSpec Sk(SocketKind k, int x, int y, CellSide facing) =>
            new SocketSpec(k, x, y, facing, k == Lantern ? new[] { "Lit", "Dim", "Out" } : k == Shelf ? new[] { "Jars", "Hams", "Sacks" } : k == Rack ? new[] { "Bottles", "Casks", "Empty" } : k == Barrel ? new[] { "Full", "Stacked" } : new[] { "Stacked", "Open", "Broken" });

        static CellarRoom Oven(string id, string name, string sil, string[] rows, params SocketSpec[] socks) => CellarRoom.Parse(id, name, RoomKind.Combat, SubTheme.Bakery, sil, rows, socks);

        static List<CellarRoom> Build()
        {
            const CellSide N = CellSide.North, E = CellSide.East, S = CellSide.South, W = CellSide.West;
            return new List<CellarRoom>
            {
                // 3x4. West door from the entry corridor, east door to the proofing room, south door to the flour store.
                Oven(MouthId, "Oven mouth", "oven-mouth", new[] {
                    ".s.",
                    "#.D",
                    "D.s",
                    "CD." },
                    Sk(Lantern, 0, 0, S), Sk(Crate, 2, 3, N)),
                // 4x3. West door from the oven mouth, south door to the kneading hall.
                Oven(ProofingId, "Proofing room", "proofing-racks", new[] {
                    "C#.C",
                    "D.s.",
                    "s.D." },
                    Sk(Rack, 2, 0, S), Sk(Lantern, 3, 1, W)),
                // 3x3 ring round a flour-bin block. North door from the oven mouth, east door to the kneading hall.
                Oven(FlourId, "Flour store", "flour-ring", new[] {
                    "CD.",
                    ".#D",
                    "sCs" },
                    Sk(Barrel, 0, 1, N), Sk(Lantern, 2, 0, S)),
                // 4x3. North door from the proofing room, west door from the flour store, south door to the antechamber.
                Oven(KneadingId, "Kneading hall", "kneading-hall", new[] {
                    "..Ds",
                    "D.S.",
                    ".D.C" },
                    Sk(Shelf, 0, 0, S), Sk(Lantern, 3, 1, W)),
                // 3x3. North door from the kneading hall, west door to the heart. The Dough Warden (the Tier2 spawn) guards the only way in.
                Oven(AntechamberId, "Flue antechamber", "antechamber", new[] {
                    ".D.",
                    "DS.",
                    "s.C" },
                    Sk(Lantern, 2, 0, S), Sk(Crate, 2, 1, W)),
                // 3x3. The calm antechamber of floor 3 (plan 3 task 4): no guard, one Tier1 spawn, and an east door to the stair hall. North door from the kneading hall, west door to the hearth.
                Oven(AntechamberCalmId, "Flue antechamber (calm)", "antechamber", new[] {
                    ".D.",
                    "DsD",
                    "s.C" },
                    Sk(Lantern, 2, 0, S), Sk(Crate, 1, 2, N)),
                // 5x3. The baker's bed (Z) by the great oven (the solid blocks), the rescue bank point (B). One door, east.
                CellarRoom.Parse(HeartId, "Baker's hearth", RoomKind.Heart, SubTheme.Bakery, "hearth", new[] {
                    "#...#",
                    ".Z..D",
                    "..B.." },
                    Sk(Shelf, 2, 0, S), Sk(Lantern, 0, 2, E)),
            };
        }

        // Room rules for every bakery room, unique ids, and the set-piece's own rules (one heart, one antechamber with a Tier2 spawn, 4 oven rooms).
        public static List<string> Validate()
        {
            var errs = new List<string>();
            var ids = new HashSet<string>();
            foreach (CellarRoom r in All)
            {
                errs.AddRange(r.Validate());
                if (!ids.Add(r.Id)) errs.Add("bakery pool: room id '" + r.Id + "' is used twice");
                if (CellarRoomPool.Find(r.Id) != null) errs.Add("bakery pool: room id '" + r.Id + "' is also in the cellar pool");
            }
            CellarRoom ante = Find(AntechamberId);
            if (ante == null || !System.Linq.Enumerable.Any(ante.Spawns, s => s.Tier == SpawnTier.Tier2)) errs.Add("bakery pool: the antechamber needs a Tier2 spawn for the Dough Warden");
            CellarRoom calm = Find(AntechamberCalmId);
            if (calm == null || System.Linq.Enumerable.Any(calm.Spawns, s => s.Tier == SpawnTier.Tier2)) errs.Add("bakery pool: the calm antechamber must exist and have no Tier2 spawn");
            if (Find(HeartId) == null) errs.Add("bakery pool: no heart room");
            return errs;
        }
    }
}
