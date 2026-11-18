// T12 LOD pass for the Hollowmere scene, run once per build after GeneratedMeshes.Externalize and before the scene is saved.
// A LODGroup needs one renderer GameObject per LOD (about 2.5 KB of scene each), which the 45 MB scene budget cannot take for 10,000 un-LODed objects. Unity's own Mesh LOD keeps the
// levels inside the mesh asset instead (zero scene bytes): this pass generates them for every mesh over MinTris that no LODGroup already covers (builder-generated mesh assets via
// MeshLodUtility, FBX meshes via the importer's generateMeshLods), then turns shadow casting off on renderers under 0.5 m (small plants, pebbles, props).
// Objects that already sit under a LODGroup (trees, adopted props, the kit's own LOD sets) are left alone.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereLods
    {
        public const int MinTris = 400;       // meshes under this stay single-level
        public const int MaxLevels = 4;       // extra levels below the original
        public const float ShadowMinExtent = 0.5f;

        public static void Apply(Scene scene)
        {
            var generated = new HashSet<Mesh>(); var fbxPaths = new SortedSet<string>(System.StringComparer.Ordinal);
            int skippedEmpty = 0, lodMeshes = 0, covered = 0, shadowsOff = 0, small = 0;
            var meshes = new List<Mesh>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>(); Mesh m = mf.sharedMesh;
                    if (mr == null || m == null) continue;
                    // shadows: tiny things do not need to cast
                    Vector3 sz = mr.bounds.size;
                    if (mr.shadowCastingMode != ShadowCastingMode.Off && !PrefabUtility.IsPartOfPrefabInstance(mr) && Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < ShadowMinExtent) { mr.shadowCastingMode = ShadowCastingMode.Off; shadowsOff++; }
                    if (Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) < ShadowMinExtent) small++;
                    if (mf.GetComponentInParent<LODGroup>() != null) { covered++; continue; }
                    if (Tris(m) < MinTris) continue;
                    if (!AssetDatabase.Contains(m)) continue;
                    if (!m.name.StartsWith("Comb_", System.StringComparison.Ordinal) && generated.Add(m)) meshes.Add(m);   // combined plant cells stay single-level (small meshes, many copies)
                }
            }
            meshes = meshes.OrderBy(m => AssetDatabase.GetAssetPath(m), System.StringComparer.Ordinal).ThenBy(m => m.name, System.StringComparer.Ordinal).ToList();
            foreach (Mesh m in meshes)
            {
                string path = AssetDatabase.GetAssetPath(m);
                if (path.StartsWith(GeneratedMeshes.Root, System.StringComparison.Ordinal) && path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (m.lodCount > 1) continue;
                    if (Enumerable.Range(0, m.subMeshCount).Any(i => m.GetIndexCount(i) == 0 || m.GetTopology(i) != MeshTopology.Triangles)) { skippedEmpty++; continue; }   // an empty sub-mesh crashes the generator natively
                    Debug.Log("SC LODGEN " + path + " tris=" + Tris(m) + " verts=" + m.vertexCount + " subs=" + m.subMeshCount + " read=" + m.isReadable + " topo=" + m.GetTopology(0) + " idx=" + m.indexFormat + " subIdx=" + string.Join(",", Enumerable.Range(0, m.subMeshCount).Select(i => m.GetIndexCount(i) + (m.GetTopology(i) == MeshTopology.Triangles ? "" : "T"))) + " uv=" + m.uv.Length + " nrm=" + m.normals.Length + " tan=" + m.tangents.Length + " col=" + m.colors.Length + " uv2=" + m.uv2.Length + " bs=" + m.blendShapeCount);
                    MeshLodUtility.GenerateMeshLods(m, MaxLevels);
                    EditorUtility.SetDirty(m); lodMeshes++;
                }
                else if (path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) fbxPaths.Add(path);
            }
            AssetDatabase.SaveAssets();
            int fbx = 0;
            foreach (string p in fbxPaths)
            {
                if (!(AssetImporter.GetAtPath(p) is ModelImporter mi) || mi.generateMeshLods) continue;
                mi.generateMeshLods = true; mi.SaveAndReimport(); fbx++;
            }
            Debug.Log("SC HOLLOWMERE LODS: " + lodMeshes + " generated mesh assets given Mesh LOD (" + skippedEmpty + " skipped: empty sub-mesh), " + fbx + " of " + fbxPaths.Count + " FBX importers switched on, " + covered + " renderers already under a LODGroup, shadows off on " + shadowsOff + " (" + small + " renderers under " + ShadowMinExtent + " m)");
        }

        public static int Tris(Mesh m) { long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i); return (int)(n / 3); }
    }
}
