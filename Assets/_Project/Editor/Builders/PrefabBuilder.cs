// Prefabs built from code (an existing prefab is edited in place by BuilderUtil.WritePrefab, so its GUID and NetworkObject hash survive).
//   PlayerCapsule  the mannequin hero (the file keeps its M1 name). Authority: the owning client moves it (owner-authoritative NetworkTransform);
//                  the host spawns it (ArenaPlayerSpawner) and owns nothing else about it. Owner-only parts ship disabled; HeroNetState enables them.
//   TargetDummy    a host-spawned target for the lock-on (M2). NetworkObject, no movement, no life yet.
//   HeroCameraRig  the owner's Cinemachine 3 rig (not networked): FreeCam, LockCam, a target group and the debug lock indicator.
using SleepingCalamity.Core;
using SleepingCalamity.Gameplay;
using SleepingCalamity.Presentation;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SleepingCalamity.EditorTools
{
    public static class PrefabBuilder
    {
        public static void Build()
        {
            MaterialLibrary.BuildAll();
            BuildCameraRig();
            BuildDummy();
            BuildPlayer();
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.PlayerPrefab);
            BuilderUtil.RefreshNetworkObjectHash(AssetPaths.DummyPrefab);
        }

        static void BuildPlayer()
        {
            Material material = MaterialLibrary.Get("M_GreyboxPlayer");
            Material accent = MaterialLibrary.Get("M_GreyboxElite");
            var rigPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroCameraRig>(AssetPaths.CameraRigPrefab);
            if (rigPrefab == null) throw new System.IO.FileNotFoundException("Missing prefab " + AssetPaths.CameraRigPrefab);

            BuilderUtil.WritePrefab(AssetPaths.PlayerPrefab, root =>
            {
                BuilderUtil.GetOrAdd<NetworkObject>(root);
                NetworkTransform transform = BuilderUtil.GetOrAdd<NetworkTransform>(root);
                transform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // the owner writes its own pose
                transform.SyncRotAngleX = false;                                    // the hero only turns about Y
                transform.SyncRotAngleZ = false;
                transform.SyncScaleX = transform.SyncScaleY = transform.SyncScaleZ = false;

                // Owner-only physics: the CharacterController. Remote copies enable the CapsuleCollider instead. Both ship disabled.
                CharacterController cc = BuilderUtil.GetOrAdd<CharacterController>(root);
                cc.height = MovementMath.CapsuleHeight;
                cc.radius = MovementMath.CapsuleRadius;
                cc.center = new Vector3(0f, MovementMath.CapsuleHeight * 0.5f, 0f);
                cc.stepOffset = MovementMath.StepOffset;
                cc.slopeLimit = MovementMath.SlopeLimit;
                cc.skinWidth = MovementMath.SkinWidth;
                cc.enabled = false;
                CapsuleCollider remote = BuilderUtil.GetOrAdd<CapsuleCollider>(root);
                remote.height = MovementMath.CapsuleHeight;
                remote.radius = MovementMath.CapsuleRadius;
                remote.center = new Vector3(0f, MovementMath.CapsuleHeight * 0.5f, 0f);
                remote.enabled = false;

                BuilderUtil.GetOrAdd<HeroNetState>(root);
                BuilderUtil.GetOrAdd<HeroLocomotion>(root).enabled = false;
                BuilderUtil.GetOrAdd<TargetLockController>(root).enabled = false;
                BuilderUtil.GetOrAdd<HeroAnimationFeeder>(root);
                BuilderUtil.GetOrAdd<HeroCameraRigSpawner>(root).Configure(rigPrefab);

                // M1's capsule sat directly under the root with a collider of its own; the visuals now live under "Visual" with no colliders.
                Transform oldBody = root.transform.Find("Body");
                if (oldBody != null) Object.DestroyImmediate(oldBody.gameObject);

                Transform visual = BuilderUtil.EnsurePath(root.transform, "Visual");
                BuilderUtil.GetOrAdd<MannequinAnimDriver>(visual.gameObject);
                // A 1.8 m mannequin on the root's origin: a capsule body, a round head and a nose block that shows which way it faces.
                BuilderUtil.EnsurePrimitive(visual, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), Quaternion.identity,
                    new Vector3(0.7f, 0.82f, 0.7f), material, keepCollider: false);
                BuilderUtil.EnsurePrimitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.62f, 0f), Quaternion.identity,
                    new Vector3(0.36f, 0.36f, 0.36f), material, keepCollider: false);
                BuilderUtil.EnsurePrimitive(visual, "Nose", PrimitiveType.Cube, new Vector3(0f, 1.62f, 0.22f), Quaternion.identity,
                    new Vector3(0.1f, 0.08f, 0.2f), accent, keepCollider: false);

                Transform pivot = BuilderUtil.EnsurePath(root.transform, "CameraPivot");
                pivot.localPosition = new Vector3(0f, MovementMath.CameraPivotHeight, 0f);

                BuilderUtil.SetLayerRecursive(root, Layers.Player);
            });
        }

        static void BuildDummy()
        {
            Material body = MaterialLibrary.Get("M_GreyboxDummy");
            BuilderUtil.WritePrefab(AssetPaths.DummyPrefab, root =>
            {
                BuilderUtil.GetOrAdd<NetworkObject>(root);
                BuilderUtil.GetOrAdd<Targetable>(root);
                BuilderUtil.GetOrAdd<TargetDummy>(root);
                CapsuleCollider capsule = BuilderUtil.GetOrAdd<CapsuleCollider>(root);
                capsule.height = 1.8f;
                capsule.radius = 0.45f;
                capsule.center = new Vector3(0f, 0.9f, 0f);

                Transform visual = BuilderUtil.EnsurePath(root.transform, "Visual");
                GameObject torso = BuilderUtil.EnsurePrimitive(visual, "Torso", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 0f), Quaternion.identity,
                    new Vector3(0.8f, 0.8f, 0.8f), body, keepCollider: false);
                GameObject head = BuilderUtil.EnsurePrimitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.75f, 0f), Quaternion.identity,
                    new Vector3(0.5f, 0.5f, 0.5f), body, keepCollider: false);
                BuilderUtil.GetOrAdd<TargetDummyVisual>(root).Configure(
                    new[] { torso.GetComponent<Renderer>(), head.GetComponent<Renderer>() }, body, MaterialLibrary.Get("M_GreyboxElite"), visual);

                BuilderUtil.SetLayerRecursive(root, Layers.Enemy);
            });
        }

        static void BuildCameraRig()
        {
            LayerMask obstacles = Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Structure);
            LayerMask seeThrough = Layers.Mask(Layers.Enemy);
            Material indicatorMaterial = MaterialLibrary.Get("M_LockIndicator");

            BuilderUtil.WritePrefab(AssetPaths.CameraRigPrefab, root =>
            {
                CinemachineCamera free = BuildVirtualCamera(root.transform, "FreeCam", HeroCameraRig.FreeBoom, HeroCameraRig.FreePriority, obstacles, seeThrough);
                CinemachineCamera locked = BuildVirtualCamera(root.transform, "LockCam", HeroCameraRig.LockBoom, 0, obstacles, seeThrough);

                Transform groupTransform = BuilderUtil.EnsurePath(root.transform, "LockGroup");
                CinemachineTargetGroup group = BuilderUtil.GetOrAdd<CinemachineTargetGroup>(groupTransform.gameObject);
                group.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
                group.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
                group.UpdateMethod = CinemachineTargetGroup.UpdateMethods.LateUpdate;

                GameObject indicator = BuilderUtil.EnsurePrimitive(root.transform, "LockIndicator", PrimitiveType.Sphere, Vector3.zero, Quaternion.identity,
                    new Vector3(0.22f, 0.22f, 0.22f), indicatorMaterial, keepCollider: false);
                indicator.SetActive(false);

                BuilderUtil.GetOrAdd<HeroCameraRig>(root).Configure(free, locked, group, indicator.transform);
            });
        }

        // One CinemachineCamera: orbital follow around the Follow target (WorldSpace binding, so the Horizontal axis is a world yaw), rotation
        // composer on the LookAt target and a Deoccluder. The axes are driven from code by HeroCameraRig, never by Cinemachine's own input.
        static CinemachineCamera BuildVirtualCamera(Transform parent, string name, float boom, int priority, LayerMask obstacles, LayerMask seeThrough)
        {
            Transform t = BuilderUtil.EnsurePath(parent, name);
            CinemachineCamera cam = BuilderUtil.GetOrAdd<CinemachineCamera>(t.gameObject);
            LensSettings lens = LensSettings.Default;
            lens.FieldOfView = HeroCameraRig.Fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 500f;
            cam.Lens = lens;
            cam.Priority = priority;

            CinemachineOrbitalFollow orbit = BuilderUtil.GetOrAdd<CinemachineOrbitalFollow>(t.gameObject);
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = boom;
            orbit.TargetOffset = Vector3.zero;
            orbit.TrackerSettings = TrackerSettings.Default;
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.25f, 0.12f);
            InputAxis h = orbit.HorizontalAxis;
            h.Range = new Vector2(-180f, 180f);
            h.Wrap = true;
            h.Center = 0f;
            h.Value = 0f;
            h.Recentering.Enabled = false;
            orbit.HorizontalAxis = h;
            InputAxis v = orbit.VerticalAxis;
            v.Range = new Vector2(HeroLocomotion.MinPitchDownDeg, HeroLocomotion.MaxPitchDownDeg);
            v.Wrap = false;
            v.Center = HeroLocomotion.FreePitchDownDeg;
            v.Value = HeroLocomotion.FreePitchDownDeg;
            v.Recentering.Enabled = false;
            orbit.VerticalAxis = v;
            InputAxis r = orbit.RadialAxis;
            r.Range = new Vector2(1f, 1f);
            r.Value = 1f;
            r.Center = 1f;
            orbit.RadialAxis = r;

            CinemachineRotationComposer composer = BuilderUtil.GetOrAdd<CinemachineRotationComposer>(t.gameObject);
            composer.Composition = ScreenComposerSettings.Default;
            composer.Damping = new Vector2(0.25f, 0.4f);
            composer.TargetOffset = Vector3.zero;

            CinemachineDeoccluder deoccluder = BuilderUtil.GetOrAdd<CinemachineDeoccluder>(t.gameObject);
            deoccluder.CollideAgainst = obstacles;
            deoccluder.TransparentLayers = seeThrough;
            deoccluder.MinimumDistanceFromTarget = 0.3f;
            CinemachineDeoccluder.ObstacleAvoidance avoid = deoccluder.AvoidObstacles;
            avoid.Enabled = true;
            avoid.DistanceLimit = 0f;
            avoid.MinimumOcclusionTime = 0f;
            avoid.CameraRadius = 0.4f;
            avoid.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            avoid.MaximumEffort = 4;
            avoid.SmoothingTime = 0f;
            avoid.Damping = 0.4f;
            avoid.DampingWhenOccluded = 0.2f;
            avoid.UseFollowTarget.Enabled = true;   // rays start at the camera pivot, not at the group centre
            avoid.UseFollowTarget.YOffset = 0f;
            deoccluder.AvoidObstacles = avoid;
            return cam;
        }
    }
}
