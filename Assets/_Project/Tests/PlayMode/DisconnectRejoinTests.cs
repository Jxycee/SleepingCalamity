// Authority: test-only. M7 S3 (docs/design/m7-proposal.md section 4): scripted disconnect and rejoin proofs on a host + real TestClients (no scenes: the director, the cellar syncs and the heroes are spawned
// by the test, like StateHashSmokeTests). After every case the world must be consistent: ReplicatedStateHash equal among the peers that remain, no stuck lock, no error log (the test runner fails a test on any
// unexpected error log).
//   1 a client drops at the bank floor carrying a bag item and found pieces      2 a client drops right after sending a bank / stair / go-home request      3 the builder-lock holder drops (ghost, placed town)
//   4 a go-home from the bank room with a client already gone                   5 rejoin keeps weapon growth (same PlayerId), a fresh player does not inherit it
//   6 a world that records four players, only two of them connected: the absent two stay untouched in the save, difficulty scales to the present two
// Rules the code applies and these tests pin: the found pieces are the PARTY's (DivePieces, host-only, safe on pickup); a dropped hero's bag (PartyBags, keyed by PlayerKey) stays and comes home / waits in a satchel with the party, it is not
// forfeited to the leaver and not dropped on the floor. NGO destroys the leaver's player object. The stir meter's player factor and the difficulty scale follow who is connected.
// The bank cases run at the bank floor (5): the dive starts there through the held checkpoint (plan 3 decision 2), the bank press opens the bank room, and going home is the shared choice.
// NOT covered: a crash-style drop (transport timeout rather than a graceful leave), relay latency, scene-managed clients, a host that drops (no host migration in Phase 1), visuals.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DisconnectRejoinTests
    {
        private const float Timeout = 40f;
        private const int Frame = 100, Oven = 101, Door = 200, Sign = 201;
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);
        private static readonly string Axe = AttackCatalog.Heirlooms[0];
        private string _dir;
        private GameObject _combatGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_combatGo != null) { Object.Destroy(_combatGo); _combatGo = null; }
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the clients StartClients created
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public List<PlayModeHarness.TestClient> Clients = new List<PlayModeHarness.TestClient>();
            public HashSet<int> Dropped = new HashSet<int>();
            public DiveDirector Host; public NetworkObject HostHero;
            public List<NetworkObject> ClientHeroes = new List<NetworkObject>();   // each client's own copy (the owner side)
            public int Ends;                                                       // DiveEnded events seen on the host

            public NetworkManager ClientNm(int i) => Clients[i].NetworkManager;
            public ulong ClientId(int i) => Clients[i].NetworkManager.LocalClientId;
            public DiveDirector PeerDirector(int i) => ClientNm(i).SpawnManager.SpawnedObjects[Host.NetworkObjectId].GetComponent<DiveDirector>();
            public DiveHaulSync Haul => Host.HaulSync;
            public List<NetworkManager> Remaining()
            {
                var l = new List<NetworkManager> { HostNm };
                for (int i = 0; i < Clients.Count; i++) if (!Dropped.Contains(i)) l.Add(Clients[i].NetworkManager);
                return l;
            }
        }

        // Host (no scenes) + `clients` TestClients, the director spawned and configured, one hero per peer spawned by the host. firstClientIndex picks the clients' PlayerIds (PlayerId(20 + i)).
        private IEnumerator Start(System.Action<Rig> done, int clients, System.Action<AppBootstrap> beforeHost = null, int firstClientIndex = 20, bool saves = false)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            if (saves) _dir = PlayModeHarness.BeginSaveTest();   // after the load: it switches the world save off
            var rig = new Rig { App = AppBootstrap.Current };
            beforeHost?.Invoke(rig.App);
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            yield return PlayModeHarness.StartClients(rig.App, clients, false, rig.Clients, firstClientIndex);
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True, "[start] ServerConfigure");
            rig.Host.DiveEnded += _ => rig.Ends++;
            rig.HostHero = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, Home, Quaternion.identity);
            for (int i = 0; i < clients; i++)
                rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.ClientId(i), false, true, false, Home + new Vector3(2f * (i + 1), 0f, 0f), Quaternion.identity);
            for (int i = 0; i < clients; i++)
            {
                NetworkManager cnm = rig.ClientNm(i);
                yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && cnm.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "[start] client " + i + " never received its hero and the director");
                rig.ClientHeroes.Add(cnm.LocalClient.PlayerObject);
            }
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

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        private static void Move(NetworkObject o, Vector3 pos)
        {
            var cc = o.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            o.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // ---------------------------------------------------------------- dive helpers

        private static IEnumerator FloorBuilt(Rig rig, int floor, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null && rig.Host.ContentsSync != null, Timeout, step + ": the dive never spawned its syncs");
            foreach (NetworkManager nm in rig.Remaining())
            {
                NetworkManager peer = nm;
                yield return PlayModeHarness.WaitUntil(() =>
                {
                    ulong id = rig.Host.FloorSync.NetworkObjectId;
                    return peer.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject o) && o != null && o.GetComponent<CellarFloorSync>().BuiltFloor == floor && o.GetComponent<CellarFloorSync>().Built != null;
                }, Timeout, step + ": a peer never built floor " + floor);
            }
        }

        private IEnumerator StartDive(Rig rig)
        {
            rig.Host.SeedOverride = 12345u;   // every dive draws a fresh random seed otherwise; the bank point and walk positions below must be the same floors every run
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[dive] the host starts the dive");
            yield return FloorBuilt(rig, 1, "[dive F1]");
        }

        // The dive starts at the bank floor through the checkpoint pick (decision 2): the guardian is already beaten, the party arrives at the bank room's entrance.
        private IEnumerator StartAtBank(Rig rig)
        {
            rig.Host.SeedOverride = 12345u;
            rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Host.ServerStartDive(UndercroftPlan.BankFloor), Is.True, "[dive F5] the host starts the dive at the bank");
            yield return FloorBuilt(rig, UndercroftPlan.BankFloor, "[dive F5]");
        }

        // Host-side stair descent (the DiveDirector rules run in full: open stair, the asker near it); the host hero walks to the stair each retry because the arrival teleport may land late.
        private IEnumerator Descend(Rig rig, int from)
        {
            rig.Host.ServerOpenStair(from);
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (rig.Host.FloorSync.Built == null || !CellarFloorBuilder.TryStairWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out Vector3 stair)) return false;
                Move(rig.HostHero, stair + new Vector3(1f, 0.1f, 0f));
                return rig.Host.ServerTryStair(rig.HostNm.LocalClientId, from);
            }, Timeout, "[stair F" + from + "] the host-side descent was refused");
            yield return FloorBuilt(rig, from + 1, "[F" + (from + 1) + " arrive]");
        }

        private static Vector3 BankPos(Rig rig)
        {
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out Vector3 bank), Is.True, "the floor has a bank point");
            return bank + new Vector3(0.5f, 0.1f, 0f);
        }

        // Both go-home presses of a two-player party at the bank: the host's hero stands at the bank (the range check), the host presses, then client `i` walks there and presses.
        private IEnumerator GoHomeTogether(Rig rig, int i, string step)
        {
            Move(rig.HostHero, BankPos(rig));
            Assert.That(rig.Host.ServerTryGoHome(rig.HostNm.LocalClientId), Is.True, step + ": the host's go-home press is accepted");
            if (!rig.Host.NetActive.Value) yield break;
            yield return ClientTo(rig, i, BankPos(rig), step + " walk to the bank");
            float asked = -10f;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (!rig.Host.NetActive.Value) return true;
                if (Time.realtimeSinceStartup - asked >= 1f) { asked = Time.realtimeSinceStartup; rig.PeerDirector(i).RequestGoHome(); }
                return false;
            }, Timeout, step + ": the second player's go-home press never ended the dive");
        }

        // The client's hero (owner copy) walks to pos and the host's copy has arrived (a client RPC is range-checked against the host copy).
        private static IEnumerator ClientTo(Rig rig, int i, Vector3 pos, string step)
        {
            ulong id = rig.ClientId(i);
            NetworkObject onHost = null;
            foreach (NetworkObject o in rig.HostNm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == id) onHost = o;
            Assert.That(onHost, Is.Not.Null, step + ": the host has client " + i + "'s hero");
            yield return PlayModeHarness.WaitUntil(() => { Move(rig.ClientHeroes[i], pos); return onHost != null && Flat(onHost.transform.position, pos) < 1.5f; }, Timeout, step + ": client hero never arrived on the host");
        }

        // A client's bank press is one RPC; the host refuses it while the hero's replicated position is still short of the bank point, and a refusal is not retried. Ask again every
        // second until the bank lands (a second bank at a spent point is refused, so re-asking is safe).
        private static bool AskBankUntil(Rig rig, int i, ref float askedAt, System.Func<bool> done)
        {
            if (done()) return true;
            if (Time.realtimeSinceStartup - askedAt >= 1f) { askedAt = Time.realtimeSinceStartup; rig.PeerDirector(i).RequestBank(); }
            return false;
        }

        // Leaves with a graceful shutdown and waits until the host, the roster and every remaining peer have forgotten the client and its hero.
        private IEnumerator Drop(Rig rig, int i, string step)
        {
            ulong id = rig.ClientId(i);
            System.Threading.Tasks.Task leave = rig.Clients[i].Session.LeaveAsync();
            rig.Dropped.Add(i);
            yield return PlayModeHarness.Await(leave, Timeout);
            yield return AfterDrop(rig, id, step);
        }

        private IEnumerator AfterDrop(Rig rig, ulong id, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => !IsListed(rig.HostNm, id), Timeout, step + ": the host still lists the dropped client");
            yield return PlayModeHarness.WaitUntil(() => rig.App.Roster.ConnectedCount == rig.HostNm.ConnectedClientsIds.Count, Timeout, step + ": the roster still counts the dropped client");
            foreach (NetworkManager nm in rig.Remaining())
            {
                NetworkManager peer = nm;
                yield return PlayModeHarness.WaitUntil(() => !HasHeroOf(peer, id), Timeout, step + ": a peer still has the dropped hero (orphaned player object)");
            }
        }

        private static bool IsListed(NetworkManager nm, ulong id) { foreach (ulong c in nm.ConnectedClientsIds) if (c == id) return true; return false; }

        private static bool HasHeroOf(NetworkManager nm, ulong clientId)
        {
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == clientId) return true;
            return false;
        }

        private static IEnumerator Consistent(Rig rig, string step) { yield return ReplicatedStateHash.AssertAllEqual(rig.Remaining(), step); }

        // ================================================================ case 1

        // Catches: a dropped client's hero left behind, the shared finds losing or duplicating the leaver's carried pieces, the stir player factor and party count not following the drop, a dive that cannot open the bank room or go home
        // after a drop. Rule pinned: the pieces found stay safe in the party; the leaver's own bag (keyed by their PlayerKey) keeps its items and they come home with the party.
        [UnityTest]
        public IEnumerator Case1_ClientDropsAtTheBankFloor_CarryingPieces_DiveOpensTheBankRoomAndGoesHome()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 2);
            yield return StartAtBank(rig);
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.DoorId), Is.True);
            ulong leaverId = rig.ClientId(0);
            Assert.That(rig.Haul.ServerAddItem(leaverId, 101), Is.True, "the leaver carries one item");
            PlayerBag leaverBag = rig.Haul.ServerBagOf(leaverId);   // the bag object itself: after the drop the roster no longer maps the client id to its key
            yield return Consistent(rig, "case1 before the drop");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(3), "host + 2 clients");
            Assert.That(rig.Haul.Session.Stir.PlayersPresent, Is.EqualTo(3), "the stir meter counts the three");
            var before = rig.Haul.Current;

            yield return Drop(rig, 0, "case1");
            yield return Consistent(rig, "case1 after the drop");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(2), "party count: host + 1 client");
            yield return PlayModeHarness.WaitUntil(() => rig.Haul.Session.Stir.PlayersPresent == 2, Timeout, "case1: the stir meter's player factor never followed the drop");
            Assert.That(rig.Host.NetActive.Value && rig.PeerDirector(1).NetActive.Value, Is.True, "the dive carries on for host and the other client");
            Assert.That(rig.Haul.Current.FoundPieces, Is.EqualTo(before.FoundPieces), "the pieces found stay in the party (not lost, not duplicated)");
            Assert.That(leaverBag.Carried, Is.EqualTo(new[] { 101 }), "the leaver's own bag keeps its item (not lost, not duplicated)");
            Assert.That(rig.Haul.Current.FoundPieces, Is.EqualTo(2), "pieces are safe on pickup");
            Assert.That(rig.Haul.Current.CurrentFloor, Is.EqualTo(UndercroftPlan.BankFloor));

            // The dive can still open the bank room (the remaining client asks, in range) ...
            yield return ClientTo(rig, 1, BankPos(rig), "case1 walk to the bank");
            float askedAt1 = -10f;
            yield return PlayModeHarness.WaitUntil(() => AskBankUntil(rig, 1, ref askedAt1, () => rig.Haul.Session.Phase == DivePhase.BankRoom), Timeout, "case1: the remaining client could not open the bank room");
            Assert.That(leaverBag.BankOpen, Is.False, "case1: the leaver has no bank visit (capacity follows who is present)");
            Assert.That(leaverBag.Carried, Is.EqualTo(new[] { 101 }), "case1: the leaver's item is still carried in the open room");
            yield return Consistent(rig, "case1 bank room");
            Assert.That(rig.Haul.ServerEnterBankRoom(), Is.False, "the room is open already (no duplicate bank)");

            // ... and go home from it: the shared choice needs both remaining players.
            yield return GoHomeTogether(rig, 1, "case1 go home");
            yield return PlayModeHarness.WaitUntil(() => !rig.PeerDirector(1).NetActive.Value, Timeout, "case1: the remaining client never went home");
            Assert.That(rig.Ends, Is.EqualTo(1), "the dive ended exactly once");
            Assert.That(StockCount(rig), Is.EqualTo(2), "both found pieces are in the town stock");
            Assert.That(leaverBag.Carried, Is.Empty, "case1: the leaver's bag came home with the party");
            Assert.That(leaverBag.Stash.ContainsKey(101), Is.True, "case1: and its item is in the leaver's stash");
            yield return Consistent(rig, "case1 home");
        }

        // Catches: the host's placement map growing with every join (a dropped client's id kept in DiveDirector._placed), and a later joiner not being sent to the dive floor.
        [UnityTest]
        public IEnumerator Case1b_DroppedClient_IsPrunedFromPlacement_LaterJoinerIsPlacedOnTheDiveFloor()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1);
            yield return StartDive(rig);
            ulong gone = rig.ClientId(0);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.TestPlacedHas(gone) && rig.Host.TestPlacedHas(rig.HostNm.LocalClientId), Timeout, "case1b: the party was never placed on floor 1");
            yield return Drop(rig, 0, "case1b");
            Assert.That(rig.Host.TestPlacedHas(gone), Is.False, "the dropped client's placement record was pruned");
            Assert.That(rig.Host.TestPlacedHas(rig.HostNm.LocalClientId), Is.True, "the host's record stays");

            yield return PlayModeHarness.StartClients(rig.App, 1, false, rig.Clients, 31);
            int back = rig.Clients.Count - 1;
            ulong late = rig.ClientId(back);
            NetworkManager cnm = rig.ClientNm(back);
            rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, late, false, true, false, Home + new Vector3(4f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null, Timeout, "case1b: the late joiner never received its hero");
            yield return FloorBuilt(rig, 1, "[case1b late]");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.TestPlacedHas(late), Timeout, "case1b: the late joiner was never placed");
            NetworkObject onHost = null;
            foreach (NetworkObject o in rig.HostNm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == late) onHost = o;
            Assert.That(onHost, Is.Not.Null);
            yield return PlayModeHarness.WaitUntil(() => Flat(onHost.transform.position, Home) > 50f, Timeout, "case1b: the late joiner was not teleported to the dive floor (at " + onHost.transform.position + ")");
        }

        // ================================================================ case 2

        public enum Request { Bank, Stair, GoHome }

        // Catches: a request in flight from a client that drops handled twice (duplicate bank / double stair / double end), a half-applied request, or a dive left in a state nothing can finish.
        [UnityTest]
        public IEnumerator Case2_ClientDropsRightAfterSendingARequest_HandledOnceOrRefusedCleanly([Values(Request.Bank, Request.Stair, Request.GoHome)] Request kind)
        {
            Rig rig = null;
            yield return Start(r => rig = r, 2);
            if (kind == Request.Stair) yield return StartDive(rig); else yield return StartAtBank(rig);
            Vector3 at;
            if (kind == Request.Stair)
            {
                rig.Host.ServerOpenStair(1);
                Assert.That(CellarFloorBuilder.TryStairWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out Vector3 stair), Is.True);
                at = stair + new Vector3(1f, 0.1f, 0f);
            }
            else
            {
                Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
                Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.DoorId), Is.True);
                at = BankPos(rig);
            }
            yield return Consistent(rig, "case2 before");
            yield return ClientTo(rig, 0, at, "case2 walk");
            int floorBefore = rig.Haul.Current.CurrentFloor;
            DiveDirector peer = rig.PeerDirector(0);
            ulong id = rig.ClientId(0);

            // The request is sent, then the client leaves in the same frame.
            if (kind == Request.Bank) peer.RequestBank(); else if (kind == Request.Stair) peer.RequestStair(1); else peer.RequestGoHome();
            System.Threading.Tasks.Task leave = rig.Clients[0].Session.LeaveAsync();
            rig.Dropped.Add(0);
            yield return PlayModeHarness.Await(leave, Timeout);
            yield return AfterDrop(rig, id, "case2");
            yield return Seconds(1.5f);   // a request that did get through has been handled by now

            switch (kind)
            {
                case Request.Bank:
                    {
                        var s = rig.Haul.Current;
                        bool handled = rig.Haul.Session.Phase == DivePhase.BankRoom;   // pieces are safe on pickup, so the open bank room is what a handled request leaves behind
                        Assert.That(s.FoundPieces, Is.EqualTo(2), "case2 bank: the pieces are safe either way");
                        if (!handled) Assert.That(rig.Haul.ServerEnterBankRoom(), Is.True, "case2 bank: the refused request left the bank usable");
                        Assert.That(rig.Haul.ServerEnterBankRoom(), Is.False, "case2 bank: no duplicate bank");
                        break;
                    }
                case Request.Stair:
                    {
                        int f = rig.Haul.Current.CurrentFloor;
                        Assert.That(f == floorBefore || f == floorBefore + 1, Is.True, "case2 stair: the party is on floor " + floorBefore + " or exactly one below, was " + f);
                        yield return FloorBuilt(rig, f, "case2 stair");
                        if (f == floorBefore) yield return Descend(rig, 1);   // the refused request left the stair usable
                        Assert.That(rig.Haul.Current.CurrentFloor, Is.EqualTo(2), "case2 stair: one descent, not two");
                        break;
                    }
                default:
                    {
                        if (rig.Host.NetActive.Value) { Assert.That(rig.Ends, Is.EqualTo(0)); yield return GoHomeTogether(rig, 1, "case2 go-home"); }   // the leaver's press (if it got through) does not count for the party that is left
                        Assert.That(rig.Ends, Is.EqualTo(1), "case2 go-home: the dive ended exactly once");
                        yield return PlayModeHarness.WaitUntil(() => !rig.PeerDirector(1).NetActive.Value, Timeout, "case2 go-home: the other client never went home");
                        Assert.That(StockCount(rig), Is.EqualTo(2), "case2 go-home: the banked pieces are in the stock once");
                        break;
                    }
            }
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(2));
            yield return Consistent(rig, "case2 after " + kind);
        }

        // ================================================================ case 3

        private static IEnumerator Take(TownSync who, TownSync host)
        {
            who.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => who.IsBuilder && host.NetBuilder.Value == who.NetworkManager.LocalClientId, Timeout, "the lock was never granted");
        }

        private static int StockCount(Rig rig) { int n = 0; foreach (int _ in rig.Host.Town.State.StockPieces) n++; return n; }

        private static string Sig(TownState s) => TownWire.From(s).Signature();

        // Catches: a lock held by a client that is gone (nobody can build), a ghost left floating, the placed town changed or the world's stock lost by the drop, the next builder unable to take the lock.
        [UnityTest]
        public IEnumerator Case3_BuilderLockHolderDrops_LockFreed_GhostCleared_TownUnchanged_NextBuilderPlaces()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 2);
            foreach (int p in new[] { Frame, Oven, Door, Sign }) Assert.That(rig.Host.Town.State.AddPiece(p), Is.EqualTo(PlaceResult.Ok));
            rig.Host.Town.State.AddResident(TownBuildings.BakerResident);
            TownSync hostTown = rig.Host.TownSync;
            hostTown.ServerRefresh();
            ulong tid = hostTown.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm(0).SpawnManager.SpawnedObjects.ContainsKey(tid) && rig.ClientNm(1).SpawnManager.SpawnedObjects.ContainsKey(tid), Timeout, "case3: the clients never received the TownSync");
            TownSync a = rig.ClientNm(0).SpawnManager.SpawnedObjects[tid].GetComponent<TownSync>(), b = rig.ClientNm(1).SpawnManager.SpawnedObjects[tid].GetComponent<TownSync>();
            ulong aId = rig.ClientId(0);

            yield return Take(a, hostTown);
            a.SendGhost("bakery", "F02", 0, true);
            yield return PlayModeHarness.WaitUntil(() => hostTown.NetGhost.Value.Active, Timeout, "case3: the ghost never reached the host");
            a.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.Town.State.PlacedBuildingOf("bakery") != null, Timeout, "case3: the host never placed the frame");
            a.SendGhost("bakery", "F02", 0, true);   // a new ghost pose is up when the holder drops (a frame that is placed clears it again, so re-send for a different building is not needed: the lock itself is what is under test)
            string placed = Sig(rig.Host.Town.State);
            yield return PlayModeHarness.WaitUntil(() => b.View.PlacedBuildingOf("bakery") != null, Timeout, "case3: the other client never saw the frame");
            Assert.That(hostTown.NetBuilder.Value, Is.EqualTo(aId), "the dropping client holds the lock");

            yield return Drop(rig, 0, "case3");
            yield return PlayModeHarness.WaitUntil(() => hostTown.BuilderIsFree && b.BuilderIsFree, Timeout, "case3: the lock never freed on the host and the remaining client");
            Assert.That(hostTown.NetGhost.Value.Active, Is.False, "case3: the ghost is cleared");
            Assert.That(Sig(rig.Host.Town.State), Is.EqualTo(placed), "case3: the placed town is unchanged by the drop");
            Assert.That(Sig(b.View), Is.EqualTo(placed), "case3: and the remaining client sees the same town");
            yield return Consistent(rig, "case3 lock freed");

            yield return Take(b, hostTown);
            b.RequestPlacePart("bakery", "oven");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.Town.State.PlacedBuildingOf("bakery").PiecesByPart[1] >= 0, Timeout, "case3: the next builder could not place a part");
            b.ReleaseBuildLock();
            yield return PlayModeHarness.WaitUntil(() => hostTown.BuilderIsFree, Timeout, "case3: the next builder's release");
            yield return PlayModeHarness.WaitUntil(() => Sig(b.View) == Sig(rig.Host.Town.State), Timeout, "case3: host and client towns never agreed after the next builder");
            yield return Consistent(rig, "case3 next builder placed");
        }

        // ================================================================ case 4

        // Catches: a go-home that needs every client (a gone client's hero leaving the party stuck), the stock not receiving the found pieces, the dive not ending on the remaining peer.
        [UnityTest]
        public IEnumerator Case4_GoHomeFromTheBankRoom_WithAClientAlreadyGone()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 2);
            yield return StartAtBank(rig);
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.DoorId), Is.True);
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.BakerId), Is.True);
            Assert.That(rig.Haul.ServerEnterBankRoom(), Is.True, "[bank] the host-side bank press opens the room");
            Assert.That(rig.Haul.Current.FoundPieces, Is.EqualTo(2));
            yield return Drop(rig, 1, "case4");
            yield return Consistent(rig, "case4 after the drop");
            yield return GoHomeTogether(rig, 0, "case4 go home");
            yield return PlayModeHarness.WaitUntil(() => !rig.PeerDirector(0).NetActive.Value, Timeout, "case4: the remaining client never went home");
            Assert.That(rig.Ends, Is.EqualTo(1), "one end");
            Assert.That(StockCount(rig), Is.EqualTo(2), "the found pieces are in the stock");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "the baker came home");
            yield return Consistent(rig, "case4 home");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync.BuiltFloor == CellarFloorSync.NoFloor, Timeout, "case4: the floor was never cleared");
            Assert.That(rig.Host.ServerStartDive(), Is.True, "a new dive can start afterwards");
        }

        // ================================================================ case 5 and 6 helpers

        private CombatHost MakeCombat(Rig rig)
        {
            _combatGo = new GameObject("CombatHost_DisconnectTests");
            CombatHost combat = _combatGo.AddComponent<CombatHost>();
            combat.WavesEnabled = false; combat.WipeResetsParty = false;
            Assert.That(combat.ServerBegin(rig.HostNm), Is.True, "the combat host began");
            return combat;
        }

        private static string KeyIdOf(Rig rig, ulong clientId)
        {
            Assert.That(rig.App.Roster.TryGetKey(clientId, out PlayerKey key), Is.True, "the roster knows client " + clientId);
            return key.Id;
        }

        // ================================================================ case 5

        // Catches: weapon growth lost across a drop and rejoin (it is keyed by the recorded PlayerKey, not the NGO client id), or a fresh player inheriting a leaver's growth.
        [UnityTest]
        public IEnumerator Case5_Rejoin_WithTheSamePlayerId_KeepsWeaponGrowth_AFreshPlayerDoesNot()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1, app => Assert.That(app.WorldSaveFlow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local), Is.Not.Null, "a multiplayer world was made"), 20, true);
            CombatHost combat = MakeCombat(rig);
            ulong oldId = rig.ClientId(0);
            string pid = KeyIdOf(rig, oldId);
            Assert.That(pid, Is.EqualTo(PlayModeHarness.PlayerId(20)));
            combat.Growth(oldId).Weapon(Axe).AddAbs(40f);
            WeaponState w = combat.Growth(oldId).Weapon(Axe);
            (int level, float abs) = (w.Level, w.Abs);
            PlayerGrowth record = combat.Growth(oldId);
            Assert.That(w.LifetimeLevels + abs, Is.GreaterThan(0f), "the setup earned growth");

            yield return Drop(rig, 0, "case5");
            Assert.That(rig.App.Roster.TryFindConnected(PlayerKey.Local(pid), out _), Is.False, "the leaver is no longer connected");

            // Same PlayerId, a new connection.
            yield return PlayModeHarness.StartClients(rig.App, 1, false, rig.Clients, 20);
            int back = rig.Clients.Count - 1;
            ulong newId = rig.ClientId(back);
            Assert.That(newId, Is.Not.EqualTo(oldId), "the rejoin got a new client id");
            Assert.That(rig.App.Roster.TryFindConnected(PlayerKey.Local(pid), out ulong found) && found == newId, Is.True, "the roster maps the key to the new client id");
            WeaponState again = combat.Growth(newId).Weapon(Axe);
            Assert.That((again.Level, again.Abs), Is.EqualTo((level, abs)), "the rejoined player keeps their weapon growth");
            Assert.That(combat.Growth(newId), Is.SameAs(record), "the very same growth record");

            // A fresh player (another PlayerId) starts fresh, whatever client id it gets.
            yield return PlayModeHarness.StartClients(rig.App, 1, false, rig.Clients, 30);
            ulong freshId = rig.ClientId(rig.Clients.Count - 1);
            Assert.That(KeyIdOf(rig, freshId), Is.EqualTo(PlayModeHarness.PlayerId(30)));
            WeaponState fresh = combat.Growth(freshId).Weapon(Axe);
            Assert.That((fresh.LifetimeLevels, fresh.Abs), Is.EqualTo((0, 0f)), "a fresh player does not inherit the leaver's growth");
            Assert.That(combat.Growth(freshId), Is.Not.SameAs(record));

            // The world file keeps it too: the section holds the player after the drop.
            WeaponsSave saved = rig.App.World.CaptureWeapons();
            WeaponState persisted = GrowthSaves.Restore("x", pid, saved).Weapon(Axe);
            Assert.That((persisted.Level, persisted.Abs), Is.EqualTo((level, abs)), "the world's weapon section holds the leaver's growth");
        }

        // ================================================================ case 6

        // Catches: a four-player world that needs all four to play, an absent recorded player's record or growth touched by a session without them, difficulty scaling to the recorded rather than the present count.
        [UnityTest]
        public IEnumerator Case6_TwoOfFourRecordedPlayers_Dive_AbsentTwoUntouchedInTheSave_ScaleFollowsPresent()
        {
            Rig rig = null;
            string worldId = null, hostPid = null;
            string absentA = PlayModeHarness.PlayerId(22), absentB = PlayModeHarness.PlayerId(23);
            float seedA = 0f, seedB = 0f, seedPresent = 0f; int levelA = 0, levelB = 0;
            yield return Start(r => rig = r, 1, app =>
            {
                worldId = app.WorldSaveFlow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local);
                Assert.That(worldId, Is.Not.Null);
                WorldSaveStore store = app.WorldSaveFlow.Library.Open(worldId);
                hostPid = PlayerKey.Local(app.Services.Get<ISettings>().EnsureLocalPlayerId()).Id;
                var players = new WorldPlayersSave { Kind = (int)WorldKind.Multiplayer, Identity = (int)IdentityKind.Local };
                foreach (string id in new[] { hostPid, PlayModeHarness.PlayerId(21), absentA, absentB })
                    players.Recorded.Add(new WorldPlayerSave { Identity = (int)IdentityKind.Local, Id = id, Name = "p" + id.Substring(0, 2) });
                store.Current.Players = players;
                var weapons = new WeaponsSave();
                foreach (var (id, amount) in new[] { (hostPid, 10f), (PlayModeHarness.PlayerId(21), 15f), (absentA, 20f), (absentB, 45f) })
                {
                    var g = new PlayerGrowth(id); g.Weapon(Axe).AddAbs(amount);
                    GrowthSaves.Capture(g, id, weapons);
                    if (id == absentA) { seedA = g.Weapon(Axe).Abs; levelA = g.Weapon(Axe).Level; }
                    if (id == absentB) { seedB = g.Weapon(Axe).Abs; levelB = g.Weapon(Axe).Level; }
                    if (id == PlayModeHarness.PlayerId(21)) seedPresent = g.Weapon(Axe).Abs;
                }
                store.Current.Weapons = weapons;
                store.MarkDirty();
                Assert.That(store.Save(), Is.True, "the four-player seed world was written");
            }, 21, true);   // firstClientIndex 21: the one connecting client is recorded player 21
            Assert.That(rig.App.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "the session opened the seeded world");
            Assert.That(rig.App.World.Players.Recorded.Count, Is.EqualTo(4), "the world records four players");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(2), "only two are here");

            // Two present players may start and play.
            Assert.That(rig.App.World.TryStartSession(rig.App.Roster), Is.EqualTo(WorldPlayerVerdict.Ok), "two of the four recorded players can start");
            int scale = rig.App.World.ScalePlayers(rig.App.Roster);
            Assert.That(scale, Is.EqualTo(2), "difficulty scales to the two present players, not the four recorded");
            Assert.That(CoopScaling.ExtraEnemiesPerRoom(scale), Is.EqualTo(1));
            yield return StartAtBank(rig);
            Assert.That(rig.Haul.Session.Stir.PlayersPresent, Is.EqualTo(2), "the stir meter counts the present players");
            yield return Consistent(rig, "case6 two present, bank floor");

            // The present players' growth is restored from the save; they earn some; the absent two are never loaded.
            CombatHost combat = MakeCombat(rig);
            Assert.That(combat.Growth(rig.ClientId(0)).Weapon(Axe).Abs, Is.EqualTo(seedPresent), "the present client's saved growth was restored");
            combat.Growth(rig.ClientId(0)).Weapon(Axe).AddAbs(5f);
            float earned = combat.Growth(rig.ClientId(0)).Weapon(Axe).Abs;
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
            yield return ClientTo(rig, 0, BankPos(rig), "case6 walk to the bank");
            float askedAt6 = -10f;
            yield return PlayModeHarness.WaitUntil(() => AskBankUntil(rig, 0, ref askedAt6, () => rig.Haul.Session.Phase == DivePhase.BankRoom), Timeout, "case6: the present client could not open the bank room");
            Assert.That(rig.Haul.ServerBagOf(rig.ClientId(0)).BankOpen, Is.True, "case6: the present client has a bank visit (capacity follows who is present: the two absent players have none)");

            // Leave: the session saves; the absent two must be exactly as seeded.
            yield return PlayModeHarness.Await(rig.App.Services.Get<INetworkSession>().LeaveAsync(), Timeout);
            string text = File.ReadAllText(Path.Combine(_dir, worldId + ".json"));
            WorldSave file = JsonSaveFile<WorldSave>.DeserializeOrNull(text, w => w.SchemaVersion);
            Assert.That(file.Players.Recorded.Count, Is.EqualTo(4), "all four stay recorded");
            var ids = new List<string>(); foreach (WorldPlayerSave p in file.Players.Recorded) ids.Add(p.Id);
            Assert.That(ids, Is.EquivalentTo(new[] { hostPid, PlayModeHarness.PlayerId(21), absentA, absentB }), "the same four players");
            WeaponState a = GrowthSaves.Restore("a", absentA, file.Weapons).Weapon(Axe), bW = GrowthSaves.Restore("b", absentB, file.Weapons).Weapon(Axe);
            Assert.That((a.Level, a.Abs), Is.EqualTo((levelA, seedA)), "absent player A's growth is untouched");
            Assert.That((bW.Level, bW.Abs), Is.EqualTo((levelB, seedB)), "absent player B's growth is untouched");
            Assert.That(GrowthSaves.Restore("c", PlayModeHarness.PlayerId(21), file.Weapons).Weapon(Axe).Abs, Is.EqualTo(earned), "the present client's earned growth was saved");
        }
    }
}
