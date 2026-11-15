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

    // Post-hit grace (HurtGrace, GuardMath.cs): the window lengths per hit kind, the knockdown, the flicker and the knockback that makes a hit heavy.
    // Heavy must last at least as long as the knockdown it contains (the owner's grace pauses with it).
    public static class HurtGraceTuning
    {
        public static float HeavyKnockback = 3f, LightSeconds = 0.8f, HeavySeconds = 1.2f, MultiHitSeconds = 0.15f, KnockdownSeconds = 0.9f, FlickerSeconds = 0.4f;
        public static string Validate() => HeavySeconds < KnockdownSeconds
            ? "heavySeconds (" + HeavySeconds + ") must not be less than knockdownSeconds (" + KnockdownSeconds + ")" : null;
        public static Knob[] Knobs() => new[]
        {
            new Knob("heavyKnockback", () => HeavyKnockback, v => HeavyKnockback = (float)v, 0.1, 100),
            new Knob("lightSeconds", () => LightSeconds, v => LightSeconds = (float)v, 0.05, 10),
            new Knob("heavySeconds", () => HeavySeconds, v => HeavySeconds = (float)v, 0.05, 10),
            new Knob("multiHitSeconds", () => MultiHitSeconds, v => MultiHitSeconds = (float)v, 0.05, 5),
            new Knob("knockdownSeconds", () => KnockdownSeconds, v => KnockdownSeconds = (float)v, 0, 10),
            new Knob("flickerSeconds", () => FlickerSeconds, v => FlickerSeconds = (float)v, 0, 10),
        };
    }

    // The enemy tier multipliers (EnemyCatalog): tier 1 is floors 1-2, tier 2 floors 3-4. Applied on top of each enemy's base table; the party-size life
    // scaling (CoopScaling) multiplies after them, so a change here moves solo and co-op together.
    public static class EnemyTuning
    {
        public static Knob[] Knobs() => new[]
        {
            new Knob("tier1Atk", () => EnemyCatalog.Tier1Atk, v => EnemyCatalog.Tier1Atk = (float)v, 0.01, 20),
            new Knob("tier2Atk", () => EnemyCatalog.Tier2Atk, v => EnemyCatalog.Tier2Atk = (float)v, 0.01, 20),
            new Knob("tier2Life", () => EnemyCatalog.Tier2Life, v => EnemyCatalog.Tier2Life = (float)v, 0.1, 50),
            new Knob("tier2Def", () => EnemyCatalog.Tier2Def, v => EnemyCatalog.Tier2Def = (float)v, 0.1, 50),
            new Knob("soloAtk", () => EnemyCatalog.SoloAtk, v => EnemyCatalog.SoloAtk = (float)v, 0.01, 20),
            new Knob("soloLife", () => EnemyCatalog.SoloLife, v => EnemyCatalog.SoloLife = (float)v, 0.05, 20),
            new Knob("cellarsMaxTier", () => EnemyCatalog.CellarsMaxTier, v => EnemyCatalog.CellarsMaxTier = (int)v, 1, 2, true),
            new Knob("tier2MinFloor", () => EnemyCatalog.Tier2MinFloor, v => EnemyCatalog.Tier2MinFloor = (int)v, 1, 5, true),
        };
    }

    // "attacks": { "axe_1": { "range": 2.5, "hitOn": [10, 15] } }. Ids are the hero ground attacks (hit 1, hit 2, finisher, charge, special) of the four
    // heirloom sets; the air versions follow from the ground ones. Enemy attack ids are the separate "enemyAttacks" section (EnemyAttackTuning below):
    // they are changed in place, because EnemyDefinition, the AI and EnemyAttackRules hold those AttackData by reference.
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
                AttackData next = Tuned(cur, kv.Key, fields, source, "attacks");
                if (next != cur) set.Replace(slot, next);
            }
        }

        // The override logic shared with EnemyAttackTuning: the attack's values with `f` laid over them, or `a` itself when nothing took effect. `section` is
        // only the name in messages. The simulator's export names (mult, hitOnFrame, hitOffFrame; Tools/balance_sim/data/game_numbers.json) are accepted as
        // aliases of multiplier and the two ends of hitOn, so a value set from the simulator applies as written.
        internal static AttackData Tuned(AttackData a, string id, JObject f, string source, string section)
        {
            int total = a.TotalFrames, cancel = a.CancelFrom, stop = a.HitstopFrames;
            FrameWindow hitOn = a.HitOn, combo = a.ComboOpen, fin = a.FinisherAllowed, ifr = a.IFrames, armor = a.SuperArmor, turn = a.Turn, lunge = a.Lunge;
            float mult = a.Multiplier, range = a.Range, half = a.HalfAngleDeg, band = a.HeightBand, kb = a.Knockback, rev = a.Revenge, kick = a.CameraKick,
                poise = a.Poise, turnSpeed = a.TurnDegPerSec, lungeM = a.LungeMeters;
            bool gb = a.GuardBreak, guardable = a.Guardable, fire = a.Fire;
            bool changed = false;

            foreach (var kv in f)
            {
                string path = section + "." + id + "." + kv.Key;
                if (string.Equals(kv.Key, "hitOnFrame", StringComparison.OrdinalIgnoreCase) || string.Equals(kv.Key, "hitOffFrame", StringComparison.OrdinalIgnoreCase))
                {
                    if (!Number(kv.Value, path, source, 0, 100000, true, out double edge)) continue;
                    hitOn = string.Equals(kv.Key, "hitOnFrame", StringComparison.OrdinalIgnoreCase) ? new FrameWindow((int)edge, hitOn.End) : new FrameWindow(hitOn.Start, (int)edge);
                    changed = true; continue;
                }
                string name = string.Equals(kv.Key, "mult", StringComparison.OrdinalIgnoreCase) ? "multiplier" : kv.Key;
                string key = Array.Find(Ints, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
                if (key != null)
                {
                    if (!Number(kv.Value, path, source, 0, 100000, true, out double v)) continue;
                    if (key == "totalFrames") total = (int)v; else if (key == "cancelFrom") cancel = (int)v; else stop = (int)v;
                    changed = true; continue;
                }
                key = Array.Find(Windows, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
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
                key = Array.Find(Floats, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
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
                key = Array.Find(Bools, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
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
            // An attack with no early cancel (every enemy attack: CancelFrom == TotalFrames) keeps having none when its length changes, unless cancelFrom is set.
            if (a.CancelFrom >= a.TotalFrames && cancel == a.CancelFrom && total != a.TotalFrames) cancel = Math.Max(hitOn.End, total);
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

    // "enemyAttacks": { "rat_lunge": { "multiplier": 1.2, "range": 1.5, "hitOn": [28, 36], "totalFrames": 62 } }. The same keys, ranges and checks as
    // "attacks" (AttackTuning.Tuned), plus the simulator's export names as aliases: mult = multiplier, hitOnFrame / hitOffFrame = the two ends of hitOn.
    // Ids are AttackCatalog.AllEnemyAttacks (test_nova is not tunable). The values are written INTO the existing AttackData (AttackData.Retune), so the
    // enemy definitions, the id registry and the network attack index keep pointing at the same objects. After an override the enemy rules
    // (EnemyAttackRules.Problem: wind-up floors, glint 12-24 frames before the hitbox, lunge before strike, reach against the body, punish window) must
    // still hold; an attack that breaks one is rolled back to what it was and warned about. The tell's own frames (EnemyTell: pose end, glint) are not
    // tunable, so a hitOn change must keep the glint lead in range.
    public static class EnemyAttackTuning
    {
        public static void Reset() => AttackCatalog.ResetEnemyAttacks();

        public static void Apply(JObject body, string source)
        {
            foreach (var kv in body)
            {
                if (!AttackCatalog.EnemyAttackById.TryGetValue(kv.Key, out AttackData cur) || cur == AttackCatalog.TestNova)
                { TuningStore.Warn(source + ": unknown enemy attack '" + kv.Key + "'"); continue; }
                var fields = kv.Value as JObject;
                if (fields == null) { TuningStore.Warn(source + ": enemy attack '" + kv.Key + "' must be an object"); continue; }
                AttackData next = AttackTuning.Tuned(cur, kv.Key, fields, source, "enemyAttacks");
                if (next == cur) continue;
                AttackData before = cur.Copy();
                cur.Retune(next);
                string problem = EnemyAttackRules.Problem(cur);
                if (problem != null)
                {
                    cur.Retune(before);
                    TuningStore.Warn(source + ": enemy attack '" + kv.Key + "' ignored, " + problem);
                }
            }
        }
    }

    // "enemyPacing": { "cooldown": { "default": 1.2, "RatKing": 0.8, "Warden": 1.4 }, "recover": { "default": 0.35, "RatKing": 0.9 }, "tier1Scale": 1,
    // "tier1RecoverScale": 1, "tier2CooldownScale": 0.7, "tier2RecoverScale": 0.8, "tier1MaxAttackersPerHero": 1, ... }. The names are the ones the balance
    // export writes under "enemyPacing" (BalanceExportTests), so a simulator value set applies as written. cooldown is the seconds before the next attack,
    // recover the standing time after one (AttackPacing); both are scaled per tier. The punish window of every enemy attack must still be at least
    // EnemyAttackRules.MinPunishFrames at both tiers, else the whole section from that file is rolled back.
    public static class EnemyPacingTuning
    {
        static Knob TierKnob(string name, int tier, Func<PacingProfile, double> get, Func<PacingProfile, double, PacingProfile> set, double min, double max, bool integer = false) =>
            new Knob(name, () => get(tier == 1 ? AttackPacing.Tier1 : AttackPacing.Tier2),
                v => { if (tier == 1) AttackPacing.Tier1 = set(AttackPacing.Tier1, v); else AttackPacing.Tier2 = set(AttackPacing.Tier2, v); }, min, max, integer);

        public static string Validate()
        {
            foreach (EnemyDefinition def in EnemyCatalog.All)
                foreach (AttackData a in def.Attacks)
                    for (int tier = 1; tier <= 2; tier++)
                    {
                        int punish = EnemyAttackRules.PunishFrames(a, def.Kind, tier);
                        if (punish < EnemyAttackRules.MinPunishFrames)
                            return def.Name + " / " + a.Id + " would leave " + punish + " punish frames at tier " + tier + " (at least " + EnemyAttackRules.MinPunishFrames + ")";
                    }
            return null;
        }

        // Dotted names: the section's nested JSON ("cooldown": { "default": .. }) is flattened to "cooldown.default".
        public static Knob[] Knobs() => new[]
        {
            new Knob("cooldown.default", () => AttackPacing.CooldownDefault, v => AttackPacing.CooldownDefault = (float)v, 0.05, 60),
            new Knob("cooldown.RatKing", () => AttackPacing.CooldownRatKing, v => AttackPacing.CooldownRatKing = (float)v, 0.05, 60),
            new Knob("cooldown.Warden", () => AttackPacing.CooldownWarden, v => AttackPacing.CooldownWarden = (float)v, 0.05, 60),
            new Knob("recover.default", () => AttackPacing.RecoverDefault, v => AttackPacing.RecoverDefault = (float)v, 0, 60),
            new Knob("recover.RatKing", () => AttackPacing.RecoverRatKing, v => AttackPacing.RecoverRatKing = (float)v, 0, 60),
            TierKnob("tier1Scale", 1, p => p.CooldownScale, (p, v) => new PacingProfile(p.MaxAttackersPerHero, p.MinStrikeGapSeconds, (float)v, p.RecoverScale, p.RetrySeconds), 0.05, 10),
            TierKnob("tier1RecoverScale", 1, p => p.RecoverScale, (p, v) => new PacingProfile(p.MaxAttackersPerHero, p.MinStrikeGapSeconds, p.CooldownScale, (float)v, p.RetrySeconds), 0, 10),
            TierKnob("tier2CooldownScale", 2, p => p.CooldownScale, (p, v) => new PacingProfile(p.MaxAttackersPerHero, p.MinStrikeGapSeconds, (float)v, p.RecoverScale, p.RetrySeconds), 0.05, 10),
            TierKnob("tier2RecoverScale", 2, p => p.RecoverScale, (p, v) => new PacingProfile(p.MaxAttackersPerHero, p.MinStrikeGapSeconds, p.CooldownScale, (float)v, p.RetrySeconds), 0, 10),
            TierKnob("tier1MaxAttackersPerHero", 1, p => p.MaxAttackersPerHero, (p, v) => new PacingProfile((int)v, p.MinStrikeGapSeconds, p.CooldownScale, p.RecoverScale, p.RetrySeconds), 1, 8, true),
            TierKnob("tier2MaxAttackersPerHero", 2, p => p.MaxAttackersPerHero, (p, v) => new PacingProfile((int)v, p.MinStrikeGapSeconds, p.CooldownScale, p.RecoverScale, p.RetrySeconds), 1, 8, true),
            TierKnob("tier1MinStrikeGapSeconds", 1, p => p.MinStrikeGapSeconds, (p, v) => new PacingProfile(p.MaxAttackersPerHero, (float)v, p.CooldownScale, p.RecoverScale, p.RetrySeconds), 0, 10),
            TierKnob("tier2MinStrikeGapSeconds", 2, p => p.MinStrikeGapSeconds, (p, v) => new PacingProfile(p.MaxAttackersPerHero, (float)v, p.CooldownScale, p.RecoverScale, p.RetrySeconds), 0, 10),
            new Knob("signatureEvery", () => AttackPacing.SignatureEvery, v => AttackPacing.SignatureEvery = (int)v, 1, 50, true),
            new Knob("signatureExtraRecoverSeconds", () => AttackPacing.SignatureExtraRecoverSeconds, v => AttackPacing.SignatureExtraRecoverSeconds = (float)v, 0, 30),
        };
    }
}
