// Authority: test-only (local owner, solo host). PL-1 task 1.1: placement mode driven by button presses only (FakeInputService), solo host in the real Hollowmere scene.
// Catches: Interact at the build point not taking the builder lock / not entering InputContext.Placement, Primary not placing frame -> parts -> resident in order, a lane-blocking turn being accepted or
// refused without words, Secondary not returning the last piece, Pause not leaving (and not asking the world to save), a hero that walks away keeping the lock.
// NOT covered: the camera on screen, the wish panel, a second player (PL-1 task 1.3, SquareLoopWalkQaTests).
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class PlacementModeInputTests
    {
        private const float Timeout = TownInputRig.Timeout;
        private static readonly int[] Bakery = { PieceCatalog.FrameId, PieceCatalog.OvenId, PieceCatalog.DoorId, PieceCatalog.SignId };
        private string _dir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
            if (_dir != null) { PlayModeHarness.EndSaveTest(_dir); _dir = null; }
        }

        [UnityTest]
        public IEnumerator InteractAtBuildPoint_TakesLock_EntersPlacement()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            Assert.That(rig.Mode.Active, Is.False);
            Assert.That(rig.Town.BuilderIsFree, Is.True);
            yield return rig.EnterPlacement();
            Assert.That(rig.Town.IsBuilder, Is.True, "the host gave this peer the builder lock");
            Assert.That(rig.Input.Context, Is.EqualTo(InputContext.Placement), "the input context switched to Placement");
            Assert.That(rig.Mode.AimLot, Is.Not.Empty, "a lot is aimed at");
        }

        [UnityTest]
        public IEnumerator Primary_PlacesFrame_WhenFrameInStock()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            yield return rig.EnterPlacement();
            yield return rig.AimAt("F02");
            yield return rig.PressPrimary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "Primary never placed the frame");
            Assert.That(rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery).FoundationId, Is.EqualTo("F02"));
            Assert.That(rig.Town.View.InStock(PieceCatalog.FrameId), Is.False, "the frame left the stock");
        }

        [UnityTest]
        public IEnumerator Primary_WithoutFrame_SaysNoFrameInStock()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);   // empty stock
            yield return rig.EnterPlacement();
            yield return rig.PressPrimary();
            yield return null;
            Assert.That(rig.Mode.Message, Is.EqualTo("No frame in stock"));
            Assert.That(rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery), Is.Null);
        }

        [UnityTest]
        public IEnumerator Jump_TurnsGhost_BlockedTurnIsRefused()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            yield return rig.EnterPlacement();
            yield return rig.AimAt("F02");
            int turn0 = rig.Mode.AimTurn;
            yield return rig.PressJump();
            Assert.That(rig.Mode.AimTurn, Is.Not.EqualTo(turn0), "Jump turns the ghost");
            yield return rig.PressJump();   // F02 turned twice cuts the path graph (SquareLoopWalkQaTests S4)
            Assert.That(rig.Mode.AimTurn, Is.EqualTo(WishEvaluator.NormalTurn(turn0 + 2)));
            yield return rig.PressPrimary();
            yield return PlayModeHarness.WaitUntil(() => rig.Mode.Message == PlacementText.Refusal((byte)PlaceResult.PathBlocked), Timeout,
                "the blocked turn was not refused in words (message '" + rig.Mode.Message + "')");
            Assert.That(rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery), Is.Null, "nothing was placed");
            Assert.That(rig.Town.View.InStock(PieceCatalog.FrameId), Is.True, "the frame stayed in stock");
            yield return rig.PressJump(); yield return rig.PressJump();   // back to the lot's own turn
            yield return rig.PressPrimary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "the legal turn was not placed");
        }

        [UnityTest]
        public IEnumerator Primary_PlacesPartsInOrder_ThenResident()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery, bakerInStock: true);
            yield return rig.EnterPlacement();
            yield return rig.AimAt("F02");
            BuildingDef def = TownBuildings.Get(TownBuildings.Bakery);
            yield return rig.PressPrimary();   // the frame
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "no frame");
            int placedParts = 0;
            for (int k = 0; k < 3; k++)   // oven, door, sign: one press each, the next step is whatever PlacementSteps names
            {
                PlaceStep next = PlacementSteps.Next(rig.Town.View, TownBuildings.Bakery);
                Assert.That(next.Kind, Is.EqualTo(PlaceStepKind.Part), "step " + k);
                int idx = def.PartIndex(next.Part);
                yield return rig.PressPrimary();
                yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx] >= 0, Timeout, "the " + next.Part + " never landed");
                placedParts++;
            }
            Assert.That(placedParts, Is.EqualTo(3));
            Assert.That(rig.Town.View.IsComplete(TownBuildings.Bakery), Is.True, "frame and three parts: the bakery stands");
            Assert.That(PlacementSteps.Next(rig.Town.View, TownBuildings.Bakery).Kind, Is.EqualTo(PlaceStepKind.Resident));
            yield return rig.PressPrimary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery).Resident == TownBuildings.BakerResident, Timeout, "the baker never moved in");
            Assert.That(rig.Town.View.ResidentInStock(TownBuildings.BakerResident), Is.False);
        }

        [UnityTest]
        public IEnumerator Secondary_PicksUpLastPlaced()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            yield return rig.EnterPlacement();
            yield return rig.AimAt("F02");
            yield return rig.PressPrimary();   // frame
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "no frame");
            BuildingDef def = TownBuildings.Get(TownBuildings.Bakery);
            PlaceStep part = PlacementSteps.Next(rig.Town.View, TownBuildings.Bakery);
            int idx = def.PartIndex(part.Part);
            yield return rig.PressPrimary();   // first part
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx] >= 0, Timeout, "no part");
            yield return rig.PressSecondary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery).PiecesByPart[idx] < 0, Timeout, "Secondary never picked the part up");
            Assert.That(rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery), Is.Not.Null, "the frame stays");
            yield return rig.PressSecondary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) == null, Timeout, "a second Secondary never picked the frame up");
            Assert.That(rig.Town.View.InStock(PieceCatalog.FrameId), Is.True, "the frame is back in stock");
        }

        [UnityTest]
        public IEnumerator Pause_LeavesPlacement_ReleasesLock_WorldSaved()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => { rig = r; _dir = r.SaveDir; }, Bakery, worldSave: true);
            yield return rig.EnterPlacement();
            yield return rig.AimAt("F02");
            yield return rig.PressPrimary();
            yield return PlayModeHarness.WaitUntil(() => rig.Town.View.PlacedBuildingOf(TownBuildings.Bakery) != null, Timeout, "no frame");
            Assert.That(ServiceRoots.App.TryGet(out HostWorld world), Is.True);
            int saves = 0;
            Action onSave = () => saves++;
            world.SaveRequested += onSave;
            yield return rig.PressPause();
            world.SaveRequested -= onSave;
            yield return PlayModeHarness.WaitUntil(() => !rig.Mode.Active && rig.Town.BuilderIsFree, Timeout, "Pause never left placement and freed the lock");
            Assert.That(rig.Input.Context, Is.EqualTo(InputContext.Free), "the hero is back in the Free context");
            Assert.That(saves, Is.GreaterThan(0), "leaving placement asked the world to save");
            string id = rig.App.WorldSaveFlow.SelectedWorldId;
            string path = Path.Combine(_dir, id + ".json");
            yield return PlayModeHarness.WaitUntil(() => File.Exists(path), Timeout, "the world file was never written by leaving placement");
            WorldSave file = JsonSaveFile<WorldSave>.DeserializeOrNull(File.ReadAllText(path), w => w.SchemaVersion);
            Assert.That(file, Is.Not.Null);
            Assert.That(file.Town, Is.Not.Null);
            Assert.That(file.Town.Buildings.Count, Is.EqualTo(1), "the placed bakery is in the saved world");
        }

        [UnityTest]
        public IEnumerator WalkingAway_LeavesPlacement()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            yield return rig.EnterPlacement();
            rig.MoveHero(rig.Mode.BuildPoint.position + new Vector3(40f, 0.2f, 0f));   // past PromptRadius * 4: only a teleport does this
            yield return PlayModeHarness.WaitUntil(() => !rig.Mode.Active && rig.Town.BuilderIsFree, Timeout, "walking away never left placement and freed the lock");
            Assert.That(rig.Input.Context, Is.EqualTo(InputContext.Free));
        }
    }
}
