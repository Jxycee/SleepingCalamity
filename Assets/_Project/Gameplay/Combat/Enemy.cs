// Authority: host. The host owns an enemy's HP, AI, movement, attacks, rage, revenge and parts (an EnemyModel from Core); it writes the NetworkVariables
// below and nothing else does. Clients only read them: HP for the bars, the attack serial + id + origin so every peer can run the same telegraph and
// hitbox clock locally. Enemy hits on PLAYERS are judged by the victim's client (HeroCombat) against this clock, and the host re-checks only rules
// (the enemy is alive and that attack serial is the one it is performing, or just ended). Player hits on enemies arrive as intents (CombatHost).
// Greybox: simple state machines, no navmesh (an open arena with pillars): Chase -> Attack (telegraph, hit-on, recover) -> Recover.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public enum EnemyPhase : byte { Idle = 0, Chase = 1, Attack = 2, Recover = 3, Stagger = 4, Dead = 5 }

    [RequireComponent(typeof(Targetable))]
    public sealed class Enemy : NetworkBehaviour
    {
        private static readonly List<Enemy> s_all = new List<Enemy>();
        public static IReadOnlyList<Enemy> All => s_all;

        // ---- replicated (host writes) ----
        public readonly NetworkVariable<int> NetHp = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> NetMaxHp = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetKind = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetTier = new NetworkVariable<byte>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetPhase = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<int> NetAttackSerial = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<short> NetAttackIdx = new NetworkVariable<short>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<Vector3> NetAttackOrigin = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetRage = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetRevengePercent = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<byte> NetPartsBroken = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // ---- host only ----
        public EnemyModel Model { get; private set; }
        public readonly List<HitRecord> HitLog = new List<HitRecord>();
        public bool AiEnabled = true;                    // tests turn the AI off and drive attacks by hand
        public System.Action<Enemy> ServerDied;

        private Targetable _targetable;
        private float _staggerLeft, _recoverLeft, _cooldown, _despawnIn = -1f;
        private Vector3 _knock;
        private float _attackFrameServer;
        private double _attackEndedAt = -10.0;
        private int _lastAttackSerial;
        private Vector3 _lungeDir;
        private float _initialCooldown = 0.6f;
        private Vector3 _scatterDir;

        // ---- local on every peer ----
        private float _attackClock;                      // seconds since this peer saw the current attack begin
        private int _seenSerial;
        private float _flash;

        public EnemyDefinition Def => EnemyCatalog.Get(NetKind.Value);
        public int Tier => NetTier.Value;
        public EnemyPhase Phase => (EnemyPhase)NetPhase.Value;
        public bool Alive => Phase != EnemyPhase.Dead && NetHp.Value > 0;
        public int Serial => NetAttackSerial.Value;
        public AttackData CurrentAttack => NetAttackIdx.Value >= 0 && NetAttackIdx.Value < AttackCatalog.AllEnemyAttacks.Length ? AttackCatalog.AllEnemyAttacks[NetAttackIdx.Value] : null;
        public float AttackFrame => _attackClock * AttackData.FrameRate;
        public bool Attacking => CurrentAttack != null && AttackFrame < CurrentAttack.TotalFrames;
        public bool HitboxOn => Attacking && CurrentAttack.HitOn.Contains(AttackFrame);
        public float Flash => _flash;     // 0..1, set when this peer sees a hit on it; presentation fades it
        public Targetable Target => _targetable;

        // The centre of the attack's hitbox: the enemy itself, or the spot a lob was aimed at.
        public Vector3 AttackCentre => CurrentAttack != null && CurrentAttack.Origin == HitOrigin.GroundAtTarget ? NetAttackOrigin.Value : transform.position;
        public float YawDeg => transform.eulerAngles.y;

        public Vector3 PartWorldPosition(int i)
        {
            PartDefinition p = Def.Parts[i];
            Transform t = transform;
            return t.position + t.right * p.OffsetRight + t.forward * p.OffsetForward + Vector3.up * p.Height;
        }

        public bool PartIntact(int i) => i >= 0 && i < Def.Parts.Length && (NetPartsBroken.Value & (1 << i)) == 0;

        private void Awake()
        {
            _targetable = GetComponent<Targetable>();
        }

        private void OnEnable() { if (!s_all.Contains(this)) s_all.Add(this); }
        private void OnDisable() { s_all.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_all.Clear(); }

        // Host only, BEFORE Spawn: the kind, tier and party size ride in the spawn message.
        public void ServerSeed(EnemyKind kind, int tier, int players)
        {
            Model = new EnemyModel(EnemyCatalog.Get(kind), tier, players);
            NetKind.Value = (byte)kind;
            NetTier.Value = (byte)Model.Tier;
            NetMaxHp.Value = Model.MaxHp;
            NetHp.Value = Model.Hp;
        }

        public override void OnNetworkSpawn()
        {
            if (_targetable == null) _targetable = GetComponent<Targetable>();
            _targetable.IsElite = Def.IsElite;
            _targetable.Alive = Alive;
            _seenSerial = NetAttackSerial.Value;
            _attackClock = 999f;
            NetAttackSerial.OnValueChanged += OnSerialChanged;
            NetHp.OnValueChanged += OnHpChanged;
            NetPhase.OnValueChanged += OnPhaseChanged;
            if (IsServer)
            {
                if (Model == null) Model = new EnemyModel(Def, Tier, 1);
                _targetable.Alive = true;
                _cooldown = _initialCooldown;
            }
        }

        public override void OnNetworkDespawn()
        {
            NetAttackSerial.OnValueChanged -= OnSerialChanged;
            NetHp.OnValueChanged -= OnHpChanged;
            NetPhase.OnValueChanged -= OnPhaseChanged;
            _targetable.Alive = false;
        }

        private void OnSerialChanged(int previous, int current)
        {
            _attackClock = 0f;
            _seenSerial = current;
            CombatEvents.RaiseCue(CombatCue.EnemyTelegraph, transform.position);
        }

        private void OnHpChanged(int previous, int current)
        {
            if (current < previous) _flash = 1f;
        }

        private void OnPhaseChanged(byte previous, byte current)
        {
            _targetable.Alive = (EnemyPhase)current != EnemyPhase.Dead;
            if ((EnemyPhase)current == EnemyPhase.Dead) CombatEvents.RaiseCue(CombatCue.EnemyDie, transform.position);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_attackClock < 999f) _attackClock += dt;
            if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt * 6f);
            if (IsServer && IsSpawned) ServerUpdate(dt);
        }

        // =================================================================================================================================
        // Host: AI
        // =================================================================================================================================

        private void ServerUpdate(float dt)
        {
            if (Model == null) return;
            Model.Tick(dt);
            byte rage = (byte)Model.Rage.State;
            if (NetRage.Value != rage) NetRage.Value = rage;
            byte revenge = (byte)Mathf.RoundToInt(Model.Revenge.Fill * 100f);
            if (NetRevengePercent.Value != revenge) NetRevengePercent.Value = revenge;

            if (Phase == EnemyPhase.Dead)
            {
                if (_despawnIn > 0f)
                {
                    _despawnIn -= dt;
                    if (_despawnIn <= 0f && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
                }
                return;
            }

            ServerApplyKnock(dt);
            if (!AiEnabled) { if (Phase == EnemyPhase.Attack) ServerAdvanceAttack(dt, null); return; }

            if (Phase == EnemyPhase.Stagger)
            {
                _staggerLeft -= dt;
                if (_staggerLeft <= 0f) SetPhase(EnemyPhase.Chase);
                return;
            }

            HeroVitals target = PickTarget();
            if (_cooldown > 0f) _cooldown -= dt;
            if (Phase == EnemyPhase.Attack) { ServerAdvanceAttack(dt, target); return; }
            if (target == null) { SetPhase(EnemyPhase.Idle); return; }

            Vector3 to = target.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (Phase == EnemyPhase.Recover)
            {
                _recoverLeft -= dt;
                if (Def.Kind == EnemyKind.RatKing) MoveHorizontal(_scatterDir, Def.MoveSpeed * 0.9f, dt);   // scatter and regroup
                else FaceToward(to, 240f, dt);
                if (_recoverLeft <= 0f) SetPhase(EnemyPhase.Chase);
                return;
            }

            SetPhase(EnemyPhase.Chase);
            FaceToward(to, Def.Kind == EnemyKind.DoughWarden ? 200f : 420f, dt);
            AttackData next = ChooseAttack(dist);
            if (next != null && _cooldown <= 0f) { ServerStartAttack(next, target.transform.position); return; }
            float stop = Def.Radius + 0.55f;
            if (dist > stop + 0.3f) MoveHorizontal(to / Mathf.Max(0.001f, dist), Def.MoveSpeed * (Model.Rage.State == RageState.Raging ? 1.25f : 1f), dt);
        }

        private HeroVitals PickTarget()
        {
            HeroVitals best = null;
            float bestSq = float.MaxValue;
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (v == null || v.NetworkManager != NetworkManager || !v.IsUp) continue;
                float sq = (v.transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = v; }
            }
            return best;
        }

        private float Reach(AttackData a)
        {
            if (a.Origin == HitOrigin.GroundAtTarget) return 8.5f;
            return (a.Shape == HitShape.Circle ? a.Range : a.Range + a.LungeMeters * 0.7f) + 0.2f;
        }

        private AttackData ChooseAttack(float dist)
        {
            AttackData pick = null;
            float roll = Random.value;
            AttackData[] attacks = Def.Attacks;
            int start = Mathf.Clamp(Mathf.FloorToInt(roll * attacks.Length), 0, attacks.Length - 1);
            for (int k = 0; k < attacks.Length; k++)
            {
                AttackData a = attacks[(start + k) % attacks.Length];
                bool lob = a.Origin == HitOrigin.GroundAtTarget;
                if (lob && dist < 3.2f) continue;
                if (dist > Reach(a)) continue;
                if (a.Shape == HitShape.Circle && !lob && dist > a.Range + Def.Radius + 0.4f) continue;
                pick = a;
                break;
            }
            return pick;
        }

        private void FaceToward(Vector3 flatDir, float degPerSec, float dt)
        {
            if (flatDir.sqrMagnitude < 1e-4f) return;
            float yaw = MovementMath.TurnToward(transform.eulerAngles.y, MovementMath.YawDeg(flatDir.x, flatDir.z), degPerSec * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void MoveHorizontal(Vector3 dir, float speed, float dt)
        {
            Vector3 step = dir * (speed * dt);
            if (step.sqrMagnitude < 1e-8f) return;
            Vector3 pos = transform.position;
            Vector3 flat = new Vector3(step.x, 0f, step.z);
            if (!Blocked(pos + flat)) { transform.position = pos + flat; return; }
            Vector3 sx = new Vector3(step.x, 0f, 0f), sz = new Vector3(0f, 0f, step.z);
            if (Mathf.Abs(step.x) > Mathf.Abs(step.z)) { if (!Blocked(pos + sx)) transform.position = pos + sx; else if (!Blocked(pos + sz)) transform.position = pos + sz; }
            else { if (!Blocked(pos + sz)) transform.position = pos + sz; else if (!Blocked(pos + sx)) transform.position = pos + sx; }
        }

        private bool Blocked(Vector3 p)
        {
            float r = Def.Radius * 0.9f;
            return Physics.CheckCapsule(p + Vector3.up * (r + 0.2f), p + Vector3.up * (Def.Height - r), r, Layers.Mask(Layers.Structure), QueryTriggerInteraction.Ignore);
        }

        private void ServerApplyKnock(float dt)
        {
            if (_knock.sqrMagnitude < 1e-4f) return;
            Vector3 step = _knock * Mathf.Min(1f, dt * 8f);
            _knock -= step;
            Vector3 pos = transform.position;
            if (!Blocked(pos + step)) transform.position = pos + step;
        }

        private void SetPhase(EnemyPhase p)
        {
            if (NetPhase.Value != (byte)p) NetPhase.Value = (byte)p;
        }

        // ---- attacks ----

        public void ServerStartAttack(AttackData a, Vector3 targetPos)
        {
            int idx = System.Array.IndexOf(AttackCatalog.AllEnemyAttacks, a);
            if (idx < 0) return;
            _attackFrameServer = 0f;
            _lastAttackData = a;
            _lungeDir = transform.forward;
            NetAttackOrigin.Value = a.Origin == HitOrigin.GroundAtTarget ? new Vector3(targetPos.x, transform.position.y, targetPos.z) : transform.position;
            NetAttackIdx.Value = (short)idx;
            NetAttackSerial.Value = ++_lastAttackSerial;
            SetPhase(EnemyPhase.Attack);
        }

        private void ServerAdvanceAttack(float dt, HeroVitals target)
        {
            AttackData a = CurrentAttack;
            if (a == null) { SetPhase(EnemyPhase.Chase); return; }
            _attackFrameServer += dt * AttackData.FrameRate;
            if (target != null && a.Turn.Contains(_attackFrameServer))
            {
                Vector3 to = target.transform.position - transform.position;
                to.y = 0f;
                FaceToward(to, a.TurnDegPerSec, dt);
                _lungeDir = transform.forward;
            }
            if (a.LungeMeters > 0f && a.Lunge.Contains(_attackFrameServer))
            {
                float speed = a.LungeMeters / ((a.Lunge.End - a.Lunge.Start) / (float)AttackData.FrameRate);
                MoveHorizontal(_lungeDir, speed, dt);
            }
            if (_attackFrameServer >= a.TotalFrames)
            {
                _attackEndedAt = Time.timeAsDouble;
                NetAttackIdx.Value = -1;
                _cooldown = Def.Kind == EnemyKind.DoughWarden ? 1.4f : Def.Kind == EnemyKind.RatKing ? 0.8f : 1.2f;
                _recoverLeft = Def.Kind == EnemyKind.RatKing ? 0.9f : 0.35f;
                Vector3 away = target != null ? transform.position - target.transform.position : -transform.forward;
                away.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, away.normalized) * (Random.value < 0.5f ? 1f : -1f);
                _scatterDir = (away.normalized + side * 0.8f).normalized;
                SetPhase(EnemyPhase.Recover);
            }
        }

        // Host: is this serial an attack the enemy is performing now, or finished a moment ago? (The victim's client sees it up to a round trip late.)
        public bool ServerAcceptsSerial(int serial)
        {
            if (serial != _lastAttackSerial || serial <= 0) return false;
            if (Phase == EnemyPhase.Attack) return true;
            return Time.timeAsDouble - _attackEndedAt < 0.75;
        }

        public AttackData ServerAttackOfSerial(int serial) => serial == _lastAttackSerial ? CurrentAttack ?? _lastAttackData : null;
        private AttackData _lastAttackData;

        // Test hook: the host makes this enemy perform an attack now, without AI.
        public void ServerForceAttack(AttackData a, Vector3 targetPos)
        {
            _lastAttackData = a;
            ServerStartAttack(a, targetPos);
        }

        // =================================================================================================================================
        // Host: damage in
        // =================================================================================================================================

        public EnemyHitResult ServerApplyHit(in EnemyHitInput input, Vector3 attackerPos, double now)
        {
            EnemyHitInput h = input;
            AttackData mine = CurrentAttack;
            h.EnemyHasSuperArmor = Phase == EnemyPhase.Attack && mine != null && mine.SuperArmor.Contains(_attackFrameServer);
            EnemyHitResult r = Model.ApplyHit(h);
            if (r.Damage <= 0 && !r.Killed) return r;
            NetHp.Value = Model.Hp;

            Vector3 away = transform.position - attackerPos;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;

            if (r.Killed)
            {
                _lastAttackData = null;
                NetAttackIdx.Value = -1;
                SetPhase(EnemyPhase.Dead);
                _despawnIn = 1.4f;
                _targetable.Alive = false;
                ServerDied?.Invoke(this);
                return r;
            }
            if (r.PartBroken >= 0) NetPartsBroken.Value = (byte)(NetPartsBroken.Value | (1 << r.PartBroken));
            if (r.Staggered)
            {
                NetAttackIdx.Value = -1;
                _staggerLeft = Def.StaggerSeconds;
                _cooldown = Mathf.Max(_cooldown, 0.5f);
                SetPhase(EnemyPhase.Stagger);
            }
            if (r.Knockback > 0f) _knock += away * r.Knockback;
            if (r.RevengeTriggered)
            {
                // Break free: a quick circle that clears everyone off it (the elite's answer to being chained).
                ServerStartAttack(AttackCatalog.WardenBreakFree, transform.position);
            }
            return r;
        }

        // A successful parry by a player: the enemy is stunned for a moment and its attack is cancelled.
        public void ServerParried()
        {
            if (Phase == EnemyPhase.Dead) return;
            NetAttackIdx.Value = -1;
            _staggerLeft = Def.IsElite ? 0.7f : 1.1f;
            _cooldown = Mathf.Max(_cooldown, 0.6f);
            SetPhase(EnemyPhase.Stagger);
        }

        public bool PartsAllBroken
        {
            get { for (int i = 0; i < Def.Parts.Length; i++) if (PartIntact(i)) return false; return Def.Parts.Length > 0; }
        }
    }
}
