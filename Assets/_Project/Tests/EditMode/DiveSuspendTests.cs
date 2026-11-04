// Tests/EditMode/DiveSuspendTests.cs: the `suspend` world-save section (plan 3 task 6; decisions 8-10): SuspendSave, SuspendedBag, PlayerBag.RestoreSuspended, BagsSaveRules suspended mode.
// Pure Core + NUnit (no Newtonsoft, so Tools/cloud/sc_core_tests.py runs it); the file round trip is WorldSaveStoreTests and the real stack is SuspendSaveNetworkTests.
// Catches: a bank-room quit that loses carried items, picks, banked-this-dive items, dive gold, the chest mask, the carried satchel, stir latches or momentum; one bad bag record costing
// the other players' records or the section; a mid-dive quit that stops being a failed dive when it is not a bank suspend; piece depths that move after they were saved.
// Does not catch: the section being wired through HostWorld / WorldSaveFlow / the store's Sections table (WorldSaveStoreTests, SuspendSaveNetworkTests).
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveSuspendTests
    {
        static readonly PlayerKey KeyA = new PlayerKey(IdentityKind.Local, "aaaa");
        static readonly PlayerKey KeyB = new PlayerKey(IdentityKind.Local, "bbbb");
        static readonly PlayerKey KeyC = new PlayerKey(IdentityKind.Local, "cccc");
        static string IdA => KeyA.ToString();
        static string IdB => KeyB.ToString();
        static string IdC => KeyC.ToString();

        static IReadOnlyDictionary<string, PlayerKey> Keys(params PlayerKey[] keys) => keys.ToDictionary(k => k.ToString(), k => k);

        // A's bag mid-dive: 5 gold and 109 at home, 101 banked this dive, 104 and 105 carried, 7 dive gold, chest 63 open on (55, floor 3), a carried satchel holding 106.
        static PlayerBag MidDiveBagA()
        {
            PlayerBag b = PlayerBag.Restore(5, new Dictionary<int, int> { { 109, 1 } }, null);
            b.Add(101); b.OpenBank(1); b.Pick(0); b.PushOn();
            b.Add(104); b.Add(105);
            b.AddGold(7);
            b.MarkChestOpened(55u, 3, 63);   // bit 63: the whole ulong must survive
            b.Satchel.Restore(new LostSatchel(3, new[] { 106 }));
            b.Satchel.PickUp();
            return b;
        }

        // Two players at the cleared floor-5 bank room (slots 0 and 1).
        static DiveSession AtBank(PartyBags bags)
        {
            var s = new DiveSession(bags, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            Assert.That(s.TryStart(1, false), Is.True);
            for (int f = 1; f < UndercroftPlan.BankFloor; f++) { s.ClearFloor(f); Assert.That(s.EnterFloor(f + 1), Is.True); }
            s.ClearFloor(UndercroftPlan.BankFloor);
            Assert.That(s.EnterBankRoom(new List<int> { 0, 1 }, 777UL, -1, new[] { IdA, IdB }), Is.True);
            return s;
        }

        static string[] BySlot() => new[] { IdA, IdB };

        // The Capture step a director would run: home bags, bag records, the section.
        static SuspendSave CaptureSection(DiveSession s, out BagsSave home)
        {
            var records = new List<SuspendedBag>();
            home = BagsSaveRules.Capture(s.Bags, Keys(KeyA, KeyB), true, s.Floor, suspended: true, suspendedBags: records);
            return SuspendSaveRules.Build(s.Snapshot(), 4242u, new[] { IdA, IdB }, BySlot(), records);
        }

        // ---------------------------------------------------------------- round trip
        [Test]
        public void Suspend_RoundTrip_KeepsCarriedAndBankState()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, PlayerBag.Restore(2, null, null));
            bags.For(IdB).Add(110);
            DiveSession s = AtBank(bags);
            Assert.That(bags.For(IdA).Pick(0), Is.True);                       // 104
            Assert.That(bags.For(IdA).Pick(PlayerBag.SatchelPick), Is.True);   // the carried satchel
            s.Stir.PlayersPresent = 2; s.Stir.UnbankedValuables = 3;
            s.Retry.ActivateIdol();

            SuspendSave raw = CaptureSection(s, out BagsSave home);
            var dropped = new List<string>();
            SuspendSave clean = SuspendSaveRules.Clean(raw, dropped);
            Assert.That(clean, Is.Not.Null);
            Assert.That(dropped, Is.Empty, "a captured section is already valid");
            Assert.That(clean.Seed, Is.EqualTo(4242u));
            Assert.That(clean.Floor, Is.EqualTo(5));
            Assert.That(clean.Phase, Is.EqualTo((int)DivePhase.BankRoom));
            Assert.That(clean.BankChosen, Is.False);
            Assert.That(clean.PresentIds, Is.EqualTo(new[] { IdA, IdB }));

            // restore into a fresh party and a fresh session
            var bags2 = new PartyBags();
            BagsSaveRules.RestoreSuspended(BagsSaveRules.Clean(home, null), clean, bags2);
            var s2 = new DiveSession(bags2, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            s2.Restore(SuspendSaveRules.ToSnapshot(clean, BySlot()));

            Assert.That(s2.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(s2.Floor, Is.EqualTo(5));
            Assert.That(s2.IsCleared(5), Is.True);
            Assert.That(s2.BankChosenThisDive, Is.False);
            Assert.That(s2.Stir.Capture(), Is.EqualTo(s.Stir.Capture()), "stir latches, baseline, collapse clock and reward carry");
            Assert.That(s2.Momentum.Capture(), Is.EqualTo(s.Momentum.Capture()));
            Assert.That(s2.Retry.Capture(), Is.EqualTo(s.Retry.Capture()));

            PlayerBag a = bags2.For(IdA);
            Assert.That(a.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(a.BankedThisDive, Is.EqualTo(new Dictionary<int, int> { { 101, 1 } }));
            Assert.That(a.DiveGold, Is.EqualTo(7));
            Assert.That(a.Gold, Is.EqualTo(5));
            Assert.That(a.Stash, Is.EqualTo(new Dictionary<int, int> { { 109, 1 } }));
            Assert.That(a.ChestOpened(55u, 3, 63), Is.True);
            Assert.That(a.ChestOpened(55u, 3, 0), Is.False);
            Assert.That(a.Satchel.Carried, Is.Not.Null);
            Assert.That(a.Satchel.Carried.Items, Is.EqualTo(new[] { 106 }));
            Assert.That(a.Satchel.Carried.Floor, Is.EqualTo(3));
            Assert.That(a.Picks, Is.EqualTo(new[] { 0, PlayerBag.SatchelPick }));
            Assert.That(bags2.For(IdB).Carried, Is.EqualTo(new[] { 110 }));
            Assert.That(bags2.For(IdB).Gold, Is.EqualTo(2));

            // the host reopens the visit from the saved picks, and the choice still works on the restored bags
            Assert.That(a.BankOpen, Is.False, "restoring never opens a visit by itself");
            a.ReopenBank(2);
            Assert.That(a.BankOpen, Is.True);
            Assert.That(a.SlotCapacity, Is.EqualTo(2));
            Assert.That(a.Picks, Is.EqualTo(new[] { 0, PlayerBag.SatchelPick }), "the picks survive the reopen");
            bags2.For(IdB).ReopenBank(2);
            Assert.That(s2.PushOn(), Is.True);
            Assert.That(a.BankedThisDive[104], Is.EqualTo(1));
            Assert.That(a.BankedThisDive[106], Is.EqualTo(1), "the satchel's items were banked by the saved pick");
            Assert.That(a.Carried, Is.EqualTo(new[] { 105 }));
        }

        [Test]
        public void Suspend_AfterPushOn_RoundTrips_AndNeedsNoVisit()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            bags.For(IdA).Pick(0);
            Assert.That(s.PushOn(), Is.True);
            SuspendSave raw = CaptureSection(s, out BagsSave home);
            Assert.That(raw.BankChosen, Is.True);
            SuspendSave clean = SuspendSaveRules.Clean(raw, null);

            var bags2 = new PartyBags();
            BagsSaveRules.RestoreSuspended(BagsSaveRules.Clean(home, null), clean, bags2);
            var s2 = new DiveSession(bags2, new DiveCheckpoint(), new DivePieces(new Dictionary<int, int>()));
            s2.Restore(SuspendSaveRules.ToSnapshot(clean, BySlot()));
            Assert.That(s2.BankChosenThisDive, Is.True);
            Assert.That(s2.Momentum.PushedOn, Is.True);
            Assert.That(s2.PushOn(), Is.False, "the choice was already made");
            Assert.That(bags2.For(IdA).BankedThisDive.ContainsKey(104), Is.True, "the pick that was banked stays banked");
            Assert.That(bags2.For(IdA).Picks, Is.Empty);
            Assert.That(bags2.For(IdA).BankOpen, Is.False);
            Assert.That(s2.Stir.Capture(), Is.EqualTo(s.Stir.Capture()));
        }

        [Test]
        public void Capture_Suspended_DoesNotMakeItAFailedDive_AndWritesARecordEvenForAnEmptyHome()
        {
            var bags = new PartyBags();
            PlayerBag b = bags.For(IdA);   // nothing at home
            b.Add(104);
            var records = new List<SuspendedBag>();
            BagsSave home = BagsSaveRules.Capture(bags, Keys(KeyA), true, 5, suspended: true, suspendedBags: records);
            Assert.That(home.Players, Is.Empty, "no home gold, stash or waiting satchel: nothing for the bags section");
            Assert.That(records.Count, Is.EqualTo(1));
            Assert.That(records[0].Carried, Is.EqualTo(new[] { 104 }), "carried, not turned into a waiting satchel");
            Assert.That(b.Carried, Is.EqualTo(new[] { 104 }), "the live bag is not changed");
        }

        [Test]
        public void MidDiveQuit_StillWritesFailedCopy()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            var records = new List<SuspendedBag>();
            BagsSave save = BagsSaveRules.Capture(bags, Keys(KeyA), true, 4, suspended: false, suspendedBags: records);
            Assert.That(records, Is.Empty, "the old rule: no dive records unless suspended");
            PlayerBagSave p = save.Players.Single();
            Assert.That(p.Gold, Is.EqualTo(5 + 3), "half the dive gold is kept");
            Assert.That(p.Stash.Select(r => r.ItemId + ":" + r.Count), Is.EqualTo(new[] { "101:1", "109:1" }), "banked items are safe");
            Assert.That(p.Satchel.Floor, Is.EqualTo(4));
            Assert.That(p.Satchel.Items, Is.EqualTo(new[] { 104, 105 }), "the carried items wait in a satchel at the floor the dive failed on");
        }

        // ---------------------------------------------------------------- clean: a bad bag record costs only itself
        static SuspendedBag GoodBag(string id, int identity = (int)IdentityKind.Local) => new SuspendedBag
        {
            Identity = identity, Id = id, Carried = { 104, 105 }, BankedThisDive = { new LootCountSave { ItemId = 101, Count = 1 } },
            DiveGold = 4, ChestSeed = 9u, ChestFloor = 2, OpenedChests = 5UL, Picks = { 0 },
        };

        static SuspendSave Section(params SuspendedBag[] bags)
        {
            var s = new SuspendSave { Seed = 1u, Floor = 5, Phase = (int)DivePhase.BankRoom, PresentIds = { IdA, IdB } };
            s.Bags.AddRange(bags);
            return s;
        }

        [Test]
        public void Suspend_BadBagEntry_DropsOnlyThatBag()
        {
            var mutations = new Dictionary<string, Action<SuspendedBag>>
            {
                ["unknown identity"] = b => b.Identity = 77,
                ["no identity"] = b => b.Identity = -1,
                ["item id zero"] = b => b.Carried.Add(0),
                ["item id negative"] = b => b.Carried.Add(-3),
                ["a key in the bag"] = b => b.Carried.Add(CellarLoot.RoomKeyId),
                ["banked count zero"] = b => b.BankedThisDive.Add(new LootCountSave { ItemId = 102, Count = 0 }),
                ["banked count negative"] = b => b.BankedThisDive.Add(new LootCountSave { ItemId = 102, Count = -1 }),
                ["banked id zero"] = b => b.BankedThisDive.Add(new LootCountSave { ItemId = 0, Count = 1 }),
                ["too many carried"] = b => { b.Carried.Clear(); for (int i = 0; i <= PlayerBag.MaxCarried; i++) b.Carried.Add(104); },
                ["pick past the end"] = b => b.Picks.Add(5),
                ["pick below the satchel marker"] = b => b.Picks.Add(-2),
                ["satchel pick with no satchel"] = b => b.Picks.Add(PlayerBag.SatchelPick),
                ["pick listed twice"] = b => b.Picks.Add(0),
                ["bad satchel floor"] = b => b.CarriedSatchel = new SatchelSave { Floor = 11, Items = { 106 } },
                ["bad satchel item"] = b => b.CarriedSatchel = new SatchelSave { Floor = 3, Items = { 0 } },
                ["bad chest floor"] = b => b.ChestFloor = 11,
            };
            foreach (KeyValuePair<string, Action<SuspendedBag>> m in mutations)
            {
                SuspendedBag bad = GoodBag(KeyB.Id);
                m.Value(bad);
                var dropped = new List<string>();
                SuspendSave clean = SuspendSaveRules.Clean(Section(GoodBag(KeyA.Id), bad, GoodBag(KeyC.Id)), dropped);
                Assert.That(clean, Is.Not.Null, m.Key + ": the section survives");
                Assert.That(clean.Bags.Select(b => b.Id), Is.EqualTo(new[] { KeyA.Id, KeyC.Id }), m.Key + ": only that bag goes");
                Assert.That(dropped, Is.EqualTo(new[] { "suspend.bags." + KeyB.Id }), m.Key);
                Assert.That(clean.Floor, Is.EqualTo(5), m.Key);
            }
        }

        [Test]
        public void Suspend_RepeatedBagId_DropsTheSecond_AndTheCapAt4Holds()
        {
            var dropped = new List<string>();
            SuspendSave clean = SuspendSaveRules.Clean(Section(GoodBag(KeyA.Id), GoodBag(KeyA.Id)), dropped);
            Assert.That(clean.Bags.Count, Is.EqualTo(1));
            Assert.That(dropped, Is.EqualTo(new[] { "suspend.bags." + KeyA.Id }));

            var five = Section();
            foreach (string id in new[] { "aaaa", "bbbb", "cccc", "dddd", "eeee" }) five.Bags.Add(GoodBag(id));
            Assert.That(SuspendSaveRules.Clean(five, null).Bags.Count, Is.EqualTo(WorldPlayers.MaxRecorded), "the file never holds more than the world records");
        }

        [Test]
        public void Suspend_GoodBagWithASatchelAndSatchelPick_IsKept()
        {
            SuspendedBag b = GoodBag(KeyA.Id);
            b.CarriedSatchel = new SatchelSave { Floor = 9, Items = { 106, 107 } };   // floor 9 is a floor now
            b.Picks.Add(PlayerBag.SatchelPick);
            SuspendSave clean = SuspendSaveRules.Clean(Section(b), null);
            Assert.That(clean.Bags.Single().CarriedSatchel.Floor, Is.EqualTo(9));
            Assert.That(clean.Bags.Single().Picks, Is.EqualTo(new[] { 0, PlayerBag.SatchelPick }));
        }

        [Test]
        public void Suspend_BadFloorOrPhase_DropsTheWholeSection()
        {
            var dropped = new List<string>();
            foreach (int floor in new[] { 0, -1, 11, 99 })
            {
                SuspendSave raw = Section(GoodBag(KeyA.Id));
                raw.Floor = floor;
                dropped.Clear();
                Assert.That(SuspendSaveRules.Clean(raw, dropped), Is.Null, "floor " + floor);
                Assert.That(dropped, Is.Not.Empty);
            }
            foreach (int phase in new[] { (int)DivePhase.Town, (int)DivePhase.Ended, 9, -1 })
            {
                SuspendSave raw = Section(GoodBag(KeyA.Id));
                raw.Phase = phase;
                Assert.That(SuspendSaveRules.Clean(raw, null), Is.Null, "phase " + phase);
            }
            Assert.That(SuspendSaveRules.Clean(null, null), Is.Null);
            Assert.That(SuspendSaveRules.Clean(Section(), null), Is.Not.Null, "a section with no bag records is valid (everyone's bag was empty)");
        }

        [Test]
        public void Suspend_Clean_RepairsNumbers_AndKeepsUnknownMembers()
        {
            SuspendSave raw = Section(GoodBag(KeyA.Id));
            raw.ClearedMask = 0xFFFF;
            raw.Stir = new SuspendStirSave { FloorMask = 0xFFFF, Valuables = -4, ValuablesBaseline = 9, SecondsLeft = float.NaN, RewardCarry = -3f };
            raw.Bags[0].DiveGold = -8;
            raw.PresentIds.Add("not a key"); raw.PresentIds.Add(IdA);   // a bad and a repeated id go alone
            raw.Extra = new Dictionary<string, object> { ["future"] = 1 };
            SuspendSave c = SuspendSaveRules.Clean(raw, null);
            Assert.That(c.ClearedMask, Is.EqualTo((1 << UndercroftPlan.BossFloor) - 1), "masked to the ten floors, not rejected");
            Assert.That(c.Stir.FloorMask, Is.EqualTo((1 << UndercroftPlan.BossFloor) - 1));
            Assert.That(c.Stir.Valuables, Is.EqualTo(0));
            Assert.That(c.Stir.ValuablesBaseline, Is.EqualTo(0), "never above what was carried");
            Assert.That(c.Stir.SecondsLeft, Is.EqualTo(0f));
            Assert.That(c.Stir.RewardCarry, Is.EqualTo(0f));
            Assert.That(c.Bags[0].DiveGold, Is.EqualTo(0));
            Assert.That(c.PresentIds, Is.EqualTo(new[] { IdA, IdB }));
            Assert.That(c.Extra["future"], Is.EqualTo(1));
            Assert.That(raw.Stir.Valuables, Is.EqualTo(-4), "the raw section is not changed");
        }

        // ---------------------------------------------------------------- RestoreSuspended edge cases
        [Test]
        public void RestoreSuspended_AcceptsAMissingHomeOrMissingDiveRecord()
        {
            PlayerBag onlyDive = PlayerBag.RestoreSuspended(null, GoodBag(KeyA.Id));
            Assert.That(onlyDive.Gold, Is.EqualTo(0));
            Assert.That(onlyDive.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(onlyDive.Picks, Is.EqualTo(new[] { 0 }));

            var home = new PlayerBagSave { Identity = 0, Id = "aaaa", Gold = 9, Satchel = new SatchelSave { Floor = 2, Items = { 110 } } };
            PlayerBag onlyHome = PlayerBag.RestoreSuspended(home, null);
            Assert.That(onlyHome.Gold, Is.EqualTo(9));
            Assert.That(onlyHome.Satchel.Waiting.Items, Is.EqualTo(new[] { 110 }));
            Assert.That(onlyHome.Carried, Is.Empty);

            Assert.That(PlayerBag.RestoreSuspended(null, null).Gold, Is.EqualTo(0));
        }

        [Test]
        public void ReopenBank_DropsExcessPicksFromTheEnd_AndNeedsARecordedPick()
        {
            PlayerBag b = PlayerBag.RestoreSuspended(null, new SuspendedBag { Identity = 0, Id = "aaaa", Carried = { 101, 102, 103 }, Picks = { 2, 0, 1 } });
            b.ReopenBank(2);
            Assert.That(b.Picks, Is.EqualTo(new[] { 2, 0 }), "the party shrank: the last pick drops");
            Assert.That(b.SlotCapacity, Is.EqualTo(2));
            b.PushOn();
            Assert.That(b.BankedThisDive.Keys.OrderBy(k => k), Is.EqualTo(new[] { 101, 103 }));
            Assert.That(b.Carried, Is.EqualTo(new[] { 102 }));
        }

        // ---------------------------------------------------------------- the dive section's new fields and piece depths
        [Test]
        public void ExtraSlotOwner_RoundTrips_AndFeedsPickExtraSlotOwner()
        {
            var d = new DiveSave { ExtraSlotOwner = IdB };
            var dropped = new List<string>();
            DiveSave c = TownSaveRules.CleanDive(d, dropped);
            Assert.That(c.ExtraSlotOwner, Is.EqualTo(IdB));
            Assert.That(dropped, Is.Empty);

            string[] bySlot = { IdA, IdB, IdC };
            int previous = SuspendSaveRules.SlotOfId(c.ExtraSlotOwner, bySlot);
            Assert.That(previous, Is.EqualTo(1));
            for (ulong seed = 1; seed <= 100; seed++)
                Assert.That(BankSlots.PickExtraSlotOwner(seed, new[] { 0, 1, 2 }, previous), Is.Not.EqualTo(previous), "the loaded owner is skipped, seed " + seed);

            Assert.That(SuspendSaveRules.SlotOfId(null, bySlot), Is.EqualTo(-1));
            Assert.That(SuspendSaveRules.SlotOfId("Local:zzzz", bySlot), Is.EqualTo(-1), "a player who is not here");

            DiveSave bad = TownSaveRules.CleanDive(new DiveSave { ExtraSlotOwner = "oops" }, dropped);
            Assert.That(bad.ExtraSlotOwner, Is.Null, "a value that is no player key goes alone");
            Assert.That(dropped, Is.Not.Empty);
        }

        [Test]
        public void OldSave_NoCheckpointField_ReadsAsNotHeld()
        {
            var old = new DiveSave { ClearedMask = 3, CheckpointReached = true };   // a save from before the new fields: they sit at their defaults
            var dropped = new List<string>();
            DiveSave c = TownSaveRules.CleanDive(old, dropped);
            Assert.That(c.CheckpointHeld, Is.False);
            Assert.That(c.BossBeaten, Is.False);
            Assert.That(c.ExtraSlotOwner, Is.Null);
            Assert.That(c.PieceDepths, Is.Null, "missing stays missing (a repair at load, not a rejection)");
            Assert.That(c.CheckpointReached, Is.True, "the old field is untouched until task 12");
            Assert.That(dropped, Is.Empty, "no rejection, so no .corrupt copy for every old save");
            Assert.That(TownSaveRules.CleanDive(new DiveSave { CheckpointHeld = true, BossBeaten = true }, null).CheckpointHeld, Is.True);
            Assert.That(TownSaveRules.IsPristine(new DiveSave { CheckpointHeld = true }), Is.False, "a held checkpoint is progress");
        }

        static readonly ulong[] Seeds = { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL, 11UL, 12UL };

        [Test]
        public void PieceDepths_RoundTrip_NotRecomputed()
        {
            // Save the depths a seed assigned, then ask again under a seed that would assign differently: the saved map wins.
            IReadOnlyDictionary<int, int> saved = PieceDepths.Assign(1UL, PieceCatalog.Specs);
            ulong other = Seeds.First(s => !SameMap(PieceDepths.Assign(s, PieceCatalog.Specs), saved));
            var dive = new DiveSave { PieceDepths = saved.Select(kv => new PieceDepthSave { Id = kv.Key, Floor = kv.Value }).ToList() };
            DiveSave clean = TownSaveRules.CleanDive(dive, null);
            Assert.That(clean.PieceDepths.Count, Is.EqualTo(PieceCatalog.Specs.Count), "the baker's entry (not in PieceCatalog.All) is kept too");
            Assert.That(SameMap(TownSaveRules.ResolvePieceDepths(clean, other), saved), Is.True, "not recomputed from the new seed");
            Assert.That(SameMap(TownSaveRules.ResolvePieceDepths(clean, 1UL), saved), Is.True);
        }

        [Test]
        public void PieceDepths_MissingInOldSave_AssignedOnceThenSaved()
        {
            IReadOnlyDictionary<int, int> assigned = PieceDepths.Assign(77UL, PieceCatalog.Specs);
            Assert.That(SameMap(TownSaveRules.ResolvePieceDepths(null, 77UL), assigned), Is.True, "no dive section at all");
            Assert.That(SameMap(TownSaveRules.ResolvePieceDepths(new DiveSave(), 77UL), assigned), Is.True, "an old dive section without the field");
            Assert.That(SameMap(TownSaveRules.ResolvePieceDepths(new DiveSave { PieceDepths = new List<PieceDepthSave>() }, 77UL), assigned), Is.True, "or with an empty list");

            // one entry lost to a bad value: only it is filled in from the seed, the others stay as saved
            IReadOnlyDictionary<int, int> first = PieceDepths.Assign(1UL, PieceCatalog.Specs);
            ulong other = Seeds.First(sd => PieceDepths.Assign(sd, PieceCatalog.Specs)[101] != first[101]);
            var partial = new DiveSave { PieceDepths = first.Where(kv => kv.Key != 101).Select(kv => new PieceDepthSave { Id = kv.Key, Floor = kv.Value }).ToList() };
            IReadOnlyDictionary<int, int> resolved = TownSaveRules.ResolvePieceDepths(TownSaveRules.CleanDive(partial, null), other);
            foreach (KeyValuePair<int, int> kv in first) if (kv.Key != 101) Assert.That(resolved[kv.Key], Is.EqualTo(kv.Value), "kept as saved: " + kv.Key);
            Assert.That(resolved[101], Is.EqualTo(PieceDepths.Assign(other, PieceCatalog.Specs)[101]), "the missing one comes from the seed");
        }

        [Test]
        public void CleanDive_PieceDepths_BadEntriesDropOneByOne()
        {
            var dive = new DiveSave
            {
                PieceDepths = new List<PieceDepthSave>
                {
                    new PieceDepthSave { Id = 100, Floor = 4 },            // good
                    new PieceDepthSave { Id = 101, Floor = 1 },            // floor 1 is authored: no random piece lives there
                    new PieceDepthSave { Id = 200, Floor = 5 },            // floor 5 is the bank
                    new PieceDepthSave { Id = 201, Floor = 99 },
                    new PieceDepthSave { Id = 9999, Floor = 2 },           // unknown id
                    new PieceDepthSave { Id = 300, Floor = 7 },            // second half
                    new PieceDepthSave { Id = 301, Floor = 2 },            // second-half piece on a first-half floor
                    new PieceDepthSave { Id = 100, Floor = 2 },            // repeated id
                    new PieceDepthSave { Id = PieceCatalog.BakerId, Floor = 3 },
                    null,
                },
            };
            var dropped = new List<string>();
            DiveSave c = TownSaveRules.CleanDive(dive, dropped);
            Assert.That(c.PieceDepths.Select(p => p.Id + ":" + p.Floor), Is.EqualTo(new[] { "100:4", "300:7", "400:3" }));
            Assert.That(dropped.Count, Is.EqualTo(7));
        }

        [Test]
        public void CleanDive_Masks_ToTheTenFloors()
        {
            Assert.That(TownSaveRules.CleanDive(new DiveSave { ClearedMask = 0xFFFF }, null).ClearedMask, Is.EqualTo(0x3FF));
            Assert.That(TownSaveRules.CleanDive(new DiveSave { ClearedMask = 0b10_0001_0011 }, null).ClearedMask, Is.EqualTo(0b10_0001_0011));
        }

        // ---------------------------------------------------------------- snapshots and merges
        static DiveSave V2Dive() => new DiveSave
        {
            CheckpointHeld = true, BossBeaten = true, ExtraSlotOwner = IdB,
            PieceDepths = new List<PieceDepthSave> { new PieceDepthSave { Id = 100, Floor = 4 } },
        };

        [Test]
        public void MergeLive_LiveDiveWithoutV2Fields_KeepsSavedDepthsAndOwner()
        {
            // the live capture builds a fresh DiveSave from DiveState (it knows none of the new fields): the saved ones must not be wiped by it
            var previous = new TownSnapshot { Dive = V2Dive() };
            var live = new TownSnapshot { Dive = new DiveSave { ClearedMask = 3 } };
            TownSnapshot merged = TownSaveRules.MergeLive(live, previous);
            Assert.That(merged.Dive.ClearedMask, Is.EqualTo(3), "the live progress wins");
            Assert.That(merged.Dive.CheckpointHeld, Is.True);
            Assert.That(merged.Dive.BossBeaten, Is.True);
            Assert.That(merged.Dive.ExtraSlotOwner, Is.EqualTo(IdB));
            Assert.That(merged.Dive.PieceDepths.Select(p => p.Id + ":" + p.Floor), Is.EqualTo(new[] { "100:4" }));

            // a source that does know them says so, and may turn them off
            var knowing = new TownSnapshot { Dive = new DiveSave { V2Known = true, CheckpointHeld = false } };
            merged = TownSaveRules.MergeLive(knowing, previous);
            Assert.That(merged.Dive.CheckpointHeld, Is.False);
            Assert.That(merged.Dive.BossBeaten, Is.False);
            Assert.That(merged.Dive.ExtraSlotOwner, Is.Null);
            Assert.That(merged.Dive.PieceDepths.Select(p => p.Id), Is.EqualTo(new[] { 100 }), "depths are always the host's: a live capture never drops them");
        }

        [Test]
        public void MergeLive_Suspend_NullKeepsThePrevious_KnownNullClearsIt()
        {
            SuspendSave saved = Section(GoodBag(KeyA.Id));
            var previous = new TownSnapshot { Suspend = saved };
            Assert.That(TownSaveRules.MergeLive(new TownSnapshot(), previous).Suspend, Is.SameAs(saved), "a source that says nothing (the hub's capture today) keeps it");
            Assert.That(TownSaveRules.MergeLive(new TownSnapshot { Dive = new DiveSave() }, previous).Suspend, Is.SameAs(saved));
            TownSnapshot cleared = TownSaveRules.MergeLive(new TownSnapshot { SuspendKnown = true }, previous);
            Assert.That(cleared.Suspend, Is.Null, "using the section clears it");
            SuspendSave fresh = Section();
            Assert.That(TownSaveRules.MergeLive(new TownSnapshot { Suspend = fresh, SuspendKnown = true }, previous).Suspend, Is.SameAs(fresh));
        }

        [Test]
        public void CleanSnapshot_KeepsTheSuspendSection_AndASuspendedDiveIsNotAFailedDive()
        {
            var snap = new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = Section(GoodBag(KeyA.Id)), SuspendKnown = true };
            var rejected = new List<string>();
            var repaired = new List<string>();
            TownSnapshot c = TownSaveRules.CleanSnapshot(snap, rejected, repaired);
            Assert.That(c.Suspend, Is.Not.Null);
            Assert.That(c.SuspendKnown, Is.True, "the flag survives the clean (every call site cleans)");
            Assert.That(rejected, Is.Empty);
            Assert.That(repaired.Any(r => r.Contains("failed dive")), Is.False, "a bank suspend is not counted as a failed dive");

            // the other way: no suspend (or a bad one) and InDive is still a failed dive
            var plain = new TownSnapshot { Dive = new DiveSave { InDive = true } };
            repaired.Clear();
            Assert.That(TownSaveRules.CleanSnapshot(plain, null, repaired).Dive.InDive, Is.False);
            Assert.That(repaired.Any(r => r.Contains("failed dive")), Is.True);
            SuspendSave bad = Section(GoodBag(KeyA.Id)); bad.Floor = 77;
            repaired.Clear();
            rejected.Clear();
            TownSnapshot dropped = TownSaveRules.CleanSnapshot(new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = bad }, rejected, repaired);
            Assert.That(dropped.Suspend, Is.Null);
            Assert.That(rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }));
            Assert.That(dropped.Dive.InDive, Is.False, "the bad suspend is gone, so the quit is the old failed dive");
        }

        [Test]
        public void TownSavesCapture_PassesTheSuspendSectionThrough_Cleaned()
        {
            SuspendSave sus = Section(GoodBag(KeyA.Id)); sus.ClearedMask = 0xFFFF;
            TownSnapshot snap = TownSaves.Capture(null, null, null, null, null, sus, true);
            Assert.That(snap.SuspendKnown, Is.True);
            Assert.That(snap.Suspend.ClearedMask, Is.EqualTo(0x3FF));
            Assert.That(TownSaves.Capture(null, null, null, null, null).SuspendKnown, Is.False, "the existing callers say nothing about it");
            Assert.That(TownSaves.Capture(null, null, null, null, null).Suspend, Is.Null);
        }

        [Test]
        public void Rename_MovesTheOwnerInEverySuspendField()
        {
            SuspendSave s = Section(GoodBag(KeyA.Id));
            s.ExtraSlotOwner = IdA;
            var to = new PlayerKey(IdentityKind.Ugs, "newowner");
            SuspendSaveRules.Rename(s, KeyA, to);
            Assert.That(s.Bags[0].Identity, Is.EqualTo((int)IdentityKind.Ugs));
            Assert.That(s.Bags[0].Id, Is.EqualTo("newowner"));
            Assert.That(s.PresentIds, Is.EqualTo(new[] { to.ToString(), IdB }));
            Assert.That(s.ExtraSlotOwner, Is.EqualTo(to.ToString()));
            SuspendSaveRules.Rename(null, KeyA, to);   // nothing to move
            Assert.That(SuspendSaveRules.RenameId(IdC, KeyA, to), Is.EqualTo(IdC));
        }

        // ================================================================ plan 3 task 8a: resume, and the stale-suspend guard
        // Catches (below): a stale suspend surviving a source that never says "known, none", an autosave between a load and the resume wiping the pending suspend, a used suspend living on, a party
        // that resumes at the wrong floor, with its stir, momentum or bags lost, a resume that spends or sets the checkpoint, a quit in the bank room counted as a failed dive, an absent player's bag
        // lost. Does NOT catch: the host wiring (DiveSpineNetworkTests / SuspendSaveNetworkTests), a client rejoining a resumed dive (manual 2-PC walk).
        static readonly TownStock.SuspendContext NoPending = new TownStock.SuspendContext { Seed = 4242u, PresentIds = new[] { IdA, IdB }, IdsBySlot = new[] { IdA, IdB } };

        static TownStock NewStock() => new TownStock(HollowmereLayout.Create());

        static readonly IReadOnlyDictionary<int, int> Depths = PieceDepths.Assign(12345UL, PieceCatalog.Specs);

        // The way the host reloads: a director capture, the clean the world file goes through, a fresh stock restores it.
        static (TownStock.WorldRestore world, TownSnapshot loaded) Reload(DiveSession s, TownStock.SuspendContext ctx)
        {
            TownSnapshot saved = TownSaveRules.CleanSnapshot(NewStock().CaptureWorld(s, false, null, Depths, null, ctx), null, null, false);
            TownSnapshot loaded = TownSaveRules.CleanSnapshot(saved, null, null, true);   // load: the failed-dive rule applies
            return (NewStock().RestoreWorld(loaded, Depths), loaded);
        }

        [Test]
        public void Reconcile_WithSuspendSection_IsNotAFailedDive()
        {
            var s = new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = Section(GoodBag(KeyA.Id)) };
            var repaired = new List<string>();
            TownSnapshot c = TownSaveRules.CleanSnapshot(s, null, repaired, true);
            TownSaveRules.Reconcile(c, repaired, true);
            Assert.That(c.Dive.InDive, Is.True, "the section keeps the dive in progress: nobody counts a failure");
            Assert.That(c.Suspend, Is.Not.Null);
            Assert.That(repaired.Any(r => r.Contains("failed dive")), Is.False);
        }

        [Test]
        public void CleanSnapshot_DropsAStaleSuspend_WhenNoDiveIsInProgress()
        {
            var repaired = new List<string>();
            // the dive section says nothing is in progress, but a suspend section is still there: a used or left-over one
            var stale = new TownSnapshot { Dive = new DiveSave { InDive = false }, Suspend = Section(GoodBag(KeyA.Id)) };
            Assert.That(TownSaveRules.CleanSnapshot(stale, null, repaired, true).Suspend, Is.Null);
            Assert.That(repaired.Any(r => r.Contains("suspend")), Is.True, "and the drop is reported");
            // no dive section at all
            Assert.That(TownSaveRules.CleanSnapshot(new TownSnapshot { Suspend = Section(GoodBag(KeyA.Id)) }, null, null, true).Suspend, Is.Null);
            // the same on the capture path that only copies (failMidDive false)
            Assert.That(TownSaveRules.CleanSnapshot(stale, null, null, false).Suspend, Is.Null);
            // a dive in progress keeps it
            var live = new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = Section(GoodBag(KeyA.Id)) };
            Assert.That(TownSaveRules.CleanSnapshot(live, null, null, true).Suspend, Is.Not.Null);
        }

        [Test]
        public void StaleSuspend_IsWiped_WhenALaterSourceSaysNothingAndTheDiveIsOver()
        {
            // The file holds a suspend (from a used resume, say) and the party since failed and went home: the live capture is a source that never says "known, none" (SuspendKnown false)
            // and its dive section says nothing is in progress. MergeLive keeps the previous suspend for such a source; the guard must still drop it, or a later quit mid-dive would resume from it.
            var previous = new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = Section(GoodBag(KeyA.Id)) };
            var live = new TownSnapshot { Dive = new DiveSave { InDive = false } };
            TownSnapshot merged = TownSaveRules.MergeLive(live, previous);
            Assert.That(merged.Suspend, Is.Not.Null, "premise: MergeLive keeps the previous copy for a source that says nothing");
            Assert.That(TownSaveRules.CleanSnapshot(merged, null, null, false).Suspend, Is.Null, "the host's clean after the merge drops it");
        }

        [Test]
        public void CaptureWorld_AlwaysSaysWhatItKnowsAboutTheSuspend()
        {
            var bags = new PartyBags();
            var s = new DiveSession(bags, new DiveCheckpoint(), new DivePieces(Depths));
            TownSnapshot town = NewStock().CaptureWorld(s, false, null, Depths, null);
            Assert.That(town.SuspendKnown, Is.True, "a Town-phase capture says 'none'");
            Assert.That(town.Suspend, Is.Null);
            Assert.That(NewStock().CaptureWorld(null, false, null, Depths, null).SuspendKnown, Is.True, "even with no session yet");

            // the host's merge: the saved suspend is cleared by a known-none capture (it was used)
            var previous = new TownSnapshot { Dive = new DiveSave { InDive = true }, Suspend = Section(GoodBag(KeyA.Id)) };
            Assert.That(TownSaveRules.MergeLive(town, previous).Suspend, Is.Null);

            // a dive on floor 3: also known-none (only floor 5's bank writes one)
            Assert.That(s.TryStart(1, false), Is.True);
            s.ClearFloor(1); Assert.That(s.EnterFloor(2), Is.True);
            TownSnapshot below = NewStock().CaptureWorld(s, false, null, Depths, null);
            Assert.That(below.SuspendKnown, Is.True);
            Assert.That(below.Suspend, Is.Null);
            Assert.That(below.Dive.InDive, Is.True, "the old rule: a quit on floor 2 loads as a failed dive");
            Assert.That(TownSaveRules.MergeLive(below, previous).Suspend, Is.Null, "and a stale suspend does not outlive the party's trip down");
        }

        [Test]
        public void CaptureWorld_InTheBankRoom_WritesTheSuspend_BagsAsCarried_PushedOnFalse()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            bags.For(IdA).Pick(0);
            TownSnapshot snap = NewStock().CaptureWorld(s, false, null, Depths, null, NoPending);
            Assert.That(snap.SuspendKnown, Is.True);
            Assert.That(snap.Suspend, Is.Not.Null);
            Assert.That(snap.Suspend.Phase, Is.EqualTo((int)DivePhase.BankRoom));
            Assert.That(snap.Suspend.BankChosen, Is.False, "quitting before the choice: PushedOn == false");
            Assert.That(snap.Suspend.Seed, Is.EqualTo(4242u));
            Assert.That(snap.Suspend.Bags.Single(b => b.Id == KeyA.Id).Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(snap.Suspend.Bags.Single(b => b.Id == KeyA.Id).Picks, Is.EqualTo(new[] { 0 }));
            Assert.That(snap.Dive.InDive, Is.True);
            Assert.That(snap.Bags.Players.Single(p => p.Id == KeyA.Id).Satchel, Is.Null, "carried, not packed into a waiting satchel as a failed dive would");
        }

        [Test]
        public void QuitInBankRoomBeforePushOn_IsNormalSaveAndQuit_NoFailureCounted()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            bags.For(IdA).Pick(0);
            var (world, loaded) = Reload(s, NoPending);
            Assert.That(loaded.Dive.InDive, Is.True, "the load did not count a failed dive");
            Assert.That(loaded.Suspend, Is.Not.Null);
            Assert.That(world.Suspend, Is.Not.Null);
            PlayerBag a = world.Bags.For(IdA);
            Assert.That(a.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(a.Satchel.Waiting, Is.Null, "no failed-dive satchel was made");
            Assert.That(a.BankedThisDive, Is.EqualTo(new Dictionary<int, int> { { 101, 1 } }));
        }

        [Test]
        public void PendingSuspend_IsReEmitted_ByEveryCaptureUntilUsed_AutosaveBeforeTheTrapdoorKeepsIt()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            var (world, _) = Reload(s, NoPending);

            // the party waits at the trapdoor in a Town-phase session over the restored bags; the director holds the loaded section as pending
            var town = new DiveSession(world.Bags, world.Checkpoint, world.Pieces);
            var ctx = new TownStock.SuspendContext { Seed = 4242u, PresentIds = new[] { IdA }, IdsBySlot = new[] { IdA, IdB }, Pending = world.Suspend };
            TownSnapshot autosave = NewStock().CaptureWorld(town, false, null, Depths, null, ctx);
            Assert.That(autosave.Suspend, Is.SameAs(world.Suspend), "re-emitted as it was loaded");
            Assert.That(autosave.SuspendKnown, Is.True);
            Assert.That(autosave.Dive.InDive, Is.True, "a dive is still in progress, or the guard would drop the section");

            // the file written by that autosave still loads as a suspended dive with the carried items
            TownSnapshot file = TownSaveRules.CleanSnapshot(autosave, null, null, false);
            Assert.That(file.Suspend, Is.Not.Null, "the clean guard keeps it");
            TownSnapshot merged = TownSaveRules.MergeLive(autosave, new TownSnapshot());
            Assert.That(TownSaveRules.CleanSnapshot(merged, null, null, false).Suspend, Is.Not.Null);
            var (world2, loaded2) = (NewStock().RestoreWorld(TownSaveRules.CleanSnapshot(file, null, null, true), Depths), file);
            Assert.That(world2.Bags.For(IdA).Carried, Is.EqualTo(new[] { 104, 105 }), "the carried items were not lost by the autosave");
            Assert.That(loaded2.Bags, Is.Not.Null);

            // used: the capture without a pending section says known-none and the file loses it
            TownSnapshot used = NewStock().CaptureWorld(town, false, null, Depths, null, new TownStock.SuspendContext { Seed = 4242u });
            Assert.That(used.SuspendKnown, Is.True);
            Assert.That(used.Suspend, Is.Null);
            Assert.That(TownSaveRules.MergeLive(used, merged).Suspend, Is.Null);
            Assert.That(used.Dive.InDive, Is.False);
        }

        [Test]
        public void RestoreWorld_WithASuspend_RebuildsTheMidDiveBags_AndWithoutOneOnlyTheHomeHalf()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            var (world, _) = Reload(s, NoPending);
            PlayerBag a = world.Bags.For(IdA);
            Assert.That(a.Carried, Is.EqualTo(new[] { 104, 105 }));
            Assert.That(a.DiveGold, Is.EqualTo(7));
            Assert.That(a.Gold, Is.EqualTo(5));
            Assert.That(a.Satchel.Carried.Items, Is.EqualTo(new[] { 106 }));
            Assert.That(a.ChestOpened(55u, 3, 63), Is.True);

            // a failed-dive save restores the home half only
            var failedBags = new PartyBags();
            failedBags.Adopt(IdA, MidDiveBagA());
            var below = new DiveSession(failedBags, new DiveCheckpoint(), new DivePieces(Depths));
            below.TryStart(1, false);
            TownSnapshot snap = TownSaveRules.CleanSnapshot(NewStock().CaptureWorld(below, false, null, Depths, null), null, null, true);
            Assert.That(snap.Dive.InDive, Is.False);
            PlayerBag failed = NewStock().RestoreWorld(snap, Depths).Bags.For(IdA);
            Assert.That(failed.Carried, Is.Empty);
            Assert.That(failed.Satchel.Waiting.Items, Is.EqualTo(new[] { 104, 105 }));
        }

        [Test]
        public void LoadedSuspend_ResumesAtFloor6_WithStirMomentumAndBags()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            bags.For(IdA).Pick(0);
            s.Stir.PlayersPresent = 2; s.Stir.UnbankedValuables = 5;
            Assert.That(s.PushOn(), Is.True);
            Assert.That(s.SuspendsAtBank, Is.True, "floor 5 after the push-on is still a suspend state");
            var (world, _) = Reload(s, NoPending);
            Assert.That(world.Suspend.BankChosen, Is.True);

            var cp = new DiveCheckpoint(); cp.Restore(false);
            DiveSession r = SuspendSaveRules.Resume(world.Suspend, world.Bags, cp, world.Pieces, BySlot(), new[] { 0, 1 }, BySlot(), 4242UL, -1);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Floor, Is.EqualTo(6), "the next floor after the bank");
            Assert.That(r.Phase, Is.EqualTo(DivePhase.Floor));
            Assert.That(r.BankChosenThisDive, Is.True, "bank locked");
            Assert.That(r.Momentum.PushedOn, Is.True);
            Assert.That(r.Momentum.ChestRankBonus(6), Is.EqualTo(1));
            Assert.That(r.Stir.Capture().SecondHalf, Is.True);
            Assert.That(r.Stir.Value, Is.EqualTo(0f).Within(0.001f), "the second half starts clean");
            Assert.That(r.PushOn(), Is.False);
            Assert.That(r.EnterBankRoom(new List<int> { 0 }, 1UL, -1, new[] { IdA }), Is.False);
            PlayerBag a = world.Bags.For(IdA);
            Assert.That(a.BankedThisDive.ContainsKey(104), Is.True, "the pick banked at the push-on");
            Assert.That(a.Carried, Is.EqualTo(new[] { 105 }));
            Assert.That(a.DiveGold, Is.EqualTo(7));
            Assert.That(cp.Held, Is.False, "a resume neither sets nor spends the checkpoint");
            Assert.That(r.Ended, Is.Null);
        }

        [Test]
        public void LoadedSuspend_BeforePushOn_ReturnsToBankRoom_VisitsReopenedWithPicks()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            bags.For(IdB).Add(110);
            DiveSession s = AtBank(bags);
            bags.For(IdA).Pick(0); bags.For(IdA).Pick(PlayerBag.SatchelPick);
            bags.For(IdB).Pick(0);
            var (world, _) = Reload(s, NoPending);
            Assert.That(world.Suspend.BankChosen, Is.False);

            DiveSession r = SuspendSaveRules.Resume(world.Suspend, world.Bags, new DiveCheckpoint(), world.Pieces, BySlot(), new[] { 0, 1 }, BySlot(), 4242UL, -1);
            Assert.That(r.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(r.Floor, Is.EqualTo(5));
            Assert.That(r.BankChosenThisDive, Is.False);
            Assert.That(world.Bags.For(IdA).BankOpen, Is.True, "visits reopened");
            Assert.That(world.Bags.For(IdA).SlotCapacity, Is.EqualTo(2));
            Assert.That(world.Bags.For(IdA).Picks, Is.EqualTo(new[] { 0, PlayerBag.SatchelPick }), "the saved picks are still picked");
            Assert.That(world.Bags.For(IdB).Picks, Is.EqualTo(new[] { 0 }));
            Assert.That(r.PushOn(), Is.True, "the choice is still to be made");
            Assert.That(world.Bags.For(IdA).BankedThisDive.ContainsKey(104) && world.Bags.For(IdA).BankedThisDive.ContainsKey(106), Is.True);
        }

        [Test]
        public void Resume_BankRoomOnAnotherFloor_IsClampedToFloor5_AndAnUnresumablePhaseIsRefused()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            DiveSession s = AtBank(bags);
            var (world, _) = Reload(s, NoPending);

            SuspendSave odd = SuspendSaveRules.Clean(world.Suspend, null);
            odd.Floor = 8; odd.ClearedMask = 0;   // damaged or hand-edited: the bank room on floor 8, nothing cleared
            DiveSession r = SuspendSaveRules.Resume(odd, world.Bags, new DiveCheckpoint(), world.Pieces, BySlot(), new[] { 0, 1 }, BySlot(), 1UL, -1);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Floor, Is.EqualTo(UndercroftPlan.BankFloor), "clamped: the party returns to the bank room, items kept");
            Assert.That(r.Phase, Is.EqualTo(DivePhase.BankRoom));
            Assert.That(r.IsCleared(5), Is.True);
            Assert.That(world.Bags.For(IdA).Carried, Is.EqualTo(new[] { 104, 105 }));

            SuspendSave stairQuit = SuspendSaveRules.Clean(world.Suspend, null);
            stairQuit.Phase = (int)DivePhase.Floor; stairQuit.Floor = 3; stairQuit.BankChosen = false;
            Assert.That(SuspendSaveRules.IsResumable(stairQuit), Is.False, "the later stair quit is not built yet");
            Assert.That(SuspendSaveRules.Resume(stairQuit, world.Bags, new DiveCheckpoint(), world.Pieces, BySlot(), null, null, 1UL, -1), Is.Null);
            stairQuit.Floor = 5; stairQuit.BankChosen = false;
            Assert.That(SuspendSaveRules.IsResumable(stairQuit), Is.False, "floor 5 in the Floor phase without the push-on is not a state a quit saves");
            Assert.That(SuspendSaveRules.Resume(null, world.Bags, new DiveCheckpoint(), world.Pieces, null, null, null, 1UL, -1), Is.Null);
        }

        [Test]
        public void AbsentPlayer_KeepsSuspendedBagUntilBack()
        {
            var bags = new PartyBags();
            bags.Adopt(IdA, MidDiveBagA());
            bags.Adopt(IdB, new PlayerBag());
            bags.For(IdB).Add(110); bags.For(IdB).Add(111);
            DiveSession s = AtBank(bags);
            bags.For(IdB).Pick(0);
            var (world, _) = Reload(s, NoPending);

            // only A is back; B is absent: the visit shrinks to A alone, B's bag is untouched (items still carried) and still in the next capture
            DiveSession r = SuspendSaveRules.Resume(world.Suspend, world.Bags, new DiveCheckpoint(), world.Pieces, BySlot(), new[] { 0 }, new[] { IdA }, 4242UL, -1);
            Assert.That(world.Bags.For(IdA).SlotCapacity, Is.EqualTo(3));
            Assert.That(world.Bags.For(IdB).BankOpen, Is.False);
            Assert.That(world.Bags.For(IdB).Carried, Is.EqualTo(new[] { 110, 111 }), "the absent player's items wait for them");
            TownSnapshot again = NewStock().CaptureWorld(r, false, null, Depths, null, new TownStock.SuspendContext { Seed = 4242u, PresentIds = new[] { IdA }, IdsBySlot = BySlot() });
            Assert.That(again.Suspend.Bags.Single(b => b.Id == KeyB.Id).Carried, Is.EqualTo(new[] { 110, 111 }), "and their record stays in the section");
            Assert.That(again.Suspend.PresentIds, Is.EqualTo(new[] { IdA }));

            // B comes back: a visit, nothing lost
            Assert.That(r.ResizeBankRoom(new List<int> { 0, 1 }, BySlot(), 4242UL, -1), Is.True);
            Assert.That(world.Bags.For(IdB).BankOpen, Is.True);
            Assert.That(world.Bags.For(IdB).Carried, Is.EqualTo(new[] { 110, 111 }));
        }

        static bool SameMap(IReadOnlyDictionary<int, int> a, IReadOnlyDictionary<int, int> b)
        {
            if (a.Count != b.Count) return false;
            foreach (KeyValuePair<int, int> kv in a) if (!b.TryGetValue(kv.Key, out int f) || f != kv.Value) return false;
            return true;
        }
    }
}
