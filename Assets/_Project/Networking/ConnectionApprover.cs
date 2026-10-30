using System;
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace SleepingCalamity.Networking
{
    // Authority: host. NGO calls Handle on the server only (each remote join, and the host's own client inside StartHost).
    // Every decision goes through Core JoinRules, then through the world-player rules (HostWorld.Admit -> Core WorldPlayers): a join to a
    // Solo world is refused (JoinsClosed), a key that is not recorded is refused once 4 players are recorded (FullLobby), and a new key is
    // recorded once its client connects. Approved joins are parked in the JoinApprovalBridge until their client connects; the approver's own NGO
    // connection events then move them into the HostRoster (Connect / Disconnect), which is also the bridge's IJoinContextProvider.
    // The "2 or more players to start" gate is not an approval rule: it is HostWorld.TryStartSession, called by SceneFlow.
    public sealed class ConnectionApprover
    {
        private readonly NetworkSessionConfig _config;
        private readonly SessionMode _mode;
        private readonly Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _callback;
        private readonly List<RosterEntry> _roster = new List<RosterEntry>();
        private readonly List<ApprovedJoin> _pending = new List<ApprovedJoin>();

        public ConnectionApprover(NetworkSessionConfig config, SessionMode mode)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mode = mode;
            _callback = Handle;
        }

        // Assignment, never +=. NGO throws on a multicast approval callback.
        // ConnectionApproval is part of NGO's config hash, so clients set it too.
        public void Install()
        {
            NetworkManager nm = _config.NetworkManager;
            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = _callback;
            if (_config.Roster != null)
            {
                _config.Bridge.Provider = _config.Roster;
                nm.OnConnectionEvent -= OnConnectionEvent;
                nm.OnConnectionEvent += OnConnectionEvent;
            }
        }

        // Clears the callback only while it is still ours, so a stale approver never removes a newer one.
        public void Uninstall()
        {
            NetworkManager nm = _config.NetworkManager;
            if (ReferenceEquals(nm, null))
            {
                return;
            }
            if (nm.ConnectionApprovalCallback == _callback)
            {
                nm.ConnectionApprovalCallback = null;
            }
            nm.OnConnectionEvent -= OnConnectionEvent;
            if (_config.Roster != null)
            {
                if (ReferenceEquals(_config.Bridge.Provider, _config.Roster)) _config.Bridge.Provider = null;
                _config.Roster.Clear();
            }
        }

        // Host only: an approved client that connected joins the roster; a client that left keeps its slot, marked disconnected.
        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (!nm.IsServer) return;
            if (data.EventType == ConnectionEvent.ClientConnected) ClientConnected(data.ClientId);
            else if (data.EventType == ConnectionEvent.ClientDisconnected) ClientDisconnected(data.ClientId);
        }

        // The two halves of OnConnectionEvent, callable from tests (a NetworkManager that is not running raises no events).
        internal void ClientConnected(ulong clientId)
        {
            HostRoster roster = _config.Roster;
            if (roster == null) return;
            if (_config.Bridge.TryTakeApproved(clientId, out ApprovedJoin join))
            {
                roster.Connect(join);
                _config.World?.RecordEntered(join.Key, clientId, join.Name);   // the client is in: now the world writes it down, with its name
            }
        }

        internal void ClientDisconnected(ulong clientId)
        {
            HostRoster roster = _config.Roster;
            if (roster == null) return;
            roster.Disconnect(clientId);
            _config.World?.CancelPending(clientId);   // approved but never connected: its place is free again (keyed by client id)
            _config.Bridge.RemovePending(clientId);
        }

        public void Handle(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            try
            {
                Evaluate(request, response);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Reject(response, EndReason.Unknown);
            }
        }

        private void Evaluate(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (!ConnectPayloadCodec.TryDecode(request.Payload, out ConnectPayload payload) || !payload.Key.IsValid)
            {
                Reject(response, EndReason.VersionMismatch);
                return;
            }

            ulong clientId = request.ClientNetworkId;
            JoinApprovalBridge bridge = _config.Bridge;
            // Only a client that would otherwise be approved may take a stale connection over (a mismatched build must not kick anyone).
            if (_mode != SessionMode.Solo && payload.ProtocolVersion == _config.ProtocolVersion && payload.ContentHash == _config.ContentHash)
                TakeOverStaleConnection(payload.Key, clientId);
            IJoinContextProvider provider = bridge.Provider;

            // Roster = provider slots (empty if none) + pending approvals counted as connected (D4).
            _roster.Clear();
            if (provider != null)
            {
                provider.GetRoster(_roster);
            }
            bridge.GetPending(_pending);
            for (int i = 0; i < _pending.Count; i++)
            {
                _roster.Add(new RosterEntry(_pending[i].Key, _pending[i].Slot, true));
            }
            bool joinsOpen = provider == null || provider.JoinsOpen;

            var joinRequest = new JoinRequest(
                clientId,
                clientId == NetworkManager.ServerClientId,
                payload.ProtocolVersion,
                payload.ContentHash,
                payload.Key,
                payload.Name);
            var context = new JoinContext(
                _mode,
                _config.ProtocolVersion,
                _config.ContentHash,
                joinsOpen,
                NetworkConstants.MaxPlayers,
                _roster);

            JoinVerdict verdict = JoinRules.Evaluate(joinRequest, context);
            if (!verdict.Approved)
            {
                Reject(response, verdict.Reason);
                return;
            }

            HostWorld world = _config.World;
            if (world != null)
            {
                WorldPlayerVerdict worldVerdict = world.Admit(payload.Key, clientId, joinRequest.IsServerSelf);
                if (worldVerdict != WorldPlayerVerdict.Ok)
                {
                    Reject(response, HostWorld.ReasonFor(worldVerdict));
                    return;
                }
            }

            string name = DisplayNameRules.Sanitize(payload.Name, verdict.Slot);
            bridge.AddApproved(new ApprovedJoin(clientId, payload.Key, name, verdict.Slot, verdict.Reclaimed));
            response.Approved = true;
            response.CreatePlayerObject = false;
            response.Pending = false;
        }

        // A key that connects again while an older connection of the same key is still up (a dropped link the host has not noticed yet, a
        // second launch of the same profile) takes that connection over instead of being refused as a duplicate: the old one is released
        // and disconnected. Never for the host's own key (a client with the host's key is a duplicate). Also covers an older approval of the
        // same key that has not connected yet.
        private void TakeOverStaleConnection(PlayerKey key, ulong newClientId)
        {
            HostRoster roster = _config.Roster;
            if (roster == null || newClientId == NetworkManager.ServerClientId) return;
            bool found = roster.TryFindConnected(key, out ulong stale) && stale != newClientId;
            if (!found)
            {
                _config.Bridge.GetPending(_pending);
                foreach (ApprovedJoin pending in _pending)
                {
                    if (pending.Key == key && pending.ClientId != newClientId) { stale = pending.ClientId; found = true; break; }
                }
            }
            if (!found || stale == NetworkManager.ServerClientId) return;

            roster.Disconnect(stale);
            _config.Bridge.RemovePending(stale);
            _config.World?.CancelPending(stale);
            NetworkManager nm = _config.NetworkManager;
            if (nm.IsServer) nm.DisconnectClient(stale, EndReason.Kicked.ToString());
        }

        // NGO sends Reason to the pending client before dropping it; the client parses it with DisconnectClassifier.
        private static void Reject(NetworkManager.ConnectionApprovalResponse response, EndReason reason)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = reason.ToString();
        }
    }
}
