// Authority: host. Not a NetworkBehaviour (no in-scene NetworkObject): the arena holds this plain component with TargetDummyMarker children,
// and the host's ArenaPlayerSpawner calls SpawnAll once the arena's load event has completed, the same hook that spawns the players.
// Clients get the dummies as ordinary spawns. Idempotent, and a client-side call does nothing.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class TargetDummySpawner : MonoBehaviour
    {
        [SerializeField] private NetworkObject dummyPrefab;

        private readonly List<NetworkObject> _spawned = new List<NetworkObject>();

        public NetworkObject DummyPrefab => dummyPrefab;
        public IReadOnlyList<NetworkObject> Spawned => _spawned;

        public void Configure(NetworkObject prefab)
        {
            dummyPrefab = prefab;
        }

        // Returns how many dummies this call spawned.
        public int SpawnAll(NetworkManager nm)
        {
            if (nm == null || !nm.IsServer || dummyPrefab == null) return 0;
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] == null) _spawned.RemoveAt(i);
            if (_spawned.Count > 0) return 0;

            int count = 0;
            foreach (TargetDummyMarker marker in GetComponentsInChildren<TargetDummyMarker>())
            {
                NetworkObject instance = Instantiate(dummyPrefab, marker.transform.position, marker.transform.rotation);
                instance.name = marker.name;
                instance.Spawn(destroyWithScene: true);
                TargetDummy dummy = instance.GetComponent<TargetDummy>();
                if (dummy != null) dummy.ServerSetElite(marker.Elite);
                _spawned.Add(instance);
                count++;
            }
            return count;
        }
    }
}
