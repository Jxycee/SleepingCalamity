using UnityEngine;

namespace AetherStrand
{
    /// <summary>
    /// Runs the web hair solver (AetherStrandWebSolver) and renders the live ribbon mesh with a plain MeshRenderer
    /// (no bone skinning). Replaces AetherStrandPhysics for hair authored with the web app.
    ///
    /// Frame: the solver runs in a world-aligned frame with X mirrored (Unity = web with X negated). The frame origin is fixed
    /// at Start so that headPivot's start position is web (0,0.44,0)*unitScale. Rest orientation of headPivot/bustRoot at Start
    /// counts as web identity (character facing +Z, upright). This GameObject is forced to the origin with identity rotation each
    /// LateUpdate, so keep it unparented (or under a parent without rotation/scale).
    /// Authority: visual only; every client simulates its own copy.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class AetherStrandWebHair : MonoBehaviour
    {
        [Tooltip("PresetGeometry.json (plugin format; web_trace.ts output adds solver extras for exact parity).")]
        public TextAsset geometryJson;
        [Tooltip("Optional recipe.json; its physics block overrides the config below at Start.")]
        public TextAsset recipeJson;
        public AetherStrandWebSolver.Config config = new AetherStrandWebSolver.Config();
        public Transform headPivot;
        [Tooltip("Torso collider frame (web rootGroup). Null = static identity at the hair origin.")]
        public Transform bustRoot;
        public Material material;
        [Tooltip("Meters per web unit.")]
        public float unitScale = 1f;
        [Header("Head fit (character rigs)")]
        [Tooltip("If set, hair is output in this bone's local space through the per-axis fit; the component should sit on a child of this bone with identity local pose.")]
        public Transform fitBone;
        public Vector3 fitScale = Vector3.one;
        public Vector3 fitOffset = Vector3.zero;
        [Tooltip("Simulate at the web's 60 Hz (deterministic); false = one variable step per frame (web live mode).")]
        public bool fixedTimestep = true;

        AetherStrandWebSolver solver;
        AetherStrandWebMesh meshBuilder;
        MeshFilter mf;
        MeshRenderer mr;
        Vector3 origin;
        Quaternion headRest = Quaternion.identity, rootRest = Quaternion.identity;
        float accumulator;
        double hpx, hpy, hpz, hqx, hqy, hqz, hqw;
        public AetherStrandWebSolver Solver => solver;
        public float LastStepMs { get; private set; }

        void Start()
        {
            if (geometryJson != null && headPivot != null) Rebuild();
        }

        /// <summary>(Re)load geometry, create the solver and mesh, and snap to the current head pose.</summary>
        public void Rebuild()
        {
            if (recipeJson != null)
            {
                var root = (System.Collections.Generic.Dictionary<string, object>)AetherStrandJson.Parse(recipeJson.text);
                if (root.ContainsKey("physics")) config = AetherStrandWebData.LoadConfig(root["physics"]);
            }
            string[] zones;
            var locks = AetherStrandWebData.LoadGeometry(geometryJson.text, out zones);
            solver = new AetherStrandWebSolver();
            solver.Load(locks);
            meshBuilder = new AetherStrandWebMesh(solver, locks);
            if (fitBone != null) { meshBuilder.useFit = true; meshBuilder.fitScale = fitScale; meshBuilder.fitOffset = fitOffset; }

            origin = headPivot.position - new Vector3(0f, 0.44f * unitScale, 0f);
            headRest = headPivot.rotation;
            rootRest = bustRoot != null ? bustRoot.rotation : Quaternion.identity;

            if (!TryGetComponent(out mf)) mf = gameObject.AddComponent<MeshFilter>();
            if (!TryGetComponent(out mr)) mr = gameObject.AddComponent<MeshRenderer>();
            mf.sharedMesh = meshBuilder.mesh;
            if (material == null)
            {
                var sh = Shader.Find("AetherStrand/SculptedHair_URP");
                if (sh != null) material = new Material(sh);
            }
            if (material != null) mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            ReadHead(out hpx, out hpy, out hpz, out hqx, out hqy, out hqz, out hqw);
            solver.SnapToHead(hpx, hpy, hpz, hqx, hqy, hqz, hqw, config.strandWidthScale);
            Refresh();
        }

        public void ApplyImpulse(Vector3 unityWorldImpulse) => solver?.ApplyImpulse(-unityWorldImpulse.x, unityWorldImpulse.y, unityWorldImpulse.z);

        public void SnapToHead()
        {
            if (solver == null) return;
            ReadHead(out hpx, out hpy, out hpz, out hqx, out hqy, out hqz, out hqw);
            solver.SnapToHead(hpx, hpy, hpz, hqx, hqy, hqz, hqw, config.strandWidthScale);
            Refresh();
        }

        void ReadHead(out double x, out double y, out double z, out double qx, out double qy, out double qz, out double qw)
        {
            ToWebPose(headPivot, headRest, out x, out y, out z, out qx, out qy, out qz, out qw);
        }

        void ToWebPose(Transform t, Quaternion rest, out double x, out double y, out double z, out double qx, out double qy, out double qz, out double qw)
        {
            Vector3 p = (t.position - origin) / unitScale;
            x = -p.x; y = p.y; z = p.z;
            Quaternion q = t.rotation * Quaternion.Inverse(rest);
            qx = q.x; qy = -q.y; qz = -q.z; qw = q.w; // mirror across the YZ plane
        }

        void LateUpdate()
        {
            if (solver == null) return;
            float t0 = Time.realtimeSinceStartup;
            ReadHead(out hpx, out hpy, out hpz, out hqx, out hqy, out hqz, out hqw);
            double rpx = 0, rpy = 0, rpz = 0, rqx = 0, rqy = 0, rqz = 0, rqw = 1;
            if (bustRoot != null) ToWebPose(bustRoot, rootRest, out rpx, out rpy, out rpz, out rqx, out rqy, out rqz, out rqw);

            if (fixedTimestep)
            {
                accumulator += Mathf.Min(Time.deltaTime, 4f / 60f);
                const float dt = 1f / 60f;
                int n = 0;
                while (accumulator >= dt && n < 4)
                {
                    solver.Step(dt, hpx, hpy, hpz, hqx, hqy, hqz, hqw, rpx, rpy, rpz, rqx, rqy, rqz, rqw, config);
                    accumulator -= dt; n++;
                }
                if (n == 0) { LastStepMs = 0; return; }
            }
            else solver.Step(Time.deltaTime, hpx, hpy, hpz, hqx, hqy, hqz, hqw, rpx, rpy, rpz, rqx, rqy, rqz, rqw, config);
            Refresh();
            LastStepMs = (Time.realtimeSinceStartup - t0) * 1000f;
        }

        void Refresh()
        {
            if (fitBone != null) { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; }
            else transform.SetPositionAndRotation(origin, Quaternion.identity);
            meshBuilder.Update(hpx, hpy, hpz, hqx, hqy, hqz, hqw, config.strandWidthScale, unitScale);
        }
    }
}
