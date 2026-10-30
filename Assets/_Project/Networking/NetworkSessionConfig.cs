using System;
using SleepingCalamity.Runtime;
using Unity.Netcode;

namespace SleepingCalamity.Networking
{
    // Authority: none (immutable configuration). Built once in AppBootstrap and shared by the local and online sessions.
    // ProtocolVersion is BuildInfo.EffectiveProtocolVersion(dev offset), computed by AppBootstrap; this type never reads argv.
    // Roster and World are the host's join state (HostRoster, HostWorld): ConnectionApprover reads and writes them on the host only.
    // Both are optional so a client-only config (and the approver unit tests) can leave them out; without a World no world rules apply.
    public sealed class NetworkSessionConfig
    {
        public NetworkSessionConfig(NetworkManager networkManager, JoinApprovalBridge bridge, ISettings settings,
                                    int protocolVersion, ulong contentHash, bool longTimeout,
                                    HostRoster roster = null, HostWorld world = null)
        {
            NetworkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ProtocolVersion = protocolVersion;
            ContentHash = contentHash;
            LongTimeout = longTimeout;
            Roster = roster;
            World = world;
        }

        public NetworkManager NetworkManager { get; }
        public JoinApprovalBridge Bridge { get; }
        public ISettings Settings { get; }
        public int ProtocolVersion { get; }
        public ulong ContentHash { get; }
        public bool LongTimeout { get; }
        public HostRoster Roster { get; }
        public HostWorld World { get; }
    }
}
