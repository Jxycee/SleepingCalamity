// Core/World/WorldPlayers.cs: who a world belongs to (user decision, 2026-10-29). Pure C#, no engine references.
//
// The rules (CLAUDE.md "Players and saves"):
//  - A world is Solo or Multiplayer, fixed at creation, never both.
//  - A Solo world records exactly its one owner and refuses every join (TryJoin). The owner plays it through CanStart with themselves present.
//  - A Multiplayer world is created with 2 to 4 distinct players present and records them. It records up to MaxRecorded (4) unique player keys;
//    once 4 are recorded a new key is refused (WorldFull): a fifth friend starts a new world with three of them.
//  - Any subset of the recorded players can play a Multiplayer world, but EVERY session of it needs 2 or more recorded players present to
//    start (coordinator ruling of 2026-10-29): a 1-player start is refused (NotEnoughPlayers), because solo play has its own Solo world.
//    So 2 of 4 can start, 1 of 4 cannot. A recorded player who is not in this session is simply absent; they stay recorded.
//  - A recorded player can always rejoin (TryJoin returns Ok without changing the record). A key that is not recorded joins only while there is room.
//  - Difficulty scales to the players present: ScalePlayers(present) is that count (1 for Solo, 2..4 for Multiplayer), the input the
//    difficulty table reads.
// Authority: host only. The host loads this from the world save and runs these checks at connection approval and at session start;
// a client never evaluates them for itself. Saved as the optional "players" section of WorldSave (WorldSaveRules cleans it on its own).
using System;
using System.Collections.Generic;

namespace SleepingCalamity.Core
{
    public enum WorldKind : byte { Solo = 0, Multiplayer = 1 }

    public enum WorldPlayerVerdict : byte
    {
        Ok = 0,
        InvalidPlayer,       // a key with no id, an id that is too long or an identity kind that does not exist
        DuplicatePlayer,     // the same key named twice in one list (a caller bug, never silently merged)
        NotEnoughPlayers,    // a Multiplayer world needs 2 or more players present to be created or started
        TooManyPlayers,      // more than MaxRecorded players named at creation
        SoloWorldClosed,     // a Solo world refuses any join
        WorldFull,           // 4 players are recorded and this key is not one of them
        NotRecorded,         // a player present at a start is not one of the world's recorded players
        WrongPlayerCount,    // a Solo world starts with exactly its owner present
    }

    public sealed class WorldPlayers
    {
        public const int MaxRecorded = 4;
        public const int MinPlayersToStartMultiplayer = 2;

        private readonly List<PlayerKey> _recorded;

        private WorldPlayers(WorldKind kind, List<PlayerKey> recorded)
        {
            Kind = kind;
            _recorded = recorded;
        }

        public WorldKind Kind { get; }

        // In the order they were recorded. The first is the world's creator (the host at creation).
        public IReadOnlyList<PlayerKey> Recorded => _recorded;

        public bool IsRecorded(PlayerKey key) => IndexOf(_recorded, key) >= 0;

        // Creates a world. Solo: exactly 1 player present. Multiplayer: 2 to MaxRecorded distinct players present. On any refusal world is null.
        public static WorldPlayerVerdict TryCreate(WorldKind kind, IReadOnlyList<PlayerKey> present, out WorldPlayers world)
        {
            world = null;
            if (kind != WorldKind.Solo && kind != WorldKind.Multiplayer) return WorldPlayerVerdict.InvalidPlayer;
            WorldPlayerVerdict list = CheckList(present);
            if (list != WorldPlayerVerdict.Ok) return list;

            int count = present.Count;
            if (kind == WorldKind.Solo)
            {
                if (count != 1) return WorldPlayerVerdict.WrongPlayerCount;
            }
            else
            {
                if (count < MinPlayersToStartMultiplayer) return WorldPlayerVerdict.NotEnoughPlayers;
                if (count > MaxRecorded) return WorldPlayerVerdict.TooManyPlayers;
            }

            world = new WorldPlayers(kind, new List<PlayerKey>(present));
            return WorldPlayerVerdict.Ok;
        }

        // Connection approval: may this key take part in this world? A Solo world refuses everyone. A recorded key is accepted again without
        // a change. A new key is recorded while fewer than MaxRecorded are recorded, else refused (WorldFull).
        public WorldPlayerVerdict TryJoin(PlayerKey key)
        {
            if (!IsValidKey(key)) return WorldPlayerVerdict.InvalidPlayer;
            if (Kind == WorldKind.Solo) return WorldPlayerVerdict.SoloWorldClosed;
            if (IsRecorded(key)) return WorldPlayerVerdict.Ok;
            if (_recorded.Count >= MaxRecorded) return WorldPlayerVerdict.WorldFull;
            _recorded.Add(key);
            return WorldPlayerVerdict.Ok;
        }

        // Session start: may this group begin play? Solo: exactly the owner. Multiplayer: 2 or more distinct players, every one recorded.
        // Does not change the record (a new player is admitted with TryJoin first).
        public WorldPlayerVerdict CanStart(IReadOnlyList<PlayerKey> present)
        {
            WorldPlayerVerdict list = CheckList(present);
            if (list != WorldPlayerVerdict.Ok) return list;

            for (int i = 0; i < present.Count; i++)
                if (!IsRecorded(present[i])) return WorldPlayerVerdict.NotRecorded;

            if (Kind == WorldKind.Solo) return present.Count == 1 ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.WrongPlayerCount;
            return present.Count >= MinPlayersToStartMultiplayer ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.NotEnoughPlayers;
        }

        // The difficulty scale input: how many of the world's recorded players are present (distinct; keys that are not recorded do not count).
        public int ScalePlayers(IReadOnlyList<PlayerKey> present)
        {
            if (present == null) return 0;
            var seen = new List<PlayerKey>(MaxRecorded);
            for (int i = 0; i < present.Count; i++)
                if (IsRecorded(present[i]) && IndexOf(seen, present[i]) < 0) seen.Add(present[i]);
            return seen.Count;
        }

        public static bool IsValidKey(PlayerKey key)
        {
            return key.IsValid && Enum.IsDefined(typeof(IdentityKind), key.Kind);
        }

        // The save form (WorldSave.Players).
        public WorldPlayersSave ToSave()
        {
            var save = new WorldPlayersSave { Kind = (int)Kind, Recorded = new List<WorldPlayerSave>(_recorded.Count) };
            foreach (PlayerKey key in _recorded) save.Recorded.Add(new WorldPlayerSave { Identity = (int)key.Kind, Id = key.Id });
            return save;
        }

        // Reads a save section, applying the same rules as creation and join: false for anything a real world could not hold (an unknown
        // kind, a bad or repeated key, a Solo world without exactly one player, a Multiplayer world with fewer than two or more than four).
        public static bool TryFromSave(WorldPlayersSave save, out WorldPlayers world)
        {
            world = null;
            if (save == null || save.Recorded == null || save.Kind < 0 || save.Kind > (int)WorldKind.Multiplayer) return false;
            var keys = new List<PlayerKey>(save.Recorded.Count);
            foreach (WorldPlayerSave p in save.Recorded)
            {
                if (p == null || p.Identity < 0 || p.Identity > byte.MaxValue) return false;
                keys.Add(new PlayerKey((IdentityKind)p.Identity, p.Id));
            }
            return TryCreate((WorldKind)save.Kind, keys, out world) == WorldPlayerVerdict.Ok;
        }

        private static WorldPlayerVerdict CheckList(IReadOnlyList<PlayerKey> keys)
        {
            if (keys == null) return WorldPlayerVerdict.NotEnoughPlayers;
            for (int i = 0; i < keys.Count; i++)
            {
                if (!IsValidKey(keys[i])) return WorldPlayerVerdict.InvalidPlayer;
                for (int j = 0; j < i; j++)
                    if (keys[j] == keys[i]) return WorldPlayerVerdict.DuplicatePlayer;
            }
            return WorldPlayerVerdict.Ok;
        }

        private static int IndexOf(List<PlayerKey> list, PlayerKey key)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == key) return i;
            return -1;
        }
    }
}
