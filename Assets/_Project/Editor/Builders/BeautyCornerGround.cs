// Data-driven placement for the beauty-corner pieces that are not one FBX at one spot (M9 batch 2): the B6 plaza cobbles (6,732 setts + 45 loose stones from
// cobble_placements.json), the flagstone ring and its bed, and the B9 crack weeds scattered over the json's weed slots. Called by BeautyCornerBuilder.PlaceRow for
// the rows flagged Scatter; each returns false when its art is missing so the row falls back to its greybox.
//
// Cobbles: one prefab per sett variant under Content/Prefabs/BeautyCorner (root with a LODGroup: LOD0 mesh until the sett is about 10 m from the camera, LOD1
// mesh beyond, culled far out), all sharing one GPU-instanced material, so the SRP batcher / GPU Resident Drawer sees 11 meshes and one material. The json's
// own per-stone "lod" is ignored: it was a static pick for the wide camera, the LODGroup does it per camera. Positions are Unity metres around the fountain
// centre and rotations are Unity quaternions, used as they are (checked: the yaw of the ring-hugging setts follows the circle tangent, mirrored it would not).
// Meshes come from the FBX MeshFilters, not the node transforms: the FBX nodes carry the export layout, the mesh pivot (sett top centre) is what the json assumes.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal static class BeautyCornerGround
    {
        const string PrefabFolder = "Assets/_Project/Content/Prefabs/BeautyCorner";
        const float Lod0Height = 0.025f, CullHeight = 0.002f;   // LODGroup screen-relative heights: a 0.2 m sett at 46 degrees fov is 0.025 at about 10 m

        [System.Serializable] sealed class Stone { public string v; public int lod; public string kind; public float[] p, q, s; public bool seen; }
        [System.Serializable] sealed class Slot { public float[] p; public string kind; }
        [System.Serializable] sealed class Placements { public Stone[] stones; public Slot[] weed_slots; }

        public static bool TryPlace(Transform group, BeautyCornerTable.Row row)
        {
            switch (row.Id)
            {
                case "PlazaCobbles": return Cobbles(group, row);
                case "FountainRing": return Ring(group, row);
                case "CrackWeeds": return Weeds(group, row);
                default: return false;
            }
        }

        // One child of the row group that holds the whole scatter. worldOrigin: the data is in scene coordinates, so the holder stays at the origin.
        static Transform Holder(Transform group, BeautyCornerTable.Row row, bool worldOrigin = false)
        {
            Transform go = new GameObject(row.Id).transform; go.SetParent(group, false);
            if (worldOrigin) return go;
            BeautyCornerTable.Instance inst = row.Instances[0];
            go.localPosition = inst.Pos; go.localRotation = Quaternion.Euler(0f, inst.Yaw, 0f); go.localScale = Vector3.one * inst.Scale;
            return go;
        }

        static Placements Load(string folder)
        {
            string path = folder + "/cobble_placements.json";
            if (!File.Exists(BuilderUtil.Abs(path))) return null;
            return JsonUtility.FromJson<Placements>(File.ReadAllText(BuilderUtil.Abs(path)));
        }

        static void Assign(GameObject root, Material m, bool shadows = true)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------------------------------------ cobbles
        static bool Cobbles(Transform group, BeautyCornerTable.Row row)
        {
            string folder = BeautyCornerArt.FindFolder(row.Batch, "Cobbles");
            if (folder == null) return false;
            GameObject family = BeautyCornerArt.LoadModel(folder, "CobbleFamily.fbx"), bedFbx = BeautyCornerArt.LoadModel(folder, "PlazaBed.fbx");
            Placements data = Load(folder);
            if (family == null || bedFbx == null || data == null || data.stones == null) return false;
            Material cobble = BeautyCornerArt.MaterialFromSet(folder, "Cobble", "PlazaCobbles");
            Material bed = BeautyCornerArt.MaterialFromSet(folder, "CobbleBed", "PlazaBed");
            cobble.enableInstancing = true; bed.enableInstancing = true;
            EditorUtility.SetDirty(cobble); EditorUtility.SetDirty(bed);

            Transform holder = Holder(group, row);
            var bedGo = (GameObject)PrefabUtility.InstantiatePrefab(bedFbx, holder); bedGo.name = "Art_PlazaBed";
            Assign(bedGo, bed);

            BuilderUtil.EnsureFolder(PrefabFolder);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string v in data.stones.Select(s => s.v).Distinct())
            {
                GameObject prefab = SettPrefab(v, family, cobble);
                if (prefab != null) prefabs[v] = prefab; else Debug.LogWarning("SC BEAUTY cobbles: no mesh for variant " + v);
            }
            Transform stones = new GameObject("Setts").transform; stones.SetParent(holder, false);
            int placed = 0;
            foreach (Stone s in data.stones)
            {
                if (!prefabs.TryGetValue(s.v, out GameObject prefab)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stones);
                go.transform.localPosition = new Vector3(s.p[0], s.p[1], s.p[2]);
                go.transform.localRotation = new Quaternion(s.q[0], s.q[1], s.q[2], s.q[3]);
                go.transform.localScale = new Vector3(s.s[0], s.s[1], s.s[2]);
                placed++;
            }
            Debug.Log("SC BEAUTY cobbles placed " + placed + " of " + data.stones.Length + " from " + prefabs.Count + " variant prefabs");
            return true;
        }

        // Sett_<v>.prefab: LODGroup over LOD0/LOD1 child renderers. Rewritten only when its meshes or material changed (a rewrite renumbers the prefab's file ids).
        static GameObject SettPrefab(string v, GameObject family, Material mat)
        {
            Mesh Find(string suffix)
            {
                foreach (MeshFilter mf in family.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null && string.Equals(mf.name, v + suffix, System.StringComparison.OrdinalIgnoreCase)) return mf.sharedMesh;
                return null;
            }
            Mesh m0 = Find("_LOD0"), m1 = Find("_LOD1");
            if (m0 == null) return null;
            string path = PrefabFolder + "/Sett_" + v + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                MeshFilter[] mfs = existing.GetComponentsInChildren<MeshFilter>(true);
                bool same = mfs.Length == (m1 != null ? 2 : 1) && mfs[0].sharedMesh == m0 && (m1 == null || mfs[1].sharedMesh == m1)
                    && existing.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterial == mat);
                if (same) return existing;
            }
            var root = new GameObject("Sett_" + v);
            Renderer Child(string name, Mesh mesh)
            {
                var c = new GameObject(name); c.transform.SetParent(root.transform, false);
                c.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = c.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                return r;
            }
            Renderer r0 = Child("LOD0", m0), r1 = m1 != null ? Child("LOD1", m1) : null;
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(r1 != null
                ? new[] { new LOD(Lod0Height, new[] { r0 }), new LOD(CullHeight, new[] { r1 }) }
                : new[] { new LOD(CullHeight, new[] { r0 }) });
            lod.RecalculateBounds();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        // ------------------------------------------------------------------------------------------------ flagstone ring
        static bool Ring(Transform group, BeautyCornerTable.Row row)
        {
            string folder = BeautyCornerArt.FindFolder(row.Batch, "FlagRing");
            if (folder == null) return false;
            GameObject ring = BeautyCornerArt.LoadModel(folder, "FlagRing.fbx"), bedFbx = BeautyCornerArt.LoadModel(folder, "RingBed.fbx");
            if (ring == null) return false;
            Material ringMat = BeautyCornerArt.MaterialFromSet(folder, "FlagRing", "FountainRing");
            Transform holder = Holder(group, row);
            var ringGo = (GameObject)PrefabUtility.InstantiatePrefab(ring, holder); ringGo.name = "Art_FlagRing";
            Assign(ringGo, ringMat);
            if (bedFbx != null)
            {
                string cobbles = BeautyCornerArt.FindFolder("B6", "Cobbles");
                Material bed = cobbles != null ? BeautyCornerArt.MaterialFromSet(cobbles, "CobbleBed", "PlazaBed") : null;   // RingBed shares the PlazaBed tileable
                if (bed != null)
                {
                    var bedGo = (GameObject)PrefabUtility.InstantiatePrefab(bedFbx, holder); bedGo.name = "Art_RingBed";
                    Assign(bedGo, bed);
                }
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ weeds
        // A subset of the json's weed slots (the missing setts, joints, ring edge and facade foot), all five Weed_A..E sizes in rotation, scale and yaw varied.
        static bool Weeds(Transform group, BeautyCornerTable.Row row)
        {
            string cobbles = BeautyCornerArt.FindFolder("B6", "Cobbles");
            Placements data = cobbles != null ? Load(cobbles) : null;
            if (data == null || data.weed_slots == null) return false;
            var models = new List<GameObject>();
            Material mat = null;
            foreach (char c in "ABCDE")
            {
                string folder = BeautyCornerArt.FindFolder(row.Batch, "Weed_" + c);
                string fbx = folder != null ? BeautyCornerArt.FirstFbx(folder) : null;
                if (fbx == null) continue;
                GameObject go = BeautyCornerArt.LoadModel(folder, Path.GetFileName(fbx));
                if (go == null) continue;
                models.Add(go);
                if (mat == null) mat = BeautyCornerArt.MaterialFromSet(folder, "T_B9_Foliage", "CrackWeeds", foliage: true);   // the five folders hold one identical atlas
            }
            if (models.Count == 0 || mat == null) return false;

            Transform holder = Holder(group, row, worldOrigin: true);
            var rnd = new System.Random(9);
            var seen = new Dictionary<string, int>();
            int n = 0;
            foreach (Slot s in data.weed_slots)
            {
                int k = seen.TryGetValue(s.kind, out int c) ? c : 0; seen[s.kind] = k + 1;
                int every = s.kind == "facade_foot" ? 1 : s.kind == "missing_sett" ? 3 : 2;
                if (k % every != 0) continue;
                GameObject model = models[n % models.Count];
                var w = (GameObject)PrefabUtility.InstantiatePrefab(model, holder); w.name = "Weed_" + (char)('A' + n % models.Count) + "_" + n;
                Assign(w, mat, shadows: false);
                float yaw = s.kind == "facade_foot" ? (n % 2 == 0 ? 0f : 180f) : (float)(rnd.NextDouble() * 360.0);   // wall-foot runs follow the wall (local x)
                float scale = 0.8f + (float)rnd.NextDouble() * 0.45f;
                w.transform.localPosition = new Vector3(s.p[0], s.p[1], s.p[2]);
                w.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                w.transform.localScale = Vector3.one * scale;
                n++;
            }
            Debug.Log("SC BEAUTY weeds placed " + n + " of " + data.weed_slots.Length + " slots with " + models.Count + " variants");
            return true;
        }
    }
}
