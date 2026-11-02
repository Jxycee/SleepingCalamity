// Authority: host. The dive spine (docs/PHASE1.md 7.2 floor loop, checkpoint and banking). One DiveDirector per session, spawned by the host (DiveHub in the dive scene, or a test).
// It owns the dive FLOW; the data stays where it lives: the floor (seed, floor) in CellarFloorSync, the haul/DiveState/stir in DiveHaulSync (both spawned here once per session
// and kept: ending a dive sets the floor to NoFloor, it never despawns them, so banked pieces, the checkpoint and the cleared floors survive a return dive).
// Host-written NetworkVariables (late joiners get them with the spawn): NetActive, NetHeartBanked, NetCheckpoint, and the three world points NetHome / NetHubStair / NetOrigin.
// Clients ask by RPC and the host re-checks everything: RequestDescendRpc (hub stair; range), RequestStairRpc(fromFloor) (current floor must equal fromFloor, so two presses in one
// tick advance once; range; gate key), RequestGoHomeRpc (floor 4, banked, range), RequestBankRpc (range, then DiveHaulSync.ServerTryBankFor). A refusal for range or a shut stair sends the asker a short notice (NoticeRpc).
// The client prompts use a radius at least 0.5 m under the host's accept radius (DiveHaulSync.BankRadius), so a shown prompt is never refused for range. An owner hides the stair/bank/go-home prompts (so no press goes out) until the floor it
// stands on, the last teleport it applied (seed, floor), matches its built floor: a press between a floor rebuild and the teleport landing would otherwise skip a floor.
// Players move themselves (owner-authoritative NetworkTransform), so the host never writes a player transform: it sends TeleportRpc to the OWNER (SendTo.SpecifiedInParams), carrying the
// target plus the (seed, floor) it needs. The owner parks it as pending and applies it only once ITS OWN floor sync has built that (seed, floor) (an RPC can outrun the NetworkVariable that
// triggers the build), then disables the CharacterController, sets the pose and calls NetworkTransform.Teleport (remote copies do not interpolate across the map). A host-side placement pass
// (every 0.2 s) sends a teleport to every connected player that has not been placed for the current floor generation, which covers the first placement, floor changes, late joiners, a
// player that fell out of the world, and the trip home.
// Contents API (host, stable): Instance (set on spawn, cleared on despawn), RequireGateKey (default false: every stair is open; true: a stair needs ServerOpenStair(floor) first, the gate key opens floors 1-3),
// ServerOpenStair(floor) (idempotent; the opened set lives for the current dive and is cleared on dive start, end and fail), events DiveStarted, FloorChanged(floor), DiveEnded(reason); DiveHaulSync.Banked/DiveFailed stay.
// Debug (dev build, host): F7 starts a dive from anywhere, F8 abandons it (fails the dive like the stir countdown does).
// Host disconnect: NGO shuts the session down and everything despawns (no host migration in Phase 1).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public enum DiveEndReason : byte { Failed = 0, WentHome = 1, Abandoned = 2 }

    public sealed class DiveDirector : NetworkBehaviour, HostWorld.ITownSource
    {
        public const float PromptRadius = 3.5f;       // a client shows the stair/descend prompt inside this; the host accepts DiveHaulSync.BankRadius (5 m)
        public const float PromptMargin = 0.5f;
        public const float BankPromptRadius = DiveHaulSync.BankRadius - PromptMargin;   // bank / go-home prompt: covers the way-home column 1.8 m off the bank cell, still inside the host's range

        // The director of this process's host when it has one (a client-only process gets its own). Set on spawn, cleared on despawn.
        public static DiveDirector Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }
        public const float PlacementInterval = 0.2f;
        public const float FloorClearDelay = 0.75f;   // after the party was sent home, the floor stays this long so nobody drops through it first
        public const float PendingTimeout = 10f;      // an owner applies a teleport after this long even when its floor never built

        public readonly NetworkVariable<bool> NetActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> NetHeartBanked = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<bool> NetCheckpoint = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> NetStartFloor = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // the floor the next dive starts on (hub prompt)
        public readonly NetworkVariable<bool> NetStairOpen = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // the current floor's stair can be used (gate key taken or not required)
        public readonly NetworkVariable<Vector3> NetHome = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetHubStair = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetOrigin = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---- host-only configuration and hooks ----
        public bool RequireGateKey;                                 // host: true keeps every stair shut until ServerOpenStair(floor) (the gate key); false = all stairs open
        private readonly HashSet<int> _openedStairs = new HashSet<int>();
        public int[] PiecesPerFloor = DiveHaulSync.DefaultPiecesPerFloor;
        public StirTuning DiveStir = StirTuning.Default;            // tests shorten the countdown
        public uint? SeedOverride;                                  // tests and the debug start may pin the dive seed
        public event System.Action DiveStarted;
        public event System.Action<int> FloorChanged;
        public event System.Action<DiveEndReason> DiveEnded;
        public int RequestsHandled { get; private set; }            // host: RPCs received, accepted or refused (a test waits for refusals to land)

        // M6 S3, host only: the town's stock, held in memory (no save yet: S2). Created on first use from the Hollowmere layout. StockReceived(newPieces) fires when a dive end moved the banked haul in.
        public TownStock Town => _town ?? (_town = new TownStock(HollowmereLayout.Create()));
        public event System.Action<int> StockReceived;
        private TownStock _town;
        private DiveState _savedDive;   // the dive state restored from the world save: what a save captures while no dive has begun (never adopted by the haul sync)
        private TownSnapshot _loadedTown;   // the loaded snapshot: the first dive builds its haul and dive state from it with the then-current PiecesPerFloor / DiveStir
        private HostWorld _world;

        // HostWorld.ITownSource (M6 step 0), host only: the hub's town saves through the stock/town/residents/dive sections. Registered in OnNetworkSpawn, unregistered on despawn.
        // The baker banked at the heart (not yet home) is written as a banked resident; residents are passed as unknown (null), so the host keeps its saved first-meet beats.
        public TownSnapshot CaptureTown()
        {
            DiveState dive = _haulBegun && HaulSync != null && HaulSync.HostState != null ? HaulSync.HostState : _savedDive;
            return Town.Capture(dive, _heartBanked ? new[] { TownBuildings.BakerResident } : null, _residents);
        }

        // M6 S5, host only: the baker's first-meet beat (Q4 C). Latched by BakerTick the first time he stands in his complete bakery, kept in _residents (written into the residents
        // save section by CaptureTown, restored by RestoreTown; null until a latch exists or one was loaded, so an unknown copy never overwrites the saved one) and mirrored to
        // every peer through TownSync.NetBakerMet. It never goes back to false.
        private ResidentsSave _residents;
        private bool _bakerMet;
        public bool BakerMet => _bakerMet;

        // Host: latches the beat from the host town (idempotent), publishes it, and saves the world once when it flips.
        public void BakerTick()
        {
            if (!IsServer || !IsSpawned) return;
            if (!_bakerMet && BakerService.NextFirstMeet(false, Town.State))
            {
                _bakerMet = true;
                if (_residents == null) _residents = new ResidentsSave();
                ResidentSave r = _residents.Residents.Find(x => x != null && x.Id == TownBuildings.BakerResident);
                if (r == null) _residents.Residents.Add(new ResidentSave { Id = TownBuildings.BakerResident, FirstMeetBeat = true }); else r.FirstMeetBeat = true;
                if (_world != null) _world.RequestSave();
            }
            if (TownSync != null && TownSync.IsSpawned && TownSync.NetBakerMet.Value != _bakerMet) TownSync.NetBakerMet.Value = _bakerMet;
        }

        // Host, at dive start: every connected player's hero gets the baker's loaves (BakerService decides; the count REPLACES last dive's leftovers, so loaves are one dive's bread).
        // A player is told when the baker is housed but his wishes are unmet.
        private readonly DiveLoafLedger _loafLedger = new DiveLoafLedger();   // host: the dive-start loaf decision, replayed to a hero that spawns mid-dive

        private void OnHeroSpawned(HeroVitals v)
        {
            if (!IsServer || v == null || v.NetworkManager != NetworkManager) return;
            int? n = _loafLedger.ForSpawn();
            if (n.HasValue) v.ServerSetLoaves(n.Value);
        }

        private void GrantLoaves()
        {
            BakerTick();
            BakerLoaves loaves = BakerService.Evaluate(Town.State);
            _loafLedger.Begin(loaves.PerPlayer);
            IReadOnlyList<HeroVitals> heroes = HeroVitals.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroVitals v = heroes[i];
                if (v == null || !v.IsSpawned || v.NetworkManager != NetworkManager) continue;
                v.ServerSetLoaves(loaves.PerPlayer);
                if (loaves.Notify) NoticeTo(v.OwnerClientId, BakerService.WishesUnmetNotice);
                else if (loaves.Granted) NoticeTo(v.OwnerClientId, "The baker packed " + loaves.PerPlayer + " loaves for you.");
            }
        }

        public void RestoreTown(TownSnapshot loaded)
        {
            _residents = loaded?.Residents != null ? TownSaveRules.MergeLatches(null, loaded.Residents) : null;
            _bakerMet = TownSaves.FirstMeetBeat(_residents, TownBuildings.BakerResident);
            _savedDive = Town.Restore(loaded, PiecesPerFloor, DiveStir);   // capture-only until the first dive (spawn-time counts); the dive that runs is built from the snapshot at its start
            _loadedTown = loaded;
            _heartBanked = false;
            if (IsServer && IsSpawned && _savedDive != null) { NetCheckpoint.Value = _savedDive.CheckpointReached; NetStartFloor.Value = _savedDive.StartFloor; }   // the hub prompt must agree with the start floor
        }

        public CellarFloorSync FloorSync { get; private set; }      // host: the spawned syncs; clients resolve their own through ResolveSyncs
        public DiveHaulSync HaulSync { get; private set; }
        public TownSync TownSync { get; private set; }   // M6 S4: host-spawned with the director; replicates the town and holds the builder lock
        public CellarContentsSync ContentsSync { get; private set; }   // contents hook: host-spawned with the other two; the host arms it per built floor (ContentsTick)
        public uint Seed => _seed;

        private uint _seed;
        private bool _haulBegun, _heartBanked, _needsPlacement;
        private int _gen;
        private readonly Dictionary<ulong, int> _placed = new Dictionary<ulong, int>();
        private float _nextPlace, _clearAt = -1f;

        // owner side: a teleport waiting for the local floor
        private bool _hasPending; private Vector3 _pendPos; private float _pendYaw; private uint _pendSeed; private int _pendFloor; private float _pendSince;
        private uint _appliedSeed; private int _appliedFloor;   // owner: the (seed, floor) of the last teleport applied (0 = none / home)
        private float _nextLookup;
        private int _armedBuild = -1;   // contents hook: the FloorSync build the contents were last armed for
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
            if (TownSync != null) TownSync.ServerForceRelease();   // a dive frees the builder lock (the holder's placement mode then leaves on its own)
            if (!_haulBegun)
            {
                if (!HaulSync.ServerBegin(PiecesPerFloor, DiveStir)) return false;
                if (_loadedTown != null) HaulSync.ServerAdopt(Town.BuildDive(_loadedTown, PiecesPerFloor, DiveStir));   // the world save's banked haul, home pieces and progress, with the current counts
                _haulBegun = true;
                _seed = SeedOverride ?? PickSeed();
            }
            else _seed = SeedOverride ?? PickSeed();   // every dive gets fresh floors: chests and healing points do not replay (piece ids are per floor, so pieces stay collectable)
            int floor = forceFloor > 0 ? forceFloor : HaulSync.HostState.StartFloor;
            if (floor < DiveRoute.FirstFloor || floor > DiveRoute.LastFloor) return false;
            _clearAt = -1f; _openedStairs.Clear();
            _heartBanked = false; NetHeartBanked.Value = false;
            HaulSync.ServerSetFloor(floor);
            RefreshStairOpen(floor);
            FloorSync.ServerSetFloor(_seed, floor);
            NetActive.Value = true;
            GrantLoaves();
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
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;
            if (!NearPlayer(clientId, NetHubStair.Value)) { NoticeTo(clientId, TooFar); return false; }
            return ServerStartDive();
        }

        public bool ServerTryStair(ulong clientId, int fromFloor)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot take the stair
            int floor = HaulSync.Current.CurrentFloor;
            if (fromFloor != floor || !DiveRoute.HasStair(floor) || FloorSync.BuiltFloor != floor) return false;
            if (!StairIsOpen(floor)) { NoticeTo(clientId, "The way down is shut."); return false; }
            if (CellarFloorBuilder.TryStairWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && !NearPlayer(clientId, stair)) { NoticeTo(clientId, TooFar); return false; }
            int next = DiveRoute.NextFloor(floor);
            HaulSync.ServerMarkCleared(floor);
            HaulSync.ServerSetFloor(next);
            RefreshStairOpen(next);
            FloorSync.ServerSetFloor(_seed, next);
            _heartBanked = false; NetHeartBanked.Value = false;
            _gen++; _needsPlacement = true;
            FloorChanged?.Invoke(next);
            return true;
        }

        public bool ServerTryGoHome(ulong clientId)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot call the party home
            int floor = HaulSync.Current.CurrentFloor;
            if (!DiveRoute.CanGoHome(floor, _heartBanked) || FloorSync.BuiltFloor != floor) return false;
            if (CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && !NearPlayer(clientId, bank)) { NoticeTo(clientId, TooFar); return false; }
            return EndDive(DiveEndReason.WentHome);
        }

        // Host: the debug "abandon dive" (F8): the dive fails exactly like the stir countdown running out (unbanked pieces go back to their floors), the party goes home.
        public bool ServerAbandonDive()
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null) return false;
            HaulSync.ServerFailDive();
            return EndDive(DiveEndReason.Abandoned);
        }

        private const string TooFar = "You are too far away.";

        // Host: a bank press with the range refusal told to the asker (the rules themselves stay DiveHaulSync.ServerTryBankFor).
        public bool ServerTryBank(ulong clientId)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;
            int floor = HaulSync.Current.CurrentFloor;
            if (DiveState.FloorHasBank(floor) && FloorSync.BuiltFloor == floor && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && !NearPlayer(clientId, bank))
            { NoticeTo(clientId, TooFar); return false; }
            if (HaulSync.HostState != null && DiveState.FloorHasBank(floor) && !HaulSync.HostState.CanBankHere) { NoticeTo(clientId, "Nothing new to bank here."); return false; }
            return HaulSync.ServerTryBankFor(clientId);
        }

        private bool StairIsOpen(int floor) => !RequireGateKey || _openedStairs.Contains(floor);

        // Host: opens the stair on `floor` for the current dive (idempotent; cleared when a dive starts, ends or fails).
        public void ServerOpenStair(int floor) { if (!IsServer) return; _openedStairs.Add(floor); if (HaulSync != null && HaulSync.Current.CurrentFloor == floor) NetStairOpen.Value = true; }

        private void RefreshStairOpen(int floor) { NetStairOpen.Value = StairIsOpen(floor); }

        // Host: the floor the next dive starts on, from the live dive state (or the saved one while no dive has begun).
        private void RefreshStartFloor()
        {
            DiveState s = _haulBegun && HaulSync != null && HaulSync.HostState != null ? HaulSync.HostState : _savedDive;
            if (s != null) NetStartFloor.Value = s.StartFloor;
        }

        // Ends the dive: the party is sent home at once, the floor is cleared a moment later. Failed/abandoned: the haul already failed the dive (floor 0, stir reset).
        private bool EndDive(DiveEndReason reason)
        {
            if (!NetActive.Value) return false;
            if (reason == DiveEndReason.WentHome)
            {
                HaulSync.ServerSetFloor(DiveState.Town);
                HaulSync.ServerBankInTown();   // the stir reset of a finished dive (the heart bank already banked the haul)
            }
            // M6 S3: whatever the party banked is safe and comes home with it, whether the dive ended well or not: the banked haul moves into the host-held town stock
            // (the haul keeps it "collected" so the floors never respawn it). The heart bank is the baker's rescue.
            if (HaulSync != null && HaulSync.HostState != null)
            {
                int stocked = Town.Receive(HaulSync.HostState.Haul, _heartBanked);
                if (stocked > 0 || _heartBanked) StockReceived?.Invoke(stocked);
            }
            _loafLedger.End();
            NetActive.Value = false; _heartBanked = false; NetHeartBanked.Value = false; _openedStairs.Clear(); NetStairOpen.Value = true;
            RefreshStartFloor();
            _gen++; _needsPlacement = true;
            _clearAt = Time.unscaledTime + FloorClearDelay;
            ContentsClear();   // contents hook: the dive is over (ended, failed or abandoned): its enemies go
            ResetParty();      // wipe: every hero arrives home up and moving, and the combat host forgets the wipe
            DiveEnded?.Invoke(reason);
            return true;
        }

        private void OnBanked()
        {
            if (!IsServer || HaulSync == null) return;
            int floor = HaulSync.Current.CurrentFloor;
            if (floor == DiveState.CheckpointFloor && !NetCheckpoint.Value) NetCheckpoint.Value = true;
            RefreshStartFloor();
            if (floor == DiveState.HeartFloor) { _heartBanked = true; NetHeartBanked.Value = true; }
        }

        private void OnDiveFailed() { if (NetActive.Value) EndDive(DiveEndReason.Failed); }

        private bool EnsureSyncs()
        {
            if (FloorSync != null && HaulSync != null && ContentsSync != null) return true;
            NetworkObject floorPrefab = FindPrefab<CellarFloorSync>(), haulPrefab = FindPrefab<DiveHaulSync>(), contentsPrefab = FindPrefab<CellarContentsSync>();
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
            // Contents hook: the host-authoritative floor contents (chests, enemies, gate key). The director arms it per built floor and opens the stair on its ExitUnlocked.
            if (ContentsSync == null && contentsPrefab != null)
            {
                ContentsSync = NetworkManager.SpawnManager.InstantiateAndSpawn(contentsPrefab, NetworkManager.LocalClientId, true, false, false, Vector3.zero, Quaternion.identity).GetComponent<CellarContentsSync>();
                ContentsSync.FloorSync = FloorSync; ContentsSync.HaulSync = HaulSync;
                ContentsSync.AutoArm = false;
                ContentsSync.ExitUnlocked += ServerOpenStair;
                RequireGateKey = true;
            }
            return true;
        }

        // Contents hook (host, every frame): arm the contents once per finished floor build, using the same build the dive markers wait for.
        private void ContentsTick()
        {
            if (ContentsSync == null || !ContentsSync.IsSpawned || !NetActive.Value || FloorSync == null || FloorSync.Built == null || FloorSync.Layout == null) return;
            if (FloorSync.BuiltFloor != HaulSync.Current.CurrentFloor || FloorSync.BuiltSeed != _seed || FloorSync.BuildCount == _armedBuild) return;
            _armedBuild = FloorSync.BuildCount;
            ContentsSync.ServerArmFloor(FloorSync.Layout, FloorSync.Built.Root);
        }

        // Wipe (host, every frame): every connected hero down or out during a dive ends it as a failure (PHASE1 P24). Also covers the last standing player disconnecting (their hero
        // leaves HeroVitals.All, the rest are all down). The haul fails first, which routes through OnDiveFailed to EndDive(Failed); the direct EndDive is then a no-op.
        private readonly List<PlayerVitals> _wipeScratch = new List<PlayerVitals>();
        private void WipeTick()
        {
            if (!NetActive.Value || HaulSync == null) return;
            _wipeScratch.Clear();
            IReadOnlyList<HeroVitals> heroes = HeroVitals.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroVitals v = heroes[i];
                if (v != null && v.IsSpawned && v.NetworkManager == NetworkManager) _wipeScratch.Add(v.Model);
            }
            if (!PlayerVitals.AllDown(_wipeScratch)) return;
            HaulSync.ServerFailDive();
            EndDive(DiveEndReason.Failed);
        }

        // Wipe (host): full reset of every hero of this session (up, full HP) and the combat host's wipe state, on every dive end.
        private void ResetParty()
        {
            IReadOnlyList<HeroVitals> heroes = HeroVitals.All;
            for (int i = 0; i < heroes.Count; i++)
            {
                HeroVitals v = heroes[i];
                if (v != null && v.IsSpawned && v.NetworkManager == NetworkManager) v.ServerFullReset();
            }
            if (CombatHost.Current != null) CombatHost.Current.ServerResetWipe();
        }

        private void ContentsClear() { _armedBuild = -1; if (ContentsSync != null && ContentsSync.IsSpawned) ContentsSync.ServerClearFloor(); }

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
            _appliedSeed = _pendSeed; _appliedFloor = _pendFloor;
            var cc = player.GetComponent<CharacterController>();
            bool ccOn = cc != null && cc.enabled;
            if (ccOn) cc.enabled = false;
            Quaternion rot = Quaternion.Euler(0f, _pendYaw, 0f);
            Vector3 before = player.transform.position;
            player.transform.SetPositionAndRotation(_pendPos, rot);
            var nt = player.GetComponent<NetworkTransform>();
            if (nt != null && nt.CanCommitToTransform) nt.Teleport(_pendPos, rot, player.transform.localScale);
            if (ccOn) cc.enabled = true;
            if (HeroCameraRig.Local != null) HeroCameraRig.Local.NotifyWarped(_pendPos - before);   // the camera jumps with the hero instead of gliding 200 m
            var loco = player.GetComponent<HeroLocomotion>();
            if (loco != null && loco.enabled) loco.SetView(_pendYaw, HeroLocomotion.FreePitchDownDeg);
        }

        // On a client the syncs are found in the scene by this peer's NetworkManager (one process can hold several peers' copies).
        private void ResolveSyncs()
        {
            if (IsServer) return;
            if ((FloorSync != null && HaulSync != null && ContentsSync != null) || Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + 0.5f;
            if (FloorSync == null) foreach (CellarFloorSync s in FindObjectsByType<CellarFloorSync>(FindObjectsSortMode.None)) if (s.IsSpawned && s.NetworkManager == NetworkManager) { FloorSync = s; break; }
            if (HaulSync == null) foreach (DiveHaulSync s in FindObjectsByType<DiveHaulSync>(FindObjectsSortMode.None)) if (s.IsSpawned && s.NetworkManager == NetworkManager) { HaulSync = s; HaulSync.FloorSync = FloorSync; break; }
            if (ContentsSync == null) foreach (CellarContentsSync s in FindObjectsByType<CellarContentsSync>(FindObjectsSortMode.None)) if (s.IsSpawned && s.NetworkManager == NetworkManager) { ContentsSync = s; break; }
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
        public void RequestBank() { RequestBankRpc(); }

        [Rpc(SendTo.Server)]
        private void RequestDescendRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryDescend(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void RequestStairRpc(int fromFloor, RpcParams rpcParams = default) { RequestsHandled++; ServerTryStair(rpcParams.Receive.SenderClientId, fromFloor); }

        [Rpc(SendTo.Server)]
        private void RequestBankRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryBank(rpcParams.Receive.SenderClientId); }

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
                { Prompt = PromptKind.Descend; PromptText = NetStartFloor.Value >= DiveRoute.ReturnFloor ? "Go down the Undercroft stair (return dive, floor " + NetStartFloor.Value + ")" : "Go down the Undercroft stair"; }
            }
            else
            {
                ResolveSyncs();
                if (FloorSync == null || FloorSync.Built == null || FloorSync.Layout == null) return;
                floor = FloorSync.BuiltFloor;
                if (_appliedFloor != floor || _appliedSeed != FloorSync.BuiltSeed) return;   // not yet teleported onto this floor: standing where the last one's stair was
                if (DiveRoute.HasStair(floor) && CellarFloorBuilder.TryStairWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && Flat(me, stair, PromptRadius))
                { Prompt = PromptKind.Stair; PromptText = NetStairOpen.Value ? "Take the stair down" : "The way down is shut (find the gate key)"; }
                else if (DiveState.FloorHasBank(floor) && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && Flat(me, bank, BankPromptRadius))
                {
                    if (DiveRoute.CanGoHome(floor, NetHeartBanked.Value)) { Prompt = PromptKind.GoHome; PromptText = "Take the party home"; }
                    else if (HaulSync != null && HaulSync.Current.BankSpent) { }   // already banked here and nothing new to bank: no prompt
                    else { Prompt = PromptKind.Bank; PromptText = floor == DiveState.HeartFloor ? "Bank the haul with the baker" : "Bank the haul at the checkpoint"; }
                }
            }
            if (ContentsSync != null && Prompt != PromptKind.None && ContentsSync.WouldUseInteract(me)) Prompt = PromptKind.None;   // contents hook: a chest or the door in reach takes the press, so one press does one thing
            if (Prompt == PromptKind.None) return;
            IInputService input = HeroInput.ResolveDefault();
            if (input == null || !input.Intent.InteractPressed) return;
            if (Prompt == PromptKind.Stair && !NetStairOpen.Value) return;   // the locked stair only explains itself
            if (LocalHeroIsDown(player)) return;   // a downed hero presses nothing (the host refuses it too)
            switch (Prompt)
            {
                case PromptKind.Descend: RequestDescend(); break;
                case PromptKind.Stair: RequestStair(floor); break;
                case PromptKind.Bank: RequestBank(); break;
                case PromptKind.GoHome: RequestGoHome(); break;
            }
        }

        private static bool LocalHeroIsDown(NetworkObject player) { HeroVitals v = player.GetComponent<HeroVitals>(); return v != null && !v.IsUp; }

        private void DevKeys()
        {
            IDevInput dev = HeroInput.ResolveDefault() as IDevInput;
            if (dev == null) return;
            if (dev.WasPressed(DevKey.StartDive)) ServerStartDive();
            if (dev.WasPressed(DevKey.AbandonDive)) ServerAbandonDive();
        }

        // Owner (any peer): H eats one of the baker's loaves; the host re-checks everything (HeroVitals.ServerEatLoaf).
        private void LocalEat()
        {
            if (!(HeroInput.ResolveDefault() is IDevInput dev) || !dev.WasPressed(DevKey.EatLoaf)) return;
            NetworkObject player = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            HeroVitals v = player != null ? player.GetComponent<HeroVitals>() : null;
            if (v != null) v.RequestEatLoaf();
        }

        private void MarkFloor()
        {
            ResolveSyncs();
            if (FloorSync != null && FloorSync.Built != null && FloorSync.Layout != null) CellarFloorBuilder.AddDiveMarkers(FloorSync.Layout, FloorSync.Built);
        }

        public override void OnNetworkDespawn()
        {
            HeroVitals.ServerHeroSpawned -= OnHeroSpawned;
            _loafLedger.End();
            if (_world != null)
            {
                try { _world.UnregisterTownSource(this); } catch (System.Exception e) { Debug.LogException(e); }
                _world = null;
            }
            if (TownSync != null && IsServer && TownSync.IsSpawned) TownSync.NetworkObject.Despawn(true);
            TownSync = null;
            if (HaulSync != null) { HaulSync.Banked -= OnBanked; HaulSync.DiveFailed -= OnDiveFailed; }
            if (ContentsSync != null) ContentsSync.ExitUnlocked -= ServerOpenStair;
            FloorSync = null; HaulSync = null; ContentsSync = null; _haulBegun = false; _armedBuild = -1;   // a re-spawn re-creates and re-wires the syncs (EnsureSyncs)
            _hasPending = false; _placed.Clear(); _appliedFloor = 0; _appliedSeed = 0u;
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            if (Instance == null || IsServer) Instance = this;   // one process can hold several peers' directors (tests): the host's wins
            if (IsServer && ServiceRoots.App != null && ServiceRoots.App.TryGet(out HostWorld w)) { _world = w; try { w.RegisterTownSource(this); } catch (System.Exception e) { Debug.LogException(e); } }   // a save-layer fault must not stop the spawn
            if (IsServer) { HeroVitals.ServerHeroSpawned += OnHeroSpawned; SpawnTownSync(); }
        }

        // Host (M6 S4): the town's replication object, a dynamic network prefab spawned beside the director so a late joiner gets it with the spawn. The town it publishes is Town.State.
        private void SpawnTownSync()
        {
            if (TownSync != null) return;
            NetworkObject prefab = FindPrefab<TownSync>();
            if (prefab == null) { Debug.LogError("DiveDirector: TownSync is not in the network prefab list (run the project builder)."); return; }
            TownSync = NetworkManager.SpawnManager.InstantiateAndSpawn(prefab, NetworkManager.LocalClientId, true, false, false, Vector3.zero, Quaternion.identity).GetComponent<TownSync>();
            TownSync.Director = this;
            TownSync.ServerSeedBakerMet(_bakerMet);   // a latch restored from the save: published without the first-meet line
            TownSync.ServerRefresh();
        }

        private void Update()
        {
            if (!IsSpawned) return;
            ApplyPending();
            MarkFloor();
            LocalInteract();
            LocalEat();
            if (!IsServer) return;
            DevKeys();
            ContentsTick();
            WipeTick();
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
                BakerTick();
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
