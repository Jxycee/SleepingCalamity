// Core/Tuning/TuningStore.cs: optional JSON tuning files laid over the code defaults (docs/design/m3-combat-greybox.md, "Tuning files"). Pure C#, no engine.
// Files: *.json in each folder given to LoadFolders, folders in order (StreamingAssets/Tuning first, the user's mod folder last), files in ordinal name order;
// a later value wins over an earlier one. Each file is one JSON object whose top-level keys are sections: "attacks", "poise", "weaponGrowth", "targeting",
// "heroCombat", "hurtGrace", "enemies" (tier multipliers), "enemyAttacks" (enemy attack values, changed in place) and "enemyPacing" (enemy cooldowns, recovery, tokens). An unknown section or key, a value of the wrong type or out of range, or a file that is not valid JSON is skipped (the default stays) and
// logged once through TuningStore.Log. With no files nothing changes.
// Authority: none. Tuning is read at startup, before any scene runs, on every peer alike (host and clients must load the same files).
// Enemy base tables (hp/atk/def/speed per kind) are not tunable yet (only the "enemies" tier multipliers); enemy attack ids in "attacks" are logged as unknown (use "enemyAttacks").
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public delegate void SectionApply(JObject body, string source);

        sealed class Section { public SectionApply Apply; public Action Reset; }

        static readonly Dictionary<string, Section> s_sections = new Dictionary<string, Section>(StringComparer.Ordinal);
        static readonly HashSet<string> s_logged = new HashSet<string>(StringComparer.Ordinal);

        // Where messages go (the game points it at its log). Null = silent.
        public static Action<string> Log;

        static TuningStore()
        {
            RegisterKnobSection("poise", PoiseTuning.Knobs());
            RegisterKnobSection("weaponGrowth", WeaponGrowthTuning.Knobs());
            RegisterKnobSection("targeting", TargetingTuning.Knobs(), TargetingTuning.Validate);
            RegisterKnobSection("heroCombat", HeroCombatTuning.Knobs());
            RegisterKnobSection("hurtGrace", HurtGraceTuning.Knobs(), HurtGraceTuning.Validate);
            RegisterKnobSection("enemies", EnemyTuning.Knobs());
            RegisterSection("attacks", AttackTuning.Apply, AttackTuning.Reset);
            RegisterSection("enemyAttacks", EnemyAttackTuning.Apply, EnemyAttackTuning.Reset);
            RegisterKnobSection("enemyPacing", EnemyPacingTuning.Knobs(), EnemyPacingTuning.Validate, nested: true);
        }

        public static void RegisterSection(string name, SectionApply apply, Action reset) => s_sections[name] = new Section { Apply = apply, Reset = reset };

        // validate (optional) checks ordered pairs on the values after a section is applied; it returns a message when they are inconsistent, and then the
        // whole section from that file is rolled back (the earlier values stay).
        // nested: the body may hold one level of objects ("cooldown": { "default": 1.2 }), flattened to the dotted knob names ("cooldown.default").
        static void RegisterKnobSection(string name, Knob[] knobs, Func<string> validate = null, bool nested = false)
        {
            RegisterSection(name, (body, source) =>
            {
                double[] before = new double[knobs.Length];
                for (int i = 0; i < knobs.Length; i++) before[i] = knobs[i].Value;
                foreach (var kv in nested ? Flatten(body) : Direct(body))
                {
                    Knob k = Array.Find(knobs, x => string.Equals(x.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (k == null) { Warn(source + ": unknown key '" + name + "." + kv.Key + "'"); continue; }
                    ApplyKnob(k, name + "." + kv.Key, kv.Value, source);
                }
                string problem = validate?.Invoke();
                if (problem == null) return;
                for (int i = 0; i < knobs.Length; i++) knobs[i].Set(before[i]);
                Warn(source + ": section '" + name + "' ignored, " + problem);
            }, () => { foreach (var k in knobs) k.Restore(); });
        }

        static IEnumerable<KeyValuePair<string, JToken>> Direct(JObject body)
        {
            foreach (var p in body.Properties()) yield return new KeyValuePair<string, JToken>(p.Name, p.Value);
        }

        static IEnumerable<KeyValuePair<string, JToken>> Flatten(JObject body)
        {
            foreach (var p in body.Properties())
            {
                if (p.Value is JObject inner)
                    foreach (var q in inner.Properties()) yield return new KeyValuePair<string, JToken>(p.Name + "." + q.Name, q.Value);
                else yield return new KeyValuePair<string, JToken>(p.Name, p.Value);
            }
        }

        // A JSON integer or decimal as a double. False for anything else (string, bool, null, object, array) and for NaN / Infinity.
        public static bool TryNumber(JToken t, out double d)
        {
            d = 0;
            if (t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float)) return false;
            try { d = t.Value<double>(); }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException) { return false; }
            return !double.IsNaN(d) && !double.IsInfinity(d);
        }

        static bool ApplyKnob(Knob k, string path, JToken raw, string source)
        {
            if (!TryNumber(raw, out double d)) { Warn(source + ": '" + path + "' is not a number, keeping " + k.Value); return false; }
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
            JToken root;
            try
            {
                if (json != null) json = json.TrimStart('﻿');   // a BOM that survived decoding
                // A repeated key in one object is an error (which of the two values was meant?), so the whole file is ignored.
                root = JToken.Parse(json ?? "", new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException e) { Warn(source + ": not valid JSON (" + e.Message + "), ignored"); return false; }
            var top = root as JObject;
            if (top == null) { Warn(source + ": the top level must be a JSON object, ignored"); return false; }
            foreach (var kv in top)
            {
                if (!s_sections.TryGetValue(kv.Key, out Section sec)) { Warn(source + ": unknown section '" + kv.Key + "'"); continue; }
                var body = kv.Value as JObject;
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
