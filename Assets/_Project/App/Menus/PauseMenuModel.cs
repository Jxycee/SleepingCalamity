// Not networked; the pause menu is local on every peer and only ASKS (PL-4 task 4.1). It never stops time (two players share the world). Leaving goes through
// INetworkSession.LeaveAsync, so a client leaving leaves the host playing, and a host leaving ends the session for everyone (the host's WorldSaveFlow saves when the
// session shuts down). The host is asked first. Plain C# so EditMode tests drive it with a fake session and a fake input service.
//
// States: Closed, Main (Resume / Controls / Leave to title / Quit), Controls, Confirm (the host's question). While it is open the input service's MenuOpen is up (Menu
// context: cursor free, gameplay input zeroed); the model lowers it again on every way out, and only if it raised it.
// Pause(context) is what the Pause button does, given the gameplay context of the frame BEFORE the press (the view passes it, so a Pause that just left placement mode
// cannot also open the menu): Free and LockOn open it; Placement leaves placement itself (PlacementMode), BankScreen does not open it; a downed hero is still Free.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.App
{
    public enum PauseMenuState { Closed, Main, Controls, Confirm }

    public enum PauseOutcome { Opened, Closed, Back, LeftToPlacement, Ignored }

    public sealed class PauseMenuModel : IDisposable
    {
        private enum Pending { None, Leave, Quit }

        private readonly INetworkSession session;
        private readonly IInputService input;
        private readonly Action quit;
        private readonly List<KeyValuePair<string, string>> controls = new List<KeyValuePair<string, string>>();
        private PauseMenuState state = PauseMenuState.Closed;
        private Pending pending = Pending.None;
        private bool raisedMenu;   // this model raised IInputService.MenuOpen and must lower it
        private bool disposed;

        // canSave: whether a host's leave or quit writes the world (WorldSaveFlow.CanSave); null means yes.
        private readonly Func<bool> canSave;

        public PauseMenuModel(INetworkSession session, IInputService input, Action quit, Func<bool> canSave = null)
        {
            this.canSave = canSave;
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.quit = quit ?? throw new ArgumentNullException(nameof(quit));
            session.Ended += HandleEnded;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            session.Ended -= HandleEnded;
            Close();
        }

        public event Action Changed;

        public PauseMenuState State => state;
        public bool IsOpen => state != PauseMenuState.Closed;

        // The host's question, or null. Host: always asked (the session ends and the world is saved); in a solo world the words say so.
        public string ConfirmWords
        {
            get
            {
                if (state != PauseMenuState.Confirm) return null;
                bool solo = session.Mode == SessionMode.Solo;
                bool saves = canSave == null || canSave();
                if (pending == Pending.Quit) return solo ? (saves ? PauseMenuText.SoloQuitAsk : PauseMenuText.SoloQuitAskUnsaved) : (saves ? PauseMenuText.HostQuitAsk : PauseMenuText.HostQuitAskUnsaved);
                return solo ? (saves ? PauseMenuText.SoloLeaveAsk : PauseMenuText.SoloLeaveAskUnsaved) : (saves ? PauseMenuText.HostLeaveAsk : PauseMenuText.HostLeaveAskUnsaved);
            }
        }

        public string ConfirmButtonWords => pending == Pending.Quit ? PauseMenuText.ConfirmQuitButton : PauseMenuText.ConfirmLeaveButton;

        // (action words, binding as the player sees it now), read from the action asset through the input service.
        public IReadOnlyList<KeyValuePair<string, string>> Controls
        {
            get
            {
                controls.Clear();
                foreach (string[] row in PauseMenuText.ControlRows)
                {
                    string binding;
                    try { binding = input.GetBindingDisplayString(row[1]); }
                    catch (Exception) { binding = null; }   // a display-string fault must not break the Controls screen
                    controls.Add(new KeyValuePair<string, string>(row[0], string.IsNullOrEmpty(binding) ? "-" : binding));
                }
                return controls;
            }
        }

        // What the Pause button does. gameplayContext is the gameplay context before this press.
        // menuWasOpenLastFrame: input.MenuOpen at the end of the frame before. A menu that closed on this very press (Escape closes the weapon tree) must not also open this one.
        public PauseOutcome Pause(InputContext gameplayContext, bool menuWasOpenLastFrame = false)
        {
            if (disposed) return PauseOutcome.Ignored;
            switch (state)
            {
                case PauseMenuState.Main:
                    Close();
                    return PauseOutcome.Closed;
                case PauseMenuState.Controls:
                case PauseMenuState.Confirm:
                    Back();
                    return PauseOutcome.Back;
            }

            if (gameplayContext == InputContext.Placement) return PauseOutcome.LeftToPlacement;   // PlacementMode leaves on this very press
            if (gameplayContext != InputContext.Free && gameplayContext != InputContext.LockOn) return PauseOutcome.Ignored;
            if (input.MenuOpen) return PauseOutcome.Ignored;   // another menu is up
            if (menuWasOpenLastFrame) return PauseOutcome.Ignored;   // another menu closed on this press
            if (session.State != ConnState.Connected) return PauseOutcome.Ignored;
            input.MenuOpen = true;
            raisedMenu = true;
            state = PauseMenuState.Main;
            Changed?.Invoke();
            return PauseOutcome.Opened;
        }

        public void Resume() { if (IsOpen) Close(); }

        public void ShowControls()
        {
            if (state != PauseMenuState.Main) return;
            state = PauseMenuState.Controls;
            Changed?.Invoke();
        }

        // Controls or the host's question: back to the main list.
        public void Back()
        {
            if (state != PauseMenuState.Controls && state != PauseMenuState.Confirm) return;
            pending = Pending.None;
            state = PauseMenuState.Main;
            Changed?.Invoke();
        }

        // The host is asked first; a client (or a solo player? no: solo is the host) leaves at once.
        public Task Leave()
        {
            if (state != PauseMenuState.Main) return Task.CompletedTask;
            if (session.IsHost) return Ask(Pending.Leave);
            return DoLeave();
        }

        public void Quit()
        {
            if (state != PauseMenuState.Main) return;
            if (session.IsHost) { Ask(Pending.Quit); return; }
            DoQuit();
        }

        // The host said yes.
        public Task Confirm()
        {
            if (state != PauseMenuState.Confirm) return Task.CompletedTask;
            Pending what = pending;
            if (what == Pending.Quit) { DoQuit(); return Task.CompletedTask; }
            return DoLeave();
        }

        private Task Ask(Pending what)
        {
            pending = what;
            state = PauseMenuState.Confirm;
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        // The menu lets go first (the Lobby raises its own Menu hold), then the session goes: LeaveAsync saves a host's world on its way down and returns every peer to the title.
        private async Task DoLeave()
        {
            Close();
            try
            {
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);   // the title is shown by SceneFlow when the session reports Offline; a fault here must not be silent
            }
        }

        private void DoQuit()
        {
            Close();
            quit();
        }

        private void HandleEnded(EndReason reason) => Close();

        private void Close()
        {
            bool was = state != PauseMenuState.Closed;
            state = PauseMenuState.Closed;
            pending = Pending.None;
            if (raisedMenu) input.MenuOpen = false;
            raisedMenu = false;
            if (was) Changed?.Invoke();
        }
    }
}
