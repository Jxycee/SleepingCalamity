// Authority: test-only. M6 S6 (automated part) QA walk of the Hollowmere square loop (docs/design/m6-square-loop-proposal.md sections 1 and 4): a host + one client walk it end to end through the real
// Hollowmere scene (SceneFlow, DiveHub, DiveDirector, CellarFloorSync, DiveHaulSync, CellarContentsSync, TownSync, BakerService, HostWorld and WorldSaveFlow against a throwaway world library).
// The CLIENT hero drives: owner-side moves plus client RPCs (RequestDescend / RequestStair / RequestBank / RequestGoHome / RequestOpenChest / TownSync.Request*), so every step takes the same
// server-side entry point a client RPC reaches. The host side only kills enemies through the same ServerApplyHit path a swing uses.
// Steps (every assertion message starts with its tag, e.g. "[S3 bank]"):
//   S1 start in Hollowmere from a save (loot in the town stock, baker not yet met, weapon growth restored)   S2 dive from the cistern stair, pieces on floors 1-3, bank at floor 2, bank at the floor-4 heart
//   S3 go home: haul into the town stock (pieces, loot, baker), stir reset   S4 place the bakery frame (a path-blocking turn is refused) and its parts from stock   S5 place the baker, three live wishes, service
//   S6 next dive: every player gets 3 loaves   S7 save mid-dive, leave, reload the same world: stock, town, residents, dive section and weapons survive.
// A failure that is a GAME bug is left failing; do not weaken it here.
// NOT covered by a one-process test: the interact / placement KEYS, prompts, the placement camera and wish panel on screen, the look of the town, a client that loads the Hollowmere scene itself (one
// process cannot load it twice: the client declines scenes and its hero is spawned by the test), real relay latency, real melee combat (growth is added through the CombatHost growth path a kill ends in).
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public class SquareLoopWalkQaTests
    {
        private const float Timeout = 40f;
        private const int SeedLootItem = 41, SeedLootCount = 2;
        private static readonly string Axe = AttackCatalog.Heirlooms[0];
        private string _dir;
        private PlayModeHarness.TestClient _client;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private sealed class Rig
        {
            public AppBootstrap App; public NetworkManager HostNm, ClientNm;
            public DiveDirector Host, Peer; public TownSync HostTown, PeerTown;
            public NetworkObject HostHero, ClientHeroOnHost, ClientHero;
            public CellarFloorSync HostFloor, PeerFloor; public DiveHaulSync HostHaul, PeerHaul;
            public CellarContentsSync Contents, PeerContents;
            public Transform StairTop;
        }

        // ---------------------------------------------------------------- session: host loads the real Hollowmere, one client joins (declining scenes) and gets its hero

        private IEnumerator StartSession(System.Action<Rig> done)
        {
            AppBootstrap app = AppBootstrap.Current;
            SceneFlow.StartSceneOverride = null;   // the real default: the town
            yield return PlayModeHarness.StartDirectHostWithScenes();
            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "[S1 start] the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True, "[S1 start] the multiplayer start gate opens with two players");
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && app.NetworkManager.LocalClient.PlayerObject != null, Timeout, "[S1 start] the host never loaded Hollowmere with its player");

            var rig = new Rig { App = app, HostNm = app.NetworkManager, ClientNm = _client.NetworkManager };
            Transform spawn = Find("SpawnPoint");
            Assert.That(spawn != null && Find("DiveEntrance") != null && Find("StairTop") != null, Is.True, "[S1 start] Hollowmere has its spawn point and the cistern stair markers");
            rig.StairTop = Find("StairTop");
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.IsConnectedClient, Timeout, "[S1 start] the client lost its connection");
            rig.HostHero = rig.HostNm.LocalClient.PlayerObject;
            rig.ClientHeroOnHost = rig.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, rig.ClientNm.LocalClientId, false, true, false, spawn.position + new Vector3(1.5f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => rig.ClientNm.LocalClient != null && rig.ClientNm.LocalClient.PlayerObject != null, Timeout, "[S1 start] the client never received its hero");
            rig.ClientHero = rig.ClientNm.LocalClient.PlayerObject;

            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero && DiveDirector.Instance.TownSync != null && DiveDirector.Instance.TownSync.IsSpawned, Timeout, "[S1 start] the hub never spawned the director and its TownSync");
            rig.Host = DiveDirector.Instance; rig.HostTown = rig.Host.TownSync;
            var objs = rig.ClientNm.SpawnManager.SpawnedObjects;
            ulong did = rig.Host.NetworkObjectId, tid = rig.HostTown.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => objs.ContainsKey(did) && objs.ContainsKey(tid), Timeout, "[S1 start] the client never received the director and the TownSync");
            rig.Peer = objs[did].GetComponent<DiveDirector>(); rig.PeerTown = objs[tid].GetComponent<TownSync>();
            // The scene's real PlacementMode hands back a lock it is not using (the late-grant fix); this walk drives the placement requests itself.
            foreach (PlacementMode mode in Object.FindObjectsByType<PlacementMode>(FindObjectsInactive.Include)) mode.enabled = false;
            yield return Seconds(1f);   // the arrival teleports have landed before the test moves a hero
            done(rig);
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeep(root.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { Transform r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        private static IEnumerator ResolvePeers(Rig rig)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.Host.FloorSync != null && rig.Host.HaulSync != null && rig.Host.ContentsSync != null, Timeout, "[S2 dive] the dive never spawned its floor/haul/contents syncs");
            rig.HostFloor = rig.Host.FloorSync; rig.HostHaul = rig.Host.HaulSync; rig.Contents = rig.Host.ContentsSync;
            var objs = rig.ClientNm.SpawnManager.SpawnedObjects;
            yield return PlayModeHarness.WaitUntil(() => objs.ContainsKey(rig.HostFloor.NetworkObjectId) && objs.ContainsKey(rig.HostHaul.NetworkObjectId) && objs.ContainsKey(rig.Contents.NetworkObjectId), Timeout, "[S2 dive] the client never received the dive syncs");
            rig.PeerFloor = objs[rig.HostFloor.NetworkObjectId].GetComponent<CellarFloorSync>(); rig.PeerHaul = objs[rig.HostHaul.NetworkObjectId].GetComponent<DiveHaulSync>(); rig.PeerContents = objs[rig.Contents.NetworkObjectId].GetComponent<CellarContentsSync>();
        }

        // ---------------------------------------------------------------- walk helpers (the DiveWalkQaTests ones, over the Hollowmere hub)

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

        private static IEnumerator ClientTo(Rig rig, Vector3 pos, string step)
        {
            Move(rig.ClientHero, pos);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, pos) < 1.5f, Timeout, step + ": the client hero never arrived on the host at " + pos + " (host copy at " + rig.ClientHeroOnHost.transform.position + ")");
        }

        private static IEnumerator AllBuilt(Rig rig, int floor, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.HostFloor.BuiltFloor == floor && rig.HostFloor.Built != null, Timeout, step + ": the host never built floor " + floor);
            yield return PlayModeHarness.WaitUntil(() => rig.PeerFloor.BuiltFloor == floor && rig.PeerFloor.Built != null, Timeout, step + ": the client never built floor " + floor);
            Assert.That(rig.PeerFloor.BuiltLayoutHash, Is.EqualTo(rig.HostFloor.BuiltLayoutHash), step + ": both peers build the identical floor " + floor);
            yield return PlayModeHarness.WaitUntil(() => rig.Contents.State.Floor == floor && rig.Contents.State.Seed == rig.HostFloor.BuiltSeed, Timeout, step + ": the director never armed the contents for floor " + floor);
            yield return PlayModeHarness.WaitUntil(() => rig.PeerContents.State.Floor == floor, Timeout, step + ": the client never saw the contents state for floor " + floor);
        }

        private static IEnumerator BothAtEntry(Rig rig, string step)
        {
            Vector3 entry = CellarFloorBuilder.EntryWorld(rig.HostFloor.Layout, rig.HostFloor.Built, 0);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, entry) < 6f && rig.HostHero.transform.position.y < AreaLook.Current.CellarBelowY, Timeout, step + ": the host hero is at " + rig.HostHero.transform.position + ", wanted near the entry " + entry);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, entry) < 6f, Timeout, step + ": the client hero is at " + rig.ClientHeroOnHost.transform.position + " on the host, wanted near the entry " + entry);
            yield return Seconds(1f);   // late arrival teleports must have landed before the test moves a hero
        }

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

        private static uint FindSeed()
        {
            for (uint sd = 1; sd < 2500; sd++)
            {
                CellarFloorLayout l1 = CellarFloorGenerator.Generate(sd, 1);
                if (l1.Carrier == null) continue;
                if (CellarFloorGenerator.Generate(sd, 2).Carrier == null || CellarFloorGenerator.Generate(sd, 3).Carrier == null) continue;
                return sd;
            }
            return 0;
        }

        private IEnumerator TakeGateKey(Rig rig, int floor)
        {
            string step = "[S2 F" + floor + " gate key]";
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
        }

        // Walks onto every piece spot of the floor (the client drives); the pieces-left counter must reach 0 on both peers.
        private IEnumerator CollectPieces(Rig rig, int floor)
        {
            string step = "[S2 F" + floor + " pieces]";
            int assigned = rig.HostHaul.Current.Left(floor);
            Assert.That(assigned, Is.GreaterThan(0), step + ": the floor has pieces to find");
            Assert.That(rig.Contents.PieceCount, Is.EqualTo(assigned), step + ": one piece spot per piece the haul assigned to the floor");
            for (int i = 0; i < rig.Contents.PieceCount; i++)
            {
                Assert.That(rig.Contents.TryPiecePosition(i, out Vector3 pos), Is.True, step + ": piece spot " + i + " exists");
                yield return ClientTo(rig, pos, step + " walk to piece " + i);
                int want = assigned - (i + 1);
                yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.Left(floor) == want, Timeout, step + ": walking onto piece " + i + " never took it (left " + rig.HostHaul.Current.Left(floor) + ", wanted " + want + ")");
                KillAll(rig.HostNm);
            }
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.Left(floor) == 0, Timeout, step + ": the client never saw the floor's pieces all taken");
        }

        private IEnumerator Descend(Rig rig, int from)
        {
            string step = "[S2 F" + from + " stair]";
            Assert.That(CellarFloorBuilder.TryStairWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 stair), Is.True, step + ": the floor has a stair");
            KillAll(rig.HostNm);
            yield return ClientTo(rig, stair + new Vector3(1f, 0.1f, 0f), step + " walk to the stair");
            rig.Peer.RequestStair(from);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CurrentFloor == from + 1, Timeout, step + ": the opened stair never took the party down to " + (from + 1));
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.CurrentFloor == from + 1, Timeout, step + ": the client never saw floor " + (from + 1));
            yield return AllBuilt(rig, from + 1, "[S2 F" + (from + 1) + " arrive]");
            yield return BothAtEntry(rig, "[S2 F" + (from + 1) + " arrive]");
        }

        // The client banks at the floor's bank point (checkpoint on 2, the heart on 4).
        private IEnumerator ClientBanks(Rig rig, string step)
        {
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True, step + ": the floor has a bank point");
            KillAll(rig.HostNm);
            yield return ClientTo(rig, bank + new Vector3(0.5f, 0.1f, 0f), step + " walk to the bank");
            rig.PeerHaul.RequestBank();
        }

        // ---------------------------------------------------------------- town / save helpers

        private static string Sig(TownState s) => TownWire.From(s).Signature();

        private static IEnumerator TakeLock(TownSync who, TownSync host, string step)
        {
            who.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => who.IsBuilder && host.NetBuilder.Value == who.NetworkManager.LocalClientId, Timeout, step + ": the builder lock was never granted");
        }

        private static string Pieces(TownState s) { var l = new List<int>(s.StockPieces); return string.Join(",", l); }

        // The seed world: loot in the town stock and weapon growth for both players (the host's local id and the guest's), written to the library before the session opens it.
        private static (int level, float abs, float guestAbs) SeedWorld(AppBootstrap app, out string worldId)
        {
            worldId = app.WorldSaveFlow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local);
            Assert.That(worldId, Is.Not.Null, "[S1 start] a multiplayer world was made");
            WorldSaveStore store = app.WorldSaveFlow.Library.Open(worldId);
            Assert.That(store, Is.Not.Null, "[S1 start] the seed world opens");
            store.Current.Stock = new StockSave();
            store.Current.Stock.Loot.Add(new LootCountSave { ItemId = SeedLootItem, Count = SeedLootCount });
            var weapons = new WeaponsSave();
            string hostId = PlayerKey.Local(app.Services.Get<ISettings>().EnsureLocalPlayerId()).Id, guestId = PlayerKey.Local(PlayModeHarness.PlayerId(8)).Id;
            var host = new PlayerGrowth(hostId); host.Weapon(Axe).AddAbs(40f);
            var guest = new PlayerGrowth(guestId); guest.Weapon(Axe).AddAbs(5f);
            GrowthSaves.Capture(host, hostId, weapons); GrowthSaves.Capture(guest, guestId, weapons);
            store.Current.Weapons = weapons;
            store.MarkDirty();
            Assert.That(store.Save(), Is.True, "[S1 start] the seed world was written");
            return (host.Weapon(Axe).Level, host.Weapon(Axe).Abs, guest.Weapon(Axe).Abs);
        }

        private static IEnumerator WaitCombat(string step)
        {
            yield return PlayModeHarness.WaitUntil(() => CombatHost.Current != null && CombatHost.Current.Begun, Timeout, step + ": the dive's contents never started a CombatHost");
        }

        // ---------------------------------------------------------------------------------------------------------------------------------

        [UnityTest, Timeout(1500000)]
        public IEnumerator Walk_Hollowmere_Dive_Rescue_GoHome_Place_Baker_Loaves_SaveReload([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            uint seed = FindSeed();
            Assert.That(seed, Is.Not.EqualTo(0u), "[setup] a seed with a gate-key carrier on floors 1-3");
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            PlayModeHarness.AddNetworkSimulator(net);   // before the host starts
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;
            (int seedLevel, float seedAbs, float seedGuestAbs) = SeedWorld(app, out string worldId);

            // ======== S1 start in Hollowmere from a save
            Rig rig = null;
            yield return StartSession(r => rig = r);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[S1 start] the session opened the seeded world");
            Assert.That(rig.Host.Town.LootCount(SeedLootItem), Is.EqualTo(SeedLootCount), "[S1 start] the saved loot is in the town stock");
            Assert.That(Pieces(rig.Host.Town.State), Is.Empty, "[S1 start] no bakery piece is home yet");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.False, "[S1 start] the baker is not rescued yet");
            Assert.That(rig.Host.BakerMet, Is.False, "[S1 start] the baker has not been met");
            Assert.That(rig.PeerTown.NetBakerMet.Value, Is.False, "[S1 start] the client sees the baker unmet");
            Assert.That(rig.PeerTown.BuilderIsFree, Is.True, "[S1 start] nobody holds the builder lock");
            Assert.That(Sig(rig.PeerTown.View), Is.EqualTo(Sig(rig.Host.Town.State)), "[S1 start] the client's town view equals the host's");

            // ======== S2 dive from the cistern stair, collect pieces on floors 1-3, bank at floor 2, reach the floor-4 heart
            rig.Host.SeedOverride = seed;
            yield return ClientTo(rig, rig.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f), "[S2 dive] walk to the cistern stair");
            rig.Peer.RequestDescend();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetActive.Value && rig.Peer.NetActive.Value, Timeout, "[S2 dive] the client's descend request never started the dive on both peers");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, 1, "[S2 F1 arrive]");
            yield return BothAtEntry(rig, "[S2 F1 arrive]");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(2), "[S2 F1 arrive] both players are in the dive");
            ulong hostId = rig.HostNm.LocalClientId, guestId = rig.ClientNm.LocalClientId;
            Assert.That(rig.Contents.State.ExitUnlocked, Is.False, "[S2 F1 arrive] the exit starts shut when the floor has a carrier");

            // F1: gate key (the carrier's room starts the CombatHost, whose growth is restored from the save), a chest (loot), both pieces.
            yield return TakeGateKey(rig, 1);
            yield return WaitCombat("[S1 weapons]");
            WeaponState hostAxe = CombatHost.Current.Growth(hostId).Weapon(Axe);
            Assert.That((hostAxe.Level, hostAxe.Abs), Is.EqualTo((seedLevel, seedAbs)), "[S1 weapons] the host's saved weapon growth was restored");
            Assert.That(CombatHost.Current.Growth(guestId).Weapon(Axe).Abs, Is.EqualTo(seedGuestAbs), "[S1 weapons] the guest's saved weapon growth was restored under its new client id");
            int chest = -1;
            for (int i = 0; i < rig.HostFloor.Layout.Chests.Count && chest < 0; i++) if (!rig.HostFloor.Layout.Chests[i].HoldsKey && i < 64) chest = i;
            Assert.That(chest, Is.GreaterThanOrEqualTo(0), "[S2 F1 chest] the floor has an ordinary chest");
            Assert.That(rig.Contents.TryChestPosition(chest, out Vector3 chestPos), Is.True, "[S2 F1 chest] the chest object exists");
            yield return ClientTo(rig, chestPos + new Vector3(0.5f, 0.1f, 0f), "[S2 F1 chest walk]");
            KillAll(rig.HostNm);
            int loot0 = rig.HostHaul.Current.UnbankedLoot;
            rig.PeerContents.RequestOpenChest(chest);
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.UnbankedLoot > loot0, Timeout, "[S2 F1 chest] the client's chest never put loot in the shared haul");
            int chestLoot = rig.HostHaul.Current.UnbankedLoot - loot0;
            yield return CollectPieces(rig, 1);
            Assert.That(rig.PeerHaul.Current.UnbankedPieces, Is.EqualTo(2), "[S2 F1 pieces] both floor-1 pieces are carried unbanked on the client too");

            // F2: pieces, then the checkpoint bank (pieces and loot become safe).
            yield return Descend(rig, 1);
            yield return TakeGateKey(rig, 2);
            yield return CollectPieces(rig, 2);
            Assert.That(rig.HostHaul.Current.StirValue, Is.GreaterThan(0), "[S2 F2 pieces] carrying four pieces raises the stir meter");
            yield return ClientBanks(rig, "[S2 F2 bank]");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.BankedPieces == 4 && rig.HostHaul.Current.UnbankedPieces == 0, Timeout, "[S2 F2 bank] the client's bank never banked the four carried pieces (banked " + rig.HostHaul.Current.BankedPieces + ", unbanked " + rig.HostHaul.Current.UnbankedPieces + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.CheckpointReached && rig.Peer.NetCheckpoint.Value, Timeout, "[S2 F2 bank] the checkpoint never reached the client");
            Assert.That(rig.HostHaul.Current.UnbankedLoot, Is.EqualTo(0), "[S2 F2 bank] the chest loot was banked too");
            // Weapon growth earned on the dive (what a kill's reward ends in): the save must keep it.
            yield return WaitCombat("[S2 weapons]");
            CombatHost combat = CombatHost.Current;
            WeaponState earned = combat.Growth(hostId).Weapon(Axe);
            earned.AddAbs(25f);
            (int earnedLevel, float earnedAbs) = (earned.Level, earned.Abs);
            combat.Growth(guestId).Weapon(Axe).AddAbs(7f);
            float guestEarned = combat.Growth(guestId).Weapon(Axe).Abs;

            // F3: pieces, then down to the heart.
            yield return Descend(rig, 2);
            yield return TakeGateKey(rig, 3);
            yield return CollectPieces(rig, 3);
            Assert.That(rig.HostHaul.Current.StirValue, Is.GreaterThan(0), "[S2 F3 pieces] carrying the floor-3 pieces raises the stir meter");
            yield return Descend(rig, 3);
            Assert.That(rig.HostFloor.Layout.Floor, Is.EqualTo(4), "[S2 F4 arrive] the heart");

            // F4: bank at the heart -> the baker is rescued (a resident in the haul); going home needs that bank first.
            rig.Peer.RequestGoHome();
            int handled = rig.Host.RequestsHandled;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[S2 F4 early go-home] the press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "[S2 F4 early go-home] there is no way home before the heart bank");
            yield return ClientBanks(rig, "[S2 F4 heart bank]");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetHeartBanked.Value && rig.Peer.NetHeartBanked.Value, Timeout, "[S2 F4 heart bank] the heart bank never landed on both peers");
            Assert.That(rig.HostHaul.Current.BankedPieces, Is.EqualTo(6), "[S2 F4 heart bank] all six pieces are banked");
            Assert.That(rig.Host.CaptureTown().Dive.BankedResidents, Does.Contain(TownBuildings.BakerResident), "[S2 F4 heart bank] the baker is a resident in the haul");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.False, "[S2 F4 heart bank] he is not home until the party is");

            // ======== S3 go home: the haul moves into the town stock, the stir resets
            rig.Peer.RequestGoHome();
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[S3 home] the dive never ended on both peers");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, rig.StairTop.position) < 4f && Flat(rig.ClientHeroOnHost.transform.position, rig.StairTop.position) < 4f
                && rig.HostHero.transform.position.y > AreaLook.Current.CellarBelowY && rig.ClientHeroOnHost.transform.position.y > AreaLook.Current.CellarBelowY, Timeout,
                "[S3 home] the party never landed at the stair top (host " + rig.HostHero.transform.position + ", client " + rig.ClientHeroOnHost.transform.position + ")");
            TownStock stock = rig.Host.Town;
            Assert.That(Pieces(stock.State), Is.EqualTo("100,101,200,201,300,301"), "[S3 stock] the six banked pieces are in the town stock");
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "[S3 stock] the baker is in the town stock");
            Assert.That(stock.LootCount(SeedLootItem), Is.EqualTo(SeedLootCount), "[S3 stock] the saved loot is untouched");
            Assert.That(stock.TotalLoot, Is.EqualTo(SeedLootCount + chestLoot), "[S3 stock] the chest loot came home exactly once");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.StirValue == 0 && rig.PeerHaul.Current.StirValue == 0, Timeout, "[S3 stir] the stir meter never reset (host " + rig.HostHaul.Current.StirValue + ")");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerTown.View.InStock(100) && rig.PeerTown.View.ResidentInStock(TownBuildings.BakerResident), Timeout, "[S3 stock] the client never saw the new stock");
            Assert.That(Sig(rig.PeerTown.View), Is.EqualTo(Sig(stock.State)), "[S3 stock] the client's town view equals the host's");

            // ======== S4 place the bakery frame from stock (client request); a path-blocking turn is refused; the parts follow
            SortedSet<string> conserved = stock.State.Conservation();
            TownSync peerTown = rig.PeerTown;
            var results = new List<(TownSync.Op op, byte res)>();
            peerTown.ResultReceived += (op, res) => results.Add((op, res));
            yield return TakeLock(peerTown, rig.HostTown, "[S4 lock]");
            peerTown.RequestPlaceFrame(TownBuildings.Bakery, "F02", 2);
            yield return PlayModeHarness.WaitUntil(() => results.Exists(x => x.op == TownSync.Op.PlaceFrame), Timeout, "[S4 refuse] no answer to the path-blocking frame");
            Assert.That(results.Find(x => x.op == TownSync.Op.PlaceFrame).res, Is.EqualTo((byte)PlaceResult.PathBlocked), "[S4 refuse] the host refuses a frame that cuts the path graph");
            Assert.That(stock.State.PlacedBuildingOf(TownBuildings.Bakery), Is.Null, "[S4 refuse] nothing was placed");
            Assert.That(stock.State.InStock(100) && stock.State.InStock(300), Is.True, "[S4 refuse] both frames stayed in stock");
            results.Clear();
            peerTown.RequestPlaceFrame(TownBuildings.Bakery, "F02", 0);
            yield return PlayModeHarness.WaitUntil(() => stock.State.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "[S4 frame] the host never placed the legal frame");
            yield return PlayModeHarness.WaitUntil(() => peerTown.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "[S4 frame] the client never saw the frame");
            Assert.That(results.Find(x => x.op == TownSync.Op.PlaceFrame).res, Is.EqualTo((byte)PlaceResult.Ok), "[S4 frame] the host answered Ok");
            foreach (string part in new[] { "oven", "door", "sign" })
            {
                peerTown.RequestPlacePart(TownBuildings.Bakery, part);
                int idx = TownBuildings.Get(TownBuildings.Bakery).PartIndex(part);
                yield return PlayModeHarness.WaitUntil(() => peerTown.View.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx] >= 0, Timeout, "[S4 part] the client never saw the " + part + " land");
            }
            Assert.That(stock.State.IsComplete(TownBuildings.Bakery) && peerTown.View.IsComplete(TownBuildings.Bakery), Is.True, "[S4 parts] the bakery is complete on host and client");
            Assert.That(Pieces(stock.State), Is.EqualTo("300,301"), "[S4 parts] the spare frame and oven stay in stock");
            Assert.That(stock.State.Conservation(), Is.EqualTo(conserved), "[S4 parts] placing lost and made nothing");
            Assert.That(Sig(peerTown.View), Is.EqualTo(Sig(stock.State)), "[S4 parts] host and client towns agree");

            // ======== S5 the baker moves in; the three wishes tick live; the service opens when all are met
            Assert.That(BakerService.Evaluate(stock.State).PerPlayer, Is.EqualTo(0), "[S5 baker] no loaves while the baker is not housed");
            peerTown.RequestPlaceResident(TownBuildings.Bakery);
            yield return PlayModeHarness.WaitUntil(() => peerTown.View.PlacedBuildingOf(TownBuildings.Bakery).Resident == TownBuildings.BakerResident && !peerTown.View.ResidentInStock(TownBuildings.BakerResident), Timeout, "[S5 baker] the baker never moved in on the client");
            Assert.That(stock.State.PlacedBuildingOf(TownBuildings.Bakery).Resident, Is.EqualTo(TownBuildings.BakerResident), "[S5 baker] and on the host");
            WishReport wishes = peerTown.View.Wishes(TownBuildings.Bakery);
            Assert.That(wishes.Count, Is.EqualTo(3), "[S5 wishes] the baker has three wishes");
            Assert.That(wishes.AllMet, Is.True, "[S5 wishes] the lot's own turn meets all three (" + wishes.MetCount + "/3)");
            Assert.That(BakerService.Evaluate(peerTown.View).Granted, Is.True, "[S5 service] the service is open when every wish is met");
            // Live: turning the door away un-ticks a wish on the client at once, turning back ticks it again.
            peerTown.RequestSetTurn(TownBuildings.Bakery, 1);
            yield return PlayModeHarness.WaitUntil(() => peerTown.View.PlacedBuildingOf(TownBuildings.Bakery).Turn == 1, Timeout, "[S5 wishes] the client never saw the turn");
            Assert.That(peerTown.View.Wishes(TownBuildings.Bakery).MetCount, Is.LessThan(3), "[S5 wishes] a door turned away from the square un-ticks a wish");
            Assert.That(BakerService.Evaluate(peerTown.View).Granted, Is.False, "[S5 service] the service closes while a wish is unmet");
            peerTown.RequestSetTurn(TownBuildings.Bakery, 0);
            yield return PlayModeHarness.WaitUntil(() => peerTown.View.PlacedBuildingOf(TownBuildings.Bakery).Turn == 0, Timeout, "[S5 wishes] the client never saw the turn back");
            Assert.That(peerTown.View.Wishes(TownBuildings.Bakery).AllMet, Is.True, "[S5 wishes] turned back, all three tick again");
            Assert.That(BakerService.Evaluate(stock.State).Granted, Is.True, "[S5 service] the host's service is open");
            yield return PlayModeHarness.WaitUntil(() => rig.Host.BakerMet && peerTown.NetBakerMet.Value, Timeout, "[S5 baker] the first-meet latch never reached the host and the client");
            HostWorld world = null;
            Assert.That(ServiceRoots.App != null && ServiceRoots.App.TryGet(out world), Is.True, "[S5 save] the app has its HostWorld");
            int saves = 0; world.SaveRequested += () => saves++;
            peerTown.ReleaseBuildLock();   // leaving placement mode asks the world to save
            yield return PlayModeHarness.WaitUntil(() => rig.HostTown.BuilderIsFree, Timeout, "[S5 save] the lock never freed");
            Assert.That(saves, Is.GreaterThan(0), "[S5 save] leaving placement mode asked the world to save");

            // ======== S6 the next dive: every player gets three loaves
            yield return ClientTo(rig, rig.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f), "[S6 dive] walk to the cistern stair");
            rig.Peer.RequestDescend();
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetActive.Value && rig.Peer.NetActive.Value, Timeout, "[S6 dive] the next dive never started");
            HeroVitals hostVitals = rig.HostHero.GetComponent<HeroVitals>(), clientOnHost = rig.ClientHeroOnHost.GetComponent<HeroVitals>(), clientVitals = rig.ClientHero.GetComponent<HeroVitals>();
            Assert.That(hostVitals.Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S6 loaves] the host's hero gets 3 loaves");
            Assert.That(clientOnHost.Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S6 loaves] the client's hero gets 3 loaves on the host");
            yield return PlayModeHarness.WaitUntil(() => clientVitals.Loaves == BakerService.LoavesPerPlayer, Timeout, "[S6 loaves] the client never read its 3 loaves");
            int floor2 = rig.HostHaul.Current.CurrentFloor;
            Assert.That(floor2, Is.EqualTo(DiveRoute.ReturnFloor), "[S6 dive] the checkpoint and the emptied floors 1-2 start the next dive on floor 3");
            yield return AllBuilt(rig, floor2, "[S6 arrive]");
            yield return BothAtEntry(rig, "[S6 arrive]");

            // ======== S7 save mid-dive (a quit counts as a failed dive), leave, reload the same world
            string preSig = Sig(stock.State);
            int preMask = rig.HostHaul.Current.ClearedMask, preStartFloor = rig.Host.NetStartFloor.Value;
            Assert.That((preMask & 0b11) == 0b11, Is.True, "[S7 save] floors 1 and 2 are marked cleared before the save (mask " + preMask + ")");
            app.WorldSaveFlow.SaveNow();
            WorldSave file = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), w => w.SchemaVersion);
            Assert.That(file, Is.Not.Null, "[S7 save] the world file reads back");
            Assert.That(file.Stock, Is.Not.Null, "[S7 save] the stock section was written");
            Assert.That(file.Stock.Pieces, Is.EquivalentTo(new[] { 300, 301 }), "[S7 save] the stock section holds the spare pieces");
            Assert.That(file.Stock.Loot.Exists(l => l.ItemId == SeedLootItem && l.Count == SeedLootCount), Is.True, "[S7 save] the stock section holds the loot");
            Assert.That(file.Town, Is.Not.Null, "[S7 save] the town section was written");
            Assert.That(file.Town.Buildings.Count, Is.EqualTo(1), "[S7 save] one building is placed");
            Assert.That(file.Town.Buildings[0].BuildingId + "/" + file.Town.Buildings[0].FoundationId + "/" + file.Town.Buildings[0].Resident, Is.EqualTo("bakery/F02/baker"), "[S7 save] the bakery stands on F02 with the baker in it");
            Assert.That(file.Residents, Is.Not.Null, "[S7 save] the residents section was written");
            Assert.That(file.Residents.Residents.Exists(r => r.Id == TownBuildings.BakerResident && r.FirstMeetBeat), Is.True, "[S7 save] the baker's first-meet beat is latched");
            Assert.That(file.Dive, Is.Not.Null, "[S7 save] the dive section was written");
            Assert.That(file.Dive.CheckpointReached && (file.Dive.ClearedMask & 0b11) == 0b11, Is.True, "[S7 save] the checkpoint and cleared floors are in the dive section");
            Assert.That(file.Dive.InDive, Is.True, "[S7 save] the save was written with the party below");
            Assert.That(file.Weapons, Is.Not.Null, "[S7 save] the weapons section was written");
            yield return _client.LeaveAndDestroy(); _client = null;
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            // Leaving writes the world once more (ShuttingDown, then Ended after the director despawned): the progress must still be in the file.
            WorldSave left = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), w => w.SchemaVersion);
            Assert.That(left, Is.Not.Null, "[S7 leave] the world file reads back after leaving");
            Assert.That(left.Dive, Is.Not.Null, "[S7 leave] the dive section is still there");
            Assert.That(left.Dive.CheckpointReached && left.Dive.ClearedMask == file.Dive.ClearedMask, Is.True, "[S7 leave] leaving mid-dive kept the checkpoint and cleared floors (mask " + left.Dive.ClearedMask + ", was " + file.Dive.ClearedMask + ", checkpoint " + left.Dive.CheckpointReached + ")");
            Assert.That(left.Town.Buildings.Count, Is.EqualTo(1), "[S7 leave] leaving kept the placed bakery");

            Rig again = null;
            yield return StartSession(r => again = r);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[S7 reload] the same world reopened");
            Assert.That(Sig(again.Host.Town.State), Is.EqualTo(preSig), "[S7 reload] the town, stock and residents are exactly as saved");
            Assert.That(Pieces(again.Host.Town.State), Is.EqualTo("300,301"), "[S7 reload] the stock pieces survived");
            Assert.That(again.Host.Town.LootCount(SeedLootItem), Is.EqualTo(SeedLootCount), "[S7 reload] the loot survived");
            Assert.That(again.Host.Town.TotalLoot, Is.EqualTo(SeedLootCount + chestLoot), "[S7 reload] the banked chest loot survived exactly once");
            Assert.That(again.Host.Town.State.IsComplete(TownBuildings.Bakery), Is.True, "[S7 reload] the bakery is complete");
            Assert.That(again.Host.Town.State.PlacedBuildingOf(TownBuildings.Bakery).Resident, Is.EqualTo(TownBuildings.BakerResident), "[S7 reload] the baker still lives in it");
            Assert.That(again.Host.BakerMet, Is.True, "[S7 reload] the first-meet beat stayed latched");
            yield return PlayModeHarness.WaitUntil(() => again.PeerTown.NetBakerMet.Value && Sig(again.PeerTown.View) == preSig, Timeout, "[S7 reload] the client never saw the reloaded town");
            Assert.That(again.Host.NetActive.Value, Is.False, "[S7 reload] a quit mid-dive was a failed dive: nobody is below");
            Assert.That(again.Host.NetCheckpoint.Value, Is.True, "[S7 reload] the checkpoint survived (start floor " + again.Host.NetStartFloor.Value + ", was " + preStartFloor + ")");
            Assert.That(again.Host.NetStartFloor.Value, Is.EqualTo(preStartFloor), "[S7 reload] the next dive starts where it would have");

            // The reloaded dive: the service still opens (3 loaves), the cleared floors are still cleared, the weapons are back.
            again.Host.SeedOverride = seed;
            yield return ClientTo(again, again.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f), "[S7 reload dive] walk to the cistern stair");
            again.Peer.RequestDescend();
            yield return PlayModeHarness.WaitUntil(() => again.Host.NetActive.Value && again.Peer.NetActive.Value, Timeout, "[S7 reload dive] the dive never started");
            yield return ResolvePeers(again);
            Assert.That(again.HostHero.GetComponent<HeroVitals>().Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S7 reload dive] the reloaded bakery still sends the host's hero down with 3 loaves");
            Assert.That(again.ClientHeroOnHost.GetComponent<HeroVitals>().Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S7 reload dive] and the client's");
            Assert.That(again.HostHaul.Current.CurrentFloor, Is.EqualTo(preStartFloor), "[S7 reload dive] the dive starts on the saved start floor");
            Assert.That((again.HostHaul.Current.ClearedMask & 0b11) == 0b11, Is.True, "[S7 reload dive] floors 1 and 2 are still cleared");
            Assert.That(again.HostHaul.Current.LeftFloor1 + again.HostHaul.Current.LeftFloor2 + again.HostHaul.Current.LeftFloor3, Is.EqualTo(0), "[S7 reload dive] every piece is home, none respawns on a floor");
            yield return AllBuilt(again, preStartFloor, "[S7 reload arrive]");
            yield return BothAtEntry(again, "[S7 reload arrive]");
            yield return TakeGateKey(again, preStartFloor);   // entering the carrier's room starts the CombatHost
            yield return WaitCombat("[S7 reload weapons]");
            WeaponState hostBack = CombatHost.Current.Growth(again.HostNm.LocalClientId).Weapon(Axe);
            Assert.That((hostBack.Level, hostBack.Abs), Is.EqualTo((earnedLevel, earnedAbs)), "[S7 reload weapons] the host's weapon growth, including what the dive earned, survived");
            Assert.That(CombatHost.Current.Growth(again.ClientNm.LocalClientId).Weapon(Axe).Abs, Is.EqualTo(guestEarned), "[S7 reload weapons] the guest's growth came back to the guest under its new client id");
        }

        // Regression for a walk finding: NGO despawns the session's objects in no fixed order, so the DiveHaulSync can be gone (its DiveState null) when the director's despawn hands the town to the
        // HostWorld. The dive section it captured then was the world's load-time copy (cleared floors and checkpoint rolled back), and the final save wrote that over the good file. Deterministic here:
        // the haul sync is despawned first.
        [UnityTest]
        public IEnumerator DirectorDespawn_AfterTheHaulSyncIsGone_StillHandsOverTheDiveProgress()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager nm = AppBootstrap.Current.NetworkManager;
            NetworkObject prefab = null;
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (prefab == null && entry.Prefab != null && entry.Prefab.GetComponent<DiveDirector>() != null) prefab = entry.Prefab.GetComponent<NetworkObject>();
            Assert.That(prefab, Is.Not.Null, "[S7 despawn] DiveDirector is in the network prefab list");
            DiveDirector director = nm.SpawnManager.InstantiateAndSpawn(prefab, nm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity).GetComponent<DiveDirector>();
            Assert.That(director.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            HostWorld world = null;
            Assert.That(ServiceRoots.App != null && ServiceRoots.App.TryGet(out world), Is.True, "[S7 despawn] the app has its HostWorld");
            Assert.That(director.ServerStartDive(), Is.True, "[S7 despawn] the dive starts");
            DiveHaulSync haul = director.HaulSync;
            Assert.That(haul.ServerSetFloor(2) && haul.ServerBank(), Is.True, "[S7 despawn] the floor-2 checkpoint is banked");
            Assert.That(haul.HostState.CheckpointReached, Is.True);
            haul.NetworkObject.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => haul.HostState == null, Timeout, "[S7 despawn] the haul sync never went");
            director.NetworkObject.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => !director.IsSpawned, Timeout, "[S7 despawn] the director never went");
            Assert.That(world.Town.Dive, Is.Not.Null, "[S7 despawn] the dive section was handed over");
            Assert.That(world.Town.Dive.CheckpointReached, Is.True, "[S7 despawn] the checkpoint survived the haul sync despawning first");
        }
    }
}
