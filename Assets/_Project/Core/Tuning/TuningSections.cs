// Core/Tuning/TuningSections.cs: the current values of the tunables that were constants, and their knob tables. Defaults live here; PoiseMeter, WeaponGrowth,
// TargetingConfig and HeroCombat read them (their old constant names are properties now). Pure C#. Authority: none (read at startup, same on every peer).
using System;
using Newtonsoft.Json.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core.Tuning
{
    public static class PoiseTuning
    {
        public static float ResetSeconds = 1.0f, SteadySeconds = 2.0f, PerExtraPlayer = 0.5f;
        public static Knob[] Knobs() => new[]
        {
            new Knob("resetSeconds", () => ResetSeconds, v => ResetSeconds = (float)v, 0.01, 60),
            new Knob("steadySeconds", () => SteadySeconds, v => SteadySeconds = (float)v, 0, 60),
            new Knob("perExtraPlayer", () => PerExtraPlayer, v => PerExtraPlayer = (float)v, 0, 10),
        };
    }

    public static class WeaponGrowthTuning
    {
        public static float FinisherMultiplier = 1.25f, BuildUpThreshold = 0.9f, BuildUpBonus = 0.1f;
        public static double CreditWindowSeconds = 10.0;
        public static int FlatAtkFromLevel = 5;
        public static Knob[] Knobs() => new[]
        {
            new Knob("finisherMultiplier", () => FinisherMultiplier, v => FinisherMultiplier = (float)v, 0.01, 100),
            new Knob("creditWindowSeconds", () => CreditWindowSeconds, v => CreditWindowSeconds = v, 0, 3600),
            new Knob("flatAtkFromLevel", () => FlatAtkFromLevel, v => FlatAtkFromLevel = (int)v, 0, 1000, true),
            new Knob("buildUpThreshold", () => BuildUpThreshold, v => BuildUpThreshold = (float)v, 0, 1),
            new Knob("buildUpBonus", () => BuildUpBonus, v => BuildUpBonus = (float)v, 0, 10),
        };
    }

    public static class TargetingTuning
    {
        public static float SoftConeHalfAngleDeg = 60f, SoftRange = 6f, HeightBand = 2f, AcquireRange = 10f, ReleaseRange = 13f, LosGraceSeconds = 1.0f,
            HardHeightBand = 4f, FacingWeight = 0.6f, FlickOn = 0.75f, FlickOff = 0.35f, FlickConeHalfAngleDeg = 60f,
            // Elites and bosses keep a lock longer (a wider release range and a longer sight grace) but no longer forever: a hard lock that never dropped
            // glued the camera to the Dough Warden through walls and rooms (alpha playtest item 8).
            EliteReleaseRange = 26f, EliteLosGraceSeconds = 4f;
        // Ordered pairs: a target is picked up at AcquireRange and kept out to ReleaseRange (release must not be nearer); the flick switches on above FlickOn
        // and off below FlickOff (off must not be higher).
        public static string Validate()
        {
            if (ReleaseRange < AcquireRange) return "releaseRange (" + ReleaseRange + ") must not be less than acquireRange (" + AcquireRange + ")";
            if (EliteReleaseRange < ReleaseRange) return "eliteReleaseRange (" + EliteReleaseRange + ") must not be less than releaseRange (" + ReleaseRange + ")";
            if (FlickOff > FlickOn) return "flickOff (" + FlickOff + ") must not be greater than flickOn (" + FlickOn + ")";
            return null;
        }
        public static Knob[] Knobs() => new[]
        {
            new Knob("softConeHalfAngleDeg", () => SoftConeHalfAngleDeg, v => SoftConeHalfAngleDeg = (float)v, 1, 180),
            new Knob("softRange", () => SoftRange, v => SoftRange = (float)v, 0.1, 100),
            new Knob("heightBand", () => HeightBand, v => HeightBand = (float)v, 0.1, 100),
            new Knob("acquireRange", () => AcquireRange, v => AcquireRange = (float)v, 0.1, 200),
            new Knob("releaseRange", () => ReleaseRange, v => ReleaseRange = (float)v, 0.1, 200),
            new Knob("losGraceSeconds", () => LosGraceSeconds, v => LosGraceSeconds = (float)v, 0, 60),
            new Knob("hardHeightBand", () => HardHeightBand, v => HardHeightBand = (float)v, 0.1, 100),
            new Knob("facingWeight", () => FacingWeight, v => FacingWeight = (float)v, 0, 1),
            new Knob("flickOn", () => FlickOn, v => FlickOn = (float)v, 0, 1),
            new Knob("flickOff", () => FlickOff, v => FlickOff = (float)v, 0, 1),
            new Knob("flickConeHalfAngleDeg", () => FlickConeHalfAngleDeg, v => FlickConeHalfAngleDeg = (float)v, 1, 180),
            new Knob("eliteReleaseRange", () => EliteReleaseRange, v => EliteReleaseRange = (float)v, 0.1, 400),
            new Knob("eliteLosGraceSeconds", () => EliteLosGraceSeconds, v => EliteLosGraceSeconds = (float)v, 0, 120),
        };
    }

    // The constants HeroCombat (Gameplay) used to hold; Gameplay cannot be referenced from Core, so it reads these.
    public static class HeroCombatTuning
    {
        public static float KnockbackScale = 1f, HurtStagger = 0.25f, ShotHeight = 1.3f, ShotAssistDeg = 10f, ShotRadius = 0.25f;
        public static Knob[] Knobs() => new[]
        {
            new Knob("knockbackScale", () => KnockbackScale, v => KnockbackScale = (float)v, 0, 20),
            new Knob("hurtStagger", () => HurtStagger, v => HurtStagger = (float)v, 0, 10),
            new Knob("shotHeight", () => ShotHeight, v => ShotHeight = (float)v, 0, 5),
            new Knob("shotAssistDeg", () => ShotAssistDeg, v => ShotAssistDeg = (float)v, 0, 90),
            new Knob("shotRadius", () => ShotRadius, v => ShotRadius = (float)v, 0, 5),
        };
    }

    // "attacks": { "axe_1": { "range": 2.5, "hitOn": [10, 15] } }. Ids are the hero ground attacks (hit 1, hit 2, finisher, charge, special) of the four
    // heirloom sets; the air versions follow from the ground ones. Enemy attack ids are not tunable yet (see the TODO in TuningStore.cs).
    public static class AttackTuning
    {
        static readonly string[] Ints = { "totalFrames", "cancelFrom", "hitstopFrames" };
        static readonly string[] Windows = { "hitOn", "comboOpen", "finisherAllowed", "iFrames", "superArmor", "turn", "lunge" };
        static readonly string[] Floats = { "multiplier", "range", "halfAngleDeg", "heightBand", "knockback", "revenge", "cameraKick", "poise", "turnDegPerSec", "lungeMeters" };
        static readonly string[] Bools = { "guardBreak", "guardable", "fire" };

        static AttackSet[] Sets() => new[] { AttackCatalog.Axe, AttackCatalog.Staff, AttackCatalog.Dagger, AttackCatalog.Bow };

        public static void Reset() { foreach (var s in Sets()) s.RestoreDefaults(); }

        public static void Apply(JObject body, string source)
        {
            foreach (var kv in body)
            {
                AttackSet set = null; int slot = -1;
                foreach (var s in Sets())
                    for (int i = 0; i <= AttackSet.Special && slot < 0; i++)
                        if (s.Attacks[i] != null && string.Equals(s.Attacks[i].Id, kv.Key, StringComparison.Ordinal)) { set = s; slot = i; }
                if (slot < 0) { TuningStore.Warn(source + ": unknown attack '" + kv.Key + "'"); continue; }
                var fields = kv.Value as JObject;
                if (fields == null) { TuningStore.Warn(source + ": attack '" + kv.Key + "' must be an object"); continue; }
                AttackData cur = set.Attacks[slot];
                AttackData next = Tuned(cur, kv.Key, fields, source);
                if (next != cur) set.Replace(slot, next);
            }
        }

        static AttackData Tuned(AttackData a, string id, JObject f, string source)
        {
            int total = a.TotalFrames, cancel = a.CancelFrom, stop = a.HitstopFrames;
            FrameWindow hitOn = a.HitOn, combo = a.ComboOpen, fin = a.FinisherAllowed, ifr = a.IFrames, armor = a.SuperArmor, turn = a.Turn, lunge = a.Lunge;
            float mult = a.Multiplier, range = a.Range, half = a.HalfAngleDeg, band = a.HeightBand, kb = a.Knockback, rev = a.Revenge, kick = a.CameraKick,
                poise = a.Poise, turnSpeed = a.TurnDegPerSec, lungeM = a.LungeMeters;
            bool gb = a.GuardBreak, guardable = a.Guardable, fire = a.Fire;
            bool changed = false;

            foreach (var kv in f)
            {
                string path = "attacks." + id + "." + kv.Key;
                string key = Array.Find(Ints, x => string.Equals(x, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    if (!Number(kv.Value, path, source, 0, 100000, true, out double v)) continue;
                    if (key == "totalFrames") total = (int)v; else if (key == "cancelFrom") cancel = (int)v; else stop = (int)v;
                    changed = true; continue;
                }
                key = Array.Find(Windows, x => string.Equals(x, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    var arr = kv.Value as JArray;
                    double s = 0, e = 0;
                    if (arr == null || arr.Count != 2 || !Number(arr[0], path, source, 0, 100000, true, out s) || !Number(arr[1], path, source, 0, 100000, true, out e))
                    {
                        TuningStore.Warn(source + ": '" + path + "' must be [start, end] in whole frames, keeping the default");
                        continue;
                    }
                    var w = new FrameWindow((int)s, (int)e);
                    switch (key)
                    {
                        case "hitOn": hitOn = w; break; case "comboOpen": combo = w; break; case "finisherAllowed": fin = w; break;
                        case "iFrames": ifr = w; break; case "superArmor": armor = w; break; case "turn": turn = w; break; default: lunge = w; break;
                    }
                    changed = true; continue;
                }
                key = Array.Find(Floats, x => string.Equals(x, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    if (!Number(kv.Value, path, source, 0, 100000, false, out double v)) continue;
                    float fv = (float)v;
                    switch (key)
                    {
                        case "multiplier": mult = fv; break; case "range": range = fv; break; case "halfAngleDeg": half = fv; break;
                        case "heightBand": band = fv; break; case "knockback": kb = fv; break; case "revenge": rev = fv; break;
                        case "cameraKick": kick = fv; break; case "poise": poise = fv; break; case "turnDegPerSec": turnSpeed = fv; break; default: lungeM = fv; break;
                    }
                    changed = true; continue;
                }
                key = Array.Find(Bools, x => string.Equals(x, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    if (kv.Value == null || kv.Value.Type != JTokenType.Boolean) { TuningStore.Warn(source + ": '" + path + "' must be true or false, keeping the default"); continue; }
                    bool b = kv.Value.Value<bool>();
                    if (key == "guardBreak") gb = b; else if (key == "guardable") guardable = b; else fire = b;
                    changed = true; continue;
                }
                TuningStore.Warn(source + ": unknown key '" + path + "'");
            }
            if (!changed) return a;
            string problem = Check(total, cancel, hitOn, combo, fin, ifr, armor, turn, lunge);
            if (problem != null) { TuningStore.Warn(source + ": attack '" + id + "' ignored, " + problem); return a; }
            return new AttackData(a.Id, a.Name, a.Kind, total, hitOn, mult, a.Shape, range, half, combo, fin, cancel, ifr, armor, turn, turnSpeed, lunge, lungeM, band,
                kb, rev, stop, kick, gb, guardable, fire, a.SpellPips, a.Origin, a.Tell, poise, a.TokenPool);
        }

        // Ordered pairs of one attack after the override: each window runs start..end, and the cancel point lies inside the animation.
        // windows must be in the order of the Windows array.
        static string Check(int total, int cancel, params FrameWindow[] windows)
        {
            for (int i = 0; i < windows.Length; i++)
                if (windows[i].Start > windows[i].End) return Windows[i] + " starts (" + windows[i].Start + ") after it ends (" + windows[i].End + ")";
            if (cancel > total) return "cancelFrom (" + cancel + ") is after totalFrames (" + total + ")";
            return null;
        }

        static bool Number(JToken raw, string path, string source, double min, double max, bool integer, out double v)
        {
            v = 0;
            if (!TuningStore.TryNumber(raw, out double d)) { TuningStore.Warn(source + ": '" + path + "' is not a number, keeping the default"); return false; }
            if (integer && Math.Floor(d) != d) { TuningStore.Warn(source + ": '" + path + "' must be a whole number, keeping the default"); return false; }
            if (d < min || d > max) { TuningStore.Warn(source + ": '" + path + "' = " + d + " is outside " + min + ".." + max + ", keeping the default"); return false; }
            v = d;
            return true;
        }
    }
}
