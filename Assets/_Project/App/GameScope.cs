// Not networked; runs locally on every peer. Scene-scope services for one gameplay scene (an Arena_* scene).
// Lives on a root object named "GameScope" in the gameplay scene. Registers IPauseState and publishes its registry as ServiceRoots.Game.
// Execution order -1500 so it is awake before any other scene object (spawn points, camera rigs, players) looks it up.
// No scene carries one yet: the first gameplay system that needs scene-scope services adds it to its scene builder.
using SleepingCalamity.Runtime;
using UnityEngine;

namespace SleepingCalamity.App
{
    [DefaultExecutionOrder(-1500)]
    public sealed class GameScope : MonoBehaviour
    {
        public ServiceRegistry Services { get; private set; }

        private void Awake()
        {
            Services = new ServiceRegistry();
            Services.Register<IPauseState>(new PauseState());
            ServiceRoots.SetGame(Services);
        }

        private void OnDestroy()
        {
            if (Services == null) return;
            var mine = Services;
            Services = null;
            if (ServiceRoots.Game == mine) ServiceRoots.SetGame(null);
            mine.Dispose();
        }
    }
}
