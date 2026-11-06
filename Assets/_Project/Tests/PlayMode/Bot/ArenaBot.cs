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
        public Enemy Preferred;               // when set and alive, attack this one instead of the nearest
        public bool FlankFrontalBlockers;    // walk to a broom's side instead of straight at its blocking front (a player does); off for the arena bot's old baseline

        private readonly FakeInputService _input;
        private readonly HeroLocomotion _loco;
        private readonly HeroCombat _combat;
        private readonly HeroVitals _vitals;
        private readonly Transform _hero;
        private readonly HashSet<(ulong, int)> _dodged = new HashSet<(ulong, int)>();
        private readonly Dictionary<ulong, int> _enemyHp = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> _enemySerial = new Dictionary<ulong, int>();
        private readonly HashSet<(ulong, int)> _strikes = new HashSet<(ulong, int)>();
        private float _lastStrikeAt = -1f;
        private ulong _lastStriker;
        private readonly HashSet<ulong> _stuckOn = new HashSet<ulong>();   // one stuck finding per target
        private readonly List<string> _stuckWhy = new List<string>();
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
                    Vector3 goal = target.transform.position;
                    bool flank = FlankFrontalBlockers && target.Def.FrontalBlock;
                    if (flank)
                    {
                        // the point beside the blocker (its right or left, whichever is nearer), at swing reach: outside the frontal cone
                        Vector3 right = target.transform.right * (Vector3.Dot(target.transform.right, _hero.position - target.transform.position) >= 0f ? 1f : -1f);
                        goal = target.transform.position + Flat(right).normalized * (reach * 0.9f);
                    }
                    float goalDist = flank ? Flat(goal - _hero.position).magnitude : dist;
                    if (flank && goalDist > 0.6f && dist > 0.7f) Steer(ref f, AroundObstacles(Flat(goal - _hero.position).normalized, goal));
                    else if (dist > reach) Steer(ref f, AroundObstacles(to.normalized, target.transform.position));
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
                if (Report.LastHitTaken >= 0f)
                {
                    Report.MinGapBetweenHitsTaken = Mathf.Min(Report.MinGapBetweenHitsTaken, time - Report.LastHitTaken);
                    if (time - Report.LastHitTaken < 0.5f) Report.HitsStacked++;
                }
                Report.LastHitTaken = time;
            }
            if (hp > _lastHp && _lastHp >= 0) Report.Events.Add($"{time:F2}s hero healed {hp - _lastHp}");
            _lastHp = hp;
            Report.LowestHpPercent = Mathf.Min(Report.LowestHpPercent, 100f * hp / Mathf.Max(1, _vitals.MaxHp));
            if (!_vitals.IsUp && !Report.WentDown) { Report.WentDown = true; Report.Events.Add($"{time:F2}s hero went down"); }

            int windUps = 0, waiting = 0;
            foreach (Enemy e in Enemy.All)
            {
                if (!e.IsSpawned) continue;
                if (e.Alive && e.WindingUp) windUps++;
                if (e.Alive && e.WaitingForToken) waiting++;
                if (e.Alive && e.HitboxOn && _strikes.Add((e.NetworkObjectId, e.Serial)))
                {
                    // A strike opened: how long since another enemy's strike opened (attack tokens keep these apart).
                    if (_lastStrikeAt >= 0f && _lastStriker != e.NetworkObjectId) Report.MinGapBetweenStrikes = Mathf.Min(Report.MinGapBetweenStrikes, time - _lastStrikeAt);
                    _lastStrikeAt = time;
                    _lastStriker = e.NetworkObjectId;
                    Report.Strikes++;
                }
                int ehp = e.NetHp.Value;
                if (_enemyHp.TryGetValue(e.NetworkObjectId, out int before) && ehp < before) { Report.HitsDealt++; Report.DamageDealt += before - ehp; }
                if (!_enemyHp.ContainsKey(e.NetworkObjectId)) Report.EnemiesSeen++;
                _enemyHp[e.NetworkObjectId] = ehp;
                if (e.Attacking && (!_enemySerial.TryGetValue(e.NetworkObjectId, out int seen) || seen != e.Serial)) { _enemySerial[e.NetworkObjectId] = e.Serial; Report.EnemyAttacksStarted++; if (e.CurrentAttack == AttackCatalog.WardenQuake) { Report.SignatureAttacks++; Report.Events.Add($"{time:F2}s Warden dough quake"); } }
            }
            Report.MaxWindUpsAtOnce = Mathf.Max(Report.MaxWindUpsAtOnce, windUps);
            Report.MaxWaitingAtOnce = Mathf.Max(Report.MaxWaitingAtOnce, waiting);
            var arena = CombatHost.Current;
            if (arena != null && arena.WaveNumber > Report.HighestWave) { Report.HighestWave = arena.WaveNumber; Report.Events.Add($"{time:F2}s wave {arena.WaveNumber}"); }
            if (arena != null && arena.Wave == WaveState.Cleared && Report.ClearedWaves < arena.WaveNumber) { Report.ClearedWaves = arena.WaveNumber; Report.Events.Add($"{time:F2}s wave {arena.WaveNumber} cleared"); }
        }

        private void StuckCheck(float dt, float time, float dist, float reach, Enemy target)
        {
            float moved = Flat(_hero.position - _lastPos).magnitude;
            _lastPos = _hero.position;
            if (dist > reach + 0.5f && moved < 0.002f) { _stuckClock += dt; if (_stuckWhy.Count < 6 && Mathf.Repeat(_stuckClock, 0.4f) < dt) _stuckWhy.Add(State()); }
            else { _stuckClock = 0f; _stuckWhy.Clear(); }
            if (_stuckClock > 2f && _stuckOn.Add(target.NetworkObjectId))
                Report.Findings.Add($"{time:F2}s stuck: pushing toward a {target.Def.Kind} {dist:F1} m away (reach {reach:F1}) but not moving for 2 s. " +
                                    "Hero state every 0.4 s: " + string.Join(" | ", _stuckWhy));
        }

        // What can hold the hero still: pause, a non-gameplay input context, the attack machine (MoveScale 0 while busy), hurt lock (0.2), not grounded.
        private string State()
        {
            AttackData a = _combat.CurrentAttack;
            return $"moveScale {_combat.MoveScale:F1}, attack {(a != null ? a.Id + " @" + _combat.AttackFrame.ToString("F0") + "/" + a.TotalFrames : "none")}, " +
                   $"paused {HeroInput.IsPaused()}, ctx {_input.Context}, grounded {_loco.IsGrounded}, stick {_input.Frame.MoveX:F1},{_input.Frame.MoveY:F1}, vel {_loco.Velocity.magnitude:F2}, " +
                   $"at {_hero.position.x:F1},{_hero.position.y:F1},{_hero.position.z:F1}, blocked by {Blocker()}";
        }

        // A player walks around a pillar instead of into it: when the capsule would hit level geometry within half a metre, slide along its face,
        // toward whichever side keeps the target closer. Enemies and pickups are not obstacles (the hero walks into them on purpose).
        private Vector3 AroundObstacles(Vector3 dir, Vector3 goal)
        {
            if (!Probe(dir, 0.5f, out RaycastHit h)) return dir;
            Vector3 n = Flat(h.normal).normalized;
            Vector3 slide = Flat(Vector3.ProjectOnPlane(dir, n));
            if (slide.sqrMagnitude < 0.04f) slide = Vector3.Cross(Vector3.up, n);   // head-on: pick a side
            slide.Normalize();
            Vector3 alt = -slide, toGoal = Flat(goal - _hero.position).normalized;
            if (Vector3.Dot(alt, toGoal) > Vector3.Dot(slide, toGoal) + 0.05f && Vector3.Dot(dir, n) < -0.9f) slide = alt;
            return (slide + n * 0.15f).normalized;   // a touch off the face so the capsule doesn't scrape
        }

        private bool Probe(Vector3 dir, float distance, out RaycastHit hit)
        {
            var cc = _hero.GetComponent<CharacterController>();
            float r = cc != null ? cc.radius : 0.35f;
            Vector3 a = _hero.position + Vector3.up * (r + 0.05f), b = _hero.position + Vector3.up * ((cc != null ? cc.height : 1.8f) - r);
            foreach (RaycastHit h in Physics.CapsuleCastAll(a, b, r * 0.95f, dir, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform root = h.collider.transform.root;
                if (root == _hero.root || h.collider.GetComponentInParent<Enemy>() != null || h.collider.GetComponentInParent<HealingDrop>() != null) continue;
                hit = h; return true;
            }
            hit = default; return false;
        }

        private string Blocker()
        {
            float yaw = _loco.ViewYawDeg * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 dir = (right * _input.Frame.MoveX + forward * _input.Frame.MoveY).normalized;
            var cc = _hero.GetComponent<CharacterController>();
            float r = cc != null ? cc.radius : 0.35f;
            Vector3 a = _hero.position + Vector3.up * (r + 0.05f), b = _hero.position + Vector3.up * ((cc != null ? cc.height : 1.8f) - r);
            foreach (RaycastHit h in Physics.CapsuleCastAll(a, b, r * 0.95f, dir, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.transform.root != _hero.root) return $"{Path(h.collider.transform)} ({h.collider.GetType().Name}, {h.distance:F2} m)";
            return "nothing within 0.6 m";
        }

        private static string Path(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 3 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        private Enemy Nearest(out float best)
        {
            if (Preferred != null && Preferred.IsSpawned && Preferred.Alive) { best = Flat(Preferred.transform.position - _hero.position).magnitude; return Preferred; }   // the caller's chosen target (the dive bot)
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
        public float Seconds, LowestHpPercent = 100f, LastHitTaken = -1f, MinGapBetweenHitsTaken = float.MaxValue, MinGapBetweenStrikes = float.MaxValue;
        public int HitsTaken, DamageTaken, HitsDealt, DamageDealt, Swings, Dodges, EnemiesSeen, EnemyAttacksStarted, HighestWave, ClearedWaves;
        public int Strikes, HitsStacked, MaxWindUpsAtOnce, MaxWaitingAtOnce, SignatureAttacks;   // pacing: strikes opened, hits taken < 0.5 s after another, peaks
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
            sb.AppendLine($"- Pacing: {Strikes} enemy strikes, closest two from different enemies {(Strikes > 1 && MinGapBetweenStrikes < float.MaxValue ? MinGapBetweenStrikes.ToString("F2") + " s" : "n/a")} apart; " +
                          $"most winding up at once {MaxWindUpsAtOnce}, most waiting for a token {MaxWaitingAtOnce}; hits taken < 0.5 s after another {HitsStacked}; Warden signatures {SignatureAttacks}");
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
