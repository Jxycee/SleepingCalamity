// SleepingCalamity/BackdropWisp: soft sprites on the far backdrop (the warm steam over Mawrrow's mountain, the lighthouse glint). Unlit, no depth write,
// no scene fog (the town's exp2 haze would erase anything past ~300 m), blend factors from the material (_DstBlend 10 = alpha, 1 = additive).
// Tint x vertex colour (particles) x the sprite's red channel as alpha.
Shader "SleepingCalamity/BackdropWisp"
{
    Properties
    {
        _BaseMap ("Soft sprite (R = alpha)", 2D) = "white" {}
        _Tint ("Tint (rgb, a = strength)", Color) = (1, 0.82, 0.62, 0.2)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Wisp"
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.color = i.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).r;
                return half4(_Tint.rgb * i.color.rgb, a * _Tint.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
