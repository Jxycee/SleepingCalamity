// Gameplay/Combat/EnemyCrowd.cs: the enemies of one room and the attack tokens they share (docs/research/enemy-ai.md 2.3, 10.4).
// Authority: host only. CombatHost owns one crowd per room (EnemyCrowd.ArenaRoom for the arena and anything spawned without a room); nothing here is
// replicated. A crowd is the room's MEMBERSHIP (Register on spawn, Unregister on death or despawn; room planners later). The attack tokens are NOT per
// room: every crowd of one CombatHost shares the host's single AttackTokens, because a hero's pools and strike gap must hold across every enemy that can
// target it, whichever room it spawned in (targeting is world-wide). Only an enemy spawned outside CombatHost gets a private crowd with its own tokens.
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;
using UnityEngine;

namespace SleepingCalamity.Gameplay
{
    public sealed class EnemyCrowd
    {
        public const int ArenaRoom = -1;                 // the single default crowd (arena, hunter, anything spawned without a room index)
        public const float AuditSeconds = 2f;   // how often CombatHost audits the shared tokens

        public readonly AttackTokens Tokens;
        private readonly Dictionary<int, Enemy> _members = new Dictionary<int, Enemy>();

        public EnemyCrowd(AttackTokens tokens) { Tokens = tokens ?? new AttackTokens(); }

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
            Tokens.Release(id, Time.timeAsDouble);
        }

        // True while `id` is a living member of this room. The shared tokens are audited once by CombatHost against every crowd (a crowd alone would free
        // the other rooms' holds).
        public bool IsLive(int id) => _members.TryGetValue(id, out Enemy e) && e != null && e.Alive;
    }
}
