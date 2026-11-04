// Authority: test-only. Alpha-tester bot runs of the Cellars dive (DiveBot): the real solo flow into Arena_Dive, the bot on the pad (FakeInputService replaces the
// app's input service, so the dive prompts read the bot's interact press too), hub stair -> floors 1-4 -> heart bank -> go home -> a second dive's floor 1.
// Ignored unless SC_BOT=1, so `check` never runs them: they find play bugs, they don't guard regressions. Run with
//   SC_BOT=1 SC_BOT_OUT=<dir> bash Tools/cloud/unity.sh test-play --filter DiveBotTests --out <scratch>
// Each run writes <SC_BOT_OUT>/DiveBot_<seed>.md and logs "[DIVEBOT]" lines. The test passes when the bot played (findings are the output, not failures);
// it fails only when the run itself broke (no dive started, the bot never left floor 1).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    [Category("Bot")]
    public class DiveBotTests
    {
        private DiveBot _bot;
        private Application.LogCallback _log;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_log != null) Application.logMessageReceived -= _log;
            _log = null;
            _bot?.Dispose(); _bot = null;
            Time.captureFramerate = 0;
            SceneFlow.DevDiveArena = false;
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Seed12345() { yield return Play(12345u); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Seed7() { yield return Play(7u); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Seed31() { yield return Play(31u); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Seed404() { yield return Play(404u); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Seed2024() { yield return Play(2024u); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Tough_Seed12345() { yield return Play(12345u, tough: true); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Tough_Seed7() { yield return Play(7u, tough: true); }

        [UnityTest, Timeout(1800000)]
        public IEnumerator DiveBot_Tough_Seed31() { yield return Play(31u, tough: true); }

        private IEnumerator Play(uint seed, bool tough = false)
        {
            if (System.Environment.GetEnvironmentVariable("SC_BOT") != "1") Assert.Ignore("dive bot: set SC_BOT=1 to run");
            LogAssert.ignoreFailingMessages = true;   // errors are findings, the run goes on
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.DevDiveArena = true;
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.ArenaDive);
            var hero = AppBootstrap.Current.NetworkManager.LocalClient.PlayerObject;
            var input = new FakeInputService();
            hero.GetComponent<HeroLocomotion>().Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
            ServiceRoots.App.Replace<IInputService>(input);
            Time.captureFramerate = 60;               // fixed 1/60 s steps from here on (after boot: the harness waits on game time), as fast as the machine runs

            float wait = 0f;
            while ((DiveDirector.Instance == null || DiveDirector.Instance.NetHubStair.Value == Vector3.zero) && wait < 15f) { wait += Time.deltaTime; yield return null; }
            Assert.That(DiveDirector.Instance, Is.Not.Null, "no DiveDirector in the dive hub");
            DiveDirector.Instance.SeedOverride = seed;

            _bot = new DiveBot(input, hero.transform) { Tough = tough };
            DiveBot bot = _bot;
            _log = (msg, stack, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (msg.StartsWith("[DIVEBOT]")) return;
                string first = msg.Split('\n')[0];
                bot.Note(type == LogType.Exception ? "Exception" : "Error", first.Length > 300 ? first.Substring(0, 300) : first, (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("SleepingCalamity")) ?? "");
            };
            Application.logMessageReceived += _log;

            // dive 1: hub -> floors -> home
            float limit = 2100f;
            int maxFloor = 0;
            while (bot.Seconds < limit && !(bot.WentHome && bot.Floor == 0))
            {
                bot.Tick(1f / 60f);
                maxFloor = Mathf.Max(maxFloor, bot.Floor);
                yield return null;
            }
            input.Frame = default;
            if (!bot.WentHome) bot.Note("Run", $"the dive didn't finish within {limit:0} s of play (deepest floor {maxFloor})");

            // dive 2: the hub stair again, then look at the first floor's fresh state (floor 3 once the checkpoint is banked)
            string second = "not reached";
            if (bot.WentHome)
            {
                DiveHaulSync.Summary banked = DiveDirector.Instance.HaulSync != null ? DiveDirector.Instance.HaulSync.Current : default;
                bot.WentHome = false;   // let the bot take the hub stair again
                float t0 = bot.Seconds;
                while (bot.Seconds - t0 < 120f && bot.Floor == 0) { bot.Tick(1f / 60f); yield return null; }
                for (int i = 0; i < 90; i++) { bot.Tick(1f / 60f); yield return null; }   // let contents arm
                input.Frame = default;
                if (bot.Floor > 0)
                {
                    DiveDirector d = DiveDirector.Instance;
                    CellarContentsSync c = d.ContentsSync;
                    var s = c.State;
                    var h = d.HaulSync.Current;
                    second = $"second dive starts on floor {bot.Floor}: chests open mask {s.Chests}, pieces {c.PieceCount} (visible {Enumerable.Range(0, c.PieceCount).Count(c.PieceVisible)}), found pieces {h.FoundPieces}, safe {h.BankedPieces} (after dive 1: {banked.BankedPieces}), gate key {s.GateKey}, exit unlocked {s.ExitUnlocked}, heart banked {d.NetHeartBanked.Value}";
                    if (s.Chests != 0 && s.Seed == d.FloorSync.Layout.Seed) bot.Note("State", "second dive: floor 1 chests start open (state leaked from the first dive)");
                    if (s.GateKey != 0 || s.ExitUnlocked) bot.Note("State", "second dive: floor 1 starts with the gate key out or the exit unlocked");
                    if (d.NetHeartBanked.Value) bot.Note("State", "second dive: the heart still reads banked");
                    if (h.FoundPieces != 0) bot.Note("State", $"second dive: starts with {h.FoundPieces} pieces already found this dive");
                    if (c.PieceCount == 0) bot.Note("State", "second dive: floor 1 has no bakery pieces (all banked last dive? pieces are meant to stay collectable until the town gets them)");
                }
                else bot.Note("Flow", "after going home, the hub stair didn't start a second dive within 120 s");
            }

            // report
            var sb = new StringBuilder();
            sb.AppendLine($"# Dive bot, seed {seed}{(tough ? " (tough: host tops HP up below 50%)" : "")}");
            sb.AppendLine();
            sb.AppendLine(bot.Summary());
            sb.AppendLine();
            sb.AppendLine("Combat: " + $"hits dealt {bot.Fight.HitsDealt}, hits taken {bot.Fight.HitsTaken}, damage taken {bot.Fight.DamageTaken}, lowest HP {bot.Fight.LowestHpPercent:0}%, dodges {bot.Fight.Dodges}, swings {bot.Fight.Swings}");
            sb.AppendLine();
            sb.AppendLine(second);
            sb.AppendLine();
            sb.AppendLine($"## Findings ({bot.Findings.Count})");
            sb.AppendLine();
            foreach (DiveBot.Finding f in bot.Findings) sb.AppendLine("- " + f);
            foreach (string f in bot.Fight.Findings) sb.AppendLine("- **Combat (ArenaBot)** " + f);
            sb.AppendLine();
            sb.AppendLine("## Hits taken");
            sb.AppendLine();
            foreach (string l in bot.HitLog) sb.AppendLine("    " + l);
            sb.AppendLine();
            sb.AppendLine("## Log");
            sb.AppendLine();
            foreach (string l in bot.Log) sb.AppendLine("    " + l);
            string dir = System.Environment.GetEnvironmentVariable("SC_BOT_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.temporaryCachePath, "bot");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"DiveBot_{(tough ? "tough_" : "")}{seed}.md");
            File.WriteAllText(file, sb.ToString());
            Debug.Log("[DIVEBOT] SUMMARY seed " + seed + ": " + bot.Summary());
            Debug.Log("[DIVEBOT] report: " + file);
            Assert.That(maxFloor, Is.GreaterThanOrEqualTo(1), "the bot never got into the dive. Report: " + file);
        }
    }
}
