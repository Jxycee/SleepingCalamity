// Core/Cellars/HealTuning.cs: every dive-healing number in ONE place (PHASE1 P47 "healing in the dive"; the 30 s recharge is a placeholder). Pure C#.
// CellarHealing and HealingPointState read only this struct; tests pass their own instance (e.g. a short recharge). Retune here, nowhere else.
// Heals are always FULL (to max HP): checkpoint and floor-4 banks heal everyone, a healing point heals whoever stands on it.
namespace SleepingCalamity.Core
{
    public struct HealTuning
    {
        public int ChancePercent;       // chance a floor 1-3 holds a healing point (DC2: 51%)
        public float RechargeSeconds;   // after a use, the point is spent this long
        public float RadiusMeters;      // how close a hero must stand to the point's cell centre (flat distance)

        public static HealTuning Default => new HealTuning { ChancePercent = 51, RechargeSeconds = 30f, RadiusMeters = 1.6f };
    }

    // The recharge clock of one healing point. Host-side state; peers only see the whole seconds left.
    public sealed class HealingPointState
    {
        readonly float _recharge;
        public float SecondsLeft { get; private set; }

        public HealingPointState(float rechargeSeconds) { _recharge = rechargeSeconds < 0f ? 0f : rechargeSeconds; }

        public bool Ready => SecondsLeft <= 0f;

        public void Tick(float dt) { if (SecondsLeft > 0f) { SecondsLeft -= dt; if (SecondsLeft < 0f) SecondsLeft = 0f; } }

        // Spends the point. False (nothing changes) while it is still recharging.
        public bool TryUse()
        {
            if (!Ready) return false;
            SecondsLeft = _recharge;
            return true;
        }
    }
}
