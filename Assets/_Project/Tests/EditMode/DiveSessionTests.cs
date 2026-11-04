// Assets/_Project/Tests/EditMode/DiveSessionTests.cs: tests for SleepingCalamity.Core.DiveSession (plan 3 task 2; spec decisions 13, 19, 25, 31, 32, 36-38).
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveSessionTests
    {
        const int Large1 = 101, Large2 = 102, Large3 = 103;
        const int UsableStandIn = 5000;   // an id outside the loot table reads as Usable: stands in for boots until Task 5 adds them

        static readonly string[] Ids = { "a", "b", "c", "d" };

        static DiveSession NewSession(PartyBags bags = null, DiveCheckpoint cp = null)
            => new DiveSession(bags ?? new PartyBags(), cp ?? new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));

        static string[] IdsFor(int n) { var a = new string[n]; System.Array.Copy(Ids, a, n); return a; }

        static List<int> Slots(int n) { var l = new List<int>(); for (int i = 0; i < n; i++) l.Add(i); return l; }

        // Walks floor 1 to the cleared floor 5 and opens the bank room for n players.
        static DiveSession AtBank(int n, PartyBags bags = null, DiveCheckpoint cp = null)
        {
            DiveSession s = NewSession(bags, cp);
            Assert.That(s.TryStart(1, false), Is.True);
            for (int f = 1; f < UndercroftPlan.BankFloor; f++) { s.ClearFloor(f); Assert.That(s.EnterFloor(f + 1), Is.True); }
            s.ClearFloor(UndercroftPlan.BankFloor);
            Assert.That(s.EnterBankRoom(Slots(n), 12345UL, -1, IdsFor(n)), Is.True);
            return s;
        }

        [Test]
        public void TryStart_CheckpointRules()
        {
            var cp = new DiveCheckpoint();
            var bags = new PartyBags();
            Assert.That(NewSession(bags, cp).TryStart(3, false), Is.False, "no checkpoint");

            DiveSession first = AtBank(1, bags, cp);
            Assert.That(first.GoHome(), Is.True);
            Assert.That(cp.Held, Is.True);

            DiveSession second = NewSession(bags, cp);
            Assert.That(second.TryStart(3, false), Is.True);
            Assert.That(cp.Held, Is.False, "spent");
            Assert.That(second.Floor, Is.EqualTo(3));

            Assert.That(NewSession(bags, cp).TryStart(1, false), Is.True, "floor 1 is always allowed");
            Assert.That(cp.Held, Is.False);

            cp.Restore(true);
            Assert.That(NewSession(bags, cp).TryStart(1, false), Is.True);
            Assert.That(cp.Held, Is.False, "DiveCheckpoint spends it whatever floor is picked (plan 1 rule, pinned by DiveCheckpointTests)");
            cp.Restore(true);

            Assert.That(NewSession(bags, cp).TryStart(5, true), Is.True, "boss beaten: the shortcut");
            Assert.That(cp.Held, Is.True, "spends nothing");
        }

        [Test]
        public void TryStart_Floor5_StartsAtTheBank()
        {
            var bags = new PartyBags();
            var s = NewSession(bags);
            Assert.That(s.TryStart(5, true), Is.True);
            Assert.That(s.Phase, Is.EqualTo(DivePhase.Floor));
            Assert.That(s.Floor, Is.EqualTo(5));
            Assert.That(s.StartedAtBank, Is.True);
            Assert.That(s.IsCleared(5), Is.True);
            Assert.That(s.Stir.Value, Is.EqualTo(0f));
            Assert.That(s.EnterBankRoom(Slots(1), 1UL, -1, new[] { "a" }), Is.True, "allowed at once");
            Assert.That(s.GoHome(), Is.True);
            Assert.That(s.Ended, Is.EqualTo(DiveEnd.WentHome));
            Assert.That(bags.For("a").Carried, Is.Empty);
        }

        [Test]
        public void GoHome_RightAfterArrivingAtTheBank_WithoutEnteringTheRoom()
        {
            var s = NewSession();
            s.TryStart(5, true);
            Assert.That(s.GoHome(), Is.True);
            Assert.That(s.Checkpoint.Held, Is.True);
        }

        [Test]
        public void Fail_AfterPushOn_StartsAtFloor1()
        {
            var bags = new PartyBags();
            var cp = new DiveCheckpoint();
            DiveSession s = AtBank(1, bags, cp);
            Assert.That(s.PushOn(), Is.True);
            s.Fail();
            Assert.That(s.Ended, Is.EqualTo(DiveEnd.Failed));
            Assert.That(NewSession(bags, cp).TryStart(3, false), Is.False, "decision 31: a failed dive leaves no checkpoint");
            Assert.That(NewSession(bags, cp).TryStart(1, false), Is.True);
        }

        [Test]
        public void GoHome_FromBank_BringsEveryBagHome()
        {
            var bags = new PartyBags();
            bags.For("a").Add(Large1);
            bags.For("a").Add(UsableStandIn);
            bags.For("b").Add(Large2);
            DiveSession s = AtBank(2, bags);
            Assert.That(s.GoHome(), Is.True);
            Assert.That(bags.For("a").Carried, Is.Empty);
            Assert.That(bags.For("b").Carried, Is.Empty);
            Assert.That(bags.For("a").Stash[Large1], Is.EqualTo(1));
            Assert.That(bags.For("a").Stash[UsableStandIn], Is.EqualTo(1));
            Assert.That(bags.For("b").Stash[Large2], Is.EqualTo(1));
            Assert.That(s.Phase, Is.EqualTo(DivePhase.Ended));
            Assert.That(s.Checkpoint.Held, Is.True);
        }

        [Test]
        public void PushOn_MakesPicksSafeAndLocksBank()
        {
            var bags = new PartyBags();
            for (int i = 0; i < 10; i++) bags.For("a").Add(Large1);
            DiveSession s = AtBank(1, bags);
            Assert.That(bags.For("a").Pick(0), Is.True);
            Assert.That(s.PushOn(), Is.True);
            Assert.That(bags.For("a").BankedThisDive.Count, Is.GreaterThan(0));
            Assert.That(bags.For("a").BankedTotal, Is.EqualTo(3), "solo bank has three slots");
            Assert.That(s.PushOn(), Is.False, "twice");
            Assert.That(s.GoHome(), Is.False, "the choice is made");
            Assert.That(s.Momentum.PushedOn, Is.True);
            Assert.That(s.BankChosenThisDive, Is.True);
            Assert.That(s.Stir.Value, Is.EqualTo(0f), "7 carried valuables, none above the baseline");
            Assert.That(s.EnterFloor(7), Is.False);
            Assert.That(s.EnterFloor(6), Is.True);
        }

        [Test]
        public void PushOn_BaselineIgnoresBankedPicks()
        {
            var bags = new PartyBags();
            PlayerBag a = bags.For("a");
            a.Add(Large1); a.Add(Large2); a.Add(Large3); a.Add(UsableStandIn);
            DiveSession s = AtBank(4, bags);   // four players: one slot each
            Assert.That(a.Pick(0), Is.True);
            Assert.That(s.PushOn(), Is.True);
            Assert.That(a.Carried.Count, Is.EqualTo(3));
            Assert.That(s.Stir.UnbankedValuables, Is.EqualTo(2), "3 weapons carried, 1 banked; the usable adds nothing");
            Assert.That(s.Stir.Value, Is.EqualTo(0f));
            s.Stir.UnbankedValuables = 3;
            Assert.That(s.Stir.Value, Is.EqualTo(DiveStir.PerUnbankedValuable * 1f).Within(0.001f), "only the third counts");
        }

        [Test]
        public void EnterBankRoom_RequiresFloor5Cleared()
        {
            var s = NewSession();
            s.TryStart(1, false);
            Assert.That(s.EnterBankRoom(Slots(1), 1UL, -1, new[] { "a" }), Is.False, "wrong floor");
            for (int f = 1; f < 5; f++) { s.ClearFloor(f); s.EnterFloor(f + 1); }
            Assert.That(s.EnterBankRoom(Slots(1), 1UL, -1, new[] { "a" }), Is.False, "floor 5 not cleared");
            s.ClearFloor(5);
            Assert.That(s.EnterBankRoom(Slots(1), 1UL, -1, new[] { "a" }), Is.True);
            Assert.That(s.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(s.Bags.For("a").BankOpen, Is.True);
            Assert.That(s.Bags.For("a").SlotCapacity, Is.EqualTo(3));
        }

        [Test]
        public void EnterBankRoom_ThreePlayers_ExtraSlotSkipsThePreviousOwner()
        {
            var s = NewSession();
            s.TryStart(5, true);
            Assert.That(s.EnterBankRoom(Slots(3), 99UL, 1, new[] { "a", "b", "c" }), Is.True);
            Assert.That(s.ExtraSlotOwner, Is.Not.EqualTo(1));
            int total = 0;
            foreach (string id in new[] { "a", "b", "c" }) total += s.Bags.For(id).SlotCapacity;
            Assert.That(total, Is.EqualTo(4));
        }

        [Test]
        public void Fail_AtFloor7_LeavesOneSatchelPerPlayerAtDepth7()
        {
            var bags = new PartyBags();
            DiveSession s = AtBank(2, bags);
            Assert.That(s.PushOn(), Is.True);
            Assert.That(s.EnterFloor(6), Is.True);
            s.ClearFloor(6);
            Assert.That(s.EnterFloor(7), Is.True);
            bags.For("a").Add(Large1);
            bags.For("b").Add(Large2);
            s.Fail();
            Assert.That(bags.For("a").Satchel.Waiting.Floor, Is.EqualTo(7));
            Assert.That(bags.For("b").Satchel.Waiting.Floor, Is.EqualTo(7));
            Assert.That(bags.For("a").Satchel.Waiting.Items, Is.EqualTo(new[] { Large1 }));
        }

        [Test]
        public void Finish_OnlyAtFloor7_BringsHomeAndNoCheckpoint()
        {
            var bags = new PartyBags();
            DiveSession s = AtBank(1, bags);
            s.PushOn();
            s.EnterFloor(6);
            Assert.That(s.Finish(), Is.False, "floor 6");
            Assert.That(s.EnterFloor(7), Is.True);
            Assert.That(s.EnterFloor(8), Is.False, "no stair on floor 7");
            bags.For("a").Add(Large1);
            Assert.That(s.Finish(), Is.True);
            Assert.That(s.Ended, Is.EqualTo(DiveEnd.Finished));
            Assert.That(bags.For("a").Carried, Is.Empty);
            Assert.That(bags.For("a").Stash[Large1], Is.EqualTo(1));
            Assert.That(s.Checkpoint.Held, Is.False);
        }

        [Test]
        public void ClearFloor5_BeforePushOn_ThenFloor6AfterPushOn_StirsEachHalfSeparately()
        {
            DiveSession s = AtBank(1);
            Assert.That(s.Stir.Value, Is.EqualTo(5 * DiveStir.PerFloorCleared).Within(0.001f));
            s.PushOn();
            Assert.That(s.Stir.Value, Is.EqualTo(0f));
            s.EnterFloor(6);
            s.ClearFloor(6);
            Assert.That(s.Stir.Value, Is.EqualTo(DiveStir.PerFloorCleared).Within(0.001f));
            Assert.That(s.Stir.RewardValue, Is.EqualTo(6 * DiveStir.PerFloorCleared).Within(0.001f));
        }

        [Test]
        public void EnterFloor_OnlyTheNextFloor_AndNotOutOfTheBankBeforeTheChoice()
        {
            var s = NewSession();
            s.TryStart(1, false);
            Assert.That(s.EnterFloor(3), Is.False);
            Assert.That(s.EnterFloor(2), Is.True);
            s.TryStart(1, false);   // already running: refused, floor unchanged
            Assert.That(s.Floor, Is.EqualTo(2));

            DiveSession b = AtBank(1);
            Assert.That(b.EnterFloor(6), Is.False, "bank room open, no choice yet");
        }

        [Test]
        public void SnapshotRestore_RoundTrips_BankPhase()
        {
            DiveSession s = AtBank(3);
            s.Stir.PlayersPresent = 3;
            s.Retry.ActivateIdol();
            DiveSession.SnapshotData snap = s.Snapshot();

            var r = NewSession(s.Bags, s.Checkpoint);
            r.Restore(snap);
            Assert.That(r.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(r.Floor, Is.EqualTo(5));
            Assert.That(r.IsCleared(5), Is.True);
            Assert.That(r.ExtraSlotOwner, Is.EqualTo(s.ExtraSlotOwner));
            Assert.That(r.BankChosenThisDive, Is.False);
            Assert.That(r.Retry.IdolActivated, Is.True);
            Assert.That(r.Stir.Capture(), Is.EqualTo(s.Stir.Capture()));
            Assert.That(r.Momentum.Capture(), Is.EqualTo(s.Momentum.Capture()));
            Assert.That(r.PushOn(), Is.True, "the restored room still takes the choice");
        }

        [Test]
        public void SnapshotRestore_RoundTrips_AfterPushOn()
        {
            DiveSession s = AtBank(1);
            s.PushOn();
            s.EnterFloor(6);
            var r = NewSession(s.Bags, s.Checkpoint);
            r.Restore(s.Snapshot());
            Assert.That(r.BankChosenThisDive, Is.True);
            Assert.That(r.Floor, Is.EqualTo(6));
            Assert.That(r.Momentum.PushedOn, Is.True);
            Assert.That(r.Stir.OnFloorCleared(5), Is.False, "second half restored");
            Assert.That(r.PushOn(), Is.False);
        }
    }
}
