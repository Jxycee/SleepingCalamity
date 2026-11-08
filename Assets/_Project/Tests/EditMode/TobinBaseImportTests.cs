// Import gates for the MetaPerson Tobin base model (TobinBase.fbx): Humanoid avatar, ARKit/viseme/slider shapes, height at scale 1.0425.
// Editor-only, no graphics needed.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class TobinBaseImportTests
    {
        static readonly string[] Arkit =
        {
            "eyeBlinkLeft","eyeLookDownLeft","eyeLookInLeft","eyeLookOutLeft","eyeLookUpLeft","eyeSquintLeft","eyeWideLeft",
            "eyeBlinkRight","eyeLookDownRight","eyeLookInRight","eyeLookOutRight","eyeLookUpRight","eyeSquintRight","eyeWideRight",
            "jawForward","jawLeft","jawRight","jawOpen","mouthClose","mouthFunnel","mouthPucker","mouthLeft","mouthRight",
            "mouthSmileLeft","mouthSmileRight","mouthFrownLeft","mouthFrownRight","mouthDimpleLeft","mouthDimpleRight",
            "mouthStretchLeft","mouthStretchRight","mouthRollLower","mouthRollUpper","mouthShrugLower","mouthShrugUpper",
            "mouthPressLeft","mouthPressRight","mouthLowerDownLeft","mouthLowerDownRight","mouthUpperUpLeft","mouthUpperUpRight",
            "browDownLeft","browDownRight","browInnerUp","browOuterUpLeft","browOuterUpRight","cheekPuff","cheekSquintLeft",
            "cheekSquintRight","noseSneerLeft","noseSneerRight"   // tongueOut is absent from the source (known)
        };
        static readonly string[] Visemes = { "CH","DD","E","FF","PP","RR","SS","TH","aa","ih","kk","nn","oh","ou","sil" };

        GameObject _src, _inst;

        [OneTimeSetUp]
        public void Load()
        {
            _src = AssetDatabase.LoadAssetAtPath<GameObject>(TobinBaseModelImporter.ModelPath);
            Assert.IsNotNull(_src, "TobinBase.fbx missing or failed to import");
            _inst = Object.Instantiate(_src);
        }

        [OneTimeTearDown]
        public void Unload() { if (_inst) Object.DestroyImmediate(_inst); }

        static string Strip(string n) { int i = n.LastIndexOf('.'); return i >= 0 ? n.Substring(i + 1) : n; }

        SkinnedMeshRenderer Smr(string name) =>
            _inst.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == name);

        [Test]
        public void AvatarIsValidHumanoidWithExpectedMapping()
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(TobinBaseModelImporter.ModelPath).OfType<Avatar>().FirstOrDefault();
            Assert.IsNotNull(avatar, "no Avatar sub-asset");
            Assert.IsTrue(avatar.isHuman, "avatar isHuman");
            Assert.IsTrue(avatar.isValid, "avatar isValid");

            var mi = (ModelImporter)AssetImporter.GetAtPath(TobinBaseModelImporter.ModelPath);
            var map = mi.humanDescription.human.ToDictionary(h => h.humanName, h => h.boneName);
            var table = string.Join("\n", map.OrderBy(k => k.Key).Select(k => k.Key + " -> " + k.Value));
            Debug.Log("TOBIN HUMAN MAP\n" + table);

            var expect = new Dictionary<string, string>
            {
                ["Hips"] = "Hips", ["Spine"] = "Spine", ["Chest"] = "Spine1", ["UpperChest"] = "Spine2",
                ["Neck"] = "Neck", ["Head"] = "Head", ["LeftEye"] = "LeftEye", ["RightEye"] = "RightEye",
                ["LeftShoulder"] = "LeftShoulder", ["LeftUpperArm"] = "LeftArm", ["LeftLowerArm"] = "LeftForeArm", ["LeftHand"] = "LeftHand",
                ["RightShoulder"] = "RightShoulder", ["RightUpperArm"] = "RightArm", ["RightLowerArm"] = "RightForeArm", ["RightHand"] = "RightHand",
                ["LeftUpperLeg"] = "LeftUpLeg", ["LeftLowerLeg"] = "LeftLeg", ["LeftFoot"] = "LeftFoot", ["LeftToes"] = "LeftToeBase",
                ["RightUpperLeg"] = "RightUpLeg", ["RightLowerLeg"] = "RightLeg", ["RightFoot"] = "RightFoot", ["RightToes"] = "RightToeBase",
            };
            foreach (var side in new[] { "Left", "Right" })
                foreach (var f in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    for (int k = 1; k <= 3; k++)
                    {
                        string[] seg = { "Proximal", "Intermediate", "Distal" };
                        expect[side + " " + f + " " + seg[k - 1]] = side + "Hand" + (f == "Little" ? "Pinky" : f) + k;
                    }
            var boneNames = new HashSet<string>(map.Values);
            foreach (var u in new[] { "Neck1", "Neck2", "LeftForeArm1", "LeftForeArm2", "RightForeArm1", "RightForeArm2", "HeadTop_End", "ForeArm1", "ForeArm2" })
                Assert.IsFalse(boneNames.Contains(u), "bone " + u + " should be unmapped\n" + table);
            foreach (var kv in expect)
            {
                Assert.IsTrue(map.TryGetValue(kv.Key, out var bone), "slot " + kv.Key + " unmapped\n" + table);
                Assert.AreEqual(kv.Value, bone, "slot " + kv.Key + "\n" + table);
            }
        }

        [Test]
        public void HeadHasArkitAndVisemeShapesAndTheyMoveVertices()
        {
            var head = Smr("AvatarHead");
            var m = head.sharedMesh;
            var names = Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName).ToList();
            Debug.Log("TOBIN HEAD SHAPES (" + names.Count + ")\n" + string.Join(",", names));
            var bare = new HashSet<string>(names.Select(Strip));
            foreach (var n in Arkit.Concat(Visemes)) Assert.IsTrue(bare.Contains(n), "missing shape " + n);
            Assert.AreEqual(149, names.Count, "AvatarHead shape count");

            AssertShapeMoves(head, "eyeBlinkLeft");
            AssertShapeMoves(head, "jawOpen");
            AssertShapeMoves(head, "aa");

            foreach (var meshName in new[] { "AvatarBody", "BaseUnderlay_Torso" })
            {
                var smr = Smr(meshName);
                var mesh = smr.sharedMesh;
                Assert.Greater(mesh.blendShapeCount, 0, meshName);
                bool moved = false; string last = "";
                for (int i = 0; i < mesh.blendShapeCount && !moved; i++)
                {
                    last = mesh.GetBlendShapeName(i);
                    moved = ShapeDelta(smr, i) > 0.001f;
                    if (moved) Debug.Log("TOBIN SLIDER " + meshName + " " + last + " moves " + ShapeDelta(smr, i).ToString("F4") + " m (> 1 mm)");
                }
                Assert.IsTrue(moved, meshName + ": no slider moved vertices > 1 mm (last " + last + ")");
            }
        }

        void AssertShapeMoves(SkinnedMeshRenderer smr, string bareName)
        {
            var m = smr.sharedMesh;
            int idx = Enumerable.Range(0, m.blendShapeCount).First(i => Strip(m.GetBlendShapeName(i)) == bareName);
            float d = ShapeDelta(smr, idx);
            Debug.Log("TOBIN SHAPE DELTA " + smr.name + " " + bareName + " " + d.ToString("F4") + " m");
            Assert.Greater(d, 0.001f, bareName + " max vertex delta " + d + " m");
        }

        static float ShapeDelta(SkinnedMeshRenderer smr, int index)
        {
            var a = new Mesh(); var b = new Mesh();
            try
            {
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++) smr.SetBlendShapeWeight(i, 0);
                smr.BakeMesh(a, true);
                smr.SetBlendShapeWeight(index, 100);
                smr.BakeMesh(b, true);
                smr.SetBlendShapeWeight(index, 0);
                var va = a.vertices; var vb = b.vertices;
                float max = 0;
                for (int i = 0; i < va.Length; i++) max = Mathf.Max(max, (va[i] - vb[i]).magnitude);
                return max;
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void HeightIsTobin190AndBoneScalesAreUniform()
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var smr in _inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = new Mesh();
                smr.BakeMesh(m, true);
                var l2w = smr.transform.localToWorldMatrix;
                foreach (var v in m.vertices)
                {
                    float y = l2w.MultiplyPoint3x4(v).y;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }
                Object.DestroyImmediate(m);
            }
            float h = max - min;
            Debug.Log("TOBIN HEIGHT " + h.ToString("F4") + " m (min " + min.ToString("F4") + ", max " + max.ToString("F4") + ") scale " + TobinBaseModelImporter.GlobalScale);
            Assert.AreEqual(1.90f, h, 0.01f, "model height");

            var bones = _inst.GetComponentsInChildren<Transform>(true).Where(t => !t.GetComponent<SkinnedMeshRenderer>()).ToArray();
            var hips = bones.First(t => t.name == "Hips"); var headB = bones.First(t => t.name == "Head");
            foreach (var t in new[] { hips, headB })
            {
                var s = t.lossyScale;
                Assert.AreEqual(s.x, s.y, 1e-3f, t.name + " lossy scale xy");
                Assert.AreEqual(s.x, s.z, 1e-3f, t.name + " lossy scale xz");
            }
            Assert.AreEqual(hips.lossyScale.x, headB.lossyScale.x, 1e-3f, "Hips vs Head lossy scale");
            Debug.Log("TOBIN BONE SCALE Hips " + hips.lossyScale + " Head " + headB.lossyScale);
            Assert.Less(Mathf.Abs(hips.lossyScale.x - 1f), 0.2f, "no stray 100x/0.01x on bones");
        }
    }
}
