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

        public static string For(GuideStep step)
        {
            if (!Table.TryGetValue(step.Kind, out (string Many, string One) t)) return "";
            string line = step.Count == 1 && t.One != null ? t.One : t.Many;
            return line.Replace("{0}", step.Count.ToString());
        }
    }
}
