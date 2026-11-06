// Core/Rooms/EncounterBuilder.cs: resolves a template to concrete spawns for one room, floor, seed and party size (encounter templates plan, Task 3). Pure C#, no engine references.
// A pure function of its arguments: own Rng stream "cellars.template", key ((ulong)floor << 32) | (uint)roomIndex; integer scoring only; never touches the layout, dressing or CellarEncounterPlan streams.
// Cells are ROOM-LOCAL (x, y inside the room's rows). Authority: none (static data); the host would consume it once the spawner is wired (a later request).
//   - Ambush: the template is used only when isAmbushRoom (the caller passes CellarEncounterPlan.AmbushRoom == roomIndex) and the room has no gimmick region; no other roll exists.
//   - A small-party room (CellarRoomsV2.SmallParty) holds its group at MaxHeroes: more players keep the MaxHeroes group.
//   - adds (one per extra player, at most CoopScaling.ExtraEnemiesPerRoom) are dropped from the last until FightSpace.Fits.
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public sealed class PlannedSpawn
    {
        public readonly CellPos Cell; public readonly EnemyKind Kind; public readonly SpawnTier Tier; public readonly int Wave; public readonly string Role; public readonly bool OnAuthoredCell;
        public PlannedSpawn(CellPos cell, EnemyKind kind, SpawnTier tier, int wave, string role, bool onAuthoredCell) { Cell = cell; Kind = kind; Tier = tier; Wave = wave; Role = role; OnAuthoredCell = onAuthoredCell; }
    }

    public sealed class RoomEncounter
    {
        public readonly string RoomId, TemplateId; public readonly int Floor, RoomIndex, Players, HeldHeroes;
        public readonly IReadOnlyList<PlannedSpawn> Spawns;
        public RoomEncounter(string roomId, string templateId, int floor, int roomIndex, int players, int heldHeroes, IEnumerable<PlannedSpawn> spawns)
        { RoomId = roomId; TemplateId = templateId; Floor = floor; RoomIndex = roomIndex; Players = players; HeldHeroes = heldHeroes; Spawns = spawns.ToList(); }
        public int WaveCount => Spawns.Count == 0 ? 0 : Spawns.Max(s => s.Wave) + 1;
        public int LiveIn(int wave) => Spawns.Count(s => s.Wave == wave);
        public int PeakLive { get { int m = 0; for (int w = 0; w < WaveCount; w++) m = Math.Max(m, LiveIn(w)); return m; } }
    }

    public static class EncounterBuilder
    {
        public const string StreamId = "cellars.template";

        public static ulong SeedKey(int floor, int roomIndex) => ((ulong)floor << 32) | (uint)roomIndex;

        // Standable floor cells a spawn may use: not a door, chest or feature cell. Row-major.
        public static IReadOnlyList<CellPos> FreeCells(RoomDataV2 room)
        {
            CellarRoom b = room.Base; var state = new int[room.Regions.Count]; var list = new List<CellPos>();
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    if (!room.Standable(x, y, state)) continue;
                    if (b.Doors.Any(d => d.X == x && d.Y == y) || b.Chests.Any(c => c.X == x && c.Y == y) || b.Features.Any(f => f.X == x && f.Y == y)) continue;
                    list.Add(new CellPos(x, y));
                }
            return list;
        }

        public static int HeldHeroes(string roomId, int players)
        {
            int p = CoopScaling.ClampPlayers(players);
            foreach (SmallPartyRoom s in CellarRoomsV2.SmallParty) if (s.Id == roomId) return Math.Min(p, s.MaxHeroes);
            return p;
        }

        // The roster kind a role is remapped to when its own kind is off the roster (the spawners' rule, never DoughWarden): the first of Slime, RatKing, Broom on the roster.
        public static EnemyKind Remap(EnemyKind kind, int rosterMask)
        {
            if ((CellarFloorRoster.BitOf(kind) & rosterMask) != 0) return kind;
            foreach (EnemyKind k in new[] { EnemyKind.Slime, EnemyKind.RatKing, EnemyKind.Broom }) if ((CellarFloorRoster.BitOf(k) & rosterMask) != 0) return k;
            return kind;
        }

        public static bool IsLegal(EncounterTemplate t, RoomDataV2 room, int floor, bool isAmbushRoom)
        {
            if (!t.LegalOnFloor(floor)) return false;
            if (t.IsAmbush && (!isAmbushRoom || room.Regions.Count > 0)) return false;
            if (t.SoloPeak > room.Base.Spawns.Count + 1) return false;                       // plan guardrail: solo live <= today's plan count + 1
            if ((room.Affordances & t.Wants) != t.Wants) return false;
            if (t.Kinds.Any(k => k == EnemyKind.DoughWarden || !EnemyHabitat.Fits(k, room))) return false;
            return true;
        }

        // The template a room draws for (seed, floor, roomIndex): the Ambush template when this is the Ambush room and the table lists it, else a stream pick among the legal ones.
        public static EncounterTemplate Choose(RoomDataV2 room, uint seed, int floor, int roomIndex, bool isAmbushRoom)
        {
            var legal = RoomEncounterTable.For(room.Base.Id).Select(EncounterTemplates.Find).Where(t => t != null && IsLegal(t, room, floor, isAmbushRoom)).ToList();
            if (legal.Count == 0) return null;
            if (isAmbushRoom) { EncounterTemplate a = legal.FirstOrDefault(t => t.IsAmbush); if (a != null) return a; }
            var rng = new Pcg32(Rng.Derive(seed, StreamId, SeedKey(floor, roomIndex)));
            return legal[rng.Range(0, legal.Count)];
        }

        // templateId null = Choose. reserved: room-local cells whose authored spawn must keep its cell (gate-key carrier, warden): they take the first wave-0 spawns.
        // Returns null when the room has no table entry (a set piece keeps its authored guard) or no legal template.
        public static RoomEncounter Build(RoomDataV2 room, string templateId, uint seed, int floor, int roomIndex, int players, int rosterMask, bool isAmbushRoom = false, IReadOnlyCollection<CellPos> reserved = null)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            EncounterTemplate t = templateId != null ? EncounterTemplates.Find(templateId) : Choose(room, seed, floor, roomIndex, isAmbushRoom);
            if (t == null) return null;
            int held = HeldHeroes(room.Base.Id, players), p = CoopScaling.ClampPlayers(players);

            // expand: base roles in order, then one add per extra player (wave 0), dropped from the last until the room fits
            var kinds = new List<(EnemyKind kind, int wave, string role, EncounterAnchor anchor)>();
            foreach (EncounterRole r in t.Roles) for (int i = 0; i < r.Count; i++) kinds.Add((Remap(r.Kind, rosterMask), r.Wave, r.Name, r.Anchor));
            int adds = Math.Min(held - 1, Math.Min(t.PerPlayerAdds.Count, CoopScaling.ExtraEnemiesPerRoom(held)));
            for (; adds > 0; adds--)
            {
                int peak = Math.Max(t.BaseInWave(0) + adds, t.WaveCount > 1 ? t.BaseInWave(1) : 0);
                if (peak <= GameMetrics.MaxLiveEnemies && FightSpace.Fits(room.Base, held, peak)) break;
            }
            for (int i = 0; i < adds; i++) kinds.Add((Remap(t.PerPlayerAdds[i], rosterMask), 0, "add", EncounterAnchor.Open));

            var free = FreeCells(room); var spawns = new List<PlannedSpawn>();
            for (int wave = 0; wave < t.WaveCount; wave++)
            {
                var used = new HashSet<(int, int)>();
                var reservedLeft = new List<CellPos>();
                if (wave == 0 && reserved != null) foreach (CellPos rc in reserved) if (free.Any(c => c.X == rc.X && c.Y == rc.Y) && !reservedLeft.Any(c => c.X == rc.X && c.Y == rc.Y)) reservedLeft.Add(rc);
                foreach (var k in kinds.Where(k => k.wave == wave))
                {
                    CellPos cell; bool authored;
                    if (reservedLeft.Count > 0) { cell = reservedLeft[0]; reservedLeft.RemoveAt(0); authored = true; }
                    else if (!Pick(room, free, used, k.anchor, out cell, out authored)) continue;   // no free cell left: the group is smaller (the validator reports a short wave through its fit rule only if needed)
                    used.Add((cell.X, cell.Y));
                    spawns.Add(new PlannedSpawn(cell, k.kind, SpawnTier.Tier1, wave, k.role, authored));
                }
            }
            return new RoomEncounter(room.Base.Id, t.Id, floor, roomIndex, p, held, spawns);
        }

        // The spawner adapter: the wave-0 cells in order (room-local), what the existing "extra cells" path would consume.
        public static IReadOnlyList<CellPos> ToExtraCells(RoomEncounter e) => e.Spawns.Where(s => s.Wave == 0).Select(s => s.Cell).ToList();

        static bool Pick(RoomDataV2 room, IReadOnlyList<CellPos> free, HashSet<(int, int)> used, EncounterAnchor anchor, out CellPos best, out bool authored)
        {
            best = default; authored = false; long bestScore = long.MinValue; bool found = false;
            foreach (CellPos c in free)
            {
                if (used.Contains((c.X, c.Y))) continue;
                bool a = room.Base.Spawns.Any(s => s.X == c.X && s.Y == c.Y);
                long score = (a ? 100000 : 0) + Score(room, c, anchor);
                if (score > bestScore) { bestScore = score; best = c; authored = a; found = true; }   // strict: row-major ties keep the first
            }
            return found;
        }

        static int Score(RoomDataV2 room, CellPos c, EncounterAnchor anchor)
        {
            CellarRoom b = room.Base;
            int doorDist = int.MaxValue;
            foreach (CellDoor d in b.Doors) doorDist = Math.Min(doorDist, Math.Abs(d.X - c.X) + Math.Abs(d.Y - c.Y));
            if (doorDist == int.MaxValue) doorDist = 0;
            switch (anchor)
            {
                case EncounterAnchor.Door: return -doorDist;
                case EncounterAnchor.Far: return doorDist;
                case EncounterAnchor.Dais: return room.LevelAt(c.X, c.Y) * 100 + doorDist;
                case EncounterAnchor.Pillar:
                    {
                        int n = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                            {
                                if (dx == 0 && dy == 0) continue; int x = c.X + dx, y = c.Y + dy;
                                if (!b.InBounds(x, y)) continue;
                                CellKind k = b.CellAt(x, y);
                                if (k == CellKind.Pillar) n += 2; else if (k == CellKind.Solid && (dx == 0 || dy == 0)) n++;
                            }
                        return n;
                    }
                default:   // Open: free neighbours
                    {
                        int n = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
                            { if (dx == 0 && dy == 0) continue; int x = c.X + dx, y = c.Y + dy; if (b.InBounds(x, y) && b.CellAt(x, y) == CellKind.Floor) n++; }
                        return n;
                    }
            }
        }
    }
}
