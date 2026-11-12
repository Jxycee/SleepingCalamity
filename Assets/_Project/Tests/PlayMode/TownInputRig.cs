// Authority: test-only (local owner, solo host). PL-1 task 1.1 shared setup for the town-side input fixtures (PlacementModeInputTests, DiveEntryInputTests): a solo host in the REAL Hollowmere scene
// (SceneFlow default start, DiveHub, DiveDirector, TownSync, PlacementMode), the hero wired to a FakeInputService (the same one a bot feeds), and a Press helper that gives one clean button edge.
// Every action in those fixtures goes through the fake's PlayerInputFrame; no dev key is ever pressed (DevPressed stays empty) and no Server* method is called for the action under test.
using System;
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class TownInputRig
    {
        public const float Timeout = 40f;

        public AppBootstrap App;
        public FakeInputService Input;
        public NetworkObject Hero;
        public DiveDirector Director;
        public TownSync Town;
        public PlacementMode Mode;
        public string SaveDir;

        // Solo host in Hollowmere; `stock` pieces (and the baker if asked) are put in the town stock the way a finished dive would leave them. `bakerInStock`: the rescued baker waits in the stock too.
        public static IEnumerator Start(Action<TownInputRig> done, int[] stock = null, bool bakerInStock = false, bool worldSave = false)
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            string saveDir = worldSave ? PlayModeHarness.BeginSaveTest() : null;   // the world save on, against a throwaway library (the caller ends it with EndSaveTest(rig.SaveDir))
            SceneFlow.StartSceneOverride = null;   // the real default: the town
            yield return PlayModeHarness.StartSoloAndWaitForPlayer(SceneNames.Hollowmere);
            var rig = new TownInputRig { App = AppBootstrap.Current, SaveDir = saveDir };
            rig.Hero = rig.App.NetworkManager.LocalClient.PlayerObject;
            rig.Input = new FakeInputService();
            rig.Hero.GetComponent<HeroLocomotion>().Input = rig.Input;
            rig.Hero.GetComponent<TargetLockController>().Input = rig.Input;
            rig.Hero.GetComponent<HeroCombat>().Input = rig.Input;
            ServiceRoots.App.Replace<IInputService>(rig.Input);

            yield return PlayModeHarness.WaitUntil(() => DiveDirector.Instance != null && DiveDirector.Instance.NetHubStair.Value != Vector3.zero
                && DiveDirector.Instance.TownSync != null && DiveDirector.Instance.TownSync.IsSpawned, Timeout, "the hub never spawned the director and its TownSync");
            rig.Director = DiveDirector.Instance;
            rig.Town = rig.Director.TownSync;
            rig.Mode = UnityEngine.Object.FindFirstObjectByType<PlacementMode>(FindObjectsInactive.Include);
            Assert.That(rig.Mode, Is.Not.Null, "Hollowmere has its PlacementMode");
            Assert.That(rig.Mode.BuildPoint, Is.Not.Null, "and its build point");
            if (stock != null) foreach (int p in stock) Assert.That(rig.Director.Town.State.AddPiece(p), Is.EqualTo(PlaceResult.Ok), "stock piece " + p);
            if (bakerInStock) Assert.That(rig.Director.Town.State.AddResident(TownBuildings.BakerResident), Is.EqualTo(PlaceResult.Ok));
            rig.Town.ServerRefresh();
            yield return Seconds(1f);   // the spawn teleports have landed before the test moves the hero
            done(rig);
        }

        public static IEnumerator Seconds(float s)
        {
            double until = Time.realtimeSinceStartupAsDouble + s;
            while (Time.realtimeSinceStartupAsDouble < until) { PlayModeHarness.Pace(); yield return null; }
        }

        // One clean edge: the frame is set for exactly one rendered frame, then cleared for another so the next press is an edge again.
        public IEnumerator Press(Action<FakeInputService> set)
        {
            Input.Frame = default;
            set(Input);
            yield return null;
            Input.Frame = default;
            yield return null;
        }

        public IEnumerator PressInteract() { return Press(i => i.Frame.InteractPressed = true); }
        public IEnumerator PressPrimary() { return Press(i => i.Frame.PrimaryPressed = true); }
        public IEnumerator PressSecondary() { return Press(i => i.Frame.SecondaryPressed = true); }
        public IEnumerator PressJump() { return Press(i => i.Frame.JumpPressed = true); }
        public IEnumerator PressPause() { return Press(i => i.Frame.PausePressed = true); }

        public void MoveHero(Vector3 pos)
        {
            var cc = Hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            Hero.transform.position = pos;
            if (cc != null) cc.enabled = true;
        }

        public void StandAtBuildPoint() { MoveHero(Mode.BuildPoint.position + new Vector3(0.5f, 0.2f, 0f)); }
        public void StandAtTrapdoor() { MoveHero(Director.NetHubStair.Value + new Vector3(1f, 0.1f, 0f)); }

        // Interact at the build point and wait for placement mode.
        public IEnumerator EnterPlacement()
        {
            StandAtBuildPoint();
            yield return PlayModeHarness.WaitUntil(() => Vector3.Distance(Hero.transform.position, Mode.BuildPoint.position) < 3f, Timeout, "the hero never settled at the build point");
            yield return PressInteract();
            yield return PlayModeHarness.WaitUntil(() => Mode.Active, Timeout, "Interact at the build point never entered placement mode");
        }

        // Cycles the free lots with the placement button for "next lot" (Interact) until the aim is on `lot`.
        public IEnumerator AimAt(string lot)
        {
            for (int i = 0; i < 30 && Mode.AimLot != lot; i++) yield return PressInteract();
            Assert.That(Mode.AimLot, Is.EqualTo(lot), "the next-lot button never reached " + lot);
        }

        public static float Flat(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
    }
}
