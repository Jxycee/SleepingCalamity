// Authority: test-only. PL-2 task 2.2: the guide line and its two beacons. The Hollowmere cases run a solo host in the REAL scene (TownInputRig): the guide reads the replicated town and dive state,
// the beacons are the two marker objects HollowmereHub builds (at the trapdoor and at the build point), and placement mode hides all of it. The client case runs two NGO peers in one process and checks that
// a client's HUD, fed only by what replicates, shows the same step as the host's.
// Catches: a guide that points at the wrong place for the state, a beacon left on after the step moved, a guide drawn over placement mode, a client that cannot work out the step the host sees.
// NOT covered: the IMGUI drawing (layout, fonts), how the beacons look in the final light, a second machine.
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
    public class LoopGuideHudTests
    {
        private const float Timeout = TownInputRig.Timeout;
        private static readonly int[] Frame = { PieceCatalog.FrameId };
        private static readonly int[] Bakery = { PieceCatalog.FrameId, PieceCatalog.OvenId, PieceCatalog.DoorId, PieceCatalog.SignId };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PlayModeHarness.TearDownApp();
        }

        private static LoopGuideHud FindHud()
        {
            LoopGuideHud hud = Object.FindFirstObjectByType<LoopGuideHud>();
            Assert.That(hud, Is.Not.Null, "the guide HUD spawns itself with the game");
            return hud;
        }

        private static bool On(GameObject g) => g != null && g.activeSelf;

        [UnityTest]
        public IEnumerator Town_NoPieces_GuideAtStair_BeaconOn()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);   // an empty stock
            LoopGuideHud hud = FindHud();
            yield return PlayModeHarness.WaitUntil(() => hud.Model.Step.Kind == GuideKind.GoToStair && hud.Model.Visible, Timeout, "the guide never said to find the trapdoor (step " + hud.Model.Step.Kind + ")");
            Assert.That(hud.Model.Text, Is.EqualTo(GuideText.For(new GuideStep(GuideKind.GoToStair))), "the line is the guide table's words");
            Assert.That(hud.Model.Step.PointsAtStair, Is.True);
            yield return PlayModeHarness.WaitUntil(() => hud.StairBeacon != null && hud.BuildBeacon != null, Timeout, "Hollowmere has no guide beacons (run the project builder)");
            yield return PlayModeHarness.WaitUntil(() => On(hud.StairBeacon), Timeout, "the trapdoor beacon never lit");
            Assert.That(On(hud.BuildBeacon), Is.False, "the build point beacon stays dark");
            Assert.That(TownInputRig.Flat(hud.StairBeacon.transform.position, rig.Director.NetHubStair.Value), Is.LessThan(1.5f), "the beacon stands at the trapdoor");
            Assert.That(rig.Mode.Active, Is.False);
        }

        [UnityTest]
        public IEnumerator AfterPieceHome_GuideAtBuildPoint()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Frame);   // a frame waits in the stock, as a finished dive leaves it
            LoopGuideHud hud = FindHud();
            yield return PlayModeHarness.WaitUntil(() => hud.Model.Step.Kind == GuideKind.GoToBuildPoint, Timeout, "the guide never pointed at the build point (step " + hud.Model.Step.Kind + ")");
            Assert.That(hud.Model.Step.PointsAtStair, Is.False);
            yield return PlayModeHarness.WaitUntil(() => On(hud.BuildBeacon), Timeout, "the build point beacon never lit");
            Assert.That(On(hud.StairBeacon), Is.False, "the trapdoor beacon went dark");
            Assert.That(TownInputRig.Flat(hud.BuildBeacon.transform.position, rig.Mode.BuildPoint.position), Is.LessThan(1.5f), "the beacon stands at the build point");
        }

        [UnityTest]
        public IEnumerator InPlacement_GuideHidden()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r, Bakery);
            LoopGuideHud hud = FindHud();
            yield return PlayModeHarness.WaitUntil(() => hud.Model.Visible && On(hud.BuildBeacon), Timeout, "the guide never showed before placement");
            yield return rig.EnterPlacement();
            yield return PlayModeHarness.WaitUntil(() => !hud.Model.Visible, Timeout, "the guide stayed up in placement mode");
            Assert.That(On(hud.BuildBeacon), Is.False, "and so did its beacon");
            Assert.That(On(hud.StairBeacon), Is.False);
            yield return rig.PressPause();
            yield return PlayModeHarness.WaitUntil(() => !rig.Mode.Active, Timeout, "Pause never left placement");
            yield return PlayModeHarness.WaitUntil(() => hud.Model.Visible, Timeout, "the guide never came back after placement");
        }

        [UnityTest]
        public IEnumerator Client_ShowsTheSameStepAsHost()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            yield return PlayModeHarness.StartClients(app, 1, false, null, 20);
            NetworkObject dirObj = null;
            foreach (NetworkPrefabsList list in hostNm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (dirObj == null && entry.Prefab != null && entry.Prefab.GetComponent<DiveDirector>() != null)
                        dirObj = hostNm.SpawnManager.InstantiateAndSpawn(entry.Prefab.GetComponent<NetworkObject>(), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            Assert.That(dirObj, Is.Not.Null, "DiveDirector is in the network prefab list");
            DiveDirector host = dirObj.GetComponent<DiveDirector>();
            Assert.That(host.ServerConfigure(new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 9f), new Vector3(200f, 0f, 0f)), Is.True);
            host.SeedOverride = 5150u;
            yield return PlayModeHarness.WaitUntil(() => host.TownSync != null && host.TownSync.IsSpawned, Timeout, "no TownSync");

            // The client's director is the copy NGO spawned in its own manager; its HUD reads only what replicated there.
            NetworkManager clientNm = null;
            foreach (NetworkManager nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)) if (nm != hostNm) clientNm = nm;
            Assert.That(clientNm, Is.Not.Null);
            ulong dirId = host.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => clientNm.SpawnManager.SpawnedObjects.ContainsKey(dirId), Timeout, "the client never received the director");
            DiveDirector peerDir = clientNm.SpawnManager.SpawnedObjects[dirId].GetComponent<DiveDirector>();
            yield return PlayModeHarness.WaitUntil(() => GuideSources.TownOf(peerDir) != null, Timeout, "the client never received the town object");

            var hostHud = new GameObject("TestGuideHost").AddComponent<LoopGuideHud>(); hostHud.Director = host;
            var peerHud = new GameObject("TestGuideClient").AddComponent<LoopGuideHud>(); peerHud.Director = peerDir;

            yield return Agree(hostHud, peerHud, GuideKind.GoToStair, "a new world");
            Assert.That(host.Town.State.AddPiece(PieceCatalog.FrameId), Is.EqualTo(PlaceResult.Ok));
            host.TownSync.ServerRefresh();
            yield return Agree(hostHud, peerHud, GuideKind.GoToBuildPoint, "a frame came home");

            Assert.That(host.ServerStartDive(), Is.True);
            yield return PlayModeHarness.WaitUntil(() => peerDir.NetActive.Value, Timeout, "the client never saw the dive");
            yield return PlayModeHarness.WaitUntil(() => hostHud.Model.Step.PointsAtStair == null && peerHud.Model.Step.PointsAtStair == null, Timeout, "a dive points at no beacon on either peer");
            Assert.That(peerHud.Model.Step.Kind, Is.EqualTo(hostHud.Model.Step.Kind), "in a dive");
            Assert.That(peerHud.Model.Text, Is.EqualTo(hostHud.Model.Text));
        }

        private static IEnumerator Agree(LoopGuideHud host, LoopGuideHud peer, GuideKind expected, string when)
        {
            yield return PlayModeHarness.WaitUntil(() => host.Model.Step.Kind == expected && peer.Model.Step.Kind == expected, Timeout,
                when + ": host says " + host.Model.Step.Kind + ", client says " + peer.Model.Step.Kind + ", expected " + expected);
            Assert.That(peer.Model.Text, Is.EqualTo(host.Model.Text), when);
        }
    }
}
