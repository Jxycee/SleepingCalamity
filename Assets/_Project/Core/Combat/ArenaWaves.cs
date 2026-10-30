// Core/Combat/ArenaWaves.cs: the combat test arena's waves as data (M3 greybox). Pure C#. Tuned so the user's 5-minute arena covers all three enemies,
// the elite and a tier 2 pass. P9 Placeholder.
// Authority: none (static data; the host's CombatHost reads it).
using System;

namespace SleepingCalamity.Core.Combat
{
    public readonly struct WaveEntry
    {
        public readonly EnemyKind Kind;
        public readonly int Count;
        public WaveEntry(EnemyKind kind, int count) { Kind = kind; Count = count; }
    }

    public static class ArenaWaves
    {
        public const int CompositionCount = 4;

        // Waves 1-4 are tier 1; every wave after 4 repeats the same four compositions at tier 2.
        public static int TierOf(int wave) => wave > CompositionCount ? 2 : 1;

        public static WaveEntry[] Composition(int wave)
        {
            switch ((Math.Max(1, wave) - 1) % CompositionCount)
            {
                case 0: return new[] { new WaveEntry(EnemyKind.Slime, 4) };
                case 1: return new[] { new WaveEntry(EnemyKind.Slime, 2), new WaveEntry(EnemyKind.RatKing, 2) };
                case 2: return new[] { new WaveEntry(EnemyKind.Broom, 2), new WaveEntry(EnemyKind.RatKing, 2) };
                default: return new[] { new WaveEntry(EnemyKind.DoughWarden, 1), new WaveEntry(EnemyKind.Slime, 2) };
            }
        }

        // The enemy kinds to spawn, one entry per enemy: the composition plus one extra of its most numerous non-elite kind per extra player (P23).
        public static EnemyKind[] Roster(int wave, int players)
        {
            WaveEntry[] comp = Composition(wave);
            var list = new System.Collections.Generic.List<EnemyKind>();
            EnemyKind most = EnemyKind.Slime;
            int mostCount = -1;
            foreach (WaveEntry e in comp)
            {
                for (int i = 0; i < e.Count; i++) list.Add(e.Kind);
                if (e.Kind != EnemyKind.DoughWarden && e.Count > mostCount) { most = e.Kind; mostCount = e.Count; }
            }
            for (int i = 0; i < CoopScaling.ExtraEnemiesPerRoom(players); i++) list.Add(most);
            return list.ToArray();
        }
    }
}
