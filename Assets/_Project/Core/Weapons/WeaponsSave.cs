// Core/Weapons/WeaponsSave.cs: the save shape of the weapons section and its clean rule (PHASE1 6.3). Follows the WorldSave pattern: plain
// public fields, no Unity types, [JsonExtensionData] Extra on every type so an older build writes back what a newer one added, and a clean
// rule that costs only the bad entry. Wired into WorldSave as the optional "weapons" section (M6 S0).
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
        public List<HeroSave> Heroes = new List<HeroSave>();   // per player: unlocked abilities and spell pips (M6 S0)

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    // One player's non-weapon growth, keyed by OwnerId (PlayerKey.Id) like their weapons.
    [Serializable]
    public sealed class HeroSave
    {
        public string OwnerId = "";
        public HeroAbilitiesSave Abilities = new HeroAbilitiesSave();
        public SpellPipsSave Pips = new SpellPipsSave();

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class WeaponSave
    {
        public string Key = "";            // the weapon's stable id in the world (never empty in a valid save)
        public string OwnerId = "";        // PlayerKey.Id of the player whose weapon it is
        public string DefinitionId = "";
        public int Level;
        public float Abs;
        public float Atk, Fire, Pest;
        public int LifetimeLevels, BuildUpsEver;                         // spell pip cap inputs; never lowered
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
        public float AbsorbedAtk, AbsorbedFire, AbsorbedPest;   // bonuses absorbed since this build-up
        public List<int> Fitted = new List<int>();
        public List<int> AbsorbedKinds = new List<int>();       // AttachmentKind numbers absorbed since this build-up (refunded by re-route)

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    // The hero's unlocked ability ids (known and unknown alike, so a newer build's abilities survive an older build's save).
    [Serializable]
    public sealed class HeroAbilitiesSave
    {
        public List<string> Unlocked = new List<string>();

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    [Serializable]
    public sealed class SpellPipsSave
    {
        public int Cap = SpellPips.StartCap, Current = SpellPips.StartCap, HitProgress;

        [JsonExtensionData] public Dictionary<string, object> Extra;
    }

    public static class WeaponsSaveRules
    {
        // Upper bound for LifetimeLevels and BuildUpsEver in a loaded save; far past anything SpellPips.MaxCap can show, so a damaged number
        // cannot overflow CapFor's arithmetic.
        public const int MaxCounter = 1000000;

        // A valid copy of the section (an empty one for null), nothing thrown.
        //  rejected: keys of weapons dropped whole (null, empty or repeated key, unknown definition, bad numbers); an empty key is named "".
        //  repaired: keys of weapons kept but fixed (attachments trimmed or invalid ones dropped, a bar reset, bad history entries cut).
        // Both lists are cleared first and may be null. Only valid weapons count toward the repeated-key rule.
        // History is oldest first and re-route pops the newest, so a bad entry makes every OLDER entry unreachable: that entry and the older
        // ones are dropped, the newer valid ones are kept.
        public static WeaponsSave Clean(WeaponsSave save, WeaponCatalog catalog, List<string> rejected, List<string> repaired = null)
        {
            rejected?.Clear();
            repaired?.Clear();
            catalog = catalog ?? WeaponCatalog.Default;
            var clean = new WeaponsSave();
            if (save == null) return clean;
            var seen = new HashSet<string>();
            if (save.Weapons != null)
                foreach (var w in save.Weapons)
                {
                    if (w == null || string.IsNullOrEmpty(w.Key) || !catalog.TryGet(w.DefinitionId, out var def)
                        || !Sane(w.Level, w.Abs, w.Atk, w.Fire, w.Pest) || !seen.Add(w.Key))
                    {
                        rejected?.Add(w?.Key ?? "");
                        continue;
                    }
                    bool fixedUp = false;
                    var c = new WeaponSave
                    {
                        Key = w.Key, OwnerId = w.OwnerId ?? "", DefinitionId = w.DefinitionId, Level = w.Level, Abs = w.Abs,
                        Atk = w.Atk, Fire = w.Fire, Pest = w.Pest,
                        LifetimeLevels = Math.Min(MaxCounter, Math.Max(Math.Max(0, w.LifetimeLevels), w.Level)),
                        BuildUpsEver = Math.Min(MaxCounter, Math.Max(0, w.BuildUpsEver)),
                        Fitted = CleanFitted(w.Fitted, def.SlotCount, ref fixedUp),
                        Extra = w.Extra != null ? new Dictionary<string, object>(w.Extra) : null,
                    };
                    if (c.Abs >= WeaponGrowth.LevelCost(c.Level)) { c.Abs = 0f; fixedUp = true; }   // damaged bar: empty it
                    if (w.History != null)
                        foreach (var h in w.History)
                        {
                            if (h == null || !catalog.TryGet(h.DefinitionId, out var hdef) || !Sane(h.Level, h.Abs, h.Atk, h.Fire, h.Pest)
                                || !Finite(h.AbsorbedAtk) || !Finite(h.AbsorbedFire) || !Finite(h.AbsorbedPest))
                            {
                                c.History.Clear();   // this entry and the older ones are unreachable
                                fixedUp = true;
                                continue;
                            }
                            float habs = h.Abs;
                            if (habs >= WeaponGrowth.LevelCost(h.Level)) { habs = 0f; fixedUp = true; }
                            c.History.Add(new BuildUpSnapshotSave
                            {
                                DefinitionId = h.DefinitionId, Level = h.Level, Abs = habs, Atk = h.Atk, Fire = h.Fire, Pest = h.Pest,
                                AbsorbedAtk = Math.Max(0f, h.AbsorbedAtk), AbsorbedFire = Math.Max(0f, h.AbsorbedFire), AbsorbedPest = Math.Max(0f, h.AbsorbedPest),
                                Fitted = CleanFitted(h.Fitted, hdef.SlotCount, ref fixedUp),
                                AbsorbedKinds = CleanKinds(h.AbsorbedKinds, ref fixedUp),
                                Extra = h.Extra != null ? new Dictionary<string, object>(h.Extra) : null,
                            });
                        }
                    c.BuildUpsEver = Math.Max(c.BuildUpsEver, c.History.Count);
                    if (fixedUp) repaired?.Add(c.Key);
                    clean.Weapons.Add(c);
                }
            if (save.Heroes != null)
            {
                var owners = new HashSet<string>();
                foreach (var h in save.Heroes)
                {
                    if (h == null || string.IsNullOrEmpty(h.OwnerId) || !owners.Add(h.OwnerId)) continue;   // a bad hero entry costs only itself
                    clean.Heroes.Add(new HeroSave
                    {
                        OwnerId = h.OwnerId, Abilities = CleanAbilities(h.Abilities), Pips = CleanPips(h.Pips),
                        Extra = h.Extra != null ? new Dictionary<string, object>(h.Extra) : null,
                    });
                }
            }
            if (save.Extra != null) clean.Extra = new Dictionary<string, object>(save.Extra);
            return clean;
        }

        static bool Sane(int level, float abs, float atk, float fire, float pest) =>
            level >= 0 && level <= WeaponState.MaxLevel && Finite(abs) && abs >= 0f && Finite(atk) && atk >= 0f && Finite(fire) && fire >= 0f
            && Finite(pest) && pest >= 0f;

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        static List<int> CleanFitted(List<int> fitted, int slots, ref bool changed)
        {
            var r = new List<int>();
            if (fitted == null) return r;
            foreach (int a in fitted)
            {
                if (r.Count >= slots || !Attachments.IsValid((AttachmentKind)a)) { changed = true; continue; }
                r.Add(a);
            }
            return r;
        }

        // Valid attachment numbers only (any count up to MaxAbsorbedKinds: three slots per level-up, so this is generous).
        const int MaxAbsorbedKinds = 3000;
        static List<int> CleanKinds(List<int> kinds, ref bool changed)
        {
            var r = new List<int>();
            if (kinds == null) return r;
            foreach (int a in kinds)
            {
                if (r.Count >= MaxAbsorbedKinds || !Attachments.IsValid((AttachmentKind)a)) { changed = true; continue; }
                r.Add(a);
            }
            return r;
        }

        // The hero's abilities: nulls and empty ids dropped, repeats merged. Unknown ids are kept (HeroAbilityBook carries them).
        public static HeroAbilitiesSave CleanAbilities(HeroAbilitiesSave save)
        {
            var clean = new HeroAbilitiesSave();
            if (save == null) return clean;
            var seen = new HashSet<string>();
            if (save.Unlocked != null)
                foreach (var id in save.Unlocked)
                    if (!string.IsNullOrEmpty(id) && seen.Add(id)) clean.Unlocked.Add(id);
            if (save.Extra != null) clean.Extra = new Dictionary<string, object>(save.Extra);
            return clean;
        }

        public static SpellPipsSave CleanPips(SpellPipsSave save)
        {
            if (save == null) return new SpellPipsSave();
            int cap = Math.Max(0, Math.Min(save.Cap, SpellPips.MaxCap));
            return new SpellPipsSave
            {
                Cap = cap, Current = Math.Max(0, Math.Min(save.Current, cap)), HitProgress = Math.Max(0, Math.Min(save.HitProgress, SpellPips.HitsPerPip - 1)),
                Extra = save.Extra != null ? new Dictionary<string, object>(save.Extra) : null,
            };
        }
    }
}
