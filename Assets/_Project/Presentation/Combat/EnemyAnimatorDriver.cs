// Authority: none (local, cosmetic). Presentation only, driven from the replicated Enemy state on every peer.
// Plays a rigged enemy's Animator from the same state EnemyVisual already reads: Enemy.Phase, CurrentAttack + AttackFrame (1 game frame == 1 clip frame
// at 60 fps, so an attack state is placed at normalizedTime = AttackFrame / TotalFrames every Update, exact for a late joiner), Braced and HitReact.
// EnemyAnimMap is the pure name/time mapping (no engine calls) so it can be tested without a scene.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class EnemyAnimMap
    {
        public const string Idle = "Idle", Move = "Move", Hit = "Hit", Death = "Death", Brace = "Brace", AttackPrefix = "Attack_";
        public const float ClipFps = AttackData.FrameRate;   // the rigged clips are authored at the combat frame rate

        // The state (and clip) that plays an attack: "Attack_" + the attack id (slime_hop -> Attack_slime_hop).
        public static string AttackState(string attackId) => AttackPrefix + attackId;
        public static string AttackState(AttackData attack) => AttackState(attack.Id);

        // Where in the clip an attack is: the frame of the attack clock over the attack's length, clamped to the clip.
        public static float AttackNormalizedTime(float attackFrame, int totalFrames)
        {
            if (totalFrames <= 0) return 0f;
            float n = attackFrame / totalFrames;
            return n < 0f ? 0f : n > 1f ? 1f : n;
        }

        // A clip's length in seconds for the frame count the combat catalog gives it.
        public static float ClipSeconds(int totalFrames) => totalFrames / ClipFps;

        // The non-attack state for a phase. Attack is handled by the caller (AttackState); Braced beats Idle and Move; a flinch and a stagger are Hit.
        public static string Locomotion(EnemyPhase phase, bool braced)
        {
            if (phase == EnemyPhase.Dead) return Death;
            if (phase == EnemyPhase.Stagger) return Hit;
            if (braced) return Brace;
            return phase == EnemyPhase.Chase ? Move : Idle;
        }
    }

    public sealed class EnemyAnimatorDriver
    {
        public const float CrossFadeSeconds = 0.1f, HitSeconds = 20f / EnemyAnimMap.ClipFps;

        private readonly Animator _animator;
        private readonly Dictionary<string, int> _hashes = new Dictionary<string, int>();
        private string _current;
        private float _hitUntil = -1f, _prevReact;
        private bool _started;

        public EnemyAnimatorDriver(Animator animator) { _animator = animator; }

        public Animator Animator => _animator;

        private bool Has(string state, out int hash)
        {
            if (!_hashes.TryGetValue(state, out hash))
            {
                hash = Animator.StringToHash(state);
                _hashes[state] = hash;
            }
            return _animator.HasState(0, hash);
        }

        private void Snap(string state, float normalizedTime)
        {
            if (!Has(state, out int hash)) return;
            _animator.Play(hash, 0, normalizedTime);
            _current = state;
        }

        // Called once a frame from EnemyVisual.Update (after the Enemy's own state is current).
        public void Tick(Enemy enemy, float now)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            EnemyPhase phase = enemy.Phase;
            AttackData attack = enemy.CurrentAttack;
            bool attacking = phase == EnemyPhase.Attack && attack != null && enemy.Attacking;
            if (attacking)
            {
                // Placed by the attack clock every frame (the state runs at speed 0), never advanced by the Animator itself.
                string state = EnemyAnimMap.AttackState(attack);
                if (Has(state, out int hash))
                {
                    _animator.Play(hash, 0, EnemyAnimMap.AttackNormalizedTime(enemy.AttackFrame, attack.TotalFrames));
                    _current = state;
                    _started = true;   // _prevReact stays as it was, so a flinch landed during the attack still plays Hit once it ends
                    return;
                }
            }

            string desired = EnemyAnimMap.Locomotion(phase, enemy.Braced);
            float react = enemy.HitReact;
            if (react > 0f && _prevReact <= 0f && phase != EnemyPhase.Dead && phase != EnemyPhase.Stagger) _hitUntil = now + HitSeconds;   // a landed flinch
            _prevReact = react;
            if (now < _hitUntil && phase != EnemyPhase.Dead && phase != EnemyPhase.Stagger && desired != EnemyAnimMap.Hit) desired = EnemyAnimMap.Hit;
            else if (phase == EnemyPhase.Dead) _hitUntil = -1f;

            if (!_started)
            {
                _started = true;
                // A peer that joins a corpse sees it already down (the clip holds its last frame).
                Snap(desired, desired == EnemyAnimMap.Death ? 1f : 0f);
                return;
            }
            if (desired == _current) return;
            if (!Has(desired, out int h)) return;
            // Hit restarts from its first frame (a flinch is quick); the rest ease in.
            if (desired == EnemyAnimMap.Hit || _current == null || (_current != null && _current.StartsWith(EnemyAnimMap.AttackPrefix, System.StringComparison.Ordinal) && desired == EnemyAnimMap.Death))
                Snap(desired, 0f);
            else
            {
                _animator.CrossFadeInFixedTime(h, CrossFadeSeconds, 0);
                _current = desired;
            }
        }
    }
}
