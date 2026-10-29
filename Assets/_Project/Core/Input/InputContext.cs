// Core, no engine references. Effective input context. Set by gameplay code; Menu wins via IInputService.MenuOpen.
// Ported from FishGame with the fishing, swimming and climbing contexts removed; M2/M3 add rows (lock-on, guard, dodge) as the combat input needs them.
namespace SleepingCalamity.Core
{
    public enum InputContext : byte
    {
        Free = 0,
        Menu = 1
    }
}
