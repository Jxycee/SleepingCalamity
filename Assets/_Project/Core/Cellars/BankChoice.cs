// Core/Cellars/BankChoice.cs: the shared push-on / go-home choice at the bank (plan 3 decision 11, a proposal needing the user's sign-off). Pure C#, no clock: the host advances it with Tick(dt).
// A living, present player's press opens a window of WindowSeconds; a second distinct player's press for the same choice, or the timer, confirms it; with one living player present the
// first press confirms at once. A press for the other choice swaps the window (the presser alone, the timer restarts). The same player pressing the same choice again does nothing.
// Downed or absent players are not counted: the host passes the living present players to Tick every frame, a presser who is no longer among them is dropped, a window with no presser left
// closes, and a window whose quorum fell to one living player confirms.
// Authority: host only (owned by DiveHaulSync).
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum BankChoiceKind { None = 0, PushOn = 1, GoHome = 2 }

    public enum BankChoicePress { Ignored, Opened, Swapped, Confirmed }

    public sealed class BankChoice
    {
        public const float WindowSeconds = 10f;   // placeholder: how long a press waits for a second player

        readonly List<string> _pressers = new List<string>();

        public BankChoiceKind Kind { get; private set; }
        public float SecondsLeft { get; private set; }
        public IReadOnlyList<string> Pressers => _pressers;
        public bool IsOpen => Kind != BankChoiceKind.None;

        // A press by `presser` (a bag id) for `choice`. livingPresent is the number of living present players (the quorum; at least 1 is assumed). Confirmed closes the window and the
        // confirmed choice is in ConfirmedKind (until the next Press or Tick).
        public BankChoicePress Press(string presser, BankChoiceKind choice, int livingPresent)
        {
            ConfirmedKind = BankChoiceKind.None;
            if (choice == BankChoiceKind.None || presser == null) return BankChoicePress.Ignored;
            if (livingPresent <= 1) return Confirm(choice);
            if (Kind == choice)
            {
                if (_pressers.Contains(presser)) return BankChoicePress.Ignored;
                return Confirm(choice);   // a second distinct presser confirms
            }
            bool swapped = Kind != BankChoiceKind.None;
            Kind = choice; SecondsLeft = WindowSeconds;
            _pressers.Clear(); _pressers.Add(presser);
            return swapped ? BankChoicePress.Swapped : BankChoicePress.Opened;
        }

        // The window closes with this choice confirmed; ConfirmedKind says which until the next call.
        public BankChoiceKind ConfirmedKind { get; private set; }

        BankChoicePress Confirm(BankChoiceKind choice)
        {
            ConfirmedKind = choice;
            Kind = BankChoiceKind.None; SecondsLeft = 0f; _pressers.Clear();
            return BankChoicePress.Confirmed;
        }

        // Advances the window by dt. livingPresent: the bag ids of the living present players. Returns the confirmed choice (None when nothing confirmed this tick).
        public BankChoiceKind Tick(float dt, IReadOnlyCollection<string> livingPresent)
        {
            ConfirmedKind = BankChoiceKind.None;
            if (Kind == BankChoiceKind.None) return BankChoiceKind.None;
            for (int i = _pressers.Count - 1; i >= 0; i--)
            {
                bool here = false;
                if (livingPresent != null) foreach (string id in livingPresent) if (id == _pressers[i]) { here = true; break; }
                if (!here) _pressers.RemoveAt(i);
            }
            if (_pressers.Count == 0) { Cancel(); return BankChoiceKind.None; }
            if (livingPresent != null && livingPresent.Count <= 1) { BankChoiceKind k = Kind; Confirm(k); return k; }
            SecondsLeft -= dt;
            if (SecondsLeft <= 0f) { BankChoiceKind k = Kind; Confirm(k); return k; }
            return BankChoiceKind.None;
        }

        public void Cancel() { Kind = BankChoiceKind.None; SecondsLeft = 0f; _pressers.Clear(); }
    }
}
