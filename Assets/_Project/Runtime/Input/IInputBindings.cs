// Runtime. Authority: local client only. Rebinding seam used by the settings UI.
using UnityEngine.InputSystem;

namespace SleepingCalamity.Runtime
{
    public interface IInputBindings
    {
        InputActionAsset Actions { get; }           // the runtime copy used by the adapter
        void SaveBindingOverrides();                // SaveBindingOverridesAsJson -> settings.bindingOverridesJson; settings.Save()
        void ResetAllBindingOverrides();            // RemoveAllBindingOverrides(); SaveBindingOverrides()
    }
}
