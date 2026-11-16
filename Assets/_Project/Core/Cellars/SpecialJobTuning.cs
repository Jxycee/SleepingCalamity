// Core/Cellars/SpecialJobTuning.cs: what each special-room kind does when its room is cleared (dive step 6 job 2). Pure C#. Authority: none (the host applies the numbers, once per room per floor visit).
// The effects hang on the existing SpecialKind (CellarSpecialRoom.cs): which kind a floor gets, and its room, are untouched (SpecialHash stays the same). All numbers are PLACEHOLDERS, kept small for dungeon one.
//   Quiet     = mending spring: clearing the room heals every up hero by MendPercent of max HP.
//   EliteDen  = challenge hall: clearing it pays EliteDenRolls extra loot rolls to each present hero.
//   Treasury  = vault: clearing it pays TreasuryRolls extra loot rolls to each present hero.
//   Shrine    = stir altar: clearing it adds ShrineStir to the stir for the rest of the floor (it leaves with the floor, like the Stirred modifier).
namespace SleepingCalamity.Core
{
    public static class SpecialJobTuning
    {
        public const int MendPercent = 50;       // Quiet: percent of max HP restored once
        public const int EliteDenRolls = 2;      // EliteDen: extra loot rolls per present hero
        public const int TreasuryRolls = 1;      // Treasury: extra loot rolls per present hero
        public const float ShrineStir = 12f;     // Shrine: flat stir added while the party is on that floor
        public const int ExtraRollChestBase = 70000;   // roll index base for these rolls (the floor modifiers use 50000 + room, Stirred chestIndex + 1000 k)
    }

    public readonly struct SpecialJobEffect
    {
        public readonly int MendPercent, ExtraRolls; public readonly float StirDelta;
        public SpecialJobEffect(int mendPercent, int extraRolls, float stirDelta) { MendPercent = mendPercent; ExtraRolls = extraRolls; StirDelta = stirDelta; }
        public bool Any => MendPercent > 0 || ExtraRolls > 0 || StirDelta > 0f;
        public static readonly SpecialJobEffect None = new SpecialJobEffect(0, 0, 0f);
    }

    public static class SpecialJobs
    {
        // The effect of clearing `room` on a floor whose special room is `special` (null or None: nothing). Pure; the host's once-per-room latch lives in CellarContentsSync._roomPaid.
        public static SpecialJobEffect OnClear(SpecialRoom special, int room)
        {
            if (special == null || !special.Exists || room < 0 || room != special.RoomIndex) return SpecialJobEffect.None;
            switch (special.Kind)
            {
                case SpecialKind.Quiet: return new SpecialJobEffect(SpecialJobTuning.MendPercent, 0, 0f);
                case SpecialKind.EliteDen: return new SpecialJobEffect(0, SpecialJobTuning.EliteDenRolls, 0f);
                case SpecialKind.Treasury: return new SpecialJobEffect(0, SpecialJobTuning.TreasuryRolls, 0f);
                case SpecialKind.Shrine: return new SpecialJobEffect(0, 0, SpecialJobTuning.ShrineStir);
                default: return SpecialJobEffect.None;
            }
        }

        // HP restored by a mend of `percent` of `maxHp` (rounded down, at least 1 when percent > 0 and maxHp > 0).
        public static int MendAmount(int maxHp, int percent) => maxHp <= 0 || percent <= 0 ? 0 : System.Math.Max(1, maxHp * percent / 100);

        // The stir the shrine adds on `currentFloor`: the delta while the party is on the floor the altar was triggered on, else 0. `shrineFloor` is 0 when none was triggered.
        public static float StirBonusOnFloor(int shrineFloor, float delta, int currentFloor) => shrineFloor > 0 && shrineFloor == currentFloor ? delta : 0f;
    }
}
