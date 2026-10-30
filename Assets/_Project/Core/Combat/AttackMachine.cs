// Core/Combat/AttackMachine.cs: the owner's attack state machine (docs/PHASE1.md 6.1, 6.5). Pure C#, no engine, ticked by the Gameplay controller.
// Reads one weapon's AttackSet: input buffering, the combo and finisher windows, the 0.6 s charge, cancels, hitstop on its own clock, the combo count.
// Authority: owner only. The host never runs it (it judges hit intents from the data, not from this machine).
//
// Reading of the rules:
//  - A press starts hit 1 at once (no latency). A press during an attack is buffered for InputBufferSeconds; the next hit starts as soon as the
//    buffered press and an open window (ComboOpen, or FinisherAllowed when the next hit is the finisher) agree and the current attack is past its
//    CancelFrom frame. A press after the window closed starts nothing until the attack ends, then a fresh chain.
//  - Charge: the button held for ChargeSeconds. The charge attack starts on release (from idle, or cancelling a recovery past CancelFrom). The
//    press that began the hold has already started hit 1, so "tap, keep holding, release" is a charge after one swing.
//  - Hitstop freezes this machine's own frame clock (Freeze) for a number of frames; the host and everyone else are unaffected.
using System;

namespace SleepingCalamity.Core.Combat
{
    public struct AttackInput
    {
        public bool Pressed, Held, Released;
        public bool Airborne;
    }

    public sealed class AttackMachine
    {
        public const float InputBufferSeconds = 0.25f;
        public const float ChargeSeconds = 0.6f;
        public const float ChainGraceSeconds = 1.2f;    // the combo counter survives this long after the last swing ends

        AttackSet _set;
        float _frame;
        float _frozenFrames;
        float _buffered;          // seconds a press stays valid
        float _heldFor;
        bool _chargeQueued;
        int _index = -1;
        float _sinceEnd = 99f;
        int _hitsThisChain, _chainBase;

        public AttackMachine(AttackSet set) { _set = set ?? throw new ArgumentNullException(nameof(set)); }

        public AttackSet Set => _set;
        public AttackData Current => _index >= 0 ? _set.Attacks[_index] : null;
        public int CurrentIndex => _index;
        public bool Active => _index >= 0;
        public float Frame => _frame;
        public int FrameInt => (int)_frame;
        public int SwingId { get; private set; }
        public bool StartedThisTick { get; private set; }
        public bool ChargeReady => _heldFor >= ChargeSeconds;
        public float HeldSeconds => _heldFor;
        public bool Frozen => _frozenFrames > 0f;

        // Connected hits in the current chain (the HUD's combo count); drops to 0 ChainGraceSeconds after the last swing ends.
        public int ComboCount => _hitsThisChain;

        public bool HitboxOn => Active && Current.HitOn.Contains(_frame);
        public bool Invulnerable => Active && Current.IFrames.Contains(_frame);
        public bool SuperArmor => Active && Current.SuperArmor.Contains(_frame);
        public bool Turning => Active && Current.Turn.Contains(_frame);
        public float TurnDegPerSec => Turning ? Current.TurnDegPerSec : 0f;
        public bool CanCancel => !Active || _frame >= Current.CancelFrom;
        // Free to move on its own: no attack, or one past its cancel frame (the controller lets the stick walk off the recovery).
        public bool Busy => Active && _frame < Current.CancelFrom;

        // Metres per second the attacker slides this frame (0 outside the lunge window).
        public float LungeSpeed
        {
            get
            {
                if (!Active || Current.LungeMeters <= 0f || !Current.Lunge.Contains(_frame)) return 0f;
                int frames = Current.Lunge.End - Current.Lunge.Start;
                return Current.LungeMeters / (frames / (float)AttackData.FrameRate);
            }
        }

        // The finisher's prior-hit count for the damage formula: hits connected earlier in this chain (the swing's own hits are not counted).
        public int PriorHits { get; private set; }

        public void SetAttackSet(AttackSet set)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (ReferenceEquals(set, _set)) return;
            _set = set;
            Interrupt();
            _hitsThisChain = 0;
        }

        // A hit of the current swing connected (called once per target hit, by the owner when it sends the intent).
        public void RegisterHit() { _hitsThisChain++; }

        // Hitstop: stop this machine's clock for `frames` frames at 60 Hz.
        public void Freeze(int frames) { if (frames > 0) _frozenFrames += frames; }

        // A dodge, a stagger or a weapon swap ends the swing. The combo count is kept (it times out on its own).
        public void Interrupt()
        {
            _index = -1;
            _frame = 0f;
            _chargeQueued = false;
            _frozenFrames = 0f;
            _sinceEnd = 0f;
        }

        // Starts the weapon's special (shot or spell) if the machine is free to. False when it is busy or the weapon has none.
        public bool TryStartSpecial()
        {
            if (_set.Attacks[AttackSet.Special] == null || !CanCancel) return false;
            Begin(AttackSet.Special);
            return true;
        }

        // Advances by dt seconds of real time. Returns true when a new attack began this tick (read CurrentIndex / SwingId).
        public bool Tick(float dt, in AttackInput input)
        {
            StartedThisTick = false;
            if (dt < 0f) dt = 0f;

            if (input.Pressed)
            {
                // A press during a swing waits for the cancel frame as well (a heavy axe's cancel frame is later than 0.25 s), then the usual buffer.
                float untilCancel = Active ? Math.Max(0f, (Current.CancelFrom - _frame) / AttackData.FrameRate) : 0f;
                _buffered = InputBufferSeconds + untilCancel;
            }
            else if (_buffered > 0f) _buffered = Math.Max(0f, _buffered - dt);

            if (input.Held) _heldFor += dt;
            if (input.Released)
            {
                if (_heldFor >= ChargeSeconds) _chargeQueued = true;
                _heldFor = 0f;
            }
            else if (!input.Held) _heldFor = 0f;

            if (!Active)
            {
                _sinceEnd += dt;
                if (_sinceEnd > ChainGraceSeconds) { _hitsThisChain = 0; _chainBase = 0; PriorHits = 0; }
                if (_chargeQueued) { _chargeQueued = false; Begin(AttackSet.Charge); }
                else if (_buffered > 0f) { Begin(input.Airborne ? AttackSet.AirHit1 : AttackSet.Hit1); }
                return StartedThisTick;
            }

            // Hitstop eats the frame clock, not the input clock: presses made during it are buffered as usual.
            float step = dt * AttackData.FrameRate;
            if (_frozenFrames > 0f)
            {
                float eat = Math.Min(_frozenFrames, step);
                _frozenFrames -= eat;
                step -= eat;
            }
            _frame += step;

            AttackData cur = Current;
            if (_frame >= cur.CancelFrom)
            {
                if (_chargeQueued) { _chargeQueued = false; Begin(AttackSet.Charge); return true; }
                int next = AttackSet.NextInChain(_index);
                if (next >= 0 && _buffered > 0f)
                {
                    AttackData n = _set.Attacks[next];
                    FrameWindow window = n.IsFinisher ? cur.FinisherAllowed : cur.ComboOpen;
                    if (window.Contains(_frame)) { Begin(next); return true; }
                }
            }
            if (_frame >= cur.TotalFrames)
            {
                _index = -1;
                _frame = 0f;
                _sinceEnd = 0f;
                _chargeQueued = false;
            }
            return StartedThisTick;
        }

        void Begin(int index)
        {
            if (AttackSet.IsChainStart(index)) _chainBase = _hitsThisChain;
            PriorHits = _hitsThisChain - _chainBase;
            _index = index;
            _frame = 0f;
            _frozenFrames = 0f;
            _buffered = 0f;
            _chargeQueued = false;
            SwingId++;
            StartedThisTick = true;
        }
    }
}
