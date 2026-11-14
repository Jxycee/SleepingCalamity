// Authority: test-only. M5 contents: the host (CellarContentsSync) spawns room enemies, opens chests, drops and hands out the gate key, opens the locked door and hides pieces;
// a client sees the replicated state. Host + one client in one process (scene management off; sync objects are dynamically spawned network prefabs from the owned list).
// Players are simulated through CellarContentsSync.PlayersOverride (no hero objects exist here), so the RPC range rule is judged on those positions.
// Catches: enemies not spawning on first room entry / not appearing on the client, a room not clearing, a chest opening out of range or twice, a loot chest not opening for the opener's
// own bag (per player since plan 2 task 6; the lowered visual needs a hero and is judged in PerPlayerLootNetworkTests), loot not reaching the opener's bag, the carrier not dropping the gate key, the pickup not unlocking the exit on every peer, the door staying shut with the key held, pieces not reaching
// the haul or not reappearing after a failed dive.
// Also: a party wipe (CombatHost.ServerClearEnemies) drops no key and clears no room, and the room respawns on re-entry; a floor built under a root at (200,0,0) has chests, pieces
// and room spawns where the host checks them; a player still at its pre-floor-change position is ignored until it has moved.
// Plan 3 task 9: a gold purse roll goes to the opener's dive gold (never PlayerBag.Add), momentum's rank bonus reaches the roll, and a FULL bag refuses the chest (it stays closed, only the opener is told).
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

        private static T Spawn<T>(NetworkManager host, Vector3 at = default) where T : Component
        {
            NetworkObject o = host.SpawnManager.InstantiateAndSpawn(PrefabWith<T>(host).GetComponent<NetworkObject>(), host.LocalClientId, false, false, false, at, Quaternion.identity);
            return o.GetComponent<T>();
        }

        // The generated first-half floor (2 or 4) that holds the most pieces in the fallback world deal (floor 1 is authored with no locked room, carrier or key chest; 3 and 5 are set pieces).
        private static int PiecefulFloor()
        {
            DivePieces p = DivePieces.ForWorld(DiveHaulSync.DefaultWorldSeed);
            return p.PiecesLeft(2) >= p.PiecesLeft(4) ? 2 : 4;
        }

        // A seed whose floor has a locked room with a gate, a key chest and a carrier, a gate-key objective (plan 5: a valve or pump floor keeps its carrier as an ordinary enemy) and no hazard
        // modifier (Crowded / Stirred / Dim / Ambush / Hurried change gold, rolls and rooms: FloorModifierNetworkTests covers them).
        private static uint FindSeed(int floor)
        {
            for (uint s = 1; s < 1500; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0 && FloorObjectivePlan.For(l).Objective == FloorObjective.GateKey && FloorModifierSet.For(s, floor).A == FloorModifier.None) return s;
            }
            Assert.Fail("no seed with a locked room, a carrier and a key chest"); return 0;
        }

        // Chest loot goes into the opener's bag (plan 2 task 6: loot chests are per player): the client's carried item count.
        private static int Loot(Ctx c) => c.Haul.ServerBagOf(c.ClientId).Carried.Count;

        // Loot chests no longer set the shared State.Chests bit: "opened" is the opener's own bag mask on the host.
        private static bool Opened(Ctx c, int idx) => c.Haul.ServerBagOf(c.ClientId).ChestOpened(c.Seed, c.Layout.Floor, idx);

        private IEnumerator Setup(System.Action<Ctx> done, Vector3 floorOrigin = default, uint? seedOverride = null, int startFloor = 0)
        {
            if (startFloor == 0) startFloor = PiecefulFloor();
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

            c.Floor = Spawn<CellarFloorSync>(c.HostNm, floorOrigin); c.Haul = Spawn<DiveHaulSync>(c.HostNm); c.Contents = Spawn<CellarContentsSync>(c.HostNm);
            ulong id = c.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client never received the contents object");
            c.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();
            // the peer's own sync siblings are found in the scene by FindFirstObjectByType (which could return the host's); point it at its own
            foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == client.NetworkManager) c.Peer.FloorSync = f;
            c.Contents.FloorSync = c.Floor; c.Contents.HaulSync = c.Haul;
            Assert.That(c.Peer.FloorSync, Is.Not.Null, "the client has its own floor sync");

            c.HostId = c.HostNm.LocalClientId; c.ClientId = client.NetworkManager.LocalClientId;
            c.Contents.PlayersOverride = list => { foreach (KeyValuePair<ulong, Vector3> kv in c.Players) list.Add(new CellarContentsSync.PlayerPos { ClientId = kv.Key, Position = kv.Value }); };

            c.Seed = seedOverride ?? FindSeed(startFloor);
            Assert.That(c.Haul.ServerBegin(), Is.True);
            Assert.That(c.Haul.ServerSetFloor(startFloor), Is.True);
            Assert.That(c.Floor.ServerSetFloor(c.Seed, startFloor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.FloorSync.Built != null && c.Peer.FloorSync.BuiltFloor == startFloor, Timeout, "floors were not built");
            c.Layout = c.Floor.Layout;
            // the contents state of this floor (with its piece count) has reached the peer and both peers built the piece spheres
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.Floor == startFloor && c.Contents.State.Floor == startFloor && c.Peer.PieceCount == c.Contents.PieceCount && c.Peer.PieceCount == c.Peer.State.PieceCount, Timeout, "the contents state / piece spheres never reached the client");
            done(c);
        }

        // Waits for the carrier room's enemies to be spawned and says what it saw when they are not (the count, the plan's twist, the standing players).
        private static IEnumerator RoomSpawned(Ctx c, int room, int expected, bool needCarrier)
        {
            float until = Time.realtimeSinceStartup + Timeout;
            while (Time.realtimeSinceStartup < until && !(c.Contents.EnterEnemyCount(room) == expected && (!needCarrier || c.Contents.GateKeyCarrierEnemy != null))) { PlayModeHarness.Pace(); yield return null; }
            CellarEncounterPlan plan = c.Haul.EncounterPlan;
            Assert.That(c.Contents.EnterEnemyCount(room), Is.EqualTo(expected), "the room's enemies were not spawned: floor " + c.Layout.Floor + " seed " + c.Seed + " room " + room + ", plan twist " + (plan == null ? "none" : plan.Twist + " ambush room " + plan.AmbushRoom + " count " + plan.CountIn(room))
                + ", alive " + c.Contents.AliveInRoom(room) + ", enemies on host " + EnemiesOn(c.HostNm).Count + ", players " + c.Players.Count + ", carrier " + (c.Contents.GateKeyCarrierEnemy != null));
            Assert.That(!needCarrier || c.Contents.GateKeyCarrierEnemy != null, Is.True, "no carrier enemy");
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

        // A haul sync spawned alone deals its pieces from DefaultWorldSeed with the Core table: none on the authored floor 1, the baker on floor 3, the rest on the generated floors.
        [Test]
        public void DefaultPieces_AreDealtFromTheFallbackWorldSeed_NoneOnFloor1()
        {
            var pieces = DivePieces.ForWorld(DiveHaulSync.DefaultWorldSeed);
            Assert.That(pieces.PiecesLeft(1) + pieces.PeopleLeft(1), Is.EqualTo(0), "random pieces never land on the authored floor");
            Assert.That(pieces.PeopleLeft(3), Is.EqualTo(1), "the baker sleeps on floor 3");
            Assert.That(pieces.PiecesLeft(2) + pieces.PiecesLeft(4) + pieces.PiecesLeft(6) + pieces.PiecesLeft(7), Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator EnteringARoom_SpawnsItsEnemiesOnHostAndClient_AndKillingThemClearsIt()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier; int room = car.RoomIndex;
            int expected = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(EnemiesOn(c.HostNm).Count, Is.EqualTo(0), "nothing spawns before a player enters");

            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return RoomSpawned(c, room, expected, false);
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.Client.NetworkManager).Count == expected, Timeout, "the client does not see the room's enemies");
            Assert.That(c.Contents.GateKeyCarrierEnemy, Is.Not.Null, "the carrier spawn became an enemy");

            foreach (Enemy e in EnemiesOn(c.HostNm)) Kill(e);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.AliveInRoom(room) == 0, Timeout, "the room never emptied");
            yield return PlayModeHarness.WaitUntil(() => (c.Peer.State.Cleared & (1UL << room)) != 0, Timeout, "the client never saw the room cleared");
        }

        [UnityTest]
        public IEnumerator Chest_ClientRpcOpensIt_LootInHaul_OutOfRangeRefused()
        {
            Ctx c = null; yield return Setup(x => c = x);
            int idx = -1; for (int i = 0; i < c.Layout.Chests.Count; i++) if (!c.Layout.Chests[i].HoldsKey) { idx = i; break; }
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);

            c.Players[c.ClientId] = at + new Vector3(30f, 0f, 0f);
            int handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "the request never reached the host");
            Assert.That(Opened(c, idx), Is.False, "out of range: refused");
            Assert.That(Loot(c), Is.EqualTo(0));

            c.Players[c.ClientId] = at + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => Opened(c, idx), Timeout, "the chest never opened for the client's bag");
            Assert.That(Loot(c), Is.EqualTo(1), "the loot reached the opener's bag");
            Assert.That(c.Contents.State.Chests, Is.EqualTo(0UL), "a loot chest never sets the shared opened bit (the visual lives in the opener's own PlayerBagSync view: PerPlayerLootNetworkTests)");
            Assert.That(CellarLoot.RankOf(c.Contents.LastLootItem), Is.InRange(0, 5), "an item (0 only for boots, which are gear)");
            Assert.That(c.Contents.LastLootItem, Is.EqualTo(CellarLoot.RollChest(c.Seed, c.Layout.Floor, idx, c.Haul.PlayerSlotOf(c.ClientId))), "the opener's own roll");
            Assert.That(c.Haul.ServerBagOf(c.ClientId).Carried, Is.EqualTo(new[] { c.Contents.LastLootItem }));

            handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "second request not handled");
            Assert.That(Loot(c), Is.EqualTo(1), "an open chest does not pay twice");
        }

        // BUGS 18a: the carrier dies where no hero can stand (a wall / off the walkable cells): the host settles the key on the nearest walkable cell, so it can still be picked up.
        [UnityTest]
        public IEnumerator CarrierDiesOffTheWalkableCells_KeySettlesOnAWalkableCell_AndCanBePickedUp()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier;
            bool[,] ok = CellarKeyDrop.Walkable(c.Layout, c.Floor.Built.Dressing);
            int n = CellarFloorLayout.GridSize; CellPos bad = default; bool found = false; int best = int.MaxValue;
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
            {
                if (ok[x, y]) continue;
                int d = (x - car.X) * (x - car.X) + (y - car.Y) * (y - car.Y);
                if (d < best) { best = d; bad = new CellPos(x, y); found = true; }
            }
            Assert.That(found, Is.True, "the grid has a cell nobody can stand on");

            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.GateKeyCarrierEnemy != null, Timeout, "no carrier enemy");
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + new Vector3(8f, 0f, 0f);   // keeps the room entered, out of pickup range
            Enemy carrier = c.Contents.GateKeyCarrierEnemy;
            carrier.transform.position = Cell(c, bad.X, bad.Y);
            Kill(carrier);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.GateKey == 1, Timeout, "the gate key never dropped");

            Vector3 l = c.Floor.Built.Root.InverseTransformPoint(c.Peer.State.GateKeyPos);
            int kx = Mathf.FloorToInt(l.x / CellarFloorBuilder.CellSize), ky = Mathf.FloorToInt(-l.z / CellarFloorBuilder.CellSize);
            Assert.That(ok[kx, ky], Is.True, "the key lies on a walkable cell (" + kx + "," + ky + "), not at the dead carrier's cell (" + bad.X + "," + bad.Y + ")");
            c.Players[c.HostId] = c.Peer.State.GateKeyPos + new Vector3(0.5f, 0f, 0f);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked, Timeout, "the settled key could not be picked up");
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
            Assert.That(unlockedFloor, Is.EqualTo(c.Layout.Floor));
            Assert.That(c.Peer.State.GateKey, Is.EqualTo((byte)2));
            Assert.That(System.Linq.Enumerable.Contains(c.Haul.ServerBagOf(c.HostId).Carried, CellarLoot.GateKeyId), Is.False, "keys are not loot");
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
            Assert.That(System.Linq.Enumerable.Contains(c.Haul.ServerBagOf(c.ClientId).Carried, CellarLoot.RoomKeyId), Is.False, "keys are not loot");
            Assert.That(Loot(c), Is.EqualTo(0), "the key chest adds nothing to a bag");
            Assert.That(c.Peer.State.DoorOpen, Is.False);

            c.Players[c.ClientId] = gate + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenDoor();
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.DoorOpen, Timeout, "the door never opened");
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.GateBlocking && !c.Contents.GateBlocking, Timeout, "the gate stayed solid");
        }

        [UnityTest]
        public IEnumerator PartyWipe_ClearEnemies_DropsNoKey_DoesNotClearTheRoom_AndTheRoomRespawnsOnReentry()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier; int room = car.RoomIndex;
            int expected = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return RoomSpawned(c, room, expected, true);

            CombatHost.Current.ServerClearEnemies();   // what a party wipe does
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.HostNm).Count == 0 || c.Contents.GateKeyCarrierEnemy == null, Timeout, "the wipe did not despawn the enemies");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0), "a wipe is not a kill: no gate key");
            Assert.That(c.Contents.State.Cleared & (1UL << room), Is.EqualTo(0UL), "a wiped room is not cleared");

            // the player is still standing in the room: it spawns afresh
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected && c.Contents.AliveInRoom(room) == expected, Timeout, "the room did not respawn after the wipe");
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.HostNm).Count >= expected, Timeout, "no enemies after the respawn");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0));
            Assert.That(Loot(c), Is.EqualTo(0));
        }

        // The carrier leaves without dying (despawned) while room-mates live: no key, but once the room-mates are dead the room resets and the carrier is back on the next entry.
        [UnityTest]
        public IEnumerator CarrierDespawnedAlive_RoomMatesLive_NoKey_ThenTheRoomResetsAndTheCarrierComesBack()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier; int room = car.RoomIndex;
            int expected = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            Assume.That(expected, Is.GreaterThan(1), "premise: the carrier has room-mates");
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return RoomSpawned(c, room, expected, true);

            Enemy carrier = c.Contents.GateKeyCarrierEnemy;
            carrier.NetworkObject.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.GateKeyCarrierEnemy == null, Timeout, "the carrier was not forgotten");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0), "a despawn is not a kill: no key");
            foreach (Enemy e in EnemiesOn(c.HostNm)) Kill(e);
            c.Players[c.HostId] = Cell(c, 0, 0) + new Vector3(60f, 0f, 0f);
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.HostNm).Count == 0, Timeout, "the room-mates did not die");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0), "the carrier never died: still no key");
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.GateKeyCarrierEnemy != null, Timeout, "the carrier never came back, so the key is lost");
        }

        [UnityTest]
        public IEnumerator FloorUnderARootAt200_ChestsPiecesAndRoomSpawnsAreWhereTheHostChecks()
        {
            var origin = new Vector3(200f, 0f, 0f);
            Ctx c = null; yield return Setup(x => c = x, origin);
            Transform root = c.Floor.Built.Root;
            Assert.That(root.position.x, Is.EqualTo(200f).Within(0.01f), "the floor root sits at the sync object");
            Bounds b = c.Floor.Built.Bounds;
            Assert.That(b.center.x, Is.GreaterThan(190f), "Built.Bounds is in world space");

            // chest: visual at TransformPoint(local); opening works from 1 m away and not from 30 m
            int idx = -1; for (int i = 0; i < c.Layout.Chests.Count; i++) if (!c.Layout.Chests[i].HoldsKey) { idx = i; break; }
            FloorChest fc = c.Layout.Chests[idx];
            Vector3 expectChest = root.TransformPoint(CellarFloorBuilder.CellCenter(fc.X, fc.Y));
            Assert.That(c.Contents.TryChestPosition(idx, out Vector3 at), Is.True);
            Assert.That(Mathf.Abs(at.x - expectChest.x), Is.LessThan(0.05f)); Assert.That(Mathf.Abs(at.z - expectChest.z), Is.LessThan(0.05f));
            c.Players[c.ClientId] = at + new Vector3(30f, 0f, 0f);
            int handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "request not handled");
            Assert.That(Opened(c, idx), Is.False);
            c.Players[c.ClientId] = at + new Vector3(1f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => Opened(c, idx), Timeout, "the chest did not open at x=200");

            // piece: the sphere sits inside the floor's bounds and stepping on it collects it
            int pieces = c.Haul.Session.Pieces.PiecesLeft(c.Layout.Floor);
            Assert.That(pieces, Is.GreaterThanOrEqualTo(2), "premise: the pieceful floor holds at least two pieces");
            Assert.That(c.Peer.TryPiecePosition(0, out Vector3 piece), Is.True);
            Assert.That(piece.x, Is.InRange(b.min.x - 1f, b.max.x + 1f)); Assert.That(piece.z, Is.InRange(b.min.z - 1f, b.max.z + 1f));
            c.Players[c.HostId] = piece;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.PiecesLeft == pieces - 1, Timeout, "the piece at x=200 was not collected");

            // room spawns land on their cell
            GateKeyCarrier car = c.Layout.Carrier;
            Vector3 carCell = Cell(c, car.X, car.Y);
            c.Players[c.HostId] = carCell + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.GateKeyCarrierEnemy != null, Timeout, "no carrier enemy at x=200");
            Vector3 ep = c.Contents.GateKeyCarrierEnemy.transform.position;
            Assert.That(Mathf.Sqrt((ep.x - carCell.x) * (ep.x - carCell.x) + (ep.z - carCell.z) * (ep.z - carCell.z)), Is.LessThan(3f), "the carrier spawned at its cell under the offset root");
        }

        [UnityTest]
        public IEnumerator StalePlayerPosition_IsIgnoredUntilThePlayerHasMoved()
        {
            Ctx c = null; yield return Setup(x => c = x);
            c.Contents.AutoArm = false;
            int pieces = c.Haul.Session.Pieces.PiecesLeft(c.Layout.Floor);
            Assert.That(c.Peer.TryPiecePosition(0, out Vector3 piece), Is.True);
            GateKeyCarrier car = c.Layout.Carrier;
            // a point inside the floor, more than 3 m from the piece
            Vector3[] cands = { Cell(c, car.X, car.Y), c.Floor.Built.EntryCenter, c.Peer.TryChestPosition(0, out Vector3 ch) ? ch : piece };
            Vector3 elsewhere = default; bool found = false;
            foreach (Vector3 v in cands) if (new Vector2(v.x - piece.x, v.z - piece.z).magnitude > 5f) { elsewhere = v; found = true; break; }
            Assert.That(found, Is.True, "a second spot on the floor");

            // the host "sees" the player standing on the piece when the floor is armed (the teleport has not landed yet)
            c.Players[c.HostId] = piece;
            Assert.That(c.Contents.ServerArmFloor(c.Layout, c.Floor.Built.Root), Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(c.Haul.Current.PiecesLeft, Is.EqualTo(pieces), "a stale position collects nothing");
            Assert.That(c.Contents.EnterEnemyCount(car.RoomIndex), Is.EqualTo(0));

            // the teleport lands: the player moves, then steps on the piece
            c.Players[c.HostId] = elsewhere;
            yield return new WaitForSeconds(0.3f);
            c.Players[c.HostId] = piece;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.PiecesLeft == pieces - 1, Timeout, "after moving, the piece is collected");

            // a player outside the floor never counts, even when far from the recorded position
            c.Players.Clear();
            Assert.That(c.Contents.ServerArmFloor(c.Layout, c.Floor.Built.Root), Is.True);
            c.Players[c.ClientId] = c.Floor.Built.Bounds.center + new Vector3(500f, 0f, 0f);
            yield return new WaitForSeconds(0.4f);
            Assert.That(c.Contents.EnterEnemyCount(car.RoomIndex), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ArmFloor_WithNoOrBadCarrier_UnlocksTheExitAtOnce_AndClearFloorForgets()
        {
            Ctx c = null; yield return Setup(x => c = x);
            int fl = c.Layout.Floor, other = fl == 2 ? 4 : 2;
            Assert.That(c.Contents.IsExitUnlocked(fl), Is.False, "a normal floor is locked until the gate key is picked");
            Assert.That(c.Contents.IsExitUnlocked(UndercroftPlan.Phase1LastFloor), Is.True, "the ending floor has no stair gate");
            Assert.That(c.Contents.IsExitUnlocked(other), Is.False, "the other generated floor is not the armed floor");

            c.Contents.AutoArm = false;
            CellarFloorLayout l = c.Layout;
            var noCarrier = new CellarFloorLayout(l.Seed, fl, false, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, null);
            var badCarrier = new CellarFloorLayout(l.Seed, fl, false, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, new GateKeyCarrier(99, 0, 0, 0));
            int fired = -1;
            c.Contents.ExitUnlocked += f => fired = f;

            Assert.That(c.Contents.ServerArmFloor(noCarrier, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(fl), "the event fires inside ServerArmFloor");
            Assert.That(c.Contents.IsExitUnlocked(fl), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked, Timeout, "the client never saw the unlocked exit");

            fired = -1;
            c.Contents.ServerClearFloor();
            Assert.That(c.Contents.IsExitUnlocked(fl), Is.False);
            Assert.That(c.Contents.ServerArmFloor(badCarrier, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(fl), "an invalid carrier also unlocks");

            fired = -1;
            c.Contents.ServerClearFloor();
            Assert.That(c.Contents.ServerArmFloor(l, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(-1), "a valid carrier keeps the exit locked");
            Assert.That(c.Contents.IsExitUnlocked(fl), Is.False);
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.State.ExitUnlocked, Timeout, "the client kept the old unlocked flag");
        }

        [UnityTest]
        public IEnumerator Pieces_StepOverAddsToHaul_HiddenOnClient_StayGoneAfterAFailedDive()
        {
            Ctx c = null; yield return Setup(x => c = x);
            var ids = new List<int>(c.Haul.Session.Pieces.OnFloor(c.Layout.Floor));
            int n = ids.Count;
            Assert.That(n, Is.GreaterThanOrEqualTo(2), "premise: the pieceful floor holds at least two pieces");
            Assert.That(c.Peer.TryPiecePosition(0, out Vector3 at), Is.True);
            Assert.That(c.Haul.Current.PiecesLeft, Is.EqualTo(n));
            c.Players[c.HostId] = at;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.PiecesLeft == n - 1, Timeout, "the piece was not collected");
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.PieceVisible(0) && c.Peer.PieceVisible(1), Timeout, "the client did not hide the collected piece");
            Assert.That(c.Haul.Session.Pieces.IsFound(ids[0]), Is.True, "spot 0 is the floor's lowest piece id");

            c.Players[c.HostId] = Cell(c, 0, 0) + new Vector3(60f, 0f, 0f);
            Assert.That(c.Haul.ServerFailDive(), Is.True);
            yield return new WaitForSeconds(0.6f);
            Assert.That(c.Haul.Current.PiecesLeft, Is.EqualTo(0), "after a failed dive the summary is the home one (no floor)");
            Assert.That(c.Haul.Session.Pieces.IsFound(ids[0]), Is.True, "the failed dive keeps the piece: pieces are safe on pickup");
            Assert.That(c.Peer.PieceVisible(0), Is.False, "and the piece does not reappear on the client");
        }

        private static bool NearRing(Ctx c, CellPos[] ring, Enemy e)
        {
            foreach (CellPos cell in ring)
            {
                Vector3 w = Cell(c, cell.X, cell.Y);
                if (Vector2.Distance(new Vector2(e.transform.position.x, e.transform.position.z), new Vector2(w.x, w.z)) < 2.5f) return true;
            }
            return false;
        }

        // procgen 3b: on an Ambush floor the ambush room's enemies spring after FloorTwistTuning.AmbushDelaySeconds at CellarAmbush cells, and the client sees them.
        [UnityTest]
        public IEnumerator AmbushFloor_EnteringTheAmbushRoom_SpawnsAtRingCellsAfterTheDelay_ClientSeesThem()
        {
            uint seed = 0; int floor = 0;
            for (uint s = 1; s < 3000 && floor == 0; s++)
                for (int f = 2; f <= 3 && floor == 0; f++)
                    if (CellarFloorRoster.TwistOf(s, f) == FloorTwist.Ambush) { seed = s; floor = f; }
            Assert.That(floor, Is.Not.EqualTo(0), "no Ambush floor in 3000 seeds");
            Ctx c = null; yield return Setup(x => c = x, default, seed, floor);
            CellarFloorDressing dressing = c.Floor.Built.Dressing;
            DiveHaulSync.Summary sum = c.Haul.Current;
            CellarEncounterPlan plan = c.Haul.EncounterPlan ?? CellarEncounterPlan.BuildForDive(c.Layout, dressing, sum.ExtraEnemiesPerRoom, sum.HunterLatched);
            Assert.That(plan.Twist, Is.EqualTo(FloorTwist.Ambush));
            int room = plan.AmbushRoom; int expected = plan.CountIn(room);
            Assert.That(expected, Is.GreaterThan(0));
            PlacedRoom pr = c.Layout.Rooms[room];

            // the player stands on a movable spawn of the room: the gate-key carrier and the warden keep their authored cells (CellarAmbush), so standing on one would put an enemy at the player by design
            int sx = pr.X, sy = pr.Y;
            foreach (EncounterSpawn sp in plan.Spawns)
            {
                bool fixedCell = (c.Layout.Carrier != null && c.Layout.Carrier.X == sp.X && c.Layout.Carrier.Y == sp.Y) || (c.Layout.WardenCell.HasValue && c.Layout.WardenCell.Value.X == sp.X && c.Layout.WardenCell.Value.Y == sp.Y);
                if (sp.RoomIndex == room && sp.Kind != EncounterKind.Hunter && !fixedCell) { sx = sp.X; sy = sp.Y; break; }
            }
            c.Players[c.HostId] = Cell(c, sx, sy) + Vector3.forward * 0.1f;
            Vector3 pl = c.Floor.Built.Root.InverseTransformPoint(c.Players[c.HostId]);
            CellPos[] ring = CellarAmbush.Cells(c.Layout, dressing, plan, room, new[] { new PlanePoint(pl.x, -pl.z) });   // the host keeps the ring 4 m clear of the entering player
            float t0 = Time.time;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Time.time - t0, Is.LessThan(FloorTwistTuning.AmbushDelaySeconds * 0.5f), "the frames before the delay check must stay well inside the delay");
            Assert.That(c.Contents.EnterEnemyCount(room), Is.EqualTo(0), "nothing springs before the delay");
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected, Timeout, "the ambush enemies did not spring");
            float springAt = Time.time;   // the host spawned them this frame or the one before: positions are still the spawn cells
            Assert.That(springAt - t0, Is.GreaterThanOrEqualTo(FloorTwistTuning.AmbushDelaySeconds - 0.1f), "the ambush sprang before the delay ran out");
            Assert.That(EnemiesOn(c.HostNm).Count, Is.EqualTo(expected));
            foreach (Enemy e in EnemiesOn(c.HostNm)) Assert.That(NearRing(c, ring, e), Is.True, "a host ambush enemy stands near one of the ring cells right after the spawn");
            Vector3 playerAt = c.Players[c.HostId];
            foreach (Enemy e in EnemiesOn(c.HostNm))
            {
                Vector3 d = e.transform.position - playerAt; d.y = 0f;
                Assert.That(d.magnitude, Is.GreaterThanOrEqualTo(CellarSpawnSafety.AmbushMinPlayerMeters - 0.5f), "a host ambush enemy spawns at least the safety distance from the player");
            }
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.Client.NetworkManager).Count == expected, Timeout, "the client does not see the ambush enemies");
            foreach (Enemy e in EnemiesOn(c.Client.NetworkManager)) Assert.That(NearRing(c, ring, e), Is.True, "a client ambush enemy stands near one of the ring cells");
        }

        // ---- playtest 12 / 13 ----

        private static GameObject TestWall(Ctx c, Vector3 center, Vector3 size)
        {
            Transform walls = c.Floor.Built.Root.Find(CellarFloorBuilder.WallGroup);
            Assert.That(walls, Is.Not.Null, "the floor has a Walls group");
            var go = new GameObject("TestWall"); go.transform.SetParent(walls, false); go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms();
            return go;
        }

        private static void RemoveWall(GameObject wall) { Object.DestroyImmediate(wall); Physics.SyncTransforms(); }

        // Catches: a chest opening through a wall (flat 2.5 m range only). Host-only physics; the RPC path is the same ServerTryOpenChest.
        [UnityTest]
        public IEnumerator Chest_WallBetweenPlayerAndChest_Refused_OpensOnceTheWallIsGone()
        {
            Ctx c = null; yield return Setup(x => c = x);
            int idx = -1; for (int i = 0; i < c.Layout.Chests.Count; i++) if (!c.Layout.Chests[i].HoldsKey) { idx = i; break; }
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            GameObject wall = TestWall(c, at + new Vector3(0.5f, 0f, 0f), new Vector3(0.2f, 3f, 6f));
            c.Players[c.ClientId] = at + new Vector3(1.0f, 0f, 0f);
            int handled = c.Contents.ChestRequestsHandled;
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.ChestRequestsHandled > handled, Timeout, "the request never reached the host");
            Assert.That(Opened(c, idx), Is.False, "a wall between the player and the chest: refused");
            RemoveWall(wall);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => Opened(c, idx), Timeout, "the chest never opened once the wall was gone");
        }

        // Catches: the locked door opening (by press or by the held-key auto open) for a player on the far side of a wall.
        [UnityTest]
        public IEnumerator Door_HeldKey_WallBetweenPlayerAndGate_StaysShut_OpensOnceClear()
        {
            Ctx c = null; yield return Setup(x => c = x);
            Assert.That(c.Peer.TryGatePosition(out Vector3 gate), Is.True, "the floor built its gate");
            Assert.That(c.Peer.TryChestPosition(c.Layout.KeyChestIndex, out Vector3 keyAt), Is.True);
            c.Players[c.ClientId] = keyAt + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(c.Layout.KeyChestIndex);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.RoomKeyHeld, Timeout, "the key chest gave no room key");
            Assert.That(c.Peer.State.DoorOpen, Is.False);

            GameObject wall = TestWall(c, gate + new Vector3(0.5f, 0f, 0f), new Vector3(0.2f, 3f, 6f));
            c.Players[c.HostId] = gate + new Vector3(1.0f, 0f, 0f);   // in auto-open range, but behind the wall
            c.Players[c.ClientId] = c.Players[c.HostId];
            c.Peer.RequestOpenDoor();
            yield return new WaitForSeconds(0.6f);
            Assert.That(c.Contents.State.DoorOpen, Is.False, "a wall between the player and the gate: neither the press nor the auto open works");
            RemoveWall(wall);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.DoorOpen, Timeout, "the door never auto-opened once the wall was gone");
        }

        // Soak finding 2026-11-02 (then floor 3, seed 12345; floor 3 is the bakery stand-in since plan 3 task 4, so the first seed from 12345 whose generated floor 4 has a locked room stands in): "DoorOpen but the gate still blocks". Once the door is open on the host AND on the client's copy, NO active
        // LockedGate collider is left anywhere in the scene (host and client floors share one process and one physics world here), and a CharacterController walks through the opening.
        [UnityTest]
        public IEnumerator Door_Floor4_OpenLeavesNoGateColliderOnAnyPeer_AndAHeroWalksThrough()
        {
            uint doorSeed = 12345u;
            while (!CellarFloorGenerator.Layout(doorSeed, 4).HasLockedRoom) doorSeed++;
            Ctx c = null; yield return Setup(x => c = x, default, doorSeed, 4);
            Assert.That(c.Layout.HasLockedRoom, Is.True, "the chosen seed's floor 4 has a locked room");
            Assert.That(c.Peer.TryGatePosition(out Vector3 gate), Is.True);
            Assert.That(c.Peer.TryChestPosition(c.Layout.KeyChestIndex, out Vector3 keyAt), Is.True);
            c.Players[c.HostId] = c.Players[c.ClientId] = keyAt + new Vector3(1.0f, 0f, 0f);
            c.Peer.RequestOpenChest(c.Layout.KeyChestIndex);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.RoomKeyHeld, Timeout, "the key chest gave no room key");
            Collider gateCol = CellarFloorBuilder.FindGate(c.Floor.Built.Root).GetComponent<Collider>();
            Assert.That(gateCol, Is.Not.Null, "the gate has a collider");
            Vector3 ext = gateCol.bounds.size; bool throughX = ext.x < ext.z;   // the thin axis is the walking direction
            Vector3 through = throughX ? Vector3.right : Vector3.forward;
            c.Players[c.HostId] = c.Players[c.ClientId] = gate - through * 2.0f;
            c.Peer.RequestOpenDoor();
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.DoorOpen && c.Contents.State.DoorOpen, Timeout, "the door never opened");
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.GateBlocking && !c.Contents.GateBlocking, Timeout, "a gate stayed solid");
            Physics.SyncTransforms();
            foreach (Collider col in Physics.OverlapBox(gate + Vector3.up * 1.4f, new Vector3(2f, 1.4f, 2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                Assert.That(col.name, Is.Not.EqualTo("LockedGate"), "an active gate collider is left at " + gate);

            var hero = new GameObject("ProbeHero"); hero.transform.position = gate - through * 3f + Vector3.up * 0.1f;
            var cc = hero.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.9f, 0f);
            Physics.SyncTransforms();
            float start = Vector3.Dot(hero.transform.position, through);
            for (int i = 0; i < 60; i++) { cc.Move(through * 0.1f + Vector3.down * 0.05f); yield return null; }
            float moved = Vector3.Dot(hero.transform.position, through) - start;
            Object.Destroy(hero);
            Assert.That(moved, Is.GreaterThan(5.0f), "a hero walks 6 m through the opened doorway (moved " + moved + ")");
        }

        // Catches: the ambush springing on an empty room when the only player left during the delay (it must re-arm), and then springing once someone is inside.
        [UnityTest]
        public IEnumerator AmbushFloor_PlayerLeavesDuringTheDelay_NothingSprings_ThenSpringsWhenSomeoneIsInside()
        {
            uint seed = 0; int floor = 0;
            for (uint s = 1; s < 3000 && floor == 0; s++)
                for (int f = 2; f <= 3 && floor == 0; f++)
                    if (CellarFloorRoster.TwistOf(s, f) == FloorTwist.Ambush) { seed = s; floor = f; }
            Assert.That(floor, Is.Not.EqualTo(0));
            Ctx c = null; yield return Setup(x => c = x, default, seed, floor);
            DiveHaulSync.Summary sum = c.Haul.Current;
            CellarEncounterPlan plan = c.Haul.EncounterPlan ?? CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, sum.ExtraEnemiesPerRoom, sum.HunterLatched);
            int room = plan.AmbushRoom; int expected = plan.CountIn(room); PlacedRoom pr = c.Layout.Rooms[room];
            Vector3 inside = Cell(c, plan.Spawns[0].RoomIndex == room ? plan.Spawns[0].X : pr.X, plan.Spawns[0].RoomIndex == room ? plan.Spawns[0].Y : pr.Y) + Vector3.forward * 0.1f;

            c.Players[c.HostId] = inside;
            for (int i = 0; i < 5; i++) yield return null;
            c.Players[c.HostId] = inside + Vector3.right * 80f;   // far outside every room, still a known player
            yield return new WaitForSeconds(FloorTwistTuning.AmbushDelaySeconds + 0.6f);
            Assert.That(c.Contents.EnterEnemyCount(room), Is.EqualTo(0), "nobody was in the room when the delay ended: no spring");
            Assert.That(EnemiesOn(c.HostNm).Count, Is.EqualTo(0));
            c.Players[c.HostId] = inside;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected, Timeout, "the re-armed ambush never sprang once a player was inside");
        }

        // Catches: the hunter respawning on top of a player the host's filtered list dropped (downed heroes, players not yet live). A player standing on the hunter cell who has not
        // moved since the floor armed is "stale" in the filtered list; the guard must still see him.
        [UnityTest]
        public IEnumerator Hunter_PlayerOnItsCell_NoRespawn_RespawnsOnceThePlayerLeaves()
        {
            Ctx c = null; yield return Setup(x => c = x, default, null, 4);
            // The host feeds the stir itself (players connected, valuables carried): two players here, so 13 carried valuables = 13 x 5 x sqrt 2 = 92 (floors alone cannot reach the hunter).
            for (int n = 0; n < 13; n++) Assert.That(c.Haul.ServerAddItem(c.HostId, 101), Is.True);
            DiveStir stir = c.Haul.Session.Stir;
            Assert.That(stir.HunterLatched, Is.True, "the stir latched the hunter");
            yield return PlayModeHarness.WaitUntil(() => c.Haul.EncounterPlan != null && c.Haul.EncounterPlan.Hunter != null && c.Haul.EncounterPlan.HunterLatched, Timeout, "the encounter plan has no hunter");
            EncounterSpawn h = c.Haul.EncounterPlan.Hunter;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.HunterEnemy != null, Timeout, "the hunter never spawned the first time");
            Enemy first = c.Contents.HunterEnemy;
            c.Players[c.HostId] = Cell(c, h.X, h.Y);
            Assert.That(c.Contents.ServerArmFloor(c.Layout, c.Floor.Built.Root), Is.True, "re-arm with the player already standing on the hunter cell (stale in the filtered list)");
            for (int i = 0; i < 30; i++) yield return null;
            yield return new WaitForSeconds(0.5f);
            Assert.That(c.Contents.HunterEnemy, Is.Null, "a player stands on the hunter cell: no respawn");
            c.Players[c.HostId] = Cell(c, h.X, h.Y) + Vector3.right * 80f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.HunterEnemy != null, Timeout, "the hunter never respawned once the player left");
        }

        // Catches: BUGS 18, a save with piece 0 already home left piece id 101 without a collectible sphere (spot i was mapped to PieceId(floor, i), the spot count to the remaining count).
        [UnityTest]
        public IEnumerator Pieces_PieceZeroAlreadyHome_EveryRemainingPieceStillGetsASphere()
        {
            Ctx c = null; yield return Setup(x => c = x);
            var ids = new List<int>(c.Haul.Session.Pieces.OnFloor(c.Layout.Floor));
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(2), "premise: the pieceful floor holds at least two pieces");
            int home = ids[0];
            Assert.That(c.Haul.Session.Pieces.Found(home), Is.True);
            Assert.That(c.Contents.ServerArmFloor(c.Layout, c.Floor.Built.Root), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Contents.PieceCount == ids.Count - 1 && c.Peer.PieceCount == ids.Count - 1, Timeout, "the floor did not rebuild with one piece sphere fewer");
            Assert.That(c.Contents.TryPiecePosition(0, out Vector3 at), Is.True);
            int left = ids[1];
            Assert.That(c.Haul.Session.Pieces.IsFound(left), Is.False);
            c.Players[c.HostId] = at;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Session.Pieces.IsFound(left), Timeout, "the remaining piece (" + left + ") has no collectible sphere");
        }

        // ================================================================================================================================
        // Plan 3 task 9 on the chest: gold purse, momentum rank bonus, full bag. Host + one client, players simulated by PlayersOverride (no heroes here).
        // Catches: a purse roll reaching PlayerBag.Add (which rejects it) or being lost, a purse blocked by a full bag (it needs no slot), the failed dive keeping the wrong share of the gold, momentum's rank
        // bonus not reaching the chest roll, a full bag (the carried satchel counts as a slot) opening the chest and eating the item, the refusal toast reaching the wrong peer, the refused chest being spent.
        // NOT covered: the toast's on-screen text (the walk); the Interact edge.
        // ================================================================================================================================

        private static (uint seed, int chest) FindGoldChest(int floor)
        {
            for (uint s = 1; s < 4000; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                for (int i = 0; i < l.Chests.Count && i < 64; i++)
                {
                    if (l.Chests[i].HoldsKey) continue;
                    bool all = true;
                    for (int slot = 0; slot < 4; slot++) all &= CellarLoot.RollChest(s, floor, i, slot) == CellarLoot.GoldId;
                    if (all) return (s, i);
                }
            }
            Assert.Fail("no floor-" + floor + " loot chest rolls a purse for every player slot in 4000 seeds"); return (0, 0);
        }

        [UnityTest]
        public IEnumerator GoldPurse_AddsToDiveGold_EvenWithAFullBag_FailHalvesIt()
        {
            int floor = 2;
            (uint seed, int idx) = FindGoldChest(floor);
            Ctx c = null; yield return Setup(x => c = x, default, seed, floor);
            PlayerBag bag = c.Haul.ServerBagOf(c.ClientId);
            for (int n = 0; n < PlayerBag.MaxCarried; n++) Assert.That(c.Haul.ServerAddItem(c.ClientId, 101), Is.True);
            Assert.That(bag.IsFull, Is.True, "premise: a full bag");
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            c.Players[c.ClientId] = at + new Vector3(1f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => Opened(c, idx), Timeout, "the purse chest never opened (a purse needs no slot)");
            Assert.That(c.Contents.LastLootItem, Is.EqualTo(CellarLoot.GoldId));
            Assert.That(bag.DiveGold, Is.EqualTo(CellarLoot.GoldPurse), "the purse went to the dive gold");
            Assert.That(bag.Carried.Count, Is.EqualTo(PlayerBag.MaxCarried), "and never reached PlayerBag.Add");
            Assert.That(bag.Gold, Is.EqualTo(0), "home gold waits for the dive's end");

            Assert.That(c.Haul.ServerFailDive(), Is.True);
            Assert.That(bag.Gold, Is.EqualTo(DiveGold.KeptOnFail(CellarLoot.GoldPurse)), "a failed dive keeps half the purse");
            Assert.That(bag.DiveGold, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Chest_MomentumRankBonus_ReachesTheRoll()
        {
            const int floor = 6;
            uint seed = 0; int idx = -1;
            for (uint s = 1; s < 3000 && idx < 0; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                for (int i = 0; i < l.Chests.Count && i < 64 && idx < 0; i++)
                {
                    if (l.Chests[i].HoldsKey) continue;
                    bool ok = true;
                    for (int slot = 0; slot < 4 && ok; slot++)
                    {
                        int plain = CellarLoot.RollChest(s, floor, i, slot, 0, false), bonus = CellarLoot.RollChest(s, floor, i, slot, 1, false);
                        ok = plain > 0 && bonus > 0 && plain != CellarLoot.BootsId && bonus != CellarLoot.BootsId && plain != bonus;
                    }
                    if (ok) { seed = s; idx = i; }
                }
            }
            Assert.That(idx, Is.GreaterThanOrEqualTo(0), "premise: a floor-6 chest whose roll moves with the bonus for every slot");
            Ctx c = null; yield return Setup(x => c = x, default, seed, floor);
            c.Haul.Session.Momentum.OnPushOn();   // the dive started on floor 1 and walked to the bank: pushing on earns momentum
            Assert.That(c.Haul.Session.Momentum.ChestRankBonus(floor), Is.EqualTo(1), "premise: the second half rolls one rank higher");
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            c.Players[c.ClientId] = at + new Vector3(1f, 0f, 0f);
            int slotC = c.Haul.PlayerSlotOf(c.ClientId);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => Opened(c, idx), Timeout, "the chest never opened");
            Assert.That(c.Contents.LastLootItem, Is.EqualTo(CellarLoot.RollChest(seed, floor, idx, slotC, 1, false)), "the roll carries momentum's bonus");
            Assert.That(c.Contents.LastLootItem, Is.Not.EqualTo(CellarLoot.RollChest(seed, floor, idx, slotC, 0, false)), "and differs from the plain roll");
        }

        [UnityTest]
        public IEnumerator Chest_FullBag_IsRefusedAndStaysClosedForThatPlayer()
        {
            Ctx c = null; yield return Setup(x => c = x);
            int floor = c.Layout.Floor, idx = -1;
            for (int i = 0; i < c.Layout.Chests.Count && i < 64 && idx < 0; i++)
                if (!c.Layout.Chests[i].HoldsKey && CellarLoot.RollChest(c.Seed, floor, i, c.Haul.PlayerSlotOf(c.ClientId)) != CellarLoot.GoldId && CellarLoot.RollChest(c.Seed, floor, i, c.Haul.PlayerSlotOf(c.HostId)) != CellarLoot.GoldId) idx = i;
            Assert.That(idx, Is.GreaterThanOrEqualTo(0), "premise: a loot chest that pays the client and the host an item, not a purse");
            Assert.That(c.Peer.TryChestPosition(idx, out Vector3 at), Is.True);
            PlayerBag bag = c.Haul.ServerBagOf(c.ClientId);
            for (int n = 0; n < PlayerBag.MaxCarried - 1; n++) Assert.That(c.Haul.ServerAddItem(c.ClientId, 101), Is.True);
            bag.Satchel.Restore(new LostSatchel(floor, new[] { 102 }));
            Assert.That(bag.PickUpSatchel(), Is.True, "59 items and a carried satchel: the bag is full");
            var refused = new List<(ulong, int)>(); var opened = new List<int>();
            c.Contents.ChestRefusedFull += (who, chest) => refused.Add((who, chest));
            c.Contents.ChestOpened += (chest, item) => opened.Add(chest);

            c.Players[c.ClientId] = at + new Vector3(1f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.NoticeCount == 1, Timeout, "the client was never told its bag is full");
            Assert.That(c.Peer.LastNotice, Is.EqualTo(CellarContentsSync.BagFullText));
            Assert.That(Opened(c, idx), Is.False, "the chest stays closed for that player");
            Assert.That(bag.Carried.Count, Is.EqualTo(PlayerBag.MaxCarried - 1), "nothing was added");
            Assert.That(refused, Is.EqualTo(new[] { (c.ClientId, idx) }));
            Assert.That(opened, Is.Empty, "a refused open raises no ChestOpened");
            Assert.That(c.Contents.NoticeCount, Is.EqualTo(0), "the host's own copy was not told: the notice is owner-only");

            // The chest is still there for the next player, and still closed for the full one.
            c.Players[c.HostId] = at + new Vector3(1f, 0f, 0f);
            c.Contents.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => c.Haul.ServerBagOf(c.HostId).ChestOpened(c.Seed, floor, idx), Timeout, "the host's player could not open the same chest");
            Assert.That(opened, Is.EqualTo(new[] { idx }));
            Assert.That(Opened(c, idx), Is.False);
            Assert.That(c.Peer.NoticeCount, Is.EqualTo(1), "no second toast for someone else's open");
        }
    }
}
