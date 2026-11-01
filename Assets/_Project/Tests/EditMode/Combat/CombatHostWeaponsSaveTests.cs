// Authority: none (EditMode tests). M6 S0: the world save's weapons section. A CombatHost captures every keyed player's growth into HostWorld (players who
// left too) and seeds a returning player's growth from it; a player with no entry starts fresh; a Solo world's growth follows its owner's rebound key (A32).
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Core.Combat;
using SleepingCalamity.Core.Weapons;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.Tests.EditMode
{
    public class CombatHostWeaponsSaveTests
    {
        private static readonly string Axe = AttackCatalog.Heirlooms[0];
        private GameObject _go;
        private CombatHost _host;
        private HostRoster _roster;
        private HostWorld _world;

        static PlayerKey K(string id) => new PlayerKey(IdentityKind.Local, id);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CombatHostWeaponsSaveTests");
            _host = _go.AddComponent<CombatHost>();
            _roster = new HostRoster();
            _world = new HostWorld();
            _host.Identity = _roster;
            _host.GrowthWorld = _world;
            _world.RegisterGrowthSource(_host);   // ServerBegin does this on a real host
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_go); }

        // A new host (a later session) over the same world, seeded from a saved copy of the section.
        private CombatHost NewSession(WeaponsSave saved, out HostWorld world)
        {
            world = new HostWorld();
            world.LoadWeapons(WeaponsSaveRules.Clean(saved, null, null));
            var go = new GameObject("CombatHostWeaponsSaveTests.next");
            var host = go.AddComponent<CombatHost>();
            host.Identity = _roster;
            host.GrowthWorld = world;
            return host;
        }

        // Catches: a returning player (new client id) starting with fresh heirlooms after a save and reload.
        [Test]
        public void CapturedGrowth_SeedsTheReturningPlayer_ByKey_AndAFreshPlayerStartsFresh()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _roster.Connect(new ApprovedJoin(1, K("a"), "a", 1, false));
            _host.Growth(0).Weapon(Axe).AddAbs(40f);
            _host.Growth(1).Weapon(Axe).AddAbs(5f);
            float hostAbs = _host.Growth(0).Weapon(Axe).Abs, aAbs = _host.Growth(1).Weapon(Axe).Abs;
            int hostLevel = _host.Growth(0).Weapon(Axe).Level;
            Assert.That(hostLevel + hostAbs, Is.GreaterThan(0f));

            var file = new WorldSave();
            Assert.That(_world.CaptureWeaponsInto(file), Is.True);
            Assert.That(file.Weapons.Weapons.Count, Is.EqualTo(2 * AttackCatalog.Heirlooms.Length), "keys stay unique across players");
            WorldSave json = Roundtrip(file);

            _roster.Disconnect(1);
            _roster.Connect(new ApprovedJoin(9, K("a"), "a", 1, true));
            _roster.Connect(new ApprovedJoin(10, K("newcomer"), "newcomer", 2, false));
            CombatHost next = NewSession(json.Weapons, out _);
            try
            {
                Assert.That(next.Growth(9).Weapon(Axe).Abs, Is.EqualTo(aAbs), "the returning player got theirs");
                Assert.That(next.Growth(0).Weapon(Axe).Level, Is.EqualTo(hostLevel));
                Assert.That(next.Growth(0).Weapon(Axe).Abs, Is.EqualTo(hostAbs));
                Assert.That(next.Growth(10).Weapon(Axe).Abs, Is.EqualTo(0f), "no entry: a fresh player");
                Assert.That(next.Growth(10).Weapon(Axe).Level, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(next.gameObject); }
        }

        // Catches: a player who left this session losing their growth at the next save.
        [Test]
        public void APlayerWhoLeft_IsStillInTheSave()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _roster.Connect(new ApprovedJoin(1, K("a"), "a", 1, false));
            _host.Growth(1).Weapon(Axe).AddAbs(30f);
            float aAbs = _host.Growth(1).Weapon(Axe).Abs;
            _host.Growth(0);
            var file = new WorldSave();
            _world.CaptureWeaponsInto(file);

            _roster.Disconnect(1);
            _world.UnregisterGrowthSource(_host);   // the CombatHost goes away (scene change)
            Assert.That(_world.CaptureWeaponsInto(file), Is.False, "nothing changed");
            Assert.That(file.Weapons.Weapons.Exists(w => w.OwnerId == "a" && w.DefinitionId == Axe), Is.True);
            Assert.That(GrowthSaves.Restore("Local:a", "a", file.Weapons).Weapon(Axe).Abs, Is.EqualTo(aAbs));
        }

        // Catches: unlocked abilities and spell pips lost across a save (unlocks only ever grow).
        [Test]
        public void AbilitiesAndPips_RoundTrip()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            PlayerGrowth g = _host.Growth(0);
            g.Abilities.Restore(new[] { "axe_quake", "from_a_newer_build" });
            g.Pips.TrySpend(2);
            var file = new WorldSave();
            _world.CaptureWeaponsInto(file);

            CombatHost next = NewSession(Roundtrip(file).Weapons, out _);
            try
            {
                PlayerGrowth back = next.Growth(0);
                Assert.That(back.Abilities.Has("axe_quake"), Is.True);
                CollectionAssert.Contains(back.Abilities.ToSave().Unlocked, "from_a_newer_build");
                Assert.That(back.Pips.Current, Is.EqualTo(g.Pips.Current));
            }
            finally { Object.DestroyImmediate(next.gameObject); }
        }

        // Catches: a Solo world's owner getting fresh heirlooms after a profile change (A32: a solo world rebinds to its owner).
        [Test]
        public void SoloRebind_KeepsTheOwnersGrowth()
        {
            WorldPlayers.TryCreate(WorldKind.Solo, new[] { K("old") }, out WorldPlayers solo);
            _world.Load(solo);
            _roster.Connect(new ApprovedJoin(0, K("old"), "old", 0, false));
            _host.Growth(0).Weapon(Axe).AddAbs(40f);
            float abs = _host.Growth(0).Weapon(Axe).Abs;
            var file = new WorldSave();
            _world.CaptureWeaponsInto(file);

            // A later session: the same file, but this machine's key is now "new".
            var world = new HostWorld();
            world.Load(solo);
            world.LoadWeapons(WeaponsSaveRules.Clean(Roundtrip(file).Weapons, null, null));
            Assert.That(world.Admit(K("new"), 0, true), Is.EqualTo(WorldPlayerVerdict.Ok));
            var roster = new HostRoster();
            roster.Connect(new ApprovedJoin(0, K("new"), "new", 0, false));
            var go = new GameObject("CombatHostWeaponsSaveTests.rebound");
            try
            {
                var host = go.AddComponent<CombatHost>();
                host.Identity = roster;
                host.GrowthWorld = world;
                Assert.That(host.Growth(0).Weapon(Axe).Abs, Is.EqualTo(abs));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // Catches: a growth id with no recorded key (tests, no session) leaking into the save.
        [Test]
        public void AnUnkeyedPlayer_IsNotSaved()
        {
            _host.Identity = null;
            _host.Growth(5).Weapon(Axe).AddAbs(40f);
            Assert.That(_world.CaptureWeaponsInto(new WorldSave()), Is.False, "nothing to write");
        }

        // Catches: the world holding the file's own section object (WorldSaveFlow.Open passes store.Current.Weapons), so capture compared it with itself
        // and never marked the file dirty from the second session on.
        [Test]
        public void LoadedByReference_StillCapturesNewGrowth()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _host.Growth(0).Weapon(Axe).AddAbs(10f);
            var file = new WorldSave();
            Assert.That(_world.CaptureWeaponsInto(file), Is.True);

            var world = new HostWorld();
            world.LoadWeapons(file.Weapons);   // by reference, as Open does
            Assert.That(world.Weapons, Is.Not.SameAs(file.Weapons), "a separate copy");
            var go = new GameObject("CombatHostWeaponsSaveTests.byref");
            try
            {
                var host = go.AddComponent<CombatHost>();
                host.Identity = _roster;
                host.GrowthWorld = world;
                world.RegisterGrowthSource(host);
                WeaponState axe = host.Growth(0).Weapon(Axe);
                float before = axe.Abs;
                axe.AddAbs(20f);
                Assert.That(axe.Abs, Is.Not.EqualTo(before));
                Assert.That(world.CaptureWeaponsInto(file), Is.True, "growth since the load is seen");
                Assert.That(GrowthSaves.Restore("Local:host", "host", file.Weapons).Weapon(Axe).Abs, Is.EqualTo(axe.Abs));
                Assert.That(world.CaptureWeaponsInto(file), Is.False, "and nothing more to write after");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // Catches: two CombatHosts alive at once (a scene change overlapping the old host's teardown) losing growth to whichever captures last.
        [Test]
        public void TwoLiveHosts_LoseNoGrowth()
        {
            _roster.Connect(new ApprovedJoin(0, K("host"), "host", 0, false));
            _roster.Connect(new ApprovedJoin(1, K("a"), "a", 1, false));
            _host.Growth(0).Weapon(Axe).AddAbs(10f);
            _host.Growth(1).Weapon(Axe).AddAbs(10f);
            float aAbs = _host.Growth(1).Weapon(Axe).Abs;
            float firstHostAbs = _host.Growth(0).Weapon(Axe).Abs;

            var go = new GameObject("CombatHostWeaponsSaveTests.second");
            try
            {
                var b = go.AddComponent<CombatHost>();
                b.Identity = _roster;
                b.GrowthWorld = _world;
                _world.RegisterGrowthSource(b);   // host B is enabled while A is still alive
                WeaponState bAxe = b.Growth(0).Weapon(Axe);
                Assert.That(bAxe.Abs, Is.EqualTo(firstHostAbs), "B started from A's growth");
                bAxe.AddAbs(5f);
                float bAbs = bAxe.Abs;
                Assert.That(bAbs, Is.Not.EqualTo(firstHostAbs));

                var file = new WorldSave();
                Assert.That(_world.CaptureWeaponsInto(file), Is.True);
                Assert.That(GrowthSaves.Restore("Local:host", "host", file.Weapons).Weapon(Axe).Abs, Is.EqualTo(bAbs), "B's increment on top of A's");
                Assert.That(GrowthSaves.Restore("Local:a", "a", file.Weapons).Weapon(Axe).Abs, Is.EqualTo(aAbs), "A's player B never touched");
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static WorldSave Roundtrip(WorldSave s)
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<WorldSave>(Newtonsoft.Json.JsonConvert.SerializeObject(s));
        }
    }
}
