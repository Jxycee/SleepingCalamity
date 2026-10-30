// Authority: host. Not a NetworkBehaviour (no in-scene NetworkObject): the combat arena holds this plain component, and the host's ArenaPlayerSpawner calls
// ServerBegin once the arena's load event has completed, the same hook that spawns the players and dummies. It owns the host-side rules around hits:
//  - validating a player's hit intent (the attacker is up, the target is alive, the attack exists on that weapon, one hit per target per swing, the spell
//    costs a pip once per swing), then applying damage, reactions, revenge and rewards through the Core models and sending the results back;
//  - validating an enemy-hits-player report from the victim's client (the enemy is alive and that attack serial is the one it is performing, one hit per
//    serial), then applying damage to the host-held HP;
//  - each player's growth (their heirlooms, style ledger, pips), the healing drops, and the wave director (waves 1-4 tier 1, then tier 2).
// Test hooks: AiEnabled, WavesEnabled, CritChance, SpawnEnemy.
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public enum WaveState : byte { Idle = 0, Countdown = 1, Active = 2, Cleared = 3, Wiped = 4 }

    // What every peer knows about the arena's progress (set by the host's WaveClientRpc on a hero; read by the HUD).
    public static class ArenaStatus
    {
        public static int Wave, EnemiesLeft, Tier = 1;
        public static WaveState State;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { Wave = 0; EnemiesLeft = 0; Tier = 1; State = WaveState.Idle; }
    }

    public sealed class CombatHost : MonoBehaviour
    {
        public static CombatHost Current { get; private set; }

        [SerializeField] private NetworkObject slimePrefab, ratPrefab, broomPrefab, wardenPrefab, healPrefab;
        [SerializeField] private float arenaRadius = 14f;

        public bool AiEnabled = true;
        public bool WavesEnabled = true;
        public float CritChance = DamageFormula.CritChance;
        public float ClearHeal = 20f;

        private NetworkManager _nm;
        private Pcg32 _rng = Rng.Stream(0x5CA11UL, "combat.crit", 0UL);
        private readonly Dictionary<string, PlayerGrowth> _growth = new Dictionary<string, PlayerGrowth>();
        private readonly Dictionary<ulong, HostPlayerState> _states = new Dictionary<ulong, HostPlayerState>();
        private readonly HashSet<long> _enemyHitDedupe = new HashSet<long>();
        private readonly List<Enemy> _alive = new List<Enemy>();

        private WaveState _wave = WaveState.Idle;
        private int _waveNumber;
        private float _waveTimer, _broadcastTimer, _wipeTimer;
        private bool _begun;

        public bool Begun => _begun;
        public int WaveNumber => _waveNumber;
        public WaveState Wave => _wave;
        public IReadOnlyList<Enemy> AliveEnemies => _alive;

        private sealed class HostPlayerState
        {
            public int LastSwing = -1, HitsInSwing, PriorAtSwing, ChainHits;
            public bool Rejected;
            public double LastChainHit = -10.0;
            public readonly HashSet<ulong> Hit = new HashSet<ulong>();
        }

        public void Configure(NetworkObject slime, NetworkObject rat, NetworkObject broom, NetworkObject warden, NetworkObject heal)
        {
            slimePrefab = slime; ratPrefab = rat; broomPrefab = broom; wardenPrefab = warden; healPrefab = heal;
        }

        private void OnEnable() { Current = this; }
        private void OnDisable() { if (Current == this) Current = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; }

        // Idempotent; a client-side call does nothing. Returns true when it started now.
        public bool ServerBegin(NetworkManager nm)
        {
            if (nm == null || !nm.IsServer || _begun) return false;
            _nm = nm;
            _begun = true;
            _wave = WavesEnabled ? WaveState.Countdown : WaveState.Idle;
            _waveTimer = 4f;
            _waveNumber = 1;
            return true;
        }

        // ---- growth ----

        public PlayerGrowth Growth(ulong clientId)
        {
            string id = clientId.ToString();
            if (!_growth.TryGetValue(id, out PlayerGrowth g))
            {
                g = new PlayerGrowth(id);
                _growth[id] = g;
            }
            return g;
        }

        private HostPlayerState StateOf(ulong clientId)
        {
            if (!_states.TryGetValue(clientId, out HostPlayerState s)) { s = new HostPlayerState(); _states[clientId] = s; }
            return s;
        }

        public void ServerSyncGrowth(HeroCombat hero)
        {
            if (hero == null || !hero.IsSpawned) return;
            PlayerGrowth g = Growth(hero.OwnerClientId);
            WeaponState w = g.Weapon(hero.WeaponKey);
            hero.ServerPushGrowth(hero.WeaponKey, w.Level, w.Abs, w.LevelCostNow, w.Stats.Atk, w.Stats.Fire, w.Stats.Pest, g.Pips.Current, g.Pips.Cap);
        }

        public int PlayerCount()
        {
            int n = 0;
            foreach (HeroVitals v in HeroVitals.All) if (v != null && v.IsSpawned && v.NetworkManager == _nm) n++;
            return Mathf.Max(1, n);
        }

        // =================================================================================================================================
        // Player hits on enemies
        // =================================================================================================================================

        public void ServerPlayerHitsEnemy(HeroCombat attacker, int swingId, int attackIndex, Enemy e, int part, Vector3 attackerPos, float attackerYaw)
        {
            if (_nm == null || !_nm.IsServer || attacker == null || e == null) return;
            HeroVitals vitals = attacker.GetComponent<HeroVitals>();
            if (vitals == null || !vitals.IsUp || !e.Alive || e.Model == null || e.Phase == EnemyPhase.Dead) return;
            string weaponKey = attacker.WeaponKey;
            AttackData attack = AttackCatalog.ForFamily(WeaponStyle.FamilyOf(weaponKey)).Get(attackIndex);
            if (attack == null) return;

            ulong cid = attacker.OwnerClientId;
            HostPlayerState ps = StateOf(cid);
            PlayerGrowth g = Growth(cid);
            double now = Time.timeAsDouble;
            if (swingId != ps.LastSwing)
            {
                ps.LastSwing = swingId;
                ps.Hit.Clear();
                ps.HitsInSwing = 0;
                ps.Rejected = false;
                if (now - ps.LastChainHit > AttackMachine.ChainGraceSeconds) ps.ChainHits = 0;
                ps.PriorAtSwing = ps.ChainHits;
                if (attack.SpellPips > 0)
                {
                    ps.Rejected = !g.Pips.TrySpend(attack.SpellPips);   // the rule is checked here, whatever the client's mirror said
                    ServerSyncGrowth(attacker);
                }
            }
            if (ps.Rejected || !ps.Hit.Add(e.NetworkObjectId)) return;

            if (part >= e.Def.Parts.Length || part < -1 || (part >= 0 && !e.PartIntact(part))) part = -1;
            WeaponState w = g.Weapon(weaponKey);
            Vector3 toAttacker = attackerPos - e.transform.position;
            toAttacker.y = 0f;
            float bearing = toAttacker.sqrMagnitude > 1e-4f ? Mathf.DeltaAngle(e.transform.eulerAngles.y, MovementMath.YawDeg(toAttacker.x, toAttacker.z)) : 0f;

            var input = new EnemyHitInput
            {
                Attack = attack,
                Atk = w.Stats.Atk, Fire = w.Stats.Fire, Pest = w.Stats.Pest,
                PriorChainHits = ps.PriorAtSwing,
                BearingToAttackerDeg = bearing,
                PartIndex = part,
                CritRoll = _rng.NextFloat(),
                CritChance = CritChance,
            };
            EnemyHitResult r = e.ServerApplyHit(input, attackerPos, now);
            if (r.Damage <= 0) return;

            ps.ChainHits++;
            ps.HitsInSwing++;
            ps.LastChainHit = now;
            e.HitLog.Add(new HitRecord(cid.ToString(), weaponKey, now));

            AbsGain gain = CombatRewards.OnHit(g, weaponKey, e.NetworkObjectId.ToString(), attack, r, ps.HitsInSwing, attack.Kind == AttackKind.Shot && part >= 0);
            if (gain.Abs > 0f) attacker.ServerReportAbs(weaponKey, gain.Abs, gain.TechPop, gain.LevelsGained, Why(attack, r));
            if (r.PartBroken >= 0) SpawnHeal(e.PartWorldPosition(r.PartBroken), Mathf.RoundToInt(e.Model.Parts[r.PartBroken].HealPerDrop));

            byte flags = 0;
            if (r.Crit) flags |= HeroCombat.FlagCrit;
            if (r.Blocked) flags |= HeroCombat.FlagBlocked;
            if (r.WeakPart) flags |= HeroCombat.FlagWeakPart;
            if (r.Killed) flags |= HeroCombat.FlagKilled;
            if (attack.IsFinisher) flags |= HeroCombat.FlagFinisher;
            if (r.PartBroken >= 0) flags |= HeroCombat.FlagPartBroken;
            if (r.GuardBreakHit) flags |= HeroCombat.FlagGuardBreak;
            if (r.ElementMatch) flags |= HeroCombat.FlagElement;
            attacker.ServerBroadcastHit(e.NetworkObjectId, r.Damage, flags);

            if (r.Killed) OnEnemyKilled(e, cid, attack.IsFinisher, now);
            ServerSyncGrowth(attacker);
        }

        private static string Why(AttackData a, in EnemyHitResult r)
        {
            if (r.GuardBreakHit) return "guard break";
            if (r.WeakPart) return "weak part";
            if (r.ElementMatch) return "element match";
            return a.IsFinisher ? "finisher" : a.Kind == AttackKind.Shot ? "shot" : "style";
        }

        private void OnEnemyKilled(Enemy e, ulong killerClient, bool finisher, double now)
        {
            _alive.Remove(e);
            List<AbsGain> gains = CombatRewards.OnKill(_growth, e.HitLog, now, e.Def.Species, killerClient.ToString(), finisher);
            foreach (AbsGain gain in gains)
            {
                if (!ulong.TryParse(gain.PlayerId, out ulong cid)) continue;
                HeroCombat hero = FindHero(cid);
                if (hero == null) continue;
                hero.ServerReportAbs(gain.WeaponKey, gain.Abs, gain.TechPop, gain.LevelsGained, "kill: " + e.Def.Name);
                ServerSyncGrowth(hero);
            }
            string key = e.NetworkObjectId.ToString();
            foreach (PlayerGrowth g in _growth.Values) g.ForgetEnemy(key);
        }

        private HeroCombat FindHero(ulong clientId)
        {
            if (_nm == null) return null;
            if (_nm.ConnectedClients.TryGetValue(clientId, out NetworkClient c) && c.PlayerObject != null) return c.PlayerObject.GetComponent<HeroCombat>();
            return null;
        }

        // =================================================================================================================================
        // Enemy hits on players (judged by the victim's client)
        // =================================================================================================================================

        public void ServerEnemyHitsPlayer(HeroCombat victim, Enemy e, int serial, bool guarded)
        {
            if (_nm == null || !_nm.IsServer || victim == null || e == null || !e.Alive || e.Model == null) return;
            if (!e.ServerAcceptsSerial(serial)) return;
            AttackData a = e.ServerAttackOfSerial(serial);
            if (a == null) return;
            HeroVitals vitals = victim.GetComponent<HeroVitals>();
            if (vitals == null || !vitals.IsUp) return;
            long key = ((long)victim.OwnerClientId << 48) ^ ((long)e.NetworkObjectId << 24) ^ (uint)serial;
            if (_enemyHitDedupe.Count > 512) _enemyHitDedupe.Clear();
            if (!_enemyHitDedupe.Add(key)) return;

            int dmg = e.Model.AttackDamage(a, vitals.Model.Def);
            if (guarded) dmg = Mathf.Max(1, Mathf.CeilToInt(dmg * GuardMath.GuardedDamage - 1e-4f));
            int lost = vitals.ServerDamage(dmg);
            victim.ServerBroadcastHurt(lost, guarded);
        }

        public void ServerParry(HeroCombat victim, Enemy e, int serial)
        {
            if (_nm == null || !_nm.IsServer || victim == null || e == null || !e.Alive) return;
            if (!e.ServerAcceptsSerial(serial)) return;
            long key = ((long)victim.OwnerClientId << 48) ^ ((long)e.NetworkObjectId << 24) ^ (uint)serial;
            if (!_enemyHitDedupe.Add(key)) return;
            e.ServerParried();
            victim.ServerBroadcastParry();
            string weaponKey = victim.WeaponKey;
            if (WeaponStyle.FamilyOf(weaponKey) != WeaponFamily.Dagger) return;   // only the dagger and shield guards (P46)
            AbsGain gain = CombatRewards.OnParry(Growth(victim.OwnerClientId), weaponKey, e.NetworkObjectId.ToString());
            victim.ServerReportAbs(weaponKey, gain.Abs, gain.TechPop, gain.LevelsGained, "parry");
            ServerSyncGrowth(victim);
        }

        // =================================================================================================================================
        // Spawning
        // =================================================================================================================================

        public Enemy SpawnEnemy(EnemyKind kind, int tier, Vector3 position, bool ai = true)
        {
            if (_nm == null || !_nm.IsServer) return null;
            NetworkObject prefab = kind == EnemyKind.Slime ? slimePrefab : kind == EnemyKind.RatKing ? ratPrefab : kind == EnemyKind.Broom ? broomPrefab : wardenPrefab;
            if (prefab == null) return null;
            Vector3 toCentre = -position;
            toCentre.y = 0f;
            Quaternion rot = toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre.normalized) : Quaternion.identity;
            NetworkObject instance = Instantiate(prefab, position, rot);
            Enemy e = instance.GetComponent<Enemy>();
            e.ServerSeed(kind, tier, PlayerCount());
            e.AiEnabled = AiEnabled && ai;
            instance.Spawn(destroyWithScene: true);
            _alive.Add(e);
            e.ServerDied += OnEnemyDied;
            return e;
        }

        private void OnEnemyDied(Enemy e) { _alive.Remove(e); }

        public void SpawnHeal(Vector3 at, int amount)
        {
            if (_nm == null || !_nm.IsServer || healPrefab == null) return;
            Vector3 p = new Vector3(at.x, 0.8f, at.z);
            NetworkObject drop = Instantiate(healPrefab, p, Quaternion.identity);
            drop.GetComponent<HealingDrop>().ServerInit(amount);
            drop.Spawn(destroyWithScene: true);
        }

        public void ServerClearEnemies()
        {
            foreach (Enemy e in new List<Enemy>(_alive))
                if (e != null && e.NetworkObject != null && e.NetworkObject.IsSpawned) e.NetworkObject.Despawn(true);
            _alive.Clear();
        }

        // F6 in the arena: kill off the wave and move on.
        public void ServerSkipWave()
        {
            if (_wave == WaveState.Active) ServerClearEnemies();
        }

        // =================================================================================================================================
        // Wave director
        // =================================================================================================================================

        private void Update()
        {
            if (!_begun || _nm == null || !_nm.IsServer || !_nm.IsListening) return;
            float dt = Time.deltaTime;
            _alive.RemoveAll(x => x == null || !x.IsSpawned);

            // A wipe: every hero down ends the dive; the greybox arena resets the party and restarts the wave.
            var party = new List<PlayerVitals>();
            foreach (HeroVitals v in HeroVitals.All) if (v != null && v.IsSpawned && v.NetworkManager == _nm) party.Add(v.Model);
            if (_wave != WaveState.Wiped && PlayerVitals.AllDown(party))
            {
                _wave = WaveState.Wiped;
                _wipeTimer = 4f;
                ServerClearEnemies();
            }
            if (_wave == WaveState.Wiped)
            {
                _wipeTimer -= dt;
                if (_wipeTimer <= 0f)
                {
                    foreach (HeroVitals v in HeroVitals.All) if (v != null && v.IsSpawned && v.NetworkManager == _nm) v.ServerFullReset();
                    _wave = WavesEnabled ? WaveState.Countdown : WaveState.Idle;
                    _waveTimer = 3f;
                }
            }
            else if (WavesEnabled)
            {
                switch (_wave)
                {
                    case WaveState.Countdown:
                        _waveTimer -= dt;
                        if (_waveTimer <= 0f) SpawnWave();
                        break;
                    case WaveState.Active:
                        if (_alive.Count == 0)
                        {
                            _wave = WaveState.Cleared;
                            _waveTimer = 4f;
                            foreach (HeroVitals v in HeroVitals.All)
                            {
                                if (v == null || !v.IsSpawned || v.NetworkManager != _nm || !v.IsUp) continue;
                                int healed = v.ServerHeal(Mathf.RoundToInt(ClearHeal));
                                if (healed > 0) v.GetComponent<HeroCombat>()?.ServerBroadcastHeal(healed);
                            }
                        }
                        break;
                    case WaveState.Cleared:
                        _waveTimer -= dt;
                        if (_waveTimer <= 0f) { _waveNumber++; _wave = WaveState.Countdown; _waveTimer = 3f; }
                        break;
                }
            }

            _broadcastTimer -= dt;
            if (_broadcastTimer <= 0f)
            {
                _broadcastTimer = 0.5f;
                HeroCombat carrier = null;
                foreach (HeroVitals v in HeroVitals.All)
                    if (v != null && v.IsSpawned && v.NetworkManager == _nm) { carrier = v.GetComponent<HeroCombat>(); break; }
                if (carrier != null) carrier.ServerBroadcastWave(_waveNumber, (byte)_wave, _alive.Count, ArenaWaves.TierOf(_waveNumber));
            }
        }

        private void SpawnWave()
        {
            int players = PlayerCount();
            EnemyKind[] roster = ArenaWaves.Roster(_waveNumber, players);
            int tier = ArenaWaves.TierOf(_waveNumber);
            for (int i = 0; i < roster.Length; i++)
            {
                float angle = (i / (float)roster.Length) * 360f + _waveNumber * 37f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * (arenaRadius - (roster[i] == EnemyKind.DoughWarden ? 4f : 0f));
                SpawnEnemy(roster[i], tier, p);
            }
            _wave = WaveState.Active;
        }
    }
}
