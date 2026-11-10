// Stylised hair cards (Tobin's HairDeck hair, Art/Blender/tools/experimental/tobin_hair/hairdeck): two-sided lit cards, no alpha.
// Mesh contract (hd_export.py): uv0 = (u across the card 0..1, v along it, 0 root .. 1 tip), uv1.x = per-card random 0..1,
// vertex colour = HairDeck's colour along the card (sRGB). Hair direction = the uv0 bitangent (imported MikkTSpace tangents).
// Colour: root -> middle -> tip gradient (sampled from the reference) with light strands and per-strand variation, or HairDeck's vertex
// colours (_GradientMix 0). Shading: wrapped diffuse, root darkening / tip lift along v, procedural strands across u, two Kajiya-Kay highlight lobes
// (a sharp one shifted toward the tip, a broad tinted one toward the root), main + additional lights, receives and casts shadows.
Shader "SleepingCalamity/HairCards"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _UseVertexColor ("Use vertex colour (0 = tint only)", Range(0, 1)) = 1
        _GradientMix ("Gradient colour (1) vs HairDeck vertex colour (0)", Range(0, 1)) = 1
        _RootColor ("Gradient root", Color) = (0.25, 0.17, 0.13, 1)
        _MidColor ("Gradient middle", Color) = (0.46, 0.35, 0.26, 1)
        _TipColor ("Gradient tip", Color) = (0.56, 0.44, 0.33, 1)
        _GradMid ("Gradient middle position along the card", Range(0.05, 0.95)) = 0.4
        _BlondeColor ("Light strand colour", Color) = (0.68, 0.55, 0.40, 1)
        _BlondeChance ("Share of light strands", Range(0, 1)) = 0.15
        _StrandVar ("Per-strand brightness variation", Range(0, 0.6)) = 0.22
        _RootDark ("Root darkening", Range(0, 1)) = 0.25
        _RootLen ("Root darkening length (fraction of the card)", Range(0.01, 1)) = 0.35
        _TipLift ("Tip lightening", Range(0, 1)) = 0.12
        _CardVar ("Per-card brightness variation", Range(0, 0.5)) = 0.12
        _Strands ("Strands across a card", Float) = 9
        _StrandDepth ("Strand contrast", Range(0, 1)) = 0.35
        _Wrap ("Diffuse wrap", Range(0, 1)) = 0.4
        _Spec1Color ("Highlight 1 colour", Color) = (0.55, 0.47, 0.40, 1)
        _Spec1Exp ("Highlight 1 sharpness", Range(1, 512)) = 120
        _Spec1Shift ("Highlight 1 shift", Range(-1, 1)) = 0.08
        _Spec2Color ("Highlight 2 colour (tinted by the hair)", Color) = (0.35, 0.28, 0.22, 1)
        _Spec2Exp ("Highlight 2 sharpness", Range(1, 512)) = 24
        _Spec2Shift ("Highlight 2 shift", Range(-1, 1)) = -0.12
        _SpecNoise ("Highlight break-up", Range(0, 1)) = 0.5
        _Ambient ("Ambient strength", Range(0, 3)) = 1.4
        _ShadowStrength ("Received shadow strength (hair self-shadow is soft)", Range(0, 1)) = 0.55
        _ShadowNormalOffset ("Shadow lookup normal offset (m)", Range(0, 0.02)) = 0.008
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half _UseVertexColor; half _GradientMix; half4 _RootColor; half4 _MidColor; half4 _TipColor; half _GradMid;
            half4 _BlondeColor; half _BlondeChance; half _StrandVar; half _RootDark; half _RootLen; half _TipLift; half _CardVar;
            float _Strands; half _StrandDepth; half _Wrap;
            half4 _Spec1Color; half _Spec1Exp; half _Spec1Shift; half4 _Spec2Color; half _Spec2Exp; half _Spec2Shift; half _SpecNoise;
            half _Ambient; half _ShadowStrength; float _ShadowNormalOffset; float _Cull;
        CBUFFER_END
        float Hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }
        float Noise1(float x) { float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f); return lerp(Hash11(i), Hash11(i + 1), f); }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 hairWS : TEXCOORD2;
                float4 uv : TEXCOORD3; half4 color : TEXCOORD4; half fog : TEXCOORD5;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS; o.hairWS = n.bitangentWS;
                o.uv = float4(i.uv, i.uv1); o.color = i.color; o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 KK(float3 T, float3 N, float3 H, half shift, half e)
            {
                float3 t = normalize(T + N * shift); float th = dot(t, H);
                return smoothstep(-1.0, 0.0, th) * pow(saturate(sqrt(max(0.0, 1.0 - th * th))), e);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 N = normalize(i.normalWS); N = dot(N, V) < 0.0 ? -N : N;   // thin cards: the seen side faces the viewer (SV_IsFrontFace flips wrong under the hair rig's mirrored transform)
                float3 T = normalize(i.hairWS);
                float u = i.uv.x, v = saturate(i.uv.y), cardRnd = i.uv.z;
                half3 vc = lerp(half3(1, 1, 1), SRGBToLinear(i.color.rgb), _UseVertexColor);
                half3 grad = v < _GradMid ? lerp(_RootColor.rgb, _MidColor.rgb, smoothstep(0.0, _GradMid, v))
                                          : lerp(_MidColor.rgb, _TipColor.rgb, smoothstep(_GradMid, 1.0, v));
                float r2 = Hash11(cardRnd * 911.0 + 7.0);
                grad = lerp(grad, _BlondeColor.rgb, (r2 > 1.0 - _BlondeChance ? 0.75 : 0.0) * smoothstep(0.1, 0.6, v));   // light strands, toward the tip
                grad *= 1.0 + _StrandVar * (Hash11(cardRnd * 313.0 + 1.0) * 2.0 - 1.0);
                half3 base = _BaseColor.rgb * lerp(vc, grad, _GradientMix);
                half strand = Noise1(u * _Strands + cardRnd * 37.0) * 0.6 + Noise1(u * _Strands * 2.7 + cardRnd * 11.0 + v * 1.5) * 0.4;
                base *= (1.0 - _StrandDepth * 0.5) + _StrandDepth * strand;
                base *= 1.0 + _CardVar * (cardRnd * 2.0 - 1.0);
                half root = 1.0 - _RootDark * (1.0 - smoothstep(0.0, _RootLen, v));
                base *= root; base += base * _TipLift * smoothstep(0.55, 1.0, v);
                half specMask = lerp(1.0, Noise1(u * _Strands * 1.7 + cardRnd * 53.0 + v * 4.0), _SpecNoise);

                float4 sc = TransformWorldToShadowCoord(i.positionWS + N * _ShadowNormalOffset);
                Light ml = GetMainLight(sc);
                float3 L = ml.direction; float3 H = normalize(L + V);
                half atten = lerp(1.0h, ml.shadowAttenuation, _ShadowStrength) * ml.distanceAttenuation;
                half ndl = dot(N, L);
                half diff = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half3 spec = (_Spec1Color.rgb * KK(T, N, H, _Spec1Shift, _Spec1Exp) + _Spec2Color.rgb * base * 2.0 * KK(T, N, H, _Spec2Shift, _Spec2Exp)) * specMask * saturate(ndl * 2.0 + 0.3);
                half3 c = (base * diff + spec) * ml.color * atten;
                c += base * SampleSH(N) * _Ambient * (0.55 + 0.45 * root);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if USE_FORWARD_PLUS
                InputData inputData = (InputData)0; inputData.positionWS = i.positionWS; inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #endif
                LIGHT_LOOP_BEGIN(count)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    half ad = saturate((dot(N, al.direction) + _Wrap) / (1.0 + _Wrap));
                    c += base * ad * al.color * al.distanceAttenuation * al.shadowAttenuation;
                LIGHT_LOOP_END
                #endif
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert(Attributes i) : SV_POSITION
            {
                float3 pw = TransformObjectToWorld(i.positionOS.xyz); float3 nw = TransformObjectToWorldNormal(i.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - pw);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            float4 vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n) * 0.5 + 0.5; return half4(PackFloat2To888(oct), 0);
                #else
                return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
