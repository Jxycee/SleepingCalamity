// Tests for SleepingCalamity.Core.NetworkConstants.ParseTestPort (the SC_TEST_PORT override).
using SleepingCalamity.Core;
using NUnit.Framework;

namespace SleepingCalamity.Tests.EditMode
{
    public class NetworkTestPortTests
    {
        [Test]
        public void ParseTestPort_Unset_UsesDefault()
        {
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort(null));
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort(""));
        }

        [Test]
        public void ParseTestPort_ValidOverride_IsUsed()
        {
            Assert.AreEqual((ushort)7793, NetworkConstants.ParseTestPort("7793"));
        }

        [Test]
        public void ParseTestPort_Invalid_FallsBackToDefault()
        {
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort("abc"));
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort("70000"));
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort("-1"));
            Assert.AreEqual(NetworkConstants.DefaultTestPort, NetworkConstants.ParseTestPort("0"));
        }
    }
}
