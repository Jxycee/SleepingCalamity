// Core, no engine references, unit-tested. What the bank screen's buttons mean this frame, from the routed intent (InputContext.BankScreen) and the dev keys (Core cannot see DevKey, so the caller
// passes their presses in as bools: the dev keys stay as a second path, OR-ed in). Presentation owns the cursor and the requests; this only decides cursor step / pick / push on / go home.
// Authority: local peer only (it reads this peer's own input; the host re-checks every request).
namespace SleepingCalamity.Core
{
    public struct BankScreenDevPresses
    {
        public bool Prev, Next, Pick, PushOn, GoHome;
    }

    public struct BankScreenAction
    {
        public int CursorDelta;                 // -1 = one row up, +1 = one row down (the dev keys and the stick can sum to 0 or +-2)
        public bool Pick, PushOn, GoHome;
        public float MoveY;                     // this frame's stick value: the caller passes it back as prevMoveY next frame
    }

    public static class BankScreenInput
    {
        // The stick (or W/S) must cross this to step the cursor once; it steps again only after coming back below it (no auto-repeat while held).
        public const float StepThreshold = 0.6f;

        // armed = false (the screen only just opened, or the hero is down): nothing fires from the intent, but MoveY is still returned so a stick held on entry is not a fresh press.
        // The dev keys are not gated by `armed` (a development-build convenience, and the bot's path).
        public static BankScreenAction Read(float prevMoveY, in PlayerIntent intent, in BankScreenDevPresses dev, bool armed)
        {
            var a = new BankScreenAction { MoveY = intent.BankMoveY };
            if (armed)
            {
                float y = intent.BankMoveY;
                if (y >= StepThreshold && prevMoveY < StepThreshold) a.CursorDelta--;          // up: the previous row
                if (y <= -StepThreshold && prevMoveY > -StepThreshold) a.CursorDelta++;        // down: the next row
                a.Pick = intent.BankPickPressed;
                a.PushOn = intent.BankPushOnPressed;
                a.GoHome = intent.BankGoHomePressed;
            }
            if (dev.Prev) a.CursorDelta--;
            if (dev.Next) a.CursorDelta++;
            a.Pick |= dev.Pick;
            a.PushOn |= dev.PushOn;
            a.GoHome |= dev.GoHome;
            return a;
        }

        // Whether this peer's hero is close enough to the bank to hold the bank screen (the same flat test as the host's range rule, with the director's prompt margin as `radius`). A bank cell the
        // peer cannot trust yet (no built floor, or the built floor is not the dive floor) passes, exactly like the host, which then skips its range check too. Out of range the hero keeps walking
        // (nothing is frozen) and the screen is not armed, so a player away from the bank can never freeze, nor be asked to press, anything.
        public static bool InRange(bool bankKnown, float meX, float meZ, float bankX, float bankZ, float radius)
        {
            if (!bankKnown) return true;
            float dx = meX - bankX, dz = meZ - bankZ;
            return dx * dx + dz * dz <= radius * radius;
        }

        // The cursor after a step, wrapping both ways (rows <= 0 keeps it at 0).
        public static int Step(int cursor, int delta, int rows)
        {
            if (rows <= 0) return 0;
            int c = (cursor + delta) % rows;
            return c < 0 ? c + rows : c;
        }
    }
}
