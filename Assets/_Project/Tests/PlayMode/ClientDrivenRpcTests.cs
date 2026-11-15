// Authority: test-only. Client-driven RPC tests (audit 2026-11-12 "Test gaps worth a queue item"): a REAL second NetworkManager (TestClient) sends the RPC and the HOST's state is asserted.
// Each RPC gets an accepted case and host-refused cases. Private owner-side RPCs (HeroCombat.CastServerRpc, ParryServerRpc, ReviveHoldServerRpc) are invoked on the client's own hero by
// reflection, which runs NGO's generated send path (the owner's detection code is not run: a bad request has no input path). The public wrappers (DiveDirector.RequestFinish, TownSync.SendGhost)
// are the same calls the keys make. Delivery of a REFUSED request is proven with an ordered canary RPC on the same object (HeroCombat.RequestSyncServerRpc -> TreePushes, or a request that is
// answered), never a fixed delay: RPCs from one client to one object arrive in send order.
// Catches: CastServerRpc spending a spell pip for a downed hero or twice for one swing; ParryServerRpc accepted for an attack serial the enemy is not performing; ReviveHoldServerRpc that
// revives from out of reach, from a downed reviver or by the downed hero itself; RequestFinishRpc that ends the dive with the objective open, out of range or for a downed hero; SetGhostRpc that
// publishes the ghost for a player who does not hold the build lock, or keeps a ghost for a foundation that does not exist.
// NOT covered: a modded client sending an RPC on ANOTHER player's hero (CLAUDE.md: no anti-cheat), real input (swing / guard / interact key), relay latency, two real machines (manual 2-player walk).
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class ClientDrivenRpcTests
    {
        private const float Timeout = 25f;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();
        private bool _layersChanged;
        private bool[] _savedIgnore;

        private static readonly int[][] LayerPairs =
        {
            new[] { Layers.Player, Layers.RemotePlayer },
            new[] { Layers.Player, Layers.Player },
            new[] { Layers.RemotePlayer, Layers.RemotePlayer },
        };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CellarContentsSync.PumpSecondsOverrideForTests = 0f;
            if (_layersChanged)
            {
                for (int i = 0; i < LayerPairs.Length; i++) Physics.IgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1], _savedIgnore[i]);
                _layersChanged = false;
            }
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
            Scene arena = SceneManager.GetSceneByName(SceneNames.ArenaCombat);
            if (arena.isLoaded) yield return SceneManager.UnloadSceneAsync(arena);
        }

        private static IEnumerator Seconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
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

        // Owner-side move: the CharacterController is off while the pose is written (as the director's own teleport does).
        private static void Move(NetworkObject ownerCopy, Vector3 pos)
        {
            var cc = ownerCopy.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            ownerCopy.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // Puts the owner's hero at `pos` and waits until the HOST's copy of it is there (the host checks the host's copy).
        private static IEnumerator Place(NetworkObject own, NetworkObject onHost, Vector3 pos, string what)
        {
            double until = Time.realtimeSinceStartupAsDouble + Timeout;
            while (Flat(onHost.transform.position, pos) > 2f)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail(what + ": the host's copy is at " + onHost.transform.position + ", wanted " + pos);
                Move(own, pos);
                double slice = Time.realtimeSinceStartupAsDouble + 0.3;
                while (Time.realtimeSinceStartupAsDouble < slice && Flat(onHost.transform.position, pos) > 2f) yield return null;
            }
        }

        // =================================================================================================================================
        // Combat: CastServerRpc, ParryServerRpc, ReviveHoldServerRpc (host + client heroes in the combat arena, AI and waves off)
        // =================================================================================================================================

        private sealed class Ctx
        {
            public AppBootstrap App;
            public NetworkManager HostNm, ClientNm;
            public NetworkObject HostHero, ClientHeroOnClient, ClientHeroOnHost;
            public HeroCombat HostCombat, ClientCombat;
            public HeroVitals HostVitals, ClientVitalsOnHost;
            public CombatHost Combat;
        }

        private IEnumerator CombatSetup(Ctx c, Vector3 clientPos)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            c.App = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            c.HostNm = c.App.NetworkManager;
            var client = new PlayModeHarness.TestClient(c.App, PlayModeHarness.PlayerId(9));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            c.ClientNm = client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => c.HostNm.ConnectedClientsIds.Count == 2 && c.ClientNm.IsConnectedClient, Timeout, "the client never connected");

            foreach (Camera cam in Object.FindObjectsByType<Camera>()) if (cam.name == "LobbyCamera") cam.enabled = false;
            foreach (AudioListener l in Object.FindObjectsByType<AudioListener>()) if (l.name == "LobbyCamera") l.enabled = false;
            yield return SceneManager.LoadSceneAsync(SceneNames.ArenaCombat, LoadSceneMode.Additive);

            _savedIgnore = new bool[LayerPairs.Length];
            for (int i = 0; i < LayerPairs.Length; i++)
            {
                _savedIgnore[i] = Physics.GetIgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1]);
                Physics.IgnoreLayerCollision(LayerPairs[i][0], LayerPairs[i][1], true);
            }
            _layersChanged = true;

            c.Combat = Object.FindAnyObjectByType<CombatHost>();
            Assert.That(c.Combat, Is.Not.Null, "Arena_Combat has the CombatHost");
            c.Combat.WavesEnabled = false;
            c.Combat.AiEnabled = false;
            c.Combat.CritChance = 0f;
            Assert.That(c.Combat.ServerBegin(c.HostNm), Is.True);

            ulong clientId = c.ClientNm.LocalClientId;
            c.HostHero = c.HostNm.SpawnManager.InstantiateAndSpawn(c.App.PlayerPrefab, c.HostNm.LocalClientId, false, true, false, Vector3.zero, Quaternion.identity);
            c.ClientHeroOnHost = c.HostNm.SpawnManager.InstantiateAndSpawn(c.App.PlayerPrefab, clientId, false, true, false, clientPos, Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.LocalClient != null && c.ClientNm.LocalClient.PlayerObject != null
                                                          && c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(c.HostHero.NetworkObjectId), Timeout, "the client never received the heroes");
            c.ClientHeroOnClient = c.ClientNm.LocalClient.PlayerObject;
            c.HostCombat = c.HostHero.GetComponent<HeroCombat>();
            c.ClientCombat = c.ClientHeroOnClient.GetComponent<HeroCombat>();
            c.HostVitals = c.HostHero.GetComponent<HeroVitals>();
            c.ClientVitalsOnHost = c.ClientHeroOnHost.GetComponent<HeroVitals>();
            foreach (NetworkObject hero in new[] { c.HostHero, c.ClientHeroOnClient })   // no real keyboard: both heroes read an idle fake input
            {
                var input = new FakeInputService();
                hero.GetComponent<HeroLocomotion>().Input = input;
                hero.GetComponent<TargetLockController>().Input = input;
                hero.GetComponent<HeroCombat>().Input = input;
            }
            yield return Seconds(0.6f);   // settle on the ground; the spawn pushes land
        }

        private static MethodInfo Rpc(string name)
        {
            MethodInfo m = typeof(HeroCombat).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, "HeroCombat." + name);
            return m;
        }

        // The ordered canary: the same client sends RequestSyncServerRpc on the same object right after the request under test. The host answers it with a forced tree push, which the
        // owner counts (TreePushes); RPCs from one client to one object are processed in send order, so once the push is back the earlier request has been handled (and refused or applied).
        private static IEnumerator Canary(Ctx c)
        {
            int before = c.ClientCombat.TreePushes;
            Rpc("RequestSyncServerRpc").Invoke(c.ClientCombat, null);
            yield return PlayModeHarness.WaitUntil(() => c.ClientCombat.TreePushes > before, Timeout, "the canary (RequestSyncServerRpc) never came back");
        }

        // ---- CastServerRpc ----

        // Catches: CastServerRpc that does not spend the host's pip (the owner's mirror is not the rule), that spends again for the same swing id, or that spends for a downed hero.
        // NOT covered: the real swing that sends it (input), a cast whose pip the host cannot pay (HostPlayerState.Rejected is not observable from outside the hit path).
        [UnityTest]
        public IEnumerator ClientCast_SpendsOnePipOnTheHostOncePerSwing_AndIsRefusedWhileDowned()
        {
            var c = new Ctx();
            yield return CombatSetup(c, new Vector3(8f, 0f, 0f));
            ulong clientId = c.ClientNm.LocalClientId;
            byte staff = (byte)System.Array.IndexOf(AttackCatalog.Heirlooms, WeaponCatalog.Staff);
            Assert.That(AttackCatalog.Staff.Attacks[AttackSet.Special].SpellPips, Is.GreaterThan(0), "test premise: the staff's Special is a spell");
            MethodInfo cast = Rpc("CastServerRpc");
            Assert.That(c.Combat.Growth(clientId).Pips.Current, Is.EqualTo(SpellPips.StartCap), "premise: a fresh player has full pips");

            // Accepted: the host spends one pip.
            cast.Invoke(c.ClientCombat, new object[] { 1, staff, (byte)AttackSet.Special });
            yield return PlayModeHarness.WaitUntil(() => c.Combat.Growth(clientId).Pips.Current == SpellPips.StartCap - 1, Timeout, "the host never spent the pip");
            // Same swing id again: no second charge (the canary proves the duplicate was handled).
            cast.Invoke(c.ClientCombat, new object[] { 1, staff, (byte)AttackSet.Special });
            yield return Canary(c);
            Assert.That(c.Combat.Growth(clientId).Pips.Current, Is.EqualTo(SpellPips.StartCap - 1), "one swing is paid once");
            // A new swing pays again (the check is per swing, not once ever).
            cast.Invoke(c.ClientCombat, new object[] { 2, staff, (byte)AttackSet.Special });
            yield return PlayModeHarness.WaitUntil(() => c.Combat.Growth(clientId).Pips.Current == SpellPips.StartCap - 2, Timeout, "the second swing never paid");

            // Refused: a downed hero casts nothing.
            c.ClientVitalsOnHost.ServerDamage(500);
            Assert.That(c.ClientVitalsOnHost.IsDowned, Is.True);
            cast.Invoke(c.ClientCombat, new object[] { 3, staff, (byte)AttackSet.Special });
            yield return Canary(c);
            Assert.That(c.Combat.Growth(clientId).Pips.Current, Is.EqualTo(SpellPips.StartCap - 2), "a downed hero's cast was spent anyway");
            Assert.That(c.Combat.Growth(c.HostNm.LocalClientId).Pips.Current, Is.EqualTo(SpellPips.StartCap), "the host hero's pips are untouched");
        }

        // ---- ParryServerRpc ----

        // Catches: a parry the host accepts for an attack serial the enemy is not performing (a late or invented serial), or a valid parry that never staggers the enemy.
        // NOT covered: the owner's guard timing that decides a parry (GuardMath, EditMode), the dagger ABS reward (needs the dagger equipped), the dedupe of a repeated serial.
        [UnityTest]
        public IEnumerator ClientParry_StaggersTheEnemyOnTheHost_OnlyForTheSerialItIsPerforming()
        {
            var c = new Ctx();
            yield return CombatSetup(c, new Vector3(8f, 0f, 0f));
            MethodInfo parry = Rpc("ParryServerRpc");
            Enemy wrong = c.Combat.SpawnEnemy(EnemyKind.Slime, 1, new Vector3(8f, 0f, 3f), ai: false);
            Enemy right = c.Combat.SpawnEnemy(EnemyKind.Slime, 1, new Vector3(9f, 0f, 3f), ai: false);
            yield return PlayModeHarness.WaitUntil(() => c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(wrong.NetworkObjectId)
                                                         && c.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(right.NetworkObjectId), Timeout, "the client never received the enemies");

            // Refused: a serial the enemy is not performing (its attack is running, the serial is another one).
            wrong.ServerForceAttack(AttackCatalog.TestNova, c.HostHero.transform.position);
            Assert.That(wrong.Phase, Is.EqualTo(EnemyPhase.Attack));
            int serial = wrong.Serial;
            Assert.That(wrong.ServerAcceptsSerial(serial), Is.True, "premise: the real serial would be accepted");
            parry.Invoke(c.ClientCombat, new object[] { wrong.NetworkObjectId, serial + 7 });
            yield return Canary(c);
            Assert.That(wrong.Phase, Is.Not.EqualTo(EnemyPhase.Stagger), "a parry for a serial the enemy never performed staggered it");

            // Accepted: the real serial while the attack runs.
            right.ServerForceAttack(AttackCatalog.TestNova, c.HostHero.transform.position);
            Assert.That(right.Phase, Is.EqualTo(EnemyPhase.Attack));
            parry.Invoke(c.ClientCombat, new object[] { right.NetworkObjectId, right.Serial });
            yield return PlayModeHarness.WaitUntil(() => right.Phase == EnemyPhase.Stagger, 3f, "the host never staggered the parried enemy");
        }

        // ---- ReviveHoldServerRpc ----

        // Sends a revive hold from the client every 0.2 s (as HeroCombat.ReportReviveHold does) for `seconds`, or until `until` is true; returns the highest revive progress seen on the host.
        private static IEnumerator HoldFor(Ctx c, HeroVitals target, ulong targetNetId, float seconds, System.Func<bool> until, float[] maxProgress)
        {
            MethodInfo hold = Rpc("ReviveHoldServerRpc");
            double end = Time.realtimeSinceStartupAsDouble + seconds, nextSend = 0;
            maxProgress[0] = 0f;
            while (Time.realtimeSinceStartupAsDouble < end && !(until != null && until()))
            {
                if (Time.realtimeSinceStartupAsDouble >= nextSend)
                {
                    hold.Invoke(c.ClientCombat, new object[] { targetNetId });
                    nextSend = Time.realtimeSinceStartupAsDouble + 0.2;
                }
                if (target.Model.ReviveProgress > maxProgress[0]) maxProgress[0] = target.Model.ReviveProgress;
                yield return null;
            }
        }

        // Catches: ReviveHoldServerRpc that revives from out of reach, from a downed reviver, or by the downed hero itself; and a valid hold that never revives (hold timer or progress broken).
        // NOT covered: the owner's own reach scan (ReportReviveHold) and the Interact key; the downed timer running out.
        [UnityTest]
        public IEnumerator ClientRevive_InReachRevives_OutOfReachDownedReviverAndSelfAreRefused()
        {
            var c = new Ctx();
            yield return CombatSetup(c, new Vector3(8f, 0f, 0f));   // 8 m from the host hero: out of reach (3.5 m on the host)
            var max = new float[1];
            c.HostVitals.ServerDamage(500);
            Assert.That(c.HostVitals.IsDowned, Is.True);

            // Refused: out of reach for 1.5 s of continuous holds.
            yield return HoldFor(c, c.HostVitals, c.HostHero.NetworkObjectId, 1.5f, null, max);
            yield return Canary(c);
            Assert.That(max[0], Is.EqualTo(0f), "a hold from out of reach advanced the revive");
            Assert.That(c.HostVitals.IsDowned, Is.True);

            // Refused: in reach but the reviver is downed too.
            yield return Place(c.ClientHeroOnClient, c.ClientHeroOnHost, new Vector3(2f, 0f, 0f), "client hero beside the downed host hero");
            c.ClientVitalsOnHost.ServerDamage(500);
            Assert.That(c.ClientVitalsOnHost.IsDowned, Is.True);
            yield return HoldFor(c, c.HostVitals, c.HostHero.NetworkObjectId, 1.5f, null, max);
            yield return Canary(c);
            Assert.That(max[0], Is.EqualTo(0f), "a downed reviver advanced the revive");

            // Refused: the downed hero cannot revive itself (a hold on its own object).
            yield return HoldFor(c, c.ClientVitalsOnHost, c.ClientHeroOnHost.NetworkObjectId, 1.5f, null, max);
            yield return Canary(c);
            Assert.That(max[0], Is.EqualTo(0f), "a hero revived itself");
            Assert.That(c.ClientVitalsOnHost.IsDowned, Is.True);

            // Accepted: the reviver stands up and holds in reach until the host hero is back up (3 s of holding).
            c.ClientVitalsOnHost.ServerFullReset();
            Assert.That(c.ClientVitalsOnHost.IsUp, Is.True);
            yield return HoldFor(c, c.HostVitals, c.HostHero.NetworkObjectId, 15f, () => c.HostVitals.IsUp, max);
            Assert.That(c.HostVitals.IsUp, Is.True, "an in-reach hold never revived the downed hero (best progress " + max[0] + ")");
            Assert.That(max[0], Is.GreaterThan(0f));
        }

        // =================================================================================================================================
        // Town: SetGhostRpc
        // =================================================================================================================================

        private sealed class TownRig
        {
            public AppBootstrap App; public NetworkManager HostNm;
            public DiveDirector Director; public TownSync Host;
        }

        private IEnumerator TownStart(System.Action<TownRig> done)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new TownRig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Director = dirObj.GetComponent<DiveDirector>();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.TownSync != null && rig.Director.TownSync.IsSpawned, Timeout, "the director never spawned its TownSync");
            rig.Host = rig.Director.TownSync;
            foreach (int piece in new[] { 100, 101, 200, 201 }) Assert.That(rig.Director.Town.State.AddPiece(piece), Is.EqualTo(PlaceResult.Ok));
            rig.Host.ServerRefresh();
            done(rig);
        }

        private IEnumerator TownJoin(TownRig rig, int index, System.Action<TownSync> done)
        {
            var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8 + index));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "client " + index + " never connected");
            ulong id = rig.Host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(id), Timeout, "client " + index + " never received the TownSync");
            done(client.NetworkManager.SpawnManager.SpawnedObjects[id].GetComponent<TownSync>());
        }

        // Catches: SetGhostRpc publishing the ghost for a player who does not hold the build lock (the lock is the only thing that stops a watcher from moving the builder's ghost), a ghost for a
        // foundation that does not exist surviving, and a late ghost for a building the frame already placed. NOT covered: the placement UI that sends it every frame, ghost smoothing on watchers.
        [UnityTest]
        public IEnumerator ClientGhost_OnlyTheLockHolderMovesIt_UnknownFoundationAndPlacedBuildingClearIt()
        {
            TownRig rig = null; TownSync holder = null, watcher = null;
            yield return TownStart(r => rig = r);
            yield return TownJoin(rig, 0, t => holder = t);
            yield return TownJoin(rig, 1, t => watcher = t);

            var holderResults = new List<(TownSync.Op, byte)>();
            holder.ResultReceived += (op, res) => holderResults.Add((op, res));
            var watcherResults = new List<(TownSync.Op, byte)>();
            watcher.ResultReceived += (op, res) => watcherResults.Add((op, res));
            holder.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => holder.IsBuilder && rig.Host.NetBuilder.Value == holder.NetworkManager.LocalClientId, Timeout, "the lock was never granted");

            // Refused: the watcher (no lock) sends a ghost. Its answered SetTurn request is the ordered canary.
            watcher.SendGhost("bakery", "F02", 0, true);
            int answered = watcherResults.Count;
            watcher.RequestSetTurn("bakery", 0);
            yield return PlayModeHarness.WaitUntil(() => watcherResults.Count > answered, Timeout, "the watcher's follow-up request was never answered");
            Assert.That(rig.Host.NetGhost.Value.Active, Is.False, "a player without the build lock published a ghost");

            // Accepted: the holder's ghost reaches the host.
            holder.SendGhost("bakery", "F02", 0, true);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetGhost.Value.Active && rig.Host.NetGhost.Value.Foundation.ToString() == "F02", Timeout, "the holder's ghost never reached the host");
            Assert.That(rig.Host.NetGhost.Value.Building.ToString(), Is.EqualTo("bakery"));

            // Refused: the holder names a foundation that does not exist: the ghost is cleared, not kept or moved.
            holder.SendGhost("bakery", "NOPE", 0, true);
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetGhost.Value.Active, Timeout, "a ghost for an unknown foundation stayed on the host");

            // Refused: a late ghost for a building the frame already placed leaves no ghost.
            holder.RequestPlaceFrame("bakery", "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Town.State.PlacedBuildingOf("bakery") != null, Timeout, "the frame was never placed");
            holder.SendGhost("bakery", "F03", 1, true);
            answered = holderResults.Count;
            holder.RequestSetTurn("bakery", 1);
            yield return PlayModeHarness.WaitUntil(() => holderResults.Count > answered, Timeout, "the holder's follow-up request was never answered");
            Assert.That(rig.Host.NetGhost.Value.Active, Is.False, "a ghost was published for a building that is already placed");
        }

        // =================================================================================================================================
        // Dive: RequestFinishRpc (floor 7's end door)
        // =================================================================================================================================

        private sealed class DiveRig
        {
            public AppBootstrap App; public NetworkManager HostNm, ClientNm;
            public DiveDirector Host, Peer;
            public NetworkObject ClientHeroOnHost, ClientHeroOwn;
        }

        // Catches: RequestFinishRpc ending the dive while floor 7's pump is unfinished, from out of range, or for a downed hero; and a valid finish from a client that never ends the dive.
        // NOT covered: the "Leave the Undercroft" prompt and the interact key; the valve variant of the objective (DiveSpineNetworkTests.Floor7_ValveOrPump_GatesFinish drives the host rule for both).
        [UnityTest]
        public IEnumerator ClientFinish_RefusedWhileObjectiveOpen_OutOfRange_Downed_AcceptedWhenAllHold()
        {
            uint seed = 0;
            for (uint sd = 1; sd < 3000 && seed == 0; sd++) if (FloorObjectivePlan.For(CellarFloorGenerator.Layout(sd, 7)).Objective == FloorObjective.HoldPump) seed = sd;
            Assert.That(seed, Is.Not.EqualTo(0u), "a seed whose floor 7 holds a pump");

            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            var rig = new DiveRig { App = AppBootstrap.Current };
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            rig.HostNm = rig.App.NetworkManager;
            var client = new PlayModeHarness.TestClient(rig.App, PlayModeHarness.PlayerId(8));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            rig.ClientNm = client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.IsConnectedClient, Timeout, "the client never connected");
            Vector3 home = new Vector3(0f, 0f, -6f);
            NetworkObject dirObj = rig.HostNm.SpawnManager.InstantiateAndSpawn(Prefab<DiveDirector>(rig.HostNm), rig.HostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            rig.Host = dirObj.GetComponent<DiveDirector>();
            Assert.That(rig.Host.ServerConfigure(home, new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "the client never received the director");
            rig.Peer = rig.ClientNm.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>();
            // A host hero as well: the client hero going down must not be a party wipe that fails the dive.
            rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.HostNm.LocalClientId, false, true, false, home, Quaternion.identity);
            rig.ClientHeroOnHost = rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, rig.ClientNm.LocalClientId, false, true, false, home + new Vector3(2f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.LocalClient != null && rig.ClientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            rig.ClientHeroOwn = rig.ClientNm.LocalClient.PlayerObject;
            HeroVitals vitals = rig.ClientHeroOnHost.GetComponent<HeroVitals>();
            ulong clientId = rig.ClientNm.LocalClientId;
            yield return Seconds(0.5f);

            rig.Host.SeedOverride = seed;
            DiveEndReason? ended = null; rig.Host.DiveEnded += r => ended = r;
            Assert.That(rig.Host.ServerStartDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null, Timeout, "the dive never spawned its syncs");
            CellarFloorSync floor = rig.Host.FloorSync; DiveHaulSync haul = rig.Host.HaulSync;
            yield return PlayModeHarness.WaitUntil(() => floor.BuiltFloor == 1 && floor.Built != null, Timeout, "floor 1 never built");
            Vector3 entry = CellarFloorBuilder.EntryWorld(floor.Layout, floor.Built, 0);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, entry) < 6f, Timeout, "the client hero never reached the floor-1 entry");
            yield return Seconds(0.5f);   // the arrival teleports have landed before the test moves a hero

            Assert.That(haul.ServerSetFloor(7), Is.True);
            Assert.That(floor.ServerSetFloor(seed, 7), Is.True);
            yield return PlayModeHarness.WaitUntil(() => floor.BuiltFloor == 7 && floor.Built != null, Timeout, "floor 7 never built");
            CellarContentsSync cs = rig.Host.ContentsSync;
            yield return PlayModeHarness.WaitUntil(() => cs != null && cs.State.Floor == 7 && cs.State.Seed == seed && cs.Plan != null, Timeout, "the contents were never armed for floor 7");
            Assert.That(cs.CurrentObjective, Is.EqualTo(FloorObjective.HoldPump));
            Assert.That(CellarFloorBuilder.TryEndWorld(floor.Layout, floor.Built, out Vector3 end), Is.True);
            Assert.That(cs.TryPumpPosition(out Vector3 pump), Is.True);
            Assert.That(Flat(pump, end), Is.GreaterThan(DiveHaulSync.BankRadius + 2f), "test premise: the pump is out of the end door's range");
            yield return Seconds(0.5f);
            Vector3 atEnd = end + new Vector3(0.5f, 0.1f, 0f);

            // Refused: at the end door, the pump unfinished.
            yield return Place(rig.ClientHeroOwn, rig.ClientHeroOnHost, atEnd, "client hero at the end door");
            int handled = rig.Host.RequestsHandled;
            rig.Peer.RequestFinish();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the first finish press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "the dive ended with floor 7's pump unfinished");
            Assert.That(ended, Is.Null);

            // The client runs the pump (host rule, as its start press does), then stays in the ring until the exit opens.
            CellarContentsSync.PumpSecondsOverrideForTests = 0.8f;
            yield return Place(rig.ClientHeroOwn, rig.ClientHeroOnHost, pump + new Vector3(1.2f, 0.1f, 0f), "client hero at the pump");
            double until = Time.realtimeSinceStartupAsDouble + Timeout;
            while (!cs.State.PumpStarted)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("the pump never started");
                cs.ServerTryStartPump(clientId);
                yield return null;
            }
            yield return PlayModeHarness.WaitUntil(() => cs.State.ExitUnlocked, Timeout, "the pump never opened the exit");

            // Refused: the objective is done but the client stands at the pump, far from the door.
            handled = rig.Host.RequestsHandled;
            rig.Peer.RequestFinish();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the far finish press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "the dive ended for a client far from the end door");
            Assert.That(ended, Is.Null);

            // Refused: at the door, objective done, but the hero is downed.
            yield return Place(rig.ClientHeroOwn, rig.ClientHeroOnHost, atEnd, "client hero back at the end door");
            vitals.ServerDamage(500);
            Assert.That(vitals.IsDowned, Is.True);
            handled = rig.Host.RequestsHandled;
            rig.Peer.RequestFinish();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "the downed finish press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "a downed hero ended the dive");
            Assert.That(ended, Is.Null);

            // Accepted: up again, at the door, objective done.
            vitals.ServerFullReset();
            Assert.That(vitals.IsUp, Is.True);
            handled = rig.Host.RequestsHandled;
            rig.Peer.RequestFinish();
            yield return PlayModeHarness.WaitUntil(() => ended.HasValue, Timeout, "the valid finish from the client never ended the dive");
            Assert.That(ended, Is.EqualTo(DiveEndReason.Finished));
            Assert.That(rig.Host.NetActive.Value, Is.False);
        }
    }
}
