// Authority: test-only. M5 contents: the host (CellarContentsSync) spawns room enemies, opens chests, drops and hands out the gate key, opens the locked door and hides pieces;
// a client sees the replicated state. Host + one client in one process (scene management off; sync objects are dynamically spawned network prefabs from the owned list).
// Players are simulated through CellarContentsSync.PlayersOverride (no hero objects exist here), so the RPC range rule is judged on those positions.
// Catches: enemies not spawning on first room entry / not appearing on the client, a room not clearing, a chest opening out of range or twice, a client not seeing the mask, loot
// not reaching the haul, the carrier not dropping the gate key, the pickup not unlocking the exit on every peer, the door staying shut with the key held, pieces not reaching
// the haul or not reappearing after a failed dive.
// Also: a party wipe (CombatHost.ServerClearEnemies) drops no key and clears no room, and the room respawns on re-entry; a floor built under a root at (200,0,0) has chests, pieces
// and room spawns where the host checks them; a player still at its pre-floor-change position is ignored until it has moved.
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

        private IEnumerator Setup(System.Action<Ctx> done, Vector3 floorOrigin = default, uint? seedOverride = null, int startFloor = 1)
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

            c.Seed = seedOverride ?? FindSeed();
            Assert.That(c.Haul.ServerBegin(new[] { 2, 2, 2 }), Is.True);
            Assert.That(c.Haul.ServerSetFloor(startFloor), Is.True);
            Assert.That(c.Floor.ServerSetFloor(c.Seed, startFloor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.FloorSync.Built != null && c.Peer.FloorSync.BuiltFloor == startFloor && c.Peer.PieceCount > 0 && c.Contents.PieceCount > 0, Timeout, "floors were not built");
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
            Assert.That(c.Haul.HostState.Haul.LootUnbanked(CellarLoot.GateKeyId), Is.EqualTo(0), "keys are not loot");
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
            Assert.That(c.Haul.HostState.Haul.LootUnbanked(CellarLoot.RoomKeyId), Is.EqualTo(0), "keys are not loot");
            Assert.That(c.Haul.Current.UnbankedLoot, Is.EqualTo(0), "the key chest adds nothing to the haul");
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
            int expected = CellarEncounterPlan.Build(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected && c.Contents.GateKeyCarrierEnemy != null, Timeout, "the room's enemies were not spawned");

            CombatHost.Current.ServerClearEnemies();   // what a party wipe does
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.HostNm).Count == 0 || c.Contents.GateKeyCarrierEnemy == null, Timeout, "the wipe did not despawn the enemies");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0), "a wipe is not a kill: no gate key");
            Assert.That(c.Contents.State.Cleared & (1UL << room), Is.EqualTo(0UL), "a wiped room is not cleared");

            // the player is still standing in the room: it spawns afresh
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected && c.Contents.AliveInRoom(room) == expected, Timeout, "the room did not respawn after the wipe");
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.HostNm).Count >= expected, Timeout, "no enemies after the respawn");
            Assert.That(c.Contents.State.GateKey, Is.EqualTo((byte)0));
            Assert.That(c.Haul.Current.UnbankedLoot, Is.EqualTo(0));
        }

        // The carrier leaves without dying (despawned) while room-mates live: no key, but once the room-mates are dead the room resets and the carrier is back on the next entry.
        [UnityTest]
        public IEnumerator CarrierDespawnedAlive_RoomMatesLive_NoKey_ThenTheRoomResetsAndTheCarrierComesBack()
        {
            Ctx c = null; yield return Setup(x => c = x);
            GateKeyCarrier car = c.Layout.Carrier; int room = car.RoomIndex;
            int expected = CellarEncounterPlan.Build(c.Layout, c.Floor.Built.Dressing, 0, false).CountIn(room);
            Assume.That(expected, Is.GreaterThan(1), "premise: the carrier has room-mates");
            c.Players[c.HostId] = Cell(c, car.X, car.Y) + Vector3.forward * 0.1f;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected && c.Contents.GateKeyCarrierEnemy != null, Timeout, "the room's enemies were not spawned");

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
            Assert.That(c.Contents.State.Chests, Is.EqualTo(0UL));
            c.Players[c.ClientId] = at + new Vector3(1f, 0f, 0f);
            c.Peer.RequestOpenChest(idx);
            yield return PlayModeHarness.WaitUntil(() => (c.Peer.State.Chests & (1UL << idx)) != 0, Timeout, "the chest did not open at x=200");

            // piece: the sphere sits inside the floor's bounds and stepping on it collects it
            Assert.That(c.Peer.TryPiecePosition(0, out Vector3 piece), Is.True);
            Assert.That(piece.x, Is.InRange(b.min.x - 1f, b.max.x + 1f)); Assert.That(piece.z, Is.InRange(b.min.z - 1f, b.max.z + 1f));
            c.Players[c.HostId] = piece;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.LeftFloor1 == 1, Timeout, "the piece at x=200 was not collected");

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
            Assert.That(c.Haul.Current.LeftFloor1, Is.EqualTo(2), "a stale position collects nothing");
            Assert.That(c.Contents.EnterEnemyCount(car.RoomIndex), Is.EqualTo(0));

            // the teleport lands: the player moves, then steps on the piece
            c.Players[c.HostId] = elsewhere;
            yield return new WaitForSeconds(0.3f);
            c.Players[c.HostId] = piece;
            yield return PlayModeHarness.WaitUntil(() => c.Haul.Current.LeftFloor1 == 1, Timeout, "after moving, the piece is collected");

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
            Assert.That(c.Contents.IsExitUnlocked(1), Is.False, "a normal floor 1 is locked until the gate key is picked");
            Assert.That(c.Contents.IsExitUnlocked(4), Is.True, "floor 4 has no stair gate");
            Assert.That(c.Contents.IsExitUnlocked(2), Is.False, "floor 2 is not the armed floor");

            c.Contents.AutoArm = false;
            CellarFloorLayout l = c.Layout;
            var noCarrier = new CellarFloorLayout(l.Seed, 1, false, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, null);
            var badCarrier = new CellarFloorLayout(l.Seed, 1, false, l.Theme, l.Rooms, l.Corridors, l.Links, l.DeadEnds, l.Chests, l.LockedRoomIndex, l.LockedLinkIndex, l.KeyChestIndex, new GateKeyCarrier(99, 0, 0, 0));
            int fired = -1;
            c.Contents.ExitUnlocked += f => fired = f;

            Assert.That(c.Contents.ServerArmFloor(noCarrier, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(1), "the event fires inside ServerArmFloor");
            Assert.That(c.Contents.IsExitUnlocked(1), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked, Timeout, "the client never saw the unlocked exit");

            fired = -1;
            c.Contents.ServerClearFloor();
            Assert.That(c.Contents.IsExitUnlocked(1), Is.False);
            Assert.That(c.Contents.ServerArmFloor(badCarrier, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(1), "an invalid carrier also unlocks");

            fired = -1;
            c.Contents.ServerClearFloor();
            Assert.That(c.Contents.ServerArmFloor(l, c.Floor.Built.Root), Is.True);
            Assert.That(fired, Is.EqualTo(-1), "a valid carrier keeps the exit locked");
            Assert.That(c.Contents.IsExitUnlocked(1), Is.False);
            yield return PlayModeHarness.WaitUntil(() => !c.Peer.State.ExitUnlocked, Timeout, "the client kept the old unlocked flag");
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
            CellPos[] ring = CellarAmbush.Cells(c.Layout, dressing, plan, room);
            PlacedRoom pr = c.Layout.Rooms[room];

            c.Players[c.HostId] = Cell(c, plan.Spawns[0].RoomIndex == room ? plan.Spawns[0].X : pr.X, plan.Spawns[0].RoomIndex == room ? plan.Spawns[0].Y : pr.Y) + Vector3.forward * 0.1f;
            float t0 = Time.time;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Time.time - t0, Is.LessThan(FloorTwistTuning.AmbushDelaySeconds * 0.5f), "the frames before the delay check must stay well inside the delay");
            Assert.That(c.Contents.EnterEnemyCount(room), Is.EqualTo(0), "nothing springs before the delay");
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == expected, Timeout, "the ambush enemies did not spring");
            float springAt = Time.time;   // the host spawned them this frame or the one before: positions are still the spawn cells
            Assert.That(springAt - t0, Is.GreaterThanOrEqualTo(FloorTwistTuning.AmbushDelaySeconds - 0.1f), "the ambush sprang before the delay ran out");
            Assert.That(EnemiesOn(c.HostNm).Count, Is.EqualTo(expected));
            foreach (Enemy e in EnemiesOn(c.HostNm)) Assert.That(NearRing(c, ring, e), Is.True, "a host ambush enemy stands near one of the ring cells right after the spawn");
            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(c.Client.NetworkManager).Count == expected, Timeout, "the client does not see the ambush enemies");
            foreach (Enemy e in EnemiesOn(c.Client.NetworkManager)) Assert.That(NearRing(c, ring, e), Is.True, "a client ambush enemy stands near one of the ring cells");
        }
    }
}
