// Core/Cellars/FloorModifiers.cs: the two modifier slots of a floor (plan 5 task 2; design note 2026-11-04-floor-card-and-modifiers section 3). Pure C#, no engine references.
//   Slot A is a hazard with a printed reward, slot B a free boon; either may be empty. Calm floors (DiveFloorTable.IsCalmFloor) have none, floors 2 and 4 fill at most one slot, floor 7 may fill both.
//   Never two modifiers of one family on a floor; a modifier already used earlier in the dive becomes empty (earlier floors are replayed from the seed, as CellarFloorRoster.TwistOf did).
//   Dim is not rolled: slot A is Dim exactly when CellarAtmosphere.IsDarkFloor(seed, floor) on a generated floor (even calm floor 6), and it is exempt from the repeat rule (IsDarkFloor already allows one per dive).
//   The old hidden twists are folded in: Crowded and Ambush are hazards, Sparse is the Thin twist's boon (CellarFloorRoster.TwistOf derives from this set).
// A pure function of (seed, floor) on the new stream "cellars.modifier": no layout, dressing, atmosphere, loot or encounter stream is drawn from, so those goldens never move.
// Every number is a placeholder. Authority: none (static data derived from the dive seed; every peer computes the same answer).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum FloorModifier : byte { None = 0, Crowded = 1, Ambush = 2, Stirred = 3, Hurried = 4, Dim = 5, Brimming = 6, FatLarder = 7, Sparse = 20, WarmHands = 21, Hushed = 22, StillAir = 23 }

    public enum ModifierFamily : byte { None, Enemies, Stir, Clock, Light, Healing, Ambience }

    // PLACEHOLDER tuning in ONE spot (decisions 1 and 2 of plan 5). Hazard weights are equal among Crowded, Ambush, Stirred, Hurried; boon weights equal among Sparse, WarmHands, Hushed.
    public static class FloorModifierTuning
    {
        public const int EarlyAPercent = 40, EarlyBPercent = 35;   // placeholder: floors 2 and 4, one roll 0..99: < 40 hazard, < 75 boon, else none
        public const int LateAPercent = 75, LateBPercent = 60;     // placeholder: floors >= 6, rolled independently
        public const float StirredBonus = 10f;                     // placeholder: stir added while the party is on the Stirred floor
        public const float HurriedCollapseMultiplier = 1.25f;      // placeholder: collapse multiplier on a Hurried floor (DiveStir.CollapseMultiplier is 1.5)
        public const float WarmHandsRechargeSeconds = 15f;         // placeholder: healing point recharge (30 s otherwise)
        public const int CrowdedGoldPerClear = 15, AmbushGold = 40, HurriedGold = 30;   // placeholder: gold per present player
        public const int DimRankBonus = 1;                         // placeholder: rank bonus on every chest roll on a Dim floor
        public const int StirredExtraRolls = 1;                    // placeholder: extra chest roll per player on a Stirred floor
        public const float BrimmingRechargeSeconds = 45f;          // placeholder: healing point recharge on a Brimming (damp) floor
        public const int BrimmingRollsPerRoom = 1;                 // placeholder: extra loot roll per present hero for each room cleared on a Brimming floor
        public const int FatLarderRolls = 1;                       // placeholder: extra loot roll per present hero when the Fat Larder room is cleared
    }

    public readonly struct FloorModifierSet
    {
        public readonly FloorModifier A, B;
        public FloorModifierSet(FloorModifier a, FloorModifier b) { A = a; B = b; }

        static readonly FloorModifier[] Hazards = { FloorModifier.Crowded, FloorModifier.Ambush, FloorModifier.Stirred, FloorModifier.Hurried, FloorModifier.Brimming, FloorModifier.FatLarder };   // append only: the draw is Range(0, Length), so growing the array shifts goldens (plan 5c, deliberate)
        static readonly FloorModifier[] Boons = { FloorModifier.Sparse, FloorModifier.WarmHands, FloorModifier.Hushed, FloorModifier.StillAir };

        public bool Has(FloorModifier m) => m != FloorModifier.None && (A == m || B == m);

        public static ModifierFamily FamilyOf(FloorModifier m)
        {
            switch (m)
            {
                case FloorModifier.Crowded: case FloorModifier.Ambush: case FloorModifier.Sparse: case FloorModifier.FatLarder: return ModifierFamily.Enemies;
                case FloorModifier.Stirred: case FloorModifier.Hushed: return ModifierFamily.Stir;
                case FloorModifier.Hurried: return ModifierFamily.Clock;
                case FloorModifier.Dim: return ModifierFamily.Light;
                case FloorModifier.WarmHands: case FloorModifier.Brimming: return ModifierFamily.Healing;
                case FloorModifier.StillAir: return ModifierFamily.Ambience;
                default: return ModifierFamily.None;
            }
        }

        // The modifiers of one floor: a pure function of (seed, floor). Compute on floor entry and cache; never per frame (it replays the earlier floors, and the atmosphere roll, each call).
        public static FloorModifierSet For(uint seed, int floor)
        {
            if (!CellarFloorGenerator.IsGeneratedFloor(floor)) return default;
            var used = new HashSet<FloorModifier>();   // earlier generated floors of the dive, replayed
            for (int f = UndercroftPlan.FirstFloor; f <= floor; f++)
            {
                if (!CellarFloorGenerator.IsGeneratedFloor(f)) continue;
                FloorModifierSet s = Roll(seed, f, used);
                if (f == floor) return s;
                if (s.A != FloorModifier.None && s.A != FloorModifier.Dim) used.Add(s.A);
                if (s.B != FloorModifier.None) used.Add(s.B);
            }
            return default;
        }

        static FloorModifierSet Roll(uint seed, int floor, HashSet<FloorModifier> used)
        {
            // every draw happens on every call, so a rule change never shifts a later draw
            var rng = new Pcg32(Rng.Derive(seed, "cellars.modifier", (ulong)floor));
            int r1 = rng.Range(0, 100), r2 = rng.Range(0, 100), hazard = rng.Range(0, Hazards.Length), boon = rng.Range(0, Boons.Length);
            bool dark = CellarAtmosphere.IsDarkFloor(seed, floor), calm = DiveFloorTable.IsCalmFloor(floor), early = floor < 6;
            FloorModifier a = FloorModifier.None, b = FloorModifier.None;
            if (dark) a = FloorModifier.Dim;
            if (!calm)
            {
                if (early)
                {
                    if (!dark && r1 < FloorModifierTuning.EarlyAPercent) a = Hazards[hazard];
                    else if (!dark && r1 < FloorModifierTuning.EarlyAPercent + FloorModifierTuning.EarlyBPercent) b = Boons[boon];
                }
                else
                {
                    if (!dark && r1 < FloorModifierTuning.LateAPercent) a = Hazards[hazard];
                    if (r2 < FloorModifierTuning.LateBPercent) b = Boons[boon];
                }
            }
            if (a != FloorModifier.None && a != FloorModifier.Dim && used.Contains(a)) a = FloorModifier.None;
            if (b != FloorModifier.None && (used.Contains(b) || FamilyOf(b) == FamilyOf(a))) b = FloorModifier.None;
            return new FloorModifierSet(a, b);
        }

        // The stir the Stirred hazard adds while the party is on its floor: StirredBonus when the floor the party is on has it, else 0 (it leaves with the floor).
        public static float StirredBonusFor(uint seed, int currentFloor) => For(seed, currentFloor).Has(FloorModifier.Stirred) ? FloorModifierTuning.StirredBonus : 0f;

        // ---- the effects the host applies on a floor with this set (plan 5 task 5); pure helpers so the numbers live here and the host code only calls them ----
        public int GoldPerRoomClear => Has(FloorModifier.Crowded) ? FloorModifierTuning.CrowdedGoldPerClear : 0;       // each present player, each time a room becomes cleared
        public int AmbushRoomGold => Has(FloorModifier.Ambush) ? FloorModifierTuning.AmbushGold : 0;                    // each present player, when the ambush room is cleared
        public int LeaveGold => Has(FloorModifier.Hurried) ? FloorModifierTuning.HurriedGold : 0;                       // each present player, when the party leaves by the stair or exit
        public int ExtraChestRolls => Has(FloorModifier.Stirred) ? FloorModifierTuning.StirredExtraRolls : 0;           // extra rolls per loot chest per player
        public int ChestRankBonus(int baseBonus) => baseBonus + (Has(FloorModifier.Dim) ? FloorModifierTuning.DimRankBonus : 0);
        // Warm Hands shortens the recharge, Brimming (damp) lengthens it; they share the Healing family, so never both.
        public float HealRechargeSeconds(float baseSeconds) =>
            Has(FloorModifier.WarmHands) ? System.Math.Min(baseSeconds, FloorModifierTuning.WarmHandsRechargeSeconds)
            : Has(FloorModifier.Brimming) ? System.Math.Max(baseSeconds, FloorModifierTuning.BrimmingRechargeSeconds) : baseSeconds;
        // Extra loot rolls each present hero gets when `room` becomes cleared (once per room: the host's _roomPaid guard). Brimming: every room; Fat Larder: only its fat room (fatRoom is CellarEncounterPlan.FatRoom, -1 when none).
        public int ExtraRollsOnClear(int room, int fatRoom) =>
            (Has(FloorModifier.Brimming) ? FloorModifierTuning.BrimmingRollsPerRoom : 0)
            + (Has(FloorModifier.FatLarder) && fatRoom >= 0 && room == fatRoom ? FloorModifierTuning.FatLarderRolls : 0);
        public bool AmbientLife => !Has(FloorModifier.StillAir);   // presentation: false means no drips or steam on this floor (the overlays themselves are later P5 work)
        // The factor the collapse is started with (DiveStir.BeginCollapse): it scales the final seconds, minimum included, so a Hurried floor's 1.25 vs 1.5 shows even on a short walk.
        public float CollapseFactor => Has(FloorModifier.Hurried) ? FloorModifierTuning.HurriedCollapseMultiplier / DiveStir.CollapseMultiplier : 1f;

        public ulong Hash()
        {
            var h = Fnv1a64.Create();
            h.AppendInt32LE((int)A); h.AppendInt32LE((int)B);
            return h.Value;
        }
    }
}
