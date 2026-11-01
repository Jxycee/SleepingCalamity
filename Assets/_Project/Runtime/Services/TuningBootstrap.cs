// Runtime/Services/TuningBootstrap.cs: loads the optional tuning files once at startup, before the first scene (Core/Tuning/TuningStore.cs has the rules).
// Folders, in order, later wins: <StreamingAssets>/Tuning, then <persistentDataPath>/Tuning (the user's mod folder). Missing folders are fine.
// Authority: none (every peer loads its own copy at startup; host and clients must have the same files for the numbers to agree).
using SleepingCalamity.Core.Tuning;
using System.IO;
using UnityEngine;

namespace SleepingCalamity.Runtime
{
    public static class TuningBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Load()
        {
            TuningStore.Log = Debug.LogWarning;
            TuningStore.Reset();   // a domain-reload-less play session must not keep the last run's overrides
            int n = TuningStore.LoadFolders(Path.Combine(Application.streamingAssetsPath, "Tuning"), Path.Combine(Application.persistentDataPath, "Tuning"));
            if (n > 0) Debug.Log("[Tuning] read " + n + " tuning file(s)");
        }
    }
}
