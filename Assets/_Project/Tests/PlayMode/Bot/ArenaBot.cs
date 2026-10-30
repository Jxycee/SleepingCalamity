// Authority: test-only (local owner). A scripted playtester for the combat arena, in the spirit of an AI autopilot but deterministic:
// it plays through the same FakeInputService a player's pad would feed (camera-relative stick, attack and dodge presses), so it exercises
// the real input, locomotion, combat and host paths. Policy: walk to the nearest live enemy, attack in reach, roll away from an enemy whose
// telegraphed attack would reach it. It records what happened (BotReport) and flags things a human tester would call out.
using System.Collections.Generic;
using System.Text;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class ArenaBot
    {
        public float AttackReach = 1.35f;     // start swinging when the target's body surface is this close (m, flat)
        public float SwingEvery = 0.3f;       // seconds between attack presses (the combo buffer takes the rest)
        public float DodgeMargin = 0.6f;      // roll when inside an attack's range plus this
        public bool Dodges = true;

        private readonly FakeInputService _input;
        private readonly HeroLocomotion _loco;
        private readonly HeroCombat _combat;
        private readonly HeroVitals _vitals;
        private readonly Transform _hero;
        private readonly HashSet<(ulong, int)> _dodged = new HashSet<(ulong, int)>();
        private readonly Dictionary<ulong, int> _enemyHp = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> _enemySerial = new Dictionary<ulong, int>();
        private readonly HashSet<ulong> _stuckOn = new HashSet<ulong>();   // one stuck finding per target
        private float _swingClock, _stuckClock;
        private Vector3 _lastPos;
        private int _lastHp = -1;
        private bool _pressedLast;

        public readonly BotReport Report = new BotReport();

        public ArenaBot(FakeInputService input, Transform hero)
        {
            _input = input;
            _hero = hero;
            _loco = hero.GetComponent<HeroLocomotion>();
            _combat = hero.GetComponent<HeroCombat>();
            _vitals = hero.GetComponent<HeroVitals>();
            _lastPos = _hero.position;
        }

        /// <summary>One frame of play: read the world, write the input frame, record events.</summary>
        public void Tick(float dt, float time)
        {
            Record(dt, time);
            var f = new PlayerInputFrame();
            if (_pressedLast) { _pressedLast = false; _input.Frame = f; return; }   // release a press for one frame so the next one is an edge
            if (_vitals.IsUp)
            {
                Enemy target = Nearest(out float dist);
                if (Dodges && Threat(out Enemy attacker))
                {
                    Vector3 away = Flat(_hero.position - attacker.AttackCentre);
                    Vector3 side = Vector3.Cross(Vector3.up, away).normalized;
                    Steer(ref f, (away.normalized + side * 0.5f).normalized);
                    if (_combat.AllowsDodge) { f.DodgePressed = true; _pressedLast = true; _dodged.Add((attacker.NetworkObjectId, attacker.Serial)); Report.Dodges++; }
                }
                else if (target != null)
                {
                    Vector3 to = Flat(target.transform.position - _hero.position);
                    float reach = AttackReach + target.Def.Radius;
                    if (dist > reach) Steer(ref f, to.normalized);
                    else
                    {
                        Steer(ref f, to.normalized * 0.25f);   // a nudge keeps the hero facing its target
                        _swingClock -= dt;
                        if (_swingClock <= 0f) { f.PrimaryPressed = f.PrimaryHeld = true; _pressedLast = true; _swingClock = SwingEvery; Report.Swings++; }
                    }
                    StuckCheck(dt, time, dist, reach, target);
                }
            }
            _input.Frame = f;
        }

        private void Record(float dt, float time)
        {
            Report.Seconds = time;
            int hp = _vitals.Hp;
            if (_lastHp >= 0 && hp < _lastHp)
            {
                Report.HitsTaken++; Report.DamageTaken += _lastHp - hp;
                Report.Events.Add($"{time:F2}s hero hit for {_lastHp - hp} ({hp}/{_vitals.MaxHp})");
                if (Report.LastHitTaken >= 0f) Report.MinGapBetweenHitsTaken = Mathf.Min(Report.MinGapBetweenHitsTaken, time - Report.LastHitTaken);
                Report.LastHitTaken = time;
            }
            if (hp > _lastHp && _lastHp >= 0) Report.Events.Add($"{time:F2}s hero healed {hp - _lastHp}");
            _lastHp = hp;
            Report.LowestHpPercent = Mathf.Min(Report.LowestHpPercent, 100f * hp / Mathf.Max(1, _vitals.MaxHp));
            if (!_vitals.IsUp && !Report.WentDown) { Report.WentDown = true; Report.Events.Add($"{time:F2}s hero went down"); }

            foreach (Enemy e in Enemy.All)
            {
                if (!e.IsSpawned) continue;
                int ehp = e.NetHp.Value;
                if (_enemyHp.TryGetValue(e.NetworkObjectId, out int before) && ehp < before) { Report.HitsDealt++; Report.DamageDealt += before - ehp; }
                if (!_enemyHp.ContainsKey(e.NetworkObjectId)) Report.EnemiesSeen++;
                _enemyHp[e.NetworkObjectId] = ehp;
                if (e.Attacking && (!_enemySerial.TryGetValue(e.NetworkObjectId, out int seen) || seen != e.Serial)) { _enemySerial[e.NetworkObjectId] = e.Serial; Report.EnemyAttacksStarted++; }
            }
            var arena = CombatHost.Current;
            if (arena != null && arena.WaveNumber > Report.HighestWave) { Report.HighestWave = arena.WaveNumber; Report.Events.Add($"{time:F2}s wave {arena.WaveNumber}"); }
            if (arena != null && arena.Wave == WaveState.Cleared && Report.ClearedWaves < arena.WaveNumber) { Report.ClearedWaves = arena.WaveNumber; Report.Events.Add($"{time:F2}s wave {arena.WaveNumber} cleared"); }
        }

        private void StuckCheck(float dt, float time, float dist, float reach, Enemy target)
        {
            float moved = Flat(_hero.position - _lastPos).magnitude;
            _lastPos = _hero.position;
            if (dist > reach + 0.5f && moved < 0.002f) _stuckClock += dt; else _stuckClock = 0f;
            if (_stuckClock > 2f && _stuckOn.Add(target.NetworkObjectId))
                Report.Findings.Add($"{time:F2}s stuck: pushing toward a {target.Def.Kind} {dist:F1} m away (reach {reach:F1}) but not moving for 2 s");
        }

        private Enemy Nearest(out float best)
        {
            best = float.MaxValue; Enemy pick = null;
            foreach (Enemy e in Enemy.All)
            {
                if (!e.IsSpawned || !e.Alive) continue;
                float d = Flat(e.transform.position - _hero.position).magnitude;
                if (d < best) { best = d; pick = e; }
            }
            return pick;
        }

        private bool Threat(out Enemy attacker)
        {
            attacker = null;
            foreach (Enemy e in Enemy.All)
            {
                if (!e.IsSpawned || !e.Alive || !e.Attacking) continue;
                AttackData a = e.CurrentAttack;
                if (e.AttackFrame >= a.HitOn.Start || _dodged.Contains((e.NetworkObjectId, e.Serial))) continue;   // only during the wind-up, once per attack
                float framesLeft = a.HitOn.Start - e.AttackFrame;
                if (framesLeft > 18f) continue;   // roll late in the telegraph, like a player would
                if (Flat(_hero.position - e.AttackCentre).magnitude <= a.Range + DodgeMargin) { attacker = e; return true; }
            }
            return false;
        }

        private void Steer(ref PlayerInputFrame f, Vector3 worldDir)
        {
            float yaw = _loco.ViewYawDeg * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            f.MoveX = Mathf.Clamp(Vector3.Dot(worldDir, right), -1f, 1f);
            f.MoveY = Mathf.Clamp(Vector3.Dot(worldDir, forward), -1f, 1f);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    public sealed class BotReport
    {
        public string Goal = "";
        public bool GoalMet;
        public float Seconds, LowestHpPercent = 100f, LastHitTaken = -1f, MinGapBetweenHitsTaken = float.MaxValue;
        public int HitsTaken, DamageTaken, HitsDealt, DamageDealt, Swings, Dodges, EnemiesSeen, EnemyAttacksStarted, HighestWave, ClearedWaves;
        public bool WentDown;
        public readonly List<string> Events = new List<string>();
        public readonly List<string> Findings = new List<string>();

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Bot playtest: {Goal}");
            sb.AppendLine($"**Goal {(GoalMet ? "MET" : "NOT MET")}** after {Seconds:F1} s.");
            sb.AppendLine();
            sb.AppendLine($"- Waves reached {HighestWave}, cleared {ClearedWaves}; enemies seen {EnemiesSeen}; enemy attacks started {EnemyAttacksStarted}");
            sb.AppendLine($"- Hero: took {HitsTaken} hits / {DamageTaken} damage, lowest HP {LowestHpPercent:F0}%, went down: {(WentDown ? "yes" : "no")}" +
                          (HitsTaken > 1 ? $", closest two hits {MinGapBetweenHitsTaken:F2} s apart" : ""));
            sb.AppendLine($"- Hero: {Swings} attack presses, {HitsDealt} hits / {DamageDealt} damage dealt, {Dodges} rolls");
            sb.AppendLine();
            sb.AppendLine("## Findings");
            if (Findings.Count == 0) sb.AppendLine("- none");
            foreach (string s in Findings) sb.AppendLine("- " + s);
            sb.AppendLine();
            sb.AppendLine("## Timeline");
            foreach (string s in Events) sb.AppendLine("- " + s);
            return sb.ToString();
        }
    }
}
