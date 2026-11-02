// Pure rules for walking the Cellars dive (docs/PHASE1.md 7.2 floor loop): where a dive starts, which floor the stair leads to, who may go home.
// No engine types; the host-side DiveDirector (Presentation) applies them. Floors 1-3 have a stair down; floor 4 (the heart) has the baker's rescue bank and the way home.
namespace SleepingCalamity.Core
{
    public static class DiveRoute
    {
        public const int FirstFloor = 1, ReturnFloor = 3, LastFloor = 4;

        // A fresh dive starts on floor 1; once the floor-2 checkpoint was reached in this session a return dive may start on floor 3.
        public static int StartFloor(bool checkpointReached) => checkpointReached ? ReturnFloor : FirstFloor;

        // Nothing is missable (PHASE1 7.2): the return dive skips floors 1-2 only once the checkpoint was reached and every piece on them is collected; otherwise the dive starts on floor 1.
        public static int StartFloor(bool checkpointReached, int piecesLeftOnFloors1And2) => StartFloor(checkpointReached && piecesLeftOnFloors1And2 <= 0);

        // The floor below `floor`, or 0 when there is none (the heart, the town, a bad id).
        public static int NextFloor(int floor) => floor >= FirstFloor && floor < LastFloor ? floor + 1 : 0;

        public static bool HasStair(int floor) => NextFloor(floor) != 0;

        // The party may leave from the heart only after banking there.
        public static bool CanGoHome(int floor, bool heartBanked) => floor == LastFloor && heartBanked;
    }
}
