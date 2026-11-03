// Assets/_Project/Core/Cellars/DiveMomentum.cs: why push on, and the paid boss retry (spec decisions 25, 32, 36, 37). Pure C#.
// Momentum: walk down to the bank and push on, and every second-half chest rolls one rank higher; an unbroken dive (from floor 1) also opens the
// extra chest room after the boss. BossRetry: one per dive at the boss only; the idol found this dive pays, gold is the fallback.
namespace SleepingCalamity.Core
{
    public sealed class DiveMomentum
    {
        private bool _walkedToBank;

        public bool Unbroken { get; private set; }
        public bool PushedOn { get; private set; }

        public void OnDiveStart(int startFloor)
        {
            Unbroken = startFloor == UndercroftPlan.FirstFloor;
            _walkedToBank = startFloor < UndercroftPlan.BankFloor;
            PushedOn = false;
        }

        // The party chose "push on" at the bank. A dive that started at the bank starts cold.
        public void OnPushOn() { if (_walkedToBank) PushedOn = true; }

        public int ChestRankBonus(int floor) => PushedOn && floor > UndercroftPlan.BankFloor ? 1 : 0;

        public bool EndChestRoomOpens(bool bossBeaten) => bossBeaten && Unbroken && PushedOn;
    }

    public enum RetryPrice { None, Idol, Gold }

    public sealed class BossRetry
    {
        public const int GoldPrice = 500;   // placeholder

        public bool UsedThisDive { get; private set; }
        public bool IdolActivated { get; private set; }

        public void OnDiveStart() { UsedThisDive = false; IdolActivated = false; }

        public void ActivateIdol() => IdolActivated = true;

        public RetryPrice PriceFor(int floor, int gold)
        {
            if (floor != UndercroftPlan.BossFloor || UsedThisDive) return RetryPrice.None;
            if (IdolActivated) return RetryPrice.Idol;
            return gold >= GoldPrice ? RetryPrice.Gold : RetryPrice.None;
        }

        // Pays and spends the dive's retry. False (nothing changes) when no retry is available.
        public bool TryRetry(int floor, ref int gold)
        {
            RetryPrice price = PriceFor(floor, gold);
            if (price == RetryPrice.None) return false;
            if (price == RetryPrice.Gold) gold -= GoldPrice;
            else IdolActivated = false;
            UsedThisDive = true;
            return true;
        }
    }
}
