// Not networked; runs locally on every peer. A minimal IMGUI panel on the App object so the foundation can be driven without UI Toolkit:
// the world picker (two tabs, Solo and Multiplayer, each up to 10 worlds; New world and Delete), Solo, dev-direct host and join (debug builds),
// online host and join by code (Relay), Start (the host's start gate, SceneFlow.TryStartSession), Leave, and the last toast.
// It is the manual-walk surface for M1; the real menus replace it later (UI assembly). The picker only chooses which world the NEXT hosted
// session opens (WorldSaveFlow.Select); joining someone else's session needs no world.
using System;
using System.Collections.Generic;
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

        // The world picker: which tab, which identity kind for the Multiplayer tab, and the listing (refreshed on demand, not every frame).
        private WorldKind tab = WorldKind.Solo;
        private bool onlineWorlds;
        private List<WorldSummary> listed = new List<WorldSummary>();
        private bool listDirty = true;

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

        private IdentityKind PickerIdentity => tab == WorldKind.Solo || !onlineWorlds ? IdentityKind.Local : IdentityKind.Ugs;

        private void OnGUI()
        {
            if (!EnsureServices()) return;
            GUILayout.BeginArea(new Rect(12, 12, 460, 720), GUI.skin.box);
            GUILayout.Label("Sleeping Calamity: session (dev panel)");
            GUILayout.Label("State: " + session.State + "   Mode: " + session.Mode + (session.IsHost ? "   (host)" : ""));
            if (!string.IsNullOrEmpty(session.JoinCode)) GUILayout.Label("Join code: " + session.JoinCode);

            if (session.State == ConnState.Offline)
            {
                DrawWorldPicker();
                GUILayout.Space(6);
                if (GUILayout.Button("Solo (opens the selected solo world)")) Run("Solo", () => session.StartLocalAsync(false, CancellationToken.None));
                if (GUILayout.Button("Host online (Relay; selected online multiplayer world)")) Run("Host online", () => session.HostOnlineAsync(CancellationToken.None));
                GUILayout.BeginHorizontal();
                joinCode = GUILayout.TextField(joinCode, 12, GUILayout.Width(140));
                if (GUILayout.Button("Join by code")) Run("Join online", () => session.JoinOnlineAsync(joinCode, CancellationToken.None));
                GUILayout.EndHorizontal();
                if (Debug.isDebugBuild)
                {
                    GUILayout.Label("Dev-direct (loopback or LAN, no UGS)");
                    if (GUILayout.Button("Host direct (selected local multiplayer world)")) Run("Host direct", () => session.StartLocalAsync(true, CancellationToken.None));
                    GUILayout.BeginHorizontal();
                    directAddress = GUILayout.TextField(directAddress, 40, GUILayout.Width(180));
                    if (GUILayout.Button("Join direct")) Run("Join direct", () => session.JoinDirectAsync(directAddress, NetworkConstants.DefaultPort, CancellationToken.None));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                listDirty = true;
                SceneFlow flow = app.SceneFlow;
                if (session.State == ConnState.Connected && session.IsHost && flow != null && app.World != null && !app.World.Started)
                {
                    GUILayout.Label("Connected players: " + app.Roster.ConnectedCount + "   (a multiplayer world needs 2 or more to start)");
                    if (GUILayout.Button("Start session")) flow.TryStartSession();
                    if (flow.LastStartVerdict != WorldPlayerVerdict.Ok) GUILayout.Label("Cannot start: " + WorldStartText.ToastKey(flow.LastStartVerdict) + " (" + flow.LastStartVerdict + ")");
                }
                if (GUILayout.Button("Leave")) Run("Leave", () => session.LeaveAsync());
            }

            if (!string.IsNullOrEmpty(status)) GUILayout.Label(status);
            if (Time.unscaledTime < toastUntil) GUILayout.Label("Message: " + toast);
            GUILayout.EndArea();
        }

        // Two tabs (Solo worlds, Multiplayer worlds); on the Multiplayer tab a switch between local (direct) and online worlds. Each list holds up
        // to WorldSummary.MaxPerList worlds; New world is off at the cap. Select picks the world the next hosted session opens; Delete sets it aside.
        private void DrawWorldPicker()
        {
            WorldSaveFlow flow = app.WorldSaveFlow;
            if (flow == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tab == WorldKind.Solo, "Solo worlds", GUI.skin.button) && tab != WorldKind.Solo) { tab = WorldKind.Solo; listDirty = true; }
            if (GUILayout.Toggle(tab == WorldKind.Multiplayer, "Multiplayer worlds", GUI.skin.button) && tab != WorldKind.Multiplayer) { tab = WorldKind.Multiplayer; listDirty = true; }
            GUILayout.EndHorizontal();
            if (tab == WorldKind.Multiplayer)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(!onlineWorlds, "Direct (local)", GUI.skin.button) && onlineWorlds) { onlineWorlds = false; listDirty = true; }
                if (GUILayout.Toggle(onlineWorlds, "Online", GUI.skin.button) && !onlineWorlds) { onlineWorlds = true; listDirty = true; }
                GUILayout.EndHorizontal();
            }

            if (listDirty)
            {
                listed = new List<WorldSummary>(flow.ListWorlds(tab, PickerIdentity));
                listDirty = false;
            }

            if (listed.Count == 0) GUILayout.Label("No worlds yet: the next session makes one, or press New world.");
            for (int i = 0; i < listed.Count; i++)
            {
                WorldSummary w = listed[i];
                bool selected = w.Id == flow.SelectedWorldId;
                GUILayout.BeginHorizontal();
                string label = (selected ? "> " : "  ") + "World " + (i + 1) + "  [" + string.Join(", ", w.PlayerNames) + "]  " + ShortTime(w.LastPlayedUtc);
                if (GUILayout.Button(label, GUILayout.Width(340))) flow.Select(w.Id);
                if (GUILayout.Button("Delete", GUILayout.Width(70)))
                {
                    status = flow.DeleteWorld(w.Id) ? "World set aside." : "Could not delete that world.";
                    listDirty = true;
                }
                GUILayout.EndHorizontal();
            }

            bool room = listed.Count < WorldSummary.MaxPerList;
            GUI.enabled = room;
            if (GUILayout.Button("New world (" + listed.Count + "/" + WorldSummary.MaxPerList + ")"))
            {
                status = flow.CreateWorld(tab, PickerIdentity) != null ? "World created and selected." : "That list is full.";
                listDirty = true;
            }
            GUI.enabled = true;
        }

        private static string ShortTime(string iso) => string.IsNullOrEmpty(iso) ? "" : (iso.Length >= 16 ? iso.Substring(0, 16).Replace('T', ' ') : iso);

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
