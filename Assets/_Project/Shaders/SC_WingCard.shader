// T24: a photo-textured wing mesh for butterflies, bees and birds near the eye (Hollowmere ambient life). Companion to SC_BirdCard (which stays for far flyers): same wing flap and the same
// _Fold / _FadeNear / _FadeFar behaviour, but the colour comes from a real photo of a pinned specimen with an alpha cut-out (_MainTex, alpha-clipped), lit by the sun and the sky,
// with a translucency term so a wing held against the light glows (a thin wing passes light: the term is the sun behind the wing seen from the camera, tinted by the albedo).
// The mesh (AmbientWingMesh) has the wing span along X (x in -1..1, so |x| is the flap weight), body forward +Z, uv = the photo's own 0..1 layout. Unlit-ish: no shadows cast or received.
Shader "SleepingCalamity/WingCard"
{
    Properties
    {
        _MainTex ("Photo (RGB) and cut-out (A)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.5
        _Translucency ("Backlit wing glow", Range(0, 2)) = 0.9
        _FlapSpeed ("Flap speed (rad/s)", Float) = 20
        _FlapAmp ("Flap amplitude (fraction of the half-span)", Float) = 0.5
        _Fold ("Wings folded (0 open, 1 tucked)", Range(0, 1)) = 0
        _Dihedral ("Resting wing-tip lift", Float) = 0.12
        _FadeNear ("Fully faded at (m from the camera)", Float) = 0.4
        _FadeFar ("Fully visible from (m from the camera)", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Cutoff, _Translucency;
                float _FlapSpeed, _FlapAmp;
                half _Fold;
                float _Dihedral, _FadeNear, _FadeFar;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; float camDist : TEXCOORD2; float3 viewWS : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 p = i.positionOS.xyz;
                float4x4 m = GetObjectToWorldMatrix();
                float phase = m._m03 * 1.7 + m._m13 * 0.9 + m._m23 * 2.3;
                float w = saturate(abs(p.x));
                float flap = sin(_Time.y * _FlapSpeed + phase) * _FlapAmp * w * w * (1.0 - _Fold);
                p.y += flap + _Dihedral * w * w * (1.0 - _Fold);
                p.x *= lerp(1.0, 1.0 - 0.75 * w, _Fold);   // folded: the wings close over the back (a perched butterfly)
                p.y += _Fold * w * 0.55 * (1.0 - w * 0.4);
                VertexPositionInputs v = GetVertexPositionInputs(p);
                o.positionCS = v.positionCS;
                o.camDist = distance(v.positionWS, GetCameraPositionWS());
                o.viewWS = normalize(GetCameraPositionWS() - v.positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(v.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                clip(tex.a - _Cutoff);
                clip(saturate((i.camDist - _FadeNear) / max(0.01, _FadeFar - _FadeNear)) - InterleavedGradientNoise(i.positionCS.xy, 0) * 0.999);
                half3 albedo = tex.rgb * _Tint.rgb;
                Light l = GetMainLight();
                half3 amb = SampleSH(half3(0, 1, 0));
                half3 c = albedo * (amb * 1.0h + l.color * 0.55h);
                half back = pow(saturate(dot(-i.viewWS, l.direction)), 2.0h);   // sun behind the wing, seen from the camera
                c += albedo * albedo * l.color * back * _Translucency * 1.6h;
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
