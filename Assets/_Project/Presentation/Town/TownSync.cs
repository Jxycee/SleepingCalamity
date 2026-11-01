// Authority: HOST. M6 S4 town replication, the builder's lock and the placement requests. The host's TownState (DiveDirector.Town.State, the writer) is the only truth; this object publishes it
// as ONE host-written NetworkVariable (NetTown, an INetworkSerializable snapshot of the stock, the placed buildings, their turns, parts and residents), so a late joiner gets the whole town
// with the spawn and no RPC is ever replayed. Every peer, the host included, rebuilds a local TownState (View) from NetTown, and TownView / the wish panel read only View: one source.
//   Builder lock (Q2 A, one builder at a time): NetBuilder (host-written ulong, NoBuilder = nobody). The host grants RequestBuildLockRpc if free, clears it on ReleaseBuildLockRpc from the
//   holder AND when the holder disconnects (OnClientDisconnectCallback). Releasing also asks the world save to write (HostWorld.RequestSave): "save on leaving placement mode".
//   Requests are [Rpc(SendTo.Server)]; the sender is RpcParams.Receive.SenderClientId (never an argument). The host checks sender == lock holder, then re-validates with the Core TownState
//   rules on its own state (CanPlaceFrame incl. the path refusal, PlacePart, PlaceResident, SetTurn and the pick-ups) and republishes. A client-side check is only a prompt.
//   Ghost pose: the holder sends (building, foundation, turn, active); the host mirrors it in NetGhost (NetworkVariable, host-written). Every peer evaluates WishEvaluator.Evaluate(layout,
//   building, foundation, turn) locally from it, so watchers see the ghost and the wish ticks live.
//   Results: the host answers the sender with ResultRpc (one-shot feedback, never replayed on late join: the town itself lives in NetTown).
// Spawned by the host's DiveDirector (a dynamic network prefab, like DiveHaulSync): late joiners get it with the spawn, no scene sync involved.
using System;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    // The whole town in one network value. Wraps the pure TownWire; arrays are written length-first.
    public struct TownNet : INetworkSerializable, IEquatable<TownNet>
    {
        public TownWire Wire;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            if (s.IsWriter && Wire == null) Wire = new TownWire();
            if (s.IsReader) Wire = new TownWire();
            Wire.StockPieces = SerializeInts(s, Wire.StockPieces);
            int nr = Wire.StockResidents.Length; s.SerializeValue(ref nr);
            if (s.IsReader) Wire.StockResidents = new string[nr];
            for (int i = 0; i < nr; i++) { string r = Wire.StockResidents[i] ?? ""; s.SerializeValue(ref r); Wire.StockResidents[i] = r; }
            int nb = Wire.Buildings.Length; s.SerializeValue(ref nb);
            if (s.IsReader) Wire.Buildings = new TownWire.Building[nb];
            for (int i = 0; i < nb; i++)
            {
                if (s.IsReader) Wire.Buildings[i] = new TownWire.Building();
                TownWire.Building b = Wire.Buildings[i];
                string id = b.BuildingId ?? "", lot = b.FoundationId ?? "", res = b.Resident ?? ""; int turn = b.Turn;
                s.SerializeValue(ref id); s.SerializeValue(ref lot); s.SerializeValue(ref res); s.SerializeValue(ref turn);
                b.BuildingId = id; b.FoundationId = lot; b.Resident = res; b.Turn = turn;
                b.Parts = SerializeInts(s, b.Parts);
            }
        }

        private static int[] SerializeInts<T>(BufferSerializer<T> s, int[] values) where T : IReaderWriter
        {
            int n = values != null ? values.Length : 0; s.SerializeValue(ref n);
            var arr = s.IsReader ? new int[n] : (values ?? new int[0]);
            for (int i = 0; i < n; i++) { int v = arr[i]; s.SerializeValue(ref v); arr[i] = v; }
            return arr;
        }

        public bool Equals(TownNet o) => (Wire == null ? "" : Wire.Signature()) == (o.Wire == null ? "" : o.Wire.Signature());
        public override bool Equals(object obj) => obj is TownNet o && Equals(o);
        public override int GetHashCode() => Wire == null ? 0 : Wire.Signature().GetHashCode();
    }

    // The holder's ghost, mirrored by the host.
    public struct GhostNet : INetworkSerializable, IEquatable<GhostNet>
    {
        public bool Active;
        public FixedString32Bytes Building, Foundation;
        public int Turn;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Active); s.SerializeValue(ref Building); s.SerializeValue(ref Foundation); s.SerializeValue(ref Turn);
        }

        public bool Equals(GhostNet o) => Active == o.Active && Building == o.Building && Foundation == o.Foundation && Turn == o.Turn;
        public override bool Equals(object obj) => obj is GhostNet o && Equals(o);
        public override int GetHashCode() => unchecked(Turn * 31 + Foundation.GetHashCode());
    }

    public sealed class TownSync : NetworkBehaviour
    {
        public const ulong NoBuilder = ulong.MaxValue;
        public const byte NotBuilder = 200, NoTown = 201;   // ResultRpc codes beside PlaceResult (0-17)
        public const int MaxArg = 24;                        // longest id string a request may carry

        public enum Op : byte { Lock, Release, Ghost, PlaceFrame, SetTurn, PlacePart, PlaceResident, PickUpBuilding, PickUpPart, PickUpResident }

        public static TownSync Local { get; private set; }

        public readonly NetworkVariable<TownNet> NetTown = new NetworkVariable<TownNet>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<ulong> NetBuilder = new NetworkVariable<ulong>(NoBuilder, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<GhostNet> NetGhost = new NetworkVariable<GhostNet>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public DiveDirector Director;   // host only (set by the director that spawned this)

        private TownLayout _layout;
        private TownState _view;        // every peer: rebuilt from NetTown
        private string _viewSig = "!";
        private string _publishedSig = "!";
        private float _nextPoll;

        public event Action<Op, byte> ResultReceived;   // this peer's own requests, answered by the host
        public event Action ViewChanged;

        public TownLayout Layout => _layout ?? (_layout = HollowmereLayout.Create());
        // The replicated town as this peer sees it (identical on every peer once the variable has arrived).
        public TownState View { get { EnsureView(); return _view; } }
        public bool IsBuilder => IsSpawned && NetBuilder.Value == NetworkManager.LocalClientId;
        public bool BuilderIsFree => NetBuilder.Value == NoBuilder;

        // ------------------------------------------------------------------ lifecycle
        public override void OnNetworkSpawn()
        {
            if (Local == null || IsServer) Local = this;
            NetTown.OnValueChanged += OnTownChanged;
            if (IsServer) NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            EnsureView();
        }

        public override void OnNetworkDespawn()
        {
            NetTown.OnValueChanged -= OnTownChanged;
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            if (Local == this) Local = null;
        }

        private void OnTownChanged(TownNet previous, TownNet current) { ApplyWire(current.Wire); }

        private void EnsureView()
        {
            if (_view == null) ApplyWire(NetTown.Value.Wire);
        }

        private void ApplyWire(TownWire wire)
        {
            string sig = wire == null ? "" : wire.Signature();
            if (_view != null && sig == _viewSig) return;
            _view = (wire ?? new TownWire()).ToState(Layout);
            _viewSig = sig;
            ViewChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + 0.2f;
            ServerRefresh();   // the dive's go-home stocks the town outside any request: this catches it
        }

        // ------------------------------------------------------------------ host: publish
        private TownState HostState => Director != null ? Director.Town.State : null;

        // Host: publishes the host TownState to NetTown when it differs from what is published (cheap compare of the stable signature).
        public bool ServerRefresh()
        {
            if (!IsServer || !IsSpawned || HostState == null) return false;
            TownWire wire = TownWire.From(HostState);
            string sig = wire.Signature();
            if (sig == _publishedSig) return false;
            _publishedSig = sig;
            NetTown.Value = new TownNet { Wire = wire };
            ApplyWire(wire);   // the host reads the same replicated shape it published
            return true;
        }

        // ------------------------------------------------------------------ the lock
        public bool ServerIsBuilder(ulong who) => IsServer && NetBuilder.Value == who && who != NoBuilder;

        private bool ServerAcquire(ulong who)
        {
            if (!IsServer || !IsSpawned || HostState == null || !NetworkManager.ConnectedClients.ContainsKey(who)) return false;
            if (NetBuilder.Value == who) return true;
            if (NetBuilder.Value != NoBuilder) return false;
            NetBuilder.Value = who;
            NetGhost.Value = default;
            return true;
        }

        private bool ServerRelease(ulong who)
        {
            if (!ServerIsBuilder(who)) return false;
            NetBuilder.Value = NoBuilder;
            NetGhost.Value = default;
            ServerRefresh();
            if (ServiceRoots.App != null && ServiceRoots.App.TryGet(out HostWorld world)) world.RequestSave();   // leaving placement mode saves the world
            return true;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsServer && NetBuilder.Value == clientId) ServerRelease(clientId);   // a holder who drops frees the lock
        }

        // ------------------------------------------------------------------ requests (any peer; the host answers)
        public void RequestBuildLock() => RequestBuildLockRpc();
        public void ReleaseBuildLock() => ReleaseBuildLockRpc();
        public void SendGhost(string buildingId, string foundationId, int turn, bool active) => SetGhostRpc(buildingId ?? "", foundationId ?? "", turn, active);
        public void RequestPlaceFrame(string buildingId, string foundationId, int turn) => PlaceFrameRpc(buildingId ?? "", foundationId ?? "", turn);
        public void RequestSetTurn(string buildingId, int turn) => SetTurnRpc(buildingId ?? "", turn);
        public void RequestPlacePart(string buildingId, string part) => PlacePartRpc(buildingId ?? "", part ?? "");
        public void RequestPlaceResident(string buildingId) => PlaceResidentRpc(buildingId ?? "");
        public void RequestPickUpBuilding(string buildingId) => PickUpBuildingRpc(buildingId ?? "");
        public void RequestPickUpPart(string buildingId, string part) => PickUpPartRpc(buildingId ?? "", part ?? "");
        public void RequestPickUpResident(string buildingId) => PickUpResidentRpc(buildingId ?? "");

        [Rpc(SendTo.Server)]
        private void RequestBuildLockRpc(RpcParams rpcParams = default)
        {
            ulong who = rpcParams.Receive.SenderClientId;
            Reply(who, Op.Lock, ServerAcquire(who) ? (byte)PlaceResult.Ok : NotBuilder);
        }

        [Rpc(SendTo.Server)]
        private void ReleaseBuildLockRpc(RpcParams rpcParams = default)
        {
            ulong who = rpcParams.Receive.SenderClientId;
            Reply(who, Op.Release, ServerRelease(who) ? (byte)PlaceResult.Ok : NotBuilder);
        }

        [Rpc(SendTo.Server)]
        private void SetGhostRpc(string buildingId, string foundationId, int turn, bool active, RpcParams rpcParams = default)
        {
            ulong who = rpcParams.Receive.SenderClientId;
            if (!ServerIsBuilder(who)) return;
            if (!active || !Fits(buildingId) || !Fits(foundationId) || TownBuildings.Get(buildingId) == null || Layout.Foundation(foundationId) == null) { NetGhost.Value = default; return; }
            NetGhost.Value = new GhostNet { Active = true, Building = buildingId, Foundation = foundationId, Turn = WishEvaluator.NormalTurn(turn) };
        }

        [Rpc(SendTo.Server)]
        private void PlaceFrameRpc(string buildingId, string foundationId, int turn, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PlaceFrame, s => Fits(buildingId) && Fits(foundationId) ? s.PlaceFrame(buildingId, foundationId, turn) : PlaceResult.UnknownBuilding);

        [Rpc(SendTo.Server)]
        private void SetTurnRpc(string buildingId, int turn, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.SetTurn, s => Fits(buildingId) ? s.SetTurn(buildingId, turn) : PlaceResult.UnknownBuilding);

        [Rpc(SendTo.Server)]
        private void PlacePartRpc(string buildingId, string part, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PlacePart, s => Fits(buildingId) && Fits(part) ? s.PlacePart(buildingId, part) : PlaceResult.UnknownPart);

        [Rpc(SendTo.Server)]
        private void PlaceResidentRpc(string buildingId, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PlaceResident, s => Fits(buildingId) ? s.PlaceResident(buildingId) : PlaceResult.UnknownBuilding);

        [Rpc(SendTo.Server)]
        private void PickUpBuildingRpc(string buildingId, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PickUpBuilding, s => Fits(buildingId) && s.PickUpBuilding(buildingId) ? PlaceResult.Ok : PlaceResult.NotPlaced);

        [Rpc(SendTo.Server)]
        private void PickUpPartRpc(string buildingId, string part, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PickUpPart, s => Fits(buildingId) && Fits(part) && s.PickUpPart(buildingId, part) ? PlaceResult.Ok : PlaceResult.UnknownPart);

        [Rpc(SendTo.Server)]
        private void PickUpResidentRpc(string buildingId, RpcParams rpcParams = default)
            => Mutate(rpcParams, Op.PickUpResident, s => Fits(buildingId) && s.PickUpResident(buildingId) ? PlaceResult.Ok : PlaceResult.NotPlaced);

        private static bool Fits(string s) => s != null && s.Length <= MaxArg;

        // Host: sender must hold the lock; the rule runs on the host's own TownState; an Ok republishes the town.
        private void Mutate(RpcParams rpcParams, Op op, Func<TownState, PlaceResult> rule)
        {
            ulong who = rpcParams.Receive.SenderClientId;
            if (!ServerIsBuilder(who)) { Reply(who, op, NotBuilder); return; }
            TownState state = HostState;
            if (state == null) { Reply(who, op, NoTown); return; }
            PlaceResult result = rule(state);
            if (result == PlaceResult.Ok) { NetGhost.Value = default; ServerRefresh(); }
            Reply(who, op, (byte)result);
        }

        private void Reply(ulong who, Op op, byte result)
        {
            if (who == NetworkManager.LocalClientId) { ResultReceived?.Invoke(op, result); return; }
            ResultRpc((byte)op, result, RpcTarget.Single(who, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ResultRpc(byte op, byte result, RpcParams rpcParams)
        {
            ResultReceived?.Invoke((Op)op, result);
        }
    }
}
