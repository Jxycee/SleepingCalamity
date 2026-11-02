// Authority: test-only. M7 S2 (docs/design/m7-proposal.md section 4): co-op alpha-tester bots. The host plays Arena_Dive with the DiveBot; one (pair) or three (quad) TestClients play it with FollowerBots.
// Ignored unless SC_BOT=1 (they find play bugs, they don't guard regressions). Run with
//   SC_BOT=1 SC_BOT_OUT=<dir> [SC_COOP_MINUTES=5] powershell ... Tools\unity.ps1 test-play -Filter CoopDiveBotTests -OutDir <scratch>
// Each run writes <SC_BOT_OUT>/CoopBot_<pair|quad>_<seed>.md (default Reference/qa/m7). Report-only: the test fails only when the run itself broke (the party never booted, the host bot never got into the dive).
// CoopDiveFastTests is the small `check`-tier twin.
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    [Category("Bot")]
    public class CoopDiveBotTests
    {
        private CoopDiveRun _run;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _run?.Dispose(); _run = null;
            yield return PlayModeHarness.TearDownApp();   // also leaves and disposes the clients
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator CoopBot_Pair_Seed12345() { yield return Play(1, 12345u); }

        [UnityTest, Timeout(3600000)]
        public IEnumerator CoopBot_Quad_Seed12345() { yield return Play(3, 12345u); }

        private IEnumerator Play(int clients, uint seed)
        {
            if (System.Environment.GetEnvironmentVariable("SC_BOT") != "1") Assert.Ignore("co-op bot: set SC_BOT=1 to run");
            LogAssert.ignoreFailingMessages = true;   // errors are findings, the run goes on
            float minutes = 5f;
            if (float.TryParse(System.Environment.GetEnvironmentVariable("SC_COOP_MINUTES"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m) && m > 0f) minutes = m;

            yield return CoopDiveRun.Boot(clients, seed, r => _run = r);
            CoopDiveRun run = _run;
            yield return run.Play(minutes * 60f, 10f, realTimeLimit: 3000f);
            yield return run.Quiesce(2f);

            string name = CoopDiveRun.ClientCountName(clients);
            string dir = System.Environment.GetEnvironmentVariable("SC_BOT_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reference", "qa", "m7"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"CoopBot_{name}_{seed}.md");
            File.WriteAllText(file, run.Report($"{name} ({clients + 1} players), {minutes:0.#} min"));
            Debug.Log($"[COOP] {name} seed {seed}: {run.Seconds:0} s, deepest floor {run.MaxFloor}, dive fails {run.Bot.DiveFails}, downs {run.Downs.Count}, hash samples {run.HashSamples} mismatches {run.HashMismatches}, errors {run.ErrorCount}, warnings {run.WarningCount}. Report: {file}");
            foreach (FollowerBot f in run.Followers) Debug.Log("[COOP] " + f.Summary());
            Assert.That(run.MaxFloor, Is.GreaterThanOrEqualTo(1), "the host bot never got into the dive. Report: " + file);
        }
    }
}
