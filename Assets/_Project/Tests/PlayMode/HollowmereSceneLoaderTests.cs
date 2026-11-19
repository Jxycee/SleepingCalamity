// Authority: test-only. T24b: Hollowmere's static art is in additive scenes that HollowmereSceneLoader loads locally when the town awakes (never through NGO).
// Catches: an art scene missing from Build Settings or never loaded, a loader that loads some but not all, a collider that ended up in an art scene (the play loop would depend on a local
// load), the town no longer being the active scene, an art scene surviving the next Single load (it would pile up across sessions), the art pieces a player sees missing under the old paths.
// T24c adds: the town torn down while its art loads leaves no art scene in the Lobby, and the next town load still gets every art scene exactly once (counted over every loaded scene:
// GetSceneByName sees only the first of two copies); the host's live scene filter (the delegate NGO holds, not just the static helper) refuses the art scenes; a late join logs none of NGO's
// "failed validation on the Host" warnings (SceneFlow switches NGO's warning off for the refusal it makes on purpose).
// NOT covered by a one-process test: a client that loads the town itself and a late joiner that loads the art (one process cannot load Hollowmere twice; the client declines scenes, so the
// loader runs once, on the host). A real second machine runs the same Awake when NGO's synchronisation loads Hollowmere there: add it to the manual 2-player walk (docs/superpowers/handoff/USER_TODO.md).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class HollowmereSceneLoaderTests
    {
        private const float Timeout = 60f;

        [UnityTearDown]
        public IEnumerator TearDown() { yield return PlayModeHarness.TearDownApp(); }

        private static IEnumerator WaitForArt()
        {
            yield return PlayModeHarness.WaitUntil(() => HollowmereSceneLoader.AllLoaded(SceneNames.HollowmereArt) && HollowmereSceneLoader.Pending == 0, Timeout, "the art scenes never finished loading");
        }

        private static void AssertEachArtSceneOnce(string context)
        {
            foreach (string name in SceneNames.HollowmereArt) Assert.That(HollowmereSceneLoader.CountLoaded(name), Is.EqualTo(1), context + ": " + name + " must be loaded exactly once");
            int hollowmereScenes = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).name.StartsWith("Hollowmere", System.StringComparison.Ordinal)) hollowmereScenes++;
            Assert.That(hollowmereScenes, Is.EqualTo(SceneNames.HollowmereArt.Length + 1), context + ": the town plus its art scenes, nothing else named Hollowmere*");
        }

        [UnityTest]
        public IEnumerator Town_LoadsItsArtScenesLocally_KeepsColliders_AndTheNextSingleLoadUnloadsThem()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            int requestedBefore = HollowmereSceneLoader.Requested;   // a process-wide counter: earlier town tests in this run have counted too
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Hollowmere), "the town stays the active scene");

            yield return WaitForArt();
            Assert.That(HollowmereSceneLoader.Requested - requestedBefore, Is.EqualTo(SceneNames.HollowmereArt.Length), "one load per art scene, none duplicated");
            AssertEachArtSceneOnce("after the solo start");

            // The host's live filter: the delegate NGO holds refuses every art scene as an additive load, and still lets the town and the arenas through.
            NetworkSceneManager.VerifySceneBeforeLoadingDelegateHandler live = AppBootstrap.Current.NetworkManager.SceneManager.VerifySceneBeforeLoading;
            Assert.That(live, Is.Not.Null, "SceneFlow installed its scene filter on the host");
            foreach (string name in SceneNames.HollowmereArt)
                Assert.That(live(SceneUtility.GetBuildIndexByScenePath(name), name, LoadSceneMode.Additive), Is.False, "the live host filter refuses " + name);
            Assert.That(live(SceneUtility.GetBuildIndexByScenePath(SceneNames.Hollowmere), SceneNames.Hollowmere, LoadSceneMode.Single), Is.True, "the live host filter lets the town itself through");
            Assert.That(live(SceneUtility.GetBuildIndexByScenePath(SceneNames.ArenaCombat), SceneNames.ArenaCombat, LoadSceneMode.Additive), Is.True, "the live host filter lets other scenes through");

            int renderers = 0;
            foreach (string name in SceneNames.HollowmereArt)
            {
                Scene scene = SceneManager.GetSceneByName(name);
                Assert.That(scene.isLoaded, Is.True, name);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, name + ": the colliders stay in the town scene");
                    renderers += root.GetComponentsInChildren<Renderer>(true).Length;
                }
            }
            Assert.That(renderers, Is.GreaterThan(1000), "the art arrived");

            // The ground the hero stands on is the town scene's own: every non-player collider a ray down at the spawn point meets belongs to Hollowmere itself.
            Vector3 spawn = Object.FindFirstObjectByType<Gameplay.SpawnPoint>().transform.position;
            RaycastHit[] hits = Physics.RaycastAll(spawn + Vector3.up * 3f, Vector3.down, 10f).Where(h => h.collider.GetComponentInParent<Unity.Netcode.NetworkObject>() == null).ToArray();
            Assert.That(hits.Length, Is.GreaterThan(0), "ground under the spawn point");
            Assert.That(hits.All(h => h.collider.gameObject.scene.name == SceneNames.Hollowmere), Is.True, "the spawn ground belongs to the town scene, not an art scene");

            // Leaving: the next Single load takes the art scenes with the town.
            yield return PlayModeHarness.TearDownApp();
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            yield return PlayModeHarness.WaitUntil(() => SceneNames.HollowmereArt.All(n => HollowmereSceneLoader.CountLoaded(n) == 0), 20f, "an art scene survived the next Single load");
        }

        // T24b review 1: the session goes Offline while the five additive loads are queued and SceneFlow loads the Lobby; an art scene that activates after that used to stay in the Lobby,
        // and the next town load skipped it (its art missing). The frame the town is up is the moment the loads are most likely still in flight.
        [UnityTest]
        public IEnumerator TownTornDownWhileItsArtLoads_LeavesNoArtInTheLobby_AndTheNextTownGetsEachSceneOnce()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.StartSceneOverride = null;   // the real default: the town
            AppBootstrap app = AppBootstrap.Current;
            app.Services.Get<LocalNetworkSession>().Port = NetworkConstants.TestPort;
            var cts = new System.Threading.CancellationTokenSource();
            yield return PlayModeHarness.Await(app.Services.Get<INetworkSession>().StartLocalAsync(false, cts.Token), 30f, cts);
            cts.Dispose();
            // Leave on the first frame the town is the active scene: its five additive loads were asked for in its Awake and are still running.
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere, Timeout, "the town never became the active scene");
            Assert.That(HollowmereSceneLoader.Pending, Is.GreaterThan(0), "the art loads must still be in flight for this test to cover the race");

            yield return PlayModeHarness.LeaveAndWaitForLobby();   // SceneFlow loads the Lobby as a Single load, destroying the town and its loader
            yield return PlayModeHarness.WaitUntil(() => HollowmereSceneLoader.Pending == 0 && SceneNames.HollowmereArt.All(n => HollowmereSceneLoader.CountLoaded(n) == 0), 30f,
                "an art scene is still loaded in the Lobby (or its load/unload never finished)");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Lobby));
            yield return null; yield return null;
            foreach (string name in SceneNames.HollowmereArt) Assert.That(HollowmereSceneLoader.CountLoaded(name), Is.EqualTo(0), name + " must not outlive the town");

            // The town again: every art scene arrives, once.
            int requestedBefore = HollowmereSceneLoader.Requested;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            yield return WaitForArt();
            Assert.That(HollowmereSceneLoader.Requested - requestedBefore, Is.EqualTo(SceneNames.HollowmereArt.Length), "the second town asked for each art scene once");
            AssertEachArtSceneOnce("after the second town load");
        }

        // T24b review 2 / nit: a late join runs the host's scene-sync listing, which used to log "failed validation on the Host" once per art scene. The client declines scenes (one process cannot
        // load the town twice); the host side is what the warnings came from.
        [UnityTest]
        public IEnumerator LateJoin_LogsNoSceneValidationWarning_AndLoadsNoSecondCopyOfTheArt()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.StartSceneOverride = null;   // the real default: the town
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            yield return PlayModeHarness.StartClients(app, 1, true, null, 20);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && app.NetworkManager.LocalClient.PlayerObject != null, Timeout, "the host never loaded the town with its player");
            yield return WaitForArt();

            var warnings = new List<string>();
            void Capture(string condition, string stack, LogType type) { if (type == LogType.Warning && condition.Contains("failed validation on the Host")) warnings.Add(condition); }
            Application.logMessageReceived += Capture;
            try
            {
                yield return PlayModeHarness.StartClients(app, 1, true, null, 21);   // joins after the town is up: the host lists every loaded scene for it
                yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 3, 20f, "the late joiner never reached the roster");
                for (int i = 0; i < 30; i++) yield return null;
            }
            finally { Application.logMessageReceived -= Capture; }
            Assert.That(warnings, Is.Empty, "NGO's refusal warnings are switched off for the art scenes: " + string.Join(" | ", warnings));
            AssertEachArtSceneOnce("after the late join");
        }
    }
}
