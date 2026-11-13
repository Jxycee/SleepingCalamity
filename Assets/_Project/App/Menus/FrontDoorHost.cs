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
        // The lobby reads these every frame: the person objects are reused (the model copies one when the host asks about it), so no frame allocates.
        private readonly List<FrontDoorPerson> newPool = new List<FrontDoorPerson>();
        private readonly List<FrontDoorPerson> awayPool = new List<FrontDoorPerson>();
        private readonly List<FrontDoorPerson> strangerPool = new List<FrontDoorPerson>();
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
                    Put(newcomers, newPool, person.Key, person.Value);
            if (newcomers.Count == 0) return;
            foreach (PlayerKey key in players.Recorded)
            {
                bool connected = false;
                foreach (KeyValuePair<PlayerKey, string> person in people) if (person.Key == key) connected = true;
                if (connected) continue;
                string name = players.NameOf(key);
                Put(recordedAway, awayPool, key, string.IsNullOrEmpty(name) ? "Player " + (recordedAway.Count + 1) : name);
            }
        }

        public WorldPlayerVerdict RenamePlayer(PlayerKey from, PlayerKey to) =>
            world == null ? WorldPlayerVerdict.NotRecorded : world.RenameRecorded(from, to);

        // Connected but not recorded in a world that has no place left for them (a lobby visitor of a full world).
        public void GetStrangers(List<FrontDoorPerson> into)
        {
            into.Clear();
            WorldPlayers players = world?.Players;
            if (players == null || players.Kind != WorldKind.Multiplayer || world.Started) return;
            roster.GetConnectedPeople(people);
            // The host is never a stranger (HostWorld.Admit admits it even when its sign-in id changed) and cannot be sent away.
            bool hasHostKey = roster.TryGetKey(Unity.Netcode.NetworkManager.ServerClientId, out PlayerKey hostKey);
            foreach (KeyValuePair<PlayerKey, string> person in people)
                if ((!hasHostKey || person.Key != hostKey) && !players.IsRecorded(person.Key) && players.CanJoin(person.Key) == WorldPlayerVerdict.WorldFull)
                    Put(into, strangerPool, person.Key, person.Value);
        }

        // Adds a person to `into`, reusing the pooled object at that position.
        private static void Put(List<FrontDoorPerson> into, List<FrontDoorPerson> pool, PlayerKey key, string name)
        {
            int at = into.Count;
            while (pool.Count <= at) pool.Add(new FrontDoorPerson());
            FrontDoorPerson person = pool[at];
            person.Key = key;
            person.Name = name;
            into.Add(person);
        }

        public bool SendAway(PlayerKey key) => flow.SendAway(key);

        // Host side: this machine is never "waiting for a place". A guest learns it from the host, which has no channel for it yet.
        public bool WaitsForPlace => false;

        public int ConnectedCount => roster.ConnectedCount;

        public void GetConnectedNames(List<string> into) => roster.GetConnectedNames(into);

        public bool TryStartSession() => flow.TryStartSession();

        public WorldPlayerVerdict LastStartVerdict => flow.LastStartVerdict;
    }
}
