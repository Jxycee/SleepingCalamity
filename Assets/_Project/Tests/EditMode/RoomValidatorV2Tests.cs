// Tests/EditMode/RoomValidatorV2Tests.cs: the state-aware v2 room validator, incl. the red-team F7 softlock cases (metrics gym plan, Task 4). Core-only.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class RoomValidatorV2Tests
    {
        // 5 x 3, x right, y down. Doors (0,1) = index 0 and (2,2) = index 1; chest (4,2); spawns (0,0) and (0,2).
        static readonly string[] Rows = { "s....", "D....", "s.D.C" };
        static CellarRoom Base() => CellarRoom.Parse("hall5x3", "Hall", RoomKind.Combat, SubTheme.Larder, "hall", Rows);

        static GimmickRegion Water(params int[] values) => new GimmickRegion("w", 'w', GimmickKind.Water, values);

        static RoomDataV2 Make(string[] levels = null, string[] regionRows = null, GimmickRegion[] regions = null, StairCell[] stairs = null,
            ToolSocket[] sockets = null, DoorV2[] doors = null, int version = RoomDataV2.SchemaVersion) =>
            RoomDataV2.Author(Base(), levels, regionRows, regions, stairs, sockets, doors, RoomAffordance.None, version);

        // A valid gimmick room: water over (3,0) and (3,1) cycling 0 / 80 cm, a Control at (1,0), the door at (2,2) is a gate that needs the water at state 0.
        static RoomDataV2 Gated(ToolVerb tool = ToolVerb.None, string controlRegion = "w") => Make(null,
            new[] { "...w.", "...w.", "....." }, new[] { Water(0, 80) }, null,
            new[] { new ToolSocket("valve", 1, 0, CellSide.South, SocketPurpose.Control, tool, controlRegion) },
            new[] { new DoorV2(1, DoorType.Gate, 0, ToolVerb.None, "w", 0) });

        static string Joined(List<string> errs) => string.Join(" | ", errs);

        [Test] public void EveryPoolRoom_UpgradesAndValidates()
        {
            Assert.That(RoomValidatorV2.ValidateAll(CellarRoomPool.All.Select(RoomDataV2.FromV1)), Is.Empty);
            Assert.That(CellarBakeryPool.All.Count, Is.GreaterThan(0));
            Assert.That(RoomValidatorV2.ValidateAll(CellarBakeryPool.All.Select(RoomDataV2.FromV1)), Is.Empty);
        }

        [Test] public void PlainRoom_IsValid() => Assert.That(RoomValidatorV2.Validate(Make()), Is.Empty);

        [Test] public void GatedRoom_IsValid() => Assert.That(RoomValidatorV2.Validate(Gated()), Is.Empty, Joined(RoomValidatorV2.Validate(Gated())));

        [Test] public void StateCount_IsTheProductOfRegionStates()
        {
            Assert.That(RoomValidatorV2.StateCount(Make()), Is.EqualTo(1));
            Assert.That(RoomValidatorV2.StateCount(Gated()), Is.EqualTo(2));
        }

        [Test] public void UnknownVersion_IsRejected()
        {
            var errs = RoomValidatorV2.Validate(Make(version: 3));
            Assert.That(errs.Count, Is.EqualTo(1));
            Assert.That(errs[0], Does.StartWith("hall5x3: ").And.Contain("version 3"));
        }

        [Test] public void DoorAboveLevelZero_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Make(new[] { "00000", "10000", "00000" }));
            Assert.That(errs.Any(e => e.Contains("door")), Is.True, Joined(errs));
        }

        [Test] public void LevelTwo_IsNotValidatedYet()
        {
            var errs = RoomValidatorV2.Validate(Make(new[] { "00002", "00000", "00000" }));
            Assert.That(errs.Any(e => e.Contains("level 2")), Is.True, Joined(errs));
        }

        [Test] public void StairOnAChest_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Make(new[] { "00000", "00001", "00000" }, stairs: new[] { new StairCell(4, 2, CellSide.North) }));
            Assert.That(errs.Any(e => e.Contains("stair") && e.Contains("chest")), Is.True, Joined(errs));
        }

        [Test] public void StairWithoutAHigherNeighbour_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Make(stairs: new[] { new StairCell(1, 1, CellSide.East) }));
            Assert.That(errs.Any(e => e.Contains("stair") && e.Contains("one level higher")), Is.True, Joined(errs));
        }

        [Test] public void ControlForAnotherRoomsRegion_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Gated(ToolVerb.None, "ghost"));
            Assert.That(errs.Any(e => e.Contains("ghost")), Is.True, Joined(errs));
        }

        [Test] public void ChestThatGoesDeep_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Make(null, new[] { ".....", ".....", "....w" }, new[] { Water(0, 80) }));
            Assert.That(errs.Any(e => e.Contains("chest") && e.Contains("(4,2)")), Is.True, Joined(errs));
        }

        [Test] public void DeepCell_WithNoStandableNeighbour_IsAnError()
        {
            // (2,1) is walled in by pillars above and below and by deep cells either side
            CellarRoom pillared = CellarRoom.Parse("pits", "Pits", RoomKind.Combat, SubTheme.Larder, "hall", new[] { "s.P.s", "D...D", ".CP.." });
            RoomDataV2 room = RoomDataV2.Author(pillared, null, new[] { ".....", ".www.", "....." }, new[] { Water(0, 80) }, null, null, null, RoomAffordance.None);
            var errs = RoomValidatorV2.Validate(room);
            Assert.That(errs.Any(e => e.Contains("(2,1)") && e.Contains("standable")), Is.True, Joined(errs));
        }

        [Test] public void RegionThatChangesNothing_IsAnError()
        {
            var errs = RoomValidatorV2.Validate(Make(null, new[] { "...w.", "...w.", "....." }, new[] { Water(0, 30) }));
            Assert.That(errs.Any(e => e.Contains("region w")), Is.True, Joined(errs));
        }

        [Test] public void BalconyTrap_IsASoftlock()
        {
            // The east column is a balcony one level up; its only way up is the stair at (3,0), which floods to 260 cm; the only Control sits on the balcony.
            var room = Make(new[] { "00001", "00001", "00001" }, new[] { "...w.", ".....", "....." }, new[] { Water(0, 260) },
                new[] { new StairCell(3, 0, CellSide.East) },
                new[] { new ToolSocket("valve", 4, 0, CellSide.West, SocketPurpose.Control, ToolVerb.None, "w") });
            var errs = RoomValidatorV2.Validate(room);
            Assert.That(errs.Count, Is.EqualTo(1), Joined(errs));
            Assert.That(errs[0], Does.StartWith("hall5x3: softlock: from (").And.Contain("cannot be reached"));
        }

        [Test] public void StairWorks_WhenTheStairNeverFloods()
        {
            // the same balcony with the water kept to the room's centre-south: dry stair, Control reachable, and the hero can come back down
            var room = Make(new[] { "00001", "00001", "00001" }, new[] { ".....", "...w.", "....." }, new[] { Water(0, 80) },
                new[] { new StairCell(3, 0, CellSide.East) },
                new[] { new ToolSocket("valve", 4, 0, CellSide.West, SocketPurpose.Control, ToolVerb.None, "w") });
            Assert.That(RoomValidatorV2.Validate(room), Is.Empty, Joined(RoomValidatorV2.Validate(room)));
        }

        [Test] public void ToolControl_NeedsTheTool()
        {
            RoomDataV2 room = Gated(ToolVerb.Sluice);
            Assert.That(RoomValidatorV2.Validate(room, new[] { ToolVerb.Sluice }), Is.Empty, Joined(RoomValidatorV2.Validate(room, new[] { ToolVerb.Sluice })));
            var without = RoomValidatorV2.Validate(room);
            Assert.That(without.Any(e => e.Contains("softlock")), Is.True, Joined(without));
            Assert.That(RoomValidatorV2.Validate(room, new[] { ToolVerb.Bellows }).Any(e => e.Contains("softlock")), Is.True);
        }

        [Test] public void Validate_IsDeterministic() =>
            Assert.That(RoomValidatorV2.Validate(Gated(ToolVerb.Sluice)), Is.EqualTo(RoomValidatorV2.Validate(Gated(ToolVerb.Sluice))));
    }
}
