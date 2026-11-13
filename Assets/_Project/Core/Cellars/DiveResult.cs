// Core/Cellars/DiveResult.cs: what one player's dive brought home (PL-2 task 2.3). Pure C#, no engine, no network.
// Authority: none (a pure function). The ending is the host's (DiveDirector.NetEnd); the numbers are each owner's own (a peer reads its owner-only BagView and the party's found pieces) and are
// built into a result on that peer, so one player never sees another's bag. Rules mirror PlayerBag: GoHome / a finished dive bring everything home and keep all dive gold; Fail / an abandoned dive
// keep banked items, leave the carried ones in one lost satchel, lose a carried satchel and keep half the dive gold (DiveGold.KeptOnFail). Pieces and people are safe on pickup either way.
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    // Same values as Presentation's DiveEndReason (Failed, WentHome, Abandoned, Finished), so the replicated byte maps one to one.
    public enum DiveEnding : byte { Failed = 0, WentHome = 1, Abandoned = 2, Finished = 3 }

    public struct DiveResultInput
    {
        public DiveEnding Ending;
        public int ClearedMask;        // floors cleared this dive (bit floor-1), as DiveHaulSync.Summary.ClearedMask
        public int LastFloor;          // the floor the dive ended on
        public int PiecesFound;        // pieces found this dive
        public IReadOnlyList<string> PeopleFound;   // residents found this dive (null = none)
        public int Carried;            // items carried when the dive ended (not banked)
        public bool SatchelCarried;    // a lost satchel was being carried
        public int Banked;             // items banked at the bank this dive
        public int DiveGold;           // gold earned this dive and not yet kept
    }

    public sealed class DiveResult
    {
        public DiveEnding Ending { get; private set; }
        public int FloorsCleared { get; private set; }
        public int LastFloor { get; private set; }
        public int PiecesFound { get; private set; }
        public IReadOnlyList<string> PeopleFound { get; private set; }
        public int ItemsHome { get; private set; }       // items now in the home stash
        public int ItemsBanked { get; private set; }     // of those, the ones banked at the bank
        public int ItemsLost { get; private set; }       // items left in the lost satchel
        public bool SatchelLost { get; private set; }    // a carried satchel was lost for good
        public int GoldKept { get; private set; }
        public int GoldLost { get; private set; }

        public bool Fell => Ending == DiveEnding.Failed || Ending == DiveEnding.Abandoned;

        public static int CountFloors(int mask) { int n = 0; for (uint m = (uint)mask; m != 0; m &= m - 1) n++; return n; }

        public static DiveResult Build(in DiveResultInput i)
        {
            int carried = System.Math.Max(0, i.Carried), banked = System.Math.Max(0, i.Banked), gold = System.Math.Max(0, i.DiveGold);
            bool fell = i.Ending == DiveEnding.Failed || i.Ending == DiveEnding.Abandoned;
            int kept = fell ? DiveGold.KeptOnFail(gold) : gold;
            return new DiveResult
            {
                Ending = i.Ending, FloorsCleared = CountFloors(i.ClearedMask), LastFloor = System.Math.Max(0, i.LastFloor), PiecesFound = System.Math.Max(0, i.PiecesFound),
                PeopleFound = i.PeopleFound == null ? new List<string>() : new List<string>(i.PeopleFound),
                ItemsBanked = banked, ItemsHome = fell ? banked : banked + carried, ItemsLost = fell ? carried : 0,
                SatchelLost = fell && i.SatchelCarried, GoldKept = kept, GoldLost = gold - kept,
            };
        }
    }
}
