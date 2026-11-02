// Authority: test-only. M7 S2 fast twin of the co-op bot runs (check tier, no env gate): the host DiveBot + one FollowerBot play 90 s of game time of a pinned-seed dive in Arena_Dive.
// Asserts only that the run itself held together: no error or exception in the log, no Netcode warning, replicated state equal on both peers at four mid points and after the bots stop,
// both heroes spawned, and the follower really walked (>= 10 m of its own steps, teleports excluded). Nothing about outcomes (floors, deaths): those are CoopDiveBotTests' report.
// NOT covered: more than two players (CoopDiveBotTests quad), latency, transforms and enemies in the hash.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class CoopDiveFastTests
    {
        private CoopDiveRun _run;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _run?.Dispose(); _run = null;
            yield return PlayModeHarness.TearDownApp();
        }

        // Catches: a replicated-state divergence, a Netcode error/warning, or a client hero that cannot move while the host plays a dive in the real scene with two peers connected.
        [UnityTest, Timeout(900000)]
        public IEnumerator HostBotAndFollower_PlayNinetySeconds_StateAgrees_NoNetcodeNoise_FollowerWalks()
        {
            yield return CoopDiveRun.Boot(1, 12345u, r => _run = r);
            CoopDiveRun run = _run;
            Assert.That(run.HostHero, Is.Not.Null, "[boot] the host hero");
            Assert.That(run.Followers.Count, Is.EqualTo(1), "[boot] one follower");
            Assert.That(HeroVitals.All.Count(v => v != null && v.IsSpawned && v.NetworkManager == run.HostNm), Is.EqualTo(2), "[boot] both heroes are spawned on the host");

            yield return run.Play(90f, 20f, realTimeLimit: 800f);
            yield return run.Quiesce(3f);

            // the final comparison: the bots are idle, so give replication a few seconds of game time and then require agreement
            string diff = run.Mismatch();
            float waited = 0f;
            while (diff != null && waited < 8f) { yield return run.Quiesce(0.5f); waited += 0.5f; diff = run.Mismatch(); }

            Debug.Log($"[COOP] fast twin: {run.Seconds:0} s, deepest floor {run.MaxFloor}, hash samples {run.HashSamples} agreed {run.HashPassed} mismatches {run.HashMismatches}, errors {run.ErrorCount}, warnings {run.WarningCount} [{string.Join(" | ", run.Warnings)}]; {run.Followers[0].Summary()}");
            Assert.That(run.Errors, Is.Empty, "no error or exception in the log:\n" + string.Join("\n", run.Errors));
            string[] netWarnings = run.Warnings.Where(w => w.Contains("Netcode") || w.Contains("Network")).ToArray();
            Assert.That(netWarnings, Is.Empty, "no Netcode warning in the log:\n" + string.Join("\n", netWarnings));
            Assert.That(run.HashSamples, Is.GreaterThanOrEqualTo(3), "the hash was sampled mid-run (at 20 s, 40 s, ...)");
            Assert.That(run.HashMismatches, Is.EqualTo(0), "replicated state differs between the peers mid-run:\n" + string.Join("\n", run.HashFindings));
            Assert.That(diff, Is.Null, "replicated state differs between the peers after the bots stopped:\n" + diff);
            Assert.That(run.Followers[0].MovedMeters, Is.GreaterThanOrEqualTo(10f), "the follower walked at least 10 m by itself. " + run.Followers[0].Summary());
        }
    }
}
