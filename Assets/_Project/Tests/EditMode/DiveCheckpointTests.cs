// Tests for SleepingCalamity.Core.DiveCheckpoint (spec decisions 13, 31, 38).
using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class DiveCheckpointTests
    {
        [Test]
        public void WithoutACheckpoint_OnlyFloorOne()
        {
            var cp = new DiveCheckpoint();
            Assert.That(cp.CanStartOn(1, false), Is.True);
            Assert.That(cp.CanStartOn(3, false), Is.False);
            Assert.That(cp.CanStartOn(5, false), Is.False);
            Assert.That(cp.TryStart(5, false), Is.False);
        }

        [Test]
        public void GoingHomeFromTheBank_AllowsAnyFloorUpToTheBank_Once()
        {
            var cp = new DiveCheckpoint();
            cp.OnWentHomeFromBank();
            Assert.That(cp.Held, Is.True);
            for (int f = 1; f <= UndercroftPlan.BankFloor; f++) Assert.That(cp.CanStartOn(f, false), Is.True, $"floor {f}");
            Assert.That(cp.CanStartOn(6, false), Is.False, "never past the bank");
            Assert.That(cp.TryStart(3, false), Is.True);
            Assert.That(cp.Held, Is.False, "starting uses the checkpoint up");
            Assert.That(cp.CanStartOn(3, false), Is.False, "the next dive starts on floor 1 again");
        }

        [Test]
        public void StartingOnFloorOne_AlsoUsesTheCheckpoint()
        {
            var cp = new DiveCheckpoint();
            cp.OnWentHomeFromBank();
            Assert.That(cp.TryStart(1, false), Is.True);
            Assert.That(cp.Held, Is.False);
        }

        [Test]
        public void AfterTheBoss_TheBankIsAlwaysOpen_WithoutUsingTheCheckpoint()
        {
            var cp = new DiveCheckpoint();
            Assert.That(cp.TryStart(UndercroftPlan.BankFloor, true), Is.True);
            cp.OnWentHomeFromBank();
            Assert.That(cp.TryStart(UndercroftPlan.BankFloor, true), Is.True);
            Assert.That(cp.Held, Is.True, "the shortcut doesn't spend the checkpoint");
            Assert.That(cp.CanStartOn(3, true), Is.True, "the held checkpoint still allows a floor pick");
        }

        [Test]
        public void Restore_BringsBackTheSavedState()
        {
            var cp = new DiveCheckpoint();
            cp.Restore(true);
            Assert.That(cp.CanStartOn(4, false), Is.True);
        }
    }
}
