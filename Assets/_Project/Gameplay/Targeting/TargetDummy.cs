// Authority: host. A target dummy (M2 test target, no life yet): spawned by the host (TargetDummySpawner), which writes NetElite once, before Spawn.
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

        // Host only, called BEFORE Spawn: the value is part of the spawn message, so no client ever sees a plain elite for a tick.
        public void ServerSeedElite(bool elite)
        {
            NetElite.Value = elite;
            if (_targetable == null) _targetable = GetComponent<Targetable>();
            if (_targetable != null) _targetable.IsElite = elite;
        }

        private void OnEliteChanged(bool previous, bool current)
        {
            if (_targetable != null) _targetable.IsElite = current;
        }
    }
}
