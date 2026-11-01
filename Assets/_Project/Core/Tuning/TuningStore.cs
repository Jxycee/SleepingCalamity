// Core/Tuning/TuningStore.cs: optional JSON tuning files laid over the code defaults (docs/design/m3-combat-greybox.md, "Tuning files"). Pure C#, no engine.
// Files: *.json in each folder given to LoadFolders, folders in order (StreamingAssets/Tuning first, the user's mod folder last), files in ordinal name order;
// a later value wins over an earlier one. Each file is one JSON object whose top-level keys are sections: "attacks", "poise", "weaponGrowth", "targeting",
// "heroCombat". An unknown section or key, a value of the wrong type or out of range, or a file that is not valid JSON is skipped (the default stays) and
// logged once through TuningStore.Log. With no files nothing changes.
// Authority: none. Tuning is read at startup, before any scene runs, on every peer alike (host and clients must load the same files).
// TODO(enemy tuning): the enemy tables (EnemyModel, EnemyRules, Core/Combat/Ai, enemy attacks) belong to another session. When that lands, add an "enemies"
// section with TuningStore.RegisterSection("enemies", apply, reset) from their own class; nothing here touches them. Enemy attack ids in "attacks" are logged
// as unknown.
using System;
using System.Collections.Generic;
using System.IO;

namespace SleepingCalamity.Core.Tuning
{
    // One tunable number. Default is whatever the backing value held when the knob was registered.
    public sealed class Knob
    {
        public readonly string Name;
        public readonly double Min, Max, Default;
        public readonly bool Integer;
        readonly Func<double> _get;
        readonly Action<double> _set;

        public Knob(string name, Func<double> get, Action<double> set, double min, double max, bool integer = false)
        {
            Name = name; _get = get; _set = set; Min = min; Max = max; Integer = integer; Default = get();
        }

        public double Value => _get();
        public void Set(double v) => _set(v);
        public void Restore() => _set(Default);
    }

    public static class TuningStore
    {
        public delegate void SectionApply(Dictionary<string, object> body, string source);

        sealed class Section { public SectionApply Apply; public Action Reset; }

        static readonly Dictionary<string, Section> s_sections = new Dictionary<string, Section>(StringComparer.Ordinal);
        static readonly HashSet<string> s_logged = new HashSet<string>(StringComparer.Ordinal);

        // Where messages go (the game points it at its log). Null = silent.
        public static Action<string> Log;

        static TuningStore()
        {
            RegisterKnobSection("poise", PoiseTuning.Knobs());
            RegisterKnobSection("weaponGrowth", WeaponGrowthTuning.Knobs());
            RegisterKnobSection("targeting", TargetingTuning.Knobs());
            RegisterKnobSection("heroCombat", HeroCombatTuning.Knobs());
            RegisterSection("attacks", AttackTuning.Apply, AttackTuning.Reset);
        }

        public static void RegisterSection(string name, SectionApply apply, Action reset) => s_sections[name] = new Section { Apply = apply, Reset = reset };

        static void RegisterKnobSection(string name, Knob[] knobs)
        {
            RegisterSection(name, (body, source) =>
            {
                foreach (var kv in body)
                {
                    Knob k = Array.Find(knobs, x => string.Equals(x.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (k == null) { Warn(source + ": unknown key '" + name + "." + kv.Key + "'"); continue; }
                    ApplyKnob(k, name + "." + kv.Key, kv.Value, source);
                }
            }, () => { foreach (var k in knobs) k.Restore(); });
        }

        static bool ApplyKnob(Knob k, string path, object raw, string source)
        {
            if (!(raw is double d) || double.IsNaN(d) || double.IsInfinity(d)) { Warn(source + ": '" + path + "' is not a number, keeping " + k.Value); return false; }
            if (k.Integer && Math.Floor(d) != d) { Warn(source + ": '" + path + "' must be a whole number, keeping " + k.Value); return false; }
            if (d < k.Min || d > k.Max) { Warn(source + ": '" + path + "' = " + d + " is outside " + k.Min + ".." + k.Max + ", keeping " + k.Value); return false; }
            k.Set(d);
            return true;
        }

        // Logs a message the first time only (the same bad key in two files, or a file read twice, is reported once).
        public static void Warn(string message)
        {
            if (s_logged.Add(message)) Log?.Invoke("[Tuning] " + message);
        }

        // Back to the code defaults everywhere, and the log's "already said" memory is cleared.
        public static void Reset()
        {
            foreach (var s in s_sections.Values) s.Reset?.Invoke();
            s_logged.Clear();
        }

        // Applies one file's text. False when it is not a valid JSON object (nothing from it is applied).
        public static bool ApplyJson(string json, string source)
        {
            object root;
            try { root = MiniJson.Parse(json); }
            catch (FormatException e) { Warn(source + ": not valid JSON (" + e.Message + "), ignored"); return false; }
            var top = root as Dictionary<string, object>;
            if (top == null) { Warn(source + ": the top level must be a JSON object, ignored"); return false; }
            foreach (var kv in top)
            {
                if (!s_sections.TryGetValue(kv.Key, out Section sec)) { Warn(source + ": unknown section '" + kv.Key + "'"); continue; }
                var body = kv.Value as Dictionary<string, object>;
                if (body == null) { Warn(source + ": section '" + kv.Key + "' must be an object"); continue; }
                sec.Apply(body, source);
            }
            return true;
        }

        // Loads every *.json in each folder, in the order given. Missing folders are skipped without a message. Returns the number of files read.
        public static int LoadFolders(params string[] folders)
        {
            int n = 0;
            if (folders == null) return 0;
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                string[] files;
                try { files = Directory.GetFiles(folder, "*.json"); }
                catch (Exception e) { Warn(folder + ": cannot list (" + e.Message + ")"); continue; }
                Array.Sort(files, StringComparer.Ordinal);
                foreach (string f in files)
                {
                    string text;
                    try { text = File.ReadAllText(f); }
                    catch (Exception e) { Warn(f + ": cannot read (" + e.Message + ")"); continue; }
                    ApplyJson(text, Path.GetFileName(f));
                    n++;
                }
            }
            return n;
        }
    }
}
