// M5 banking: the Core haul and dive state. Per-player loot plan task 3: pieces are safe on pickup (a failed dive keeps them, they never return to a floor) and DiveState owns the
// party's bags. Catches: loot counted as stir, a failed dive losing a collected piece or sending it back to its floor, floors un-clearing, banking at a floor with no bank point,
// a bag not failed / pushed on / brought home with the dive, two identical runs diverging.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class DiveHaulTests
    {
        private static DiveHaul Haul() => new DiveHaul(new[] { 2, 3, 1 });

        [Test] public void PiecesLeft_IsAssignedMinusCollected()
        {
            DiveHaul h = Haul();
            Assert.That(new[] { h.PiecesLeft(1), h.PiecesLeft(2), h.PiecesLeft(3), h.PiecesLeft(4) }, Is.EqualTo(new[] { 2, 3, 1, 0 }));
            Assert.That(h.AddPiece(DiveHaul.PieceId(2, 0)), Is.True);
            Assert.That(h.PiecesLeft(2), Is.EqualTo(2));
            h.Bank();
            Assert.That(h.PiecesLeft(2), Is.EqualTo(2), "banked pieces stay collected");
        }

        [Test] public void AddPiece_RefusesUnknownAndDuplicates()
        {
            DiveHaul h = Haul();
            Assert.That(h.AddPiece(999), Is.False);
            Assert.That(h.AddPiece(DiveHaul.PieceId(1, 0)), Is.True);
            Assert.That(h.AddPiece(DiveHaul.PieceId(1, 0)), Is.False);
            h.Bank();
            Assert.That(h.AddPiece(DiveHaul.PieceId(1, 0)), Is.False, "a banked piece cannot be found twice");
        }

        [Test] public void LootNeverCountsForStir()
        {
            DiveHaul h = Haul();
            h.AddLoot(7, 5);
            Assert.That(h.UnbankedLootCount, Is.EqualTo(5));
            Assert.That(h.BankedPieceCount, Is.EqualTo(0));
            h.AddPiece(DiveHaul.PieceId(1, 1));
            Assert.That(h.BankedPieceCount, Is.EqualTo(1), "a piece is safe the moment it is picked up");
            Assert.That(h.AddLoot(7, 0), Is.False);
        }

        [Test] public void Bank_MovesTheLegacyUnbankedLootToBanked_PiecesAreAlreadySafe()
        {
            DiveHaul h = Haul();
            h.AddPiece(DiveHaul.PieceId(1, 0)); h.AddLoot(7, 2); h.AddLoot(7, 1); h.AddLoot(9, 4);
            Assert.That(h.BankedPieceCount, Is.EqualTo(1));
            h.Bank();
            Assert.That(h.BankedPieceCount, Is.EqualTo(1));
            Assert.That(h.UnbankedLootCount, Is.EqualTo(0)); Assert.That(h.BankedLootCount, Is.EqualTo(7));
            Assert.That(h.LootBanked(7), Is.EqualTo(3));
            h.AddLoot(7, 2); h.Bank();
            Assert.That(h.LootBanked(7), Is.EqualTo(5), "banking accumulates");
        }

        [Test] public void AddPiece_IsSafeAtOnce_FailDiveKeepsIt()
        {
            DiveHaul h = Haul();
            int assigned = h.PiecesLeft(1);
            Assert.That(h.AddPiece(DiveHaul.PieceId(1, 0)), Is.True);
            h.FailDive();
            Assert.That(h.IsCollected(DiveHaul.PieceId(1, 0)), Is.True);
            Assert.That(h.IsBanked(DiveHaul.PieceId(1, 0)), Is.True);
            Assert.That(h.PiecesLeft(1), Is.EqualTo(assigned - 1));
            Assert.That(h.AddPiece(DiveHaul.PieceId(1, 0)), Is.False, "a kept piece cannot be found twice");
        }

        [Test] public void FailDive_DropsTheLegacyUnbankedLoot_KeepsEveryPieceAndTheBankedLoot()
        {
            DiveHaul h = Haul();
            h.AddPiece(DiveHaul.PieceId(1, 0)); h.AddLoot(7, 2); h.Bank();
            h.AddPiece(DiveHaul.PieceId(2, 0)); h.AddPiece(DiveHaul.PieceId(3, 0)); h.AddLoot(7, 9);
            Assert.That(h.PiecesLeft(2), Is.EqualTo(2));
            h.FailDive();
            Assert.That(h.UnbankedLootCount, Is.EqualTo(0));
            Assert.That(h.PiecesLeft(2), Is.EqualTo(2), "the piece found on floor 2 stays found");
            Assert.That(h.PiecesLeft(3), Is.EqualTo(0));
            Assert.That(h.PiecesLeft(1), Is.EqualTo(1));
            Assert.That(h.BankedPieceCount, Is.EqualTo(3)); Assert.That(h.LootBanked(7), Is.EqualTo(2));
        }

        [Test] public void RestoreBankedPiece_IsSafeButNotFoundThisDive()
        {
            DiveHaul h = Haul();
            Assert.That(h.RestoreBankedPiece(DiveHaul.PieceId(1, 0)), Is.True);
            Assert.That(h.RestoreBankedPiece(DiveHaul.PieceId(1, 0)), Is.False);
            Assert.That(h.RestoreBankedPiece(999), Is.False);
            Assert.That(h.BankedPieceCount, Is.EqualTo(1)); Assert.That(h.FoundThisDive, Is.EqualTo(0));
            h.AddPiece(DiveHaul.PieceId(1, 1));
            Assert.That(h.FoundThisDive, Is.EqualTo(1));
        }

        [Test] public void DiveState_ClearedFloorsStayCleared()
        {
            var s = new DiveState(Haul());
            Assert.That(s.MarkCleared(1), Is.True); s.MarkCleared(3);
            Assert.That(s.MarkCleared(0), Is.False); Assert.That(s.MarkCleared(5), Is.False);
            s.SetFloor(3); s.FailDive();
            Assert.That(s.CurrentFloor, Is.EqualTo(0));
            Assert.That(s.IsCleared(1) && !s.IsCleared(2) && s.IsCleared(3), Is.True);
            Assert.That(s.ClearedMask, Is.EqualTo(5));
        }

        [Test] public void DiveState_BankPointsAreFloor2AndFloor4()
        {
            var s = new DiveState(Haul());
            s.Haul.AddPiece(DiveHaul.PieceId(1, 0));
            foreach (int f in new[] { 0, 1, 3 }) { s.SetFloor(f); Assert.That(s.BankAtPoint(), Is.False, "floor " + f); }
            Assert.That(s.Haul.BankedPieceCount, Is.EqualTo(1), "safe on pickup, whether or not a bank was pressed");
            Assert.That(s.CheckpointReached, Is.False);
            s.SetFloor(2); Assert.That(s.BankAtPoint(), Is.True);
            Assert.That(s.CheckpointReached, Is.True); Assert.That(s.Haul.BankedPieceCount, Is.EqualTo(1));
            s.FailDive();
            Assert.That(s.CheckpointReached, Is.True, "the checkpoint survives a failed dive");
            s.Haul.AddPiece(DiveHaul.PieceId(2, 0)); s.SetFloor(4);
            Assert.That(s.BankAtPoint(), Is.True, "the heart is a bank point");
            Assert.That(s.SetFloor(5), Is.False); Assert.That(s.SetFloor(-1), Is.False);
        }

        [Test] public void DiveState_BankPointHealsOncePerDive_UnlessSomethingNewWaits()
        {
            var s = new DiveState(Haul());
            s.SetFloor(2);
            Assert.That(s.BankAtPoint(), Is.True, "the first press banks (and heals)");
            Assert.That(s.CanBankHere, Is.False);
            Assert.That(s.BankAtPoint(), Is.False, "bank spam with nothing new is refused");
            s.Haul.AddPiece(DiveHaul.PieceId(2, 0));
            Assert.That(s.BankAtPoint(), Is.False, "a piece is safe on pickup: it gives no second bank press");
            s.Bags.For("a").Add(104);
            Assert.That(s.BankAtPoint(), Is.False, "carrying something does not reopen a used bank point (it healed without limit)");
            Assert.That(s.CanBankHere, Is.False);
            s.FailDive(); s.SetFloor(2);
            Assert.That(s.BankAtPoint(), Is.True, "a new dive re-arms the bank point");
        }

        [Test] public void DiveState_AnOpenBankVisit_LetsThePointBankOnce_AnyBankOpen()
        {
            var s = new DiveState(Haul());
            s.SetFloor(2);
            Assert.That(s.BankAtPoint(), Is.True);
            Assert.That(s.Bags.AnyBankOpen, Is.False);
            PlayerBag a = s.Bags.For("a"); a.Add(104);
            a.OpenBank(1);                                       // a visit opens (a new bank room entry) after the point was used
            Assert.That(s.Bags.AnyBankOpen, Is.True);
            Assert.That(s.CanBankHere, Is.True, "an open visit has not been pushed on yet");
            Assert.That(s.BankAtPoint(), Is.True);
            Assert.That(a.BankedThisDive.ContainsKey(104), Is.True);
            Assert.That(s.Bags.AnyBankOpen, Is.False);
            Assert.That(s.BankAtPoint(), Is.False, "the visit is closed: nothing new waits");
        }

        [Test] public void FoundThisDive_ResetsWhenADiveStarts_NotWhenItEnds()
        {
            var s = new DiveState(Haul());
            s.SetFloor(1);
            s.Haul.AddPiece(DiveHaul.PieceId(1, 0));
            Assert.That(s.Haul.FoundThisDive, Is.EqualTo(1));
            s.SetFloor(DiveState.Town);
            Assert.That(s.Haul.FoundThisDive, Is.EqualTo(1), "the dive's summary still shows what it found");
            s.SetFloor(1);
            Assert.That(s.Haul.FoundThisDive, Is.EqualTo(0), "a new dive starts at 0 (the piece itself stays collected)");
            Assert.That(s.Haul.IsCollected(DiveHaul.PieceId(1, 0)), Is.True);
            s.Haul.AddPiece(DiveHaul.PieceId(1, 1)); s.SetFloor(2);
            Assert.That(s.Haul.FoundThisDive, Is.EqualTo(1), "moving between floors does not reset it");
        }

        [Test] public void FailDive_FailsEveryBagAtTheCurrentFloor()
        {
            var s = new DiveState(Haul());
            s.SetFloor(3);
            s.Bags.For("a").Add(104);
            s.Bags.For("b").Add(110); s.Bags.For("b").AddGold(8);
            s.FailDive();
            Assert.That(s.CurrentFloor, Is.EqualTo(DiveState.Town));
            PlayerBag a = s.Bags.For("a");
            Assert.That(a.Satchel.Waiting.Floor, Is.EqualTo(3), "the floor before it reset");
            Assert.That(a.Satchel.Waiting.Items, Is.EqualTo(new[] { 104 }));
            Assert.That(a.Carried, Is.Empty);
            Assert.That(s.Bags.For("b").Gold, Is.EqualTo(4)); Assert.That(s.Bags.For("b").Satchel.Waiting.Items, Is.EqualTo(new[] { 110 }));
        }

        [Test] public void BankAtPoint_PushesOnOpenBags_BankInTownBringsAllHome()
        {
            var s = new DiveState(Haul());
            s.SetFloor(2);
            PlayerBag a = s.Bags.For("a"), b = s.Bags.For("b");
            a.Add(101); a.Add(112); b.Add(104);
            a.OpenBank(1);                                   // b has no open visit
            Assert.That(s.BankAtPoint(), Is.True);
            Assert.That(a.BankedThisDive.ContainsKey(112) && a.BankedThisDive.Count == 1, Is.True, "the best item fills the free slot");
            Assert.That(a.Carried, Is.EqualTo(new[] { 101 }));
            Assert.That(b.Carried, Is.EqualTo(new[] { 104 }), "a bag with no open visit is untouched");
            a.AddGold(6);
            s.SetFloor(DiveState.Town);
            Assert.That(s.BankInTown(), Is.True);
            Assert.That(a.Carried, Is.Empty); Assert.That(b.Carried, Is.Empty);
            Assert.That(a.Stash, Is.EqualTo(new Dictionary<int, int> { { 101, 1 }, { 112, 1 } }));
            Assert.That(b.Stash, Is.EqualTo(new Dictionary<int, int> { { 104, 1 } }));
            Assert.That(a.Gold, Is.EqualTo(6));
        }

        [Test] public void DiveState_StartFloor_ReturnDiveOnlyWhenFloors1And2AreDone()
        {
            var s = new DiveState(Haul());
            s.SetFloor(2); s.BankAtPoint();
            Assert.That(s.CheckpointReached, Is.True);
            Assert.That(s.StartFloor, Is.EqualTo(1), "pieces are still on floors 1-2");
            foreach (int f in new[] { 1, 2 }) for (int i = 0; i < s.Haul.PiecesLeft(f) + 10; i++) s.Haul.AddPiece(DiveHaul.PieceId(f, i));
            Assert.That(s.Haul.PiecesLeft(1) + s.Haul.PiecesLeft(2), Is.EqualTo(0));
            Assert.That(s.StartFloor, Is.EqualTo(3));
        }

        [Test] public void DiveState_BankInTownNeedsTown()
        {
            var s = new DiveState(Haul());
            s.Haul.AddPiece(DiveHaul.PieceId(1, 0));
            s.SetFloor(1); Assert.That(s.BankInTown(), Is.False);
            s.SetFloor(0); Assert.That(s.BankInTown(), Is.True);
            Assert.That(s.Haul.BankedPieceCount, Is.EqualTo(1)); Assert.That(s.CheckpointReached, Is.False);
        }

        [Test] public void SameOperations_SameState()
        {
            static string Run()
            {
                var s = new DiveState(new DiveHaul(new[] { 2, 2, 2 }));
                s.SetFloor(1); s.Haul.AddPiece(100); s.Haul.AddLoot(3, 2); s.MarkCleared(1);
                s.SetFloor(2); s.Haul.AddPiece(200); s.BankAtPoint(); s.Haul.AddPiece(300); s.FailDive();
                return string.Join(",", s.Haul.BankedPieces) + "|" + s.Haul.PiecesLeft(1) + s.Haul.PiecesLeft(2) + s.Haul.PiecesLeft(3) + "|" + s.ClearedMask + s.CheckpointReached;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
            Assert.That(Run(), Is.EqualTo("100,200,300|111|1True"));
        }
    }
}
