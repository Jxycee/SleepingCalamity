// Core/Cellars/CellarSpawnSafety.cs: pure distance rules the Cellars host uses so a spawn never lands on a player (playtest 12, 18). Pure C#, no engine references.
// Positions are in layout metres on the floor plane: x = east, y = south (the builder's -z), so cell (cx, cy) has its centre at ((cx + 0.5) * CellMeters, (cy + 0.5) * CellMeters).
// Authority: none (static rules); the host passes the positions of the players it sees.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public readonly struct PlanePoint
    {
        public readonly float X, Y;
        public PlanePoint(float x, float y) { X = x; Y = y; }
    }

    public static class CellarSpawnSafety
    {
        public const float AmbushMinPlayerMeters = 4f;   // an ambush spawn keeps at least this far from every player present
        public const float HunterMinPlayerMeters = 8f;  // the hunter (re)spawns only with nobody this close to its cell

        public static float CellCenterX(int cx) => (cx + 0.5f) * CellarFloorLayout.CellMeters;
        public static float CellCenterY(int cy) => (cy + 0.5f) * CellarFloorLayout.CellMeters;

        // Distance in metres from the centre of `c` to the nearest player; +infinity with no players.
        public static float NearestPlayer(CellPos c, IReadOnlyList<PlanePoint> players)
        {
            if (players == null || players.Count == 0) return float.PositiveInfinity;
            float x = CellCenterX(c.X), y = CellCenterY(c.Y), best = float.PositiveInfinity;
            for (int i = 0; i < players.Count; i++)
            {
                float dx = players[i].X - x, dy = players[i].Y - y, d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                if (d < best) best = d;
            }
            return best;
        }

        // True when no player is closer than `min` metres to the centre of `c` (also true with no players).
        public static bool FarFromAll(CellPos c, IReadOnlyList<PlanePoint> players, float min) => NearestPlayer(c, players) >= min;
    }
}
