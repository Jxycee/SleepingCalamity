// Places the heroes' home art (Ring1/HeroesHome: HeroesHome_Cistern.fbx and HeroesHome_Annex.fbx) into Hollowmere. Each FBX has the children Frame (opaque, multi-material),
// Decals, Glow (emissive), FOOTPRINT (collider box, never rendered) and SOCKET_* empties; the vertex colour is the WearLit "wear" (R grime, G moss, B polish).
// Materials: the facade's (BeautyCornerArt.BuildFacadeMaterials) plus the HH_* ones below, all in Content/Materials/BeautyCorner, built once per run and cached.
// The art comes in with its front toward local +Z, so it is turned 180 degrees like the B4 facade. The cistern also gets two warm point lights for the Undercroft stair.
using System.Collections.Generic;
using SleepingCalamity.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class HeroesHomeArt
    {
        const string MaterialsFolder = "Assets/_Project/Content/Materials/BeautyCorner";
        const string WearShaderName = "SleepingCalamity/WearLit";
        const float PaintLum = 0.17f;
        static Dictionary<string, Material> cache;

        static readonly (string Slot, string Source, Color Tint)[] Tinted =
        {
            ("HH_OldStone", "sandstone", new Color(0.86f, 0.76f, 0.60f)), ("HH_Mortar", "sandstone", new Color(0.46f, 0.41f, 0.34f)), ("HH_Slate", "sandstone", new Color(0.50f, 0.47f, 0.43f)),
            ("HH_PlankGrey", "oak_timber", Color.white), ("HH_Canvas", "canvas", Color.white), ("HH_ClothMadder", "canvas", new Color(0.85f, 0.30f, 0.22f)),
            ("HH_ClothBlue", "canvas", new Color(0.45f, 0.58f, 0.80f)), ("HH_ClothLinen", "canvas", Color.white),
        };
        static readonly (string Slot, Color Paint)[] Paints =
        {
            ("HH_PaintRed", new Color(0.2542f, 0.0423f, 0.0232f)), ("HH_PaintBlue", new Color(0.0742f, 0.1499f, 0.2623f)), ("HH_PaintOchre", new Color(0.4342f, 0.2542f, 0.0423f)),
        };

        static Dictionary<string, Material> Materials()
        {
            if (cache != null && System.Linq.Enumerable.All(cache.Values, m => m != null)) return cache;
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            Shader wear = Shader.Find(WearShaderName);
            foreach (var s in Tinted)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                m.SetColor("_BaseColor", new Color(Mathf.Clamp01(s.Tint.r), Mathf.Clamp01(s.Tint.g), Mathf.Clamp01(s.Tint.b), 1f).gamma);
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m); map[s.Slot] = m;
            }
            foreach (var s in Paints)
            {
                Material src = LookTestBuilder.Mat("painted_wood");
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                m.EnableKeyword("_SC_PAINT");
                m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
                m.SetVector("_SpecColor", new Vector4(s.Paint.r / PaintLum, s.Paint.g / PaintLum, s.Paint.b / PaintLum, 1f));   // raw linear
                m.SetShaderPassEnabled("MotionVectors", true);
                EditorUtility.SetDirty(m); map[s.Slot] = m;
            }
            map["HH_Rope"] = BeautyCornerArt.Flat("HH_Rope", new Color(0.32f, 0.25f, 0.16f), 0.1f, wear);
            map["HH_Dark"] = BeautyCornerArt.Flat("HH_Dark", new Color(0.035f, 0.028f, 0.022f), 0.05f, wear);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material glow = LookTestBuilder.Ensure(MaterialsFolder + "/M_HH_Glow.mat", lit);
            Color warm = new Color(1.0f, 0.55f, 0.22f);
            glow.SetColor("_BaseColor", warm.gamma); glow.SetFloat("_Smoothness", 0.2f); glow.SetFloat("_Metallic", 0f);
            glow.SetColor("_EmissionColor", warm * 4f); glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow); map["HH_Glow"] = glow;

            string listPath = "Assets/_Project/Content/Art/Town/Ring1/HeroesHome/HH_List_BaseColor.png";
            if (AssetImporter.GetAtPath(listPath) != null)
            {
                BeautyCornerArt.ImportTexture(listPath, srgb: true, normal: false, alphaTransparency: true);
                Material list = LookTestBuilder.Ensure(MaterialsFolder + "/M_HH_List.mat", lit);
                list.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(listPath));
                list.SetColor("_BaseColor", Color.white);
                BeautyCornerArt.AlphaBlend(list);
                list.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(list); map["HH_List"] = list;
            }
            AssetDatabase.SaveAssets();
            cache = map;
            return map;
        }

        // Instantiates the FBX <id> under parent; false when the folder or FBX is missing (the caller keeps its greybox).
        public static bool Place(Transform parent, string id, Vector3 doorLocal)
        {
            string folder = BeautyCornerArt.FindFolder("Ring1", "HeroesHome");
            if (folder == null) return false;
            GameObject fbx = BeautyCornerArt.LoadModel(folder, id + ".fbx", keepMaterialSlots: true);
            if (fbx == null) return false;
            var map = Materials();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            obj.name = id + "_Art";
            obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            foreach (var r in obj.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.gameObject.name == "FOOTPRINT") { r.enabled = false; continue; }
                if (r.gameObject.name == "Decals") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (r.gameObject.name == "Glow") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = mats[i] != null ? mats[i].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (r.gameObject.name == "Glow" && map.TryGetValue("HH_Glow", out Material gm) && !map.ContainsKey(slot)) mats[i] = gm;   // the Glow child may carry a default "Lit" slot
                    else if (map.TryGetValue(slot, out Material m)) mats[i] = m;
                    else Debug.LogWarning("SC HEROESHOME unknown material slot '" + slot + "' on " + id + "/" + r.gameObject.name);
                }
                r.sharedMaterials = mats;
            }
            AddWalls(parent, obj.transform, doorLocal);
            return true;
        }

        // Walls for an enterable house: the Frame is one 77k-vertex mesh with the door shut and the undercroft stair inside, so instead of a mesh collider four boxes follow the
        // FOOTPRINT rectangle (0.5 m thick, 3.3 m high), with a 1.6 m gap in the wall the door node stands at. The porch and the cistern drum stay walk-through (outside the footprint).
        static void AddWalls(Transform g, Transform art, Vector3 doorLocal)
        {
            Transform fp = art.Find("FOOTPRINT");
            MeshFilter fm = fp != null ? fp.GetComponent<MeshFilter>() : null;
            if (fm == null || fm.sharedMesh == null) return;
            Bounds b = fm.sharedMesh.bounds;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int c = 0; c < 8; c++)
            {
                Vector3 p = g.InverseTransformPoint(fp.TransformPoint(new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z)));
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            const float t = 0.5f, h = 3.3f, gap = 1.6f;
            var root = new GameObject("Collider_Walls"); root.transform.SetParent(g, false);
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            // the wall the door node is nearest to (the node stands just outside it)
            float dW = Mathf.Abs(doorLocal.x - x0), dE = Mathf.Abs(doorLocal.x - x1), dS = Mathf.Abs(doorLocal.z - z0), dN = Mathf.Abs(doorLocal.z - z1);
            float m = Mathf.Min(Mathf.Min(dW, dE), Mathf.Min(dS, dN));
            void Wall(string name, Vector3 centre, Vector3 size, bool alongX, bool doorWall)
            {
                if (!doorWall) { Box(root.transform, name, centre, size); return; }
                float lo = (alongX ? centre.x - size.x * 0.5f : centre.z - size.z * 0.5f), hi = lo + (alongX ? size.x : size.z);
                DoorSpan(alongX ? doorLocal.x : doorLocal.z, lo, hi, gap, out float a, out float c);
                if (a - lo > 0.05f) Box(root.transform, name + "_A", alongX ? new Vector3((lo + a) * 0.5f, centre.y, centre.z) : new Vector3(centre.x, centre.y, (lo + a) * 0.5f), alongX ? new Vector3(a - lo, size.y, size.z) : new Vector3(size.x, size.y, a - lo));
                if (hi - c > 0.05f) Box(root.transform, name + "_B", alongX ? new Vector3((c + hi) * 0.5f, centre.y, centre.z) : new Vector3(centre.x, centre.y, (c + hi) * 0.5f), alongX ? new Vector3(hi - c, size.y, size.z) : new Vector3(size.x, size.y, hi - c));
            }
            Wall("WallS", new Vector3(cx, h * 0.5f, z0 + t * 0.5f), new Vector3(x1 - x0, h, t), true, m == dS);
            Wall("WallN", new Vector3(cx, h * 0.5f, z1 - t * 0.5f), new Vector3(x1 - x0, h, t), true, m == dN);
            Wall("WallW", new Vector3(x0 + t * 0.5f, h * 0.5f, cz), new Vector3(t, h, z1 - z0), false, m == dW);
            Wall("WallE", new Vector3(x1 - t * 0.5f, h * 0.5f, cz), new Vector3(t, h, z1 - z0), false, m == dE);
        }

        // The door opening [a, c] inside a wall run [lo, hi]. A NaN or infinite door coordinate (a degenerate door direction) falls back to the wall's middle, so no NaN reaches a collider.
        internal static void DoorSpan(float door, float lo, float hi, float gap, out float a, out float c)
        {
            if (float.IsNaN(door) || float.IsInfinity(door)) door = (lo + hi) * 0.5f;
            a = Mathf.Clamp(door - gap * 0.5f, lo, hi); c = Mathf.Clamp(door + gap * 0.5f, lo, hi);
        }

        static void Box(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var b = go.AddComponent<BoxCollider>(); b.center = centre; b.size = size;
        }

        // Two warm point lights for the Undercroft stair (cistern only): one under the floor at the stair, one at the stair head. World positions from the stair marker.
        public static void AddStairLights(Transform parent, Transform stair)
        {
            Light a = LookTestBuilder.AddLight(parent, "UndercroftLight", LightType.Point, Vector3.zero, Quaternion.identity, new Color(1f, 0.62f, 0.30f), 3f, 5f, LightShadows.None);
            a.transform.position = stair.position + new Vector3(0f, -0.8f, 0.4f);
            Light b = LookTestBuilder.AddLight(parent, "StairHeadLight", LightType.Point, Vector3.zero, Quaternion.identity, new Color(1f, 0.70f, 0.42f), 1.2f, 4f, LightShadows.None);
            b.transform.position = stair.position + new Vector3(0f, 1.9f, -0.2f);
            Light c = LookTestBuilder.AddLight(parent, "DoorLanternLight", LightType.Point, Vector3.zero, Quaternion.identity, new Color(1f, 0.62f, 0.32f), 2.5f, 6f, LightShadows.None);
            c.transform.position = new Vector3(12.85f, parent.position.y + 2.0f, 8.55f);   // just inside the front door (the door lantern)
        }
    }
}
