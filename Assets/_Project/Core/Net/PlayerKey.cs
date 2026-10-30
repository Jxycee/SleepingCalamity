using System;
using System.Text;

namespace SleepingCalamity.Core
{
    // Persistent identity of a player: used to detect duplicates, to reclaim slots and as the key of a world's recorded players.
    // Equal when Kind matches and Id matches by ordinal (case-sensitive) comparison.
    public readonly struct PlayerKey : IEquatable<PlayerKey>
    {
        // The longest valid id, in UTF-8 bytes (not chars): a FixedString64Bytes holds 61 bytes, so a valid id is never truncated on a roster.
        public const int MaxIdLength = 61;

        public readonly IdentityKind Kind;
        public readonly string Id;

        public PlayerKey(IdentityKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public bool IsValid => !string.IsNullOrEmpty(Id) && Id.Length <= MaxIdLength && Encoding.UTF8.GetByteCount(Id) <= MaxIdLength;

        // A Local id is a lowercase GUID in "N" format (32 hex digits, no dashes), made by Guid.NewGuid().ToString("N"). Returns that form for
        // any case of a valid one, or "" for anything else (the settings store then makes a fresh id).
        public static string NormalizeLocalId(string raw)
        {
            if (raw == null || raw.Length != 32) return "";
            var sb = new StringBuilder(32);
            foreach (char c in raw)
            {
                bool digit = c >= '0' && c <= '9';
                bool lower = c >= 'a' && c <= 'f';
                bool upper = c >= 'A' && c <= 'F';
                if (!digit && !lower && !upper) return "";
                sb.Append(upper ? (char)(c + 32) : c);
            }
            return sb.ToString();
        }

        // The key of this machine's Local identity (dev-direct and solo worlds). The id is normalized; an invalid one gives an invalid key.
        public static PlayerKey Local(string id) => new PlayerKey(IdentityKind.Local, NormalizeLocalId(id));

        public bool Equals(PlayerKey other)
        {
            return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Kind * 397) ^ (Id == null ? 0 : StringComparer.Ordinal.GetHashCode(Id));
            }
        }

        public override string ToString()
        {
            return Kind + ":" + Id;
        }

        public static bool operator ==(PlayerKey a, PlayerKey b) => a.Equals(b);

        public static bool operator !=(PlayerKey a, PlayerKey b) => !a.Equals(b);
    }
}
