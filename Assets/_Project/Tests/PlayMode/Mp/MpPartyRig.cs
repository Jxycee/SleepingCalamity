// Authority: test-only. Shared rig for the multiplayer-readiness tests (QUEUE.md "multiplayer readiness (M7)", triage Q1 + Q6): one real host App plus 1-3 in-process TestClients with scene
// management off (the StateHashSmokeTests / StairHandoverTests boot, copied rather than shared because those files hold private rigs), an optional NetworkSimulator condition, a DiveDirector
// spawned and configured by the test, one hero per peer placed around the hub stair, and a log tap that keeps every error / exception and every Netcode warning seen after boot.
// MPPM (com.unity.multiplayer.playmode 3.0.0) is not used: its virtual players are extra editor processes started from the editor window, and the wrapper runs a single -batchmode -nographics
// editor, so extra peers here are in-process NetworkManagers like every other PlayMode network test.
// NOT covered (same limits as the rig it copies): scene sync to a real second machine, a real relay, owner-side NGO settings, transforms and enemies in the state hash, visuals.
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class MpPartyRig
    {
        public const float Timeout = 40f;
        public static readonly Vector3 Home = new Vector3(0f, 0f, -6f), HubStair = new Vector3(0f, 0f, 9f), Origin = new Vector3(200f, 0f, 0f);

        public AppBootstrap App;
        public NetworkManager HostNm;
        public NetCondition Net;
        public readonly List<PlayModeHarness.TestClient> Clients = new List<PlayModeHarness.TestClient>();
        public readonly List<NetworkManager> Peers = new List<NetworkManager>();           // host first, then the clients in join order
        public readonly List<NetworkObject> HeroesOnHost = new List<NetworkObject>();       // index i = peer i's hero as the host holds it (what the host-side range checks read)
        public readonly List<NetworkObject> OwnCopies = new List<NetworkObject>();          // index i = peer i's own copy of its hero (the owner side: what a teleport moves)
        public readonly List<DiveDirector> Directors = new List<DiveDirector>();           // index i = peer i's copy of the director (0 = the host's)
        public DiveDirector Host;
        public int Players => Peers.Count;

        // ---- log tap (hooked after boot, so the boot's own noise is not counted) ----
        public readonly List<string> Errors = new List<string>(), Warnings = new List<string>();
        Application.LogCallback _tap;

        // Boots host + `clients` TestClients under `net` (the simulator goes in BEFORE the host starts), spawns the director and one hero per peer, and hands the rig to `done` as soon as it exists,
        // so a failing boot still reaches the test's teardown. Returns with every client holding its hero and the director and the log tap hooked.
        public static IEnumerator Boot(int clients, NetCondition net, Action<MpPartyRig> done)
        {
            var rig = new MpPartyRig { Net = net };
            done(rig);
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            PlayModeHarness.AddNetworkSimulator(net);
            rig.App = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            yield return PlayModeHarness.StartClients(rig.App, clients, false, rig.Clients);   // disposed by PlayModeHarness.TearDownApp
            rig.Peers.Add(rig.HostNm);
            foreach (PlayModeHarness.TestClient c in rig.Clients) rig.Peers.Add(c.NetworkManager);

            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(Home, HubStair, Origin), Is.True, "[boot] ServerConfigure");
            // heroes stand around the hub stair (inside the host's accept radius), so a client's real trapdoor press is accepted
            for (int i = 0; i < rig.Peers.Count; i++)
            {
                Vector3 at = HubStair + new Vector3(-3f + 2f * i, 0f, -1f);
                rig.HeroesOnHost.Add(rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.Peers[i].LocalClientId, false, true, false, at, Quaternion.identity));
            }
            rig.OwnCopies.Add(rig.HeroesOnHost[0]);
            rig.Directors.Add(rig.Host);
            for (int i = 1; i < rig.Peers.Count; i++)
            {
                NetworkManager cnm = rig.Peers[i];
                yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && cnm.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "[boot] client " + i + " never received its hero and the director");
                rig.OwnCopies.Add(cnm.LocalClient.PlayerObject);
                rig.Directors.Add(cnm.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>());
            }
            yield return Seconds(0.5f);
            rig._tap = (msg, stack, type) =>
            {
                if (type == LogType.Log) return;
                string first = msg.Split('\n')[0];
                if (first.Length > 300) first = first.Substring(0, 300);
                string line = type + ": " + first;
                if (type == LogType.Warning) rig.Warnings.Add(line); else rig.Errors.Add(line);
            };
            Application.logMessageReceived += rig._tap;
        }

        public void Dispose()
        {
            if (_tap != null) { Application.logMessageReceived -= _tap; _tap = null; }
        }

        // The run was clean: no error or exception, no Netcode / Network warning since the party was ready.
        public void AssertCleanLog(string step)
        {
            Assert.That(Errors, Is.Empty, "[" + step + "] no error or exception in the log:\n" + string.Join("\n", Errors));
            string[] net = Warnings.FindAll(w => w.Contains("Netcode") || w.Contains("Network")).ToArray();
            Assert.That(net, Is.Empty, "[" + step + "] no Netcode warning in the log:\n" + string.Join("\n", net));
        }

        // Every peer's replicated state equals the host's (ReplicatedStateHash.AssertAllEqual, paced and bounded; no hash mismatch).
        public IEnumerator AssertAgree(string step, float timeoutSeconds = 40f) { yield return ReplicatedStateHash.AssertAllEqual(Peers, step, timeoutSeconds); }

        // ---------------------------------------------------------------------------------------------------------------- waiting
        public static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        public static NetworkObject Prefab<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        public static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

        // Moves a hero copy (CharacterController off while the transform is set).
        public static void Move(NetworkObject o, Vector3 pos)
        {
            var cc = o.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            o.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // ---------------------------------------------------------------------------------------------------------------- dive helpers
        // Every peer built `floor` (and the host spawned the dive's syncs).
        public IEnumerator FloorBuiltEverywhere(int floor, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => Host.FloorSync != null && Host.HaulSync != null && Host.ContentsSync != null, Timeout, step + ": the dive never spawned its syncs");
            foreach (NetworkManager nm in Peers)
            {
                NetworkManager peer = nm;
                yield return PlayModeHarness.WaitUntil(() =>
                {
                    ulong id = Host.FloorSync.NetworkObjectId;
                    return peer.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject o) && o != null && o.GetComponent<CellarFloorSync>().BuiltFloor == floor && o.GetComponent<CellarFloorSync>().Built != null;
                }, Timeout, step + ": peer " + peer.LocalClientId + " never built floor " + floor);
            }
            yield return PlayModeHarness.WaitUntil(() => Host.ContentsSync.State.Floor == floor, Timeout, step + ": the contents were never armed for floor " + floor);
        }

        // Every hero (the host's copy and the owner's own copy) stands within 6 m of its entry slot on the built floor: the arrival teleports have landed.
        public IEnumerator AllAtEntry(string step)
        {
            yield return PlayModeHarness.WaitUntil(() =>
            {
                CellarFloorSync fs = Host.FloorSync;
                if (fs.Built == null) return false;
                int slot = 0;
                foreach (ulong id in HostNm.ConnectedClientsIds)
                {
                    int i = Peers.FindIndex(p => p.LocalClientId == id);
                    Vector3 e = CellarFloorBuilder.EntryWorld(fs.Layout, fs.Built, slot++);
                    if (Flat(HeroesOnHost[i].transform.position, e) >= 6f || Flat(OwnCopies[i].transform.position, e) >= 6f) return false;
                }
                return true;
            }, Timeout, step + ": the party never arrived at the floor's entry (" + Where() + ")");
            yield return Seconds(1f);   // late teleports must have landed before the test moves a hero
        }

        // Every hero (host copy and own copy) is back around the hub's home point and the dive is over on every peer.
        public IEnumerator AllHome(string step)
        {
            yield return PlayModeHarness.WaitUntil(() =>
            {
                foreach (DiveDirector d in Directors) if (d.NetActive.Value) return false;
                for (int i = 0; i < Peers.Count; i++)
                    if (Flat(HeroesOnHost[i].transform.position, Home) > SpawnPoint.SlotRadius + 3f || Flat(OwnCopies[i].transform.position, Home) > SpawnPoint.SlotRadius + 3f) return false;
                return true;
            }, Timeout, step + ": the party was never back at the hub with the dive over (" + Where() + ")");
        }

        string Where()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Peers.Count; i++) sb.Append("hero ").Append(i).Append(" host copy ").Append(HeroesOnHost[i].transform.position).Append(" own ").Append(OwnCopies[i].transform.position).Append("; ");
            return sb.ToString();
        }

        // Moves the last peer's own hero next to the floor's stair and waits until the host's copy of it is within `reach` flat metres of the stair cell (owner-authoritative: the move takes a trip).
        public IEnumerator PeerToStair(int peer, string step)
        {
            Assert.That(CellarFloorBuilder.TryStairWorld(Host.FloorSync.Layout, Host.FloorSync.Built, out Vector3 stair), Is.True, step + ": the floor has a stair");
            Move(OwnCopies[peer], stair + new Vector3(1f, 0.1f, 0f));
            yield return PlayModeHarness.WaitUntil(() => Flat(HeroesOnHost[peer].transform.position, stair) < 3f, Timeout, step + ": the hero never reached the stair on the host (host copy " + HeroesOnHost[peer].transform.position + ", stair " + stair + ")");
        }

        // Real client trapdoor press (RequestDescend RPC), retried every 2 s while the host has not started the dive (a refusal for range is not retried by the game; a lost packet is retransmitted by the transport).
        public IEnumerator DescendFrom(int peer, string step)
        {
            float asked = -10f;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (Host.NetActive.Value) return true;
                if (Time.realtimeSinceStartup - asked >= 2f) { asked = Time.realtimeSinceStartup; Directors[peer].RequestDescend(); }
                return false;
            }, Timeout, step + ": the trapdoor press never started the dive");
        }

        // Real client stair press on `from`, retried every 2 s until the host's haul is on floor from + 1 (idempotent on the host: it advances only while the floor equals `from`).
        public IEnumerator StairFrom(int peer, int from, string step)
        {
            float asked = -10f;
            yield return PlayModeHarness.WaitUntil(() =>
            {
                if (Host.HaulSync.Current.CurrentFloor == from + 1) return true;
                if (Time.realtimeSinceStartup - asked >= 2f) { asked = Time.realtimeSinceStartup; Directors[peer].RequestStair(from); }
                return false;
            }, Timeout, step + ": the stair press never took the party to floor " + (from + 1));
        }
    }
}
