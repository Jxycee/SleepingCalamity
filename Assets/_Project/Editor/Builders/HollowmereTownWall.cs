// Hollowmere's town wall and its two gatehouses in art (Q8). Art/Blender exports the kit to Content/Art/Town/Wall/<Piece>/<Piece>.fbx (SOURCES.md there
// lists the material slots): modules Wall_Straight_A and _B (4 m, along local X), fittings Wall_Buttress (outside), Wall_End (a jamb at each opening),
// Wall_Stair (inside), variants Wall_Breach, Wall_Crumbled and Wall_Patched. Gatehouses LowerGate and EastGate: Content/Art/Town/Wall/<Gate>/<Gate>.fbx.
// Module frame (art contract): pivot at the module centre on the wall's centre line at local ground 0, run along Blender +X, the outside of the town at
// Blender -Y = Unity local +Z (exported -Z forward / Y up, like the B4 facade). The plan (which piece where, stretch, ends, buttresses, stairs) is
// Core/Town/TownWallPlan; this file only places it: each module at HollowmereLayout.Ground under its centre, yawed to the run, stretched along X.
// Gatehouse frame: pivot at the passage centre at ground, passage axis along Blender Y, outside at -Y (= local +Z). Centred between the two piers, yaw from
// the pier line (TownWallPlan.GatePlacement), at the piers' height. Ruined shows Shell; Rebuilt shows Frame (Part_Door stays hidden: ShowDoorLeaves). When the gatehouse FBX carries the
// piers (GatePiersIncluded), the pier greyboxes are not built. The child names are constants below: the exact layout is provisional until the art lands.
// Without the art every entry point returns false and HollowmereBuilder keeps today's greybox (box wall pieces and box piers).
// Materials: the B4 facade slots (FK_*, BeautyCornerArt.BuildFacadeMaterials) plus the TW_* table (SOURCES.md slots table: shared sandstone / oak tileables, tint, normal scale).
// Unknown slots log a warning and take a plain stone material; nothing throws.
// Authority: none (editor-time scene building).
using System.Collections.Generic;
using System.Linq;
using SleepingCalamity.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.EditorTools
{
    public static class HollowmereTownWall
    {
        public const string Folder = "Assets/_Project/Content/Art/Town/Wall";
        const string MaterialsFolder = "Assets/_Project/Content/Materials/Hollowmere";
        const string WearShaderName = "SleepingCalamity/WearLit";

        // --- art contract constants (change here when the export differs) ---
        public const float ModuleArtYaw = 0f;     // extra yaw on a module: the FBX's local +Z is the outside (checked in the Q8 captures)
        public const float GateArtYaw = 0f;
        public const bool EndClosesPlusX = true;  // Wall_End: pivot on the cut face at x 0, the body runs to -X; mirrored (scale.x -1) at the other side of an opening
        public const bool GatePiersIncluded = true;
        public const bool ShowDoorLeaves = false;   // Part_Door stays off: the gates are open passages (the leaves would close the arch with no collider behind them)
        public const string GateShell = "Shell", GateFrame = "Frame", GateDoor = "Part_Door", GateSocketDoor = "SOCKET_Door", GateFootprint = "FOOTPRINT";

        public const string RuinedSuffix = "_Ruined";   // Wall_Straight_A_Ruined / _B_Ruined: the Ruined preset's straight modules

        public static string FileFor(WallPiece p)
        {
            switch (p)
            {
                case WallPiece.StraightA: return "Wall_Straight_A";
                case WallPiece.StraightB: return "Wall_Straight_B";
                case WallPiece.Breach: return "Wall_Breach";
                case WallPiece.Crumbled: return "Wall_Crumbled";
                case WallPiece.Patched: return "Wall_Patched";
                case WallPiece.Buttress: return "Wall_Buttress";
                case WallPiece.End: return "Wall_End";
                default: return "Wall_Stair";
            }
        }

        public readonly struct GateDef
        {
            public readonly string Name, PierA, PierB;
            public GateDef(string name, string a, string b) { Name = name; PierA = a; PierB = b; }
        }
        public static readonly GateDef[] Gates = { new GateDef("LowerGate", "LowerGate_W", "LowerGate_E"), new GateDef("EastGate", "EastGate_N", "EastGate_S") };

        // The TW_* slots (Art/Town/Wall/SOURCES.md, slots table): shared B1 tileable, linear tint, normal scale. TW_Walkway's brightening tint is dropped (see BuildMaterials).
        static readonly (string Slot, string Source, Color Tint, float Bump)[] TwSlots =
        {
            ("TW_Rubble", "sandstone", new Color(0.64f, 0.61f, 0.57f), 1.4f),
            ("TW_RubbleDark", "sandstone", new Color(0.42f, 0.42f, 0.41f), 1.4f),    // polish 2: per-stone value / hue spread
            ("TW_RubbleWarm", "sandstone", new Color(0.76f, 0.66f, 0.54f), 1.4f),
            ("TW_RubblePale", "sandstone", new Color(0.84f, 0.80f, 0.73f), 1.3f),
            ("TW_RubbleOchre", "sandstone", new Color(1.00f, 0.89f, 0.73f), 1.4f),
            ("TW_Coping", "sandstone", new Color(1.14f, 1.09f, 1.00f), 0.9f),
            ("TW_Patch", "sandstone", new Color(1.30f, 1.16f, 0.94f), 1.0f),
            ("TW_Footing", "sandstone", new Color(0.46f, 0.43f, 0.39f), 1.2f),
            ("TW_Joint", "sandstone", new Color(0.66f, 0.61f, 0.54f), 1.0f),
            ("TW_Crack", "sandstone", new Color(0.10f, 0.09f, 0.08f), 0.6f),
            ("TW_Walkway", "oak_timber", new Color(2.70f, 2.50f, 2.30f), 1.2f),
        };

        static string PiecePath(string name) => Folder + "/" + name + "/" + name + ".fbx";
        static bool Exists(string name) => AssetDatabase.IsValidFolder(Folder) && AssetImporter.GetAtPath(PiecePath(name)) != null;

        static Dictionary<string, Material> BuildMaterials()
        {
            Dictionary<string, Material> map = BeautyCornerArt.BuildFacadeMaterials();
            BuilderUtil.EnsureFolder(MaterialsFolder);
            Shader wear = Shader.Find(WearShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in TwSlots)
            {
                Material src = LookTestBuilder.Mat(s.Source);
                if (src == null) { Debug.LogWarning("SC HOLLOWMERE wall: TW slot source '" + s.Source + "' missing for " + s.Slot); continue; }
                Material m = LookTestBuilder.Ensure(MaterialsFolder + "/M_" + s.Slot + ".mat", wear);
                m.CopyPropertiesFromMaterial(src);
                foreach (var k in src.enabledKeywords) m.EnableKeyword(k.name);
                // The tint multiplies in linear space, so SetColor gets the gamma value (unclamped: coping and patch stone are brighter than the tileable).
                // TW_Walkway's 2.7 x would be absurd as a plain colour, so it stays untinted (like FK_OakDoor).
                Color c = s.Tint.r > 1.5f ? Color.white : new Color(Mathf.LinearToGammaSpace(s.Tint.r), Mathf.LinearToGammaSpace(s.Tint.g), Mathf.LinearToGammaSpace(s.Tint.b), 1f);
                m.SetColor("_BaseColor", c);
                m.SetColor("_Color", m.GetColor("_BaseColor")); m.SetShaderPassEnabled("MotionVectors", true);
                m.SetFloat("_BumpScale", s.Bump);
                EditorUtility.SetDirty(m);
                map[s.Slot] = m;
            }
            AssetDatabase.SaveAssets();
            return map;
        }

        // Replaces every renderer's slot materials from the map; an unknown slot logs a warning and takes plain stone.
        static void ApplyMaterials(GameObject obj, Dictionary<string, Material> slots, string label)
        {
            foreach (Renderer r in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name == GateFootprint) continue;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int k = 0; k < mats.Length; k++)
                {
                    string slot = k < r.sharedMaterials.Length && r.sharedMaterials[k] != null ? r.sharedMaterials[k].name : "";
                    int dot = slot.IndexOf('.'); if (dot > 0) slot = slot.Substring(0, dot);
                    if (!slots.TryGetValue(slot, out mats[k]) || mats[k] == null)
                    {
                        Debug.LogWarning("SC HOLLOWMERE wall " + label + ": no material for slot '" + slot + "' on " + r.name);
                        mats[k] = LookTestBuilder.Mat("sandstone");
                    }
                    if (slot.StartsWith("FK_Decal")) r.shadowCastingMode = ShadowCastingMode.Off;
                }
                r.sharedMaterials = mats;
            }
        }

        static GameObject Load(string name)
        {
            if (!Exists(name)) return null;
            GameObject fbx = BeautyCornerArt.LoadModel(Folder + "/" + name, name + ".fbx", keepMaterialSlots: true);
            if (fbx == null) return null;
            Renderer probe = fbx.GetComponentInChildren<Renderer>(true);   // first import: slots can still be the default "Lit" (see HollowmereTownhouse)
            if (probe != null && probe.sharedMaterials.Length > 0 && probe.sharedMaterials[0] != null && probe.sharedMaterials[0].name == "Lit")
            {
                AssetDatabase.ImportAsset(PiecePath(name), ImportAssetOptions.ForceUpdate);
                fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PiecePath(name));
            }
            return fbx;
        }

        // ------------------------------------------------------------------------------------------------ the wall
        public static bool ArtPresent => Exists(FileFor(WallPiece.StraightA)) || Exists(FileFor(WallPiece.StraightB));

        // Builds the TownWall group in both presets from the run of wall pieces. False (nothing built) when no straight module is exported yet.
        public static bool TryBuild(Transform ruined, Transform rebuilt, TownLayout t, List<(TownPoint A, TownPoint B)> run)
        {
            if (!ArtPresent) return false;
            Dictionary<string, Material> slots = BuildMaterials();
            var cache = new Dictionary<WallPiece, GameObject>();
            var ruinedCache = new Dictionary<WallPiece, GameObject>();
            // Ruined preset: the straight modules come as their _Ruined variants (more chipped and missing merlons) when exported.
            GameObject PieceFor(WallPiece p, bool rebuilt)
            {
                if (rebuilt || (p != WallPiece.StraightA && p != WallPiece.StraightB)) return Piece(p);
                if (!ruinedCache.TryGetValue(p, out GameObject g)) ruinedCache[p] = g = Load(FileFor(p) + RuinedSuffix);
                return g != null ? g : Piece(p);
            }
            GameObject Piece(WallPiece p)
            {
                if (cache.TryGetValue(p, out GameObject g)) return g;
                g = Load(FileFor(p));
                if (g == null && (p == WallPiece.StraightA || p == WallPiece.StraightB)) g = Load(FileFor(p == WallPiece.StraightA ? WallPiece.StraightB : WallPiece.StraightA));
                if (g == null && (p == WallPiece.Breach || p == WallPiece.Crumbled || p == WallPiece.Patched)) g = Piece(WallPiece.StraightA);   // variant not exported yet: plain wall
                if (g == null) Debug.LogWarning("SC HOLLOWMERE wall: " + FileFor(p) + " not exported, skipped");
                cache[p] = g;
                return g;
            }
            // Runs meet a gatehouse's side face only where its art is in (else the greybox piers stand and the run ends on a Wall_End).
            List<TownWallPlan.WallGate> gates = TownWallPlan.Gates(t).FindAll(g => Exists(g.Name));
            var tally = new List<string>();
            foreach (bool isRebuilt in new[] { false, true })
            {
                Transform parent = ChildGroup(isRebuilt ? rebuilt : ruined, "TownWall");
                List<WallSlot> plan = TownWallPlan.Plan(run, (x, z) => HollowmereLayout.Ground(t, x, z), isRebuilt, gates);
                int placed = 0;
                foreach (WallSlot s in plan)
                {
                    GameObject prefab = PieceFor(s.Piece, isRebuilt);
                    if (prefab == null) continue;
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    obj.name = (s.Piece == WallPiece.End ? "End" : "M" + s.Index.ToString("000")) + "_" + s.Piece;
                    // Pivot Y: the highest ground under the piece's centre line minus 0.1 m; the 1.5 m footing covers the fall on the low side.
                    float y = float.MinValue;
                    TownPoint along = s.Piece == WallPiece.End ? s.Close * -1f : s.Chord;   // an End's body runs away from the opening, 2 m
                    float reach = s.Piece == WallPiece.End ? TownWallPlan.EndLength : s.Reach;
                    for (int k = 0; k <= 4; k++)
                    {
                        float f = s.Piece == WallPiece.End ? reach * k / 4f : reach * (-1f + k / 2f);
                        y = Mathf.Max(y, HollowmereLayout.Ground(t, s.Centre.X + along.X * f, s.Centre.Z + along.Z * f));
                    }
                    obj.transform.position = new Vector3(s.Centre.X, y - 0.1f, s.Centre.Z);
                    obj.transform.rotation = Quaternion.Euler(0f, s.Yaw + ModuleArtYaw, 0f);
                    float sx = s.Piece == WallPiece.Buttress ? 1f : s.Piece == WallPiece.End ? 1f : s.Stretch;
                    if (s.Piece == WallPiece.End)
                    {
                        TownPoint ex = new TownPoint(Mathf.Cos(s.Yaw * Mathf.Deg2Rad), -Mathf.Sin(s.Yaw * Mathf.Deg2Rad));   // module-local +X in the ground plane
                        float d = ex.X * s.Close.X + ex.Z * s.Close.Z;
                        sx = (d >= 0f) == EndClosesPlusX ? 1f : -1f;   // the body must run away from the opening
                    }
                    else if (s.Piece == WallPiece.Stair && s.Mirror) sx = -sx;
                    obj.transform.localScale = new Vector3(sx, 1f, 1f);
                    AddColliders(obj, s.Piece);
                    ApplyMaterials(obj, slots, obj.name);
                    placed++;
                }
                tally.Add((isRebuilt ? "Rebuilt " : "Ruined ") + placed + " [" + string.Join(" ", plan.GroupBy(x => x.Piece).OrderBy(g => g.Key).Select(g => g.Key + "=" + g.Count())) + "]");
            }
            Debug.Log("SC HOLLOWMERE town wall art placed: " + string.Join(", ", tally));
            return true;
        }

        // Module frame in Unity (Blender x -> x, z -> y, outside -Y -> +Z): boxes from SOURCES.md. Wall_Breach has none (a gap with rubble the player can climb);
        // the stair uses its own mesh (a box would wall off the steps).
        static void AddColliders(GameObject obj, WallPiece piece)
        {
            switch (piece)
            {
                case WallPiece.StraightA: case WallPiece.StraightB: case WallPiece.Patched: Box(obj, new Vector3(0f, 0.95f, 0f), new Vector3(4f, 4.9f, 1.3f)); break;
                case WallPiece.Crumbled: Box(obj, new Vector3(0f, 0.6f, 0f), new Vector3(4f, 4.2f, 1.4f)); break;
                case WallPiece.Buttress: Box(obj, new Vector3(0f, 0.655f, 1.13f), new Vector3(1.3f, 4.37f, 1.6f)); break;
                case WallPiece.End: Box(obj, new Vector3(-1.07f, 0.95f, 0f), new Vector3(2.27f, 4.9f, 1.7f)); break;
                case WallPiece.Stair:
                    foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                    break;
            }
        }

        static void Box(GameObject go, Vector3 centre, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>(); b.center = centre; b.size = size;
        }

        static Transform ChildGroup(Transform parent, string name)
        {
            Transform c = parent.Find(name);
            if (c != null) return c;
            c = new GameObject(name).transform; c.SetParent(parent, false);
            return c;
        }

        // ------------------------------------------------------------------------------------------------ gatehouses
        public static GateDef? GateOf(string pierId)
        {
            foreach (GateDef g in Gates) if (g.PierA == pierId || g.PierB == pierId) return g;
            return null;
        }

        // Called for each TownGate pier landmark. Places the gatehouse once (at the gate's first pier) under `landmarks` and returns true when the pier greybox
        // should be skipped (the gatehouse art is in and carries the piers). False: build the greybox pier as before.
        public static bool PierHandled(Transform landmarks, TownLayout t, Landmark pier, bool rebuilt)
        {
            GateDef? def = GateOf(pier.Id);
            if (def == null || !Exists(def.Value.Name)) return false;
            if (pier.Id == def.Value.PierA && !PlaceGate(landmarks, t, def.Value, rebuilt)) return false;
            return GatePiersIncluded && landmarks.Find(def.Value.Name) != null;
        }

        static bool PlaceGate(Transform landmarks, TownLayout t, GateDef def, bool rebuilt)
        {
            Landmark a = t.Landmarks.Find(l => l.Id == def.PierA), b = t.Landmarks.Find(l => l.Id == def.PierB);
            if (a == null || b == null) { Debug.LogWarning("SC HOLLOWMERE gate " + def.Name + ": pier landmark missing"); return false; }
            GameObject fbx = Load(def.Name);
            if (fbx == null) return false;
            TownWallPlan.GatePlacement(a.Footprint.Centre, b.Footprint.Centre, out TownPoint centre, out float yaw, out _);
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(fbx, landmarks);
            obj.name = def.Name;
            float gy = HollowmereLayout.Ground(t, centre.X, centre.Z);   // the ground (lane surface) through the passage
            obj.transform.localPosition = new Vector3(centre.X, gy, centre.Z);
            obj.transform.localRotation = Quaternion.Euler(0f, yaw + GateArtYaw, 0f);
            PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform shell = obj.transform.Find(GateShell), frame = obj.transform.Find(GateFrame), door = obj.transform.Find(GateDoor);
            if (shell == null && frame == null) Debug.LogWarning("SC HOLLOWMERE gate " + def.Name + ": no " + GateShell + "/" + GateFrame + " child, showing the whole export");
            else
            {
                if (shell != null) shell.gameObject.SetActive(!rebuilt);
                if (frame != null) frame.gameObject.SetActive(rebuilt);
                if (door != null) door.gameObject.SetActive(rebuilt && ShowDoorLeaves);
            }
            Transform fp = obj.transform.Find(GateFootprint);
            if (fp != null) { var r = fp.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false; }   // the footprint is never a collider: it would close the passage
            // Colliders: one box per pier, the passage between them stays open (SOURCES: clear half widths and block depths).
            var (halfPassage, depth) = def.Name == "LowerGate" ? (2.1f, 4.4f) : (2.02f, 4.6f);
            float halfWidth = TownWallPlan.GateDefs.First(d => d.Name == def.Name).HalfWidth, mid = (halfPassage + halfWidth) * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                var pierBox = new GameObject("Collider_Pier" + (side < 0f ? "W" : "E")); pierBox.transform.SetParent(obj.transform, false);
                Box(pierBox, new Vector3(side * mid, 2.95f, 0f), new Vector3(halfWidth - halfPassage, 5.9f, depth));
            }
            ApplyMaterials(obj, BuildMaterials(), def.Name);
            Debug.Log("SC HOLLOWMERE gate " + def.Name + " placed " + (rebuilt ? "Rebuilt" : "Ruined") + " centre=" + centre + " yaw=" + yaw.ToString("0.0") + " groundY=" + gy.ToString("0.00") + " pierY=" + ((a.Y + b.Y) * 0.5f).ToString("0.00"));
            return true;
        }
    }
}
