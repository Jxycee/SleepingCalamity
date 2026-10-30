// Authority: host. A target dummy (M2 test target, no life yet): spawned by the host (TargetDummySpawner), which also writes NetElite once.
// Every peer reads NetElite and mirrors it into the local Targetable. Locking onto a dummy is owner-local and never touches this object.
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    [RequireComponent(typeof(Targetable))]
    public sealed class TargetDummy : NetworkBehaviour
    {
        public readonly NetworkVariable<bool> NetElite = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Targetable _targetable;

        private void Awake()
        {
            _targetable = GetComponent<Targetable>();
        }

        public override void OnNetworkSpawn()
        {
            if (_targetable == null) _targetable = GetComponent<Targetable>();
            _targetable.IsElite = NetElite.Value;
            NetElite.OnValueChanged += OnEliteChanged;
        }

        public override void OnNetworkDespawn()
        {
            NetElite.OnValueChanged -= OnEliteChanged;
        }

        // Host only.
        public void ServerSetElite(bool elite)
        {
            if (!IsServer) return;
            NetElite.Value = elite;
            if (_targetable != null) _targetable.IsElite = elite;
        }

        private void OnEliteChanged(bool previous, bool current)
        {
            if (_targetable != null) _targetable.IsElite = current;
        }
    }
}
