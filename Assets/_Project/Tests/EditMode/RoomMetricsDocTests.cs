// Tests/EditMode/RoomMetricsDocTests.cs: docs/ROOM_METRICS.md carries the generated GameMetrics table, so the doc cannot drift from the code (metrics gym plan, Task 6). Core-only.
using System;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class RoomMetricsDocTests
    {
        const string Begin = "<!-- metrics:begin -->", End = "<!-- metrics:end -->";

        // Walks up from the working directory (the Core runner's project root, or Unity's project root, which is the repo root here) to the folder holding docs/ROOM_METRICS.md.
        static string FindDoc()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                string p = Path.Combine(dir.FullName, "docs", "ROOM_METRICS.md");
                if (File.Exists(p)) return p;
                dir = dir.Parent;
            }
            return null;
        }

        [Test] public void DocTable_MatchesGameMetrics()
        {
            string path = FindDoc();
            Assert.That(path, Is.Not.Null, "docs/ROOM_METRICS.md not found above " + Directory.GetCurrentDirectory());
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            int a = text.IndexOf(Begin, StringComparison.Ordinal), b = text.IndexOf(End, StringComparison.Ordinal);
            Assert.That(a, Is.GreaterThanOrEqualTo(0), "begin marker missing"); Assert.That(b, Is.GreaterThan(a), "end marker missing");
            string have = text.Substring(a + Begin.Length, b - a - Begin.Length).Trim();
            string want = GameMetrics.MarkdownTable().Replace("\r\n", "\n").Trim();
            Assert.That(have, Is.EqualTo(want), "ROOM_METRICS.md table is stale. Paste this between the markers:\n" + want);
        }
    }
}
