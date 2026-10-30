// Authority: owner (local). Creates the camera rig for the hero on the owning peer, and only for the first local owner in the process. Remote
// copies of a hero get no rig. The rig is a plain (non-networked) prefab instance that lives and dies with the owner's hero.
using SleepingCalamity.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class HeroCameraRigSpawner : NetworkBehaviour
    {
        [SerializeField] private HeroCameraRig rigPrefab;

        private HeroCameraRig _rig;

        public HeroCameraRig Rig => _rig;

        public void Configure(HeroCameraRig prefab)
        {
            rigPrefab = prefab;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner || rigPrefab == null || HeroCameraRig.Local != null) return;
            HeroNetState hero = GetComponent<HeroNetState>();
            if (hero == null) return;
            _rig = Instantiate(rigPrefab);
            _rig.name = "HeroCameraRig (" + name + ")";
            _rig.Bind(hero);
        }

        public override void OnNetworkDespawn()
        {
            if (_rig != null) Destroy(_rig.gameObject);
            _rig = null;
        }
    }
}
