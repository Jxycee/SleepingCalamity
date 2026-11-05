// Authority: test-only. M5 QA walk (ported to the 7-floor dive, plan 3 task 12): a host + one client walk the dive floors (the authored floor 1, then the generated floor that holds a piece), the stir
// wiring and the bank room through the real DiveDirector, CellarFloorSync, DiveHaulSync, CellarContentsSync and player prefab.
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

        // The first-half generated floor (2 or 4) that holds a bakery piece in the fallback world (the director's depths when no world is loaded); pieces never lie on floors 1, 3 or 5.
        private static int PieceFloor()
        {
            IReadOnlyDictionary<int, int> depths = PieceDepths.Assign(DiveDirector.FallbackWorldSeed, PieceCatalog.Specs);
            foreach (int f in new[] { 2, 4 }) foreach (KeyValuePair<int, int> kv in depths) if (kv.Value == f && !PieceCatalog.IsPerson(kv.Key)) return f;
            Assert.Fail("[setup] no first-half piece on floor 2 or 4");
            return 0;
        }

        // First seed whose generated floor `floor` has a gate-key carrier, the locked room and its key chest.
        private static uint FindSeed(int floor)
        {
            for (uint sd = 1; sd < 2500; sd++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(sd, floor);
                if (l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0) return sd;
            }
            return 0;
        }

        // Starts the dive on `floor` through the checkpoint pick (a held checkpoint allows floors 1-5) and waits until every peer built it.
        private IEnumerator StartOn(Rig rig, int floor, string step)
        {
            if (floor > UndercroftPlan.FirstFloor) rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Peer.ServerStartDive(), Is.False, step + " a client cannot start a dive");
            Assert.That(rig.Host.ServerStartDive(floor), Is.True, step + " the host starts the dive on floor " + floor);
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, floor, step);
            yield return BothAtEntry(rig, step);
            Assert.That(rig.Peer.NetActive.Value, Is.True, step + " the client sees the dive active");
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
        public IEnumerator Walk_Floor1ToTheGeneratedPieceFloor_GateKey_Chest_Piece_LockedDoor_Descend([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            int target = PieceFloor();
            uint seed = FindSeed(target);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed with a carrier and a locked room on floor " + target);
            Rig rig = null;
            yield return Start(net, r => rig = r);
            rig.Host.SeedOverride = seed;
            yield return StartOn(rig, 1, "[F1 start]");
            Assert.That(rig.Host.RequireGateKey, Is.True, "[F1 start] contents gate the stair");
            Assert.That(rig.HostHaul.Current.PiecesLeft, Is.EqualTo(0), "[F1 start] the authored floor holds no piece");

            // Down to the target floor: the authored floors open at once, a generated floor's stair stays shut until the carrier's key is taken.
            for (int f = 1; f < target; f++)
            {
                yield return TakeGateKey(rig, f);
                KillAll(rig.HostNm);
                yield return Descend(rig, f);
            }
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(target));
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 shutStair), Is.True);
            yield return ClientTo(rig, shutStair + new Vector3(1f, 0.1f, 0f), "[F" + target + " stair shut]");
            int handled = rig.Host.RequestsHandled;
            rig.Peer.RequestStair(target);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[F" + target + " stair shut] the press never reached the host");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(target), "[F" + target + " stair shut] the stair stays shut while the carrier holds the key");
            yield return TakeGateKey(rig, target);
            int piecesAssigned = rig.HostHaul.Current.PiecesLeft;
            Assert.That(piecesAssigned, Is.GreaterThan(0), "[F" + target + " start] the floor has pieces left to find");

            // Open a (non-key) chest from the client: loot enters the opener's bag on both peers.
            int chest = -1;
            for (int i = 0; i < rig.HostFloor.Layout.Chests.Count && chest < 0; i++) if (!rig.HostFloor.Layout.Chests[i].HoldsKey && i < 64) chest = i;
            Assert.That(chest, Is.GreaterThanOrEqualTo(0), "[chest] the floor has an ordinary chest");
            Assert.That(rig.Contents.TryChestPosition(chest, out Vector3 chestPos), Is.True, "[chest] the chest object exists");
            yield return ClientTo(rig, chestPos + new Vector3(0.5f, 0.1f, 0f), "[chest walk]");
            KillAll(rig.HostNm);
            ulong clientId = rig.Client.NetworkManager.LocalClientId;
            int loot0 = rig.HostHaul.ServerBagOf(clientId).Carried.Count;   // chest loot goes into the opener's bag (plan 2 task 6: loot chests are per player, no shared opened bit)
            rig.PeerContents.RequestOpenChest(chest);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.ServerBagOf(clientId).Carried.Count > loot0, Timeout, "[chest] the client's open-chest request never put loot in the opener's bag on the host (requests handled " + rig.Contents.ChestRequestsHandled + ")");
            Assert.That(rig.Contents.State.Chests & (1UL << chest), Is.EqualTo(0UL), "[chest] a loot chest never sets the shared opened bit");
            // the opener's own client sees its item through its owner-only bag view, and its own copy of the chest lowered (the per-NetworkManager lookup: the host's hero is the 'last spawned' Local)
            NetworkManager clientNm = rig.Client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => { PlayerBagSync v = PlayerBagSync.ForLocalPlayer(clientNm); return v != null && v.View.Carried.Length == loot0 + 1; }, Timeout, "[chest] the opener's client never saw the item in its own bag view");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.ChestShownOpen(chest), Timeout, "[chest] the client never showed the chest open");

            // A bakery piece -> pieces-left drops on both peers, the piece is safe and in the town stock at pickup.
            Assert.That(rig.Contents.PieceCount, Is.EqualTo(piecesAssigned), "[piece] the contents placed one spot per piece the floor holds");
            Assert.That(rig.Contents.TryPiecePosition(0, out Vector3 piecePos), Is.True, "[piece] piece 0 exists");
            yield return ClientTo(rig, piecePos, "[piece walk]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.PiecesLeft == piecesAssigned - 1, Timeout, "[piece] walking onto the piece never took it (host left " + rig.HostHaul.Current.PiecesLeft + ", was " + piecesAssigned + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.PiecesLeft == piecesAssigned - 1, Timeout, "[piece] pieces-left never dropped on the client");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.FoundPieces == 1, Timeout, "[piece] the piece is found and safe on both peers (client found " + rig.PeerHaul.Current.FoundPieces + ")");
            Assert.That(System.Linq.Enumerable.Any(rig.Host.Town.State.StockPieces), Is.True, "[piece] the piece reached the town stock at pickup, not at the dive's end");
            yield return PlayModeHarness.WaitUntil(() => !rig.PeerContents.PieceVisible(0), Timeout, "[piece] the client still draws the collected piece");

            // Locked room key chest + door.
            CellarFloorLayout lay = rig.HostFloor.Layout;
            Assert.That(lay.HasLockedRoom && lay.KeyChestIndex >= 0 && lay.KeyChestIndex < 64, Is.True, "[locked room] the seed was picked for it");
            int kc = lay.KeyChestIndex;
            Assert.That(rig.Contents.TryChestPosition(kc, out Vector3 kcPos), Is.True, "[key chest] exists");
            Assert.That(rig.Contents.TryGatePosition(out Vector3 gatePos), Is.True, "[locked door] the locked room has a gate");
            Assert.That(rig.Contents.GateBlocking, Is.True, "[locked door] shut at first");
            yield return ClientTo(rig, kcPos + new Vector3(0.5f, 0.1f, 0f), "[key chest walk]");
            KillAll(rig.HostNm);
            rig.PeerContents.RequestOpenChest(kc);
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.RoomKeyHeld, Timeout, "[key chest] the key chest never gave the room key");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.RoomKeyHeld, Timeout, "[key chest] the client never saw the room key");
            yield return ClientTo(rig, gatePos + new Vector3(1f, 0.1f, 0f), "[locked door walk]");
            rig.PeerContents.RequestOpenDoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.DoorOpen, Timeout, "[locked door] holding the key at the gate never opened it");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.DoorOpen && !rig.PeerContents.GateBlocking, Timeout, "[locked door] the client still sees the gate shut");

            // On down: the cleared floors are marked, and the stir counts them.
            KillAll(rig.HostNm);
            yield return Descend(rig, target);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(target + 1));
            Assert.That((rig.HostHaul.Current.ClearedMask & (1 << (target - 1))) != 0, Is.True, "[arrive] the floor above is marked cleared");
            Assert.That(rig.HostHaul.Current.StirValue, Is.GreaterThan(0), "[arrive] a cleared floor raises the stir meter (pieces never do)");
        }

        [UnityTest]
        public IEnumerator Stir_PastFortyAndSeventy_AddsExtraEnemiesAndTheHunter([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            uint seed = FindSeed(2);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed");
            Rig rig = null;
            yield return Start(net, r => rig = r);
            // The stir is the valuables the party carries (plan 3 task 10; pieces and floors alone cannot reach the hunter): two players, so each valuable is worth 5 x sqrt 2 = 7.07.
            ulong stirHost = rig.HostNm.LocalClientId;
            rig.Host.SeedOverride = seed;
            yield return StartOn(rig, 2, "[stir start]");   // a generated floor: the authored one has no rooms to spawn extras in
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null, Timeout, "[stir] the host never built an encounter plan");
            Assert.That(rig.HostHaul.EncounterPlan.ExtraPerRoom, Is.EqualTo(0), "[stir] no extras at the start");
            Assert.That(rig.HostHaul.EncounterPlan.Hunter, Is.Null, "[stir] no hunter at the start");

            // 40: eight valuables = 56.6.
            for (int n = 0; n < 8; n++) Assert.That(rig.HostHaul.ServerAddItem(stirHost, 101), Is.True);
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

            // 70: thirteen valuables = 92.
            for (int n = 0; n < 5; n++) Assert.That(rig.HostHaul.ServerAddItem(stirHost, 101), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.HunterLatched && rig.PeerHaul.Current.HunterLatched, Timeout, "[stir 70] the hunter never latched on both peers (value " + rig.HostHaul.Current.StirValue + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null && rig.HostHaul.EncounterPlan.Hunter != null, Timeout, "[stir 70] the encounter plan has no hunter");
            yield return PlayModeHarness.WaitUntil(() => CountAlive(rig.HostNm, EnemyKind.RatKing) > 0, Timeout, "[stir 70] the contents never spawned the hunter enemy (rat kings alive before: " + rats0 + ")");
            Assert.That(rig.HostHaul.Current.DeepClosesLatched, Is.False, "[stir 70] below 100 the deep has not closed");
        }

        [UnityTest]
        public IEnumerator BankRoom_ClientBanks_GoHome_FindsSurvive_ThenSecondDiveFails_PiecesStaySafe([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            int pieceFloor = PieceFloor();
            uint seed = FindSeed(pieceFloor);
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed");
            Rig rig = null;
            yield return Start(net, r => rig = r);
            rig.Host.SeedOverride = seed;
            ulong hostId = rig.HostNm.LocalClientId;
            yield return StartOn(rig, UndercroftPlan.BankFloor, "[F5 start]");
            Assert.That(rig.HostHaul.Session.StartedAtBank, Is.True, "[F5 start] the dive starts in the bank room's floor (decision 2)");
            Assert.That(rig.HostHaul.ServerAddPiece(PieceCatalog.SpareFrameId), Is.True, "[F5 find] a spare piece (a second-half piece never lies on the floors the second dive walks)");
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True, "[F5 bank] the bank floor has the bank room cell");
            Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
            yield return ClientTo(rig, bank + new Vector3(-0.5f, 0.1f, 0f), "[F5 bank walk]");
            rig.PeerHaul.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DivePhase == DivePhase.BankRoom && rig.PeerHaul.Current.DivePhase == DivePhase.BankRoom, Timeout, "[F5 bank] the bank press never opened the room on both peers");
            rig.Peer.RequestGoHome();
            int handled = rig.Host.RequestsHandled;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[F5 go home] the press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "[F5 go home] two players: one press only opens the confirm window");
            Assert.That(rig.Host.ServerTryGoHome(hostId), Is.True, "[F5 go home] the second player's press confirms");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[F5 go home] the dive never ended on both peers");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(1), "[F5 go home] the find survives");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetCheckpointHeld.Value && rig.Peer.NetCheckpointHeld.Value, Timeout, "[F5 go home] the checkpoint never reached both peers");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, Home) < 6f && Flat(rig.ClientHeroOnHost.transform.position, Home) < 8f, Timeout, "[F5 go home] the party is not back in the hub");
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == CellarFloorSync.NoFloor && rig.PeerFloor.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "[F5 go home] the floor was never cleared on both peers");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == 0, Timeout, "[F5 go home] the contents were never cleared");

            // Second dive (the checkpoint pick): take a real piece on the piece floor, then the whole party falls.
            yield return StartOn(rig, pieceFloor, "[dive 2 start]");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(1), "[dive 2 start] the find of dive 1 is still there");
            int before = rig.HostHaul.Current.PiecesLeft;
            Assert.That(before, Is.GreaterThan(0), "[dive 2 piece] the floor has a piece left");
            Assert.That(rig.Contents.TryPiecePosition(0, out Vector3 pp), Is.True, "[dive 2 piece]");
            yield return ClientTo(rig, pp, "[dive 2 piece walk]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.PiecesLeft == before - 1 && rig.PeerHaul.Current.PiecesLeft == before - 1, Timeout, "[dive 2 piece] pieces-left never dropped on both peers");
            KillAll(rig.HostNm);

            var hv = rig.HostHero.GetComponent<HeroVitals>(); var cv = rig.ClientHeroOnHost.GetComponent<HeroVitals>();
            hv.ServerDamage(100000); cv.ServerDamage(100000);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[dive 2 fail] the wipe never ended the dive on both peers");
            Assert.That(rig.HostHaul.Current.CurrentFloor, Is.EqualTo(0), "[dive 2 fail] floor reset");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(2), "[dive 2 fail] pieces are safe on pickup: the failed dive keeps the one it found");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, Home) < 6f && Flat(rig.ClientHeroOnHost.transform.position, Home) < 8f, Timeout, "[dive 2 fail] the party is not back in the hub");
            yield return PlayModeHarness.WaitUntil(() => hv.IsUp && cv.IsUp, Timeout, "[dive 2 fail] the heroes stayed down");
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == 0, Timeout, "[dive 2 fail] the contents were never cleared");
            Assert.That(rig.Host.Pieces.PiecesLeft(pieceFloor), Is.EqualTo(before - 1), "[dive 2 fail] the collected piece is not back on its floor");

            // A third dive starts clean on floor 1 (the pick was spent): the finds stay found.
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[dive 3 start]");
            yield return AllBuilt(rig, 1, "[dive 3 start]");
            Assert.That(rig.HostHaul.Current.FoundPieces, Is.EqualTo(2), "[dive 3 start] found pieces stay found");
            Assert.That(rig.Host.Pieces.PiecesLeft(pieceFloor), Is.EqualTo(before - 1), "[dive 3 start] the collected piece is not offered again");
        }
    }
}
