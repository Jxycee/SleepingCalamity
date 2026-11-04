// Per-player loot plan task 3: the "bags" section through Newtonsoft JSON (the wire form the world file uses). Split from BagsSaveTests because it uses Newtonsoft (the
// PC core runner skips files that do; the Unity gate runs this one).
// Catches: a renamed or retyped field in the on-disk form, capture -> file -> clean -> restore not giving the same bag back, unknown members dropped, a saved bag restoring with
// carried items or dive gold, a WorldSave without bags writing a "bags" key.
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class BagsSaveJsonTests
    {
        static readonly PlayerKey KeyA = new PlayerKey(IdentityKind.Local, "aaaa");

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true } },
            Formatting = Formatting.Indented,
        };

        [Test] public void RoundTrip_ThroughJson_GoldStashSatchel()
        {
            var bags = new PartyBags();
            bags.Adopt(KeyA.ToString(), PlayerBag.Restore(12, new Dictionary<int, int> { { 104, 2 }, { 110, 1 } }, new LostSatchel(3, new[] { 105, 106 })));
            BagsSave captured = BagsSaveRules.Capture(bags, new Dictionary<string, PlayerKey> { { KeyA.ToString(), KeyA } }, false, 0);
            string json = JsonConvert.SerializeObject(captured, Settings);
            Assert.That(json, Does.Contain("\"players\"").And.Contain("\"gold\": 12").And.Contain("\"satchel\"").And.Contain("\"stash\"").And.Contain("\"itemId\": 104"));
            BagsSave loaded = JsonConvert.DeserializeObject<BagsSave>(json, Settings);
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(loaded, dropped);
            Assert.That(dropped, Is.Empty);
            var into = new PartyBags();
            BagsSaveRules.Restore(clean, into);
            PlayerBag a = into.For(KeyA.ToString());
            Assert.That(a.Gold, Is.EqualTo(12));
            Assert.That(a.Stash.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + kv.Value), Is.EqualTo(new[] { "104:2", "110:1" }));
            Assert.That(a.Satchel.Waiting.Floor, Is.EqualTo(3));
            Assert.That(a.Satchel.Waiting.Items, Is.EqualTo(new[] { 105, 106 }));
            Assert.That(a.Carried, Is.Empty); Assert.That(a.DiveGold, Is.EqualTo(0));
        }

        [Test] public void UnknownMembers_AreWrittenBack()
        {
            const string json = "{ \"players\": [ { \"identity\": 0, \"id\": \"p\", \"gold\": 3, \"future\": [1, 2] } ], \"alsoNew\": true }";
            BagsSave clean = BagsSaveRules.Clean(JsonConvert.DeserializeObject<BagsSave>(json, Settings), new List<string>());
            string back = JsonConvert.SerializeObject(clean, Settings);
            Assert.That(back, Does.Contain("\"future\"").And.Contain("\"alsoNew\""));
        }

        [Test] public void AMissingStashOrSatchel_Loads()
        {
            BagsSave loaded = JsonConvert.DeserializeObject<BagsSave>("{ \"players\": [ { \"identity\": 0, \"id\": \"p\" }, { \"identity\": 0, \"id\": \"q\", \"stash\": null, \"satchel\": null } ] }", Settings);
            var dropped = new List<string>();
            BagsSave clean = BagsSaveRules.Clean(loaded, dropped);
            Assert.That(clean.Players.Count, Is.EqualTo(2)); Assert.That(dropped, Is.Empty);
            Assert.That(clean.Players.All(p => p.Satchel == null && p.Stash.Count == 0 && p.Gold == 0), Is.True);
        }

        [Test] public void AWorldSaveWithoutBags_WritesNoBagsKey()
        {
            var save = new WorldSave();
            Assert.That(JsonConvert.SerializeObject(save, Settings), Does.Not.Contain("bags"));
            save.Bags = new BagsSave();
            save.Bags.Players.Add(new PlayerBagSave { Identity = 0, Id = "p", Gold = 1 });
            Assert.That(JsonConvert.SerializeObject(save, Settings), Does.Contain("\"bags\""));
        }

        [Test] public void WorldSaveRules_CleansTheBagsSection_AndNamesItWhenAnEntryWasDropped()
        {
            var save = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(5UL), Bags = new BagsSave() };
            save.Bags.Players.Add(new PlayerBagSave { Identity = 99, Id = "bad", Gold = 1 });
            save.Bags.Players.Add(new PlayerBagSave { Identity = 0, Id = "good", Gold = 2 });
            var rejected = new List<string>();
            WorldSave clean = WorldSaveRules.Clean(save, 1UL, rejected);
            Assert.That(clean.Bags.Players.Select(p => p.Id), Is.EqualTo(new[] { "good" }));
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionBags }), "named, so the store keeps the original as .corrupt");
            Assert.That(clean.WorldSeed, Is.EqualTo(WorldSaveRules.SeedToHex(5UL)));

            var good = new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(5UL), Bags = new BagsSave() };
            good.Bags.Players.Add(new PlayerBagSave { Identity = 0, Id = "good", Gold = 2 });
            WorldSaveRules.Clean(good, 1UL, rejected);
            Assert.That(rejected, Is.Empty);
        }

        [Test] public void NoBagsSection_StaysNull()
        {
            WorldSave clean = WorldSaveRules.Clean(new WorldSave { WorldSeed = WorldSaveRules.SeedToHex(5UL) }, 1UL, null);
            Assert.That(clean.Bags, Is.Null);
        }
    }
}
