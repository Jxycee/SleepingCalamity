// Authority: test-only. Multiplayer readiness, QUEUE.md "Network conditions test set at 150 ms / 2% and 300 ms / 5%" (triage Q6).
// What DiveWalkQaTests / SquareLoopWalkQaTests already run under all three NetConditions: the walks (gate key, chest, piece, descend, stir, bank, go home, rescue, baker, save reload). None of them
// compares the replicated state of the peers or watches the log for Netcode warnings under latency, and nothing at all rejoins under latency. This file adds exactly that.
//
// TIERS (PlayMode is not category-filtered by the wrapper, so `check` runs every un-ignored test):
//   * CoreLoop_ClientDrivenDive_StateAgreesAtEveryStep_UnderLatency   [Values 150/2, 300/5]     CHECK tier. Host + 1 client. Real client RPCs (trapdoor, stair), host abandon back to the hub, a second dive
//                                                                                              start; ReplicatedStateHash.AssertAllEqual after every step, no error log, no Netcode warning.
//   * QuadCoreLoop_UnderLatency                                       [Values 150/2, 300/5]     SLOW tier: Assert.Ignore unless SC_MP=1, [Category("Slow")]. Same loop with host + 3 clients.
//   * Rejoin_MidDive_UnderLatency_HashAgrees                          [Values 150/2, 300/5]     SLOW tier (SC_MP=1). Three players, one drops mid-dive and rejoins with the same PlayerId.
// Run the slow tier:  SC_MP=1 powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>\Tools\unity.ps1 test-play -Filter NetConditionCoreLoopTests -OutDir <scratch>
//
// NOT covered: the walks' leg-by-leg assertions (DiveWalkQa owns those), the 3-4 player enemy scaling (PartyScaleSmokeTests), packet reordering / jitter (the simulator preset has none), a real relay,
// a host that drops, save/load under latency (SquareLoopWalk does the save reload), enemies and transforms in the hash. The hash also covers only what ReplicatedStateHash covers today.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class NetConditionCoreLoopTests
    {
        private MpPartyRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig?.Dispose(); _rig = null;
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the clients
        }

        private static void RequireSlowTier()
        {
            if (System.Environment.GetEnvironmentVariable("SC_MP") != "1") Assert.Ignore("slow multiplayer-readiness tier: set SC_MP=1 to run");
        }

        // Catches: a replicated-state divergence, a Netcode warning or an error log across the whole enter / floor change / return-to-town loop when the link is slow and lossy, driven by the client's own RPCs.
        // Tier: check.
        [UnityTest, Timeout(600000)]
        public IEnumerator CoreLoop_ClientDrivenDive_StateAgreesAtEveryStep_UnderLatency([Values(NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            yield return Loop(1, net);
        }

        // Catches: the same, with four peers on the slow link. Tier: slow (SC_MP=1).
        [UnityTest, Timeout(1200000), Category("Slow")]
        public IEnumerator QuadCoreLoop_UnderLatency([Values(NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            RequireSlowTier();
            yield return Loop(3, net);
        }

        private IEnumerator Loop(int clients, NetCondition net)
        {
            yield return MpPartyRig.Boot(clients, net, r => _rig = r);
            MpPartyRig rig = _rig;
            string tag = "[" + net + " x" + rig.Players + "]";
            int last = rig.Players - 1;
            Assert.That(rig.Players, Is.EqualTo(clients + 1), tag + " the party is up");
            yield return rig.AssertAgree(tag + " connected, in the hub");

            // enter the dive: the last client presses the trapdoor (real RPC through the simulated link)
            rig.Host.SeedOverride = 12345u;
            yield return rig.DescendFrom(last, tag + " [F1 enter]");
            yield return rig.FloorBuiltEverywhere(1, tag + " [F1 enter]");
            yield return rig.AllAtEntry(tag + " [F1 enter]");
            yield return rig.AssertAgree(tag + " F1 entered");
            foreach (DiveDirector d in rig.Directors) Assert.That(d.NetActive.Value, Is.True, tag + " every peer sees the dive active");

            // a piece, an item and some damage: haul, bag and hero state replicate
            Assert.That(rig.Host.HaulSync.ServerAddPiece(PieceCatalog.FrameId), Is.True, tag + " [F1 piece]");
            Assert.That(rig.Host.HaulSync.ServerAddItem(rig.HostNm.LocalClientId, 101), Is.True, tag + " [F1 loot]");
            rig.HeroesOnHost[0].GetComponent<SleepingCalamity.Gameplay.HeroVitals>().ServerDamage(15);
            yield return rig.AssertAgree(tag + " F1 piece, item, damage");

            // floor change: the last client walks to the stair and presses it (real RPC)
            rig.Host.ServerOpenStair(1);
            yield return rig.PeerToStair(last, tag + " [F1 stair]");
            yield return rig.StairFrom(last, 1, tag + " [F1 stair]");
            yield return rig.FloorBuiltEverywhere(2, tag + " [F2 arrive]");
            yield return rig.AllAtEntry(tag + " [F2 arrive]");
            yield return rig.AssertAgree(tag + " F2 arrived");
            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("CurrentFloor=2").And.Contain("FoundPieces=1"), tag + " the hash shows floor 2 and the found piece");

            // return to town: the host abandons the dive (the fail path, exactly what the stir countdown running out does): everyone is sent home
            Assert.That(rig.Host.ServerAbandonDive(), Is.True, tag + " [F2 home] the dive ends");
            yield return rig.AllHome(tag + " [F2 home]");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync.BuiltFloor == CellarFloorSync.NoFloor, MpPartyRig.Timeout, tag + " [F2 home] the floor never cleared");
            yield return rig.AssertAgree(tag + " back in the hub");

            // the town is reusable: a second dive starts and builds on every peer
            rig.Host.SeedOverride = 12345u;
            Assert.That(rig.Host.ServerStartDive(), Is.True, tag + " [dive 2] the host starts a second dive");
            yield return rig.FloorBuiltEverywhere(1, tag + " [dive 2]");
            yield return rig.AssertAgree(tag + " second dive entered");

            Debug.Log("[MP] " + tag + " core loop done; errors " + rig.Errors.Count + ", warnings " + rig.Warnings.Count + " [" + string.Join(" | ", rig.Warnings) + "]");
            rig.AssertCleanLog(tag);
        }

        // Catches: a late joiner (same PlayerId, new connection) that does not receive the running dive's state, or a stale hero left behind by the drop, when the link is slow and lossy. Tier: slow (SC_MP=1).
        [UnityTest, Timeout(1200000), Category("Slow")]
        public IEnumerator Rejoin_MidDive_UnderLatency_HashAgrees([Values(NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            RequireSlowTier();
            yield return MpPartyRig.Boot(2, net, r => _rig = r);
            MpPartyRig rig = _rig;
            string tag = "[rejoin " + net + "]";
            rig.Host.SeedOverride = 12345u;
            Assert.That(rig.Host.ServerStartDive(), Is.True, tag + " the dive starts");
            yield return rig.FloorBuiltEverywhere(1, tag + " [F1]");
            yield return rig.AllAtEntry(tag + " [F1]");
            Assert.That(rig.Host.HaulSync.ServerAddPiece(PieceCatalog.FrameId), Is.True, tag + " a piece is found before the drop");
            yield return rig.AssertAgree(tag + " before the drop");

            // client 2 leaves (graceful) and every remaining peer forgets its hero
            int leaver = 2;
            ulong oldId = rig.Peers[leaver].LocalClientId;
            yield return PlayModeHarness.Await(rig.Clients[leaver - 1].Session.LeaveAsync(), MpPartyRig.Timeout);
            rig.Peers.RemoveAt(leaver); rig.HeroesOnHost.RemoveAt(leaver); rig.OwnCopies.RemoveAt(leaver); rig.Directors.RemoveAt(leaver);
            foreach (NetworkManager nm in rig.Peers)
            {
                NetworkManager peer = nm;
                yield return PlayModeHarness.WaitUntil(() => !HasHeroOf(peer, oldId), MpPartyRig.Timeout, tag + " a peer still holds the dropped hero");
            }
            yield return rig.AssertAgree(tag + " after the drop");

            // the same PlayerId joins again (a new client id) mid-dive; the host spawns its hero like the arena spawner would
            yield return PlayModeHarness.StartClients(rig.App, 1, false, rig.Clients, 21);   // PlayerId(21): the second client's id
            PlayModeHarness.TestClient back = rig.Clients[rig.Clients.Count - 1];
            NetworkManager cnm = back.NetworkManager;
            Assert.That(cnm.LocalClientId, Is.Not.EqualTo(oldId), tag + " the rejoin got a new client id");
            rig.Peers.Add(cnm);
            rig.HeroesOnHost.Add(rig.HostNm.SpawnManager.InstantiateAndSpawn(rig.App.PlayerPrefab, cnm.LocalClientId, false, true, false, MpPartyRig.HubStair, Quaternion.identity));
            yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && cnm.SpawnManager.SpawnedObjects.ContainsKey(rig.Host.NetworkObjectId), MpPartyRig.Timeout, tag + " the rejoiner never received its hero and the director");
            rig.OwnCopies.Add(cnm.LocalClient.PlayerObject);
            rig.Directors.Add(cnm.SpawnManager.SpawnedObjects[rig.Host.NetworkObjectId].GetComponent<DiveDirector>());
            yield return PlayModeHarness.WaitUntil(() => rig.Directors[rig.Directors.Count - 1].NetActive.Value, MpPartyRig.Timeout, tag + " the rejoiner never saw the running dive");
            yield return rig.FloorBuiltEverywhere(1, tag + " [rejoin F1]");
            yield return rig.AssertAgree(tag + " the rejoiner agrees with the party");
            rig.AssertCleanLog(tag);
        }

        private static bool HasHeroOf(NetworkManager nm, ulong clientId)
        {
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) if (o != null && o.IsPlayerObject && o.OwnerClientId == clientId) return true;
            return false;
        }
    }
}
