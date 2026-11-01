// The Q13 undercroft stair (Reference/hollowmere/undercroft_stair/CONTRACT.md): UndercroftStair.unity, the approved dungeon-corner cellar vault (built by
// DungeonCornerBuilder.BuildRoom at the origin) plus a winding stair from the heroes' home cellar door down to the vault's StairEntry. The stair is the
// CellarStair kit (Assets/_Project/Content/Art/Cellar/B5/CellarStair/CellarStair.fbx, root CellarStair, pieces StairDoor / StairFlight / StairTurn, each with its
// pivot at its own origin, cloned to every placement with the vault kit's CV_* slot materials), with a greybox stand-in per piece while the FBX (or that child)
// is missing. Placements are a chain: each piece's origin is the previous piece's exit, yaw is Unity degrees about +Y (world = (x cos + z sin, y, -x sin + z cos)).
// Light grows downward: house daylight spot, a candle on a corbel at the first turn, a B11 lantern on a hook at the second turn, a warm bounce in the entry
// tunnel, and the room's own lanterns. One reflection probe per zone, three capture cameras (UndercroftStairCapture renders them), a hidden 1.75 m figure.
// Idempotent: the scene is rebuilt through BuilderUtil.SaveScene.
using System;
using System.Collections.Generic;
using System.IO;
using SleepingCalamity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class UndercroftStairBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/UndercroftStair.unity";
        public const string KitFolder = "Assets/_Project/Content/Art/Cellar/B5/CellarStair";
        public const string KitFbx = "CellarStair.fbx";
        const float HouseSpot = 3.5f, HouseFill = 0.8f;   // house daylight tuning

        // piece, position, yaw (degrees about +Y). Door and flight A share an origin: the door's landing runs the other way (local -Z).
        static readonly (string Name, string Piece, Vector3 Pos, float Yaw)[] Placements =
        {
            ("StairDoor", "StairDoor", new Vector3(3.2f, 9.0f, -4.0f), 180f),
            ("StairFlightA", "StairFlight", new Vector3(3.2f, 9.0f, -4.0f), 180f),
            ("StairTurn1", "StairTurn", new Vector3(3.2f, 7.4f, -6.0f), 180f),
            ("StairFlightB", "StairFlight", new Vector3(2.6f, 6.6f, -6.6f), -90f),
            ("StairTurn2", "StairTurn", new Vector3(0.6f, 5.0f, -6.6f), -90f),
            ("StairFlightC", "StairFlight", new Vector3(0f, 4.2f, -6.0f), 0f),
        };

        [MenuItem("SleepingCalamity/Art/Build Undercroft Stair")]
        public static void Build()
        {
            Scene scene = DungeonCornerBuilder.NewRoomScene();
            var root = new GameObject("UndercroftStair").transform;
            DungeonCornerBuilder.RoomKit room = DungeonCornerBuilder.BuildRoom(root);
            // Stair-only round mote material (the room's M_DC_Mote has no texture and renders particles as squares); the dungeon captures stay as approved.
            var own = new Dictionary<string, Material>(room.Own) { ["mote"] = RoundMote() };

            GameObject kit = BeautyCornerArt.LoadModel(KitFolder, KitFbx, keepMaterialSlots: true);
            Debug.Log("SC STAIR kit " + (kit != null ? KitFolder + "/" + KitFbx : "FBX missing, greybox for every piece"));
            Dictionary<string, Material> slots = kit != null ? room.SlotMap() : null;

            Transform stair = new GameObject("Stair").transform; stair.SetParent(root, false);
            var report = new SortedDictionary<string, string>();
            foreach (var p in Placements)
            {
                var go = new GameObject(p.Name);
                go.transform.SetParent(stair, false); go.transform.localPosition = p.Pos; go.transform.localRotation = Quaternion.Euler(0, p.Yaw, 0);
                Transform src = kit != null ? kit.transform.Find(p.Piece) : null;
                if (src != null) { DungeonCornerBuilder.Clone(src, go.transform, slots, p.Piece); report[p.Piece] = "art"; }
                else { Greybox(p.Piece, go.transform); report[p.Piece] = "greybox"; }
            }
            foreach (var kv in report) Debug.Log("SC STAIR piece " + kv.Key + " " + kv.Value);

            // Vault-stone plug over the entry tunnel mouth (x +-0.6, y 4.7..5.2, z -4.05..-3.9).
            LookTestBuilder.Box(stair, "TunnelPlug", new Vector3(0f, 4.95f, -3.975f), new Vector3(1.2f, 0.5f, 0.15f), LookTestBuilder.Mat("vault_stone"));

            Transform lighting = new GameObject("StairLighting").transform; lighting.SetParent(root, false);
            // The heroes' home (cistern-house) around the top camera: a 4 x 4 m shell, door wall's house face at world z -2.4, lit by broad cool window light from the east wall.
            HouseShell(stair, room.SlotMap());
            // Broad soft daylight from a window out of frame (east wall): a wide spot with soft shadows plus a shadowless cool fill, so walls, floor, leaf and jambs all read.
            Vector3 winPos = new Vector3(4.9f, 11.0f, 0.5f), winAim = new Vector3(2.8f, 9.4f, -2.0f);
            Light day = LookTestBuilder.AddLight(lighting, "HouseDaylight", LightType.Spot, winPos, Quaternion.LookRotation(winAim - winPos, Vector3.up),
                new Color(0.86f, 0.89f, 0.96f), HouseSpot, 12f, LightShadows.Soft);
            day.spotAngle = 110f; day.innerSpotAngle = 70f;
            // Cool daylight spilling out of the door onto the landing and the first steps.
            LookTestBuilder.AddLight(lighting, "DoorSpill", LightType.Point, new Vector3(3.2f, 9.9f, -3.1f), Quaternion.identity, new Color(0.86f, 0.89f, 0.96f), 1.6f, 3.6f, LightShadows.None);
            LookTestBuilder.AddLight(lighting, "HouseFill", LightType.Point, new Vector3(3.2f, 11.2f, -0.6f), Quaternion.identity, new Color(0.86f, 0.89f, 0.96f), HouseFill, 8f, LightShadows.None);
            // Few, small motes well down the stair (none within 2 m of a capture camera).
            DungeonCornerBuilder.Motes(lighting, "Motes_Stair", new Vector3(3.2f, 8.2f, -5.2f), new Vector3(0.8f, 0.9f, 1.2f), 24, own);

            // Turn1: candle on a small corbel on the front wall (world z -7.2 face, 1.4 m above the turn's floor).
            Vector3 candle = new Vector3(3.2f, 8.8f, -7.05f);
            Material stone = LookTestBuilder.Mat("vault_stone");
            LookTestBuilder.Box(stair, "Corbel_Top", new Vector3(candle.x, candle.y - 0.05f, -7.08f), new Vector3(0.34f, 0.1f, 0.26f), stone);
            LookTestBuilder.Box(stair, "Corbel_Foot", new Vector3(candle.x, candle.y - 0.16f, -7.14f), new Vector3(0.2f, 0.12f, 0.14f), stone);
            DungeonCornerBuilder.CandleCluster(stair, lighting, own, candle, "Turn1", new Vector3(0, 0.25f, 0.15f));
            Light candleLight = lighting.Find("CandleLightTurn1").GetComponent<Light>();
            candleLight.intensity = 2.4f; candleLight.range = 3.5f; candleLight.color = new Color(1.0f, 0.76f, 0.52f);   // the room's paler gold

            // Turn2: a B11 lantern on a hook on the front wall (world x -0.6 face), gold, soft shadows, motes.
            DungeonCornerBuilder.Lanterns(stair, lighting, own, new[] { (new Vector3(-0.55f, 6.4f, -6.8f), new Vector3(-1, 0, 0)) }, "Turn2", 6f, 7f);

            // Warm bounce mid StairEntry.
            LookTestBuilder.AddLight(lighting, "EntryBounce", LightType.Point, new Vector3(0f, 2.5f, -2.5f), Quaternion.identity, new Color(0.96f, 0.88f, 0.76f), 2.0f, 4.5f, LightShadows.None);
            // Greyer ambient than the room's warm umber/plum (stair scene only): the tunnel and walls read stone, not maroon.
            RenderSettings.ambientSkyColor = new Color(0.19f, 0.18f, 0.17f); RenderSettings.ambientEquatorColor = new Color(0.18f, 0.17f, 0.16f);

            // Top zone only: a local volume that overrides just the vignette (the shared DungeonCorner_Profile stays as approved).
            TopVignetteVolume(lighting);

            // One probe per zone (the room keeps its own): house/top, turns, bottom.
            AddZoneProbe(lighting, "Probe_Top", new Vector3(3.2f, 9.6f, -3.0f), new Vector3(5f, 4.6f, 6f));
            AddZoneProbe(lighting, "Probe_Turns", new Vector3(1.9f, 6.4f, -6.6f), new Vector3(5.2f, 4.4f, 3.4f));
            AddZoneProbe(lighting, "Probe_Bottom", new Vector3(0f, 3.4f, -4.5f), new Vector3(3f, 3.4f, 5f));

            Transform cams = new GameObject("CaptureCameras").transform; cams.SetParent(root, false);
            LookTestBuilder.AddCapture(cams, "Cap_Stair_Top", new Vector3(3.0f, 10.5f, 0.4f), new Vector3(3.2f, 9.2f, -3.4f), 62f);   // inside the house: open plank leaf, arched doorway, landing and first flight
            LookTestBuilder.AddCapture(cams, "Cap_Stair_Turn", new Vector3(1.6f, 7.4f, -6.4f), new Vector3(-0.2f, 4.5f, -6.3f), 62f);
            LookTestBuilder.AddCapture(cams, "Cap_Stair_Bottom", new Vector3(0f, 2.5f, -3.0f), new Vector3(0.2f, 0.4f, 5.0f), 62f);
            DungeonCornerBuilder.DarkBackground(cams);

            // 1.75 m figure on flight A's 4th tread (local top y -0.8, z 0.875; yaw 180 puts it at world z -4.875).
            Transform dbg = new GameObject("Debug").transform; dbg.SetParent(root, false);
            LookTestBuilder.Prim(dbg, "Figure_1.75m", PrimitiveType.Capsule, new Vector3(3.2f, 8.2f + 0.875f, -4.875f), new Vector3(0.5f, 0.875f, 0.5f), LookTestBuilder.Mat("iron"));
            dbg.gameObject.SetActive(false);

            BuilderUtil.SaveScene(scene, ScenePath);
            Debug.Log("SC STAIR scene built " + ScenePath);
        }

        static void TopVignetteVolume(Transform parent)
        {
            const string path = "Assets/_Project/Content/Materials/UndercroftStair/US_TopVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            profile.components.RemoveAll(c => c == null);   // drop entries left by an unsaved component
            var vig = LookTestBuilder.Comp<UnityEngine.Rendering.Universal.Vignette>(profile);   // sub-asset of the profile, updated in place
            vig.intensity.Override(0.15f);
            EditorUtility.SetDirty(vig); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            var go = new GameObject("TopVolume"); go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(3.2f, 10.2f, -1.4f);
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(4.8f, 3.4f, 6.4f);
            var vol = go.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = false; vol.priority = 2f; vol.blendDistance = 1f; vol.sharedProfile = profile;
        }

        // House interior: limewashed stone walls, flagstone floor, two oak beams, a faint cool window quad on the east wall (the light itself comes from HouseDaylight).
        static void HouseShell(Transform parent, Dictionary<string, Material> slots)
        {
            Material M(string k, string fallback) => slots != null && slots.TryGetValue(k, out Material m) ? m : LookTestBuilder.Mat(fallback);
            Material lime = M("CV_Limewash", "limewash"), flag = M("CV_Flagstone", "flagstone"), oak = M("CV_Oak", "oak_timber");
            Transform h = new GameObject("House").transform; h.SetParent(parent, false);
            const float x0 = 1.2f, x1 = 5.2f, zN = -2.4f, zS = 1.6f, y0 = 9.0f, y1 = 11.8f, t = 0.2f;
            LookTestBuilder.Box(h, "Floor", new Vector3((x0 + x1) / 2, y0 - 0.115f, (zN + zS) / 2), new Vector3(x1 - x0 + 0.4f, 0.2f, zS - zN + 0.2f), flag);
            LookTestBuilder.Box(h, "Ceiling", new Vector3((x0 + x1) / 2, y1 + t / 2, (zN + zS) / 2), new Vector3(x1 - x0 + 0.4f, t, zS - zN + 0.4f), lime);
            LookTestBuilder.Box(h, "WallW", new Vector3(x0 - t / 2, (y0 + y1) / 2, (zN + zS) / 2), new Vector3(t, y1 - y0, zS - zN + 0.4f), lime);
            LookTestBuilder.Box(h, "WallE", new Vector3(x1 + t / 2, (y0 + y1) / 2, (zN + zS) / 2), new Vector3(t, y1 - y0, zS - zN + 0.4f), lime);
            LookTestBuilder.Box(h, "WallS", new Vector3((x0 + x1) / 2, (y0 + y1) / 2, zS + t / 2), new Vector3(x1 - x0 + 0.4f, y1 - y0, t), lime);
            // door wall shoulders beyond the piece's x 1.6..4.8 (the piece's wall is z -2.8..-2.4)
            LookTestBuilder.Box(h, "DoorWallShoulderW", new Vector3((x0 + 1.6f) / 2, (y0 + y1) / 2, -2.6f), new Vector3(1.6f - x0, y1 - y0, 0.4f), lime);
            LookTestBuilder.Box(h, "DoorWallShoulderE", new Vector3((4.8f + x1) / 2, (y0 + y1) / 2, -2.6f), new Vector3(x1 - 4.8f, y1 - y0, 0.4f), lime);
            float[] beams = { -1.0f, 0.6f };
            for (int i = 0; i < beams.Length; i++)
                LookTestBuilder.Box(h, "Beam_" + i, new Vector3((x0 + x1) / 2, y1 - 0.14f, beams[i]), new Vector3(x1 - x0, 0.28f, 0.22f), oak);
            Material win = LookTestBuilder.Ensure("Assets/_Project/Content/Materials/UndercroftStair/M_US_Window.mat", Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit not found"));
            win.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f)); win.EnableKeyword("_EMISSION"); win.SetColor("_EmissionColor", new Color(0.86f, 0.89f, 0.96f) * 1.2f);
            win.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; EditorUtility.SetDirty(win); AssetDatabase.SaveAssets();
            LookTestBuilder.Box(h, "WindowQuad", new Vector3(x1 - 0.02f, 10.5f, 0.5f), new Vector3(0.03f, 1.1f, 0.7f), win);
        }

        // Soft round additive mote: a generated 32 px radial-falloff texture saved next to the stair's own material.
        static Material RoundMote()
        {
            const string folder = "Assets/_Project/Content/Materials/UndercroftStair";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/Content/Materials", "UndercroftStair");
            string texPath = folder + "/T_US_MoteRound.png";
            if (!File.Exists(texPath))   // generated once; rewriting it every build would churn the import
            {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Mathf.Clamp01(Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f), a = (1f - d) * (1f - d) * (3f - 2f * (1f - d));
                    t.SetPixel(x, y, new Color(a, a, a, a));
                }
            File.WriteAllBytes(texPath, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.wrapMode = TextureWrapMode.Clamp; imp.SaveAndReimport();
            }
            Shader pu = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? throw new InvalidOperationException("URP Particles/Unlit not found");
            Material m = LookTestBuilder.Ensure(folder + "/M_US_Mote.mat", pu);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 1f); m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 1f);
            m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetColor("_BaseColor", new Color(1.0f, 0.80f, 0.54f));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            return m;
        }

        static void AddZoneProbe(Transform lighting, string name, Vector3 pos, Vector3 size)
        {
            LookTestBuilder.AddProbe(lighting, pos, size);
            // AddProbe names every probe "ReflectionProbe": rename the one just added (the last child) and give it the dungeon's low intensity.
            Transform t = lighting.GetChild(lighting.childCount - 1);
            t.name = name; t.GetComponent<ReflectionProbe>().intensity = 0.35f;
        }

        // --------------------------------------------------------------------------------------------- greybox pieces
        // Stand-ins at the contract extents, in each piece's local frame, from LookTest shapes and the B2 materials.
        static void Greybox(string piece, Transform t)
        {
            Material brick = LookTestBuilder.Mat("old_brick"), vault = LookTestBuilder.Mat("vault_stone"), flag = LookTestBuilder.Mat("flagstone"),
                oak = LookTestBuilder.Mat("oak_timber"), iron = LookTestBuilder.Mat("iron"), lime = LookTestBuilder.Mat("limewash");
            switch (piece)
            {
                case "StairFlight":
                {
                    // 8 steps, tread 0.25, rise 0.2, run 2.0, drop 1.6; nosing line y = -0.8 z
                    for (int k = 0; k < 8; k++)
                    {
                        float top = -0.2f * (k + 1), h = top + 1.8f;
                        LookTestBuilder.Box(t, "Step" + k, new Vector3(0, top - h * 0.5f, 0.25f * k + 0.125f), new Vector3(1.2f, h, 0.25f), flag);
                    }
                    float ang = Mathf.Atan(0.8f) * Mathf.Rad2Deg, len = Mathf.Sqrt(2f * 2f + 1.6f * 1.6f);
                    Vector3 mid = new Vector3(0, -0.8f, 1.0f);   // the nosing line at mid-run
                    LookTestBuilder.Box(t, "WallL", mid + new Vector3(-0.8f, 0, 0), new Vector3(0.4f, 3.6f, len), brick, new Vector3(ang, 0, 0));
                    LookTestBuilder.Box(t, "WallR", mid + new Vector3(0.8f, 0, 0), new Vector3(0.4f, 3.6f, len), brick, new Vector3(ang, 0, 0));
                    LookTestBuilder.Box(t, "Vault", mid + new Vector3(0, 2.35f, 0), new Vector3(2.0f, 0.3f, len), vault, new Vector3(ang, 0, 0));
                    LookTestBuilder.Box(t, "ArchBand", new Vector3(0, 1.1f, 0.1f), new Vector3(1.2f, 0.5f, 0.2f), brick);
                    break;
                }
                case "StairTurn":
                {
                    // right-hand winder quarter turn about C = (0.6, 0): 4 wedges, tops -0.2 .. -0.8, exit heading +X
                    Vector3 c = new Vector3(0.6f, 0f, 0f);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = (22.5f * k + 11.25f) * Mathf.Deg2Rad;
                        Vector3 dir = new Vector3(-Mathf.Cos(a), 0, Mathf.Sin(a));
                        float top = -0.2f * (k + 1), h = top + 1.8f;
                        LookTestBuilder.Box(t, "Winder" + k, c + dir * 0.6f + new Vector3(0, top - h * 0.5f, 0), new Vector3(0.46f, h, 1.2f), flag,
                            new Vector3(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0));
                    }
                    LookTestBuilder.Prim(t, "Newel", PrimitiveType.Cylinder, new Vector3(0.6f, 0.45f, 0f), new Vector3(0.24f, 2.25f, 0.24f), vault);
                    LookTestBuilder.Box(t, "WallOuter", new Vector3(-0.8f, 0.45f, 0.8f), new Vector3(0.4f, 4.5f, 1.6f), brick);
                    LookTestBuilder.Box(t, "WallFront", new Vector3(0f, 0.45f, 1.4f), new Vector3(2.0f, 4.5f, 0.4f), brick);
                    LookTestBuilder.Box(t, "CornerPier", new Vector3(0.8f, 0.45f, -0.2f), new Vector3(0.4f, 4.5f, 0.4f), brick);
                    LookTestBuilder.Box(t, "ExitTympanum", new Vector3(0.8f, 2.0f, 0.6f), new Vector3(0.4f, 1.4f, 1.2f), brick);   // above the next flight's arch (top y 1.3)
                    LookTestBuilder.Box(t, "SailVault", new Vector3(0f, 2.65f, 0.8f), new Vector3(2.0f, 0.1f, 1.6f), vault);
                    LookTestBuilder.Box(t, "ArchBand", new Vector3(0, 1.1f, 0.1f), new Vector3(1.2f, 0.5f, 0.2f), brick);
                    break;
                }
                case "StairDoor":
                {
                    // landing z -1.2..0, door wall z -1.6..-1.2, house floor z -3.6..-1.6, floor y 0
                    LookTestBuilder.Box(t, "Landing", new Vector3(0, -0.1f, -0.6f), new Vector3(1.2f, 0.2f, 1.2f), flag);
                    LookTestBuilder.Box(t, "LandingWallL", new Vector3(-0.8f, 0.75f, -0.6f), new Vector3(0.4f, 1.9f, 1.2f), brick);
                    LookTestBuilder.Box(t, "LandingWallR", new Vector3(0.8f, 0.75f, -0.6f), new Vector3(0.4f, 1.9f, 1.2f), brick);
                    LookTestBuilder.Shape(t, "LandingVault", LookTestGeometry.Shape.VaultInterior, new Vector3(0, 1.7f, -1.2f), new Vector3(0.6f, 0, 1.2f), vault, default, 2f, 24);
                    // door wall with an arched opening 1.0 wide (x +-0.5), springing 1.6, top 2.2
                    LookTestBuilder.Box(t, "DoorWallL", new Vector3(-1.05f, 1.5f, -1.4f), new Vector3(1.1f, 3.4f, 0.4f), lime);
                    LookTestBuilder.Box(t, "DoorWallR", new Vector3(1.05f, 1.5f, -1.4f), new Vector3(1.1f, 3.4f, 0.4f), lime);
                    LookTestBuilder.Box(t, "DoorLintel", new Vector3(0, 2.7f, -1.4f), new Vector3(1.0f, 1.0f, 0.4f), lime);
                    LookTestBuilder.Box(t, "JambL", new Vector3(-0.56f, 0.9f, -1.62f), new Vector3(0.12f, 1.8f, 0.06f), vault);
                    LookTestBuilder.Box(t, "JambR", new Vector3(0.56f, 0.9f, -1.62f), new Vector3(0.12f, 1.8f, 0.06f), vault);
                    LookTestBuilder.Box(t, "Threshold", new Vector3(0, 0.01f, -1.4f), new Vector3(1.0f, 0.04f, 0.4f), vault);
                    // door leaf hinged at x -0.5 on the house side, open 105 degrees toward -Z: leaf direction (cos 105, 0, -sin 105)
                    Vector3 hinge = new Vector3(-0.5f, 0f, -1.62f), leaf = new Vector3(Mathf.Cos(105f * Mathf.Deg2Rad), 0f, -Mathf.Sin(105f * Mathf.Deg2Rad));
                    LookTestBuilder.Box(t, "DoorLeaf", hinge + leaf * 0.5f + new Vector3(0, 1.05f, 0), new Vector3(1.0f, 2.1f, 0.06f), oak, new Vector3(0, 105f, 0));
                    LookTestBuilder.Box(t, "DoorStrap", hinge + leaf * 0.5f + new Vector3(0, 1.5f, 0), new Vector3(0.9f, 0.08f, 0.08f), iron, new Vector3(0, 105f, 0));
                    LookTestBuilder.Box(t, "HouseFloor", new Vector3(0, -0.1f, -2.6f), new Vector3(3.2f, 0.2f, 2.0f), flag);
                    break;
                }
                default: throw new InvalidOperationException("UndercroftStair: no greybox for piece '" + piece + "'");
            }
        }
    }
}
