// Core/Cellars/StirTuning.cs: every stir-meter number in ONE place (PHASE1 7.2 "The stir meter", P21 placeholders). Pure C#, no engine references.
// StirMeter reads only this struct; tests pass their own instance (e.g. a short countdown). Retune here, nowhere else.
namespace SleepingCalamity.Core
{
    public struct StirTuning
    {
        public float PerFloorCleared;       // meter points per floor cleared this dive
        public float PerUnbankedPiece;      // unused since plan 2 (spec 4.5); DiveStir replaces it in plan 3
        public float PerExtraPlayer;        // added to the multiplier per player beyond the first (0.25 = +25% each)
        public float Max;                   // clamp ceiling
        public float ExtraEnemyThreshold;   // latch: one extra enemy per room
        public float HunterThreshold;       // latch: rat-king hunter released
        public float DeepClosesThreshold;   // latch: the deep closes, countdown starts
        public float DeepClosesSeconds;     // countdown length; 0 fails the dive unless a bank point is reached first
        public int ExtraEnemiesPerRoom;     // what ExtraEnemiesPerRoom reports once the first latch is on

        public static StirTuning Default => new StirTuning
        {
            PerFloorCleared = 10f, PerUnbankedPiece = 5f, PerExtraPlayer = 0.25f, Max = 100f,
            ExtraEnemyThreshold = 40f, HunterThreshold = 70f, DeepClosesThreshold = 100f, DeepClosesSeconds = 90f,
            ExtraEnemiesPerRoom = 1,
        };
    }
}
