// B4 spots (editor time, authority: none): the home door set (S1), the bakery lot's stages (S3) and the market row (S4), from Core HollowmereDressing.Spots (design
// docs/design/area1-hollowmere.md S1, S3, S4 and section 6). Called from HollowmereBuilder.BuildScene after the clutter. Placement by stage:
//   Always (-1)  -> Set/B4Spots (visible in both presets)      Ruined (0) -> Preset_Ruined/B4Spots      Rebuilt (2) -> Preset_Rebuilt/B4Spots
//   Frame (1)    -> Stage1_Frame under the town root, built INACTIVE (the placed frame shows it; no preset shows it yet).
// A piece is an adopted prefab (code T.., scaled so its longest extent is Row.Size), a B4Kit FBX (sc_scaffold_kit.py, facade-kit slots), a Clutter piece (FlowerBox), the procedural
// washing line, or one of ours resolved by name (B7 props, the square's pots). Solid rows get one box collider; loose rows none. Deterministic: row order, no randomness.
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static partial class HollowmereBuilder
    {
        const string AdoptedRoot = "Assets/_Project/Content/Art/Town/Adopted/";
        const string B4KitRoot = "Assets/_Project/Content/Art/Town/B4Kit/";
        static readonly HashSet<string> B4B7Pieces = new HashSet<string> { "Barrel_A", "Barrel_B", "Barrel_C", "Basket_Bread", "Basket_Lidded", "Basket_Produce", "Bucket_A", "Bucket_B", "Crate_A", "Crate_B", "Crate_C", "Jug_A", "Jug_B", "Sack_A", "Sack_B", "Sack_C", "Sack_Pair" };

        static void BuildB4Spots(Transform root, Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            Transform both = Child(Child(set, "B4Spots"), "Both"), ru = Child(Child(ruined, "B4Spots"), "Ruined"), rb = Child(Child(rebuilt, "B4Spots"), "Rebuilt");
            Transform frame = Child(Child(root, "Stage1_Frame"), "Bakery");
            frame.parent.gameObject.SetActive(false);
            Dictionary<string, Material> kitMats = null;
            int placed = 0, missing = 0;
            var missingNames = new HashSet<string>();
            foreach (SpotRow r in HollowmereDressing.Spots)
            {
                Transform parent = r.Stage == HollowmereDressing.Always ? both : r.Stage == HollowmereDressing.StageRuined ? ru : r.Stage == HollowmereDressing.StageRebuilt ? rb : frame;
                float ground = SpotGround(t, r);
                GameObject go = r.Piece == "WashingLine" ? BuildWashingLine(parent, r, ground) : PlaceSpot(parent, r, ground, ref kitMats);
                if (go == null) { missing++; missingNames.Add(r.Piece); continue; }
                if (!r.Loose && !r.Tilted) FitCollider(go);
                placed++;
            }
            Debug.Log("SC HOLLOWMERE B4 spots: " + placed + " placed, " + missing + " missing" + (missing > 0 ? " (" + string.Join(", ", missingNames) + ")" : ""));
        }

        static float SpotGround(TownLayout t, SpotRow r)
        {
            Foundation f = r.Anchor != null ? t.Foundations.FirstOrDefault(x => x.Id == r.Anchor) : null;
            float g = HollowmereLayout.Ground(t, r.X, r.Z);
            if (float.IsNaN(g)) g = 0f;
            if (f != null) g = Mathf.Max(g, f.Y);
            return g + r.Y;
        }

        static GameObject PlaceSpot(Transform parent, SpotRow r, float ground, ref Dictionary<string, Material> kitMats)
        {
            var root = new GameObject("B4_" + r.Id);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(r.X, ground, r.Z), Quaternion.Euler(r.TiltX, r.Yaw, r.TiltZ));
            GameObject art = null;
            bool adopted = r.Piece.Length >= 3 && r.Piece[0] == 'T' && char.IsDigit(r.Piece[1]);
            if (adopted)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdoptedRoot + r.Piece + "/" + r.Piece + ".prefab");
                if (prefab != null) { art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform); ScaleTo(art, r.Size); }
            }
            else if (AssetDatabase.IsValidFolder(B4KitRoot + r.Piece))
            {
                GameObject fbx = BeautyCornerArt.LoadModel(B4KitRoot + r.Piece, r.Piece + ".fbx", keepMaterialSlots: true);
                if (fbx != null)
                {
                    fbx = HollowmereClutter.ReimportIfDefaultLit(fbx, B4KitRoot + r.Piece + "/" + r.Piece + ".fbx", "B4 kit " + r.Piece);
                    kitMats = kitMats ?? BeautyCornerArt.BuildFacadeMaterials();
                    art = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                    foreach (Renderer rd in art.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[Mathf.Max(1, rd.sharedMaterials.Length)];
                        for (int k = 0; k < mats.Length; k++)
                        {
                            string slot = k < rd.sharedMaterials.Length && rd.sharedMaterials[k] != null ? rd.sharedMaterials[k].name : "";
                            int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                            if (!kitMats.TryGetValue(slot, out mats[k])) { Debug.LogWarning("SC HOLLOWMERE B4 kit: no material for slot '" + slot + "' on " + r.Piece); mats[k] = kitMats["FK_Oak"]; }
                        }
                        rd.sharedMaterials = mats;
                    }
                }
            }
            else if (r.Piece == "FlowerBox")
            {
                art = HollowmereClutter.PlaceArt(r.Piece, root.transform);
            }
            else
            {
                var row = new BeautyCornerTable.Row
                {
                    Brief = "B4 spot", Id = "B4_" + r.Id, Batch = B4B7Pieces.Contains(r.Piece) ? "B7" : "Square", Names = new[] { r.Piece },
                    Instances = new[] { new BeautyCornerTable.Instance(0f, 0f, 0f, 0f, 1f, r.Piece) }, Placeholder = new BeautyCornerTable.Part[0],
                };
                BeautyCornerBuilder.PlaceRow(root.transform, row);
                if (root.transform.childCount > 0)
                {
                    Transform placed = root.transform.GetChild(root.transform.childCount - 1);
                    if (!placed.name.StartsWith("PLACEHOLDER_") && placed.GetComponentInChildren<Renderer>(true) != null) { art = placed.gameObject; placed.localPosition = Vector3.zero; placed.localRotation = Quaternion.identity; }
                }
            }
            if (art == null) { Object.DestroyImmediate(root); Debug.LogWarning("SC HOLLOWMERE B4: art missing for " + r.Id + " (" + r.Piece + ")"); return null; }
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return root;
        }

        // Scales the instance so its longest renderer extent is `size` metres, then seats it on the row's ground (the adopted prefab pivot is bottom centre already; this also fixes a drifted one).
        static void ScaleTo(GameObject art, float size)
        {
            if (size <= 0f) return;
            if (!BoundsOf(art, out Bounds b) || b.size.magnitude < 1e-4f) return;
            art.transform.localScale = Vector3.one * (size / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            BoundsOf(art, out Bounds after);
            art.transform.position += new Vector3(0f, art.transform.parent.position.y - after.min.y, 0f);
        }

        static bool BoundsOf(GameObject go, out Bounds b)
        {
            b = default; bool any = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.bounds.size == Vector3.zero) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static void FitCollider(GameObject root)
        {
            Transform tr = root.transform; bool any = false; Bounds lb = default;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.bounds.size == Vector3.zero) continue;
                Bounds w = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 l = tr.InverseTransformPoint(c);
                    if (!any) { lb = new Bounds(l, Vector3.zero); any = true; } else lb.Encapsulate(l);
                }
            }
            if (!any) return;
            var box = root.AddComponent<BoxCollider>(); box.center = lb.center; box.size = lb.size * 0.9f;
        }

        // The home's washing line: a sagging rope from the lean-to's front post to the house wall with five garments in the four dyes (woad blue, ochre, madder red, moss green).
        static GameObject BuildWashingLine(Transform parent, SpotRow r, float ground)
        {
            var root = new GameObject("B4_" + r.Id); root.transform.SetParent(parent, false);
            Vector3 a = new Vector3(6.2f, ground + 1.72f, 10.1f), b = new Vector3(8.0f, ground + 2.05f, 8.9f);
            Material rope = TownSurface.Timber();
            Color[] dyes = { new Color(0.16f, 0.30f, 0.52f), new Color(0.74f, 0.50f, 0.14f), new Color(0.58f, 0.18f, 0.14f), new Color(0.30f, 0.46f, 0.20f), new Color(0.66f, 0.58f, 0.46f) };
            const int segs = 10; Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float u = i / (float)segs; Vector3 p = Vector3.Lerp(a, b, u); p.y -= 4f * 0.22f * u * (1f - u);
                Vector3 mid = (prev + p) * 0.5f; float len = Vector3.Distance(prev, p);
                GameObject seg = LookTestBuilder.Prim(root.transform, "Rope" + i, PrimitiveType.Cylinder, mid, new Vector3(0.012f, len * 0.5f, 0.012f), rope);
                seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, p - prev);
                prev = p;
            }
            for (int g = 0; g < dyes.Length; g++)
            {
                float u = (g + 0.7f) / (dyes.Length + 0.4f); Vector3 p = Vector3.Lerp(a, b, u); p.y -= 4f * 0.22f * u * (1f - u);
                Material m = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/Hollowmere/M_B4_Cloth" + g + ".mat", Shader.Find("Universal Render Pipeline/Lit"));
                m.SetColor("_BaseColor", dyes[g].gamma); m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_Metallic", 0f);
                m.SetShaderPassEnabled("MotionVectors", true); EditorUtility.SetDirty(m);
                GameObject cloth = LookTestBuilder.Prim(root.transform, "Garment" + g, PrimitiveType.Cube, p + new Vector3(0f, -0.2f - 0.03f * (g % 2), 0f), new Vector3(0.05f, 0.4f + 0.06f * (g % 3), 0.3f - 0.03f * (g % 2)), m);
                cloth.transform.rotation = Quaternion.Euler(0f, -56f + 3f * g, 2f * (g - 2));
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return root;
        }
    }
}
