// Authority: test-only. Plan 5 task 4: the two new floor objectives live in the dive. The host (CellarContentsSync) alone decides valves turned and the pump's progress and writes them into the
// replicated ContentsState; a client only asks (ValveHoldRpc / StartPumpRpc) and sees the result. Host + one client in one process (scene management off); the heroes are simulated through
// CellarContentsSync.PlayersOverride (no hero objects), so every reach rule is judged on the positions the test sets. Seeds are found with the Core FloorObjectivePlan, never hard-coded.
// Catches: a valve turned by a hold that was interrupted, out of reach or from the wrong floor; the two valves not turning in either order or not unlocking the exit; the exit opening twice; a pump that
// starts out of reach, never fills, fills with nobody in the ring, never unlocks the exit or lets more than GameMetrics.MaxLiveEnemies enemies live; a client (or a late joiner) whose valve bits, pump percent or
// markers differ from the host's; peers computing a different plan. The valve-floor carrier and the floor-7 finish gate are in DiveSpineNetworkTests (they need the director).
// NOT covered: the owner's held-input sampling (LocalInteract reads the real input; these tests send the same RPCs it sends), marker visuals, hero deaths to pump waves (no hero objects here).
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
    public class FloorObjectiveNetworkTests
    {
        private const float Timeout = 25f;
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
            CellarContentsSync.PumpSecondsOverrideForTests = 0f;
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

        // First seed whose floor `floor` really gets `objective` (not a layout fallback) and whose first hazard slot is empty (a hazard changes enemy counts and gold: FloorModifierNetworkTests).
        private static uint FindSeed(FloorObjective objective, int floor, bool needCarrier = false)
        {
            for (uint s = 1; s < 4000; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                if (needCarrier && l.Carrier == null) continue;
                if (FloorObjectivePlan.For(l).Objective == objective && FloorModifierSet.For(s, floor).A == FloorModifier.None) return s;
            }
            Assert.Fail("no seed with objective " + objective + " on floor " + floor); return 0;
        }

        private IEnumerator Setup(System.Action<Ctx> done, uint seed, int floor)
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

            c.Floor = Spawn<CellarFloorSync>(c.HostNm); c.Haul = Spawn<DiveHaulSync>(c.HostNm); c.Contents = Spawn<CellarContentsSync>(c.HostNm);
            ulong id = c.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client never received the contents object");
            c.Peer = client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();
            PinPeer(c.Peer, client);
            c.Contents.FloorSync = c.Floor; c.Contents.HaulSync = c.Haul; c.Haul.FloorSync = c.Floor;
            Assert.That(c.Peer.FloorSync, Is.Not.Null, "the client has its own floor sync");

            c.HostId = c.HostNm.LocalClientId; c.ClientId = client.NetworkManager.LocalClientId;
            c.Contents.PlayersOverride = list => { foreach (KeyValuePair<ulong, Vector3> kv in c.Players) list.Add(new CellarContentsSync.PlayerPos { ClientId = kv.Key, Position = kv.Value }); };

            Assert.That(c.Haul.ServerBegin(), Is.True);
            Assert.That(c.Haul.ServerSetFloor(floor), Is.True);
            Assert.That(c.Floor.ServerSetFloor(seed, floor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.FloorSync.Built != null && c.Peer.FloorSync.BuiltFloor == floor, Timeout, "floors were not built");
            c.Layout = c.Floor.Layout;
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.Floor == floor && c.Contents.State.Floor == floor && c.Peer.Plan != null && c.Contents.Plan != null, Timeout, "the contents state / plan never reached both peers");
            done(c);
        }

        // One process holds both peers' floor syncs: a peer's contents is pinned to the floor sync of its own NetworkManager (FindFirstObjectByType could return the host's).
        private static void PinPeer(CellarContentsSync peer, PlayModeHarness.TestClient client)
        {
            foreach (CellarFloorSync f in Object.FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (f.NetworkManager == client.NetworkManager) peer.FloorSync = f;
        }

        private static Vector3 Cell(Ctx c, CellPos p) => c.Floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(p.X, p.Y));
        private static Vector3 ValveAt(Ctx c, int v) => Cell(c, v == 0 ? c.Contents.Plan.ValveCellA : c.Contents.Plan.ValveCellB);
        private static Vector3 Far(Ctx c) => Cell(c, new CellPos(0, 0)) + new Vector3(80f, 0f, 80f);

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // The owner side of a hold: sends the report every ReportInterval for `seconds` (what LocalInteract does while the button is down), through the RPC.
        private static IEnumerator HoldValve(CellarContentsSync from, int valve, float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds, next = 0;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                if (Time.realtimeSinceStartupAsDouble >= next) { from.RequestValveHold(valve); next = Time.realtimeSinceStartupAsDouble + FloorObjectiveTuning.ReportInterval; }
                PlayModeHarness.Pace(); yield return null;
            }
        }

        private static IEnumerator HoldUntilTurned(CellarContentsSync from, CellarContentsSync watch, int valve)
        {
            double until = Time.realtimeSinceStartupAsDouble + Timeout, next = 0;
            while ((watch.State.ValveMask & (1 << valve)) == 0)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("valve " + valve + " never turned (mask " + watch.State.ValveMask + ")");
                if (Time.realtimeSinceStartupAsDouble >= next) { from.RequestValveHold(valve); next = Time.realtimeSinceStartupAsDouble + FloorObjectiveTuning.ReportInterval; }
                PlayModeHarness.Pace(); yield return null;
            }
        }

        // ---- two valves ----

        [UnityTest]
        public IEnumerator TwoValves_BothTurned_UnlocksExit_AnyOrder([Values(false, true)] bool bFirst)
        {
            uint seed = FindSeed(FloorObjective.TwoValves, 2);
            Ctx c = null; yield return Setup(x => c = x, seed, 2);
            Assert.That(c.Contents.Plan.Hash(), Is.EqualTo(c.Peer.Plan.Hash()), "host and client compute the same objective and cells");
            Assert.That(c.Contents.State.Objective, Is.EqualTo((byte)FloorObjective.TwoValves));
            Assert.That(c.Peer.State.Objective, Is.EqualTo((byte)FloorObjective.TwoValves));
            Assert.That(c.Contents.State.ExitUnlocked, Is.False, "a valve floor does not open at arm time");
            Assert.That(c.Peer.TryValvePosition(0, out _) && c.Peer.TryValvePosition(1, out _) && c.Contents.TryValvePosition(1, out _), Is.True, "every peer built both valve markers");
            int unlocked = 0; c.Contents.ExitUnlocked += f => { if (f == 2) unlocked++; };

            c.Players[c.HostId] = ValveAt(c, 1) + new Vector3(0.6f, 0f, 0f);   // the host stands at B, the client at A: two players at once
            c.Players[c.ClientId] = ValveAt(c, 0) + new Vector3(0.6f, 0f, 0f);
            int first = bFirst ? 1 : 0, second = 1 - first;
            CellarContentsSync firstFrom = bFirst ? c.Contents : c.Peer, secondFrom = bFirst ? c.Peer : c.Contents;
            yield return HoldUntilTurned(firstFrom, c.Peer, first);
            Assert.That(c.Contents.ValvesTurned, Is.EqualTo(1));
            Assert.That(c.Contents.State.ExitUnlocked, Is.False, "one valve is not enough");
            yield return PlayModeHarness.WaitUntil(() => c.Peer.ValveShownTurned(first) && !c.Peer.ValveShownTurned(second), Timeout, "the client's marker did not follow the valve bit");
            yield return HoldUntilTurned(secondFrom, c.Peer, second);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked && c.Contents.State.ExitUnlocked, Timeout, "both valves turned but the exit never unlocked");
            Assert.That(c.Peer.ValvesTurned, Is.EqualTo(2));
            Assert.That(c.Peer.ObjectiveLine, Is.EqualTo("Open both valves (2/2)"));
            yield return Seconds(0.6f);
            Assert.That(unlocked, Is.EqualTo(1), "the exit unlocks once, however the valves were turned");
            // A valve that is turned stays turned: reports for it change nothing and the state does not move.
            CellarContentsSync.ContentsState before = c.Contents.State;
            yield return HoldValve(c.Peer, 0, 0.5f);
            Assert.That(before.Equals(c.Contents.State), Is.True, "a turned valve stays turned and nothing else moves");
        }

        [UnityTest]
        public IEnumerator Valve_HoldInterrupted_OrOutOfReach_DoesNotTurn()
        {
            uint seed = FindSeed(FloorObjective.TwoValves, 2);
            Ctx c = null; yield return Setup(x => c = x, seed, 2);
            c.Players[c.HostId] = Far(c);
            c.Players[c.ClientId] = ValveAt(c, 0) + new Vector3(0.6f, 0f, 0f);
            int handled = c.Contents.ValveHoldRequests;

            yield return HoldValve(c.Peer, 0, FloorObjectiveTuning.ValveHoldSeconds * 0.55f);
            yield return Seconds(FloorObjectiveTuning.ReportTimeout + 0.4f);   // reports stop: the progress resets
            yield return HoldValve(c.Peer, 0, FloorObjectiveTuning.ValveHoldSeconds * 0.55f);
            yield return Seconds(0.4f);
            Assert.That(c.Contents.ValveHoldRequests, Is.GreaterThan(handled), "the reports reached the host");
            Assert.That(c.Contents.State.ValveMask, Is.EqualTo((byte)0), "two interrupted holds (each under the hold time) never turn the valve, even though they add up to more");

            c.Players[c.ClientId] = ValveAt(c, 0) + new Vector3(FloorObjectiveTuning.ValveReachMeters + CellarContentsSync.ValveReportReachSlack + 1.0f, 0f, 0f);   // too far for the host to accept
            yield return HoldValve(c.Peer, 0, FloorObjectiveTuning.ValveHoldSeconds * 1.6f);
            Assert.That(c.Contents.State.ValveMask, Is.EqualTo((byte)0), "a hold from out of reach is refused by the host");

            c.Players[c.ClientId] = ValveAt(c, 0) + new Vector3(0.6f, 0f, 0f);
            yield return HoldUntilTurned(c.Peer, c.Contents, 0);   // the same hero held in reach: turns
            Assert.That(c.Contents.ValvesTurned, Is.EqualTo(1));
            Assert.That(c.Contents.State.ExitUnlocked, Is.False);
        }

        // ---- the pump ----

        private static IEnumerator WaitPumpDone(Ctx c)
        {
            yield return PlayModeHarness.WaitUntil(() => c.Contents.State.ExitUnlocked && c.Contents.State.PumpPercent == 100, Timeout * 2f, "the pump never finished (percent " + c.Contents.State.PumpPercent + ", started " + c.Contents.State.PumpStarted + ")");
        }

        [UnityTest]
        public IEnumerator Pump_Start_Fill_UnlocksExit()
        {
            CellarContentsSync.PumpSecondsOverrideForTests = 6f;
            uint seed = FindSeed(FloorObjective.HoldPump, 2);
            Ctx c = null; yield return Setup(x => c = x, seed, 2);
            Assert.That(c.Contents.Plan.Hash(), Is.EqualTo(c.Peer.Plan.Hash()));
            Assert.That(c.Peer.TryPumpPosition(out Vector3 pump), Is.True, "the client built the pump marker");
            Color idle = c.Peer.PumpMarkerColor;
            int unlocked = 0; c.Contents.ExitUnlocked += f => unlocked++;

            c.Players[c.HostId] = Far(c);
            c.Players[c.ClientId] = pump + new Vector3(FloorObjectiveTuning.PumpStartReach + 1.5f, 0f, 0f);   // inside the ring but out of start reach
            c.Peer.RequestStartPump();
            yield return Seconds(0.6f);
            Assert.That(c.Contents.State.PumpStarted, Is.False, "the host refuses a start from out of reach");
            Assert.That(c.Contents.State.PumpPercent, Is.EqualTo((byte)0));

            c.Players[c.ClientId] = pump + new Vector3(1.2f, 0f, 0f);
            c.Peer.RequestStartPump();
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.PumpStarted, Timeout, "the pump never started on the client");
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.PumpPercent >= 5, Timeout, "the pump never filled with a hero in the ring");
            Assert.That(c.Peer.PumpMarkerColor, Is.Not.EqualTo(idle), "the pump marker changed colour once started");

            // pauses (never resets) while nobody is in the ring
            c.Players[c.ClientId] = Far(c);
            yield return Seconds(0.5f);   // let in-flight ticks land
            byte held = c.Contents.State.PumpPercent;
            yield return Seconds(1.2f);
            Assert.That(c.Contents.State.PumpPercent, Is.EqualTo(held), "nobody in the ring: the pump pauses, and does not run back");
            Assert.That(held, Is.GreaterThan((byte)0));

            c.Players[c.ClientId] = pump + new Vector3(1.2f, 0f, 0f);
            yield return WaitPumpDone(c);
            yield return PlayModeHarness.WaitUntil(() => c.Peer.State.ExitUnlocked && c.Peer.State.PumpPercent == 100, Timeout, "the client never saw the pump done");
            Assert.That(c.Peer.ObjectiveLine, Is.EqualTo("Start the pump and hold it (100%)"));
            yield return Seconds(0.4f);
            Assert.That(unlocked, Is.EqualTo(1), "the pump unlocks the exit once");
            Assert.That(c.Contents.WavesStarted, Is.InRange(FloorObjectiveTuning.MaxWavesAlive, FloorObjectiveTuning.WaveCount), "the first two waves came; the third waits while two earlier waves live (nobody kills them here), unless the live cap trimmed one to nothing");
        }

        [UnityTest]
        public IEnumerator Pump_WavesTrimmedAtCap()
        {
            CellarContentsSync.PumpSecondsOverrideForTests = 8f;
            uint seed = FindSeed(FloorObjective.HoldPump, 2);
            Ctx c = null; yield return Setup(x => c = x, seed, 2);
            Assert.That(c.Peer.TryPumpPosition(out Vector3 pump), Is.True);
            CombatHost combat = c.Contents.CombatForTests;
            Assert.That(combat, Is.Not.Null);
            // the heroes stand in the pump room (which spawns its own room enemies on entry: not newly capped, plan 5 deferred); let those appear first
            c.Players[c.HostId] = pump + new Vector3(1.2f, 0f, 0f);
            c.Players[c.ClientId] = pump + new Vector3(-1.2f, 0f, 0f);
            CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(c.Layout, c.Floor.Built.Dressing, 0, false);
            int room = c.Contents.Plan.PumpRoom;
            yield return PlayModeHarness.WaitUntil(() => c.Contents.EnterEnemyCount(room) == plan.CountIn(room), Timeout, "the pump room's own enemies never appeared");
            // inert fodder far away fills the floor to two places under the cap
            while (combat.AliveEnemies.Count < GameMetrics.MaxLiveEnemies - 2) Assert.That(combat.SpawnEnemy(EnemyKind.Slime, 1, pump + new Vector3(40f + combat.AliveEnemies.Count, 0f, 40f), ai: false), Is.Not.Null);
            int live0 = combat.AliveEnemies.Count;
            Assert.That(live0, Is.LessThanOrEqualTo(GameMetrics.MaxLiveEnemies), "premise: the floor starts at or under the cap");
            int free = GameMetrics.MaxLiveEnemies - live0;

            c.Contents.RequestStartPump();
            int max = 0; double until = Time.realtimeSinceStartupAsDouble + Timeout * 2f;
            while (!c.Contents.State.ExitUnlocked)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("the pump never finished; percent " + c.Contents.State.PumpPercent + ", live " + combat.AliveEnemies.Count);
                max = Mathf.Max(max, combat.AliveEnemies.Count);
                PlayModeHarness.Pace(); yield return null;
            }
            Assert.That(max, Is.LessThanOrEqualTo(GameMetrics.MaxLiveEnemies), "pump waves never push the floor above the live cap");
            Assert.That(c.Contents.WavesStarted, Is.EqualTo(FloorObjectiveTuning.WaveCount));
            Assert.That(c.Contents.PumpEnemiesSpawned, Is.EqualTo(PumpHold.WaveSize(2, live0)).And.LessThanOrEqualTo(free), "wave 0 took the free places (" + free + "); waves 1 and 2 were trimmed to nothing, nobody killed anything");
        }

        // ---- late join ----

        [UnityTest]
        public IEnumerator LateJoiner_SeesValveAndPumpState([Values(FloorObjective.TwoValves, FloorObjective.HoldPump)] FloorObjective objective)
        {
            CellarContentsSync.PumpSecondsOverrideForTests = 40f;   // the pump runs slowly: it is still running when the joiner arrives
            uint seed = FindSeed(objective, 2);
            Ctx c = null; yield return Setup(x => c = x, seed, 2);
            if (objective == FloorObjective.TwoValves)
            {
                c.Players[c.HostId] = ValveAt(c, 0) + new Vector3(0.6f, 0f, 0f); c.Players[c.ClientId] = Far(c);
                yield return HoldUntilTurned(c.Contents, c.Contents, 0);
            }
            else
            {
                Assert.That(c.Contents.TryPumpPosition(out Vector3 pump), Is.True);
                c.Players[c.HostId] = pump + new Vector3(1.2f, 0f, 0f); c.Players[c.ClientId] = Far(c);
                c.Contents.RequestStartPump();
                yield return PlayModeHarness.WaitUntil(() => c.Contents.State.PumpPercent >= 1, Timeout, "the pump never filled");
                c.Players[c.HostId] = Far(c);   // park it: the percent the joiner sees is the one the host holds
                yield return Seconds(0.5f);
            }
            var late = new PlayModeHarness.TestClient(AppBootstrap.Current, PlayModeHarness.PlayerId(9));
            _clients.Add(late);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(late.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => late.NetworkManager.IsConnectedClient, Timeout, "the late client never connected");
            ulong id = c.Contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => late.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "the late client never received the contents object");
            CellarContentsSync peer = late.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<CellarContentsSync>();
            PinPeer(peer, late);
            yield return PlayModeHarness.WaitUntil(() => peer.FloorSync != null && peer.FloorSync.Built != null && peer.State.Floor == 2 && peer.Plan != null, Timeout, "the late joiner never built the floor / got the state");
            Assert.That(peer.Plan.Hash(), Is.EqualTo(c.Contents.Plan.Hash()), "the joiner computes the same plan");
            Assert.That(peer.State.Objective, Is.EqualTo((byte)objective));
            if (objective == FloorObjective.TwoValves)
            {
                Assert.That(peer.State.ValveMask, Is.EqualTo((byte)1), "the joiner sees the valve that was already turned");
                yield return PlayModeHarness.WaitUntil(() => peer.ValveShownTurned(0) && !peer.ValveShownTurned(1), Timeout, "the joiner's markers do not show valve 0 turned and valve 1 shut");
            }
            else
            {
                Assert.That(peer.State.PumpStarted, Is.True, "the joiner sees the pump started");
                Assert.That(peer.State.PumpPercent, Is.EqualTo(c.Contents.State.PumpPercent), "and its progress");
                yield return PlayModeHarness.WaitUntil(() => peer.PumpMarkerShown, Timeout, "the joiner built no pump marker");
            }
        }
    }
}
