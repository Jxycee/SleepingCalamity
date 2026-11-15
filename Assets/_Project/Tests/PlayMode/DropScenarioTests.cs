// Authority: test-only. Audit 2026-11-12 "test gaps": disconnect scenarios the DisconnectRejoinTests cases do not reach (downed, mid-revive, mid floor change, and the HOST dropping as seen from a client).
// Host (no scenes) + real TestClients; the director, the cellar syncs and the heroes are spawned by the test, like DisconnectRejoinTests (whose helpers are copied here: that file belongs to another change).
//   D1 a DOWNED client drops (graceful and server-forced): the dive goes on (the ghost is not counted as a standing hero), then the last standing client drops while the host is down: the wipe fails the dive exactly once
//   D2 the REVIVER drops mid-hold (the hold is reported by the client's own HeroCombat through a FakeInputService, so the reports stop only because the client is gone): the host's hold lapses, the downed hero's countdown runs again
//   D3 the DOWNED client drops while the host is holding the revive on them: no exception, the host's reports find no target, the vitals list holds no stale entry
//   D4 a client leaves in the same frame the host takes the stair: one advance, no stale teleport, the same bag and a placement on the NEW floor when the same player rejoins
//   D5 the HOST drops while a client mid-dive holds the bank screen: the client ends Offline with EndReason.HostLeft, its objects are gone, and the BankScreen input context is handed back
// Catches: a dropped downed hero blocking or double-counting the party wipe, a revive hold that outlives its reviver (progress or a paused countdown stuck), a request or teleport for a client that just left raising an error,
// a rejoiner placed on the floor it left, a client that keeps the host's objects or a frozen input context after the host is gone, a host loss classified as anything but HostLeft.
// NOT covered: a real transport timeout (a client that vanishes without a disconnect message: the harness has only a graceful LeaveAsync and the server-forced HostNm.DisconnectClient, which run the same host callbacks), relay
// latency, scene-managed clients, the client UI after a host loss (the Lobby hand-off), the owner-side NetworkTransform settings.
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DropScenarioTests
    {
        private const float Timeout = 40f;
        private static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the clients StartClients created
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
            public DiveHaulSync Haul => Host.HaulSync;
            public List<NetworkManager> Remaining()
            {
                var l = new List<NetworkManager> { HostNm };
                for (int i = 0; i < Clients.Count; i++) if (!Dropped.Contains(i)) l.Add(Clients[i].NetworkManager);
                return l;
            }
        }

        private IEnumerator Start(System.Action<Rig> done, int clients, int firstClientIndex = 20)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new Rig { App = AppBootstrap.Current };
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

        private static NetworkObject HeroOnHost(Rig rig, ulong clientId)
        {
            foreach (NetworkObject o in rig.HostNm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == clientId) return o;
            return null;
        }

        private static bool IsListed(NetworkManager nm, ulong id) { foreach (ulong c in nm.ConnectedClientsIds) if (c == id) return true; return false; }

        private static bool HasHeroOf(NetworkManager nm, ulong clientId)
        {
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == clientId) return true;
            return false;
        }

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

        private IEnumerator StartDive(Rig rig, uint seed = 12345u)
        {
            rig.Host.SeedOverride = seed;
            Assert.That(rig.Host.ServerStartDive(), Is.True, "[dive] the host starts the dive");
            yield return FloorBuilt(rig, 1, "[dive F1]");
        }

        private IEnumerator StartAtBank(Rig rig)
        {
            rig.Host.SeedOverride = 12345u;
            rig.Host.TestHoldCheckpoint(true);
            Assert.That(rig.Host.ServerStartDive(UndercroftPlan.BankFloor), Is.True, "[dive F5] the host starts the dive at the bank");
            yield return FloorBuilt(rig, UndercroftPlan.BankFloor, "[dive F5]");
        }

        private static Vector3 BankPos(Rig rig)
        {
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out Vector3 bank), Is.True, "the floor has a bank point");
            return bank + new Vector3(0.5f, 0.1f, 0f);
        }

        // The client's hero (owner copy) walks to pos and the host's copy has arrived.
        private static IEnumerator ClientTo(Rig rig, int i, Vector3 pos, string step)
        {
            NetworkObject onHost = HeroOnHost(rig, rig.ClientId(i));
            Assert.That(onHost, Is.Not.Null, step + ": the host has client " + i + "'s hero");
            yield return PlayModeHarness.WaitUntil(() => { Move(rig.ClientHeroes[i], pos); return Flat(onHost.transform.position, pos) < 1.5f; }, Timeout, step + ": client hero never arrived on the host");
        }

        // Graceful leave, or (hard) the host throws the client out with no client-side leave path (the closest the harness gets to a vanished client).
        private IEnumerator Drop(Rig rig, int i, bool hard, string step)
        {
            ulong id = rig.ClientId(i);
            System.Threading.Tasks.Task leave = StartDrop(rig, i, hard);
            if (leave != null) yield return PlayModeHarness.Await(leave, Timeout);
            yield return AfterDrop(rig, id, step);
        }

        private static System.Threading.Tasks.Task StartDrop(Rig rig, int i, bool hard)
        {
            rig.Dropped.Add(i);
            if (hard) { rig.HostNm.DisconnectClient(rig.ClientId(i), "test: forced drop"); return null; }
            return rig.Clients[i].Session.LeaveAsync();
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

        // ================================================================ D1

        // Catches: a dropped DOWNED hero left in the wipe count (it would make the party look alive or dead wrongly), the dive ending when a downed teammate leaves while others stand, the wipe not firing when the
        // last standing player leaves with the others down (DiveDirector.WipeTick), a double end.
        [UnityTest]
        public IEnumerator D1_DownedClientDrops_DiveCarriesOn_ThenTheLastStandingLeaving_WipesTheDiveOnce([Values(false, true)] bool hard)
        {
            Rig rig = null;
            yield return Start(r => rig = r, 2);
            yield return StartDive(rig);
            ulong id0 = rig.ClientId(0);   // a client's LocalClientId resets to 0 once it has left: cache the ids first
            HeroVitals v0 = HeroOnHost(rig, id0).GetComponent<HeroVitals>();
            HeroVitals hostV = rig.HostHero.GetComponent<HeroVitals>();
            v0.ServerDamage(100000);
            Assert.That(v0.IsDowned, Is.True, "premise: client 0 is downed");
            yield return Seconds(0.5f);
            Assert.That(rig.Host.NetActive.Value, Is.True, "one hero down is not a wipe");

            yield return Drop(rig, 0, hard, "D1 downed leaver");
            yield return Seconds(1.5f);
            Assert.That(rig.Host.NetActive.Value, Is.True, "the downed leaver did not end the dive: host and client 1 still stand");
            Assert.That(rig.Ends, Is.EqualTo(0), "no end yet");
            Assert.That(HeroOnHost(rig, id0), Is.Null, "the leaver's hero is gone on the host");

            // Host down, client 1 the last one standing; it leaves: nobody is up, the wipe fails the dive once.
            hostV.ServerDamage(100000);
            Assert.That(hostV.IsDowned, Is.True);
            yield return Seconds(0.5f);
            Assert.That(rig.Host.NetActive.Value, Is.True, "client 1 still stands: no wipe");
            yield return Drop(rig, 1, hard, "D1 last standing");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value, Timeout, "D1: the last standing player leaving never failed the dive");
            yield return Seconds(1f);
            Assert.That(rig.Ends, Is.EqualTo(1), "the dive ended exactly once");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(1), "only the host is left");
        }

        // ================================================================ D2 / D3 revive

        private static void HoldInteract(NetworkObject ownerHero, FakeInputService input)
        {
            input.Frame = default;
            input.Frame.InteractHeld = true;
            ownerHero.GetComponent<HeroCombat>().Input = input;
        }

        // Catches: a revive hold that outlives its reviver. The hold is reported by the client's own HeroCombat (FakeInputService InteractHeld, real ReviveHoldServerRpc every 0.2 s), so the reports stop only because
        // the client left. The host must let the progress fall back to 0 and run the downed countdown again (a held revive pauses it: a stuck hold would freeze DownedLeft for good).
        [UnityTest]
        public IEnumerator D2_ReviverDropsMidHold_HostDropsTheHold_TheCountdownRuns_NoStuckProgress()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1);
            HeroVitals target = rig.HostHero.GetComponent<HeroVitals>();
            Move(rig.HostHero, Home);
            Move(rig.ClientHeroes[0], Home + new Vector3(1.5f, 0f, 0f));
            yield return Seconds(0.5f);
            target.ServerDamage(100000);
            Assert.That(target.IsDowned, Is.True, "premise: the host hero is downed");
            var input = new FakeInputService();
            HoldInteract(rig.ClientHeroes[0], input);
            Vector3 at = Home, near = Home + new Vector3(1.5f, 0f, 0f);   // no floor in this rig: the heroes are held in place every frame or gravity pulls them apart
            yield return PlayModeHarness.WaitUntil(() => { Move(rig.HostHero, at); Move(rig.ClientHeroes[0], near); return target.Model.ReviveProgress > 0.4f && !target.IsUp; }, Timeout, "D2: the client's hold never raised the revive progress (reports not arriving, or revived too early)");
            Assert.That(target.IsDowned, Is.True, "still downed: the revive is not finished");

            yield return Drop(rig, 0, true, "D2");   // server-forced and immediate, so the 3 s hold cannot finish first
            yield return PlayModeHarness.WaitUntil(() => target.Model.ReviveProgress == 0f, 3f, "D2: the revive progress stayed up after the reviver left (stuck hold)");
            Assert.That(target.IsDowned, Is.True, "no one revived the hero");
            float left = target.Model.DownedLeft;
            yield return Seconds(0.8f);
            Assert.That(target.Model.DownedLeft, Is.LessThan(left - 0.3f), "the downed countdown runs again: the dropped reviver's hold is gone");
            Assert.That(target.NetReviveProgress.Value, Is.EqualTo(0f), "and the replicated progress is 0");
        }

        // Catches: the host's own revive reports (real HeroCombat path) running against a hero that left: an exception or error log from the stale target, a stale HeroVitals entry, the revive state left half-done.
        [UnityTest]
        public IEnumerator D3_DownedClientDropsWhileTheHostIsReviving_NoErrors_NoStaleVitals_HostKeepsStanding()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1);
            ulong cid = rig.ClientId(0);
            NetworkObject copy = HeroOnHost(rig, cid);
            HeroVitals target = copy.GetComponent<HeroVitals>();
            Move(rig.HostHero, Home);
            Move(rig.ClientHeroes[0], Home + new Vector3(1.5f, 0f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(copy.transform.position, Home) < 3f, Timeout, "D3: the client hero never arrived on the host");
            target.ServerDamage(100000);
            Assert.That(target.IsDowned, Is.True, "premise: the client hero is downed");
            var input = new FakeInputService();
            HoldInteract(rig.HostHero, input);
            Vector3 at = Home, near = Home + new Vector3(1.5f, 0f, 0f);   // no floor in this rig: hold the heroes in place every frame
            yield return PlayModeHarness.WaitUntil(() => { Move(rig.HostHero, at); Move(rig.ClientHeroes[0], near); return target.Model.ReviveProgress > 0.4f; }, Timeout, "D3: the host's hold never raised the revive progress");

            yield return Drop(rig, 0, false, "D3");
            yield return Seconds(1f);   // the host keeps holding Interact against nothing: no error, no exception
            Assert.That(HeroVitals.All.Count, Is.EqualTo(1), "only the host's own hero is registered in this process (a dropped hero's vitals left in the list would be stale)");
            Assert.That(rig.HostHero.GetComponent<HeroVitals>().IsUp, Is.True, "the host still stands");
            input.Frame = default;
        }

        // ================================================================ D4

        // Catches: a stair taken in the same frame the client leaves raising an error (a TeleportRpc or placement for a client that is gone), the dive advancing twice or not at all, the leaver's placement record or bag lost,
        // and a rejoiner of the same PlayerId placed on the floor the party already left. Pinned: one advance, the bag (keyed by PlayerKey) returns, the rejoiner lands on the NEW floor's entry.
        [UnityTest]
        public IEnumerator D4_ClientLeavesInTheFrameTheHostTakesTheStair_OneAdvance_NoStaleTeleport_RejoinerLandsOnTheNewFloor()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1);
            yield return StartDive(rig);
            ulong hostId = rig.HostNm.LocalClientId, oldId = rig.ClientId(0);
            Assert.That(rig.App.Roster.TryGetKey(oldId, out PlayerKey key), Is.True);
            Assert.That(rig.Haul.ServerAddItem(oldId, 101), Is.True, "the leaver carries one item");
            PlayerBag bag = rig.Haul.ServerBagOf(oldId);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.TestPlacedHas(oldId) && rig.Host.TestPlacedHas(hostId), Timeout, "D4: the party was never placed on floor 1");

            rig.Host.ServerOpenStair(1);
            Vector3 stair = default;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (!CellarFloorBuilder.TryStairWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, out stair)) return false;
                Move(rig.HostHero, stair + new Vector3(1f, 0.1f, 0f));
                return Flat(rig.HostHero.transform.position, stair) < 3f;
            }, Timeout, "D4: the host hero never reached the stair");
            // Same frame: the party takes the stair and the client leaves.
            Assert.That(rig.Host.ServerTryStair(hostId, 1), Is.True, "the host's stair press is accepted");
            System.Threading.Tasks.Task leave = StartDrop(rig, 0, false);
            yield return PlayModeHarness.Await(leave, Timeout);
            yield return AfterDrop(rig, oldId, "D4");
            yield return FloorBuilt(rig, 2, "[D4 F2]");
            yield return Seconds(1.5f);
            Assert.That(rig.Haul.Current.CurrentFloor, Is.EqualTo(2), "exactly one advance");
            Assert.That(rig.Host.TestPlacedHas(oldId), Is.False, "the leaver's placement record is pruned, nothing is sent to them");
            Assert.That(bag.Carried, Is.EqualTo(new[] { 101 }), "the leaver's bag keeps its item for the rejoin");

            // The same player comes back on the new floor.
            yield return PlayModeHarness.StartClients(rig.App, 1, false, rig.Clients, 20);
            int back = rig.Clients.Count - 1;
            ulong newId = rig.ClientId(back);
            Assert.That(newId, Is.Not.EqualTo(oldId), "a new client id");
            Assert.That(rig.App.Roster.TryFindConnected(key, out ulong found) && found == newId, Is.True, "the roster maps the key to the new client id");
            Assert.That(rig.Haul.ServerBagOf(newId), Is.SameAs(bag), "the very same bag");
            rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, newId, false, true, false, Home + new Vector3(2f, 0f, 0f), Quaternion.identity);
            NetworkManager cnm = rig.ClientNm(back);
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null, Timeout, "D4: the rejoiner never received a hero");
            yield return FloorBuilt(rig, 2, "[D4 rejoin]");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.TestPlacedThisGen(newId), Timeout, "D4: the rejoiner was never placed on the current floor");
            NetworkObject onHost = HeroOnHost(rig, newId);
            Vector3 entry = CellarFloorBuilder.EntryWorld(rig.Host.FloorSync.Layout, rig.Host.FloorSync.Built, 0);
            yield return PlayModeHarness.WaitUntil(() => Flat(onHost.transform.position, entry) < 8f, Timeout, "D4: the rejoiner is not at floor 2's entry (at " + onHost.transform.position + ", entry " + entry + ")");
            yield return ReplicatedStateHash.AssertAllEqual(rig.Remaining(), "D4 after the rejoin");
        }

        // ================================================================ D5

        // Catches: the host leaving while a client holds the bank screen: the client never learns it (no Ended / wrong EndReason), keeps host objects, or stays frozen in InputContext.BankScreen (the hero cannot move in the next
        // session). The input service is one object in this process, but only the client's director ever held the context here (the host hero is out of the bank's range), so only the client's OnNetworkDespawn can hand it back.
        // NOT covered beyond the header: the Lobby hand-off and the toast the real client UI shows.
        [UnityTest]
        public IEnumerator D5_HostLeavesWhileAClientHoldsTheBankScreen_ClientEndsOffline_HostLeft_ContextReleased()
        {
            Rig rig = null;
            yield return Start(r => rig = r, 1);
            var input = new FakeInputService();
            ServiceRoots.App.Replace<IInputService>(input);
            yield return StartAtBank(rig);
            Move(rig.HostHero, Home);   // far from the bank: the host's own director never holds the screen
            Assert.That(rig.Haul.ServerAddPiece(PieceCatalog.FrameId), Is.True);
            yield return ClientTo(rig, 0, BankPos(rig), "D5 walk to the bank");
            Assert.That(rig.Haul.ServerEnterBankRoom(), Is.True, "the bank room is open");
            yield return PlayModeHarness.WaitUntil(() => { Move(rig.ClientHeroes[0], BankPos(rig)); return input.GameplayContext == InputContext.BankScreen; }, Timeout,
                "D5: the client's director never held InputContext.BankScreen (context " + input.GameplayContext + "), so the test would prove nothing");

            var ended = new List<EndReason>();
            PlayModeHarness.TestClient client = rig.Clients[0];
            client.Session.Ended += ended.Add;
            System.Threading.Tasks.Task hostLeave = rig.App.Services.Get<INetworkSession>().LeaveAsync();
            yield return PlayModeHarness.Await(hostLeave, Timeout);
            yield return PlayModeHarness.WaitUntil(() => ended.Count > 0, Timeout, "D5: the client never reported that its session ended after the host left");
            Assert.That(ended, Is.EqualTo(new[] { EndReason.HostLeft }), "the host leaving is classified as HostLeft, once");
            Assert.That(client.Session.State, Is.EqualTo(ConnState.Offline), "the client session is Offline");
            Assert.That(client.NetworkManager.IsListening, Is.False, "the client's NetworkManager stopped");
            Assert.That(client.NetworkManager.SpawnManager == null || client.NetworkManager.SpawnManager.SpawnedObjects.Count == 0, Is.True, "no host objects left on the client");
            Assert.That(input.GameplayContext, Is.EqualTo(InputContext.Free), "the bank screen's input context was handed back");
            rig.Dropped.Add(0);
        }
    }
}
