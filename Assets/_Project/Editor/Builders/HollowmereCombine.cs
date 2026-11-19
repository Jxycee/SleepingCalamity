// T12 scene diet: merges the small plain decoration objects (one MeshFilter + MeshRenderer, no collider, no children, no LODGroup above) into one mesh per
// (parent, 12 m cell, material list, shadow mode, layer). A renderer object costs about 2.5 KB of scene text (3.2 KB as a prefab instance); the grass strips, weeds, tufts and
// kerb pieces were about 4,000 of them. The combined meshes are builder-generated and go to Content/Generated through GeneratedMeshes.Externalize (run after this pass).
// Same materials, same vertices, same shadows: the frames do not change except for light-probe and reflection-probe sampling at the cell centre (the scene has none in the town).
// Only opaque or alpha-tested materials (queue <= 2450) are merged; anything with another component, a child, a collider, or a LODGroup above stays as it is.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    internal static class HollowmereCombine
    {
        const float Cell = 12f;
        const int MaxTris = 300;
        const float MaxExtent = 4f;
        const int MaxVerts = 20000;

        struct Cand { public GameObject Go; public Transform Dead; public MeshFilter Mf; public MeshRenderer Mr; public Transform Parent; public string Key; }

        public static void Apply(Scene scene)
        {
            // phase 1: kit FBX meshes are not readable (CombineMeshes needs it): switch Read/Write on for the FBX of every otherwise-qualifying candidate, reimport, then scan again
            var need = new SortedSet<string>(System.StringComparer.Ordinal);
            Scan(scene, need, new Dictionary<string, int>());
            int flipped = 0;
            foreach (string path in need)
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); flipped++; }
            var reasons = new Dictionary<string, int>();
            List<Cand> cands = Scan(scene, null, reasons);
            Debug.Log("SC HOLLOWMERE COMBINE scan: " + flipped + " FBX switched to Read/Write; rejected " + string.Join(", ", reasons.OrderBy(kv => kv.Key, System.StringComparer.Ordinal).Select(kv => kv.Key + "=" + kv.Value)));
            Merge(cands);
        }

        static void Count(Dictionary<string, int> r, string k) { r.TryGetValue(k, out int n); r[k] = n + 1; }

        static List<Cand> Scan(Scene scene, SortedSet<string> needReadable, Dictionary<string, int> reasons)
        {
            var cands = new List<Cand>(); var seenDead = new HashSet<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Hollowmere") continue;
                foreach (Transform grp in root.transform)
                {
                    if (!(grp.name.StartsWith("Preset_") || grp.name == "Set")) continue;
                    foreach (MeshFilter mf in grp.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var go = mf.gameObject; var mr = go.GetComponent<MeshRenderer>();
                        if (mr == null || mf.sharedMesh == null || go.transform.childCount != 0) { Count(reasons, "noMrOrChildren"); continue; }
                        if (go.GetComponents<Component>().Length != 3) { Count(reasons, "otherComponent"); continue; }
                        if (go.GetComponentInParent<LODGroup>() != null) { Count(reasons, "lodGroup"); continue; }
                        if (go.GetComponentInParent<SleepingCalamity.Presentation.AmbientClothSway>() != null) { Count(reasons, "swaying"); continue; }   // T24: washing-line cloths are moved by AmbientClothSway
                        Mesh m = mf.sharedMesh; if (HollowmereLods.Tris(m) > MaxTris || m.vertexCount > MaxVerts) { Count(reasons, "tooBig"); continue; }
                        Vector3 sz = mr.bounds.size; if (Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) > MaxExtent) { Count(reasons, "tooWide"); continue; }
                        if (mr.sharedMaterials.Any(x => x == null || x.renderQueue > 2450)) { Count(reasons, "transparentOrNullMat"); continue; }
                        if (mr.sharedMaterials.Length != m.subMeshCount) { Count(reasons, "matCount"); continue; }
                        // the object to remove: the outermost prefab instance when it holds nothing but this renderer (and bare transforms), else the object itself
                        Transform dead = go.transform;
                        if (PrefabUtility.IsPartOfPrefabInstance(go))
                        {
                            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                            if (outer == null) { Count(reasons, "noOuter"); continue; }
                            bool pure = true; int renderers = 0;
                            foreach (Transform t in outer.GetComponentsInChildren<Transform>(true))
                            {
                                int comps = t.GetComponents<Component>().Length;
                                if (t.GetComponent<MeshFilter>() != null) { renderers++; if (comps != 3) pure = false; }
                                else if (comps != 1) pure = false;
                            }
                            if (!pure || renderers != 1) { Count(reasons, "instanceNotPure"); continue; }
                            dead = outer.transform;
                        }
                        if (dead.parent == null || !seenDead.Add(dead)) { Count(reasons, "noParent"); continue; }
                        if (!m.isReadable)
                        {
                            string mp = AssetDatabase.GetAssetPath(m);
                            if (needReadable != null && mp.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) needReadable.Add(mp);
                            Count(reasons, "notReadable"); continue;
                        }
                        Vector3 c = mr.bounds.center;
                        string key = dead.parent.GetEntityId() + "|" + Mathf.FloorToInt(c.x / Cell) + "|" + Mathf.FloorToInt(c.z / Cell) + "|" + string.Join(",", mr.sharedMaterials.Select(x => x.GetEntityId())) + "|" + (int)mr.shadowCastingMode + "|" + mr.receiveShadows + "|" + go.layer + "|" + go.activeInHierarchy + "|" + go.isStatic + "|" + mr.renderingLayerMask + "|" + (int)mr.lightProbeUsage + "|" + (int)mr.reflectionProbeUsage;
                        cands.Add(new Cand { Go = go, Dead = dead, Mf = mf, Mr = mr, Parent = dead.parent, Key = key });
                    }
                }
            }
            return cands;
        }

        static void Merge(List<Cand> cands)
        {
            int removed = 0, made = 0, kept = 0;
            // deterministic order: the hierarchy order of discovery, grouped by key in order of first appearance
            var order = new List<string>(); var groups = new Dictionary<string, List<Cand>>();
            foreach (Cand c in cands) { if (!groups.TryGetValue(c.Key, out var l)) { groups[c.Key] = l = new List<Cand>(); order.Add(c.Key); } l.Add(c); }
            var perParent = new Dictionary<Transform, int>();
            foreach (string key in order)
            {
                List<Cand> g = groups[key];
                if (g.Count < 2) { kept += g.Count; continue; }
                for (int from = 0; from < g.Count;)   // split so one combined mesh stays under the vertex limit
                {
                    long verts = 0; int to = from;
                    while (to < g.Count && (to == from || verts + g[to].Mf.sharedMesh.vertexCount < MaxVerts)) verts += g[to++].Mf.sharedMesh.vertexCount;
                    var part = g.GetRange(from, to - from); from = to;
                    if (part.Count < 2) { kept += part.Count; continue; }
                    Transform parent = part[0].Parent; Matrix4x4 w2l = parent.worldToLocalMatrix;
                    Material[] mats = part[0].Mr.sharedMaterials; int subs = mats.Length;
                    var perSub = new List<Mesh>();
                    for (int s = 0; s < subs; s++)
                    {
                        var ci = new CombineInstance[part.Count];
                        for (int i = 0; i < part.Count; i++) ci[i] = new CombineInstance { mesh = part[i].Mf.sharedMesh, subMeshIndex = s, transform = w2l * part[i].Go.transform.localToWorldMatrix };
                        var sm = new Mesh { indexFormat = IndexFormat.UInt32 }; sm.CombineMeshes(ci, true, true); perSub.Add(sm);
                    }
                    var final = new Mesh { indexFormat = IndexFormat.UInt32 };
                    var fi = perSub.Select(m => new CombineInstance { mesh = m, subMeshIndex = 0, transform = Matrix4x4.identity }).ToArray();
                    final.CombineMeshes(fi, false, false);
                    foreach (Mesh m in perSub) Object.DestroyImmediate(m);
                    perParent.TryGetValue(parent, out int n); perParent[parent] = n + 1;
                    final.name = "Comb_" + Sanitize(parent.name) + "_" + n;
                    final.RecalculateBounds();
                    var go = new GameObject("Comb_" + n); go.transform.SetParent(parent, false);
                    go.SetActive(part[0].Go.activeSelf); go.layer = part[0].Go.layer; GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(part[0].Go));
                    go.AddComponent<MeshFilter>().sharedMesh = final;
                    var mr = go.AddComponent<MeshRenderer>(); MeshRenderer src = part[0].Mr;
                    mr.sharedMaterials = mats; mr.shadowCastingMode = src.shadowCastingMode; mr.receiveShadows = src.receiveShadows; mr.renderingLayerMask = src.renderingLayerMask;
                    mr.lightProbeUsage = src.lightProbeUsage; mr.reflectionProbeUsage = src.reflectionProbeUsage; mr.motionVectorGenerationMode = src.motionVectorGenerationMode;
                    foreach (Cand c in part) { Object.DestroyImmediate(c.Dead.gameObject); removed++; }
                    made++;
                }
            }
            Debug.Log("SC HOLLOWMERE COMBINE: " + cands.Count + " candidates, " + removed + " objects merged into " + made + " combined meshes, " + kept + " left single");
        }

        static string Sanitize(string s) { var sb = new System.Text.StringBuilder(); foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_'); return sb.ToString(); }
    }
}
