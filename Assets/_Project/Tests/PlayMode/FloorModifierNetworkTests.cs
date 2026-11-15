// Authority: test-only. Plan 5 task 5: the floor modifiers' effects and printed rewards, all host-decided (CellarContentsSync for rooms, chests and the hunter; DiveHaulSync for stir, the collapse, healing
// recharge and the leave reward). Host + one client in one process (scene management off); the heroes are simulated through CellarContentsSync.PlayersOverride (no hero objects), so ranges are judged on
// the positions the test sets. Seeds are found with the Core FloorModifierSet, never hard-coded.
// Catches: Stirred's flat stir missing or staying after the party leaves the floor, its extra chest roll missing or bypassing the bag; Crowded / Ambush gold missing, paid to one player only, or paid for the wrong
// room; Hurried's collapse still 1.5x or its leave gold missing; Hushed still spawning the latched hunter (and the control floor not spawning it); Warm Hands' recharge unchanged; Dim's rank bonus missing from
// chest rolls.
// NOT covered: Dim's darkness (the atmosphere ships already and is judged on a walk), the card text (FloorCardTests), the effect numbers (FloorModifierTests), held-input feel.
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class FloorModifierNetworkTests
    {
        private const float Timeout = 25f;
        private const int Valuable = 101;   // a 5-stir item (DiveStir.PerUnbankedValuable)
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        private sealed class Ctx
        {
            public NetworkManager HostNm; public PlayModeHarness.TestClient Client;
            public CellarFloorSync Floor; public DiveHaulSync Haul; public CellarContentsSync Contents, Peer;
            public CellarFloorLayout Layout; public uint Seed; public int FloorNo;
            public readonly Dictionary<ulong, Vector3> Players = new Dictionary<ulong, Vector3>();
            public ulong HostId, ClientId;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private static GameObject PrefabWith<T>(NetworkManager host) where T : Component
        {
            foreach (NetworkPrefabsList list in host.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab;
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static T Spawn<T>(NetworkManager host) where T : Component
        {
            NetworkObject o = host.SpawnManager.InstantiateAndSpawn(PrefabWith<T>(host).GetComponent<NetworkObject>(), host.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            return o.GetComponent<T>();
        }

        private static uint FindSeed(System.Func<uint, CellarFloorLayout, FloorModifierSet, bool> want, int floor = 2, uint limit = 8000)
        {
            for (uint s = 1; s < limit; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                if (want(s, l, FloorModifierSet.For(s, floor))) return s;
            }
            Assert.Fail("no seed matches on floor " + floor); return 0;
        }

        private static int LootChest(CellarFloorLayout l) { for (int i = 0; i < l.Chests.Count; i++) if (!l.Chests[i].HoldsKey) return i; return -1; }

        private IEnumerator Setup(System.Action<Ctx> done, uint seed, int floor = 2)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            var c = new Ctx { HostNm = app.NetworkManager, Seed = seed, FloorNo = floor };
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client); c.Client = client;
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client never connected");

            // A present hero = a connected client with a player object (the payouts skip clients without one). Positions are judged through PlayersOverride, so these stay parked.
            c.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, c.HostNm.LocalClientId, false, true, false, new Vector3(0f, -50f, 0f), Quaternion.identity);
            c.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, client.NetworkManager.LocalClientId, false, true, false, new Vector3(2f, -50f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.LocalClient != null && client.NetworkManager.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");

            c.Floor = Spawn<CellarFloorSync>(c.HostNm); c.Haul = Spawn<DiveHaulSync>(c.HostNm); c.Contents = Spawn<CellarContentsSync>(c.HostNm);
            ulong id = c.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client never received the contents object");
            c.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();
            foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == client.NetworkManager) c.Peer.FloorSync = f;
            c.Contents.FloorSync = c.Floor; c.Contents.HaulSync = c.Haul; c.Haul.FloorSync = c.Floor;
            Assert.That(c.Peer.FloorSync, Is.Not.Null, "the client has its own floor sync");

            c.HostId = c.HostNm.LocalClientId; c.ClientId = client.NetworkManager.LocalClientId;
            c.Contents.PlayersOverride = list => { foreach (KeyValuePair<ulong, Vector3> kv in c.Players) list.Add(new CellarContentsSync.PlayerPos { ClientId = kv.Key, Position = kv.Value }); };

            c.Haul.DiveSeed = seed;
            Assert.That(c.Haul.ServerBegin(), Is.True);
            Assert.That(c.Haul.ServerSetFloor(floor), Is.True);
            Assert.That(c.Floor.ServerSetFloor(seed, floor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.FloorSync.Built != null && c.Peer.FloorSync.BuiltFloor == floor, Timeout, "floors were not built");
            c.Layout = c.Floor.Layout;
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.Floor == floor && c.Contents.State.Floor == floor && c.Contents.Plan != null, Timeout, "the contents state never reached both peers");
            done(c);
        }

        private static Vector3 Cell(Ctx c, int x, int y) => c.Floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(x, y));
        private static int DiveGold(Ctx c, ulong id) => c.Haul.ServerBagOf(id).DiveGold;

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        private static void KillEveryEnemy(NetworkManager nm)
        {
            var hit = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            foreach (NetworkObject o in new List<NetworkObject>(nm.SpawnManager.SpawnedObjects.Values))
            {
                Enemy e = o != null ? o.GetComponent<Enemy>() : null;
                for (int i = 0; e != null && i < 6 && e.Alive; i++) e.ServerApplyHit(hit, e.transform.position + Vector3.back, Time.timeAsDouble);
            }
        }

        // The first room with plan spawns: the host steps into it (spawning them), then everything dies. Returns the room.
        private static IEnumerator ClearARoom(Ctx c, System.Action<int> room)
        {
            CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false);
            int r = -1; EncounterSpawn first = null;
            foreach (EncounterSpawn sp in plan.Spawns) if (sp.Kind != EncounterKind.Hunter && (plan.Twist != FloorTwist.Ambush || sp.RoomIndex != plan.AmbushRoom)) { r = sp.RoomIndex; first = sp; break; }
            Assert.That(r, Is.GreaterThanOrEqualTo(0), "premise: the floor has a room with enemies");
            c.Players[c.HostId] = Cell(c, first.X, first.Y) + Vector3.forward * 0.1f;
            c.Players[c.ClientId] = Cell(c, first.X, first.Y) + Vector3.back * 0.3f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(r) > 0, Timeout, "the room never spawned its enemies");
            KillEveryEnemy(c.HostNm);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.AliveInRoom(r) == 0 && (c.Contents.State.Cleared & (1UL << r)) != 0, Timeout, "the room never cleared");
            room(r);
        }

        // The room ClearARoom picks: the first room with a non-hunter spawn (and, on an Ambush floor, not the ambush room).
        private static int FirstClearRoom(CellarEncounterPlan plan)
        {
            foreach (EncounterSpawn sp in plan.Spawns) if (sp.Kind != EncounterKind.Hunter && (plan.Twist != FloorTwist.Ambush || sp.RoomIndex != plan.AmbushRoom)) return sp.RoomIndex;
            return -1;
        }

        // The first room with a non-hunter spawn other than `not` (the fat room is the first Combat room, so the ordinary room is a later one).
        private static int OtherRoom(CellarEncounterPlan plan, int not)
        {
            foreach (EncounterSpawn sp in plan.Spawns) if (sp.Kind != EncounterKind.Hunter && sp.RoomIndex != not) return sp.RoomIndex;
            return -1;
        }

        private static EncounterSpawn SpawnIn(CellarEncounterPlan plan, int room)
        {
            foreach (EncounterSpawn sp in plan.Spawns) if (sp.RoomIndex == room && sp.Kind != EncounterKind.Hunter) return sp;
            return null;
        }

        // Clears one named room: both heroes step in, everything dies, wait for the clear. Time-boxed waits only.
        private static IEnumerator ClearRoom(Ctx c, CellarEncounterPlan plan, int r)
        {
            EncounterSpawn first = SpawnIn(plan, r);
            Assert.That(first, Is.Not.Null, "premise: room " + r + " has enemies");
            c.Players[c.HostId] = Cell(c, first.X, first.Y) + Vector3.forward * 0.1f;
            c.Players[c.ClientId] = Cell(c, first.X, first.Y) + Vector3.back * 0.3f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(r) > 0, Timeout, "room " + r + " never spawned its enemies");
            KillEveryEnemy(c.HostNm);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.AliveInRoom(r) == 0 && (c.Contents.State.Cleared & (1UL << r)) != 0, Timeout, "room " + r + " never cleared");
        }

        // What one extra roll on `room` gives `id` (GiveExtraRoll: chest index 50000 + room, + 1000 * k for k = 1). A fresh bag, no rank bonus on these floors.
        private static int ExtraRollItem(Ctx c, ulong id, int room) => CellarLoot.RollChest(c.Seed, c.FloorNo, 51000 + room, c.Haul.PlayerSlotOf(id), 0, false);

        private static bool RollsAnItemForEverySlot(uint seed, int floor, int room)
        {
            for (int slot = 0; slot < 4; slot++) if (CellarLoot.RollChest(seed, floor, 51000 + room, slot, 0, false) == CellarLoot.GoldId) return false;
            return true;
        }

        private static void AssertPaid(Ctx c, ulong id, int room, int items0, int gold0, string who)
        {
            bool gold = ExtraRollItem(c, id, room) == CellarLoot.GoldId;
            Assert.That(c.Haul.ServerBagOf(id).Carried.Count, Is.EqualTo(items0 + (gold ? 0 : 1)), who + ": exactly BrimmingRollsPerRoom (" + FloorModifierTuning.BrimmingRollsPerRoom + ") item roll(s)");
            Assert.That(DiveGold(c, id), Is.EqualTo(gold0 + (gold ? CellarLoot.GoldPurse : 0)), who + ": a purse only when the roll is gold");
        }

        // ---- Brimming / Fat Larder (extra rolls on a room clear; host-paid to every client with a hero object) ----

        [UnityTest]
        public IEnumerator Brimming_EveryPresentClientGetsTheRoll_AndAReClearPaysNothing()
        {
            Assert.That(FloorModifierTuning.BrimmingRollsPerRoom, Is.EqualTo(1), "the test's expectation is written for one roll per room");
            uint seed = FindSeed((s, l, m) => m.Has(FloorModifier.Brimming) && FirstClearRoom(CellarEncounterPlan.BuildForDive(l, CellarFloorDressing.Resolve(l), 0, false)) >= 0);
            Ctx c = null; yield return Setup(x => c = x, seed);
            int h0 = c.Haul.ServerBagOf(c.HostId).Carried.Count, k0 = c.Haul.ServerBagOf(c.ClientId).Carried.Count, hg0 = DiveGold(c, c.HostId), kg0 = DiveGold(c, c.ClientId);
            int room = -1;
            yield return ClearARoom(c, r => room = r);
            AssertPaid(c, c.HostId, room, h0, hg0, "host");
            AssertPaid(c, c.ClientId, room, k0, kg0, "client");

            // the room resets and clears again: the host's once-per-room guard pays nothing (PayRoomClear is the call the clear checks make)
            int h1 = c.Haul.ServerBagOf(c.HostId).Carried.Count, k1 = c.Haul.ServerBagOf(c.ClientId).Carried.Count, hg1 = DiveGold(c, c.HostId), kg1 = DiveGold(c, c.ClientId);
            typeof(CellarContentsSync).GetMethod("PayRoomClear", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(c.Contents, new object[] { room });
            Assert.That(c.Haul.ServerBagOf(c.HostId).Carried.Count, Is.EqualTo(h1), "a second clear of the same room gives the host nothing");
            Assert.That(c.Haul.ServerBagOf(c.ClientId).Carried.Count, Is.EqualTo(k1), "nor the client");
            Assert.That(DiveGold(c, c.HostId), Is.EqualTo(hg1));
            Assert.That(DiveGold(c, c.ClientId), Is.EqualTo(kg1));
        }

        [UnityTest]
        public IEnumerator FatLarder_OnlyTheFatRoomPays()
        {
            uint seed = FindSeed((s, l, m) =>
            {
                if (!m.Has(FloorModifier.FatLarder)) return false;
                CellarEncounterPlan p = CellarEncounterPlan.BuildForDive(l, CellarFloorDressing.Resolve(l), 0, false);
                return p.FatRoom >= 0 && SpawnIn(p, p.FatRoom) != null && OtherRoom(p, p.FatRoom) >= 0;
            });
            Ctx c = null; yield return Setup(x => c = x, seed);
            CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false);
            int plain = OtherRoom(plan, plan.FatRoom);
            int h0 = c.Haul.ServerBagOf(c.HostId).Carried.Count, k0 = c.Haul.ServerBagOf(c.ClientId).Carried.Count, hg0 = DiveGold(c, c.HostId), kg0 = DiveGold(c, c.ClientId);
            yield return ClearRoom(c, plan, plain);
            Assert.That(c.Haul.ServerBagOf(c.HostId).Carried.Count, Is.EqualTo(h0), "an ordinary room pays no extra roll on a Fat Larder floor (host)");
            Assert.That(c.Haul.ServerBagOf(c.ClientId).Carried.Count, Is.EqualTo(k0), "(client)");
            Assert.That(DiveGold(c, c.HostId), Is.EqualTo(hg0));
            Assert.That(DiveGold(c, c.ClientId), Is.EqualTo(kg0));

            yield return ClearRoom(c, plan, plan.FatRoom);
            AssertPaid(c, c.HostId, plan.FatRoom, h0, hg0, "host (fat room)");
            AssertPaid(c, c.ClientId, plan.FatRoom, k0, kg0, "client (fat room)");
        }

        [UnityTest]
        public IEnumerator Brimming_AFullBagGetsTheNotice_AndNoItem()
        {
            uint seed = FindSeed((s, l, m) => m.Has(FloorModifier.Brimming) && FirstClearRoom(CellarEncounterPlan.BuildForDive(l, CellarFloorDressing.Resolve(l), 0, false)) is int r && r >= 0 && RollsAnItemForEverySlot(s, 2, r));
            Ctx c = null; yield return Setup(x => c = x, seed);
            PlayerBag bag = c.Haul.ServerBagOf(c.ClientId);
            for (int n = 0; n < PlayerBag.MaxCarried; n++) Assert.That(c.Haul.ServerAddItem(c.ClientId, 101), Is.True);
            Assert.That(bag.IsFull, Is.True, "premise: the client's bag is full");
            int k0 = bag.Carried.Count, notices0 = c.Peer.NoticeCount;
            yield return ClearARoom(c, r => { });
            yield return PlayModeHarness.WaitUntil(() => c.Peer.NoticeCount == notices0 + 1, Timeout, "the client was never told its bag is full");
            Assert.That(c.Peer.LastNotice, Is.EqualTo(CellarContentsSync.BagFullText));
            Assert.That(bag.Carried.Count, Is.EqualTo(k0), "the full bag took no item");
        }

        // ---- Stirred ----

        [UnityTest]
        public IEnumerator Stirred_AddsBonus_AndExtraRoll_AndLeavesWithTheFloor()
        {
            uint seed = FindSeed((s, l, m) => m.A == FloorModifier.Stirred && LootChest(l) >= 0);
            Ctx c = null; yield return Setup(x => c = x, seed);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Stir.ModifierBonus == FloorModifierTuning.StirredBonus, Timeout, "the Stirred floor never added its flat stir (bonus " + c.Haul.Session.Stir.ModifierBonus + ")");
            Assert.That(c.Haul.Session.Stir.Value, Is.GreaterThanOrEqualTo(FloorModifierTuning.StirredBonus), "the bonus counts like any stir");

            int idx = LootChest(c.Layout);
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            c.Players[c.ClientId] = at + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.ServerBagOf(c.ClientId).ChestOpened(seed, 2, idx), Timeout, "the chest never opened");
            int slot = c.Haul.PlayerSlotOf(c.ClientId);
            int main = c.Contents.LastLootItem, extra = c.Contents.LastExtraLootItem;
            Assert.That(extra, Is.EqualTo(CellarLoot.RollChest(seed, 2, idx + 1000, slot, 0, false)).Or.EqualTo(CellarLoot.RollChest(seed, 2, idx + 1000, slot, 0, true)), "the extra roll is the derived chest index's roll");
            int items = (main == CellarLoot.GoldId ? 0 : 1) + (extra == CellarLoot.GoldId ? 0 : 1);
            Assert.That(c.Haul.ServerBagOf(c.ClientId).Carried.Count, Is.EqualTo(items), "both rolls reached the opener's bag (a purse goes to gold)");

            // it leaves with the floor: the next floor (a Stirred modifier never repeats within a dive) has no bonus
            Assert.That(c.Haul.ServerSetFloor(4), Is.True);
            Assert.That(c.Floor.ServerSetFloor(seed, 4), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Floor.BuiltFloor == 4 && c.Floor.Built != null, Timeout, "floor 4 never built");
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Stir.ModifierBonus == 0f, Timeout, "the bonus followed the party to the next floor");
        }

        // ---- Crowded / Ambush ----

        [UnityTest]
        public IEnumerator Crowded_GoldPerClear_PerPlayer()
        {
            uint seed = FindSeed((s, l, m) => m.A == FloorModifier.Crowded);
            Ctx c = null; yield return Setup(x => c = x, seed);
            int host0 = DiveGold(c, c.HostId), client0 = DiveGold(c, c.ClientId);
            int room = -1;
            yield return ClearARoom(c, r => room = r);
            Assert.That(DiveGold(c, c.HostId), Is.EqualTo(host0 + FloorModifierTuning.CrowdedGoldPerClear), "the host's reward for the cleared room");
            Assert.That(DiveGold(c, c.ClientId), Is.EqualTo(client0 + FloorModifierTuning.CrowdedGoldPerClear), "and the client's: every present player");
            Assert.That(c.Contents.GoldPaidForClears, Is.EqualTo(2 * FloorModifierTuning.CrowdedGoldPerClear), "paid once for the one room");
        }

        [UnityTest]
        public IEnumerator Ambush_GoldWhenTheAmbushRoomClears_PerPlayer()
        {
            uint seed = FindSeed((s, l, m) => m.A == FloorModifier.Ambush && CellarEncounterPlan.BuildForDive(l, CellarFloorDressing.Resolve(l), 0, false).AmbushRoom >= 0);
            Ctx c = null; yield return Setup(x => c = x, seed);
            CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false);
            Assert.That(plan.Twist, Is.EqualTo(FloorTwist.Ambush));
            EncounterSpawn first = null; foreach (EncounterSpawn sp in plan.Spawns) if (sp.RoomIndex == plan.AmbushRoom && sp.Kind != EncounterKind.Hunter) { first = sp; break; }
            Assert.That(first, Is.Not.Null);
            int host0 = DiveGold(c, c.HostId), client0 = DiveGold(c, c.ClientId);
            c.Players[c.HostId] = Cell(c, first.X, first.Y) + Vector3.forward * 0.1f;   // inside the ambush room: it springs after its delay
            c.Players[c.ClientId] = Cell(c, first.X, first.Y) + Vector3.back * 0.3f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(plan.AmbushRoom) > 0, Timeout, "the ambush never sprang");
            KillEveryEnemy(c.HostNm);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.AliveInRoom(plan.AmbushRoom) == 0 && (c.Contents.State.Cleared & (1UL << plan.AmbushRoom)) != 0, Timeout, "the ambush room never cleared");
            Assert.That(DiveGold(c, c.HostId), Is.EqualTo(host0 + FloorModifierTuning.AmbushGold));
            Assert.That(DiveGold(c, c.ClientId), Is.EqualTo(client0 + FloorModifierTuning.AmbushGold));
        }

        // ---- Hurried ----

        [UnityTest]
        public IEnumerator Hurried_CollapseShorter_AndGoldOnLeave()
        {
            uint seed = FindSeed((s, l, m) => m.A == FloorModifier.Hurried);
            Ctx c = null; yield return Setup(x => c = x, seed);
            float path = DiveFloorTable.PathSecondsToTarget(c.Layout);
            int expected = Mathf.CeilToInt(DiveStir.CollapseSeconds(path, FloorModifierTuning.HurriedCollapseMultiplier / DiveStir.CollapseMultiplier));
            int unhurried = Mathf.CeilToInt(DiveStir.CollapseSeconds(path));
            TestContext.WriteLine("path " + path + " s: hurried " + expected + " s, unhurried " + unhurried + " s");
            for (int i = 0; i < 20; i++) Assert.That(c.Haul.ServerAddItem(c.HostId, Valuable), Is.True);   // 20 x 5 x sqrt 2: past 100
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.DeepClosesLatched, Timeout, "the collapse never began (stir " + c.Haul.Current.StirValue + ")");
            Assert.That(c.Haul.Current.DeepSecondsLeft, Is.InRange(expected - 3, expected), "the Hurried floor's collapse is 1.25x the walk, minimum scaled too");
            Assert.That(c.Haul.Current.DeepSecondsLeft, Is.LessThan(unhurried - 2), "shorter than the ordinary collapse of this floor");

            int host0 = DiveGold(c, c.HostId), client0 = DiveGold(c, c.ClientId);
            Assert.That(c.Haul.ServerEnterFloor(3), Is.True, "the party takes the stair");
            Assert.That(DiveGold(c, c.HostId), Is.EqualTo(host0 + FloorModifierTuning.HurriedGold), "Hurried's reward for leaving by the stair, host");
            Assert.That(DiveGold(c, c.ClientId), Is.EqualTo(client0 + FloorModifierTuning.HurriedGold), "and the client");
        }

        // ---- Hushed ----

        [UnityTest]
        public IEnumerator Hushed_NoHunter([Values(true, false)] bool hushed)
        {
            uint seed = FindSeed((s, l, m) => hushed ? m.B == FloorModifier.Hushed : (m.B == FloorModifier.None && m.A == FloorModifier.None));
            Ctx c = null; yield return Setup(x => c = x, seed);
            for (int i = 0; i < 14; i++) Assert.That(c.Haul.ServerAddItem(c.HostId, Valuable), Is.True);   // 14 x 5 x sqrt 2 = 99: the hunter latches (70), the collapse (100) does not
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Stir.HunterLatched && c.Haul.EncounterPlan != null && c.Haul.EncounterPlan.Hunter != null, Timeout, "the hunter never latched into the plan (stir " + c.Haul.Current.StirValue + ")");
            if (hushed)
            {
                yield return Seconds(2f);
                Assert.That(c.Contents.HunterEnemy, Is.Null, "a Hushed floor never spawns the latched hunter");
            }
            else yield return PlayModeHarness.WaitUntil(() => c.Contents.HunterEnemy != null, Timeout, "the control floor never spawned the latched hunter");
        }

        // ---- Warm Hands ----

        [UnityTest]
        public IEnumerator WarmHands_FasterRecharge([Values(true, false)] bool warm)
        {
            uint seed = FindSeed((s, l, m) => (warm ? m.B == FloorModifier.WarmHands : m.B != FloorModifier.WarmHands && !m.Has(FloorModifier.Brimming)) && CellarHealing.PointFor(l, CellarFloorDressing.Resolve(l)).HasValue);
            Ctx c = null; yield return Setup(x => c = x, seed);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.HealRechargeSecondsNow > 0f, Timeout, "the floor's healing point never came up");
            Assert.That(c.Haul.HealRechargeSecondsNow, Is.EqualTo(warm ? FloorModifierTuning.WarmHandsRechargeSeconds : HealTuning.Default.RechargeSeconds), warm ? "Warm Hands: 15 s" : "no modifier: the ordinary 30 s");
        }

        // ---- Dim ----

        [UnityTest]
        public IEnumerator Dim_RankBonus()
        {
            // a Dim floor with a loot chest whose roll the bonus really changes, for every roster slot
            uint seed = 0; int idx = -1;
            for (uint s = 1; s < 12000 && seed == 0; s++)
            {
                if (!(CellarAtmosphere.IsDarkFloor(s, 2) && FloorModifierSet.For(s, 2).Has(FloorModifier.Dim))) continue;
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, 2);
                for (int i = 0; i < l.Chests.Count && seed == 0; i++)
                {
                    if (l.Chests[i].HoldsKey) continue;
                    bool differs = true;
                    for (int slot = 0; slot < 4 && differs; slot++) differs = CellarLoot.RollChest(s, 2, i, slot, 0, false) != CellarLoot.RollChest(s, 2, i, slot, FloorModifierTuning.DimRankBonus, false);
                    if (differs) { seed = s; idx = i; }
                }
            }
            if (seed == 0) { seed = FindSeed((s, l, m) => m.Has(FloorModifier.Dim) && LootChest(l) >= 0, 2, 12000); idx = -1; }
            Ctx c = null; yield return Setup(x => c = x, seed);
            if (idx < 0) idx = LootChest(c.Layout);
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            c.Players[c.ClientId] = at + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.ServerBagOf(c.ClientId).ChestOpened(seed, 2, idx), Timeout, "the chest never opened");
            int slotOf = c.Haul.PlayerSlotOf(c.ClientId);
            Assert.That(c.Contents.LastLootItem, Is.EqualTo(CellarLoot.RollChest(seed, 2, idx, slotOf, FloorModifierTuning.DimRankBonus, false)).Or.EqualTo(CellarLoot.RollChest(seed, 2, idx, slotOf, FloorModifierTuning.DimRankBonus, true)), "the roll carries the Dim rank bonus");
            Assert.That(c.Contents.LastExtraLootItem, Is.EqualTo(0), "Dim gives no extra roll (that is Stirred)");
        }
    }
}
