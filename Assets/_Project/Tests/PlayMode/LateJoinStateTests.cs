// Authority: test-only. Audit 2026-11-12 "test gaps": a client that connects AFTER state exists must see that state from the spawn alone (nothing is replayed). Host first, state built up with NO client connected, then
// one TestClient joins late and the test reads what ITS copies of the objects show. Host (no scenes) + one late client in one process; the sync objects are the owned network prefabs. Players on the contents host are
// simulated through CellarContentsSync.PlayersOverride, like CellarContentsNetworkTests.
//   L1 contents: live enemies (one hurt, one dead), a cleared room and the opened key chest reach the late client: the same enemies with the same HP / kind / liveness, the room's cleared bit, the key chest shown open
//      on the client's own built floor, the room key held, the door still shut.
//   L2 healing: a point already used (recharging) shows as recharging on the late client: NetHealSecondsLeft > 0 and the client's own orb dark; a point still ready shows lit.
// Catches: a contents / enemy / healing value that is only sent as an event (a late joiner would see a fresh, untouched floor), an enemy's NetworkVariables not seeded before Spawn, a client that builds the floor but never
// applies the state to it (chest not lowered, orb lit while recharging), a late joiner that reads another peer's sync objects.
// NOT covered: "room entered" (only the cleared mask is replicated: ContentsState has no entered bit), per-player loot chests (they live in the opener's owner-only PlayerBagSync, so a brand-new joiner sees them closed by
// design: PerPlayerLootNetworkTests), a REJOINING player's bag, enemy positions and animation state, a late joiner into the scene-managed arena (the manual 2-player walk), more than one late client.
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
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class LateJoinStateTests
    {
        private const float Timeout = 30f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the client StartClients created
        }

        private static GameObject PrefabWith<T>(NetworkManager host) where T : Component
        {
            foreach (NetworkPrefabsList list in host.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab;
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static T Spawn<T>(NetworkManager host, Vector3 at = default) where T : Component
        {
            NetworkObject o = host.SpawnManager.InstantiateAndSpawn(PrefabWith<T>(host).GetComponent<NetworkObject>(), host.LocalClientId, false, false, false, at, Quaternion.identity);
            return o.GetComponent<T>();
        }

        private static Vector3 RoomCentre(CellarFloorSync floor, CellarFloorLayout l, int room)
        {
            PlacedRoom r = l.Rooms[room];
            return floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(r.X + r.Width / 2, r.Y + r.Height / 2));
        }

        private static List<Enemy> EnemiesOn(NetworkManager nm)
        {
            var list = new List<Enemy>();
            foreach (NetworkObject o in nm.SpawnManager.SpawnedObjects.Values) { Enemy e = o != null ? o.GetComponent<Enemy>() : null; if (e != null) list.Add(e); }
            return list;
        }

        private static void Hit(Enemy e, float atk, int times)
        {
            var input = new EnemyHitInput { Attack = AttackCatalog.Axe.Attacks[0], Atk = atk, PartIndex = -1, CritChance = 0f };
            for (int i = 0; i < times && e.Alive; i++) e.ServerApplyHit(input, e.transform.position + Vector3.back, Time.timeAsDouble);
        }

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // The generated first-half floor (2 or 4) that holds the most pieces in the fallback world deal.
        private static int PiecefulFloor()
        {
            DivePieces p = DivePieces.ForWorld(DiveHaulSync.DefaultWorldSeed);
            return p.PiecesLeft(2) >= p.PiecesLeft(4) ? 2 : 4;
        }

        // A seed whose floor has a carrier room with at least two enemies, a SECOND combat room, a key chest, a gate-key objective and no hazard modifier.
        private static uint FindSeed(int floor, out int carrierRoom, out int otherRoom)
        {
            for (uint s = 1; s < 3000; s++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(s, floor);
                if (!(l.HasLockedRoom && l.Carrier != null && l.KeyChestIndex >= 0 && FloorObjectivePlan.For(l).Objective == FloorObjective.GateKey && FloorModifierSet.For(s, floor).A == FloorModifier.None)) continue;
                CellarEncounterPlan plan = CellarEncounterPlan.BuildForDive(l, CellarFloorDressing.Resolve(l), 0, false);
                if (plan.CountIn(l.Carrier.RoomIndex) < 1) continue;
                for (int r = 0; r < l.Rooms.Count; r++)
                {
                    if (r == l.Carrier.RoomIndex || plan.CountIn(r) < 1) continue;
                    PlacedRoom pr = l.Rooms[r];
                    if (l.RoomAt(pr.X + pr.Width / 2, pr.Y + pr.Height / 2) != r) continue;
                    carrierRoom = l.Carrier.RoomIndex; otherRoom = r; return s;
                }
            }
            Assert.Fail("no seed with a carrier room of 2+, a second combat room and a key chest"); carrierRoom = otherRoom = -1; return 0;
        }

        // ================================================================ L1

        [UnityTest]
        public IEnumerator L1_LateJoiner_SeesLiveEnemies_ClearedRoom_AndTheOpenedKeyChest()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            ulong hostId = hostNm.LocalClientId;
            int floorNo = PiecefulFloor();
            uint seed = FindSeed(floorNo, out int carrierRoom, out int otherRoom);

            CellarFloorSync floor = Spawn<CellarFloorSync>(hostNm); DiveHaulSync haul = Spawn<DiveHaulSync>(hostNm); CellarContentsSync contents = Spawn<CellarContentsSync>(hostNm);
            contents.FloorSync = floor; contents.HaulSync = haul;
            Vector3 hostAt = default;
            contents.PlayersOverride = list => list.Add(new CellarContentsSync.PlayerPos { ClientId = hostId, Position = hostAt });
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetFloor(floorNo), Is.True);
            Assert.That(floor.ServerSetFloor(seed, floorNo), Is.True);
            yield return PlayModeHarness.WaitUntil(() => floor.Built != null && floor.BuiltFloor == floorNo && contents.State.Floor == floorNo, Timeout, "the host never built and armed the floor");
            CellarFloorLayout layout = floor.Layout;

            // Room 2 first: enter it, then kill what spawned there: the room is cleared. (Entered before the carrier room so the carrier room's enemies are never chased through it.)
            hostAt = RoomCentre(floor, layout, otherRoom);
            yield return PlayModeHarness.WaitUntil(() => contents.EnterEnemyCount(otherRoom) > 0, Timeout, "the second room never spawned its enemies");
            foreach (Enemy e in EnemiesOn(hostNm)) Hit(e, 100000f, 8);
            yield return PlayModeHarness.WaitUntil(() => (contents.State.Cleared & (1UL << otherRoom)) != 0, Timeout, "the second room never cleared on the host");

            // The carrier room: enemies spawn, one is hurt and one dies (not the carrier: its death would drop the gate key).
            var before = new HashSet<ulong>(); foreach (Enemy e in EnemiesOn(hostNm)) before.Add(e.NetworkObjectId);
            hostAt = RoomCentre(floor, layout, carrierRoom);
            yield return PlayModeHarness.WaitUntil(() => contents.EnterEnemyCount(carrierRoom) >= 1 && contents.GateKeyCarrierEnemy != null, Timeout, "the carrier room never spawned its enemies");
            var room = new List<Enemy>(); foreach (Enemy e in EnemiesOn(hostNm)) if (!before.Contains(e.NetworkObjectId) && e.Alive) room.Add(e);
            Assert.That(room.Count, Is.GreaterThanOrEqualTo(1), "premise: a live enemy in the carrier room");
            Enemy hurt = null, dead = null;
            foreach (Enemy e in room) if (e != contents.GateKeyCarrierEnemy) { if (hurt == null) hurt = e; else if (dead == null) dead = e; }
            if (hurt == null) hurt = contents.GateKeyCarrierEnemy;   // a room of the carrier and one mate: the mate is hurt, the carrier lives
            Hit(hurt, 5f, 1);
            Assert.That(hurt.Alive && hurt.NetHp.Value < hurt.NetMaxHp.Value, Is.True, "premise: one enemy is hurt and alive (hp " + hurt.NetHp.Value + "/" + hurt.NetMaxHp.Value + ")");
            if (dead != null) { Hit(dead, 100000f, 8); Assert.That(dead.Alive, Is.False, "premise: one enemy is dead"); }

            // The key chest opens for the party (the one chest that is shared state).
            Assert.That(contents.TryChestPosition(layout.KeyChestIndex, out Vector3 keyAt), Is.True);
            hostAt = keyAt + new Vector3(1f, 0f, 0f);
            yield return PlayModeHarness.WaitUntil(() => contents.ServerTryOpenChest(hostId, layout.KeyChestIndex) || (contents.State.Chests & (1UL << layout.KeyChestIndex)) != 0, Timeout, "the key chest never opened on the host");
            Assert.That(contents.State.RoomKeyHeld, Is.True);
            yield return Seconds(0.3f);   // the host's own state settles (a second death tick / despawn)
            var hostEnemies = new Dictionary<ulong, (int hp, int max, byte kind, bool alive)>();
            foreach (Enemy e in EnemiesOn(hostNm)) hostEnemies[e.NetworkObjectId] = (e.NetHp.Value, e.NetMaxHp.Value, e.NetKind.Value, e.Alive);
            Assert.That(hostEnemies.Count, Is.GreaterThanOrEqualTo(1), "premise: enemies exist on the host before anyone joins");

            // ---- the late joiner ----
            var clients = new List<PlayModeHarness.TestClient>();
            yield return PlayModeHarness.StartClients(app, 1, false, clients, 8);
            NetworkManager cnm = clients[0].NetworkManager;
            ulong cid = contents.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => cnm.SpawnManager.SpawnedObjects.ContainsKey(cid) && cnm.SpawnManager.SpawnedObjects.ContainsKey(floor.NetworkObjectId), Timeout, "the late client never received the sync objects");
            CellarContentsSync peer = cnm.SpawnManager.SpawnedObjects[cid].GetComponent<CellarContentsSync>();
            CellarFloorSync peerFloor = cnm.SpawnManager.SpawnedObjects[floor.NetworkObjectId].GetComponent<CellarFloorSync>();
            peer.FloorSync = peerFloor;   // one process holds both peers' syncs: pin the client's own so it never reads the host's

            yield return PlayModeHarness.WaitUntil(() => peerFloor.Built != null && peerFloor.BuiltFloor == floorNo, Timeout, "the late client never built the floor");
            yield return PlayModeHarness.WaitUntil(() => peer.State.Floor == floorNo && peer.State.Seed == seed, Timeout, "the late client never got this floor's contents state");
            Assert.That((peer.State.Cleared & (1UL << otherRoom)) != 0, Is.True, "the cleared room is cleared for the late joiner");
            Assert.That((peer.State.Cleared & (1UL << carrierRoom)) == 0, Is.True, "the room with live enemies is not cleared");
            Assert.That((peer.State.Chests & (1UL << layout.KeyChestIndex)) != 0 && peer.State.RoomKeyHeld, Is.True, "the opened key chest and the held room key reach the late joiner");
            Assert.That(peer.State.DoorOpen, Is.False, "the door was never opened");
            yield return PlayModeHarness.WaitUntil(() => peer.ChestShownOpen(layout.KeyChestIndex), Timeout, "the late client built the floor but its key chest is not lowered");
            Assert.That(peer.State.Equals(contents.State), Is.True, "the whole contents state equals the host's");

            yield return PlayModeHarness.WaitUntil(() => EnemiesOn(cnm).Count == hostEnemies.Count, Timeout, "the late client sees " + EnemiesOn(cnm).Count + " enemies, the host has " + hostEnemies.Count);
            foreach (Enemy e in EnemiesOn(cnm))
            {
                Enemy peerEnemy = e;
                Assert.That(hostEnemies.ContainsKey(peerEnemy.NetworkObjectId), Is.True, "the late client has an enemy the host does not (id " + peerEnemy.NetworkObjectId + ")");
                var h = hostEnemies[peerEnemy.NetworkObjectId];
                yield return PlayModeHarness.WaitUntil(() => peerEnemy.NetHp.Value == h.hp && peerEnemy.NetMaxHp.Value == h.max && peerEnemy.NetKind.Value == h.kind && peerEnemy.Alive == h.alive, Timeout,
                    "enemy " + peerEnemy.NetworkObjectId + " differs on the late client: hp " + peerEnemy.NetHp.Value + "/" + peerEnemy.NetMaxHp.Value + " kind " + peerEnemy.NetKind.Value + " alive " + peerEnemy.Alive + ", host hp " + h.hp + "/" + h.max + " kind " + h.kind + " alive " + h.alive);
            }
            bool sawHurtLive = false;
            foreach (Enemy e in EnemiesOn(cnm)) if (e.Alive && e.NetHp.Value < e.NetMaxHp.Value) sawHurtLive = true;
            Assert.That(sawHurtLive, Is.True, "the late client sees the hurt enemy hurt, not at full health");
        }

        // ================================================================ L2

        private static uint SeedWithPointOnFloor1()
        {
            for (uint seed = 1; seed < 500; seed++)
            {
                CellarFloorLayout l = CellarFloorGenerator.Layout(seed, 1);
                if (CellarHealing.PointFor(l, CellarFloorDressing.Resolve(l)).HasValue) return seed;
            }
            Assert.Fail("no seed below 500 has a healing point on floor 1"); return 0;
        }

        private static void Teleport(NetworkObject hero, Vector3 pos)
        {
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        [UnityTest]
        public IEnumerator L2_LateJoiner_SeesTheUsedHealingPoint_Recharging_AndItsOrbDark()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            uint seed = SeedWithPointOnFloor1();
            CellarFloorSync floor = Spawn<CellarFloorSync>(hostNm); DiveHaulSync haul = Spawn<DiveHaulSync>(hostNm);
            haul.FloorSync = floor;
            const float Recharge = 60f;   // long: still recharging when the client has joined
            Assert.That(haul.ServerBegin(), Is.True);
            Assert.That(haul.ServerSetHealTuning(new HealTuning { ChancePercent = 51, RechargeSeconds = Recharge, RadiusMeters = 1.6f }), Is.True);
            Assert.That(haul.ServerSetFloor(1), Is.True);
            Assert.That(floor.ServerSetFloor(seed, 1), Is.True);
            yield return PlayModeHarness.WaitUntil(() => floor.Built != null && floor.BuiltFloor == 1, Timeout, "the host never built the floor");
            Assert.That(floor.Built.HealingCell.HasValue, Is.True, "the floor has its healing point");
            CellPos hc = floor.Built.HealingCell.Value;
            Vector3 point = floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(hc.X, hc.Y));
            Assert.That(floor.Built.HealingOrb.activeSelf, Is.True, "ready: the orb shows");

            NetworkObject hero = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, point + new Vector3(60f, 0f, 60f), Quaternion.identity);
            HeroVitals v = hero.GetComponent<HeroVitals>();
            yield return Seconds(0.5f);
            v.ServerDamage(40);
            double until = Time.realtimeSinceStartupAsDouble + Timeout;
            while (v.Hp < v.MaxHp)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("the point never healed the hero standing on it");
                Teleport(hero, point);
                yield return null;
            }
            Assert.That(haul.NetHealSecondsLeft.Value, Is.GreaterThan(0), "the host's point is used and recharging");
            Assert.That(floor.Built.HealingOrb.activeSelf, Is.False, "the host's orb is dark");

            // ---- the late joiner ----
            var clients = new List<PlayModeHarness.TestClient>();
            yield return PlayModeHarness.StartClients(app, 1, false, clients, 8);
            NetworkManager cnm = clients[0].NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => cnm.SpawnManager.SpawnedObjects.ContainsKey(floor.NetworkObjectId) && cnm.SpawnManager.SpawnedObjects.ContainsKey(haul.NetworkObjectId), Timeout, "the late client never received the sync objects");
            DiveHaulSync peerHaul = cnm.SpawnManager.SpawnedObjects[haul.NetworkObjectId].GetComponent<DiveHaulSync>();
            CellarFloorSync peerFloor = cnm.SpawnManager.SpawnedObjects[floor.NetworkObjectId].GetComponent<CellarFloorSync>();
            peerHaul.FloorSync = peerFloor;   // one process holds both peers' floor syncs: pin the client's so it never finds the host's
            yield return PlayModeHarness.WaitUntil(() => peerFloor.Built != null && peerFloor.BuiltFloor == 1, Timeout, "the late client never built the floor");
            Assert.That(peerFloor.Built.HealingCell.HasValue, Is.True, "the late client built the same point");
            yield return PlayModeHarness.WaitUntil(() => peerHaul.NetHealSecondsLeft.Value > 0, Timeout, "the late client never saw the recharge (seconds left stayed " + peerHaul.NetHealSecondsLeft.Value + ")");
            Assert.That(peerHaul.NetHealSecondsLeft.Value, Is.InRange(1, Mathf.CeilToInt(Recharge)), "a whole number of seconds that fits the recharge");
            Assert.That(Vector3.Distance(peerFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(hc.X, hc.Y)), point), Is.LessThan(0.01f), "the late client's built point is at the same world position");
            yield return PlayModeHarness.WaitUntil(() => !peerFloor.Built.HealingOrb.activeSelf, Timeout, "the late client's own orb stayed lit while the point recharges");
        }
    }
}
