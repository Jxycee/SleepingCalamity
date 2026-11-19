// T24b: splits the built Hollowmere scene into the main scene plus additive art scenes (SceneNames.HollowmereArt), so no scene file nears GitHub's size limit and a scoped build
// writes only the sub-scenes a task touched. Common practice: additive scenes for static environment chunks, loaded by a small loader at runtime (HollowmereSceneLoader). The directional
// light, RenderSettings, volumes and probes stay defined once in the main (active) scene; the local point lights (brazier flicker included) are not blockers, so they travel with the art
// they light and pop in with it (T24c, controller decision: kept there).
//
// How: the builders still make ONE scene in memory, exactly as before (they depend on each other's colliders and caches), and the Combine / mesh externalize / LOD passes run over all
// of it. Only then does this class move whole subtrees into the sub-scenes, so every frame, mesh asset and name is what the unsplit build made. A subtree moves when the build rules
// below allow it; anything else stays in Hollowmere.unity:
//   - a subtree with a Collider, Rigidbody, NetworkBehaviour, Camera, AudioListener, Volume, probe or Terrain anywhere inside stays (colliders never move: spawn, teleport and late join
//     then do not depend on the additive loads, and the traversal scans and play loop see the same physics world);
//   - a subtree that holds, or is held by, a serialized reference to a scene object outside it, or a reference into one of its ancestors below the scene root, stays (cross-scene
//     references would be dropped on save, and an ancestor's children change when a subtree leaves it);
//   - a part of a prefab instance below its root cannot be reparented, so a prefab instance moves whole or stays whole.
// A moved subtree keeps its ancestors' names, local transforms and active state (empty copies of Hollowmere/Preset_Ruined/... in the sub-scene), so paths, the Ruined/Rebuilt switch and
// world poses are unchanged.
//
// Scoped saves: SC_SCOPED_SCENES=Town,Outskirts (names without the Hollowmere_ prefix; Main = Hollowmere.unity) writes only those scenes. The in-memory build still runs in full, because the
// builders read each other's colliders and caches; what is skipped is the save, the file normalize and the re-import of the others.
// The full build writes ManifestPath: the sorted hierarchy path of every moved subtree, per scene. A scoped save recomputes them and refuses when any scene differs from the manifest (a
// builder change moved other subtrees: the scenes on disk would then disagree and art would be missing or twice); run the full build then.
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
            public readonly Dictionary<string, List<string>> MovedPaths = new Dictionary<string, List<string>>(StringComparer.Ordinal);   // scene -> hierarchy path of each moved subtree root (sorted)
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
            foreach (string name in SceneNames.HollowmereArt) result.MovedPaths[name] = new List<string>();
            foreach ((Transform t, string target) in moves)
            {
                result.MovedPaths[target].Add(PathOf(t));   // before the move: the path in the unsplit scene
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
            foreach (List<string> paths in result.MovedPaths.Values) paths.Sort(StringComparer.Ordinal);
            SceneManager.SetActiveScene(main);
            foreach (var kv in result.KeptBy) if (kv.Value >= 20) Debug.Log("SC SPLIT kept " + kv.Value + " objects: " + kv.Key);
            Debug.Log("SC SPLIT moved " + string.Join(", ", result.MovedObjects.Select(kv => kv.Key + "=" + kv.Value + " objects")) + "; " + keptObjects + " objects of the mapped groups stay in the main scene (colliders, references, prefab parts)");
            return result;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name + "[" + SameNameIndex(t) + "]");   // T24c review: the occurrence among same-named siblings, so swapping which twin moves changes the path
            parts.Reverse();
            return string.Join("/", parts);
        }

        static int SameNameIndex(Transform t)
        {
            if (t.parent == null) return 0;
            int k = 0;
            for (int i = 0; i < t.GetSiblingIndex(); i++) if (t.parent.GetChild(i).name == t.name) k++;
            return k;
        }

        // The manifest path without the occurrence suffixes, for Transform.Find.
        public static string PlainPath(string manifestPath) => System.Text.RegularExpressions.Regex.Replace(manifestPath, @"\[\d+\](?=/|$)", "");

        static bool IsBlocker(Component c)
        {
            return c is Collider || c is Rigidbody || c is Joint || c is Unity.Netcode.NetworkBehaviour || c is Unity.Netcode.NetworkObject || c is Camera || c is AudioListener || c is Volume || c is ReflectionProbe
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
            {
                bool oIn = o.IsChildOf(node), tIn = t.IsChildOf(node);
                if (oIn != tIn) return true;
                // T24c: a reference into an ancestor of the node (a script pointing at Preset_* or Set, or at a group above the node): taking the node out changes that ancestor's children, which
                // the pointing script may walk. The scene root is exempt (it stays in the main scene, and every node is below it). Owners on an ancestor do not count: the root carries scripts.
                if (!tIn && t.parent != null && node.IsChildOf(t)) return true;
            }
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
                string manifest = ManifestText(split);
                if (only != null) CheckManifest(manifest);   // before anything is written: a scoped save must not leave the scenes on disk disagreeing with each other
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
                if (only == null) WriteManifest(manifest);   // after the main save: a failed save leaves the old manifest, not one for scenes that were never written together
            }
            finally { DeleteTemps(); }
        }

        // ------------------------------------------------------------------------------------------------ manifest (T24c)
        public const string ManifestPath = "Assets/_Project/Scenes/Hollowmere_SplitManifest.txt";

        // One block per art scene in SceneNames order: "[scene] count", then the sorted hierarchy path of each moved subtree, LF line ends.
        public static string ManifestText(Result split)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string name in SceneNames.HollowmereArt)
            {
                List<string> paths = split.MovedPaths[name];
                sb.Append('[').Append(name).Append("] ").Append(paths.Count).Append('\n');
                foreach (string p in paths) sb.Append(p).Append('\n');
            }
            return sb.ToString();
        }

        // LF only, whatever the checkout's line endings (autocrlf on a PC checkout turns the file into CRLF unless .gitattributes pins it).
        public static string ReadManifest(string absPath) => System.IO.File.ReadAllText(absPath).Replace("\r\n", "\n");

        static void WriteManifest(string text)
        {
            string abs = BuilderUtil.Abs(ManifestPath);
            if (System.IO.File.Exists(abs) && ReadManifest(abs) == text) return;
            System.IO.File.WriteAllText(abs, text, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("SC SPLIT manifest written " + ManifestPath + " (" + text.Length / 1024 + " KB)");
        }

        // Throws (the scoped build fails, exit non-zero) when the subtrees this build moves differ from the manifest the last full build wrote, for any scene.
        static void CheckManifest(string text)
        {
            string abs = BuilderUtil.Abs(ManifestPath);
            if (!System.IO.File.Exists(abs)) throw new InvalidOperationException("Scoped save refused: " + ManifestPath + " is missing. Run the full Hollowmere build (no SC_SCOPED_SCENES) first.");
            string old = ReadManifest(abs);
            if (old == text) return;
            Dictionary<string, HashSet<string>> was = ParseManifest(old), now = ParseManifest(text);
            var sb = new System.Text.StringBuilder("Scoped save refused: the subtrees this build moves into the art scenes differ from " + ManifestPath + ", so the scenes on disk would disagree (art missing or twice). Run the full Hollowmere build (no SC_SCOPED_SCENES) and commit the manifest.");
            foreach (string name in SceneNames.HollowmereArt)
            {
                was.TryGetValue(name, out HashSet<string> a); now.TryGetValue(name, out HashSet<string> b);
                a ??= new HashSet<string>(StringComparer.Ordinal); b ??= new HashSet<string>(StringComparer.Ordinal);
                List<string> added = b.Where(x => !a.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList(), removed = a.Where(x => !b.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
                if (added.Count == 0 && removed.Count == 0) continue;   // equal as sets: only the multiplicity of a repeated path differs
                sb.Append("\n  ").Append(name).Append(": +").Append(added.Count).Append(" -").Append(removed.Count);
                foreach (string x in added.Take(3)) sb.Append("\n    + ").Append(x);
                foreach (string x in removed.Take(3)) sb.Append("\n    - ").Append(x);
            }
            throw new InvalidOperationException(sb.ToString());
        }

        static Dictionary<string, HashSet<string>> ParseManifest(string text)
        {
            var d = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            HashSet<string> cur = null;
            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                if (line[0] == '[') { string name = line.Substring(1, line.IndexOf(']') - 1); d[name] = cur = new HashSet<string>(StringComparer.Ordinal); }
                else cur?.Add(line);
            }
            return d;
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
