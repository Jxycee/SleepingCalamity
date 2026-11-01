using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.UI;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class StirMeterHudTests
    {
        [Test] public void NoLatches_NoLabels() => Assert.That(StirMeterText.Labels(default), Is.Empty);

        [Test]
        public void LabelsFollowLatchFlags_NotValue()
        {
            var s = new DiveHaulSync.Summary { StirValue = 100, ExtraEnemiesPerRoom = 1 };
            Assert.That(StirMeterText.Labels(s), Is.EqualTo(new[] { "+1 enemy per room" }));
            s.HunterLatched = true; s.DeepClosesLatched = true; s.DeepSecondsLeft = 42;
            Assert.That(StirMeterText.Labels(s), Is.EqualTo(new[] { "+1 enemy per room", "The rat-king hunts", "The deep closes: 42s" }));
            s.DeepSecondsLeft = 0;
            Assert.That(StirMeterText.Labels(s).Count, Is.EqualTo(2));
        }

        [Test] public void Pieces_Text() => Assert.AreEqual("Pieces: 3 unbanked / 2 banked", StirMeterText.Pieces(new DiveHaulSync.Summary { UnbankedPieces = 3, BankedPieces = 2 }));

        [Test]
        public void Marks_FromTuning()
        {
            var t = StirTuning.Default;
            var m = StirMeterText.MarkFractions(t);
            Assert.AreEqual(t.ExtraEnemyThreshold / t.Max, m[0], 1e-4f);
            Assert.AreEqual(t.HunterThreshold / t.Max, m[1], 1e-4f);
            Assert.AreEqual(t.DeepClosesThreshold / t.Max, m[2], 1e-4f);
        }

        [Test]
        public void HiddenWithoutSync()
        {
            var go = new GameObject("hud");
            try { Assert.IsFalse(go.AddComponent<StirMeterHud>().Visible); }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
