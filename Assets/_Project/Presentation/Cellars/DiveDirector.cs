// Authority: host. The dive spine (docs/PHASE1.md 7.2 floor loop, checkpoint and banking). One DiveDirector per session, spawned by the host (DiveHub in the dive scene, or a test).
// It owns the dive FLOW; the data stays where it lives: the floor (seed, floor) in CellarFloorSync, the haul/DiveState/stir in DiveHaulSync (both spawned here once per session
// and kept: ending a dive sets the floor to NoFloor, it never despawns them, so banked pieces, the checkpoint and the cleared floors survive a return dive).
// Host-written NetworkVariables (late joiners get them with the spawn): NetActive, NetHeartBanked, NetCheckpoint, and the three world points NetHome / NetHubStair / NetOrigin.
// Clients ask by RPC and the host re-checks everything: RequestDescendRpc (hub stair; range), RequestStairRpc(fromFloor) (current floor must equal fromFloor, so two presses in one
// tick advance once; range; StairOpen hook), RequestGoHomeRpc (floor 4, banked, range). Banking itself stays DiveHaulSync.RequestBank.
// Players move themselves (owner-authoritative NetworkTransform), so the host never writes a player transform: it sends TeleportRpc to the OWNER (SendTo.SpecifiedInParams), carrying the
// target plus the (seed, floor) it needs. The owner parks it as pending and applies it only once ITS OWN floor sync has built that (seed, floor) (an RPC can outrun the NetworkVariable that
// triggers the build), then disables the CharacterController, sets the pose and calls NetworkTransform.Teleport (remote copies do not interpolate across the map). A host-side placement pass
// (every 0.2 s) sends a teleport to every connected player that has not been placed for the current floor generation, which covers the first placement, floor changes, late joiners, a
// player that fell out of the world, and the trip home.
// Contents hooks (host): StairOpen (Func<int,bool>, null = open; the gate key gates floors 1-3 there), events DiveStarted, FloorChanged(floor), DiveEnded(reason); DiveHaulSync.Banked/DiveFailed stay.
// Debug (dev build, host): F7 starts a dive from anywhere, F8 abandons it (fails the dive like the stir countdown does).
// Host disconnect: NGO shuts the session down and everything despawns (no host migration in Phase 1).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public enum DiveEndReason : byte { Failed = 0, WentHome = 1, Abandoned = 2 }

    public sealed class DiveDirector : NetworkBehaviour
    {
        public const float PromptRadius = 3.5f;       // a client shows the prompt inside this; the host accepts DiveHaulSync.BankRadius
        public const float PlacementInterval = 0.2f;
        public const float FloorClearDelay = 0.75f;   // after the party was sent home, the floor stays this long so nobody drops through it first
        public const float PendingTimeout = 10f;      // an owner applies a teleport after this long even when its floor never built

        public readonly NetworkVariable<bool> NetActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> NetHeartBanked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> NetCheckpoint = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetHome = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetHubStair = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetOrigin = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---- host-only configuration and hooks ----
        public System.Func<int, bool> StairOpen;                    // contents hook: false keeps the stair on `floor` shut (null = open)
        public int[] PiecesPerFloor = DiveHaulSync.DefaultPiecesPerFloor;
        public StirTuning DiveStir = StirTuning.Default;            // tests shorten the countdown
        public uint? SeedOverride;                                  // tests and the debug start may pin the dive seed
        public event System.Action DiveStarted;
        public event System.Action<int> FloorChanged;
        public event System.Action<DiveEndReason> DiveEnded;
        public int RequestsHandled { get; private set; }            // host: RPCs received, accepted or refused (a test waits for refusals to land)

        public CellarFloorSync FloorSync { get; private set; }      // host: the spawned syncs; clients resolve their own through ResolveSyncs
        public DiveHaulSync HaulSync { get; private set; }
        public uint Seed => _seed;

        private uint _seed;
        private bool _haulBegun, _heartBanked, _needsPlacement;
        private int _gen;
        private readonly Dictionary<ulong, int> _placed = new Dictionary<ulong, int>();
        private float _nextPlace, _clearAt = -1f;

        // owner side: a teleport waiting for the local floor
        private bool _hasPending; private Vector3 _pendPos; private float _pendYaw; private uint _pendSeed; private int _pendFloor; private float _pendSince;
        private float _nextLookup;
        private string _notice; private float _noticeUntil;

        // =================================================================================================================================
        // Host: configuration, start, flow
        // =================================================================================================================================

        // Host only: where the party lives between dives (the hub's spawn point), the hub's stair, and where the dive floors are built (far from the hub).
        public bool ServerConfigure(Vector3 home, Vector3 hubStair, Vector3 origin)
        {
            if (!IsServer || !IsSpawned) return false;
            NetHome.Value = home; NetHubStair.Value = hubStair; NetOrigin.Value = origin;
            return true;
        }

        // Host only: starts a dive on the route's start floor (3 after a reached checkpoint, else 1), or on `forceFloor`. False when one runs, or off the host.
        public bool ServerStartDive(int forceFloor = 0)
        {
            if (!IsServer || !IsSpawned || NetActive.Value) return false;
            if (!EnsureSyncs()) return false;
            if (!_haulBegun)
            {
                if (!HaulSync.ServerBegin(PiecesPerFloor, DiveStir)) return false;
                _haulBegun = true;
                _seed = SeedOverride ?? PickSeed();
            }
            else if (SeedOverride.HasValue) _seed = SeedOverride.Value;
            int floor = forceFloor > 0 ? forceFloor : DiveRoute.StartFloor(HaulSync.Current.CheckpointReached);
            if (floor < DiveRoute.FirstFloor || floor > DiveRoute.LastFloor) return false;
            _clearAt = -1f;
            _heartBanked = false; NetHeartBanked.Value = false;
            HaulSync.ServerSetFloor(floor);
            FloorSync.ServerSetFloor(_seed, floor);
            NetActive.Value = true;
            _gen++; _needsPlacement = true;
            DiveStarted?.Invoke();
            FloorChanged?.Invoke(floor);
            return true;
        }

        private static uint PickSeed()
        {
            var rng = new System.Random(unchecked(System.Environment.TickCount * 7919 + (int)(Time.realtimeSinceStartupAsDouble * 1000.0)));
            return (uint)rng.Next() ^ ((uint)rng.Next(2) << 31);
        }

        // Host: validated requests (the RPCs call these; tests may too). clientId = the asker.
        public bool ServerTryDescend(ulong clientId)
        {
            if (!IsServer || !IsSpawned || NetActive.Value) return false;
            if (!NearPlayer(clientId, NetHubStair.Value)) return false;
            return ServerStartDive();
        }

        public bool ServerTryStair(ulong clientId, int fromFloor)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null) return false;
            int floor = HaulSync.Current.CurrentFloor;
            if (fromFloor != floor || !DiveRoute.HasStair(floor) || FloorSync.BuiltFloor != floor) return false;
            if (!StairIsOpen(floor)) { NoticeTo(clientId, "The way down is shut."); return false; }
            if (CellarFloorBuilder.TryStairWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && !NearPlayer(clientId, stair)) return false;
            int next = DiveRoute.NextFloor(floor);
            HaulSync.ServerMarkCleared(floor);
            HaulSync.ServerSetFloor(next);
            FloorSync.ServerSetFloor(_seed, next);
            _heartBanked = false; NetHeartBanked.Value = false;
            _gen++; _needsPlacement = true;
            FloorChanged?.Invoke(next);
            return true;
        }

        public bool ServerTryGoHome(ulong clientId)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null) return false;
            int floor = HaulSync.Current.CurrentFloor;
            if (!DiveRoute.CanGoHome(floor, _heartBanked) || FloorSync.BuiltFloor != floor) return false;
            if (CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && !NearPlayer(clientId, bank)) return false;
            return EndDive(DiveEndReason.WentHome);
        }

        // Host: the debug "abandon dive" (F8): the dive fails exactly like the stir countdown running out (unbanked pieces go back to their floors), the party goes home.
        public bool ServerAbandonDive()
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null) return false;
            HaulSync.ServerFailDive();
            return EndDive(DiveEndReason.Abandoned);
        }

        private bool StairIsOpen(int floor) => StairOpen == null || StairOpen(floor);

        // Ends the dive: the party is sent home at once, the floor is cleared a moment later. Failed/abandoned: the haul already failed the dive (floor 0, stir reset).
        private bool EndDive(DiveEndReason reason)
        {
            if (!NetActive.Value) return false;
            if (reason == DiveEndReason.WentHome)
            {
                HaulSync.ServerSetFloor(DiveState.Town);
                HaulSync.ServerBankInTown();   // the stir reset of a finished dive (the heart bank already banked the haul)
            }
            NetActive.Value = false; _heartBanked = false; NetHeartBanked.Value = false;
            _gen++; _needsPlacement = true;
            _clearAt = Time.unscaledTime + FloorClearDelay;
            DiveEnded?.Invoke(reason);
            return true;
        }

        private void OnBanked()
        {
            if (!IsServer || HaulSync == null) return;
            int floor = HaulSync.Current.CurrentFloor;
            if (floor == DiveState.CheckpointFloor && !NetCheckpoint.Value) NetCheckpoint.Value = true;
            if (floor == DiveState.HeartFloor) { _heartBanked = true; NetHeartBanked.Value = true; }
        }

        private void OnDiveFailed() { if (NetActive.Value) EndDive(DiveEndReason.Failed); }

        private bool EnsureSyncs()
        {
            if (FloorSync != null && HaulSync != null) return true;
            NetworkObject floorPrefab = FindPrefab<CellarFloorSync>(), haulPrefab = FindPrefab<DiveHaulSync>();
            if (floorPrefab == null || haulPrefab == null) { Debug.LogError("DiveDirector: CellarFloorSync / DiveHaulSync are not in the network prefab list (run the project builder)."); return false; }
            Vector3 at = NetOrigin.Value;
            if (FloorSync == null)
                FloorSync = NetworkManager.SpawnManager.InstantiateAndSpawn(floorPrefab, NetworkManager.LocalClientId, true, false, false, at, Quaternion.identity).GetComponent<CellarFloorSync>();
            if (HaulSync == null)
            {
                HaulSync = NetworkManager.SpawnManager.InstantiateAndSpawn(haulPrefab, NetworkManager.LocalClientId, true, false, false, Vector3.zero, Quaternion.identity).GetComponent<DiveHaulSync>();
                HaulSync.FloorSync = FloorSync;
                HaulSync.Banked += OnBanked;
                HaulSync.DiveFailed += OnDiveFailed;
            }
            return true;
        }

        private NetworkObject FindPrefab<T>() where T : Component
        {
            foreach (NetworkPrefabsList list in NetworkManager.NetworkConfig.Prefabs.NetworkPrefabsLists)
                foreach (NetworkPrefab entry in list.PrefabList)
                    if (entry.Prefab != null && entry.Prefab.GetComponent<T>() != null) return entry.Prefab.GetComponent<NetworkObject>();
            return null;
        }

        // The asker's player must be within DiveHaulSync.BankRadius (flat) of `point`. A client with no player object passes (matches ServerTryBankFor; no anti-cheat by design).
        private bool NearPlayer(ulong clientId, Vector3 point)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient c) || c.PlayerObject == null) return true;
            return DiveHaulSync.InRange(c.PlayerObject.transform.position, point);
        }

        // =================================================================================================================================
        // Host: placement pass
        // =================================================================================================================================

        private void PlacementPass()
        {
            if (!_needsPlacement) return;
            bool pending = false;
            int slot = -1;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                slot++;
                if (!NetworkManager.ConnectedClients.TryGetValue(id, out NetworkClient c) || c.PlayerObject == null) continue;
                if (_placed.TryGetValue(id, out int g) && g == _gen) continue;
                if (NetActive.Value)
                {
                    if (FloorSync == null || FloorSync.Layout == null || FloorSync.Built == null || FloorSync.BuiltFloor != HaulSync.Current.CurrentFloor) { pending = true; continue; }
                    Vector3 pos = CellarFloorBuilder.EntryWorld(FloorSync.Layout, FloorSync.Built, slot);
                    _placed[id] = _gen;
                    TeleportRpc(pos, 0f, FloorSync.BuiltSeed, FloorSync.BuiltFloor, RpcTarget.Single(id, RpcTargetUse.Temp));
                }
                else
                {
                    _placed[id] = _gen;
                    TeleportRpc(HomeFor(slot), 0f, 0u, 0, RpcTarget.Single(id, RpcTargetUse.Temp));
                }
            }
            if (!NetActive.Value && !pending) _needsPlacement = false;   // home: everyone present was sent; later joiners spawn on the hub's spawn point anyway
        }

        // The hub's spawn-point circle (SpawnPoint.PositionForSlot) around NetHome.
        private Vector3 HomeFor(int slot)
        {
            if (slot <= 0) return NetHome.Value;
            return NetHome.Value + Quaternion.Euler(0f, (slot - 1) * 120f, 0f) * (Vector3.forward * SpawnPoint.SlotRadius);
        }

        // Host: a player that dropped out of the world during a dive is placed again.
        private void FallNet()
        {
            if (!NetActive.Value) return;
            float limit = NetOrigin.Value.y - 15f;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (NetworkManager.ConnectedClients.TryGetValue(id, out NetworkClient c) && c.PlayerObject != null && c.PlayerObject.transform.position.y < limit && _placed.ContainsKey(id))
                { _placed.Remove(id); _needsPlacement = true; }
        }

        // =================================================================================================================================
        // Teleport (owner side) and notices
        // =================================================================================================================================

        [Rpc(SendTo.SpecifiedInParams)]
        private void TeleportRpc(Vector3 pos, float yawDeg, uint seed, int floor, RpcParams rpcParams = default)
        {
            _hasPending = true; _pendPos = pos; _pendYaw = yawDeg; _pendSeed = seed; _pendFloor = floor; _pendSince = Time.unscaledTime;
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void NoticeRpc(string text, RpcParams rpcParams = default) { _notice = text; _noticeUntil = Time.unscaledTime + 3f; }

        private void NoticeTo(ulong clientId, string text) { NoticeRpc(text, RpcTarget.Single(clientId, RpcTargetUse.Temp)); }

        /// <summary>Owner side: whether a teleport is still waiting for the local floor (tests).</summary>
        public bool HasPendingTeleport => _hasPending;

        private void ApplyPending()
        {
            if (!_hasPending) return;
            NetworkObject player = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if (player == null) return;
            if (_pendFloor > 0 && Time.unscaledTime - _pendSince < PendingTimeout)
            {
                ResolveSyncs();
                if (FloorSync == null || FloorSync.Built == null || FloorSync.BuiltFloor != _pendFloor || FloorSync.BuiltSeed != _pendSeed) return;
            }
            _hasPending = false;
            var cc = player.GetComponent<CharacterController>();
            bool ccOn = cc != null && cc.enabled;
            if (ccOn) cc.enabled = false;
            Quaternion rot = Quaternion.Euler(0f, _pendYaw, 0f);
            player.transform.SetPositionAndRotation(_pendPos, rot);
            var nt = player.GetComponent<NetworkTransform>();
            if (nt != null && nt.CanCommitToTransform) nt.Teleport(_pendPos, rot, player.transform.localScale);
            if (ccOn) cc.enabled = true;
            var loco = player.GetComponent<HeroLocomotion>();
            if (loco != null && loco.enabled) loco.SetView(_pendYaw, HeroLocomotion.FreePitchDownDeg);
        }

        // On a client the syncs are found in the scene by this peer's NetworkManager (one process can hold several peers' copies).
        private void ResolveSyncs()
        {
            if (IsServer) return;
            if ((FloorSync != null && HaulSync != null) || Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + 0.5f;
            if (FloorSync == null) foreach (CellarFloorSync s in FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (s.IsSpawned && s.NetworkManager == NetworkManager) { FloorSync = s; break; }
            if (HaulSync == null) foreach (DiveHaulSync s in FindObjectsByType<DiveHaulSync>(FindObjectsSortMode.None)) if (s.IsSpawned && s.NetworkManager == NetworkManager) { HaulSync = s; HaulSync.FloorSync = FloorSync; break; }
        }

        // =================================================================================================================================
        // Every peer: markers, interaction prompt, request RPCs
        // =================================================================================================================================

        public enum PromptKind : byte { None = 0, Descend = 1, Stair = 2, Bank = 3, GoHome = 4 }
        public PromptKind Prompt { get; private set; }
        public string PromptText { get; private set; } = "";

        public void RequestDescend() { RequestDescendRpc(); }
        public void RequestStair(int fromFloor) { RequestStairRpc(fromFloor); }
        public void RequestGoHome() { RequestGoHomeRpc(); }

        [Rpc(SendTo.Server)]
        private void RequestDescendRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryDescend(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void RequestStairRpc(int fromFloor, RpcParams rpcParams = default) { RequestsHandled++; ServerTryStair(rpcParams.Receive.SenderClientId, fromFloor); }

        [Rpc(SendTo.Server)]
        private void RequestGoHomeRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryGoHome(rpcParams.Receive.SenderClientId); }

        private static bool Flat(Vector3 a, Vector3 b, float r) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz <= r * r; }

        // Works out what the local player can use right now, and presses it on the interact key.
        private void LocalInteract()
        {
            Prompt = PromptKind.None; PromptText = "";
            NetworkObject player = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if (player == null || _hasPending) return;
            Vector3 me = player.transform.position;
            int floor = 0;
            if (!NetActive.Value)
            {
                if (Flat(me, NetHubStair.Value, PromptRadius))
                { Prompt = PromptKind.Descend; PromptText = NetCheckpoint.Value ? "Go down the Undercroft stair (return dive, floor 3)" : "Go down the Undercroft stair"; }
            }
            else
            {
                ResolveSyncs();
                if (FloorSync == null || FloorSync.Built == null || FloorSync.Layout == null) return;
                floor = FloorSync.BuiltFloor;
                if (DiveRoute.HasStair(floor) && CellarFloorBuilder.TryStairWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && Flat(me, stair, PromptRadius))
                { Prompt = PromptKind.Stair; PromptText = "Take the stair down"; }
                else if (DiveState.FloorHasBank(floor) && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && Flat(me, bank, PromptRadius + CellarFloorBuilder.HomeMarkerOffset))
                {
                    if (DiveRoute.CanGoHome(floor, NetHeartBanked.Value)) { Prompt = PromptKind.GoHome; PromptText = "Take the party home"; }
                    else { Prompt = PromptKind.Bank; PromptText = floor == DiveState.HeartFloor ? "Bank the haul with the baker" : "Bank the haul at the checkpoint"; }
                }
            }
            if (Prompt == PromptKind.None) return;
            IInputService input = HeroInput.ResolveDefault();
            if (input == null || !input.Intent.InteractPressed) return;
            switch (Prompt)
            {
                case PromptKind.Descend: RequestDescend(); break;
                case PromptKind.Stair: RequestStair(floor); break;
                case PromptKind.Bank: if (HaulSync != null) HaulSync.RequestBank(); break;
                case PromptKind.GoHome: RequestGoHome(); break;
            }
        }

        private void DevKeys()
        {
            IDevInput dev = HeroInput.ResolveDefault() as IDevInput;
            if (dev == null) return;
            if (dev.WasPressed(DevKey.StartDive)) ServerStartDive();
            if (dev.WasPressed(DevKey.AbandonDive)) ServerAbandonDive();
        }

        private void MarkFloor()
        {
            ResolveSyncs();
            if (FloorSync != null && FloorSync.Built != null && FloorSync.Layout != null) CellarFloorBuilder.AddDiveMarkers(FloorSync.Layout, FloorSync.Built);
        }

        public override void OnNetworkDespawn()
        {
            if (HaulSync != null) { HaulSync.Banked -= OnBanked; HaulSync.DiveFailed -= OnDiveFailed; }
            _hasPending = false; _placed.Clear();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            ApplyPending();
            MarkFloor();
            LocalInteract();
            if (!IsServer) return;
            DevKeys();
            if (_clearAt >= 0f && Time.unscaledTime >= _clearAt)
            {
                _clearAt = -1f;
                if (!NetActive.Value && FloorSync != null) FloorSync.ServerSetFloor(_seed, CellarFloorSync.NoFloor);
            }
            if (Time.unscaledTime >= _nextPlace)
            {
                _nextPlace = Time.unscaledTime + PlacementInterval;
                FallNet();
                PlacementPass();
            }
        }

        private void OnGUI()
        {
            if (!IsSpawned) return;
            if (Prompt != PromptKind.None)
            {
                IInputService input = HeroInput.ResolveDefault();
                string key = input != null ? input.GetBindingDisplayString(InputActionNames.Interact) : "";
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 150f, 380f, 30f), "[" + (string.IsNullOrEmpty(key) ? "E" : key) + "]  " + PromptText);
            }
            if (_notice != null && Time.unscaledTime < _noticeUntil)
                GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 190f, 380f, 30f), _notice);
        }
    }
}
