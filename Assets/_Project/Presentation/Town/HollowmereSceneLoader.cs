// Authority: none. Local presentation: every peer (host, clients, late joiners) runs its own copy and loads the town's static art itself; NGO never sees these scenes.
// T24b: Hollowmere.unity keeps everything the play loop needs (terrain, every collider, NetworkObjects, cameras, lighting, volumes, hub and spawn); its static art (dressing, foliage, decals,
// combined meshes, the backdrop) lives in the additive scenes SceneNames.HollowmereArt, written by HollowmereSplit. This component sits on the main scene's "Hollowmere" root and loads them
// additively when that scene awakes, which happens on the host when NGO loads the town and on each client when it synchronises to it (a late joiner too). Because no collider lives in the
// additive scenes, nothing a player can touch depends on them: a hero that spawns before they finish loading stands on the terrain and the main scene's colliders, and the art pops in.
// They are not unloaded by hand: the next Single load (SceneFlow back to the Lobby, a test's next scene) unloads every loaded scene, the additive ones with the town.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Presentation
{
    public sealed class HollowmereSceneLoader : MonoBehaviour
    {
        public string[] Scenes = new string[0];

        static int s_pending;
        static int s_requested;

        // Loads started and not yet finished (all peers' loaders in this process; tests wait on it).
        public static int Pending => s_pending;
        public static int Requested => s_requested;

        public static bool AllLoaded(IEnumerable<string> names)
        {
            foreach (string n in names)
            {
                Scene s = SceneManager.GetSceneByName(n);
                if (!s.IsValid() || !s.isLoaded) return false;
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_pending = 0; s_requested = 0; }

        void Awake()
        {
            foreach (string sceneName in Scenes)
            {
                if (string.IsNullOrEmpty(sceneName)) continue;
                Scene already = SceneManager.GetSceneByName(sceneName);
                if (already.IsValid()) continue;   // the editor has it open (Play with the sub-scenes open), or another loader already asked
                if (!Application.CanStreamedLevelBeLoaded(sceneName)) { Debug.LogError("HollowmereSceneLoader: " + sceneName + " is not in Build Settings; the town's art will be missing."); continue; }
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (op == null) continue;
                s_pending++; s_requested++;
                op.completed += _ => { s_pending--; };
            }
        }
    }
}
