// Per-player loot plan task 3: the optional world-save section "bags" (Core/Save/BagsSave.cs). Pure Core + NUnit (no Newtonsoft here, so Tools/cloud/sc_core_tests.py runs it; the
// JSON round trip is BagsSaveJsonTests).
// Catches: unbanked items written into the stash or the stock on a mid-dive save (or duplicated), the live bag changed by capturing it, a client-id fallback bag or an
// unrecorded player written to disk, one bad entry costing the whole section, an out-of-range gold/stash/satchel surviving a load, a restore that does not give the same bag back,
// a world with no bags writing a "bags" key.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class BagsSaveTests
    {
        static readonly PlayerKey KeyA = new PlayerKey(IdentityKind.Local, "aaaa");
        static readonly PlayerKey KeyB = new PlayerKey(IdentityKind.Local, "bbbb");

        static IReadOnlyDictionary<string, PlayerKey> Keys(params PlayerKey[] keys) => keys.ToDictionary(k => k.ToString(), k => k);

        // A bag in the middle of a dive: carried 104, banked 101, 7 dive gold, 5 gold at home, 109 in the stash.
        static PlayerBag MidDiveBag()
        {
            PlayerBag b = PlayerBag.Restore(5, new Dictionary<int, int> { { 109, 1 } }, null);
            b.Add(101); b.OpenBank(1); b.Pick(0); b.PushOn();   // 101 banked this dive
            b.Add(104);
            b.AddGold(7);
            return b;
        }

        [Test] public void Capture_WritesRecordedPlayersOnly()
        {
            var bags = new PartyBags();
            bags.For(KeyA.ToString()).AddGold(4);
            bags.For(KeyA.ToString()).GoHome();
            bags.For(KeyB.ToString()).AddGold(4);                 // B is not recorded
            bags.For("3").AddGold(4); bags.For("3").GoHome();      // a client-id fallback bag
            BagsSave save = BagsSaveRules.Capture(bags, Keys(KeyA), false, 0);
            Assert.That(save.Players.Count, Is.EqualTo(1));
            Assert.That(save.Players[0].Id, Is.EqualTo("aaaa"));
            Assert.That(save.Players[0].Identity, Is.EqualTo((int)IdentityKind.Local));
            Assert.That(save.Players[0].Gold, Is.EqualTo(4));
        }

        [Test] public void Capture_DoesNotCap_SoTheHostFiltersToRecordedPlayersBeforeTheCap()
        {
            // Five parseable bags (the host filters to the world's recorded players afterwards): all five are written, none is dropped by the 4-entry cap before that filter.
            var bags = new PartyBags();
            var keys = new List<PlayerKey>();
            foreach (string id in new[] { "aaaa", "bbbb", "cccc", "dddd", "eeee" })
            {
                var k = new PlayerKey(IdentityKind.Local, id);
                keys.Add(k);
                bags.For(k.ToString()).AddGold(3); bags.For(k.ToString()).GoHome();
            }
            BagsSave save = BagsSaveRules.Capture(bags, Keys(keys.ToArray()), false, 0);
            Assert.That(save.Players.Count, Is.EqualTo(5));
            Assert.That(BagsSaveRules.Clean(save, null).Players.Count, Is.EqualTo(WorldPlayers.MaxRecorded), "Clean still caps (the file never holds more than the world records)");
        }

        [Test] public void Capture_SkipsEmptyBags_SoAWorldWithoutLootWritesNoEntries()
        {
            var bags = new PartyBags();
            bags.For(KeyA.ToString());
            BagsSave save = BagsSaveRules.Capture(bags, Keys(KeyA), false, 0);
            Assert.That(save.Players, Is.Empty);
            Assert.That(BagsSaveRules.IsPristine(save), Is.True);
            Assert.That(BagsSaveRules.IsPristine(null), Is.True);
        }

        [Test] public void CaptureMidDive_WritesTheFailedCopy()
        {
            var bags = new PartyBags();
            bags.Adopt(KeyA.ToString(), MidDiveBag());
            PlayerBag live = bags.For(KeyA.ToString());
            BagsSave save = BagsSaveRules.Capture(bags, Keys(KeyA), true, 2);
            PlayerBagSave p = save.Players.Single();
            Assert.That(p.Gold, Is.EqualTo(5 + 3), "5 at home + half of the 7 dive gold");
            Assert.That(p.Stash.ToDictionary(r => r.ItemId, r => r.Count), Is.EqualTo(new Dictionary<int, int> { { 101, 1 }, { 109, 1 } }), "banked 101 is safe; the carried 104 is NOT in the stash");
            Assert.That(p.Satchel.Floor, Is.EqualTo(2));
            Assert.That(p.Satchel.Items, Is.EqualTo(new[] { 104 }));
            // the live bag is untouched
            Assert.That(live.Carried, Is.EqualTo(new[] { 104 }));
            Assert.That(live.DiveGold, Is.EqualTo(7));
            Assert.That(live.BankedThisDive[101], Is.EqualTo(1));
            Assert.That(live.Gold, Is.EqualTo(5));
            Assert.That(live.Satchel.Waiting, Is.Null);
        }

        [Test] public void CaptureMidDive_ACarriedSatchelIsLost_NotSavedTwice()
        {
            PlayerBag b = PlayerBag.Restore(0, null, new LostSatchel(3, new[] { 105 }));
            b.Satchel.PickUp();
            var bags = new PartyBags(); bags.Adopt(KeyA.ToString(), b);
            BagsSave save = BagsSaveRules.Capture(bags, Keys(KeyA), true, 3);
            Assert.That(save.Players, Is.Empty, "the satchel was carried when the save was written: lost, like a failed dive");
        }

        [Test] public void RoundTrip_GoldStashSatchel()
        {
            var bags = new PartyBags();
            bags.Adopt(KeyA.ToString(), PlayerBag.Restore(12, new Dictionary<int, int> { { 104, 2 }, { 110, 1 } }, new LostSatchel(3, new[] { 105, 106 })));
            bags.Adopt(KeyB.ToString(), PlayerBag.Restore(1, null, null));
            BagsSave captured = BagsSaveRules.Capture(bags, Keys(KeyA, KeyB), false, 0);
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(captured, dropped);
            Assert.That(dropped, Is.Empty);
            var into = new PartyBags();
            BagsSaveRules.Restore(clean, into);
            Assert.That(into.All.Select(kv => kv.Key), Is.EqualTo(new[] { KeyA.ToString(), KeyB.ToString() }));
            PlayerBag a = into.For(KeyA.ToString());
            Assert.That(a.Gold, Is.EqualTo(12));
            Assert.That(a.Stash.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + kv.Value), Is.EqualTo(new[] { "104:2", "110:1" }));
            Assert.That(a.Satchel.Waiting.Floor, Is.EqualTo(3));
            Assert.That(a.Satchel.Waiting.Items, Is.EqualTo(new[] { 105, 106 }));
            Assert.That(a.Satchel.Carried, Is.Null);
            Assert.That(a.Carried, Is.Empty); Assert.That(a.DiveGold, Is.EqualTo(0)); Assert.That(a.BankOpen, Is.False);
            Assert.That(into.For(KeyB.ToString()).Gold, Is.EqualTo(1));
            Assert.That(into.For(KeyB.ToString()).Satchel.Waiting, Is.Null);
        }

        [Test] public void Clean_DropsOnlyTheBadEntry()
        {
            var raw = new BagsSave();
            raw.Players.Add(new PlayerBagSave { Identity = 99, Id = "badbad", Gold = 5 });
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "good", Gold = 6 });
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(raw, dropped);
            Assert.That(clean.Players.Select(p => p.Id), Is.EqualTo(new[] { "good" }));
            Assert.That(clean.Players[0].Gold, Is.EqualTo(6));
            Assert.That(dropped, Is.EqualTo(new[] { "bags.badbad" }));
        }

        [Test] public void Clean_DropsEmptyLongNullAndDuplicateIds_AndCapsTheCount()
        {
            var raw = new BagsSave();
            raw.Players.Add(null);
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "" });
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = new string('x', PlayerKey.MaxIdLength + 1) });
            raw.Players.Add(new PlayerBagSave { Identity = -1, Id = "noidentity" });
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "p1", Gold = 1 });
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "p1", Gold = 2 });      // duplicate: the first stays
            raw.Players.Add(new PlayerBagSave { Identity = 1, Id = "p1", Gold = 3 });      // same id, other kind: a different player
            for (int i = 2; i <= 5; i++) raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "p" + i });
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(raw, dropped);
            Assert.That(clean.Players.Count, Is.EqualTo(WorldPlayers.MaxRecorded));
            Assert.That(clean.Players.Select(p => p.Identity + ":" + p.Id), Is.EqualTo(new[] { "0:p1", "1:p1", "0:p2", "0:p3" }));
            Assert.That(clean.Players[0].Gold, Is.EqualTo(1), "the first of a duplicate is kept");
            Assert.That(dropped.Count, Is.EqualTo(4 + 1 + 2), "null, empty, long, no identity, the duplicate and the two past the cap");
            Assert.That(dropped.All(d => d.StartsWith("bags.")), Is.True);
        }

        [Test] public void Clean_ClampsAndDropsBadRows()
        {
            var raw = new BagsSave();
            var p = new PlayerBagSave { Identity = 0, Id = "p", Gold = 5_000_000 };
            p.Stash.Add(new LootCountSave { ItemId = 104, Count = 3 });
            p.Stash.Add(new LootCountSave { ItemId = 104, Count = 4 });                   // merged
            p.Stash.Add(new LootCountSave { ItemId = 105, Count = 0 });                   // dropped
            p.Stash.Add(new LootCountSave { ItemId = 106, Count = -2 });                  // dropped
            p.Stash.Add(new LootCountSave { ItemId = 0, Count = 2 });                     // dropped: not an item
            p.Stash.Add(new LootCountSave { ItemId = 107, Count = TownSaveRules.MaxLootCount + 50 });   // clamped
            p.Satchel = new SatchelSave { Floor = 2, Items = new List<int> { 110, 0, -4, 111 } };
            raw.Players.Add(p);
            var neg = new PlayerBagSave { Identity = 0, Id = "neg", Gold = -9, Satchel = new SatchelSave { Floor = 11, Items = new List<int> { 110 } } };
            raw.Players.Add(neg);
            var empty = new PlayerBagSave { Identity = 0, Id = "empty", Satchel = new SatchelSave { Floor = 1, Items = new List<int> { 0 } }, Stash = null };
            raw.Players.Add(empty);
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(raw, dropped);
            Assert.That(clean.Players.Count, Is.EqualTo(3), "bad rows never cost the player");
            PlayerBagSave c = clean.Players[0];
            Assert.That(c.Gold, Is.EqualTo(TownSaveRules.MaxLootCount));
            Assert.That(c.Stash.Select(r => r.ItemId + ":" + r.Count), Is.EqualTo(new[] { "104:7", "107:" + TownSaveRules.MaxLootCount }));
            Assert.That(c.Satchel.Items, Is.EqualTo(new[] { 110, 111 }));
            Assert.That(c.Satchel.Floor, Is.EqualTo(2));
            Assert.That(clean.Players[1].Gold, Is.EqualTo(0));
            Assert.That(clean.Players[1].Satchel, Is.Null, "floor 11 is not a floor");
            Assert.That(clean.Players[2].Satchel, Is.Null, "an empty satchel is no satchel");
            Assert.That(clean.Players[2].Stash, Is.Not.Null.And.Empty);
            Assert.That(dropped.Count, Is.GreaterThan(0));
        }

        // Catches: the satchel's floor range still stopping at the old 4 floors (a satchel lost on floor 7 or 9 would be dropped on load), or no floor being refused.
        [Test] public void Satchel_AtFloor9_IsKept()
        {
            var kept = new List<int>();
            foreach (int floor in new[] { 0, 1, 7, 9, UndercroftPlan.BossFloor, 11, -2 })
            {
                var raw = new BagsSave();   // one entry per clean: Clean keeps at most WorldPlayers.MaxRecorded
                raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "p", Gold = 1, Satchel = new SatchelSave { Floor = floor, Items = new List<int> { 110 } } });
                PlayerBagSave p = BagsSaveRules.Clean(raw, null).Players.Single();
                kept.Add(p.Satchel == null ? 0 : p.Satchel.Floor);
            }
            Assert.That(kept, Is.EqualTo(new[] { 0, 1, 7, 9, 10, 0, 0 }));
        }

        [Test] public void Clean_KeepsUnknownMembers_AndNullStaysEmpty()
        {
            var raw = new BagsSave { Extra = new Dictionary<string, object> { { "future", 1 } } };
            raw.Players.Add(new PlayerBagSave { Identity = 0, Id = "p", Extra = new Dictionary<string, object> { { "more", "x" } } });
            BagsSave clean = BagsSaveRules.Clean(raw, new List<string>());
            Assert.That(clean.Extra.ContainsKey("future"), Is.True);
            Assert.That(clean.Players[0].Extra.ContainsKey("more"), Is.True);
            Assert.That(BagsSaveRules.Clean(null, new List<string>()).Players, Is.Empty);
        }

        [Test] public void TryParseBagId_RoundTripsAPlayerKeyString_AndRefusesClientIds()
        {
            Assert.That(BagsSaveRules.TryParseBagId(KeyA.ToString(), out PlayerKey k), Is.True);
            Assert.That(k, Is.EqualTo(KeyA));
            Assert.That(BagsSaveRules.TryParseBagId("3", out _), Is.False);
            Assert.That(BagsSaveRules.TryParseBagId("1:abc", out _), Is.False, "a numeric kind is not a kind name");
            Assert.That(BagsSaveRules.TryParseBagId("Nope:abc", out _), Is.False);
            Assert.That(BagsSaveRules.TryParseBagId("Local:", out _), Is.False);
            Assert.That(BagsSaveRules.TryParseBagId(null, out _), Is.False);
        }
    }
}
