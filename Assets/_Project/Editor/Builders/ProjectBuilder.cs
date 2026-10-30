// The single build entry point and SleepingCalamity/Build All. Every scene and prefab comes from a builder step here;
// add a Step per builder as they land (steps run in order and all run even after a failure). Tools/unity.ps1 build and check read the SC BUILDALL lines.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class ProjectBuilder
    {
        [MenuItem("SleepingCalamity/Build All")]
        public static void BuildAllMenu()
        {
            // A builder may replace the open scene, so offer to save it first. Cancel aborts the build.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("SC BUILDALL CANCELLED");
                return;
            }
            Execute();
        }

        // Batchmode entry: -executeMethod SleepingCalamity.EditorTools.ProjectBuilder.BuildAll, always together with -quit.
        // Success returns normally, so -quit prints "Exiting batchmode successfully". Failure exits 1 in batchmode only.
        public static void BuildAll()
        {
            bool ok = Execute();
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        // Runs every step even after a failure and collects the errors.
        public static bool RunAll(List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            Step(errors, "ProjectSettings", ProjectSettingsBuilder.Apply);
            Step(errors, "AnimationImport", AnimationImportBuilder.Build);
            Step(errors, "Prefabs", PrefabBuilder.Build);
            Step(errors, "NetworkPrefabs", NetworkPrefabsBuilder.Build);
            Step(errors, "Scenes", SceneBuilder.BuildAll);
            Step(errors, "LookTest", LookTestBuilder.Build);
            Step(errors, "BeautyCorner", BeautyCornerBuilder.Build);
            Step(errors, "BuildSettings", SceneBuilder.ApplyBuildSettings);
            Step(errors, "Save", AssetDatabase.SaveAssets);
            Step(errors, "Validate", () => errors.AddRange(ValidationRunner.Run()));   // after Save: the validators read what is on disk
            return errors.Count == 0;
        }

        static bool Execute()
        {
            var errors = new List<string>();
            if (RunAll(errors))
            {
                Debug.Log("SC BUILDALL PASS");
                return true;
            }
            Debug.Log("SC BUILDALL FAIL errors=" + errors.Count);
            foreach (string error in errors) Debug.Log("SC BUILDALL ERROR " + error);
            return false;
        }

        static void Step(List<string> errors, string label, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                errors.Add(label + ": " + e.GetType().Name + ": " + e.Message);
                Debug.LogException(e);
            }
        }
    }
}
