// Runs every validator and provides SleepingCalamity/Validate. Add a validator here when a builder step gets an invariant worth keeping.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class ValidationRunner
    {
        public static List<string> Run()
        {
            var all = new List<string>();
            Add(all, "NetworkPrefabs", Validators.NetworkPrefabs());
            Add(all, "BuildScenes", Validators.BuildScenes());
            Add(all, "InputActions", Validators.InputActions());
            Add(all, "Manifest", Validators.Manifest());
            Add(all, "ProjectSettings", Validators.ProjectSettingsInvariants());
            Add(all, "MeshReferences", Validators.MeshReferences());
            Add(all, "MissingScripts", Validators.MissingScripts());
            Add(all, "BootstrapReferences", Validators.BootstrapReferences());
            Add(all, "SceneNetworkObjectHashes", Validators.SceneNetworkObjectHashes());
            return all;
        }

        [MenuItem("SleepingCalamity/Validate")]
        public static void RunMenu()
        {
            List<string> errors = Run();
            if (errors.Count == 0) { Debug.Log("SC VALIDATE PASS"); return; }
            Debug.Log("SC VALIDATE FAIL errors=" + errors.Count);
            foreach (string error in errors) Debug.Log("SC VALIDATE ERROR " + error);
        }

        static void Add(List<string> into, string label, List<string> errors)
        {
            foreach (string error in errors) into.Add(label + ": " + error);
        }
    }
}
