// Core/Cellars/DiveCheckpoint.cs: the halfway bank as a one-time checkpoint with a floor pick (spec decisions 13, 31, 38). Pure C#.
// Going home from the bank leaves it; the next dive may start on floors 1 to the bank and spends it whatever floor it picks. After the boss the bank
// is always open (the tithe-gate shortcut) and starting there spends nothing. The world save stores Held (plan 3).
namespace SleepingCalamity.Core
{
    public sealed class DiveCheckpoint
    {
        public bool Held { get; private set; }

        public void OnWentHomeFromBank() => Held = true;

        public void Restore(bool held) => Held = held;

        public bool CanStartOn(int floor, bool bossBeaten)
        {
            if (floor == UndercroftPlan.FirstFloor) return true;
            if (bossBeaten && floor == UndercroftPlan.BankFloor) return true;
            return Held && floor > UndercroftPlan.FirstFloor && floor <= UndercroftPlan.BankFloor;
        }

        // Starts a dive on `floor`. False (nothing changes) when that floor isn't allowed.
        public bool TryStart(int floor, bool bossBeaten)
        {
            if (!CanStartOn(floor, bossBeaten)) return false;
            bool shortcut = bossBeaten && floor == UndercroftPlan.BankFloor;
            if (!shortcut) Held = false;
            return true;
        }
    }
}
