// Authority: host (the component exists in the dive scene on every peer but only the host acts). The Arena_Dive scene's marker: it holds the hub's stair (the Undercroft stair stand-in) and the
// far-away origin where the dive floors are built, and once the host's session is listening it spawns the one DiveDirector of the session (a dynamic network prefab from the owned list) and
// configures it with the spawn point, the stair and the origin. Clients get the director with the scene sync / spawn like any network object.
using SleepingCalamity.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class DiveHub : MonoBehaviour
    {
        public Transform Entrance;   // where the party stands to go down
        public Transform Origin;     // where the dive floors are built (far from the hub)
        private DiveDirector _director;
        private float _next;

        public DiveDirector Director => _director;

        public void Configure(Transform entrance, Transform origin) { Entrance = entrance; Origin = origin; }

        private void Update()
        {
            if (_director != null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !nm.IsListening || nm.SpawnManager == null) return;
            DiveDirector existing = FindFirstObjectByType<DiveDirector>();
            if (existing != null && existing.NetworkManager == nm) { _director = existing; return; }
            NetworkObject prefab = null;
            foreach (NetworkPrefabsList list in nm.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (prefab == null && entry.Prefab != null && entry.Prefab.GetComponent<DiveDirector>() != null) prefab = entry.Prefab.GetComponent<NetworkObject>();
            if (prefab == null) { Debug.LogError("DiveHub: DiveDirector is not in the network prefab list (run the project builder)."); _next = float.MaxValue; return; }
            NetworkObject spawned = nm.SpawnManager.InstantiateAndSpawn(prefab, nm.LocalClientId, true, false, false, Vector3.zero, Quaternion.identity);
            _director = spawned.GetComponent<DiveDirector>();
            SpawnPoint spawn = FindFirstObjectByType<SpawnPoint>();
            _director.ServerConfigure(spawn != null ? spawn.transform.position : Vector3.zero,
                Entrance != null ? Entrance.position : Vector3.zero, Origin != null ? Origin.position : new Vector3(200f, 0f, 0f));
        }
    }
}
