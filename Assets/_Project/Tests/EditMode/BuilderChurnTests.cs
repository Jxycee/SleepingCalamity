// Authority: none (EditMode). Guards the "two builds in a row leave git clean" rule (Tools/cloud/sc_churn_check.sh is the full check): builder helpers that
// write shared assets must produce the same bytes however often they run.
using System.IO;
using NUnit.Framework;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class BuilderChurnTests
    {
        const string CrackWeedsPath = "Assets/_Project/Content/Materials/BeautyCorner/M_CrackWeeds.mat";

        [Test]
        public void CrackWeedsMaterial_BuiltTwice_ChangesNoBytes_AndUsesTheFixedWeedASource()
        {
            Material first = BeautyCornerArt.CrackWeedsMaterial();
            Assert.That(first, Is.Not.Null, "the B9 Weed_A folder is committed");
            string abs = BuilderUtil.Abs(CrackWeedsPath);
            byte[] bytes = File.ReadAllBytes(abs);

            Material second = BeautyCornerArt.CrackWeedsMaterial();
            Assert.That(File.ReadAllBytes(abs), Is.EqualTo(bytes), "a second build of the same material rewrote it");
            Assert.That(AssetDatabase.GetAssetPath(second.GetTexture("_BaseMap")), Does.Contain("/" + BeautyCornerArt.SharedFoliageFolder + "/"),
                "the shared weed material must not depend on which folder a scatter loaded first");
        }
    }
}
