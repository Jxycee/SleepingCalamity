// Core/Combat/AttackCatalog.cs: the four heirlooms as attack sets, and the enemies' attacks, as data (docs/PHASE1.md 6.1, 6.5, 6.6). Pure C#.
// P9 Placeholders throughout: frame windows are tuned by feel in the M3 playtest and again in M7. Frames are 60 Hz.
// Authority: none (static data).
using System.Collections.Generic;
using SleepingCalamity.Core.Weapons;

namespace SleepingCalamity.Core.Combat
{
    public static class AttackCatalog
    {
        public const int AxeIdx = 0, StaffIdx = 1, DaggerIdx = 2, BowIdx = 3;

        static FrameWindow W(int a, int b) => new FrameWindow(a, b);

        // ---- Axe: heavy. Slow, long recovery, a guard-breaking finisher, a charge with armour. ----
        public static readonly AttackSet Axe = new AttackSet("Axe",
            new AttackData("axe_1", "Axe swing", AttackKind.Normal, 34, W(11, 16), 1.0f, HitShape.Cone, 2.3f, 55f, comboOpen: W(14, 30), cancelFrom: 22,
                turn: W(0, 11), turnDegPerSec: 540f, lunge: W(6, 14), lungeMeters: 0.7f, knockback: 0.8f, revenge: 1f, hitstopFrames: 6, cameraKick: 0.35f, poise: 2f),
            new AttackData("axe_2", "Axe backswing", AttackKind.Normal, 36, W(12, 17), 1.1f, HitShape.Cone, 2.4f, 60f, comboOpen: W(15, 32), finisherAllowed: W(15, 32),
                cancelFrom: 24, turn: W(0, 12), turnDegPerSec: 540f, lunge: W(6, 15), lungeMeters: 0.7f, knockback: 0.9f, revenge: 1f, hitstopFrames: 6, cameraKick: 0.4f, poise: 2f),
            new AttackData("axe_f", "Axe overhead", AttackKind.Finisher, 58, W(22, 28), 1.6f, HitShape.Cone, 2.7f, 70f, cancelFrom: 44,
                turn: W(0, 20), turnDegPerSec: 420f, lunge: W(14, 24), lungeMeters: 1.0f, knockback: 3.0f, revenge: 3f, hitstopFrames: 14, cameraKick: 0.9f, guardBreak: true, poise: 5f),
            new AttackData("axe_c", "Axe whirlwind", AttackKind.Charge, 62, W(24, 32), 2.0f, HitShape.Circle, 3.0f, 180f, cancelFrom: 48, superArmor: W(0, 36),
                turn: W(0, 10), turnDegPerSec: 360f, knockback: 4.0f, revenge: 4f, hitstopFrames: 14, cameraKick: 1.0f, guardBreak: true, poise: 6f),
            null);

        // ---- Staff: long reach and wide sweeps. Its Secondary casts a spell that costs a pip. ----
        public static readonly AttackSet Staff = new AttackSet("Staff",
            new AttackData("staff_1", "Staff sweep", AttackKind.Normal, 26, W(8, 14), 1.0f, HitShape.Cone, 3.3f, 85f, comboOpen: W(10, 22), cancelFrom: 17,
                turn: W(0, 8), turnDegPerSec: 600f, lunge: W(4, 10), lungeMeters: 0.4f, knockback: 0.6f, revenge: 1f, hitstopFrames: 5, cameraKick: 0.25f),
            new AttackData("staff_2", "Staff return sweep", AttackKind.Normal, 26, W(8, 14), 1.1f, HitShape.Cone, 3.3f, 85f, comboOpen: W(10, 22), finisherAllowed: W(10, 22),
                cancelFrom: 17, turn: W(0, 8), turnDegPerSec: 600f, lunge: W(4, 10), lungeMeters: 0.4f, knockback: 0.6f, revenge: 1f, hitstopFrames: 5, cameraKick: 0.25f),
            new AttackData("staff_f", "Staff spin", AttackKind.Finisher, 46, W(14, 24), 1.6f, HitShape.Circle, 3.5f, 180f, cancelFrom: 34, turn: W(0, 12), turnDegPerSec: 480f,
                knockback: 2.6f, revenge: 3f, hitstopFrames: 12, cameraKick: 0.7f),
            new AttackData("staff_c", "Staff sweep, charged", AttackKind.Charge, 50, W(18, 26), 2.0f, HitShape.Cone, 4.6f, 100f, cancelFrom: 38, turn: W(0, 10), turnDegPerSec: 480f,
                knockback: 3.5f, revenge: 4f, hitstopFrames: 12, cameraKick: 0.9f),
            new AttackData("staff_s", "Cinder burst", AttackKind.Spell, 42, W(14, 20), 2.2f, HitShape.Circle, 4.5f, 180f, cancelFrom: 34, superArmor: W(0, 20),
                knockback: 2.5f, revenge: 4f, hitstopFrames: 8, cameraKick: 0.7f, fire: true, spellPips: 1));

        // ---- Dagger and shield: fast. Its Secondary is the guard (see GuardMath). ----
        public static readonly AttackSet Dagger = new AttackSet("Dagger and shield",
            new AttackData("dag_1", "Dagger jab", AttackKind.Normal, 16, W(5, 9), 1.0f, HitShape.Cone, 1.9f, 45f, comboOpen: W(6, 15), cancelFrom: 11,
                turn: W(0, 5), turnDegPerSec: 900f, lunge: W(2, 8), lungeMeters: 0.8f, knockback: 0.3f, revenge: 1f, hitstopFrames: 4, cameraKick: 0.15f, poise: 0.5f),
            new AttackData("dag_2", "Dagger slash", AttackKind.Normal, 16, W(5, 9), 1.1f, HitShape.Cone, 1.9f, 50f, comboOpen: W(6, 15), finisherAllowed: W(6, 15), cancelFrom: 11,
                turn: W(0, 5), turnDegPerSec: 900f, lunge: W(2, 8), lungeMeters: 0.8f, knockback: 0.3f, revenge: 1f, hitstopFrames: 4, cameraKick: 0.15f, poise: 0.5f),
            new AttackData("dag_f", "Dagger flurry", AttackKind.Finisher, 30, W(10, 17), 1.6f, HitShape.Cone, 2.1f, 55f, cancelFrom: 22, turn: W(0, 9), turnDegPerSec: 720f,
                lunge: W(6, 14), lungeMeters: 1.2f, knockback: 1.8f, revenge: 3f, hitstopFrames: 11, cameraKick: 0.6f, poise: 2f),
            new AttackData("dag_c", "Lunging stab", AttackKind.Charge, 40, W(12, 18), 2.0f, HitShape.Cone, 2.4f, 30f, cancelFrom: 30, turn: W(0, 10), turnDegPerSec: 720f,
                lunge: W(8, 16), lungeMeters: 3.2f, knockback: 2.0f, revenge: 4f, hitstopFrames: 11, cameraKick: 0.7f, poise: 3f),
            null);

        // ---- Bow and sword: a mid-speed sword chain; the Secondary aims and the shot is a ray. ----
        public static readonly AttackSet Bow = new AttackSet("Bow and sword",
            new AttackData("bow_1", "Sword cut", AttackKind.Normal, 22, W(7, 12), 1.0f, HitShape.Cone, 2.1f, 50f, comboOpen: W(9, 19), cancelFrom: 14,
                turn: W(0, 7), turnDegPerSec: 720f, lunge: W(3, 9), lungeMeters: 0.6f, knockback: 0.5f, revenge: 1f, hitstopFrames: 5, cameraKick: 0.25f),
            new AttackData("bow_2", "Sword return", AttackKind.Normal, 22, W(7, 12), 1.1f, HitShape.Cone, 2.1f, 55f, comboOpen: W(9, 19), finisherAllowed: W(9, 19), cancelFrom: 14,
                turn: W(0, 7), turnDegPerSec: 720f, lunge: W(3, 9), lungeMeters: 0.6f, knockback: 0.5f, revenge: 1f, hitstopFrames: 5, cameraKick: 0.25f),
            new AttackData("bow_f", "Sword rising cut", AttackKind.Finisher, 40, W(14, 21), 1.6f, HitShape.Cone, 2.3f, 60f, cancelFrom: 30, turn: W(0, 13), turnDegPerSec: 540f,
                lunge: W(8, 16), lungeMeters: 1.0f, knockback: 2.4f, revenge: 3f, hitstopFrames: 12, cameraKick: 0.7f),
            new AttackData("bow_c", "Sword spin", AttackKind.Charge, 48, W(16, 24), 2.0f, HitShape.Circle, 2.7f, 180f, cancelFrom: 36, turn: W(0, 8), turnDegPerSec: 540f,
                knockback: 3.2f, revenge: 4f, hitstopFrames: 12, cameraKick: 0.8f),
            new AttackData("bow_s", "Aimed shot", AttackKind.Shot, 30, W(4, 5), 1.8f, HitShape.Ray, 26f, 0f, cancelFrom: 20, turn: W(0, 4), turnDegPerSec: 1080f,
                knockback: 1.0f, revenge: 2f, hitstopFrames: 4, cameraKick: 0.4f));

        public static AttackSet ForFamily(WeaponFamily f)
        {
            switch (f)
            {
                case WeaponFamily.Axe: return Axe;
                case WeaponFamily.Staff: return Staff;
                case WeaponFamily.Dagger: return Dagger;
                case WeaponFamily.Bow: return Bow;
                default: return Axe;
            }
        }

        public static AttackSet ForWeaponId(string weaponId) => ForFamily(WeaponStyle.FamilyOf(weaponId));

        // The heirloom ids by hero slot, so two players in a test or a walk start with different weapons (a debug key swaps them).
        public static readonly string[] Heirlooms = { WeaponCatalog.Axe, WeaponCatalog.Staff, WeaponCatalog.DaggerShield, WeaponCatalog.BowSword };

        // ---- Enemy attacks (the same type; Kind.Enemy). The windows double as the telegraph: the hit-on frame is when the wind-up ends. ----
        // Every real attack carries an EnemyTell: the body's own wind-up (pose, hold, glint on the attacking part, sound), so it reads with the ground
        // footprint switched off (now an assist, off by default). Rules in EnemyAttackRules: the glint comes 12-24 frames before the hitbox, the enemy stops
        // turning at the glint, small enemies wind up for at least 27 frames and the elite for 54 (research recommendation 4).
        static AttackData E(string id, string name, int total, int hitOnA, int hitOnB, float mult, HitShape shape, float range, float half,
            float knockback = 1f, bool guardBreak = false, bool guardable = true, FrameWindow lunge = default, float lunges = 0f,
            FrameWindow superArmor = default, HitOrigin origin = HitOrigin.Self, FrameWindow turn = default, float turnSpeed = 0f, EnemyTell tell = null, TokenCategory? pool = null) =>
            new AttackData(id, name, AttackKind.Enemy, total, W(hitOnA, hitOnB), mult, shape, range, half, knockback: knockback, guardBreak: guardBreak, guardable: guardable,
                lunge: lunge, lungeMeters: lunges, superArmor: superArmor, origin: origin, turn: turn, turnDegPerSec: turnSpeed, heightBand: 2.5f, revenge: 0f, tell: tell, tokenPool: pool);

        static EnemyTell T(TellPart part, int poseEnd, int commit, float lean = 0f, float crouch = 0f, float rise = 0f, float twist = 0f, float tremble = 0f,
            string sfx = null, float pitch = 1f, string hold = null, bool charge = false) =>
            new EnemyTell(part, poseEnd, commit, lean, crouch, rise, twist, tremble, sfx, pitch, hold, charge);

        // Slime: squashes down into the jar and shivers, the lid flashes, then it springs.
        // The hop is a light melee attack (Melee pool); the glob below is the slime's ranged one (its enemy default).
        public static readonly AttackData SlimeHop = E("slime_hop", "Hop", 60, 34, 40, 1.0f, HitShape.Circle, 1.1f, 180f, lunge: W(16, 34), lunges: 2.2f, turn: W(0, 14), turnSpeed: 360f, pool: TokenCategory.Melee,
            tell: T(TellPart.Lid, 10, 16, crouch: 0.35f, tremble: 0.6f, sfx: CombatSfx.WindUp, pitch: 1.25f));
        // Leans back with the glob swelling on its lid; the lid flashes as it throws (the glob flies from the glint to the strike frame).
        public static readonly AttackData SlimeGlob = E("slime_glob", "Glob lob", 66, 46, 52, 0.8f, HitShape.Circle, 1.3f, 180f, origin: HitOrigin.GroundAtTarget,
            tell: T(TellPart.Lid, 18, 26, lean: 16f, crouch: 0.2f, tremble: 0.4f, sfx: CombatSfx.WindUp, pitch: 1.1f));
        // Run 5 balance (2026-11-15): Rat-king bite multipliers 1.1 -> 0.85 (lunge) and 1.3 -> 1.0 (spin); its life stays 180 (three axe openings, 2026-10-30).
        // Rat-king: rears back on its haunches, crown glints, lunges. Recovery 18 -> 26 frames after the bite (research recommendation 6: the punish window).
        public static readonly AttackData RatLunge = E("rat_lunge", "Lunge", 62, 28, 36, 0.85f, HitShape.Cone, 1.45f, 42f, lunge: W(16, 28), lunges: 3.4f, turn: W(0, 14), turnSpeed: 720f,
            tell: T(TellPart.Head, 8, 14, lean: 22f, crouch: 0.2f, tremble: 0.8f, sfx: CombatSfx.WindUpScratch, pitch: 1.2f));
        // Coils its body the other way, tail raised and glinting, then whips round. Recovery 16 -> 22 frames.
        public static readonly AttackData RatSpin = E("rat_spin", "Tail spin", 62, 30, 40, 1.0f, HitShape.Circle, 1.45f, 180f, knockback: 2f,
            tell: T(TellPart.Tail, 12, 16, crouch: 0.1f, twist: 75f, tremble: 0.4f, sfx: CombatSfx.WindUpScratch, pitch: 1.0f));
        // Broom: twists its bristles back with a wooden creak, bristles flash, sweeps.
        public static readonly AttackData BroomSweep = E("broom_sweep", "Sweep", 70, 36, 44, 1.2f, HitShape.Cone, 2.4f, 80f, knockback: 3f, turn: W(0, 20), turnSpeed: 300f,
            tell: T(TellPart.Bristles, 14, 22, lean: 10f, twist: 60f, tremble: 0.3f, sfx: CombatSfx.WindUpWood, pitch: 1.0f));
        // Draws its handle back, the handle glints, jabs forward.
        public static readonly AttackData BroomPoke = E("broom_poke", "Handle poke", 52, 28, 34, 1.0f, HitShape.Cone, 1.9f, 30f, lunge: W(16, 28), lunges: 1.2f, turn: W(0, 14), turnSpeed: 420f,
            tell: T(TellPart.Handle, 8, 14, lean: 20f, tremble: 0.3f, sfx: CombatSfx.WindUpWood, pitch: 1.25f));
        // Dough Warden: big and slow, every tell at least 0.9 s. Multipliers x1.4 in Run 5 (2026-11-15, with a 2.4 s cooldown): hits that matter, fewer of them.
        public static readonly AttackData WardenSlam = E("warden_slam", "Rolling-pin slam", 100, 56, 64, 2.1f, HitShape.Cone, 3.0f, 55f, knockback: 4f, guardBreak: true,
            superArmor: W(0, 100), turn: W(0, 40), turnSpeed: 200f,
            tell: T(TellPart.RollingPin, 28, 40, lean: 22f, rise: 0.25f, tremble: 0.5f, sfx: CombatSfx.WindUpHeavy, pitch: 0.9f));
        public static readonly AttackData WardenPound = E("warden_pound", "Oven-door pound", 116, 72, 80, 2.5f, HitShape.Circle, 3.0f, 180f, knockback: 5f, guardBreak: true,
            superArmor: W(0, 116),
            tell: T(TellPart.Body, 40, 56, lean: 16f, crouch: 0.2f, rise: 0.35f, tremble: 0.6f, sfx: CombatSfx.WindUpHeavy, pitch: 0.8f));
        // Wind-up 40 -> 54 frames (the elite floor; research recommendation 4).
        public static readonly AttackData WardenSweep = E("warden_sweep", "Flour sweep", 98, 54, 64, 1.5f, HitShape.Cone, 3.0f, 100f, knockback: 3f, superArmor: W(0, 98),
            turn: W(0, 36), turnSpeed: 240f,
            tell: T(TellPart.RollingPin, 26, 38, lean: 8f, twist: 80f, tremble: 0.4f, sfx: CombatSfx.WindUpHeavy, pitch: 1.0f));
        // The Warden's answer when its revenge gauge fills: a quick big circle that clears everyone off it. Wind-up 30 -> 36 (research recommendation 4);
        // it is not in the Warden's attack list (it answers being chained), so the 54-frame elite floor does not apply.
        public static readonly AttackData WardenBreakFree = E("warden_breakfree", "Break free", 78, 36, 44, 1.0f, HitShape.Circle, 3.0f, 180f, knockback: 6f, guardBreak: true,
            superArmor: W(0, 78),
            tell: T(TellPart.Body, 14, 22, crouch: 0.3f, tremble: 1.0f, sfx: CombatSfx.WindUpHeavy, pitch: 1.1f));
        // The Warden's signature (the elite's dramatic tell): it rises to full height leaning far back with the rolling pin overhead, a low bell tolls as
        // it peaks, the dough crown glows hotter through a long trembling hold, flashes, and it brings everything down in a ring. 1.5 s to the hitbox
        // (KH2's single big strikes are 1.2-1.3 s), then a second stuck in the floor: the reward for reading it. Chosen every few attacks, never at random.
        public static readonly AttackData WardenQuake = E("warden_quake", "Dough quake", 160, 90, 100, 2.8f, HitShape.Circle, 3.0f, 180f, knockback: 6f, guardBreak: true,
            superArmor: W(0, 160), turn: W(0, 50), turnSpeed: 160f,
            tell: T(TellPart.Crown, 50, 72, lean: 26f, rise: 0.6f, tremble: 0.9f, sfx: CombatSfx.WindUpHeavy, pitch: 0.7f, hold: CombatSfx.EliteToll, charge: true));
        // The broom's answer to a hammered guard (combat depth, change 4): three blocked front hits inside 2 s and it braces, the bristles flash and it
        // shoves them back at you. Not in the broom's attack list (it answers being hit, like the Warden's break-free), but it keeps every small-enemy
        // tell rule: 27 frames of wind-up, the glint 12 before the hitbox, no turning after the glint.
        public static readonly AttackData BroomRiposte = E("broom_riposte", "Bristle riposte", 58, 27, 33, 1.0f, HitShape.Cone, 2.0f, 50f, knockback: 2f, turn: W(0, 12), turnSpeed: 420f,
            tell: T(TellPart.Bristles, 9, 15, lean: 14f, tremble: 0.3f, sfx: CombatSfx.WindUpWood, pitch: 1.4f));
        // Test-only: a huge circle that no roll can leave, so a dodge test proves the i-frames and not the distance.
        public static readonly AttackData TestNova = E("test_nova", "Test nova", 60, 12, 40, 1.0f, HitShape.Circle, 30f, 180f, knockback: 0f);

        public static readonly AttackData[] AllEnemyAttacks =
        {
            SlimeHop, SlimeGlob, RatLunge, RatSpin, BroomSweep, BroomPoke, WardenSlam, WardenPound, WardenSweep, WardenBreakFree, TestNova, WardenQuake, BroomRiposte,
        };

        // Lookup by id for the hit intents of enemies against players.
        public static readonly IReadOnlyDictionary<string, AttackData> EnemyAttackById = BuildById();

        static Dictionary<string, AttackData> BuildById()
        {
            var d = new Dictionary<string, AttackData>();
            foreach (AttackData a in AllEnemyAttacks) d[a.Id] = a;
            return d;
        }

        // The code defaults of every enemy attack, cloned once at static init (after the catalogue above), so tuning can be undone. Tuning (Core/Tuning
        // "enemyAttacks") changes the SAME instances in place (AttackData.Retune): EnemyDefinition.Attacks, AllEnemyAttacks, EnemyAttackById, the `==`
        // identity checks and the network attack index (Array.IndexOf over AllEnemyAttacks) all stay valid. Startup only; every peer loads the same files.
        static readonly AttackData[] s_enemyDefaults = SnapshotEnemyAttacks();

        static AttackData[] SnapshotEnemyAttacks()
        {
            var d = new AttackData[AllEnemyAttacks.Length];
            for (int i = 0; i < d.Length; i++) d[i] = AllEnemyAttacks[i].Copy();
            return d;
        }

        // Back to the code defaults.
        public static void ResetEnemyAttacks()
        {
            for (int i = 0; i < AllEnemyAttacks.Length; i++) AllEnemyAttacks[i].Retune(s_enemyDefaults[i]);
        }

        // Overwrite one enemy attack's tunable values in place from `tunedValues` (see AttackData.Retune). False for an unknown id. No rule check here:
        // the tuning section runs EnemyAttackRules.Problem and rolls back.
        public static bool TuneEnemyAttack(string id, AttackData tunedValues)
        {
            if (tunedValues == null || id == null || !EnemyAttackById.TryGetValue(id, out AttackData a)) return false;
            a.Retune(tunedValues);
            return true;
        }
    }
}
