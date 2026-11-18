// T12 decal budget (design rule 4: about 300 for the area). The ground-blend strips and litter patches that overlap their neighbours are thinned to every second one by name
// (merging was not needed: the dropped ones sit inside the 4-10 m footprints of the kept ones); contact decals under props (Hay_*, Pier*, Grime_*, Soot_*, Nook*, Pit_*, Tended*)
// are never dropped. Deterministic: the rule reads only the name.
using System.Text.RegularExpressions;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereDecalThin
    {
        static int Sum(string name) { int s = 0; foreach (Match m in Regex.Matches(name, "[0-9]+")) s += int.Parse(m.Value); return s; }

        public static bool Drop(string name)
        {
            bool odd = (Sum(name) & 1) == 1;
            if (name.StartsWith("Rut_") || name.StartsWith("WornPath_") || name.StartsWith("HillDecal_") || name.StartsWith("PathMud_") || name.StartsWith("TownWallFoot_") || name.StartsWith("TrunkMoss_")) return odd;
            if (name.StartsWith("BankDecal_") || name.StartsWith("SBankDecal_") || name.StartsWith("ScarpDecal_") || name.StartsWith("SlopeDecal_") || name.StartsWith("CragDecal_")) return odd;
            if (name.StartsWith("CobbleLeaves_") || name.StartsWith("CobbleMoss_") || name.StartsWith("FootDecal_") || name.StartsWith("GraveMoss_")) return odd;
            if (name.StartsWith("Litter_") && name.EndsWith("b")) return true;
            if (name.StartsWith("Gap") && (char.IsDigit(name[name.Length - 1]) || name.EndsWith("C") || name.EndsWith("B"))) return true;
            return false;
        }
    }
}
