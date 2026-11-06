// Authority: test-only. Plan 5 task 6: the floor card HUD picks up the live dive state. One host (no client), the hero simulated through CellarContentsSync.PlayersOverride, a TwoValves floor found with the Core
// FloorObjectivePlan. The text itself is Core's FloorCardText (EditMode/Core tests); these tests check that the HUD shows the full card on arrival, drops to the compact panel after FullCardSeconds, caches the card
// (one FloorCard.For per floor, not per frame) and follows the valve mask.
// NOT covered: the IMGUI drawing itself (layout, fonts).
using System.Collections;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using SleepingCalamity.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class FloorCardHudTests
    {
        private const float Timeout = 25f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private static T Spawn<T>(NetworkManager host) where T : Component
        {
            foreach (NetworkPrefabsList list in host.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null)
                        return host.SpawnManager.InstantiateAndSpawn(entry.Prefab.GetComponent<NetworkObject>(), host.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity).GetComponent<T>();
            Assert.Fail(typeof(T).Name + " is not in the network prefab list (run the project builder)");
            return null;
        }

        private static uint FindValveSeed(int floor)
        {
            for (uint s = 1; s < 4000; s++)
                if (FloorObjectivePlan.For(CellarFloorGenerator.Layout(s, floor)).Objective == FloorObjective.TwoValves) return s;
            Assert.Fail("no seed with two valves on floor " + floor); return 0;
        }

        private sealed class Ctx { public CellarFloorSync Floor; public DiveHaulSync Haul; public CellarContentsSync Contents; public ulong HostId; public Vector3 Hero; public uint Seed; }

        private IEnumerator Setup(System.Action<Ctx> done)
        {
            const int floor = 2;
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager nm = app.NetworkManager;
            var c = new Ctx { Seed = FindValveSeed(floor), HostId = nm.LocalClientId };
            c.Floor = Spawn<CellarFloorSync>(nm); c.Haul = Spawn<DiveHaulSync>(nm); c.Contents = Spawn<CellarContentsSync>(nm);
            c.Contents.FloorSync = c.Floor; c.Contents.HaulSync = c.Haul; c.Haul.FloorSync = c.Floor;
            c.Hero = new Vector3(500f, 0f, 500f);
            c.Contents.PlayersOverride = list => list.Add(new CellarContentsSync.PlayerPos { ClientId = c.HostId, Position = c.Hero });
            Assert.That(c.Haul.ServerBegin(), Is.True);
            Assert.That(c.Haul.ServerSetFloor(floor), Is.True);
            Assert.That(c.Floor.ServerSetFloor(c.Seed, floor), Is.True);
            yield return PlayModeHarness.WaitUntil(() => c.Floor.Built != null && c.Floor.BuiltFloor == floor && c.Contents.State.Floor == floor && c.Contents.Plan != null, Timeout, "the floor was not built / armed");
            done(c);
        }

        [UnityTest]
        public IEnumerator Hud_ShowsFullCardOnArrival_ThenPanel()
        {
            Ctx c = null; yield return Setup(x => c = x);
            var m = new FloorCardHudModel();
            m.Tick(c.Contents, 0, 100f);
            Assert.That(m.Mode, Is.EqualTo(FloorCardMode.Full), "the card is full on arrival");
            Assert.That(m.Card, Is.Not.Null);
            Assert.That(m.Card.Floor, Is.EqualTo(2));
            Assert.That(m.Card.Objective, Is.EqualTo(FloorObjective.TwoValves));
            string name = m.Card.Name;
            Assert.That(m.Lines(true)[0], Does.Contain(name).And.Contain("Floor 2"));
            Assert.That(m.Lines(true), Does.Contain("Open both valves (0/2)"));
            for (float t = 100.5f; t < 100f + FloorCardHudModel.FullCardSeconds; t += 0.5f) m.Tick(c.Contents, 0, t);
            Assert.That(m.Mode, Is.EqualTo(FloorCardMode.Full), "still full just before the time is up");
            m.Tick(c.Contents, 0, 100f + FloorCardHudModel.FullCardSeconds + 0.1f);
            Assert.That(m.Mode, Is.EqualTo(FloorCardMode.Compact), "then the compact panel");
            Assert.That(m.Lines(false)[0], Is.EqualTo(name));
            Assert.That(m.Lines(false), Does.Contain("Open both valves (0/2)"));
            Assert.That(m.ComputeCount, Is.EqualTo(1), "the card is computed once per floor, not per tick");
            m.Tick(c.Contents, 3, 200f);
            Assert.That(m.Lines(false), Does.Contain("Pieces left here: 3"));

            // The live component finds the dive state on its own and shows it.
            var go = new GameObject("FloorCardHudTest"); var hud = go.AddComponent<FloorCardHud>();
            hud.Contents = c.Contents; hud.Haul = c.Haul;
            yield return PlayModeHarness.WaitUntil(() => hud.Model.Mode != FloorCardMode.Hidden && hud.Model.Card != null, Timeout, "the HUD component never picked up the floor");
            Assert.That(hud.Model.Card.Name, Is.EqualTo(name));
            Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Hud_ProgressFollowsValveMask()
        {
            Ctx c = null; yield return Setup(x => c = x);
            var m = new FloorCardHudModel();
            m.Tick(c.Contents, 0, 0f);
            Assert.That(m.ObjectiveLine, Is.EqualTo("Open both valves (0/2)"));
            Vector3 valve = c.Floor.Built.Root.TransformPoint(CellarFloorBuilder.CellCenter(c.Contents.Plan.ValveCellA.X, c.Contents.Plan.ValveCellA.Y));
            c.Hero = valve + new Vector3(0.6f, 0f, 0f);
            double until = Time.realtimeSinceStartupAsDouble + Timeout, next = 0;
            while ((c.Contents.State.ValveMask & 1) == 0)
            {
                if (Time.realtimeSinceStartupAsDouble > until) Assert.Fail("valve 0 never turned");
                if (Time.realtimeSinceStartupAsDouble >= next) { c.Contents.RequestValveHold(0); next = Time.realtimeSinceStartupAsDouble + FloorObjectiveTuning.ReportInterval; }
                PlayModeHarness.Pace(); yield return null;
            }
            m.Tick(c.Contents, 0, 1f);
            Assert.That(m.ObjectiveLine, Is.EqualTo("Open both valves (1/2)"), "the HUD line follows the valve bit");
            Assert.That(m.Lines(false), Does.Contain("Open both valves (1/2)"));
            Assert.That(m.ComputeCount, Is.EqualTo(1), "progress changes do not recompute the card");
        }
    }
}
