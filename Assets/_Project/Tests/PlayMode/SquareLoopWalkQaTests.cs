// Authority: test-only. M6 S6 (automated part) QA walk of the Hollowmere square loop (docs/design/m6-square-loop-proposal.md sections 1 and 4): a host + one client walk it end to end through the real
// Hollowmere scene (SceneFlow, DiveHub, DiveDirector, CellarFloorSync, DiveHaulSync, CellarContentsSync, TownSync, BakerService, HostWorld and WorldSaveFlow against a throwaway world library).
// The CLIENT hero drives: owner-side moves plus client RPCs (RequestDescend / RequestStair / RequestBank / RequestGoHome / RequestOpenChest / TownSync.Request*), so every step takes the same
// server-side entry point a client RPC reaches. The host side only kills enemies through the same ServerApplyHit path a swing uses.
// Steps (every assertion message starts with its tag, e.g. "[S3 bank]"):
//   S1 start in Hollowmere from a save (baker not yet met, weapon section kept)   S2 start the dive at the bank (the checkpoint pick, plan 3 decision 2), find every piece and the baker, open the bank room
//   S3 go home from the bank room: the finds are in the town stock, the checkpoint is held, stir reset   S4 place the bakery frame (a path-blocking turn is refused) and its parts from stock   S5 place the baker, three live wishes, service
//   S6 next dive: every player gets 3 loaves   S7 save mid-dive, leave, reload the same world: stock, town, residents, dive section and weapons survive.
// The floors themselves are walked by DiveWalkQaTests and the DiveBot; this loop is about what the dive hands the town.
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
            ServiceRoots.App.Replace<SleepingCalamity.Runtime.IInputService>(new SleepingCalamity.Tests.PlayMode.FakeInputService());   // the real adapter's binding display strings (the trapdoor's floor-pick prompt) crash a headless Linux editor
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
            if (rig.HostFloor.BuiltFloor == UndercroftPlan.BankFloor && CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 arrival)) entry = arrival;   // a dive started at the bank arrives at the bank room's entrance
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, entry) < 6f && rig.HostHero.transform.position.y < AreaLook.Current.CellarBelowY, Timeout, step + ": the host hero is at " + rig.HostHero.transform.position + ", wanted near the entry " + entry);
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.ClientHeroOnHost.transform.position, entry) < 6f, Timeout, step + ": the client hero is at " + rig.ClientHeroOnHost.transform.position + " on the host, wanted near the entry " + entry);
            yield return Seconds(1f);   // late arrival teleports must have landed before the test moves a hero
        }

        // Starts the dive the way a client does: held checkpoint, the client at the trapdoor, one request for the picked floor.
        private IEnumerator ClientStartsDive(Rig rig, int floor, string step)
        {
            yield return ClientTo(rig, rig.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f), step + " walk to the cistern stair");
            rig.Peer.RequestStartDive(floor);
            yield return PlayModeHarness.WaitUntil(() => rig.Host.NetActive.Value && rig.Peer.NetActive.Value, Timeout, step + " the client's request never started the dive on both peers");
        }

        // ---------------------------------------------------------------- town / save helpers

        private static string Sig(TownState s) => TownWire.From(s).Signature();

        private static IEnumerator TakeLock(TownSync who, TownSync host, string step)
        {
            who.RequestBuildLock();
            yield return PlayModeHarness.WaitUntil(() => who.IsBuilder && host.NetBuilder.Value == who.NetworkManager.LocalClientId, Timeout, step + ": the builder lock was never granted");
        }

        private static string Pieces(TownState s) { var l = new List<int>(s.StockPieces); return string.Join(",", l); }

        // The seed world: weapon growth for both players (the host's local id and the guest's), written to the library before the session opens it.
        private static (int level, float abs, float guestAbs) SeedWorld(AppBootstrap app, out string worldId)
        {
            worldId = app.WorldSaveFlow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local);
            Assert.That(worldId, Is.Not.Null, "[S1 start] a multiplayer world was made");
            WorldSaveStore store = app.WorldSaveFlow.Library.Open(worldId);
            Assert.That(store, Is.Not.Null, "[S1 start] the seed world opens");
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

        // The client walks into a room whose plan has enemies, so the dive's contents begin the CombatHost (weapon growth is restored from the save there); everything in the room is then killed.
        private static IEnumerator EnterFightRoom(Rig rig, string step)
        {
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.EncounterPlan != null, Timeout, step + ": the host never built an encounter plan");
            CellarEncounterPlan plan = rig.HostHaul.EncounterPlan;
            CellarFloorLayout lay = rig.HostFloor.Layout;
            int room = -1;
            for (int r = 0; r < lay.Rooms.Count && room < 0; r++) if (plan.CountIn(r) > 0) room = r;
            Assert.That(room, Is.GreaterThanOrEqualTo(0), step + ": the floor has a room with enemies");
            CellPos enter = default; float bestGap = -1f;
            foreach (CellPos c in CellarEncounterPlan.FreeCellsOf(lay, CellarFloorDressing.Resolve(lay), room))
            {
                float gap = float.MaxValue;
                foreach (EncounterSpawn sp in plan.Spawns) if (sp.RoomIndex == room) gap = Mathf.Min(gap, Mathf.Abs(sp.X - c.X) + Mathf.Abs(sp.Y - c.Y));
                if (gap > bestGap) { bestGap = gap; enter = c; }
            }
            Assert.That(bestGap, Is.GreaterThan(0f), step + ": the room has a free cell that is not a spawn cell");
            yield return ClientTo(rig, rig.HostFloor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(enter.X, enter.Y)), step + " enter the room");
            yield return PlayModeHarness.WaitUntil(() => CombatHost.Current != null && CombatHost.Current.Begun, Timeout, step + ": entering the room never started a CombatHost");
            KillAll(rig.HostNm);
        }

        // ---------------------------------------------------------------------------------------------------------------------------------

        [UnityTest, Timeout(1500000)]
        public IEnumerator Walk_Hollowmere_Dive_Rescue_GoHome_Place_Baker_Loaves_SaveReload([Values(NetCondition.None, NetCondition.Rtt150Loss2, NetCondition.Rtt300Loss5)] NetCondition net)
        {
            const uint seed = 12345u;
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            PlayModeHarness.AddNetworkSimulator(net);   // before the host starts
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;
            (int seedLevel, float seedAbs, float seedGuestAbs) = SeedWorld(app, out string worldId);

            // ======== S1 start in Hollowmere from a save
            Rig rig = null;
            yield return StartSession(r => rig = r);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[S1 start] the session opened the seeded world");
            Assert.That(Pieces(rig.Host.Town.State), Is.Empty, "[S1 start] no bakery piece is home yet");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.False, "[S1 start] the baker is not rescued yet");
            Assert.That(rig.Host.BakerMet, Is.False, "[S1 start] the baker has not been met");
            Assert.That(rig.PeerTown.NetBakerMet.Value, Is.False, "[S1 start] the client sees the baker unmet");
            Assert.That(rig.PeerTown.BuilderIsFree, Is.True, "[S1 start] nobody holds the builder lock");
            Assert.That(Sig(rig.PeerTown.View), Is.EqualTo(Sig(rig.Host.Town.State)), "[S1 start] the client's town view equals the host's");

            // ======== S2 the dive starts at the bank (the held checkpoint is the pick), every piece and the baker are found, the bank room opens
            rig.Host.SeedOverride = seed;
            rig.Host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.NetCheckpointHeld.Value && rig.Peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "[S2 dive] the client never saw the held checkpoint");
            yield return ClientStartsDive(rig, UndercroftPlan.BankFloor, "[S2 dive]");
            yield return ResolvePeers(rig);
            yield return AllBuilt(rig, UndercroftPlan.BankFloor, "[S2 F5 arrive]");
            yield return BothAtEntry(rig, "[S2 F5 arrive]");
            Assert.That(rig.HostNm.ConnectedClientsIds.Count, Is.EqualTo(2), "[S2 F5 arrive] both players are in the dive");
            ulong hostId = rig.HostNm.LocalClientId, guestId = rig.ClientNm.LocalClientId;
            Assert.That(rig.HostHaul.Session.StartedAtBank, Is.True, "[S2 F5 arrive] the party starts at the bank room's floor");
            Assert.That(rig.HostHaul.ServerAddItem(guestId, 101), Is.True, "[S2 find] the client carries one item (a chest's loot goes to the opener's bag)");
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.StirValue > 0, Timeout, "[S2 find] the carried item (a valuable) never stirred");
            int stirWithItem = rig.HostHaul.Current.StirValue;
            foreach (int id in new[] { 100, 101, 200, 201, 300, 301 }) Assert.That(rig.HostHaul.ServerAddPiece(id), Is.True, "[S2 find] piece " + id);
            Assert.That(rig.HostHaul.ServerAddPiece(PieceCatalog.BakerId), Is.True, "[S2 find] the baker");
            yield return PlayModeHarness.WaitUntil(() => rig.PeerHaul.Current.FoundPieces >= 6, Timeout, "[S2 find] the client never saw the six pieces found");
            Assert.That(rig.Host.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "[S2 find] the baker is safe in the town stock the moment he is found");
            Assert.That(rig.HostHaul.Current.StirValue, Is.EqualTo(stirWithItem), "[S2 find] pieces never stir (only the carried valuable does)");
            Assert.That(CellarFloorBuilder.TryBankWorld(rig.HostFloor.Layout, rig.HostFloor.Built, out Vector3 bank), Is.True, "[S2 F5 bank] the floor has a bank point");
            Move(rig.HostHero, bank + new Vector3(0.5f, 0.1f, 0f));
            yield return ClientTo(rig, bank + new Vector3(-0.5f, 0.1f, 0f), "[S2 F5 bank walk]");
            rig.Peer.RequestBank();
            yield return PlayModeHarness.WaitUntil(() => rig.HostHaul.Current.DivePhase == DivePhase.BankRoom && rig.PeerHaul.Current.DivePhase == DivePhase.BankRoom, Timeout, "[S2 F5 bank] the client's bank press never opened the bank room on both peers");
            Assert.That(rig.HostHaul.ServerBagOf(guestId).BankOpen, Is.True, "[S2 F5 bank] the client's bank visit is open");

            // ======== S3 go home from the bank room: the finds are in the town stock, the checkpoint is held, the stir resets
            rig.Peer.RequestGoHome();
            int handled = rig.Host.RequestsHandled;
            yield return PlayModeHarness.WaitUntil(() => rig.Host.RequestsHandled > handled, Timeout, "[S3 home] the press never reached the host");
            Assert.That(rig.Host.NetActive.Value, Is.True, "[S3 home] two players: one press only opens the confirm window");
            Assert.That(rig.Host.ServerTryGoHome(hostId), Is.True, "[S3 home] the second player's press confirms");
            yield return PlayModeHarness.WaitUntil(() => !rig.Host.NetActive.Value && !rig.Peer.NetActive.Value, Timeout, "[S3 home] the dive never ended on both peers");
            yield return PlayModeHarness.WaitUntil(() => Flat(rig.HostHero.transform.position, rig.StairTop.position) < 4f && Flat(rig.ClientHeroOnHost.transform.position, rig.StairTop.position) < 4f
                && rig.HostHero.transform.position.y > AreaLook.Current.CellarBelowY && rig.ClientHeroOnHost.transform.position.y > AreaLook.Current.CellarBelowY, Timeout,
                "[S3 home] the party never landed at the stair top (host " + rig.HostHero.transform.position + ", client " + rig.ClientHeroOnHost.transform.position + ")");
            TownStock stock = rig.Host.Town;
            Assert.That(Pieces(stock.State), Is.EqualTo("100,101,200,201,300,301"), "[S3 stock] the six found pieces are in the town stock");
            Assert.That(stock.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "[S3 stock] the baker is in the town stock");
            Assert.That(rig.Host.Checkpoint.Held, Is.True, "[S3 home] going home from the bank holds the checkpoint");
            PlayerBag guestBag = rig.HostHaul.ServerBagOf(guestId);
            Assert.That(guestBag.Carried, Is.Empty, "[S3 stock] the client's carried item came home");
            Assert.That(guestBag.Stash.TryGetValue(101, out int stashed) ? stashed : 0, Is.EqualTo(1), "[S3 stock] the client's item is in its own stash, exactly once");
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
            yield return PlayModeHarness.WaitUntil(() => rig.Peer.NetCheckpointHeld.Value && rig.Peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "[S6 dive] the client never saw the checkpoint pick");
            yield return ClientStartsDive(rig, UndercroftPlan.FirstFloor, "[S6 dive]");
            HeroVitals hostVitals = rig.HostHero.GetComponent<HeroVitals>(), clientOnHost = rig.ClientHeroOnHost.GetComponent<HeroVitals>(), clientVitals = rig.ClientHero.GetComponent<HeroVitals>();
            Assert.That(hostVitals.Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S6 loaves] the host's hero gets 3 loaves");
            Assert.That(clientOnHost.Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S6 loaves] the client's hero gets 3 loaves on the host");
            yield return PlayModeHarness.WaitUntil(() => clientVitals.Loaves == BakerService.LoavesPerPlayer, Timeout, "[S6 loaves] the client never read its 3 loaves");
            int floor2 = rig.HostHaul.Current.CurrentFloor;
            Assert.That(floor2, Is.EqualTo(UndercroftPlan.FirstFloor), "[S6 dive] the next dive starts on the picked floor, 1");
            Assert.That(rig.Host.Checkpoint.Held, Is.False, "[S6 dive] starting a dive spends the checkpoint");
            yield return AllBuilt(rig, floor2, "[S6 arrive]");
            yield return BothAtEntry(rig, "[S6 arrive]");
            // Weapon growth: the saved growth is restored when the contents begin the CombatHost; what the dive earns goes into the save (S7).
            yield return EnterFightRoom(rig, "[S6 weapons]");
            WeaponState hostAxe = CombatHost.Current.Growth(hostId).Weapon(Axe);
            Assert.That((hostAxe.Level, hostAxe.Abs), Is.EqualTo((seedLevel, seedAbs)), "[S6 weapons] the host's saved weapon growth was restored");
            Assert.That(CombatHost.Current.Growth(guestId).Weapon(Axe).Abs, Is.EqualTo(seedGuestAbs), "[S6 weapons] the guest's saved weapon growth was restored under its new client id");
            hostAxe.AddAbs(25f);
            (int earnedLevel, float earnedAbs) = (hostAxe.Level, hostAxe.Abs);
            CombatHost.Current.Growth(guestId).Weapon(Axe).AddAbs(7f);
            float guestEarned = CombatHost.Current.Growth(guestId).Weapon(Axe).Abs;

            // ======== S7 save mid-dive (a quit counts as a failed dive), leave, reload the same world
            string preSig = Sig(stock.State);
            app.WorldSaveFlow.SaveNow();
            WorldSave file = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), w => w.SchemaVersion);
            Assert.That(file, Is.Not.Null, "[S7 save] the world file reads back");
            Assert.That(file.Stock, Is.Not.Null, "[S7 save] the stock section was written");
            Assert.That(file.Stock.Pieces, Is.EquivalentTo(new[] { 300, 301 }), "[S7 save] the stock section holds the spare pieces");
            Assert.That(file.Stock.Loot, Is.Empty, "[S7 save] the retired town loot is no longer written");
            Assert.That(file.Town, Is.Not.Null, "[S7 save] the town section was written");
            Assert.That(file.Town.Buildings.Count, Is.EqualTo(1), "[S7 save] one building is placed");
            Assert.That(file.Town.Buildings[0].BuildingId + "/" + file.Town.Buildings[0].FoundationId + "/" + file.Town.Buildings[0].Resident, Is.EqualTo("bakery/F02/baker"), "[S7 save] the bakery stands on F02 with the baker in it");
            Assert.That(file.Residents, Is.Not.Null, "[S7 save] the residents section was written");
            Assert.That(file.Residents.Residents.Exists(r => r.Id == TownBuildings.BakerResident && r.FirstMeetBeat), Is.True, "[S7 save] the baker's first-meet beat is latched");
            Assert.That(file.Dive, Is.Not.Null, "[S7 save] the dive section was written");
            Assert.That(file.Dive.InDive, Is.True, "[S7 save] the save was written with the party below");
            Assert.That(file.Dive.PieceDepths, Is.Not.Null.And.Count.EqualTo(PieceCatalog.Specs.Count), "[S7 save] the piece depths are saved once");
            Assert.That(file.Dive.BankedPieces, Is.Empty, "[S7 save] nothing waits banked: found pieces are in the stock");
            Assert.That(file.Weapons, Is.Not.Null, "[S7 save] the weapons section was written");
            yield return _client.LeaveAndDestroy(); _client = null;
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            // Leaving writes the world once more (ShuttingDown, then Ended after the director despawned): the progress must still be in the file.
            WorldSave left = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(Path.Combine(_dir, worldId + ".json")), w => w.SchemaVersion);
            Assert.That(left, Is.Not.Null, "[S7 leave] the world file reads back after leaving");
            Assert.That(left.Dive, Is.Not.Null, "[S7 leave] the dive section is still there");
            Assert.That(left.Dive.PieceDepths, Is.Not.Null.And.Count.EqualTo(PieceCatalog.Specs.Count), "[S7 leave] leaving kept the piece depths");
            Assert.That(left.Town.Buildings.Count, Is.EqualTo(1), "[S7 leave] leaving kept the placed bakery");

            Rig again = null;
            yield return StartSession(r => again = r);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[S7 reload] the same world reopened");
            Assert.That(Sig(again.Host.Town.State), Is.EqualTo(preSig), "[S7 reload] the town, stock and residents are exactly as saved");
            Assert.That(Pieces(again.Host.Town.State), Is.EqualTo("300,301"), "[S7 reload] the stock pieces survived");
            Assert.That(again.Host.Town.State.IsComplete(TownBuildings.Bakery), Is.True, "[S7 reload] the bakery is complete");
            Assert.That(again.Host.Town.State.PlacedBuildingOf(TownBuildings.Bakery).Resident, Is.EqualTo(TownBuildings.BakerResident), "[S7 reload] the baker still lives in it");
            Assert.That(again.Host.BakerMet, Is.True, "[S7 reload] the first-meet beat stayed latched");
            yield return PlayModeHarness.WaitUntil(() => again.PeerTown.NetBakerMet.Value && Sig(again.PeerTown.View) == preSig, Timeout, "[S7 reload] the client never saw the reloaded town");
            Assert.That(again.Host.NetActive.Value, Is.False, "[S7 reload] a quit mid-dive was a failed dive: nobody is below");
            Assert.That(again.Host.NetCheckpointHeld.Value, Is.False, "[S7 reload] the checkpoint was spent by the dive the party quit in");
            Assert.That(again.Host.NetPickableMaxFloor.Value, Is.EqualTo(UndercroftPlan.FirstFloor), "[S7 reload] the next dive starts on floor 1");

            // The reloaded dive: the service still opens (3 loaves) and every piece is home, none respawns on a floor.
            again.Host.SeedOverride = seed;
            yield return ClientTo(again, again.Peer.NetHubStair.Value + new Vector3(1f, 0.1f, 0f), "[S7 reload dive] walk to the cistern stair");
            again.Peer.RequestDescend();
            yield return PlayModeHarness.WaitUntil(() => again.Host.NetActive.Value && again.Peer.NetActive.Value, Timeout, "[S7 reload dive] the dive never started");
            yield return ResolvePeers(again);
            Assert.That(again.HostHero.GetComponent<HeroVitals>().Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S7 reload dive] the reloaded bakery still sends the host's hero down with 3 loaves");
            Assert.That(again.ClientHeroOnHost.GetComponent<HeroVitals>().Loaves, Is.EqualTo(BakerService.LoavesPerPlayer), "[S7 reload dive] and the client's");
            Assert.That(again.HostHaul.Current.CurrentFloor, Is.EqualTo(UndercroftPlan.FirstFloor), "[S7 reload dive] the dive starts on floor 1");
            Assert.That(again.Host.Pieces.PiecesLeft(UndercroftPlan.FirstFloor) + again.Host.Pieces.PiecesLeft(2) + again.Host.Pieces.PiecesLeft(4), Is.EqualTo(0), "[S7 reload dive] the found pieces are home: none respawns on a floor");
            yield return AllBuilt(again, UndercroftPlan.FirstFloor, "[S7 reload arrive]");
            yield return BothAtEntry(again, "[S7 reload arrive]");
            yield return EnterFightRoom(again, "[S7 reload weapons]");
            WeaponState hostBack = CombatHost.Current.Growth(again.HostNm.LocalClientId).Weapon(Axe);
            Assert.That((hostBack.Level, hostBack.Abs), Is.EqualTo((earnedLevel, earnedAbs)), "[S7 reload weapons] the host's weapon growth, including what the dive earned, survived");
            Assert.That(CombatHost.Current.Growth(again.ClientNm.LocalClientId).Weapon(Axe).Abs, Is.EqualTo(guestEarned), "[S7 reload weapons] the guest's growth came back to the guest under its new client id");
        }

        // Regression for a walk finding: NGO despawns the session's objects in no fixed order, so the DiveHaulSync can be gone (its session null) when the director's despawn hands the town to the
        // HostWorld. The dive section it captured then was the world's load-time copy (cleared floors and checkpoint rolled back), and the final save wrote that over the good file. Deterministic here:
        // the haul sync is despawned first.
        [UnityTest]
        public IEnumerator DirectorDespawn_AfterTheHaulSyncIsGone_StillHandsOverTheDiveProgress()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            ServiceRoots.App.Replace<SleepingCalamity.Runtime.IInputService>(new SleepingCalamity.Tests.PlayMode.FakeInputService());   // the real adapter's binding display strings (the trapdoor's floor-pick prompt) crash a headless Linux editor
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
            director.TestHoldCheckpoint(true);   // the checkpoint a go-home from the bank room would hold
            Assert.That(director.Checkpoint.Held, Is.True);
            haul.NetworkObject.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => haul.Session == null, Timeout, "[S7 despawn] the haul sync never went");
            director.NetworkObject.Despawn(true);
            yield return PlayModeHarness.WaitUntil(() => !director.IsSpawned, Timeout, "[S7 despawn] the director never went");
            Assert.That(world.Town.Dive, Is.Not.Null, "[S7 despawn] the dive section was handed over");
            Assert.That(world.Town.Dive.CheckpointHeld, Is.True, "[S7 despawn] the checkpoint survived the haul sync despawning first");
        }
    }
}
