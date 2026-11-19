// T24b: the Hollowmere scene split. Catches: a scene file over the size budget, a collider, NetworkObject, camera or volume that ended up in an additive art scene (spawn, teleport and
// late join would then depend on a local load), a missing loader or Build Settings entry (the art would silently not load), and the main scene losing its play-loop objects.
// T24c adds the manifest of moved subtrees (a scoped save compares against it).
// NOT covered here: the art arriving at runtime (PlayMode HollowmereSceneLoaderTests start the town and check each art scene loads once, with no collider); a real second machine loading
// the scenes (a user walk).
using System.IO;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.EditorTools;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.EditMode
{
    public class HollowmereSplitTests
    {
        const long MaxSceneBytes = 20L * 1024 * 1024;

        static string Path(string scene) => BuilderUtil.Abs("Assets/_Project/Scenes/" + scene + ".unity");

        [Test]
        public void EverySceneFile_IsUnderTwentyMegabytes()
        {
            foreach (string name in SceneNames.HollowmereArt.Concat(new[] { SceneNames.Hollowmere }))
            {
                Assert.That(File.Exists(Path(name)), Is.True, name + ".unity exists");
                Assert.That(new FileInfo(Path(name)).Length, Is.LessThan(MaxSceneBytes), name + ".unity is under 20 MB");
            }
        }

        [Test]
        public void ArtScenes_AreInBuildSettings_AfterTheGameplayScenes()
        {
            string[] expected = SceneNames.BuildOrder.Concat(SceneNames.HollowmereArt).Select(n => "Assets/_Project/Scenes/" + n + ".unity").ToArray();
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path).ToArray(), Is.EqualTo(expected));
            Assert.That(EditorBuildSettings.scenes.All(s => s.enabled), Is.True);
        }

        [Test]
        public void ArtScenes_HoldNoPhysicsNetworkingCamerasOrVolumes()
        {
            foreach (string name in SceneNames.HollowmereArt)
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + name + ".unity", OpenSceneMode.Additive);
                try
                {
                    int renderers = 0;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        Assert.That(root.name, Is.EqualTo("Hollowmere"), name + " root");
                        Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, name + " has no collider");
                        Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty, name + " has no rigidbody");
                        Assert.That(root.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true), Is.Empty, name + " has no NetworkBehaviour");
                        Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty, name + " has no camera");
                        Assert.That(root.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "Volume"), Is.False, name + " has no volume");
                        renderers += root.GetComponentsInChildren<Renderer>(true).Length;
                    }
                    Assert.That(renderers, Is.GreaterThan(0), name + " holds art");
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [Test]
        public void NgoSync_RefusesTheArtScenes_AndNothingElse()
        {
            foreach (string art in SceneNames.HollowmereArt)
            {
                Assert.That(SceneFlow.AllowsNgoSceneLoad(art, LoadSceneMode.Additive), Is.False, art + " is loaded locally, never through NGO's sync");
                Assert.That(SceneNames.IsHollowmereArt(art), Is.True, art);
                Assert.That(SceneNames.IsGameplay(art), Is.False, art + " is not a gameplay scene");
            }
            Assert.That(SceneFlow.AllowsNgoSceneLoad(SceneNames.Hollowmere, LoadSceneMode.Single), Is.True);
            Assert.That(SceneFlow.AllowsNgoSceneLoad(SceneNames.ArenaCombat, LoadSceneMode.Additive), Is.True);
            Assert.That(SceneNames.IsHollowmereArt(null), Is.False);
            Assert.That(SceneNames.IsHollowmereArt(SceneNames.Hollowmere), Is.False);
        }

        // TownView hides a lot's ruin object while a building stands there: if the split took that object apart, the art that moved would stay visible. Scripts' targets move whole or stay whole.
        [Test]
        public void TownViewRuinObjects_StayWholeInTheMainScene()
        {
            Scene main = EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Additive);
            var art = SceneNames.HollowmereArt.Select(n => EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + n + ".unity", OpenSceneMode.Additive)).ToList();
            try
            {
                GameObject root = main.GetRootGameObjects().First(g => g.name == "Hollowmere");
                TownView view = root.GetComponentsInChildren<TownView>(true).First();
                int checkedLots = 0;
                foreach (TownView.Lot lot in view.Lots)
                {
                    if (lot.Ruin == null) continue;
                    checkedLots++;
                    var names = new System.Collections.Generic.List<string>();
                    for (Transform t = lot.Ruin.transform; t.parent != null; t = t.parent) names.Insert(0, t.name);
                    string rel = string.Join("/", names);   // relative to the Hollowmere root
                    foreach (Scene a in art)
                        foreach (GameObject r in a.GetRootGameObjects())
                            Assert.That(r.transform.Find(rel), Is.Null, "the ruin object " + rel + " exists in " + a.name + ": part of its art moved out of the object TownView hides");
                }
                Assert.That(checkedLots, Is.GreaterThan(3), "the lots' ruin objects were found (TownView has 7)");
            }
            finally { foreach (Scene a in art) EditorSceneManager.CloseScene(a, true); EditorSceneManager.CloseScene(main, true); }
        }

        // T24c: the manifest a scoped save checks against exists, is sorted, and every path it lists is really in its art scene (so it was written from this split, not a stale one).
        [Test]
        public void Manifest_ListsEveryMovedSubtree_AndEachIsInItsArtScene()
        {
            string abs = BuilderUtil.Abs(HollowmereSplit.ManifestPath);
            Assert.That(File.Exists(abs), Is.True, HollowmereSplit.ManifestPath + " is missing: run the full Hollowmere build and commit it");
            string[] lines = HollowmereSplit.ReadManifest(abs).Split('\n').Where(l => l.Length > 0).ToArray();
            var open = SceneNames.HollowmereArt.ToDictionary(n => n, n => EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + n + ".unity", OpenSceneMode.Additive));
            try
            {
                int i = 0, total = 0;
                foreach (string name in SceneNames.HollowmereArt)
                {
                    Assert.That(lines[i], Does.StartWith("[" + name + "] "), "one block per art scene, in SceneNames order");
                    int count = int.Parse(lines[i].Substring(name.Length + 3));
                    Assert.That(count, Is.GreaterThan(0), name + " has moved subtrees");
                    GameObject root = open[name].GetRootGameObjects().First(g => g.name == "Hollowmere");
                    string previous = null;
                    for (int k = 1; k <= count; k++)
                    {
                        string path = lines[i + k];
                        Assert.That(path, Does.StartWith("Hollowmere/"), name);
                        if (previous != null) Assert.That(string.CompareOrdinal(previous, path), Is.LessThanOrEqualTo(0), name + ": paths are sorted");
                        previous = path;
                        Assert.That(root.transform.Find(path.Substring("Hollowmere/".Length)), Is.Not.Null, name + " does not hold " + path);
                        total++;
                    }
                    i += count + 1;
                }
                Assert.That(i, Is.EqualTo(lines.Length), "nothing after the last block");
                Assert.That(total, Is.GreaterThan(100), "the manifest is not empty");
            }
            finally { foreach (Scene s in open.Values) EditorSceneManager.CloseScene(s, true); }
        }

        [Test]
        public void MainScene_KeepsThePlayLoopAndTheLoader()
        {
            Scene scene = EditorSceneManager.OpenScene(HollowmereBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject root = scene.GetRootGameObjects().First(g => g.name == "Hollowmere");
                HollowmereSceneLoader loader = root.GetComponent<HollowmereSceneLoader>();
                Assert.That(loader, Is.Not.Null, "the loader is on the Hollowmere root");
                Assert.That(loader.Scenes, Is.EqualTo(SceneNames.HollowmereArt), "the loader loads every art scene");
                Assert.That(root.GetComponentsInChildren<Gameplay.SpawnPoint>(true).Length, Is.EqualTo(1), "one spawn point");
                Assert.That(root.GetComponentsInChildren<Terrain>(true).Length, Is.GreaterThan(0), "terrain");
                Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.GreaterThan(1000), "the colliders stay in the main scene");
                Assert.That(root.GetComponentsInChildren<Camera>(true).Length, Is.GreaterThan(20), "the capture cameras stay in the main scene");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
