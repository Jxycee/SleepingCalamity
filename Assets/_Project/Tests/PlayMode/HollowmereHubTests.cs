// Authority: test-only. M6 S3: Hollowmere is the session (hub) scene. Real SceneFlow starts (solo, then host + one client that declines scenes), the DiveHub at the cistern-house stair spawns
// the DiveDirector, the dive floors are built far under the town, fail and go-home land the party at the stair top, the banked haul moves to the host's town stock, and the look
// separates by area (AreaLook: town volume/sky/fog/sun in the town, the cellar's in the cellar, and back).
// Catches: Hollowmere not a gameplay scene (no player), no camera/spawn point/hub in the scene, the hub's stair or origin in the wrong place, a trip home landing at the spawn instead of
// the stair, banked pieces not reaching the stock (or reaching it twice), the town's fog/sky/sun reaching the cellar (or the cellar's fog staying on the town after a trip).
// NOT covered by a one-process test: a client that loads the scene itself (one process cannot load Hollowmere twice, so the client declines scenes: TownView on a client, late join into the
// town), the real look on screen (the assertions read the global render state, not a capture), the interact KEY.
using System.Collections;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HollowmereHubTests
    {
        private const float Timeout = 40f;
        private PlayModeHarness.TestClient _client;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
        }

        private static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }

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

        private static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        // Runs in LateUpdate after both the camera brain and AreaLook: the point just before rendering. Records the first frame where the global look does not match the area the camera
        // is in. (WaitForEndOfFrame is not an option: it never resumes in a -nographics run.)
        [DefaultExecutionOrder(2000)]
        private sealed class LookProbe : MonoBehaviour
        {
            public string Failure;
            private void LateUpdate()
            {
                if (Failure != null) return;
                AreaLook look = AreaLook.Current; Camera cam = Camera.main;
                if (look == null || cam == null) return;
                bool cellar = cam.transform.position.y < look.CellarBelowY;
                if (look.CurrentArea != (cellar ? AreaLook.Area.Cellar : AreaLook.Area.Town)) Failure = "the look lags the camera (camera y " + cam.transform.position.y + ", area " + look.CurrentArea + ")";
                else if ((RenderSettings.skybox == null) != cellar) Failure = "the sky does not match the area the camera is in";
                else if (Mathf.Abs(look.CellarWeight - (cellar ? 1f : 0f)) > 0.001f || Mathf.Abs(look.TownWeight - (cellar ? 0f : 1f)) > 0.001f) Failure = "the grade does not match the area the camera is in";
            }
        }

        // Waits (bounded) until `done`, failing on the first rendered frame whose look does not match the camera's area.
        private static IEnumerator WatchLook(LookProbe probe, System.Func<bool> done, string what)
        {
            double until = Time.realtimeSinceStartupAsDouble + Timeout;
            while (!done())
            {
                Assert.That(probe.Failure, Is.Null);
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("Timed out after " + Timeout + " s: " + what);
                yield return null;
            }
            Assert.That(probe.Failure, Is.Null);
        }

        [UnityTest]
        public IEnumerator GoHome_AfterTheHeartBank_LandsAtTheStairTop_TownStockHoldsTheBankedPieces_BakerReceived()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            AppBootstrap app = AppBootstrap.Current;
            NetworkObject hero = app.NetworkManager.LocalClient.PlayerObject;
            Transform top = Find("StairTop");
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero, Timeout, "the hub never spawned the director");
            DiveDirector director = DiveDirector.Instance;

            director.SeedOverride = 777u;
            Assert.That(director.ServerStartDive(4), Is.True);
            yield return PlayModeHarness.WaitUntil(() => director.FloorSync != null && director.FloorSync.Built != null && director.FloorSync.BuiltFloor == 4, Timeout, "floor 4 never built");
            yield return PlayModeHarness.WaitUntil(() => hero.transform.position.y < AreaLook.Current.CellarBelowY, Timeout, "the hero never arrived under the town");
            yield return Seconds(1f);   // the arrival teleports have landed before the test moves the hero

            // Bank at the heart: the hero stands at the bank point (the go-home range check), a piece is in the haul.
            Assert.That(CellarFloorBuilder.TryBankWorld(director.FloorSync.Layout, director.FloorSync.Built, out Vector3 bank), Is.True);
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = bank + new Vector3(0.5f, 0.1f, 0f);
            if (cc != null) cc.enabled = true;
            Assert.That(director.HaulSync.ServerAddPiece(DiveHaul.PieceId(3, 0)), Is.True);
            Assert.That(director.HaulSync.ServerBank(), Is.True, "floor 4 is a bank point");
            yield return PlayModeHarness.WaitUntil(() => director.NetHeartBanked.Value, Timeout, "the heart bank never registered");

            Assert.That(director.ServerTryGoHome(app.NetworkManager.LocalClientId), Is.True, "the party can go home after the heart bank");
            yield return PlayModeHarness.WaitUntil(() => !director.NetActive.Value, Timeout, "go home never ended the dive");
            yield return PlayModeHarness.WaitUntil(() => Flat(hero.transform.position, top.position) < 3f, Timeout, "the party never landed at the stair top");
            Assert.That(hero.transform.position.y, Is.GreaterThan(AreaLook.Current.CellarBelowY));
            Assert.That(director.Town.State.InStock(DiveHaul.PieceId(3, 0)), Is.True, "the banked piece is in the town stock");
            Assert.That(director.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.True, "the heart bank brought the baker home");
        }

        [UnityTest]
        public IEnumerator Solo_StartsInTheTown_HubAtTheCisternStair_FailLandsAtTheStairTop_BankedPiecesReachTheTownStock_LookSeparatesByArea()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            AppBootstrap app = AppBootstrap.Current;
            NetworkObject hero = app.NetworkManager.LocalClient.PlayerObject;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Hollowmere));

            // The player starts by the heroes' home and stays on the ground there.
            Transform spawn = Find("SpawnPoint"), stair = Find("DiveEntrance"), top = Find("StairTop");
            Assert.That(spawn != null && stair != null && top != null, Is.True, "the scene has the spawn point and the hub markers");
            yield return Seconds(2f);
            Assert.That(Flat(hero.transform.position, spawn.position), Is.LessThan(2f), "the player spawned by the heroes' home");
            Assert.That(Mathf.Abs(hero.transform.position.y - spawn.position.y), Is.LessThan(2f), "and stands on the ground (the terrain collider holds)");
            Assert.That(Camera.main, Is.Not.Null, "the scene has its gameplay camera");
            Assert.That(AreaLook.Current, Is.Not.Null);
            Assert.That(AreaLook.Current.CurrentArea, Is.EqualTo(AreaLook.Area.Town));

            // The hub spawned the director at the cistern stair, the floors' origin under the town.
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero, Timeout, "the hub never spawned the director");
            DiveDirector director = DiveDirector.Instance;
            Assert.That(director.NetHubStair.Value, Is.EqualTo(stair.position).Using(Vector3EqualityComparer.Instance));
            Assert.That(director.NetHome.Value, Is.EqualTo(top.position).Using(Vector3EqualityComparer.Instance), "home is the stair top");
            Assert.That(director.NetOrigin.Value.y, Is.LessThan(AreaLook.Current.CellarBelowY - 50f), "the floors are built far under the town");
            Assert.That(Flat(top.position, stair.position), Is.LessThan(DiveDirector.PromptRadius), "the stair top is inside the stair's prompt range");

            // Remember the town's global look.
            Material townSky = RenderSettings.skybox; float townFog = RenderSettings.fogDensity; Color townFogColor = RenderSettings.fogColor; float townAmbient = RenderSettings.ambientIntensity;
            Assert.That(townSky, Is.Not.Null);
            Light sun = RenderSettings.sun;
            Assert.That(sun, Is.Not.Null);

            // Dive to floor 2, bank a piece there, then abandon (the dive fails): the party must land at the stair top and the banked piece must be in the town stock.
            director.SeedOverride = 4242u;
            Assert.That(director.ServerStartDive(2), Is.True);
            yield return PlayModeHarness.WaitUntil(() => director.FloorSync != null && director.FloorSync.Built != null && director.FloorSync.BuiltFloor == 2, Timeout, "floor 2 never built");
            // The look is right on every rendered frame of the teleport (checked at the end of each frame, once the camera is down there too).
            var probe = new GameObject("LookProbe").AddComponent<LookProbe>();
            yield return WatchLook(probe, () => hero.transform.position.y < AreaLook.Current.CellarBelowY && Camera.main.transform.position.y < AreaLook.Current.CellarBelowY, "the hero and camera never arrived under the town");
            Assert.That(AreaLook.Current.CurrentArea, Is.EqualTo(AreaLook.Area.Cellar), "the look is already the cellar's on the first frame the camera is down there");
            yield return Seconds(0.2f);

            // In the cellar: none of the town's look is on.
            Assert.That(RenderSettings.skybox, Is.Null, "no town sky in the cellar");
            Assert.That(sun.enabled, Is.False, "no sun in the cellar");
            Assert.That(AreaLook.Current.TownWeight, Is.EqualTo(0f).Within(0.01f), "the town grade is off");
            Assert.That(AreaLook.Current.CellarWeight, Is.EqualTo(1f).Within(0.01f), "the cellar grade is on");
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(CellarArtTuning.Ambient).Within(0.001f));
            Assert.That(RenderSettings.fogDensity, Is.Not.EqualTo(townFog).Within(0.0001f), "the cellar has its own fog");
            Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(DefaultReflectionMode.Custom));

            Assert.That(director.HaulSync.ServerAddPiece(DiveHaul.PieceId(1, 0)), Is.True);
            Assert.That(director.HaulSync.ServerBank(), Is.True, "floor 2 is a bank point");
            Assert.That(director.ServerAbandonDive(), Is.True);
            yield return WatchLook(probe, () => Flat(hero.transform.position, top.position) < 3f && Camera.main.transform.position.y > AreaLook.Current.CellarBelowY, "the party never landed at the stair top");
            Assert.That(AreaLook.Current.CurrentArea, Is.EqualTo(AreaLook.Area.Town), "the look is already the town's on the first frame the camera is back up");
            Assert.That(hero.transform.position.y, Is.GreaterThan(AreaLook.Current.CellarBelowY));
            yield return Seconds(0.2f);

            // Back in the town: the town's look, exactly (the floor build wrote the cellar fog into the global settings meanwhile).
            Assert.That(RenderSettings.skybox, Is.EqualTo(townSky));
            Assert.That(sun.enabled, Is.True);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(townFog).Within(0.00001f), "the cellar fog does not stay on the town");
            Assert.That(RenderSettings.fogColor, Is.EqualTo(townFogColor));
            Assert.That(RenderSettings.ambientIntensity, Is.EqualTo(townAmbient).Within(0.001f));
            Assert.That(AreaLook.Current.TownWeight, Is.EqualTo(1f).Within(0.01f));
            Assert.That(AreaLook.Current.CellarWeight, Is.EqualTo(0f).Within(0.01f));

            // The banked piece came home; nothing was stocked twice.
            Assert.That(director.Town.State.InStock(DiveHaul.PieceId(1, 0)), Is.True, "the banked piece is in the town stock");
            Assert.That(director.Town.State.ResidentInStock(TownBuildings.BakerResident), Is.False, "the heart was not banked, so no baker");
            int count = 0; foreach (int _ in director.Town.State.StockPieces) count++;
            Assert.That(count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TwoPeers_BothStartInTheTown_SeeTheSameHub_AndBothLandAtTheStairTopAfterTheDive()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.StartSceneOverride = null;   // the real default: the town
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(6), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && app.NetworkManager.LocalClient.PlayerObject != null,
                Timeout, "the host never loaded the town with its player");

            // The client declined the scene, so its hero is spawned the way the arena spawner would.
            NetworkManager hostNm = app.NetworkManager, clientNm = _client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => clientNm.IsConnectedClient, Timeout, "the client lost its connection");
            NetworkObject clientHeroOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientNm.LocalClientId, false, true, false,
                Find("SpawnPoint").position + new Vector3(1.5f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            NetworkObject hostHero = hostNm.LocalClient.PlayerObject;

            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero, Timeout, "the hub never spawned the director");
            DiveDirector host = DiveDirector.Instance;
            ulong id = host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => clientNm.SpawnManager.SpawnedObjects.ContainsKey(id) && clientNm.SpawnManager.SpawnedObjects[id].GetComponent<DiveDirector>().NetHubStair.Value != Vector3.zero,
                Timeout, "the client never received the director");
            DiveDirector peer = clientNm.SpawnManager.SpawnedObjects[id].GetComponent<DiveDirector>();

            // Same hub on both peers: the cistern stair, the stair top, the origin under the town.
            Transform stair = Find("DiveEntrance"), top = Find("StairTop");
            Assert.That(peer.NetHubStair.Value, Is.EqualTo(stair.position).Using(Vector3EqualityComparer.Instance));
            Assert.That(peer.NetHome.Value, Is.EqualTo(top.position).Using(Vector3EqualityComparer.Instance));
            Assert.That(peer.NetOrigin.Value, Is.EqualTo(host.NetOrigin.Value));
            Assert.That(Flat(hostHero.transform.position, Find("SpawnPoint").position), Is.LessThan(2.5f), "the host started by the heroes' home");

            // Dive and fail: both heroes land at the stair top.
            host.SeedOverride = 99u;
            Assert.That(host.ServerStartDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => hostHero.transform.position.y < AreaLook.Current.CellarBelowY && clientHeroOnHost.transform.position.y < AreaLook.Current.CellarBelowY,
                Timeout, "the party never reached the floor under the town");
            Assert.That(host.ServerAbandonDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => Flat(hostHero.transform.position, top.position) < 4f && Flat(clientHeroOnHost.transform.position, top.position) < 4f
                && hostHero.transform.position.y > AreaLook.Current.CellarBelowY && clientHeroOnHost.transform.position.y > AreaLook.Current.CellarBelowY,
                Timeout, "the party never landed at the stair top (host " + hostHero.transform.position + ", client " + clientHeroOnHost.transform.position + ")");
            yield return PlayModeHarness.WaitUntil(() => !peer.NetActive.Value, Timeout, "the client never saw the dive end");
        }
    }
}
