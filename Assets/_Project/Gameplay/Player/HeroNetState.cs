// Authority: owner. The hero's NetworkBehaviour: its only replicated state is the owner-written NetStance flags for the animation (the pose rides on the owner-authoritative
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
        public const byte StanceLocked = 1, StanceDodging = 2, StanceGuarding = 4, StanceAiming = 8;   // M3: the dagger guard and the bow aim

        // Owner writes, everyone reads: whether the hero is hard-locked and whether it is dodging, as bit flags. Only the animation reads it (a
        // remote copy cannot tell a strafe from a walk, or a dodge from a run, by its transform alone). Nothing the host rules on.
        public readonly NetworkVariable<byte> NetStance = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private CharacterController _characterController;
        private CapsuleCollider _remoteCollider;
        private HeroLocomotion _locomotion;
        private TargetLockController _lockController;

        public bool OwnerRigActive { get; private set; }
        public bool StanceIsLocked => (NetStance.Value & StanceLocked) != 0;
        public bool StanceIsDodging => (NetStance.Value & StanceDodging) != 0;
        public bool StanceIsGuarding => (NetStance.Value & StanceGuarding) != 0;
        public bool StanceIsAiming => (NetStance.Value & StanceAiming) != 0;
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

        // Owner only; writes only on a change.
        public void OwnerSetStance(bool locked, bool dodging, bool guarding = false, bool aiming = false)
        {
            if (!IsSpawned || !IsOwner) return;
            byte flags = (byte)((locked ? StanceLocked : 0) | (dodging ? StanceDodging : 0) | (guarding ? StanceGuarding : 0) | (aiming ? StanceAiming : 0));
            if (NetStance.Value != flags) NetStance.Value = flags;
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
