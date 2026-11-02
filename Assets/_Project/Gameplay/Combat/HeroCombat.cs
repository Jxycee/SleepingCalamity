// Authority: OWNER for the swing, its hit detection, guard, aim and the judging of enemy hits on this hero (docs/PHASE1.md 6.4, P10); HOST for everything
// the intents cause (CombatHost validates them and applies damage, reactions, revenge and rewards). The owner's copy runs the Core AttackMachine and shows
// the spark, hitstop and camera kick at once; it then sends a hit intent. Enemy hits are judged here, on the victim's client, against the enemy's replicated
// attack clock and this hero's own dodge / guard state, so a visible dodge is never hit. Remote copies only mirror the owner's NetSwing / stance for visuals.
// Replicated: NetWeapon (the heirloom index; the HOST writes it when the owner asks through SetWeaponServerRpc, so it is in place before any later RPC of that
// owner is handled) and NetSwing (owner writes: swing serial + attack index, for remote visuals). Everything else rides RPCs. Every hit intent carries the
// weapon index and the chain's prior hits of its swing, so the host judges it against what the owner actually swung.
// Hurt grace (combat-timings "Recommended M3 values" 1-3). AUTHORITY: the victim's OWNER client judges grace (the same place that judges enemy hits, GuardMath.cs:3):
// a guarded or hurt outcome inside the window is dropped before EnemyHitMeServerRpc, so the host never applies damage for it; a roll or parry inside the
// window still plays its cue and reports to the host. The HOST applies damage and tells every peer how the hit looked (HurtClientRpc: heavy/knockdown flag);
// non-owner peers run a cosmetic GraceTimer from it (the pulse, no gameplay). No NetworkVariable.
// Weapon tree (M4): the owner asks (TreeActionServerRpc: fit / remove / build-up / re-route); the HOST applies it in WeaponTreeHost and pushes the snapshot back (SyncTreeOwnerRpc).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    [DefaultExecutionOrder(-20)]
    public sealed class HeroCombat : NetworkBehaviour
    {
        public const byte NoAttack = 0xFF;
        // Tunable through Core/Tuning ("heroCombat"); defaults 1 and 0.25.
        public static float KnockbackScale => SleepingCalamity.Core.Tuning.HeroCombatTuning.KnockbackScale;
        public static float HurtStagger => SleepingCalamity.Core.Tuning.HeroCombatTuning.HurtStagger;

        // The owner's view of its own growth, refreshed by the host after every change (HUD, the spell gate).
        public sealed class GrowthMirror
        {
            public string WeaponKey = WeaponCatalog.Axe;
            public int Level, Pips, PipCap = SpellPips.StartCap;
            public float Abs, LevelCost = 40f, Atk = 22f, Fire, Pest;
        }

        public static HeroCombat Local { get; private set; }   // the first local owner in this process (HUD, camera kick)

        private static readonly List<HeroCombat> s_all = new List<HeroCombat>();
        public static IReadOnlyList<HeroCombat> All => s_all;

        public readonly NetworkVariable<byte> NetWeapon = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<uint> NetSwing = new NetworkVariable<uint>(NoAttack << 16, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private AttackMachine _machine = new AttackMachine(AttackCatalog.Axe);
        private HeroLocomotion _loco;
        private TargetLockController _lock;
        private HeroVitals _vitals;
        private HeroNetState _net;
        private IInputService _input;
        private readonly HitstopBudget _budget = new HitstopBudget();
        private readonly HashSet<ulong> _swingHit = new HashSet<ulong>();
        private readonly HashSet<long> _judged = new HashSet<long>();
        private readonly GrowthMirror _mirror = new GrowthMirror();
        private int _hitsInSwing;
        private int _pendingWeapon = -1;   // owner: the heirloom asked for and not yet confirmed by the host (a second F5 inside one round trip builds on it)
        private byte _swingWeapon, _swingPrior;   // the heirloom index and the chain's prior hits when the current swing began (sent with every hit of it)
        private bool _shotFired, _wasDodging, _guarding, _aiming, _wasGuarding, _whiffChecked;
        private float _guardStart, _reviveReport, _hurtLock, _dodgeBuffer;
        private float _guardEnd = -10f, _rollEndedAt = -10f, _rollGap = 10f;   // combat depth: the parry lockout and the perfect dodge's chain rule
        private bool _parryArmed = true;
        private readonly GraceTimer _grace = new GraceTimer();   // owner: gameplay grace; other peers: cosmetic, started by HurtClientRpc
        private float _knockdownLeft;                             // owner: the hero is down for this long (no movement at all)
        private int _swingSerial, _published;
        private Vector3 _impulse;

        // remote clock (every peer): when it saw the current swing begin
        private uint _seenSwing = NoAttack << 16;
        private float _remoteClock = 999f;
        private bool _syncRequested;

        public IInputService Input
        {
            get => _input ?? HeroInput.ResolveDefault();
            set => _input = value;
        }

        public AttackMachine Machine => _machine;
        public GrowthMirror Mirror => _mirror;
        public int WeaponIndex => NetWeapon.Value;
        public int WeaponIndexOrPending => _pendingWeapon >= 0 ? _pendingWeapon : NetWeapon.Value;   // owner: where the next swap starts from
        public string WeaponKey => AttackCatalog.Heirlooms[Mathf.Clamp(NetWeapon.Value, 0, AttackCatalog.Heirlooms.Length - 1)];
        public WeaponFamily Family => WeaponStyle.FamilyOf(WeaponKey);
        public bool Guarding => _guarding;
        public bool Aiming => _aiming;
        public bool ShowGuard => IsOwner ? _guarding : _net != null && _net.StanceIsGuarding;
        public bool ShowAim => IsOwner ? _aiming : _net != null && _net.StanceIsAiming;
        public bool IsUp => _vitals == null || _vitals.IsUp;
        public bool SwingConnected => IsOwner && _hitsInSwing > 0;   // owner only: the current swing has landed a hit (the arc turns warm; a whiff stays dim)
        public int ComboCount => _machine.ComboCount;

        // The attack being shown (owner: the machine; remote: from NetSwing) and its frame (60 Hz), for the swing visuals on every peer.
        public AttackData CurrentAttack
        {
            get
            {
                if (IsOwner) return _machine.Current;
                int idx = (int)((NetSwing.Value >> 16) & 0xFF);
                if (idx == NoAttack) return null;
                AttackData a = AttackCatalog.ForFamily(Family).Get(idx);
                return a != null && _remoteClock * AttackData.FrameRate < a.TotalFrames ? a : null;
            }
        }
        public float AttackFrame => IsOwner ? _machine.Frame : _remoteClock * AttackData.FrameRate;

        // The index of the attack being shown (AttackSet index 0..7), or NoAttack: the owner's machine, or the replicated NetSwing on a remote copy.
        public int CurrentAttackIndex
        {
            get
            {
                if (IsOwner) return _machine.Active ? _machine.CurrentIndex : NoAttack;
                return CurrentAttack != null ? (int)((NetSwing.Value >> 16) & 0xFF) : NoAttack;
            }
        }

        // Local, cosmetic, every peer: counts up in HurtClientRpc (SendTo.Everyone) so the animation can flinch a remote copy too. No extra traffic.
        public int HurtSerial { get; private set; }
        public bool HurtWasHeavy { get; private set; }
        public bool HitboxOn => CurrentAttack != null && CurrentAttack.HitOn.Contains(AttackFrame);

        // ---- what HeroLocomotion asks (owner) ----
        // 0..1 red tint for presentation: one sine half-cycle over the grace window (owner only; 0 elsewhere and when not hurt).
        public float HurtPulse => _grace.Pulse;
        public bool HurtFlickerOn => _grace.FlickerOn;
        public bool HurtFlickering => _grace.Flickering;
        public bool HurtGraceActive => _grace.Active;
        public bool KnockedDown => _knockdownLeft > 0f;
        public bool AllowsDodge => IsUp && _machine.CanCancel && _hurtLock <= 0f;   // a raised guard does not eat the press: the roll lowers it (alpha playtest item 15)
        public float MoveScale => !IsUp ? 0f : _machine.Busy || _knockdownLeft > 0f ? 0.0f : _hurtLock > 0f ? 0.2f : _guarding || _aiming ? 0.5f : 1f;
        public float LungeSpeed => _machine.LungeSpeed;
        public bool Frozen => !IsUp;

        public bool WantsFacing(out float yawDeg, out float degPerSec)
        {
            yawDeg = transform.eulerAngles.y;
            degPerSec = 0f;
            if (!IsUp) return true;
            if (_aiming && _loco != null)
            {
                yawDeg = _loco.ViewYawDeg;
                degPerSec = 1080f;
                return true;
            }
            if (!_machine.Active) return false;
            if (_machine.Turning)
            {
                Targetable t = _lock != null ? (_lock.Locked != null ? _lock.Locked : _lock.Soft) : null;
                Vector3 to = Vector3.zero;
                if (t != null) { to = t.transform.position - transform.position; to.y = 0f; }
                else
                {
                    PlayerIntent intent = Input != null ? Input.Intent : default;
                    if (_loco != null && (Mathf.Abs(intent.MoveX) + Mathf.Abs(intent.MoveY)) > 0.2f)
                    {
                        float yaw = _loco.ViewYawDeg * Mathf.Deg2Rad;
                        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), right = new Vector3(fwd.z, 0f, -fwd.x);
                        to = right * intent.MoveX + fwd * intent.MoveY;
                    }
                }
                if (to.sqrMagnitude > 0.04f)
                {
                    yawDeg = MovementMath.YawDeg(to.x, to.z);
                    degPerSec = _machine.TurnDegPerSec;
                }
                return true;
            }
            return true;   // mid-swing, past the turn window: hold the facing
        }

        public void AddImpulse(Vector3 velocity) { _impulse += velocity; }
        public Vector3 ConsumeImpulse(float dt)
        {
            Vector3 v = _impulse;
            _impulse = Vector3.MoveTowards(_impulse, Vector3.zero, 22f * dt);
            return v;
        }

        // =================================================================================================================================

        private void Awake()
        {
            _loco = GetComponent<HeroLocomotion>();
            _lock = GetComponent<TargetLockController>();
            _vitals = GetComponent<HeroVitals>();
            _net = GetComponent<HeroNetState>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Local = null; s_all.Clear(); }

        public override void OnNetworkSpawn()
        {
            if (!s_all.Contains(this)) s_all.Add(this);
            NetWeapon.OnValueChanged += OnWeaponChanged;
            NetSwing.OnValueChanged += OnSwingChanged;
            _seenSwing = NetSwing.Value;
            ApplyWeapon();
            if (IsOwner)
            {
                if (Local == null) Local = this;
                _mirror.WeaponKey = WeaponKey;
                RequestSyncServerRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            NetWeapon.OnValueChanged -= OnWeaponChanged;
            NetSwing.OnValueChanged -= OnSwingChanged;
            s_all.Remove(this);
            if (Local == this) Local = null;
        }

        private void OnWeaponChanged(byte previous, byte current)
        {
            bool wasSwinging = IsOwner && _machine.Active;
            ApplyWeapon();   // a new attack set interrupts a swing in progress
            if (wasSwinging && !_machine.Active) PublishSwing(NoAttack);   // so remote copies stop showing it
            if (_pendingWeapon == current) _pendingWeapon = -1;
        }
        private void OnSwingChanged(uint previous, uint current)
        {
            _remoteClock = 0f;
            _seenSwing = current;
            if (!IsOwner && (int)((current >> 16) & 0xFF) == AttackSet.Special && Family == WeaponFamily.Bow)
            {
                // A remote archer loosed an arrow: draw its flight (a straight line to the end of its range; the hit itself is the host's business).
                Vector3 o = transform.position + Vector3.up * ShotHeight;
                CombatEvents.RaiseArrow(o, o + transform.forward * AttackCatalog.Bow.Attacks[AttackSet.Special].Range, false);
            }
        }

        private void ApplyWeapon()
        {
            _machine.SetAttackSet(AttackCatalog.ForFamily(Family));
            _mirror.WeaponKey = WeaponKey;
            _guarding = _aiming = false;
        }

        // The owner picks a heirloom (the arena's F5 key; a town swap later). The host writes NetWeapon and re-syncs the growth mirror once; this copy changes
        // its attack set when the new value arrives (at once on the host, a round trip later on a client).
        public void OwnerSetWeapon(int index)
        {
            if (!IsOwner) return;
            int i = ((index % AttackCatalog.Heirlooms.Length) + AttackCatalog.Heirlooms.Length) % AttackCatalog.Heirlooms.Length;
            if (WeaponIndexOrPending == i) return;
            _pendingWeapon = i;
            if (_machine.Active) { _machine.Interrupt(); PublishSwing(NoAttack); }
            SetWeaponServerRpc((byte)i);
        }

        [Rpc(SendTo.Server)]
        private void SetWeaponServerRpc(byte index)
        {
            if (index >= AttackCatalog.Heirlooms.Length || NetWeapon.Value == index) return;
            NetWeapon.Value = index;
            CombatHost host = CombatHost.Current;
            if (host != null) host.ServerSyncGrowth(this, force: true);
        }

        // =================================================================================================================================
        // Per frame
        // =================================================================================================================================

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_remoteClock < 999f) _remoteClock += dt;
            if (!IsSpawned || dt <= 0f) return;
            // Every peer: the owner's gameplay window (stops during pause, like the knockdown it covers), the others' cosmetic one.
            if (!IsOwner || !HeroInput.IsPaused()) _grace.Tick(dt);
            if (!IsOwner) return;
            OwnerUpdate(dt);
        }

        private void OwnerUpdate(float dt)
        {
            IInputService input = Input;
            if (input == null || _loco == null || !_loco.enabled) return;
            if (HeroInput.IsPaused()) return;
            PlayerIntent intent = input.Intent;
            if (_hurtLock > 0f) _hurtLock -= dt;
            if (_knockdownLeft > 0f) _knockdownLeft -= dt;

            if (!IsUp)
            {
                if (_machine.Active) { _machine.Interrupt(); PublishSwing(NoAttack); }
                _guarding = _aiming = false;
                PushStance();
                return;
            }

            if (input is IDevInput dev)
            {
                if (dev.WasPressed(DevKey.SwapWeapon)) OwnerSetWeapon(WeaponIndexOrPending + 1);
                if (dev.WasPressed(DevKey.SkipWave)) SkipWaveServerRpc();
            }

            bool dodging = _loco.IsDodging;
            if (dodging && !_wasDodging)
            {
                if (_machine.Active) PublishSwing(NoAttack);
                _machine.Interrupt();
                _rollGap = Time.time - _rollEndedAt;   // a chained roll (DodgeTiming.ChainLockoutSeconds) can never be perfect
            }
            else if (!dodging && _wasDodging) _rollEndedAt = Time.time;
            _wasDodging = dodging;

            WeaponFamily family = Family;
            bool free = !dodging && _machine.CanCancel && _hurtLock <= 0f;
            _guarding = family == WeaponFamily.Dagger && intent.SecondaryHeld && free;
            _aiming = family == WeaponFamily.Bow && intent.SecondaryHeld && (free || _machine.Active && _machine.Current.Kind == AttackKind.Shot);
            if (!_aiming) _machine.ClearSpecialBuffer();   // a shot press only waits while the aim button is still held
            if (_guarding && !_wasGuarding)
            {
                _guardStart = Time.time;
                _parryArmed = GuardMath.ParryArmed(Time.time - _guardEnd);   // a quick re-raise guards but cannot parry
            }
            else if (!_guarding && _wasGuarding) _guardEnd = Time.time;
            _wasGuarding = _guarding;

            var ai = new AttackInput { Pressed = intent.PrimaryPressed, Held = intent.PrimaryHeld, Released = intent.PrimaryReleased, Airborne = !_loco.IsGrounded };
            if (_guarding) ai = default;   // a raised shield does not swing
            bool startedSpecial = false;
            if (!dodging && _hurtLock <= 0f)
            {
                if (_aiming && intent.PrimaryPressed)
                {
                    ai.Pressed = false;
                    startedSpecial = _machine.RequestSpecial();   // busy (the previous shot's recovery): the press is buffered, not lost
                }
                else if (family == WeaponFamily.Staff && intent.SecondaryPressed && _mirror.Pips > 0 && _machine.CanCancel)
                {
                    startedSpecial = _machine.TryStartSpecial();
                    if (startedSpecial) _mirror.Pips--;
                }
            }

            bool started = startedSpecial;
            if (!dodging && _hurtLock <= 0f)
            {
                AttackInput tick = _aiming ? default : ai;   // aiming: the shot above is the only attack
                started |= _machine.Tick(dt, in tick);
            }
            if (started) OnSwingStarted();

            if (_machine.Active)
            {
                AttackData a = _machine.Current;
                if (_machine.HitboxSwept) DetectHits(a);   // swept: a clock that steps over a short window (the shot's single frame) still fires it
                if (!_whiffChecked && a.Kind != AttackKind.Shot && a.Kind != AttackKind.Spell && _machine.Frame >= a.HitOn.End)
                {
                    _whiffChecked = true;
                    if (_hitsInSwing == 0) CombatEvents.RaiseCue(CombatCue.Whiff, transform.position + Vector3.up);   // swung at nothing: a different, dull sound
                }
            }

            JudgeIncoming(dt);
            ReportReviveHold(intent, dt);
            PushStance();
        }

        private void OnSwingStarted()
        {
            AttackData a = _machine.Current;
            _swingHit.Clear();
            _hitsInSwing = 0;
            _shotFired = false;
            _whiffChecked = false;
            _swingSerial++;
            _swingWeapon = NetWeapon.Value;
            _swingPrior = (byte)Mathf.Clamp(_machine.PriorHits, 0, 255);
            PublishSwing((byte)_machine.CurrentIndex);
            if (a.SpellPips > 0) CastServerRpc(_swingSerial, _swingWeapon, (byte)_machine.CurrentIndex);   // the host spends the pip now, hit or miss
            CombatEvents.RaiseSwing(this, a);
            CombatEvents.RaiseCue(a.Kind == AttackKind.Spell ? CombatCue.Spell : a.Kind == AttackKind.Shot ? CombatCue.Shot : CombatCue.Swing, transform.position);
        }

        private void PublishSwing(byte attackIndex)
        {
            _published++;   // every publish is a new value, so a remote copy restarts its clock even for the same attack twice in a row
            NetSwing.Value = (uint)(_published & 0xFFFF) | ((uint)attackIndex << 16);
        }

        private void PushStance()
        {
            if (_net != null)
            {
                bool locked = _lock != null && _lock.IsLocked;
                _net.OwnerSetStance(locked, _loco.IsDodging, _guarding, _aiming);
            }
        }

        // =================================================================================================================================
        // Player hits on enemies: detect here, tell the host
        // =================================================================================================================================

        private void DetectHits(AttackData a)
        {
            if (a.Shape == HitShape.Ray)
            {
                if (!_shotFired) { _shotFired = true; FireShot(a); }
                return;
            }
            Vector3 o = transform.position;
            float yaw = transform.eulerAngles.y;
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || !e.IsSpawned || e.NetworkManager != NetworkManager || !e.Alive) continue;
                if (_swingHit.Contains(e.NetworkObjectId)) continue;
                EnemyDefinition d = e.Def;
                int part = -1;
                for (int p = 0; p < d.Parts.Length; p++)
                {
                    if (!e.PartIntact(p)) continue;
                    Vector3 pp = e.PartWorldPosition(p);
                    float r = d.Parts[p].Radius;
                    if (HitGeometry.Hits(a, o.x, o.y, o.z, yaw, pp.x, pp.y - r, pp.z, r, 2f * r)) { part = p; break; }
                }
                Vector3 ep = e.transform.position;
                bool body = HitGeometry.Hits(a, o.x, o.y, o.z, yaw, ep.x, ep.y, ep.z, d.Radius, d.Height);
                if (part < 0 && !body) continue;
                if (!MeleeSight.Clear(o, ep)) continue;   // a wall between the two: the swing does not reach through it (alpha playtest item 2)
                Vector3 point = part >= 0 ? e.PartWorldPosition(part) : ep + Vector3.up * (d.Height * 0.55f);
                RegisterHit(a, e, part, point);
            }
        }

        // Tunable through Core/Tuning ("heroCombat"); defaults 1.3, 10 and 0.25.
        public static float ShotHeight => SleepingCalamity.Core.Tuning.HeroCombatTuning.ShotHeight;
        public static float ShotAssistDeg => SleepingCalamity.Core.Tuning.HeroCombatTuning.ShotAssistDeg;
        public static float ShotRadius => SleepingCalamity.Core.Tuning.HeroCombatTuning.ShotRadius;

        private static Vector3 EnemyCentre(Enemy e) => e.transform.position + Vector3.up * (e.Def.Height * 0.5f);

        // The aimed shot. Root cause of "the bow does not fire sometimes" (docs/design/m3-combat-greybox.md): (1) its hitbox is ONE 60 Hz frame and the old
        // once-per-rendered-frame check stepped over it under 60 fps (fixed in AttackMachine.HitboxSwept); (2) a click during the previous shot's recovery was
        // dropped (RequestSpecial buffers it); (3) the ray left at 1.4 m along the hero's facing, over the top of the slime (1.0 m) and the rat-king (1.2 m), and
        // aimed at a Targetable.AimPoint that sits ABOVE a slime. Now: from chest height, along the camera's aim, snapped to the enemy nearest the crosshair
        // (within ShotAssistDeg), at its body centre, with a thick ray. Every shot draws an arrow, hit or miss.
        private void FireShot(AttackData a)
        {
            Vector3 origin = transform.position + Vector3.up * ShotHeight;
            float yaw = _loco != null ? _loco.ViewYawDeg : transform.eulerAngles.y;
            Vector3 dir = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

            Enemy aimed = null;
            Targetable t = _lock != null ? (_lock.Locked != null ? _lock.Locked : _lock.Soft) : null;
            if (t != null)
            {
                Enemy le = t.GetComponent<Enemy>();
                if (le != null && le.Alive)
                {
                    Vector3 flat = EnemyCentre(le) - origin;
                    flat.y = 0f;
                    if (flat.sqrMagnitude > 0.04f && Vector3.Angle(dir, flat) < 75f) aimed = le;
                }
            }
            if (aimed == null)
            {
                float best = float.MaxValue;
                IReadOnlyList<Enemy> all = Enemy.All;
                for (int i = 0; i < all.Count; i++)
                {
                    Enemy e = all[i];
                    if (e == null || !e.IsSpawned || e.NetworkManager != NetworkManager || !e.Alive) continue;
                    Vector3 to = EnemyCentre(e) - origin;
                    Vector3 flat = new Vector3(to.x, 0f, to.z);
                    float dist = flat.magnitude;
                    if (dist > a.Range || dist < 0.05f) continue;
                    float slack = Mathf.Asin(Mathf.Min(1f, e.Def.Radius / dist)) * Mathf.Rad2Deg;
                    float angle = Vector3.Angle(dir, flat);
                    if (angle > ShotAssistDeg + slack) continue;
                    if (angle < best) { best = angle; aimed = e; }
                }
            }
            if (aimed != null) dir = (EnemyCentre(aimed) - origin).normalized;

            int mask = Layers.Mask(Layers.Enemy) | Layers.Mask(Layers.Structure) | Layers.Mask(Layers.Ground);
            RaycastHit[] hits = Physics.SphereCastAll(origin, ShotRadius, dir, a.Range, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            Vector3 end = origin + dir * a.Range;
            bool landed = false;
            foreach (RaycastHit h in hits)
            {
                if (h.collider.gameObject.layer != Layers.Enemy) { end = h.distance > 0.001f ? h.point : origin; break; }   // a wall stops the arrow
                Enemy e = h.collider.GetComponentInParent<Enemy>();
                if (e == null || e.NetworkManager != NetworkManager || !e.Alive) continue;
                int part = -1;
                for (int p = 0; p < e.Def.Parts.Length; p++)
                {
                    if (!e.PartIntact(p)) continue;
                    Vector3 pc = e.PartWorldPosition(p);
                    Vector3 closest = origin + dir * Mathf.Max(0f, Vector3.Dot(pc - origin, dir));
                    if ((closest - pc).sqrMagnitude <= e.Def.Parts[p].Radius * e.Def.Parts[p].Radius) { part = p; break; }
                }
                end = h.distance > 0.001f ? h.point : EnemyCentre(e);   // a sphere that starts inside the capsule reports no point
                RegisterHit(a, e, part, end);
                landed = true;
                break;
            }
            CombatEvents.RaiseArrow(origin, end, landed);
        }

        private void RegisterHit(AttackData a, Enemy e, int part, Vector3 point)
        {
            _swingHit.Add(e.NetworkObjectId);
            _hitsInSwing++;
            _machine.RegisterHit();
            int stop = _budget.Request(a.HitstopFrames, Time.time);
            _machine.Freeze(stop);
            // Combat depth cues, predicted from the replicated phase and attack clock (the host decides): a committed enemy clanks (it will not flinch,
            // it trades), an open one rings (x1.5).
            if (e.CommittedNow) CombatEvents.RaiseCue(CombatCue.Blocked, point);
            else if (e.OpenNow) CombatEvents.RaiseCue(CombatCue.HitCrit, point);
            e.ShowHit(transform.position, a.IsFinisher || a.Kind == AttackKind.Charge ? 1f : a.Kind == AttackKind.Shot ? 0.4f : 0.5f);   // white flash and squash at contact
            CombatEvents.RaiseLocalHit(new LocalHit(point, a, part >= 0, a.CameraKick));
            CombatEvents.RaiseKick(a.CameraKick);
            SubmitHitServerRpc(_swingSerial, _swingWeapon, (byte)_machine.CurrentIndex, _swingPrior, e.NetworkObjectId, (sbyte)part, transform.position, transform.eulerAngles.y);
        }

        [Rpc(SendTo.Server)]
        private void SubmitHitServerRpc(int swingId, byte weaponIndex, byte attackIndex, byte priorChainHits, ulong enemyNetId, sbyte part, Vector3 attackerPos, float attackerYaw)
        {
            CombatHost host = CombatHost.Current;
            if (host == null || NetworkManager == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetId, out NetworkObject obj)) return;
            Enemy e = obj.GetComponent<Enemy>();
            if (e != null) host.ServerPlayerHitsEnemy(this, swingId, attackIndex, e, part, attackerPos, attackerYaw, priorChainHits, weaponIndex);
        }

        // A spell swing began on the owner: the host spends its pips (keyed by the swing, so the hits of the same swing do not pay again).
        [Rpc(SendTo.Server)]
        private void CastServerRpc(int swingId, byte weaponIndex, byte attackIndex)
        {
            CombatHost host = CombatHost.Current;
            if (host != null) host.ServerCast(this, swingId, weaponIndex, attackIndex);
        }

        // =================================================================================================================================
        // Enemy hits on this hero: judged here
        // =================================================================================================================================

        private void JudgeIncoming(float dt)
        {
            Vector3 me = transform.position;
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || !e.IsSpawned || e.NetworkManager != NetworkManager || !e.Alive || !e.HitboxOn) continue;
                long key = ((long)e.NetworkObjectId << 32) | (uint)e.Serial;
                if (_judged.Contains(key)) continue;
                AttackData a = e.CurrentAttack;
                Vector3 c = e.AttackCentre;
                if (!HitGeometry.Hits(a, c.x, c.y, c.z, e.YawDeg, me.x, me.y, me.z, MovementMath.CapsuleRadius, MovementMath.CapsuleHeight)) continue;
                if (!MeleeSight.Clear(e.transform.position, me)) continue;   // through a doorframe or wall: no hit (not marked judged, so it can still land once the line clears)
                if (_judged.Count > 256) _judged.Clear();
                _judged.Add(key);
                ResolveEnemyHit(e, a);
            }
        }

        private void ResolveEnemyHit(Enemy e, AttackData a)
        {
            Vector3 me = transform.position;
            if (_loco.IsInvulnerable || _machine.Invulnerable)
            {
                CombatEvents.RaiseCue(CombatCue.Dodge, me);
                // A last-moment roll (the hit met it in its first 0.15 s, and it was not chained onto another roll) opens the attacker (combat depth, change 5).
                float openFor = Mathf.Max(0f, (e.AttackFrame - a.HitOn.Start) / AttackData.FrameRate);
                if (DodgeTiming.IsPerfect(_loco.IsDodging, _loco.DodgeElapsed, _rollGap, openFor))
                {
                    CombatEvents.RaiseCue(CombatCue.Parry, me);
                    PerfectDodgeServerRpc(e.NetworkObjectId, e.Serial);
                }
                return;   // a visible dodge is never hit
            }
            Vector3 toEnemy = e.transform.position - me;
            toEnemy.y = 0f;
            float bearing = toEnemy.sqrMagnitude > 1e-4f ? Mathf.DeltaAngle(transform.eulerAngles.y, MovementMath.YawDeg(toEnemy.x, toEnemy.z)) : 0f;
            GuardOutcome g = GuardMath.Resolve(_guarding, Time.time - _guardStart, bearing, a.GuardBreak, a.Guardable, _parryArmed);
            if (g == GuardOutcome.Parried)
            {
                CombatEvents.RaiseCue(CombatCue.Parry, me);
                CombatEvents.RaiseKick(0.6f);
                ParryServerRpc(e.NetworkObjectId, e.Serial);
                return;
            }
            if (HurtGraceActive) return;   // post-hit grace: a guarded or hurt outcome does nothing (and the host is never told); rolls and parries above still count
            bool guarded = g == GuardOutcome.Guarded;
            if (guarded)
            {
                CombatEvents.RaiseCue(CombatCue.Guarded, me);
                AddImpulse(-toEnemy.normalized * 3f);
            }
            else
            {
                CombatEvents.RaiseCue(CombatCue.PlayerHurt, me);
                CombatEvents.RaiseKick(0.5f);
                CombatEvents.RaiseHurtFrom(toEnemy);   // the red screen-edge flash on the side it came from
                if (!_machine.SuperArmor)
                {
                    if (_machine.Active) { PublishSwing(NoAttack); _machine.Interrupt(); }
                    if (HurtGrace.IsHeavyHit(a.Knockback, a.GuardBreak)) _hurtLock = _knockdownLeft = HurtGrace.KnockdownSeconds;   // killed: the host decides; a downed hero is not IsUp anyway
                    else _hurtLock = HurtStagger;
                }
                _grace.Start(HurtGrace.Classify(a.Knockback, false, a.GuardBreak));
                if (a.Knockback > 0f && toEnemy.sqrMagnitude > 1e-4f) AddImpulse(-toEnemy.normalized * Mathf.Min(8f, a.Knockback * 2.4f));
            }
            EnemyHitMeServerRpc(e.NetworkObjectId, e.Serial, guarded);
        }

        [Rpc(SendTo.Server)]
        private void EnemyHitMeServerRpc(ulong enemyNetId, int serial, bool guarded)
        {
            CombatHost host = CombatHost.Current;
            if (host == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetId, out NetworkObject obj)) return;
            Enemy e = obj.GetComponent<Enemy>();
            if (e != null) host.ServerEnemyHitsPlayer(this, e, serial, guarded);
        }

        [Rpc(SendTo.Server)]
        private void PerfectDodgeServerRpc(ulong enemyNetId, int serial)
        {
            CombatHost host = CombatHost.Current;
            if (host == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetId, out NetworkObject obj)) return;
            Enemy e = obj.GetComponent<Enemy>();
            if (e != null) host.ServerPerfectDodge(this, e, serial);
        }

        [Rpc(SendTo.Server)]
        private void ParryServerRpc(ulong enemyNetId, int serial)
        {
            CombatHost host = CombatHost.Current;
            if (host == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetId, out NetworkObject obj)) return;
            Enemy e = obj.GetComponent<Enemy>();
            if (e != null) host.ServerParry(this, e, serial);
        }

        // =================================================================================================================================
        // Revive
        // =================================================================================================================================

        private void ReportReviveHold(PlayerIntent intent, float dt)
        {
            _reviveReport -= dt;
            if (!intent.InteractHeld || _reviveReport > 0f) return;
            IReadOnlyList<HeroVitals> all = HeroVitals.All;
            for (int i = 0; i < all.Count; i++)
            {
                HeroVitals v = all[i];
                if (v == null || !v.IsSpawned || v == _vitals || v.NetworkManager != NetworkManager || !v.IsDowned) continue;
                if ((v.transform.position - transform.position).sqrMagnitude > HeroVitals.ReviveReach * HeroVitals.ReviveReach) continue;
                _reviveReport = 0.2f;
                ReviveHoldServerRpc(v.NetworkObjectId);
                return;
            }
        }

        [Rpc(SendTo.Server)]
        private void ReviveHoldServerRpc(ulong targetNetId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out NetworkObject obj)) return;
            HeroVitals target = obj.GetComponent<HeroVitals>();
            if (target != null && _vitals != null) target.ServerReviveHold(_vitals);
        }

        // =================================================================================================================================
        // Growth mirror and results from the host
        // =================================================================================================================================

        [Rpc(SendTo.Server)]
        private void RequestSyncServerRpc()
        {
            CombatHost host = CombatHost.Current;
            if (host != null) host.ServerSyncGrowth(this, force: true);
        }

        // Host -> this hero's owner.
        public void ServerPushGrowth(string weaponKey, int level, float abs, float cost, float atk, float fire, float pest, int pips, int pipCap)
        {
            SyncGrowthOwnerRpc(weaponKey, level, abs, cost, atk, fire, pest, pips, pipCap);
        }

        [Rpc(SendTo.Owner)]
        private void SyncGrowthOwnerRpc(string weaponKey, int level, float abs, float cost, float atk, float fire, float pest, int pips, int pipCap)
        {
            _mirror.WeaponKey = weaponKey;
            _mirror.Level = level;
            _mirror.Abs = abs;
            _mirror.LevelCost = cost;
            _mirror.Atk = atk;
            _mirror.Fire = fire;
            _mirror.Pest = pest;
            _mirror.Pips = pips;
            _mirror.PipCap = pipCap;
            // The tree is pushed only when its shape changes, so keep its ABS and stats current from this mirror (same weapon state, same level).
            if (TreeMirror.Level == level) { TreeMirror.Abs = abs; TreeMirror.Stats = new WeaponStats(atk, fire, pest); }
        }

        // ---- Weapon tree (M4 UI). Host -> owner: the snapshot the tree shows. Owner -> host: a choice, applied by WeaponTreeHost on the host. ----

        public WeaponTreeSnapshot TreeMirror { get; private set; } = new WeaponTreeSnapshot();
        public int TreePushes { get; private set; }   // how many tree snapshots the owner has received (tests wait on it)

        // Owner only (the arena's weapon-tree window). The host decides; the snapshot comes back through SyncTreeOwnerRpc.
        public void OwnerTreeAction(TreeAction action, int arg = 0)
        {
            if (!IsOwner) return;
            TreeActionServerRpc((byte)action, (byte)arg);
        }

        [Rpc(SendTo.Server)]
        private void TreeActionServerRpc(byte action, byte arg)
        {
            WeaponTreeHost.Apply(CombatHost.Current, this, (TreeAction)action, arg);
        }

        public void ServerPushTree(WeaponTreeSnapshot s)
        {
            byte flags = (byte)((s.HasTarget ? 1 : 0) | (s.BuildUpOpen ? 2 : 0) | (s.CanReroute ? 4 : 0) | (s.AtWorkbench ? 8 : 0));
            SyncTreeOwnerRpc(s.DefinitionId, s.Level, s.Abs, s.Stats.Atk, s.Stats.Fire, s.Stats.Pest, s.Fitted ?? new int[0], flags,
                s.ProgressAtk, s.ProgressFire, s.ProgressPest, s.RestoredDefinitionId ?? "");
        }

        [Rpc(SendTo.Owner)]
        private void SyncTreeOwnerRpc(string definitionId, int level, float abs, float atk, float fire, float pest, int[] fitted, byte flags,
                                      float progAtk, float progFire, float progPest, string restoredId)
        {
            TreePushes++;
            TreeMirror = new WeaponTreeSnapshot
            {
                DefinitionId = definitionId, Level = level, Abs = abs, Stats = new WeaponStats(atk, fire, pest), Fitted = fitted ?? new int[0],
                HasTarget = (flags & 1) != 0, BuildUpOpen = (flags & 2) != 0, CanReroute = (flags & 4) != 0, AtWorkbench = (flags & 8) != 0,
                ProgressAtk = progAtk, ProgressFire = progFire, ProgressPest = progPest,
                RestoredDefinitionId = restoredId.Length == 0 ? null : restoredId,
            };
        }

        // Host -> everyone: a hit on an enemy resolved (the number, the sounds, the part break).
        public const byte FlagCrit = 1, FlagBlocked = 2, FlagWeakPart = 4, FlagKilled = 8, FlagFinisher = 16, FlagPartBroken = 32, FlagGuardBreak = 64, FlagElement = 128;

        public void ServerBroadcastHit(ulong enemyNetId, int damage, byte flags)
        {
            HitResultClientRpc(enemyNetId, damage, flags);
        }

        [Rpc(SendTo.Everyone)]
        private void HitResultClientRpc(ulong enemyNetId, int damage, byte flags)
        {
            if (NetworkManager == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(enemyNetId, out NetworkObject obj)) return;
            Vector3 at = obj.transform.position + Vector3.up * 1.6f;
            CombatEvents.RaiseNumber(new DamageNumber(at, damage, (flags & FlagCrit) != 0, (flags & FlagBlocked) != 0, false, false, (flags & FlagWeakPart) != 0, (flags & FlagFinisher) != 0));
            if (!IsOwner)   // the owner already played the hit locally, at the moment of contact
                CombatEvents.RaiseCue((flags & FlagFinisher) != 0 ? CombatCue.HitFinisher : (flags & FlagBlocked) != 0 ? CombatCue.Blocked : CombatCue.HitNormal, at);
            else if ((flags & FlagBlocked) != 0) CombatEvents.RaiseCue(CombatCue.Blocked, at);
            if ((flags & FlagCrit) != 0) CombatEvents.RaiseCue(CombatCue.HitCrit, at);
        }

        // Host -> everyone: this hero was hurt (HP number over the hero, a sound).
        public void ServerBroadcastHurt(int damage, bool guarded, bool heavy = false)
        {
            HurtClientRpc(damage, guarded, heavy);
        }

        [Rpc(SendTo.Everyone)]
        private void HurtClientRpc(int damage, bool guarded, bool heavy)
        {
            // Same reaction on every peer: `heavy` (knockback >= 3 or a guard break) comes from the host, which has the attack. The owner's own window is already
            // running from its judging; the other peers start a cosmetic one for the pulse.
            if (!guarded)
            {
                HurtSerial++;
                HurtWasHeavy = heavy;
                if (!IsOwner) _grace.Start(heavy ? HurtKind.Heavy : HurtKind.Light);
            }
            Vector3 at = transform.position + Vector3.up * 2.1f;
            CombatEvents.RaiseNumber(new DamageNumber(at, damage, false, guarded, true, false, false));
            CombatEvents.RaiseHeroHurt(this, guarded);
            if (!IsOwner) CombatEvents.RaiseCue(guarded ? CombatCue.Guarded : CombatCue.PlayerHurt, transform.position);
        }

        public void ServerBroadcastHeal(int amount) { HealClientRpc(amount); }

        [Rpc(SendTo.Everyone)]
        private void HealClientRpc(int amount)
        {
            CombatEvents.RaiseNumber(new DamageNumber(transform.position + Vector3.up * 2.1f, amount, false, false, true, true, false));
            CombatEvents.RaiseCue(CombatCue.Heal, transform.position);
        }

        // Host -> this hero's owner: ABS earned and why (the debug HUD's "ABS gains", the tech pop).
        public void ServerReportAbs(string weaponKey, float abs, bool techPop, int levelsGained, string why)
        {
            AbsOwnerRpc(weaponKey, abs, techPop, levelsGained, why);
        }

        [Rpc(SendTo.Owner)]
        private void AbsOwnerRpc(string weaponKey, float abs, bool techPop, int levelsGained, string why)
        {
            CombatEvents.RaiseAbs(new AbsReport(weaponKey, abs, techPop, levelsGained, why));
            if (techPop) CombatEvents.RaiseCue(CombatCue.Pop, transform.position);
        }

        // Host -> everyone: the arena's wave, for the HUD (sent twice a second through whichever hero the host picks).
        public void ServerBroadcastWave(int wave, byte state, int left, int tier) { WaveClientRpc(wave, state, left, tier); }

        [Rpc(SendTo.Everyone)]
        private void WaveClientRpc(int wave, byte state, int left, int tier)
        {
            ArenaStatus.Wave = wave;
            ArenaStatus.State = (WaveState)state;
            ArenaStatus.EnemiesLeft = left;
            ArenaStatus.Tier = tier;
        }

        [Rpc(SendTo.Server)]
        private void SkipWaveServerRpc()
        {
            CombatHost host = CombatHost.Current;
            if (host != null) host.ServerSkipWave();
        }

        // Host -> everyone: the parry flash.
        public void ServerBroadcastParry() { ParryClientRpc(); }

        [Rpc(SendTo.Everyone)]
        private void ParryClientRpc()
        {
            if (!IsOwner) CombatEvents.RaiseCue(CombatCue.Parry, transform.position);
        }
    }
}
