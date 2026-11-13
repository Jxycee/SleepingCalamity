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
        private readonly HostWorld world;
        private readonly List<KeyValuePair<PlayerKey, string>> people = new List<KeyValuePair<PlayerKey, string>>();

        public FrontDoorHost(SceneFlow flow, HostRoster roster, HostWorld world = null)
        {
            this.flow = flow;
            this.roster = roster;
            this.world = world;
        }

        // Audit A3. Offered only while the hosted world is Multiplayer and its session has not started (HostWorld.RenameRecorded says the same).
        public void GetRenameChoices(List<FrontDoorPerson> newcomers, List<FrontDoorPerson> recordedAway)
        {
            newcomers.Clear();
            recordedAway.Clear();
            WorldPlayers players = world?.Players;
            if (players == null || players.Kind != WorldKind.Multiplayer || world.Started) return;
            roster.GetConnectedPeople(people);
            foreach (KeyValuePair<PlayerKey, string> person in people)
                if (!players.IsRecorded(person.Key) && players.CanJoin(person.Key) != WorldPlayerVerdict.WrongIdentityKind)
                    newcomers.Add(new FrontDoorPerson { Key = person.Key, Name = person.Value });
            if (newcomers.Count == 0) return;
            foreach (PlayerKey key in players.Recorded)
            {
                bool connected = false;
                foreach (KeyValuePair<PlayerKey, string> person in people) if (person.Key == key) connected = true;
                if (connected) continue;
                string name = players.NameOf(key);
                recordedAway.Add(new FrontDoorPerson { Key = key, Name = string.IsNullOrEmpty(name) ? "Player " + (recordedAway.Count + 1) : name });
            }
        }

        public WorldPlayerVerdict RenamePlayer(PlayerKey from, PlayerKey to) =>
            world == null ? WorldPlayerVerdict.NotRecorded : world.RenameRecorded(from, to);

        public int ConnectedCount => roster.ConnectedCount;

        public void GetConnectedNames(List<string> into) => roster.GetConnectedNames(into);

        public bool TryStartSession() => flow.TryStartSession();

        public WorldPlayerVerdict LastStartVerdict => flow.LastStartVerdict;
    }
}
