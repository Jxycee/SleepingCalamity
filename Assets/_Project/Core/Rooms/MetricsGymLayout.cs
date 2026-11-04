// Core/Rooms/MetricsGymLayout.cs: the metrics gym as pure data, ten lanes of real measurements the editor builder instantiates (metrics gym plan, Task 6; room-authoring spec).
// Metres, y up, +Z along a lane. Lanes are placed side by side on X with 10 m between their bounds. Authority: none (static data).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SleepingCalamity.Core
{
    public enum GymShape : byte { Box, Capsule, Marker, PilotRoom }

    // X, Y, Z are the centre; SX, SY, SZ the size (Capsule: SX = diameter, SY = height; Marker: a thin post). PilotState applies to PilotRoom only.
    public readonly struct GymPiece
    {
        public readonly string Lane, Name; public readonly GymShape Shape; public readonly float X, Y, Z, SX, SY, SZ; public readonly string Label; public readonly int PilotState;
        public GymPiece(string lane, string name, GymShape shape, float x, float y, float z, float sx, float sy, float sz, string label = "", int pilotState = 0)
        { Lane = lane; Name = name; Shape = shape; X = x; Y = y; Z = z; SX = sx; SY = sy; SZ = sz; Label = label ?? ""; PilotState = pilotState; }
        public GymPiece At(float dx) => new GymPiece(Lane, Name, Shape, X + dx, Y, Z, SX, SY, SZ, Label, PilotState);
    }

    public static class MetricsGymLayout
    {
        public const float LaneGap = 10f, SlabThickness = 0.1f, MarkerThickness = 0.05f, MarkerHeight = 2f;

        static readonly string[] LaneNames = { "run", "dodge", "gaps", "ledges", "stair", "doors", "water", "camera", "fight", "pilot" };
        public static IReadOnlyList<string> Lanes => LaneNames;

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        public static IReadOnlyList<GymPiece> Build()
        {
            var result = new List<GymPiece>(); float right = 0f; bool first = true;
            foreach (string lane in LaneNames)
            {
                var ps = BuildLane(lane);
                float minX = ps.Min(p => p.X - p.SX / 2), maxX = ps.Max(p => p.X + p.SX / 2);
                float dx = first ? -minX : right + LaneGap - minX;
                foreach (var p in ps) result.Add(p.At(dx));
                right = maxX + dx; first = false;
            }
            return result;
        }

        // Pieces in lane-local coordinates (x = 0 is the lane's centre line, z = 0 its start line).
        static List<GymPiece> BuildLane(string lane)
        {
            var l = new List<GymPiece>();
            void Slab(string name, float x, float z, float sx, float sz) => l.Add(new GymPiece(lane, name, GymShape.Box, x, -SlabThickness / 2, z, sx, SlabThickness, sz));
            void Block(string name, float x, float z, float sx, float h, float sz, string label = "") => l.Add(new GymPiece(lane, name, GymShape.Box, x, h / 2, z, sx, h, sz, label));
            void Post(string name, float x, float z, string label) => l.Add(new GymPiece(lane, name, GymShape.Marker, x, MarkerHeight / 2, z, MarkerThickness, MarkerHeight, MarkerThickness, label));
            void Cap(string name, float x, float z, float d, float h) => l.Add(new GymPiece(lane, name, GymShape.Capsule, x, h / 2, z, d, h, d));
            float cell = GameMetrics.CellMeters;

            switch (lane)
            {
                case "run":
                    {
                        float len = MovementMath.RunSpeed * 3 + 2f;
                        Slab("floor", 0, len / 2 - 1f, 6f, len);
                        for (int k = 1; k <= 3; k++) Post("run" + k, 0, MovementMath.RunSpeed * k, k + " s run");
                        break;
                    }
                case "dodge":
                    {
                        float d = MovementProbe.DodgeDistance();
                        Slab("floor", 0, (d + 2f) / 2 - 1f, 6f, d + 2f);
                        Post("dodge", 0, d, "dodge " + F(d) + " m");
                        break;
                    }
                case "gaps":
                    {
                        // Floor slabs with pits between them; the pit widths are the gaps.
                        float j = MovementProbe.RunJumpDistance();
                        float[] widths = { 2.0f, 3.0f, j - 0.2f, j + 0.3f, cell };
                        float z = 0f; Slab("floor0", 0, -2f, 6f, 4f);
                        for (int i = 0; i < widths.Length; i++)
                        {
                            Post("gap" + (i + 1), -3.5f, z + widths[i] / 2, F(widths[i]) + " m gap");
                            z += widths[i];
                            Slab("floor" + (i + 1), 0, z + 2f, 6f, 4f); z += 4f;
                        }
                        break;
                    }
                case "ledges":
                    {
                        float[] hs = { MovementMath.StepOffset, 1.0f, MovementProbe.JumpApex(), GameMetrics.MaxClimbableLedge, GameMetrics.LevelHeightCm / 100f };
                        string[] tags = { "step offset", "1 m", "jump apex", "max climbable", "one level" };
                        Slab("floor", 0, hs.Length * 2f, 6f, hs.Length * 4f + 4f);
                        for (int i = 0; i < hs.Length; i++) Block("ledge" + (i + 1), 0, 3f + i * 4f, 2f, hs[i], 2f, tags[i] + " " + F(hs[i]) + " m");
                        break;
                    }
                case "stair":
                    {
                        float rise = GameMetrics.StairRiseCm / 100f, run = GameMetrics.StairRunCm / 100f;
                        Slab("floor", 0, cell + 2f, cell + 2f, cell * 2f + 4f);
                        for (int i = 0; i < GameMetrics.StepsPerLevel; i++) Block("step" + (i + 1), 0, run * (i + 0.5f), cell, rise * (i + 1), run);
                        Block("deck", 0, cell + cell / 2f, cell, GameMetrics.LevelHeightCm / 100f, cell, "level 1");
                        break;
                    }
                case "doors":
                    {
                        Slab("floor", 0, 3f, 10f, 10f);
                        float[] openings = { GameMetrics.DoorWidth, GameMetrics.WideArchWidth };
                        float lintel = GameMetrics.WallHeight - GameMetrics.DoorHeight;
                        for (int i = 0; i < 2; i++)
                        {
                            float z = i * 6f, o = openings[i], jamb = (8f - o) / 2f;
                            Block("door" + (i + 1) + "_left", -(o / 2 + jamb / 2), z, jamb, GameMetrics.WallHeight, 0.3f);
                            Block("door" + (i + 1) + "_right", o / 2 + jamb / 2, z, jamb, GameMetrics.WallHeight, 0.3f);
                            l.Add(new GymPiece(lane, "door" + (i + 1) + "_lintel", GymShape.Box, 0, GameMetrics.DoorHeight + lintel / 2, z, o, lintel, 0.3f, F(o) + " x " + F(GameMetrics.DoorHeight) + " m"));
                        }
                        l.Add(new GymPiece(lane, "vault_crown", GymShape.Box, 0, GameMetrics.RoomVaultCrown, 3f, 10f, MarkerThickness, 0.3f, "room vault crown " + F(GameMetrics.RoomVaultCrown) + " m"));
                        break;
                    }
                case "water":
                    {
                        // Each pool is a 4 x 4 m volume whose height is its depth, hanging below the floor line.
                        float[] depths = { GameMetrics.WadeMaxCm / 100f, 1.0f }; string[] tags = { "wade ", "deep " };
                        Slab("floor0", 0, -2f, 6f, 4f);
                        float z = 0f;
                        for (int i = 0; i < 2; i++)
                        {
                            l.Add(new GymPiece(lane, "pool" + (i + 1), GymShape.Box, 0, -depths[i] / 2, z + cell / 2, cell, depths[i], cell, tags[i] + F(depths[i]) + " m"));
                            z += cell; Slab("floor" + (i + 1), 0, z + 1f, 6f, 2f); z += 2f;
                        }
                        break;
                    }
                case "camera":
                    {
                        float boom = GameMetrics.CameraFreeBoom, pitch = GameMetrics.CameraPitchDeg * (float)Math.PI / 180f;
                        float up = boom * (float)Math.Sin(pitch), back = boom * (float)Math.Cos(pitch);
                        Slab("floor", 0, -back / 2f, 8f, back + 4f);
                        Cap("hero", 0, 0, MovementMath.CapsuleRadius * 2f, MovementMath.CapsuleHeight);
                        Post("pivot", 0.6f, 0, "pivot " + F(MovementMath.CameraPivotHeight) + " m");
                        l.Add(new GymPiece(lane, "boom", GymShape.Box, 0, MovementMath.CameraPivotHeight + up / 2, -back / 2f, MarkerThickness, up, back, "boom " + F(boom) + " m at " + F(GameMetrics.CameraPitchDeg) + " deg (bounds)"));
                        l.Add(new GymPiece(lane, "ceiling", GymShape.Box, 0, GameMetrics.RoomVaultCrown + SlabThickness / 2, -back / 2f, 8f, SlabThickness, back + 4f, "room vault crown"));
                        break;
                    }
                case "fight":
                    {
                        var (cw, ch) = FightSpace.SmallestRectangle(GameMetrics.FullPartyHeroes, GameMetrics.MaxLiveEnemies);
                        float sx = cw * cell, sz = ch * cell, t = 0.4f;   // inner span; cw <= ch so the long side runs along +Z
                        Slab("floor", 0, sz / 2, sx, sz);
                        l.Add(new GymPiece(lane, "wall_west", GymShape.Box, -(sx / 2 + t / 2), GameMetrics.WallHeight / 2, sz / 2, t, GameMetrics.WallHeight, sz + 2 * t));
                        l.Add(new GymPiece(lane, "wall_east", GymShape.Box, sx / 2 + t / 2, GameMetrics.WallHeight / 2, sz / 2, t, GameMetrics.WallHeight, sz + 2 * t));
                        l.Add(new GymPiece(lane, "wall_south", GymShape.Box, 0, GameMetrics.WallHeight / 2, -t / 2, sx, GameMetrics.WallHeight, t));
                        l.Add(new GymPiece(lane, "wall_north", GymShape.Box, 0, GameMetrics.WallHeight / 2, sz + t / 2, sx, GameMetrics.WallHeight, t));
                        float[] hx = { -4.5f, -1.5f, 1.5f, 4.5f };
                        for (int i = 0; i < 4; i++) Cap("hero" + (i + 1), hx[i], 3f, MovementMath.CapsuleRadius * 2f, MovementMath.CapsuleHeight);
                        for (int i = 0; i < 8; i++) Cap("enemy" + (i + 1), hx[i % 4], 11f + (i / 4) * 3f, 1.0f, 1.2f);
                        break;
                    }
                default:   // pilot
                    {
                        float room = 4 * cell, gap = 4f;
                        for (int s = 0; s < 2; s++)
                            l.Add(new GymPiece(lane, "pilot_state" + s, GymShape.PilotRoom, s * (room + gap), GameMetrics.WallHeight / 2, room / 2, room, GameMetrics.WallHeight, room, s == 0 ? "hanging hall, drained" : "hanging hall, flooded", s));
                        break;
                    }
            }
            return l;
        }
    }
}
