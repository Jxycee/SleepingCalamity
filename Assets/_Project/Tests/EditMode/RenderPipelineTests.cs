// The project renders through URP. The URP template assigns the pipeline per quality level (the global default stays
// empty), so every quality level must name a URP asset. Guards against a settings change dropping one.
using NUnit.Framework;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class RenderPipelineTests
    {
        [Test]
        public void QualityLevels_AllUseUrp()
        {
            int count = QualitySettings.names.Length;
            Assert.That(count, Is.GreaterThan(0), "no quality levels");
            for (int i = 0; i < count; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i);
                Assert.That(asset, Is.Not.Null, "quality level " + QualitySettings.names[i] + " has no render pipeline asset");
                Assert.That(asset.GetType().Name, Is.EqualTo("UniversalRenderPipelineAsset"), "quality level " + QualitySettings.names[i]);
            }
        }
    }
}
