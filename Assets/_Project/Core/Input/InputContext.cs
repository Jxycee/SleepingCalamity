// Core, no engine references. Effective input context. Set by gameplay code; Menu wins via IInputService.MenuOpen.
// Ported from FishGame with the fishing, swimming and climbing contexts removed; M2/M3 add rows (lock-on, guard, dodge) as the combat input needs them.
namespace SleepingCalamity.Core
{
    public enum InputContext : byte
    {
        Free = 0,
        Menu = 1,
        LockOn = 2,  // M2: hard lock active. Look is zeroed (the camera follows the target) and the right stick / switch keys feed SwitchX/Y instead.
    Placement = 3 // M6 S4: town placement mode. Move/look/jump/dodge/lock are zeroed (the hero stands still, the placement camera is fixed); the switch input, primary (place), secondary (pick up), interact (next lot), jump (turn) and pause (leave) pass.
    }
}
