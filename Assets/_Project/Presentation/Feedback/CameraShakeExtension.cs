// Authority: none (local, cosmetic). A Cinemachine extension added to the hero's two cameras at runtime by HeroCameraRig. It adds the rig's current shake
// rotation (a TraumaShake sample, computed once per frame in the rig's Update) at the Noise stage, after aim and body, so Cinemachine's own damping and
// the deoccluder never overwrite it and it never moves the follow target.
using Unity.Cinemachine;
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    public sealed class CameraShakeExtension : CinemachineExtension
    {
        public HeroCameraRig Rig;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Noise || Rig == null) return;
            Vector3 e = Rig.ShakeEuler;
            if (e == Vector3.zero) return;
            state.OrientationCorrection = state.OrientationCorrection * Quaternion.Euler(e.y, e.x, e.z);   // pitch, yaw, roll
        }
    }
}
