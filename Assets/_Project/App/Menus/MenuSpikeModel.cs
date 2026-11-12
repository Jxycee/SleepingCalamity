// Not networked; local to each peer. The plain C# end of the PL-3 UI Toolkit spike (task 3.0): the view calls Activate() when the spike button is pressed
// and the tests read Activations. The real front door models (FrontDoorModel, later) follow the same shape: no UnityEngine types in the model.
using System;

namespace SleepingCalamity.App
{
    public sealed class MenuSpikeModel
    {
        public int Activations { get; private set; }

        public event Action Activated;

        public void Activate()
        {
            Activations++;
            Activated?.Invoke();
        }
    }
}
