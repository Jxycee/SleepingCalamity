// Not networked; runs locally on every peer. A minimal IMGUI panel on the App object so the foundation can be driven without UI Toolkit:
// Solo, dev-direct host and join (debug builds), online host and join by code (Relay), Start (the host's start gate, SceneFlow.TryStartSession),
// Leave, and the last toast. It is the manual-walk surface for M1; the real menus replace it later (UI assembly).
using System;
using System.Threading;
using System.Threading.Tasks;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.App
{
    public sealed class DevSessionPanel : MonoBehaviour
    {
        private const float ToastSeconds = 6f;

        private string joinCode = "";
        private string directAddress = "127.0.0.1";
        private string status = "";
        private string toast = "";
        private float toastUntil;
        private IDisposable toastSubscription;
        private INetworkSession session;
        private AppBootstrap app;

        private void OnEnable()
        {
            app = GetComponent<AppBootstrap>();
        }

        private void OnDisable()
        {
            toastSubscription?.Dispose();
            toastSubscription = null;
        }

        private bool EnsureServices()
        {
            if (app == null || app.Services == null) return false;
            if (session == null) session = app.Services.Get<INetworkSession>();
            if (toastSubscription == null)
                toastSubscription = app.Services.Get<EventBus>().Subscribe<ToastRequested>(t =>
                {
                    toast = t.Key + (string.IsNullOrEmpty(t.Arg) ? "" : " (" + t.Arg + ")");
                    toastUntil = Time.unscaledTime + ToastSeconds;
                });
            return true;
        }

        private void OnGUI()
        {
            if (!EnsureServices()) return;
            GUILayout.BeginArea(new Rect(12, 12, 360, 520), GUI.skin.box);
            GUILayout.Label("Sleeping Calamity: session (dev panel)");
            GUILayout.Label("State: " + session.State + "   Mode: " + session.Mode + (session.IsHost ? "   (host)" : ""));
            if (!string.IsNullOrEmpty(session.JoinCode)) GUILayout.Label("Join code: " + session.JoinCode);

            if (session.State == ConnState.Offline)
            {
                if (GUILayout.Button("Solo")) Run("Solo", () => session.StartLocalAsync(false, CancellationToken.None));
                if (GUILayout.Button("Host online (Relay)")) Run("Host online", () => session.HostOnlineAsync(CancellationToken.None));
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, 12, GUILayout.Width(140));
                if (GUILayout.Button("Join by code")) Run("Join online", () => session.JoinOnlineAsync(joinCode, CancellationToken.None));
                GUILayout.EndHorizontal();
                if (Debug.isDebugBuild)
                {
                    GUILayout.Label("Dev-direct (loopback or LAN, no UGS)");
                    if (GUILayout.Button("Host direct")) Run("Host direct", () => session.StartLocalAsync(true, CancellationToken.None));
                    GUILayout.BeginHorizontal();
                    directAddress = GUILayout.TextField(directAddress, 40, GUILayout.Width(180));
                    if (GUILayout.Button("Join direct")) Run("Join direct", () => session.JoinDirectAsync(directAddress, NetworkConstants.DefaultPort, CancellationToken.None));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                SceneFlow flow = app.SceneFlow;
                if (session.State == ConnState.Connected && session.IsHost && flow != null && app.World != null && !app.World.Started)
                {
                    GUILayout.Label("Connected players: " + app.Roster.ConnectedCount + "   (a multiplayer world needs 2 or more to start)");
                    if (GUILayout.Button("Start session")) flow.TryStartSession();
                    if (flow.LastStartVerdict != WorldPlayerVerdict.Ok) GUILayout.Label("Cannot start: " + flow.LastStartVerdict);
                }
                if (GUILayout.Button("Leave")) Run("Leave", () => session.LeaveAsync());
            }

            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            if (Time.unscaledTime < toastUntil) GUILayout.Label("Message: " + toast);
            GUILayout.EndArea();
        }

        // Start tasks fault with NetworkSessionException; the toasts come from Ended, so this only keeps a status line.
        private async void Run(string label, Func<Task> start)
        {
            status = label + "...";
            try
            {
                await start();
                status = "";
            }
            catch (Exception e)
            {
                status = label + " failed: " + e.GetBaseException().Message;
            }
        }
    }
}
