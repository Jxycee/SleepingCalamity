// M5 dive spine: Arena_Dive.unity, a gameplay scene (the Arena_ prefix makes the host spawn the players here) that stands in for the Undercroft stair until the town is a session scene.
// A 30 m stone pad with the spawn point at its centre, a greybox stair mouth on the north side (a dark gap, a lintel and a violet light column at the DiveHub entrance), the camera with its
// Cinemachine Brain (the owner's rig drives it), a sun, and the DiveHub (spawns the host's DiveDirector). The dive floors are NOT in the scene: every peer builds them from (seed, floor) far
// away at the hub's Origin (200 m east), so the hub and the dive never overlap. Idempotent through BuilderUtil.SaveScene. Called from SceneBuilder.BuildAll.
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class DiveArenaBuilder
    {
        public static readonly Vector3 EntrancePosition = new Vector3(0f, 0f, 9f), OriginPosition = new Vector3(200f, 0f, 0f);

        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material ground = MaterialLibrary.Get("M_GreyboxGround");
            Material wall = MaterialLibrary.Get("M_GreyboxWall");
            Material step = MaterialLibrary.Get("M_GreyboxStep");

            var cameraObject = new GameObject("ArenaCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 14f, -16f), Quaternion.Euler(40f, 0f, 0f));
            CinemachineBrain brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.35f);

            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject slab = BuilderUtil.EnsurePrimitive(new GameObject("Ground").transform, "Slab", PrimitiveType.Cube,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(30f, 1f, 30f), ground, keepCollider: true);
            BuilderUtil.SetLayerRecursive(slab.transform.parent.gameObject, Layers.Ground);

            var spawn = new GameObject("SpawnPoint");
            spawn.AddComponent<SpawnPoint>();
            spawn.transform.position = new Vector3(0f, 0f, -6f);

            // The stair mouth: two jambs and a lintel around a dark gap (walkable pad stays flat; the stair itself is the DiveHub entrance marker).
            Transform mouth = new GameObject("StairMouth").transform;
            Block(mouth, "Jamb_W", new Vector3(-2.2f, 1.6f, 12f), new Vector3(1.2f, 3.2f, 1.2f), wall);
            Block(mouth, "Jamb_E", new Vector3(2.2f, 1.6f, 12f), new Vector3(1.2f, 3.2f, 1.2f), wall);
            Block(mouth, "Lintel", new Vector3(0f, 3.4f, 12f), new Vector3(5.6f, 0.6f, 1.2f), wall);
            Block(mouth, "Wall_N", new Vector3(0f, 1.5f, 14.75f), new Vector3(30f, 3f, 0.5f), wall);
            for (int i = 0; i < 3; i++) Block(mouth, "Step_" + (i + 1), new Vector3(0f, 0.12f * (i + 1), 10.2f + 0.8f * i), new Vector3(3.2f, 0.24f * (i + 1), 0.8f), step);

            var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.name = "EntranceColumn"; column.transform.position = EntrancePosition + new Vector3(0f, 1.6f, 0f); column.transform.localScale = new Vector3(0.25f, 1.6f, 0.25f);
            Object.DestroyImmediate(column.GetComponent<Collider>());
            column.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Get("M_GreyboxElite");

            var entrance = new GameObject("DiveEntrance"); entrance.transform.position = EntrancePosition;
            var origin = new GameObject("DiveOrigin"); origin.transform.position = OriginPosition;
            var hub = new GameObject("DiveHub");
            hub.AddComponent<DiveHub>().Configure(entrance.transform, origin.transform);

            BuilderUtil.SaveScene(scene, AssetPaths.DiveArenaScene);
        }

        static void Block(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject block = BuilderUtil.EnsurePrimitive(parent, name, PrimitiveType.Cube, center, Quaternion.identity, size, material, keepCollider: true);
            block.layer = Layers.Structure;
        }
    }
}
