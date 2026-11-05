// Authority: test-only (a CLIENT peer's owner copy of its hero; every request goes through that peer's own DiveDirector RPCs, so the host sees what a real client sends).
// M7 S2 co-op bot. A full DiveBot cannot share a process with another (DiveDirector.Instance and the App's input service are singletons), so a client hero is driven by this
// lighter bot through its OWN FakeInputService (HeroLocomotion / TargetLockController / HeroCombat .Input are per hero): it follows the lead hero (the host's DiveBot) to within a few
// metres through the camera-relative stick, fights what is in sight with ArenaBot, eats a loaf when hurt, holds interact over a downed hero (the revive), and asks for bank / stair / go-home
// through its own DiveDirector (RequestBank / RequestStair / RequestGoHome) when it stands at that spot with the lead. Pickups and chests happen by proximity as the lead walks them.
// It does NOT bake a NavMesh (the host DiveBot's bake is process-global and every peer builds its floor at the same place): it queries that one and falls back to a straight steer.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class FollowerBot
    {
        public const float FollowStart = 3.5f, FollowStop = 2.5f;

        readonly FakeInputService _input;
        readonly Transform _hero;
        readonly NetworkManager _nm;
        readonly DiveDirector _dir;
        readonly System.Func<Transform> _lead;
        readonly HeroLocomotion _loco;
        readonly HeroVitals _vitals;
        readonly CharacterController _cc;
        readonly ArenaBot _fighter;
        readonly NavMeshPath _path = new NavMeshPath();
        readonly List<Vector3> _corners = new List<Vector3>();
        readonly HashSet<string> _seen = new HashSet<string>();

        public readonly int Index;
        public readonly List<string> Findings = new List<string>();
        public readonly List<string> Log = new List<string>();
        public float Seconds, MovedMeters, DownedSeconds, FightSeconds;
        public int BankRequests, StairRequests, PushOnRequests, ReviveHoldSeconds10, LoavesEaten, StuckEvents, Rescues, Downs;
        public float ReviveHoldSeconds => ReviveHoldSeconds10 * 0.1f;
        public bool Moving { get; private set; }

        float _nextRequest, _loafAt = -99f, _replanAt, _progressClock, _stuckAt = -1f, _farClock, _reviveTick;
        Vector3 _progressPos, _lastPos;
        int _corner, _stuckSpells;
        bool _wasUp = true, _jumpNext;
        float _downedAt = -1f;

        public FollowerBot(int index, FakeInputService input, NetworkObject ownerCopy, DiveDirector peerDirector, System.Func<Transform> lead)
        {
            Index = index; _input = input; _hero = ownerCopy.transform; _nm = ownerCopy.NetworkManager; _dir = peerDirector; _lead = lead;
            _loco = ownerCopy.GetComponent<HeroLocomotion>(); _vitals = ownerCopy.GetComponent<HeroVitals>(); _cc = ownerCopy.GetComponent<CharacterController>();
            _fighter = new ArenaBot(input, _hero) { Dodges = true, FlankFrontalBlockers = true };
            _progressPos = _lastPos = _hero.position;
        }

        public void Release() { _input.Frame = default; }
        public Vector3 Position => _hero.position;
        public bool IsUp => _vitals != null && _vitals.IsUp;

        void Say(string s) { string line = $"{Seconds,7:0.0}s: {s}"; Log.Add(line); Debug.Log($"[FOLLOWER {Index}] {s}"); }
        void Note(string kind, string text)
        {
            if (!_seen.Add(kind + "|" + text)) return;
            Findings.Add($"**{kind}** at {Seconds:0} s ({_hero.position.x:0.0}, {_hero.position.y:0.0}, {_hero.position.z:0.0}): {text}");
            Debug.Log($"[FOLLOWER {Index}] FINDING {kind}: {text}");
        }

        // ------------------------------------------------------------------------------------------------ the tick
        public void Tick(float dt)
        {
            Seconds += dt;
            Watch(dt);
            var f = new PlayerInputFrame();
            Transform lead = _lead();
            if (_vitals == null || _cc == null || lead == null || _dir == null || !_dir.IsSpawned) { _input.Frame = f; return; }
            if (!_vitals.IsUp || _dir.HasPendingTeleport) { _input.Frame = f; return; }

            if (_vitals.Loaves > 0 && _vitals.Hp < _vitals.MaxHp * 0.5f && Seconds - _loafAt > 1f) { _loafAt = Seconds; _vitals.RequestEatLoaf(); LoavesEaten++; }

            HeroVitals downed = NearestDowned(out float downedDist);
            bool enemyClose = EnemyInSight(5f);
            if (downed != null && !enemyClose)
            {
                Vector3 to = Flat(downed.transform.position - _hero.position);
                if (downedDist > HeroVitals.ReviveReach * 0.6f) Steer(ref f, to.normalized);
                else { f.InteractHeld = true; _reviveTick += dt; if (_reviveTick >= 0.1f) { _reviveTick -= 0.1f; ReviveHoldSeconds10++; } }
                _input.Frame = f;
                return;
            }
            if (EnemyInSight(9f))
            {
                FightSeconds += dt;
                _fighter.Tick(dt, Seconds);
                _progressPos = _hero.position; _progressClock = 0f;
                return;
            }

            Requests(lead.position);

            Vector3 toLead = Flat(lead.position - _hero.position);
            float d = toLead.magnitude;
            Moving = Moving ? d > FollowStop : d > FollowStart;
            if (!Moving) { _progressClock = 0f; _progressPos = _hero.position; _stuckSpells = 0; _farClock = 0f; _input.Frame = f; return; }
            Walk(ref f, dt, lead.position, d);
            _input.Frame = f;
        }

        // ------------------------------------------------------------------------------------------------ prompts: the same rules DiveDirector.LocalInteract applies, sent from this peer's own director
        void Requests(Vector3 lead)
        {
            if (Seconds < _nextRequest || !_dir.NetActive.Value) return;
            CellarFloorSync fs = _dir.FloorSync;
            if (fs == null || fs.Built == null || fs.Layout == null) return;
            int floor = fs.BuiltFloor;
            Vector3 me = _hero.position;
            DiveHaulSync haul = _dir.HaulSync;
            bool chosen = haul != null && haul.Current.BankChosen;
            if (DiveFloorTable.HasStair(floor, chosen) && CellarFloorBuilder.TryDescentWorld(fs.Layout, fs.Built, out Vector3 stair) && Flat(me - stair).magnitude <= DiveDirector.PromptRadius)
            {
                if (_dir.NetStairOpen.Value && Flat(lead - stair).magnitude <= DiveDirector.PromptRadius + 2.5f)
                { _dir.RequestStair(floor); StairRequests++; _nextRequest = Seconds + 2.5f; Say($"asked for the stair on floor {floor}"); }
                return;
            }
            if (DiveFloorTable.HasBank(floor) && haul != null && CellarFloorBuilder.TryBankWorld(fs.Layout, fs.Built, out Vector3 bank) && Flat(me - bank).magnitude <= DiveDirector.BankPromptRadius
                && Flat(lead - bank).magnitude <= DiveDirector.BankPromptRadius + 2.5f)
            {
                DiveHaulSync.Summary sum = haul.Current;
                if (sum.DivePhase == DivePhase.BankRoom)
                {
                    // inside the room the leader's push-on opens the shared choice: the follower joins it (a repeated press by the same player changes nothing)
                    if (sum.ChoiceKind == BankChoiceKind.PushOn) { haul.RequestPushOn(); PushOnRequests++; _nextRequest = Seconds + 2.5f; Say("joined the push-on"); }
                }
                else if (!chosen && (sum.ClearedMask & (1 << (UndercroftPlan.BankFloor - 1))) != 0) { _dir.RequestBank(); BankRequests++; _nextRequest = Seconds + 2.5f; Say($"asked for the bank room on floor {floor}"); }
            }
        }

        // ------------------------------------------------------------------------------------------------ walking
        void Walk(ref PlayerInputFrame f, float dt, Vector3 target, float dist)
        {
            if (_jumpNext) { _jumpNext = false; f.JumpPressed = true; }
            if (_corners.Count == 0 || Seconds >= _replanAt) Plan(target);
            Vector3 dir = Flat(target - _hero.position).normalized;
            if (_corners.Count > 0)
            {
                while (_corner < _corners.Count - 1 && Flat(_corners[_corner] - _hero.position).magnitude < 0.45f) _corner++;
                Vector3 to = Flat(_corners[_corner] - _hero.position);
                if (to.sqrMagnitude > 0.01f) dir = to.normalized;
            }
            Steer(ref f, dir);
            f.SprintHeld = dist > 8f;

            _progressClock += dt;
            if (_progressClock >= 2.5f)
            {
                float moved = Flat(_hero.position - _progressPos).magnitude;
                if (moved < 0.4f)
                {
                    StuckEvents++; _stuckSpells++; _jumpNext = true; _replanAt = 0f;
                    if (_stuckSpells == 3) Note("Stuck", $"following the lead ({dist:0.0} m away): moved {moved:0.00} m in 2.5 s three times running; blocked by {Blocker(dir)}");
                    if (_stuckSpells >= 6 && dist > 6f) Rescue($"stuck {_stuckSpells} spells, {dist:0} m from the lead");
                }
                else _stuckSpells = 0;
                _progressClock = 0f; _progressPos = _hero.position;
            }
            _farClock = dist > 25f ? _farClock + dt : 0f;
            if (_farClock > 30f) Rescue($"{dist:0} m from the lead for 30 s");
        }

        // A stranded follower is put back beside the lead so the run goes on; each one is a finding (a real player would walk or die trying).
        void Rescue(string why)
        {
            Transform lead = _lead(); if (lead == null) return;
            Rescues++; _stuckSpells = 0; _farClock = 0f; _corners.Clear();
            Note("Rescue", why + "; moved beside the lead");
            bool on = _cc.enabled; _cc.enabled = false;
            _hero.position = lead.position + new Vector3(1.2f, 0.1f, 0f);
            _cc.enabled = on;
            _progressPos = _lastPos = _hero.position;
        }

        void Plan(Vector3 target)
        {
            _replanAt = Seconds + 1.0f; _corners.Clear(); _corner = 0;
            if (!NavMesh.SamplePosition(_hero.position, out NavMeshHit from, 2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(target, out NavMeshHit to, 2f, NavMesh.AllAreas)) return;
            if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete) _corners.AddRange(_path.corners);
        }

        void Steer(ref PlayerInputFrame f, Vector3 worldDir)
        {
            float yaw = _loco.ViewYawDeg * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            var right = new Vector3(forward.z, 0f, -forward.x);
            f.MoveX = Mathf.Clamp(Vector3.Dot(worldDir, right), -1f, 1f);
            f.MoveY = Mathf.Clamp(Vector3.Dot(worldDir, forward), -1f, 1f);
        }

        string Blocker(Vector3 dir)
        {
            float r = _cc.radius;
            Vector3 a = _hero.position + Vector3.up * (r + _cc.stepOffset), b = _hero.position + Vector3.up * (_cc.height - r);
            foreach (RaycastHit h in Physics.CapsuleCastAll(a, b, r * 0.95f, dir, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.transform.root != _hero.root) return $"{h.collider.transform.parent?.name}/{h.collider.name} ({h.collider.GetType().Name}, {h.distance:0.00} m)";
            return "nothing at body height";
        }

        // ------------------------------------------------------------------------------------------------ senses
        HeroVitals NearestDowned(out float dist)
        {
            dist = float.MaxValue; HeroVitals pick = null;
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (v == null || !v.IsSpawned || v == _vitals || v.NetworkManager != _nm || !v.IsDowned) continue;
                float d = Flat(v.transform.position - _hero.position).magnitude;
                if (d < dist) { dist = d; pick = v; }
            }
            return pick;
        }

        // Only this peer's own copies of the enemies (one process holds every peer's copy of each).
        bool EnemyInSight(float range)
        {
            Vector3 eye = _hero.position + Vector3.up * 1.2f;
            foreach (Enemy e in Enemy.All)
            {
                if (e == null || !e.IsSpawned || !e.Alive || e.NetworkManager != _nm) continue;
                Vector3 to = e.transform.position + Vector3.up * 0.8f - eye;
                if (Flat(to).magnitude > range || Mathf.Abs(to.y) > 3f) continue;
                bool blocked = false;
                foreach (RaycastHit h in Physics.RaycastAll(eye, to.normalized, to.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (h.collider.transform.root != _hero.root && h.collider.GetComponentInParent<Enemy>() == null) { blocked = true; break; }
                if (!blocked) return true;
            }
            return false;
        }

        void Watch(float dt)
        {
            Vector3 p = _hero.position;
            float step = Flat(p - _lastPos).magnitude;
            if (step < 3f) MovedMeters += step;   // teleports (dive start, a stair, a rescue) are not walking
            _lastPos = p;
            if (_vitals == null) return;
            bool up = _vitals.IsUp;
            if (_wasUp && !up) { Downs++; _downedAt = Seconds; Say($"went down (HP {_vitals.Hp})"); }
            if (!up) DownedSeconds += dt;
            if (!_wasUp && up) Say($"back up after {Seconds - _downedAt:0} s");
            _wasUp = up;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public string Summary() =>
            $"follower {Index}: {Seconds:0} s, walked {MovedMeters:0} m, downs {Downs} ({DownedSeconds:0} s down), revive holds {ReviveHoldSeconds:0.0} s, requests bank {BankRequests} / stair {StairRequests} / push-on {PushOnRequests}, loaves {LoavesEaten}, fight {FightSeconds:0} s, stuck spells {StuckEvents}, rescues {Rescues}, findings {Findings.Count}";

        public BotReport Fight => _fighter.Report;
    }
}
