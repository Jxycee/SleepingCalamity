// Authority: none (EditMode tests). CombatHost keeps weapon growth per recorded player (HostRoster PlayerKey), not per NGO client id (decision A34).
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class CombatHostGrowthKeyTests
    {
        private static readonly string Axe = AttackCatalog.Heirlooms[0];
        private GameObject _go;
        private CombatHost _host;
        private HostRoster _roster;

        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatHostGrowthKeyTests");
            _host = _go.AddComponent<CombatHost>();
            _roster = new HostRoster();
            _host.Identity = _roster;
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_go); }

        // Catches: growth lost when a player drops and rejoins under a new client id.
        [Test]
        public void Rejoin_UnderNewClientId_GetsTheSameGrowthBack()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _roster.Connect(new ApprovedJoin(1, K("a"), "a", 1, false));
            _host.Growth(1).Weapon(Axe).AddAbs(40f);
            float abs = _host.Growth(1).Weapon(Axe).Abs;
            int level = _host.Growth(1).Weapon(Axe).Level;
            Assert.That(_host.Growth(1).Weapon(Axe).LifetimeLevels + abs, Is.GreaterThan(0f), "the setup earned something");

            _roster.Disconnect(1);
            _roster.Connect(new ApprovedJoin(7, K("a"), "a", 1, true));

            PlayerGrowth back = _host.Growth(7);
            Assert.That(back, Is.SameAs(_host.Growth(7)));
            Assert.That(back.Weapon(Axe).Abs, Is.EqualTo(abs));
            Assert.That(back.Weapon(Axe).Level, Is.EqualTo(level));
        }

        // Catches: two recorded players sharing one growth record.
        [Test]
        public void TwoClients_KeepSeparateGrowth()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _roster.Connect(new ApprovedJoin(1, K("a"), "a", 1, false));
            _host.Growth(1).Weapon(Axe).AddAbs(40f);
            Assert.That(_host.Growth(1), Is.Not.SameAs(_host.Growth(0)));
            Assert.That(_host.Growth(1).Weapon(Axe).LifetimeLevels + _host.Growth(1).Weapon(Axe).Abs, Is.GreaterThan(0f));
            Assert.That(_host.Growth(0).Weapon(Axe).LifetimeLevels, Is.EqualTo(0));
            Assert.That(_host.Growth(0).Weapon(Axe).Abs, Is.EqualTo(0f));
        }

        // Catches: a solo host (or a session with no identity source) changing its growth id.
        [Test]
        public void SoloHost_And_NoIdentity_StayStable()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            PlayerGrowth solo = _host.Growth(0);
            Assert.That(_host.Growth(0), Is.SameAs(solo), "same record every call");
            _host.Identity = null;
            Assert.That(_host.Growth(5).PlayerId, Is.EqualTo("5"), "no roster: the client id is the id, as before");
        }
    }
}
