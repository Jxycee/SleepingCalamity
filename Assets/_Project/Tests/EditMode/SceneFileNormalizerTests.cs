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

        const string PrefabInstanceScene =
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\nOcclusionCullingSettings:\n  a: 1\n" +
            "--- !u!1001 &5555\nPrefabInstance:\n  m_Modification: {}\n" +
            "--- !u!1 &700\nGameObject:\n  m_Component:\n  - component: {fileID: 701}\n" +
            "--- !u!4 &701\nTransform:\n  m_GameObject: {fileID: 700}\n  m_Children: []\n" +
            "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_Roots:\n  - {fileID: 701}\n";

        [Test]
        public void Normalize_LeavesPrefabInstanceStrippedAndNegativeIdScenesUntouched_WithAWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SceneFileNormalizer"));
            Assert.That(SceneFileNormalizer.Normalize(PrefabInstanceScene), Is.EqualTo(PrefabInstanceScene));

            string stripped = PrefabInstanceScene.Replace("--- !u!1001 &5555\n", "--- !u!4 &5555 stripped\n");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SceneFileNormalizer"));
            Assert.That(SceneFileNormalizer.Normalize(stripped), Is.EqualTo(stripped));

            string negative = PrefabInstanceScene.Replace("--- !u!1001 &5555\n", "--- !u!4 &-5555\n");
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
