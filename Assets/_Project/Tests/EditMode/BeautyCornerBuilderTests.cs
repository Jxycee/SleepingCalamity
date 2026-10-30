// Authority: none (pure EditMode tests). The beauty corner (M9) builder: its placement table covers every row of docs/art/beauty-corner-brief.md and the scene builds.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.EditMode
{
    public class BeautyCornerBuilderTests
    {
        [Test]
        public void Table_CoversEveryBriefRow()
        {
            foreach (string brief in BeautyCornerTable.BriefRows)
                Assert.IsTrue(BeautyCornerTable.ForBrief(brief).Any(), "no placement row for brief row: " + brief);
            foreach (BeautyCornerTable.Row r in BeautyCornerTable.Rows)
            {
                Assert.Contains(r.Brief, BeautyCornerTable.BriefRows, "row " + r.Id + " names an unknown brief row");
                Assert.IsNotEmpty(r.Instances, r.Id + " has no instances");
                Assert.IsNotEmpty(r.Placeholder, r.Id + " has no placeholder parts");
                Assert.IsNotEmpty(r.Names, r.Id + " has no art folder names");
            }
            Assert.AreEqual(BeautyCornerTable.Rows.Length, BeautyCornerTable.Rows.Select(r => r.Id).Distinct().Count(), "row ids must be unique");
        }

        [Test]
        public void Table_PropsClusterHasTheBriefCounts()
        {
            Assert.AreEqual(3, BeautyCornerTable.InstanceCount("Barrels"));
            Assert.AreEqual(3, BeautyCornerTable.InstanceCount("Crates"));
            Assert.AreEqual(4, BeautyCornerTable.InstanceCount("Sacks"));
            Assert.AreEqual(2, BeautyCornerTable.InstanceCount("Buckets"));
            Assert.AreEqual(3, BeautyCornerTable.InstanceCount("Baskets"));
            Assert.AreEqual(2, BeautyCornerTable.InstanceCount("Jugs"));
            Assert.AreEqual(2, BeautyCornerTable.ForBrief(BeautyCornerTable.Lanterns).Sum(r => r.Instances.Length), "one stall lantern, one bracket lantern");
        }

        [Test]
        public void Table_CamerasMatchTheBrief()
        {
            var cams = BeautyCornerTable.Cameras.ToDictionary(c => c.Name);
            Assert.AreEqual(new Vector3(1f, 1.8f, -9f), cams["Cap_Corner_Wide"].Pos);
            Assert.AreEqual(new Vector3(-1f, 3f, 6f), cams["Cap_Corner_Wide"].Target);
            Assert.AreEqual(new Vector3(5f, 1.7f, -2.5f), cams["Cap_Corner_Mid"].Pos);
            Assert.AreEqual(new Vector3(-1.5f, 1.8f, 5f), cams["Cap_Corner_Mid"].Target);
            Assert.AreEqual(new Vector3(1.6f, 1.4f, -1.6f), cams["Cap_Corner_Close"].Pos);
            Assert.AreEqual(new Vector3(0f, 1.0f, 0.6f), cams["Cap_Corner_Close"].Target);
        }

        [Test]
        public void Scene_BuildsWithEveryRowAndTheThreeCameras()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                BeautyCornerBuilder.Build();
                Scene scene = EditorSceneManager.OpenScene(BeautyCornerBuilder.ScenePath, OpenSceneMode.Single);
                GameObject root = scene.GetRootGameObjects().First(g => g.name == "BeautyCorner");
                Transform set = root.transform.Find("Set");
                foreach (BeautyCornerTable.Row r in BeautyCornerTable.Rows)
                {
                    Transform group = set.Find(r.Id);
                    Assert.IsNotNull(group, "scene has no group for " + r.Id);
                    Assert.AreEqual(r.Instances.Length, group.childCount, r.Id + " instance count");
                }
                Assert.AreEqual(3, root.GetComponentsInChildren<Camera>(true).Length);
                Assert.IsNotNull(RenderSettings.sun);
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
