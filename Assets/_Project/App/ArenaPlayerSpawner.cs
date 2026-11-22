// Authority: host only. Not itself a NetworkBehaviour: it listens to the host's NetworkSceneManager and spawns one player object per
// connected client once a gameplay scene is loaded (OnLoadEventCompleted for the clients that loaded with the host) and for a client that
// joins later (OnSynchronizeComplete). Attach / Detach follow NetworkManager.OnServerStarted / OnServerStopped (AppBootstrap).
// The player prefab is owned by its client (owner-authoritative NetworkTransform); movement itself arrives with M2.
// It is also the moment a player who joined a running session counts as one of the world's players: the spawn calls HostWorld.RecordHeroSpawned
// (user decision 2026-11-13, audit A2; before the start gate nobody is recorded).
// Both hooks fire once, so a host tick (Tick, driven by AppBootstrap.Update, host only) repairs a miss: a connected client without a hero in a loaded gameplay scene gets one (audit A4).
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
            _attachedTo.OnLoad += OnLoadStarted;
        }

        public void Detach()
        {
            _gameplayLoaded = false;
            _missingSince.Clear();
            if (_attachedTo == null) return;
            _attachedTo.OnLoadEventCompleted -= OnLoadEventCompleted;
            _attachedTo.OnSynchronizeComplete -= OnSynchronizeComplete;
            _attachedTo.OnLoad -= OnLoadStarted;
            _attachedTo = null;
        }

        // Audit A4 (closes N7): the two spawn hooks above are one-shots. A client that NGO lists in clientsTimedOut (a slow PC), or that synchronizes while the host is still
        // loading, never gets another one. This host tick (AppBootstrap.Update) is the repair: while a gameplay scene is loaded it spawns a hero for any connected client without a
        // player object (SpawnFor is idempotent). A client is given RepairGraceSeconds first, so the normal hooks (and a test that places the hero itself) win the race; it never
        // runs while a scene load is in flight (hooks clear the flag on OnLoad, set it on the gameplay load's completion). Returns the number of heroes it spawned.
        public const float RepairIntervalSeconds = 0.5f;
        public const float RepairGraceSeconds = 2f;
        private bool _gameplayLoaded;
        private float _nextRepairAt;
        private readonly Dictionary<ulong, float> _missingSince = new Dictionary<ulong, float>();
        private readonly List<ulong> _scratch = new List<ulong>();

        public int Tick(float now)
        {
            if (!_nm.IsServer || !_nm.IsListening || _playerPrefab == null) return 0;
            if (!_gameplayLoaded || !SceneNames.IsGameplay(SceneManager.GetActiveScene().name)) { _missingSince.Clear(); return 0; }
            if (now < _nextRepairAt) return 0;
            _nextRepairAt = now + RepairIntervalSeconds;

            int spawned = 0;
            _scratch.Clear();
            foreach (KeyValuePair<ulong, NetworkClient> pair in _nm.ConnectedClients)
            {
                if (pair.Value.PlayerObject != null) { _missingSince.Remove(pair.Key); continue; }
                if (!_missingSince.TryGetValue(pair.Key, out float since)) { _missingSince[pair.Key] = now; continue; }
                if (now - since >= RepairGraceSeconds) _scratch.Add(pair.Key);
            }
            foreach (ulong clientId in _scratch)
            {
                SpawnFor(clientId);
                if (_nm.ConnectedClients.TryGetValue(clientId, out NetworkClient c) && c.PlayerObject != null) { _missingSince.Remove(clientId); spawned++; }
            }
            if (_missingSince.Count > 0)
            {
                // Forget clients that left.
                List<ulong> gone = null;
                foreach (ulong id in _missingSince.Keys) if (!_nm.ConnectedClients.ContainsKey(id)) (gone ??= new List<ulong>()).Add(id);
                if (gone != null) foreach (ulong id in gone) _missingSince.Remove(id);
            }
            return spawned;
        }

        // A scene load has started (the host's own): no repair until the gameplay scene's load event completes.
        private void OnLoadStarted(ulong clientId, string sceneName, LoadSceneMode mode, AsyncOperation operation)
        {
            _gameplayLoaded = false;
            _missingSince.Clear();
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            _gameplayLoaded = SceneNames.IsGameplay(sceneName);
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
