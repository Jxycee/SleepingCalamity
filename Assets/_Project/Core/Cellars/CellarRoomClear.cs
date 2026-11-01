// Core/Cellars/CellarRoomClear.cs: per-room spawned/alive counts of one Cellars floor and the cleared set (PHASE1 7.2). Pure C#, no engine references.
// A room is cleared when at least one enemy was spawned into it and none is alive. The host drives it (Spawn on each spawn, Died on each death); peers only see the
// ClearedMask the host publishes. Authority: host (this object lives on the host only).
using System.Collections.Generic;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public sealed class CellarRoomClear
    {
        readonly Dictionary<int, int> _spawned = new Dictionary<int, int>(), _alive = new Dictionary<int, int>();
        ulong _cleared;

        public ulong ClearedMask => _cleared;
        public int Spawned(int room) => _spawned.TryGetValue(room, out int n) ? n : 0;
        public int Alive(int room) => _alive.TryGetValue(room, out int n) ? n : 0;
        public bool IsCleared(int room) => room >= 0 && room < 64 && (_cleared & (1UL << room)) != 0;

        public void Spawn(int room)
        {
            if (room < 0 || room >= 64) return;
            _spawned[room] = Spawned(room) + 1; _alive[room] = Alive(room) + 1;
            _cleared &= ~(1UL << room);   // a late extra into a cleared room reopens it
        }

        // True when this death cleared the room.
        public bool Died(int room)
        {
            if (room < 0 || room >= 64 || Alive(room) <= 0) return false;
            _alive[room] = Alive(room) - 1;
            if (_alive[room] > 0) return false;
            _cleared |= 1UL << room;
            return true;
        }

        public void Reset() { _spawned.Clear(); _alive.Clear(); _cleared = 0; }
    }

    // Which Tier1 enemy kind stands on an authored or extra spawn: a floor-banded weighted pick, deterministic per (seed, floor, room, index) on the stream "cellars.mix".
    public static class CellarEnemyMix
    {
        // weights for Slime, Broom, RatKing per floor 1-4
        static readonly int[][] Weights = { new[] { 100, 0, 0 }, new[] { 60, 40, 0 }, new[] { 20, 50, 30 }, new[] { 30, 40, 30 } };

        public static EnemyKind Tier1Kind(uint seed, int floor, int room, int index)
        {
            int[] w = Weights[System.Math.Max(1, System.Math.Min(4, floor)) - 1];
            var rng = new Pcg32(Rng.Derive(seed, "cellars.mix", ((ulong)floor << 32) | ((uint)room << 8) | (uint)index));
            int roll = rng.Range(0, w[0] + w[1] + w[2]);
            return roll < w[0] ? EnemyKind.Slime : roll < w[0] + w[1] ? EnemyKind.Broom : EnemyKind.RatKing;
        }
    }
}
