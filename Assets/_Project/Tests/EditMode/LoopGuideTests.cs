// Authority: none (pure EditMode tests). PL-2 task 2.1: LoopGuide.Next picks the one step from a small snapshot; GuideText has words for every step.
// Catches: a guide that points at the stair when a frame waits in stock, that hides the numbers, or a step kind added with no words.
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public sealed class LoopGuideTests
    {
        [Test]
        public void NewWorld_PointsAtTheStair()
        {
            GuideStep s = LoopGuide.Next(default);
            Assert.That(s.Kind, Is.EqualTo(GuideKind.GoToStair));
            Assert.That(s.PointsAtStair, Is.True);
        }

        [Test]
        public void InDive_PiecesOnFloor_SaysHowManyLeft()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { InDive = true, Floor = 2, PiecesLeftOnFloor = 2 });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.PiecesLeftHere));
            Assert.That(s.Count, Is.EqualTo(2));
            Assert.That(GuideText.For(s), Does.Contain("2"));
            Assert.That(GuideText.For(LoopGuide.Next(new GuideSnapshot { InDive = true, Floor = 2, PiecesLeftOnFloor = 1 })), Does.StartWith("1 piece left"));
        }

        [Test]
        public void InDive_NoneLeft_PointsAtTheStairDown()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { InDive = true, Floor = 1, PiecesLeftOnFloor = 0 });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.StairDown));
            Assert.That(s.PointsAtStair, Is.Null, "in a dive no town beacon applies");
        }

        [Test]
        public void HomeWithFrame_PointsAtBuildPoint()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { FrameInStock = true, PartsInStock = 1 });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.GoToBuildPoint));
            Assert.That(s.PointsAtStair, Is.False);
        }

        [Test]
        public void FramePlaced_PartsInStock_SaysPlaceParts()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { FramePlaced = true, PartsMissing = 3, PartsInStock = 2 });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.PlaceParts));
            Assert.That(s.Count, Is.EqualTo(2));
            Assert.That(GuideText.For(s), Does.Contain("2"));
        }

        [Test]
        public void FramePlaced_NothingInStock_SaysDiveForMore()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { FramePlaced = true, PartsMissing = 3 });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.DiveForMore));
            Assert.That(s.Count, Is.EqualTo(3));
            Assert.That(s.PointsAtStair, Is.True);
        }

        [Test]
        public void BakerInStock_BuildingDone_SaysPlaceTheBaker()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { FramePlaced = true, PartsMissing = 0, BakerInStock = true });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.PlaceTheBaker));
            Assert.That(LoopGuide.Next(new GuideSnapshot { FramePlaced = true }).Kind, Is.EqualTo(GuideKind.FindTheBaker), "no baker yet: dive for him");
            Assert.That(LoopGuide.Next(new GuideSnapshot { FramePlaced = true, BakerPlaced = true }).Kind, Is.EqualTo(GuideKind.MeetTheWishes), "housed but no loaves: his wishes");
        }

        [Test]
        public void ServiceOpen_SaysDiveWithLoaves()
        {
            GuideStep s = LoopGuide.Next(new GuideSnapshot { FramePlaced = true, BakerPlaced = true, ServiceOpen = true });
            Assert.That(s.Kind, Is.EqualTo(GuideKind.DiveWithLoaves));
            Assert.That(s.PointsAtStair, Is.True);
        }

        [Test]
        public void EveryStep_HasWords()
        {
            foreach (GuideKind k in System.Enum.GetValues(typeof(GuideKind)))
            {
                Assert.That(GuideText.Kinds, Does.Contain(k), k + " has no entry in the table");
                foreach (int n in new[] { 0, 1, 3 }) Assert.That(GuideText.For(new GuideStep(k, n)), Is.Not.Empty.And.Not.Contain("{0}"), k + " with " + n);
            }
            Assert.That(GuideText.Kinds.Count(), Is.EqualTo(System.Enum.GetValues(typeof(GuideKind)).Length));
        }

        // The town fill: a real TownState gives the numbers a HUD needs (frame in stock before anything stands, then the parts, then the baker).
        [Test]
        public void FromTown_FollowsTheBakeryFromFrameToLoaves()
        {
            var town = new TownState(HollowmereLayout.Create());
            Assert.That(LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0)).Kind, Is.EqualTo(GuideKind.GoToStair));
            town.AddPiece(PieceCatalog.FrameId);
            Assert.That(LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0)).Kind, Is.EqualTo(GuideKind.GoToBuildPoint));
            Assert.That(town.PlaceFrame(TownBuildings.Bakery, "F02", 0), Is.EqualTo(PlaceResult.Ok));
            GuideStep missing = LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0));
            Assert.That(missing.Kind, Is.EqualTo(GuideKind.DiveForMore));
            Assert.That(missing.Count, Is.EqualTo(3));
            town.AddPiece(PieceCatalog.OvenId); town.AddPiece(PieceCatalog.SpareFrameId);
            GuideStep parts = LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0));
            Assert.That(parts.Kind, Is.EqualTo(GuideKind.PlaceParts));
            Assert.That(parts.Count, Is.EqualTo(1), "a spare frame is no use to the standing bakery");
            foreach (int p in new[] { PieceCatalog.DoorId, PieceCatalog.SignId }) town.AddPiece(p);
            foreach (string part in new[] { "oven", "door", "sign" }) Assert.That(town.PlacePart(TownBuildings.Bakery, part), Is.EqualTo(PlaceResult.Ok));
            Assert.That(LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0)).Kind, Is.EqualTo(GuideKind.FindTheBaker));
            town.AddResident(TownBuildings.BakerResident);
            Assert.That(LoopGuide.Next(GuideSnapshot.FromTown(town, false, 0, 0)).Kind, Is.EqualTo(GuideKind.PlaceTheBaker));
            Assert.That(town.PlaceResident(TownBuildings.Bakery), Is.EqualTo(PlaceResult.Ok));
            Assert.That(LoopGuide.Next(GuideSnapshot.FromTown(town, true, 3, 2)).Kind, Is.EqualTo(GuideKind.PiecesLeftHere), "in a dive the dive numbers win");
        }
    }
}
