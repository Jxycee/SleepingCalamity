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

        // Pieces are safe on pickup (per-player loot plan task 3): the line shows the safe count only. Task 4 reworks it to "found this dive" / "home".
        [Test] public void Pieces_Text() => Assert.AreEqual("Pieces: 2 safe", StirMeterText.Pieces(new DiveHaulSync.Summary { UnbankedPieces = 0, BankedPieces = 2 }));

        [Test]
        public void PiecesLeftHere_FollowsTheCurrentFloor()
        {
            var s = new DiveHaulSync.Summary { CurrentFloor = 2, LeftFloor1 = 0, LeftFloor2 = 1, LeftFloor3 = 2 };
            Assert.AreEqual("Pieces left on this floor: 1", StirMeterText.PiecesLeftHere(s));
            s.CurrentFloor = 3; Assert.AreEqual("Pieces left on this floor: 2", StirMeterText.PiecesLeftHere(s));
            s.CurrentFloor = 4; Assert.IsNull(StirMeterText.PiecesLeftHere(s));
            s.CurrentFloor = 0; Assert.IsNull(StirMeterText.PiecesLeftHere(s));
        }

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
