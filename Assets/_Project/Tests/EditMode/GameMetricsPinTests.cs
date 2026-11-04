// Tests/EditMode/GameMetricsPinTests.cs: each GameMetrics mirror still equals the Presentation/Gameplay constant it copies (metrics gym plan, Task 1). Not Core-only: needs Presentation and Gameplay.
using NUnit.Framework;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;

namespace SleepingCalamity.Tests.EditMode
{
    public class GameMetricsPinTests
    {
        [Test] public void CameraMirrors_EqualTheRig()
        {
            Assert.That(GameMetrics.CameraFreeBoom, Is.EqualTo(HeroCameraRig.FreeBoom));
            Assert.That(GameMetrics.CameraLockBoom, Is.EqualTo(HeroCameraRig.LockBoom));
            Assert.That(GameMetrics.CameraFov, Is.EqualTo(HeroCameraRig.Fov));
            Assert.That(GameMetrics.CameraPitchDeg, Is.EqualTo(HeroLocomotion.FreePitchDownDeg));   // HeroLocomotion.cs:17
        }

        [Test] public void CellarMirrors_EqualTheBuilder()
        {
            Assert.That(GameMetrics.DoorWidth, Is.EqualTo(CellarFloorBuilder.DoorWidth));
            Assert.That(GameMetrics.DoorHeight, Is.EqualTo(CellarFloorBuilder.DoorHeight));
            Assert.That(GameMetrics.WallHeight, Is.EqualTo(CellarFloorBuilder.WallHeight));
            Assert.That(GameMetrics.RoomVaultCrown, Is.EqualTo(CellarFloorBuilder.RoomSpring + CellarFloorBuilder.VaultRise).Within(1e-4f));      // Art.cs:84 crown = spring + rise
            Assert.That(GameMetrics.CorridorVaultCrown, Is.EqualTo(CellarFloorBuilder.CorridorSpring + CellarFloorBuilder.VaultRise).Within(1e-4f));
            Assert.That(GameMetrics.CellMeters, Is.EqualTo((float)CellarFloorBuilder.CellSize));
        }
    }
}
