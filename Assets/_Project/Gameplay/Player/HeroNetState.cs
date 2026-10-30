// Authority: owner. The hero's NetworkBehaviour: it owns no replicated state of its own in M2 (the pose rides on the owner-authoritative
// NetworkTransform on the same object; the host never moves a player, docs/PHASE1.md 6.4). Its job is the owner / remote split, as in FishGame's
// PlayerNetState: the owner-only parts (CharacterController, HeroLocomotion, TargetLockController) are enabled here on the owning peer and never by
// the prefab; every other peer enables only the CapsuleCollider, on layer RemotePlayer, so local players bump remote bodies.
using SleepingCalamity.Core;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class HeroNetState : NetworkBehaviour
    {
        private CharacterController _characterController;
        private CapsuleCollider _remoteCollider;
        private HeroLocomotion _locomotion;
        private TargetLockController _lockController;

        public bool OwnerRigActive { get; private set; }
        public HeroLocomotion Locomotion => _locomotion;
        public TargetLockController LockController => _lockController;

        // The point the camera follows (1.5 m up, docs/PHASE1.md 6.5). Falls back to the root when the prefab has no pivot child.
        public Transform CameraPivot
        {
            get
            {
                Transform pivot = transform.Find("CameraPivot");
                return pivot != null ? pivot : transform;
            }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _remoteCollider = GetComponent<CapsuleCollider>();
            _locomotion = GetComponent<HeroLocomotion>();
            _lockController = GetComponent<TargetLockController>();
        }

        public override void OnNetworkSpawn()
        {
            gameObject.layer = IsOwner ? Layers.Player : Layers.RemotePlayer;
            SetLayerRecursive(transform, gameObject.layer);
            if (IsOwner) EnableOwnerRig();
            else if (_remoteCollider != null) _remoteCollider.enabled = true;
        }

        public override void OnNetworkDespawn()
        {
            if (OwnerRigActive) DisableOwnerRig();
            if (_remoteCollider != null) _remoteCollider.enabled = false;
        }

        private void EnableOwnerRig()
        {
            if (_characterController != null) _characterController.enabled = true;
            if (_locomotion != null) _locomotion.enabled = true;
            if (_lockController != null) _lockController.enabled = true;
            OwnerRigActive = true;
        }

        private void DisableOwnerRig()
        {
            if (_lockController != null) _lockController.enabled = false;
            if (_locomotion != null) _locomotion.enabled = false;
            if (_characterController != null) _characterController.enabled = false;
            OwnerRigActive = false;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayerRecursive(child, layer);
        }
    }
}
