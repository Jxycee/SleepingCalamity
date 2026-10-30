// Tests for SleepingCalamity.Core.PlayerKey.
using SleepingCalamity.Core;
using NUnit.Framework;

namespace SleepingCalamity.Tests.EditMode
{
    public class PlayerKeyTests
    {
        [Test]
        public void PlayerKey_Equality_UsesKindAndOrdinalId()
        {
            var a = new PlayerKey(IdentityKind.Ugs, "abc");
            var same = new PlayerKey(IdentityKind.Ugs, "abc");
            var otherKind = new PlayerKey(IdentityKind.Local, "abc");
            var otherCase = new PlayerKey(IdentityKind.Ugs, "ABC");

            Assert.IsTrue(a == same);
            Assert.IsFalse(a != same);
            Assert.IsTrue(a.Equals(same));
            Assert.AreEqual(a.GetHashCode(), same.GetHashCode());
            Assert.IsTrue(a != otherKind);
            Assert.IsFalse(a.Equals(otherKind));
            Assert.IsTrue(a != otherCase, "Ids compare ordinally (case-sensitive).");
        }

        [Test]
        public void PlayerKey_EqualsObject_HandlesBoxedAndOtherTypes()
        {
            var a = new PlayerKey(IdentityKind.Ugs, "abc");
            object boxed = new PlayerKey(IdentityKind.Ugs, "abc");

            Assert.IsTrue(a.Equals(boxed));
            Assert.IsFalse(a.Equals("Ugs:abc"));
            Assert.IsFalse(a.Equals(null));
        }

        [Test]
        public void PlayerKey_IsValid_RejectsEmptyNullAndOverlong()
        {
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, "").IsValid);
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, null).IsValid);
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, new string('x', PlayerKey.MaxIdLength + 1)).IsValid);
            Assert.IsTrue(new PlayerKey(IdentityKind.Ugs, new string('x', PlayerKey.MaxIdLength)).IsValid);
            Assert.IsTrue(new PlayerKey(IdentityKind.Local, "a").IsValid);
        }

        // Catches: MaxIdLength above the FixedString64Bytes capacity (61), so a "valid" 62..64 char id is silently byte-truncated on the roster
        // (the roster identity then differs from the connecting key and a slot reclaim or takeover check compares the wrong ids).
        [Test]
        public void PlayerKey_MaxIdLength_MatchesRosterIdCapacity()
        {
            Assert.AreEqual(new Unity.Collections.FixedString64Bytes().Capacity, PlayerKey.MaxIdLength, "MaxIdLength must equal the roster FixedString64Bytes capacity");
            var key = new PlayerKey(IdentityKind.Ugs, new string('k', PlayerKey.MaxIdLength));
            Assert.IsTrue(key.IsValid);
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, new string('k', 62)).IsValid, "62 chars no longer fit the roster id");
        }

        // Catches: the id limit counted in chars while the roster field holds bytes (31 two-byte chars are 62 bytes, one too many).
        [Test]
        public void PlayerKey_IsValid_CountsUtf8BytesNotChars()
        {
            string twoByte = ((char)0x00E9).ToString();                 // 2 UTF-8 bytes
            Assert.IsTrue(new PlayerKey(IdentityKind.Ugs, string.Concat(System.Linq.Enumerable.Repeat(twoByte, 30))).IsValid, "60 bytes");
            Assert.IsFalse(new PlayerKey(IdentityKind.Ugs, string.Concat(System.Linq.Enumerable.Repeat(twoByte, 31))).IsValid, "62 bytes in 31 chars");
        }

        [Test]
        public void PlayerKey_NormalizeLocalId_IsLowercaseGuidN()
        {
            string n = System.Guid.NewGuid().ToString("N");
            Assert.AreEqual(n, PlayerKey.NormalizeLocalId(n));
            Assert.AreEqual(n, PlayerKey.NormalizeLocalId(n.ToUpperInvariant()), "case folded");
            Assert.AreEqual("", PlayerKey.NormalizeLocalId(System.Guid.NewGuid().ToString("D")), "dashes are not the N format");
            Assert.AreEqual("", PlayerKey.NormalizeLocalId(n.Substring(1)));
            Assert.AreEqual("", PlayerKey.NormalizeLocalId("g" + n.Substring(1)));
            Assert.AreEqual("", PlayerKey.NormalizeLocalId(null));
            Assert.IsTrue(PlayerKey.Local(n.ToUpperInvariant()).IsValid);
            Assert.AreEqual(n, PlayerKey.Local(n.ToUpperInvariant()).Id);
            Assert.IsFalse(PlayerKey.Local("not a guid").IsValid);
        }

        [Test]
        public void PlayerKey_ToString_IsKindColonId()
        {
            Assert.AreEqual("Local:abc", new PlayerKey(IdentityKind.Local, "abc").ToString());
            Assert.AreEqual("Ugs:p-1", new PlayerKey(IdentityKind.Ugs, "p-1").ToString());
        }

        [Test]
        public void PlayerKey_Default_IsInvalidLocalWithNullId()
        {
            var d = default(PlayerKey);
            Assert.AreEqual(IdentityKind.Local, d.Kind);
            Assert.IsNull(d.Id);
            Assert.IsFalse(d.IsValid);
            Assert.IsTrue(d == new PlayerKey(IdentityKind.Local, null));
            Assert.DoesNotThrow(() => d.GetHashCode());
            Assert.AreEqual("Local:", d.ToString());
        }
    }
}
