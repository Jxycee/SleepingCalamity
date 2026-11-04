// Core/Rooms/RoomDataV2.cs: room data v2, the frozen room format (levels, water/heat gimmick regions, stairs, tool sockets, typed doors) that wraps an
// unchanged v1 CellarRoom (metrics gym plan, Task 3; spec 3.2 and 11.5 F7). Pure C#. The floor generator keeps reading v1; RoomValidatorV2 checks v2.
// Authority: none (static data).
using System;
using System.Collections.Generic;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum DoorType : byte { Plain, WideArch, Gate, Locked, ToolSeal, Secret }   // no crawl: the hero has no crouch

    // Frozen verbs (spec 3.2; names are placeholders). Adding one is a deliberate change to RoomDataV2Tests.ToolVerbs_AreFrozen.
    public enum ToolVerb : byte { None, Sluice, Bellows, Blast, Smoke, Star, Tide }

    public enum GimmickKind : byte { Water, Heat }

    public enum SocketPurpose : byte { Control, Alcove }

    [Flags] public enum RoomAffordance { None = 0, OpenFloor = 1, Water = 2, Ledge = 4, HighCeiling = 8, FullParty = 16 }

    public sealed class DoorV2
    {
        public readonly int DoorIndex;   // into Base.Doors
        public readonly DoorType Type; public readonly int Level; public readonly ToolVerb Tool; public readonly string GateRegion; public readonly int GateOpenState;
        public DoorV2(int doorIndex, DoorType type, int level = 0, ToolVerb tool = ToolVerb.None, string gateRegion = null, int gateOpenState = 0)
        { DoorIndex = doorIndex; Type = type; Level = level; Tool = tool; GateRegion = gateRegion; GateOpenState = gateOpenState; }
    }

    // A set of cells that share one state value (a water surface height in cm above level 0, or heat 0 off / 1 burning). The state is an index into StateValues.
    public sealed class GimmickRegion
    {
        public readonly string Id; public readonly char Letter; public readonly GimmickKind Kind; public readonly IReadOnlyList<int> StateValues;
        IReadOnlyList<CellPos> cells = new CellPos[0];
        public GimmickRegion(string id, char letter, GimmickKind kind, params int[] stateValues)
        { Id = id; Letter = letter; Kind = kind; StateValues = (stateValues ?? new int[0]).ToArray(); }
        public int StateCount => StateValues.Count;
        public IReadOnlyList<CellPos> Cells => cells;   // filled by RoomDataV2.Author
        internal GimmickRegion WithCells(IEnumerable<CellPos> c) { var r = new GimmickRegion(Id, Letter, Kind, StateValues.ToArray()); r.cells = c.ToList(); return r; }
    }

    // The cell is at its own level; the neighbour on Up is one level higher.
    public sealed class StairCell
    {
        public readonly int X, Y; public readonly CellSide Up;
        public StairCell(int x, int y, CellSide up) { X = x; Y = y; Up = up; }
    }

    // A Control cycles its Region to (state + 1) % StateCount when the party owns Tool (None needs nothing). Facing: the wall is behind it, as for dressing sockets.
    public sealed class ToolSocket
    {
        public readonly string Id; public readonly int X, Y; public readonly CellSide Facing; public readonly SocketPurpose Purpose; public readonly ToolVerb Tool; public readonly string Region;
        public ToolSocket(string id, int x, int y, CellSide facing, SocketPurpose purpose, ToolVerb tool = ToolVerb.None, string region = null)
        { Id = id; X = x; Y = y; Facing = facing; Purpose = purpose; Tool = tool; Region = region; }
    }

    public sealed class RoomDataV2
    {
        public const int SchemaVersion = 2;
        public readonly int Version; public readonly CellarRoom Base;
        public readonly IReadOnlyList<DoorV2> Doors; public readonly IReadOnlyList<GimmickRegion> Regions; public readonly IReadOnlyList<StairCell> Stairs; public readonly IReadOnlyList<ToolSocket> Sockets;
        public readonly RoomAffordance AuthoredTags;
        readonly int[,] levels; readonly int[,] regionIndex;   // [x, y]; regionIndex -1 = none

        RoomDataV2(int version, CellarRoom b, int[,] levels, int[,] regionIndex, IEnumerable<DoorV2> doors, IEnumerable<GimmickRegion> regions, IEnumerable<StairCell> stairs, IEnumerable<ToolSocket> sockets, RoomAffordance tags)
        {
            Version = version; Base = b; this.levels = levels; this.regionIndex = regionIndex; Doors = doors.ToList(); Regions = regions.ToList();
            Stairs = stairs.ToList(); Sockets = sockets.ToList(); AuthoredTags = tags;
        }

        public int LevelAt(int x, int y) => levels[x, y];
        public int RegionIndexAt(int x, int y) => regionIndex[x, y];
        public GimmickRegion RegionAt(int x, int y) { int i = regionIndex[x, y]; return i < 0 ? null : Regions[i]; }
        public int RegionIndex(string id) { for (int i = 0; i < Regions.Count; i++) if (Regions[i].Id == id) return i; return -1; }

        // Water: the surface height minus the cell floor (negative = dry); anything else 0. state = one state index per region.
        public int DepthCm(int x, int y, IReadOnlyList<int> state)
        {
            int i = regionIndex[x, y];
            if (i < 0 || Regions[i].Kind != GimmickKind.Water) return 0;
            return Regions[i].StateValues[state[i]] - levels[x, y] * GameMetrics.LevelHeightCm;
        }

        public bool Burning(int x, int y, IReadOnlyList<int> state)
        {
            int i = regionIndex[x, y];
            return i >= 0 && Regions[i].Kind == GimmickKind.Heat && Regions[i].StateValues[state[i]] == 1;
        }

        public bool Standable(int x, int y, IReadOnlyList<int> state) =>
            Base.CellAt(x, y) == CellKind.Floor && DepthCm(x, y, state) <= GameMetrics.WadeMaxCm && !Burning(x, y, state);

        public RoomAffordance Affordances
        {
            get
            {
                RoomAffordance a = AuthoredTags;
                if (Regions.Any(r => r.Kind == GimmickKind.Water)) a |= RoomAffordance.Water;
                for (int x = 0; x < Base.Width; x++) for (int y = 0; y < Base.Height; y++) if (levels[x, y] > 0) a |= RoomAffordance.Ledge;
                if (FightSpace.Fits(Base, GameMetrics.FullPartyHeroes, GameMetrics.MaxLiveEnemies)) a |= RoomAffordance.FullParty;
                if (HasOpenWindow()) a |= RoomAffordance.OpenFloor;
                return a;
            }
        }

        bool HasOpenWindow()
        {
            for (int x0 = 0; x0 + 3 <= Base.Width; x0++)
                for (int y0 = 0; y0 + 3 <= Base.Height; y0++)
                {
                    bool ok = true; int lv = levels[x0, y0];
                    for (int x = x0; x < x0 + 3 && ok; x++)
                        for (int y = y0; y < y0 + 3 && ok; y++)
                            ok = Base.CellAt(x, y) == CellKind.Floor && levels[x, y] == lv && regionIndex[x, y] < 0;
                    if (ok) return true;
                }
            return false;
        }

        // Version 2, all levels 0, doors Locked where CellDoor.Locked else Plain, no regions, stairs or sockets.
        public static RoomDataV2 FromV1(CellarRoom room) => Author(room, null, null, null, null, null, null, RoomAffordance.None);

        // levelRows: one digit per cell, row 0 north (null = all 0). regionRows: a region's Letter or '.' per cell (null = none). doorOverrides replace the
        // FromV1 door with the same DoorIndex (an index outside the room's doors is kept so the validator reports it). Throws ArgumentException on a malformed
        // grid (wrong row count or width, unknown letter or character, repeated region letter); rule checks belong to RoomValidatorV2.
        public static RoomDataV2 Author(CellarRoom baseRoom, string[] levelRows, string[] regionRows, GimmickRegion[] regions, StairCell[] stairs, ToolSocket[] sockets,
            DoorV2[] doorOverrides, RoomAffordance authoredTags, int version = SchemaVersion)
        {
            if (baseRoom == null) throw new ArgumentException("no base room");
            int w = baseRoom.Width, h = baseRoom.Height;
            regions = regions ?? new GimmickRegion[0];
            var lv = new int[w, h]; var ri = new int[w, h];
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) ri[x, y] = -1;
            void CheckGrid(string[] rows, string what)
            {
                if (rows.Length != h) throw new ArgumentException(baseRoom.Id + ": " + what + " has " + rows.Length + " rows, expected " + h);
                for (int y = 0; y < h; y++) if (rows[y] == null || rows[y].Length != w) throw new ArgumentException(baseRoom.Id + ": " + what + " row " + y + " is not " + w + " wide");
            }
            if (levelRows != null)
            {
                CheckGrid(levelRows, "level rows");
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        char c = levelRows[y][x];
                        if (c < '0' || c > '9') throw new ArgumentException(baseRoom.Id + ": level '" + c + "' at (" + x + "," + y + ") is not a digit");
                        lv[x, y] = c - '0';
                    }
            }
            var cellLists = new List<CellPos>[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                cellLists[i] = new List<CellPos>();
                if (regions[i].Letter == '.' ) throw new ArgumentException(baseRoom.Id + ": region " + regions[i].Id + " uses '.' as its letter");
                for (int j = 0; j < i; j++) if (regions[j].Letter == regions[i].Letter) throw new ArgumentException(baseRoom.Id + ": regions " + regions[j].Id + " and " + regions[i].Id + " share the letter '" + regions[i].Letter + "'");
            }
            if (regionRows != null)
            {
                CheckGrid(regionRows, "region rows");
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    {
                        char c = regionRows[y][x];
                        if (c == '.') continue;
                        int idx = Array.FindIndex(regions, r => r.Letter == c);
                        if (idx < 0) throw new ArgumentException(baseRoom.Id + ": region letter '" + c + "' at (" + x + "," + y + ") matches no region");
                        ri[x, y] = idx; cellLists[idx].Add(new CellPos(x, y));
                    }
            }
            var doors = new List<DoorV2>();
            for (int i = 0; i < baseRoom.Doors.Count; i++) doors.Add(new DoorV2(i, baseRoom.Doors[i].Locked ? DoorType.Locked : DoorType.Plain));
            if (doorOverrides != null)
                foreach (DoorV2 o in doorOverrides)
                {
                    int at = doors.FindIndex(d => d.DoorIndex == o.DoorIndex);
                    if (at >= 0) doors[at] = o; else doors.Add(o);
                }
            return new RoomDataV2(version, baseRoom, lv, ri, doors, regions.Select((r, i) => r.WithCells(cellLists[i])), stairs ?? new StairCell[0], sockets ?? new ToolSocket[0], authoredTags);
        }
    }
}
