// Core/Combat/Ai/ThreatTable.cs: who one enemy goes for, stage 3 of docs/research/enemy-ai.md (sections 6.2, 6.4, 10.2). Pure C#, no UnityEngine.
// Authority: host. One ThreatTable per Enemy (inside its AiBrain); TargetLedger is the host-wide count of who targets whom (CombatHost owns one, shared by
// every room's EnemyCrowd, because targeting is world-wide). Nothing here is replicated.
//
// Scope choice (documented per the stage 3 brief): candidates are ALL heroes of the enemy's world, but filtered by the enemy's own ranges: a hero is
// noticed within AggroRange and kept until LeashRange (hysteresis in distance too). Room membership (EnemyCrowd) stays bookkeeping; the ranges are what
// make "a hero in another room" irrelevant, and load counting is world-wide so two rooms working one hero still count against each other.
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core.Combat
{
    // All the placeholder numbers of target selection in one place (doc 6.2 start weights). Tune here, not at the call sites.
    public sealed class ThreatTuning
    {
        public const float DefaultAggroRange = 12f, DefaultLeashRange = 20f, DefaultRetargetInterval = 0.75f;

        public float WDist = 1.0f;       // nearer is higher
        public float WHit = 0.8f;        // damage this hero dealt to this enemy lately (decayed damage / HitReference, capped at 1)
        public float WStick = 0.6f;      // bonus for the current target: a rival must beat it by this much to take over
        public float WLoad = 0.7f;       // heroes with fewer enemies already on them score higher (the fairness term)
        public float WRole = 0.3f;       // ranged enemies prefer the farthest hero
        public float LoadCap = 3f;       // load term reaches 0 at this many other enemies on the hero
        public float HitHalfLifeSeconds = 6f;   // recent damage halves every this many seconds (exponential, so a lone attacker's pull fades too)
        public float HitReference = 20f;        // decayed damage that gives the full hit pull; pull = min(1, decayed / HitReference)

        public static readonly ThreatTuning Default = new ThreatTuning();
    }

    // Host-wide: which hero each enemy currently targets. Keys are opaque ints (hash codes), like the token keys.
    public sealed class TargetLedger
    {
        private readonly Dictionary<int, int> _target = new Dictionary<int, int>();

        public void Set(int enemy, int hero) { _target[enemy] = hero; }
        public void Remove(int enemy) { _target.Remove(enemy); }
        public void Clear() { _target.Clear(); }
        public int Count => _target.Count;

        // How many enemies other than `exceptEnemy` target `hero`.
        public int CountOn(int hero, int exceptEnemy)
        {
            int n = 0;
            foreach (KeyValuePair<int, int> kv in _target) if (kv.Value == hero && kv.Key != exceptEnemy) n++;
            return n;
        }
    }

    public sealed class ThreatTable
    {
        private readonly ThreatTuning _t;
        private readonly float _aggro, _leash, _retarget;
        private readonly bool _unlimited;   // hunters: no range FILTER (they always know where the heroes are); the distance scale stays at the normal aggro range
        private readonly List<int> _hitIds = new List<int>();
        private readonly List<float> _hitAmounts = new List<float>();
        private int _current;
        private bool _hasCurrent;
        private float _sinceSwitch;

        public ThreatTable(float aggroRange = ThreatTuning.DefaultAggroRange, float leashRange = ThreatTuning.DefaultLeashRange,
            float retargetInterval = ThreatTuning.DefaultRetargetInterval, ThreatTuning tuning = null, bool unlimitedRange = false)
        {
            _unlimited = unlimitedRange;
            _t = tuning ?? ThreatTuning.Default;
            _aggro = Math.Max(0.01f, aggroRange);
            _leash = Math.Max(_aggro, leashRange);
            _retarget = Math.Max(0f, retargetInterval);
        }

        public bool HasCurrent => _hasCurrent;
        public int CurrentId => _current;

        // The current target is forgotten (the enemy was staggered or its AI was switched off): it picks afresh next time.
        public void ClearCurrent() { _hasCurrent = false; }

        // A hero hurt this enemy (host: after a damaging hit). Recent damage pulls the enemy toward that hero.
        public void RecordHit(int heroId, float damage)
        {
            if (damage <= 0f) return;
            int i = _hitIds.IndexOf(heroId);
            if (i < 0) { _hitIds.Add(heroId); _hitAmounts.Add(damage); }
            else _hitAmounts[i] += damage;
        }

        public void Reset() { _hasCurrent = false; _sinceSwitch = 0f; _hitIds.Clear(); _hitAmounts.Clear(); }

        // Index into `heroes` of the hero to target this tick, or -1. `locked`: the enemy is in Tell or Strike, so a living current target is kept whatever
        // the scores say (doc 6.2 hard rule). `rangedBias`: the role term prefers the farthest hero. Downed heroes (Up false) are never chosen.
        public int Pick(IReadOnlyList<AiHero> heroes, float x, float y, float z, float dt, bool locked, bool rangedBias = false)
        {
            _sinceSwitch += dt;
            DecayHits(dt);

            int cur = -1;
            if (_hasCurrent)
                for (int i = 0; i < heroes.Count; i++) if (heroes[i].Up && heroes[i].Id == _current) { cur = i; break; }
            if (locked && cur >= 0) return cur;

            int best = -1;
            float bestScore = float.NegativeInfinity, curScore = float.NegativeInfinity;
            for (int i = 0; i < heroes.Count; i++)
            {
                AiHero h = heroes[i];
                if (!h.Up) continue;
                float dx = h.X - x, dy = h.Y - y, dz = h.Z - z;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                bool isCur = i == cur;
                // Room scope (doc 6.4): a hero inside this enemy's own room is always noticed and kept, whatever the distance (the room is the aggro
                // range). Everyone else: noticed within AggroRange, kept until LeashRange. Hunters skip the filter, never the distance scale below.
                if (!h.InRoom && !_unlimited && dist > (isCur ? _leash : _aggro)) continue;

                float near = Clamp01(1f - dist / _aggro);
                float share = 0f;
                int hi = _hitIds.IndexOf(h.Id);
                if (hi >= 0) share = Clamp01(_hitAmounts[hi] / Math.Max(0.01f, _t.HitReference));
                float load = Clamp01(1f - h.Load / Math.Max(1f, _t.LoadCap));
                float role = rangedBias ? Clamp01(dist / _aggro) : 0f;
                float score = _t.WDist * near + _t.WHit * share + (isCur ? _t.WStick : 0f) + _t.WLoad * load + _t.WRole * role;
                if (isCur) curScore = score;
                if (score > bestScore) { bestScore = score; best = i; }   // first wins a tie
            }

            if (best < 0) { _hasCurrent = false; return -1; }                       // nobody in range (or all down)
            if (cur >= 0 && curScore > float.NegativeInfinity)
            {
                // Keep unless a rival beats the sticky score AND the retarget interval has passed.
                if (best == cur || _sinceSwitch < _retarget || bestScore <= curScore) return cur;
            }
            _current = heroes[best].Id;
            _hasCurrent = true;
            _sinceSwitch = 0f;
            return best;
        }

        private void DecayHits(float dt)
        {
            if (_hitIds.Count == 0) return;
            float f = (float)Math.Pow(0.5, dt / Math.Max(0.01f, _t.HitHalfLifeSeconds));
            for (int i = _hitAmounts.Count - 1; i >= 0; i--)
            {
                _hitAmounts[i] *= f;
                if (_hitAmounts[i] < 0.01f) { _hitAmounts.RemoveAt(i); _hitIds.RemoveAt(i); }
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
