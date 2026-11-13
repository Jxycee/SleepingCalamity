// Authority: test-only (local owner, solo host). PL-1 task 1.1: the trapdoor driven by button presses only (FakeInputService, every dev key off), solo host in the real Hollowmere scene.
// Catches: the hub prompt not appearing at the trapdoor or appearing out of range, Interact not starting a dive (or starting one from afar), a return dive in a release build starting anywhere but floor 1
// (PL-4 4.2: the pick defaults to the bank floor and a real button cycles it), a walked-over piece not reaching the town stock.
// Setup that is not the action under test: the held checkpoint (TestHoldCheckpoint) and, for the pickup, the floor-2 start that a held checkpoint allows.
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class DiveEntryInputTests
    {
        private const float Timeout = TownInputRig.Timeout;

        [UnityTearDown]
        public IEnumerator TearDown() { yield return PlayModeHarness.TearDownApp(); }

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

        // PL-4 4.2 (replaces PL-1's pin ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint): with a held checkpoint the pick defaults to the bank floor, so a plain Interact uses the pick the party earned.
        [UnityTest]
        public IEnumerator ReturnDive_DefaultsToTheBankFloor()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value && rig.Director.PickableFloors().Count > 1, Timeout, "the held checkpoint never offered more floors");
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Input.DevPressed, Is.Empty, "no dev key is pressed");
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.BankFloor), "the pick defaults to the bank floor");
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value && rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null, Timeout, "the dive never started");
            Assert.That(rig.Director.FloorSync.BuiltFloor, Is.EqualTo(UndercroftPlan.BankFloor), "a plain Interact starts the return dive on the bank floor");
            Assert.That(rig.Director.NetCheckpointHeld.Value, Is.False, "the start spent the checkpoint");
        }

        // The floor-pick button is the Lock button (Tab; right-stick click on a pad): lock-on has nothing to do at the trapdoor.
        [UnityTest]
        public IEnumerator FloorPick_Button_CyclesAllowedFloors()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value && rig.Director.PickableFloors().Count == UndercroftPlan.BankFloor, Timeout, "the held checkpoint never offered floors 1 to the bank");
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.BankFloor));
            int[] expected = { 1, 2, 3 };
            foreach (int want in expected)
            {
                yield return rig.Press(i => i.Frame.LockPressed = true);
                yield return PlayModeHarness.WaitUntil(() => rig.Director.PickFloor == want, Timeout, "the pick button never reached floor " + want + " (at " + rig.Director.PickFloor + ")");
            }
            Assert.That(rig.Input.DevPressed, Is.Empty, "no dev key is pressed");
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value && rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null, Timeout, "the dive never started");
            Assert.That(rig.Director.FloorSync.BuiltFloor, Is.EqualTo(3), "Interact starts on the picked floor");
        }

        [UnityTest]
        public IEnumerator Prompt_NamesTheRealButton()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value && rig.Director.PickableFloors().Count > 1, Timeout, "the held checkpoint never offered more floors");
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Director.PromptText, Does.Not.Contain("G changes"), "the prompt no longer names the dev key");
            Assert.That(rig.Director.PromptText, Does.Contain(InputActionNames.Lock), "the prompt names the bound pick button (the fake input reports the action path as its display string)");
        }

        [UnityTest]
        public IEnumerator FloorPick_NoCheckpoint_OnlyFloor1()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Director.PickableFloors(), Is.EqualTo(new[] { UndercroftPlan.FirstFloor }));
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.FirstFloor));
            yield return rig.Press(i => i.Frame.LockPressed = true);
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.FirstFloor), "the pick button does nothing without a checkpoint");
            Assert.That(rig.Director.PromptText, Does.Not.Contain(InputActionNames.Lock), "the prompt offers no pick button without a checkpoint");
            ulong me = rig.App.NetworkManager.LocalClientId;
            Assert.That(rig.Director.ServerTryStartDive(me, 3), Is.False, "the host refuses floor 3 without a checkpoint");
            Assert.That(rig.Director.NetActive.Value, Is.False);
        }

        [UnityTest]
        public IEnumerator FloorPick_AboveBank_Refused()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value, Timeout, "the checkpoint never showed");
            rig.StandAtTrapdoor();
            yield return TownInputRig.Seconds(0.3f);
            ulong me = rig.App.NetworkManager.LocalClientId;
            Assert.That(rig.Director.ServerTryStartDive(me, UndercroftPlan.BankFloor + 1), Is.False, "a floor below the bank is refused");
            Assert.That(rig.Director.NetActive.Value, Is.False);
            Assert.That(rig.Director.NetCheckpointHeld.Value, Is.True, "a refused pick leaves the checkpoint held");
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
