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

        // The HUD line of the local player's own bag: carried count, items safe this dive, and gold = home gold + this dive's gold.
        [Test]
        public void Bag_LineShowsCarriedSafeAndGold()
        {
            var v = new BagView { Gold = 5, DiveGold = 7, BankedThisDive = 2 };
            v.Carried.Add(101); v.Carried.Add(104); v.Carried.Add(110);
            Assert.AreEqual("Bag: 3 carried, 2 safe, 12 gold", StirMeterText.Bag(v));
            Assert.AreEqual("Bag: 0 carried, 0 safe, 0 gold", StirMeterText.Bag(default));
        }

        // Pieces are safe on pickup and go to the town stock at once: the line says how many the party has found so far.
        [Test] public void Pieces_Text() => Assert.AreEqual("Pieces found: 3", StirMeterText.Pieces(new DiveHaulSync.Summary { FoundPieces = 3 }));
        [Test] public void Pieces_TextWithNothingFoundYet() => Assert.AreEqual("Pieces found: 0", StirMeterText.Pieces(new DiveHaulSync.Summary()));

        [Test]
        public void PiecesLeftHere_FollowsTheCurrentFloor()
        {
            var s = new DiveHaulSync.Summary { CurrentFloor = 2, PiecesLeft = 1 };
            Assert.AreEqual("Pieces left on this floor: 1", StirMeterText.PiecesLeftHere(s));
            s.PiecesLeft = 4; Assert.AreEqual("Pieces left on this floor: 4", StirMeterText.PiecesLeftHere(s));
            s.PiecesLeft = 0; s.PeopleLeft = 1; Assert.AreEqual("Someone is waiting on this floor", StirMeterText.PiecesLeftHere(s));
            s.PeopleLeft = 0; Assert.IsNull(StirMeterText.PiecesLeftHere(s));
            s.PiecesLeft = 2; s.CurrentFloor = 0; Assert.IsNull(StirMeterText.PiecesLeftHere(s));
        }

        [Test]
        public void Marks_FromDiveStir()
        {
            var m = StirMeterText.MarkFractions();
            Assert.AreEqual(DiveStir.ExtraEnemyThreshold / DiveStir.Max, m[0], 1e-4f);
            Assert.AreEqual(DiveStir.HunterThreshold / DiveStir.Max, m[1], 1e-4f);
            Assert.AreEqual(DiveStir.DeepClosesThreshold / DiveStir.Max, m[2], 1e-4f);
        }

        // Catches: the HUD bar still drawn from the retired stir tuning (its Max, its marks) instead of the DiveStir the host runs: the fill is Value / DiveStir.Max and a full bar is the collapse threshold.
        [Test]
        public void Fill_IsDiveStirValueOverItsMax()
        {
            var stir = new DiveStir { PlayersPresent = 1, UnbankedValuables = 8 };   // 8 x 5 = 40: the extra-enemy mark
            Assert.AreEqual(0.4f, StirMeterText.Fill(stir.Value, DiveStir.Max), 1e-4f);
            Assert.AreEqual(1f, StirMeterText.Fill(StirMeterText.MarkFractions()[2] * DiveStir.Max, DiveStir.Max), 1e-4f, "the third mark is a full bar: the collapse");
        }

        [Test]
        public void Summary_CarriesTheRewardRankBonus_AndTheCollapseSeconds()
        {
            var s = new DiveHaulSync.Summary { RewardRankBonus = 2, DeepSecondsLeft = 9 };
            Assert.That(s.Equals(new DiveHaulSync.Summary { RewardRankBonus = 2, DeepSecondsLeft = 9 }), Is.True);
            Assert.That(s.Equals(new DiveHaulSync.Summary { RewardRankBonus = 1, DeepSecondsLeft = 9 }), Is.False, "a changed reward bonus republishes");
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
