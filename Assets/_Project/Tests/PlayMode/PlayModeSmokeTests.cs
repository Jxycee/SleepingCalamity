// Proves the PlayMode runner starts, ticks frames and exits cleanly. Replace with real PlayMode coverage as systems land.
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator PlayMode_AdvancesFrames()
        {
            int start = Time.frameCount;
            yield return null;
            yield return null;
            Assert.That(Time.frameCount, Is.GreaterThan(start));
        }
    }
}
