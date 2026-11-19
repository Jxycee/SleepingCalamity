// T24b: splits the built Hollowmere scene into the main scene plus additive art scenes (SceneNames.HollowmereArt), so no scene file nears GitHub's size limit and a scoped build
// writes only the sub-scenes a task touched. Common practice: additive scenes for static environment chunks, loaded by a small loader at runtime (HollowmereSceneLoader); lighting,
// RenderSettings and volumes stay defined once in the main (active) scene.
//
// How: the builders still make ONE scene in memory, exactly as before (they depend on each other's colliders and caches), and the Combine / mesh externalize / LOD passes run over all
// of it. Only then does this class move whole subtrees into the sub-scenes, so every frame, mesh asset and name is what the unsplit build made. A subtree moves when the build rules
// below allow it; anything else stays in Hollowmere.unity:
//   - a subtree with a Collider, Rigidbody, NetworkBehaviour, Camera, AudioListener, Volume, probe or Terrain anywhere inside stays (colliders never move: spawn, teleport and late join
//     then do not depend on the additive loads, and the traversal scans and play loop see the same physics world);
//   - a subtree that holds, or is held by, a serialized reference to a scene object outside it stays (cross-scene references would be dropped on save);
//   - a part of a prefab instance below its root cannot be reparented, so a prefab instance moves whole or stays whole.
// A moved subtree keeps its ancestors' names, local transforms and active state (empty copies of Hollowmere/Preset_Ruined/... in the sub-scene), so paths, the Ruined/Rebuilt switch and
// world poses are unchanged.
//
// Scoped saves: SC_SCOPED_SCENES=Town,Outskirts (names without the Hollowmere_ prefix; Main = Hollowmere.unity) writes only those scenes. The in-memory build still runs in full, because the
// builders read each other's colliders and caches; what is skipped is the save, the file normalize and the re-import of the others.
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereSplit
    {
        public const string MainName = "Main";

        // Group name (the child of Preset_Ruined, Preset_Rebuilt or Set; the Set groups are written "Set/<name>") -> sub-scene. Anything not listed stays in the main scene.
        static readonly Dictionary<string, string> GroupScene = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // inside the wall
            { "Foundations", SceneNames.HollowmereTown }, { "Landmarks", SceneNames.HollowmereTown }, { "Dressing", SceneNames.HollowmereTown }, { "Clutter", SceneNames.HollowmereTown },
            { "Corner", SceneNames.HollowmereTown }, { "ContactAO", SceneNames.HollowmereTown }, { "Streets", SceneNames.HollowmereTown }, { "TownWall", SceneNames.HollowmereTown },
            { "Lanes", SceneNames.HollowmereLanes }, { "BellYard", SceneNames.HollowmereTown }, { "HM_Smoke", SceneNames.HollowmereTown }, { "EdgeTufts_RuinedOnly", SceneNames.HollowmereTown },
            { "Wild", SceneNames.HollowmereTown }, { "Foliage", SceneNames.HollowmereTown },
            { "Set/Keep", SceneNames.HollowmereTown }, { "Set/Corner", SceneNames.HollowmereTown }, { "Set/TownWall", SceneNames.HollowmereTown },
            // outside the wall
            { "Roadside", SceneNames.HollowmereOutskirts }, { "Orchard", SceneNames.HollowmereOutskirts }, { "Allotments", SceneNames.HollowmereOutskirts }, { "Road", SceneNames.HollowmereOutskirts },
            { "T13c", SceneNames.HollowmereOutskirts }, { "Revetment", SceneNames.HollowmereOutskirts },
            { "Set/Shore", SceneNames.HollowmereOutskirts }, { "Set/LakeShore", SceneNames.HollowmereOutskirts }, { "Set/Stream", SceneNames.HollowmereOutskirts }, { "Set/Wild", SceneNames.HollowmereOutskirts },
            // the distant ring, ridges, mountain and landmarks (T22)
            { "Set/Country", SceneNames.HollowmereBackdrop },
        };

        // EditorSceneManager.NewScene(Additive) refuses while the open scene is untitled, so the builder gives its empty scene a throwaway path first (an empty file, a moment to write); the real
        // path is set by the final save of the main scene, which also deletes the throwaway. A scoped build that skips the main scene never overwrites Hollowmere.unity this way.
        const string TempScenePath = "Assets/_Project/Scenes/_HollowmereBuild.unity";

        static string TempPath(string subScene) => "Assets/_Project/Scenes/_HollowmereBuild_" + subScene + ".unity";

        static void DeleteTemps()
        {
            foreach (string p in new[] { TempScenePath }.Concat(SceneNames.HollowmereArt.Select(TempPath)))
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null) AssetDatabase.DeleteAsset(p);
        }

        public static void TitleScene(Scene scene)
        {
            DeleteTemps();
            if (!EditorSceneManager.SaveScene(scene, TempScenePath)) throw new InvalidOperationException("Could not write " + TempScenePath);
        }

        public static string ScenePath(string subScene) => "Assets/_Project/Scenes/" + subScene + ".unity";

        public sealed class Result
        {
            public readonly Dictionary<string, Scene> Scenes = new Dictionary<string, Scene>(StringComparer.Ordinal);
            public readonly Dictionary<string, int> MovedObjects = new Dictionary<string, int>(StringComparer.Ordinal);
            public int KeptGroupObjects;
            public readonly SortedDictionary<string, int> KeptBy = new SortedDictionary<string, int>(StringComparer.Ordinal);   // "<preset>/<group> <reason>" -> objects that stay
        }

        // ------------------------------------------------------------------------------------------------ split
        public static Result Split(Scene main)
        {
            var result = new Result();
            GameObject rootGo = main.GetRootGameObjects().FirstOrDefault(g => g.name == "Hollowmere") ?? throw new InvalidOperationException("No Hollowmere root to split");
            Transform root = rootGo.transform;

            // objects that must stay: a blocker component anywhere inside, and cross references
            var tainted = new HashSet<Transform>();
            foreach (Component c in rootGo.GetComponentsInChildren<Component>(true))
                if (c != null && IsBlocker(c)) for (Transform t = c.transform; t != null && tainted.Add(t); t = t.parent) { }
            List<(Transform Owner, Transform Target)> edges = ReferenceEdges(rootGo);
            // A node a project script points at, or carries a project script itself, is never taken apart: TownView hides a lot's ruin object, an ambient script sways its children. Splitting such a node
            // would leave the script acting on the part that stayed behind (the colliders) and not on the art that moved, so it moves whole or stays whole.
            var pinned = new HashSet<Transform>();
            foreach ((Transform o, Transform t) in edges) { pinned.Add(o); pinned.Add(t); }
            foreach (MonoBehaviour mb in rootGo.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("SleepingCalamity", StringComparison.Ordinal)) pinned.Add(mb.transform);

            var moves = new List<(Transform T, string Scene)>();
            int keptObjects = 0;
            foreach (Transform preset in root.Cast<Transform>().ToList())
            {
                bool isSet = preset.name == "Set";
                if (!isSet && !preset.name.StartsWith("Preset_", StringComparison.Ordinal)) continue;
                foreach (Transform group in preset.Cast<Transform>().ToList())
                {
                    string key = isSet ? "Set/" + group.name : group.name;
                    if (!GroupScene.TryGetValue(key, out string target)) continue;
                    if (target == SceneNames.HollowmereTown && preset.name == "Preset_Rebuilt") target = SceneNames.HollowmereRebuilt;   // keeps each file well under 20 MB: the hidden preset's copy of the town art
                    Collect(group, target, tainted, pinned, edges, moves, ref keptObjects, result.KeptBy, preset.name + "/" + group.name);
                }
            }
            result.KeptGroupObjects = keptObjects;

            // create the sub-scenes in a fixed order, then move
            foreach (string name in SceneNames.HollowmereArt)
            {
                Scene s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);   // needs a titled main scene: TitleScene
                if (!EditorSceneManager.SaveScene(s, TempPath(name))) throw new InvalidOperationException("Could not write " + TempPath(name));   // the next NewScene(Additive) refuses while this one is untitled
                result.Scenes[name] = s; result.MovedObjects[name] = 0;
            }
            var chains = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach ((Transform t, string target) in moves)
            {
                Scene sub = result.Scenes[target];
                int n = t.GetComponentsInChildren<Transform>(true).Length;
                Transform parentCopy = EnsureAncestors(sub, target, t.parent, chains);
                Vector3 lp = t.localPosition, ls = t.localScale; Quaternion lr = t.localRotation;
                t.SetParent(null, false);
                SceneManager.MoveGameObjectToScene(t.gameObject, sub);
                t.SetParent(parentCopy, false);
                t.localPosition = lp; t.localRotation = lr; t.localScale = ls;
                result.MovedObjects[target] += n;
            }
            SceneManager.SetActiveScene(main);
            foreach (var kv in result.KeptBy) if (kv.Value >= 20) Debug.Log("SC SPLIT kept " + kv.Value + " objects: " + kv.Key);
            Debug.Log("SC SPLIT moved " + string.Join(", ", result.MovedObjects.Select(kv => kv.Key + "=" + kv.Value + " objects")) + "; " + keptObjects + " objects of the mapped groups stay in the main scene (colliders, references, prefab parts)");
            return result;
        }

        static bool IsBlocker(Component c)
        {
            return c is Collider || c is Rigidbody || c is Joint || c is Unity.Netcode.NetworkBehaviour || c is Camera || c is AudioListener || c is Volume || c is ReflectionProbe
                || c is LightProbeGroup || c is LightProbeProxyVolume || c is Terrain || c is CharacterController;
        }

        // Moves the largest subtrees under 'node' that may move: a node moves whole when nothing inside is a blocker and no reference crosses its boundary; otherwise its children are tried
        // (never the children of a prefab instance: those cannot be reparented).
        static void Collect(Transform node, string target, HashSet<Transform> tainted, HashSet<Transform> pinned, List<(Transform Owner, Transform Target)> edges, List<(Transform T, string Scene)> moves, ref int kept, SortedDictionary<string, int> keptBy, string groupKey)
        {
            bool isTainted = tainted.Contains(node);
            bool crosses = Crosses(node, edges);
            if (!isTainted && !crosses) { moves.Add((node, target)); return; }
            bool whole = crosses || pinned.Contains(node) || PrefabUtility.IsPartOfPrefabInstance(node.gameObject);
            string reason = crosses ? "reference" : pinned.Contains(node) ? "script" : "blocker(collider etc.)";
            int n = whole ? node.GetComponentsInChildren<Transform>(true).Length : 1;
            kept += n;   // a whole node stays with everything under it; otherwise only the node itself stays and its children are tried
            string k = groupKey + " " + reason + (whole ? " (whole subtree)" : "");
            keptBy.TryGetValue(k, out int old); keptBy[k] = old + n;
            if (whole) return;
            foreach (Transform c in node.Cast<Transform>().ToList()) Collect(c, target, tainted, pinned, edges, moves, ref kept, keptBy, groupKey);
        }

        static bool Crosses(Transform node, List<(Transform Owner, Transform Target)> edges)
        {
            foreach ((Transform o, Transform t) in edges)
                if (o.IsChildOf(node) != t.IsChildOf(node)) return true;
            return false;
        }

        // Serialized references from a component to a scene object (a Component or GameObject that is not an asset). Components that only hold asset references are skipped.
        static List<(Transform Owner, Transform Target)> ReferenceEdges(GameObject rootGo)
        {
            var edges = new List<(Transform, Transform)>();
            foreach (Component c in rootGo.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter) continue;
                if (c is MeshRenderer mr)
                {
                    if (mr.probeAnchor != null) edges.Add((mr.transform, mr.probeAnchor));
                    continue;
                }
                using var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool enter = true;
                while (it.Next(enter))   // Next, not NextVisible: [HideInInspector] references count too
                {
                    enter = it.propertyType != SerializedPropertyType.String && !(it.isArray && it.arraySize > 5000);
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object o = it.objectReferenceValue;
                    if (o == null || EditorUtility.IsPersistent(o)) continue;
                    Transform tt = o is Component oc ? oc.transform : o is GameObject og ? og.transform : null;
                    if (tt != null && tt != c.transform) edges.Add((c.transform, tt));
                }
            }
            return edges;
        }

        // The sub-scene's copy of the original ancestor chain (names, local transforms, active state, layer, static flags), created on first use.
        static Transform EnsureAncestors(Scene sub, string target, Transform originalParent, Dictionary<string, Transform> chains)
        {
            var stack = new List<Transform>();
            for (Transform p = originalParent; p != null; p = p.parent) stack.Add(p);
            stack.Reverse();
            Transform parentCopy = null; string key = target;
            foreach (Transform orig in stack)
            {
                key += "/" + orig.name;
                if (!chains.TryGetValue(key, out Transform copy))
                {
                    var go = new GameObject(orig.name);
                    SceneManager.MoveGameObjectToScene(go, sub);
                    copy = go.transform;
                    if (parentCopy != null) copy.SetParent(parentCopy, false);
                    copy.localPosition = orig.localPosition; copy.localRotation = orig.localRotation; copy.localScale = orig.localScale;
                    go.layer = orig.gameObject.layer; go.tag = orig.gameObject.tag;
                    GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(orig.gameObject));
                    go.SetActive(orig.gameObject.activeSelf);
                    chains[key] = copy;
                }
                parentCopy = copy;
            }
            return parentCopy;
        }

        // ------------------------------------------------------------------------------------------------ save
        // Writes the sub-scenes, then the main scene (its NetworkObject hash refresh reopens it, which closes the other scenes, so it goes last). 'only' = scene names to write, null = all.
        public static void Save(Scene main, Result split, string mainPath, ISet<string> only)
        {
            try
            {
                foreach (string name in SceneNames.HollowmereArt)
                {
                    if (only != null && !only.Contains(Short(name))) continue;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    BuilderUtil.SaveScene(split.Scenes[name], ScenePath(name));
                    Debug.Log("SC SPLIT saved " + name + " " + new System.IO.FileInfo(BuilderUtil.Abs(ScenePath(name))).Length / 1024 + " KB in " + sw.Elapsed.TotalSeconds.ToString("F1") + "s");
                }
                if (only != null && !only.Contains(MainName)) return;
                var swm = System.Diagnostics.Stopwatch.StartNew();
                BuilderUtil.SaveScene(main, mainPath);
                Debug.Log("SC SPLIT saved " + mainPath + " " + new System.IO.FileInfo(BuilderUtil.Abs(mainPath)).Length / 1024 + " KB in " + swm.Elapsed.TotalSeconds.ToString("F1") + "s");
            }
            finally { DeleteTemps(); }
        }

        static string Short(string sub) => sub.StartsWith("Hollowmere_", StringComparison.Ordinal) ? sub.Substring("Hollowmere_".Length) : sub;

        // SC_SCOPED_SCENES=Town,Backdrop,Main; null when unset (write everything).
        public static HashSet<string> ScopedFromEnvironment()
        {
            string env = Environment.GetEnvironmentVariable("SC_SCOPED_SCENES");
            if (string.IsNullOrWhiteSpace(env)) return null;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in env.Split(',')) { string n = part.Trim(); if (n.Length > 0) set.Add(n.StartsWith("Hollowmere_", StringComparison.Ordinal) ? Short(n) : n.Equals("Hollowmere", StringComparison.OrdinalIgnoreCase) ? MainName : n); }
            foreach (string n in set)
                if (n != MainName && !SceneNames.HollowmereArt.Any(a => string.Equals(Short(a), n, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("SC_SCOPED_SCENES: unknown scene '" + n + "'; use Main and " + string.Join(", ", SceneNames.HollowmereArt.Select(Short)));
            return set;
        }

        // ------------------------------------------------------------------------------------------------ editor helpers
        // Opens Hollowmere with its art scenes (what a player sees), the main scene active. Every editor tool that used to open Hollowmere.unity alone calls this.
        public static Scene OpenAll(string mainPath)
        {
            Scene main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Single);
            foreach (string name in SceneNames.HollowmereArt)
                if (System.IO.File.Exists(BuilderUtil.Abs(ScenePath(name)))) EditorSceneManager.OpenScene(ScenePath(name), OpenSceneMode.Additive);
            SceneManager.SetActiveScene(main);
            return main;
        }

        // Every root object named "Hollowmere" in the loaded scenes (the main scene's first).
        public static List<GameObject> AllRoots()
        {
            var list = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                foreach (GameObject g in s.GetRootGameObjects()) if (g.name == "Hollowmere") list.Add(g);
            }
            return list.OrderBy(g => g.scene == SceneManager.GetActiveScene() ? 0 : 1).ToList();
        }
    }
}
