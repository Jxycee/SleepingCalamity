using NUnit.Framework;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class FeedbackSettingsTests
    {
        [Test]
        public void OldSettingsFile_WithoutFeedbackKeys_KeepsDefaults_AndOtherSettings()
        {
            var d = new SettingsData();
            JsonUtility.FromJsonOverwrite("{\"version\":1,\"masterVolume\":33,\"displayName\":\"Old\"}", d);
            d.Clamp();
            Assert.AreEqual(33, d.masterVolume);
            Assert.AreEqual(1f, d.shakeScale);
            Assert.AreEqual(1f, d.rumbleScale);
            Assert.IsFalse(d.reduceFlashes);
        }

        [Test]
        public void FeedbackSettings_ClampAndRoundTrip()
        {
            var d = new SettingsData { shakeScale = 7f, rumbleScale = -2f, reduceFlashes = true };
            d.Clamp();
            Assert.AreEqual(1f, d.shakeScale);
            Assert.AreEqual(0f, d.rumbleScale);
            d.shakeScale = float.NaN;
            d.Clamp();
            Assert.AreEqual(1f, d.shakeScale);
            d.shakeScale = 0.4f;
            SettingsData c = d.Clone();
            Assert.AreEqual(0.4f, c.shakeScale, 1e-6f);
            Assert.IsTrue(c.reduceFlashes);
        }
    }
}
