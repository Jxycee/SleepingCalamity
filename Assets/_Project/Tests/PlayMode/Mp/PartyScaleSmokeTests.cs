// Authority: test-only. Multiplayer readiness, QUEUE.md "3-4 player smoke with bots + MPPM" (triage Q1).
// A party of 2, 3 and 4 (host + 1-3 in-process TestClients, MpPartyRig: no scenes, the real DiveDirector / CellarFloorSync / CellarContentsSync / CombatHost / hero prefab) plays the core loop:
// a client presses the hub trapdoor (real RPC), the party arrives on floor 1, a hero walks into a combat room and the enemies that SPAWN are counted on the host and on every peer against the party-size
// rule (CoopScaling.ExtraEnemiesPerRoom: the room's planned enemies + 1 per extra player; wave 0 spawns on entry, wave 1 once wave 0 is down), a client presses the stair (floor change), the host abandons
// the dive and everyone is back at the hub. ReplicatedStateHash.AssertAllEqual runs after every step on all peers, and the log must hold no error / exception and no Netcode warning.
// "Bots": the heroes are driven by the test through the same RPCs a player's presses end in (RequestDescend, RequestStair) and by moving the owner copy of the hero; the walking bots of Bot/CoopDiveRun
// stay the soak (CoopDiveBotTests / CoopSoakTests, SC_BOT / SC_SOAK) and CoopDiveFastTests the check-tier bot run for 2 players. MPPM is not usable here, see MpPartyRig.
//
// TIERS (PlayMode is not category-filtered by the wrapper, so `check` runs every un-ignored test):
//   * Party_EntersDive_ScalesEnemies_ChangesFloor_ReturnsToHub  [Values 2, 3, 4]  CHECK tier, no latency.
//   * PartyUnderLatency_...                                      [Values 3, 4] x [150/2, 300/5]  SLOW tier: Assert.Ignore unless SC_MP=1, [Category("Slow")].
// Run the slow tier:  SC_MP=1 powershell -NoProfile -ExecutionPolicy Bypass -File <worktree>\Tools\unity.ps1 test-play -Filter PartyScaleSmokeTests -OutDir <scratch>
//
// NOT covered: rooms the plan-driven spawner fills (bakery set pieces, Tier 2 / warden rooms: CellarTemplateSpawns.Resolve returns null there, and the plan ignores party size), the ambush room
// (delayed spring), enemy life scaling (CoopScaling.LifeMultiplier), the stir extras on top, real melee, walking bots, scene sync to a real second machine, the hash does not hold enemies.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PartyScaleSmokeTests
    {
        private MpPartyRig _rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig?.Dispose(); _rig = null;
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the clients
        }

        // Catches: a party size the director or the room spawner reads wrongly (the live enemy count not following CoopScaling), a peer that misses spawned enemies, a floor change or return to the hub that
        // leaves peers disagreeing or a hero behind, a Netcode warning or error with 2-4 players. Tier: check.
        [UnityTest, Timeout(900000)]
        public IEnumerator Party_EntersDive_ScalesEnemies_ChangesFloor_ReturnsToHub([Values(2, 3, 4)] int players)
        {
            yield return Run(players, NetCondition.None);
        }

        // Catches: the same on the slow / lossy link (the enemy replication, the stair press and the hub return with 3-4 peers). Tier: slow (SC_MP=1).
        [UnityTest, Timeout(1800000), Category("Slow")]
        public IEnumerator PartyUnderLatency_EntersDive_ScalesEnemies_ChangesFloor_ReturnsToHub([Values(3, 4)] int players, [Values(NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            if (System.Environment.GetEnvironmentVariable("SC_MP") != "1") Assert.Ignore("slow multiplayer-readiness tier: set SC_MP=1 to run");
            yield return Run(players, net);
        }

        private IEnumerator Run(int players, NetCondition net)
        {
            yield return MpPartyRig.Boot(players - 1, net, r => _rig = r);
            MpPartyRig rig = _rig;
            string tag = "[" + players + "p " + net + "]";
            int last = rig.Players - 1;
            Assert.That(rig.Players, Is.EqualTo(players), tag + " the party is up");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(players), tag + " the host counts every player");
            yield return rig.AssertAgree(tag + " connected, in the hub");

            // enter: the last client's trapdoor press
            rig.Host.SeedOverride = 12345u;
            yield return rig.DescendFrom(last, tag + " [F1 enter]");
            yield return rig.FloorBuiltEverywhere(1, tag + " [F1 enter]");
            yield return rig.AllAtEntry(tag + " [F1 enter]");
            yield return rig.AssertAgree(tag + " F1 entered");

            // enemy scaling on the first floor with a template combat room (floor 1, else floor 2 after the stair)
            int floor = 1;
            bool scaled = false;
            for (int attempt = 0; attempt < 2 && !scaled; attempt++)
            {
                scaled = false;
                IEnumerator check = ScaleCheck(rig, players, floor, tag, ok => scaled = ok);
                while (check.MoveNext()) yield return check.Current;
                if (scaled) break;
                yield return Stair(rig, floor, tag);
                floor++;
            }
            Assert.That(scaled, Is.True, tag + " floor 1 and floor 2 of the pinned seed hold no template combat room to count enemies in (pick another seed)");
            yield return rig.AssertAgree(tag + " after the fight");

            // floor change (when the scaling room was on floor 1 the stair has not been taken yet)
            if (floor == 1) { yield return Stair(rig, 1, tag); floor = 2; }
            Assert.That(ReplicatedStateHash.Describe(rig.HostNm), Does.Contain("CurrentFloor=" + floor), tag + " the hash shows floor " + floor);

            // return to the hub: every hero up and home, on every peer
            Assert.That(rig.Host.ServerAbandonDive(), Is.True, tag + " [home] the dive ends");
            yield return rig.AllHome(tag + " [home]");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync.BuiltFloor == CellarFloorSync.NoFloor, MpPartyRig.Timeout, tag + " [home] the floor never cleared");
            yield return rig.AssertAgree(tag + " back in the hub");
            foreach (HeroVitals v in rig.HeroesOnHost.Select(h => h.GetComponent<HeroVitals>())) Assert.That(v.IsUp, Is.True, tag + " every hero is up after the return");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(players), tag + " nobody was lost on the way");

            Debug.Log("[MP] " + tag + " party loop done; errors " + rig.Errors.Count + ", warnings " + rig.Warnings.Count + " [" + string.Join(" | ", rig.Warnings) + "]");
            rig.AssertCleanLog(tag);
        }

        // The last client takes the stair of `from` (real RPC), then every peer built the next floor and the party arrived.
        private static IEnumerator Stair(MpPartyRig rig, int from, string tag)
        {
            rig.Host.ServerOpenStair(from);
            yield return rig.PeerToStair(rig.Players - 1, tag + " [F" + from + " stair]");
            yield return rig.StairFrom(rig.Players - 1, from, tag + " [F" + from + " stair]");
            yield return rig.FloorBuiltEverywhere(from + 1, tag + " [F" + (from + 1) + " arrive]");
            yield return rig.AllAtEntry(tag + " [F" + (from + 1) + " arrive]");
            yield return rig.AssertAgree(tag + " F" + (from + 1) + " arrived");
        }

        // Picks a template combat room of the built floor and counts the enemies that spawn when the host hero walks in. done(false) when the floor has no such room.
        private static IEnumerator ScaleCheck(MpPartyRig rig, int players, int floor, string tag, System.Action<bool> done)
        {
            string step = tag + " [F" + floor + " enemies]";
            CellarFloorSync fs = rig.Host.FloorSync;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.HaulSync.EncounterPlan != null && rig.Host.HaulSync.EncounterPlan.Floor == floor, MpPartyRig.Timeout, step + ": the encounter plan never arrived");
            CellarEncounterPlan plan = rig.Host.HaulSync.EncounterPlan;
            CellarFloorLayout layout = fs.Layout;
            CellarFloorDressing dressing = fs.Built.Dressing;

            int room = -1, baseCount = int.MaxValue; CellPos spot = default;
            for (int r = 0; r < layout.Rooms.Count; r++)
            {
                if (plan.Twist == FloorTwist.Ambush && r == plan.AmbushRoom) continue;   // the ambush springs after a delay: not this test
                List<EncounterSpawn> mine = plan.Spawns.Where(s => s.RoomIndex == r && s.Kind != EncounterKind.Hunter).ToList();
                if (mine.Count == 0 || mine.Count >= baseCount) continue;
                if (CellarTemplateSpawns.Resolve(layout, dressing, plan, r, 1) == null) continue;   // a plan-driven room (set piece, Tier 2, warden): no party scaling in it, see the header
                room = r; baseCount = mine.Count; spot = mine[0].Cell;
            }
            if (room < 0) { done(false); yield break; }

            int expected = baseCount + CoopScaling.ExtraEnemiesPerRoom(players);
            Assert.That(CoopScaling.ExtraEnemiesPerRoom(players), Is.EqualTo(players - 1), step + ": the rule is +1 enemy per extra player");
            Assert.That(rig.Host.ContentsSync.EnterEnemyCount(room), Is.EqualTo(0), step + ": nothing spawned before a hero entered room " + room);

            // the host hero walks into the room
            Vector3 at = fs.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(spot.X, spot.Y));
            MpPartyRig.Move(rig.HeroesOnHost[0], at);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync.EnterEnemyCount(room) > 0, MpPartyRig.Timeout, step + ": entering room " + room + " never spawned its enemies (plan base " + baseCount + ", " + players + " players)");
            yield return MpPartyRig.Seconds(0.5f);
            int wave0 = rig.Host.ContentsSync.EnterEnemyCount(room);
            Assert.That(wave0, Is.LessThanOrEqualTo(expected), step + ": wave 0 alone is within the party's total");
            Assert.That(AliveEnemies(rig.HostNm), Is.EqualTo(wave0), step + ": every spawned enemy lives on the host");
            foreach (NetworkManager peer in rig.Peers)
            {
                NetworkManager p = peer;
                yield return PlayModeHarness.WaitUntil(() => AliveEnemies(p) == wave0, MpPartyRig.Timeout, step + ": peer " + p.LocalClientId + " sees " + AliveEnemies(p) + " enemies, the host spawned " + wave0);
            }

            // wave 0 down: wave 1 (if the room has one) follows, and the total must meet the rule
            Debug.Log("[MP] " + step + " room " + room + ": plan base " + baseCount + ", wave 0 " + wave0 + ", rule total " + expected);
            KillAll(rig.HostNm);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync.EnterEnemyCount(room) >= expected || rig.Host.ContentsSync.AliveInRoom(room) == 0, MpPartyRig.Timeout, step + ": the room's later enemies never spawned");
            yield return MpPartyRig.Seconds(1f);
            int total = rig.Host.ContentsSync.EnterEnemyCount(room);
            Assert.That(total, Is.EqualTo(expected), step + ": room " + room + " spawned " + total + " enemies in total with " + players + " players; the rule (plan base " + baseCount + " + " + (players - 1) + " extra) says " + expected + " (wave 0 was " + wave0 + ")");
            int wave1 = total - wave0;
            foreach (NetworkManager peer in rig.Peers)
            {
                NetworkManager p = peer;
                yield return PlayModeHarness.WaitUntil(() => AliveEnemies(p) == wave1, MpPartyRig.Timeout, step + ": peer " + p.LocalClientId + " sees " + AliveEnemies(p) + " wave-1 enemies, the host spawned " + wave1);
            }
            KillAll(rig.HostNm);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.ContentsSync.AliveInRoom(room) == 0 && rig.Host.ContentsSync.State.Cleared != 0, MpPartyRig.Timeout, step + ": the room never cleared");
            done(true);
        }

        private static int AliveEnemies(NetworkManager nm)
        {
            int n = 0;
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values)
            {
                if (o == null) continue;
                Enemy e = o.GetComponent<Enemy>();
                if (e != null && e.Alive) n++;
            }
            return n;
        }

        // The real hit path (what a swing ends in) on every living enemy of the host.
        private static void KillAll(NetworkManager nm)
        {
            var hit = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = 100000f, PartIndex = -1, CritChance = 0f };
            foreach (NetworkObject o in new List<NetworkObject>(nm.SpawnManager.SpawnedObjects.Values))
            {
                if (o == null) continue;
                Enemy e = o.GetComponent<Enemy>();
                if (e == null || !e.Alive) continue;
                for (int i = 0; i < 6 && e != null && e.Alive; i++) e.ServerApplyHit(hit, e.transform.position + Vector3.back, Time.timeAsDouble);
            }
        }
    }
}
