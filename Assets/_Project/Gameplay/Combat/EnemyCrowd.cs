// Gameplay/Combat/EnemyCrowd.cs: the enemies of one room and the attack tokens they share (docs/research/enemy-ai.md 2.3, 10.4).
// Authority: host only. CombatHost owns one crowd per room (EnemyCrowd.ArenaRoom for the arena and anything spawned without a room); nothing here is
// replicated. Enemies Register on spawn and Unregister on death or despawn, so a room's table never holds a dead enemy's token and two rooms never
// share one table.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Gameplay
{
    public sealed class EnemyCrowd
    {
        public const int ArenaRoom = -1;                 // the single default crowd (arena, hunter, anything spawned without a room index)
        public const float AuditSeconds = 2f;

        public readonly AttackTokens Tokens = new AttackTokens();
        private readonly Dictionary<int, Enemy> _members = new Dictionary<int, Enemy>();
        private float _auditIn = AuditSeconds;

        public int Count => _members.Count;
        public bool Contains(Enemy e) => e != null && _members.ContainsKey(e.GetHashCode());

        public void Register(Enemy e)
        {
            if (e != null) _members[e.GetHashCode()] = e;
        }

        // Left the room by any path (death, despawn, clear): its tokens go with it.
        public void Unregister(Enemy e)
        {
            if (e == null) return;
            int id = e.GetHashCode();
            _members.Remove(id);
            Tokens.Release(id);
        }

        // Host, every frame: about every AuditSeconds frees tokens whose holder is no longer a living member (a missed Release can never shrink a pool for good).
        public void Tick(float dt, double now)
        {
            _auditIn -= dt;
            if (_auditIn > 0f) return;
            _auditIn = AuditSeconds;
            Tokens.Audit(now, IsLive);
        }

        private bool IsLive(int id) => _members.TryGetValue(id, out Enemy e) && e != null && e.Alive;
    }
}
