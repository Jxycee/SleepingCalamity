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
            // stairs stone r2 (town-art-leftovers): the jointless step_stone scan for the StoneTread slabs; LK_StepRiser is the sandstone StoneBlock under them, tinted darker than the treads.
            ("LK_StepStone", "step_stone", new Color(0.84f, 0.82f, 0.78f), 1.0f), ("LK_StepRiser", "sandstone", new Color(0.70f, 0.66f, 0.60f), 0.8f),
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
            _treadMeshes.Clear(); _treadJoints.Clear(); _rows = rows; _stone = null; _riser = null;
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
                        else { AddStoneTread(obj.transform, r); AddStepSkirt(obj.transform, r, t, rowY); AddStepReadability(obj.transform); if (stepBottom) AddContactStrip(obj.transform, contactY - rowY); }
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
        static Material _dressed, _contact, _stone, _riser;
        static List<LaneKitRow> _rows;
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
            if (_riser == null) _riser = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_StepRiser.mat") ?? _dressed;   // stairs stone r2: darker than the treads, so the riser reads under the nosing
            float h = top - bottom;
            GameObject blk = LookTestBuilder.Box(step, "StoneBlock", new Vector3(0f, bottom + h * 0.5f, 0.2695f), new Vector3(2f, h, 0.563f), _riser, default, 2f);
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

        // soft dark contact strip on the ground where the bottom riser meets the paving: three stacked translucent quads 4 mm apart (H4: 1 mm risked z-fight at distance; no collider, no shadows)
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
                GameObject q = LookTestBuilder.Prim(step, "ContactStrip" + i, PrimitiveType.Quad, new Vector3(0f, localGroundY + 0.012f + i * 0.004f, -depth[i] * 0.5f - 0.01f), new Vector3(1.96f, depth[i], 1f), _contact);
                q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // A9: tread and riser shapes read in the stone pattern. Local frame of a LaneStep (BoxOf): the tread top at y 0.16 spans z 0..0.55 uphill, the riser is the
        // z = 0 face. A pale nosing along the tread's front edge (visual only, no collider, no shadow casting). A dark riser band was tried in round 1 and floated black wedges over the
        // terrain at the flight's edges (Cap_HM_TerraceSteps), so it is gone.
        // Stairs stone r1 (town-art-leftovers): the pale Nosing strip is gone (it was one more plank band); the StoneTread slabs carry a real chamfered nosing instead.
        static void AddStepReadability(Transform step)
        {
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
            GameObject ri = LookTestBuilder.Prim(step, "RiserShade", PrimitiveType.Quad, new Vector3(0f, 0.1375f - 0.025f, -0.034f), new Vector3(1.96f, 0.05f, 1f), _shade);   // stairs stone r1: 4 mm in front of the slab riser face (z -0.03)
            ri.GetComponent<MeshFilter>().sharedMesh = ShadeMesh();
            ri.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // stairs stone r2 (town-art-leftovers): the back-edge contact band (the foot of the next step's riser) is 15 cm deep and fades to ~66% dark along a curve (was 14 cm, 38%), so each
            // step reads from the descending camera as light nosing, dark line, lower tread.
            GameObject sh = LookTestBuilder.Prim(step, "TreadShade", PrimitiveType.Quad, new Vector3(0f, 0.168f, 0.55f - 0.075f), new Vector3(1.96f, 0.15f, 1f), _shade);
            sh.GetComponent<MeshFilter>().sharedMesh = TreadShadeMesh();
            sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            sh.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // unit quad in XY facing -Z, y 0..1 (centred on 0.5): vertex alpha 0 at the bottom edge, 0.38 at the top edge
        static Material _shade;
        static Mesh _shadeMesh, _treadShadeMesh;
        // like ShadeMesh, three rows (alpha 0 / 0.26 / 0.66 at y 0 / 0.55 / 1): a soft ramp that is darkest right at the next riser
        static Mesh TreadShadeMesh()
        {
            if (_treadShadeMesh != null) return _treadShadeMesh;
            float[] ys = { 0f, 0.55f, 1f }, al = { 0f, 0.26f, 0.66f };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var c = new List<Color>(); var tri = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                v.Add(new Vector3(-0.5f, ys[i] - 0.5f, 0f)); v.Add(new Vector3(0.5f, ys[i] - 0.5f, 0f));
                uv.Add(new Vector2(0, ys[i])); uv.Add(new Vector2(1, ys[i]));
                c.Add(new Color(1, 1, 1, al[i])); c.Add(new Color(1, 1, 1, al[i]));
            }
            for (int i = 0; i < 2; i++) { int a = i * 2; tri.AddRange(new[] { a, a + 2, a + 1, a + 2, a + 3, a + 1 }); }
            _treadShadeMesh = new Mesh { name = "LK_TreadShadeGradient", vertices = v.ToArray(), uv = uv.ToArray(), colors = c.ToArray(), triangles = tri.ToArray() };
            _treadShadeMesh.RecalculateNormals(); _treadShadeMesh.RecalculateBounds();
            return _treadShadeMesh;
        }
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

        // stairs stone r1 (town-art-leftovers): three 0.19 m sett rows per tread read as planks from the play camera; full-depth slabs with staggered joints and a chamfered nosing.
        // The art step's renderers are switched off (its BoxCollider stays the walk surface); a builder-made "StoneTread" child replaces them. Local frame of a LaneStep: tread top
        // y 0.16 over z 0..0.55 uphill, riser at z 0. The child carries localScale.x = 1 / ScaleX, which cancels the step's X scale, so the mesh is authored in true metres
        // (slab widths, bevels and normals are not stretched). Each slab is one piece front to back (z -0.03..0.55, a 3 cm overhanging nosing) from y 0.10 (the top of the
        // StoneBlock skirt) to 0.16 +/- 4 mm, with a 2.5 cm front chamfer, 5 mm back chamfer, 8 mm side chamfers and a 3-5 mm worn dip. Submesh 1 is a dark mortar quad at y 0.145 under the 9 mm joints.
        // Vertex colour is the WearLit mask (R grime, G moss, B polish); never (1,1,1). Meshes are built per (row Id, X scale), cached for the second preset, deterministic (System.Random
        // seeded from the Id) and live in the scene like the other builder meshes. Joints are random per step (seeded by the Id), which staggers neighbours without needing the flight order.
        static readonly Dictionary<string, Mesh> _treadMeshes = new Dictionary<string, Mesh>();
        static Material _mortar;
        static void AddStoneTread(Transform step, LaneKitRow r)
        {
            foreach (Renderer rend in step.GetComponentsInChildren<Renderer>(true)) rend.enabled = false;
            if (_dressed == null) _dressed = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_Dressed.mat") ?? LookTestBuilder.Mat("sandstone");
            if (_stone == null) _stone = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_StepStone.mat") ?? _dressed;   // stairs stone r2: jointless step_stone scan
            if (_mortar == null) _mortar = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_LK_Mortar.mat") ?? _dressed;
            string key = r.Id + "|" + r.ScaleX.ToString("0.###");
            if (!_treadMeshes.TryGetValue(key, out Mesh mesh) || mesh == null) _treadMeshes[key] = mesh = BuildTreadMesh(r);
            var go = new GameObject("StoneTread");
            go.transform.SetParent(step, false);
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, r.ScaleX), 1f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { _stone, _mortar };
            mr.shadowCastingMode = ShadowCastingMode.On; mr.receiveShadows = true;
        }

        // stairs stone r2 (town-art-leftovers): true stagger. Joint x positions (metres, local to the step) are drawn per step from a seed of its Id; a draw is rejected while any
        // joint lies within 0.15 m of a joint of the step one run downhill in the same flight (that step's joints are computed first, recursively), so no joint continues up the flight.
        const float JointClear = 0.15f;
        static readonly Dictionary<string, float[]> _treadJoints = new Dictionary<string, float[]>();
        static LaneKitRow StepBelow(LaneKitRow r)
        {
            if (_rows == null) return null;
            float yaw = r.Yaw * Mathf.Deg2Rad;
            float ex = r.X - Mathf.Sin(yaw) * LaneKitPlan.StepRun, ez = r.Z - Mathf.Cos(yaw) * LaneKitPlan.StepRun;
            foreach (LaneKitRow o in _rows)
                if (o.Piece == LanePiece.LaneStep && o != r && o.Lane == r.Lane && Mathf.Abs(Mathf.DeltaAngle(o.Yaw, r.Yaw)) < 2f && (o.X - ex) * (o.X - ex) + (o.Z - ez) * (o.Z - ez) < 0.0225f) return o;
            return null;
        }
        static float[] JointsFor(LaneKitRow r, int depth = 0)
        {
            string key = r.Id + "|" + r.ScaleX.ToString("0.###");
            if (_treadJoints.TryGetValue(key, out float[] done)) return done;
            LaneKitRow below = depth < 64 ? StepBelow(r) : null;
            float[] bj = below != null ? JointsFor(below, depth + 1) : new float[0];
            float W = 2f * r.ScaleX;
            int count = Mathf.Max(2, Mathf.CeilToInt((W + 0.05f) / 0.95f));
            float[] best = null;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var rnd = new System.Random(StableSeed(r.Id) + attempt * 7919);
                var wd = new float[count]; float sum = 0f;
                for (int i = 0; i < count; i++) { wd[i] = 0.8f + 0.4f * (float)rnd.NextDouble(); sum += wd[i]; }
                var j = new float[count - 1]; float cur = -W * 0.5f; bool ok = true;
                for (int i = 0; i < count - 1; i++)
                {
                    cur += wd[i] / sum * W; j[i] = cur;
                    foreach (float b in bj) if (Mathf.Abs(b - cur) < JointClear) ok = false;
                }
                best = j;
                if (ok) break;
            }
            _treadJoints[key] = best;
            return best;
        }

        static int StableSeed(string s) { unchecked { uint h = 2166136261u; foreach (char ch in s ?? "") h = (h ^ ch) * 16777619u; return (int)(h & 0x7fffffff); } }

        static Mesh BuildTreadMesh(LaneKitRow r)
        {
            var rnd = new System.Random(StableSeed(r.Id));
            float Rnd() => (float)rnd.NextDouble();
            var P = new List<Vector3>(); var N = new List<Vector3>(); var UVs = new List<Vector2>(); var C = new List<Color>();
            var T0 = new List<int>(); var T1 = new List<int>();
            int V(Vector3 p, Vector3 n, Vector2 uv, Color c) { P.Add(p); N.Add(n); UVs.Add(uv); C.Add(c); return P.Count - 1; }
            void Tri(List<int> T, int a, int b, int c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(P[b] - P[a], P[c] - P[a]), n) >= 0f) { T.Add(a); T.Add(b); T.Add(c); } else { T.Add(a); T.Add(c); T.Add(b); }
            }
            void Quad(List<int> T, int a, int b, int c, int d, Vector3 n) { Tri(T, a, b, c, n); Tri(T, a, c, d, n); }

            const float UvScale = 1f / 1.1f, zf = -0.03f, zb = 0.55f, yb = 0.10f, cf = 0.025f, cb = 0.005f, cs = 0.008f, gap = 0.009f;
            float W = 2f * r.ScaleX;
            float[] jx = JointsFor(r);
            int count = jx.Length + 1;
            for (int i = 0; i < count; i++)
            {
                float sx0 = i == 0 ? -W * 0.5f : jx[i - 1], sx1 = i == count - 1 ? W * 0.5f : jx[i];
                bool leftOuter = i == 0, rightOuter = i == count - 1;
                float x0 = sx0 + (leftOuter ? 0f : gap * 0.5f), x1 = sx1 - (rightOuter ? 0f : gap * 0.5f);
                float yt = 0.16f + (Rnd() * 2f - 1f) * 0.004f, dip = 0.003f + 0.002f * Rnd(), rBase = 0.03f + 0.15f * Rnd();
                float offU = Rnd(), offV = Rnd(); bool flip = Rnd() < 0.5f;
                float stepVar = 0.12f * Rnd();   // stairs stone r3 (town-art-leftovers): per-step R offset 0..0.12 so neighbouring steps differ slightly
                Vector2 UV(float a, float b) => flip ? new Vector2(-(a + offU), -(b + offV)) * UvScale : new Vector2(a + offU, b + offV) * UvScale;   // r2: 1 tile = 1.75 m, so no slab shows a repeat
                float Moss(float x)
                {
                    float m = 0.03f;
                    if (leftOuter) m = Mathf.Max(m, 0.5f * Mathf.Clamp01(1f - (x - x0) / 0.06f)); else m = Mathf.Max(m, 0.14f * Mathf.Clamp01(1f - (x - x0) / 0.04f));
                    if (rightOuter) m = Mathf.Max(m, 0.5f * Mathf.Clamp01(1f - (x1 - x) / 0.06f)); else m = Mathf.Max(m, 0.14f * Mathf.Clamp01(1f - (x1 - x) / 0.04f));
                    return m;
                }
                Color Col(float x, float z, float polish, float grime = -1f) => new Color(grime >= 0f ? grime : Mathf.Min(0.2f, rBase * (1f + 0.5f * (z - zf) / (zb - zf))), Moss(x), Mathf.Clamp(polish, 0f, 0.8f), 1f);

                float TopGrime(float z) { float t = Mathf.Clamp01((z - 0.07f) / (zb - cb - 0.07f)); return Mathf.Min(0.6f, 0.40f * t + stepVar * t); }
                float xa = x0 + cs, xb = x1 - cs, za = zf + cf, zc = zb - cb, yF = yt - cf, yB = yt - cb;
                // top grid: 5 x 5 vertices, columns tight at the ends for the moss band, rows dense at the front; analytic smooth normals from the dip bump (1-u^2)(1-v^2)
                float[] gx = { xa, Mathf.Min(x0 + 0.06f, (xa + xb) * 0.5f), (xa + xb) * 0.5f, Mathf.Max(x1 - 0.06f, (xa + xb) * 0.5f), xb };
                // stairs stone r3 (town-art-leftovers): from the descending camera only tread tops show and all read the same pale value (a flat paved floor). Tone, not geometry: rows at
                // z 0.07 and 0.30 so the vertex mask ramps: front 7 cm of the top is polished (B 0.8, R 0, the light nosing line), then R grime ramps to ~0.40 at the back edge (the next riser's foot),
                // plus stepVar per step. Seen from above that is a light nosing and a darker band behind it per step.
                float[] gz = { za, 0.07f, 0.30f, zc - 0.1f, zc };
                float xc = (xa + xb) * 0.5f, hx = (xb - xa) * 0.5f, zcen = (za + zc) * 0.5f, hz = (zc - za) * 0.5f;
                var g = new int[5, 5];
                for (int a = 0; a < 5; a++)
                    for (int b = 0; b < 5; b++)
                    {
                        float u = (gx[a] - xc) / hx, v = (gz[b] - zcen) / hz, bump = (1f - u * u) * (1f - v * v);
                        float dhdx = dip * 2f * u * (1f - v * v) / hx, dhdz = dip * 2f * v * (1f - u * u) / hz;
                        g[a, b] = V(new Vector3(gx[a], yt - dip * bump, gz[b]), new Vector3(-dhdx, 1f, -dhdz).normalized, UV(gx[a], gz[b]), Col(gx[a], gz[b], b <= 1 ? 0.8f : 0.7f * bump, TopGrime(gz[b])));
                    }
                for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) Quad(T0, g[a, b], g[a, b + 1], g[a + 1, b + 1], g[a + 1, b], Vector3.up);

                // chamfer ring: inner corners at the top grid's edge, outer corners at the slab outline (front 2.5 cm, back 5 mm, sides 8 mm wide)
                Vector3 oFL = new Vector3(x0, yF, zf), oFR = new Vector3(x1, yF, zf), oBR = new Vector3(x1, yB, zb), oBL = new Vector3(x0, yB, zb);
                Vector3 iFL = new Vector3(xa, yt, za), iFR = new Vector3(xb, yt, za), iBR = new Vector3(xb, yt, zc), iBL = new Vector3(xa, yt, zc);
                void Bevel(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, bool sideUV, float pol0, float pol1, float grime = -1f)
                {
                    Vector3 n = Vector3.Cross(p2 - p0, p3 - p1).normalized; if (n.y < 0f) n = -n;
                    Vector2 Map(Vector3 p) => sideUV ? UV(p.z, p.y) : UV(p.x, p.y);
                    int a = V(p0, n, Map(p0), Col(p0.x, p0.z, pol0, grime)), b = V(p1, n, Map(p1), Col(p1.x, p1.z, pol0, grime));
                    int c = V(p2, n, Map(p2), Col(p2.x, p2.z, pol1, grime)), d = V(p3, n, Map(p3), Col(p3.x, p3.z, pol1, grime));
                    Quad(T0, a, b, c, d, n);
                }
                Bevel(oFL, oFR, iFR, iFL, false, 0.8f, 0.7f, 0.0f);   // front chamfer: 45 degrees, polished and clean (the light nosing line); r2: grime 0
                Bevel(oBR, oBL, iBL, iBR, false, 0.05f, 0.05f);
                Bevel(oBL, oFL, iFL, iBL, true, 0.1f, 0.05f);
                Bevel(oFR, oBR, iBR, iFR, true, 0.1f, 0.05f);
                // walls (y 0.10 up to the chamfer outline); the underside sits on the StoneBlock skirt and is not built
                void Wall(Vector3 lo0, Vector3 lo1, Vector3 hi1, Vector3 hi0, Vector3 n, bool sideUV)
                {
                    Vector2 Map(Vector3 p) => sideUV ? UV(p.z, p.y) : UV(p.x, p.y);
                    float pol = n.z < -0.5f ? 0.2f : 0f;
                    float gr = n.z < -0.5f ? 0.40f : -1f;   // r2: the front face (riser lip) is grimed to R 0.40, a deliberate step darker than the polished chamfer above it
                    int a = V(lo0, n, Map(lo0), Col(lo0.x, lo0.z, 0f, gr)), b = V(lo1, n, Map(lo1), Col(lo1.x, lo1.z, 0f, gr));
                    int c = V(hi1, n, Map(hi1), Col(hi1.x, hi1.z, pol, gr)), d = V(hi0, n, Map(hi0), Col(hi0.x, hi0.z, pol, gr));
                    Quad(T0, a, b, c, d, n);
                }
                Wall(new Vector3(x0, yb, zf), new Vector3(x1, yb, zf), oFR, oFL, Vector3.back, false);
                Wall(new Vector3(x1, yb, zb), new Vector3(x0, yb, zb), oBL, oBR, Vector3.forward, false);
                Wall(new Vector3(x0, yb, zb), new Vector3(x0, yb, zf), oFL, oBL, Vector3.left, true);
                Wall(new Vector3(x1, yb, zf), new Vector3(x1, yb, zb), oBR, oFR, Vector3.right, true);
            }
            // mortar: one dark quad under the joints, y 0.145, the full tread footprint (slabs hide all but the 9 mm gaps)
            Color mc = new Color(0.55f, 0.1f, 0f, 1f);
            int m0 = V(new Vector3(-W * 0.5f, 0.145f, zf), Vector3.up, new Vector2(-W * 0.5f, zf), mc), m1 = V(new Vector3(-W * 0.5f, 0.145f, zb), Vector3.up, new Vector2(-W * 0.5f, zb), mc);
            int m2 = V(new Vector3(W * 0.5f, 0.145f, zb), Vector3.up, new Vector2(W * 0.5f, zb), mc), m3 = V(new Vector3(W * 0.5f, 0.145f, zf), Vector3.up, new Vector2(W * 0.5f, zf), mc);
            Quad(T1, m0, m1, m2, m3, Vector3.up);

            var mesh = new Mesh { name = "LK_StoneTread_" + r.Id, vertices = P.ToArray(), normals = N.ToArray(), uv = UVs.ToArray(), colors = C.ToArray(), subMeshCount = 2 };
            mesh.SetTriangles(T0, 0); mesh.SetTriangles(T1, 1);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
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
