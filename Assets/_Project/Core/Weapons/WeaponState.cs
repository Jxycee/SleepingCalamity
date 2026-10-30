// Core/Weapons/WeaponState.cs: one weapon's growth state and the rules that change it (PHASE1 6.3, P12-P15). Pure C#, no engine.
// Stats are the weapon's PERMANENT stats. A fitted attachment is inert until the next level-up absorbs it (then it is spent and its bonus is
// permanent); PreviewLevelUp shows both outcomes first. Build-up and re-route keep a snapshot stack, so a build-up can always be undone and
// returns the weapon exactly as it was (level, ABS, stats, fitted attachments, definition).
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

    // What re-route restores.
    public sealed class BuildUpSnapshot
    {
        public string DefinitionId;
        public int Level;
        public float Abs;
        public WeaponStats Stats;
        public AttachmentKind[] Fitted;
    }

    public sealed class WeaponState
    {
        readonly WeaponCatalog _catalog;
        readonly List<AttachmentKind> _fitted = new List<AttachmentKind>();
        readonly List<BuildUpSnapshot> _history = new List<BuildUpSnapshot>();

        public string DefinitionId { get; private set; }
        public int Level { get; private set; }
        public float Abs { get; private set; }            // ABS inside the current level (0 up to the level cost)
        public WeaponStats Stats { get; private set; }
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

        // ---- ABS and levelling -------------------------------------------------------------------------------------------------------

        // Adds ABS (non-positive and non-finite amounts are ignored), levelling up as often as it pays for. Returns the levels gained.
        // The first level-up absorbs every fitted attachment; later levels from the same gain find the slots empty.
        public int AddAbs(float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount)) return 0;
            Abs += amount;
            int gained = 0;
            while (Abs >= LevelCostNow)
            {
                Abs -= LevelCostNow;
                LevelUp();
                gained++;
            }
            return gained;
        }

        void LevelUp()
        {
            foreach (var a in _fitted) Stats += Attachments.Bonus(a);
            _fitted.Clear();
            Level++;
            Stats += new WeaponStats(WeaponGrowth.AtkGain(Level));
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

        // Level -> 0, ABS keeps its fill ratio, the weapon becomes the target and gains +10% of the target's base stats. Fitted attachments
        // stay (up to the target's slots). The old state is pushed for re-route.
        public bool TryBuildUp()
        {
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
            if (_fitted.Count > target.SlotCount) _fitted.RemoveRange(target.SlotCount, _fitted.Count - target.SlotCount);
            return true;
        }

        public bool CanReroute => _history.Count > 0;

        // Undo the latest build-up: the weapon returns exactly as it was before it (progress made since is not kept).
        public bool TryReroute()
        {
            if (_history.Count == 0) return false;
            var s = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            DefinitionId = s.DefinitionId; Level = s.Level; Abs = s.Abs; Stats = s.Stats;
            _fitted.Clear();
            _fitted.AddRange(s.Fitted);
            return true;
        }

        // ---- Save --------------------------------------------------------------------------------------------------------------------

        public WeaponSave ToSave(string key = "", string ownerId = "")
        {
            var s = new WeaponSave
            {
                Key = key ?? "", OwnerId = ownerId ?? "", DefinitionId = DefinitionId, Level = Level, Abs = Abs,
                Atk = Stats.Atk, Fire = Stats.Fire, Pest = Stats.Pest,
            };
            foreach (var a in _fitted) s.Fitted.Add((int)a);
            foreach (var h in _history)
            {
                var hs = new BuildUpSnapshotSave
                {
                    DefinitionId = h.DefinitionId, Level = h.Level, Abs = h.Abs, Atk = h.Stats.Atk, Fire = h.Stats.Fire, Pest = h.Stats.Pest,
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
            if (s.Fitted != null) foreach (int a in s.Fitted) w._fitted.Add((AttachmentKind)a);
            if (s.History != null)
                foreach (var h in s.History)
                {
                    var snap = new BuildUpSnapshot
                    {
                        DefinitionId = h.DefinitionId, Level = h.Level, Abs = h.Abs, Stats = new WeaponStats(h.Atk, h.Fire, h.Pest),
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
