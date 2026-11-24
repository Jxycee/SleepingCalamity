Shader "Parity/BustWeb"
{
    // Test-mannequin shader approximating three.js MeshStandardMaterial under the web's 3-light rig + hemisphere + environment ambient,
    // with the web's display transform (ACES exposure 1.16 + sRGB) in the shader. Harness only.
    Properties
    {
        _Color ("Albedo (linear)", Color) = (1,1,1,1)
        _Rough ("Roughness", Float) = 0.8
        _KeyDirW ("k", Vector) = (0,1,0,0) _KeyColW ("kc", Vector) = (1,1,1,0)
        _FillDirW ("f", Vector) = (0,1,0,0) _FillColW ("fc", Vector) = (1,1,1,0)
        _RimDirW ("r", Vector) = (0,1,0,0) _RimColW ("rc", Vector) = (1,1,1,0)
        _SkyW ("s", Vector) = (1,1,1,0) _GndW ("g", Vector) = (1,1,1,0)
        _EnvAmb ("env ambient add", Vector) = (0,0,0,0)
        _SpecK ("spec scale", Float) = 0.04
        _Dbg ("debug", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Rough; float _SpecK; float _Dbg;
                float4 _KeyDirW, _KeyColW, _FillDirW, _FillColW, _RimDirW, _RimColW, _SkyW, _GndW, _EnvAmb;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; };
            struct VO { float4 cs : SV_POSITION; float3 n : TEXCOORD0; float3 ws : TEXCOORD1; };
            VO Vert(A i) { VO o; o.cs = TransformObjectToHClip(i.p.xyz); o.n = TransformObjectToWorldNormal(i.n); o.ws = TransformObjectToWorld(i.p.xyz); return o; }
            float3 WebDisplay(float3 c, float exposure)
            {
                c *= exposure / 0.6;
                float3 a = float3(dot(c, float3(0.59719, 0.35458, 0.04823)), dot(c, float3(0.07600, 0.90834, 0.01566)), dot(c, float3(0.02840, 0.13383, 0.83777)));
                a = (a * (a + 0.0245786) - 0.000090537) / (a * (0.983729 * a + 0.4329510) + 0.238081);
                float3 o = saturate(float3(dot(a, float3(1.60475, -0.53108, -0.07367)), dot(a, float3(-0.10208, 1.10813, -0.00605)), dot(a, float3(-0.00327, -0.07276, 1.07602))));
                return lerp(1.055 * pow(o, 1.0 / 2.4) - 0.055, o * 12.92, step(o, 0.0031308));
            }
            half4 Frag(VO i) : SV_Target
            {
                float3 N = normalize(i.n);
                float3 Vw = normalize(_WorldSpaceCameraPos - i.ws);
                float3 irr = lerp(_GndW.rgb, _SkyW.rgb, N.y * 0.5 + 0.5) + _EnvAmb.rgb * (0.6 + 0.4 * N.y)
                    + _KeyColW.rgb * saturate(dot(N, normalize(_KeyDirW.xyz)))
                    + _FillColW.rgb * saturate(dot(N, normalize(_FillDirW.xyz)))
                    + _RimColW.rgb * saturate(dot(N, normalize(_RimDirW.xyz)));
                float3 c = _Color.rgb * irr;
                if (_Dbg > 2.5) return half4(N * 0.5 + 0.5, 1); if (_Dbg > 1.5) return half4(irr, 1); if (_Dbg > 0.5) return half4(_Color.rgb * 4, 1);
                float3 H = normalize(normalize(_KeyDirW.xyz) + Vw);
                c += _KeyColW.rgb * pow(saturate(dot(N, H)), lerp(6, 48, 1 - _Rough)) * (1 - _Rough) * _SpecK;
                return half4(WebDisplay(c, 1.16), 1);
            }
            ENDHLSL
        }
    }
}
