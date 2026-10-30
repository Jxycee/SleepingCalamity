// Authority: test-only. Goal-driven bot playtests of the combat arena (ArenaBot): the real solo flow, the bot on the pad, a markdown report per run.
// Ignored unless SC_BOT=1, so `check` never runs them: they answer balance and feel questions ("can a player clear wave 1?"), not regressions. Run them with
//   set SC_BOT=1 then Tools/unity.ps1 test-play -Filter "BotPlaytestTests" -OutDir <dir>
// The report goes to %SC_BOT_OUT% (default: <temporaryCachePath>/bot) and its summary to the test log ("[BOT]" lines).
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
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

        private static IEnumerator Play(string goal, float limit, bool dodges, System.Func<BotReport, bool> done, System.Func<BotReport, bool> met)
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
