// Authority: test-only. Goal-driven bot playtests of the combat arena (ArenaBot): the real solo flow, the bot on the pad, a markdown report per run.
// Ignored unless SC_BOT=1, so `check` never runs them: they answer balance and feel questions ("can a player clear wave 1?"), not regressions. Run them with
//   set SC_BOT=1 then Tools/unity.ps1 test-play -Filter "BotPlaytestTests" -OutDir <dir>
// The report goes to %SC_BOT_OUT% (default: <temporaryCachePath>/bot) and its summary to the test log ("[BOT]" lines).
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    [Category("Bot")]
    public class BotPlaytestTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneFlow.DevCombatArena = false;
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator Bot_ClearsWaveOne_KeepingHpAbove20Percent()
        {
            yield return Play("clear wave 1 keeping HP above 20%", 90f, dodges: true,
                done: r => r.ClearedWaves >= 1 || r.WentDown,
                met: r => r.ClearedWaves >= 1 && r.LowestHpPercent > 20f);
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator Bot_NoRolls_ClearsWaveOne()
        {
            yield return Play("clear wave 1 without rolling (a button-masher)", 90f, dodges: false,
                done: r => r.ClearedWaves >= 1 || r.WentDown,
                met: r => r.ClearedWaves >= 1 && !r.WentDown);
        }

        [UnityTest, Timeout(260000)]
        public IEnumerator Bot_SurvivesThreeWaves()
        {
            yield return Play("clear three waves without going down", 200f, dodges: true,
                done: r => r.ClearedWaves >= 3 || r.WentDown,
                met: r => r.ClearedWaves >= 3 && !r.WentDown);
        }

        // Tier 2 (enemy polish, 2026-10-30): the first mixed wave at the retuned x1.54 attack step. The goal is the pacing promise, not a perfect run: never
        // more than two enemies winding up at once and no two strikes from different enemies opening closer than the tier-2 gap.
        [UnityTest, Timeout(200000)]
        public IEnumerator Bot_TierTwo_WaveFive_StaysReadable()
        {
            yield return Play("wave 5 (tier 2): at most 2 wind-ups at once, strikes at least 0.35 s apart", 120f, dodges: true,
                done: r => r.ClearedWaves >= 5 || r.WentDown,
                met: r => r.Strikes > 3 && r.MaxWindUpsAtOnce <= 2 && r.MinGapBetweenStrikes >= AttackPacing.Tier2.MinStrikeGapSeconds - 0.02f,
                startWave: 5);
        }

        // The Dough Warden's signature (the "dough quake", every 3 Warden attacks) must fire under the real AI in wave 4, alongside the slimes, without
        // breaking the two-attackers rule (its token is exclusive).
        [UnityTest, Timeout(200000)]
        public IEnumerator Bot_WaveFour_WardenSignatureFires()
        {
            yield return Play("wave 4: the Warden's dough quake fires, at most 2 wind-ups at once", 120f, dodges: true,
                done: r => r.SignatureAttacks >= 1 && r.Seconds > 20f || r.ClearedWaves >= 4 || r.WentDown,
                met: r => r.SignatureAttacks >= 1 && r.MaxWindUpsAtOnce <= 2,
                startWave: 4);
        }

        private static IEnumerator Play(string goal, float limit, bool dodges, System.Func<BotReport, bool> done, System.Func<BotReport, bool> met, int startWave = 1)
        {
            if (System.Environment.GetEnvironmentVariable("SC_BOT") != "1") Assert.Ignore("bot playtest: set SC_BOT=1 to run");
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.DevCombatArena = true;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.ArenaCombat);
            var hero = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            var input = new FakeInputService();
            hero.GetComponent<HeroLocomotion>().Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
            if (startWave > 1)
            {
                yield return null;
                CombatHost.Current.ServerJumpToWave(startWave);
            }
            var bot = new ArenaBot(input, hero.transform) { Dodges = dodges };
            bot.Report.Goal = goal;

            float start = Time.time;
            while (Time.time - start < limit && !done(bot.Report))
            {
                bot.Tick(Time.deltaTime, Time.time - start);
                yield return null;
            }
            input.Frame = default;
            BotReport r = bot.Report;
            r.GoalMet = met(r);
            if (r.EnemiesSeen == 0) r.Findings.Add("no enemy ever spawned");
            else if (r.EnemyAttacksStarted == 0) r.Findings.Add("enemies spawned but never attacked");
            if (r.HitsDealt == 0 && r.Swings > 10) r.Findings.Add($"{r.Swings} attack presses and not one hit landed");
            if (!done(r)) r.Findings.Add($"ran out of time ({limit:F0} s) before the goal resolved");

            string md = r.ToMarkdown();
            string dir = System.Environment.GetEnvironmentVariable("SC_BOT_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.temporaryCachePath, "bot");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, TestContext.CurrentContext.Test.Name + ".md");
            File.WriteAllText(file, md);
            foreach (string line in md.Split('\n')) Debug.Log("[BOT] " + line.TrimEnd());
            Debug.Log("[BOT] report: " + file);
            Assert.That(r.GoalMet, Is.True, $"goal not met: {goal}. Report: {file}");
        }
    }
}
