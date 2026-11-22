// Authority: none (presentation only, every peer binds its own copy). Ground-blend step: loads the town's baked ground (written at build time by EditorTools.SCGroundBaker as raw
// .bytes: height R16, albedo RGBA32 with smoothness in A) into two textures and publishes them, with the bake's bounds, as the shader globals the ruin skin
// (SC_WearForwardPass, _SC_RUINSKIN) reads. Globals sit outside every material's cbuffer, so the SRP batcher is untouched. No bake bound = bounds.z of 0 = the shader's old pivot fallback.
// TextAsset references survive scene and player builds; the textures are rebuilt from them on enable (ExecuteAlways: edit mode, captures and play all bind).
using UnityEngine;

namespace SleepingCalamity.Presentation
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SCGroundBlendBinder : MonoBehaviour
    {
        public TextAsset HeightBytes;          // R16, little endian, Resolution x Resolution, row 0 = min z
        public TextAsset AlbedoBytes;          // RGBA32, same layout
        public int Resolution = 512;
        public Vector4 GroundBounds;           // (minX, minZ, 1/sizeX, 1/sizeZ)
        public Vector4 GroundHeightParams;     // (minY, rangeY, 1/res, 1/res)

        static readonly int IdHeight = Shader.PropertyToID("_SC_GroundHeightTex");
        static readonly int IdAlbedo = Shader.PropertyToID("_SC_GroundAlbedoTex");
        static readonly int IdBounds = Shader.PropertyToID("_SC_GroundBounds");
        static readonly int IdParams = Shader.PropertyToID("_SC_GroundHeightParams");

        Texture2D height, albedo;

        void OnEnable() => Bind();

        void OnDisable()
        {
            Shader.SetGlobalVector(IdBounds, Vector4.zero);   // shader falls back to the pivot measure
            Release();
        }

        void OnValidate() { if (isActiveAndEnabled) Bind(); }

        /// <summary>Rebuilds the two textures from the byte assets and sets the globals. Does nothing (and leaves the shader on its fallback) when the data is missing or the wrong size.</summary>
        public void Bind()
        {
            Release();
            int n = Resolution * Resolution;
            if (HeightBytes == null || AlbedoBytes == null || Resolution < 2) return;
            if (HeightBytes.bytes.Length != n * 2 || AlbedoBytes.bytes.Length != n * 4)
            {
                Debug.LogWarning("SCGroundBlendBinder: bake data has the wrong size for resolution " + Resolution + "; ground blend off");
                return;
            }
            height = new Texture2D(Resolution, Resolution, TextureFormat.R16, false, true) { name = "SC_GroundHeight", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            height.LoadRawTextureData(HeightBytes.GetData<byte>()); height.Apply(false, true);
            albedo = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, false) { name = "SC_GroundAlbedo", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            albedo.LoadRawTextureData(AlbedoBytes.GetData<byte>()); albedo.Apply(false, true);
            Shader.SetGlobalTexture(IdHeight, height);
            Shader.SetGlobalTexture(IdAlbedo, albedo);
            Shader.SetGlobalVector(IdBounds, GroundBounds);
            Shader.SetGlobalVector(IdParams, GroundHeightParams);
        }

        void Release()
        {
            if (height != null) DestroyTex(height);
            if (albedo != null) DestroyTex(albedo);
            height = albedo = null;
        }

        static void DestroyTex(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
    }
}
