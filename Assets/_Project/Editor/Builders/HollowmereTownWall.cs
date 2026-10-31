// Hollowmere's town wall and its two gatehouses in art (Q8). Art/Blender exports the kit to Content/Art/Town/Wall/<Piece>/<Piece>.fbx (SOURCES.md there
// lists the material slots): modules Wall_Straight_A and _B (4 m, along local X), fittings Wall_Buttress (outside), Wall_End (a jamb at each opening),
// Wall_Stair (inside), variants Wall_Breach, Wall_Crumbled and Wall_Patched. Gatehouses LowerGate and EastGate: Content/Art/Town/Wall/<Gate>/<Gate>.fbx.
// Module frame (art contract): pivot at the module centre on the wall's centre line at local ground 0, run along Blender +X, the outside of the town at
// Blender -Y = Unity local +Z (exported -Z forward / Y up, like the B4 facade). The plan (which piece where, stretch, ends, buttresses, stairs) is
// Core/Town/TownWallPlan; this file only places it: each module at HollowmereLayout.Ground under its centre, yawed to the run, stretched along X.
// Gatehouse frame: pivot at the passage centre at ground, passage axis along Blender Y, outside at -Y (= local +Z). Centred between the two piers, yaw from
// the pier line (TownWallPlan.GatePlacement), at the piers' height. Ruined shows Shell; Rebuilt shows Frame + Part_Door. When the gatehouse FBX carries the
// piers (GatePiersIncluded), the pier greyboxes are not built. The child names are constants below: the exact layout is provisional until the art lands.
// Without the art every entry point returns false and HollowmereBuilder keeps today's greybox (box wall pieces and box piers).
// Materials: the B4 facade slots (FK_*, BeautyCornerArt.BuildFacadeMaterials) plus the TW_* table (PLACEHOLDER until SOURCES.md lists the real slots).
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
        public const float ModuleArtYaw = 0f;     // extra yaw on a module: 0 when the FBX's +Z is the outside; check on a test placement
        public const float GateArtYaw = 0f;
        public const bool EndClosesPlusX = true;  // Wall_End closes its local +X end (the wall body runs toward -X); mirrored (scale.x -1) at the other end
        public const bool GatePiersIncluded = true;
        public const string GateShell = "Shell", GateFrame = "Frame", GateDoor = "Part_Door", GateSocketDoor = "SOCKET_Door", GateFootprint = "FOOTPRINT";

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

        // PLACEHOLDER: the TW_* slots of the wall kit. The real names, sources and tints come from Art/Town/Wall/SOURCES.md once the art agent lists them.
        static readonly (string Slot, string Source, Color Tint)[] TwSlots =
        {
            ("TW_Stone", "sandstone", Color.white),
            ("TW_StoneDark", "sandstone", new Color(0.62f, 0.58f, 0.52f)),
            ("TW_Mortar", "sandstone", new Color(0.30f, 0.28f, 0.24f)),
            ("TW_Oak", "oak_timber", Color.white),
            ("TW_Iron", "iron", Color.white),
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
                m.SetColor("_BaseColor", new Color(Mathf.Clamp01(s.Tint.r), Mathf.Clamp01(s.Tint.g), Mathf.Clamp01(s.Tint.b), 1f).gamma);
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
            var tally = new List<string>();
            foreach (bool isRebuilt in new[] { false, true })
            {
                Transform parent = ChildGroup(isRebuilt ? rebuilt : ruined, "TownWall");
                List<WallSlot> plan = TownWallPlan.Plan(run, (x, z) => HollowmereLayout.Ground(t, x, z), isRebuilt);
                int placed = 0;
                foreach (WallSlot s in plan)
                {
                    GameObject prefab = Piece(s.Piece);
                    if (prefab == null) continue;
                    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    obj.name = (s.Piece == WallPiece.End ? "End" : "M" + s.Index.ToString("000")) + "_" + s.Piece;
                    float y = HollowmereLayout.Ground(t, s.Centre.X, s.Centre.Z);
                    obj.transform.position = new Vector3(s.Centre.X, y, s.Centre.Z);
                    obj.transform.rotation = Quaternion.Euler(0f, s.Yaw + ModuleArtYaw, 0f);
                    float sx = TownWallPlan.IsModule(s.Piece) || s.Piece == WallPiece.Buttress || s.Piece == WallPiece.Stair ? s.Stretch : 1f;
                    if (s.Piece == WallPiece.End)
                    {
                        TownPoint ex = new TownPoint(Mathf.Cos(s.Yaw * Mathf.Deg2Rad), -Mathf.Sin(s.Yaw * Mathf.Deg2Rad));   // module-local +X in the ground plane
                        float d = ex.X * s.Close.X + ex.Z * s.Close.Z;
                        sx = (d >= 0f) == EndClosesPlusX ? 1f : -1f;
                    }
                    obj.transform.localScale = new Vector3(sx, 1f, 1f);
                    ApplyMaterials(obj, slots, obj.name);
                    placed++;
                }
                tally.Add((isRebuilt ? "Rebuilt " : "Ruined ") + placed);
            }
            Debug.Log("SC HOLLOWMERE town wall art placed: " + string.Join(", ", tally));
            return true;
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
            obj.transform.localPosition = new Vector3(centre.X, (a.Y + b.Y) * 0.5f, centre.Z);
            obj.transform.localRotation = Quaternion.Euler(0f, yaw + GateArtYaw, 0f);
            PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform shell = obj.transform.Find(GateShell), frame = obj.transform.Find(GateFrame), door = obj.transform.Find(GateDoor);
            if (shell == null && frame == null) Debug.LogWarning("SC HOLLOWMERE gate " + def.Name + ": no " + GateShell + "/" + GateFrame + " child, showing the whole export");
            else
            {
                if (shell != null) shell.gameObject.SetActive(!rebuilt);
                if (frame != null) frame.gameObject.SetActive(rebuilt);
                if (door != null) door.gameObject.SetActive(rebuilt);
            }
            Transform fp = obj.transform.Find(GateFootprint);
            if (fp != null) { var r = fp.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false; }
            ApplyMaterials(obj, BuildMaterials(), def.Name);
            Debug.Log("SC HOLLOWMERE gate " + def.Name + " placed " + (rebuilt ? "Rebuilt" : "Ruined") + " centre=" + centre + " yaw=" + yaw.ToString("0.0"));
            return true;
        }
    }
}
