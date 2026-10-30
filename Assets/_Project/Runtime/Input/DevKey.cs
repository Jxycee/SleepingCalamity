// Runtime. Dev-only keys read through IDevInput (F-keys). Not part of the rebindable input asset. New dev actions append a value and a key in
// PlayerInputAdapter.TryDevKey (FishGame's fishing dev keys did not come across).
namespace SleepingCalamity.Runtime
{
    public enum DevKey : byte
    {
        ToggleOverlay = 0,          // F3
        SwapWeapon = 1,             // F5: cycle the four heirlooms (M3 arena)
        SkipWave = 2                // F6: the host clears the arena wave (M3 arena)
    }
}
