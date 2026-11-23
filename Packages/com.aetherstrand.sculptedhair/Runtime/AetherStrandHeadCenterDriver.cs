using UnityEngine;

namespace AetherStrand
{
    /// <summary>
    /// Keeps the hair shader's head-centre (used for the dome normal blend and dome shading) in sync with the head bone.
    /// Authority: local presentation only (no networking).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class AetherStrandHeadCenterDriver : MonoBehaviour
    {
        public Transform headBone;
        public Vector3 localCenter = new Vector3(0f, 0.425f, -0.01f);
        public Renderer target;

        static readonly int HeadCenterId = Shader.PropertyToID("_HeadCenterWS");
        MaterialPropertyBlock _mpb;

        void OnEnable() { Apply(); }
        void LateUpdate() { Apply(); }

        public void Apply()
        {
            if (headBone == null) return;
            var r = target != null ? target : GetComponent<Renderer>();
            if (r == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);
            Vector3 w = headBone.TransformPoint(localCenter);
            _mpb.SetVector(HeadCenterId, new Vector4(w.x, w.y, w.z, 0f));
            r.SetPropertyBlock(_mpb);
        }
    }
}
