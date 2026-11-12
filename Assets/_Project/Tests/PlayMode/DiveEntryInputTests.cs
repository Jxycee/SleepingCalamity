// Authority: test-only (local owner, solo host). PL-1 task 1.1: the trapdoor driven by button presses only (FakeInputService, every dev key off), solo host in the real Hollowmere scene.
// Catches: the hub prompt not appearing at the trapdoor or appearing out of range, Interact not starting a dive (or starting one from afar), a return dive in a release build starting anywhere but floor 1
// (ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint PINS today's behaviour, PL-4 task 4.2 replaces it), a walked-over piece not reaching the town stock.
// Setup that is not the action under test: the held checkpoint (TestHoldCheckpoint) and, for the pickup, the floor-2 start that a held checkpoint allows (a button for that pick is PL-4).
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
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

        // Today's release behaviour: with a checkpoint held the prompt offers floors 1..bank but the cycle button is the dev key G, so a press without it starts on floor 1.
        [UnityTest]
        public IEnumerator ReturnDive_NoDevKey_StartsOnFloor1_DespiteCheckpoint()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            rig.Director.TestHoldCheckpoint(true);
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetCheckpointHeld.Value && rig.Director.PickableFloors().Count > 1, Timeout, "the held checkpoint never offered more floors");
            rig.StandAtTrapdoor();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.Prompt == DiveDirector.PromptKind.Descend, Timeout, "no trapdoor prompt");
            Assert.That(rig.Input.DevPressed, Is.Empty, "no dev key is pressed");
            Assert.That(rig.Director.PickFloor, Is.EqualTo(UndercroftPlan.FirstFloor), "the pick defaults to floor 1");
            Assert.That(rig.Director.PromptText, Does.Contain("G changes the floor"), "the prompt still names the dev key (PL-4 4.2 gives it a real button)");
            yield return rig.PressInteract();
            yield return PlayModeHarness.WaitUntil(() => rig.Director.NetActive.Value && rig.Director.FloorSync != null && rig.Director.FloorSync.Built != null, Timeout, "the dive never started");
            Assert.That(rig.Director.FloorSync.BuiltFloor, Is.EqualTo(UndercroftPlan.FirstFloor), "a release build starts a return dive on floor 1 whatever the checkpoint holds");
            // The rules spend the checkpoint on any start ("whatever floor it picks", DiveCheckpoint), so the default press burns the bank pick the party earned: without the dev key the player never gets to use it.
            Assert.That(rig.Director.NetCheckpointHeld.Value, Is.False, "PL break (fixed by PL-4 4.2): the floor-1 start spent the checkpoint");
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
