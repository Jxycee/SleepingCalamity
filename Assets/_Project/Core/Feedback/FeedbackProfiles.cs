// Authority: none (pure data, cosmetic). The default hit-feedback profiles: the numbers of hit-feedback.md section 13.2 as start values, keyed by id. The
// Presentation `HitFeedbackLibrary` asset can override any entry by id, so one data change retunes a weight class. The hitstop columns are information for
// the victim hold and tuning: the attacker's live hitstop stays AttackData.HitstopFrames (decision A33).
using System;

namespace SleepingCalamity.Core.Feedback
{
    public enum HitWeight : byte { Light = 0, Normal = 1, Heavy = 2, Finisher = 3 }

    [Serializable]
    public sealed class FeedbackProfileData
    {
        public string Id;
        public int AttackerHitstopFrames;   // 60 Hz frames (information; the live value is AttackData.HitstopFrames)
        public int VictimHitstopFrames;     // presentation hold of the victim (not wired yet)
        public float MultiHitFalloff = 0.5f;
        public float Trauma;                // camera shake added, 0..1
        public float RumbleLow, RumbleHigh; // 0..1
        public int RumbleMs;
        public int FlashWhiteFrames;        // victim white flash length (information; the live flash is Enemy.Flash)

        public FeedbackProfileData() { }
        public FeedbackProfileData(string id, int atk, int victim, float trauma, float low, float high, int ms, int flash)
        { Id = id; AttackerHitstopFrames = atk; VictimHitstopFrames = victim; Trauma = trauma; RumbleLow = low; RumbleHigh = high; RumbleMs = ms; FlashWhiteFrames = flash; }
    }

    public static class HitWeights
    {
        // Finisher: a finisher or charge. Heavy: 8+ frames of hitstop. Light: a shot or 2 or fewer frames. Otherwise Normal.
        public static HitWeight Classify(bool finisherOrCharge, bool shot, int hitstopFrames)
        {
            if (finisherOrCharge) return HitWeight.Finisher;
            if (hitstopFrames >= 8) return HitWeight.Heavy;
            if (shot || hitstopFrames <= 2) return HitWeight.Light;
            return HitWeight.Normal;
        }
    }

    public static class FeedbackProfiles
    {
        public const string HitLight = "hit_light", HitNormal = "hit_normal", HitHeavy = "hit_heavy", HitFinisher = "hit_finisher", Blocked = "blocked",
            Kill = "kill", EliteKill = "elite_kill", HeroHurt = "hero_hurt", HeroHurtGuarded = "hero_hurt_guarded", Parry = "parry", EnemySlam = "enemy_slam";

        public static readonly FeedbackProfileData[] Defaults =
        {
            new FeedbackProfileData(HitLight,         3,  2, 0.08f, 0.00f, 0.20f,  50, 3),
            new FeedbackProfileData(HitNormal,        4,  3, 0.12f, 0.00f, 0.25f,  60, 4),
            new FeedbackProfileData(HitHeavy,         8,  5, 0.25f, 0.35f, 0.35f, 100, 4),
            new FeedbackProfileData(HitFinisher,     12,  8, 0.35f, 0.60f, 0.50f, 160, 5),
            new FeedbackProfileData(Blocked,          1,  1, 0.05f, 0.10f, 0.10f,  40, 2),
            new FeedbackProfileData(Kill,             3,  3, 0.30f, 0.40f, 0.30f, 120, 3),
            new FeedbackProfileData(EliteKill,       12, 12, 0.50f, 0.80f, 0.30f, 300, 4),
            new FeedbackProfileData(HeroHurt,         0,  0, 0.40f, 0.70f, 0.30f, 180, 0),
            new FeedbackProfileData(HeroHurtGuarded,  1,  0, 0.15f, 0.30f, 0.10f,  80, 0),
            new FeedbackProfileData(Parry,            4,  0, 0.20f, 0.20f, 0.70f,  80, 3),
            new FeedbackProfileData(EnemySlam,        0,  0, 0.50f, 0.80f, 0.20f, 300, 0),
        };

        public static FeedbackProfileData Get(string id)
        {
            for (int i = 0; i < Defaults.Length; i++) if (Defaults[i].Id == id) return Defaults[i];
            return null;
        }

        public static string IdFor(HitWeight w)
        {
            switch (w)
            {
                case HitWeight.Light: return HitLight;
                case HitWeight.Heavy: return HitHeavy;
                case HitWeight.Finisher: return HitFinisher;
                default: return HitNormal;
            }
        }
    }
}
