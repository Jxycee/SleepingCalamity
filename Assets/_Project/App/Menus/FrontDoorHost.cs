// Not networked; local on every peer. Adapts what the dev panel already reads on the host (SceneFlow's start gate, HostRoster's connected players) to
// the front door's IFrontDoorHost. It adds no rules: TryStartSession is SceneFlow.TryStartSession, and the host's own gate (HostWorld.TryStartSession)
// still decides. On a client both are empty (SceneFlow refuses outside the server; the roster is only filled on the host).
using System.Collections.Generic;
using SleepingCalamity.Core;
using SleepingCalamity.Runtime;

namespace SleepingCalamity.App
{
    public sealed class FrontDoorHost : IFrontDoorHost
    {
        private readonly SceneFlow flow;
        private readonly HostRoster roster;

        public FrontDoorHost(SceneFlow flow, HostRoster roster)
        {
            this.flow = flow;
            this.roster = roster;
        }

        public int ConnectedCount => roster.ConnectedCount;

        public void GetConnectedNames(List<string> into) => roster.GetConnectedNames(into);

        public bool TryStartSession() => flow.TryStartSession();

        public WorldPlayerVerdict LastStartVerdict => flow.LastStartVerdict;
    }
}
