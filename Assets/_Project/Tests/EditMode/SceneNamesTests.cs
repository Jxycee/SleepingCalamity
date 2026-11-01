using NUnit.Framework;
using SleepingCalamity.Core;

namespace SleepingCalamity.Tests.EditMode
{
    public class SceneNamesTests
    {
        [Test]
        public void SceneNames_BuildOrder_BootstrapFirst()
        {
            Assert.That(SceneNames.BuildOrder, Is.EqualTo(new[] { "Bootstrap", "Lobby", "Arena_Greybox", "Arena_Combat", "Arena_Dive", "Hollowmere" }));
        }

        [Test]
        public void SceneNames_IsGameplay_ArenaScenesAndTheTown()
        {
            Assert.That(SceneNames.IsGameplay(SceneNames.Hollowmere), Is.True, "M6 S3: the town is the session scene");
            Assert.That(SceneNames.IsGameplay("hollowmere"), Is.False);
            Assert.That(SceneNames.IsGameplay(SceneNames.ArenaGreybox), Is.True);
            Assert.That(SceneNames.IsGameplay("Arena_Other"), Is.True);
            Assert.That(SceneNames.IsGameplay(SceneNames.Bootstrap), Is.False);
            Assert.That(SceneNames.IsGameplay(SceneNames.Lobby), Is.False);
            Assert.That(SceneNames.IsGameplay("arena_greybox"), Is.False);
            Assert.That(SceneNames.IsGameplay(""), Is.False);
            Assert.That(SceneNames.IsGameplay(null), Is.False);
        }
    }
}
