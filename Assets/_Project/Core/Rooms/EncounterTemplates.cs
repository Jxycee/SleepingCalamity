// Core/Rooms/EncounterTemplates.cs: the eight tier-1 encounter templates (research 08 section 7.1 minus Elite arena and Hunter). Authority: none (static data).
// Deviations from the research table, both because the Cellars are tier 1 only and a solo room may hold at most its authored spawn count + 1:
//   Gel swarm is 4 (2 swarm + 2 lobbers; research 5); Mixed tier is Rat + Broom + Slime (research: a tier-2 Rat + 2 tier-1 Rats).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core.Combat;

namespace SleepingCalamity.Core
{
    public static class EncounterTemplates
    {
        public const string PackId = "pack", GelSwarmId = "gel_swarm", ShieldWallId = "shield_wall", RushBombardId = "rush_bombard",
            GuardedCasterId = "guarded_caster", MixedTierId = "mixed_tier", AmbushId = "ambush", ReinforcedId = "reinforced";

        static IReadOnlyList<EncounterTemplate> all;
        public static IReadOnlyList<EncounterTemplate> All => all ?? (all = Make().AsReadOnly());

        public static EncounterTemplate Find(string id) { foreach (EncounterTemplate t in All) if (t.Id == id) return t; return null; }

        static List<EncounterTemplate> Make()
        {
            const EnemyKind R = EnemyKind.RatKing, S = EnemyKind.Slime, B = EnemyKind.Broom;
            EncounterRole Role(string n, EnemyKind k, int c, int w, EncounterAnchor a) => new EncounterRole(n, k, c, w, a);
            return new List<EncounterTemplate>
            {
                new EncounterTemplate(PackId, "Pack", RoomAffordance.OpenFloor, new[] { Role("rusher", R, 3, 0, EncounterAnchor.Open) }, R, R, R),
                new EncounterTemplate(GelSwarmId, "Gel swarm", RoomAffordance.None, new[] { Role("swarm", S, 2, 0, EncounterAnchor.Open), Role("lobber", S, 2, 0, EncounterAnchor.Dais) }, S, S, S),
                new EncounterTemplate(ShieldWallId, "Shield wall", RoomAffordance.None, new[] { Role("shield", B, 2, 0, EncounterAnchor.Door), Role("blocker", S, 2, 0, EncounterAnchor.Open) }, S, S, B),
                new EncounterTemplate(RushBombardId, "Rush and bombard", RoomAffordance.None, new[] { Role("rusher", R, 2, 0, EncounterAnchor.Open), Role("lobber", S, 2, 0, EncounterAnchor.Pillar) }, R, S, R),
                new EncounterTemplate(GuardedCasterId, "Guarded caster", RoomAffordance.None, new[] { Role("shield", B, 1, 0, EncounterAnchor.Door), Role("lobber", S, 2, 0, EncounterAnchor.Far) }, S, S, B),
                new EncounterTemplate(MixedTierId, "Mixed tier", RoomAffordance.OpenFloor, new[] { Role("rusher", R, 1, 0, EncounterAnchor.Open), Role("shield", B, 1, 0, EncounterAnchor.Door), Role("lobber", S, 1, 0, EncounterAnchor.Far) }, R, S, B),
                new EncounterTemplate(AmbushId, "Ambush", RoomAffordance.None, new[] { Role("hidden", R, 2, 0, EncounterAnchor.Open) }, R, R, R),
                new EncounterTemplate(ReinforcedId, "Reinforced", RoomAffordance.OpenFloor, new[] { Role("rusher", R, 3, 0, EncounterAnchor.Open), Role("reinforcement", R, 2, 1, EncounterAnchor.Door) }, R, R, R),
            };
        }
    }
}
