// Authority: test-only. M6 S4: the host-authoritative town (TownSync) over real NGO peers in one process (host + clients, scene management off; the director and its TownSync are spawned from the
// owned network prefab list). Requests go through the same Request* wrappers the placement UI calls, so they take the real [Rpc(SendTo.Server)] path with the sender taken from RpcParams.
// Catches: a client placement that host and client disagree on, a host that does not re-validate (path-blocking frame accepted), a request from a peer without the lock applied, the lock granted
// twice, not freed by release or by the holder's disconnect, a late joiner that sees an empty town (state replayed by RPC instead of living in the NetworkVariable), a ghost pose that does not
// reach the host and watchers, leaving placement mode not asking the world to save (d) or not WRITING the town to the world file (e), the resident and pick-up RPCs.
// NOT covered by a one-process test: the placement KEYS, camera and wish panel on screen (PlacementMode reads the scene and local input; the router row is an EditMode test), the real
// Hollowmere scene on a client that loads it (a declined-scene peer has no scene), relay latency.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class TownSyncNetworkTests
    {
        private const float Timeout = 25f;
        private const int Frame = 100, Oven = 101, Door = 200, Sign = 201;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();
        private string _dir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public DiveDirector Director; public TownSync Host;
        }

        private static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private IEnumerator Start(System.Action<Rig> done, params int[] stock)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new Rig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Director = dirObj.GetComponent<DiveDirector>();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.TownSync != null && rig.Director.TownSync.IsSpawned, Timeout, "the director never spawned its TownSync");
            rig.Host = rig.Director.TownSync;
            foreach (int p in stock) Assert.That(rig.Director.Town.State.AddPiece(p), Is.EqualTo(PlaceResult.Ok));
            rig.Director.Town.State.AddResident(TownBuildings.BakerResident);
            rig.Host.ServerRefresh();
            done(rig);
        }

        private IEnumerator Join(Rig rig, int index, System.Action<PlayModeHarness.TestClient, TownSync> done)
        {
            var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8 + index));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client " + index + " never connected");
            ulong id = rig.Host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client " + index + " never received the TownSync");
            done(client, client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<TownSync>());
        }

        private static IEnumerator Take(TownSync who, TownSync host)
        {
            who.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => who.IsBuilder && host.NetBuilder.Value == who.NetworkManager.LocalClientId, Timeout, "the lock was never granted");
        }

        private static string Sig(TownState s) => TownWire.From(s).Signature();

        // (a) A client takes the lock, places the frame and a part; host and client agree. A pick-up returns it. The host answers the sender with the result.
        [UnityTest]
        public IEnumerator Client_TakesTheLock_PlacesTheBakery_HostAndClientTownsAgree()
        {
            Rig rig = null; TownSync peer = null;
            yield return Start(r => rig = r, Frame, Oven, Door, Sign);
            yield return Join(rig, 0, (c, t) => peer = t);
            yield return PlayModeHarness.WaitUntil(() => peer.View.InStock(Frame), Timeout, "the client never saw the stock");
            Assert.That(peer.BuilderIsFree, Is.True);

            var results = new List<(TownSync.Op, byte)>();
            peer.ResultReceived += (op, res) => results.Add((op, res));
            yield return Take(peer, rig.Host);

            peer.SendGhost("bakery", "F02", 0, true);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetGhost.Value.Active && rig.Host.NetGhost.Value.Foundation.ToString() == "F02", Timeout, "the ghost never reached the host");
            Assert.That(rig.Host.NetGhost.Value.Building.ToString(), Is.EqualTo("bakery"));

            peer.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Town.State.PlacedBuildingOf("bakery") != null, Timeout, "the host never placed the frame");
            yield return PlayModeHarness.WaitUntil(() => peer.View.PlacedBuildingOf("bakery") != null, Timeout, "the client never saw the frame");
            Assert.That(rig.Host.NetGhost.Value.Active, Is.False, "the ghost clears once the frame stands");
            peer.RequestPlacePart("bakery", "oven");
            yield return PlayModeHarness.WaitUntil(() => peer.View.PlacedBuildingOf("bakery").PiecesByPart[1] >= 0, Timeout, "the client never saw the oven");

            Assert.That(Sig(peer.View), Is.EqualTo(Sig(rig.Director.Town.State)), "host and client towns are identical");
            Assert.That(Sig(rig.Host.View), Is.EqualTo(Sig(rig.Director.Town.State)), "the host reads the same replicated shape");
            Assert.That(results.Count, Is.GreaterThanOrEqualTo(3), "lock, frame and part were all answered");
            Assert.That(results.TrueForAll(x => x.Item2 == (byte)PlaceResult.Ok), Is.True);

            peer.RequestPickUpPart("bakery", "oven");
            yield return PlayModeHarness.WaitUntil(() => peer.View.InStock(Oven) && peer.View.PlacedBuildingOf("bakery").PiecesByPart[1] < 0, Timeout, "the pick-up never came back to stock");
            Assert.That(Sig(peer.View), Is.EqualTo(Sig(rig.Director.Town.State)));

            // The baker: oven, door and sign slotted, then he moves in (resident RPCs), and moves out again.
            peer.RequestPlacePart("bakery", "oven"); peer.RequestPlacePart("bakery", "door"); peer.RequestPlacePart("bakery", "sign");
            yield return PlayModeHarness.WaitUntil(() => peer.View.IsComplete("bakery"), Timeout, "the bakery never completed on the client");
            peer.RequestPlaceResident("bakery");
            yield return PlayModeHarness.WaitUntil(() => peer.View.PlacedBuildingOf("bakery").Resident == "baker" && !peer.View.ResidentInStock("baker"), Timeout, "the baker never moved in");
            Assert.That(rig.Director.Town.State.PlacedBuildingOf("bakery").Resident, Is.EqualTo("baker"), "on the host too");
            peer.RequestPickUpResident("bakery");
            yield return PlayModeHarness.WaitUntil(() => peer.View.ResidentInStock("baker") && peer.View.PlacedBuildingOf("bakery").Resident == null, Timeout, "the baker never moved out");
            Assert.That(Sig(peer.View), Is.EqualTo(Sig(rig.Director.Town.State)));
            peer.RequestPickUpBuilding("bakery");
            yield return PlayModeHarness.WaitUntil(() => peer.View.PlacedBuildingOf("bakery") == null && peer.View.InStock(Frame), Timeout, "the building never came back to stock");
            Assert.That(rig.Director.Town.State.Conservation(), Is.EqualTo(peer.View.Conservation()), "nothing was lost or made");
        }

        // (b) The host re-validates: a path-blocking frame is refused (S1's F02 + turn 2), and a peer without the lock is refused.
        [UnityTest]
        public IEnumerator Host_RefusesAPathBlockingFrame_AndARequestFromANonHolder()
        {
            Rig rig = null; TownSync peer = null;
            yield return Start(r => rig = r, Frame, Oven, Door, Sign);
            yield return Join(rig, 0, (c, t) => peer = t);
            var clientResults = new List<byte>(); var hostResults = new List<byte>();
            peer.ResultReceived += (op, res) => { if (op == TownSync.Op.PlaceFrame) clientResults.Add(res); };
            rig.Host.ResultReceived += (op, res) => { if (op == TownSync.Op.PlaceFrame) hostResults.Add(res); };

            yield return Take(peer, rig.Host);
            peer.RequestPlaceFrame("bakery", "F02", 2);
            yield return PlayModeHarness.WaitUntil(() => clientResults.Count == 1, Timeout, "no answer to the blocked frame");
            Assert.That(clientResults[0], Is.EqualTo((byte)PlaceResult.PathBlocked));
            Assert.That(rig.Director.Town.State.PlacedBuildingOf("bakery"), Is.Null, "nothing was placed");
            Assert.That(rig.Director.Town.State.InStock(Frame), Is.True, "the frame stayed in stock");
            Assert.That(peer.View.PlacedBuildingOf("bakery"), Is.Null);

            // The host peer does not hold the lock: its own request is refused.
            rig.Host.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => hostResults.Count == 1, Timeout, "no answer to the non-holder");
            Assert.That(hostResults[0], Is.EqualTo(TownSync.NotBuilder));
            Assert.That(rig.Director.Town.State.PlacedBuildingOf("bakery"), Is.Null);

            // A bad frame request with garbage ids is refused, not thrown.
            peer.RequestPlaceFrame("castle", new string('x', 200), 1);
            yield return PlayModeHarness.WaitUntil(() => clientResults.Count == 2, Timeout, "no answer to the garbage request");
            Assert.That(clientResults[1], Is.Not.EqualTo((byte)PlaceResult.Ok));

            // The good turn is accepted.
            peer.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Town.State.PlacedBuildingOf("bakery") != null, Timeout, "the legal frame was refused");
        }

        // (c) A late joiner gets the placed town (and the lock and ghost) with the spawn, no request replayed.
        [UnityTest]
        public IEnumerator LateJoiner_SeesThePlacedTown_TheLock_AndTheGhost()
        {
            Rig rig = null; TownSync first = null, late = null;
            yield return Start(r => rig = r, Frame, Oven, Door, Sign);
            yield return Join(rig, 0, (c, t) => first = t);
            yield return Take(first, rig.Host);
            first.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => first.View.PlacedBuildingOf("bakery") != null, Timeout, "the frame never landed");
            first.RequestPlacePart("bakery", "door");
            yield return PlayModeHarness.WaitUntil(() => first.View.PlacedBuildingOf("bakery").PiecesByPart[2] >= 0, Timeout, "the door never landed");

            yield return Join(rig, 1, (c, t) => late = t);
            yield return PlayModeHarness.WaitUntil(() => late.View.PlacedBuildingOf("bakery") != null, Timeout, "the late joiner never saw the bakery");
            Assert.That(Sig(late.View), Is.EqualTo(Sig(rig.Director.Town.State)), "the late joiner's town equals the host's");
            Assert.That(late.View.PlacedBuildingOf("bakery").PiecesByPart[2], Is.GreaterThanOrEqualTo(0), "with the door slotted");
            Assert.That(late.BuilderIsFree, Is.False, "the late joiner sees the lock held");
            Assert.That(late.IsBuilder, Is.False);
            Assert.That(late.NetBuilder.Value, Is.EqualTo(first.NetworkManager.LocalClientId));
        }

        // (d) The lock passes on release, a second asker is refused while it is held, the holder's disconnect frees it, and leaving placement mode asks the world to save.
        [UnityTest]
        public IEnumerator Lock_PassesOnRelease_AndFreesWhenTheHolderDisconnects_ReleaseAsksForASave()
        {
            Rig rig = null; TownSync a = null, b = null; PlayModeHarness.TestClient clientA = null;
            yield return Start(r => rig = r, Frame, Oven, Door, Sign);
            yield return Join(rig, 0, (c, t) => { clientA = c; a = t; });
            yield return Join(rig, 1, (c, t) => b = t);
            int saves = 0;
            HostWorld world = null;
            bool haveWorld = ServiceRoots.App != null && ServiceRoots.App.TryGet(out world);
            Assert.That(haveWorld, Is.True, "the app has its HostWorld");
            world.SaveRequested += () => saves++;

            yield return Take(a, rig.Host);
            var bResults = new List<byte>();
            b.ResultReceived += (op, res) => { if (op == TownSync.Op.Lock) bResults.Add(res); };
            b.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => bResults.Count == 1, Timeout, "no answer to the second asker");
            Assert.That(bResults[0], Is.EqualTo(TownSync.NotBuilder), "the lock is held: one builder at a time");
            Assert.That(rig.Host.NetBuilder.Value, Is.EqualTo(a.NetworkManager.LocalClientId));
            b.ReleaseBuildLock();   // a non-holder cannot release it
            yield return Wait(0.5f);
            Assert.That(rig.Host.NetBuilder.Value, Is.EqualTo(a.NetworkManager.LocalClientId), "a non-holder's release changed nothing");

            int before = saves;
            a.ReleaseBuildLock();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BuilderIsFree && b.BuilderIsFree, Timeout, "the release never freed the lock");
            Assert.That(saves, Is.GreaterThan(before), "leaving placement mode asked the world to save");

            yield return Take(b, rig.Host);
            Assert.That(a.IsBuilder, Is.False);
            b.SendGhost("bakery", "F03", 1, true);
            yield return PlayModeHarness.WaitUntil(() => a.NetGhost.Value.Active && a.NetGhost.Value.Foundation.ToString() == "F03", Timeout, "a watcher never saw the ghost");

            // Now the other way: a holder who drops frees the lock (and clears the ghost).
            yield return clientA.LeaveAndDestroy();
            _clients.Remove(clientA);
            Assert.That(rig.Host.NetBuilder.Value, Is.EqualTo(b.NetworkManager.LocalClientId));
            PlayModeHarness.TestClient holder = null; TownSync c3 = null;
            yield return Join(rig, 2, (c, t) => { holder = c; c3 = t; });
            b.ReleaseBuildLock();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BuilderIsFree, Timeout, "b never released");
            yield return Take(c3, rig.Host);
            yield return holder.LeaveAndDestroy();
            _clients.Remove(holder);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BuilderIsFree && !rig.Host.NetGhost.Value.Active, Timeout, "the lock stayed held by a client that left");
        }

        // Review fixes (M6 S4): the dive's go-home stocks the town outside any request; the 0.2 s poll republishes it to clients. (The real EndDive needs a generated floor; this adds the piece to the host's
        // TownState exactly as Town.Receive does, WITHOUT ServerRefresh.)
        [UnityTest]
        public IEnumerator GoHomeStock_ReachesTheClient_ThroughThePoll()
        {
            Rig rig = null; TownSync peer = null;
            yield return Start(r => rig = r, Frame);
            yield return Join(rig, 0, (c, t) => peer = t);
            Assert.That(peer.View.InStock(Oven), Is.False);
            Assert.That(rig.Director.Town.State.AddPiece(Oven), Is.EqualTo(PlaceResult.Ok));
            yield return PlayModeHarness.WaitUntil(() => peer.View.InStock(Oven), Timeout, "the client never saw the piece the host stocked outside a request");
        }

        // A dive starting frees the builder lock whoever holds it (a guest cannot block a dive). PlacementMode itself reads local input and the scene, so it is not driven here: its reaction (not the holder
        // any more -> Leave) is the existing Active && !IsBuilder branch.
        [UnityTest]
        public IEnumerator StartingADive_FreesTheBuilderLock()
        {
            Rig rig = null; TownSync peer = null;
            yield return Start(r => rig = r, Frame);
            yield return Join(rig, 0, (c, t) => peer = t);
            yield return Take(peer, rig.Host);
            rig.Director.ServerStartDive();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BuilderIsFree && !peer.IsBuilder, Timeout, "the dive left the lock with the client");
        }

        // A grant that arrives after the client gave up waiting: the peer holds the lock but its PlacementMode is not Active and not asking. It must hand the lock back.
        [UnityTest]
        public IEnumerator LateGrant_WithoutPlacementMode_IsReleased()
        {
            Rig rig = null;
            yield return Start(r => rig = r, Frame);
            var go = new GameObject("PlacementModeLeakTest");
            go.AddComponent<PlacementMode>();   // TownSync.Local on the host peer is the host's sync
            try
            {
                bool granted = false;
                rig.Host.ResultReceived += (op, res) => { if (op == TownSync.Op.Lock && res == (byte)PlaceResult.Ok) granted = true; };
                rig.Host.RequestBuildLock();   // the host peer is granted while its (inactive, not-waiting) mode looks on
                yield return PlayModeHarness.WaitUntil(() => granted, Timeout, "no grant");
                yield return PlayModeHarness.WaitUntil(() => rig.Host.BuilderIsFree, Timeout, "the idle holder never released the lock");
            }
            finally { Object.Destroy(go); }
        }

        // (e) Solo: the host builds (its own request, sender = the host's client id) and leaving placement mode puts the bakery on DISK at once (not on the 10 s flush tick).
        [UnityTest]
        public IEnumerator Solo_LeavingPlacementMode_WritesTheTownToTheWorldFile()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.TownSync != null && DiveDirector.Instance.TownSync.IsSpawned, 40f, "the hub never spawned the director and its TownSync");
            DiveDirector director = DiveDirector.Instance;
            TownSync sync = director.TownSync;
            foreach (int p in new[] { Frame, Oven, Door, Sign }) director.Town.State.AddPiece(p);
            sync.ServerRefresh();

            // The scene's real PlacementMode hands back a lock it is not using (the late-grant fix); this test drives the requests itself.
            foreach (PlacementMode mode in Object.FindObjectsByType<PlacementMode>(FindObjectsInactive.Include)) mode.enabled = false;
            yield return Take(sync, sync);
            sync.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => director.Town.State.PlacedBuildingOf("bakery") != null, Timeout, "the host never placed its own frame");
            string worldId = app.WorldSaveFlow.SelectedWorldId;
            Assert.That(worldId, Is.Not.Null);
            sync.ReleaseBuildLock();   // leaving placement mode
            yield return PlayModeHarness.WaitUntil(() => sync.BuilderIsFree, Timeout, "the lock never freed");

            WorldSave saved = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), w => w.SchemaVersion);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.Town, Is.Not.Null, "the town section was written on release");
            Assert.That(saved.Town.Buildings.Count, Is.EqualTo(1));
            Assert.That(saved.Town.Buildings[0].BuildingId, Is.EqualTo("bakery"));
            Assert.That(saved.Town.Buildings[0].FoundationId, Is.EqualTo("F02"));
        }

        private static IEnumerator Wait(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }
    }
}
