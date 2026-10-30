// Core/Weapons/WeaponsSave.cs: the save shape of the weapons section and its clean rule (PHASE1 6.3). Follows the WorldSave pattern: plain
// public fields, no Unity types, [JsonExtensionData] Extra on every type so an older build writes back what a newer one added, and a clean
// rule that costs only the bad entry. NOT wired into WorldSave yet (M6); today it is only round-trip tested.
// Authority: host only (part of the host's world save).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SleepingCalamity.Core.Weapons
{
    [Serializable]
    public sealed class WeaponsSave
    {
        public List<WeaponSave> Weapons = new List<WeaponSave>();

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class WeaponSave
    {
        public string Key = "";            // the weapon's stable id in the world
        public string OwnerId = "";        // PlayerKey.Id of the player whose weapon it is
        public string DefinitionId = "";
        public int Level;
        public float Abs;
        public float Atk, Fire, Pest;
        public List<int> Fitted = new List<int>();                       // AttachmentKind numbers
        public List<BuildUpSnapshotSave> History = new List<BuildUpSnapshotSave>();   // oldest first; re-route pops the last

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class BuildUpSnapshotSave
    {
        public string DefinitionId = "";
        public int Level;
        public float Abs;
        public float Atk, Fire, Pest;
        public List<int> Fitted = new List<int>();

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    public static class WeaponsSaveRules
    {
        // A valid copy of the section (an empty one for null). A weapon with an unknown definition, a bad number or a repeated key is dropped
        // alone and its key named in rejected (cleared first; may be null). Fitted attachments that are unknown or exceed the slots are
        // dropped from that weapon; a weapon whose history entry is bad loses that entry's whole history (older ones cannot be reached).
        public static WeaponsSave Clean(WeaponsSave save, WeaponCatalog catalog, List<string> rejected)
        {
            rejected?.Clear();
            catalog = catalog ?? WeaponCatalog.Default;
            var clean = new WeaponsSave();
            if (save == null) return clean;
            var seen = new HashSet<string>();
            if (save.Weapons != null)
                foreach (var w in save.Weapons)
                {
                    if (w == null || !seen.Add(w.Key ?? "") || !catalog.TryGet(w.DefinitionId, out var def)
                        || !Sane(w.Level, w.Abs, w.Atk, w.Fire, w.Pest))
                    {
                        rejected?.Add(w?.Key ?? "");
                        continue;
                    }
                    var c = new WeaponSave
                    {
                        Key = w.Key ?? "", OwnerId = w.OwnerId ?? "", DefinitionId = w.DefinitionId, Level = w.Level, Abs = w.Abs,
                        Atk = w.Atk, Fire = w.Fire, Pest = w.Pest, Fitted = CleanFitted(w.Fitted, def.SlotCount),
                        Extra = w.Extra != null ? new Dictionary<string, object>(w.Extra) : null,
                    };
                    // Abs is held inside the level: at or past the cost means the save is damaged, so the bar resets to empty.
                    float cost = WeaponGrowth.LevelCost(c.Level);
                    if (c.Abs >= cost) c.Abs = 0f;
                    if (w.History != null)
                        foreach (var h in w.History)
                        {
                            if (h == null || !catalog.TryGet(h.DefinitionId, out var hdef) || !Sane(h.Level, h.Abs, h.Atk, h.Fire, h.Pest))
                            {
                                c.History.Clear();
                                rejected?.Add(c.Key);
                                break;
                            }
                            c.History.Add(new BuildUpSnapshotSave
                            {
                                DefinitionId = h.DefinitionId, Level = h.Level, Abs = h.Abs, Atk = h.Atk, Fire = h.Fire, Pest = h.Pest,
                                Fitted = CleanFitted(h.Fitted, hdef.SlotCount),
                                Extra = h.Extra != null ? new Dictionary<string, object>(h.Extra) : null,
                            });
                        }
                    clean.Weapons.Add(c);
                }
            if (save.Extra != null) clean.Extra = new Dictionary<string, object>(save.Extra);
            return clean;
        }

        static bool Sane(int level, float abs, float atk, float fire, float pest) =>
            level >= 0 && level < 10000 && Finite(abs) && abs >= 0f && Finite(atk) && atk >= 0f && Finite(fire) && fire >= 0f && Finite(pest) && pest >= 0f;

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        static List<int> CleanFitted(List<int> fitted, int slots)
        {
            var r = new List<int>();
            if (fitted == null) return r;
            foreach (int a in fitted)
            {
                if (r.Count >= slots) break;
                if (Attachments.IsValid((AttachmentKind)a)) r.Add(a);
            }
            return r;
        }
    }
}
