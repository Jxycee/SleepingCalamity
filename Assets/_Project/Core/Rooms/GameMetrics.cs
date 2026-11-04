// Core/Rooms/GameMetrics.cs: the one table of hero, camera, cellar and design measurements room authors and the metrics gym read (metrics gym plan, Task 1; room-authoring spec).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SleepingCalamity.Core
{
    public readonly struct MetricRow
    {
        public readonly string Key; public readonly float Value; public readonly string Unit; public readonly string Source;
        public MetricRow(string key, float value, string unit, string source) { Key = key; Value = value; Unit = unit; Source = source; }
    }

    // Mirrors copy values from Presentation/Gameplay (Core cannot reference them); GameMetricsPinTests proves each one still equals its source.
    public static class GameMetrics
    {
        // ---- mirrors ----
        public const float CameraFreeBoom = 5.0f, CameraLockBoom = 6.5f, CameraFov = 50f, CameraPitchDeg = 15f;
        public const float DoorWidth = 2.4f, DoorHeight = 2.8f, WallHeight = 3.6f, RoomVaultCrown = 5.1f, CorridorVaultCrown = 4.1f;

        // ---- design values (plan 4) ----
        public const int LevelHeightCm = 200, StairRiseCm = 20, StairRunCm = 40, WadeMaxCm = 50, MaxLiveEnemies = 8, FullPartyHeroes = 4, MaxValidatedLevel = 1;
        public const float WideArchWidth = 3.6f;   // placeholder

        public static int StepsPerLevel => LevelHeightCm / StairRiseCm;
        public static float CellMeters => CellarFloorLayout.CellMeters;
        public static float MaxClimbableLedge => MovementMath.JumpHeight + MovementMath.StepOffset;

        const string Mm = "Core/Player/MovementMath.cs:13-23", Probe = "MovementProbe", Design = "design (plan 4)";
        const string Cam = "Presentation/Hero/HeroCameraRig.cs:17", Pitch = "Gameplay/Player/HeroLocomotion.cs:17";
        const string Bld = "Presentation/Cellars/CellarFloorBuilder.cs:65", Art = "Presentation/Cellars/CellarFloorBuilder.Art.cs:51,84";

        public static IReadOnlyList<MetricRow> Rows() => new[]
        {
            new MetricRow("walk_speed", MovementMath.WalkSpeed, "m/s", Mm),
            new MetricRow("jog_speed", MovementMath.JogSpeed, "m/s", Mm),
            new MetricRow("run_speed", MovementMath.RunSpeed, "m/s", Mm),
            new MetricRow("capsule_height", MovementMath.CapsuleHeight, "m", Mm),
            new MetricRow("capsule_radius", MovementMath.CapsuleRadius, "m", Mm),
            new MetricRow("step_offset", MovementMath.StepOffset, "m", Mm),
            new MetricRow("slope_limit", MovementMath.SlopeLimit, "deg", Mm),
            new MetricRow("jump_height", MovementMath.JumpHeight, "m", Mm),
            new MetricRow("dodge_speed", MovementMath.DodgeSpeed, "m/s", Mm),
            new MetricRow("dodge_duration", MovementMath.DodgeDuration, "s", Mm),
            new MetricRow("camera_pivot_height", MovementMath.CameraPivotHeight, "m", Mm),
            new MetricRow("dodge_distance", MovementProbe.DodgeDistance(), "m", Probe),
            new MetricRow("run_jump_distance", MovementProbe.RunJumpDistance(), "m", Probe),
            new MetricRow("jump_apex", MovementProbe.JumpApex(), "m", Probe),
            new MetricRow("max_climbable_ledge", MaxClimbableLedge, "m", Design),
            new MetricRow("camera_free_boom", CameraFreeBoom, "m", Cam),
            new MetricRow("camera_lock_boom", CameraLockBoom, "m", Cam),
            new MetricRow("camera_fov", CameraFov, "deg", Cam),
            new MetricRow("camera_pitch", CameraPitchDeg, "deg", Pitch),
            new MetricRow("cell_size", CellMeters, "m", "Core/Cellars/CellarFloorLayout.cs:68"),
            new MetricRow("door_width", DoorWidth, "m", Bld),
            new MetricRow("door_height", DoorHeight, "m", Bld),
            new MetricRow("wall_height", WallHeight, "m", Bld),
            new MetricRow("room_vault_crown", RoomVaultCrown, "m", Art),
            new MetricRow("corridor_vault_crown", CorridorVaultCrown, "m", Art),
            new MetricRow("level_height", LevelHeightCm, "cm", Design),
            new MetricRow("stair_rise", StairRiseCm, "cm", Design),
            new MetricRow("stair_run", StairRunCm, "cm", Design),
            new MetricRow("steps_per_level", StepsPerLevel, "steps", Design),
            new MetricRow("wade_max", WadeMaxCm, "cm", Design),
            new MetricRow("max_live_enemies", MaxLiveEnemies, "enemies", Design),
            new MetricRow("full_party_heroes", FullPartyHeroes, "heroes", Design),
            new MetricRow("max_validated_level", MaxValidatedLevel, "level", Design),
            new MetricRow("wide_arch_width", WideArchWidth, "m", Design),
        };

        public static string MarkdownTable()
        {
            var sb = new StringBuilder("| Key | Value | Unit | Source |\n|---|---|---|---|\n");
            foreach (var r in Rows())
                sb.Append("| ").Append(r.Key).Append(" | ").Append(r.Value.ToString("0.##", CultureInfo.InvariantCulture)).Append(" | ").Append(r.Unit).Append(" | ").Append(r.Source).Append(" |\n");
            return sb.ToString();
        }
    }
}
