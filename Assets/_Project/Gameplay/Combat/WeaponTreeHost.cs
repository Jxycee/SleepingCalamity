// Authority: HOST. Applies the weapon-tree choices a hero's owner asks for (HeroCombat.TreeActionServerRpc) to the host's PlayerGrowth through the Core
// WeaponState API, re-checking the game rules only (slot room, the 90% build-up gate, "has something to re-route", AtWorkbench; no anti-cheat), then
// re-pushes growth and the tree snapshot to the owner. The owner never changes a weapon itself; it only reads the snapshot (HeroCombat.TreeMirror).
// Greybox gaps (M6/M7 work): there is no attachment bag yet, so Fit always has stock, and attachments a build-up or re-route hands back are dropped.
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Gameplay
{
    public enum TreeAction : byte { Fit = 0, Remove = 1, BuildUp = 2, Reroute = 3 }

    public static class WeaponTreeHost
    {
        // Re-route is only allowed at the heroes' home workbench (PHASE1 6.3). No workbench exists in code yet, so the arena greybox is always "at" it;
        // replace this body when the town workbench lands.
        public static bool AtWorkbench(ulong clientId) => true;

        public static WeaponTreeSnapshot Snapshot(CombatHost host, HeroCombat hero)
        {
            WeaponState w = host.Growth(hero.OwnerClientId).Weapon(hero.WeaponKey);
            return WeaponTreeSnapshot.From(w, AtWorkbench(hero.OwnerClientId));
        }

        // Host only. `arg` is an AttachmentKind for Fit / Remove and ignored otherwise. Returns whether the weapon changed.
        public static bool Apply(CombatHost host, HeroCombat hero, TreeAction action, int arg)
        {
            if (host == null || hero == null || !hero.IsSpawned) return false;
            WeaponState w = host.Growth(hero.OwnerClientId).Weapon(hero.WeaponKey);
            bool changed;
            switch (action)
            {
                case TreeAction.Fit: changed = w.Fit((AttachmentKind)arg); break;
                case TreeAction.Remove: changed = w.Remove((AttachmentKind)arg); break;
                case TreeAction.BuildUp: changed = w.TryBuildUp(); break;
                case TreeAction.Reroute: changed = AtWorkbench(hero.OwnerClientId) && w.TryReroute(); break;
                default: changed = false; break;
            }
            host.ServerSyncGrowth(hero);   // always re-push, so a refused click also refreshes a stale panel
            return changed;
        }
    }
}
