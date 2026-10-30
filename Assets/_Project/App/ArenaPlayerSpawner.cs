// Authority: host only. Not itself a NetworkBehaviour: it listens to the host's NetworkSceneManager and spawns one player object per
// connected client once a gameplay scene is loaded (OnLoadEventCompleted for the clients that loaded with the host) and for a client that
// joins later (OnSynchronizeComplete). Attach / Detach follow NetworkManager.OnServerStarted / OnServerStopped (AppBootstrap).
// The player prefab is owned by its client (owner-authoritative NetworkTransform); movement itself arrives with M2.
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.App
{
    public sealed class ArenaPlayerSpawner
    {
        private readonly NetworkManager _nm;
        private readonly NetworkObject _playerPrefab;
        private readonly HostRoster _roster;
        private NetworkSceneManager _attachedTo;

        public ArenaPlayerSpawner(NetworkManager nm, NetworkObject playerPrefab, HostRoster roster)
        {
            _nm = nm ?? throw new ArgumentNullException(nameof(nm));
            _playerPrefab = playerPrefab;
            _roster = roster;
        }

        public void Attach()
        {
            Detach();
            if (!_nm.IsServer || _nm.SceneManager == null) return;
            _attachedTo = _nm.SceneManager;
            _attachedTo.OnLoadEventCompleted += OnLoadEventCompleted;
            _attachedTo.OnSynchronizeComplete += OnSynchronizeComplete;
        }

        public void Detach()
        {
            if (_attachedTo == null) return;
            _attachedTo.OnLoadEventCompleted -= OnLoadEventCompleted;
            _attachedTo.OnSynchronizeComplete -= OnSynchronizeComplete;
            _attachedTo = null;
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            if (!SceneNames.IsGameplay(sceneName)) return;
            foreach (ulong clientId in clientsCompleted) SpawnFor(clientId);
            SpawnTargetDummies();
        }

        // A client that joins while a gameplay scene is up: NGO has synchronized the scene to it, now it gets its player.
        private void OnSynchronizeComplete(ulong clientId)
        {
            if (SceneNames.IsGameplay(SceneManager.GetActiveScene().name)) SpawnFor(clientId);
        }

        // M2: the arena's target dummies are spawned by the host at the same moment as the players (idempotent).
        public int SpawnTargetDummies()
        {
            if (!_nm.IsServer) return 0;
            TargetDummySpawner dummies = UnityEngine.Object.FindFirstObjectByType<TargetDummySpawner>();
            return dummies != null ? dummies.SpawnAll(_nm) : 0;
        }

        // Idempotent: a client that already has a player object is left alone.
        public void SpawnFor(ulong clientId)
        {
            if (!_nm.IsServer || _playerPrefab == null) return;
            if (_nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client) && client.PlayerObject != null) return;

            Vector3 position = Vector3.zero;
            SpawnPoint point = UnityEngine.Object.FindFirstObjectByType<SpawnPoint>();
            if (point != null) position = point.PositionForSlot(SlotOf(clientId));
            else Debug.LogWarning("ArenaPlayerSpawner: no SpawnPoint in the scene; spawning at the origin.");

            NetworkObject player = _nm.SpawnManager.InstantiateAndSpawn(_playerPrefab, clientId, destroyWithScene: true, isPlayerObject: true,
                position: position, rotation: Quaternion.identity);
            if (player == null) Debug.LogError("ArenaPlayerSpawner: spawning the player for client " + clientId + " failed.");
        }

        // The roster slot of the client (0..3, kept for a rejoin), so a player always spawns at the same place around the point. Without a
        // roster entry (a test that spawns by hand) the join order stands in.
        private int SlotOf(ulong clientId)
        {
            if (_roster != null && _roster.TryGetSlot(clientId, out byte rosterSlot)) return rosterSlot;
            int slot = 0;
            foreach (ulong id in _nm.ConnectedClientsIds)
            {
                if (id == clientId) return slot;
                slot++;
            }
            return slot;
        }
    }
}
