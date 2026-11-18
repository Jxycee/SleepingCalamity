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
        // T09b scene diet: every generated mesh leaves the scene (rule 3.7; the 157 lane treads of 20 KB each stayed under the old 6000 B floor and were 3.2 MB of the scene file).
        // Meshes with the same content (the lane treads of one size) share one asset.
        const int MinBytes = 0;

        public static void Externalize(Scene scene, string area)
        {
            string folder = Root + "/" + area;
            BuilderUtil.EnsureFolder(folder);
            var byInstance = new Dictionary<Mesh, Mesh>();
            var byContent = new Dictionary<string, Mesh>();
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
                if ((m.hideFlags & HideFlags.DontSaveInEditor) != 0) return m;   // LookTestGeometry rebuilds its box/cone meshes (HideAndDontSave) on load: the scene stores no mesh for them, an asset would be dead weight
                if (byInstance.TryGetValue(m, out Mesh done)) return done;
                long size = (long)m.vertexCount * 32 + IndexCount(m) * 4L;
                if (size < MinBytes && !AlwaysAsset(m.name)) { kept++; byInstance[m] = m; return m; }
                string ck = ContentKey(m);
                if (byContent.TryGetValue(ck, out Mesh same)) { byInstance[m] = same; return same; }
                string stem = Sanitize(string.IsNullOrEmpty(m.name) ? "Mesh" : m.name);
                nameCount.TryGetValue(stem, out int n); nameCount[stem] = ++n;
                string path = folder + "/" + (n == 1 ? stem : stem + "_" + n) + ".asset";
                usedPaths.Add(path);
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Mesh result;
                if (existing == null) { AssetDatabase.CreateAsset(m, path); result = m; }
                else { EditorUtility.CopySerialized(m, existing); EditorUtility.SetDirty(existing); result = existing; }   // the fresh mesh is left unreferenced (never saved)
                byInstance[m] = result; byContent[ck] = result; bytes += size; moved++;
                return result;
            }
        }

        // Meshes that stay assets whatever their size (the orchard's tree mounds are only ~4 KB since T07b cut the trees to four, but rule 3.7 keeps them out of the scene file).
        static bool AlwaysAsset(string name) => name == "OR_Mounds";

        // A content key: counts plus an FNV-1a hash over positions, normals, uvs, colors and triangle indices (same bytes in, same key out).
        static string ContentKey(Mesh m)
        {
            ulong h = 1469598103934665603UL;
            void Mix(float f) { h = (h ^ (uint)System.BitConverter.SingleToInt32Bits(f)) * 1099511628211UL; }
            foreach (Vector3 v in m.vertices) { Mix(v.x); Mix(v.y); Mix(v.z); }
            foreach (Vector3 v in m.normals) { Mix(v.x); Mix(v.y); Mix(v.z); }
            foreach (Vector2 v in m.uv) { Mix(v.x); Mix(v.y); }
            foreach (Color c in m.colors) { Mix(c.r); Mix(c.g); Mix(c.b); Mix(c.a); }
            for (int i = 0; i < m.subMeshCount; i++) foreach (int t in m.GetIndices(i)) h = (h ^ (uint)t) * 1099511628211UL;
            return m.vertexCount + "_" + IndexCount(m) + "_" + m.subMeshCount + "_" + h.ToString("x");
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
