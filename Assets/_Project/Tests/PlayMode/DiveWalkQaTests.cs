// Authority: test-only. M5 QA walk: a host + one client walk the dive floors 1-4 through the real DiveDirector, CellarFloorSync, DiveHaulSync, CellarContentsSync and player prefab.
// The CLIENT hero drives (owner-side moves + client RPCs); the host side only kills enemies through the same ServerApplyHit path a swing uses.
// Every assertion message starts with the walk step ("[F1 chest]") so a failure names the broken step. A failure that is a GAME bug is left failing; do not weaken it here.
// NOT covered by a one-process test: the interact KEY and prompts, the greybox look, real relay latency, real melee combat.
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
    public class DiveWalkQaTests
    {
        private const float Timeout = 40f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm; public PlayModeHarness.TestClient Client;
            public DiveDirector Host, Peer;
            public NetworkObject HostHero, ClientHeroOnHost, ClientHero;
            public CellarFloorSync HostFloor, PeerFloor; public DiveHaulSync HostHaul, PeerHaul;
            public CellarContentsSync Contents, PeerContents;
        }

        private IEnumerator Start(NetCondition net, System.Action<Rig> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            PlayModeHarness.AddNetworkSimulator(net);   // before the host starts
            var rig = new Rig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8));
            _clients.Add(client); rig.Client = client;
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "[start] the client never connected");
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True, "[start] ServerConfigure");
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "[start] the client never received the director");
            rig.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>();
            rig.HostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, Home, Quaternion.identity);
            NetworkManager cnm = client.NetworkManager;
            rig.ClientHeroOnHost = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, cnm.LocalClientId, false, true, false, Home + new Vector3(2f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null, Timeout, "[start] the client never received its hero");
            rig.ClientHero = cnm.LocalClient.PlayerObject;
            yield return Seconds(0.5f);
            done(rig);
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static T Peer<T>(Rig rig, NetworkBehaviour host) where T : NetworkBehaviour
        {
            return rig.Client.NetworkManager.SpawnManager.SpawnedObjects[host.NetworkObjectId].GetComponent<T>();
        }

        private static IEnumerator ResolvePeers(Rig rig)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null && rig.Host.ContentsSync != null, Timeout, "[start dive] the dive never spawned its floor/haul/contents syncs");
            rig.HostFloor = rig.Host.FloorSync; rig.HostHaul = rig.Host.HaulSync; rig.Contents = rig.Host.ContentsSync;
            var objs = rig.Client.NetworkManager.SpawnManager.SpawnedObjects;
            yield return PlayModeHarness.WaitUntil(() => objs.ContainsKey(rig.HostFloor.NetworkObjectId) && objs.ContainsKey(rig.HostHaul.NetworkObjectId) && objs.ContainsKey(rig.Contents.NetworkObjectId), Timeout, "[start dive] the client never received the dive syncs");
            rig.PeerFloor = Peer<CellarFloorSync>(rig, rig.HostFloor); rig.PeerHaul = Peer<DiveHaulSync>(rig, rig.HostHaul); rig.PeerContents = Peer<CellarContentsSync>(rig, rig.Contents);
        }

        // Both peers built `floor` with the identical layout, and the host armed the contents for it.
        private static IEnumerator AllBuilt(Rig rig, int floor, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == floor && rig.HostFloor.Built != null, Timeout, step + ": the host never built floor " + floor);
            yield return PlayModeHarness.WaitUntil(() => rig.PeerFloor.BuiltFloor == floor && rig.PeerFloor.Built != null, Timeout, step + ": the client never built floor " + floor);
            Assert.That(rig.PeerFloor.BuiltLayoutHash, Is.EqualTo(rig.HostFloor.BuiltLayoutHash), step + ": both peers build the identical floor " + floor + " (LayoutHash)");
            Assert.That(rig.PeerFloor.BuiltLayoutHash, Is.Not.EqualTo(0UL), step + ": layout hash is set");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == floor && rig.Contents.State.Seed == rig.HostFloor.BuiltSeed, Timeout, step + ": the director never armed the contents for floor " + floor);
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.Floor == floor, Timeout, step + ": the client never saw the contents state for floor " + floor);
        }

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        private static void Move(NetworkObject ownerCopy, Vector3 pos)
        {
            var cc = ownerCopy.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ownerCopy.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // The client hero walks (teleports its own copy) to `pos` and the host sees it there.
        private static IEnumerator ClientTo(Rig rig, Vector3 pos, string step)
        {
            Move(rig.ClientHero, pos);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, pos) < 1.5f, Timeout, step + ": the client hero never arrived on the host at " + pos + " (host copy at " + rig.ClientHeroOnHost.transform.position + ")");
        }

        private static IEnumerator BothAtEntry(Rig rig, string step)
        {
            Vector3 entry = CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, entry) < 6f, Timeout, step + ": the host hero is at " + rig.HostHero.transform.position + ", wanted near the entry " + entry);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, entry) < 6f, Timeout, step + ": the client hero is at " + rig.ClientHeroOnHost.transform.position + " on the host, wanted near the entry " + entry);
            Assert.That(Flat(rig.ClientHero.transform.position, entry), Is.LessThan(6f), step + ": the client's own copy was moved to the entry");
            yield return Seconds(1f);   // late arrival teleports must have landed before the test moves a hero
        }

        // Real hit path (what a swing ends in) on every living enemy, so a walking hero is not killed mid-step. Returns how many were killed.
        private static int KillAll(NetworkManager nm)
        {
            var hit = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            int n = 0;
            foreach (NetworkObject o in new List<NetworkObject>(nm.SpawnManager.SpawnedObjects.Values))
            {
                if (o == null) continue;
                Enemy e = o.GetComponent<Enemy>();
                if (e == null || !e.Alive) continue;
                for (int i = 0; i < 6 && e != null && e.Alive; i++) e.ServerApplyHit(hit, e.transform.position + Vector3.back, Time.timeAsDouble);
                n++;
            }
            return n;
        }

        private static int CountAlive(NetworkManager nm, EnemyKind kind)
        {
            int n = 0;
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values)
            {
                if (o == null) continue;
                Enemy e = o.GetComponent<Enemy>();
                if (e != null && e.Alive && e.NetKind.Value == (byte)kind) n++;
            }
            return n;
        }

        // First seed whose floors 1-3 all have a gate-key carrier and whose floor 1 has the locked room + key chest.
        private static uint FindSeed(bool need2and3)
        {
            for (uint sd = 1; sd < 2500; sd++)
            {
                CellarFloorLayout l1 = CellarFloorGenerator.Layout(sd, 1);
                if (!(l1.HasLockedRoom && l1.Carrier != null && l1.KeyChestIndex >= 0)) continue;
                if (need2and3 && (CellarFloorGenerator.Layout(sd, 2).Carrier == null || CellarFloorGenerator.Layout(sd, 3).Carrier == null)) continue;
                return sd;
            }
            return 0;
        }

        // Kill the carrier the way the game does (enter its room, hit it), take the dropped key; the client drives. Skips when the floor never needed a key.
        private IEnumerator TakeGateKey(Rig rig, int floor)
        {
            string step = "[F" + floor + " gate key]";
            if (rig.Contents.State.ExitUnlocked) yield break;
            GateKeyCarrier car = rig.HostFloor.Layout.Carrier;
            Assert.That(car, Is.Not.Null, step + ": the exit is shut yet the layout has no carrier (soft-lock)");
            Vector3 carrierCell = rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(car.X, car.Y));
            yield return ClientTo(rig, carrierCell, step + " walk to the carrier room");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.GateKeyCarrierEnemy != null, Timeout, step + ": entering the carrier's room never spawned the carrier");
            Enemy carrier = rig.Contents.GateKeyCarrierEnemy;
            var hit = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            for (int i = 0; i < 6 && carrier.Alive; i++) carrier.ServerApplyHit(hit, carrier.transform.position + Vector3.back, Time.timeAsDouble);
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.GateKey == 1, Timeout, step + ": the carrier never dropped the gate key");
            KillAll(rig.HostNm);
            yield return ClientTo(rig, rig.Contents.State.GateKeyPos + new Vector3(0.5f, 0.1f, 0f), step + " walk to the key");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.ExitUnlocked, Timeout, step + ": picking the key up never unlocked the exit");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.ExitUnlocked, Timeout, step + ": the client never saw the exit unlocked");
        }

        private IEnumerator Descend(Rig rig, int from)
        {
            string step = "[F" + from + " stair]";
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True, step + ": the floor has a stair");
            yield return ClientTo(rig, stair + new Vector3(1f, 0.1f, 0f), step + " walk to the stair");
            rig.Peer.RequestStair(from);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == from + 1, Timeout, step + ": the opened stair never took the party down to " + (from + 1));
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.CurrentFloor == from + 1, Timeout, step + ": the client never saw floor " + (from + 1));
            yield return AllBuilt(rig, from + 1, "[F" + (from + 1) + " arrive]");
            yield return BothAtEntry(rig, "[F" + (from + 1) + " arrive]");
        }

        // ---------------------------------------------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Walk_Floors1To3_GateKey_Chest_Piece_LockedDoor_Checkpoint_Descend([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            uint seed = FindSeed(true);
            if (seed == 0) seed = FindSeed(false);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed with a carrier and a locked room");
            Rig rig = null;
            yield return Start(net, r => rig = r);
            rig.Host.SeedOverride = seed;
            Assert.That(rig.Peer.ServerStartDive(), Is.False, "[F1 start] a client cannot start a dive");
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[F1 start] the host starts the dive");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1, "[F1 start]");
            yield return BothAtEntry(rig, "[F1 start]");
            Assert.That(rig.Peer.NetActive.Value, Is.True, "[F1 start] the client sees the dive active");
            Assert.That(rig.Host.RequireGateKey, Is.True, "[F1 start] contents gate the stair");
            Assert.That(rig.Contents.State.ExitUnlocked, Is.False, "[F1 start] the exit starts shut when the floor has a carrier");
            int piecesAssigned = rig.HostHaul.Current.LeftFloor1;
            Assert.That(piecesAssigned, Is.GreaterThan(0), "[F1 start] floor 1 has pieces left to find");

            // F1: stair refused while the carrier holds the key.
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair1), Is.True);
            yield return ClientTo(rig, stair1 + new Vector3(1f, 0.1f, 0f), "[F1 stair shut]");
            int handled = rig.Host.RequestsHandled;
            rig.Peer.RequestStair(1);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[F1 stair shut] the press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(1), "[F1 stair shut] the stair stays shut while the carrier holds the key");

            // F1: carrier kill -> key -> stair opens (peer sees it too).
            yield return TakeGateKey(rig, 1);

            // F1: open a (non-key) chest from the client: loot enters the shared haul on both peers.
            int chest = -1;
            for (int i = 0; i < rig.HostFloor.Layout.Chests.Count && chest < 0; i++) if (!rig.HostFloor.Layout.Chests[i].HoldsKey && i < 64) chest = i;
            Assert.That(chest, Is.GreaterThanOrEqualTo(0), "[F1 chest] the floor has an ordinary chest");
            Assert.That(rig.Contents.TryChestPosition(chest, out Vector3 chestPos), Is.True, "[F1 chest] the chest object exists");
            yield return ClientTo(rig, chestPos + new Vector3(0.5f, 0.1f, 0f), "[F1 chest walk]");
            KillAll(rig.HostNm);
            ulong clientId = rig.Client.NetworkManager.LocalClientId;
            int loot0 = rig.HostHaul.ServerBagOf(clientId).Carried.Count;   // chest loot goes into the opener's bag (plan 2 task 6: loot chests are per player, no shared opened bit)
            rig.PeerContents.RequestOpenChest(chest);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.ServerBagOf(clientId).Carried.Count > loot0, Timeout, "[F1 chest] the client's open-chest request never put loot in the opener's bag on the host (requests handled " + rig.Contents.ChestRequestsHandled + ")");
            Assert.That(rig.Contents.State.Chests & (1UL << chest), Is.EqualTo(0UL), "[F1 chest] a loot chest never sets the shared opened bit");
            // the opener's own client sees its item through its owner-only bag view, and its own copy of the chest lowered (the per-NetworkManager lookup: the host's hero is the 'last spawned' Local)
            NetworkManager clientNm = rig.Client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync v = PlayerBagSync.ForLocalPlayer(clientNm); return v != null && v.View.Carried.Length == loot0 + 1; }, Timeout, "[F1 chest] the opener's client never saw the item in its own bag view");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.ChestShownOpen(chest), Timeout, "[F1 chest] the client never showed the chest open");

            // F1: bakery piece -> pieces-left drops on both peers.
            Assert.That(rig.Contents.PieceCount, Is.GreaterThan(0), "[F1 piece] the contents placed piece spots");
            Assert.That(rig.Contents.TryPiecePosition(0, out Vector3 piecePos), Is.True, "[F1 piece] piece 0 exists");
            yield return ClientTo(rig, piecePos, "[F1 piece walk]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.LeftFloor1 == piecesAssigned - 1, Timeout, "[F1 piece] walking onto the piece never took it (host left " + rig.HostHaul.Current.LeftFloor1 + ", was " + piecesAssigned + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.LeftFloor1 == piecesAssigned - 1, Timeout, "[F1 piece] pieces-left never dropped on the client");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.FoundPieces == 1 && rig.PeerHaul.Current.BankedPieces == 1, Timeout, "[F1 piece] the piece is found and safe on both peers (client found " + rig.PeerHaul.Current.FoundPieces + ", safe " + rig.PeerHaul.Current.BankedPieces + ")");
            Assert.That(System.Linq.Enumerable.Any(rig.Host.Town.State.StockPieces), Is.True, "[F1 piece] the piece reached the town stock at pickup, not at the dive's end");
            yield return PlayModeHarness.WaitUntil(() => !rig.PeerContents.PieceVisible(0), Timeout, "[F1 piece] the client still draws the collected piece");

            // F1: locked room key chest + door.
            CellarFloorLayout l1 = rig.HostFloor.Layout;
            if (l1.HasLockedRoom && l1.KeyChestIndex >= 0 && l1.KeyChestIndex < 64)
            {
                int kc = l1.KeyChestIndex;
                Assert.That(rig.Contents.TryChestPosition(kc, out Vector3 kcPos), Is.True, "[F1 key chest] exists");
                Assert.That(rig.Contents.TryGatePosition(out Vector3 gatePos), Is.True, "[F1 locked door] the locked room has a gate");
                Assert.That(rig.Contents.GateBlocking, Is.True, "[F1 locked door] shut at first");
                yield return ClientTo(rig, kcPos + new Vector3(0.5f, 0.1f, 0f), "[F1 key chest walk]");
                KillAll(rig.HostNm);
                rig.PeerContents.RequestOpenChest(kc);
                yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.RoomKeyHeld, Timeout, "[F1 key chest] the key chest never gave the room key");
                yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.RoomKeyHeld, Timeout, "[F1 key chest] the client never saw the room key");
                yield return ClientTo(rig, gatePos + new Vector3(1f, 0.1f, 0f), "[F1 locked door walk]");
                rig.PeerContents.RequestOpenDoor();
                yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.DoorOpen, Timeout, "[F1 locked door] holding the key at the gate never opened it");
                yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.DoorOpen && !rig.PeerContents.GateBlocking, Timeout, "[F1 locked door] the client still sees the gate shut");
            }
            else Debug.Log("[DiveWalkQa] seed " + seed + " has no locked room on floor 1; door step skipped");

            // F1 -> F2.
            KillAll(rig.HostNm);
            yield return Descend(rig, 1);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(2));

            // F2: checkpoint bank with the floor-1 piece, hurt heroes get healed.
            var hostVitals = rig.HostHero.GetComponent<HeroVitals>(); var clientVitals = rig.ClientHeroOnHost.GetComponent<HeroVitals>();
            hostVitals.ServerDamage(20); clientVitals.ServerDamage(20);
            Assert.That(hostVitals.Hp, Is.LessThan(hostVitals.MaxHp), "[F2 bank] test premise: the host hero is hurt");
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank2), Is.True, "[F2 bank] floor 2 has the checkpoint bank point");
            yield return ClientTo(rig, bank2 + new Vector3(0.5f, 0.1f, 0f), "[F2 bank walk]");
            rig.PeerHaul.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.HostState.BankUsed(2) && rig.HostHaul.Current.BankedPieces == 1, Timeout, "[F2 bank] the client's bank press never used the checkpoint (used " + rig.HostHaul.HostState.BankUsed(2) + ", safe pieces " + rig.HostHaul.Current.BankedPieces + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CheckpointReached && rig.Host.NetCheckpoint.Value && rig.Peer.NetCheckpoint.Value, Timeout, "[F2 bank] checkpoint never reached / synced to the client");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.BankedPieces == 1, Timeout, "[F2 bank] the client never saw the banked count");
            yield return PlayModeHarness.WaitUntil(() => hostVitals.Hp == hostVitals.MaxHp && clientVitals.Hp == clientVitals.MaxHp, Timeout, "[F2 bank] banking never healed the party (host " + hostVitals.Hp + "/" + hostVitals.MaxHp + ", client " + clientVitals.Hp + "/" + clientVitals.MaxHp + ")");

            // F2 -> F3.
            yield return TakeGateKey(rig, 2);
            yield return Descend(rig, 2);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(3));
            Assert.That((rig.HostHaul.Current.ClearedMask & 0b11) == 0b11, Is.True, "[F3 arrive] floors 1 and 2 are marked cleared");
        }

        [UnityTest]
        public IEnumerator Stir_PastFortyAndSeventy_AddsExtraEnemiesAndTheHunter([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            uint seed = FindSeed(false);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed");
            Rig rig = null;
            yield return Start(net, r => rig = r);
            // Pieces no longer feed the stir (plan 2, spec 4.5) and floors alone cap it at 52.5 in the real 4-floor dive, so the test raises the per-floor term: 35 x 1.25 (two players) = 43.75 after one
            // floor cleared, 87.5 after two. The real hunter and deep-closes stir comes with plan 3's valuables stir.
            StirTuning tuning = StirTuning.Default; tuning.PerFloorCleared = 35f;
            rig.Host.SeedOverride = seed; rig.Host.DiveStir = tuning;
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[stir start]");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1, "[stir start]");
            yield return BothAtEntry(rig, "[stir start]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null, Timeout, "[stir] the host never built an encounter plan");
            Assert.That(rig.HostHaul.EncounterPlan.ExtraPerRoom, Is.EqualTo(0), "[stir] no extras at the start");
            Assert.That(rig.HostHaul.EncounterPlan.Hunter, Is.Null, "[stir] no hunter at the start");

            // 40: one floor cleared = 43.75.
            Assert.That(rig.HostHaul.ServerMarkCleared(1), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.ExtraEnemiesPerRoom >= 1, Timeout, "[stir 40] the meter never latched an extra enemy (value " + rig.HostHaul.Current.StirValue + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.ExtraEnemiesPerRoom >= 1, Timeout, "[stir 40] the client never saw the extra-enemy latch");
            Assert.That(rig.HostHaul.Current.HunterLatched, Is.False, "[stir 40] the hunter waits for 70");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null && rig.HostHaul.EncounterPlan.ExtraPerRoom >= 1, Timeout, "[stir 40] the encounter plan never gained extras");
            CellarEncounterPlan plan = rig.HostHaul.EncounterPlan;
            int room = -1;
            for (int r = 0; r < rig.HostFloor.Layout.Rooms.Count && room < 0; r++) if (plan.CountIn(r, EncounterKind.Extra) > 0) room = r;
            Assert.That(room, Is.GreaterThanOrEqualTo(0), "[stir 40] the plan has an extra enemy in some room");

            // The contents spawn the plan's enemies (authored + extras) when a player enters that room.
            // Stand on a free cell of the room that is not a spawn cell and is as far from every spawn as the room allows: a hero dropped onto a spawn cell is shoved out by the enemy
            // that spawns there (and can be pushed off the floor edge), so the host copy never settles within the arrival radius.
            CellarFloorLayout stirLayout = rig.HostFloor.Layout;
            CellPos enterCell = default; float bestGap = -1f;
            foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(stirLayout, CellarFloorDressing.Resolve(stirLayout), room))
            {
                float gap = float.MaxValue;
                foreach (EncounterSpawn sp in plan.Spawns) if (sp.RoomIndex == room) gap = Mathf.Min(gap, Mathf.Abs(sp.X - c.X) + Mathf.Abs(sp.Y - c.Y));
                if (gap > bestGap) { bestGap = gap; enterCell = c; }
            }
            Assert.That(bestGap, Is.GreaterThan(0f), "[stir 40] the room has a free cell that is not a spawn cell");
            Vector3 roomCell = rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(enterCell.X, enterCell.Y));
            int rats0 = CountAlive(rig.HostNm, EnemyKind.RatKing);
            yield return ClientTo(rig, roomCell, "[stir 40 enter room]");
            int expected = plan.CountIn(room);
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.EnterEnemyCount(room) == expected, Timeout, "[stir 40] entering room " + room + " spawned " + rig.Contents.EnterEnemyCount(room) + " enemies, the plan says " + expected + " (extras " + plan.CountIn(room, EncounterKind.Extra) + ")");
            KillAll(rig.HostNm);

            // 70: two floors cleared = 87.5.
            Assert.That(rig.HostHaul.ServerMarkCleared(2), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.HunterLatched && rig.PeerHaul.Current.HunterLatched, Timeout, "[stir 70] the hunter never latched on both peers (value " + rig.HostHaul.Current.StirValue + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null && rig.HostHaul.EncounterPlan.Hunter != null, Timeout, "[stir 70] the encounter plan has no hunter");
            yield return PlayModeHarness.WaitUntil(() => CountAlive(rig.HostNm, EnemyKind.RatKing) > 0, Timeout, "[stir 70] the contents never spawned the hunter enemy (rat kings alive before: " + rats0 + ")");
            Assert.That(rig.HostHaul.Current.DeepClosesLatched, Is.False, "[stir 70] below 100 the deep has not closed");
        }

        [UnityTest]
        public IEnumerator Floor4_ClientBanksAtTheHeart_GoHome_BankedSurvives_ThenSecondDiveFails_PiecesStaySafe([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            uint seed = FindSeed(false);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed");
            Rig rig = null;
            yield return Start(net, r => rig = r);
            rig.Host.SeedOverride = seed;
            Assert.That(rig.Host.ServerStartDive(4), Is.True, "[F4 start]");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 4, "[F4 start]");
            yield return BothAtEntry(rig, "[F4 start]");
            int left1 = rig.HostHaul.Current.LeftFloor1;
            Assert.That(rig.HostHaul.ServerAddPiece(DiveHaul.PieceId(1, 0)), Is.True, "[F4 bank] a carried piece");
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True, "[F4 bank] the heart has the baker");
            yield return ClientTo(rig, bank + new Vector3(0.5f, 0.1f, 0f), "[F4 bank walk]");
            rig.Peer.RequestGoHome();
            int handled = rig.Host.RequestsHandled;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[F4 early go-home] never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "[F4 early go-home] no way home before the bank");
            rig.PeerHaul.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.BankedPieces == 1 && rig.Host.NetHeartBanked.Value && rig.Peer.NetHeartBanked.Value, Timeout, "[F4 bank] the heart bank never landed on both peers");
            rig.Peer.RequestGoHome();
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[F4 go home] the dive never ended on both peers");
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(1), "[F4 go home] banked survives");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, Home) < 6f && Flat(rig.ClientHeroOnHost.transform.position, Home) < 8f, Timeout, "[F4 go home] the party is not back in the hub");
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor && rig.PeerFloor.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "[F4 go home] the floor was never cleared on both peers");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == 0, Timeout, "[F4 go home] the contents were never cleared");

            // Second dive: take a real piece on floor 1, then the whole party falls.
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[dive 2 start]");
            yield return AllBuilt(rig, 1, "[dive 2 start]");
            yield return BothAtEntry(rig, "[dive 2 start]");
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(1), "[dive 2 start] banked from dive 1 is still there");
            int before = rig.HostHaul.Current.LeftFloor1;
            Assert.That(rig.Contents.PieceCount, Is.GreaterThan(1), "[dive 2 piece] floor 1 has a second piece spot");
            Assert.That(rig.Contents.TryPiecePosition(1, out Vector3 pp), Is.True, "[dive 2 piece]");
            yield return ClientTo(rig, pp, "[dive 2 piece walk]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.LeftFloor1 == before - 1 && rig.PeerHaul.Current.LeftFloor1 == before - 1, Timeout, "[dive 2 piece] pieces-left never dropped on both peers");
            KillAll(rig.HostNm);

            var hv = rig.HostHero.GetComponent<HeroVitals>(); var cv = rig.ClientHeroOnHost.GetComponent<HeroVitals>();
            hv.ServerDamage(100000); cv.ServerDamage(100000);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[dive 2 fail] the wipe never ended the dive on both peers");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(0), "[dive 2 fail] floor reset");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(1), "[dive 2 fail] the piece found in dive 2 is still counted for that dive");
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(2), "[dive 2 fail] pieces are safe on pickup: the failed dive keeps the one it found");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.LeftFloor1 == before - 1 && rig.PeerHaul.Current.LeftFloor1 == before - 1, Timeout, "[dive 2 fail] pieces-left on floor 1 went back up although the piece is safe (host " + rig.HostHaul.Current.LeftFloor1 + ", client " + rig.PeerHaul.Current.LeftFloor1 + ", wanted " + (before - 1) + ")");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, Home) < 6f && Flat(rig.ClientHeroOnHost.transform.position, Home) < 8f, Timeout, "[dive 2 fail] the party is not back in the hub");
            yield return PlayModeHarness.WaitUntil(() => hv.IsUp && cv.IsUp, Timeout, "[dive 2 fail] the heroes stayed down");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == 0, Timeout, "[dive 2 fail] the contents were never cleared");

            // A third dive starts clean: the piece found in dive 2 stays collected (never offered again) and the found counter restarts.
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[dive 3 start]");
            yield return AllBuilt(rig, 1, "[dive 3 start]");
            Assert.That(rig.HostHaul.Current.LeftFloor1, Is.EqualTo(before - 1), "[dive 3 start] the collected piece is not offered again");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(0), "[dive 3 start] found counts this dive only");
        }
    }
}
