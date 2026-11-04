// Editor/Builders/MetricsGymBuilder.cs: MetricsGym.unity, the scene of the game's real measurements built from MetricsGymLayout (metrics gym plan, Task 7; room-authoring spec).
// One root per lane, each piece a primitive named "<name> | <label>", a MetricsGymRecord holding GameMetrics.Rows(), the pilot room in both water states. Not in Build Settings. Deterministic.
using System.Linq;
using SleepingCalamity.Core;
using SleepingCalamity.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepingCalamity.EditorTools
{
    public static class MetricsGymBuilder
    {
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("GymCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 12f, -14f), Quaternion.Euler(35f, 0f, 0f));
            var sun = new GameObject("Sun");
            sun.AddComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var rows = GameMetrics.Rows();
            var recordObject = new GameObject("MetricsGymRecord");
            var record = recordObject.AddComponent<MetricsGymRecord>();
            record.Keys = rows.Select(r => r.Key).ToArray();
            record.Values = rows.Select(r => r.Value).ToArray();
            record.Units = rows.Select(r => r.Unit).ToArray();

            var pieces = MetricsGymLayout.Build();
            foreach (string lane in MetricsGymLayout.Lanes)
            {
                var root = new GameObject("Lane_" + lane).transform;
                foreach (GymPiece p in pieces.Where(q => q.Lane == lane)) BuildPiece(root, p);
            }
            BuilderUtil.SaveScene(scene, AssetPaths.MetricsGymScene);
        }

        static void BuildPiece(Transform lane, GymPiece p)
        {
            string name = p.Label.Length > 0 ? p.Name + " | " + p.Label : p.Name;
            var pos = new Vector3(p.X, p.Y, p.Z);
            var size = new Vector3(p.SX, p.SY, p.SZ);
            switch (p.Shape)
            {
                case GymShape.PilotRoom:
                    {
                        // The piece centre is the room footprint centre; the room's own origin is its south-west corner on the floor.
                        RoomDataV2 room = PilotRoomsV2.HangingHall;
                        var state = new int[room.Regions.Count];
                        for (int i = 0; i < state.Length; i++) state[i] = System.Math.Min(p.PilotState, room.Regions[i].StateCount - 1);
                        GameObject g = RoomGreybox.Build(room, state, new Vector3(p.X - p.SX / 2f, 0f, p.Z - p.SZ / 2f), lane);
                        g.name = name;
                        break;
                    }
                case GymShape.Capsule:
                    {
                        Material m = MaterialLibrary.Get(p.Name.StartsWith("enemy") ? "M_GreyboxElite" : "M_GreyboxPlayer");
                        BuilderUtil.EnsurePrimitive(lane, name, PrimitiveType.Capsule, pos, Quaternion.identity, new Vector3(size.x, size.y / 2f, size.z), m, keepCollider: false);
                        break;
                    }
                case GymShape.Marker:
                    BuilderUtil.EnsurePrimitive(lane, name, PrimitiveType.Cube, pos, Quaternion.identity, size, MaterialLibrary.Get("M_GreyboxElite"), keepCollider: false);
                    break;
                default:
                    {
                        string mat = p.Name.StartsWith("floor") ? "M_GreyboxGround" : p.Name.StartsWith("pool") ? "M_GreyboxDummy"
                            : p.Name.StartsWith("ledge") || p.Name.StartsWith("step") || p.Name == "deck" ? "M_GreyboxStep" : "M_GreyboxWall";
                        GameObject g = BuilderUtil.EnsurePrimitive(lane, name, PrimitiveType.Cube, pos, Quaternion.identity, size, MaterialLibrary.Get(mat), keepCollider: true);
                        g.layer = Layers.Structure;
                        break;
                    }
            }
        }
    }
}
