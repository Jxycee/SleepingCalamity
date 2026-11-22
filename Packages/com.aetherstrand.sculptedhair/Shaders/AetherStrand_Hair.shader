Shader "AetherStrand/SculptedHair_URP"
{
    Properties
    {
        [Header(Textures)]
        [MainTexture] _GrooveAoMap ("Carved Strand Groove AO (R)", 2D) = "white" {}
        _FlowIdMap ("Flow / ID / Shift Map (R=ID, G=Grad, B=Shift)", 2D) = "gray" {}
        [Normal] _NormalMap ("Strand Tangent Normal", 2D) = "bump" {}
        _AlphaMap ("Feathered Wispy-Tip Alpha Mask (R)", 2D) = "white" {}
        [Toggle(_USE_ALPHA_CLIP)] _UseAlphaClip ("Enable Feathered Wispy-Tip Clipping", Float) = 1
        _AlphaClip ("Alpha Cutoff", Range(0.05, 0.85)) = 0.34
        _DitherStrength ("Tip Dither Strength (Non-MSAA)", Range(0.0, 1.0)) = 0.0

        [Header(Root To Tip Color Gradient)]
        _RootColor ("Root Color", Color) = (0.149, 0.094, 0.059, 1.0)
        _BaseColor ("Mid / Base Color", Color) = (0.298, 0.200, 0.133, 1.0)
        _TipColor ("Tip Color", Color) = (0.380, 0.263, 0.176, 1.0)
        _StrandVariation ("Per-Strand Tonal Variation", Range(0.0, 0.4)) = 0.220
        _RootAO ("Root Ambient Occlusion", Range(0.0, 1.0)) = 0.460
        _GrooveDepth ("Carved Fiber Groove Depth", Range(0.1, 1.0)) = 0.680
        _HeadCenterBlend ("Radial Head Normal Blend", Range(0.0, 0.7)) = 0.30

        [Header(Kajiya Kay Dual Lobe Anisotropic Specular)]
        _PrimarySpecColor ("Primary Specular Color", Color) = (0.851, 0.729, 0.612, 1.0)
        _PrimaryShift ("Primary Tangent Shift", Range(-0.6, 0.6)) = 0.080
        _PrimaryPower ("Primary Specular Power", Range(16.0, 256.0)) = 84.0
        _PrimaryIntensity ("Primary Specular Intensity", Range(0.0, 3.0)) = 0.340

        _SecondarySpecColor ("Secondary Specular Color", Color) = (0.549, 0.365, 0.227, 1.0)
        _SecondaryShift ("Secondary Tangent Shift", Range(-0.6, 0.6)) = -0.150
        _SecondaryPower ("Secondary Specular Power", Range(8.0, 140.0)) = 32.0
        _SecondaryIntensity ("Secondary Specular Intensity", Range(0.0, 3.0)) = 0.200

        [Header(Rim And Backlight Scatter)]
        _RimIntensity ("Rim Backlight Intensity", Range(0.0, 3.0)) = 0.280
        _RimPower ("Rim Fresnel Exponent", Range(1.0, 6.0)) = 3.40
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_GrooveAoMap); SAMPLER(sampler_GrooveAoMap);
        TEXTURE2D(_FlowIdMap);   SAMPLER(sampler_FlowIdMap);
        TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
        TEXTURE2D(_AlphaMap);    SAMPLER(sampler_AlphaMap);

        // Optional per-renderer head center (set via MaterialPropertyBlock by AetherStrandPhysics)
        float4 _HeadCenterWS; // xyz = world center, w = 1 when active

        CBUFFER_START(UnityPerMaterial)
            float4 _GrooveAoMap_ST;
            float4 _RootColor;
            float4 _BaseColor;
            float4 _TipColor;
            float4 _PrimarySpecColor;
            float4 _SecondarySpecColor;
            float  _StrandVariation;
            float  _RootAO;
            float  _GrooveDepth;
            float  _HeadCenterBlend;
            float  _PrimaryShift;
            float  _PrimaryPower;
            float  _PrimaryIntensity;
            float  _SecondaryShift;
            float  _SecondaryPower;
            float  _SecondaryIntensity;
            float  _RimIntensity;
            float  _RimPower;
            float  _AlphaClip;
            float  _DitherStrength;
        CBUFFER_END

        float EvaluateTipAlpha(float2 uv, float4 vertColor, out float2 strandUv)
        {
            float t = saturate(uv.y);
            strandUv = float2(uv.x * 0.65 + vertColor.b * 3.13, t);
            float cardAlpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, strandUv).r;
            float edgeDist = min(abs(uv.x - 0.34), abs(uv.x - 0.66));
            float lateralFray = smoothstep(0.0, 0.06, edgeDist);
            float tipRamp = smoothstep(0.72, 0.98, t);
            return cardAlpha * lerp(1.0, 0.52 + 0.48 * lateralFray, tipRamp);
        }

        float InterleavedGradientNoise(float2 pixelPos)
        {
            return frac(52.9829189 * frac(dot(pixelPos, float2(0.06711056, 0.00583715))));
        }

        void ApplyTipAlphaClip(float2 uv, float4 vertColor, float2 pixelPos, bool allowDither)
        {
        #ifdef _USE_ALPHA_CLIP
            float t = saturate(uv.y);
            if (vertColor.a > 0.5 && t > 0.70)
            {
                float2 unusedUv;
                float combinedAlpha = EvaluateTipAlpha(uv, vertColor, unusedUv);
                float cutoff = _AlphaClip;
                if (allowDither && _DitherStrength > 0.001)
                {
                    float ign = InterleavedGradientNoise(pixelPos);
                    cutoff = lerp(_AlphaClip, ign, _DitherStrength * 0.5);
                }
                clip(combinedAlpha - cutoff);
            }
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR; // R=PhysicsMask, G=LayerAO, B=StrandSeed, A=TipClipFlag
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 vertColor   : TEXCOORD5;
                float  fogFactor   : TEXCOORD6;
            };

            float3 ShiftTangent(float3 T, float3 N, float shift)
            {
                return normalize(T + N * shift);
            }

            float StrandSpecular(float3 T, float3 V, float3 L, float exponent)
            {
                float3 H = normalize(L + V);
                float dotTH = dot(T, H);
                float sinTH = sqrt(max(0.0001, 1.0 - dotTH * dotTH));
                float dirAtten = smoothstep(-1.0, 0.0, dotTH);
                return dirAtten * pow(sinTH, exponent);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS  = posInputs.positionCS;
                output.positionWS  = posInputs.positionWS;
                output.normalWS    = normInputs.normalWS;
                output.tangentWS   = normInputs.tangentWS;
                output.bitangentWS = normInputs.bitangentWS;
                output.uv          = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor   = input.color;
                output.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input, half facing : VFACE) : SV_Target
            {
                float t = saturate(input.uv.y);
                float2 strandUv = float2(input.uv.x * 0.65 + input.vertColor.b * 3.13, t);

                ApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy, true);

                float grooveAo = SAMPLE_TEXTURE2D(_GrooveAoMap, sampler_GrooveAoMap, strandUv).r;
                float3 flowId  = SAMPLE_TEXTURE2D(_FlowIdMap, sampler_FlowIdMap, strandUv).rgb;
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, strandUv), _GrooveDepth * 0.34);

                float faceSign = facing >= 0.0h ? 1.0 : -1.0;
                float3 N = normalize(input.normalWS) * faceSign;
                float3 T = normalize(input.tangentWS);
                // Sculpted hair locks unwrap V from Root (0) to Tip (1), so hair flow follows Bitangent
                float3 B = normalize(input.bitangentWS);

                float3 perturbedN = normalize(T * normalTS.x + B * normalTS.y + N * normalTS.z);
                if (_HeadCenterWS.w > 0.5 && _HeadCenterBlend > 0.001)
                {
                    float3 radialN = normalize(input.positionWS - _HeadCenterWS.xyz);
                    perturbedN = normalize(lerp(perturbedN, radialN, _HeadCenterBlend));
                }

                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Smooth 3-Stop Root -> Base -> Tip Gradient
                float3 gradColor = t < 0.24
                    ? lerp(_RootColor.rgb, _BaseColor.rgb, smoothstep(0.0, 0.24, t))
                    : lerp(_BaseColor.rgb, _TipColor.rgb, smoothstep(0.24, 1.0, t));

                float fiberTone = (flowId.r - 0.5) * 0.85 + (input.vertColor.b - 0.5) * 0.28;
                float3 albedo = saturate(gradColor * (1.0 + fiberTone * _StrandVariation));

                float rootOcclusion = lerp(1.0 - _RootAO * 0.55, 1.0, smoothstep(0.0, 0.16, t));
                float grooveCrevice = lerp(1.0, grooveAo, _GrooveDepth * 0.56);
                float layerAO = lerp(0.84, 1.0, saturate(input.vertColor.g));
                float totalAO = rootOcclusion * grooveCrevice * layerAO;

                // Main directional light + URP shadows
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = normalize(mainLight.direction);
                float3 lightCol = mainLight.color * mainLight.distanceAttenuation * lerp(0.35, 1.0, mainLight.shadowAttenuation);

                float ndl = saturate((dot(perturbedN, L) + 0.54) / 1.54);
                float3 ambientSH = SampleSH(perturbedN);
                float3 diffuse = albedo * (ambientSH * 0.64 + lightCol * ndl * 0.86) * totalAO;

                // Dual-Lobe Kajiya-Kay Anisotropic Specular
                float shiftNoise = (flowId.b - 0.5) * 0.34;
                float3 t1 = ShiftTangent(B, perturbedN, _PrimaryShift + shiftNoise);
                float3 t2 = ShiftTangent(B, perturbedN, _SecondaryShift + shiftNoise * 1.2);

                float fiberCrest = smoothstep(0.46, 0.94, grooveAo);
                float microGlint = 0.78 + 0.22 * smoothstep(0.30, 0.85, flowId.r);
                float specMask = smoothstep(0.06, 0.20, t) * (0.28 + 0.72 * fiberCrest) * microGlint * layerAO;
                float shadowGate = smoothstep(-0.12, 0.48, dot(N, L)) * lerp(0.28, 1.0, mainLight.shadowAttenuation);

                float spec1 = StrandSpecular(t1, V, L, _PrimaryPower) * _PrimaryIntensity;
                float spec2 = StrandSpecular(t2, V, L, _SecondaryPower) * _SecondaryIntensity;

                float3 spec1Tint = lerp(albedo * 1.45, _PrimarySpecColor.rgb, 0.46);
                float3 spec2Tint = lerp(albedo * 1.30, _SecondarySpecColor.rgb, 0.48);

                float3 specular = (
                    spec1Tint * spec1 * 0.54 +
                    spec2Tint * spec2 * 0.46
                ) * lightCol * specMask * shadowGate * rootOcclusion;

                // Rim Backlight Separation
                float fresnel = pow(1.0 - saturate(dot(perturbedN, V)), _RimPower);
                float backGate = pow(saturate(dot(V, -L)), 1.6) * 0.5 + 0.5;
                float tipWeight = lerp(0.25, 1.0, smoothstep(0.25, 0.95, t));
                float3 rimTint = lerp(albedo * 1.5, lightCol, 0.28);
                float3 rim = rimTint * fresnel * backGate * tipWeight * _RimIntensity * 0.42 * layerAO;

                float3 finalColor = diffuse + specular + rim;

            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                    float3 aL = normalize(addLight.direction);
                    float3 aCol = addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation;
                    float aNdl = saturate((dot(perturbedN, aL) + 0.45) / 1.45);
                    float aSpec = StrandSpecular(t1, V, aL, _PrimaryPower * 0.8) * _PrimaryIntensity * 0.42;
                    finalColor += (albedo * aNdl * totalAO + _PrimarySpecColor.rgb * aSpec * specMask) * aCol;
                LIGHT_LOOP_END
            #endif

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _USE_ALPHA_CLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 vertColor  : TEXCOORD1;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                ApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy, false);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _USE_ALPHA_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 vertColor  : TEXCOORD1;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                ApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy, false);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthNormVert
            #pragma fragment DepthNormFrag
            #pragma shader_feature_local _USE_ALPHA_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 vertColor  : TEXCOORD2;
            };

            Varyings DepthNormVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half4 DepthNormFrag(Varyings input, half facing : VFACE) : SV_Target
            {
                ApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy, false);
                float3 n = normalize(input.normalWS) * (facing >= 0.0h ? 1.0 : -1.0);
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
