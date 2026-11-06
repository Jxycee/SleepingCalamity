// Core/Diagnostics/DiveSummaryReport.cs: the end-of-dive numbers as paste-ready text and JSON for the playtest notes (docs/playtest/NOTES.md). Pure C#, no engine references.
// Authority: none (formats numbers a local observer already holds; writes nothing itself).
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SleepingCalamity.Core
{
    public sealed class DiveSummaryReport
    {
        public struct PlayerRow
        {
            public string Name;
            public int DamageTaken, HealsUsed, LoavesEaten, LoavesLeft;
        }

        public double DiveSeconds;
        public int FinalFloor, FailFloor, PartySize;
        public List<KeyValuePair<int, double>> FloorSeconds = new List<KeyValuePair<int, double>>();
        public List<PlayerRow> Players = new List<PlayerRow>();

        public int TotalDamage { get { int t = 0; foreach (var p in Players) t += p.DamageTaken; return t; } }
        public int TotalHeals { get { int t = 0; foreach (var p in Players) t += p.HealsUsed; return t; } }
        public int TotalLoaves { get { int t = 0; foreach (var p in Players) t += p.LoavesEaten; return t; } }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("Dive time ").Append(DiveStatsModel.FormatTime(DiveSeconds)).Append(", final floor ").Append(FinalFloor)
              .Append(FailFloor > 0 ? ", failed on floor " + FailFloor : ", not failed").Append(", players ").Append(PartySize).Append('\n');
            sb.Append("Per floor:");
            if (FloorSeconds.Count == 0) sb.Append(" none");
            foreach (var f in FloorSeconds) sb.Append(" F").Append(f.Key).Append(' ').Append(DiveStatsModel.FormatTime(f.Value));
            sb.Append('\n');
            foreach (var p in Players)
                sb.Append(p.Name).Append(": damage ").Append(p.DamageTaken).Append(", heals ").Append(p.HealsUsed).Append(", loaves eaten ").Append(p.LoavesEaten).Append(", loaves left ").Append(p.LoavesLeft).Append('\n');
            if (Players.Count > 1) sb.Append("Party: damage ").Append(TotalDamage).Append(", heals ").Append(TotalHeals).Append(", loaves eaten ").Append(TotalLoaves).Append('\n');
            return sb.ToString();
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\"diveSeconds\":").Append(Num(DiveSeconds)).Append(",\"finalFloor\":").Append(FinalFloor).Append(",\"failFloor\":").Append(FailFloor)
              .Append(",\"partySize\":").Append(PartySize).Append(",\"floorSeconds\":{");
            for (int i = 0; i < FloorSeconds.Count; i++)
                sb.Append(i > 0 ? "," : "").Append('"').Append(FloorSeconds[i].Key).Append("\":").Append(Num(FloorSeconds[i].Value));
            sb.Append("},\"players\":[");
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerRow p = Players[i];
                sb.Append(i > 0 ? "," : "").Append("{\"name\":\"").Append(Esc(p.Name)).Append("\",\"damageTaken\":").Append(p.DamageTaken).Append(",\"healsUsed\":").Append(p.HealsUsed)
                  .Append(",\"loavesEaten\":").Append(p.LoavesEaten).Append(",\"loavesLeft\":").Append(p.LoavesLeft).Append('}');
            }
            sb.Append("],\"party\":{\"damageTaken\":").Append(TotalDamage).Append(",\"healsUsed\":").Append(TotalHeals).Append(",\"loavesEaten\":").Append(TotalLoaves).Append("}}");
            return sb.ToString();
        }

        private static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        private static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
