// Moves builder-generated meshes out of a scene file (where Unity writes each as hex text) into mesh assets under Content/Generated/<area>.
// One pass over the built scene, just before it is saved: every MeshFilter / MeshCollider mesh that is not already an asset and is over a size floor becomes
// <folder>/<mesh name>.asset (created, or overwritten in place so the .meta GUID stays stable), and the scene's references are pointed at the asset.
// Assets in the folder that this build did not produce are deleted. Same mesh instance used twice stays one asset; two different instances that share a name
// get _2, _3 in hierarchy order (deterministic). Builds are deterministic, so a second build rewrites identical bytes.
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    internal static class GeneratedMeshes
    {
        public const string Root = "Assets/_Project/Content/Generated";
        // Raw payload estimate (vertices x 32 B + indices x 4 B). About 20 KB as scene text; the lane treads and card tiles are moved, small props stay embedded.
        const int MinBytes = 6000;

        public static void Externalize(Scene scene, string area)
        {
            string folder = Root + "/" + area;
            BuilderUtil.EnsureFolder(folder);
            var byInstance = new Dictionary<Mesh, Mesh>();
            var usedPaths = new HashSet<string>();
            var nameCount = new Dictionary<string, int>();
            long bytes = 0; int moved = 0, kept = 0;
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                foreach (MeshFilter mf in rootGo.GetComponentsInChildren<MeshFilter>(true))
                    mf.sharedMesh = Map(mf.sharedMesh);
                foreach (MeshCollider mc in rootGo.GetComponentsInChildren<MeshCollider>(true))
                    mc.sharedMesh = Map(mc.sharedMesh);
            }
            int deleted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!usedPaths.Contains(p) && AssetDatabase.DeleteAsset(p)) deleted++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SC GENERATEDMESHES " + area + ": " + moved + " mesh assets (" + (bytes / 1024) + " KB payload), " + kept + " small meshes left embedded, " + deleted + " stale assets deleted");

            Mesh Map(Mesh m)
            {
                if (m == null || AssetDatabase.Contains(m)) return m;
                if (byInstance.TryGetValue(m, out Mesh done)) return done;
                long size = (long)m.vertexCount * 32 + IndexCount(m) * 4L;
                if (size < MinBytes) { kept++; byInstance[m] = m; return m; }
                string stem = Sanitize(string.IsNullOrEmpty(m.name) ? "Mesh" : m.name);
                nameCount.TryGetValue(stem, out int n); nameCount[stem] = ++n;
                string path = folder + "/" + (n == 1 ? stem : stem + "_" + n) + ".asset";
                usedPaths.Add(path);
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh result;
                if (existing == null) { AssetDatabase.CreateAsset(m, path); result = m; }
                else { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); result = existing; }   // the fresh mesh is left unreferenced (never saved)
                byInstance[m] = result; bytes += size; moved++;
                return result;
            }
        }

        static long IndexCount(Mesh m)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i);
            return n;
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            return sb.ToString();
        }
    }
}
