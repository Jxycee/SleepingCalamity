// Authority: test-only (local owner, solo host). PL-1 task 1.1: the trapdoor driven by button presses only (FakeInputService, every dev key off), solo host in the real Hollowmere scene.
// Catches: the hub prompt not appearing at the trapdoor or appearing out of range, Interact not starting a dive (or starting one from afar), a return dive in a release build starting anywhere but floor 1
// (PL-4 4.2: ReturnDive_DefaultsToTheBankFloor, the Lock button cycles the pick, the host validates a client's pick), a walked-over piece not reaching the town stock.
// Setup that is not the action under test: the held checkpoint (TestHoldCheckpoint) and, for the pickup, the floor-2 start that a held checkpoint allows (a button for that pick is PL-4).
using System.Collections;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using SleepingCalamity.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DiveEntryInputTests
    {
        private const float Timeout = TownInputRig.Timeout;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest]
        public IEnumerator InteractAtTrapdoor_StartsDive_Solo()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt at the trapdoor");
            Assert.That(rig.Director.NetActive.Value, Is.False);
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value, Timeout, "Interact at the trapdoor never started the dive");
            yield return PlayModeHarness.WaitUntil(() => rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null && rig.Director.FloorSync.BuiltFloor == UndercroftPlan.FirstFloor, Timeout, "floor 1 never built");
            yield return PlayModeHarness.WaitUntil(() => rig.Hero.transform.position.y < AreaLook.Current.CellarBelowY, Timeout, "the hero never arrived under the town");
        }

        [UnityTest]
        public IEnumerator OutOfRange_NoPrompt_NoStart()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            Assert.That(TownInputRig.Flat(rig.Hero.transform.position, rig.Director.NetHubStair.Value), Is.GreaterThan(DiveDirector.PromptRadius + 5f), "the spawn is far from the trapdoor");
            yield return rig.PressInteract();
            yield return TownInputRig.Seconds(0.5f);
            Assert.That(rig.Director.Prompt, Is.EqualTo(DiveDirector.PromptKind.None));
            Assert.That(rig.Director.NetActive.Value, Is.False, "an Interact far from the trapdoor started nothing");
            // Just outside the prompt radius too.
            rig.MoveHero(rig.Director.NetHubStair.Value + new Vector3(DiveDirector.PromptRadius + 1.5f, 0.1f, 0f));
            yield return TownInputRig.Seconds(0.3f);
            yield return rig.PressInteract();
            yield return TownInputRig.Seconds(0.5f);
            Assert.That(rig.Director.Prompt, Is.EqualTo(DiveDirector.PromptKind.None));
            Assert.That(rig.Director.NetActive.Value, Is.False);
        }

        private static IEnumerator HoldCheckpointAndStand(TownInputRig rig)
        {
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value && rig.Director.PickableFloors().Count > 1, Timeout, "the held checkpoint never offered more floors");
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
        }

        // PL-4 4.2: with a held checkpoint the plain Interact press starts on the bank floor (the pick the party earned), not floor 1, and spends the checkpoint.
        [UnityTest]
        public IEnumerator ReturnDive_DefaultsToTheBankFloor()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            yield return HoldCheckpointAndStand(rig);
            Assert.That(rig.Input.DevPressed, Is.Empty, "no dev key is pressed");
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.BankFloor), "the pick defaults to the bank floor");
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value && rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null, Timeout, "the dive never started");
            Assert.That(rig.Director.FloorSync.BuiltFloor, Is.EqualTo(UndercroftPlan.BankFloor), "the return dive starts on the bank floor");
            Assert.That(rig.Director.NetCheckpointHeld.Value, Is.False, "the start spends the checkpoint");
        }

        // The Lock button (a real, rebindable action; no dev key) steps the pick through 1..bank and wraps; Interact then starts on the shown floor.
        [UnityTest]
        public IEnumerator FloorPick_Button_CyclesAllowedFloors()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            yield return HoldCheckpointAndStand(rig);
            var seen = new System.Collections.Generic.List<int> { rig.Director.PickFloor };
            for (int i = 0; i < 5; i++)
            {
                int before = rig.Director.PickFloor;
                yield return rig.Press(f => f.Frame.LockPressed = true);
                yield return PlayModeHarness.WaitUntil(() => rig.Director.PickFloor != before, Timeout, "the cycle button never changed the pick from " + before);
                seen.Add(rig.Director.PickFloor);
            }
            Assert.That(seen, Is.EqualTo(new[] { 5, 1, 2, 3, 4, 5 }), "the pick wraps through every allowed floor");
            yield return rig.Press(f => f.Frame.LockPressed = true);
            yield return rig.Press(f => f.Frame.LockPressed = true);
            yield return rig.Press(f => f.Frame.LockPressed = true);   // 5 -> 1 -> 2 -> 3
            yield return PlayModeHarness.WaitUntil(() => rig.Director.PickFloor == 3, Timeout, "the pick never reached floor 3");
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value && rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null, Timeout, "the dive never started");
            Assert.That(rig.Director.FloorSync.BuiltFloor, Is.EqualTo(3), "Interact starts on the picked floor");
        }

        [UnityTest]
        public IEnumerator Prompt_NamesTheRealButton()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            yield return HoldCheckpointAndStand(rig);
            Assert.That(rig.Director.PromptText, Does.Contain(InputActionNames.Lock), "the prompt names the cycle button (the fake input echoes the action path)");
            Assert.That(rig.Director.PromptText, Does.Not.Contain("G changes"), "the dev key is not named");
        }

        [UnityTest]
        public IEnumerator FloorPick_NoCheckpoint_OnlyFloor1()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Director.PickableFloors(), Is.EqualTo(new[] { UndercroftPlan.FirstFloor }));
            yield return rig.Press(f => f.Frame.LockPressed = true);
            yield return TownInputRig.Seconds(0.3f);
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.FirstFloor), "the button has nothing to cycle");
            Assert.That(rig.Director.ServerStartDive(3), Is.False, "the host refuses floor 3 without the checkpoint");
            Assert.That(rig.Director.NetActive.Value, Is.False);
        }

        [UnityTest]
        public IEnumerator FloorPick_AboveBank_Refused()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            Assert.That(rig.Director.ServerStartDive(UndercroftPlan.BankFloor + 1), Is.False, "a floor below the bank floor is not pickable");
            Assert.That(rig.Director.NetActive.Value, Is.False);
            Assert.That(rig.Director.Checkpoint.Held, Is.True, "a refused pick does not spend the checkpoint");
        }

        // A client's press is only a request: the host decides against the checkpoint it holds. The client's own default pick is the bank floor once it sees the checkpoint.
        [UnityTest]
        public IEnumerator FloorPick_ClientPick_HostValidates()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            ServiceRoots.App.Replace<IInputService>(new FakeInputService());   // the real adapter's binding display string needs a desktop keyboard layout (the Linux editor crashes headless)
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.IsConnectedClient, Timeout, "the client never connected");
            Vector3 home = new Vector3(0f, 0f, -6f), hubStair = new Vector3(0f, 0f, 9f);
            NetworkObject dirObj = hostNm.SpawnManager.InstantiateAndSpawn(PrefabOf<DiveDirector>(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            DiveDirector host = dirObj.GetComponent<DiveDirector>();
            Assert.That(host.ServerConfigure(home, hubStair, new Vector3(200f, 0f, 0f)), Is.True);
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(dirObj.NetworkObjectId), Timeout, "the client never received the director");
            DiveDirector peer = client.NetworkManager.SpawnManager.SpawnedObjects[dirObj.NetworkObjectId].GetComponent<DiveDirector>();
            hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, home, Quaternion.identity);
            hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, client.NetworkManager.LocalClientId, false, true, false, hubStair + new Vector3(1f, 0.1f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => client.NetworkManager.LocalClient != null && client.NetworkManager.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            yield return TownInputRig.Seconds(0.8f);

            int handled = host.RequestsHandled;
            peer.RequestStartDive(3);
            yield return PlayModeHarness.WaitUntil(() => host.RequestsHandled > handled, Timeout, "the press never reached the host");
            Assert.That(host.NetActive.Value, Is.False, "no checkpoint: the host refuses the client's floor 3");

            host.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => peer.NetCheckpointHeld.Value && peer.NetPickableMaxFloor.Value == UndercroftPlan.BankFloor, Timeout, "the client never saw the checkpoint");
            yield return PlayModeHarness.WaitUntil(() => peer.PickFloor == UndercroftPlan.BankFloor, Timeout, "the client's pick never defaulted to the bank floor (it is " + peer.PickFloor + ")");
            handled = host.RequestsHandled;
            peer.RequestStartDive(UndercroftPlan.BankFloor + 1);
            yield return PlayModeHarness.WaitUntil(() => host.RequestsHandled > handled, Timeout, "the above-bank press never reached the host");
            Assert.That(host.NetActive.Value, Is.False, "a floor above the bank pick is refused");
            Assert.That(host.Checkpoint.Held, Is.True);

            handled = host.RequestsHandled;
            peer.RequestStartDive(3);   // an allowed pick below the default (floor 5 is built in the solo tests; two peers building it in one process crashes the cloud editor)
            yield return PlayModeHarness.WaitUntil(() => host.RequestsHandled > handled && host.NetActive.Value, Timeout, "the client's allowed pick never started the dive");
            Assert.That(host.Session.Floor, Is.EqualTo(3));
            Assert.That(host.Checkpoint.Held, Is.False);
            yield return PlayModeHarness.WaitUntil(() => host.FloorSync != null && host.FloorSync.Built != null && peer.FloorSync != null && peer.FloorSync.Built != null, Timeout, "the floor never built on both peers");   // let the build finish before the teardown
        }

        private readonly System.Collections.Generic.List<PlayModeHarness.TestClient> _clients = new System.Collections.Generic.List<PlayModeHarness.TestClient>();

        private static NetworkObject PrefabOf<T>(NetworkManager nm) where T : Component
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list");
            return null;
        }

        // A real pickup on a generated floor: walk onto each waiting piece of floor 2 (the hero is moved there, the host's own range check picks the piece up) and it reaches the town stock.
        // Floor 2 is reached through the held checkpoint's pick; the frame lives there in every new world (decision D1).
        [UnityTest]
        public IEnumerator WalkOverPiece_PieceReachesTownStock()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            DiveDirector d = rig.Director;
            Assert.That(d.Town.State.InStock(PieceCatalog.FrameId), Is.False);
            d.TestHoldCheckpoint(true);
            Assert.That(d.ServerStartDive(2), Is.True, "the checkpoint allows starting on floor 2");
            yield return PlayModeHarness.WaitUntil(() => d.FloorSync != null && d.FloorSync.Built != null && d.FloorSync.BuiltFloor == 2, Timeout, "floor 2 never built");
            yield return PlayModeHarness.WaitUntil(() => rig.Hero.transform.position.y < AreaLook.Current.CellarBelowY, Timeout, "the hero never arrived under the town");
            yield return PlayModeHarness.WaitUntil(() => d.ContentsSync != null && d.ContentsSync.State.Floor == 2 && d.ContentsSync.PieceCount > 0, Timeout, "floor 2 shows no waiting piece");
            yield return TownInputRig.Seconds(1f);
            int before = 0; foreach (int _ in d.Town.State.StockPieces) before++;
            int count = d.ContentsSync.PieceCount, taken = 0;
            for (int i = 0; i < count; i++)
            {
                if (!d.ContentsSync.PieceVisible(i) || !d.ContentsSync.TryPiecePosition(i, out Vector3 at)) continue;
                rig.MoveHero(at + new Vector3(0f, 0.2f, 0f));
                int n = i;
                yield return PlayModeHarness.WaitUntil(() => !d.ContentsSync.PieceVisible(n), Timeout, "piece " + n + " was never picked up where the hero stands");
                taken++;
            }
            Assert.That(taken, Is.GreaterThan(0), "floor 2 had a piece to walk over");
            int after = 0; foreach (int _ in d.Town.State.StockPieces) after++;
            Assert.That(after - before, Is.EqualTo(taken), "every piece walked over reached the town stock at once");
            Assert.That(d.Town.State.InStock(PieceCatalog.FrameId), Is.True, "the bakery frame lives on floor 2 in a new world (D1)");
        }
    }
}
