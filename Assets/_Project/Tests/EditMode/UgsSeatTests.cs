// Authority: none (EditMode test of the host's Lobby-seat handling). Drives UgsNetworkSession.OnConnectionEvent (private, NGO callback) by
// reflection with a recording stand-in for the Sessions SDK's IHostSession, so no UGS service is touched.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Networking;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class UgsSeatTests
    {
        // Stands in for IHostSession through DispatchProxy (the interface has ~40 members; only RemovePlayerAsync matters). Must be public.
        public class SeatRecorder : DispatchProxy
        {
            public readonly List<string> Removed = new List<string>();

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod.Name == "RemovePlayerAsync") Removed.Add((string)args[0]);
                if (targetMethod.ReturnType == typeof(Task)) return Task.CompletedTask;
                return targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
            }
        }

        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

        // Catches (audit A1): a stale-connection takeover freeing the Lobby seat of the player who is rejoining right now. The approver kicks
        // the old client id of key P while P's new client is approved; NGO then raises ClientDisconnected(old), and the handler removes P's
        // seat by the cached player id.
        // NOT covered: the real NGO kick and timing, and whether the Lobby lets P join twice (needs real Relay: USER_TODO 2-player walk).
        [Test]
        public void Takeover_OldClientDisconnect_DoesNotFreeSeatOfSameKey()
        {
            var go = new GameObject("UgsSeatTest");
            try
            {
                NetworkManager nm = go.AddComponent<NetworkManager>();
                if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
                var bridge = new JoinApprovalBridge();
                var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "sc_ugsseat_test_settings.json")); // never loaded or saved
                var config = new NetworkSessionConfig(nm, bridge, settings, 1, 77UL, false, new HostRoster(), new HostWorld());
                var session = new UgsNetworkSession(config);
                Type t = typeof(UgsNetworkSession);

                FieldInfo hostField = t.GetField("_host", Inst);
                var create = typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(hostField.FieldType, typeof(SeatRecorder));
                object fake = create.Invoke(null, null);
                var recorder = (SeatRecorder)fake;

                t.GetField("_isHost", Inst).SetValue(session, true);
                hostField.SetValue(session, fake);
                var seats = (Dictionary<ulong, string>)t.GetField("_ugsIdByClient", Inst).GetValue(session);
                seats[1] = "P";                                                       // the stale client 1, cached when it was approved
                var key = new PlayerKey(IdentityKind.Ugs, "P");
                bridge.AddApproved(new ApprovedJoin(2, key, "P", 1, false));          // P's new client 2 is approved, not yet connected

                var data = new ConnectionEventData { EventType = ConnectionEvent.ClientDisconnected, ClientId = 1 };
                t.GetMethod("OnConnectionEvent", Inst).Invoke(session, new object[] { nm, data });

                CollectionAssert.DoesNotContain(recorder.Removed, "P", "the old client's disconnect freed the seat of the player who is rejoining");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // Catches: the A1 fix going too far - a player whose only connection drops keeping their Lobby seat, so their own rejoin is refused as
        // "full" (SeatRules must free the seat when no other connection of theirs is up or approved).
        // NOT covered: the real NGO and Sessions SDK (the host session is a DispatchProxy stand-in).
        [Test]
        public void TheLastConnectionOfAPlayer_Disconnects_FreesTheSeat()
        {
            var go = new GameObject("UgsSeatTest");
            try
            {
                NetworkManager nm = go.AddComponent<NetworkManager>();
                if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
                var bridge = new JoinApprovalBridge();
                var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "sc_ugsseat_test_settings.json")); // never loaded or saved
                var config = new NetworkSessionConfig(nm, bridge, settings, 1, 77UL, false, new HostRoster(), new HostWorld());
                var session = new UgsNetworkSession(config);
                Type t = typeof(UgsNetworkSession);

                FieldInfo hostField = t.GetField("_host", Inst);
                var create = typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(hostField.FieldType, typeof(SeatRecorder));
                object fake = create.Invoke(null, null);
                var recorder = (SeatRecorder)fake;

                t.GetField("_isHost", Inst).SetValue(session, true);
                hostField.SetValue(session, fake);
                var seats = (Dictionary<ulong, string>)t.GetField("_ugsIdByClient", Inst).GetValue(session);
                seats[1] = "P";
                seats[2] = "Q";                                                       // another player stays: their seat is not touched
                bridge.AddApproved(new ApprovedJoin(3, new PlayerKey(IdentityKind.Ugs, "R"), "R", 2, false));

                var data = new ConnectionEventData { EventType = ConnectionEvent.ClientDisconnected, ClientId = 1 };
                t.GetMethod("OnConnectionEvent", Inst).Invoke(session, new object[] { nm, data });

                CollectionAssert.Contains(recorder.Removed, "P", "the player who left keeps their seat, so their own rejoin would be refused as full");
                CollectionAssert.DoesNotContain(recorder.Removed, "Q");
                CollectionAssert.DoesNotContain(recorder.Removed, "R");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
