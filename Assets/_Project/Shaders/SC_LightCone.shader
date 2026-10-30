// Soft additive light-shaft cone: the fallback for volumetric god rays and lamp shafts (PHASE1 3.5). Alpha fades along the cone's length
// (uv.y: 0 at the source, 1 at the far end) and toward its silhouette edge (view-facing falloff), so a stack of cones reads as dusty light.
// Unlit, additive, no depth write; fog is not applied (the shaft is light, it should not be fogged out).
Shader "SleepingCalamity/LightCone"
{
    Properties
    {
        _Color ("Colour (HDR)", Color) = (1.0, 0.86, 0.6, 1)
        _Intensity ("Intensity", Range(0, 2)) = 0.12
        _EdgePower ("Edge falloff", Range(0.5, 6)) = 2.0
        _LengthFade ("Length fade", Range(0.2, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Cone"
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _LengthFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 nWS : TEXCOORD1; float3 vWS : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = i.uv;
                o.nWS = TransformObjectToWorldNormal(i.normalOS);
                o.vWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float facing = abs(dot(normalize(i.nWS), normalize(i.vWS)));
                float edge = pow(saturate(facing), _EdgePower);
                float along = pow(saturate(1.0 - i.uv.y), _LengthFade);
                float a = edge * along * _Intensity;
                return half4(_Color.rgb * a, 1);
            }
            ENDHLSL
        }
    }
}
