// Authority: host. The dive spine (spec 2026-11-03 sections 4-5: the Undercroft's floors 1-7, the halfway bank room on floor 5, the checkpoint floor pick, the stand-in ending). One DiveDirector per session,
// spawned by the host (DiveHub in the dive scene, or a test). It owns the dive FLOW and the party state that outlives a dive: PartyBags, DiveCheckpoint and DivePieces (Core/Cellars); every dive gets a fresh
// DiveSession over them (Core/Cellars/DiveSession: the rules for floor, phase, stir, momentum). The data stays where it lives: the floor (seed, floor) in CellarFloorSync, the session and its summary in
// DiveHaulSync (both spawned here once per session and kept: ending a dive sets the floor to NoFloor, it never despawns them, so found pieces, the checkpoint and the bags survive a return dive).
// Host-written NetworkVariables (late joiners get them with the spawn): NetActive, NetCheckpointHeld (the next dive may pick a floor), NetPickableMaxFloor (the deepest floor it may pick), NetStairOpen, and
// the three world points NetHome / NetHubStair / NetOrigin (where the party lands, the church trapdoor, where the floors are built).
// Clients ask by RPC and the host re-checks everything: RequestStartDiveRpc(floor) (trapdoor range, DiveCheckpoint.CanStartOn), RequestDescendRpc (floor 1), RequestStairRpc(fromFloor) (current floor must equal
// fromFloor, so two presses in one tick advance once; range; gate key; floor 5's stair only after the bank choice), RequestGoHomeRpc (the cleared bank floor, range), RequestFinishRpc (floor 7's EndCell, range),
// RequestBankRpc (range, then DiveHaulSync.ServerTryBankFor). A refusal for range or a shut stair sends the asker a short notice (NoticeRpc).
// Pieces and people are safe the moment they are picked up (DiveHaulSync.PieceAdded): the director moves each into the town stock at once (TownStock.ReceiveFound); the baker is a person on floor 3.
// A dive that started at the bank (floor 5, decision 2) places the party at the bank room cell and never spawns the guardian; so does a resumed bank-room suspend (plan 3 task 8a). A loaded bank
// suspend waits (NetSuspendedDive) until the party enters the trapdoor, which resumes it instead of starting a dive.
// The client prompts use a radius at least 0.5 m under the host's accept radius (DiveHaulSync.BankRadius), so a shown prompt is never refused for range. An owner hides the stair/bank/go-home prompts (so no press goes out) until the floor it
// stands on, the last teleport it applied (seed, floor), matches its built floor: a press between a floor rebuild and the teleport landing would otherwise skip a floor.
// Players move themselves (owner-authoritative NetworkTransform), so the host never writes a player transform: it sends TeleportRpc to the OWNER (SendTo.SpecifiedInParams), carrying the
// target plus the (seed, floor) it needs. The owner parks it as pending and applies it only once ITS OWN floor sync has built that (seed, floor) (an RPC can outrun the NetworkVariable that
// triggers the build), then disables the CharacterController, sets the pose and calls NetworkTransform.Teleport (remote copies do not interpolate across the map). A host-side placement pass
// (every 0.2 s) sends a teleport to every connected player that has not been placed for the current floor generation, which covers the first placement, floor changes, late joiners, a
// player that fell out of the world, and the trip home.
// Contents API (host, stable): Instance (set on spawn, cleared on despawn), RequireGateKey (default false: every stair is open; true: a gate-key stair needs ServerOpenStair(floor) first, the gate key opens floors 1-4 and 6),
// ServerOpenStair(floor) (idempotent; the opened set lives for the current dive and is cleared on dive start, end and fail), events DiveStarted, FloorChanged(floor), DiveEnded(reason); DiveHaulSync.Banked/DiveFailed stay.
// Debug (dev build, host): F7 starts a dive from anywhere (always floor 1), F8 abandons it (fails the dive like the stir countdown does); G cycles the hub prompt's floor pick.
// Bank room (plan 3 task 8, 8a): the bank press enters the room (DiveHaulSync.ServerEnterBankRoom); inside it a minimal IMGUI bank screen (dev keys [ ] P Y N) asks for picks, push-on and go-home; the
// shared choice confirms in DiveHaulSync, which raises PushedOn (saved at once) / WentHome (EndDive). Every capture says what it knows about the bank suspend (TownStock.CaptureWorld): a quit in the bank
// room, or on floor 5 after the push-on, saves a suspend; a loaded one waits as pending and is re-emitted until the trapdoor resumes it (ServerResumeSuspended).
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
    public enum DiveEndReason : byte { Failed = 0, WentHome = 1, Abandoned = 2, Finished = 3 }

    // Runs before HeroLocomotion (order 0): the gravity hold must be decided in the same frame a replicated floor swap lands, or the owner hero takes one gravity step over cells the new floor lacks
    // (a long frame under load sank a client hero 1.3 m once). ApplyPending also lands the arrival teleport before the hero moves.
    [DefaultExecutionOrder(-30)]
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
        public readonly NetworkVariable<bool> NetCheckpointHeld = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // DiveCheckpoint.Held: the next dive may pick a floor
        public readonly NetworkVariable<bool> NetSuspendedDive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // a loaded bank suspend waits: the trapdoor resumes it instead of starting a dive
        public readonly NetworkVariable<int> NetPickableMaxFloor = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // the deepest floor the next dive may start on (hub prompt)
        public readonly NetworkVariable<bool> NetStairOpen = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);   // the current floor's stair can be used (gate key taken or not required)
        public readonly NetworkVariable<Vector3> NetHome = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetHubStair = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetOrigin = new NetworkVariable<Vector3>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---- host-only configuration and hooks ----
        public bool RequireGateKey;                                 // host: true keeps every stair shut until ServerOpenStair(floor) (the gate key); false = all stairs open
        private readonly HashSet<int> _openedStairs = new HashSet<int>();
        public uint? SeedOverride;                                  // tests and the debug start may pin the dive seed
        public event System.Action DiveStarted;
        public event System.Action<int> FloorChanged;
        public event System.Action<DiveEndReason> DiveEnded;
        public int RequestsHandled { get; private set; }            // host: RPCs received, accepted or refused (a test waits for refusals to land)

        // M6 S3, host only: the town's stock, held in memory and saved through HostWorld (RestoreTown / CaptureTown). Created on first use from the Hollowmere layout. A piece or person goes into the stock the
        // moment it is found (HaulSync.PieceAdded -> TownStock.ReceiveFound), so "found" is "the town holds it"; StockReceived(1) fires for each one newly stocked.
        public TownStock Town => _town ?? (_town = new TownStock(HollowmereLayout.Create()));
        public event System.Action<int> StockReceived;
        private TownStock _town;
        private HostWorld _world;

        // The party's state between dives (host only): bags, the one-time checkpoint and the pieces outlive every DiveSession; a fresh session is built over them for each dive.
        public const ulong FallbackWorldSeed = DiveHaulSync.DefaultWorldSeed;   // a director with no world save (tests) deals its pieces from this seed
        private PartyBags _bags = new PartyBags();
        private DiveCheckpoint _checkpoint = new DiveCheckpoint();
        private DivePieces _pieces;
        private DiveSession _session;
        private bool _bossBeaten;                 // the boss shortcut (decision 13): no boss exists yet, so only a loaded save can set it
        private string _extraSlotOwner;           // the saved previous owner of the 3-player extra bank slot (a bag id): DiveHaulSync.PreviousExtraOwner works in roster slots, this is what the file keeps
        private SuspendSave _pendingSuspend;      // a bank suspend loaded with the world and not resumed yet (plan 3 task 8a): every capture re-emits it until the trapdoor uses it
        private IReadOnlyDictionary<int, int> _depths;

        public PartyBags Bags => _bags;
        public DiveCheckpoint Checkpoint => _checkpoint;
        public DivePieces Pieces => _pieces;
        public DiveSession Session => _session;   // host only; a Town-phase session between dives

        private IReadOnlyDictionary<int, int> Depths()
        {
            if (_world != null && _world.PieceFloors != null && _world.PieceFloors.Count > 0) return _world.PieceFloors;
            return PieceDepths.Assign(FallbackWorldSeed, PieceCatalog.Specs);
        }

        private DiveSession NewSession() => new DiveSession(_bags, _checkpoint, _pieces);

        private void EnsureWorldState()
        {
            if (_depths == null) _depths = Depths();
            if (_pieces == null) _pieces = new DivePieces(_depths);
            if (_session == null) _session = NewSession();
        }

        // Tests: holds or releases the one-time checkpoint without walking the whole dive to the bank (the real way is going home from the bank room).
        internal void TestHoldCheckpoint(bool held) { _checkpoint.Restore(held); RefreshHub(); }

        // Host: the hub prompt's two net values, from the checkpoint rule (floors 1 to the bank, or just 1 and the bank after the boss).
        private void RefreshHub()
        {
            if (!IsServer || !IsSpawned) return;
            int max = UndercroftPlan.FirstFloor;
            for (int f = UndercroftPlan.BankFloor; f > UndercroftPlan.FirstFloor; f--)
                if (_checkpoint.CanStartOn(f, _bossBeaten)) { max = f; break; }
            if (NetCheckpointHeld.Value != _checkpoint.Held) NetCheckpointHeld.Value = _checkpoint.Held;
            if (NetSuspendedDive.Value != (_pendingSuspend != null)) NetSuspendedDive.Value = _pendingSuspend != null;
            if (NetPickableMaxFloor.Value != max) NetPickableMaxFloor.Value = max;
        }

        // Every peer: the floors the hub prompt offers, from the replicated values (1 always, the deepest pickable floor, and every floor between while the checkpoint is held).
        public IReadOnlyList<int> PickableFloors()
        {
            var list = new List<int>();
            int max = NetPickableMaxFloor.Value;
            for (int f = UndercroftPlan.FirstFloor; f <= max; f++)
                if (f == UndercroftPlan.FirstFloor || f == max || NetCheckpointHeld.Value) list.Add(f);
            return list;
        }

        // HostWorld.ITownSource (M6 step 0), host only: the hub's town saves through the stock/town/residents/dive/bags sections. Registered in OnNetworkSpawn, unregistered on despawn.
        // The sections come from TownStock.CaptureWorld over the live session; residents are passed as unknown (null), so the host keeps its saved first-meet beats.
        // Every capture says what it knows about the bank suspend (TownStock.CaptureWorld): the bank room or floor 5 after the push-on writes one with the bags as carried, a loaded one nobody has
        // resumed yet is re-emitted, anything else is "known, none" (a used or ended suspend leaves the file). The 3-player extra slot's previous owner is saved from DiveHaulSync as a bag id.
        public TownSnapshot CaptureTown()
        {
            EnsureWorldState();
            var ctx = new TownStock.SuspendContext { Seed = _seed, Pending = _pendingSuspend };
            if (HaulSync != null && HaulSync.IsSpawned && HaulSync.IsServer)
            {
                string[] bySlot = HaulSync.IdsBySlot();
                ctx.IdsBySlot = bySlot;
                var slots = new List<int>(); var present = new List<string>();
                HaulSync.GetPresence(slots, present);
                ctx.PresentIds = present;
                int prev = HaulSync.PreviousExtraOwner;
                if (prev >= 0 && prev < bySlot.Length && bySlot[prev] != null) _extraSlotOwner = bySlot[prev];
            }
            return Town.CaptureWorld(_session, _bossBeaten, _extraSlotOwner, _depths, _residents, ctx);
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
            _depths = Depths();
            TownStock.WorldRestore w = Town.RestoreWorld(loaded, _depths);   // the stock and town, plus the bags, checkpoint and pieces the party runs with
            _bags = w.Bags; _checkpoint = w.Checkpoint; _pieces = w.Pieces; _bossBeaten = w.BossBeaten; _extraSlotOwner = w.ExtraSlotOwner;
            _pendingSuspend = w.Suspend;   // a loaded bank suspend: the bags above already hold its mid-dive halves, the trapdoor resumes it (ServerResumeSuspended)
            _session = NewSession();
            if (IsServer && IsSpawned) { if (HaulSync != null) { HaulSync.ServerAdopt(_session); HaulSync.PreviousExtraOwner = -1; } RefreshHub(); }   // the hub prompt must agree with the loaded checkpoint
        }

        public CellarFloorSync FloorSync { get; private set; }      // host: the spawned syncs; clients resolve their own through ResolveSyncs
        public DiveHaulSync HaulSync { get; private set; }
        public TownSync TownSync { get; private set; }   // M6 S4: host-spawned with the director; replicates the town and holds the builder lock
        public CellarContentsSync ContentsSync { get; private set; }   // contents hook: host-spawned with the other two; the host arms it per built floor (ContentsTick)
        public uint Seed => _seed;

        private uint _seed;
        private bool _needsPlacement;
        private int _gen;
        private readonly Dictionary<ulong, int> _placed = new Dictionary<ulong, int>();
        private float _nextPlace, _clearAt = -1f;
        private bool _placeRetry;   // host: a placement is still waiting for the built floor; PlacementPass runs every frame until it is done
        // owner side: the gravity hold while a floor hand-over is unresolved (see UpdateHold)
        private HandoverHoldState _hold;
        private NetworkManager _dcMgr;   // host: the manager whose disconnect callback prunes _placed

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

        // Host only: starts a dive on `pickedFloor` (0 = floor 1; the hub's checkpoint pick otherwise). The host re-checks DiveCheckpoint.CanStartOn: without the checkpoint only floor 1 starts, and a start on
        // another floor spends the checkpoint. False (nothing changes) when a dive runs, the floor is refused, or off the host. Every dive gets fresh floors and a fresh DiveSession over the party's
        // bags, checkpoint and pieces (found pieces stay found; chests and healing points do not replay).
        public bool ServerStartDive(int pickedFloor = 0)
        {
            if (!IsServer || !IsSpawned || NetActive.Value) return false;
            if (!EnsureSyncs()) return false;
            EnsureWorldState();
            if (_pendingSuspend != null) return ServerResumeSuspended();   // a loaded bank suspend: the trapdoor resumes it, the pick does not matter
            int floor = pickedFloor > 0 ? pickedFloor : UndercroftPlan.FirstFloor;
            if (!UndercroftPlan.IsFloor(floor) || floor > DiveFloorTable.LastBuiltFloor || !_checkpoint.CanStartOn(floor, _bossBeaten)) return false;
            if (TownSync != null) TownSync.ServerForceRelease();   // a dive frees the builder lock (the holder's placement mode then leaves on its own)
            _session = NewSession();
            if (!HaulSync.ServerAdopt(_session) || !HaulSync.ServerStart(floor, _bossBeaten)) return false;
            _seed = SeedOverride ?? PickSeed();
            _clearAt = -1f; _openedStairs.Clear();
            HaulSync.DiveSeed = _seed;   // the 3-player extra bank slot owner is picked from it when the bank room opens
            if (HaulSync.PreviousExtraOwner < 0) HaulSync.PreviousExtraOwner = HaulSync.SlotOfBagId(_extraSlotOwner);   // after a load: the saved previous owner, as a roster slot
            RefreshStairOpen(floor);
            FloorSync.ServerSetFloor(_seed, floor);
            NetActive.Value = true;
            RefreshHub();
            GrantLoaves();
            MarkPlacementNow();
            DiveStarted?.Invoke();
            FloorChanged?.Invoke(floor);
            return true;
        }

        // Host only: the party enters the trapdoor with a loaded bank suspend (plan 3 task 8a, decision 9). The saved session comes back as it was (SuspendSaveRules.Resume): push-on chosen => the start of
        // the NEXT floor (floor 6) with the saved stir, momentum, retry and bank state, the bank locked; push-on not yet chosen => the bank room again, visits reopened with the saved picks. The saved
        // dive seed is used, so the chest masks still match their seed and floor. A resume is not a start: the checkpoint is neither spent nor set, no failure is counted, and the section is used up
        // (the next capture says "known, none"). The hero state (HP, loaves) is not saved: heroes arrive healed and the baker's loaves are packed again. A section that cannot resume (a damaged
        // phase) settles as a failed dive so no carried item is lost. Players recorded but absent keep their suspended bag untouched (the bank visits follow who is present).
        private bool ServerResumeSuspended()
        {
            SuspendSave s = _pendingSuspend;
            if (s == null || NetActive.Value || HaulSync == null || FloorSync == null) return false;
            var slots = new List<int>(); var present = new List<string>();
            HaulSync.GetPresence(slots, present);
            string[] bySlot = HaulSync.IdsBySlot();
            HaulSync.PreviousExtraOwner = HaulSync.PreviousExtraOwner >= 0 ? HaulSync.PreviousExtraOwner : HaulSync.SlotOfBagId(_extraSlotOwner);
            DiveSession session = SuspendSaveRules.Resume(s, _bags, _checkpoint, _pieces, bySlot, slots, present, s.Seed, HaulSync.PreviousExtraOwner);
            _pendingSuspend = null;   // used either way
            if (session == null)
            {
                _bags.FailAll(Mathf.Clamp(s.Floor, UndercroftPlan.FirstFloor, UndercroftPlan.BossFloor));   // cannot resume: the carried items wait in satchels, nothing is lost
                RefreshHub();
                return false;
            }
            if (TownSync != null) TownSync.ServerForceRelease();
            _session = session;
            HaulSync.ServerAdoptResumed(session, s.Seed);
            _seed = s.Seed; _clearAt = -1f; _openedStairs.Clear();
            RefreshStairOpen(session.Floor);
            FloorSync.ServerSetFloor(_seed, session.Floor);
            NetActive.Value = true;
            RefreshHub();
            GrantLoaves();
            MarkPlacementNow();
            DiveStarted?.Invoke();
            FloorChanged?.Invoke(session.Floor);
            return true;
        }

        private static uint PickSeed()
        {
            var rng = new System.Random(unchecked(System.Environment.TickCount * 7919 + (int)(Time.realtimeSinceStartupAsDouble * 1000.0)));
            return (uint)rng.Next() ^ ((uint)rng.Next(2) << 31);
        }

        // Host: validated requests (the RPCs call these; tests may too). clientId = the asker.
        public bool ServerTryDescend(ulong clientId) => ServerTryStartDive(clientId, UndercroftPlan.FirstFloor);

        // The hub trapdoor: the asker is up and in range, then ServerStartDive re-checks the checkpoint rule (a floor the checkpoint does not allow is refused with a notice).
        public bool ServerTryStartDive(ulong clientId, int floor)
        {
            if (!IsServer || !IsSpawned || NetActive.Value) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;
            if (!NearPlayer(clientId, NetHubStair.Value)) { NoticeTo(clientId, TooFar); return false; }
            if (!ServerStartDive(floor)) { NoticeTo(clientId, "You cannot start on that floor."); return false; }
            return true;
        }

        public bool ServerTryStair(ulong clientId, int fromFloor)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null || _session == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot take the stair
            int floor = _session.Floor;
            if (_session.Phase != DivePhase.Floor || fromFloor != floor || !DiveFloorTable.HasStair(floor, _session.BankChosenThisDive) || FloorSync.BuiltFloor != floor) return false;
            if (!StairIsOpen(floor)) { NoticeTo(clientId, "The way down is shut."); return false; }
            if (CellarFloorBuilder.TryDescentWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && !NearPlayer(clientId, stair)) { NoticeTo(clientId, TooFar); return false; }
            int next = UndercroftPlan.NextFloor(floor, DiveFloorTable.LastBuiltFloor);
            HaulSync.ServerMarkCleared(floor);   // feeds the stir (a no-op on the bank floor, which its guardian already cleared)
            if (!HaulSync.ServerEnterFloor(next)) return false;
            RefreshStairOpen(next);
            FloorSync.ServerSetFloor(_seed, next);
            MarkPlacementNow();
            FloorChanged?.Invoke(next);
            return true;
        }

        // The "go home" press at the bank (plan 3 task 8, decision 11): the floor's guardian is down, the choice is not made yet. The press opens, joins or swaps the confirm window (DiveHaulSync); the
        // dive ends when it is confirmed (a solo party at once, a larger one on a second player's press or the timer). The checkpoint is held afterwards. True when the press was accepted.
        public bool ServerTryGoHome(ulong clientId)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null || _session == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot call the party home
            int floor = _session.Floor;
            if (!DiveFloorTable.HasBank(floor) || !_session.IsCleared(floor) || _session.BankChosenThisDive || FloorSync.BuiltFloor != floor) return false;
            if (CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && !NearPlayer(clientId, bank)) { NoticeTo(clientId, TooFar); return false; }
            return HaulSync.ServerChooseGoHome(clientId);   // the shared choice: it ends the dive (OnWentHome) the moment it is confirmed, which a solo party's first press does
        }

        // The Phase 1 stand-in ending: the party stands at floor 7's EndCell and the dive finishes, everything carried comes home (DiveSession.Finish), no checkpoint.
        public bool ServerTryFinish(ulong clientId)
        {
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null || _session == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;   // a downed hero cannot end the dive
            int floor = _session.Floor;
            if (_session.Phase != DivePhase.Floor || floor != DiveFloorTable.LastBuiltFloor || FloorSync.BuiltFloor != floor) return false;
            if (CellarFloorBuilder.TryEndWorld(FloorSync.Layout, FloorSync.Built, out Vector3 end) && !NearPlayer(clientId, end)) { NoticeTo(clientId, TooFar); return false; }
            if (!HaulSync.ServerFinish()) return false;
            return EndDive(DiveEndReason.Finished);
        }

        // Host: the debug "abandon dive" (F8): the dive fails exactly like the stir countdown running out (every bag fails; found pieces stay safe), the party goes home.
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
            if (!IsServer || !IsSpawned || !NetActive.Value || HaulSync == null || FloorSync == null || _session == null) return false;
            if (!DiveHaulSync.AskerIsUp(NetworkManager, clientId)) return false;
            int floor = _session.Floor;
            if (!DiveFloorTable.HasBank(floor)) return false;
            if (FloorSync.BuiltFloor == floor && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && !NearPlayer(clientId, bank))
            { NoticeTo(clientId, TooFar); return false; }
            if (_session.Phase == DivePhase.Floor && !_session.IsCleared(floor)) { NoticeTo(clientId, "The way to the bank room is guarded."); return false; }
            return HaulSync.ServerTryBankFor(clientId);
        }

        // A gate-key stair needs ServerOpenStair first; the bank floor's and the ending floor's ways do not (the guardian and the bank choice gate them).
        private bool StairIsOpen(int floor) => !RequireGateKey || !CellarContentsSync.HasGateKeyStair(floor) || _openedStairs.Contains(floor);

        // Host: opens the stair on `floor` for the current dive (idempotent; cleared when a dive starts, ends or fails).
        public void ServerOpenStair(int floor) { if (!IsServer) return; _openedStairs.Add(floor); if (HaulSync != null && HaulSync.Current.CurrentFloor == floor) NetStairOpen.Value = true; }

        private void RefreshStairOpen(int floor) { NetStairOpen.Value = StairIsOpen(floor); }

        // Ends the dive: the party is sent home at once, the floor is cleared a moment later. The caller has already ended the session (go home, finish or fail: the bags are settled, the checkpoint is
        // set when the party went home from the bank); the party then waits in a fresh Town-phase session over the same bags, checkpoint and pieces.
        private bool EndDive(DiveEndReason reason)
        {
            if (!NetActive.Value) return false;
            if (_session != null && _session.Phase != DivePhase.Ended && (reason == DiveEndReason.Failed || reason == DiveEndReason.Abandoned)) HaulSync.ServerFailDive();
            _loafLedger.End();
            NetActive.Value = false; _openedStairs.Clear(); NetStairOpen.Value = true;
            _session = NewSession();
            if (HaulSync != null) HaulSync.ServerAdopt(_session);
            RefreshHub();
            _gen++; _needsPlacement = true; _placeRetry = true;   // placed home on the next frame (after ResetParty below), not up to 0.2 s later
            _clearAt = Time.unscaledTime + FloorClearDelay;
            ContentsClear();   // contents hook: the dive is over (ended, failed or abandoned): its enemies go
            ResetParty();      // wipe: every hero arrives home up and moving, and the combat host forgets the wipe
            DiveEnded?.Invoke(reason);
            return true;
        }

        private void OnBanked() { RefreshHub(); }

        // Host: the party chose to go home at the bank (confirmed). Raised from the press or from the haul sync's timer.
        private void OnWentHome() { if (NetActive.Value) EndDive(DiveEndReason.WentHome); }

        // Host: the party pushed on. The suspend (the next capture writes it: floor 5, push-on chosen, bags as carried) goes to disk now rather than at the next autosave.
        private void OnPushedOn() { RefreshHub(); if (_world != null) _world.RequestSave(); }

        private void OnDiveFailed() { if (NetActive.Value) EndDive(DiveEndReason.Failed); }

        // Host: the bank floor's guardian died: the floor is cleared (feeds the stir) and the bank room opens.
        private void OnGuardianDefeated(int floor)
        {
            if (!IsServer || HaulSync == null || _session == null || _session.Floor != floor) return;
            HaulSync.ServerMarkCleared(floor);
        }

        // Host: a piece or person is safe the moment it is found (spec 4.4), so it goes into the town stock at once, not at the dive's end. ReceiveFound is idempotent.
        private void OnPieceAdded(int pieceId)
        {
            if (!IsServer || HaulSync == null) return;
            if (Town.ReceiveFound(pieceId)) StockReceived?.Invoke(1);
        }

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
                HaulSync.PushedOn += OnPushedOn;
                HaulSync.WentHome += OnWentHome;
                HaulSync.DiveFailed += OnDiveFailed;
                HaulSync.PieceAdded += OnPieceAdded;
                EnsureWorldState();
                HaulSync.ServerAdopt(_session);   // the haul sync runs the director's session (its own spawn-time one is a stand-in for tests that spawn it alone)
            }
            // Contents hook: the host-authoritative floor contents (chests, enemies, gate key). The director arms it per built floor and opens the stair on its ExitUnlocked.
            if (ContentsSync == null && contentsPrefab != null)
            {
                ContentsSync = NetworkManager.SpawnManager.InstantiateAndSpawn(contentsPrefab, NetworkManager.LocalClientId, true, false, false, Vector3.zero, Quaternion.identity).GetComponent<CellarContentsSync>();
                ContentsSync.FloorSync = FloorSync; ContentsSync.HaulSync = HaulSync;
                ContentsSync.AutoArm = false;
                ContentsSync.ExitUnlocked += ServerOpenStair;
                ContentsSync.GuardianDefeated += OnGuardianDefeated;
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

        // Host: a floor was just swapped (the new floor is already built here): every hero is placed in this same frame instead of at the next 0.2 s tick,
        // and PlacementPass keeps retrying every frame until each hero was sent (it defers while FloorSync has not built the requested floor).
        // Tests: pushes the 0.2 s placement tick out so only the swap-frame placement (MarkPlacementNow) can place anyone in time.
        internal void TestDelayPlacement(float seconds) { _nextPlace = Time.unscaledTime + seconds; }
        internal bool TestPlacedHas(ulong clientId) => _placed.ContainsKey(clientId);
        internal bool TestPlacedThisGen(ulong clientId) => _placed.TryGetValue(clientId, out int g) && g == _gen;

        // Host: a dropped client's placement record goes (the map would otherwise grow with every join; client ids are never reused).
        private void OnClientGone(ulong clientId) { _placed.Remove(clientId); }

        private void MarkPlacementNow()
        {
            _gen++; _needsPlacement = true; _placeRetry = true;
            PlacementPass();
            UpdateHold();
        }

        private void PlacementPass()
        {
            _placeRetry = false;
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
                    Vector3 pos = EntryFor(slot);
                    _placed[id] = _gen;
                    TeleportRpc(pos, 0f, FloorSync.BuiltSeed, FloorSync.BuiltFloor, RpcTarget.Single(id, RpcTargetUse.Temp));
                }
                else
                {
                    _placed[id] = _gen;
                    TeleportRpc(HomeFor(slot), 0f, 0u, 0, RpcTarget.Single(id, RpcTargetUse.Temp));
                }
            }
            _placeRetry = pending;
            if (!NetActive.Value && !pending) _needsPlacement = false;   // home: everyone present was sent; later joiners spawn on the hub's spawn point anyway
        }

        // Where player `slot` arrives on the built floor: the layout's entry cells, or the bank room's cell when the dive started at the bank (decision 2; the guardian is not spawned).
        private Vector3 EntryFor(int slot)
        {
            if (_session != null && (_session.StartedAtBank || _session.Phase == DivePhase.BankRoom) && FloorSync.BuiltFloor == UndercroftPlan.BankFloor && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank))
                return bank + new Vector3(0.8f * Mathf.Max(0, slot), 0.1f, 0.8f);
            return CellarFloorBuilder.EntryWorld(FloorSync.Layout, FloorSync.Built, slot);
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

        /// <summary>Shows `text` as the notice on the director of this NetworkManager's peer (CellarContentsSync's owner-only refusals use it). A no-op when that peer has no director.</summary>
        public static void ShowNoticeOnLocal(NetworkManager nm, string text)
        {
            foreach (DiveDirector d in FindObjectsByType<DiveDirector>(FindObjectsSortMode.None))
                if (d.IsSpawned && d.NetworkManager == nm) { d._notice = text; d._noticeUntil = Time.unscaledTime + 3f; return; }
        }

        /// <summary>Owner side: the notice shown right now (a refusal's reason, sent to the asker only), or null. Never set by joining or by a save restore.</summary>
        public string Notice => _notice != null && Time.unscaledTime < _noticeUntil ? _notice : null;

        /// <summary>Owner side: whether a teleport is still waiting for the local floor (tests).</summary>
        public bool HasPendingTeleport => _hasPending;

        private float _testApplyAfter;   // tests: the owner holds back an arrival teleport until this time (opens the gravity-hold window deterministically)
        internal void TestDelayApply(float seconds) { _testApplyAfter = Time.unscaledTime + seconds; }

        private void ApplyPending()
        {
            if (!_hasPending || Time.unscaledTime < _testApplyAfter) return;
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

        // Owner side (host-as-owner and clients): while the dive floor this peer sees (0 = home) differs from the floor of the last teleport applied, the floor under the local hero was
        // swapped but the arrival teleport has not landed, so the hero would sink through cells the new floor lacks. HeroLocomotion.HoldVertical freezes its gravity for that window only:
        // released when the teleport applies (the keys match again), after PendingTimeout (a lost teleport never freezes a hero), and on despawn. Never touches other heroes (owner copy only).
        private void UpdateHold()
        {
            NetworkObject player = NetworkManager != null && NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            HeroLocomotion loco = player != null ? player.GetComponent<HeroLocomotion>() : null;
            if (loco == null) { _hold.Active = false; return; }
            int key = 0;
            if (NetActive.Value)
            {
                ResolveSyncs();
                int haulFloor = HaulSync != null ? HaulSync.Current.CurrentFloor : 0;
                int syncFloor = 0; uint syncSeed = 0u;
                if (FloorSync != null) { var n = FloorSync.Net.Value; syncFloor = n.Floor; syncSeed = n.Seed; }
                key = HandoverHold.Key(true, haulFloor, syncFloor, syncSeed, _appliedFloor, _appliedSeed);   // whichever variable changed first (-1: neither resolved yet)
            }
            bool hold = HandoverHold.Decide(ref _hold, key, _appliedFloor, Time.unscaledTime, PendingTimeout);
            if (loco.HoldVertical != hold) loco.HoldVertical = hold;
        }

        private void ReleaseHold()
        {
            _hold.Active = false;
            NetworkObject player = NetworkManager != null && NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            HeroLocomotion loco = player != null ? player.GetComponent<HeroLocomotion>() : null;
            if (loco != null) loco.HoldVertical = false;
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

        public enum PromptKind : byte { None = 0, Descend = 1, Stair = 2, Bank = 3, GoHome = 4, Finish = 5 }
        public PromptKind Prompt { get; private set; }
        public string PromptText { get; private set; } = "";

        // The hub prompt: the floor the next press starts on. With no checkpoint it is always floor 1; with one, the G key (dev builds) cycles PickableFloors().
        public int PickFloor { get; private set; } = UndercroftPlan.FirstFloor;

        public static string HubPromptText(bool checkpointHeld, IReadOnlyList<int> floors, int pick, bool suspended = false)
        {
            if (suspended) return "Return to your dive at the bank room";
            if (!checkpointHeld && floors.Count <= 1) return "Go down the Undercroft";
            return "Go down the Undercroft: start on floor " + pick + "  (pickable: " + string.Join(", ", floors) + "; G changes the floor)";
        }

        // Host and clients: the pick follows the replicated floors (a pick no longer offered falls back to the first).
        private void ClampPick(IReadOnlyList<int> floors)
        {
            if (floors.Count == 0) { PickFloor = UndercroftPlan.FirstFloor; return; }
            bool ok = false;
            foreach (int f in floors) if (f == PickFloor) { ok = true; break; }
            if (!ok) PickFloor = floors[0];
        }

        public void RequestDescend() { RequestDescendRpc(); }
        public void RequestStartDive(int floor) { RequestStartDiveRpc(floor); }
        public void RequestStair(int fromFloor) { RequestStairRpc(fromFloor); }
        public void RequestGoHome() { RequestGoHomeRpc(); }
        public void RequestFinish() { RequestFinishRpc(); }
        public void RequestBank() { RequestBankRpc(); }

        [Rpc(SendTo.Server)]
        private void RequestDescendRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryDescend(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void RequestStartDiveRpc(int floor, RpcParams rpcParams = default) { RequestsHandled++; ServerTryStartDive(rpcParams.Receive.SenderClientId, floor); }

        [Rpc(SendTo.Server)]
        private void RequestStairRpc(int fromFloor, RpcParams rpcParams = default) { RequestsHandled++; ServerTryStair(rpcParams.Receive.SenderClientId, fromFloor); }

        [Rpc(SendTo.Server)]
        private void RequestBankRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryBank(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void RequestGoHomeRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryGoHome(rpcParams.Receive.SenderClientId); }

        [Rpc(SendTo.Server)]
        private void RequestFinishRpc(RpcParams rpcParams = default) { RequestsHandled++; ServerTryFinish(rpcParams.Receive.SenderClientId); }

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
                IReadOnlyList<int> floors = PickableFloors();
                ClampPick(floors);
                if (Flat(me, NetHubStair.Value, PromptRadius))
                {
                    Prompt = PromptKind.Descend; PromptText = HubPromptText(NetCheckpointHeld.Value, floors, PickFloor, NetSuspendedDive.Value);
                    if (floors.Count > 1 && !NetSuspendedDive.Value && HeroInput.ResolveDefault() is IDevInput cyc && cyc.WasPressed(DevKey.CycleFloor))
                    {
                        int at = 0;
                        for (int i = 0; i < floors.Count; i++) if (floors[i] == PickFloor) at = i;
                        PickFloor = floors[(at + 1) % floors.Count];
                        PromptText = HubPromptText(NetCheckpointHeld.Value, floors, PickFloor, false);
                    }
                }
            }
            else
            {
                ResolveSyncs();
                if (FloorSync == null || FloorSync.Built == null || FloorSync.Layout == null) return;
                floor = FloorSync.BuiltFloor;
                if (_appliedFloor != floor || _appliedSeed != FloorSync.BuiltSeed) return;   // not yet teleported onto this floor: standing where the last one's stair was
                bool chosen = HaulSync != null && HaulSync.Current.BankChosen;
                bool bankCleared = HaulSync != null && (HaulSync.Current.ClearedMask & (1 << (UndercroftPlan.BankFloor - 1))) != 0;
                if (DiveFloorTable.HasStair(floor, chosen) && CellarFloorBuilder.TryDescentWorld(FloorSync.Layout, FloorSync.Built, out Vector3 stair) && Flat(me, stair, PromptRadius))
                { Prompt = PromptKind.Stair; PromptText = NetStairOpen.Value ? "Take the stair down" : "The way down is shut (find the gate key)"; }
                else if (floor == DiveFloorTable.LastBuiltFloor && CellarFloorBuilder.TryEndWorld(FloorSync.Layout, FloorSync.Built, out Vector3 end) && Flat(me, end, PromptRadius))
                { Prompt = PromptKind.Finish; PromptText = "Leave the Undercroft"; }
                else if (DiveFloorTable.HasBank(floor) && !chosen && bankCleared && CellarFloorBuilder.TryBankWorld(FloorSync.Layout, FloorSync.Built, out Vector3 bank) && Flat(me, bank, BankPromptRadius))
                {
                    bool inRoom = HaulSync != null && HaulSync.Current.DivePhase == DivePhase.BankRoom;
                    if (inRoom) { }   // inside the bank room Interact does nothing (a stray press must never push a solo party on): only the bank screen keys (Y push on, N go home) decide
                    else if (Flat(me, bank + new Vector3(CellarFloorBuilder.HomeMarkerOffset, 0f, 0f), 1.0f)) { Prompt = PromptKind.GoHome; PromptText = "Take the party home"; }   // the green column beside the lamp
                    else { Prompt = PromptKind.Bank; PromptText = "Rest in the bank room"; }
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
                case PromptKind.Descend: RequestStartDive(NetCheckpointHeld.Value && !NetSuspendedDive.Value ? PickFloor : UndercroftPlan.FirstFloor); break;
                case PromptKind.Stair: RequestStair(floor); break;
                case PromptKind.Bank: RequestBank(); break;
                case PromptKind.GoHome: RequestGoHome(); break;
                case PromptKind.Finish: RequestFinish(); break;
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

        // =================================================================================================================================
        // The bank screen (plan 3 task 8): the minimum UI that drives the picks and the shared choice while the party is in the bank room. Every peer runs it for its own hero; it only asks (PlayerBagSync
        // RequestPick / RequestUnpick, DiveHaulSync.RequestPushOn, RequestGoHome): the host re-checks everything. Keys are the dev keys of PlayerInputAdapter ([ ] move, P pick, Y push on, N go home) until the
        // input asset gets bank actions, so like the hub's floor cycle they work in development builds only; the draw is IMGUI. Rows: the carried satchel first (when carried), then the carried items.
        // =================================================================================================================================
        public bool BankScreenOpen { get; private set; }
        public int BankCursor { get; private set; }

        private static bool BankRowIsSatchel(BagView v, int row) => v.SatchelCarried && row == 0;
        private static int BankRowCarriedIndex(BagView v, int row) => row - (v.SatchelCarried ? 1 : 0);
        private static int BankRows(BagView v) => v.Carried.Length + (v.SatchelCarried ? 1 : 0);

        private static bool BankRowPicked(BagView v, int row)
        {
            byte pick = BankRowIsSatchel(v, row) ? BagView.SatchelPickByte : (byte)BankRowCarriedIndex(v, row);
            for (int i = 0; i < v.Picks.Length; i++) if (v.Picks[i] == pick) return true;
            return false;
        }

        private void LocalBankScreen()
        {
            BankScreenOpen = false;
            if (!NetActive.Value) return;
            ResolveSyncs();
            if (HaulSync == null || HaulSync.Current.DivePhase != DivePhase.BankRoom) return;
            PlayerBagSync bag = PlayerBagSync.ForLocalPlayer(NetworkManager);
            if (bag == null) return;
            BankScreenOpen = true;
            BagView v = bag.View;
            int rows = BankRows(v);
            BankCursor = rows > 0 ? Mathf.Clamp(BankCursor, 0, rows - 1) : 0;
            IDevInput dev = HeroInput.ResolveDefault() as IDevInput;
            NetworkObject player = NetworkManager.LocalClient != null ? NetworkManager.LocalClient.PlayerObject : null;
            if (dev == null || player == null || LocalHeroIsDown(player)) return;   // a downed hero presses nothing (the host refuses it too)
            if (rows > 0 && dev.WasPressed(DevKey.BankPrev)) BankCursor = (BankCursor + rows - 1) % rows;
            if (rows > 0 && dev.WasPressed(DevKey.BankNext)) BankCursor = (BankCursor + 1) % rows;
            if (rows > 0 && dev.WasPressed(DevKey.BankPick))
            {
                int pick = BankRowIsSatchel(v, BankCursor) ? PlayerBag.SatchelPick : BankRowCarriedIndex(v, BankCursor);
                if (BankRowPicked(v, BankCursor)) bag.RequestUnpick(pick); else bag.RequestPick(pick);
            }
            if (dev.WasPressed(DevKey.BankPushOn)) HaulSync.RequestPushOn();
            if (dev.WasPressed(DevKey.BankGoHome)) RequestGoHome();
        }

        private void DrawBankScreen()
        {
            PlayerBagSync bag = PlayerBagSync.ForLocalPlayer(NetworkManager);
            if (bag == null || HaulSync == null) return;
            BagView v = bag.View;
            DiveHaulSync.Summary sum = HaulSync.Current;
            int rows = BankRows(v);
            float h = 120f + 20f * Mathf.Min(rows, 14);
            var area = new Rect(20f, 80f, 360f, h);
            GUI.Box(area, "Bank room: " + v.Picks.Length + " of " + v.SlotCapacity + " slots picked");
            for (int row = 0; row < Mathf.Min(rows, 14); row++)
            {
                string name = BankRowIsSatchel(v, row) ? "Satchel (" + v.SatchelFloor + ")" : CellarLoot.NameOf(v.Carried[BankRowCarriedIndex(v, row)]);
                GUI.Label(new Rect(area.x + 12f, area.y + 28f + 20f * row, 336f, 20f), (row == BankCursor ? "> " : "   ") + (BankRowPicked(v, row) ? "[x] " : "[ ] ") + name);
            }
            if (rows == 0) GUI.Label(new Rect(area.x + 12f, area.y + 28f, 336f, 20f), "(nothing carried)");
            string choice = "";
            if (sum.ChoiceKind != BankChoiceKind.None)
            {
                int pressed = 0; for (int b = sum.ChoiceMask; b != 0; b >>= 1) pressed += b & 1;
                choice = (sum.ChoiceKind == BankChoiceKind.PushOn ? "Push on" : "Go home") + ": " + pressed + " pressed, " + sum.ChoiceSecondsLeft + " s";
            }
            GUI.Label(new Rect(area.x + 12f, area.y + h - 66f, 336f, 20f), choice);
            GUI.Label(new Rect(area.x + 12f, area.y + h - 46f, 336f, 40f), "[ ] move   P pick   Y push on   N go home\nEmpty slots stay empty; a second player's press, or the timer, confirms.");
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
            ReleaseHold();
            if (_dcMgr != null) { _dcMgr.OnClientDisconnectCallback -= OnClientGone; _dcMgr = null; }
            HeroVitals.ServerHeroSpawned -= OnHeroSpawned;
            _loafLedger.End();
            if (_world != null)
            {
                try { _world.UnregisterTownSource(this); } catch (System.Exception e) { Debug.LogException(e); }
                _world = null;
            }
            if (TownSync != null && IsServer && TownSync.IsSpawned) TownSync.NetworkObject.Despawn(true);
            TownSync = null;
            if (HaulSync != null) { HaulSync.Banked -= OnBanked; HaulSync.PushedOn -= OnPushedOn; HaulSync.WentHome -= OnWentHome; HaulSync.DiveFailed -= OnDiveFailed; HaulSync.PieceAdded -= OnPieceAdded; }
            if (ContentsSync != null) { ContentsSync.ExitUnlocked -= ServerOpenStair; ContentsSync.GuardianDefeated -= OnGuardianDefeated; }
            FloorSync = null; HaulSync = null; ContentsSync = null; _armedBuild = -1;   // a re-spawn re-creates and re-wires the syncs (EnsureSyncs)
            _hasPending = false; _placed.Clear(); _appliedFloor = 0; _appliedSeed = 0u;
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            if (Instance == null || IsServer) Instance = this;   // one process can hold several peers' directors (tests): the host's wins
            if (IsServer && ServiceRoots.App != null && ServiceRoots.App.TryGet(out HostWorld w)) { _world = w; try { w.RegisterTownSource(this); } catch (System.Exception e) { Debug.LogException(e); } }   // a save-layer fault must not stop the spawn
            if (IsServer) { EnsureWorldState(); RefreshHub(); HeroVitals.ServerHeroSpawned += OnHeroSpawned; _dcMgr = NetworkManager; _dcMgr.OnClientDisconnectCallback += OnClientGone; SpawnTownSync(); }
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
            UpdateHold();
            MarkFloor();
            LocalInteract();
            LocalEat();
            LocalBankScreen();
            if (!IsServer) return;
            DevKeys();
            ContentsTick();
            WipeTick();
            if (_clearAt >= 0f && Time.unscaledTime >= _clearAt)
            {
                _clearAt = -1f;
                if (!NetActive.Value && FloorSync != null) FloorSync.ServerSetFloor(_seed, CellarFloorSync.NoFloor);
            }
            if (_placeRetry) PlacementPass();
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
            if (BankScreenOpen) DrawBankScreen();
        }
    }
}
