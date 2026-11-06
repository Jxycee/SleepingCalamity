// Core/Rooms/EncounterTemplate.cs: one enemy-group shape for a Cellars combat room (encounter templates plan, Task 1; research 08 section 7.1). Pure C#, no engine references.
// A template names roles (kind, count, wave, anchor class), the kind each extra player adds, and the room affordance it wants. Cells are resolved later by EncounterBuilder.
// Authority: none (static data).
using System;
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    // Where a role prefers to stand inside the room (resolved by integer scoring, never floats).
    public enum EncounterAnchor : byte { Door, Open, Pillar, Dais, Far }

    public sealed class EncounterRole
    {
        public readonly string Name; public readonly EnemyKind Kind; public readonly int Count, Wave; public readonly EncounterAnchor Anchor;
        public EncounterRole(string name, EnemyKind kind, int count, int wave, EncounterAnchor anchor) { Name = name; Kind = kind; Count = count; Wave = wave; Anchor = anchor; }
    }

    public sealed class EncounterTemplate
    {
        public const int MaxWaves = 2;
        public readonly string Id, Name; public readonly IReadOnlyList<EncounterRole> Roles;
        public readonly IReadOnlyList<EnemyKind> PerPlayerAdds;   // adds[i] joins wave 0 for player i + 2: at most one per extra player (CoopScaling.ExtraEnemiesPerRoom)
        public readonly RoomAffordance Wants;

        public EncounterTemplate(string id, string name, RoomAffordance wants, IEnumerable<EncounterRole> roles, params EnemyKind[] perPlayerAdds)
        { Id = id; Name = name; Wants = wants; Roles = roles.ToList(); PerPlayerAdds = perPlayerAdds ?? new EnemyKind[0]; }

        public int WaveCount => Roles.Max(r => r.Wave) + 1;
        public bool IsMultiWave => WaveCount > 1;
        public int BaseCount => Roles.Sum(r => r.Count);                                   // solo, all waves
        public int BaseInWave(int wave) => Roles.Where(r => r.Wave == wave).Sum(r => r.Count);
        public int SoloPeak => Enumerable.Range(0, WaveCount).Max(BaseInWave);              // most enemies alive at once, solo
        public IReadOnlyList<EnemyKind> Kinds => Roles.Select(r => r.Kind).Concat(PerPlayerAdds).Distinct().ToList();
        public int DistinctKinds => Kinds.Count;
        public bool IsAmbush => Id == EncounterTemplates.AmbushId;
        // Floors 1-2 allow one wave and two kinds (plan guardrail); the rest are floor 3 up.
        public bool LegalOnFloor(int floor) => floor >= 3 || (!IsMultiWave && DistinctKinds <= 2);
    }
}
