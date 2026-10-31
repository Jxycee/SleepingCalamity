// Hollowmere market and lane clutter (queue Q14): places Core/Town/HollowmereClutter's plan under each preset root's child "Clutter".
// Art: Assets/_Project/Content/Art/Town/Clutter/<Asset>/<Asset>.fbx from Art/Blender/town/sc_clutter.py (contract Reference/hollowmere/clutter/CONTRACT.md): the front is
// local +Z, the pivot the ground centre of the footprint, a flat FOOTPRINT box (renderer disabled here). A missing FBX falls back to a greybox sized to the footprint.
//  - Ground rows: HollowmereClutter.Rows of the preset, on HollowmereLayout.Ground (never y = 0), a BoxCollider per row, tilted props sat on the ground.
//  - Signs: ShopSign_<Trade> with the pivot on the wall face, 3.0 m above the ground there, the arm out along the lot's door direction.
//  - Washing lines: Rope_1m pieces along a catenary between two Hook parts, Cloth_* parts every 0.7 to 1.0 m (seeded mix), pegs at the top.
//  - Slots: CL_* material names map to the shared B1 tileables (+ a tint) or a flat colour, built like HollowmereArt's Tinted / Flat specs as M_CL_*.mat.
// The EditMode tests (HollowmereClutterTests) read the same plan; the summary log line is "SC HOLLOWMERE clutter: ...".
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Plan = SleepingCalamity.Core.HollowmereClutter;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereClutter
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Clutter";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string WearShaderName = "SleepingCalamity/WearLit";
        const float RopeLength = 1f;                 // Rope_1m runs along +X from 0 to 1
        const float ClothDrop = 0.12f;               // the line's end stands this far out from the wall hook
        static readonly string[] Cloths = { "Cloth_Shirt", "Cloth_Sheet", "Cloth_Apron", "Cloth_Towel", "Cloth_Socks" };
        static readonly string[] Rags = { "Cloth_Rag_A", "Cloth_Rag_B" };

        // slot -> source tileable and linear tint (the contract table); flat slots a colour and smoothness
        static readonly (string Slot, string Source, Color Tint)[] TintedSlots =
        {
            ("CL_Oak", "oak_timber", new Color(0.80f, 0.72f, 0.62f)), ("CL_OakGrey", "oak_timber", new Color(0.62f, 0.60f, 0.58f)),
            ("CL_Paint", "plaster", new Color(0.62f, 0.74f, 0.54f)), ("CL_PaintRed", "plaster", new Color(0.82f, 0.40f, 0.28f)),   // plaster, not painted_wood: that map is blue-grey and turned the sage teal
            ("CL_Iron", "iron", new Color(1f, 1f, 1f)), ("CL_Stone", "sandstone", new Color(1f, 1f, 1f)), ("CL_Tile", "roof_tile", new Color(1f, 1f, 1f)),
            ("CL_Canvas", "canvas", new Color(1.0f, 0.94f, 0.82f)), ("CL_Rope", "canvas", new Color(1.0f, 0.85f, 0.62f)),
            ("CL_Linen", "canvas", new Color(1.15f, 1.12f, 1.05f)), ("CL_ClothBlue", "canvas", new Color(0.45f, 0.58f, 0.80f)),
            ("CL_ClothRed", "canvas", new Color(0.95f, 0.55f, 0.45f)), ("CL_Rag", "canvas", new Color(0.62f, 0.58f, 0.52f)),
        };
        static readonly (string Slot, Color Linear, float Smooth)[] FlatSlots =
        {
            ("CL_Soil", new Color(0.10f, 0.07f, 0.045f), 0.15f), ("CL_Grain", new Color(0.62f, 0.48f, 0.25f), 0.2f), ("CL_Leaf", new Color(0.18f, 0.30f, 0.08f), 0.3f),
            ("CL_Petal", new Color(0.85f, 0.35f, 0.30f), 0.3f), ("CL_PetalB", new Color(0.85f, 0.72f, 0.25f), 0.3f), ("CL_Dead", new Color(0.30f, 0.20f, 0.10f), 0.2f),
            ("CL_Water", new Color(0.025f, 0.04f, 0.03f), 0.94f), ("CL_Gilt", new Color(0.70f, 0.48f, 0.16f), 0.5f),
        };

        static readonly Dictionary<string, GameObject> Loaded = new Dictionary<string, GameObject>();
        static Dictionary<string, Material> slotMats;
        static int rowsArt, rowsGrey, signsArt, signsGrey, linesArt, linesGrey, partsPlaced; static long tris;

        // ------------------------------------------------------------------------------------------------ entry
        // Called once from HollowmereBuilder.BuildScene after the square and the lots are placed.
        public static void Build(Transform ruined, Transform rebuilt, TownLayout t)
        {
            rowsArt = rowsGrey = signsArt = signsGrey = linesArt = linesGrey = partsPlaced = 0; tris = 0;
            lineKit = null; lineKitTried = false;
            var ru = Child(ruined, "Clutter"); var rb = Child(rebuilt, "Clutter");
            int nRb = 0, nRu = 0;
            foreach (ClutterRow r in Plan.Rows)
            {
                PlaceRow(r.Preset == DecoPreset.Ruined ? ru : rb, r, t);
                if (r.Preset == DecoPreset.Ruined) nRu++; else nRb++;
            }
            foreach (ClutterSign s in Plan.Signs) PlaceSign(rb, s, t);
            foreach (WashingSpan w in Plan.Spans) PlaceLine(w.Preset == DecoPreset.Ruined ? ru : rb, w, t);
            Debug.Log("SC HOLLOWMERE clutter: rebuilt rows=" + nRb + " ruined rows=" + nRu + " signs=" + Plan.Signs.Count + " washing lines=" + Plan.Spans.Count
                + " | art rows=" + rowsArt + " greybox rows=" + rowsGrey + " art signs=" + signsArt + " greybox signs=" + signsGrey
                + " art lines=" + linesArt + " greybox lines=" + linesGrey + " line parts=" + partsPlaced + " tris=" + tris);
        }

        static Transform Child(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ------------------------------------------------------------------------------------------------ ground rows
        static void PlaceRow(Transform parent, ClutterRow r, TownLayout t)
        {
            // stand on the highest corner of the footprint so the base never floats on a slope (the plan keeps the corners within a slope limit)
            Footprint fp = Plan.FootprintOf(r);
            float y = HollowmereLayout.Ground(t, r.X, r.Z);
            foreach (TownPoint p in fp.Corners()) y = Mathf.Max(y, HollowmereLayout.Ground(t, p.X, p.Z));
            var root = new GameObject("Clutter_" + r.Id).transform; root.SetParent(parent, false);
            root.SetPositionAndRotation(new Vector3(r.X, y, r.Z), Quaternion.Euler(0f, r.Yaw, 0f));

            GameObject art = Instantiate(r.Asset, root);
            Transform body;
            if (art != null) { body = art.transform; rowsArt++; }
            else { body = Greybox(root, r); rowsGrey++; }
            if (r.Tilted) SitOnGround(root, body, r, y);

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, r.Height * 0.5f, 0f); box.size = new Vector3(r.SizeX, r.Height, r.SizeZ);
        }

        // A toppled prop: tilt the art about its base, then drop it onto the ground and centre it on the footprint.
        static void SitOnGround(Transform root, Transform body, ClutterRow r, float ground)
        {
            body.localRotation = Quaternion.Euler(r.TiltX, 0f, r.TiltZ);
            Bounds b = default; bool any = false;
            foreach (Renderer rd in body.GetComponentsInChildren<Renderer>(true))
            {
                if (rd.name == "FOOTPRINT" || !rd.enabled || rd.bounds.size == Vector3.zero) continue;
                if (!any) { b = rd.bounds; any = true; } else b.Encapsulate(rd.bounds);
            }
            if (!any) return;
            body.position += new Vector3(root.position.x - b.center.x, ground - b.min.y, root.position.z - b.center.z);
        }

        // ------------------------------------------------------------------------------------------------ signs
        static void PlaceSign(Transform parent, ClutterSign s, TownLayout t)
        {
            float y = HollowmereLayout.Ground(t, s.X, s.Z) + s.PivotHeight;
            var root = new GameObject(s.Id).transform; root.SetParent(parent, false);
            root.SetPositionAndRotation(new Vector3(s.X, y, s.Z), Quaternion.Euler(0f, s.Yaw, 0f));
            GameObject art = Instantiate(s.Asset, root);
            if (art != null) { signsArt++; return; }
            signsGrey++;
            Material iron = LookTestBuilder.Mat("iron"), wood = LookTestBuilder.Mat("painted_wood");
            LookTestBuilder.Box(root, "Plate", new Vector3(0f, 0f, 0.02f), new Vector3(0.3f, 0.4f, 0.04f), iron);
            LookTestBuilder.Box(root, "Arm", new Vector3(0f, 0.12f, Plan.SignArm * 0.5f), new Vector3(0.05f, 0.05f, Plan.SignArm), iron);
            LookTestBuilder.Box(root, "Board", new Vector3(0f, -0.3f, Plan.SignArm * 0.65f), new Vector3(0.05f, 0.5f, 0.6f), wood);   // face along the arm
        }

        // ------------------------------------------------------------------------------------------------ washing lines
        sealed class LineKit
        {
            public GameObject Fbx; public Dictionary<string, (Mesh Mesh, Material[] Mats)> Parts = new Dictionary<string, (Mesh, Material[])>();
        }
        static LineKit lineKit; static bool lineKitTried;

        static LineKit Kit()
        {
            if (lineKitTried) return lineKit;
            lineKitTried = true;
            GameObject fbx = Model("WashingLine");
            if (fbx == null) return null;
            Dictionary<string, Material> slots = Slots();
            var kit = new LineKit { Fbx = fbx };
            foreach (MeshFilter mf in fbx.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name == "FOOTPRINT") continue;
                var mr = mf.GetComponent<MeshRenderer>();
                kit.Parts[mf.name] = (mf.sharedMesh, MapSlots(mr != null ? mr.sharedMaterials : new Material[0], slots, "WashingLine/" + mf.name));
            }
            foreach (string need in new[] { "Rope_1m", "Hook" })
                if (!kit.Parts.ContainsKey(need)) throw new System.InvalidOperationException("WashingLine.fbx has no " + need);
            lineKit = kit;
            return kit;
        }

        static void PlaceLine(Transform parent, WashingSpan w, TownLayout t)
        {
            var root = new GameObject(w.Id).transform; root.SetParent(parent, false);
            var a = new Vector3(w.AX, HollowmereLayout.Ground(t, w.AX, w.AZ) + w.AH, w.AZ);
            var e = new Vector3(w.BX, HollowmereLayout.Ground(t, w.BX, w.BZ) + w.BH, w.BZ);
            Vector3 flat = new Vector3(e.x - a.x, 0f, e.z - a.z).normalized;
            // the rope runs between the hook tips, a little out from each wall; a parabola with Sag at the middle (as the bunting)
            Vector3 pa = a + flat * ClothDrop, pe = e - flat * ClothDrop;
            Vector3 P(float s) { Vector3 p = Vector3.Lerp(pa, pe, s); p.y -= 4f * w.Sag * s * (1f - s); return p; }
            LineKit kit = Kit();

            // hooks: pivot at the wall plane, out along local +Z (toward the other end of the line)
            PlacePart(root, kit, "Hook", a, Quaternion.LookRotation(flat, Vector3.up), Vector3.one, "Hook_A");
            PlacePart(root, kit, "Hook", e, Quaternion.LookRotation(-flat, Vector3.up), Vector3.one, "Hook_B");

            int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(pa, pe) / RopeLength));
            var pts = new Vector3[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = P(i / (float)n);
            var arc = new float[n + 1];
            for (int i = 1; i <= n; i++) arc[i] = arc[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            float total = arc[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 d = pts[i + 1] - pts[i];
                PlacePart(root, kit, "Rope_1m", pts[i], Quaternion.FromToRotation(Vector3.right, d), new Vector3(d.magnitude / RopeLength, 1f, 1f), "Rope_" + i);
            }

            // cloths: a seeded mix, 0.7 to 1.0 m apart (a sheet needs more room), from 0.5 m off the first hook
            var rnd = new System.Random(w.Id.Aggregate(11, (x, c) => x * 31 + c));
            string[] set = w.Torn ? Rags : Cloths;
            int k = 0; float at = 0.5f + (float)rnd.NextDouble() * 0.3f;
            while (at < total - 0.5f)
            {
                string name = set[rnd.Next(set.Length)];
                float width = name == "Cloth_Sheet" ? 1.2f : 0.7f;
                if (at + width * 0.5f > total - 0.3f) break;
                int seg = 1; while (seg < n && arc[seg] < at) seg++;
                float f = Mathf.InverseLerp(arc[seg - 1], arc[seg], at);
                Vector3 pos = Vector3.Lerp(pts[seg - 1], pts[seg], f), tan = (pts[seg] - pts[seg - 1]).normalized;
                Vector3 up = (Vector3.up - tan * Vector3.Dot(Vector3.up, tan)).normalized;
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(tan, up), up);   // local +X along the rope, hanging -Y, billow in Z
                PlaceCloth(root, kit, name, pos, rot, k);
                if (kit != null && kit.Parts.ContainsKey("Peg")) PlacePart(root, kit, "Peg", pos + tan * (width * 0.5f - 0.08f), rot, Vector3.one, "Peg_" + k + "b");
                k++;
                at += width * 0.5f + 0.7f + (float)rnd.NextDouble() * 0.3f + (name == "Cloth_Sheet" ? 0.4f : 0f);
            }
            if (kit != null) linesArt++; else linesGrey++;
        }

        static void PlaceCloth(Transform root, LineKit kit, string name, Vector3 pos, Quaternion rot, int k)
        {
            if (kit != null && kit.Parts.ContainsKey(name)) { PlacePart(root, kit, name, pos, rot, Vector3.one, name + "_" + k); return; }
            if (kit != null) throw new System.InvalidOperationException("WashingLine.fbx has no " + name);
            // greybox: a flat sheet hanging from the line
            string[] mats = { "canvas", "plaster", "canvas" };
            GameObject q = LookTestBuilder.Box(root, "PLACEHOLDER_" + name + "_" + k, pos + rot * new Vector3(0f, -0.4f, 0f), new Vector3(name == "Cloth_Sheet" ? 1.2f : 0.6f, 0.8f, 0.01f), LookTestBuilder.Mat(mats[k % mats.Length]), default, 1f);
            q.transform.rotation = rot; q.transform.position = pos + rot * new Vector3(0f, -0.4f, 0f);
        }

        static void PlacePart(Transform root, LineKit kit, string part, Vector3 pos, Quaternion rot, Vector3 scale, string name)
        {
            partsPlaced++;
            if (kit == null || !kit.Parts.TryGetValue(part, out var p))
            {
                if (part == "Rope_1m")
                {
                    GameObject c = LookTestBuilder.Prim(root, "PLACEHOLDER_" + name, PrimitiveType.Cylinder, pos, new Vector3(0.025f, scale.x * 0.5f * RopeLength, 0.025f), LookTestBuilder.Mat("canvas"));
                    c.transform.rotation = rot * Quaternion.Euler(0f, 0f, 90f); c.transform.position = pos + rot * new Vector3(scale.x * 0.5f * RopeLength, 0f, 0f);
                }
                return;
            }
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = p.Mats;
            r.shadowCastingMode = part == "Peg" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            for (int sm = 0; sm < p.Mesh.subMeshCount; sm++) tris += p.Mesh.GetIndexCount(sm) / 3;
        }

        // ------------------------------------------------------------------------------------------------ art and materials
        static GameObject Model(string asset)
        {
            if (Loaded.TryGetValue(asset, out GameObject cached) && cached != null) return cached;
            GameObject fbx = BeautyCornerArt.LoadModel(Folder + "/" + asset, asset + ".fbx", keepMaterialSlots: true);
            if (fbx == null) return null;
            // A first import can hand back the prefab built before the importer settings applied (every slot the default "Lit"): reimport once.
            Renderer probe = fbx.GetComponentInChildren<Renderer>(true);
            if (probe != null && probe.sharedMaterials.Length > 0 && probe.sharedMaterials[0] != null && probe.sharedMaterials[0].name == "Lit")
            {
                string path = Folder + "/" + asset + "/" + asset + ".fbx";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Loaded[asset] = fbx;
            return fbx;
        }

        // The prefab instance of an asset with its slots mapped, FOOTPRINT hidden. Null when the art is not there (the caller builds a greybox).
        static GameObject Instantiate(string asset, Transform parent)
        {
            GameObject fbx = Model(asset);
            if (fbx == null) return null;
            Dictionary<string, Material> slots = Slots();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
            obj.name = "Art_" + asset;
            obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.identity;
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == "FOOTPRINT") { r.enabled = false; continue; }
                r.sharedMaterials = MapSlots(r.sharedMaterials, slots, asset + "/" + r.name);
                foreach (MeshFilter mf in r.GetComponents<MeshFilter>())
                    if (mf.sharedMesh != null) for (int sm = 0; sm < mf.sharedMesh.subMeshCount; sm++) tris += mf.sharedMesh.GetIndexCount(sm) / 3;
            }
            return obj;
        }

        static Material[] MapSlots(Material[] current, Dictionary<string, Material> slots, string where)
        {
            var mats = new Material[Mathf.Max(1, current.Length)];
            for (int k = 0; k < mats.Length; k++)
            {
                string slot = k < current.Length && current[k] != null ? current[k].name : "";
                int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                if (!slots.TryGetValue(slot, out mats[k])) throw new System.InvalidOperationException("SC HOLLOWMERE clutter: no material for slot '" + slot + "' on " + where);
            }
            return mats;
        }

        static Dictionary<string, Material> Slots()
        {
            if (slotMats != null && slotMats.Count > 0 && slotMats.Values.All(m => m != null)) return slotMats;
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? throw new System.InvalidOperationException("Shader not found: " + WearShaderName);
            slotMats = new Dictionary<string, Material>();
            foreach (var s in TintedSlots) slotMats[s.Slot] = HollowmereArt.Tinted(s.Slot, s.Source, s.Tint, wear);
            foreach (var f in FlatSlots) slotMats[f.Slot] = HollowmereArt.Flat(f.Slot, f.Linear, f.Smooth, wear);
            AssetDatabase.SaveAssets();
            return slotMats;
        }

        // ------------------------------------------------------------------------------------------------ greybox
        // Boxes and cylinders sized to the footprint and height, in the row's frame (front = local +Z). Returns the holder.
        static Transform Greybox(Transform root, ClutterRow r)
        {
            var g = new GameObject("PLACEHOLDER_" + r.Asset).transform; g.SetParent(root, false);
            float sx = r.SizeX, sz = r.SizeZ, h = r.Height;
            Material W() => r.Broken ? BeautyCornerBuilder.PlaceholderMat("dark") : LookTestBuilder.Mat("oak_timber");
            switch (r.Kind)
            {
                case ClutterKind.RainBarrel:
                    LookTestBuilder.Prim(g, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, h * 0.5f, 0f), new Vector3(sx, h * 0.5f, sz), LookTestBuilder.Mat("oak_timber"));
                    break;
                case ClutterKind.Well:
                    LookTestBuilder.Prim(g, "Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.4f, 1.6f), LookTestBuilder.Mat("sandstone"));
                    if (!r.Broken)
                    {
                        LookTestBuilder.Box(g, "PostL", new Vector3(-0.7f, 1.5f, 0f), new Vector3(0.15f, 3f, 0.15f), W());
                        LookTestBuilder.Box(g, "PostR", new Vector3(0.7f, 1.5f, 0f), new Vector3(0.15f, 3f, 0.15f), W());
                        LookTestBuilder.Box(g, "Roof", new Vector3(0f, 2.9f, 0f), new Vector3(1.9f, 0.25f, 1.4f), LookTestBuilder.Mat("roof_tile"));
                    }
                    else LookTestBuilder.Box(g, "Stump", new Vector3(-0.7f, 0.7f, 0f), new Vector3(0.15f, 1.4f, 0.15f), W());
                    break;
                case ClutterKind.SackStack:
                    LookTestBuilder.Box(g, "Sacks", new Vector3(0f, h * 0.5f, 0f), new Vector3(sx, h, sz), LookTestBuilder.Mat("canvas"));
                    break;
                case ClutterKind.FlowerBox:
                    LookTestBuilder.Box(g, "Trough", new Vector3(0f, h * 0.35f, 0f), new Vector3(sx, h * 0.7f, sz), W());
                    LookTestBuilder.Box(g, "Flowers", new Vector3(0f, h * 0.75f, 0f), new Vector3(sx * 0.9f, h * 0.3f, sz * 0.8f), BeautyCornerBuilder.PlaceholderMat("green"));
                    break;
                case ClutterKind.Handcart:
                    LookTestBuilder.Box(g, "Bed", new Vector3(0f, h * 0.5f, -sz * 0.1f), new Vector3(sx, 0.35f, sz * 0.65f), W());
                    LookTestBuilder.Box(g, "Shafts", new Vector3(0f, h * 0.45f, sz * 0.3f), new Vector3(sx * 0.8f, 0.06f, sz * 0.4f), W());
                    LookTestBuilder.Prim(g, "WheelL", PrimitiveType.Cylinder, new Vector3(-sx * 0.5f, 0.35f, -sz * 0.1f), new Vector3(0.7f, 0.04f, 0.7f), LookTestBuilder.Mat("iron")).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    LookTestBuilder.Prim(g, "WheelR", PrimitiveType.Cylinder, new Vector3(sx * 0.5f, 0.35f, -sz * 0.1f), new Vector3(0.7f, 0.04f, 0.7f), LookTestBuilder.Mat("iron")).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                default:   // CrateStack
                    LookTestBuilder.Box(g, "CratesLow", new Vector3(0f, h * 0.25f, 0f), new Vector3(sx, h * 0.5f, sz), W());
                    LookTestBuilder.Box(g, "CratesHigh", new Vector3(-sx * 0.1f, h * 0.75f, 0f), new Vector3(sx * 0.6f, h * 0.5f, sz * 0.85f), W(), new Vector3(0f, 12f, 0f));
                    break;
            }
            return g;
        }
    }
}
