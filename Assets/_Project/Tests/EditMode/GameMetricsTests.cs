// Tests/EditMode/GameMetricsTests.cs: GameMetrics and MovementProbe measure the hero's real numbers in pure Core (metrics gym plan, Task 1). Core-only.
using System;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class GameMetricsTests
    {
        [Test] public void Dodge_IsConstantSpeedForItsDuration() =>
            Assert.That(MovementProbe.DodgeDistance(), Is.EqualTo(MovementMath.DodgeSpeed * MovementMath.DodgeDuration).Within(0.15f));

        [Test] public void JumpApex_MatchesJumpHeight() =>
            Assert.That(MovementProbe.JumpApex(), Is.EqualTo(MovementMath.JumpHeight).Within(0.05f));

        [Test] public void RunJump_IsShorterThanOneCell()
        {
            float d = MovementProbe.RunJumpDistance();
            Assert.That(d, Is.InRange(3.0f, 4.0f));            // sprint drops in the air: about 3.6 m
            Assert.That(d, Is.LessThan(GameMetrics.CellMeters));
        }

        [Test] public void Boots_RaiseRunJumpByLessThanHalfAMetre() =>   // spec 5: +5% run speed must not open new gaps
            Assert.That(MovementProbe.RunJumpDistance(MovementMath.RunSpeed * 1.05f) - MovementProbe.RunJumpDistance(), Is.InRange(0f, 0.5f));

        [Test] public void OneLevel_IsAboveTheClimbBound() =>
            Assert.That(GameMetrics.LevelHeightCm / 100f, Is.GreaterThan(GameMetrics.MaxClimbableLedge + 0.3f));

        [Test] public void Stair_FitsTheController()
        {
            Assert.That(GameMetrics.StairRiseCm / 100f, Is.LessThanOrEqualTo(MovementMath.StepOffset));
            Assert.That(Math.Atan2(GameMetrics.StairRiseCm, GameMetrics.StairRunCm) * 180 / Math.PI, Is.LessThan(MovementMath.SlopeLimit));
            Assert.That(GameMetrics.StepsPerLevel * GameMetrics.StairRunCm, Is.LessThanOrEqualTo(GameMetrics.CellMeters * 100));
        }

        [Test] public void Rows_HaveUniqueKeysAndSources()
        {
            var rows = GameMetrics.Rows();
            Assert.That(rows.Select(r => r.Key).Distinct().Count(), Is.EqualTo(rows.Count));
            foreach (var r in rows)
            {
                Assert.That(r.Key, Is.Not.Empty);
                Assert.That(r.Unit, Is.Not.Empty, r.Key);
                Assert.That(r.Source, Is.Not.Empty, r.Key);
            }
            var keys = rows.Select(r => r.Key).ToList();
            Assert.That(keys, Does.Contain("dodge_distance"));
            Assert.That(keys, Does.Contain("run_jump_distance"));
            Assert.That(keys, Does.Contain("level_height"));
        }

        [Test] public void MarkdownTable_HasHeaderAndOneLinePerRow()
        {
            var lines = GameMetrics.MarkdownTable().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToList();
            Assert.That(lines[0], Is.EqualTo("| Key | Value | Unit | Source |"));
            Assert.That(lines.Count, Is.GreaterThanOrEqualTo(GameMetrics.Rows().Count + 1));
        }
    }
}
