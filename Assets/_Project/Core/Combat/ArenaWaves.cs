// Core/Combat/ArenaWaves.cs: the combat test arena's waves as data (M3 greybox). Pure C#. Tuned so the user's 5-minute arena covers all three enemies,
// the elite and a tier 2 pass. P9 Placeholder. After the first playtest (2026-10-30) waves 1 and 2 are an onboarding: wave 1 was four slimes, now two;
// wave 2 was two slimes and two rat-kings, now three slimes and ONE rat-king. Waves 3 and 4 are unchanged.
// Enemy polish (2026-10-30): tier 2 no longer repeats waves 1-4. Each tier-2 wave MIXES kinds (a slime's lob from range while a rat-king rushes, a broom
// that must be flanked while something else attacks), so the step to tier 2 is pressure and mix, with AttackPacing keeping the strikes apart.
// Combat depth (2026-10-30, "fewer, tougher enemies"): two enemies per tier-1 wave and three per tier-2 wave, each with about twice the life, so each
// fight is a short duel (OoT: the others wait). Wave 2: a rat-king and a slime; 3: a broom and a rat-king; 4: the Warden alone.
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

        // Waves 1-4 are tier 1; every wave after 4 is tier 2 and cycles through its own four mixed compositions (below).
        public static int TierOf(int wave) => wave > CompositionCount ? 2 : 1;

        public static WaveEntry[] Composition(int wave)
        {
            if (TierOf(wave) >= 2)
                switch ((Math.Max(1, wave) - 1) % CompositionCount)
                {
                    case 0: return new[] { new WaveEntry(EnemyKind.RatKing, 1), new WaveEntry(EnemyKind.Broom, 1), new WaveEntry(EnemyKind.Slime, 1) };
                    case 1: return new[] { new WaveEntry(EnemyKind.RatKing, 2), new WaveEntry(EnemyKind.Broom, 1) };
                    case 2: return new[] { new WaveEntry(EnemyKind.Broom, 2), new WaveEntry(EnemyKind.Slime, 1) };
                    default: return new[] { new WaveEntry(EnemyKind.DoughWarden, 1), new WaveEntry(EnemyKind.RatKing, 1) };
                }
            switch ((Math.Max(1, wave) - 1) % CompositionCount)
            {
                case 0: return new[] { new WaveEntry(EnemyKind.Slime, 2) };
                case 1: return new[] { new WaveEntry(EnemyKind.RatKing, 1), new WaveEntry(EnemyKind.Slime, 1) };
                case 2: return new[] { new WaveEntry(EnemyKind.Broom, 1), new WaveEntry(EnemyKind.RatKing, 1) };
                default: return new[] { new WaveEntry(EnemyKind.DoughWarden, 1) };
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
