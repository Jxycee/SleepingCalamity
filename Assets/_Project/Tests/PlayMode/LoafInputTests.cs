// Authority: test-only. PL-2 task 2.5: the UseItem button eats a loaf through the real input path (FakeInputService frame -> router -> DiveDirector.LocalEat -> HeroVitals.RequestEatLoaf -> host
// ServerEatLoaf). Solo in the real Hollowmere scene, and host + one client in one process. No dev key is pressed.
// Catches: a button that never reaches the eat request, one that eats at full HP or with no loaf, a client press the host does not apply.
// Setup that is not the action under test: loaves and damage are put on the hero by the host (ServerSetLoaves, ServerDamage).
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using SleepingCalamity.App;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace SleepingCalamity.Tests.PlayMode
{
    public class LoafInputTests
    {
        private const float Timeout = TownInputRig.Timeout;
        private readonly List<PlayModeHarness.TestClient> _clients = new List<PlayModeHarness.TestClient>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (PlayModeHarness.TestClient client in _clients) yield return client.LeaveAndDestroy();
            _clients.Clear();
            yield return PlayModeHarness.TearDownApp();
        }

        [UnityTest]
        public IEnumerator UseItem_EatsALoaf_HealsAndCountsDown()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            HeroVitals v = rig.Hero.GetComponent<HeroVitals>();
            v.ServerSetLoaves(3);
            Assert.That(v.ServerDamage(50), Is.EqualTo(50));
            int hurt = v.Hp;
            yield return rig.Press(i => i.Frame.UseItemPressed = true);
            yield return PlayModeHarness.WaitUntil(() => v.Loaves == 2, Timeout, "the UseItem press never ate a loaf");
            Assert.That(v.Hp, Is.EqualTo(hurt + HeroVitals.LoafHeal));
            Assert.That(rig.Input.DevPressed, Is.Empty, "no dev key");
        }

        [UnityTest]
        public IEnumerator UseItem_NoLoaves_DoesNothing()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            HeroVitals v = rig.Hero.GetComponent<HeroVitals>();
            v.ServerSetLoaves(0);
            v.ServerDamage(50);
            int hurt = v.Hp;
            yield return rig.Press(i => i.Frame.UseItemPressed = true);
            yield return TownInputRig.Seconds(0.5f);
            Assert.That(v.Hp, Is.EqualTo(hurt), "nothing to eat, nothing healed");
            Assert.That(v.Loaves, Is.EqualTo(0));
        }

        // RequestEatLoaf's rule today: the host refuses at full HP and keeps the loaf.
        [UnityTest]
        public IEnumerator UseItem_AtFullHealth_KeepsTheLoaf()
        {
            TownInputRig rig = null;
            yield return TownInputRig.Start(r => rig = r);
            HeroVitals v = rig.Hero.GetComponent<HeroVitals>();
            v.ServerSetLoaves(3);
            Assert.That(v.Hp, Is.EqualTo(v.MaxHp));
            yield return rig.Press(i => i.Frame.UseItemPressed = true);
            yield return TownInputRig.Seconds(0.5f);
            Assert.That(v.Loaves, Is.EqualTo(3), "a full-health hero does not waste a loaf");
        }

        private static NetworkObject DirectorPrefab(NetworkManager nm)
        {
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<DiveDirector>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            Assert.Fail("DiveDirector is not in the network prefab list (run the project builder)");
            return null;
        }

        // The client's own button press reaches its own hero's owner RPC and the host applies it; the host's hero (full HP) is untouched.
        [UnityTest]
        public IEnumerator Client_UseItem_HostApplies()
        {
            yield return PlayModeHarness.LoadBootstrapAndWaitForLobby();
            AppBootstrap app = AppBootstrap.Current;
            yield return PlayModeHarness.StartDirectHostWithoutScenes();
            NetworkManager hostNm = app.NetworkManager;
            var client = new PlayModeHarness.TestClient(app, PlayModeHarness.PlayerId(8));
            _clients.Add(client);
            var cts = new CancellationTokenSource();
            yield return PlayModeHarness.Await(client.JoinAsync(cts.Token), Timeout, cts);
            cts.Dispose();
            NetworkManager clientNm = client.NetworkManager;
            yield return PlayModeHarness.WaitUntil(() => hostNm.ConnectedClientsIds.Count == 2 && clientNm.IsConnectedClient, Timeout, "the client never connected");

            NetworkObject dirObj = hostNm.SpawnManager.InstantiateAndSpawn(DirectorPrefab(hostNm), hostNm.LocalClientId, false, false, false, Vector3.zero, Quaternion.identity);
            ulong dirId = dirObj.NetworkObjectId;
            yield return PlayModeHarness.WaitUntil(() => clientNm.SpawnManager.SpawnedObjects.ContainsKey(dirId), Timeout, "the client never received the director");

            Vector3 far = new Vector3(400f, 0f, 400f);
            NetworkObject hostHero = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, hostNm.LocalClientId, false, true, false, far, Quaternion.identity);
            NetworkObject clientHeroOnHost = hostNm.SpawnManager.InstantiateAndSpawn(app.PlayerPrefab, clientNm.LocalClientId, false, true, false, far + new Vector3(8f, 0f, 0f), Quaternion.identity);
            yield return PlayModeHarness.WaitUntil(() => clientNm.LocalClient != null && clientNm.LocalClient.PlayerObject != null, Timeout, "the client never received its hero");
            HeroVitals clientHero = clientNm.LocalClient.PlayerObject.GetComponent<HeroVitals>();
            HeroVitals onHost = clientHeroOnHost.GetComponent<HeroVitals>();
            HeroVitals hostVitals = hostHero.GetComponent<HeroVitals>();

            onHost.ServerSetLoaves(3);
            hostVitals.ServerSetLoaves(3);
            Assert.That(onHost.ServerDamage(50), Is.EqualTo(50));
            yield return PlayModeHarness.WaitUntil(() => clientHero.Loaves == 3 && clientHero.Hp == clientHero.MaxHp - 50, Timeout, "the client never saw its loaves and damage");

            // One process, one input service: the press is read by both peers' directors, each for its own local hero.
            var input = new FakeInputService();
            ServiceRoots.App.Replace<IInputService>(input);
            input.Frame.UseItemPressed = true;
            yield return null;
            input.Frame = default;
            yield return PlayModeHarness.WaitUntil(() => onHost.Loaves == 2 && onHost.Hp == onHost.MaxHp - 50 + HeroVitals.LoafHeal, Timeout, "the host never applied the client's UseItem");
            Assert.That(hostVitals.Loaves, Is.EqualTo(3), "the host's own full-health hero kept its loaf");
        }
    }
}
