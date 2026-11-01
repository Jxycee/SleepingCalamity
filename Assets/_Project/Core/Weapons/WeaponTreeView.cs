// Core/Weapons/WeaponTreeView.cs: the data the greybox weapon-tree UI shows (PHASE1 6.3, M4) and the pure view model that formats it. No engine.
// WeaponTreeSnapshot is what the HOST sends to a weapon's owner after every change (HeroCombat.ServerPushTree); the owner only reads it. The view
// model derives everything else (names, slot count, target stats, absorb preview) from the snapshot and the catalog, so little goes over the wire.
// Authority: host writes the snapshot (From); the owner reads it and builds a view. Nothing here changes game state.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SleepingCalamity.Core.Weapons
{
    public sealed class WeaponTreeSnapshot
    {
        public string DefinitionId = WeaponCatalog.Axe;
        public int Level;
        public float Abs;
        public WeaponStats Stats;
        public int[] Fitted = Array.Empty<int>();       // AttachmentKind values
        public bool HasTarget, BuildUpOpen, CanReroute, AtWorkbench;
        public float ProgressAtk, ProgressFire, ProgressPest;   // value / target base capped at 1; 1 for a stat the target does not ask for
        public string RestoredDefinitionId;             // what a re-route would turn the weapon back into (null when it cannot)

        // Host side: what the tree shows beyond the growth mirror (weapon, definition, level, fitted attachments, flags, progress fractions at 1%). ABS and stats
        // are left out on purpose: the growth mirror already carries them (HeroCombat.SyncGrowthOwnerRpc refreshes TreeMirror's), so a plain ABS tick needs no tree push.
        public string SyncKey(string weaponKey)
        {
            var sb = new System.Text.StringBuilder(64);
            sb.Append(weaponKey).Append('|').Append(DefinitionId).Append('|').Append(Level).Append('|').Append(RestoredDefinitionId).Append('|');
            foreach (int f in Fitted ?? Array.Empty<int>()) sb.Append(f).Append(',');
            sb.Append('|').Append(HasTarget ? 1 : 0).Append(BuildUpOpen ? 1 : 0).Append(CanReroute ? 1 : 0).Append(AtWorkbench ? 1 : 0);
            sb.Append('|').Append((int)Math.Round(ProgressAtk * 100f)).Append(',').Append((int)Math.Round(ProgressFire * 100f)).Append(',').Append((int)Math.Round(ProgressPest * 100f));
            return sb.ToString();
        }

        // Host side: reads a WeaponState. `atWorkbench` is the host's AtWorkbench predicate (re-route is only allowed there).
        public static WeaponTreeSnapshot From(WeaponState w, bool atWorkbench)
        {
            var p = w.GetBuildUpProgress();
            var fitted = new int[w.Fitted.Count];
            for (int i = 0; i < fitted.Length; i++) fitted[i] = (int)w.Fitted[i];
            var rp = w.GetReroutePreview();
            return new WeaponTreeSnapshot
            {
                DefinitionId = w.DefinitionId, Level = w.Level, Abs = w.Abs, Stats = w.Stats, Fitted = fitted,
                HasTarget = p.HasTarget, BuildUpOpen = p.Available, CanReroute = rp.Possible, AtWorkbench = atWorkbench,
                ProgressAtk = p.Atk, ProgressFire = p.Fire, ProgressPest = p.Pest, RestoredDefinitionId = rp.RestoredDefinitionId,
            };
        }
    }

    public readonly struct SlotView
    {
        public readonly bool Filled;
        public readonly AttachmentKind Kind;   // only meaningful when Filled
        public readonly string Label;
        public SlotView(bool filled, AttachmentKind kind, string label) { Filled = filled; Kind = kind; Label = label; }
    }

    public readonly struct ThresholdView
    {
        public readonly string Label;
        public readonly float Fraction;        // 0..1 of the target base
        public readonly float Marker;          // where the 90% line sits on the bar (0.9)
        public readonly bool Met;
        public ThresholdView(string label, float fraction, bool met) { Label = label; Fraction = fraction; Marker = WeaponGrowth.BuildUpThreshold; Met = met; }
    }

    // Everything the IMGUI window draws and which buttons are enabled. Build with Build(); fields are plain data.
    public sealed class WeaponTreeView
    {
        public string Title, LevelLine, StatsLine, AbsorbLine, BuildUpLine, RerouteLine;
        public float AbsFraction;
        public List<SlotView> Slots = new List<SlotView>();
        public List<ThresholdView> Thresholds = new List<ThresholdView>();
        public bool CanFit, CanBuildUp, CanReroute;   // button enabled states

        static string N(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        public static string AttachmentName(AttachmentKind k)
        {
            switch (k)
            {
                case AttachmentKind.Ember: return "Ember (+5 fire)";
                case AttachmentKind.PestBane: return "Pest-bane (+5 pest)";
                case AttachmentKind.Heartwood: return "Heartwood (+2 ATK)";
                default: return k.ToString();
            }
        }

        static string StatText(WeaponStats s) => "ATK " + N(s.Atk) + "  Fire " + N(s.Fire) + "  Pest " + N(s.Pest);

        public static WeaponTreeView Build(WeaponTreeSnapshot s, WeaponCatalog catalog = null)
        {
            catalog = catalog ?? WeaponCatalog.Default;
            var v = new WeaponTreeView();
            catalog.TryGet(s.DefinitionId, out var def);
            v.Title = def != null ? def.DisplayName : s.DefinitionId;
            float cost = WeaponGrowth.LevelCost(s.Level);
            v.AbsFraction = cost <= 0f ? 0f : Math.Max(0f, Math.Min(1f, s.Abs / cost));
            v.LevelLine = "Level " + s.Level + "   ABS " + N(s.Abs) + " / " + N(cost);
            v.StatsLine = StatText(s.Stats);

            int slots = def != null ? def.SlotCount : 0;
            var fitted = new List<AttachmentKind>();
            var absorb = s.Stats;
            foreach (int k in s.Fitted ?? Array.Empty<int>())
            {
                var kind = (AttachmentKind)k;
                if (!Attachments.IsValid(kind)) continue;
                fitted.Add(kind);
                absorb += Attachments.Bonus(kind);
            }
            for (int i = 0; i < Math.Max(slots, fitted.Count); i++)
                v.Slots.Add(i < fitted.Count ? new SlotView(true, fitted[i], AttachmentName(fitted[i])) : new SlotView(false, default, "(empty)"));
            v.CanFit = fitted.Count < slots;

            var plain = s.Stats + new WeaponStats(WeaponGrowth.AtkGain(s.Level + 1));
            var withAbsorb = absorb + new WeaponStats(WeaponGrowth.AtkGain(s.Level + 1));
            v.AbsorbLine = fitted.Count == 0
                ? "Next level (" + (s.Level + 1) + "): " + StatText(plain) + "  (no attachment to absorb)"
                : "Next level (" + (s.Level + 1) + ") absorbs " + fitted.Count + ": " + StatText(withAbsorb) + "  (without: ATK " + N(plain.Atk) + ")";

            if (!s.HasTarget || !catalog.TryGet(def?.BuildUpTargetId, out var target))
            {
                v.BuildUpLine = "No build-up from this weapon";
            }
            else
            {
                v.BuildUpLine = "Build-up: " + target.DisplayName + " (opens at " + N(WeaponGrowth.BuildUpThreshold * 100f) + "% of its base)";
                AddThreshold(v, "ATK", s.Stats.Atk, target.Base.Atk, s.ProgressAtk);
                AddThreshold(v, "Fire", s.Stats.Fire, target.Base.Fire, s.ProgressFire);
                AddThreshold(v, "Pest", s.Stats.Pest, target.Base.Pest, s.ProgressPest);
            }
            v.CanBuildUp = s.HasTarget && s.BuildUpOpen;

            if (!s.CanReroute) v.RerouteLine = "Re-route: nothing to undo";
            else if (!s.AtWorkbench) v.RerouteLine = "Re-route: back to " + Name(catalog, s.RestoredDefinitionId) + " (only at the home workbench)";
            else v.RerouteLine = "Re-route: back to " + Name(catalog, s.RestoredDefinitionId) + " (loses what was gained since)";
            v.CanReroute = s.CanReroute && s.AtWorkbench;
            return v;
        }

        static string Name(WeaponCatalog c, string id) => id != null && c.TryGet(id, out var d) ? d.DisplayName : (id ?? "?");

        // Only stats the target asks for (base > 0) get a bar, as the Core progress treats the others as met.
        static void AddThreshold(WeaponTreeView v, string name, float value, float targetBase, float fraction)
        {
            if (targetBase <= 0f) return;
            v.Thresholds.Add(new ThresholdView(name + " " + N(value) + " / " + N(targetBase) + "  (need " + N(targetBase * WeaponGrowth.BuildUpThreshold) + ")",
                fraction, value * 10f >= targetBase * 9f));
        }
    }
}
