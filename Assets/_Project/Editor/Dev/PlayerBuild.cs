// Editor-only. Builds a non-development Windows player from the scenes in EditorBuildSettings (run ProjectBuilder.BuildAll first so the list is current).
// Output folder comes from the SC_PLAYER_OUT environment variable (outside the repo); exe is SleepingCalamity.exe. Run: unity.ps1 exec -Method SleepingCalamity.EditorTools.PlayerBuild.BuildWindows
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace SleepingCalamity.EditorTools
{
    public static class PlayerBuild
    {
        public static void BuildWindows()
        {
            string dir = Environment.GetEnvironmentVariable("SC_PLAYER_OUT");
            if (string.IsNullOrEmpty(dir)) { UnityEngine.Debug.LogError("PlayerBuild: set SC_PLAYER_OUT"); EditorApplication.Exit(1); return; }
            Directory.CreateDirectory(dir);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = Path.Combine(dir, "SleepingCalamity.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None };
            BuildReport r = BuildPipeline.BuildPlayer(opts);
            UnityEngine.Debug.Log("PlayerBuild: " + r.summary.result + " errors=" + r.summary.totalErrors + " size=" + r.summary.totalSize);
            EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
