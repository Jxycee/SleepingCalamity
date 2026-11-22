// Tests/EditMode/BankRoomRulesTests.cs: the bank room's pure rules (plan 3 task 8): the shared push-on / go-home confirm window (BankChoice), capacity that follows presence
// (DiveSession.ResizeBankRoom), the empty-slot rule (no auto-fill) and momentum's rank bonus into the chest roll. Pure Core + NUnit (runs under Tools/cloud/sc_core_tests.py).
// Catches: a confirm window that confirms on the same player's second press, never times out, ignores a swap or counts a downed player; capacity that does not shrink or grow with the party,
// a leaver's picks still being banked, a rejoiner without a visit, the 3-player extra slot repeating its previous owner; the second half's chests not rolling a rank higher after the push-on.
// Does NOT catch: the host wiring (DiveHaulNetworkTests), the roster, RPCs or late join (PlayMode and the manual 2-PC walk).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class BankRoomRulesTests
    {
        static readonly string[] Ids = { "a", "b", "c", "d" };

        // ---------------------------------------------------------------- the confirm window
        [Test]
        public void ConfirmWindow_Solo_ConfirmsAtOnce()
        {
            var w = new BankChoice();
            Assert.That(w.Press("a", BankChoiceKind.PushOn, 1), Is.EqualTo(BankChoicePress.Confirmed));
            Assert.That(w.ConfirmedKind, Is.EqualTo(BankChoiceKind.PushOn));
            Assert.That(w.IsOpen, Is.False);
        }

        [Test]
        public void ConfirmWindow_FirstPressOpens_SamePlayerAgainIsIgnored_SecondPlayerConfirms()
        {
            var w = new BankChoice();
            Assert.That(w.Press("a", BankChoiceKind.GoHome, 3), Is.EqualTo(BankChoicePress.Opened));
            Assert.That(w.Kind, Is.EqualTo(BankChoiceKind.GoHome));
            Assert.That(w.SecondsLeft, Is.EqualTo(BankChoice.WindowSeconds));
            Assert.That(w.Press("a", BankChoiceKind.GoHome, 3), Is.EqualTo(BankChoicePress.Ignored), "one player cannot confirm alone");
            Assert.That(w.IsOpen, Is.True);
            Assert.That(w.Press("b", BankChoiceKind.GoHome, 3), Is.EqualTo(BankChoicePress.Confirmed));
            Assert.That(w.ConfirmedKind, Is.EqualTo(BankChoiceKind.GoHome));
            Assert.That(w.IsOpen, Is.False);
        }

        [Test]
        public void ConfirmWindow_TimerConfirms_ThePendingChoice()
        {
            var w = new BankChoice();
            var here = new List<string> { "a", "b" };
            w.Press("a", BankChoiceKind.PushOn, 2);
            Assert.That(w.Tick(BankChoice.WindowSeconds - 1f, here), Is.EqualTo(BankChoiceKind.None));
            Assert.That(w.IsOpen, Is.True);
            Assert.That(w.Tick(1.01f, here), Is.EqualTo(BankChoiceKind.PushOn), "the timer confirms what the window held");
            Assert.That(w.IsOpen, Is.False);
            Assert.That(w.Tick(5f, here), Is.EqualTo(BankChoiceKind.None), "nothing left to confirm");
        }

        [Test]
        public void ConfirmWindow_OtherChoiceSwaps_AndRestartsTheTimer()
        {
            var w = new BankChoice();
            var here = new List<string> { "a", "b" };
            w.Press("a", BankChoiceKind.PushOn, 2);
            w.Tick(6f, here);
            Assert.That(w.Press("b", BankChoiceKind.GoHome, 2), Is.EqualTo(BankChoicePress.Swapped));
            Assert.That(w.Kind, Is.EqualTo(BankChoiceKind.GoHome));
            Assert.That(w.Pressers, Is.EqualTo(new[] { "b" }), "the swap keeps only the new presser");
            Assert.That(w.SecondsLeft, Is.EqualTo(BankChoice.WindowSeconds), "and restarts the timer");
            Assert.That(w.Press("a", BankChoiceKind.GoHome, 2), Is.EqualTo(BankChoicePress.Confirmed));
            Assert.That(w.ConfirmedKind, Is.EqualTo(BankChoiceKind.GoHome));
        }

        [Test]
        public void ConfirmWindow_DownedOrAbsentPlayersAreNotCounted()
        {
            var w = new BankChoice();
            // a pressed; then a goes down: no presser is left, the window closes
            w.Press("a", BankChoiceKind.PushOn, 3);
            Assert.That(w.Tick(0.1f, new List<string> { "b", "c" }), Is.EqualTo(BankChoiceKind.None));
            Assert.That(w.IsOpen, Is.False, "a downed presser's press is withdrawn");

            // a pressed, b and c go down: a is the only living player, the quorum is one, so the press stands
            w.Press("a", BankChoiceKind.GoHome, 3);
            Assert.That(w.Tick(0.1f, new List<string> { "a" }), Is.EqualTo(BankChoiceKind.GoHome));

            // the quorum is the living present players: with three here and one downed, two living players are two presses
            w.Press("a", BankChoiceKind.PushOn, 2);
            Assert.That(w.IsOpen, Is.True, "two living players: one press is not enough");
            Assert.That(w.Press("b", BankChoiceKind.PushOn, 2), Is.EqualTo(BankChoicePress.Confirmed));
        }

        [Test]
        public void ConfirmWindow_PressForNoChoice_OrFromNobody_IsIgnored()
        {
            var w = new BankChoice();
            Assert.That(w.Press("a", BankChoiceKind.None, 2), Is.EqualTo(BankChoicePress.Ignored));
            Assert.That(w.Press(null, BankChoiceKind.PushOn, 2), Is.EqualTo(BankChoicePress.Ignored));
            Assert.That(w.IsOpen, Is.False);
        }

        // ---------------------------------------------------------------- empty slots stay empty
        [Test]
        public void PushOn_EmptySlotsStayEmpty()
        {
            var bags = new PartyBags();
            bags.For("a").Add(101); bags.For("a").Add(112);
            DiveSession s = AtBank(1, bags);
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(3));
            Assert.That(s.PushOn(), Is.True);
            Assert.That(bags.For("a").BankedThisDive, Is.Empty, "no pick, nothing banked: the three free slots were not filled for the player");
            Assert.That(bags.For("a").Carried, Is.EqualTo(new[] { 101, 112 }));
        }

        // ---------------------------------------------------------------- capacity follows presence
        static List<int> Slots(params int[] s) => new List<int>(s);
        static string[] IdsOf(params int[] slots) { var a = new string[slots.Length]; for (int i = 0; i < slots.Length; i++) a[i] = Ids[slots[i]]; return a; }

        static DiveSession AtBank(int n, PartyBags bags, ulong seed = 12345UL, int previous = -1)
        {
            var s = new DiveSession(bags, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            Assert.That(s.TryStart(1, false), Is.True);
            for (int f = 1; f < UndercroftPlan.BankFloor; f++) { s.ClearFloor(f); Assert.That(s.EnterFloor(f + 1), Is.True); }
            s.ClearFloor(UndercroftPlan.BankFloor);
            var slots = new List<int>(); for (int i = 0; i < n; i++) slots.Add(i);
            Assert.That(s.EnterBankRoom(slots, seed, previous, IdsOf(slots.ToArray())), Is.True);
            return s;
        }

        [Test]
        public void ResizeBankRoom_PlayerLeaves_ShrinksCapacityAndUnpicksExcess()
        {
            var bags = new PartyBags();
            foreach (string id in Ids) { bags.For(id).Add(101); bags.For(id).Add(102); bags.For(id).Add(103); }
            DiveSession s = AtBank(2, bags);   // two players: two slots each
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(2));
            Assert.That(bags.For("a").Pick(0), Is.True); Assert.That(bags.For("a").Pick(1), Is.True);
            Assert.That(bags.For("b").Pick(2), Is.True);

            // b leaves: a is alone, three slots; nothing of a's is lost
            Assert.That(s.ResizeBankRoom(Slots(0), IdsOf(0), 12345UL, -1), Is.True);
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(3));
            Assert.That(bags.For("a").Picks, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(bags.For("b").BankOpen, Is.False, "the leaver's visit is cancelled ...");
            Assert.That(bags.For("b").Picks, Is.Empty, "... so a pick of theirs is not banked behind their back");
            Assert.That(bags.For("b").Carried.Count, Is.EqualTo(3), "their items stay carried");

            // the party grows to four: a's two picks keep their slots (never taken away), the joiners share what remains of the four slots
            Assert.That(s.ResizeBankRoom(Slots(0, 1, 2, 3), IdsOf(0, 1, 2, 3), 12345UL, -1), Is.True);
            foreach (string id in Ids) Assert.That(bags.For(id).BankOpen, Is.True, id);
            Assert.That(bags.For("a").Picks, Is.EqualTo(new[] { 0, 1 }), "a pick is never dropped by a resize");
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(2));
            Assert.That(TotalCapacity(bags, 4), Is.EqualTo(BankSlots.TotalSlots(4)), "the party never goes above the total");
            Assert.That(s.ExtraSlotOwner, Is.EqualTo(-1), "four players: no extra slot");
        }

        static int TotalCapacity(PartyBags bags, int n) { int t = 0; for (int i = 0; i < n; i++) t += bags.For(Ids[i]).SlotCapacity; return t; }

        // PARKING_LOT 2026-11-03 "party shrinks": four players drop to two, the two share the full TotalSlots(2) and keep every pick.
        [Test]
        public void ResizeBankRoom_PartyShrinks_PresentPlayersShareTheFullTotal_PicksKept()
        {
            var bags = new PartyBags();
            foreach (string id in Ids) { bags.For(id).Add(101); bags.For(id).Add(102); }
            DiveSession s = AtBank(4, bags);
            Assert.That(bags.For("a").Pick(0), Is.True);
            Assert.That(bags.For("b").Pick(1), Is.True);
            Assert.That(s.ResizeBankRoom(Slots(0, 1), IdsOf(0, 1), 5UL, -1), Is.True);
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(2));
            Assert.That(bags.For("b").SlotCapacity, Is.EqualTo(2));
            Assert.That(TotalCapacity(bags, 2), Is.EqualTo(BankSlots.TotalSlots(2)));
            Assert.That(bags.For("a").Picks, Is.EqualTo(new[] { 0 }));
            Assert.That(bags.For("b").Picks, Is.EqualTo(new[] { 1 }));
        }

        // PARKING_LOT 2026-11-03 "late joiner": two players with all four slots picked, a third joins. The pickers keep every pick; the joiner is sized from what remains (none), so the party stays at 4.
        [Test]
        public void ResizeBankRoom_LateJoiner_NeverLiftsThePartyAboveTheTotal_AndNeverTakesAPick()
        {
            var bags = new PartyBags();
            for (int i = 0; i < 3; i++) { bags.For(Ids[i]).Add(101); bags.For(Ids[i]).Add(102); }
            DiveSession s = AtBank(2, bags);
            foreach (string id in new[] { "a", "b" }) { Assert.That(bags.For(id).Pick(0), Is.True); Assert.That(bags.For(id).Pick(1), Is.True); }
            Assert.That(s.ResizeBankRoom(Slots(0, 1, 2), IdsOf(0, 1, 2), 9UL, -1), Is.True);
            Assert.That(bags.For("a").Picks.Count, Is.EqualTo(2));
            Assert.That(bags.For("b").Picks.Count, Is.EqualTo(2));
            Assert.That(bags.For("c").BankOpen, Is.True);
            Assert.That(bags.For("c").SlotCapacity, Is.EqualTo(0), "nothing left for the joiner");
            Assert.That(TotalCapacity(bags, 3), Is.EqualTo(BankSlots.TotalSlots(3)));
        }

        // The joiner takes the unused slots of others (a solo player with one pick of three keeps it and two free slots; the joiner gets one of them).
        [Test]
        public void ResizeBankRoom_LateJoiner_TakesOnlyUnusedSlots()
        {
            var bags = new PartyBags();
            bags.For("a").Add(101); bags.For("b").Add(102);
            DiveSession s = AtBank(1, bags);
            Assert.That(bags.For("a").Pick(0), Is.True);
            Assert.That(s.ResizeBankRoom(Slots(0, 1), IdsOf(0, 1), 3UL, -1), Is.True);
            Assert.That(bags.For("a").Picks, Is.EqualTo(new[] { 0 }));
            Assert.That(TotalCapacity(bags, 2), Is.EqualTo(BankSlots.TotalSlots(2)));
            Assert.That(bags.For("a").SlotCapacity, Is.EqualTo(2));
            Assert.That(bags.For("b").SlotCapacity, Is.EqualTo(2));
        }

        [Test]
        public void ResizeBankRoom_Rejoiner_GetsAVisit()
        {
            var bags = new PartyBags();
            bags.For("a").Add(101); bags.For("b").Add(102);
            DiveSession s = AtBank(2, bags);
            Assert.That(s.ResizeBankRoom(Slots(0), IdsOf(0), 1UL, -1), Is.True);
            Assert.That(bags.For("b").BankOpen, Is.False);
            Assert.That(s.ResizeBankRoom(Slots(0, 1), IdsOf(0, 1), 1UL, -1), Is.True);
            Assert.That(bags.For("b").BankOpen, Is.True, "the rejoiner gets a visit");
            Assert.That(bags.For("b").SlotCapacity, Is.EqualTo(2));
            Assert.That(bags.For("b").Pick(0), Is.True);
        }

        [Test]
        public void ResizeBankRoom_ExtraOwnerLeaves_RepicksButNeverThePreviousOwner()
        {
            var bags = new PartyBags();
            const int previous = 2;
            DiveSession s = AtBank(3, bags, 777UL, previous);
            int owner = s.ExtraSlotOwner;
            Assert.That(owner, Is.InRange(0, 2));
            Assert.That(owner, Is.Not.EqualTo(previous), "never the previous owner");
            Assert.That(bags.For(Ids[owner]).SlotCapacity, Is.EqualTo(2));

            // the owner leaves and a fourth player takes the seat: three present again, the extra slot is picked again among them, still skipping the previous owner
            var slots = new List<int> { 0, 1, 2, 3 }; slots.Remove(owner);
            Assert.That(s.ResizeBankRoom(slots, IdsOf(slots.ToArray()), 777UL, previous), Is.True);
            Assert.That(s.ExtraSlotOwner, Is.Not.EqualTo(owner), "the owner left");
            Assert.That(slots, Does.Contain(s.ExtraSlotOwner));
            Assert.That(s.ExtraSlotOwner, Is.Not.EqualTo(previous));
            int extras = 0, total = 0;
            foreach (int slot in slots) { int c = bags.For(Ids[slot]).SlotCapacity; total += c; if (c == 2) extras++; }
            Assert.That(extras, Is.EqualTo(1), "one extra slot");
            Assert.That(total, Is.EqualTo(BankSlots.TotalSlots(3)));

            // a resize that keeps the owner present keeps the owner
            int keep = s.ExtraSlotOwner;
            Assert.That(s.ResizeBankRoom(slots, IdsOf(slots.ToArray()), 999UL, previous), Is.True);
            Assert.That(s.ExtraSlotOwner, Is.EqualTo(keep));
        }

        [Test]
        public void ResizeBankRoom_KeepsPicksOfARestoredVisit_AndRefusesOutsideTheRoom()
        {
            var bags = new PartyBags();
            bags.For("a").Add(101); bags.For("a").Add(102);
            DiveSession s = AtBank(1, bags);
            Assert.That(bags.For("a").Pick(1), Is.True);
            Assert.That(s.ResizeBankRoom(Slots(0), IdsOf(0), 1UL, -1), Is.True);
            Assert.That(bags.For("a").Picks, Is.EqualTo(new[] { 1 }), "a resize keeps the picks that still fit");
            Assert.That(s.ResizeBankRoom(Slots(), new string[0], 1UL, -1), Is.False, "nobody present");
            Assert.That(s.PushOn(), Is.True);
            Assert.That(s.ResizeBankRoom(Slots(0), IdsOf(0), 1UL, -1), Is.False, "the room is closed after the push-on");
        }

        // ---------------------------------------------------------------- momentum
        [Test]
        public void Momentum_SecondHalfChests_RollOneRankHigher_AfterPushOn()
        {
            var m = new DiveMomentum();
            m.OnDiveStart(1);
            int before = m.ChestRankBonus(6);
            m.OnPushOn();
            int after = m.ChestRankBonus(6);
            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.EqualTo(1));
            Assert.That(m.ChestRankBonus(4), Is.EqualTo(0), "first-half floors never get it");

            int higher = 0;
            for (uint seed = 1; seed <= 200; seed++)
                for (int chest = 0; chest < 4; chest++)
                {
                    int rankBefore = CellarLoot.RankOf(CellarLoot.RollFor(seed, 6, chest, 0, before));
                    int rankAfter = CellarLoot.RankOf(CellarLoot.RollFor(seed, 6, chest, 0, after));
                    Assert.That(rankAfter, Is.EqualTo(System.Math.Min(CellarLoot.MaxRank, rankBefore + 1)), "seed " + seed + " chest " + chest + ": exactly one rank up, capped at the top");
                    if (rankAfter > rankBefore) higher++;
                }
            Assert.That(higher, Is.GreaterThan(0));
        }
    }
}
