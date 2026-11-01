// Authority: host. The host owns an enemy's HP, AI, movement, attacks, rage, revenge and parts (an EnemyModel from Core); it writes the NetworkVariables
// below and nothing else does. Clients only read them: HP for the bars, the attack serial + id + origin so every peer can run the same telegraph and
// hitbox clock locally. Enemy hits on PLAYERS are judged by the victim's client (HeroCombat) against this clock, and the host re-checks only rules
// (the enemy is alive and that attack serial is the one it is performing, or just ended). Player hits on enemies arrive as intents (CombatHost).
// Greybox: simple state machines, no navmesh (an open arena with pillars): Chase -> Attack (telegraph, hit-on, recover) -> Recover.
// Attack tokens (host only, never replicated; AttackPacing): before starting an attack the host asks for a token on its target hero. At most two enemies
// wind up at one hero at once and their strikes land a gap apart; a refused enemy hovers at the edge of its reach and strafes until it gets one. Tokens
// lapse when the hit window closes and are released early on stagger, parry, death and despawn. The Warden's signature plays alone: while others still
// hold tokens on its hero it reserves the hero (no new tokens there) and starts once they lapse. Its revenge break-free takes a forced hold (no checks,
// but later attacks keep their gap from it). The choice of attack is Core EnemyAttackChooser (the signature on a cadence of AI-chosen attacks).
// Combat depth (host): poise, steadiness after a flinch, the committed window from the glint, openings, the broom's guard count and its riposte, and the
// perfect-dodge stumble (Core Poise.cs, EnemyModel). NetFlags mirrors steady / braced for the visuals; the committed and open windows are pure functions
// of the replicated phase and attack clock, so every peer evaluates them for cosmetics while only the host's answer changes the fight.
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
        public readonly NetworkVariable<byte> NetFlags = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public const byte FlagSteady = 1, FlagBraced = 2;

        // ---- host only ----
        public EnemyModel Model { get; private set; }
        public readonly List<HitRecord> HitLog = new List<HitRecord>();
        public bool AiEnabled = true;                    // tests turn the AI off and drive attacks by hand
        public System.Action<Enemy> ServerDied;
        public System.Action<Enemy> ServerDespawned;     // host: this enemy left the world by any path (kill, clear, wipe, test); NetworkObjectId is still valid

        // One counter for every enemy on this host, so an attack serial is never seen twice even when NetworkObjectIds are recycled (Bootstrap recycles them
        // after 120 s): the victim's judged-set and the host's dedupe key on (enemy id, serial) and must never meet a stale pair.
        private static int s_nextAttackSerial;

        // Host: the room's crowd (CombatHost owns it) holds who may attack which hero right now (Core AttackTokens; keys are the Unity objects' hash codes,
        // their instance ids, unique across the NetworkManagers of a test process). The tokens are the host's single set, shared by every room's crowd; an enemy
        // spawned outside CombatHost gets a private crowd (and tokens) of its own.
        public EnemyCrowd Crowd { get; private set; }
        private AttackTokens Tokens => (Crowd ??= new EnemyCrowd(new AttackTokens())).Tokens;
        private TargetLedger Targets => (Crowd ??= new EnemyCrowd(new AttackTokens())).Targets;
        public void ServerJoinCrowd(EnemyCrowd crowd) { Crowd = crowd; }

        private Targetable _targetable;
        private float _staggerLeft, _recoverLeft, _cooldown, _despawnIn = -1f;
        private Vector3 _knock;
        private float _attackFrameServer;
        private double _attackEndedAt = -10.0;
        private int _lastAttackSerial;
        private Vector3 _lungeDir;
        private float _initialCooldown = 0.6f;
        private Vector3 _scatterDir;
        // Host: the decisions (Core AiBrain, stage 1 of docs/research/enemy-ai.md): token retry, hover/strafe, the attack choice and the signature cadence.
        private AiBrain _brain;
        private readonly AiContext _ctx = new AiContext();
        private readonly List<HeroVitals> _heroBuf = new List<HeroVitals>();   // parallel to _ctx.Heroes
        private static readonly System.Func<float> s_rand = () => Random.value;
        private AiBrain Brain => _brain ??= new AiBrain(Tokens, s_rand, Def, Hunts);
        // Host: true for enemies spawned without a room (arena waves, the hunter): they chase from anywhere. Room enemies use their aggro/leash ranges. Set before the first tick.
        public bool Hunts;
        private CombatHost _host; private int _room = EnemyCrowd.ArenaRoom;   // host: the room this enemy belongs to (room scope of its aggro, doc 6.4)
        public void ServerBindRoom(CombatHost host, int room) { _host = host; _room = room; }
        public int Room => _room;
        private Vector3 _home; private bool _homeSet;   // host: where it spawned; after a leash it walks back here
        private const float HomeArrive = 1f;
        private const float HomeGiveUp = 8f;   // host: a walk home blocked by a corner stops here and idles instead of grinding the wall
        private float _homeWalk;
        private float _bracedLeft;                       // host: seconds the broom still shows its brace after a blocked hit
        public const float BracedShowSeconds = 0.35f;

        // ---- local on every peer ----
        private float _attackClock;                      // seconds since this peer saw the current attack begin
        private int _seenSerial;
        private float _flash, _flashHold, _hitReact, _hitHeavy;
        private float _localHitAt = -10f;
        private Vector3 _hitDir;

        public EnemyDefinition Def => EnemyCatalog.Get(NetKind.Value);
        public int Tier => NetTier.Value;
        public EnemyPhase Phase => (EnemyPhase)NetPhase.Value;
        public bool Alive => Phase != EnemyPhase.Dead && NetHp.Value > 0;
        public int Serial => NetAttackSerial.Value;
        public AttackData CurrentAttack => NetAttackIdx.Value >= 0 && NetAttackIdx.Value < AttackCatalog.AllEnemyAttacks.Length ? AttackCatalog.AllEnemyAttacks[NetAttackIdx.Value] : null;
        public float AttackFrame => _attackClock * AttackData.FrameRate;
        public bool Attacking => CurrentAttack != null && AttackFrame < CurrentAttack.TotalFrames;
        public bool HitboxOn => Attacking && CurrentAttack.HitOn.Contains(AttackFrame);
        public bool WindingUp => Attacking && AttackFrame < CurrentAttack.HitOn.Start;
        public bool WaitingForToken => _brain != null && _brain.WaitingForToken;   // host only (tests, the bot report)
        // Combat depth windows, on every peer (cosmetic on clients; the host decides with its own clock in ServerApplyHit).
        public bool CommittedNow => Attacking && EnemyWindows.Committed(CurrentAttack, AttackFrame);
        public bool OpenNow => Phase == EnemyPhase.Stagger || Phase == EnemyPhase.Recover || Attacking && EnemyWindows.Spent(CurrentAttack, AttackFrame);
        public bool Steady => (NetFlags.Value & FlagSteady) != 0;
        public bool Braced => (NetFlags.Value & FlagBraced) != 0;
        // Hit feedback for presentation (every peer): Flash is 1 (full white) for FlashHoldSeconds after a landed hit, then fades in FlashFadeSeconds.
        // HitReact is 1 on the hit and falls to 0 over HitReactSeconds (a squash and a small shove away from the attacker, HitDirection, scaled by HitHeavy).
        public const float FlashHoldSeconds = 0.08f, FlashFadeSeconds = 0.12f, HitReactSeconds = 0.2f;
        public float Flash => _flashHold > 0f ? 1f : _flash;
        public float HitReact => _hitReact;
        public float HitHeavy => _hitHeavy;
        public Vector3 HitDirection => _hitDir;   // world, flat, from the attacker toward this enemy (zero when unknown)
        public Targetable Target => _targetable;

        // The centre of the attack's hitbox: the enemy itself, or the spot a lob was aimed at.
        public Vector3 AttackCentre => CurrentAttack != null && CurrentAttack.Origin == HitOrigin.GroundAtTarget ? NetAttackOrigin.Value : transform.position;
        public float YawDeg => transform.eulerAngles.y;

        // The attacker's own client calls this at the moment of contact (before the host answers), so the flash and squash are instant. The NetHp change that
        // follows a round trip later is then ignored for a moment (OnHpChanged), so one hit never flashes twice. heavy: 0 light .. 1 finisher or charge.
        // Combat depth: the client cannot know whether the host will flinch the enemy (poise is host state), so contact plays only a small reaction
        // (ContactReact); the full flinch is the replicated Stagger phase. A committed enemy (glint to end of hitbox) barely budges: the clank.
        public const float ContactReact = 0.4f, CommittedReact = 0.12f;
        public void ShowHit(Vector3 attackerPos, float heavy)
        {
            _localHitAt = Time.time;
            Vector3 away = transform.position - attackerPos;
            away.y = 0f;
            if (CommittedNow) StartHitFeedback(Vector3.zero, 0f, CommittedReact);
            else StartHitFeedback(away, heavy, ContactReact);
        }

        private void StartHitFeedback(Vector3 awayFlat, float heavy, float react)
        {
            _flashHold = FlashHoldSeconds;
            _flash = 1f;
            _hitReact = Mathf.Clamp01(react);
            _hitHeavy = Mathf.Clamp01(heavy);
            _hitDir = awayFlat.sqrMagnitude > 1e-4f ? awayFlat.normalized : Vector3.zero;
        }

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
        private static void ResetStatics() { s_all.Clear(); s_nextAttackSerial = 0; }

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
                Crowd?.Register(this);
                if (Model == null) Model = new EnemyModel(Def, Tier, 1);
                _targetable.Alive = true;
                _cooldown = _initialCooldown;
                _home = transform.position; _homeSet = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            NetAttackSerial.OnValueChanged -= OnSerialChanged;
            NetHp.OnValueChanged -= OnHpChanged;
            NetPhase.OnValueChanged -= OnPhaseChanged;
            _targetable.Alive = false;
            if (IsServer)
            {
                Crowd?.Unregister(this);
                ServerDespawned?.Invoke(this);
            }
        }

        private void OnSerialChanged(int previous, int current)
        {
            _attackClock = 0f;
            _seenSerial = current;
            // No generic telegraph cue any more: EnemyVisual plays this attack's own wind-up, hold and glint sounds from its EnemyTell.
        }

        private void OnHpChanged(int previous, int current)
        {
            if (current < previous && Time.time - _localHitAt > 0.5f) StartHitFeedback(Vector3.zero, 0.3f, ContactReact);   // someone else's hit (or a remote client's): no direction known
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
            if (_flashHold > 0f) _flashHold -= dt;
            else if (_flash > 0f) _flash = Mathf.Max(0f, _flash - dt / FlashFadeSeconds);
            if (_hitReact > 0f) _hitReact = Mathf.Max(0f, _hitReact - dt / HitReactSeconds);
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
            if (_bracedLeft > 0f) _bracedLeft -= dt;
            byte flags = (byte)((Model.Poise.Steady ? FlagSteady : 0) | (_bracedLeft > 0f ? FlagBraced : 0));
            if (NetFlags.Value != flags) NetFlags.Value = flags;

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
            // The flinch runs out with or without the AI (tests turn it off), so steadiness and the end of the opening are the same in both.
            if (Phase == EnemyPhase.Stagger)
            {
                _staggerLeft -= dt;
                if (_staggerLeft <= 0f)
                {
                    Model.Poise.BeginSteady();   // one flinch per exchange: steady for a moment, so flinch loops cannot stunlock it
                    SetPhase(AiEnabled ? EnemyPhase.Chase : EnemyPhase.Idle);
                }
                DropTarget();   // a flinching enemy is not on anyone: its load no longer counts against the hero
                return;
            }
            if (!AiEnabled) { DropTarget(); if (Phase == EnemyPhase.Attack) ServerAdvanceAttack(dt, null); return; }

            // Threat targeting (stage 3): locked while telling or striking, so a target switch never happens mid-attack.
            HeroVitals target = PickTarget(dt, locked: Phase == EnemyPhase.Attack);
            if (_cooldown > 0f) _cooldown -= dt;
            if (Phase == EnemyPhase.Attack) { ServerAdvanceAttack(dt, target); return; }
            if (target == null) { ServerReturnHome(dt); return; }
            _homeWalk = 0f;

            // The brain decides (Core AiBrain); this applies its action. PickTarget just filled the context's hero snapshot.
            FillContext(dt);
            AiAction act = Brain.Tick(_ctx);
            if (Phase == EnemyPhase.Recover)
            {
                _recoverLeft = act.RecoverLeft;
                if (act.MoveSpeed > 0f) MoveHorizontal(new Vector3(act.MoveX, 0f, act.MoveZ), act.MoveSpeed, dt);
                else FaceToward(new Vector3(act.FaceX, 0f, act.FaceZ), act.FaceDegPerSec, dt);
                if (act.State != AiState.Recover) SetPhase(EnemyPhase.Chase);
                return;
            }

            SetPhase(EnemyPhase.Chase);
            FaceToward(new Vector3(act.FaceX, 0f, act.FaceZ), act.FaceDegPerSec, dt);
            if (act.StartAttack != null) { ServerStartAttack(act.StartAttack, target.transform.position); return; }
            if (act.MoveSpeed > 0f) MoveHorizontal(new Vector3(act.MoveX, 0f, act.MoveZ), act.MoveSpeed, dt);
        }

        // Host: forget the current target and its ledger entry.
        private void DropTarget() { Targets.Remove(GetHashCode()); _brain?.Threat.ClearCurrent(); }

        // Host: nobody in range (leashed, or all down). Room enemies walk back to where they spawned (target = home), then idle there within 1 m.
        private void ServerReturnHome(float dt)
        {
            Vector3 p = transform.position;
            float dx = _home.x - p.x, dz = _home.z - p.z;
            if (Hunts || !_homeSet || dx * dx + dz * dz <= HomeArrive * HomeArrive || _homeWalk >= HomeGiveUp) { SetPhase(EnemyPhase.Idle); return; }
            _homeWalk += dt;
            SetPhase(EnemyPhase.Chase);
            Vector3 dir = new Vector3(dx, 0f, dz).normalized;
            FaceToward(dir, 420f, dt);
            MoveHorizontal(dir, Def.MoveSpeed * 0.7f, dt);
        }

        private void FillContext(float dt)
        {
            AiContext c = _ctx;
            Vector3 p = transform.position;
            c.SelfId = GetHashCode();
            c.Def = Def;
            c.Tier = Tier;
            c.Raging = Model.Rage.State == RageState.Raging;
            c.SelfX = p.x; c.SelfY = p.y; c.SelfZ = p.z;
            c.Dt = dt;
            c.Time = Time.timeAsDouble;
            c.Phase = Phase == EnemyPhase.Recover ? AiState.Recover : AiState.Approach;
            c.CooldownLeft = _cooldown;
            c.RecoverLeft = _recoverLeft;
            c.ScatterX = _scatterDir.x; c.ScatterZ = _scatterDir.z;
        }

        // Host: record that `heroId` (a HeroVitals hash code) hurt this enemy; recent damage pulls its attention (ThreatTable).
        public void ServerRecordThreat(int heroId, float damage) { Brain.Threat.RecordHit(heroId, damage); }

        // Snapshots the living heroes of this world into the context, each with the number of OTHER enemies already on it (host-wide ledger).
        private void SnapshotHeroes()
        {
            _ctx.Heroes.Clear();
            _heroBuf.Clear();
            int self = GetHashCode();
            // the room override (always noticed, never leashed) holds only while this enemy is itself inside its room: one that followed a hero into a corridor leashes normally
            bool selfInRoom = _room >= 0 && _host != null && _host.RoomOf != null && _host.RoomOf(transform.position) == _room;
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (v == null || v.NetworkManager != NetworkManager || !v.IsUp) continue;
                Vector3 hp = v.transform.position;
                int id = v.GetHashCode();
                bool inRoom = selfInRoom && _host.RoomOf(hp) == _room;
                _ctx.Heroes.Add(new AiHero { Id = id, X = hp.x, Y = hp.y, Z = hp.z, Up = true, InRoom = inRoom, Load = Targets.CountOn(id, self) });
                _heroBuf.Add(v);
            }
            Vector3 p = transform.position;
            _ctx.SelfX = p.x; _ctx.SelfY = p.y; _ctx.SelfZ = p.z;
        }

        // The target for this tick by Core ThreatTable (aggro/leash, load balance, stickiness, no switch while locked); null when nobody is in range.
        private HeroVitals PickTarget(float dt, bool locked)
        {
            SnapshotHeroes();
            _ctx.Def = Def;
            _ctx.Dt = dt;
            Brain.PickTarget(_ctx, locked);
            int self = GetHashCode();
            if (_ctx.TargetIndex >= 0) Targets.Set(self, _ctx.Heroes[_ctx.TargetIndex].Id); else Targets.Remove(self);
            return _ctx.TargetIndex >= 0 ? _heroBuf[_ctx.TargetIndex] : null;
        }

        // For forced attacks (riposte, break-free): the hero this enemy is already on, else the nearest one. Does not advance the threat clock.
        private HeroVitals CurrentOrNearestHero()
        {
            SnapshotHeroes();
            int idx = -1;
            if (Brain.Threat.HasCurrent)
                for (int i = 0; i < _ctx.Heroes.Count; i++) if (_ctx.Heroes[i].Id == Brain.Threat.CurrentId) { idx = i; break; }
            if (idx < 0) idx = AiBrain.PickTarget(_ctx.Heroes, _ctx.SelfX, _ctx.SelfY, _ctx.SelfZ);
            return idx >= 0 ? _heroBuf[idx] : null;
        }

        // How far a glob can be lobbed (m): Core EnemyAttackChooser.LobReach (was 8.5; the glob is drawn in flight, EnemyVisual).
        public const float LobReach = EnemyAttackChooser.LobReach;

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
            _lastAttackSerial = ++s_nextAttackSerial;
            NetAttackSerial.Value = _lastAttackSerial;
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
                Tokens.Release(GetHashCode(), Time.timeAsDouble);
                _cooldown = AttackPacing.CooldownSeconds(Def.Kind, Tier);
                _recoverLeft = AttackPacing.RecoverSeconds(Def.Kind, Tier) + (a == AttackCatalog.WardenQuake ? AttackPacing.SignatureExtraRecoverSeconds : 0f);
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
            bool attacking = Phase == EnemyPhase.Attack && mine != null;
            h.EnemyHasSuperArmor = attacking && mine.SuperArmor.Contains(_attackFrameServer);
            h.EnemyCommitted = attacking && EnemyWindows.Committed(mine, _attackFrameServer);
            h.EnemyOpen = ServerOpen;
            h.EnemyStaggered = Phase == EnemyPhase.Stagger;
            h.EnemyAttacking = attacking;
            EnemyHitResult r = Model.ApplyHit(h);
            if (r.Damage <= 0 && !r.Killed) return r;
            NetHp.Value = Model.Hp;

            Vector3 away = transform.position - attackerPos;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;

            if (r.Killed)
            {
                Crowd?.Unregister(this);
                _lastAttackData = null;
                NetAttackIdx.Value = -1;
                SetPhase(EnemyPhase.Dead);
                _despawnIn = 1.4f;
                _targetable.Alive = false;
                ServerDied?.Invoke(this);
                return r;
            }
            if (r.PartBroken >= 0) NetPartsBroken.Value = (byte)(NetPartsBroken.Value | (1 << r.PartBroken));
            if (r.Braced) _bracedLeft = BracedShowSeconds;
            if (r.Staggered)
            {
                EndAttackNow();
                _staggerLeft = r.GuardBroken ? EnemyWindows.GuardBreakSeconds : Def.StaggerSeconds;
                _cooldown = Mathf.Max(_cooldown, 0.5f);
                SetPhase(EnemyPhase.Stagger);
            }
            else if (r.RiposteTriggered && Phase != EnemyPhase.Attack && Phase != EnemyPhase.Stagger)
            {
                // The broom's guard was hammered: it answers whatever the tokens say (like the break-free), recording a forced hold on its nearest hero so
                // later attacks keep their gap from it.
                HeroVitals near = CurrentOrNearestHero();
                if (near != null) Tokens.ForceAcquire(near.GetHashCode(), GetHashCode(), Time.timeAsDouble, AttackCatalog.BroomRiposte, AttackPacing.ForTier(Tier), Def.CategoryOf(AttackCatalog.BroomRiposte));
                else Tokens.Release(GetHashCode(), Time.timeAsDouble);
                ServerStartAttack(AttackCatalog.BroomRiposte, transform.position);
            }
            if (r.Knockback > 0f) _knock += away * r.Knockback;
            if (r.RevengeTriggered)
            {
                // Break free: a quick circle that clears everyone off it (the elite's answer to being chained). It answers the heroes hitting it, so it
                // starts whatever the tokens say, but records a forced hold on its nearest hero so the next attacks keep their gap from it.
                HeroVitals near = CurrentOrNearestHero();
                if (near != null) Tokens.ForceAcquire(near.GetHashCode(), GetHashCode(), Time.timeAsDouble, AttackCatalog.WardenBreakFree, AttackPacing.ForTier(Tier), Def.CategoryOf(AttackCatalog.WardenBreakFree));
                else Tokens.Release(GetHashCode(), Time.timeAsDouble);
                ServerStartAttack(AttackCatalog.WardenBreakFree, transform.position);
            }
            return r;
        }

        // A stagger or a parry cuts the attack short: clients stop its clock, and the grace window for a victim's report already in flight starts now
        // (only when an attack was actually running, so a stagger during Recover does not reopen the window for an old serial).
        private void EndAttackNow()
        {
            if (Phase == EnemyPhase.Attack) _attackEndedAt = Time.timeAsDouble;
            NetAttackIdx.Value = -1;
            Tokens.Release(GetHashCode(), Time.timeAsDouble);
        }

        // Host: the enemy is open (x1.5, double poise): flinching or stunned, recovering after its attack, or past the hitbox of the attack it is in.
        public bool ServerOpen
        {
            get
            {
                if (Phase == EnemyPhase.Stagger || Phase == EnemyPhase.Recover) return true;
                AttackData mine = CurrentAttack;
                return Phase == EnemyPhase.Attack && mine != null && EnemyWindows.Spent(mine, _attackFrameServer);
            }
        }

        // A perfect dodge by a player (combat depth, change 5): the enemy stumbles out of its attack and is open for a moment.
        // A late report (inside the serial's grace) only counts while the enemy is still in or just after that attack, and a second hero's report on the
        // same area attack never extends a stun already running (Max), so co-op cannot chain it.
        public void ServerPerfectDodged()
        {
            if (Phase != EnemyPhase.Attack && Phase != EnemyPhase.Recover && Phase != EnemyPhase.Stagger) return;
            EndAttackNow();
            float open = Def.IsElite ? EnemyWindows.PerfectDodgeOpenSecondsElite : EnemyWindows.PerfectDodgeOpenSeconds;
            _staggerLeft = Phase == EnemyPhase.Stagger ? Mathf.Max(_staggerLeft, open) : open;
            _cooldown = Mathf.Max(_cooldown, 0.6f);
            SetPhase(EnemyPhase.Stagger);
        }

        // A successful parry by a player: the enemy is stunned for a moment and its attack is cancelled.
        public void ServerParried()
        {
            if (Phase != EnemyPhase.Attack && Phase != EnemyPhase.Recover && Phase != EnemyPhase.Stagger) return;   // same late-report rule as the perfect dodge
            EndAttackNow();
            float stun = Def.IsElite ? 0.7f : 1.1f;
            _staggerLeft = Phase == EnemyPhase.Stagger ? Mathf.Max(_staggerLeft, stun) : stun;
            _cooldown = Mathf.Max(_cooldown, 0.6f);
            SetPhase(EnemyPhase.Stagger);
        }

        public bool PartsAllBroken
        {
            get { for (int i = 0; i < Def.Parts.Length; i++) if (PartIntact(i)) return false; return Def.Parts.Length > 0; }
        }
    }
}
