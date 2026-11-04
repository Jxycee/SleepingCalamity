// Tests/EditMode/WorldSaveRulesTests.cs: what a loaded world save may hold (Core/Save/WorldSaveRules). Pure Core.
// Ported from FishGame's world save tests (seed, clean-per-section); its fishing sections are gone, the players section is new.
using System.Collections.Generic;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class WorldSaveRulesTests
    {
        const ulong DefaultSeed = 0xF15F15F1UL;

        static WorldPlayerSave P(string id, int identity = 0) => new WorldPlayerSave { Identity = identity, Id = id };

        static WorldSave Good()
        {
            return new WorldSave
            {
                WorldSeed = "00000000DEADBEEF",
                Players = new WorldPlayersSave { Kind = (int)WorldKind.Multiplayer, Recorded = new List<WorldPlayerSave> { P("a"), P("b"), P("c") } },
            };
        }

        // Catches: a 64-bit seed losing its top bits through the hex form, a short, long or non-hex seed accepted, or lower case refused.
        [Test]
        public void Seed_RoundTripsAllSixtyFourBits_OnlyExactHex()
        {
            Assert.That(WorldSaveRules.SeedToHex(0xFEDCBA9876543210UL), Is.EqualTo("FEDCBA9876543210"));
            Assert.That(WorldSaveRules.TryParseSeed("FEDCBA9876543210", out ulong s) && s == 0xFEDCBA9876543210UL, Is.True);
            Assert.That(WorldSaveRules.TryParseSeed("fedcba9876543210", out ulong lower) && lower == 0xFEDCBA9876543210UL, Is.True, "either case");
            Assert.That(WorldSaveRules.TryParseSeed(WorldSaveRules.SeedToHex(0UL), out ulong zero) && zero == 0UL, Is.True);
            foreach (string bad in new[] { null, "", "F15F15F1", "00000000F15F15F1A", "00000000F15F15G1", " 0000000F15F15F1", "-000000000000001" })
                Assert.That(WorldSaveRules.TryParseSeed(bad, out _), Is.False, "'" + bad + "'");
        }

        // Catches: a good save altered on load (a section reset, a value changed, the schema version or the saved time dropped), or a rejection
        // reported for it.
        [Test]
        public void Clean_KeepsAGoodSaveAsItIs()
        {
            var rejected = new List<string> { "stale" };
            WorldSave good = Good();
            good.SchemaVersion = 1;
            good.SavedAtUtc = "2026-10-29T19:40:00Z";
            WorldSave clean = WorldSaveRules.Clean(good, DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty, "the list is cleared and nothing is rejected");
            Assert.That(clean.SchemaVersion, Is.EqualTo(1));
            Assert.That(clean.SavedAtUtc, Is.EqualTo("2026-10-29T19:40:00Z"));
            Assert.That(clean.WorldSeed, Is.EqualTo("00000000DEADBEEF"));
            Assert.That(clean.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer));
            Assert.That(clean.Players.Recorded.Count, Is.EqualTo(3));
            Assert.That(clean.Players.Recorded[2].Identity, Is.EqualTo(0));
            Assert.That(clean.Players.Identity, Is.EqualTo(0), "the world's identity kind is stored");
            Assert.That(clean.Players.Recorded[2].Id, Is.EqualTo("c"));
        }

        // Catches: a bad seed taking the players with it (or the other way round): each section costs only itself.
        [Test]
        public void Clean_ABadSectionCostsOnlyItself()
        {
            var rejected = new List<string>();

            WorldSave badSeed = Good();
            badSeed.WorldSeed = "nope";
            WorldSave clean = WorldSaveRules.Clean(badSeed, DefaultSeed, rejected);
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionSeed }));
            Assert.That(clean.WorldSeed, Is.EqualTo(WorldSaveRules.SeedToHex(DefaultSeed)), "the default seed");
            Assert.That(clean.Players.Recorded.Count, Is.EqualTo(3), "players untouched");

            WorldSave badPlayers = Good();
            badPlayers.Players.Kind = 9;   // an unknown kind: the one thing that rejects the whole section
            clean = WorldSaveRules.Clean(badPlayers, DefaultSeed, rejected);
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
            Assert.That(clean.Players, Is.Null, "the players section resets to 'none recorded'");
            Assert.That(clean.WorldSeed, Is.EqualTo("00000000DEADBEEF"), "seed untouched");

            WorldSave both = Good();
            both.WorldSeed = "";
            both.Players.Kind = -1;
            clean = WorldSaveRules.Clean(both, DefaultSeed, rejected);
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionSeed, WorldSaveRules.SectionPlayers }));
            Assert.That(clean.Players, Is.Null);
        }

        [Test]
        public void Clean_NullIsANewWorld_MissingPlayersIsNotAnError()
        {
            var rejected = new List<string> { "stale" };
            WorldSave fresh = WorldSaveRules.Clean(null, DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty);
            Assert.That(fresh.WorldSeed, Is.EqualTo(WorldSaveRules.SeedToHex(DefaultSeed)));
            Assert.That(fresh.Players, Is.Null);
            Assert.That(fresh.SchemaVersion, Is.EqualTo(WorldSave.CurrentSchemaVersion));

            WorldSave noPlayers = new WorldSave { WorldSeed = "00000000DEADBEEF" };
            WorldSave clean = WorldSaveRules.Clean(noPlayers, DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty, "a world with no players recorded yet is fine");
            Assert.That(clean.Players, Is.Null);
        }

        // Catches: a hand-edited save recording five players (the 4-player cap bypassed), or one bad entry costing the good ones.
        [Test]
        public void Players_DropOnlyTheBadEntries_AndNeverKeepMoreThanFour()
        {
            var rejected = new List<string>();

            WorldSave oneBad = Good();
            oneBad.Players.Recorded.Insert(1, P(""));
            oneBad.Players.Recorded.Insert(1, null);
            oneBad.Players.Recorded.Insert(1, P("a"));          // a repeat of the first
            oneBad.Players.Recorded.Insert(1, P("x", 77));      // an identity kind that does not exist
            oneBad.Players.Recorded.Insert(1, P("u", 1));       // another identity kind than the world's
            WorldSave clean = WorldSaveRules.Clean(oneBad, DefaultSeed, rejected);
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }), "named, so the store keeps the original as .corrupt");
            Assert.That(clean.Players.Recorded.ConvertAll(p => p.Id), Is.EqualTo(new[] { "a", "b", "c" }), "the good players kept, in order");

            WorldSave five = Good();
            five.Players.Recorded.Add(P("d"));
            five.Players.Recorded.Add(P("e"));
            clean = WorldSaveRules.Clean(five, DefaultSeed, rejected);
            Assert.That(clean.Players.Recorded.Count, Is.EqualTo(WorldPlayers.MaxRecorded), "the fifth is cut");
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));
        }

        // Catches (risk review): damage to the list turning the world into "no world yet", so the next start creates a new one (and a
        // Multiplayer world could come back as a Solo one). The kind is stored apart and survives however many records do.
        [Test]
        public void Players_TheKindSurvivesLostRecords()
        {
            var rejected = new List<string>();

            WorldSave multiOfOne = Good();
            multiOfOne.Players.Recorded = new List<WorldPlayerSave> { P("a") };
            WorldSave clean = WorldSaveRules.Clean(multiOfOne, DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty, "nothing was damaged: one record is a legal state for a world that lost players");
            Assert.That(clean.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer));
            Assert.That(clean.Players.Recorded.Count, Is.EqualTo(1));

            WorldSave noRecords = Good();
            noRecords.Players.Recorded.Clear();
            clean = WorldSaveRules.Clean(noRecords, DefaultSeed, rejected);
            Assert.That(clean.Players, Is.Not.Null, "the world is still there");
            Assert.That(clean.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer));

            WorldSave soloOfTwo = Good();
            soloOfTwo.Players.Kind = (int)WorldKind.Solo;
            clean = WorldSaveRules.Clean(soloOfTwo, DefaultSeed, rejected);
            Assert.That(clean.Players.Kind, Is.EqualTo((int)WorldKind.Solo));
            Assert.That(clean.Players.Recorded.ConvertAll(p => p.Id), Is.EqualTo(new[] { "a" }), "a Solo world keeps its first record only");
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));

            WorldSave solo = Good();
            solo.Players = new WorldPlayersSave { Kind = (int)WorldKind.Solo, Recorded = new List<WorldPlayerSave> { P("owner") } };
            Assert.That(WorldSaveRules.Clean(solo, DefaultSeed, rejected).Players.Kind, Is.EqualTo((int)WorldKind.Solo));
            Assert.That(rejected, Is.Empty);

            WorldSave nullList = Good();
            nullList.Players.Recorded = null;
            clean = WorldSaveRules.Clean(nullList, DefaultSeed, rejected);
            Assert.That(clean.Players.Kind, Is.EqualTo((int)WorldKind.Multiplayer), "a null list is a list with nothing in it");
            Assert.That(clean.Players.Recorded.Count, Is.EqualTo(0));
        }

        // Catches: unknown members of a newer build's file lost when this build cleans and writes the save back.
        [Test]
        public void Clean_KeepsUnknownMembers()
        {
            WorldSave good = Good();
            good.Extra = new Dictionary<string, object> { { "futureThing", 5 } };
            good.Players.Extra = new Dictionary<string, object> { { "futureField", "x" } };
            good.Players.Recorded[0].Extra = new Dictionary<string, object> { { "colour", "red" } };
            WorldSave clean = WorldSaveRules.Clean(good, DefaultSeed, new List<string>());
            Assert.That(clean.Extra["futureThing"], Is.EqualTo(5));
            Assert.That(clean.Players.Extra["futureField"], Is.EqualTo("x"));
            Assert.That(clean.Players.Recorded[0].Extra["colour"], Is.EqualTo("red"));
        }
    
        static SuspendedBag SuspBag(string id, params int[] carried)
        {
            var b = new SuspendedBag { Identity = 0, Id = id };
            b.Carried.AddRange(carried);
            return b;
        }

        static WorldSave WithSuspend(int floor = 5, int phase = (int)DivePhase.BankRoom)
        {
            WorldSave w = Good();
            w.Dive = new DiveSave { InDive = true, ClearedMask = 0b11111 };
            w.Bags = new BagsSave { Players = { new PlayerBagSave { Identity = 0, Id = "a", Gold = 6 } } };
            w.Suspend = new SuspendSave { Seed = 9u, Floor = floor, Phase = phase, PresentIds = { "Local:a" }, Bags = { SuspBag("a", 101, 102) } };
            return w;
        }

        // Catches: the suspend section being dropped by Clean, rejected when it is good, or counted as a failed dive; a bad bag record costing the section.
        [Test]
        public void Clean_Suspend_IsKept_AndIsNotAFailedDive()
        {
            var rejected = new List<string>();
            WorldSave clean = WorldSaveRules.Clean(WithSuspend(), DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty);
            Assert.That(clean.Suspend, Is.Not.Null);
            Assert.That(clean.Suspend.Bags[0].Carried, Is.EqualTo(new[] { 101, 102 }));
            Assert.That(clean.Dive.InDive, Is.True, "a bank suspend is not a failed dive");
            Assert.That(clean.Bags.Players[0].Gold, Is.EqualTo(6), "the home bags stay beside it");

            WorldSave oneBad = WithSuspend();
            oneBad.Suspend.Bags.Add(SuspBag("a", 101));   // a repeated id
            rejected.Clear();
            clean = WorldSaveRules.Clean(oneBad, DefaultSeed, rejected);
            Assert.That(clean.Suspend.Bags.Count, Is.EqualTo(1));
            Assert.That(rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }), "named so the original file is kept");
        }

        // Catches: a bad floor or phase in the suspend section taking the dive, bags or the rest of the file with it.
        [Test]
        public void BadSuspendFloor_DropsOnlySuspend()
        {
            foreach (WorldSave bad in new[] { WithSuspend(floor: 99), WithSuspend(phase: 7) })
            {
                var rejected = new List<string>();
                WorldSave clean = WorldSaveRules.Clean(bad, DefaultSeed, rejected);
                Assert.That(clean.Suspend, Is.Null);
                Assert.That(rejected, Is.EqualTo(new[] { TownSaveRules.SectionSuspend }));
                Assert.That(clean.Dive.ClearedMask, Is.EqualTo(0b11111), "the dive section is untouched");
                Assert.That(clean.Dive.InDive, Is.False, "and with no valid suspend the quit is the old failed dive");
                Assert.That(clean.Bags.Players[0].Gold, Is.EqualTo(6));
                Assert.That(clean.Players.Recorded.Count, Is.EqualTo(3));
            }
        }

        // Catches: an old save's dive section (no new fields) being rejected, so every old world would keep a .corrupt copy.
        [Test]
        public void OldDiveSection_WithoutTheNewFields_IsNotRejected()
        {
            WorldSave old = Good();
            old.Dive = new DiveSave { ClearedMask = 3, CheckpointReached = true };
            var rejected = new List<string>();
            WorldSave clean = WorldSaveRules.Clean(old, DefaultSeed, rejected);
            Assert.That(rejected, Is.Empty);
            Assert.That(clean.Dive.CheckpointHeld, Is.False);
            Assert.That(clean.Dive.PieceDepths, Is.Null);
            Assert.That(clean.Suspend, Is.Null);
        }

        // Catches (risk review): a section without a kind being read as Solo (the zero default) and the world quietly changing kind.
        [Test]
        public void Players_AMissingKindIsRejected_NeverReadAsSolo()
        {
            var rejected = new List<string>();
            WorldSave noKind = Good();
            noKind.Players = new WorldPlayersSave { Recorded = new List<WorldPlayerSave> { P("a") } };   // Kind left at its invalid default
            Assert.That(noKind.Players.Kind, Is.EqualTo(-1));
            Assert.That(WorldSaveRules.Clean(noKind, DefaultSeed, rejected).Players, Is.Null);
            Assert.That(rejected, Is.EqualTo(new[] { WorldSaveRules.SectionPlayers }));

            WorldSave noIdentityOnRecord = Good();
            noIdentityOnRecord.Players.Recorded = new List<WorldPlayerSave> { new WorldPlayerSave { Id = "a" }, P("b") };   // a record without its identity kind
            WorldSave clean = WorldSaveRules.Clean(noIdentityOnRecord, DefaultSeed, rejected);
            Assert.That(clean.Players.Recorded.ConvertAll(p => p.Id), Is.EqualTo(new[] { "b" }), "that one record is damaged, not Local");
        }

        // Catches: a malformed world id kept as the file's name, and the picker header misreporting a world.
        [Test]
        public void WorldId_IsKeptOnlyWhenValid_AndSummarizeReadsTheHeader()
        {
            WorldSave save = Good();
            save.WorldId = "../evil";
            save.CreatedAtUtc = "2026-10-30T09:00:00Z";
            save.SavedAtUtc = "2026-10-30T10:00:00Z";
            save.Players.Recorded[0].Name = "Ann";
            WorldSave clean = WorldSaveRules.Clean(save, DefaultSeed, new List<string>());
            Assert.That(clean.WorldId, Is.EqualTo(""), "blanked, the store fills the real one");
            Assert.That(clean.CreatedAtUtc, Is.EqualTo("2026-10-30T09:00:00Z"));

            clean.WorldId = "0123456789abcdef0123456789abcdef";
            WorldSummary summary = WorldSaveRules.Summarize(clean);
            Assert.That(summary.Id, Is.EqualTo(clean.WorldId));
            Assert.That(summary.Kind, Is.EqualTo(WorldKind.Multiplayer));
            Assert.That(summary.Identity, Is.EqualTo(0));
            Assert.That(summary.PlayerNames, Is.EqualTo(new[] { "Ann", "?", "?" }));
            Assert.That(summary.LastPlayedUtc, Is.EqualTo("2026-10-30T10:00:00Z"));
            Assert.That(summary.Matches(WorldKind.Multiplayer, IdentityKind.Local), Is.True);
            Assert.That(summary.Matches(WorldKind.Multiplayer, IdentityKind.Ugs), Is.False, "Local and online worlds never cross");
            Assert.That(summary.Matches(WorldKind.Solo, IdentityKind.Local), Is.False);

            Assert.That(WorldSaveRules.Summarize(new WorldSave()), Is.Null, "no players section: not a world to pick");
            Assert.That(WorldSaveRules.Summarize(null), Is.Null);
        }

        [Test]
        public void StartRefusals_AllHaveAMessage()
        {
            foreach (WorldPlayerVerdict verdict in (WorldPlayerVerdict[])System.Enum.GetValues(typeof(WorldPlayerVerdict)))
            {
                if (verdict == WorldPlayerVerdict.Ok) { Assert.That(WorldStartText.ToastKey(verdict), Is.Null); continue; }
                Assert.That(WorldStartText.ToastKey(verdict), Does.StartWith("world."), verdict.ToString());
            }
            Assert.That(WorldStartText.ToastKey(WorldPlayerVerdict.NotEnoughPlayers), Is.EqualTo("world.need_two_players"));
            Assert.That(WorldStartText.ToastKey(WorldPlayerVerdict.WrongIdentityKind), Is.EqualTo("world.wrong_kind"));
        }
    }
}
