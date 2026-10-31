// Tests/EditMode/SceneFileNormalizerTests.cs: the scene file normaliser (Editor/Builders/SceneFileNormalizer) that makes BuildAll idempotent.
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.EditMode
{
    public class SceneFileNormalizerTests
    {
        // A scene shaped like Unity's output: fixed settings documents, two root objects (camera with transform and listener, light with a child),
        // SceneRoots last. The ids are parameters, standing in for Unity's random ones; docsFirst picks which root's documents come first in the file.
        static string Scene(long cam, long camTf, long lis, long light, long lightTf, long kid, long kidTf, bool lightFirst)
        {
            string camera =
                $"--- !u!1 &{cam}\nGameObject:\n  m_Name: Cam\n  m_Component:\n  - component: {{fileID: {camTf}}}\n  - component: {{fileID: {lis}}}\n" +
                $"--- !u!4 &{camTf}\nTransform:\n  m_GameObject: {{fileID: {cam}}}\n  m_Children: []\n  m_Father: {{fileID: 0}}\n" +
                $"--- !u!81 &{lis}\nAudioListener:\n  m_GameObject: {{fileID: {cam}}}\n";
            string lightDocs =
                $"--- !u!1 &{light}\nGameObject:\n  m_Name: Sun\n  m_Component:\n  - component: {{fileID: {lightTf}}}\n" +
                $"--- !u!4 &{lightTf}\nTransform:\n  m_GameObject: {{fileID: {light}}}\n  m_Children:\n  - {{fileID: {kidTf}}}\n  m_Father: {{fileID: 0}}\n" +
                $"--- !u!1 &{kid}\nGameObject:\n  m_Name: Kid\n  m_Component:\n  - component: {{fileID: {kidTf}}}\n" +
                $"--- !u!4 &{kidTf}\nTransform:\n  m_GameObject: {{fileID: {kid}}}\n  m_Children: []\n  m_Father: {{fileID: {lightTf}}}\n" +
                "  m_Script: {fileID: 11500000, guid: abc, type: 3}\n";
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  m_SceneGUID: 0\n" +
                (lightFirst ? lightDocs + camera : camera + lightDocs) +
                $"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {{fileID: {camTf}}}\n  - {{fileID: {lightTf}}}\n";
        }

        [Test]
        public void SameScene_WithDifferentRandomIdsAndFileOrder_NormalizesToIdenticalText()
        {
            string a = SceneFileNormalizer.Normalize(Scene(752349271, 752349273, 752349272, 1694564916, 1694564919, 1694564920, 1694564921, true));
            string b = SceneFileNormalizer.Normalize(Scene(2025133072, 2025133075, 2025133073, 1694556615, 1694556617, 1694556620, 1694556621, false));
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void Normalize_IsIdempotent_KeepsFixedDocsAndGuidReferences_AndTheTrailingNewline()
        {
            string once = SceneFileNormalizer.Normalize(Scene(5, 6, 7, 8, 9, 10, 11, false));
            Assert.That(SceneFileNormalizer.Normalize(once), Is.EqualTo(once));
            StringAssert.Contains("--- !u!29 &1\n", once);
            StringAssert.Contains("--- !u!1660057539 &9223372036854775807\n", once);
            StringAssert.Contains("{fileID: 11500000, guid: abc, type: 3}", once);
            Assert.That(once.EndsWith("\n"), Is.True);
            Assert.That(once.EndsWith("\n\n"), Is.False);
        }

        [Test]
        public void Normalize_NumbersByHierarchy_AndRewritesEveryReference()
        {
            string s = SceneFileNormalizer.Normalize(Scene(900, 901, 902, 100, 101, 50, 51, true));
            // Roots in SceneRoots order: camera first (1000 GO, 1001 transform, 1002 listener), then light (1003, 1004), then its child (1005, 1006).
            StringAssert.Contains("--- !u!1 &1000\nGameObject:\n  m_Name: Cam", s);
            StringAssert.Contains("--- !u!4 &1004\nTransform:\n  m_GameObject: {fileID: 1003}\n  m_Children:\n  - {fileID: 1006}", s);
            StringAssert.Contains("m_Father: {fileID: 1004}", s);
            StringAssert.Contains("m_Roots:\n  - {fileID: 1001}\n  - {fileID: 1004}", s);
            Assert.That(s.IndexOf("&1000"), Is.LessThan(s.IndexOf("&1003")), "documents are written in new-id order");
        }

        // A PrefabInstance placed under a scene object through its stripped transform, as Unity saves a prefab dropped into a scene.
        static string PrefabScene(long inst, long strippedTf, long go, long tf, bool instFirst)
        {
            string instance = $"--- !u!1001 &{inst}\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {{fileID: {tf}}}\n    m_Modifications:\n" +
                "    - target: {fileID: -8679921383154817045, guid: abc, type: 3}\n      objectReference: {fileID: 0}\n  m_SourcePrefab: {fileID: 100100000, guid: abc, type: 3}\n";
            string stripped = $"--- !u!4 &{strippedTf} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: -8679921383154817045, guid: abc, type: 3}}\n  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" +
                (instFirst ? instance + stripped : stripped + instance) +
                $"--- !u!1 &{go}\nGameObject:\n  m_Component:\n  - component: {{fileID: {tf}}}\n" +
                $"--- !u!4 &{tf}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n  m_Children:\n  - {{fileID: {strippedTf}}}\n" +
                $"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {{fileID: {tf}}}\n";
        }

        [Test]
        public void Normalize_RenumbersPrefabInstancesAndStrippedTransforms_AndKeepsGuidReferences()
        {
            string a = SceneFileNormalizer.Normalize(PrefabScene(5760954, 5760955, 13215343, 13215344, true));
            string b = SceneFileNormalizer.Normalize(PrefabScene(900, 123456, 77, 78, false));
            Assert.That(b, Is.EqualTo(a));
            StringAssert.Contains("--- !u!1 &1000\n", a);
            StringAssert.Contains("--- !u!4 &1001\nTransform:\n  m_GameObject: {fileID: 1000}\n  m_Children:\n  - {fileID: 1003}", a);
            StringAssert.Contains("--- !u!1001 &1002\nPrefabInstance:", a);
            StringAssert.Contains("m_TransformParent: {fileID: 1001}", a);
            StringAssert.Contains("--- !u!4 &1003 stripped\n", a);
            StringAssert.Contains("m_PrefabInstance: {fileID: 1002}", a);
            StringAssert.Contains("target: {fileID: -8679921383154817045, guid: abc, type: 3}", a);
            Assert.That(SceneFileNormalizer.Normalize(a), Is.EqualTo(a));
        }

        // A prefab instance that Unity saved with an added component on one of its objects: the instance, its stripped transform, a stripped GameObject, a stripped
        // component and the added (real) component that sits on the stripped GameObject. ids = instance, strippedTf, strippedGo, strippedComp, addedComp, go, tf.
        static string StrippedScene(long[] ids, bool reversed)
        {
            long inst = ids[0], sTf = ids[1], sGo = ids[2], sComp = ids[3], added = ids[4], go = ids[5], tf = ids[6];
            string instance = $"--- !u!1001 &{inst}\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {{fileID: {tf}}}\n    m_Modifications: []\n    m_AddedComponents:\n" +
                $"    - targetCorrespondingSourceObject: {{fileID: 1111, guid: abc, type: 3}}\n      insertIndex: -1\n      addedObject: {{fileID: {added}}}\n  m_SourcePrefab: {{fileID: 100100000, guid: abc, type: 3}}\n";
            string strippedTf = $"--- !u!4 &{sTf} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: 4444, guid: abc, type: 3}}\n  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            string strippedGo = $"--- !u!1 &{sGo} stripped\nGameObject:\n  m_CorrespondingSourceObject: {{fileID: 1111, guid: abc, type: 3}}\n  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            string strippedComp = $"--- !u!114 &{sComp} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 2222, guid: abc, type: 3}}\n  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            string addedComp = $"--- !u!114 &{added}\nMonoBehaviour:\n  m_GameObject: {{fileID: {sGo}}}\n  m_Enabled: 1\n";
            string[] prefabDocs = reversed
                ? new[] { addedComp, strippedComp, strippedGo, strippedTf, instance }
                : new[] { instance, strippedTf, strippedGo, strippedComp, addedComp };
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" + string.Concat(prefabDocs) +
                $"--- !u!1 &{go}\nGameObject:\n  m_Component:\n  - component: {{fileID: {tf}}}\n" +
                $"--- !u!4 &{tf}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n  m_Children:\n  - {{fileID: {sTf}}}\n" +
                $"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {{fileID: {tf}}}\n";
        }

        [Test]
        public void Normalize_StrippedGameObjectsAndComponents_DoNotDependOnOldIds()
        {
            string a = SceneFileNormalizer.Normalize(StrippedScene(new long[] { 5001, 5002, 5003, 5004, 5005, 6001, 6002 }, false));
            // The stripped GameObject and stripped component swap their old-id order, and the file order is reversed.
            string b = SceneFileNormalizer.Normalize(StrippedScene(new long[] { 90, 80, 8000, 7000, 70, 60, 50 }, true));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(SceneFileNormalizer.Normalize(a), Is.EqualTo(a));
            StringAssert.DoesNotContain("&5001", a); StringAssert.DoesNotContain("&8000", a); StringAssert.DoesNotContain("&7000", a);
            long Id(string pattern) => long.Parse(System.Text.RegularExpressions.Regex.Match(a, pattern).Groups[1].Value);
            Assert.That(Id(@"--- !u!1 &(\d+) stripped"), Is.LessThan(Id(@"--- !u!114 &(\d+)\nMonoBehaviour:\n  m_GameObject")),
                "the stripped GameObject is numbered before the component added to it");
        }

        // Two prefab instances nested under a prefab instance: each places itself with m_TransformParent = the outer instance's stripped transform, and nothing
        // in the scene lists their stripped transforms as a child, so only the parent link orders them.
        static string NestedScene(long outer, long outerTf, long b, long bTf, long c, long cTf, long go, long tf, bool reversed)
        {
            string Inst(long id, long parentTf, string guid) =>
                $"--- !u!1001 &{id}\nPrefabInstance:\n  m_Modification:\n    m_TransformParent: {{fileID: {parentTf}}}\n    m_Modifications: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}\n";
            string Stripped(long id, long inst, string guid) =>
                $"--- !u!4 &{id} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: 4444, guid: {guid}, type: 3}}\n  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n";
            string[] docs =
            {
                Inst(outer, tf, "aaa"), Stripped(outerTf, outer, "aaa"), Inst(b, outerTf, "bbb"), Stripped(bTf, b, "bbb"), Inst(c, outerTf, "ccc"), Stripped(cTf, c, "ccc"),
            };
            if (reversed) System.Array.Reverse(docs);
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" + string.Concat(docs) +
                $"--- !u!1 &{go}\nGameObject:\n  m_Component:\n  - component: {{fileID: {tf}}}\n" +
                $"--- !u!4 &{tf}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n  m_Children:\n  - {{fileID: {outerTf}}}\n" +
                $"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {{fileID: {tf}}}\n";
        }

        [Test]
        public void Normalize_NestedPrefabInstances_AreOrderedByTheirParentLink_NotByOldIds()
        {
            string a = SceneFileNormalizer.Normalize(NestedScene(3001, 3002, 3003, 3004, 3005, 3006, 4001, 4002, false));
            // Old ids of the two nested instances swapped (c before b), file order reversed.
            string b = SceneFileNormalizer.Normalize(NestedScene(900, 901, 2000, 2001, 1000, 1001, 10, 11, true));
            Assert.That(b, Is.EqualTo(a));
            Assert.That(SceneFileNormalizer.Normalize(a), Is.EqualTo(a));
            // Outer instance, then nested bbb, then nested ccc (their source guids give the stable order).
            Assert.That(a.IndexOf("guid: bbb, type: 3}\n", System.StringComparison.Ordinal), Is.GreaterThan(-1));
            Assert.That(a.IndexOf("m_SourcePrefab: {fileID: 100100000, guid: bbb", System.StringComparison.Ordinal),
                Is.LessThan(a.IndexOf("m_SourcePrefab: {fileID: 100100000, guid: ccc", System.StringComparison.Ordinal)));
        }

        [Test]
        public void Normalize_LeavesNegativeIdScenesUntouched_WithAWarning()
        {
            string negative = PrefabScene(5555, 5556, 700, 701, true).Replace("--- !u!1001 &5555\n", "--- !u!1001 &-5555\n");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SceneFileNormalizer"));
            Assert.That(SceneFileNormalizer.Normalize(negative), Is.EqualTo(negative));
        }

        [Test]
        public void Normalize_NegativeReferencesAreNotMapped()
        {
            string s = "--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" +
                "--- !u!1 &700\nGameObject:\n  m_Component:\n  - component: {fileID: 701}\n" +
                "--- !u!4 &701\nTransform:\n  m_GameObject: {fileID: 700}\n  m_Children: []\n  m_Other: {fileID: -42}\n" +
                "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {fileID: 701}\n";
            string n = SceneFileNormalizer.Normalize(s);
            StringAssert.Contains("m_Other: {fileID: -42}", n);
            StringAssert.Contains("--- !u!1 &1000\n", n);
        }

        [Test]
        public void Normalize_RemapsTheRenderSettingsSunToTheLightsNewId()
        {
            string Scene(long go, long tf, long light) =>
                "--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" +
                $"--- !u!104 &2\nRenderSettings:\n  m_Sun: {{fileID: {light}}}\n  m_Fog: 0\n" +
                $"--- !u!1 &{go}\nGameObject:\n  m_Component:\n  - component: {{fileID: {tf}}}\n  - component: {{fileID: {light}}}\n" +
                $"--- !u!4 &{tf}\nTransform:\n  m_GameObject: {{fileID: {go}}}\n  m_Children: []\n" +
                $"--- !u!108 &{light}\nLight:\n  m_GameObject: {{fileID: {go}}}\n" +
                $"--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {{fileID: {tf}}}\n";
            string a = SceneFileNormalizer.Normalize(Scene(10, 11, 12));
            string b = SceneFileNormalizer.Normalize(Scene(9000, 9001, 9002));
            Assert.That(b, Is.EqualTo(a));
            StringAssert.Contains("RenderSettings:\n  m_Sun: {fileID: 1002}\n", a);
            StringAssert.Contains("--- !u!108 &1002\nLight:", a);
        }

        [Test]
        public void BuildScenes_HaveNoStaleNetworkObjectHashes()
        {
            Assert.That(Validators.SceneNetworkObjectHashes(), Is.Empty);
        }

        [Test]
        public void Normalize_HandlesEmptyAndSettingsOnlyInput()
        {
            Assert.That(SceneFileNormalizer.Normalize(""), Is.EqualTo(""));
            Assert.That(SceneFileNormalizer.Normalize(null), Is.Null);
            string settings = "%YAML 1.1\n--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n";
            Assert.That(SceneFileNormalizer.Normalize(settings), Is.EqualTo(settings));
        }
    }
}
