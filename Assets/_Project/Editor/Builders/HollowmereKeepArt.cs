// The keep terrace kit (Q9, sc_keep_terrace.py): KeepTerrace.fbx holds direct-child pieces (WallSegment, WallEnd, Buttress, Planter, StairStep, Balustrade,
// BalustradeRaked, Newel, Bench), each at its own origin, in Unity axes. HollowmereBuilder.BuildKeep clones them along the retaining wall and the two stairs;
// Load returns null when the export is missing and the builder keeps its greybox. Materials: KT_* slots built like the other final-art slots (HollowmereArt.Tinted/Flat).
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    internal sealed class HollowmereKeepArt
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Keep/KeepTerrace";
        const string FoliageMat = "Assets/_Project/Content/Materials/BeautyCorner/M_B9_Foliage.mat";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";

        readonly GameObject fbx;
        readonly Dictionary<string, Material> slots;

        HollowmereKeepArt(GameObject fbx, Dictionary<string, Material> slots) { this.fbx = fbx; this.slots = slots; }

        public static HollowmereKeepArt Load()
        {
            GameObject fbx = BeautyCornerArt.LoadModel(Folder, "KeepTerrace.fbx", keepMaterialSlots: true);
            if (fbx == null) return null;
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find("SleepingCalamity/WearLit") ?? throw new InvalidOperationException("Shader not found: SleepingCalamity/WearLit");
            var map = new Dictionary<string, Material>
            {
                ["KT_Rubble"] = HollowmereArt.Tinted("KT_Rubble", "sandstone", new Color(0.56f, 0.56f, 0.56f), wear),
                ["KT_Dressed"] = HollowmereArt.Tinted("KT_Dressed", "sandstone", new Color(0.94f, 0.94f, 0.92f), wear),
                ["KT_Mortar"] = HollowmereArt.Tinted("KT_Mortar", "sandstone", new Color(0.26f, 0.245f, 0.21f), wear),
                ["KT_Soil"] = HollowmereArt.Flat("KT_Soil", new Color(0.034f, 0.025f, 0.017f), 0.05f, wear),
                ["M_B9_Foliage"] = AssetDatabase.LoadAssetAtPath<Material>(FoliageMat) ?? throw new InvalidOperationException("Missing " + FoliageMat),
            };
            map["KT_Soil"].SetFloat("_Smoothness", 0.05f);   // roughness 0.95
            AssetDatabase.SaveAssets();
            return new HollowmereKeepArt(fbx, map);
        }

        public Material CoreMaterial() => slots["KT_Rubble"];

        public Transform Piece(string name) => fbx.transform.Find(name) ?? throw new InvalidOperationException("KeepTerrace.fbx has no piece " + name);

        // Clones a piece under parent at world position pos, turned yaw degrees about Y, its axes scaled by scale (on top of the import scale).
        public GameObject Clone(string piece, Transform parent, string name, Vector3 pos, float yaw, Vector3 scale)
        {
            Transform src = Piece(piece);
            GameObject go = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * src.localRotation;
            go.transform.localScale = Vector3.Scale(src.localScale, scale);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] old = r.sharedMaterials, mats = new Material[Mathf.Max(1, old.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < old.Length && old[k] != null ? old[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) throw new InvalidOperationException("KeepTerrace: no material for slot '" + slot + "' on " + piece);
                }
                r.sharedMaterials = mats;
            }
            return go;
        }

        // Which way the raked balustrade climbs along its local X in the export: +1 when its higher end is at +X, -1 at -X (from the mesh itself).
        public float RakeSign()
        {
            Transform src = Piece("BalustradeRaked");
            MeshFilter mf = src.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 1f;
            Vector3[] v = mf.sharedMesh.vertices;
            double mx = 0, my = 0; foreach (Vector3 p in v) { mx += p.x; my += p.y; }
            mx /= v.Length; my /= v.Length;
            double cov = 0; foreach (Vector3 p in v) cov += (p.x - mx) * (p.y - my);
            return cov >= 0 ? 1f : -1f;
        }
    }
}
