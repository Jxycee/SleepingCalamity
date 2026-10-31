// Authority: none (local, per-player preference). Optional combat assists. The ground footprint under an enemy attack (EnemyVisual) is an assist now,
// off by default (user, 2026-10-30): attacks read from the enemy's own wind-up. Remembered per machine in PlayerPrefs; the arena's F2 toggles it.
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public static class CombatAssists
    {
        private const string GroundTelegraphKey = "sc.assist.groundTelegraph";
        private static int s_ground = -1;   // -1 not read yet

        public static bool GroundTelegraph
        {
            get
            {
                if (s_ground < 0) s_ground = PlayerPrefs.GetInt(GroundTelegraphKey, 0) != 0 ? 1 : 0;
                return s_ground == 1;
            }
            set
            {
                s_ground = value ? 1 : 0;
                PlayerPrefs.SetInt(GroundTelegraphKey, s_ground);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_ground = -1; }
    }
}
