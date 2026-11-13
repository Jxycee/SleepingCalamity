// Not networked; local on every peer. The always-on toast line (PL-3 review fix): save warnings ("save.world_read_only", "save.world_load_failed", "world.library_full") are published on the
// EventBus as ToastRequested, and in a release build nothing else listens (the dev panel is not drawn). This puts their words (UiText.Toast) on screen for a few seconds, newest on top,
// in every build and every scene (it lives on the App object). Keys the front door already says in its own screens (session end reasons "err.*" and the start refusals "world.need_two_players" ...)
// are not repeated here. The dev panel keeps its own raw line. Subscribes lazily because the App composes its services in its own Awake.
using System;
using System.Collections.Generic;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.App
{
    public sealed class ToastLine : MonoBehaviour
    {
        private readonly ToastQueue queue = new ToastQueue(key => Debug.LogWarning("ToastLine: no words for the toast key '" + key + "'; nothing is shown."));
        private readonly List<string> lines = new List<string>();
        private AppBootstrap app;
        private IDisposable subscription;
        private GUIStyle style;

        public ToastQueue Queue => queue;

        // The words showing now, newest first; tests read it.
        public IReadOnlyList<string> Current
        {
            get
            {
                queue.Lines(Time.unscaledTimeAsDouble, lines);
                return lines;
            }
        }

        // The door's own screens say these; the line does not echo them.
        public static bool ShownByTheFrontDoor(string key) =>
            key != null && (key.StartsWith("err.", StringComparison.Ordinal) || (key.StartsWith("world.", StringComparison.Ordinal) && key != "world.library_full"));

        private void OnEnable()
        {
            app = GetComponent<AppBootstrap>();
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void Update()
        {
            if (subscription != null || app == null || app.Services == null) return;
            if (!app.Services.TryGet(out EventBus bus)) return;
            subscription = bus.Subscribe<ToastRequested>(t =>
            {
                if (!ShownByTheFrontDoor(t.Key)) queue.Add(t.Key, t.Arg, t.Seconds, Time.unscaledTimeAsDouble);
            });
        }

        private void OnGUI()
        {
            IReadOnlyList<string> now = Current;
            if (now.Count == 0) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = 20 };
                style.normal.textColor = new Color(1f, 0.93f, 0.8f);
            }
            float width = Mathf.Min(760f, Screen.width - 40f);
            float y = Screen.height - 24f;
            for (int i = now.Count - 1; i >= 0; i--)   // oldest lowest, newest on top
            {
                float height = style.CalcHeight(new GUIContent(now[i]), width) + 8f;
                y -= height + 6f;
                GUI.Box(new Rect((Screen.width - width) / 2f, y, width, height), now[i], style);
            }
        }
    }
}
