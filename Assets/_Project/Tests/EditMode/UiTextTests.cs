// PL-3 task 3.1: every refusal, end reason and toast key a player can meet has words. Catches: a new key, verdict or end reason added
// without text (the player would read "world.library_full"). NOT covered: how the words look on screen (the views).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SleepingCalamity.Core;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class UiTextTests
    {
        [Test]
        public void EveryStartVerdict_HasWords()
        {
            foreach (WorldPlayerVerdict verdict in Enum.GetValues(typeof(WorldPlayerVerdict)))
            {
                string words = UiText.StartRefusal(verdict);
                if (verdict == WorldPlayerVerdict.Ok)
                {
                    Assert.That(words, Is.Null, "Ok is not a refusal");
                    continue;
                }
                Assert.That(words, Is.Not.Null.And.Not.Empty, "no words for start verdict " + verdict);
                Assert.That(words, Does.Not.Match(@"(world|err|save)\.[a-z_]+"), "raw key shown for " + verdict);
                // The toast a refused start publishes shows the same words as the lobby line.
                Assert.That(UiText.Toast(WorldStartText.ToastKey(verdict)), Is.EqualTo(words), "toast and lobby words differ for " + verdict);
            }
            Assert.That(UiText.StartRefusal(WorldPlayerVerdict.NotEnoughPlayers), Does.Contain("2"), "the single-player refusal names the number");
            Assert.That(UiText.StartRefusal((WorldPlayerVerdict)200), Is.Not.Null.And.Not.Empty, "an undefined verdict still says something");
        }

        [Test]
        public void EveryEndReason_HasWords()
        {
            foreach (EndReason reason in Enum.GetValues(typeof(EndReason)))
            {
                string words = UiText.EndedBecause(reason);
                if (EndReasonText.ToastKey(reason) == null)
                {
                    Assert.That(words, Is.Null, reason + " ends quietly");
                    Assert.That(UiText.JoinRefusal(reason), Is.Null, reason + " is no refusal");
                    continue;
                }
                Assert.That(words, Is.Not.Null.And.Not.Empty, "no words for end reason " + reason);
                Assert.That(words, Does.Not.Match(@"(world|err|save)\.[a-z_]+"), "raw key shown for " + reason);
                Assert.That(UiText.Toast(EndReasonText.ToastKey(reason)), Is.EqualTo(words), "toast and screen words differ for " + reason);
                Assert.That(UiText.JoinRefusal(reason), Is.Not.Null.And.Not.Empty, "no join words for " + reason);
            }
            Assert.That(UiText.EndedBecause((EndReason)200), Is.Not.Null.And.Not.Empty, "an undefined reason still says something");
            Assert.That(UiText.JoinRefusal(EndReason.FullLobby), Does.Contain("full"));
            Assert.That(UiText.JoinRefusal(EndReason.InvalidCode), Does.Contain("code"));
        }

        [Test]
        public void EveryPublishedToastKey_HasWords()
        {
            // 1. every key in the list kept beside the table has words (and library_full names the kind it was given)
            foreach (string key in UiText.PublishedToastKeys)
                Assert.That(UiText.Toast(key, "Solo"), Is.Not.Null.And.Not.Empty, "no words for toast key " + key);
            Assert.That(UiText.Toast("world.library_full", "Solo"), Does.Contain("solo"));
            Assert.That(UiText.Toast("world.library_full", "Multiplayer"), Does.Contain("multiplayer"));
            Assert.That(UiText.Toast("no.such_key"), Is.Null, "an unknown key has no words, so a presenter can tell");

            // 2. the keys the two key tables can produce are in the list
            var inList = new HashSet<string>(UiText.PublishedToastKeys);
            foreach (WorldPlayerVerdict v in Enum.GetValues(typeof(WorldPlayerVerdict)))
            {
                string key = WorldStartText.ToastKey(v);
                if (key != null) Assert.That(inList, Does.Contain(key), "WorldStartText key missing from UiText.PublishedToastKeys: " + key);
            }
            foreach (EndReason r in Enum.GetValues(typeof(EndReason)))
            {
                string key = EndReasonText.ToastKey(r);
                if (key != null) Assert.That(inList, Does.Contain(key), "EndReasonText key missing from UiText.PublishedToastKeys: " + key);
            }

            // 3. every literal key handed to "new ToastRequested(...)" in the game's source is in the list (a new publisher must add its row)
            string root = Path.Combine(Application.dataPath, "_Project");
            var call = new Regex(@"new\s+ToastRequested\s*\((?<args>[^;]*);", RegexOptions.Singleline);
            var literal = new Regex("\"(?<key>[a-z_]+\\.[a-z_]+)\"");
            int calls = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                foreach (Match m in call.Matches(File.ReadAllText(file)))
                {
                    calls++;
                    foreach (Match k in literal.Matches(m.Groups["args"].Value))
                        Assert.That(inList, Does.Contain(k.Groups["key"].Value), Path.GetFileName(file) + " publishes a toast key with no words: " + k.Groups["key"].Value);
                }
            }
            Assert.That(calls, Is.GreaterThanOrEqualTo(4), "the scan found the publishers (WorldSaveFlow x2, SceneFlow x2)");
        }
    }
}
