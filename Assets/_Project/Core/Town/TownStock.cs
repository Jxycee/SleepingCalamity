// Core/Town/TownStock.cs: the town's host-held stock (M6 S3): the TownState (pieces and residents in stock, buildings placed) plus the world-save glue of the new dive (plan 3 tasks 7-8a).
// A piece or person found in the dive is safe at once (ReceiveFound): it goes straight into stock, so "found" is exactly "in stock or placed". Receive() moves a DivePieces' found set (and the
// baker) into stock and is idempotent. CaptureWorld writes the stock, town, residents, dive, bags and suspend sections from the live session; RestoreWorld rebuilds the stock and what the director
// runs with (bags, checkpoint, pieces) from a loaded snapshot; an old save's banked pieces and banked baker come home there. The loot of the old dive (stock.loot, dive.bankedLoot) is no longer
// held here: TownSaves.MigrateLegacyLoot moves it into the first recorded player's stash when the world loads.
// Authority: host only (the instance lives in the host's DiveDirector). Pure C#, no engine references.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class TownStock
    {
        private readonly TownLayout _layout;

        public TownStock(TownLayout layout) { _layout = layout; State = new TownState(layout); }

        public TownState State { get; private set; }
        // Moves every piece the party has found (and the rescued baker) into the town. Returns the number of NEW pieces stocked; running it twice moves nothing twice.
        public int Receive(DivePieces pieces, bool bakerRescued)
        {
            int added = 0;
            if (pieces != null)
                foreach (int id in pieces.FoundIds)
                {
                    if (PieceCatalog.IsPerson(id)) { ReceiveFound(id); continue; }
                    if (State.AddPiece(id) == PlaceResult.Ok) added++;
                }
            if (bakerRescued) State.AddResident(TownBuildings.BakerResident);
            return added;
        }

        // ---- plan 3 task 7: the new dive's world state (DiveSession + the director's persistent bags, checkpoint and pieces) ----
        // A piece or person found in the dive is safe at once (spec 4.4): it goes straight into stock, so "found" is exactly "in stock or placed". False for an id already home.
        public bool ReceiveFound(int id)
        {
            if (PieceCatalog.IsPerson(id)) { string r = PieceCatalog.ResidentOf(id); return r != null && State.AddResident(r) == PlaceResult.Ok; }
            return State.AddPiece(id) == PlaceResult.Ok;
        }

        // True when the town holds the piece or person (stock or placed).
        public bool Holds(int id)
        {
            if (PieceCatalog.IsPerson(id))
            {
                string r = PieceCatalog.ResidentOf(id);
                if (r == null) return false;
                if (State.ResidentInStock(r)) return true;
                foreach (PlacedBuilding b in State.Placed) if (b.Resident == r) return true;
                return false;
            }
            if (State.InStock(id)) return true;
            foreach (PlacedBuilding b in State.Placed) foreach (int p in b.PiecesByPart) if (p == id) return true;
            return false;
        }

        // The sections for the world save from the live session. The session's bags, checkpoint and pieces are the director's persistent ones. Found pieces are already in stock (ReceiveFound), so the
        // dive section's on-floor lists come out right without the old haul. depths: the world's piece depths (written into the dive section so a later save keeps them); extraSlotOwner: the saved
        // previous owner of the 3-player extra slot (a bag id or null). The dive section is marked V2Known so the saved CheckpointHeld, BossBeaten and ExtraSlotOwner never carry over a live value.
        // What the director tells the capture about the bank-room suspend (plan 3 tasks 8 and 8a). Seed / PresentIds / IdsBySlot: the dive seed, the bag ids of the players present and the bag id of
        // each roster slot (null for an empty one), written into a live suspend. Pending: a suspend loaded with the world that nobody has resumed yet (the party waits at the trapdoor); it is
        // re-emitted verbatim until the director uses it, so an autosave in between cannot lose it.
        public sealed class SuspendContext
        {
            public uint Seed;
            public IReadOnlyList<string> PresentIds;
            public IReadOnlyList<string> IdsBySlot;
            public SuspendSave Pending;
        }

        // Every capture from the director says what it knows about the suspend (SuspendKnown is always true): a live bank-room or after-push-on session writes one with its bags saved as carried
        // (BagsSaveRules.Capture suspended: true); no session in a dive but a pending loaded one re-emits it (and marks the dive in progress); anything else says "known, none", which clears
        // the saved copy (a used suspend, a dive that ended, a party that left floor 5). The suspend is set after TownSaves.Capture's own clean (that one knows no dive and no suspend).
        public TownSnapshot CaptureWorld(DiveSession session, bool bossBeaten, string extraSlotOwner, IReadOnlyDictionary<int, int> depths, ResidentsSave residents, SuspendContext suspend = null)
        {
            TownSnapshot snap = TownSaves.Capture(State, null, residents);
            bool inSession = session != null && (session.Phase == DivePhase.Floor || session.Phase == DivePhase.BankRoom);
            SuspendSave pending = !inSession ? suspend?.Pending : null;
            bool inDive = inSession || pending != null;
            if (snap.Dive != null)
            {
                snap.Dive.InDive = inDive;
                snap.Dive.ClearedMask = inSession ? session.Snapshot().ClearedMask : pending != null ? pending.ClearedMask : 0;   // fresh floors every dive: a mask only matters while the party is below
                snap.Dive.CheckpointHeld = session != null && session.Checkpoint.Held;
                snap.Dive.BossBeaten = bossBeaten;
                snap.Dive.ExtraSlotOwner = extraSlotOwner;
                snap.Dive.V2Known = true;
                if (depths != null && depths.Count > 0)
                {
                    snap.Dive.PieceDepths = new List<PieceDepthSave>();
                    foreach (KeyValuePair<int, int> kv in depths) snap.Dive.PieceDepths.Add(new PieceDepthSave { Id = kv.Key, Floor = kv.Value });
                }
            }
            snap.SuspendKnown = true;
            if (session == null) return snap;
            if (inSession && session.SuspendsAtBank)
            {
                var records = new List<SuspendedBag>();
                snap.Bags = BagsSaveRules.Capture(session.Bags, BagKeys(session.Bags), true, session.Floor, true, records);
                snap.Suspend = SuspendSaveRules.Build(session.Snapshot(), suspend != null ? suspend.Seed : 0u, suspend?.PresentIds, suspend?.IdsBySlot, records);
            }
            else if (pending != null)
            {
                snap.Bags = BagsSaveRules.Capture(session.Bags, BagKeys(session.Bags), false, 0);   // the home half; the mid-dive half rides in the pending section
                snap.Suspend = pending;
            }
            else snap.Bags = BagsSaveRules.Capture(session.Bags, BagKeys(session.Bags), inSession, session.Floor);
            return snap;
        }

        public sealed class WorldRestore
        {
            public PartyBags Bags = new PartyBags();
            public DiveCheckpoint Checkpoint = new DiveCheckpoint();
            public DivePieces Pieces;
            public bool BossBeaten;
            public string ExtraSlotOwner;
            public SuspendSave Suspend;   // a loaded bank-room suspend waiting to be resumed (the bags above hold its mid-dive halves); null: none
        }

        // Rebuilds the stock and the town from loaded (cleaned, conserved) sections and returns what the director runs with: each recorded player's gold, stash and waiting satchel, the checkpoint,
        // and the pieces with every one the town holds marked found. An old save's banked haul comes home here (Receive), exactly as Restore did. depths: the world's piece depths.
        public WorldRestore RestoreWorld(TownSnapshot snap, IReadOnlyDictionary<int, int> depths)
        {
            State = TownSaves.ToState(snap, _layout);
            // An old save's pieces banked in the haul but not yet home, and its banked baker, come home now (found pieces are safe at once in the new dive, so nothing new is ever left banked).
            var known = new Dictionary<int, int>();
            if (depths != null) foreach (KeyValuePair<int, int> kv in depths) known[kv.Key] = kv.Value;
            if (snap?.Dive != null) foreach (int id in snap.Dive.BankedPieces) if (!known.ContainsKey(id)) known[id] = 0;
            var banked = new DivePieces(known);
            if (snap?.Dive != null) banked.Restore(snap.Dive.BankedPieces);
            bool baker = snap?.Dive != null && snap.Dive.BankedResidents.Contains(TownBuildings.BakerResident);
            Receive(banked, baker);
            var w = new WorldRestore { Pieces = new DivePieces(depths) };
            if (snap?.Suspend != null) { BagsSaveRules.RestoreSuspended(snap.Bags, snap.Suspend, w.Bags); w.Suspend = snap.Suspend; }   // carried items, picks, dive gold come back with the home half
            else BagsSaveRules.Restore(snap?.Bags, w.Bags);
            if (snap?.Dive != null) { w.Checkpoint.Restore(snap.Dive.CheckpointHeld); w.BossBeaten = snap.Dive.BossBeaten; w.ExtraSlotOwner = snap.Dive.ExtraSlotOwner; }
            var found = new List<int>();
            if (depths != null) foreach (int id in depths.Keys) if (Holds(id)) found.Add(id);
            w.Pieces.Restore(found);
            return w;
        }

        static Dictionary<string, PlayerKey> BagKeys(PartyBags bags)
        {
            var keys = new Dictionary<string, PlayerKey>();
            foreach (KeyValuePair<string, PlayerBag> kv in bags.All)
                if (BagsSaveRules.TryParseBagId(kv.Key, out PlayerKey key)) keys[kv.Key] = key;
            return keys;
        }
    }
}
