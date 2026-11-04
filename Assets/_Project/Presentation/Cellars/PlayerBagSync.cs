// Authority: host writes, owner reads. The host builds BagView.From(its PlayerBag for this object's owner) every frame and writes Net only when it changed (Net is a server-write NetworkVariable
// with ReadPermission.Owner: only the owner and the host receive it, a late joiner gets its own with the spawn and everyone else's as default). Decision 39: loot is shown only to its owner.
// The owner asks to move carried items into bank slots by RPC (RequestPick / RequestUnpick -> SendTo.Server); the host refuses any request whose sender is not this object's owner, then
// calls DiveHaulSync.ServerPick / ServerUnpick, which re-check everything (the asker is up, the floor has a bank, the asker is in range) and the bag re-checks capacity and item kinds.
// Clients never write Net. With no DiveHaulSync (town, tests) the host writes default.
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SleepingCalamity.Core;

namespace SleepingCalamity.Presentation
{
    // One player's bag as the owner sees it. Item ids ride as ushort (an id above 65535 shows as 0 = unknown, so indexes stay aligned with the host's bag); a pick is a carried index,
    // or SatchelPickByte for the carried satchel.
    public struct BagView : INetworkSerializable, IEquatable<BagView>
    {
        public const byte SatchelPickByte = 255;

        public FixedList128Bytes<ushort> Carried;   // up to Capacity (63) ids; PlayerBag.MaxCarried is 60
        public FixedList32Bytes<byte> Picks;        // up to Capacity (30); the bank visit holds at most 3
        public int Gold, DiveGold, StashTotal, BankedThisDive, SlotCapacity;   // StashTotal and BankedThisDive count items, not kinds
        public bool BankOpen, SatchelWaiting, SatchelCarried;
        public byte SatchelFloor;                   // the floor of the carried satchel, else of the waiting one, else 0
        public uint ChestSeed;
        public int ChestFloor;
        public ulong OpenedChests;

        public static BagView From(PlayerBag bag)
        {
            var v = new BagView();
            if (bag == null) return v;
            for (int i = 0; i < bag.Carried.Count && v.Carried.Length < v.Carried.Capacity; i++)
            {
                int id = bag.Carried[i];
                v.Carried.Add(id > 0 && id <= ushort.MaxValue ? (ushort)id : (ushort)0);
            }
            for (int i = 0; i < bag.Picks.Count && v.Picks.Length < v.Picks.Capacity; i++)
            {
                int p = bag.Picks[i];
                if (p == PlayerBag.SatchelPick) v.Picks.Add(SatchelPickByte);
                else if (p >= 0 && p < SatchelPickByte) v.Picks.Add((byte)p);
            }
            v.Gold = bag.Gold; v.DiveGold = bag.DiveGold;
            v.StashTotal = Total(bag.Stash); v.BankedThisDive = Total(bag.BankedThisDive);
            v.SlotCapacity = bag.SlotCapacity; v.BankOpen = bag.BankOpen;
            LostSatchel carried = bag.Satchel.Carried, waiting = bag.Satchel.Waiting;
            v.SatchelCarried = carried != null; v.SatchelWaiting = waiting != null;
            LostSatchel shown = carried ?? waiting;
            v.SatchelFloor = shown == null ? (byte)0 : (byte)Mathf.Clamp(shown.Floor, 0, 255);
            v.ChestSeed = bag.ChestSeed; v.ChestFloor = bag.ChestFloor; v.OpenedChests = bag.OpenedChests;
            return v;
        }

        private static int Total(System.Collections.Generic.IReadOnlyDictionary<int, int> counts)
        {
            long sum = 0;
            foreach (System.Collections.Generic.KeyValuePair<int, int> kv in counts) sum += kv.Value;
            return (int)Math.Min(sum, int.MaxValue);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            byte n = (byte)Carried.Length;
            s.SerializeValue(ref n);
            if (s.IsReader) Carried.Clear();
            for (int i = 0; i < n; i++)
            {
                ushort id = s.IsWriter ? Carried[i] : (ushort)0;
                s.SerializeValue(ref id);
                if (s.IsReader && Carried.Length < Carried.Capacity) Carried.Add(id);   // a longer list than we hold cannot come from From(); read it through so the stream stays aligned
            }
            byte m = (byte)Picks.Length;
            s.SerializeValue(ref m);
            if (s.IsReader) Picks.Clear();
            for (int i = 0; i < m; i++)
            {
                byte p = s.IsWriter ? Picks[i] : (byte)0;
                s.SerializeValue(ref p);
                if (s.IsReader && Picks.Length < Picks.Capacity) Picks.Add(p);
            }
            s.SerializeValue(ref Gold); s.SerializeValue(ref DiveGold); s.SerializeValue(ref StashTotal);
            s.SerializeValue(ref BankedThisDive); s.SerializeValue(ref SlotCapacity);
            s.SerializeValue(ref BankOpen); s.SerializeValue(ref SatchelWaiting); s.SerializeValue(ref SatchelCarried);
            s.SerializeValue(ref SatchelFloor);
            s.SerializeValue(ref ChestSeed); s.SerializeValue(ref ChestFloor); s.SerializeValue(ref OpenedChests);
        }

        public bool Equals(BagView o)
        {
            if (Carried.Length != o.Carried.Length || Picks.Length != o.Picks.Length) return false;
            for (int i = 0; i < Carried.Length; i++) if (Carried[i] != o.Carried[i]) return false;
            for (int i = 0; i < Picks.Length; i++) if (Picks[i] != o.Picks[i]) return false;
            return Gold == o.Gold && DiveGold == o.DiveGold && StashTotal == o.StashTotal && BankedThisDive == o.BankedThisDive && SlotCapacity == o.SlotCapacity
                && BankOpen == o.BankOpen && SatchelWaiting == o.SatchelWaiting && SatchelCarried == o.SatchelCarried && SatchelFloor == o.SatchelFloor
                && ChestSeed == o.ChestSeed && ChestFloor == o.ChestFloor && OpenedChests == o.OpenedChests;
        }

        public override bool Equals(object obj) => obj is BagView o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Gold;
                h = h * 31 + DiveGold; h = h * 31 + StashTotal; h = h * 31 + BankedThisDive; h = h * 31 + SlotCapacity;
                h = h * 31 + Carried.Length;
                for (int i = 0; i < Carried.Length; i++) h = h * 31 + Carried[i];
                h = h * 31 + Picks.Length;
                for (int i = 0; i < Picks.Length; i++) h = h * 31 + Picks[i];
                h = h * 31 + (BankOpen ? 1 : 0) + (SatchelWaiting ? 2 : 0) + (SatchelCarried ? 4 : 0);
                h = h * 31 + SatchelFloor; h = h * 31 + (int)ChestSeed; h = h * 31 + ChestFloor; h = h * 31 + (int)OpenedChests;
                return h;
            }
        }
    }

    public sealed class PlayerBagSync : NetworkBehaviour
    {
        public readonly NetworkVariable<BagView> Net = new NetworkVariable<BagView>(default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

        public BagView View => Net.Value;

        // The owner's instance on this peer (set when the owned object spawns here, cleared when it despawns). With several peers in one process (tests) the last one spawned wins.
        public static PlayerBagSync Local { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Local = null; }

        // Host only: pick/unpick requests received over the RPC, accepted or refused (lets a test wait for a refusal to land).
        public int RequestsHandled { get; private set; }

        private DiveHaulSync _haul;          // host only; the haul sync of this object's own NetworkManager
        private float _nextHaulLookup;       // the scene search runs at most once a second while no haul sync is known (town, haul-less tests)

        public override void OnNetworkSpawn()
        {
            if (IsOwner) Local = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Local == this) Local = null;
            _haul = null;
        }

        // Asks the host to move carried item `index` (PlayerBag.SatchelPick = -1 for the carried satchel) into a bank slot. The host decides.
        public void RequestPick(int index) { if (IsSpawned) RequestPickRpc(index); }
        public void RequestUnpick(int index) { if (IsSpawned) RequestUnpickRpc(index); }

        [Rpc(SendTo.Server)]
        private void RequestPickRpc(int index, RpcParams rpcParams = default)
        {
            RequestsHandled++;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;   // only the owner moves items in this bag
            DiveHaulSync haul = ResolveHaul(true);
            if (haul != null) haul.ServerPick(OwnerClientId, index);
        }

        [Rpc(SendTo.Server)]
        private void RequestUnpickRpc(int index, RpcParams rpcParams = default)
        {
            RequestsHandled++;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            DiveHaulSync haul = ResolveHaul(true);
            if (haul != null) haul.ServerUnpick(OwnerClientId, index);
        }

        // The host's DiveHaulSync (not a client copy of it: in-process test peers each hold one). Cached; searched for at most once a second unless `force`.
        private DiveHaulSync ResolveHaul(bool force)
        {
            if (_haul != null && _haul.IsSpawned) return _haul;
            _haul = null;
            if (!force && Time.unscaledTime < _nextHaulLookup) return null;
            _nextHaulLookup = Time.unscaledTime + 1f;
            foreach (DiveHaulSync h in FindObjectsByType<DiveHaulSync>(FindObjectsSortMode.None))
                if (h.IsSpawned && h.IsServer && h.NetworkManager == NetworkManager) { _haul = h; break; }
            return _haul;
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;
            DiveHaulSync haul = ResolveHaul(false);
            BagView next = haul == null ? default : BagView.From(haul.ServerBagIfAny(OwnerClientId));   // no bag yet shows as default and never creates one
            if (!next.Equals(Net.Value)) Net.Value = next;   // only send when something the owner can see changed
        }
    }
}
