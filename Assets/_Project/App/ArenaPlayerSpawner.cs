// Authority: host only. Not itself a NetworkBehaviour: it listens to the host's NetworkSceneManager and spawns one player object per
// connected client once a gameplay scene is loaded (OnLoadEventCompleted for the clients that loaded with the host) and for a client that
// joins later (OnSynchronizeComplete). Attach / Detach follow NetworkManager.OnServerStarted / OnServerStopped (AppBootstrap).
// The player prefab is owned by its client (owner-authoritative NetworkTransform); movement itself arrives with M2.
// It is also the moment a player who joined a running session counts as one of the world's players: the spawn calls HostWorld.RecordHeroSpawned
// (user decision 2026-11-13, audit A2; before the start gate nobody is recorded).
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
        private readonly HostWorld _world;
        private NetworkSceneManager _attachedTo;

        public ArenaPlayerSpawner(NetworkManager nm, NetworkObject playerPrefab, HostRoster roster, HostWorld world = null)
        {
            _nm = nm ?? throw new ArgumentNullException(nameof(nm));
            _playerPrefab = playerPrefab;
            _roster = roster;
            _world = world;
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
            // Identity first: a hero's spawn-time growth sync must key by the recorded player, not leave a client-id record behind.
            CombatHost combat = UnityEngine.Object.FindFirstObjectByType<CombatHost>();
            if (combat != null && _nm.IsServer) combat.Identity = _roster;
            foreach (ulong clientId in clientsCompleted) SpawnFor(clientId);
            SpawnTargetDummies();
            BeginCombat();
        }

        // M3: the combat arena's host-side rules and wave director start once the players exist (idempotent; nothing in the greybox arena).
        public bool BeginCombat()
        {
            if (!_nm.IsServer) return false;
            CombatHost combat = UnityEngine.Object.FindFirstObjectByType<CombatHost>();
            if (combat != null) combat.Identity = _roster;   // growth follows the recorded player, not the client id
            return combat != null && combat.ServerBegin(_nm);
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
            if (player == null)
            {
                Debug.LogError("ArenaPlayerSpawner: spawning the player for client " + clientId + " failed.");
                return;
            }

            // A hero in a session that is already running: this player is playing the world, so the world records them now (user decision
            // 2026-11-13, audit A2). At a session start the gate has recorded the group already and this is a no-op.
            if (_world != null && _roster != null && _roster.TryGetKey(clientId, out PlayerKey key)) _world.RecordHeroSpawned(key, clientId);
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
