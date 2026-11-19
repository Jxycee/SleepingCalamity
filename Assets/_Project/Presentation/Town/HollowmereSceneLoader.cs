// Authority: none. Local presentation: every peer (host, clients, late joiners) runs its own copy and loads the town's static art itself; NGO never sees these scenes.
// T24b: Hollowmere.unity keeps everything the play loop needs (terrain, every collider, NetworkObjects, cameras, the directional light, volumes, probes, hub and spawn); its static art
// (dressing, foliage, decals, combined meshes, the backdrop, and the local point lights that belong to that art) lives in the additive scenes SceneNames.HollowmereArt, written by
// HollowmereSplit. This component sits on the main scene's "Hollowmere" root and loads them additively when that scene awakes, which happens on the host when NGO loads the town and on each
// client when it synchronises to it (a late joiner too). Because no collider lives in the additive scenes, nothing a player can touch depends on them: a hero that spawns before they
// finish loading stands on the terrain and the main scene's colliders, and the art (and its point lights) pops in.
// T24c teardown: the loader owns the art scenes it asked for. When it is destroyed (the town goes: SceneFlow loads the Lobby, the session ends, a test tears down) it unloads them, and a
// load still in flight is unloaded the moment it completes, so no art scene can survive into the Lobby and make the next town load skip its art. A scene of that name that no live loader
// owns (an orphan, or one the editor opened for Play) is stale: it is unloaded first, then loaded fresh, so each art scene exists exactly once. One slot per scene name serialises this.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Presentation
{
    public sealed class HollowmereSceneLoader : MonoBehaviour
    {
        public string[] Scenes = new string[0];

        enum Phase { Loading, Loaded, Unloading }

        sealed class Slot
        {
            public string Name;
            public HollowmereSceneLoader Owner;   // null (Unity null too) when no live loader wants this scene
            public Scene Scene;
            public Phase State;
            public bool WantLoad;                 // an owner arrived while the scene was unloading: load it again when the unload ends
        }

        static readonly Dictionary<string, Slot> s_slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        static int s_pending;
        static int s_requested;
        static bool s_quitting;

        // Loads and unloads started and not yet finished (all peers' loaders in this process; tests wait on it).
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

        // How many loaded scenes carry this name (GetSceneByName sees only the first one, so a duplicate would hide).
        public static int CountLoaded(string name)
        {
            int n = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++) { Scene s = SceneManager.GetSceneAt(i); if (s.isLoaded && s.name == name) n++; }
            return n;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pending = 0; s_requested = 0; s_quitting = false; s_slots.Clear();
            Application.quitting -= OnQuitting; Application.quitting += OnQuitting;
        }

        static void OnQuitting() { s_quitting = true; }

        void Awake()
        {
            foreach (string sceneName in Scenes)
                if (!string.IsNullOrEmpty(sceneName)) Request(sceneName, this);
        }

        void OnDestroy()
        {
            if (s_quitting) return;
            foreach (Slot slot in s_slots.Values.Where(s => ReferenceEquals(s.Owner, this)).ToList())
            {
                slot.Owner = null; slot.WantLoad = false;
                if (slot.State == Phase.Loaded) BeginUnload(slot);   // a load in flight unloads itself when it completes (OnLoaded)
            }
        }

        static void Request(string name, HollowmereSceneLoader owner)
        {
            if (s_slots.TryGetValue(name, out Slot slot))
            {
                bool ownerAlive = slot.Owner != null && slot.Owner.gameObject.scene.isLoaded;   // a loader whose town scene is already going away owns nothing
                switch (slot.State)
                {
                    case Phase.Loading:
                        if (!ownerAlive) slot.Owner = owner;   // the loader that asked is gone: its load in flight serves the new one
                        return;
                    case Phase.Unloading:
                        if (!ownerAlive) { slot.Owner = owner; slot.WantLoad = true; }
                        return;
                    case Phase.Loaded:
                        if (ownerAlive) return;   // a live loader owns it
                        if (slot.Scene.IsValid() && slot.Scene.isLoaded) { slot.Owner = owner; slot.WantLoad = true; BeginUnload(slot); return; }   // orphan: stale, reload fresh
                        s_slots.Remove(name);
                        break;
                }
            }
            if (!Application.CanStreamedLevelBeLoaded(name)) { Debug.LogError("HollowmereSceneLoader: " + name + " is not in Build Settings; the town's art will be missing."); return; }
            var fresh = new Slot { Name = name, Owner = owner };
            s_slots[name] = fresh;
            var stale = new List<Scene>();   // a copy nobody here loaded: the editor had it open for Play, or it outlived its loader in another scene context
            for (int i = 0; i < SceneManager.sceneCount; i++) { Scene s = SceneManager.GetSceneAt(i); if (s.name == name) stale.Add(s); }
            if (stale.Count == 0) { BeginLoad(fresh); return; }
            fresh.State = Phase.Unloading; fresh.WantLoad = true;
            int left = 0;
            foreach (Scene s in stale)
            {
                if (!s.isLoaded) continue;
                AsyncOperation op = SceneManager.UnloadSceneAsync(s);
                if (op == null) { Debug.LogError("HollowmereSceneLoader: Unity refused to unload the stale scene " + s.name + "; not loading a second copy"); fresh.WantLoad = false; continue; }   // a reload now would leave two copies
                left++; s_pending++;
                op.completed += _ => { s_pending--; if (--left == 0) FinishUnload(fresh); };
            }
            if (left == 0) FinishUnload(fresh);
        }

        static void BeginLoad(Slot slot)
        {
            slot.State = Phase.Loading;
            AsyncOperation op = SceneManager.LoadSceneAsync(slot.Name, LoadSceneMode.Additive);
            if (op == null) { Forget(slot); return; }
            s_pending++; s_requested++;
            op.completed += _ => OnLoaded(slot);
        }

        static void OnLoaded(Slot slot)
        {
            s_pending--;
            if (!s_slots.TryGetValue(slot.Name, out Slot current) || current != slot) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.isLoaded && s.name == slot.Name) { slot.Scene = s; break; }
            }
            slot.State = Phase.Loaded;
            if (slot.Owner == null && !s_quitting) BeginUnload(slot);   // the town went while this was loading
        }

        static void BeginUnload(Slot slot)
        {
            if (!slot.Scene.IsValid() || !slot.Scene.isLoaded) { FinishUnload(slot); return; }   // already gone (the Single load that took the town down unloaded it)
            AsyncOperation op = SceneManager.UnloadSceneAsync(slot.Scene);
            if (op == null) { Debug.LogError("HollowmereSceneLoader: Unity refused to unload " + slot.Name + "; not reloading it"); slot.WantLoad = false; FinishUnload(slot); return; }
            slot.State = Phase.Unloading;
            s_pending++;
            op.completed += _ => { s_pending--; FinishUnload(slot); };
        }

        static void FinishUnload(Slot slot)
        {
            if (slot.WantLoad && slot.Owner != null && !s_quitting) { slot.WantLoad = false; BeginLoad(slot); return; }
            Forget(slot);
        }

        static void Forget(Slot slot)
        {
            if (s_slots.TryGetValue(slot.Name, out Slot current) && current == slot) s_slots.Remove(slot.Name);
        }
    }
}
