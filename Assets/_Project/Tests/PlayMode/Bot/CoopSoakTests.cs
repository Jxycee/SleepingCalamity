// Authority: test-only. M7 S4 (docs/design/m7-proposal.md section 4): the long co-op soak. A quad party (host + 3 clients, the world maximum) plays Arena_Dive for SC_SOAK_MINUTES of game time
// at Time.captureFramerate 60 with the S2 bots (CoopDiveRun loops dives: the bot walks the hub stair again whenever a dive is over), while this harness records, per phase (hub, floor 1..4+):
// real wall time per frame (Stopwatch; captureFramerate fixes Time.deltaTime), managed-heap growth per frame, plus heap / NetworkObject / enemy counts every 10 s, and a forced-collect heap every 120 s
// (the leak test: the unforced heap is a sawtooth). The log tap and ReplicatedStateHash sampling are CoopDiveRun's.
// Ignored unless SC_SOAK=1:  SC_SOAK=1 [SC_SOAK_MINUTES=30] [SC_BOT_OUT=<dir>] powershell ... Tools\unity.ps1 test-play -Filter CoopSoakTests -OutDir <scratch>
// Writes <SC_BOT_OUT>/Soak_quad_<seed>.md (default Reference/qa/m7). PlayMode is not category-filtered by the wrapper, so SC_SOAK is what keeps `check` from running it.
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SleepingCalamity.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace SleepingCalamity.Tests.PlayMode
{
    [Category("Bot"), Category("Soak")]
    public class CoopSoakTests
    {
        private CoopDiveRun _run;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _run?.Dispose(); _run = null;
            yield return PlayModeHarness.TearDownApp();
        }

        sealed class Phase
        {
            public readonly List<float> Ms = new List<float>();
            public long AllocBytes; public int Frames; public float GameSeconds;
        }

        struct Sample { public float T; public int Floor; public double HeapMb; public int NetObjects, Enemies, EnemiesAll; }

        [UnityTest, Timeout(4200000)]
        public IEnumerator CoopSoak_Quad_Seed12345()
        {
            if (System.Environment.GetEnvironmentVariable("SC_SOAK") != "1") Assert.Ignore("co-op soak: set SC_SOAK=1 to run");
            LogAssert.ignoreFailingMessages = true;
            float minutes = 30f;
            if (float.TryParse(System.Environment.GetEnvironmentVariable("SC_SOAK_MINUTES"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float m) && m > 0f) minutes = m;
            const uint seed = 12345u;

            yield return CoopDiveRun.Boot(3, seed, r => _run = r);
            CoopDiveRun run = _run;
            // SC_SOAK_TOUGH=1: heal the party below 50% HP so the run keeps cycling (bank, go-home, next dive); without it the quad party wipes on floors 3-4 and the soak measures that instead
            if (System.Environment.GetEnvironmentVariable("SC_SOAK_TOUGH") == "1") { run.ToughParty = true; run.Bot.Tough = true; }

            var phases = new SortedDictionary<int, Phase>();
            var samples = new List<Sample>();
            var forced = new List<(float T, double Mb)>();
            var sw = Stopwatch.StartNew();
            long lastTicks = sw.ElapsedTicks, lastHeap = System.GC.GetTotalMemory(false);
            float nextSample = 0f, nextForced = 120f;
            int prevFloor = 0, divesStarted = 0, skipFrame = 0;
            double startHeapMb = ForcedHeapMb(); lastTicks = sw.ElapsedTicks; lastHeap = System.GC.GetTotalMemory(false);
            long wallStart = sw.ElapsedMilliseconds;

            run.EveryFrame = r =>
            {
                long now = sw.ElapsedTicks;
                float ms = (float)((now - lastTicks) * 1000.0 / Stopwatch.Frequency);
                lastTicks = now;
                long heap = System.GC.GetTotalMemory(false);
                int floor = Mathf.Min(r.Bot.Floor, 4);
                if (floor > 0 && prevFloor == 0) divesStarted++;
                prevFloor = floor;
                if (skipFrame > 0) { skipFrame--; lastHeap = heap; }   // the forced collect's own frame is not a gameplay frame
                else
                {
                    if (!phases.TryGetValue(floor, out Phase p)) phases[floor] = p = new Phase();
                    p.Ms.Add(ms); p.Frames++; p.GameSeconds += CoopDiveRun.Step;
                    if (heap > lastHeap) p.AllocBytes += heap - lastHeap;
                    lastHeap = heap;
                }
                if (r.Seconds >= nextSample)
                {
                    nextSample += 10f;
                    int enemiesHost = 0, enemiesAll = Enemy.All.Count;
                    for (int i = 0; i < enemiesAll; i++) if (Enemy.All[i] != null && Enemy.All[i].IsSpawned && Enemy.All[i].NetworkManager == r.HostNm) enemiesHost++;
                    samples.Add(new Sample { T = r.Seconds, Floor = r.Bot.Floor, HeapMb = heap / 1048576.0, NetObjects = r.HostNm.SpawnManager.SpawnedObjects.Count, Enemies = enemiesHost, EnemiesAll = enemiesAll });
                }
                if (r.Seconds >= nextForced)
                {
                    nextForced += 120f;
                    forced.Add((r.Seconds, ForcedHeapMb()));
                    skipFrame = 1; lastTicks = sw.ElapsedTicks; lastHeap = System.GC.GetTotalMemory(false);
                }
            };

            float wallLimit = 2700f;   // 45 min of real time
            yield return run.Play(minutes * 60f, 10f, realTimeLimit: wallLimit);
            double wallSeconds = (sw.ElapsedMilliseconds - wallStart) / 1000.0;
            run.EveryFrame = null;
            double endHeapMb = ForcedHeapMb();
            yield return run.Quiesce(2f);

            string dir = System.Environment.GetEnvironmentVariable("SC_BOT_OUT");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reference", "qa", "m7"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"Soak_quad_{seed}.md");
            File.WriteAllText(file, Build(run, minutes, wallSeconds, divesStarted, phases, samples, forced, startHeapMb, endHeapMb));
            Debug.Log($"[SOAK] quad seed {seed}: {run.Seconds:0} s game in {wallSeconds:0} s wall, dives started {divesStarted}, hash mismatches {run.HashMismatches}, errors {run.ErrorCount}, warnings {run.WarningCount}, heap {startHeapMb:0.0} -> {endHeapMb:0.0} MB. Report: {file}");
            Assert.That(run.MaxFloor, Is.GreaterThanOrEqualTo(1), "the host bot never got into the dive. Report: " + file);
        }

        static double ForcedHeapMb()
        {
            System.GC.Collect(); System.GC.WaitForPendingFinalizers(); System.GC.Collect();
            return System.GC.GetTotalMemory(true) / 1048576.0;
        }

        static float Pct(List<float> sorted, double p) => sorted.Count == 0 ? 0f : sorted[Mathf.Clamp((int)System.Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];

        // least-squares slope of y over t (t in seconds) -> per minute
        static double SlopePerMin(IList<(float T, double V)> pts)
        {
            if (pts.Count < 2) return 0;
            double mt = pts.Average(p => (double)p.T), mv = pts.Average(p => p.V), num = 0, den = 0;
            foreach (var p in pts) { num += (p.T - mt) * (p.V - mv); den += (p.T - mt) * (p.T - mt); }
            return den <= 0 ? 0 : num / den * 60.0;
        }

        static string Build(CoopDiveRun run, float minutes, double wall, int divesStarted, SortedDictionary<int, Phase> phases, List<Sample> samples, List<(float T, double Mb)> forced, double startMb, double endMb)
        {
            var sb = new StringBuilder();
            double forcedSlope = SlopePerMin(forced.Select(f => (f.T, f.Mb)).ToList());
            double rawSlope = SlopePerMin(samples.Select(s => (s.T, s.HeapMb)).ToList());
            int maxObjs = samples.Count == 0 ? 0 : samples.Max(s => s.NetObjects);
            double objSlope = SlopePerMin(samples.Select(s => (s.T, (double)s.NetObjects)).ToList());
            double enemySlope = SlopePerMin(samples.Select(s => (s.T, (double)s.EnemiesAll)).ToList());
            int errors = run.ErrorCount, warns = run.WarningCount;
            bool completed = run.Seconds >= minutes * 60f - 1f;
            var why = new List<string>();
            if (errors > 0) why.Add(errors + " errors/exceptions");
            if (run.HashMismatches > 0) why.Add(run.HashMismatches + " hash mismatches");
            if (!completed) why.Add($"stopped at {run.Seconds:0} s of {minutes * 60f:0} s game time (wall limit)");
            if (phases.Count > 0 && phases.Values.Max(p => p.GameSeconds) > 0.5f * run.Seconds) why.Add("the bot spent over half the run in one phase (stuck dive: repeated dive cycles not covered)");
            if (divesStarted < 3) why.Add($"only {divesStarted} dives started");
            if (forcedSlope >= 0.5) why.Add($"forced-collect heap grows {forcedSlope:0.00} MB/min");
            if (objSlope >= 0.05) why.Add($"NetworkObject count trends +{objSlope:0.00}/min");

            string verdict = why.Count == 0 ? "CLEAN" : "NOT CLEAN";
            sb.AppendLine($"# Co-op soak, quad (4 players), seed {run.Seed}");
            sb.AppendLine();
            sb.AppendLine($"**Verdict: {verdict}**{(why.Count > 0 ? " (" + string.Join("; ", why) + ")" : "")}");
            sb.AppendLine();
            sb.AppendLine($"Game time {run.Seconds:0} s of {minutes * 60f:0} s planned, {wall:0} s wall ({wall / System.Math.Max(1.0, run.Seconds):0.00} wall s per game s), Time.captureFramerate 60, host DiveBot + 3 FollowerBots.");
            sb.AppendLine();
            sb.AppendLine("## Dives");
            sb.AppendLine();
            sb.AppendLine($"- Dives started (host bot left the hub into floor 1): {divesStarted}; taken home: {run.DivesHome + (run.Bot.WentHome ? 1 : 0)}; failed: {run.Bot.DiveFails} (low HP {run.Bot.Deaths}, near a floor/traversal fault {run.Bot.FaultDeaths}); floors left: {run.Bot.FloorsCleared}; deepest floor {run.MaxFloor}");
            sb.AppendLine($"- Hero downs {run.Downs.Values.Sum()} ({string.Join(", ", run.Downs.OrderBy(k => k.Key).Select(k => "client " + k.Key + ": " + k.Value))}); out {run.Outs.Values.Sum()}; revives {run.Revives.Values.Sum()}; time downed {run.DownedSeconds.Values.Sum():0} s");
            sb.AppendLine($"- Host bot: enemies killed {run.Bot.EnemiesKilled}, chests {run.Bot.ChestsOpened}, pieces {run.Bot.PiecesTaken}, stuck events {run.Bot.StuckEvents}, hearts banked: {run.Bot.HeartBanked}");
            sb.AppendLine();
            sb.AppendLine("## Frame time per phase (real wall ms per frame, Stopwatch)");
            sb.AppendLine();
            sb.AppendLine("| Phase | Game s | Frames | Mean | p95 | p99 | Max | Managed alloc / frame |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            var all = new List<float>();
            foreach (var kv in phases)
            {
                var s = kv.Value.Ms.OrderBy(x => x).ToList(); all.AddRange(s);
                sb.AppendLine($"| {(kv.Key == 0 ? "hub" : kv.Key == 4 ? "floor 4+" : "floor " + kv.Key)} | {kv.Value.GameSeconds:0} | {kv.Value.Frames} | {s.Average():0.0} | {Pct(s, 0.95):0.0} | {Pct(s, 0.99):0.0} | {s.Last():0.0} | {kv.Value.AllocBytes / (double)System.Math.Max(1, kv.Value.Frames):0} B |");
            }
            all.Sort();
            if (all.Count > 0) sb.AppendLine($"| **all** | {all.Count / 60f:0} | {all.Count} | {all.Average():0.0} | {Pct(all, 0.95):0.0} | {Pct(all, 0.99):0.0} | {all.Last():0.0} | |");
            sb.AppendLine();
            sb.AppendLine("Frame time is the whole loop of the one process: four peers' simulation, the bots, the hash and this sampler, uncapped (no vsync wait). Managed alloc per frame is the sum of positive deltas of GC.GetTotalMemory(false) (a collection inside a frame hides some allocation, so it is a lower bound).");
            sb.AppendLine();
            sb.AppendLine("## Heap and object trend");
            sb.AppendLine();
            sb.AppendLine($"- Managed heap after a forced collect: start {startMb:0.0} MB, end {endMb:0.0} MB (delta {endMb - startMb:+0.0;-0.0} MB); forced-collect samples every 120 s: {string.Join(", ", forced.Select(f => $"{f.T:0}s={f.Mb:0.0}"))}; trend {forcedSlope:+0.00;-0.00} MB/min");
            sb.AppendLine($"- Unforced heap (sawtooth) trend over {samples.Count} samples: {rawSlope:+0.00;-0.00} MB/min");
            sb.AppendLine($"- Host-side NetworkObjects: max {maxObjs}, trend {objSlope:+0.000;-0.000}/min; enemies (all peers' copies) trend {enemySlope:+0.000;-0.000}/min");
            sb.AppendLine();
            sb.AppendLine("| Game s | Floor | Heap MB (unforced) | NetworkObjects (host) | Enemies (host / all peers) |");
            sb.AppendLine("|---:|---:|---:|---:|---:|");
            int stride = System.Math.Max(1, samples.Count / 40);
            for (int i = 0; i < samples.Count; i += stride) { Sample s = samples[i]; sb.AppendLine($"| {s.T:0} | {s.Floor} | {s.HeapMb:0.0} | {s.NetObjects} | {s.Enemies} / {s.EnemiesAll} |"); }
            sb.AppendLine();
            sb.AppendLine($"## Hash (ReplicatedStateHash, every 10 s, agree on any frame within {CoopDiveRun.HashWindow:0} s)");
            sb.AppendLine();
            sb.AppendLine($"- Samples {run.HashSamples}, agreed {run.HashPassed}, mismatches {run.HashMismatches}");
            foreach (string h in run.HashFindings) sb.AppendLine("- " + h);
            sb.AppendLine();
            sb.AppendLine($"## Log (since the party was ready): {errors} errors/exceptions ({run.Errors.Count} distinct), {warns} warnings ({run.Warnings.Count} distinct)");
            sb.AppendLine();
            sb.AppendLine("| Count | Kind | First line | Where (first occurrence) |");
            sb.AppendLine("|---:|---|---|---|");
            var firstWhere = run.Errors.Concat(run.Warnings).ToDictionary(l => l.Split(':')[0] + "|" + (l.IndexOf("  [") > 0 ? l.Substring(l.IndexOf(": ") + 2, l.IndexOf("  [") - l.IndexOf(": ") - 2) : l.Substring(l.IndexOf(": ") + 2)), l => l.IndexOf("  [") > 0 ? l.Substring(l.IndexOf("  [")).Trim() : "", System.StringComparer.Ordinal);
            foreach (var kv in run.IssueCounts.OrderByDescending(k => k.Value))
            {
                int bar = kv.Key.IndexOf('|');
                firstWhere.TryGetValue(kv.Key, out string where);
                sb.AppendLine($"| {kv.Value} | {kv.Key.Substring(0, bar)} | {kv.Key.Substring(bar + 1).Replace("|", "\\|")} | {(where ?? "").Replace("|", "\\|")} |");
            }
            sb.AppendLine();
            var groups = new[] { "NullReference", "NetworkVariable", "Netcode", "NGO", "NetworkObject", "RPC", "Exception" };
            sb.AppendLine("Grouped by keyword: " + string.Join(", ", groups.Select(g => g + " " + run.IssueCounts.Where(k => k.Key.IndexOf(g, System.StringComparison.OrdinalIgnoreCase) >= 0).Sum(k => k.Value))));
            sb.AppendLine();
            sb.AppendLine("## What this cannot show");
            sb.AppendLine();
            sb.AppendLine("- Loopback transport inside one process: no latency, loss, jitter, MTU or bandwidth limits (the QA walks under NetCondition cover those). Network cost is a memcpy here.");
            sb.AppendLine("- One process runs all four peers, so frame time is the SUM of four peers' work plus the bots: a real peer would run roughly a quarter of the simulation but also render, and the host's share is in between. Frame time here is a relative regression signal, not a per-player FPS.");
            sb.AppendLine("- The editor (not a player build) with the test runner, and likely -nographics: no rendering, UI, audio or GPU cost. Editor GC and Mono differ from IL2CPP builds.");
            sb.AppendLine("- Bots, not people: no menus, inventory, town building, disconnect/rejoin or unusual input. Followers trail the host bot. Seed is pinned (12345), so floor layouts repeat per dive index, not per time.");
            sb.AppendLine("- Fixed 60 Hz captureFramerate: no variable-delta bugs, no vsync pacing.");
            sb.AppendLine();
            sb.AppendLine("## Proposed frame-time budgets for the quad case (PROPOSALS, derived from this one run, not agreed)");
            sb.AppendLine();
            if (all.Count > 0)
            {
                float p95 = Pct(all, 0.95), p99 = Pct(all, 0.99);
                sb.AppendLine($"- Observed overall p95 {p95:0.0} ms, p99 {p99:0.0} ms. Proposed regression budgets: p95 < {Mathf.Ceil(p95 * 1.5f):0} ms and p99 < {Mathf.Ceil(p99 * 1.5f):0} ms per phase (about 1.5x the observed values, to leave room for machine noise), measured by this soak on the same machine class.");
                sb.AppendLine("- Managed allocation: budget against the table's B/frame per phase (propose 1.5x the observed hub and floor values), and a forced-collect heap slope of < 0.5 MB/min.");
                sb.AppendLine("- Object count: host NetworkObjects must return to within a small margin of the hub baseline after each dive (the table above), trend < 0.05 per minute.");
            }
            sb.AppendLine();
            sb.AppendLine("## Host bot log (tail)");
            sb.AppendLine();
            foreach (string l in run.Bot.Log.Skip(System.Math.Max(0, run.Bot.Log.Count - 60))) sb.AppendLine("    " + l);
            sb.AppendLine();
            sb.AppendLine($"## Bot findings ({run.Bot.Findings.Count + run.Followers.Sum(f => f.Findings.Count)})");
            sb.AppendLine();
            foreach (DiveBot.Finding f in run.Bot.Findings.Take(60)) sb.AppendLine("- host: " + f);
            foreach (FollowerBot fb in run.Followers) foreach (string s in fb.Findings.Take(30)) sb.AppendLine($"- follower {fb.Index}: " + s);
            return sb.ToString();
        }
    }
}
