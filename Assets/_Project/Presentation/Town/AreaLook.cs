// Authority: none (local presentation, every peer runs its own). M6 S3 look separation (proposal Q1 A, risk "look leakage"): Hollowmere is the session scene and the Cellars floors are
// built far under it in the same scene, so the town's grade, fog, sky, ambient and sun must not reach the cellar and the cellar's fog must not reach the town. Fog, ambient, skybox,
// reflection mode and the sun are GLOBAL render state (a local post volume cannot carry them), so this switcher owns all of it: it watches the local camera's height, and below
// CellarBelowY it is in the Cellar area, else the Town area. Per area it sets
//   - the two global post volumes' weights (town profile 1/0, cellar DungeonCorner profile 0/1, snapped at once on an area change, so a profile never bleeds through an overlay),
//   - the town's directional lights and town-only atmosphere roots (off in the cellar), the camera clear (solid dark in the cellar),
//   - RenderSettings: skybox, ambient trilight and intensity, reflection mode and fog (the town's are snapshotted from the scene at Awake; the cellar's fog is the last floor build's
//     CellarFloorBuilder.LastFog, which the build writes into RenderSettings at once, even while the viewer is still in the town: the Town area re-asserts its own fog every frame).
// Keyed by the camera, not by the dive state, because a teleport lags the dive flow. Test hook: ObserverOverride.
using UnityEngine;
using UnityEngine.Rendering;

namespace SleepingCalamity.Presentation
{
    [DefaultExecutionOrder(1000)]   // after CinemachineBrain (200)
    public sealed class AreaLook : MonoBehaviour
    {
        public enum Area : byte { Town = 0, Cellar = 1 }

        public Volume TownVolume, CellarVolume;
        public Light[] TownLights = new Light[0];          // the town's sun and shade fill
        public GameObject[] TownOnly = new GameObject[0];  // town-only atmosphere roots (pollen, god-ray shafts)
        public float CellarBelowY = -60f;                  // a viewer lower than this is in the cellar
        public float BlendSeconds = 0.4f;                  // unused: weights snap on an area change (kept so the stored scene value stays valid)
        public Transform ObserverOverride;                 // tests: stands in for the main camera

        public static AreaLook Current { get; private set; }
        public Area CurrentArea { get; private set; } = Area.Town;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; }

        public static Area AreaFor(float y, float cellarBelowY) => y < cellarBelowY ? Area.Cellar : Area.Town;

        // The town's global state, snapshotted from the scene before anything can change it.
        private bool _snap;
        private Material _sky; private UnityEngine.Rendering.AmbientMode _ambientMode; private Color _skyC, _eqC, _grC; private float _ambI;
        private DefaultReflectionMode _reflect; private bool _fog; private FogMode _fogMode; private float _fogD; private Color _fogC;
        private CameraClearFlags _clearFlags; private Color _background; private Camera _clearCamera;
        private bool _first = true;

        private static readonly Color CellarSky = new Color(0.23f, 0.19f, 0.18f), CellarEquator = new Color(0.22f, 0.18f, 0.15f), CellarGround = new Color(0.14f, 0.11f, 0.09f);
        private static readonly Color CellarFogDefault = new Color(0.16f, 0.13f, 0.11f);
        private const float CellarFogDensityDefault = 0.007f;

        public float TownWeight => TownVolume != null ? TownVolume.weight : 1f;
        public float CellarWeight => CellarVolume != null ? CellarVolume.weight : 0f;

        private void Awake()
        {
            Current = this;
            Snapshot();
        }

        private void OnDestroy() { if (Current == this) Current = null; }

        private void Snapshot()
        {
            _sky = RenderSettings.skybox; _ambientMode = RenderSettings.ambientMode; _skyC = RenderSettings.ambientSkyColor; _eqC = RenderSettings.ambientEquatorColor;
            _grC = RenderSettings.ambientGroundColor; _ambI = RenderSettings.ambientIntensity; _reflect = RenderSettings.defaultReflectionMode;
            _fog = RenderSettings.fog; _fogMode = RenderSettings.fogMode; _fogD = RenderSettings.fogDensity; _fogC = RenderSettings.fogColor;
            _snap = true;
        }

        // LateUpdate with a late execution order: the Cinemachine brain moves the camera in its own LateUpdate, so reading the camera any earlier sees last frame's position and the
        // teleport frame would render with the old area's sky, sun, fog and grade. Running after the brain, the look is right on the first rendered frame.
        private void LateUpdate()
        {
            if (!_snap) Snapshot();
            Transform observer = ObserverOverride;
            if (observer == null) { Camera cam = Camera.main; if (cam != null) observer = cam.transform; }
            if (observer != null)
            {
                Area wanted = AreaFor(observer.position.y, CellarBelowY);
                if (wanted != CurrentArea || _first) Apply(wanted);
            }
            else if (_first) Apply(Area.Town);
            _first = false;
            if (CurrentArea == Area.Town) HoldTownFog();   // a floor build may have written the cellar fog while the viewer is still up here
        }

        private void HoldTownFog()
        {
            if (RenderSettings.fog != _fog) RenderSettings.fog = _fog;
            if (RenderSettings.fogMode != _fogMode) RenderSettings.fogMode = _fogMode;
            if (!Mathf.Approximately(RenderSettings.fogDensity, _fogD)) RenderSettings.fogDensity = _fogD;
            if (RenderSettings.fogColor != _fogC) RenderSettings.fogColor = _fogC;
        }

        // Public so a test can drive an area directly.
        public void Apply(Area area)
        {
            if (!_snap) Snapshot();
            CurrentArea = area;
            bool town = area == Area.Town;
            RenderSettings.skybox = town ? _sky : null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = town ? _skyC : CellarSky; RenderSettings.ambientEquatorColor = town ? _eqC : CellarEquator;
            RenderSettings.ambientGroundColor = town ? _grC : CellarGround; RenderSettings.ambientIntensity = town ? _ambI : CellarArtTuning.Ambient;
            if (town) RenderSettings.ambientMode = _ambientMode;
            RenderSettings.defaultReflectionMode = town ? _reflect : DefaultReflectionMode.Custom;
            if (town) HoldTownFog();
            else
            {
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = CellarFloorBuilder.HasLastFog ? CellarFloorBuilder.LastFogDensity : CellarFogDensityDefault;
                RenderSettings.fogColor = CellarFloorBuilder.HasLastFog ? CellarFloorBuilder.LastFogColor : CellarFogDefault;
            }
            foreach (Light l in TownLights) if (l != null) l.enabled = town;
            foreach (GameObject g in TownOnly) if (g != null) g.SetActive(town);
            SetCameraClear(town);
            // The grade snaps with the area (a teleport is a cut, so a blend would show the old area's grade for a moment).
            if (TownVolume != null) TownVolume.weight = town ? 1f : 0f;
            if (CellarVolume != null) CellarVolume.weight = town ? 0f : 1f;
            DynamicGI.UpdateEnvironment();
        }

        private void SetCameraClear(bool town)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (town)
            {
                if (_clearCamera != null) { _clearCamera.clearFlags = _clearFlags; _clearCamera.backgroundColor = _background; _clearCamera = null; }
                return;
            }
            if (_clearCamera == null) { _clearCamera = cam; _clearFlags = cam.clearFlags; _background = cam.backgroundColor; }
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.015f, 0.012f);
        }
    }
}
