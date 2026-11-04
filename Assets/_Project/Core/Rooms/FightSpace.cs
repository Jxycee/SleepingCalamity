// Core/Rooms/FightSpace.cs: how many floor cells a fight needs, from the hero's dodge reach and the enemies' reach (metrics gym plan, Task 2; room-authoring spec).
using System;

namespace SleepingCalamity.Core
{
    // Pure arithmetic: each combatant claims a circular bubble of floor; a room fits when its floor holds them all at UsableFloorFraction.
    public static class FightSpace
    {
        public const float EnemyBubbleRadius = 2.0f;   // placeholder: tier-1 reach 1.45 + body 0.55
        public const float UsableFloorFraction = 0.8f;  // placeholder: the share of floor cells not lost to props and edges

        public static float HeroBubbleRadius => MovementProbe.DodgeDistance() / 2f + MovementMath.CapsuleRadius;   // 2.1

        public static int MinShortSide(int heroes) => heroes >= 3 ? 3 : 2;

        public static int RequiredFloorCells(int heroes, int enemies)
        {
            float hr = HeroBubbleRadius;
            double area = heroes * Math.PI * hr * hr + enemies * Math.PI * EnemyBubbleRadius * EnemyBubbleRadius;
            double cell = GameMetrics.CellMeters * GameMetrics.CellMeters * UsableFloorFraction;
            return (int)Math.Ceiling(area / cell - 1e-9);
        }

        public static bool Fits(CellarRoom room, int heroes, int enemies) =>
            room.FloorCount >= RequiredFloorCells(heroes, enemies) && Math.Min(room.Width, room.Height) >= MinShortSide(heroes);

        // Smallest area with w <= h, w >= MinShortSide and w*h >= Required; ties go to the smaller h.
        public static (int w, int h) SmallestRectangle(int heroes, int enemies)
        {
            int need = RequiredFloorCells(heroes, enemies), minW = MinShortSide(heroes);
            (int w, int h) best = (0, 0); int bestArea = int.MaxValue;
            for (int w = minW; w <= need; w++)
                for (int h = w; h <= need; h++)
                {
                    int area = w * h;
                    if (area < need) continue;
                    if (area < bestArea || (area == bestArea && h < best.h)) { best = (w, h); bestArea = area; }
                    break;   // larger h only grows the area
                }
            return best;
        }
    }
}
