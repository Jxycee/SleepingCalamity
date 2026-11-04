// Runtime. Dev-only keys read through IDevInput (F-keys). Not part of the rebindable input asset. New dev actions append a value and a key in
// PlayerInputAdapter.TryDevKey (FishGame's fishing dev keys did not come across).
namespace SleepingCalamity.Runtime
{
    public enum DevKey : byte
    {
        ToggleOverlay = 0,          // F3
        SwapWeapon = 1,             // F5: cycle the four heirlooms (M3 arena)
        SkipWave = 2,               // F6: the host clears the arena wave (M3 arena)
        StartDive = 3,              // F7: the host starts a Cellars dive from anywhere (M5 spine)
        AbandonDive = 4,            // F8: the host abandons the running dive (M5 spine)
        EatLoaf = 5,                // H: eat one of the baker's loaves (M6 S5; a placeholder until the input asset gets an item action)
        CycleFloor = 6,             // G: the hub trapdoor prompt's next pickable floor while the checkpoint is held (plan 3 task 7; minimum UI)
        BankPrev = 7,               // [: the bank screen's cursor up (plan 3 task 8; minimum UI, a placeholder until the input asset gets bank actions)
        BankNext = 8,               // ]: the bank screen's cursor down
        BankPick = 9,               // P: pick / unpick the item under the cursor for a bank slot
        BankPushOn = 10,            // Y: press "push on"
        BankGoHome = 11             // N: press "go home"
    }
}
