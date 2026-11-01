// M6 S3: what turns the Hollowmere art scene into the session (hub) scene (docs/design/m6-square-loop-proposal.md, Q1 A). Called from HollowmereBuilder.BuildScene.
//   - A MainCamera with a Cinemachine Brain and a listener (the hero's rig drives it), like the arenas.
//   - One SpawnPoint in the square by the heroes' home door; the DiveHub at the cistern house's cellar stair (HollowmereLayout.UndercroftStair), its Landing a step in front of the stair
//     (go-home and fail land the party there). The dive floors are built far under the town at HubOrigin by every peer; no scene loads during play.
//   - The look separation: a second global post volume with the dungeon corner's profile (weight 0) and an AreaLook that swaps volumes, sky, fog, ambient and the sun by camera height.
//   - TownView: the bakery's frame and parts as greybox groups on lot F02, hidden until TownState says they stand (the lot's ruin hides while the frame is up).
// Everything is emitted here; never hand-edit the scene.
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SleepingCalamity.EditorTools
{
    public static partial class HollowmereBuilder
    {
        public static readonly Vector3 HubOrigin = new Vector3(0f, -200f, 0f);   // the dive floors are built here, under the whole town (the lowest town ground is the lake at -12)
        public const float CellarBelowY = -100f;                                  // a camera lower than this is in the cellar (AreaLook)
        public const string BakeryLotId = "F02";

        static void BuildHub(Transform root, Transform lighting, TownLayout t)
        {
            Transform hub = Child(root, "Hub");

            // Camera: the owner's rig drives it; far clip as the play cameras so the backdrop ridges draw.
            var cameraObject = new GameObject("ArenaCamera"); cameraObject.transform.SetParent(hub, false);
            cameraObject.tag = "MainCamera";
            Camera cam = cameraObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f; cam.farClipPlane = HollowmereRoad.FarClip; cam.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.requiresDepthTexture = true;
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.35f);
            cameraObject.transform.SetPositionAndRotation(new Vector3(12.5f, Ground(t, 12.5f, -6f) + 8f, -6f), Quaternion.Euler(30f, 0f, 0f));

            // Spawn: in the square, a few metres in front of the heroes' home door (the door node is at 12.5, 6.75; the door faces the square).
            Landmark home = t.Landmarks.First(l => l.Id == "HeroesHome_Cistern");
            PathNode door = t.Nodes.First(n => n.Id == home.DoorNode);
            var spawn = new GameObject("SpawnPoint"); spawn.transform.SetParent(hub, false);
            spawn.transform.position = new Vector3(door.Pos.X, Ground(t, door.Pos.X, door.Pos.Z - 3.0f) + 0.1f, door.Pos.Z - 3.0f);
            spawn.AddComponent<SpawnPoint>();

            // The stair: the marker the home art already carries (inside the house), the landing a step in front of it (stair top), the floors' origin far below.
            var entrance = new GameObject("DiveEntrance"); entrance.transform.SetParent(hub, false);
            entrance.transform.position = new Vector3(HollowmereLayout.UndercroftStair.X, home.Y, HollowmereLayout.UndercroftStair.Z);
            var landing = new GameObject("StairTop"); landing.transform.SetParent(hub, false);
            Vector3 landXZ = entrance.transform.position + new Vector3(-1.2f, 0f, -1.6f);
            landing.transform.position = new Vector3(landXZ.x, LandingY(t, landXZ, home.Y), landXZ.z);
            var origin = new GameObject("DiveOrigin"); origin.transform.SetParent(hub, false);
            origin.transform.position = HubOrigin;
            var diveHub = new GameObject("DiveHub"); diveHub.transform.SetParent(hub, false);
            diveHub.AddComponent<DiveHub>().Configure(entrance.transform, origin.transform, landing.transform);

            // Look separation: the cellar's own global volume (weight 0 until AreaLook is under the town) beside the town's.
            Volume town = Object.FindFirstObjectByType<Volume>();
            DungeonCornerBuilder.BuildProfile();
            var cellarGo = new GameObject("PostFX_Cellar"); cellarGo.transform.SetParent(lighting, false);
            Volume cellar = cellarGo.AddComponent<Volume>(); cellar.isGlobal = true; cellar.priority = 1f; cellar.weight = 0f;
            cellar.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(LookTestBuilder.RenderingFolder + "/" + DungeonCornerBuilder.ProfileName + ".asset");
            var look = new GameObject("AreaLook").AddComponent<AreaLook>(); look.transform.SetParent(hub, false);
            look.TownVolume = town; look.CellarVolume = cellar; look.CellarBelowY = CellarBelowY;
            look.TownLights = lighting.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional).ToArray();
            GameObject shafts = GameObject.Find(HollowmereLook.ShaftsRoot);
            look.TownOnly = shafts != null ? new[] { shafts } : new GameObject[0];

            BuildTownView(root, t);
        }

        // The stair top stands on whatever is really there (floor, step or terrain): a ground raycast from above, falling back to the layout ground, then the house floor.
        static float LandingY(TownLayout t, Vector3 xz, float homeY)
        {
            Physics.SyncTransforms();
            if (Physics.Raycast(new Vector3(xz.x, homeY + 6f, xz.z), Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y + 0.1f;
            float g = Ground(t, xz.x, xz.z);
            return (float.IsNaN(g) ? homeY : g) + 0.1f;
        }

        static float Ground(TownLayout t, float x, float z) => HollowmereLayout.Ground(t, x, z);

        // The bakery on lot F02, split into the frame and its three parts (TownBuildings.Bakery order), plus the baker at his door.
        static void BuildTownView(Transform root, TownLayout t)
        {
            Foundation f = t.Foundations.First(x => x.Id == BakeryLotId);
            Transform view = Child(root, "TownView");
            var lotRoot = new GameObject("Bakery_" + f.Id).transform; lotRoot.SetParent(view, false);
            lotRoot.localPosition = new Vector3(f.Centre.X, f.Y, f.Centre.Z);
            lotRoot.localRotation = Quaternion.Euler(0f, f.FacingYaw, 0f);
            Transform g = new GameObject("Greybox").transform; g.SetParent(lotRoot, false);
            LookTestBuilder.Box(g, "Footing", new Vector3(0f, 0.05f, 0f), new Vector3(f.Side, 0.4f, f.Side), LookTestBuilder.Mat("sandstone"), default, 2f);
            Building(g, f, HollowmereLayout.BuildingKind.Bakery);

            BuildingDef def = TownBuildings.Get(TownBuildings.Bakery);
            var parts = new GameObject[def.Parts.Length];
            for (int i = 0; i < parts.Length; i++) { parts[i] = new GameObject("Part_" + def.Parts[i]); parts[i].transform.SetParent(lotRoot, false); }
            foreach (Transform child in g.Cast<Transform>().ToArray())
            {
                string n = child.name;
                int part = n == "OvenBay" || n == "Chimney" ? def.PartIndex("oven")
                    : n == "Door" || n == "DoorMarker" || n.StartsWith("Shutter") ? def.PartIndex("door")
                    : n == "SignArm" || n == "Sign" ? def.PartIndex("sign") : def.PartIndex(BuildingDef.FramePart);
                child.SetParent(parts[part].transform, true);
            }
            Object.DestroyImmediate(g.gameObject);
            var baker = LookTestBuilder.Prim(lotRoot, "Baker", PrimitiveType.Capsule, new Vector3(0f, 0.9f, -f.Side * 0.5f + 0.2f), new Vector3(0.6f, 0.9f, 0.6f), M("label"));
            foreach (Collider c in lotRoot.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // a view, not furniture: no blocking
            foreach (GameObject p in parts) p.SetActive(false);
            baker.SetActive(false);
            lotRoot.gameObject.SetActive(false);

            Transform ruins = root.Find(RuinedRoot + "/Foundations");
            Transform ruin = ruins != null ? ruins.Cast<Transform>().FirstOrDefault(c => c.name.StartsWith(BakeryLotId + "_", System.StringComparison.Ordinal)) : null;
            var townView = view.gameObject.AddComponent<TownView>();
            townView.Lots = new[]
            {
                new TownView.Lot { FoundationId = f.Id, Ruin = ruin != null ? ruin.gameObject : null, Root = lotRoot, Parts = parts, Resident = baker, LotYaw = f.Yaw, LotFacing = f.Facing },
            };
        }
    }
}
