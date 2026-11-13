// Authority: test-only. PL-1 tasks 1.2 and 1.3: the playable loop by player input only, from floor 1 (see LoopBot). Ignored unless SC_BOT=1, so `check` never runs them:
//   $env:SC_BOT=1; Tools\unity.ps1 test-play -Filter LoopBotTests -OutDir <scratch>
// SoloLoop_Tough_InputOnly_FrameHome_Placed_SurvivesReload  trapdoor press -> DiveBot dive (host tops HP up) -> home by the bank room's go-home button or a death -> the frame is in the stock (it lives on
//   floor 2 in every new world, D1) -> build point press -> place what is in stock -> Pause -> leave -> start the same world again -> the town is exactly as it was. Seeds 7, 12345, 2026.
// SoloLoop_Measure  the same loop with Tough OFF, up to 4 dives, stopping when the frame is home; never fails; writes Reference/sessions/pl1-loop-proof/RESULTS.md (dives to the first frame, game minutes,
//   deaths per floor, the floor each piece was found on).
// PairLoop_Tough_BothByInput_FrameHome_ClientPlacesPart_SurvivesReload  host + one in-process client in the real Hollowmere: the host hero on the host DiveBot, the client hero on its own FakeInputService +
//   FollowerBot (wired per hero like CoopDiveRun.Wire), real pickups from floor 1. The client declines scene loads, so it has NO PlacementMode and no Hollowmere: it places a part through TownSync
//   requests (as SquareLoopWalkQaTests does) while the host places the frame by input.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    [Category("Bot")]
    public class LoopBotTests
    {
        private const float Timeout = TownInputRig.Timeout;
        private string _dir;
        private PlayModeHarness.TestClient _client;
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _exceptions = new List<string>();   // LogType.Exception entries only (unlike plain errors, never acceptable)
        private Application.LogCallback _tap;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_tap != null) Application.logMessageReceived -= _tap;
            _tap = null;
            Time.captureFramerate = 0;
            if (_client != null) { yield return _client.LeaveAndDestroy(); _client = null; }
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        private void Arm()
        {
            if (Environment.GetEnvironmentVariable("SC_BOT") != "1") Assert.Ignore("loop bot: set SC_BOT=1 to run");
            LogAssert.ignoreFailingMessages = true;   // errors are findings, the run goes on
            _tap = (msg, stack, type) =>
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                if (msg.StartsWith("[DIVEBOT]") || msg.StartsWith("[LOOPBOT]")) return;
                string first = msg.Split('\n')[0];
                if (type == LogType.Exception && _exceptions.Count < 30) _exceptions.Add(first.Length > 300 ? first.Substring(0, 300) : first);
                if (_errors.Count < 30 && !_errors.Contains(first)) _errors.Add(first.Length > 300 ? first.Substring(0, 300) : first);
            };
            Application.logMessageReceived += _tap;
        }

        // ================================================================================================ solo
        [UnityTest, Timeout(7200000)]
        public IEnumerator SoloLoop_Tough_InputOnly_FrameHome_Placed_SurvivesReload([Values(7u, 12345u, 2026u)] uint seed)
        {
            Arm();
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => { rig = r; _dir = r.SaveDir; }, worldSave: true);
            rig.Director.SeedOverride = seed;
            var loop = new LoopBot(rig) { Tough = true };
            for (int n = 0; n < 3 && !loop.FrameHome; n++)
            {
                yield return loop.PlayDive();
                Assert.That(loop.Dives[n].TimedOut, Is.False, "dive " + (n + 1) + " did not end within its time limit: " + string.Join(" | ", loop.Log));
            }
            Assert.That(loop.FrameHome, Is.True, "no frame came home within 3 dives: " + string.Join(" | ", loop.Log));
            Assert.That(loop.PieceFloor.TryGetValue(PieceCatalog.FrameId, out int frameFloor) ? frameFloor : -1, Is.EqualTo(2), "the frame was picked up on floor 2 (D1)");

            yield return loop.PlaceStock();
            TownState town = rig.Director.Town.State;
            Assert.That(town.PlacedBuildingOf(TownBuildings.Bakery), Is.Not.Null, "the frame stands");
            Assert.That(town.InStock(PieceCatalog.FrameId), Is.False, "the frame left the stock");
            string placedSig = LoopBot.Sig(town);
            string placedId = town.PlacedBuildingOf(TownBuildings.Bakery).FoundationId;

            TownInputRig again = null;
            yield return loop.LeaveAndReopen(r => again = r);
            TownState reloaded = again.Director.Town.State;
            Assert.That(reloaded.PlacedBuildingOf(TownBuildings.Bakery), Is.Not.Null, "the placed bakery survived leaving and reloading the world");
            Assert.That(reloaded.PlacedBuildingOf(TownBuildings.Bakery).FoundationId, Is.EqualTo(placedId));
            Assert.That(LoopBot.Sig(reloaded), Is.EqualTo(placedSig), "the town, stock and residents are exactly as they were");
            Assert.That(again.Director.NetActive.Value, Is.False, "nobody is below after the reload");
            Assert.That(_exceptions, Is.Empty, "exceptions were logged during the run");
            Debug.Log("[LOOPBOT] seed " + seed + " dives " + loop.Dives.Count + " errors " + _errors.Count + (_errors.Count > 0 ? ": " + string.Join(" || ", _errors) : ""));
        }

        [UnityTest, Timeout(7200000)]
        public IEnumerator SoloLoop_Measure([Values(7u, 12345u, 2026u)] uint seed)
        {
            Arm();
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.SeedOverride = seed;
            var loop = new LoopBot(rig) { Tough = false, StopWhenFrameHome = true };
            string problem = null;
            for (int n = 0; n < 4 && !loop.FrameHome; n++)
            {
                yield return loop.PlayDive();
                if (loop.Dives[n].TimedOut) { problem = "dive " + (n + 1) + " timed out"; break; }
            }
            WriteResults(seed, loop, problem);
            Debug.Log("[LOOPBOT] measure seed " + seed + ": frame home " + loop.FrameHome + " in " + loop.Dives.Count + " dives, " + loop.Dives.Sum(x => x.Seconds) / 60f + " min");
        }

        private static string ResultsPath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reference", "sessions", "pl1-loop-proof", "RESULTS.md"));

        // One "## Seed N" section per seed; a rerun of a seed replaces its section, the others stay.
        private void WriteResults(uint seed, LoopBot loop, string problem)
        {
            var sb = new StringBuilder();
            sb.AppendLine("## Seed " + seed);
            sb.AppendLine();
            bool home = loop.FrameHome;
            float minutes = loop.Dives.Sum(d => d.Seconds) / 60f;
            sb.AppendLine($"- Frame home: **{(home ? "yes" : "no")}**, dives needed: **{loop.Dives.Count}**{(home ? "" : " (gave up: 4 dives or a time limit)")}, game minutes played: **{minutes:0.0}** (first frame {(home ? "reached in the last dive" : "never")}){(problem != null ? ", note: " + problem : "")}");
            sb.AppendLine($"- Deaths (failed dives) by the floor they ended on: {(loop.DeathsByFloor.Count == 0 ? "none" : string.Join(", ", loop.DeathsByFloor.OrderBy(k => k.Key).Select(k => "F" + k.Key + " x" + k.Value)))}");
            sb.AppendLine($"- Pieces found, by floor: {(loop.PieceFloor.Count == 0 ? "none" : string.Join(", ", loop.PieceFloor.OrderBy(k => k.Key).Select(k => k.Key + " on F" + k.Value)))}{(loop.BakerFloor > 0 ? ", baker on F" + loop.BakerFloor : "")}");
            sb.AppendLine("- Errors in the log: " + (_errors.Count == 0 ? "none" : _errors.Count + " distinct"));
            sb.AppendLine();
            sb.AppendLine("| dive | game s | ended | floor | deepest | found |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (LoopBot.DiveRecord d in loop.Dives)
                sb.AppendLine($"| {d.Number} | {d.Seconds:0} | {(d.End.HasValue ? d.End.ToString() : d.StoppedEarly ? "stopped (frame home)" : "timed out")} | F{d.EndFloor} | F{d.MaxFloor} | {(d.Found.Count == 0 ? "-" : string.Join("; ", d.Found))} |");
            if (_errors.Count > 0) { sb.AppendLine(); sb.AppendLine("Log errors:"); foreach (string e in _errors) sb.AppendLine("- " + e); }
            string path = ResultsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sections = new SortedDictionary<uint, string>();
            if (File.Exists(path))
                foreach (string part in File.ReadAllText(path).Split(new[] { "\n## Seed " }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string body = part.StartsWith("## Seed ") ? part.Substring(8) : part;
                    int nl = body.IndexOf('\n');
                    if (nl > 0 && uint.TryParse(body.Substring(0, nl).Trim(), out uint other) && other != seed) sections[other] = "## Seed " + body.TrimEnd() + "\n";
                }
            sections[seed] = sb.ToString().TrimEnd() + "\n";
            var all = new StringBuilder();
            all.AppendLine("# PL-1 loop proof: first building by player input only (SoloLoop_Measure, Tough off)");
            all.AppendLine();
            all.AppendLine("Bot: LoopBot (trapdoor press, DiveBot through FakeInputService, bank-room go-home press or a death, build-point press). Dives start on floor 1 (the bot presses Lock past the bank-floor default); the frame lives on floor 2 (D1); floor 3 mix 30/50/20 (D4).");
            foreach (var kv in sections) { all.AppendLine(); all.Append(kv.Value); }
            File.WriteAllText(path, all.ToString());
            Debug.Log("[LOOPBOT] results: " + path);
        }

        // ================================================================================================ pair
        private sealed class Pair
        {
            public TownInputRig Rig;
            public NetworkObject ClientHeroOnHost, ClientHero;
            public DiveDirector Peer; public TownSync PeerTown;
            public FakeInputService ClientInput; public FollowerBot Follower;
            public Transform SpawnPoint;
        }

        private static Transform FindIn(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform t = FindDeep(root.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++) { Transform r = FindDeep(t.GetChild(i), name); if (r != null) return r; }
            return null;
        }

        private static void Move(NetworkObject o, Vector3 pos)
        {
            var cc = o.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            o.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        // The SquareLoopWalkQaTests session (host loads Hollowmere, one client joins declining scenes and gets its hero from the test), then both heroes on their own fake input.
        private IEnumerator StartPair(Action<Pair> done)
        {
            AppBootstrap app = AppBootstrap.Current;
            SceneFlow.StartSceneOverride = null;
            yield return PlayModeHarness.StartDirectHostWithScenes();
            _client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8), declineScenes: true);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(_client.JoinAsync(cts.Token), 20f, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => app.Roster.ConnectedCount == 2, 20f, "[pair] the second player never reached the roster");
            Assert.That(app.SceneFlow.TryStartSession(), Is.True, "[pair] the multiplayer start gate opens with two players");
            yield return PlayModeHarness.WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.Hollowmere && app.NetworkManager.LocalClient.PlayerObject != null, Timeout, "[pair] the host never loaded Hollowmere with its player");
            var p = new Pair();
            NetworkManager hostNm = app.NetworkManager, clientNm = _client.NetworkManager;
            p.SpawnPoint = FindIn("SpawnPoint");
            Assert.That(p.SpawnPoint, Is.Not.Null, "[pair] Hollowmere has its spawn point");
            yield return PlayModeHarness.WaitUntil(() => clientNm.IsConnectedClient, Timeout, "[pair] the client lost its connection");
            NetworkObject hostHero = hostNm.LocalClient.PlayerObject;
            p.ClientHeroOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientNm.LocalClientId, false, true, false, p.SpawnPoint.position + new Vector3(1.5f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null, Timeout, "[pair] the client never received its hero");
            p.ClientHero = clientNm.LocalClient.PlayerObject;

            // the host hero: the app's input service is the host's fake (every peer's director reads it too: only the host presses buttons); the client hero reads its own
            p.Rig = new TownInputRig { App = app, Hero = hostHero, Input = new FakeInputService(), SaveDir = _dir };
            yield return p.Rig.Finish(null, false);
            var objs = clientNm.SpawnManager.SpawnedObjects;
            ulong did = p.Rig.Director.NetworkObjectId, tid = p.Rig.Town.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => objs.ContainsKey(did) && objs.ContainsKey(tid), Timeout, "[pair] the client never received the director and the TownSync");
            p.Peer = objs[did].GetComponent<DiveDirector>(); p.PeerTown = objs[tid].GetComponent<TownSync>();
            p.ClientInput = new FakeInputService();
            p.ClientHero.GetComponent<HeroLocomotion>().Input = p.ClientInput;
            p.ClientHero.GetComponent<TargetLockController>().Input = p.ClientInput;
            p.ClientHero.GetComponent<HeroCombat>().Input = p.ClientInput;
            p.Follower = new FollowerBot(1, p.ClientInput, p.ClientHero, p.Peer, () => p.Rig.Hero != null ? p.Rig.Hero.transform : null);
            done(p);
        }

        [UnityTest, Timeout(7200000)]
        public IEnumerator PairLoop_Tough_BothByInput_FrameHome_ClientPlacesPart_SurvivesReload()
        {
            Arm();
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            _dir = PlayModeHarness.BeginSaveTest();
            AppBootstrap app = AppBootstrap.Current;
            string worldId = app.WorldSaveFlow.CreateWorld(WorldKind.Multiplayer, IdentityKind.Local);
            Assert.That(worldId, Is.Not.Null, "[pair] a multiplayer world was made");
            Pair pair = null;
            yield return StartPair(p => pair = p);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[pair] the session opened the world");
            pair.Rig.Director.SeedOverride = 7u;

            var loop = new LoopBot(pair.Rig) { Tough = true };
            HeroVitals clientVitals = pair.ClientHeroOnHost.GetComponent<HeroVitals>();
            float heal = 0f;
            loop.ExtraTick = dt =>
            {
                pair.Follower.Tick(dt);
                heal += dt;
                if (heal >= 1f) { heal = 0f; if (clientVitals.IsUp && clientVitals.Hp < clientVitals.MaxHp / 2) clientVitals.ServerHeal(clientVitals.MaxHp); }   // the same test-only top-up the host DiveBot gets
            };
            loop.BeforeDive = () => Pre(pair);
            for (int n = 0; n < 3 && !loop.FrameHome; n++)
            {
                yield return loop.PlayDive();
                pair.ClientInput.Frame = default;
                Assert.That(loop.Dives[n].TimedOut, Is.False, "dive " + (n + 1) + " did not end within its time limit: " + string.Join(" | ", loop.Log));
                if (loop.Dives[n].End.HasValue)
                    yield return PlayModeHarness.WaitUntil(() => pair.ClientHeroOnHost.transform.position.y > AreaLook.Current.CellarBelowY && !pair.Peer.NetActive.Value, Timeout, "[pair] the client never came home");
            }
            Assert.That(loop.FrameHome, Is.True, "no frame came home within 3 dives: " + string.Join(" | ", loop.Log));
            yield return PlayModeHarness.WaitUntil(() => pair.PeerTown.View.InStock(PieceCatalog.FrameId), Timeout, "[pair] the client never saw the frame in the stock");

            // the host places the frame by input, then leaves placement
            yield return loop.PlaceStock(frameOnly: true);
            TownState hostTown = pair.Rig.Director.Town.State;
            Assert.That(hostTown.PlacedBuildingOf(TownBuildings.Bakery), Is.Not.Null, "[pair] the host's frame stands");
            yield return PlayModeHarness.WaitUntil(() => pair.PeerTown.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "[pair] the client never saw the frame");

            // the client has no PlacementMode (it declined the Hollowmere scene), so it asks the host through TownSync, as a real client's mode would
            PlaceStep next = PlacementSteps.Next(pair.PeerTown.View, TownBuildings.Bakery);
            if (next.Kind == PlaceStepKind.Part)
            {
                pair.PeerTown.RequestBuildLock();
                yield return PlayModeHarness.WaitUntil(() => pair.PeerTown.IsBuilder, Timeout, "[pair] the builder lock was never granted to the client");
                int idx = TownBuildings.Get(TownBuildings.Bakery).PartIndex(next.Part);
                pair.PeerTown.RequestPlacePart(TownBuildings.Bakery, next.Part);
                yield return PlayModeHarness.WaitUntil(() => pair.PeerTown.View.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx] >= 0, Timeout, "[pair] the client's " + next.Part + " never landed");
                pair.PeerTown.ReleaseBuildLock();
                yield return PlayModeHarness.WaitUntil(() => pair.Rig.Town.BuilderIsFree, Timeout, "[pair] the client's lock never freed");
                Assert.That(hostTown.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx], Is.GreaterThanOrEqualTo(0), "[pair] the host holds the client's part");
            }
            else Assert.Inconclusive("the dive brought home only the frame (next step " + next.Kind + "): the client had no part to place. Found: " + string.Join(" | ", loop.Log));
            Assert.That(LoopBot.Sig(pair.PeerTown.View), Is.EqualTo(LoopBot.Sig(hostTown)), "[pair] host and client towns agree");
            string placedSig = LoopBot.Sig(hostTown);

            // leave and start the same world again
            app.WorldSaveFlow.SaveNow();
            yield return _client.LeaveAndDestroy(); _client = null;
            yield return PlayModeHarness.LeaveAndWaitForLobby();
            Pair again = null;
            yield return StartPair(p => again = p);
            Assert.That(app.WorldSaveFlow.SelectedWorldId, Is.EqualTo(worldId), "[pair] the same world reopened");
            Assert.That(LoopBot.Sig(again.Rig.Director.Town.State), Is.EqualTo(placedSig), "[pair] the town, stock and residents are exactly as saved");
            yield return PlayModeHarness.WaitUntil(() => LoopBot.Sig(again.PeerTown.View) == placedSig, Timeout, "[pair] the client never saw the reloaded town");
            Assert.That(_exceptions, Is.Empty, "[pair] exceptions were logged during the run");
            Debug.Log("[LOOPBOT] pair dives " + loop.Dives.Count + " errors " + _errors.Count + (_errors.Count > 0 ? ": " + string.Join(" || ", _errors) : "") + " " + pair.Follower.Summary());
        }

        // Before each dive: the second hero stands at the trapdoor too (the host's press moves the whole party).
        private static IEnumerator Pre(Pair p)
        {
            Vector3 at = p.Rig.Director.NetHubStair.Value + new Vector3(-1f, 0.1f, 0f);
            Move(p.ClientHero, at);
            yield return PlayModeHarness.WaitUntil(() => TownInputRig.Flat(p.ClientHeroOnHost.transform.position, at) < 1.5f, Timeout, "[pair] the client hero never arrived at the trapdoor on the host");
        }
    }
}
