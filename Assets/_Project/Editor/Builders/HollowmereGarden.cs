// Garden and allotment decorations for Hollowmere (queue Q6, Rebuilt preset only). Art: Assets/_Project/Content/Art/Town/Garden/<Asset>/<Asset>.fbx from
// Art/Blender/town/sc_garden.py (SOURCES.md there): VegBed_A/B, Trellis, Scarecrow, WaterButt, Fence_Run (Part_PostA/B), Fence_Gate (Part_Leaf), ColdFrame, each with
// a flat FOOTPRINT and no maps. The FBX front is local +Z, so a piece is turned 180 to face a lot's door side (local -Z) like the kit buildings.
// Slots map by name to the shared B1 tileables (LookTest M_ materials; M_GD_packed_soil / M_GD_meadow_grass are made here the same way) and to flat Lit materials
// M_GD_Leaf / Produce / Straw / Glass under Content/Materials/Hollowmere; the beds' Cabbage slot is the textured set next to VegBed_A.fbx (Cabbage_BaseColor/_Normal/_ORM, painted by sc_cabbage_tex.py), double sided. FOOTPRINT keeps a BoxCollider and loses its renderer (as the kit pieces do).
// Everything stands on the lot's footing slab (top FootingTop above the pad; the lot frame already carries the pad height f.Y) and inside the lot footprint.
//  - Allotment(g, f): the four garden lots (HollowmereLayout.RebuiltGardens), a fenced plot with the gate on the door side, two bed columns, a trellis,
//    a water butt, a cold frame or a second bed, and a scarecrow on two of them. Seeded by lot id.
//  - Yard(g, f, kind): the greybox ring 2 homes whose yard behind the house is deep enough for a bed, a water butt at the back wall and a back fence run.
//  - AddCaptureCameras: Cap_HM_Allotment (R2_07 from its lane side) and Cap_HM_Yard (the first yard lot from behind), plus the run's summary log line.
using System.Collections.Generic;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereGarden
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Garden";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string TexRoot = "Assets/_Project/Content/Textures/B1_town/";
        const float FootingTop = 0.25f;          // top of the lot's footing slab above the pad
        const float RunPost = 1.9f;              // Fence_Run: post centre to post centre
        static readonly string[] Scarecrows = { "R2_07", "R3_02" };

        static readonly Dictionary<string, GameObject> Loaded = new Dictionary<string, GameObject>();
        static Dictionary<string, Material> slotMats;
        static readonly HashSet<string> lots = new HashSet<string>();
        static int lotsAllot, lotsYard, pieces; static long tris;
        static string firstYardLot, allotCamLot = "R2_07";
        const float FenceHeight = 0.75f;         // the whole fence is lowered so the beds read over it
        static readonly List<Vector2> posts = new List<Vector2>();   // post positions already standing on this lot (lot frame): one post per junction

        // ------------------------------------------------------------------------------------------------ allotments
        // Fills a garden lot in its lot frame g. False when the art is missing (the caller keeps the greybox).
        public static bool Allotment(Transform g, Foundation f)
        {
            if (Model("Fence_Run") == null || Model("VegBed_A") == null) { Debug.LogWarning("SC HOLLOWMERE garden art missing: " + Folder); return false; }
            var root = new GameObject("Allotment").transform; root.SetParent(g, false);
            posts.Clear();
            float half = f.Side * 0.5f, iH = half - 0.4f, e = half - 0.3f;
            float colX = Mathf.Min(half - 1.05f, 1.9f);
            float s = Rand(f.Id, 1) > 0.5f ? 1f : -1f;                 // which side takes the butt and the cold frame
            float xg = (Rand(f.Id, 2) - 0.5f) * 0.6f;                  // gate offset along the door side
            bool frame = Rand(f.Id, 3) > 0.4f;
            // left column (-s): the long bed at the back, a short bed in front of it
            Place(root, "VegBed_A", -s * colX, iH - 1.3f, -s * 90f + Jit(f.Id, 10, 3f));
            Place(root, "VegBed_B", -s * colX, iH - 3.65f, -s * 90f + Jit(f.Id, 11, 3f));
            // right column (+s): a water butt at the front, a cold frame or bed beside it, the long bed at the back
            Place(root, "WaterButt", s * colX, -(iH - 0.5f), 180f + Jit(f.Id, 12, 40f));
            float zc2 = -(iH - 0.5f) + 0.41f + 0.2f + (frame ? 0.625f : 0.85f);
            Place(root, frame ? "ColdFrame" : "VegBed_B", s * colX, zc2, s * 90f + Jit(f.Id, 13, 3f));
            Place(root, "VegBed_A", s * colX, iH - 1.3f, s * 90f + 180f + Jit(f.Id, 14, 3f));
            Place(root, "Trellis", xg * 0.5f, iH - 0.2f, 180f + Jit(f.Id, 15, 2f));
            if (System.Array.IndexOf(Scarecrows, f.Id) >= 0) Place(root, "Scarecrow", xg, iH - 1.5f, 180f + Jit(f.Id, 16, 25f));
            // soil over the slab interior and tufts along the fence foot
            Soil(root, f.Side - 0.4f);
            for (int i = 0; i < 8; i++)
            {
                float t = Rand(f.Id, 30 + i), u = Rand(f.Id, 50 + i), inner = e - 0.22f;
                int side = i % 3;   // back, left, right (the door side has the gate and stays clear)
                Vector3 lp = side == 0 ? new Vector3((t - 0.5f) * 2f * inner, 0f, inner) : new Vector3(side == 1 ? -inner : inner, 0f, (t - 0.5f) * 2f * inner * 0.8f + 0.3f);
                lp.y = FootingTop + 0.03f;
                HollowmereFoliage.Tuft(root, "Grass_" + "ABC"[(int)(u * 2.999f)], root.TransformPoint(lp), u * 360f, 0.7f + 0.5f * Rand(f.Id, 70 + i));
            }
            // fence: the door side is the gate alone (the short stubs beside it read as a second and third fence row in front of the side runs), the other three sides closed; all lowered
            Place(root, "Fence_Gate", xg, -e, 180f);
            Vector2 fl = new Vector2(-e, -e), bl = new Vector2(-e, e), br = new Vector2(e, e), fr = new Vector2(e, -e);
            Chain(root, fl, bl, true, true); Chain(root, bl, br, false, true); Chain(root, br, fr, false, true);
            lots.Add(f.Id); lotsAllot++;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ yards beside homes
        // Back-yard room of a greybox home (HollowmereBuilder.Building's block size and position) in the lot frame; the yard starts at the plinth's back edge.
        static float YardRoom(Foundation f, out float backEdge, out float width)
        {
            float max = Foundation.MaxBuilding(f.Size);
            width = max * (0.82f + 0.16f * Rand(f.Id, 1)); float d = max * (0.72f + 0.22f * Rand(f.Id, 2));
            backEdge = -(max - d) * 0.35f + d * 0.5f + 0.1f;
            return f.Side * 0.5f - 0.4f - backEdge;
        }

        // A bed, a water butt at the back wall and a back fence run on a ring 2 home lot with a yard deep enough (at least 1.3 m); other lots are left alone.
        public static void Yard(Transform g, Foundation f, HollowmereLayout.BuildingKind kind)
        {
            if (kind != HollowmereLayout.BuildingKind.Home || f.Ring != 2 || f.Size == FoundationSize.S) return;
            float room = YardRoom(f, out float be, out float w);
            if (room < 1.3f || Model("Fence_Run") == null) return;
            var root = new GameObject("Yard").transform; root.SetParent(g, false);
            posts.Clear();
            float half = f.Side * 0.5f, e = half - 0.3f;
            float s = Rand(f.Id, 4) > 0.5f ? 1f : -1f;
            Place(root, "VegBed_B", -s * 1.0f, be + 0.2f + 0.55f, 0f + Jit(f.Id, 20, 2f));
            Place(root, "WaterButt", s * 1.15f, be + 0.15f + 0.41f, 180f + Jit(f.Id, 21, 40f));
            Chain(root, new Vector2(-e, e), new Vector2(e, e), true, true);
            lots.Add(f.Id); lotsYard++;
            if (firstYardLot == null) firstYardLot = f.Id;
        }

        // ------------------------------------------------------------------------------------------------ capture cameras and the summary line
        // Called once per Build after the cameras group exists: adds the two garden captures and logs the totals of this build.
        public static void AddCaptureCameras(Transform cams, TownLayout t)
        {
            AddCam(cams, t, "Cap_HM_Allotment", allotCamLot, 3.4f, 1.65f, 55f, false);
            if (firstYardLot != null) AddCam(cams, t, "Cap_HM_Yard", firstYardLot, 1.0f, 6.5f, 58f, true);   // from above and behind: the ground behind a ring 2 lot is hillside
            Debug.Log("SC HOLLOWMERE garden lots=" + lots.Count + " pieces=" + pieces + " tris=" + tris);
            Debug.Log("SC HOLLOWMERE garden allotments=" + lotsAllot + " yards=" + lotsYard + " yardFirst=" + firstYardLot);
            lots.Clear(); lotsAllot = lotsYard = pieces = 0; tris = 0; firstYardLot = null;
        }

        static void AddCam(Transform cams, TownLayout t, string name, string lotId, float back, float eye, float fov, bool fromBehind)
        {
            Foundation f = t.Foundations.Find(x => x.Id == lotId);
            if (f == null) return;
            Vector3 dir = Quaternion.Euler(0f, f.FacingYaw, 0f) * Vector3.back * (fromBehind ? -1f : 1f);   // lot frame -Z = door side; world direction out of the lot
            Vector3 c = new Vector3(f.Centre.X, 0f, f.Centre.Z);
            Vector3 p = c + dir * (f.Side * 0.5f + back);
            p.y = Mathf.Max(HollowmereLayout.Ground(t, p.x, p.z), HollowmereLayout.WaterY) + eye;
            Vector3 target = c; target.y = f.Y + 0.4f;
            Camera cam = LookTestBuilder.AddCapture(cams, name, p, target, fov);
            cam.farClipPlane = 400f;
        }

        // ------------------------------------------------------------------------------------------------ placing
        // One fenced run of a polyline segment a to b (lot-frame x,z): Fence_Run pieces scaled to fit, fronts turned out of the lot. a to b must go clockwise seen
        // from above with the lot on its right... in practice: direction d, outward normal (-d.z, d.x). showA / showB: the post at the first / last end.
        static void Chain(Transform root, Vector2 a, Vector2 b, bool showA, bool showB)
        {
            Vector2 d = b - a; float len = d.magnitude; if (len < 0.5f) return; d /= len;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / RunPost)); float scale = len / (n * RunPost);
            float yaw = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;   // front (sin, cos) = outward normal (-d.z, d.x)
            for (int i = 0; i < n; i++)
            {
                Vector2 c = a + d * ((i + 0.5f) * len / n);
                GameObject o = Place(root, "Fence_Run", c.x, c.y, yaw, scale);
                if (o == null) continue;
                Vector2 pa = c - d * (RunPost * scale * 0.5f), pb = c + d * (RunPost * scale * 0.5f);
                SetPart(o, "Part_PostA", i == 0 && showA && AddPost(pa));
                SetPart(o, "Part_PostB", (i < n - 1 || showB) && AddPost(pb));
            }
        }

        // True when no post stands within 0.2 m of p yet (then it is recorded); a second post at a junction is hidden.
        static bool AddPost(Vector2 p)
        {
            foreach (Vector2 q in posts) if ((q - p).sqrMagnitude < 0.04f) return false;
            posts.Add(p); return true;
        }

        // A 3 cm slab of packed soil over the footing top, inset 0.2 m from its edge, UVs in metres / 2 (the tileable convention).
        static void Soil(Transform root, float slab)
        {
            float h = slab * 0.5f - 0.2f, y = FootingTop + 0.03f;
            var m = new Mesh { name = "GD_Soil" };
            m.vertices = new[] { new Vector3(-h, y, -h), new Vector3(-h, y, h), new Vector3(h, y, h), new Vector3(h, y, -h) };
            m.uv = new[] { new Vector2(-h, -h) * 0.5f, new Vector2(-h, h) * 0.5f, new Vector2(h, h) * 0.5f, new Vector2(h, -h) * 0.5f };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            var go = new GameObject("GD_Soil"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = Slots()["packed_soil"]; r.shadowCastingMode = ShadowCastingMode.Off;
            tris += 2;
        }

        static void SetPart(GameObject o, string part, bool on)
        {
            Transform t = o.transform.Find(part);
            if (t == null) throw new System.InvalidOperationException("SC HOLLOWMERE garden: " + o.name + " has no " + part);
            t.gameObject.SetActive(on);
        }

        // faceYaw: the direction the art's front (-Y in Blender) faces in the lot frame: front = (sin, cos). lengthScale stretches the piece along its own x.
        static GameObject Place(Transform root, string asset, float x, float z, float faceYaw, float lengthScale = 1f)
        {
            GameObject fbx = Model(asset);
            if (fbx == null) return null;
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
            obj.name = "GD_" + asset;
            obj.transform.localPosition = new Vector3(x, FootingTop, z);
            obj.transform.localRotation = Quaternion.Euler(0f, faceYaw, 0f);
            obj.transform.localScale = new Vector3(lengthScale, asset.StartsWith("Fence_") ? FenceHeight : 1f, 1f);
            Dictionary<string, Material> slots = Slots();
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") continue;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE garden: no material for slot '" + slot + "' on " + asset + "/" + r.name); mats[k] = LookTestBuilder.Mat("oak_timber"); }
                }
                r.sharedMaterials = mats;
            }
            Transform fp = obj.transform.Find("FOOTPRINT");
            if (fp == null) throw new System.InvalidOperationException("SC HOLLOWMERE garden " + asset + ": no FOOTPRINT child");
            var fpr = fp.GetComponent<MeshRenderer>(); if (fpr != null) fpr.enabled = false;
            Mesh m = fp.GetComponent<MeshFilter>().sharedMesh;
            var box = fp.gameObject.AddComponent<BoxCollider>(); box.center = m.bounds.center; box.size = m.bounds.size;
            pieces++;
            foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name == "FOOTPRINT" || mf.sharedMesh == null) continue;
                // counted per placed piece; parts hidden afterwards by Chain are still counted in the asset's own total, so keep the figure an upper bound
                for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += mf.sharedMesh.GetIndexCount(sm) / 3;
            }
            return obj;
        }

        static GameObject Model(string asset)
        {
            if (Loaded.TryGetValue(asset, out GameObject cached) && cached != null) return cached;
            string folder = Folder + "/" + asset;
            GameObject fbx = BeautyCornerArt.LoadModel(folder, asset + ".fbx", keepMaterialSlots: true);
            if (fbx == null) return null;
            // A first import can hand back the prefab built before the importer settings applied (every slot the default "Lit"): reimport once.
            Renderer probe = fbx.GetComponentInChildren<Renderer>(true);
            if (probe != null && probe.sharedMaterials.Length > 0 && probe.sharedMaterials[0] != null && probe.sharedMaterials[0].name == "Lit")
            {
                string path = folder + "/" + asset + ".fbx";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Loaded[asset] = fbx;
            return fbx;
        }

        // ------------------------------------------------------------------------------------------------ materials
        static Dictionary<string, Material> Slots()
        {
            if (slotMats != null && slotMats.Count > 0 && slotMats["Leaf"] != null && slotMats.ContainsKey("Cabbage") && slotMats.ContainsKey("burlap")) return slotMats;
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new System.InvalidOperationException("URP Lit not found");
            slotMats = new Dictionary<string, Material>();
            foreach (string n in new[] { "oak_timber", "canvas", "iron", "sandstone" }) slotMats[n] = LookTestBuilder.Mat(n);
            slotMats["packed_soil"] = Tileable("packed_soil", lit);
            slotMats["meadow_grass"] = Tileable("meadow_grass", lit);
            slotMats["Leaf"] = FlatLit("Leaf", new Color(0x6f / 255f, 0x8a / 255f, 0x34 / 255f), 0.25f, lit);
            slotMats["Produce"] = FlatLit("Produce", new Color(0x7f / 255f, 0x9c / 255f, 0x5c / 255f), 0.3f, lit);
            // Cabbage: the procedural leaf atlas (outer leaf / pale heart halves), waxy, both faces rendered (the leaves are single-sided sheets)
            Material cab = BeautyCornerArt.MaterialFromSet(Folder + "/VegBed_A", "Cabbage", "GD_Cabbage", foliage: false);
            if (cab != null) { cab.SetFloat("_Cull", 0f); EditorUtility.SetDirty(cab); }
            slotMats["Cabbage"] = cab != null ? cab : FlatLit("Cabbage", new Color(0x5b / 255f, 0x7a / 255f, 0x5a / 255f), 0.4f, lit);
            // Scarecrow cloth: the canvas tileable tinted per slot (the Blender `tint` attribute is not read by Unity): coat, burlap head, two patches, their seam thread, hat band
            slotMats["coat"] = TintedCanvas("coat", 0.58f, 0.64f, 0.72f, lit);
            slotMats["burlap"] = TintedCanvas("burlap", 0.95f, 0.80f, 0.55f, lit);
            slotMats["patch_ochre"] = TintedCanvas("patch_ochre", 0.93f, 0.72f, 0.42f, lit);
            slotMats["patch_rust"] = TintedCanvas("patch_rust", 0.74f, 0.42f, 0.34f, lit);
            slotMats["patch_seam"] = TintedCanvas("patch_seam", 0.50f, 0.37f, 0.27f, lit);
            slotMats["leather"] = TintedCanvas("leather", 0.50f, 0.34f, 0.26f, lit);
            slotMats["Straw"] = FlatLit("Straw", new Color(0xc9 / 255f, 0xa8 / 255f, 0x5a / 255f), 0.2f, lit);
            // Glass: dark, smooth and slightly see-through (the B4 facade's FK_Glass colour), straight-alpha blend like the decals
            Material glass = LookTestBuilder.Ensure(MaterialsFolder + "/M_GD_Glass.mat", lit);
            BeautyCornerArt.AlphaBlend(glass);
            glass.SetColor("_BaseColor", new Color(0.05f, 0.075f, 0.095f, 0.45f).gamma); glass.SetColor("_Color", glass.GetColor("_BaseColor"));
            glass.SetFloat("_Smoothness", 0.92f); glass.SetFloat("_SpecularHighlights", 1f); glass.SetFloat("_EnvironmentReflections", 1f);
            glass.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); glass.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(glass); slotMats["Glass"] = glass;
            AssetDatabase.SaveAssets();
            return slotMats;
        }

        // The B1 canvas material with its base colour multiplied by a tint (linear RGB as the generator writes it): same maps, its own asset.
        static Material TintedCanvas(string slot, float r, float g, float b, Shader lit)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_GD_" + slot + ".mat", lit);
            m.CopyPropertiesFromMaterial(LookTestBuilder.Mat("canvas"));
            Color c = new Color(r, g, b).gamma;
            m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material FlatLit(string slot, Color srgb, float smooth, Shader lit)
        {
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_GD_" + slot + ".mat", lit);
            m.SetColor("_BaseColor", srgb); m.SetColor("_Color", srgb); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        // A B1 ground tileable as URP Lit, built the way LookTestBuilder.BuildMaterials does (albedo, normal, metallic-smoothness, occlusion; UV = metres / 2).
        static Material Tileable(string set, Shader lit)
        {
            string dir = TexRoot + set + "/" + set;
            Texture2D Tex(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + suffix + ".png");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_GD_" + set + ".mat", lit);
            Texture2D alb = Tex("_albedo"), nrm = Tex("_normal"), ms = Tex("_ms"), occ = Tex("_occ");
            if (alb == null) Debug.LogWarning("SC HOLLOWMERE garden: texture missing " + dir + "_albedo.png");
            m.SetTexture("_BaseMap", alb); m.SetColor("_BaseColor", Color.white); m.SetColor("_Color", Color.white);
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (ms != null) { m.SetTexture("_MetallicGlossMap", ms); m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (occ != null) { m.SetTexture("_OcclusionMap", occ); m.SetFloat("_OcclusionStrength", 1f); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------------ seeded variety (the same hash as HollowmereBuilder.Rand)
        static float Rand(string id, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in id) { h ^= c; h *= 16777619u; }
                h ^= (uint)salt * 0x9E3779B9u; h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static float Jit(string id, int salt, float amount) => (Rand(id, salt) - 0.5f) * 2f * amount;
    }
}
