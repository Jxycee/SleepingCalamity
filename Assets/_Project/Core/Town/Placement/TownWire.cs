// Core/Town/Placement/TownWire.cs: the town as flat wire data for replication (M6 S4 TownSync). A TownState is turned into plain arrays (stock pieces, stock residents, each placed
// building with its foundation, turn, one piece id per part and its resident) and back, so the host can publish the whole town in ONE value and a late joiner or a client
// rebuilds the same TownState without replaying any request. Pure C#, no engine or netcode references (the INetworkSerializable wrapper lives in Presentation/Town/TownSync).
// Authority: none (data). The host builds it from its TownState; peers only read it. ToState is defensive: an entry the rules refuse is dropped, never thrown.
using System;
using System.Collections.Generic;
using System.Text;

namespace SleepingCalamity.Core
{
    public sealed class TownWire
    {
        public sealed class Building
        {
            public string BuildingId = "", FoundationId = "", Resident = "";   // Resident "" = nobody moved in
            public int Turn;
            public int[] Parts = new int[0];                                    // per BuildingDef.Parts index, -1 for an empty slot
        }

        public int[] StockPieces = new int[0];
        public string[] StockResidents = new string[0];
        public Building[] Buildings = new Building[0];

        public static TownWire From(TownState state)
        {
            var w = new TownWire();
            if (state == null) return w;
            var pieces = new List<int>(state.StockPieces);
            w.StockPieces = pieces.ToArray();
            w.StockResidents = new List<string>(state.StockResidents).ToArray();
            var list = new List<Building>();
            foreach (PlacedBuilding p in state.Placed)
                list.Add(new Building { BuildingId = p.BuildingId, FoundationId = p.FoundationId, Turn = p.Turn, Parts = (int[])p.PiecesByPart.Clone(), Resident = p.Resident ?? "" });
            w.Buildings = list.ToArray();
            return w;
        }

        // A TownState over `layout` holding exactly this data (entries the town rules refuse are skipped).
        public TownState ToState(TownLayout layout)
        {
            var s = new TownState(layout);
            foreach (Building b in Buildings ?? new Building[0])
                s.RestoreBuilding(b.BuildingId ?? "", b.FoundationId ?? "", b.Turn, b.Parts ?? new int[0], !string.IsNullOrEmpty(b.Resident));
            foreach (int p in StockPieces ?? new int[0]) s.AddPiece(p);
            foreach (string r in StockResidents ?? new string[0]) s.AddResident(r ?? "");
            return s;
        }

        // A stable text form: equal towns give equal strings (the state keeps its sets sorted), so the host can tell "changed" with one compare.
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append('S');
            foreach (int p in StockPieces ?? new int[0]) sb.Append(p).Append(',');
            sb.Append('R');
            foreach (string r in StockResidents ?? new string[0]) sb.Append(r).Append(',');
            foreach (Building b in Buildings ?? new Building[0])
            {
                sb.Append('B').Append(b.BuildingId).Append('@').Append(b.FoundationId).Append('/').Append(b.Turn).Append('/');
                foreach (int p in b.Parts ?? new int[0]) sb.Append(p).Append('.');
                sb.Append('/').Append(b.Resident);
            }
            return sb.ToString();
        }
    }
}
