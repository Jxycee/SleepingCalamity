// Runs AetherStrand's AI diagnostics bundle (v2.6 ExportBundle) on the Tobin AetherStrand prefab, headless.
//   env SC_OUT_DIR (default ./hair_diag), SC_PREFAB (default HairAB/Tobin_AetherStrand.prefab)
//   unity.ps1 exec -Method SleepingCalamity.EditorTools.TobinHairDiag.Run
// Trial only (hair A/B). Never writes under Assets/: the plugin's default folder would.
using System.IO;
using AetherStrand.Editor;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class TobinHairDiag
    {
        static string Env(string k, string d) { var v = System.Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        public static void Run()
        {
            string dir = Path.GetFullPath(Env("SC_OUT_DIR", "hair_diag"));
            string path = Env("SC_PREFAB", TobinHairABBuilder.AetherPrefab);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new FileNotFoundException(path);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var rep = AetherStrandAiDiagnostics.ExportBundle(inst, dir);
                Debug.Log("SC HAIRDIAG " + dir + "\n" + AetherStrandAiDiagnostics.FormatMarkdownSummary(rep));
            }
            finally { Object.DestroyImmediate(inst); }
        }
    }
}
