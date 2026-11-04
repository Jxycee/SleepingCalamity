// Core/Save/BagsSave.cs: the optional world-save section "bags" (per-player loot plan task 3, spec decision 39) and its rules. Pure C#, no Unity types.
// Each recorded player's home gold, home stash (item id -> count) and waiting lost satchel. Nothing else of a bag is saved: carried items, bank picks, dive gold and the opened-chest
// mask are dive state. A bag captured DURING a dive is written as if the dive failed at the current floor (PlayerBag.CopyAsFailed): banked items go to the stash, half the dive gold
// is kept, the carried items become the waiting satchel, a carried satchel is lost. That matches TownSaves.Reconcile, which already turns a mid-dive save into a failed dive, so
// loading it gives the same bag. The live bag is never changed by capturing it.
// The bag key is the same string CombatHost.PlayerIdOf uses (PlayerKey.ToString(), "Kind:Id"). Capture writes only bags whose id is in keyOf (recorded players); the client-id
// fallback bags of a session with no roster are never saved.
// Clean costs only the bad entry (an unknown identity, a repeated or oversized id, a player past WorldPlayers.MaxRecorded, a bad stash row or satchel), never the section.
// A world with no bags writes no "bags" key (IsPristine). Unknown members are kept in Extra.
// Authority: host only (the host's world save).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SleepingCalamity.Core
{
    [Serializable]
    public sealed class SatchelSave
    {
        public int Floor;                                   // 1..DiveRoute.LastFloor: the floor where the player fell
        public List<int> Items = new List<int>();           // item ids, at most PlayerBag.MaxCarried
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class PlayerBagSave
    {
        public int Identity = -1;                           // IdentityKind as a number; no valid default (an entry without it is damaged)
        public string Id = "";                              // PlayerKey.Id
        public int Gold;                                    // home gold, 0..TownSaveRules.MaxLootCount
        public List<LootCountSave> Stash = new List<LootCountSave>();   // the player's own home storage
        public SatchelSave Satchel;                         // the waiting lost satchel; null: none
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class BagsSave
    {
        public List<PlayerBagSave> Players = new List<PlayerBagSave>();
        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    public static class BagsSaveRules
    {
        static Dictionary<string, object> CopyExtra(Dictionary<string, object> e) => e != null ? new Dictionary<string, object>(e) : null;

        // A fresh world's value (and a save whose players all have empty bags): not worth writing into a file that has no such section.
        public static bool IsPristine(BagsSave s) => s == null || (s.Players.Count == 0 && s.Extra == null);

        static bool TryKind(int identity, out IdentityKind kind)
        {
            kind = IdentityKind.Local;
            if (identity < byte.MinValue || identity > byte.MaxValue || !Enum.IsDefined(typeof(IdentityKind), (byte)identity)) return false;
            kind = (IdentityKind)identity;
            return true;
        }

        // The PlayerKey a bag id stands for ("Kind:Id", the PlayerKey.ToString form with a kind NAME). False for a client-id fallback id, an unknown kind or an invalid id.
        public static bool TryParseBagId(string bagId, out PlayerKey key)
        {
            key = default;
            if (string.IsNullOrEmpty(bagId)) return false;
            int colon = bagId.IndexOf(':');
            if (colon <= 0) return false;
            string name = bagId.Substring(0, colon);
            if (!Enum.TryParse(name, false, out IdentityKind kind) || !Enum.IsDefined(typeof(IdentityKind), kind) || kind.ToString() != name) return false;
            var parsed = new PlayerKey(kind, bagId.Substring(colon + 1));
            if (!parsed.IsValid) return false;
            key = parsed;
            return true;
        }

        // ---------------------------------------------------------------- clean
        // A valid copy of raw (an empty section for null). Each dropped entry adds "bags.<id>"; a bad stash row or satchel inside a kept entry adds "bags.<id>.stash" / ".satchel".
        // Clamps (gold, counts, a satchel's length) are repairs and do not add a label.
        public static BagsSave Clean(BagsSave raw, List<string> dropped)
        {
            var clean = new BagsSave();
            if (raw == null) return clean;
            clean.Extra = CopyExtra(raw.Extra);
            if (raw.Players == null) return clean;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerBagSave e in raw.Players)
            {
                string label = "bags." + (e?.Id ?? "null");
                if (e == null || !TryKind(e.Identity, out IdentityKind kind) || !new PlayerKey(kind, e.Id).IsValid) { dropped?.Add(label); continue; }
                if (!seen.Add(e.Identity + ":" + e.Id)) { dropped?.Add(label); continue; }
                if (clean.Players.Count >= WorldPlayers.MaxRecorded) { dropped?.Add(label); continue; }
                var c = new PlayerBagSave { Identity = e.Identity, Id = e.Id, Gold = Math.Max(0, Math.Min(TownSaveRules.MaxLootCount, e.Gold)), Extra = CopyExtra(e.Extra) };
                c.Stash = CleanStash(e.Stash, dropped, label + ".stash");
                c.Satchel = CleanSatchel(e.Satchel, dropped, label + ".satchel");
                clean.Players.Add(c);
            }
            return clean;
        }

        static List<LootCountSave> CleanStash(List<LootCountSave> src, List<string> dropped, string label)
        {
            var byItem = new SortedDictionary<int, long>();
            Dictionary<int, Dictionary<string, object>> extra = null;
            if (src != null)
                foreach (LootCountSave r in src)
                {
                    if (r == null || r.ItemId <= 0 || r.Count <= 0) { dropped?.Add(label); continue; }
                    byItem.TryGetValue(r.ItemId, out long have);
                    byItem[r.ItemId] = Math.Min(TownSaveRules.MaxLootCount, have + r.Count);
                    if (r.Extra != null) { if (extra == null) extra = new Dictionary<int, Dictionary<string, object>>(); extra[r.ItemId] = CopyExtra(r.Extra); }
                }
            var list = new List<LootCountSave>();
            foreach (KeyValuePair<int, long> kv in byItem)
                list.Add(new LootCountSave { ItemId = kv.Key, Count = (int)kv.Value, Extra = extra != null && extra.TryGetValue(kv.Key, out var x) ? x : null });
            return list;
        }

        static SatchelSave CleanSatchel(SatchelSave src, List<string> dropped, string label)
        {
            if (src == null) return null;
            if (src.Floor < DiveRoute.FirstFloor || src.Floor > DiveRoute.LastFloor || src.Items == null) { dropped?.Add(label); return null; }
            var items = new List<int>();
            bool lostSome = false;
            foreach (int id in src.Items)
            {
                if (id <= 0 || items.Count >= PlayerBag.MaxCarried) { lostSome = true; continue; }
                items.Add(id);
            }
            if (lostSome) dropped?.Add(label);
            if (items.Count == 0) { if (!lostSome) dropped?.Add(label); return null; }
            return new SatchelSave { Floor = src.Floor, Items = items, Extra = CopyExtra(src.Extra) };
        }

        // A Solo world rebinding to its owner's new key: `from`'s entry moves to `to` (an existing entry of `to` is replaced), so the owner keeps their gold, stash and satchel.
        public static void Rename(BagsSave save, PlayerKey from, PlayerKey to)
        {
            if (save?.Players == null || from == to) return;
            bool has = false;
            foreach (PlayerBagSave p in save.Players) if (p != null && p.Identity == (int)from.Kind && p.Id == from.Id) { has = true; break; }
            if (!has) return;
            save.Players.RemoveAll(p => p != null && p.Identity == (int)to.Kind && p.Id == to.Id);
            foreach (PlayerBagSave p in save.Players)
                if (p != null && p.Identity == (int)from.Kind && p.Id == from.Id) { p.Identity = (int)to.Kind; p.Id = to.Id; }
        }

        // ---------------------------------------------------------------- live <-> save
        // The section for the host's live bags. keyOf maps a bag id to the recorded player it belongs to; a bag whose id is not in it is not written. inDive: the party is below
        // (currentFloor 1-4), so every bag is written as the failed copy at that floor. Bags with nothing to keep (no gold, no stash, no waiting satchel) are not written.
        // The result is a fresh object that shares nothing with the live bags.
        public static BagsSave Capture(PartyBags bags, IReadOnlyDictionary<string, PlayerKey> keyOf, bool inDive, int currentFloor)
        {
            var save = new BagsSave();
            if (bags == null || keyOf == null) return save;
            foreach (KeyValuePair<string, PlayerBag> kv in bags.All)
            {
                if (!keyOf.TryGetValue(kv.Key, out PlayerKey key)) continue;
                PlayerBag bag = inDive ? kv.Value.CopyAsFailed(currentFloor) : kv.Value;
                LostSatchel waiting = bag.Satchel.Waiting;
                if (bag.Gold <= 0 && bag.Stash.Count == 0 && waiting == null) continue;
                var p = new PlayerBagSave { Identity = (int)key.Kind, Id = key.Id, Gold = bag.Gold };
                var ids = new List<int>(bag.Stash.Keys);
                ids.Sort();
                foreach (int id in ids) if (bag.Stash[id] > 0) p.Stash.Add(new LootCountSave { ItemId = id, Count = bag.Stash[id] });
                if (waiting != null) p.Satchel = new SatchelSave { Floor = waiting.Floor, Items = new List<int>(waiting.Items) };
                save.Players.Add(p);
            }
            return BagsSaveRules.Clean(save, null);
        }

        // Puts the (cleaned) saved bags into `into`, keyed by PlayerKey.ToString(). An entry that does not clean is skipped. No dive is in progress: carried items, picks and dive gold are empty.
        public static void Restore(BagsSave save, PartyBags into)
        {
            if (save == null || into == null) return;
            foreach (PlayerBagSave e in Clean(save, null).Players)
            {
                var key = new PlayerKey((IdentityKind)e.Identity, e.Id);
                var stash = new Dictionary<int, int>();
                foreach (LootCountSave r in e.Stash) stash[r.ItemId] = r.Count;
                LostSatchel waiting = e.Satchel != null ? new LostSatchel(e.Satchel.Floor, e.Satchel.Items) : null;
                into.Adopt(key.ToString(), PlayerBag.Restore(e.Gold, stash, waiting));
            }
        }
    }
}
