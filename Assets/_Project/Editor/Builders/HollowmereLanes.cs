// Hollowmere's ring 2 lane kit in the scene (Q11): garden walls, gates, terrace and lot retaining walls, lane steps, kerbs, gutters and wall lanterns, placed per
// preset from Core/Town/LaneKitPlan (rows with position, yaw, X scale and tilt; the plan decides what goes where, this file only places it).
// Art: Content/Art/Town/Lanes/LaneKit/LaneKit.fbx, root LaneKit with one direct child mesh per piece, each at its own pivot at the origin (contract
// Reference/hollowmere/lanes/CONTRACT.md). A piece is cloned by its child name and stood at the row: Quaternion.Euler(0, Yaw, Tilt), scale (ScaleX, 1, 1).
// Without the FBX (or a missing child) the piece is a greybox box at the contract's extents (BoxOf), on the sandstone / oak / iron look-test materials.
// Materials: the LK_* slot table below mirrors the kit's SOURCES.md (shared B1 tileable, tint, normal scale) on the WearLit shader; LK_GlassLit is emissive. Unknown
// slots log a warning and take plain stone. Colliders: walls, piers and steps get one box (BoxOf); gutters, kerbs, grates, gate leaves and lanterns none.
// Rebuilt lanterns carry a small warm point light (low intensity, no shadows).
// Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereLanes
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Lanes/LaneKit";
        const string FbxName = "LaneKit.fbx", MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere", WearShaderName = "SleepingCalamity/WearLit";
        public const float LanternIntensity = 0.9f, LanternRange = 6f;

        // LK_* slots (LaneKit/SOURCES.md, slots table): shared B1 tileable, linear tint, normal scale. LK_Earth and LK_GlassLit are flat (see BuildMaterials).
        static readonly (string Slot, string Source, Color Tint, float Bump)[] LkSlots =
        {
            ("LK_Rubble", "sandstone", new Color(0.57f, 0.60f, 0.66f), 0.8f), ("LK_Dressed", "sandstone", new Color(1.00f, 0.95f, 0.86f), 0.8f),   // A6: flatter relief (were 1.5, 1.3)
            ("LK_Mortar", "sandstone", new Color(0.26f, 0.245f, 0.21f), 0.6f), ("LK_Oak", "oak_timber", new Color(0.80f, 0.76f, 0.72f), 1.2f),
            ("LK_GatePaint", "painted_wood", new Color(0.66f, 0.72f, 0.28f), 1.0f), ("LK_Iron", "iron", new Color(1f, 1f, 1f), 1.0f),
            ("LK_GutterStone", "sandstone", new Color(0.50f, 0.48f, 0.44f), 1.5f),
            ("LK_StepNosing", "sandstone", new Color(0.60f, 0.56f, 0.48f), 0.5f),   // stair nosing: the sandstone PBR set, lighter and worn against the tread (tint lifted, soft relief), metres UVs from NosingMesh   // gutter channel: LK_Mortar read as a near-black ribbon
        };

        // Local box (centre, size) of each piece at the contract extents: the greybox mesh, and the collider of walls, piers and steps.
        static (Vector3 C, Vector3 S) BoxOf(LanePiece p)
        {
            switch (p)
            {
                case LanePiece.GardenWall: return (new Vector3(0, 0.275f, 0), new Vector3(2, 2.15f, 0.5f));
                case LanePiece.GardenWallBroken: return (new Vector3(0, 0, 0), new Vector3(2, 1.6f, 0.5f));
                case LanePiece.GardenWallEnd: return (new Vector3(0, 0.375f, 0), new Vector3(0.6f, 2.35f, 0.6f));
                case LanePiece.RetainWall: return (new Vector3(0, 1f, 0), new Vector3(2, 4f, 0.7f));
                case LanePiece.RetainWallLow: return (new Vector3(0, 0.45f, 0), new Vector3(2, 2.1f, 0.7f));
                case LanePiece.RetainWallBreach: return (new Vector3(0, 0.5f, 0), new Vector3(2, 3f, 0.7f));
                case LanePiece.RetainButtress: return (new Vector3(0, 0.8f, -0.675f), new Vector3(1, 3.6f, 0.65f));
                case LanePiece.LaneStep: return (new Vector3(0, -0.32f, 0.275f), new Vector3(2, 0.96f, 0.55f));
                case LanePiece.LaneKerb: return (new Vector3(0, -0.09f, 0), new Vector3(2, 0.42f, 0.3f));
                case LanePiece.Gutter: case LanePiece.GutterChoked: return (new Vector3(0, -0.115f, 0), new Vector3(2, 0.27f, 0.4f));
                case LanePiece.GutterGrate: return (new Vector3(0, -0.09f, 0), new Vector3(0.6f, 0.22f, 0.6f));
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return (new Vector3(0, 0f, -0.35f), new Vector3(0.25f, 0.6f, 0.5f));
                case LanePiece.GatePier: return (new Vector3(0, 0.65f, 0), new Vector3(0.6f, 2.9f, 0.6f));
                default: return (new Vector3(0.7f, 0.65f, 0), new Vector3(1.4f, 1.2f, 0.08f));   // GateLeaf, GateLeafBroken (hinge at the origin)
            }
        }

        static bool HasCollider(LanePiece p) => p == LanePiece.GardenWall || p == LanePiece.GardenWallBroken || p == LanePiece.GardenWallEnd || p == LanePiece.RetainWall
            || p == LanePiece.RetainWallLow || p == LanePiece.RetainWallBreach || p == LanePiece.RetainButtress || p == LanePiece.LaneStep || p == LanePiece.GatePier;

        static string GreyMat(LanePiece p)
        {
            switch (p)
            {
                case LanePiece.GateLeaf: case LanePiece.GateLeafBroken: return "oak_timber";
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return "iron";
                case LanePiece.Gutter: case LanePiece.GutterChoked: case LanePiece.GutterGrate: return "cobble";
                default: return "sandstone";
            }
        }

        public static bool ArtPresent => AssetImporter.GetAtPath(Folder + "/" + FbxName) != null;

        // Places every row in both presets under a "Lanes" group of each preset root. `set` is unused (the kit lives in the preset roots) but kept for the
        // shared Build(set, ruined, rebuilt, t) call shape of the other placers.
        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            List<LaneKitRow> rows = LaneKitPlan.Plan(t);
            GameObject fbx = ArtPresent ? BeautyCornerArt.LoadModel(Folder, FbxName, keepMaterialSlots: true) : null;
            Dictionary<string, Material> slots = fbx != null ? BuildMaterials() : null;
            var tally = new List<string>();
            foreach (bool isRebuilt in new[] { false, true })
            {
                Transform group = ChildGroup(isRebuilt ? rebuilt : ruined, "Lanes");
                var sub = new Dictionary<string, Transform>();
                int placed = 0, art = 0;
                foreach (LaneKitRow r in rows)
                {
                    if (!r.In(isRebuilt)) continue;
                    float rowY = r.Y, contactY = float.NaN;
                    bool stepBottom = false;
                    if (r.Piece == LanePiece.LaneStep) { stepBottom = !HasStepNeighbour(rows, r, -1); SeatStepEnds(r, t, stepBottom, !HasStepNeighbour(rows, r, 1), out float sink, out contactY); rowY -= sink; }
                    string family = Family(r);
                    if (!sub.TryGetValue(family, out Transform parent)) sub[family] = parent = ChildGroup(group, family);
                    GameObject obj = TryClone(fbx, r.Piece, slots, parent);
                    if (obj != null) art++; else obj = Greybox(r.Piece, parent);
                    obj.name = r.Id;
                    bool isGutter = r.Piece == LanePiece.Gutter || r.Piece == LanePiece.GutterChoked || r.Piece == LanePiece.GutterGrate;
                    obj.transform.position = new Vector3(r.X, rowY - (isGutter ? 0.02f : 0f), r.Z);
                    obj.transform.rotation = Quaternion.Euler(0f, r.Yaw, r.Tilt);
                    obj.transform.localScale = new Vector3(r.ScaleX, 1f, 1f);
                    if (HasCollider(r.Piece)) { var b = obj.AddComponent<BoxCollider>(); (Vector3 c, Vector3 s) = BoxOf(r.Piece); b.center = c; b.size = s; }
                    if (r.Piece == LanePiece.LaneStep)
                    {
                        // stairs seat 3: a bottom step whose tread lies below the paving in front of it (contactY above its tread) is buried: it is only a walk collider, no visible piece
                        // (its visible stub stuck out beside the next column's sunk first step in Cap_HM_StairSide1)
                        if (stepBottom && contactY > rowY + 0.16f + 0.02f) { foreach (Renderer rend in obj.GetComponentsInChildren<Renderer>()) rend.enabled = false; }
                        else { AddStepSkirt(obj.transform, r, t, rowY); AddStepReadability(obj.transform); if (stepBottom) AddContactStrip(obj.transform, contactY - rowY); }
                    }
                    if (isRebuilt && r.Piece == LanePiece.WallLantern) AddLanternLight(obj.transform);
                    placed++;
                }
                tally.Add((isRebuilt ? "Rebuilt " : "Ruined ") + placed + " (art " + art + ")");
            }
            Debug.Log("SC HOLLOWMERE lane kit placed: " + string.Join(", ", tally) + " rows=" + rows.Count + (fbx == null ? " [greybox]" : ""));
        }

        // stairs seat: the step box hangs 0.8 m under its tread; where the ground beside or under the flight is lower (cross slope, a drop beside the lane) the
        // sides and the front stood in the air with the underside showing. A collider-free stone foot from the step's bottom down to the lowest ground under its footprint
        // (plus 0.25 m of burial), same width and depth, so the flight is seated into the terrain on every side.
        // Stairs seat 2: the step is a solid dressed-stone block from just under its tread down to the lowest ground under its footprint (+0.1 m), full width and run,
        // collider-free (the art step's own box collider stays the walk surface). Its front is the stone riser face (a 1 cm lip past the art mesh's riser plane).
        static Material _dressed, _contact;
        static void AddStepSkirt(Transform step, LaneKitRow r, TownLayout t, float rowY)
        {
            float yaw = r.Yaw * Mathf.Deg2Rad, hw = r.ScaleX + 0.2f;
            Vector3 f = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), rt = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)), p = new Vector3(r.X, 0f, r.Z);
            float low = float.MaxValue;
            foreach (float lx in new[] { -hw, -hw * 0.5f, 0f, hw * 0.5f, hw })
                foreach (float lz in new[] { -0.1f, 0.275f, 0.55f })
                {
                    Vector3 q = p + rt * lx + f * lz;
                    low = Mathf.Min(low, HollowmereLayout.Ground(t, q.x, q.z));
                }
            float bottom = low - 0.1f - rowY, top = 0.10f;   // local y; the tread top is 0.16
            if (bottom >= top - 0.05f) return;
            if (_dressed == null) _dressed = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_Dressed.mat") ?? LookTestBuilder.Mat("sandstone");
            float h = top - bottom;
            GameObject blk = LookTestBuilder.Box(step, "StoneBlock", new Vector3(0f, bottom + h * 0.5f, 0.2695f), new Vector3(2f, h, 0.563f), _dressed, default, 2f);
            Object.DestroyImmediate(blk.GetComponent<Collider>());
            blk.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // is there a LaneStep of the same lane one run downhill (dir -1) or uphill (dir +1) of this one, in the same column
        static bool HasStepNeighbour(List<LaneKitRow> rows, LaneKitRow r, int dir)
        {
            float yaw = r.Yaw * Mathf.Deg2Rad;
            float ex = r.X + Mathf.Sin(yaw) * LaneKitPlan.StepRun * dir, ez = r.Z + Mathf.Cos(yaw) * LaneKitPlan.StepRun * dir;
            foreach (LaneKitRow o in rows)
                if (o.Piece == LanePiece.LaneStep && o != r && o.Lane == r.Lane && Mathf.Abs(Mathf.DeltaAngle(o.Yaw, r.Yaw)) < 2f && (o.X - ex) * (o.X - ex) + (o.Z - ez) * (o.Z - ez) < 0.0225f) return true;
            return false;
        }

        // The bottom step: its tread top may stand at most 0.04 m over the paving in front of it (sunk up to 0.17 m, so the rise to the next tread, at most 0.33 m, stays under the 0.4 m step offset;
        // the collider moves with it). The top step: measured only (the tread meets the landing at the lane height). contactY: the ground level in front of the bottom riser.
        static void SeatStepEnds(LaneKitRow r, TownLayout t, bool bottom, bool top, out float sink, out float contactY)
        {
            sink = 0f; contactY = float.NaN;
            float yaw = r.Yaw * Mathf.Deg2Rad;
            Vector3 f = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)), rt = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)), p = new Vector3(r.X, 0f, r.Z);
            float tread = r.Y + 0.16f;
            if (bottom)
            {
                float g = float.MinValue;
                foreach (float lx in new[] { -0.8f, 0f, 0.8f }) { Vector3 q = p + rt * (lx * r.ScaleX) - f * 0.15f; g = Mathf.Max(g, HollowmereLayout.Ground(t, q.x, q.z)); }
                contactY = g;
                sink = Mathf.Clamp(tread - (g + 0.04f), 0f, 0.17f);
                Debug.Log("SC HOLLOWMERE step bottom " + r.Id + " lane=" + r.Lane + " at=" + r.X.ToString("0.0") + "," + r.Y.ToString("0.00") + "," + r.Z.ToString("0.0") + " yaw=" + r.Yaw.ToString("0") + " treadAboveGround=" + (tread - g).ToString("0.000") + " sink=" + sink.ToString("0.000"));
            }
            if (top)
            {
                float g = float.MinValue;
                foreach (float lx in new[] { -0.8f, 0f, 0.8f }) { Vector3 q = p + rt * (lx * r.ScaleX) + f * 0.75f; g = Mathf.Max(g, HollowmereLayout.Ground(t, q.x, q.z)); }
                Debug.Log("SC HOLLOWMERE step top " + r.Id + " treadVsLanding=" + (tread - g).ToString("0.000"));
            }
        }

        // soft dark contact strip on the ground where the bottom riser meets the paving: three stacked translucent quads (no collider, no shadows)
        static void AddContactStrip(Transform step, float localGroundY)
        {
            if (_contact == null)
            {
                _contact = LookTestBuilder.Ensure(MaterialsFolder + "/M_LK_StepContact.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                _contact.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.03f, 0.15f));
                _contact.SetFloat("_Surface", 1f); _contact.SetFloat("_Blend", 0f); _contact.SetFloat("_SrcBlend", 5f); _contact.SetFloat("_DstBlend", 10f);
                _contact.SetFloat("_SrcBlendAlpha", 1f); _contact.SetFloat("_DstBlendAlpha", 10f); _contact.SetFloat("_ZWrite", 0f);
                _contact.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _contact.SetOverrideTag("RenderType", "Transparent"); _contact.renderQueue = 3000;
                EditorUtility.SetDirty(_contact);
            }
            float[] depth = { 0.14f, 0.28f, 0.45f };
            for (int i = 0; i < depth.Length; i++)
            {
                GameObject q = LookTestBuilder.Prim(step, "ContactStrip" + i, PrimitiveType.Quad, new Vector3(0f, localGroundY + 0.012f + i * 0.001f, -depth[i] * 0.5f - 0.01f), new Vector3(1.96f, depth[i], 1f), _contact);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // A9: tread and riser shapes read in the stone pattern. Local frame of a LaneStep (BoxOf): the tread top at y 0.16 spans z 0..0.55 uphill, the riser is the
        // z = 0 face. A pale nosing along the tread's front edge (visual only, no collider, no shadow casting). A dark riser band was tried in round 1 and floated black wedges over the
        // terrain at the flight's edges (Cap_HM_TerraceSteps), so it is gone.
        static Material _nosing;
        static void AddStepReadability(Transform step)
        {
            if (_nosing == null)
            {
                _nosing = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_StepNosing.mat") ?? LookTestBuilder.Mat("sandstone");
            }
            GameObject n = LookTestBuilder.Prim(step, "Nosing", PrimitiveType.Cube, new Vector3(0f, 0.158f, 0.10f), Vector3.one, _nosing);
            n.GetComponent<MeshFilter>().sharedMesh = NosingMesh();
            n.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // stairs seat 3: soft contact gradients instead of black bars. A thin quad under the nosing on the riser and one on the back of the tread, vertex alpha fading
            // from ~38% dark (30-40% darker than the stone under it) to clear over 5 cm / 14 cm. Translucent, no collider, no shadows, so nothing reads as a black slot.
            if (_shade == null)
            {
                _shade = LookTestBuilder.Ensure(MaterialsFolder + "/M_LK_StepShade.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                _shade.SetColor("_BaseColor", new Color(0.04f, 0.03f, 0.02f, 1f));
                _shade.SetFloat("_Surface", 1f); _shade.SetFloat("_Blend", 0f); _shade.SetFloat("_SrcBlend", 5f); _shade.SetFloat("_DstBlend", 10f);
                _shade.SetFloat("_SrcBlendAlpha", 1f); _shade.SetFloat("_DstBlendAlpha", 10f); _shade.SetFloat("_ZWrite", 0f);
                _shade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); _shade.SetOverrideTag("RenderType", "Transparent"); _shade.renderQueue = 3000;
                EditorUtility.SetDirty(_shade);
            }
            GameObject ri = LookTestBuilder.Prim(step, "RiserShade", PrimitiveType.Quad, new Vector3(0f, 0.1375f - 0.025f, -0.016f), new Vector3(1.96f, 0.05f, 1f), _shade);
            ri.GetComponent<MeshFilter>().sharedMesh = ShadeMesh();
            ri.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObject sh = LookTestBuilder.Prim(step, "TreadShade", PrimitiveType.Quad, new Vector3(0f, 0.162f, 0.55f - 0.07f), new Vector3(1.96f, 0.14f, 1f), _shade);
            sh.GetComponent<MeshFilter>().sharedMesh = ShadeMesh();
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // unit quad in XY facing -Z, y 0..1 (centred on 0.5): vertex alpha 0 at the bottom edge, 0.38 at the top edge
        static Material _shade;
        static Mesh _shadeMesh;
        static Mesh ShadeMesh()
        {
            if (_shadeMesh != null) return _shadeMesh;
            _shadeMesh = new Mesh
            {
                name = "LK_StepShadeGradient",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                colors = new[] { new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0.38f), new Color(1, 1, 1, 0.38f) },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            _shadeMesh.RecalculateNormals(); _shadeMesh.RecalculateBounds();
            return _shadeMesh;
        }

        // 2 x 0.04 x 0.24 m box, five faces (no underside), UVs in metres (one tile per NosingTileMetres) so the stone is not stretched across the thin strip.
        const float NosingTileMetres = 1f;
        static Mesh _nosingMesh;
        static Mesh NosingMesh()
        {
            if (_nosingMesh != null) return _nosingMesh;
            const float hx = 1f, hy = 0.02f, hz = 0.12f, t = NosingTileMetres;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); uv.Add(ua / t); uv.Add(ub / t); uv.Add(uc / t); uv.Add(ud / t);
                Vector3 cr = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(cr, n) >= 0f) tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); else tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Quad(new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), Vector3.up, new Vector2(0, 0), new Vector2(0, 2 * hz), new Vector2(2 * hx, 2 * hz), new Vector2(2 * hx, 0));
            Quad(new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz), Vector3.back, new Vector2(0, 0), new Vector2(0, 2 * hy), new Vector2(2 * hx, 2 * hy), new Vector2(2 * hx, 0));
            Quad(new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz), Vector3.forward, new Vector2(0, 0), new Vector2(0, 2 * hy), new Vector2(2 * hx, 2 * hy), new Vector2(2 * hx, 0));
            Quad(new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz), Vector3.left, new Vector2(0, 0), new Vector2(0, 2 * hy), new Vector2(2 * hz, 2 * hy), new Vector2(2 * hz, 0));
            Quad(new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz), Vector3.right, new Vector2(0, 0), new Vector2(0, 2 * hy), new Vector2(2 * hz, 2 * hy), new Vector2(2 * hz, 0));
            _nosingMesh = new Mesh { name = "LK_StepNosingBox", vertices = v.ToArray(), uv = uv.ToArray(), triangles = tri.ToArray() };
            _nosingMesh.RecalculateNormals(); _nosingMesh.RecalculateTangents(); _nosingMesh.RecalculateBounds();
            return _nosingMesh;
        }

        static string Family(LaneKitRow r)
        {
            if (r.IsRetaining || r.Piece == LanePiece.RetainButtress) return "Retaining";
            switch (r.Piece)
            {
                case LanePiece.LaneStep: case LanePiece.LaneKerb: return "Steps";
                case LanePiece.Gutter: case LanePiece.GutterChoked: case LanePiece.GutterGrate: return "Gutters";
                case LanePiece.WallLantern: case LanePiece.WallLanternBroken: return "Lanterns";
                case LanePiece.GatePier: case LanePiece.GateLeaf: case LanePiece.GateLeafBroken: return "Gates";
                default: return "GardenWalls";
            }
        }

        static GameObject TryClone(GameObject fbx, LanePiece piece, Dictionary<string, Material> slots, Transform parent)
        {
            if (fbx == null) return null;
            Transform child = fbx.transform.Find(piece.ToString());
            if (child == null) { Debug.LogWarning("SC HOLLOWMERE lanes: LaneKit.fbx has no child '" + piece + "', greybox used"); return null; }
            GameObject obj = Object.Instantiate(child.gameObject, parent);
            bool gutter = piece == LanePiece.Gutter || piece == LanePiece.GutterChoked || piece == LanePiece.GutterGrate;
            ApplyMaterials(obj, gutter ? GutterSlots(slots) : slots);
            return obj;
        }

        static Dictionary<string, Material> GutterSlots(Dictionary<string, Material> slots)
        {
            var g = new Dictionary<string, Material>(slots);
            if (slots.TryGetValue("LK_GutterStone", out Material gs) && gs != null) g["LK_Mortar"] = gs;
            return g;
        }

        static GameObject Greybox(LanePiece piece, Transform parent)
        {
            (Vector3 c, Vector3 s) = BoxOf(piece);
            GameObject root = new GameObject(piece.ToString()); root.transform.SetParent(parent, false);
            Material m = LookTestBuilder.Mat(GreyMat(piece));
            GameObject box = LookTestBuilder.Prim(root.transform, "Box", PrimitiveType.Cube, c, s, m);
            if (piece == LanePiece.GateLeafBroken) box.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (piece == LanePiece.WallLantern || piece == LanePiece.WallLanternBroken)
            {
                Material glow = piece == LanePiece.WallLantern ? LookTestBuilder.Mat("LampGlow") : null;
                if (glow != null) LookTestBuilder.Prim(root.transform, "Glass", PrimitiveType.Cube, new Vector3(0, 0, -0.5f), new Vector3(0.18f, 0.4f, 0.18f), glow);
            }
            return root;
        }

        static void AddLanternLight(Transform lantern)
        {
            var go = new GameObject("WarmLight"); go.transform.SetParent(lantern, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.45f);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.42f); l.intensity = LanternIntensity; l.range = LanternRange; l.shadows = LightShadows.None;
        }

        static Dictionary<string, Material> BuildMaterials()
        {
            var map = new Dictionary<string, Material>();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in LkSlots)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                if (src == null) { Debug.LogWarning("SC HOLLOWMERE lanes: LK slot source '" + s.Source + "' missing for " + s.Slot); continue; }
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                m.SetColor("_BaseColor", new Color(Mathf.LinearToGammaSpace(s.Tint.r), Mathf.LinearToGammaSpace(s.Tint.g), Mathf.LinearToGammaSpace(s.Tint.b), 1f));
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                m.SetFloat("_BumpScale", s.Bump);
                EditorUtility.SetDirty(m);
                map[s.Slot] = m;
            }
            map["LK_Earth"] = BeautyCornerArt.Flat("LK_Earth", new Color(0.20f, 0.15f, 0.10f), 0.05f, wear);   // round 2: silt in GutterChoked read as a black ribbon down the Terrace flight
            Material glass = BeautyCornerArt.Flat("LK_GlassLit", new Color(0.9f, 0.55f, 0.2f), 0.7f, wear);   // warm lit glass: emissive linear (1, 0.62, 0.28) x 3
            glass.SetColor("_EmissionColor", new Color(1f, 0.62f, 0.28f) * 3f); glass.EnableKeyword("_EMISSION");
            glass.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; EditorUtility.SetDirty(glass);
            map["LK_GlassLit"] = glass;
            AssetDatabase.SaveAssets();
            return map;
        }

        static void ApplyMaterials(GameObject obj, Dictionary<string, Material> slots)
        {
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k]) || mats[k] == null)
                    {
                        Debug.LogWarning("SC HOLLOWMERE lanes: no material for slot '" + slot + "' on " + r.name);
                        mats[k] = LookTestBuilder.Mat("sandstone");
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        static Transform ChildGroup(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }
    }
}
