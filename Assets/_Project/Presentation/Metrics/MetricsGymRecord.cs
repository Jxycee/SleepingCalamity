// Presentation/Metrics/MetricsGymRecord.cs: the metrics gym scene's copy of GameMetrics.Rows(), so a designer can read the numbers beside the lanes (metrics gym plan, Task 7). Authority: none (static scene data).
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class MetricsGymRecord : MonoBehaviour
    {
        public string[] Keys = new string[0];
        public float[] Values = new float[0];
        public string[] Units = new string[0];
    }
}
