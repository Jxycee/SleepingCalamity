// Authority: test-only. T24b: Hollowmere's static art is in additive scenes that HollowmereSceneLoader loads locally when the town awakes (never through NGO).
// Catches: an art scene missing from Build Settings or never loaded, a loader that loads some but not all, a collider that ended up in an art scene (the play loop would depend on a local
// load), the town no longer being the active scene, an art scene surviving the next Single load (it would pile up across sessions), the art pieces a player sees missing under the old paths.
// NOT covered by a one-process test: a client that loads the town itself and a late joiner (one process cannot load Hollowmere twice; the client declines scenes, so the loader runs once, on
// the host). A real second machine runs the same Awake when NGO's synchronisation loads Hollowmere there: add it to the manual 2-player walk (docs/superpowers/handoff/USER_TODO.md).
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
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

        [UnityTest]
        public IEnumerator Town_LoadsItsArtScenesLocally_KeepsColliders_AndTheNextSingleLoadUnloadsThem()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            int requestedBefore = HollowmereSceneLoader.Requested;   // a process-wide counter: earlier town tests in this run have counted too
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.Hollowmere), "the town stays the active scene");

            yield return PlayModeHarness.WaitUntil(() => HollowmereSceneLoader.AllLoaded(SceneNames.HollowmereArt) && HollowmereSceneLoader.Pending == 0, Timeout, "the art scenes never finished loading");
            Assert.That(HollowmereSceneLoader.Requested - requestedBefore, Is.EqualTo(SceneNames.HollowmereArt.Length), "one load per art scene, none duplicated");

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
            yield return PlayModeHarness.WaitUntil(() => SceneNames.HollowmereArt.All(n => !SceneManager.GetSceneByName(n).isLoaded), 20f, "an art scene survived the next Single load");
        }
    }
}
