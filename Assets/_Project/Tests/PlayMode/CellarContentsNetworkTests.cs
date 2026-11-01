// Authority: test-only. M5 contents: the host (CellarContentsSync) spawns room enemies, opens chests, drops and hands out the gate key, opens the locked door and hides pieces;
// a client sees the replicated state. Host + one client in one process (scene management off; sync objects are dynamically spawned network prefabs from the owned list).
// Players are simulated through CellarContentsSync.PlayersOverride (no hero objects exist here), so the RPC range rule is judged on those positions.
// Catches: enemies not spawning on first room entry / not appearing on the client, a room not clearing, a chest opening out of range or twice, a client not seeing the mask, loot
// not reaching the haul, the carrier not dropping the gate key, the pickup not unlocking the exit on every peer, the door staying shut with the key held, pieces not reaching
// the haul or not reappearing after a failed dive.
// NOT covered: the real Interact input path (rising edge of the local hero's intent), real player-object positions, three or more clients.
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
    public class CellarContentsNetworkTests
    {
        private const float Timeout = 20f;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        private sealed class Ctx
        {
            public NetworkManager HostNm; public PlayModeHarness.TestClient Client;
            public CellarFloorSync Floor; public DiveHaulSync Haul; public CellarContentsSync Contents, Peer;
            public CellarFloorLayout Layout; public uint Seed;
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

        // A seed whose floor 1 has a locked room with a gate, a key chest and a carrier.
        private static uint FindSeed()
        {
            for (uint s = 1; s < 400; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Generate(s, 1);
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0) return s;
            }
            Assert.Fail("no seed with a locked room, a carrier and a key chest"); return 0;
        }

        private IEnumerator Setup(System.Action<Ctx> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            var c = new Ctx { HostNm = app.NetworkManager };
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client); c.Client = client;
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client never connected");

            c.Floor = Spawn<CellarFloorSync>(c.HostNm); c.Haul = Spawn<DiveHaulSync>(c.HostNm); c.Contents = Spawn<CellarContentsSync>(c.HostNm);
            ulong id = c.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client never received the contents object");
            c.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();
            // the peer's own sync siblings are found in the scene by FindFirstObjectByType (which could return the host's); point it at its own
            foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == client.NetworkManager) c.Peer.FloorSync = f;
            c.Contents.FloorSync = c.Floor; c.Contents.HaulSync = c.Haul;
            Assert.That(c.Peer.FloorSync, Is.Not.Null, "the client has its own floor sync");

            c.HostId = c.HostNm.LocalClientId; c.ClientId = client.NetworkManager.LocalClientId;
            c.Contents.PlayersOverride = list => { foreach (KeyValuePair<ulong, Vector3> kv in c.Players) list.Add(new CellarContentsSync.PlayerPos { ClientId = kv.Key, Position = kv.Value }); };

            c.Seed = FindSeed();
            Assert.That(c.Haul.ServerBegin(new[] { 2, 2, 2 }), Is.True);
            Assert.That(c.Haul.ServerSetFloor(1), Is.True);
            Assert.That(c.Floor.ServerSetFloor(c.Seed, 1), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.FloorSync.Built != null && c.Peer.FloorSync.BuiltFloor == 1 && c.Peer.PieceCount > 0 && c.Contents.PieceCount > 0, Timeout, "floors were not built");
            c.Layout = c.Floor.Layout;
            done(c);
        }

        private static Vector3 Cell(Ctx c, int x, int y) => c.Floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(x, y));

        private static void Kill(Enemy e)
        {
            var input = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            for (int i = 0; i < 6 && e.Alive; i++) e.ServerApplyHit(input, e.transform.position + Vector3.back, Time.timeAsDouble);
        }

        private static List<Enemy> EnemiesOn(NetworkManager nm)
        {
            var list = new List<Enemy>();
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) { Enemy e = o.GetComponent<Enemy>(); if (e != null) list.Add(e); }
            return list;
        }

        [Test]
        public void DefaultPieceCounts_MatchTheCoreTestConstant()
        {
            Assert.That(DiveHaulSync.DefaultPiecesPerFloor, Is.EqualTo(new[] { 2, 2, 2 }));
        }

        [UnityTest]
        public IEnumerator EnteringARoom_SpawnsItsEnemiesOnHostAndClient_AndKillingThemClearsIt()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier; int room = car.RoomIndex;
            int expected = CellarEncounterPlan.Build(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(EnemiesOn(c.HostNm).Count, Is.EqualTo(0), "nothing spawns before a player enters");

            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected, Timeout, "the room's enemies were not spawned");
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.Client.NetworkManager).Count == expected, Timeout, "the client does not see the room's enemies");
            Assert.That(c.Contents.GateKeyCarrierEnemy, Is.Not.Null, "the carrier spawn became an enemy");

            foreach (Enemy e in EnemiesOn(c.HostNm)) Kill(e);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.AliveInRoom(room) == 0, Timeout, "the room never emptied");
            yield return PlayModeHarness.WaitUntil(() => (c.Peer.State.Cleared & (1UL << room)) != 0, Timeout, "the client never saw the room cleared");
        }

        [UnityTest]
        public IEnumerator Chest_ClientRpcOpensIt_MaskSyncs_LootInHaul_OutOfRangeRefused()
        {
            Ctx c = null; yield return Setup(x => c = x);
            int idx = -1; for (int i = 0; i < c.Layout.Chests.Count; i++) if (!c.Layout.Chests[i].HoldsKey) { idx = i; break; }
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);

            c.Players[c.ClientId] = at + new Vector3(30f, 0f, 0f);
            int handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "the request never reached the host");
            Assert.That(c.Contents.State.Chests, Is.EqualTo(0UL), "out of range: refused");
            Assert.That(c.Haul.Current.UnbankedLoot, Is.EqualTo(0));

            c.Players[c.ClientId] = at + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => (c.Peer.State.Chests & (1UL << idx)) != 0, Timeout, "the client never saw the chest open");
            yield return PlayModeHarness.WaitUntil(() => c.Peer.ChestShownOpen(idx), Timeout, "the client does not show the chest opened");
            Assert.That(c.Haul.Current.UnbankedLoot, Is.EqualTo(1), "the loot reached the haul");
            Assert.That(CellarLoot.RankOf(c.Contents.LastLootItem), Is.InRange(1, 5));
            Assert.That(c.Contents.LastLootItem, Is.EqualTo(CellarLoot.Roll(c.Seed, 1, idx, false)));

            handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "second request not handled");
            Assert.That(c.Haul.Current.UnbankedLoot, Is.EqualTo(1), "an open chest does not pay twice");
        }

        [UnityTest]
        public IEnumerator CarrierDrop_PickupUnlocksExit_KeyChestOpensTheDoor_OnEveryPeer()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier;
            int unlockedFloor = -1; c.Contents.ExitUnlocked += f => unlockedFloor = f;

            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.GateKeyCarrierEnemy != null, Timeout, "no carrier enemy");
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + new Vector3(0f, 0f, -0.1f) + new Vector3(8f, 0f, 0f);   // out of pickup range; still keeps the room entered
            foreach (Enemy e in EnemiesOn(c.HostNm)) Kill(e);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.GateKey == 1 && c.Peer.GateKeyMarkerShown, Timeout, "the gate key never dropped / showed on the client");
            Assert.That(c.Peer.State.ExitUnlocked, Is.False);

            c.Players[c.HostId] = c.Peer.State.GateKeyPos + new Vector3(0.5f, 0f, 0f);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked, Timeout, "picking the key up did not unlock the exit");
            Assert.That(unlockedFloor, Is.EqualTo(1));
            Assert.That(c.Peer.State.GateKey, Is.EqualTo((byte)2));
            Assert.That(c.Haul.HostState.Haul.LootUnbanked(CellarLoot.GateKeyId), Is.EqualTo(1));
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.GateKeyMarkerShown, Timeout, "the key marker stayed");

            // the locked door
            Assert.That(c.Peer.TryGatePosition(out Vector3 gate), Is.True, "the floor built its gate");
            Assert.That(c.Peer.GateBlocking, Is.True);
            c.Players[c.HostId] = Cell(c, 0, 0) + new Vector3(60f, 0f, 0f);   // away from everything
            c.Players[c.ClientId] = gate + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenDoor();
            yield return new WaitForSeconds(0.5f);
            Assert.That(c.Peer.State.DoorOpen, Is.False, "no key yet: the door stays shut");

            FloorChest kc = c.Layout.KeyChest; Assert.That(c.Peer.TryChestPosition(c.Layout.KeyChestIndex, out Vector3 keyAt), Is.True);
            c.Players[c.ClientId] = keyAt + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(c.Layout.KeyChestIndex);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.RoomKeyHeld, Timeout, "the key chest gave no room key");
            Assert.That(c.Haul.HostState.Haul.LootUnbanked(CellarLoot.RoomKeyId), Is.EqualTo(1));
            Assert.That(c.Peer.State.DoorOpen, Is.False);

            c.Players[c.ClientId] = gate + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenDoor();
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.DoorOpen, Timeout, "the door never opened");
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.GateBlocking && !c.Contents.GateBlocking, Timeout, "the gate stayed solid");
        }

        [UnityTest]
        public IEnumerator Pieces_StepOverAddsToHaul_HiddenOnClient_ReappearAfterAFailedDive()
        {
            Ctx c = null; yield return Setup(x => c = x);
            Assert.That(c.Peer.TryPiecePosition(0, out Vector3 at), Is.True);
            Assert.That(c.Haul.Current.LeftFloor1, Is.EqualTo(2));
            c.Players[c.HostId] = at;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.LeftFloor1 == 1, Timeout, "the piece was not collected");
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.PieceVisible(0) && c.Peer.PieceVisible(1), Timeout, "the client did not hide the collected piece");
            Assert.That(c.Haul.HostState.Haul.IsCollected(DiveHaul.PieceId(1, 0)), Is.True);

            c.Players[c.HostId] = Cell(c, 0, 0) + new Vector3(60f, 0f, 0f);
            Assert.That(c.Haul.ServerFailDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.LeftFloor1 == 2, Timeout, "the failed dive did not return the piece");
            yield return PlayModeHarness.WaitUntil(() => c.Peer.PieceVisible(0), Timeout, "the piece did not reappear on the client");
        }
    }
}
