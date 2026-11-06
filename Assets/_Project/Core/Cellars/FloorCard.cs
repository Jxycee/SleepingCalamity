// Core/Cellars/FloorCard.cs: the text of a floor's card (plan 5 task 2; design note 2026-11-04-floor-card-and-modifiers section 2). Pure C#, no engine references.
//   FloorCard = name + floor number + objective + modifiers, a pure function of (seed, floor, objective): the objective comes from FloorObjectivePlan.For(layout), the card never regenerates a layout.
//   FloorCardNames: the authored floors carry fixed names; each generated floor draws one of five hand-written names for its SubTheme on "cellars.card.name", never repeating within a dive
//   (earlier generated floors are replayed from the seed, their themes read from CellarFloorGenerator.Layout; cheap enough for a floor-entry call).
//   FloorCardText: every line the HUD prints, built from the tuning consts so a number changes in one place.
// Authority: none (static data derived from the dive seed; every peer computes the same answer).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public sealed class FloorCard
    {
        public readonly string Name;
        public readonly int Floor;
        public readonly FloorObjective Objective;
        public readonly FloorModifierSet Modifiers;

        FloorCard(string name, int floor, FloorObjective objective, FloorModifierSet modifiers) { Name = name; Floor = floor; Objective = objective; Modifiers = modifiers; }

        public static FloorCard For(uint seed, int floor, FloorObjective objective)
        {
            SubTheme theme = CellarFloorGenerator.IsGeneratedFloor(floor) ? CellarFloorGenerator.Layout(seed, floor).Theme : SubTheme.Any;
            return new FloorCard(FloorCardNames.NameFor(seed, floor, theme), floor, objective, FloorModifierSet.For(seed, floor));
        }
    }

    public static class FloorCardNames
    {
        public const string Floor1 = "The Undercroft Steps", Floor3 = "The Drowned Bakery", Floor5 = "The Cistern Dome";
        const string Deepest = "The Deep Cellars";   // a floor past the built dive; no card is shown for it today

        static readonly string[] Larder = { "The Smoking Larder", "The Salt Shelves", "The Cold Crocks", "The Pickle Rows", "The Hook Hall" };
        static readonly string[] WineVault = { "The Weeping Casks", "The Corkline", "The Drowned Racks", "The Vinegar Walk", "The Long Bung" };
        static readonly string[] CoalCellar = { "The Ember Chutes", "The Sooted Stair", "The Coal Throat", "The Cinder Bins", "The Black Hods" };
        static readonly string[] Bakery = { "The Proving Rooms", "The Flour Drifts", "The Cold Ovens", "The Crust Cellars", "The Peel Racks" };

        static string[] TableOf(SubTheme t) => t == SubTheme.Larder ? Larder : t == SubTheme.WineVault ? WineVault : t == SubTheme.CoalCellar ? CoalCellar : Bakery;   // Bakery and Any

        public static string NameFor(uint seed, int floor, SubTheme theme)
        {
            if (floor == 1) return Floor1;
            if (floor == 3) return Floor3;
            if (floor == UndercroftPlan.BankFloor) return Floor5;
            if (!CellarFloorGenerator.IsGeneratedFloor(floor)) return Deepest;
            var used = new HashSet<string>();
            for (int f = UndercroftPlan.FirstFloor; f <= floor; f++)
            {
                if (!CellarFloorGenerator.IsGeneratedFloor(f)) continue;
                string[] table = TableOf(f == floor ? theme : CellarFloorGenerator.Layout(seed, f).Theme);
                var left = new List<string>();
                foreach (string n in table) if (!used.Contains(n)) left.Add(n);
                var rng = new Pcg32(Rng.Derive(seed, "cellars.card.name", (ulong)f));
                string pick = left[rng.Range(0, left.Count)];   // five names, at most four generated floors: never empty
                if (f == floor) return pick;
                used.Add(pick);
            }
            return Deepest;
        }
    }

    public static class FloorCardText
    {
        public const string WayOutSuffix = " to open the way out";   // floor 7 prints any objective with this added (WithWayOut)

        // gateKey: the host's ContentsState.GateKey (0 none yet, 1 lying, 2 taken). pumpProgress is 0..1.
        public static string ObjectiveLine(FloorObjective o, int valvesTurned, float pumpProgress, bool pumpStarted, byte gateKey)
        {
            switch (o)
            {
                case FloorObjective.GateKey: return gateKey == 2 ? "Gate key found: the way down is open" : "Find the gate key";
                case FloorObjective.TwoValves: return "Open both valves (" + valvesTurned + "/2)";
                case FloorObjective.HoldPump: return pumpStarted ? "Start the pump and hold it (" + (int)(System.Math.Max(0f, System.Math.Min(1f, pumpProgress)) * 100f) + "%)" : "Start the pump";
                case FloorObjective.Rescue: return "Wake the baker";
                case FloorObjective.Guardian: return "Beat the guardian, then reach the bank";
                case FloorObjective.Ending: return "Reach the way out";
                default: return "";
            }
        }

        public static string WithWayOut(string objectiveLine) => objectiveLine.Length == 0 ? objectiveLine : objectiveLine + WayOutSuffix;

        public static string Title(FloorModifier m)
        {
            switch (m)
            {
                case FloorModifier.Crowded: return "Crowded";
                case FloorModifier.Ambush: return "Ambush";
                case FloorModifier.Stirred: return "Stirred";
                case FloorModifier.Hurried: return "Hurried";
                case FloorModifier.Dim: return "Dim";
                case FloorModifier.Sparse: return "Sparse";
                case FloorModifier.WarmHands: return "Warm Hands";
                case FloorModifier.Hushed: return "Hushed";
                default: return "";
            }
        }

        public static string EffectLine(FloorModifier m)
        {
            switch (m)
            {
                case FloorModifier.Crowded: return "More enemies in about half the rooms";
                case FloorModifier.Ambush: return "Enemies spring from one room";
                case FloorModifier.Stirred: return "The floor is stirred: +" + (int)FloorModifierTuning.StirredBonus + " stir while you are here";
                case FloorModifier.Hurried: return "The collapse comes faster here";
                case FloorModifier.Dim: return "The floor is dark";
                case FloorModifier.Sparse: return "Fewer enemies in about half the rooms";
                case FloorModifier.WarmHands: return "Healing points recharge in " + (int)FloorModifierTuning.WarmHandsRechargeSeconds + " s";
                case FloorModifier.Hushed: return "The hunter stays away this floor";
                default: return "";
            }
        }

        // The reward line of a hazard; boons and None print none.
        public static string RewardLine(FloorModifier m)
        {
            switch (m)
            {
                case FloorModifier.Crowded: return "Reward: +" + FloorModifierTuning.CrowdedGoldPerClear + " gold per room cleared, each";
                case FloorModifier.Ambush: return "Reward: +" + FloorModifierTuning.AmbushGold + " gold each when the ambush room is cleared";
                case FloorModifier.Stirred: return "Reward: every chest gives each hero " + (FloorModifierTuning.StirredExtraRolls == 1 ? "one extra roll" : FloorModifierTuning.StirredExtraRolls + " extra rolls");
                case FloorModifier.Hurried: return "Reward: +" + FloorModifierTuning.HurriedGold + " gold each when you leave by the stair";
                case FloorModifier.Dim: return "Reward: better chest finds";
                default: return "";
            }
        }
    }
}
