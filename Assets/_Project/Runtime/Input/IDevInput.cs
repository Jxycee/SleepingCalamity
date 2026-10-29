// Runtime. Authority: local client only. Implemented by PlayerInputAdapter (reads Keyboard.current F-keys, see DevKey) and FakeInputService.
// Always false unless UnityEngine.Debug.isDebugBuild, the window has focus and the context is not Menu (this goes for WasPressed AND IsHeld,
// so losing focus or opening a menu reads as "released").
// Consumers resolve it null-safely: ServiceRoots.App.Get<IInputService>() as IDevInput.
namespace SleepingCalamity.Runtime
{
    public interface IDevInput
    {
        bool WasPressed(DevKey key);
        bool IsHeld(DevKey key);    // for keys that act while held
    }
}
