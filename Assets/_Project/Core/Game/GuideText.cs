// Core/Game/GuideText.cs: the words of the guide line (PL-2 task 2.1), one table so a copy pass edits one place. Pure C#. Authority: none (static data).
// Each kind has a plural line and, where a number is in it, a singular one; {0} is the number. Placeholder copy in the project's plain tone.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public static class GuideText
    {
        // kind -> (many, one). `one` is null when the line has no number or reads the same.
        static readonly Dictionary<GuideKind, (string Many, string One)> Table = new Dictionary<GuideKind, (string, string)>
        {
            { GuideKind.GoToStair, ("Find the trapdoor in the church and dive.", null) },
            { GuideKind.PiecesLeftHere, ("{0} pieces left on this floor.", "1 piece left on this floor.") },
            { GuideKind.StairDown, ("This floor is bare. Take the stair down.", null) },
            { GuideKind.GoToBuildPoint, ("You brought a frame home. Go to the build point and place it.", null) },
            { GuideKind.PlaceParts, ("{0} parts are ready. Place them at the build point.", "1 part is ready. Place it at the build point.") },
            { GuideKind.DiveForMore, ("{0} pieces are still missing. Dive for more.", "1 piece is still missing. Dive for more.") },
            { GuideKind.PlaceTheBaker, ("The bakery stands. Place the baker at the build point.", null) },
            { GuideKind.FindTheBaker, ("The bakery stands. Dive and find the baker.", null) },
            { GuideKind.MeetTheWishes, ("The baker has moved in. See what he wishes for at the build point.", null) },
            { GuideKind.DiveWithLoaves, ("The baker's loaves are ready. Dive.", null) },
        };

        public static IEnumerable<GuideKind> Kinds => Table.Keys;

        // The dive result card (task 2.3): a title, then one line each for what came home, what was lost, gold, pieces and people. Words only; the numbers come from DiveResult.
        public static List<string> ResultLines(DiveResult r)
        {
            var l = new List<string>();
            switch (r.Ending)
            {
                case DiveEnding.WentHome: l.Add("You went home."); break;
                case DiveEnding.Finished: l.Add("You reached the end of the Undercroft."); break;
                default: l.Add("The dive failed."); break;
            }
            if (r.FloorsCleared > 0) l.Add(r.FloorsCleared == 1 ? "1 floor cleared." : r.FloorsCleared + " floors cleared.");
            l.Add(Count(r.ItemsHome, "item") + " came home" + (r.ItemsBanked > 0 && r.ItemsBanked < r.ItemsHome ? " (" + r.ItemsBanked + " banked)." : r.ItemsBanked > 0 ? " (banked)." : "."));
            if (r.ItemsLost > 0) l.Add(Count(r.ItemsLost, "item") + " " + (r.ItemsLost == 1 ? "waits" : "wait") + " in a lost satchel on the floor where you fell.");
            if (r.SatchelLost) l.Add("The satchel you were carrying is gone.");
            l.Add(r.GoldLost > 0 ? "Gold kept: " + r.GoldKept + " (" + r.GoldLost + " lost)." : "Gold kept: " + r.GoldKept + ".");
            if (r.PiecesFound > 0) l.Add(Count(r.PiecesFound, "piece") + " found, safe in town.");
            foreach (string person in r.PeopleFound) l.Add((person == TownBuildings.BakerResident ? "The baker" : person) + " is safe in town.");
            return l;
        }

        static string Count(int n, string noun) => n + " " + noun + (n == 1 ? "" : "s");

        public static string For(GuideStep step)
        {
            if (!Table.TryGetValue(step.Kind, out (string Many, string One) t)) return "";
            string line = step.Count == 1 && t.One != null ? t.One : t.Many;
            return line.Replace("{0}", step.Count.ToString());
        }
    }
}
