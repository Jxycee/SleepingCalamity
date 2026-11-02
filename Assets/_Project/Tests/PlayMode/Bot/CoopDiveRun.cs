// Authority: test-only. M7 S2 shared runner for the co-op bots: a real scene-managed host in Arena_Dive (DiveBot on the host hero) plus 1-3 TestClients that decline scenes, each with a
// FollowerBot on its owner-copy hero. It boots like SquareLoopWalkQaTests (the host loads the scene, the clients' heroes are spawned by the test), runs N seconds of game time at
// Time.captureFramerate 60 and samples ReplicatedStateHash across all peers on a schedule.
// Hash sampling differs from a literal ReplicatedStateHash.AssertAllEqual on purpose: that call yields for up to 20 s of real time while nobody ticks the bots, so here the bots keep playing
// and a sample passes if all peers' hashes agree on ANY frame within HashWindow seconds of game time (replication lag), else the diff is recorded.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class CoopDiveRun
    {
        public const float Step = 1f / 60f, HashWindow = 5f;

        public AppBootstrap App;
        public NetworkManager HostNm;
        public readonly List<PlayModeHarness.TestClient> Clients = new List<PlayModeHarness.TestClient>();
        public readonly List<NetworkManager> Peers = new List<NetworkManager>();   // host first
        public readonly List<FollowerBot> Followers = new List<FollowerBot>();
        public DiveBot Bot;
        public bool ToughParty;   // heal every host-side hero below 50% HP once a second (test-only, see Run loop)
        public FakeInputService HostInput;
        public NetworkObject HostHero;
        public uint Seed;

        // ---- log tap (hooked after boot, so the boot's own warnings are not counted) ----
        public readonly List<string> Errors = new List<string>(), Warnings = new List<string>();
        public int ErrorCount, WarningCount;
        readonly Dictionary<string, int> _issueCounts = new Dictionary<string, int>();
        Application.LogCallback _tap;

        // ---- results ----
        public float Seconds;
        public int MaxFloor, DivesHome;
        public int HashSamples, HashPassed, HashMismatches;
        public readonly List<string> HashFindings = new List<string>();
        public readonly Dictionary<ulong, int> Downs = new Dictionary<ulong, int>(), Revives = new Dictionary<ulong, int>(), Outs = new Dictionary<ulong, int>();
        public readonly Dictionary<ulong, float> DownedSeconds = new Dictionary<ulong, float>();
        readonly Dictionary<ulong, VitalState> _lastState = new Dictionary<ulong, VitalState>();
        readonly HashSet<(HeroVitals, HeroVitals)> _ignored = new HashSet<(HeroVitals, HeroVitals)>();
        public string FirstDescribe;
        public bool StateHashLooksStable = true;

        public static string ClientCountName(int clients) => clients == 1 ? "pair" : clients == 3 ? "quad" : "party" + (clients + 1);

        // Boots the party: returns with the host hero and every follower in Arena_Dive's hub, the bots built, the log tap hooked and Time.captureFramerate set.
        public static IEnumerator Boot(int clients, uint seed, System.Action<CoopDiveRun> done)
        {
            var run = new CoopDiveRun { Seed = seed };
            done(run);   // so a failing boot still reaches the test's teardown
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            SceneFlow.DevDiveArena = true;
            AppBootstrap app = AppBootstrap.Current; run.App = app;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            run.HostNm = app.NetworkManager;
            yield return PlayModeHarness.StartClients(app, clients, true, run.Clients);
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == clients + 1, 20f, "[boot] the roster never held " + (clients + 1) + " players");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True, "[boot] the multiplayer start gate opens with " + (clients + 1) + " players");
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.ArenaDive && app.NetworkManager.LocalClient != null && app.NetworkManager.LocalClient.PlayerObject != null, 60f, "[boot] the host never loaded Arena_Dive with its hero");
            run.HostHero = app.NetworkManager.LocalClient.PlayerObject;
            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero, 30f, "[boot] the hub never spawned the director");
            DiveDirector host = DiveDirector.Instance;
            host.SeedOverride = seed;

            Vector3 spawn = run.HostHero.transform.position;
            for (int i = 0; i < clients; i++)
                run.HostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, run.Clients[i].NetworkManager.LocalClientId, false, true, false, spawn + new Vector3(1.5f * (i + 1), 0f, 0.5f), Quaternion.identity);
            run.Peers.Add(run.HostNm);
            var owners = new List<NetworkObject>();
            var peerDirs = new List<DiveDirector>();
            for (int i = 0; i < clients; i++)
            {
                NetworkManager cnm = run.Clients[i].NetworkManager;
                yield return PlayModeHarness.WaitUntil(() => cnm.LocalClient != null && cnm.LocalClient.PlayerObject != null && cnm.SpawnManager.SpawnedObjects.ContainsKey(host.NetworkObjectId), 40f, "[boot] client " + i + " never received its hero and the director");
                owners.Add(cnm.LocalClient.PlayerObject);
                peerDirs.Add(cnm.SpawnManager.SpawnedObjects[host.NetworkObjectId].GetComponent<DiveDirector>());
                run.Peers.Add(cnm);
            }
            double until = Time.realtimeSinceStartupAsDouble + 1.5;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }   // the arrival teleports land

            // each hero reads its own input; the App's service belongs to the host bot (it is also what every peer's director and chest code read: see "cannot do" in the report)
            run.HostInput = new FakeInputService();
            Wire(run.HostHero, run.HostInput);
            run.Bot = new DiveBot(run.HostInput, run.HostHero.transform);
            for (int i = 0; i < clients; i++)
            {
                var fin = new FakeInputService();
                Wire(owners[i], fin);
                run.Followers.Add(new FollowerBot(i + 1, fin, owners[i], peerDirs[i], () => run.HostHero != null ? run.HostHero.transform : null));
            }
            ServiceRoots.App.Replace<IInputService>(run.HostInput);

            DiveBot bot = run.Bot;
            run._tap = (msg, stack, type) =>
            {
                if (type == LogType.Log) return;
                if (msg.StartsWith("[DIVEBOT]") || msg.StartsWith("[FOLLOWER")) return;
                string first = msg.Split('\n')[0]; if (first.Length > 300) first = first.Substring(0, 300);
                string where = (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("SleepingCalamity")) ?? "";
                string key = type + "|" + first;
                run._issueCounts[key] = (run._issueCounts.TryGetValue(key, out int n) ? n : 0) + 1;
                if (run._issueCounts[key] > 1) { if (type == LogType.Warning) run.WarningCount++; else run.ErrorCount++; return; }
                string line = $"{type}: {first}{(where.Length > 0 ? "  [" + where.Trim() + "]" : "")}";
                if (type == LogType.Warning) { run.WarningCount++; run.Warnings.Add(line); }
                else { run.ErrorCount++; run.Errors.Add(line); bot.Note(type == LogType.Exception ? "Exception" : "Error", first, where); }
            };
            Application.logMessageReceived += run._tap;
            Time.captureFramerate = 60;
        }

        static void Wire(NetworkObject hero, FakeInputService input)
        {
            hero.GetComponent<HeroLocomotion>().Input = input;
            hero.GetComponent<TargetLockController>().Input = input;
            hero.GetComponent<HeroCombat>().Input = input;
        }

        public void Dispose()
        {
            if (_tap != null) { Application.logMessageReceived -= _tap; _tap = null; }
            Bot?.Dispose(); Bot = null;
            Time.captureFramerate = 0;
            SceneFlow.DevDiveArena = false;
        }

        // M7 S4: per-frame hook (after the bots ticked, before the frame yields) and read access to the grouped log counts ("Type|first line" -> count).
        public System.Action<CoopDiveRun> EveryFrame;
        public IEnumerable<KeyValuePair<string, int>> IssueCounts => _issueCounts;

        public int Count(string kind) =>_issueCounts.Where(k => k.Key.StartsWith(kind + "|")).Sum(k => k.Value);

        // ------------------------------------------------------------------------------------------------ the run
        // Plays `seconds` of game time. A second dive is started whenever the first one is over (the bot walks the hub stair again).
        public IEnumerator Play(float seconds, float hashEvery, float realTimeLimit = 1500f, System.Action<CoopDiveRun> everySecond = null)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + realTimeLimit;
            float nextHash = hashEvery, hashStart = -1f, nextSecond = 1f;
            bool hashPending = false;
            while (Seconds < seconds)
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) { Bot.Note("Run", $"real-time limit {realTimeLimit:0} s hit at {Seconds:0} s of game time"); break; }
                Seconds += Step;
                Bot.Tick(Step);
                foreach (FollowerBot f in Followers) f.Tick(Step);
                MaxFloor = Mathf.Max(MaxFloor, Bot.Floor);
                if (Bot.WentHome && Bot.Floor == 0) { DivesHome++; Bot.WentHome = false; }   // another dive
                ObserveVitals(Step);
                EveryFrame?.Invoke(this);
                if (Seconds >= nextSecond)
                {
                    nextSecond += 1f; IgnoreOwnCopies(); everySecond?.Invoke(this);
                    // test-only top-up (like DiveBot.Tough): the party is healed below 50% so a soak reaches bank, go-home and further dives instead of wiping on floors 3-4
                    if (ToughParty) foreach (HeroVitals v in HostVitals()) if (v.IsUp && v.Hp < v.MaxHp / 2) v.ServerHeal(v.MaxHp);
                }

                if (!hashPending && Seconds >= nextHash) { hashPending = true; hashStart = Seconds; HashSamples++; }
                if (hashPending)
                {
                    string diff = Mismatch();
                    if (diff == null) { HashPassed++; hashPending = false; nextHash = Seconds + hashEvery; }
                    else if (Seconds - hashStart > HashWindow)
                    {
                        HashMismatches++; hashPending = false; nextHash = Seconds + hashEvery;
                        string who = string.Join(", ", HostVitals().Select(v => $"client {v.OwnerClientId} {v.State}"));
                        HashFindings.Add($"**Hash mismatch** at {Seconds:0} s (floor {Bot.Floor}, dive active {DiveDirector.Instance?.NetActive.Value}; heroes: {who}):\n{Indent(diff)}");
                        Debug.Log("[COOP] hash mismatch at " + Seconds.ToString("0") + " s: " + diff);
                    }
                }
                yield return null;
            }
            ReleaseInputs();
        }

        void ReleaseInputs() { HostInput.Frame = default; foreach (FollowerBot f in Followers) f.Release(); }

        // Releases every input (the bots stop) and plays `seconds` more so a final hash comparison sees a quiet world.
        public IEnumerator Quiesce(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) { ReleaseInputs(); yield return null; }
        }

        // The first differing peer pair as text, or null when every peer's hash agrees.
        public string Mismatch()
        {
            string h0 = ReplicatedStateHash.Hash(Peers[0]);
            string bad = null;
            for (int i = 1; i < Peers.Count; i++)
                if (ReplicatedStateHash.Hash(Peers[i]) != h0)
                {
                    bad = ReplicatedStateHash.Diff("host", ReplicatedStateHash.Describe(Peers[0]), ReplicatedStateHash.Describe(Peers[i]), "peer" + i);
                    break;
                }
            if (FirstDescribe == null) FirstDescribe = ReplicatedStateHash.Describe(Peers[0]);
            return bad;
        }

        static string Indent(string s) => string.Join("\n", s.Split('\n').Select(l => "    " + l));

        IEnumerable<HeroVitals> HostVitals()
        {
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].IsSpawned && all[i].NetworkManager == HostNm) yield return all[i];
        }

        void ObserveVitals(float dt)
        {
            foreach (HeroVitals v in HostVitals())
            {
                ulong id = v.OwnerClientId;
                VitalState now = v.State;
                if (_lastState.TryGetValue(id, out VitalState before) && before != now)
                {
                    if (before == VitalState.Up && now == VitalState.Downed) Downs[id] = (Downs.TryGetValue(id, out int d) ? d : 0) + 1;
                    if (before == VitalState.Downed && now == VitalState.Up) Revives[id] = (Revives.TryGetValue(id, out int r) ? r : 0) + 1;
                    if (now == VitalState.Out) Outs[id] = (Outs.TryGetValue(id, out int o) ? o : 0) + 1;
                }
                _lastState[id] = now;
                if (now == VitalState.Downed) DownedSeconds[id] = (DownedSeconds.TryGetValue(id, out float s) ? s : 0f) + dt;
            }
        }

        // One process holds every peer's copy of a hero: the owner's CharacterController and the host's remote capsule of the same hero overlap and would shove each other
        // (a harness artefact; a real peer holds only one copy). Copies of the SAME hero ignore each other; different heroes still bump, as in the game.
        void IgnoreOwnCopies()
        {
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    HeroVitals a = all[i], b = all[j];
                    // Harness only: every hero-vs-hero pair ignores collision (own copies AND different heroes), so a bot never perches on or is walled by a friend's capsule
                    // (FLOOR3_DOOR.md: dive-2 stuck at y 1.6 on a PlayerCapsule(Clone)). The real game keeps hero-vs-hero collision; the "stand on a friend's head" question stays open.
                    if (a == null || b == null || !a.IsSpawned || !b.IsSpawned) continue;
                    if (!_ignored.Add((a, b))) continue;
                    foreach (Collider ca in a.GetComponentsInChildren<Collider>(true))
                    {
                        if (!ca.gameObject.activeInHierarchy) continue;
                        foreach (Collider cb in b.GetComponentsInChildren<Collider>(true))
                            if (cb.gameObject.activeInHierarchy) Physics.IgnoreCollision(ca, cb, true);
                    }
                }
        }

        public void IgnoreCopiesNow() => IgnoreOwnCopies();

        // ------------------------------------------------------------------------------------------------ report
        public string Report(string title)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# Co-op bot, {title}, seed {Seed}");
            sb.AppendLine();
            sb.AppendLine($"Game time {Seconds:0} s, {Peers.Count} players (host DiveBot + {Followers.Count} FollowerBot).");
            sb.AppendLine();
            sb.AppendLine("## Headline");
            sb.AppendLine();
            sb.AppendLine($"- Deepest floor reached: {MaxFloor}; floors left (host bot): {Bot.FloorsCleared}; dives taken home: {DivesHome + (Bot.WentHome ? 1 : 0)}");
            sb.AppendLine($"- Dives failed: {Bot.DiveFails} (host bot: after low HP {Bot.Deaths}, near a floor/traversal fault {Bot.FaultDeaths})");
            sb.AppendLine($"- Hero downs: {Downs.Values.Sum()} ({string.Join(", ", Downs.OrderBy(k => k.Key).Select(k => "client " + k.Key + ": " + k.Value))}); out: {Outs.Values.Sum()}; revives (downed back up): {Revives.Values.Sum()}");
            sb.AppendLine($"- Time downed: {DownedSeconds.Values.Sum():0} s total ({string.Join(", ", DownedSeconds.OrderBy(k => k.Key).Select(k => "client " + k.Key + ": " + k.Value.ToString("0") + " s"))})");
            sb.AppendLine($"- Hash samples: {HashSamples}, agreed {HashPassed}, mismatches {HashMismatches}");
            sb.AppendLine($"- Log: {ErrorCount} errors/exceptions ({Errors.Count} distinct), {WarningCount} warnings ({Warnings.Count} distinct) since the party was ready");
            sb.AppendLine();
            sb.AppendLine("## Host DiveBot");
            sb.AppendLine();
            sb.AppendLine(Bot.Summary());
            sb.AppendLine();
            sb.AppendLine("## Followers");
            sb.AppendLine();
            foreach (FollowerBot f in Followers) sb.AppendLine("- " + f.Summary() + $" (swings {f.Fight.Swings}, dodges {f.Fight.Dodges}; ArenaBot hit counts are inflated by the other peers' copies of each enemy, not reported)");
            sb.AppendLine();
            sb.AppendLine($"## Hash mismatches ({HashFindings.Count})");
            sb.AppendLine();
            foreach (string h in HashFindings) sb.AppendLine("- " + h);
            sb.AppendLine();
            sb.AppendLine($"## Errors ({Errors.Count} distinct)");
            sb.AppendLine();
            foreach (string e in Errors) sb.AppendLine("- " + e);
            sb.AppendLine();
            sb.AppendLine($"## Warnings ({Warnings.Count} distinct)");
            sb.AppendLine();
            foreach (string w in Warnings) sb.AppendLine("- " + w);
            sb.AppendLine();
            sb.AppendLine($"## Findings ({Bot.Findings.Count + Followers.Sum(f => f.Findings.Count)})");
            sb.AppendLine();
            foreach (DiveBot.Finding f in Bot.Findings) sb.AppendLine("- host: " + f);
            foreach (string s in Bot.Fight.Findings) sb.AppendLine("- host **Combat (ArenaBot)** " + s);
            foreach (FollowerBot fb in Followers) foreach (string s in fb.Findings) sb.AppendLine($"- follower {fb.Index}: " + s);
            sb.AppendLine();
            sb.AppendLine("## What these bots cannot do");
            sb.AppendLine();
            sb.AppendLine("- Followers do not path to goals of their own: they trail the host bot, so they only take pickups, chests and prompts the lead walks them into. They ask for bank / stair / go-home by RPC from their own director (no key press, so the prompt code itself is only exercised for the host hero).");
            sb.AppendLine("- Every peer's director and chest code reads the one App input service (the host bot's), so a host interact press can also fire a nearby follower's own copy of the prompt; the host dedupes.");
            sb.AppendLine("- One process: every peer builds its floor at the same place and holds a copy of every hero and enemy; same-hero copies are set to ignore each other, but a follower stranded on a copy of the floor shows as a stuck/rescue finding.");
            sb.AppendLine("- Loopback transport with captureFramerate 60: no latency, loss or jitter (those are the QA walks under NetCondition). The hash covers replicated state, not transforms or enemies.");
            sb.AppendLine();
            sb.AppendLine("## Host bot log");
            sb.AppendLine();
            foreach (string l in Bot.Log) sb.AppendLine("    " + l);
            sb.AppendLine();
            sb.AppendLine("## Follower logs");
            sb.AppendLine();
            foreach (FollowerBot f in Followers) { sb.AppendLine($"### follower {f.Index}"); sb.AppendLine(); foreach (string l in f.Log) sb.AppendLine("    " + l); sb.AppendLine(); }
            return sb.ToString();
        }
    }
}
