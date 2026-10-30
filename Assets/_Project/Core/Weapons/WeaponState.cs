// Core/Weapons/WeaponState.cs: one weapon's growth state and the rules that change it (PHASE1 6.3, P12-P15). Pure C#, no engine.
// Stats are the weapon's PERMANENT stats. A fitted attachment is inert until the next level-up absorbs it (then it is spent and its bonus is
// permanent); PreviewLevelUp shows both outcomes first. Build-up and re-route keep a snapshot stack, so a build-up can always be undone and
// returns the weapon's level, ABS, stats and definition exactly as they were. Fitted attachments are NOT part of that restore: they belong
// to the player, so re-route keeps whatever is fitted now (trimmed to the restored weapon's slots; the trimmed ones are returned to the
// caller to bag). GetReroutePreview tells the UI what a re-route will cost.
// Authority: host (it applies kills and workbench actions); the owner only reads the previews.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Weapons
{
    public readonly struct LevelUpPreview
    {
        public readonly int NextLevel;
        public readonly WeaponStats WithAbsorb;      // stats after the next level-up with the fitted attachments absorbed (what really happens)
        public readonly WeaponStats WithoutAbsorb;   // stats after the next level-up if the slots were empty (remove them first)
        public readonly float AbsToNext;             // ABS still missing for it
        public LevelUpPreview(int nextLevel, WeaponStats with, WeaponStats without, float absToNext)
        { NextLevel = nextLevel; WithAbsorb = with; WithoutAbsorb = without; AbsToNext = absToNext; }
    }

    // Each fraction is value / target, capped at 1; a stat the target does not ask for reads 1. Available = every asked stat is >= 90%.
    public readonly struct BuildUpProgress
    {
        public readonly bool HasTarget, Available;
        public readonly float Atk, Fire, Pest;
        public BuildUpProgress(bool hasTarget, bool available, float atk, float fire, float pest)
        { HasTarget = hasTarget; Available = available; Atk = atk; Fire = fire; Pest = pest; }
    }

    // What a re-route would do, for the UI to warn with.
    public readonly struct ReroutePreview
    {
        public readonly bool Possible;
        public readonly string RestoredDefinitionId;
        public readonly WeaponStats RestoredStats;
        public readonly WeaponStats AbsorbedSinceBuildUp;   // attachment bonuses absorbed after the build-up: gone with the re-route
        public readonly WeaponStats StatsLost;              // everything the weapon has now beyond the restored stats (>= the absorbed part)
        public ReroutePreview(bool possible, string id, WeaponStats restored, WeaponStats absorbed, WeaponStats lost)
        { Possible = possible; RestoredDefinitionId = id; RestoredStats = restored; AbsorbedSinceBuildUp = absorbed; StatsLost = lost; }
    }

    // What re-route restores.
    public sealed class BuildUpSnapshot
    {
        public string DefinitionId;
        public int Level;
        public float Abs;
        public WeaponStats Stats;
        public AttachmentKind[] Fitted;              // the fitted set at build-up time (kept for the record; re-route does not restore it)
        public WeaponStats AbsorbedSince;            // attachment bonuses absorbed after this build-up
    }

    public sealed class WeaponState
    {
        // Placeholder safety cap: a level past it is never reached (it also keeps a hostile ABS amount from running away).
        public const int MaxLevel = 9999;

        readonly WeaponCatalog _catalog;
        readonly List<AttachmentKind> _fitted = new List<AttachmentKind>();
        readonly List<BuildUpSnapshot> _history = new List<BuildUpSnapshot>();

        public string DefinitionId { get; private set; }
        public int Level { get; private set; }
        public float Abs { get; private set; }            // ABS inside the current level (0 up to the level cost)
        public WeaponStats Stats { get; private set; }
        public int LifetimeLevels { get; private set; }   // every level ever gained; never lowered by build-up or re-route
        public int BuildUpsEver { get; private set; }     // every build-up ever made; never lowered by re-route
        public IReadOnlyList<AttachmentKind> Fitted => _fitted;
        public IReadOnlyList<BuildUpSnapshot> History => _history;

        public WeaponState(WeaponDefinition def, WeaponCatalog catalog = null)
        {
            _catalog = catalog ?? WeaponCatalog.Default;
            DefinitionId = def.Id;
            Stats = def.Base;
        }

        public static WeaponState NewHeirloom(string id, WeaponCatalog catalog = null)
        {
            catalog = catalog ?? WeaponCatalog.Default;
            if (!catalog.TryGet(id, out var def)) throw new ArgumentException("unknown weapon " + id);
            return new WeaponState(def, catalog);
        }

        public WeaponDefinition Definition => _catalog.TryGet(DefinitionId, out var d) ? d : null;
        public float LevelCostNow => WeaponGrowth.LevelCost(Level);
        public float AbsFill => Abs / LevelCostNow;   // 0..1 for the ABS bar

        // Spell pip cap for this weapon: grows with lifetime levels and build-ups, so it never drops on a build-up or a re-route.
        public int SpellPipCap => SpellPips.CapFor(LifetimeLevels, BuildUpsEver);

        // ---- ABS and levelling -------------------------------------------------------------------------------------------------------

        // Adds ABS (non-positive and non-finite amounts are ignored), levelling up as often as it pays for (closed form, so any amount is
        // instant). Returns the levels gained. The first level-up absorbs every fitted attachment; later levels from the same gain find the
        // slots empty. At MaxLevel the bar stays empty.
        public int AddAbs(float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount) || Level >= MaxLevel) return 0;
            double a = (double)Abs + amount;
            // Cost of n levels from level L: sum of 40 + 20(L+k) = 10 n^2 + (30 + 20 L) n.
            double b = 30.0 + 20.0 * Level;
            double Total(long n) => 10.0 * n * n + b * n;
            double estimate = Math.Floor((-b + Math.Sqrt(b * b + 40.0 * a)) / 20.0);
            long levels = estimate >= MaxLevel ? MaxLevel : (long)estimate;
            if (levels < MaxLevel)
            {
                while (levels > 0 && Total(levels) > a) levels--;
                while (levels < MaxLevel && Total(levels + 1) <= a) levels++;
            }
            if (levels >= MaxLevel - Level)
            {
                levels = MaxLevel - Level;
                a = 0.0;   // the bar stays empty at the cap
            }
            else a -= Total(levels);
            if (levels > 0) ApplyLevels((int)levels);
            Abs = (float)Math.Max(0.0, a);
            return (int)levels;
        }

        void ApplyLevels(int n)
        {
            var absorbed = default(WeaponStats);
            foreach (var a in _fitted) absorbed += Attachments.Bonus(a);
            Stats += absorbed;
            if (_history.Count > 0) _history[_history.Count - 1].AbsorbedSince += absorbed;
            _fitted.Clear();
            int from = Level;
            int two = Math.Max(0, Math.Min(from + n, WeaponGrowth.FlatAtkFromLevel) - from);   // levels gaining +2
            Stats += new WeaponStats(2f * two + (n - two));
            Level += n;
            LifetimeLevels += n;
        }

        // Pure: the next level-up's result, with and without the fitted attachments. WithAbsorb equals the real Stats after a level-up.
        public LevelUpPreview PreviewLevelUp()
        {
            var without = Stats + new WeaponStats(WeaponGrowth.AtkGain(Level + 1));
            var with = Stats;
            foreach (var a in _fitted) with += Attachments.Bonus(a);
            with += new WeaponStats(WeaponGrowth.AtkGain(Level + 1));
            return new LevelUpPreview(Level + 1, with, without, LevelCostNow - Abs);
        }

        // ---- Attachments --------------------------------------------------------------------------------------------------------------

        public int SlotCount => Definition?.SlotCount ?? 0;

        public bool Fit(AttachmentKind kind)
        {
            if (!Attachments.IsValid(kind) || _fitted.Count >= SlotCount) return false;
            _fitted.Add(kind);
            return true;
        }

        // Free to remove, any time (the drop goes back to the player's bag; that is the caller's job).
        public bool Remove(AttachmentKind kind) => _fitted.Remove(kind);

        // ---- Build-up and re-route ----------------------------------------------------------------------------------------------------

        public BuildUpProgress GetBuildUpProgress()
        {
            var def = Definition;
            if (def == null || def.BuildUpTargetId == null || !_catalog.TryGet(def.BuildUpTargetId, out var t))
                return new BuildUpProgress(false, false, 0f, 0f, 0f);
            Check(Stats.Atk, t.Base.Atk, out float fa, out bool oa);
            Check(Stats.Fire, t.Base.Fire, out float ff, out bool of);
            Check(Stats.Pest, t.Base.Pest, out float fp, out bool op);
            return new BuildUpProgress(true, oa && of && op, fa, ff, fp);
        }

        // value >= 90% of target, compared as value x 10 >= target x 9 so the threshold is exact for whole-number stats.
        static void Check(float value, float target, out float fraction, out bool ok)
        {
            if (target <= 0f) { fraction = 1f; ok = true; return; }
            fraction = Math.Min(1f, value / target);
            ok = value * 10f >= target * 9f;
        }

        public bool CanBuildUp => GetBuildUpProgress().Available;

        public bool TryBuildUp() => TryBuildUp(out _);

        // Level -> 0, ABS keeps its fill ratio, the weapon becomes the target and gains +10% of the target's base stats. Fitted attachments
        // stay (up to the target's slots); `dropped` lists the ones that no longer fit, for the caller to bag. The old state is pushed for
        // re-route.
        public bool TryBuildUp(out List<AttachmentKind> dropped)
        {
            dropped = new List<AttachmentKind>();
            if (!CanBuildUp) return false;
            _catalog.TryGet(Definition.BuildUpTargetId, out var target);

            _history.Add(new BuildUpSnapshot
            {
                DefinitionId = DefinitionId, Level = Level, Abs = Abs, Stats = Stats, Fitted = _fitted.ToArray(),
            });
            float fill = AbsFill;
            DefinitionId = target.Id;
            Level = 0;
            Abs = fill * WeaponGrowth.LevelCost(0);
            Stats += target.Base * WeaponGrowth.BuildUpBonus;
            BuildUpsEver++;
            Trim(target.SlotCount, dropped);
            return true;
        }

        void Trim(int slots, List<AttachmentKind> dropped)
        {
            if (_fitted.Count <= slots) return;
            for (int i = slots; i < _fitted.Count; i++) dropped.Add(_fitted[i]);
            _fitted.RemoveRange(slots, _fitted.Count - slots);
        }

        public bool CanReroute => _history.Count > 0;

        public ReroutePreview GetReroutePreview()
        {
            if (_history.Count == 0) return new ReroutePreview(false, null, default, default, default);
            var s = _history[_history.Count - 1];
            return new ReroutePreview(true, s.DefinitionId, s.Stats, s.AbsorbedSince, new WeaponStats(
                Math.Max(0f, Stats.Atk - s.Stats.Atk), Math.Max(0f, Stats.Fire - s.Stats.Fire), Math.Max(0f, Stats.Pest - s.Stats.Pest)));
        }

        public bool TryReroute() => TryReroute(out _);

        // Undo the latest build-up: level, ABS, stats and definition return exactly as they were (progress made since is not kept). The
        // attachments fitted now stay fitted, trimmed to the restored weapon's slots; the trimmed ones are returned in `dropped`.
        public bool TryReroute(out List<AttachmentKind> dropped)
        {
            dropped = new List<AttachmentKind>();
            if (_history.Count == 0) return false;
            var s = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            DefinitionId = s.DefinitionId; Level = s.Level; Abs = s.Abs; Stats = s.Stats;
            Trim(SlotCount, dropped);
            return true;
        }

        // ---- Save --------------------------------------------------------------------------------------------------------------------

        public WeaponSave ToSave(string key = "", string ownerId = "")
        {
            var s = new WeaponSave
            {
                Key = key ?? "", OwnerId = ownerId ?? "", DefinitionId = DefinitionId, Level = Level, Abs = Abs,
                Atk = Stats.Atk, Fire = Stats.Fire, Pest = Stats.Pest, LifetimeLevels = LifetimeLevels, BuildUpsEver = BuildUpsEver,
            };
            foreach (var a in _fitted) s.Fitted.Add((int)a);
            foreach (var h in _history)
            {
                var hs = new BuildUpSnapshotSave
                {
                    DefinitionId = h.DefinitionId, Level = h.Level, Abs = h.Abs, Atk = h.Stats.Atk, Fire = h.Stats.Fire, Pest = h.Stats.Pest,
                    AbsorbedAtk = h.AbsorbedSince.Atk, AbsorbedFire = h.AbsorbedSince.Fire, AbsorbedPest = h.AbsorbedSince.Pest,
                };
                foreach (var a in h.Fitted) hs.Fitted.Add((int)a);
                s.History.Add(hs);
            }
            return s;
        }

        // Rebuilds a weapon from a (cleaned) save; false when its definition is unknown.
        public static bool TryFromSave(WeaponSave s, WeaponCatalog catalog, out WeaponState state)
        {
            state = null;
            catalog = catalog ?? WeaponCatalog.Default;
            if (s == null || !catalog.TryGet(s.DefinitionId, out var def)) return false;
            var w = new WeaponState(def, catalog) { Level = s.Level, Abs = s.Abs, Stats = new WeaponStats(s.Atk, s.Fire, s.Pest) };
            w.LifetimeLevels = Math.Max(s.LifetimeLevels, s.Level);
            w.BuildUpsEver = Math.Max(s.BuildUpsEver, s.History?.Count ?? 0);
            if (s.Fitted != null) foreach (int a in s.Fitted) w._fitted.Add((AttachmentKind)a);
            if (s.History != null)
                foreach (var h in s.History)
                {
                    var snap = new BuildUpSnapshot
                    {
                        DefinitionId = h.DefinitionId, Level = h.Level, Abs = h.Abs, Stats = new WeaponStats(h.Atk, h.Fire, h.Pest),
                        AbsorbedSince = new WeaponStats(h.AbsorbedAtk, h.AbsorbedFire, h.AbsorbedPest),
                        Fitted = new AttachmentKind[h.Fitted?.Count ?? 0],
                    };
                    for (int i = 0; i < snap.Fitted.Length; i++) snap.Fitted[i] = (AttachmentKind)h.Fitted[i];
                    w._history.Add(snap);
                }
            state = w;
            return true;
        }
    }
}
