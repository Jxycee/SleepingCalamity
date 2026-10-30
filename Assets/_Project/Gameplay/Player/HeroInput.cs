// Not networked. The one place the hero components look up the local input and the pause flag.
// Every hero component takes an optional injected IInputService (HeroLocomotion.Input, TargetLockController.Input) and falls back to
// ServiceRoots.App here; two heroes in one process (the 2-player PlayMode test) each get their own, so they never read the same stick.
using SleepingCalamity.Runtime;

namespace SleepingCalamity.Gameplay
{
    public static class HeroInput
    {
        public static IInputService ResolveDefault()
        {
            ServiceRegistry app = ServiceRoots.App;
            return app != null && app.TryGet(out IInputService input) ? input : null;
        }

        public static bool IsPaused()
        {
            ServiceRegistry game = ServiceRoots.Game;
            return game != null && game.TryGet(out IPauseState pause) && pause.Paused;
        }
    }
}
