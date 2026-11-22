// Not networked; local on every peer. The plain C# end of ToastLine (PL-3 review fix): which toast words are on screen at a time. Add() turns a ToastRequested key into words with
// UiText.Toast and keeps it for DisplaySeconds (or the toast's own Seconds, if longer); Lines() lists what is still showing, newest first. Time is passed in, so EditMode tests need no clock.
// A key UiText does not know is dropped and reported once through onUnknownKey (the line must never print a raw key at a player).
using System;
using System.Collections.Generic;
using SleepingCalamity.Core;

namespace SleepingCalamity.App
{
    public sealed class ToastQueue
    {
        public const float DisplaySeconds = 6f;
        public const int MaxLines = 4;

        private struct Entry
        {
            public string Text;
            public double Until;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly HashSet<string> reportedUnknown = new HashSet<string>();
        private readonly Action<string> onUnknownKey;

        public ToastQueue(Action<string> onUnknownKey = null)
        {
            this.onUnknownKey = onUnknownKey;
        }

        public void Add(string key, string arg, float seconds, double now)
        {
            string text = key == null ? null : UiText.Toast(key, arg);
            if (text == null)
            {
                if (key != null && reportedUnknown.Add(key)) onUnknownKey?.Invoke(key);
                return;
            }
            // The same words again just renew the one line.
            entries.RemoveAll(e => e.Text == text);
            entries.Add(new Entry { Text = text, Until = now + Math.Max(DisplaySeconds, seconds) });
            if (entries.Count > MaxLines) entries.RemoveAt(0);
        }

        // The words still showing at `now`, newest first (cleared into `into`).
        public void Lines(double now, List<string> into)
        {
            entries.RemoveAll(e => e.Until <= now);
            into.Clear();
            for (int i = entries.Count - 1; i >= 0; i--) into.Add(entries[i].Text);
        }
    }
}
