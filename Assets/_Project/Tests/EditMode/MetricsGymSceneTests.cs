// Tests/EditMode/MetricsGymSceneTests.cs: the metrics gym scene exists, records GameMetrics.Rows() and has one root per lane (metrics gym plan, Task 7). Editor-only, so not in the Core test set.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.EditorTools;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.EditMode
{
    public class MetricsGymSceneTests
    {
        [Test]
        public void Scene_RecordsTheMetrics()
        {
            Assert.That(System.IO.File.Exists(BuilderUtil.Abs(AssetPaths.MetricsGymScene)), "MetricsGym.unity is missing; run the build");
            Scene scene = EditorSceneManager.OpenScene(AssetPaths.MetricsGymScene, OpenSceneMode.Additive);
            try
            {
                var found = new System.Collections.Generic.List<MetricsGymRecord>();
                foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<MetricsGymRecord>(true));
                Assert.That(found.Count, Is.EqualTo(1));
                var rows = GameMetrics.Rows();
                Assert.That(found[0].Keys.Length, Is.EqualTo(rows.Count));
                for (int i = 0; i < rows.Count; i++)
                {
                    Assert.That(found[0].Keys[i], Is.EqualTo(rows[i].Key));
                    Assert.That(found[0].Values[i], Is.EqualTo(rows[i].Value).Within(1e-4f), rows[i].Key);
                    Assert.That(found[0].Units[i], Is.EqualTo(rows[i].Unit));
                }
                foreach (string lane in MetricsGymLayout.Lanes)
                {
                    int n = 0;
                    foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Lane_" + lane) n++;
                    Assert.That(n, Is.EqualTo(1), "Lane_" + lane);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
