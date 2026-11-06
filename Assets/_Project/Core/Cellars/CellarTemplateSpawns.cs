// Core/Cellars/CellarTemplateSpawns.cs: turns one live Cellars combat room into the spawn list the host places, from EncounterBuilder templates (spawner wiring, plan 2026-11-06). Pure C#, no engine references.
// A pure function of (layout, dressing, plan, roomIndex, players, ring cells): the host calls it when a player first enters the room (or the ambush springs); nothing here is networked.
// Authority: none (static data); CellarContentsSync, which runs on the host only, consumes it.
//   - The plan stays the budget. CellarEncounterPlan.CountIn(room) already folds in the authored spawns, the stir/co-op extras and the floor twist (Thin -1, Crowded +1), so the result
//     holds EXACTLY that many enemies: surplus template spawns are dropped (last wave first), a shortfall is padded with roster-banded wave-0 enemies. The template picks kinds, cells and
//     waves, never the difficulty. (The template's own solo cap, EncounterBuilder.IsLegal, compares Base.Spawns.Count, the authored count, while the live plan count varies by twist
//     and extras; trimming to the live count keeps the room no harder than today's plan.) Validator caps hold: at most GameMetrics.MaxLiveEnemies per wave, FightSpace.Fits at the party size.
//   - Cells: the template resolves cells on the placed (rotated) room; each is checked against the plan's world-legal cells (its own spawn cells plus CellarEncounterPlan.FreeCellsOf, which
//     excludes dressing, chunk solids, chests, vignettes and so on) and replaced by the nearest unused legal cell if it is not. Distinct within a wave; wave 1 may reuse wave-0 cells.
//   - The gate-key carrier keeps its authored cell (the first wave-0 spawn). Rooms with a Tier 2 spawn or the warden cell, and rooms with no table entry (the bakery set pieces), return null:
//     the caller keeps the old plan-driven spawns.
//   - Ambush: when plan.AmbushRoom == roomIndex the Ambush template is used, and ring cells (CellarAmbush) replace the wave-0 cells in order, skipping the carrier's.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public sealed class TemplateSpawn
    {
        public readonly CellPos Cell; public readonly EnemyKind Kind; public readonly int Wave; public readonly bool Carrier;
        public TemplateSpawn(CellPos cell, EnemyKind kind, int wave, bool carrier) { Cell = cell; Kind = kind; Wave = wave; Carrier = carrier; }
    }

    public sealed class CellarTemplateRoom
    {
        public readonly string TemplateId; public readonly IReadOnlyList<TemplateSpawn> Spawns;
        public CellarTemplateRoom(string templateId, IEnumerable<TemplateSpawn> spawns) { TemplateId = templateId; Spawns = spawns.ToList(); }
        public IEnumerable<TemplateSpawn> InWave(int wave) => Spawns.Where(s => s.Wave == wave);
        public int WaveCount => Spawns.Count == 0 ? 0 : Spawns.Max(s => s.Wave) + 1;
    }

    public static class CellarTemplateSpawns
    {
        // null = keep the old plan-driven spawns for this room.
        public static CellarTemplateRoom Resolve(CellarFloorLayout layout, CellarFloorDressing dressing, CellarEncounterPlan plan, int roomIndex, int players, IReadOnlyList<CellPos> ringCells = null)
        {
            PlacedRoom p = layout.Rooms[roomIndex];
            if (p.Room.Kind != RoomKind.Combat || RoomEncounterTable.For(p.Room.Id).Count == 0) return null;
            var mine = plan.Spawns.Where(s => s.RoomIndex == roomIndex && s.Kind != EncounterKind.Hunter).ToList();
            if (mine.Count == 0) return null;
            CellPos? ward = layout.WardenCell;
            if (mine.Any(s => s.Tier == SpawnTier.Tier2 || (ward.HasValue && ward.Value.X == s.X && ward.Value.Y == s.Y))) return null;

            bool isAmbush = plan.AmbushRoom == roomIndex;
            RoomDataV2 rd = RoomDataV2.FromV1(p.Room);
            var reserved = new List<CellPos>(); CellPos? carrierWorld = null;
            if (layout.Carrier != null && mine.Any(s => s.Kind == EncounterKind.Authored && s.X == layout.Carrier.X && s.Y == layout.Carrier.Y))
            { carrierWorld = new CellPos(layout.Carrier.X, layout.Carrier.Y); reserved.Add(new CellPos(layout.Carrier.X - p.X, layout.Carrier.Y - p.Y)); }
            RoomEncounter enc = EncounterBuilder.Build(rd, null, plan.Seed, plan.Floor, roomIndex, players, plan.RosterMask, isAmbush, reserved);
            if (enc == null) return null;

            // legal world cells: the plan's own spawn cells of this room plus the room's free cells
            var legal = new List<CellPos>(); var seen = new HashSet<(int, int)>();
            foreach (EncounterSpawn s in mine) if (seen.Add((s.X, s.Y))) legal.Add(s.Cell);
            foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(layout, dressing, roomIndex)) if (seen.Add((c.X, c.Y))) legal.Add(c);

            int target = mine.Count, held = enc.HeldHeroes;
            var list = enc.Spawns.Select(s => new TemplateSpawn(p.Cell(s.Cell.X, s.Cell.Y), s.Kind, s.Wave, false)).ToList();

            // trim to the live plan count: last wave first, last spawn first (the carrier is the first wave-0 spawn, so it goes last)
            while (list.Count > target) { int i = list.FindLastIndex(s => s.Wave == list.Max(o => o.Wave)); list.RemoveAt(i); }

            // pad (never past the validator caps) with roster-banded wave-0 enemies
            for (int n = 0; list.Count < target; n++)
            {
                int live = list.Count(s => s.Wave == 0);
                if (live + 1 > GameMetrics.MaxLiveEnemies || !FightSpace.Fits(rd.Base, held, live + 1)) break;
                list.Add(new TemplateSpawn(new CellPos(-1, -1), CellarEnemyMix.Tier1Kind(plan.Seed, plan.Floor, roomIndex, 200 + n, plan.RosterMask), 0, false));   // placeholder cell: resolved below
            }

            // ambush ring: wave-0 cells in order, skipping the carrier's cell
            int ring = 0;
            var result = new List<TemplateSpawn>();
            for (int wave = 0; wave < 2; wave++)
            {
                var used = new HashSet<(int, int)>();
                if (wave == 0 && carrierWorld.HasValue) used.Add((carrierWorld.Value.X, carrierWorld.Value.Y));
                bool carrierDone = false;
                foreach (TemplateSpawn s in list.Where(o => o.Wave == wave))
                {
                    bool isCarrier = wave == 0 && carrierWorld.HasValue && !carrierDone;
                    CellPos want = s.Cell;
                    if (isCarrier) { want = carrierWorld.Value; carrierDone = true; }
                    else if (isAmbush && wave == 0 && ringCells != null)
                    {
                        while (ring < ringCells.Count && used.Contains((ringCells[ring].X, ringCells[ring].Y))) ring++;
                        if (ring < ringCells.Count) want = ringCells[ring++];
                    }
                    CellPos cell;
                    if (isCarrier) cell = want;
                    else if (!Nearest(legal, used, want, out cell)) continue;   // no free cell left: the group is one smaller
                    used.Add((cell.X, cell.Y));
                    result.Add(new TemplateSpawn(cell, s.Kind, wave, isCarrier));
                }
            }
            return result.Count == 0 ? null : new CellarTemplateRoom(enc.TemplateId, result);
        }

        // The wanted cell when it is legal and unused, else the nearest unused legal cell (squared distance, list order breaks ties).
        static bool Nearest(List<CellPos> legal, HashSet<(int, int)> used, CellPos want, out CellPos best)
        {
            best = default; long bestD = long.MaxValue; bool found = false;
            foreach (CellPos c in legal)
            {
                if (used.Contains((c.X, c.Y))) continue;
                long dx = c.X - want.X, dy = c.Y - want.Y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = c; found = true; }
            }
            return found;
        }
    }
}
