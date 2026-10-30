// Core/World/WorldPlayers.cs: who a world belongs to (user decision, 2026-10-29). Pure C#, no engine references.
//
// The rules (CLAUDE.md "Players and saves"):
//  - A world is Solo or Multiplayer, fixed at creation, never both. The kind is stored on its own (WorldPlayersSave.Kind), so a damaged
//    list of players never turns a Multiplayer world into "no world yet".
//  - A Solo world records exactly its one owner and refuses every join (CanJoin / Record). The owner plays it through CanStart with themselves
//    present. (If the owner's record is lost to damage, the first single player to start the world claims it: ClaimSoloOwner.)
//  - A Multiplayer world is created with 2 to 4 distinct players present and records them. It records up to MaxRecorded (4) unique player keys;
//    once 4 are recorded a new key is refused (WorldFull): a fifth friend starts a new world with three of them.
//  - Any subset of the recorded players can play a Multiplayer world, but EVERY session of it needs 2 or more recorded players present to
//    start (coordinator ruling of 2026-10-29): a 1-player start is refused (NotEnoughPlayers), because solo play has its own Solo world.
//    So 2 of 4 can start, 1 of 4 cannot. A recorded player who is not in this session is simply absent; they stay recorded. A Multiplayer
//    world that lost records to damage stays Multiplayer: it still needs 2 or more to start, and new players can fill the free places.
//  - One identity kind per world (coordinator ruling): the world records the IdentityKind of its first players (Local for dev-direct and
//    solo worlds, Ugs for online worlds). A key of another kind is refused (WrongIdentityKind). Local worlds and Online worlds are separate.
//  - A recorded player can always rejoin. A key that is not recorded joins only while there is room.
//  - Joining is two steps: CanJoin(key) is the pure check (the connection approver calls only this); Record(key) writes the key down and is
//    called once the client has actually entered the session. The host itself is approved before CanJoin is asked (JoinRules, IsServerSelf)
//    and enters the same way: the host-side gate (HostWorld) treats it as the owner/creator, never as a joiner. A Solo world refuses all joins.
//  - Difficulty scales to the players present: ScalePlayers(present) is that count, clamped to 1..4 (the input the difficulty table reads).
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
        WrongIdentityKind,   // the key's identity kind is not the one this world was made with
    }

    public sealed class WorldPlayers
    {
        public const int MaxRecorded = 4;
        public const int MinPlayersToStartMultiplayer = 2;

        private readonly List<PlayerKey> _recorded;
        private IdentityKind? _identity;
        private Dictionary<string, object> _sectionExtra;
        private readonly Dictionary<PlayerKey, Dictionary<string, object>> _keyExtra = new Dictionary<PlayerKey, Dictionary<string, object>>();
        private readonly Dictionary<PlayerKey, string> _names = new Dictionary<PlayerKey, string>();

        private WorldPlayers(WorldKind kind, List<PlayerKey> recorded, IdentityKind? identity)
        {
            Kind = kind;
            _recorded = recorded;
            _identity = identity ?? (recorded.Count > 0 ? recorded[0].Kind : (IdentityKind?)null);
        }

        public WorldKind Kind { get; }

        // In the order they were recorded. The first is the world's creator (the host at creation).
        public IReadOnlyList<PlayerKey> Recorded => _recorded;

        // The identity kind the world was made with; null only for a world whose records were all lost (the next recorded key sets it).
        public IdentityKind? Identity => _identity;

        public bool IsRecorded(PlayerKey key) => IndexOf(_recorded, key) >= 0;

        // Creates a world. Solo: exactly 1 player present. Multiplayer: 2 to MaxRecorded distinct players present. All of one identity kind.
        // On any refusal world is null.
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
            for (int i = 1; i < count; i++)
                if (present[i].Kind != present[0].Kind) return WorldPlayerVerdict.WrongIdentityKind;

            world = new WorldPlayers(kind, new List<PlayerKey>(present), null);
            return WorldPlayerVerdict.Ok;
        }

        // The pure join check, the only thing the connection approver asks: may this key take part in this world? Writes nothing.
        public WorldPlayerVerdict CanJoin(PlayerKey key)
        {
            if (!IsValidKey(key)) return WorldPlayerVerdict.InvalidPlayer;
            if (Kind == WorldKind.Solo) return WorldPlayerVerdict.SoloWorldClosed;
            if (_identity.HasValue && key.Kind != _identity.Value) return WorldPlayerVerdict.WrongIdentityKind;
            if (IsRecorded(key)) return WorldPlayerVerdict.Ok;
            return _recorded.Count >= MaxRecorded ? WorldPlayerVerdict.WorldFull : WorldPlayerVerdict.Ok;
        }

        // Writes the key down once its client has entered the session. Same checks as CanJoin; a recorded key is a no-op.
        public WorldPlayerVerdict Record(PlayerKey key)
        {
            WorldPlayerVerdict can = CanJoin(key);
            if (can != WorldPlayerVerdict.Ok) return can;
            if (IsRecorded(key)) return WorldPlayerVerdict.Ok;
            _recorded.Add(key);
            if (!_identity.HasValue) _identity = key.Kind;
            return WorldPlayerVerdict.Ok;
        }

        // A Solo world whose owner record was lost: the single player who starts it becomes the owner. False for any other world.
        public bool ClaimSoloOwner(PlayerKey key)
        {
            if (Kind != WorldKind.Solo || _recorded.Count != 0 || !IsValidKey(key)) return false;
            _recorded.Add(key);
            _identity = key.Kind;
            return true;
        }

        // A Solo world belongs to the machine that plays it, not to one key: when the local key changed (a new profile, a copied save, an
        // identity reset) the owner is rebound to the current key and keeps their name. True when the owner changed. False for a world that is
        // not Solo, an invalid key, or a key that already is the owner.
        public bool RebindSoloOwner(PlayerKey key)
        {
            if (Kind != WorldKind.Solo || !IsValidKey(key)) return false;
            if (_recorded.Count == 0) return ClaimSoloOwner(key);
            PlayerKey old = _recorded[0];
            if (old == key) return false;
            _names.TryGetValue(old, out string name);
            _keyExtra.TryGetValue(old, out Dictionary<string, object> extra);
            _names.Remove(old);
            _keyExtra.Remove(old);
            _recorded[0] = key;
            _identity = key.Kind;
            if (name != null) _names[key] = name;
            if (extra != null) _keyExtra[key] = extra;
            return true;
        }

        // The display name written down for a recorded player (what the world picker shows), or "" when none was kept.
        public string NameOf(PlayerKey key) => _names.TryGetValue(key, out string name) ? name : "";

        // Keeps a recorded player's display name; ignored for a key that is not recorded. Returns true when the stored name changed.
        public bool SetName(PlayerKey key, string name)
        {
            if (!IsRecorded(key) || string.IsNullOrWhiteSpace(name)) return false;
            string clean = DisplayNameRules.Sanitize(name, 0);
            if (_names.TryGetValue(key, out string old) && old == clean) return false;
            _names[key] = clean;
            return true;
        }

        // Session start: may this group begin play? Solo: exactly the owner (or, with the owner record lost, exactly one player).
        // Multiplayer: 2 or more distinct players, every one recorded. Does not change the record (a new player is recorded when they
        // enter the session).
        public WorldPlayerVerdict CanStart(IReadOnlyList<PlayerKey> present)
        {
            WorldPlayerVerdict list = CheckList(present);
            if (list != WorldPlayerVerdict.Ok) return list;

            if (Kind == WorldKind.Solo && _recorded.Count == 0)
            {
                if (present.Count != 1) return WorldPlayerVerdict.WrongPlayerCount;
                return Matches(present[0]) ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.WrongIdentityKind;
            }

            for (int i = 0; i < present.Count; i++)
            {
                if (!Matches(present[i])) return WorldPlayerVerdict.WrongIdentityKind;
                if (!IsRecorded(present[i])) return WorldPlayerVerdict.NotRecorded;
            }

            if (Kind == WorldKind.Solo) return present.Count == 1 ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.WrongPlayerCount;
            return present.Count >= MinPlayersToStartMultiplayer ? WorldPlayerVerdict.Ok : WorldPlayerVerdict.NotEnoughPlayers;
        }

        // The difficulty scale input: how many of the world's recorded players are present (distinct; keys that are not recorded do not
        // count), clamped to 1..MaxRecorded so a table never sees 0.
        public int ScalePlayers(IReadOnlyList<PlayerKey> present)
        {
            int count = 0;
            if (present != null)
            {
                var seen = new List<PlayerKey>(MaxRecorded);
                for (int i = 0; i < present.Count; i++)
                    if (IsRecorded(present[i]) && IndexOf(seen, present[i]) < 0) seen.Add(present[i]);
                count = seen.Count;
            }
            return Math.Max(1, Math.Min(MaxRecorded, count));
        }

        // Valid when it has an id of 1..PlayerKey.MaxIdLength UTF-8 bytes and an identity kind that exists.
        public static bool IsValidKey(PlayerKey key)
        {
            return key.IsValid && Enum.IsDefined(typeof(IdentityKind), key.Kind);
        }

        // The save form (WorldSave.Players). Unknown members read from a newer build's file are written back.
        public WorldPlayersSave ToSave()
        {
            var save = new WorldPlayersSave
            {
                Kind = (int)Kind,
                Identity = _identity.HasValue ? (int)_identity.Value : -1,
                Recorded = new List<WorldPlayerSave>(_recorded.Count),
                Extra = _sectionExtra == null ? null : new Dictionary<string, object>(_sectionExtra),
            };
            foreach (PlayerKey key in _recorded)
            {
                _keyExtra.TryGetValue(key, out Dictionary<string, object> extra);
                save.Recorded.Add(new WorldPlayerSave { Identity = (int)key.Kind, Id = key.Id, Name = NameOf(key), Extra = extra == null ? null : new Dictionary<string, object>(extra) });
            }
            return save;
        }

        // Reads a save section. Lenient about records (the world survives damage): the kind must be valid, bad, repeated and
        // wrong-identity records are dropped, a Solo world keeps its first record only and a list is cut to MaxRecorded. droppedAny says
        // whether anything was dropped. False only for a null section or an unknown kind.
        public static bool TryRestore(WorldPlayersSave save, out WorldPlayers world, out bool droppedAny)
        {
            world = null;
            droppedAny = false;
            if (save == null || save.Kind < 0 || save.Kind > (int)WorldKind.Multiplayer) return false;
            var kind = (WorldKind)save.Kind;
            IdentityKind? identity = save.Identity >= 0 && save.Identity <= (int)IdentityKind.Steam ? (IdentityKind)save.Identity : (IdentityKind?)null;
            if (save.Identity != -1 && !identity.HasValue) droppedAny = true;   // an identity kind that does not exist: derived from the records

            var keys = new List<PlayerKey>(MaxRecorded);
            var extras = new Dictionary<PlayerKey, Dictionary<string, object>>();
            var names = new Dictionary<PlayerKey, string>();
            int limit = kind == WorldKind.Solo ? 1 : MaxRecorded;
            if (save.Recorded == null) droppedAny = true;
            else
            {
                foreach (WorldPlayerSave p in save.Recorded)
                {
                    PlayerKey key = p != null && p.Identity >= 0 && p.Identity <= byte.MaxValue ? new PlayerKey((IdentityKind)p.Identity, p.Id) : default;
                    bool ok = p != null && IsValidKey(key) && keys.Count < limit && IndexOf(keys, key) < 0
                              && (!identity.HasValue || key.Kind == identity.Value);
                    if (!ok)
                    {
                        droppedAny = true;
                        continue;
                    }
                    if (!identity.HasValue) identity = key.Kind;
                    keys.Add(key);
                    if (p.Extra != null) extras[key] = new Dictionary<string, object>(p.Extra);
                    if (!string.IsNullOrWhiteSpace(p.Name)) names[key] = DisplayNameRules.Sanitize(p.Name, 0);
                }
            }

            world = new WorldPlayers(kind, keys, identity);
            if (save.Extra != null) world._sectionExtra = new Dictionary<string, object>(save.Extra);
            foreach (KeyValuePair<PlayerKey, Dictionary<string, object>> e in extras) world._keyExtra[e.Key] = e.Value;
            foreach (KeyValuePair<PlayerKey, string> e in names) world._names[e.Key] = e.Value;
            return true;
        }

        // Strict form of TryRestore: false when anything had to be dropped or a Multiplayer world has fewer than two records.
        public static bool TryFromSave(WorldPlayersSave save, out WorldPlayers world)
        {
            if (!TryRestore(save, out world, out bool dropped) || dropped) { world = null; return false; }
            if (world.Kind == WorldKind.Multiplayer && world.Recorded.Count < MinPlayersToStartMultiplayer) { world = null; return false; }
            if (world.Kind == WorldKind.Solo && world.Recorded.Count != 1) { world = null; return false; }
            return true;
        }

        private bool Matches(PlayerKey key) => !_identity.HasValue || key.Kind == _identity.Value;

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
