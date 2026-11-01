// Core/Combat/GrowthSaves.cs: PlayerGrowth <-> the world save's weapons section (M6 S0). Pure C#. A player's growth is stored under their OwnerId
// (PlayerKey.Id): each weapon as a WeaponSave whose Key is "<ownerId>/<weaponKey>" (a save's weapon keys are unique across the whole section),
// plus one HeroSave with the unlocked abilities and spell pips. The style ledger and tech caps are per-enemy and transient, so they are not saved.
// Authority: host only (the host's CombatHost captures and restores; a client never sees the save).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Core.Combat
{
    public static class GrowthSaves
    {
        public const string KeySeparator = "/";

        // Replaces everything `into` holds for ownerId with g's current state.
        public static void Capture(PlayerGrowth g, string ownerId, WeaponsSave into)
        {
            if (g == null || into == null || string.IsNullOrEmpty(ownerId)) return;
            Remove(into, ownerId);
            string prefix = ownerId + KeySeparator;
            foreach (KeyValuePair<string, WeaponState> kv in g.Weapons) into.Weapons.Add(kv.Value.ToSave(prefix + kv.Key, ownerId));
            into.Heroes.Add(new HeroSave { OwnerId = ownerId, Abilities = g.Abilities.ToSave(), Pips = g.Pips.ToSave() });
        }

        // Drops every weapon and the hero entry of ownerId.
        public static void Remove(WeaponsSave save, string ownerId)
        {
            if (save == null) return;
            save.Weapons.RemoveAll(w => w != null && w.OwnerId == ownerId);
            save.Heroes.RemoveAll(h => h != null && h.OwnerId == ownerId);
        }

        // Moves ownerId's entries to newOwnerId (a Solo world rebinding to its owner's new key). An existing entry of the new owner is replaced.
        public static void Rename(WeaponsSave save, string ownerId, string newOwnerId)
        {
            if (save == null || string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(newOwnerId) || ownerId == newOwnerId) return;
            bool has = false;
            foreach (var w in save.Weapons) if (w != null && w.OwnerId == ownerId) { has = true; break; }
            foreach (var h in save.Heroes) if (h != null && h.OwnerId == ownerId) { has = true; break; }
            if (!has) return;
            Remove(save, newOwnerId);
            string oldPrefix = ownerId + KeySeparator, newPrefix = newOwnerId + KeySeparator;
            foreach (var w in save.Weapons)
                if (w != null && w.OwnerId == ownerId)
                {
                    w.OwnerId = newOwnerId;
                    if (w.Key != null && w.Key.StartsWith(oldPrefix, StringComparison.Ordinal)) w.Key = newPrefix + w.Key.Substring(oldPrefix.Length);
                }
            foreach (var h in save.Heroes) if (h != null && h.OwnerId == ownerId) h.OwnerId = newOwnerId;
        }

        // The growth of ownerId from a (cleaned) save, or null when the save holds nothing for them (the caller then starts fresh heirlooms).
        // A weapon the save lacks stays its fresh heirloom. Saved unlocks come back as saved (CombatHost.ServerSyncGrowth re-checks milestones later).
        public static PlayerGrowth Restore(string playerId, string ownerId, WeaponsSave save, WeaponCatalog catalog = null)
        {
            if (save == null || string.IsNullOrEmpty(ownerId)) return null;
            string prefix = ownerId + KeySeparator;
            PlayerGrowth g = null;
            foreach (var w in save.Weapons)
            {
                if (w == null || w.OwnerId != ownerId) continue;
                if (!WeaponState.TryFromSave(w, catalog, out WeaponState state)) continue;
                string weaponKey = w.Key != null && w.Key.StartsWith(prefix, StringComparison.Ordinal) ? w.Key.Substring(prefix.Length) : w.Key;
                if (string.IsNullOrEmpty(weaponKey)) continue;
                if (g == null) g = new PlayerGrowth(playerId);
                g.Weapons[weaponKey] = state;
            }
            foreach (var h in save.Heroes)
            {
                if (h == null || h.OwnerId != ownerId) continue;
                if (g == null) g = new PlayerGrowth(playerId);
                g.Abilities.RestoreSave(h.Abilities);
                g.ReplacePips(SpellPips.FromSave(h.Pips));
                break;
            }
            return g;
        }
    }
}
