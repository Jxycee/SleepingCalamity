// Runtime. Authority: local client only. The one input seam gameplay and UI read. Implemented by PlayerInputAdapter and the tests' FakeInputService.
using System;
using SleepingCalamity.Core;

namespace SleepingCalamity.Runtime
{
    public interface IInputService
    {
        InputContext Context { get; }               // MenuOpen ? Menu : GameplayContext
        InputContext GameplayContext { get; set; }  // written by the player controller
        bool MenuOpen { get; set; }                 // written by the UI flow (any modal or main menu visible)
        PlayerIntent Intent { get; }                // routed, recomputed once per frame before gameplay Update
        bool HasFocus { get; }
        InputDeviceKind LastDevice { get; }
        event Action<InputContext> ContextChanged;
        void SetRumble(float low, float high);      // 0..1; no-op without a gamepad
        string GetBindingDisplayString(string actionPath); // e.g. "E" for InputActionNames.Interact on keyboard; "" if unknown
    }
}
