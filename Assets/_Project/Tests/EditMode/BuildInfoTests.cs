using SleepingCalamity.Core;
using NUnit.Framework;

namespace SleepingCalamity.Tests.EditMode
{
    public class BuildInfoTests
    {
        [Test]
        public void BuildInfo_EffectiveProtocolVersion_AddsDevOffset()
        {
            Assert.That(BuildInfo.ProtocolVersion, Is.EqualTo(1), "Phase 1 M1: the foundation port has no networked gameplay yet");
            Assert.That(BuildInfo.GameVersion, Is.EqualTo("0.1.0"));
            Assert.That(BuildInfo.EffectiveProtocolVersion(0), Is.EqualTo(BuildInfo.ProtocolVersion));
            Assert.That(BuildInfo.EffectiveProtocolVersion(1), Is.EqualTo(BuildInfo.ProtocolVersion + 1));
        }
    }
}
