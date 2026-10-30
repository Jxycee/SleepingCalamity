// Authority: test only (local). Stands in for PlayerInputAdapter so PlayMode tests drive the player without devices.
// Tests set Frame; Intent is routed through the real Core InputContextRouter on every read.
// Also IDevInput: tests add a key to DevPressed for one frame and remove it themselves. The isDebugBuild gate is not applied
// (PlayMode in the editor is a debug build anyway).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Tests.PlayMode
{
    public sealed class FakeInputService : IInputService, IDevInput
    {
        private InputContext _gameplayContext = InputContext.Free;
        private bool _menuOpen;

        public PlayerInputFrame Frame;
        public readonly HashSet<DevKey> DevPressed = new HashSet<DevKey>();
        public readonly HashSet<DevKey> DevHeld = new HashSet<DevKey>();   // empty by default; same Menu gating as WasPressed

        public float LastRumbleLow { get; private set; }
        public float LastRumbleHigh { get; private set; }

        public InputContext Context => _menuOpen ? InputContext.Menu : _gameplayContext;

        public InputContext GameplayContext
        {
            get => _gameplayContext;
            set
            {
                InputContext before = Context;
                _gameplayContext = value;
                RaiseIfChanged(before);
            }
        }

        public bool MenuOpen
        {
            get => _menuOpen;
            set
            {
                InputContext before = Context;
                _menuOpen = value;
                RaiseIfChanged(before);
            }
        }

        public PlayerIntent Intent => InputContextRouter.Route(Context, in Frame);
        public bool HasFocus => true;
        public InputDeviceKind LastDevice => InputDeviceKind.KeyboardMouse;

        public event Action<InputContext> ContextChanged;

        public void SetRumble(float low, float high)
        {
            LastRumbleLow = low;
            LastRumbleHigh = high;
        }

        public string GetBindingDisplayString(string actionPath) => actionPath ?? "";

        public bool WasPressed(DevKey key) => Context != InputContext.Menu && DevPressed.Contains(key);

        public bool IsHeld(DevKey key) => Context != InputContext.Menu && DevHeld.Contains(key);

        private void RaiseIfChanged(InputContext before)
        {
            InputContext after = Context;
            if (after != before) ContextChanged?.Invoke(after);
        }
    }
}
