// SleepingCalamity/Backdrop: Hollowmere's far country past the terrain (Q15 backdrop ring, the mid hills and the far ridge, their tree cards and road).
// The town's exp2 haze (HollowmereLook) turns everything past ~150 m into one flat fog colour, so the backdrop takes no scene fog and grades itself
// toward the fog colour instead (unity_FogColor: Q12's pale horizon colour, warm at low sun): _HazeNear at _DistNear to _HazeFar at _DistFar from the
// camera, a touch more at the foot (_GroundMist below _MistTop) and an optional tint, so near trees, mid hills and the far ridge sit at 2-3 distinct
// depths like painted backdrop layers. Lit by the main light (wrapped Lambert) and the ambient SH; alpha clip for the cards. No shadows cast.
Shader "SleepingCalamity/Backdrop"
{
    Properties
    {
        _BaseMap ("Base map", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _HazeNear ("Haze at DistNear", Range(0, 1)) = 0.7
        _HazeFar ("Haze at DistFar", Range(0, 1)) = 0.9
        _DistNear ("DistNear (m)", Float) = 150
        _DistFar ("DistFar (m)", Float) = 700
        _HazeTint ("Haze tint (x fog colour)", Color) = (1, 1, 1, 1)
        _GroundMist ("Extra haze at the foot", Range(0, 0.5)) = 0.06
        _MistTop ("Mist top (world y)", Float) = -5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _HazeNear, _HazeFar;
            float _DistNear, _DistFar;
            half4 _HazeTint;
            half _GroundMist;
            float _MistTop;
            half _AlphaClip, _Cull;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
                #endif
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                Light l = GetMainLight();
                half wrap = saturate(dot(n, l.direction) * 0.6 + 0.4);
                half3 lit = c.rgb * (l.color * wrap + SampleSH(n));
                float d = distance(i.positionWS, GetCameraPositionWS());
                half h = lerp(_HazeNear, _HazeFar, saturate((d - _DistNear) / max(1.0, _DistFar - _DistNear)));
                h = saturate(h + _GroundMist * saturate((_MistTop - i.positionWS.y) / 20.0));
                half3 haze = unity_FogColor.rgb * _HazeTint.rgb;
                return half4(lerp(lit, haze, h), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
}
