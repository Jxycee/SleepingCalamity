// Runtime. Dev-only keys read through IDevInput (F-keys). Not part of the rebindable input asset. New dev actions append a value and a key in
// PlayerInputAdapter.TryDevKey (FishGame's fishing dev keys did not come across).
namespace SleepingCalamity.Runtime
{
    public enum DevKey : byte
    {
        ToggleOverlay = 0           // F3
    }
}
