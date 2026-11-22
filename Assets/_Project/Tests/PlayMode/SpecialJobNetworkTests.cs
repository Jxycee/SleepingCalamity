// Authority: test-only. Dive step 6 job 2: the special room's job on a room clear, all host-decided (CellarContentsSync.ApplySpecialJob -> DiveHaulSync.ServerMendParty / ServerShrineStir / PayExtraRolls).
// Host + one client in one process (the FloorModifierNetworkTests rig: SetupWith / ClearRoom / FindSeed are shared, heroes simulated through PlayersOverride). Seeds come from the Core SpecialRoomDive, never hard-coded,
// on floors with no floor modifier so only the special job pays.
// Catches: Quiet not mending the up heroes (or mending a wrong amount, or mending again on a re-clear); EliteDen / Treasury not paying their extra rolls to every present hero (host and client) or paying twice for one
// room; Shrine not adding its stir on its own floor, or keeping it after the party changes floor.
// NOT covered: Quiet skipping a downed hero, the Shrine stir feeding the hunter/collapse thresholds (DiveStir tests), the card/notice text, Treasury on floor 1 / Shrine on floors 1 and 3 (same code path).
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Ctx = SleepingCalamity.Tests.PlayMode.FloorModifierNetworkTests.Ctx;

namespace SleepingCalamity.Tests.PlayMode
{
    public class SpecialJobNetworkTests
    {
        private const float Timeout = 25f;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        // A floor-2 seed with no floor modifier whose special room is `kind`, has enemies, and is not the ambush room.
        private static uint SeedFor(SpecialKind kind)
        {
            return FloorModifierNetworkTests.FindSeed((s, l, m) =>
            {
                if (m.A != FloorModifier.None || m.B != FloorModifier.None) return false;
                if (kind == SpecialKind.Shrine && FloorModifierSet.For(s, 3).Has(FloorModifier.Stirred)) return false;   // the test walks to floor 3 and expects no stir there
                CellarFloorDressing d = CellarFloorDressing.Resolve(l);
                SpecialRoom sp = SpecialRoomDive.Resolve(l, null, d);
                if (sp.Kind != kind || sp.RoomIndex < 0) return false;
                CellarEncounterPlan p = CellarEncounterPlan.BuildForDive(l, d, 0, false);
                return FloorModifierNetworkTests.SpawnIn(p, sp.RoomIndex) != null && p.AmbushRoom != sp.RoomIndex;
            });
        }

        private static SpecialRoom SpecialOf(Ctx c) => SpecialRoomDive.Resolve(c.Layout, null, c.Floor.Built.Dressing);
        private static CellarEncounterPlan PlanOf(Ctx c) => CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false);

        private static void ReClear(Ctx c, int room) =>   // the once-per-room guard: PayRoomClear is the call every clear check makes
            typeof(CellarContentsSync).GetMethod("PayRoomClear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(c.Contents, new object[] { room });

        private static HeroVitals HeroOf(Ctx c, ulong id) => c.HostNm.ConnectedClients[id].PlayerObject.GetComponent<HeroVitals>();

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // ---- Quiet: mends every up hero by MendPercent of max HP, once ----

        [UnityTest]
        public IEnumerator Quiet_ClearingMendsTheUpHeroes_Once()
        {
            uint seed = SeedFor(SpecialKind.Quiet);
            Ctx c = null; yield return FloorModifierNetworkTests.SetupWith(_clients, x => c = x, seed);
            SpecialRoom sp = SpecialOf(c);
            Assert.That(sp.Kind, Is.EqualTo(SpecialKind.Quiet), "premise");
            HeroVitals host = HeroOf(c, c.HostId), client = HeroOf(c, c.ClientId);
            int hurt = host.MaxHp * 6 / 10;
            host.ServerDamage(hurt); client.ServerDamage(hurt);
            int h0 = host.Hp, k0 = client.Hp;
            Assert.That(h0, Is.LessThan(host.MaxHp), "premise: the host hero is hurt");
            int mend = SpecialJobs.MendAmount(host.MaxHp, SpecialJobTuning.MendPercent);

            yield return FloorModifierNetworkTests.ClearRoom(c, PlanOf(c), sp.RoomIndex);
            yield return PlayModeHarness.WaitUntil(() => host.Hp == h0 + mend && client.Hp == k0 + mend, Timeout, "the clear never mended both heroes (host " + host.Hp + " from " + h0 + ", client " + client.Hp + " from " + k0 + ", +" + mend + ")");

            host.ServerDamage(10); client.ServerDamage(10);
            int h1 = host.Hp, k1 = client.Hp;
            ReClear(c, sp.RoomIndex);
            yield return Seconds(0.5f);
            Assert.That(host.Hp, Is.EqualTo(h1), "a second clear of the same room mends nothing (host)");
            Assert.That(client.Hp, Is.EqualTo(k1), "(client)");
        }

        // ---- EliteDen / Treasury: extra rolls to every present hero, once per room ----

        private IEnumerator ExtraRolls(SpecialKind kind, int rolls)
        {
            uint seed = SeedFor(kind);
            Ctx c = null; yield return FloorModifierNetworkTests.SetupWith(_clients, x => c = x, seed);
            SpecialRoom sp = SpecialOf(c);
            Assert.That(sp.Kind, Is.EqualTo(kind), "premise");
            int h0 = c.Haul.ServerBagOf(c.HostId).Carried.Count, k0 = c.Haul.ServerBagOf(c.ClientId).Carried.Count;
            int hg0 = FloorModifierNetworkTests.DiveGold(c, c.HostId), kg0 = FloorModifierNetworkTests.DiveGold(c, c.ClientId);

            yield return FloorModifierNetworkTests.ClearRoom(c, PlanOf(c), sp.RoomIndex);
            AssertRolls(c, c.HostId, sp.RoomIndex, rolls, h0, hg0, "host");
            AssertRolls(c, c.ClientId, sp.RoomIndex, rolls, k0, kg0, "client");

            int h1 = c.Haul.ServerBagOf(c.HostId).Carried.Count, k1 = c.Haul.ServerBagOf(c.ClientId).Carried.Count;
            int hg1 = FloorModifierNetworkTests.DiveGold(c, c.HostId), kg1 = FloorModifierNetworkTests.DiveGold(c, c.ClientId);
            ReClear(c, sp.RoomIndex);
            Assert.That(c.Haul.ServerBagOf(c.HostId).Carried.Count, Is.EqualTo(h1), "a second clear pays the host nothing");
            Assert.That(c.Haul.ServerBagOf(c.ClientId).Carried.Count, Is.EqualTo(k1), "nor the client");
            Assert.That(FloorModifierNetworkTests.DiveGold(c, c.HostId), Is.EqualTo(hg1));
            Assert.That(FloorModifierNetworkTests.DiveGold(c, c.ClientId), Is.EqualTo(kg1));
        }

        // The rolls the job makes: chest index 70000 + room, + 1000 k for k = 1..rolls (a fresh bag, no rank bonus on these floors); gold goes to the purse, items to the bag.
        private static void AssertRolls(Ctx c, ulong id, int room, int rolls, int items0, int gold0, string who)
        {
            int items = 0, gold = 0;
            for (int k = 1; k <= rolls; k++)
            {
                int item = CellarLoot.RollChest(c.Seed, c.FloorNo, SpecialJobTuning.ExtraRollChestBase + room + 1000 * k, c.Haul.PlayerSlotOf(id), 0, false);
                if (item == CellarLoot.GoldId) gold += CellarLoot.GoldPurse; else items++;
            }
            Assert.That(c.Haul.ServerBagOf(id).Carried.Count, Is.EqualTo(items0 + items), who + ": the rolled items (" + rolls + " rolls)");
            Assert.That(FloorModifierNetworkTests.DiveGold(c, id), Is.EqualTo(gold0 + gold), who + ": a purse for each gold roll");
        }

        [UnityTest] public IEnumerator EliteDen_PaysItsRollsToEveryPresentHero_Once() { yield return ExtraRolls(SpecialKind.EliteDen, SpecialJobTuning.EliteDenRolls); }
        [UnityTest] public IEnumerator Treasury_PaysItsRollsToEveryPresentHero_Once() { yield return ExtraRolls(SpecialKind.Treasury, SpecialJobTuning.TreasuryRolls); }

        // ---- Shrine: stir on its own floor only ----

        [UnityTest]
        public IEnumerator Shrine_AddsStirOnItsFloor_AndLosesItOnTheNext()
        {
            uint seed = SeedFor(SpecialKind.Shrine);
            Ctx c = null; yield return FloorModifierNetworkTests.SetupWith(_clients, x => c = x, seed);
            SpecialRoom sp = SpecialOf(c);
            Assert.That(sp.Kind, Is.EqualTo(SpecialKind.Shrine), "premise");
            Assert.That(c.Haul.Session.Stir.ModifierBonus, Is.EqualTo(0f), "no stir before the altar");

            yield return FloorModifierNetworkTests.ClearRoom(c, PlanOf(c), sp.RoomIndex);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Stir.ModifierBonus == SpecialJobTuning.ShrineStir, Timeout, "the altar never added its stir (bonus " + c.Haul.Session.Stir.ModifierBonus + ")");
            Assert.That(c.Haul.ShrineFloor, Is.EqualTo(c.FloorNo));

            Assert.That(c.Haul.ServerSetFloor(3), Is.True);
            Assert.That(c.Floor.ServerSetFloor(seed, 3), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Floor.BuiltFloor == 3 && c.Floor.Built != null, Timeout, "floor 3 never built");
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Stir.ModifierBonus == 0f, Timeout, "the shrine's stir followed the party to the next floor");
        }
    }
}
