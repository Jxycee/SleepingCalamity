// Beyond the Lower Gate: the Saltwake road and the country round Hollowmere (QUEUE Q15). The plan is Core/Town/SaltwakeRoadPlan (what stands where); this
// file only builds it:
//  1. Roadside art (both presets, under <Preset>/Road): Milestone_A/B, Shrine / Shrine_Ruined, Cart / Cart_Broken, Fence_Rail / Fence_Rail_Broken, Hedge_A/B
//     from Content/Art/Town/Road/<Piece>/<Piece>.fbx (Art/Blender/town/sc_saltwake_road.py, SOURCES.md there). Front = FBX local +Z (Blender -Y), like the
//     wall kit; the cart and the fence/hedge runs lie along local X. Slots map through the wall kit's table (FK_*, TW_*) plus B9_Foliage. Each piece stands
//     on the terrain's own height (the eroded open ground), buried by its art. Without an FBX a small greybox stands in, so the scene builds before the art.
//  2. The country (Set/Country, both presets): hedgerow cards on the field edges and crossed-card hedgerow trees and copses, from the far-tree impostor atlas
//     (Road/FarTrees/T_Road_FarTrees_*: 4 cells of 512 x 1024), merged into one mesh per 100 m tile (static), on the terrain and on the backdrop ring.
//     Cards are the standard far-foliage practice (Unity's own tree billboards are the same idea); they never stand within 25 m of the road (the plan's rule).
//  3. The backdrop ring (Set/Country/Ring): one mesh from the terrain's edge out to SaltwakeRoadPlan.RingRadius, its heights continuing
//     HollowmereLayout.NaturalGround (with the western fall) at the edge and rising into low far hills; a generated field-patchwork texture (the same
//     SaltwakeRoadPlan.Field cells as the terrain's splat, so the fields carry on past the edge), and a dirt ribbon carrying the road on toward Saltwake.
// The town's haze (HollowmereLook, exp2 0.012) leaves almost nothing past ~150 m, so the low cameras' horizon is framed by the terrain's tree lines
// (40-140 m); the ring shows in the overview (its own thin haze) and fills the fogged horizon line with ground instead of sky.
// Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereRoad
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Road";
        const string TreesFolder = Folder + "/FarTrees", BackdropFolder = Folder + "/Backdrop";
        const string FieldsTexPath = BackdropFolder + "/T_Road_Fields.png";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const float TreeHeight = 10f, TileSize = 100f;
        const float CartYaw = 90f;   // the plan's cart yaw points local +Z along the road; the cart model's length runs along local X

        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        static Dictionary<string, Material> slots;
        static Terrain terrain;
        static int placed, greybox, cards;

        // ------------------------------------------------------------------------------------------------ entry
        public static void Build(Transform set, Transform ruined, Transform rebuilt, TownLayout t)
        {
            Models.Clear(); slots = null; placed = greybox = cards = 0;
            terrain = set.GetComponentInChildren<Terrain>();
            List<RoadItem> plan = SaltwakeRoadPlan.Create(t);

            Transform roadR = Group(ruined, "Road"), roadB = Group(rebuilt, "Road");
            foreach (RoadItem i in plan)
            {
                if (i.Kind == RoadItemKind.Tree || i.Kind == RoadItemKind.FieldHedge) continue;
                if ((i.Presets & RoadPresets.Ruined) != 0) Piece(roadR, i);
                if ((i.Presets & RoadPresets.Rebuilt) != 0) Piece(roadB, i);
            }

            Transform country = Group(set, "Country");
            Cards(country, plan, t);
            Ring(country, t);
            Debug.Log("SC HOLLOWMERE road: " + placed + " roadside pieces (" + greybox + " greybox stand-ins), " + cards + " cards (trees "
                + plan.Count(i => i.Kind == RoadItemKind.Tree && !i.OnRing) + " terrain / " + plan.Count(i => i.Kind == RoadItemKind.Tree && i.OnRing) + " ring, hedgerow cards "
                + plan.Count(i => i.Kind == RoadItemKind.FieldHedge) + ")");
        }

        // Cameras for the road (absolute targets, eye height over the ground as the town's other cameras).
        public static readonly (string Name, Vector3 Pos, Vector3 Target, float Fov)[] Cameras =
        {
            ("Cap_HM_RoadOut", new Vector3(-22.6f, 1.65f, -72.4f), new Vector3(-98f, -13.5f, -94f), 56f),    // from just outside the Lower Gate, down the road into the valley
            ("Cap_HM_RoadBack", new Vector3(-97f, 1.7f, -87.6f), new Vector3(-32f, -3.5f, -60f), 55f),         // from the road below the first bend, back up at the gate, wall and keep
        };

        // ------------------------------------------------------------------------------------------------ helpers
        static Transform Group(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // The ground under a point: the terrain's own (eroded) height where there is terrain, else the ring's.
        static float GroundY(TownLayout t, float x, float z)
        {
            if (terrain != null && SaltwakeRoadPlan.OnTerrain(x, z, 0.5f)) return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            return RingHeight(x, z);
        }

        static GameObject Model(string piece)
        {
            if (Models.TryGetValue(piece, out GameObject m)) return m;
            string path = Folder + "/" + piece + "/" + piece + ".fbx";
            m = AssetImporter.GetAtPath(path) != null ? BeautyCornerArt.LoadModel(Folder + "/" + piece, piece + ".fbx", keepMaterialSlots: true) : null;
            Renderer probe = m != null ? m.GetComponentInChildren<Renderer>(true) : null;   // first import: the slots can still be the default "Lit"
            if (probe != null && probe.sharedMaterials.Length > 0 && probe.sharedMaterials[0] != null && probe.sharedMaterials[0].name == "Lit")
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Models[piece] = m;
            return m;
        }

        static Dictionary<string, Material> Slots()
        {
            if (slots != null) return slots;
            slots = HollowmereTownWall.BuildMaterials();
            Material leaf = HollowmereFoliage.FoliageMaterial();
            if (leaf != null) slots["B9_Foliage"] = leaf;
            return slots;
        }

        // ------------------------------------------------------------------------------------------------ 1. roadside pieces
        static void Piece(Transform parent, RoadItem i)
        {
            float y = GroundY(null, i.X, i.Z);
            float yaw = i.Yaw + (i.Kind == RoadItemKind.Cart ? CartYaw : 0f);
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-i.Tilt, 0f, 0f);   // a Ruined milestone leans back, away from the road
            GameObject prefab = Model(i.Piece);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                HollowmereTownWall.ApplyMaterials(go, Slots(), i.Piece);
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "FOOTPRINT") { Object.DestroyImmediate(r); }
            }
            else { go = Greybox(parent, i); greybox++; }
            go.name = i.Piece;
            go.transform.SetPositionAndRotation(new Vector3(i.X, y, i.Z), rot);
            go.transform.localScale = Vector3.one * i.Scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            if (i.Kind == RoadItemKind.Shrine || i.Kind == RoadItemKind.Cart || i.Kind == RoadItemKind.Milestone) Collider(go, i.Kind);
            placed++;
        }

        static void Collider(GameObject go, RoadItemKind k)
        {
            var b = go.AddComponent<BoxCollider>();
            switch (k)
            {
                case RoadItemKind.Shrine: b.center = new Vector3(0f, 1.3f, 0f); b.size = new Vector3(1.4f, 2.6f, 1.4f); break;
                case RoadItemKind.Cart: b.center = new Vector3(0f, 0.6f, 0f); b.size = new Vector3(3.4f, 1.2f, 1.8f); break;
                default: b.center = new Vector3(0f, 0.5f, 0f); b.size = new Vector3(0.5f, 1f, 0.4f); break;
            }
        }

        static GameObject Greybox(Transform parent, RoadItem i)
        {
            Vector3 size = i.Kind switch
            {
                RoadItemKind.Milestone => new Vector3(0.45f, 1.0f, 0.3f),
                RoadItemKind.Shrine => new Vector3(1.2f, 2.7f, 1.0f),
                RoadItemKind.Cart => new Vector3(3.4f, 1.0f, 1.8f),
                RoadItemKind.Fence => new Vector3(3.0f, 1.1f, 0.12f),
                _ => new Vector3(4.0f, 1.6f, 1.2f),
            };
            var go = new GameObject(i.Piece);
            go.transform.SetParent(parent, false);
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(go.transform, false);
            box.transform.localPosition = new Vector3(0f, size.y * 0.5f - 0.1f, 0f);
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = LookTestBuilder.Mat(i.Kind == RoadItemKind.Hedge ? "meadow_grass" : i.Kind == RoadItemKind.Fence || i.Kind == RoadItemKind.Cart ? "oak_timber" : "sandstone");
            return go;
        }

        // ------------------------------------------------------------------------------------------------ 2. cards: hedgerow trees and field hedges
        static Material CardMaterial()
        {
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TreesFolder + "/T_Road_FarTrees_BaseColor.png");
            if (albedo == null) return HollowmereFoliage.FoliageMaterial();   // before the atlas lands: the B9 leaves (UVs land on a leaf cell)
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_RD_FarTrees.mat", lit);
            m.SetTexture("_BaseMap", albedo); m.SetTexture("_MainTex", albedo);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TreesFolder + "/T_Road_FarTrees_Normal.png");
            if (normal != null) { NormalImport(normal); m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f); }
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", (float)CullMode.Off); m.doubleSidedGI = true;
            m.SetFloat("_Smoothness", 0.12f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void NormalImport(Texture2D tex)
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
        }

        sealed class CardMesh { public readonly List<Vector3> V = new List<Vector3>(); public readonly List<Vector3> N = new List<Vector3>(); public readonly List<Vector4> T = new List<Vector4>(); public readonly List<Vector2> UV = new List<Vector2>(); public readonly List<int> I = new List<int>(); }

        static void Cards(Transform country, List<RoadItem> plan, TownLayout t)
        {
            Material mat = CardMaterial();
            bool atlas = mat != null && mat.name == "M_RD_FarTrees";
            var tiles = new Dictionary<(int, int, bool), CardMesh>();
            foreach (RoadItem i in plan)
            {
                if (i.Kind != RoadItemKind.Tree && i.Kind != RoadItemKind.FieldHedge) continue;
                var key = (Mathf.FloorToInt(i.X / TileSize), Mathf.FloorToInt(i.Z / TileSize), i.OnRing);
                if (!tiles.TryGetValue(key, out CardMesh cm)) tiles[key] = cm = new CardMesh();
                float y = GroundY(t, i.X, i.Z);
                if (i.Kind == RoadItemKind.Tree) Tree(cm, new Vector3(i.X, y - 0.3f, i.Z), i.Yaw, TreeHeight * i.Scale, i.Variant, atlas);
                else Hedge(cm, new Vector3(i.X, y - 0.2f, i.Z), i.Yaw, i.Scale, i.Variant, atlas);
                cards++;
            }
            Transform root = Group(country, "Cards");
            foreach (var kv in tiles.OrderBy(k => k.Key.Item3).ThenBy(k => k.Key.Item1).ThenBy(k => k.Key.Item2))
            {
                CardMesh cm = kv.Value;
                string name = (kv.Key.Item3 ? "Ring_" : "Terrain_") + kv.Key.Item1 + "_" + kv.Key.Item2;
                var mesh = new Mesh { name = "RD_Cards_" + name, indexFormat = cm.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(cm.V); mesh.SetNormals(cm.N); mesh.SetTangents(cm.T); mesh.SetUVs(0, cm.UV); mesh.SetTriangles(cm.I, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = kv.Key.Item3 ? ShadowCastingMode.Off : ShadowCastingMode.On;   // the terrain's trees cast; the ring is far past the shadow distance
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        // UV rect of an atlas cell (4 columns): the whole tree, or a band of its crown for a hedge. Without the atlas: a B9 ivy leaf-mass cell.
        static Rect Cell(int variant, bool atlas, bool crownBand)
        {
            if (!atlas) return new Rect(0.02f, 0.27f, 0.21f, 0.21f);
            float u0 = (variant % 4) * 0.25f;
            return crownBand ? new Rect(u0 + 0.02f, 0.42f, 0.21f, 0.36f) : new Rect(u0, 0f, 0.25f, 1f);
        }

        // Three crossed vertical cards (0, 60, 120 deg). Normals bend out of a crown centre so the card shades like a rounded volume, not a flat board.
        static void Tree(CardMesh cm, Vector3 basePos, float yaw, float h, int variant, bool atlas)
        {
            Rect uv = Cell(variant, atlas, false);
            float w = atlas ? h * 0.5f : h * 0.7f;   // the atlas cell is 512 x 1024
            Vector3 crown = basePos + Vector3.up * h * 0.62f;
            for (int k = 0; k < 3; k++)
            {
                Quaternion q = Quaternion.Euler(0f, yaw + k * 60f, 0f);
                Quad(cm, basePos, q * Vector3.right, Vector3.up, w, h, uv, crown, 0.7f);
            }
        }

        static void Hedge(CardMesh cm, Vector3 basePos, float yaw, float s, int variant, bool atlas)
        {
            Rect uv = Cell(variant, atlas, true);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = q * Vector3.right, fwd = q * Vector3.forward;
            float len = 4.6f, h = 1.7f * s;
            Quad(cm, basePos, right, Vector3.up, len, h, uv, basePos + Vector3.up * h * 0.4f, 0.5f);
            Rect uv2 = Cell((variant + 1) % 4, atlas, true);
            Quad(cm, basePos + fwd * 0.45f + right * 0.8f, right, Vector3.up, len * 0.8f, h * 0.85f, uv2, basePos + Vector3.up * h * 0.4f, 0.5f);
        }

        // A vertical quad, bottom-centre at p, width w along `right`, height h; normals bend from the face normal toward (vertex - centre) by `bend`.
        static void Quad(CardMesh cm, Vector3 p, Vector3 right, Vector3 up, float w, float h, Rect uv, Vector3 centre, float bend)
        {
            int b = cm.V.Count;
            Vector3 face = Vector3.Cross(right, up).normalized;
            Vector3[] c = { p - right * (w * 0.5f), p + right * (w * 0.5f), p + right * (w * 0.5f) + up * h, p - right * (w * 0.5f) + up * h };
            Vector2[] t = { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
            for (int k = 0; k < 4; k++)
            {
                Vector3 radial = (c[k] - centre).normalized;
                Vector3 n = Vector3.Slerp(face, radial + Vector3.up * 0.35f, bend).normalized;
                cm.V.Add(c[k]); cm.N.Add(n); cm.UV.Add(t[k]); cm.T.Add(new Vector4(right.x, right.y, right.z, 1f));
            }
            cm.I.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }

        // ------------------------------------------------------------------------------------------------ 3. the backdrop ring
        const float RingCell = 20f, RingOverlap = 6f;

        // The ring's ground: NaturalGround (with the western fall) at the terrain's edge, a little under it so the terrain always wins the overlap, rising
        // into low far hills (fbm) further out.
        public static float RingHeight(float x, float z)
        {
            float dOut = OutsideTerrain(x, z);
            float near = HollowmereLayout.NaturalGround(x, z) - 0.45f;
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            float r = Mathf.Sqrt((x - c.X) * (x - c.X) + (z - c.Z) * (z - c.Z));
            float hills = -16f + 0.055f * Mathf.Max(0f, r - 200f)
                + 18f * (Mathf.PerlinNoise(x * 0.0035f + 17.3f, z * 0.0035f + 4.1f) - 0.5f)
                + 6f * (Mathf.PerlinNoise(x * 0.011f + 3.7f, z * 0.011f + 29.9f) - 0.5f);
            float k = Smooth(10f, 260f, dOut);
            return Mathf.Lerp(near, hills, k);
        }

        static float OutsideTerrain(float x, float z)
        {
            float dx = Mathf.Max(SaltwakeRoadPlan.TerrainMinX - x, 0f, x - SaltwakeRoadPlan.TerrainMaxX);
            float dz = Mathf.Max(SaltwakeRoadPlan.TerrainMinZ - z, 0f, z - SaltwakeRoadPlan.TerrainMaxZ);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float Smooth(float a, float b, float x) { float u = Mathf.Clamp01((x - a) / (b - a)); return u * u * (3f - 2f * u); }

        static void Ring(Transform country, TownLayout t)
        {
            Transform ring = Group(country, "Ring");
            float R = SaltwakeRoadPlan.RingRadius + 60f;
            TownPoint c = SaltwakeRoadPlan.RingCentre;
            int n = Mathf.CeilToInt(2f * R / RingCell);
            float x0 = c.X - R, z0 = c.Z - R;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var idx = new List<int>();
            var map = new int[(n + 1) * (n + 1)];
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float x = x0 + i * RingCell, z = z0 + j * RingCell;
                    map[j * (n + 1) + i] = verts.Count;
                    verts.Add(new Vector3(x, RingHeight(x, z), z));
                    uvs.Add(new Vector2((x - x0) / (2f * R), (z - z0) / (2f * R)));
                }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float xa = x0 + i * RingCell, za = z0 + j * RingCell, xb = xa + RingCell, zb = za + RingCell;
                    bool inside = xa >= SaltwakeRoadPlan.TerrainMinX + RingOverlap && xb <= SaltwakeRoadPlan.TerrainMaxX - RingOverlap
                        && za >= SaltwakeRoadPlan.TerrainMinZ + RingOverlap && zb <= SaltwakeRoadPlan.TerrainMaxZ - RingOverlap;
                    float cx = (xa + xb) * 0.5f - c.X, cz = (za + zb) * 0.5f - c.Z;
                    if (inside || cx * cx + cz * cz > R * R) continue;
                    int a = map[j * (n + 1) + i], b = map[j * (n + 1) + i + 1], d = map[(j + 1) * (n + 1) + i], e = map[(j + 1) * (n + 1) + i + 1];
                    idx.AddRange(new[] { a, d, e, a, e, b });
                }
            var mesh = new Mesh { name = "RD_BackdropRing", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("BackdropRing");
            go.transform.SetParent(ring, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = RingMaterial(t, x0, z0, 2f * R);
            r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            RoadRibbon(ring, t);
        }

        // The field patchwork as a texture over the ring's square (1 px = size/1024 m): the same cells as the terrain's splat, hedge lines dark along the
        // field edges, a soft per-field tone and grain. Written as a PNG asset (deterministic bytes, so rebuilds do not churn).
        static Material RingMaterial(TownLayout t, float x0, float z0, float size)
        {
            const int Res = 1024;
            var px = new Color32[Res * Res];
            TownPoint[] ringRoad = SaltwakeRoadPlan.RingRoad(t);
            for (int j = 0; j < Res; j++)
                for (int i = 0; i < Res; i++)
                {
                    float x = x0 + (i + 0.5f) * size / Res, z = z0 + (j + 0.5f) * size / Res;
                    FieldKind k = SaltwakeRoadPlan.Field(x, z, out float edge, out int cell);
                    float tone = SaltwakeRoadPlan.Hash01(cell, 91) - 0.5f;
                    Color col = k == FieldKind.Pasture ? new Color(0.36f, 0.44f, 0.19f) : k == FieldKind.Hay ? new Color(0.62f, 0.58f, 0.30f) : new Color(0.40f, 0.31f, 0.21f);
                    col *= 1f + 0.16f * tone;
                    float grain = Mathf.PerlinNoise(x * 0.09f, z * 0.09f) - 0.5f;
                    col *= 1f + 0.12f * grain;
                    float hedge = 1f - Smooth(0.8f, 2.6f, edge);
                    col = Color.Lerp(col, new Color(0.17f, 0.23f, 0.10f), hedge * 0.9f);
                    float dr = HollowmereLayout.DistanceToPolyline(new TownPoint(x, z), ringRoad);
                    col = Color.Lerp(col, new Color(0.52f, 0.45f, 0.34f), 1f - Smooth(2.0f, 3.4f, dr));
                    px[j * Res + i] = col;
                }
            var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
            tex.SetPixels32(px); tex.Apply();
            BuilderUtil.EnsureFolder(BackdropFolder);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string abs = BuilderUtil.Abs(FieldsTexPath);
            if (!File.Exists(abs) || !File.ReadAllBytes(abs).SequenceEqual(png)) { File.WriteAllBytes(abs, png); AssetDatabase.ImportAsset(FieldsTexPath, ImportAssetOptions.ForceUpdate); }
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(FieldsTexPath);

            BuilderUtil.EnsureFolder(MaterialsFolder);
            Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_RD_BackdropRing.mat", Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", albedo); m.SetTexture("_MainTex", albedo);
            m.SetFloat("_Smoothness", 0.05f); m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_SpecularHighlights", 0f); m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        // A 4.6 m dirt ribbon on the ring along SaltwakeRoadPlan.RingRoad, from the terrain's west edge on (the worn_path tileable, metre UVs).
        static void RoadRibbon(Transform ring, TownLayout t)
        {
            TownPoint[] pts = SaltwakeRoadPlan.RingRoad(t);
            var line = new List<Vector2>();
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = new Vector2(pts[i - 1].X, pts[i - 1].Z), b = new Vector2(pts[i].X, pts[i].Z);
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 4f));
                for (int s = (i == 1 ? 0 : 1); s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                    if (p.x > SaltwakeRoadPlan.TerrainMinX - 0.5f) continue;   // the terrain draws the road up to its edge
                    line.Add(p);
                }
            }
            if (line.Count < 2) return;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var idx = new List<int>();
            float along = 0f, half = 2.3f;
            for (int i = 0; i < line.Count; i++)
            {
                Vector2 d = (i < line.Count - 1 ? line[i + 1] - line[i] : line[i] - line[i - 1]).normalized, nrm = new Vector2(-d.y, d.x);
                if (i > 0) along += Vector2.Distance(line[i], line[i - 1]);
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 p = line[i] + nrm * (half * s);
                    v.Add(new Vector3(p.x, RingHeight(p.x, p.y) + 0.12f, p.y));
                    uv.Add(new Vector2(s * half / 2.5f, along / 2.5f));
                }
                if (i > 0) { int b = v.Count - 4; idx.AddRange(new[] { b, b + 2, b + 3, b, b + 3, b + 1 }); }
            }
            var mesh = new Mesh { name = "RD_RingRoad" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(idx, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject("RingRoad");
            go.transform.SetParent(ring, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = LookTestBuilder.Mat("worn_path") ?? LookTestBuilder.Mat("packed_soil");
            r.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }
}
