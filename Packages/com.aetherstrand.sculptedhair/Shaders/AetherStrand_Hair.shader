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
        _DitherStrength ("Tip Interleaved-Noise Dither (Non-MSAA/TAA)", Range(0.0, 1.0)) = 0.0

        [Header(Spherical Head Normal Smoothing)]
        _HeadCenterWS ("Head Center World Position", Vector) = (0.0, 0.865, -0.01, 0.0)
        _HeadCenterBlend ("Head-Center Normal Blend", Range(0.0, 1.0)) = 0.15

        [Header(Root To Tip Color Gradient)]
        _RootColor ("Root Color", Color) = (0.035, 0.039, 0.047, 1.0)
        _BaseColor ("Mid / Base Color", Color) = (0.145, 0.153, 0.169, 1.0)
        _TipColor ("Tip Color", Color) = (0.298, 0.314, 0.341, 1.0)
        _StrandVariation ("Per-Strand Tonal Variation", Range(0.0, 0.4)) = 0.280
        _RootAO ("Root Ambient Occlusion", Range(0.0, 1.0)) = 0.740
        _GrooveDepth ("Carved Fiber Groove Depth", Range(0.1, 1.0)) = 0.780

        [Header(Kajiya Kay Dual Lobe Anisotropic Specular)]
        _PrimarySpecColor ("Primary Specular Color", Color) = (0.784, 0.800, 0.824, 1.0)
        _PrimaryShift ("Primary Tangent Shift", Range(-0.6, 0.6)) = 0.050
        _PrimaryPower ("Primary Specular Power", Range(16.0, 256.0)) = 64.0
        _PrimaryIntensity ("Primary Specular Intensity", Range(0.0, 3.0)) = 0.360

        _SecondarySpecColor ("Secondary Specular Color", Color) = (0.361, 0.376, 0.408, 1.0)
        _SecondaryShift ("Secondary Tangent Shift", Range(-0.6, 0.6)) = -0.140
        _SecondaryPower ("Secondary Specular Power", Range(8.0, 140.0)) = 24.0
        _SecondaryIntensity ("Secondary Specular Intensity", Range(0.0, 3.0)) = 0.220

        [Header(Marschner Rim And Backlight Scatter)]
        _RimIntensity ("Rim Backlight Intensity", Range(0.0, 3.0)) = 0.480
        _RimPower ("Rim Fresnel Exponent", Range(1.0, 6.0)) = 2.70
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

        // Single shared UnityPerMaterial CBUFFER across all passes (100% SRP-Batcher safe)
        CBUFFER_START(UnityPerMaterial)
            float4 _GrooveAoMap_ST;
            float4 _HeadCenterWS;
            float4 _RootColor;
            float4 _BaseColor;
            float4 _TipColor;
            float4 _PrimarySpecColor;
            float4 _SecondarySpecColor;
            float  _UseAlphaClip;
            float  _AlphaClip;
            float  _DitherStrength;
            float  _HeadCenterBlend;
            float  _StrandVariation;
            float  _RootAO;
            float  _GrooveDepth;
            float  _PrimaryShift;
            float  _PrimaryPower;
            float  _PrimaryIntensity;
            float  _SecondaryShift;
            float  _SecondaryPower;
            float  _SecondaryIntensity;
            float  _RimIntensity;
            float  _RimPower;
        CBUFFER_END

        float AetherInterleavedGradientNoise(float2 pixelPos)
        {
            float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
            return frac(magic.z * frac(dot(pixelPos, magic.xy)));
        }

        void AetherApplyTipAlphaClip(float2 uv, float4 vertColor, float2 screenPosXY)
        {
            #ifdef _USE_ALPHA_CLIP
            float t = saturate(uv.y);
            // Solid cohesive root->mid body; outer lock tips (vertColor.a > 0.5 && t > 0.70) fray into fine wisps
            if (vertColor.a > 0.5 && t > 0.70)
            {
                float2 strandUv = float2(uv.x * 0.65 + vertColor.b * 3.13, t);
                float cardAlpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, strandUv).r;
                float edgeDist = min(abs(uv.x - 0.34), abs(uv.x - 0.66));
                float lateralFray = smoothstep(0.0, 0.06, edgeDist);
                float tipRamp = smoothstep(0.72, 0.98, t);
                float combinedAlpha = cardAlpha * lerp(1.0, 0.52 + 0.48 * lateralFray, tipRamp);
                float cutoff = _AlphaClip;
                if (_DitherStrength > 0.001)
                {
                    float ign = AetherInterleavedGradientNoise(screenPosXY);
                    cutoff = lerp(_AlphaClip, _AlphaClip + (ign - 0.5) * 0.45, _DitherStrength);
                }
                clip(combinedAlpha - cutoff);
            }
            #endif
        }

        float3 AetherApplyHeadCenterNormalBlend(float3 normalWS, float3 positionWS)
        {
            float3 N = normalize(normalWS);
            if (_HeadCenterBlend > 0.001)
            {
                float3 sphereDir = normalize(positionWS - _HeadCenterWS.xyz + float3(0.0, 0.0001, 0.0));
                N = normalize(lerp(N, sphereDir, saturate(_HeadCenterBlend)));
            }
            return N;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR; // R=RootToTipV, G=LayerAO, B=StrandSeed, A=TipAlphaClipMask
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

            float3 AS_ShiftTangent(float3 T, float3 N, float shift)
            {
                return normalize(T + N * shift);
            }

            float AS_StrandSpecular(float3 T, float3 V, float3 L, float exponent)
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

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                AetherApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy);

                float t = saturate(input.uv.y);
                float2 strandUv = float2(input.uv.x * 0.65 + input.vertColor.b * 3.13, t);

                float grooveAo = SAMPLE_TEXTURE2D(_GrooveAoMap, sampler_GrooveAoMap, strandUv).r;
                float3 flowId  = SAMPLE_TEXTURE2D(_FlowIdMap, sampler_FlowIdMap, strandUv).rgb;
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, strandUv), _GrooveDepth * 0.34);

                float faceSign = isFrontFace ? 1.0 : -1.0;
                float3 N = AetherApplyHeadCenterNormalBlend(input.normalWS * faceSign, input.positionWS);
                float3 T = normalize(input.tangentWS);
                // Sculpted hair locks unwrap V from Root (0) to Tip (1), so fiber flow follows Bitangent
                float3 B = normalize(input.bitangentWS);

                float3 perturbedN = normalize(T * normalTS.x + B * normalTS.y + N * normalTS.z);
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

                // Velvety Dual-Lobe Kajiya-Kay Anisotropic Specular
                float shiftNoise = (flowId.b - 0.5) * 0.34;
                float3 t1 = AS_ShiftTangent(B, perturbedN, _PrimaryShift + shiftNoise);
                float3 t2 = AS_ShiftTangent(B, perturbedN, _SecondaryShift + shiftNoise * 1.2);

                float fiberCrest = smoothstep(0.46, 0.94, grooveAo);
                float microGlint = 0.78 + 0.22 * smoothstep(0.30, 0.85, flowId.r);
                float specMask = smoothstep(0.06, 0.20, t) * (0.28 + 0.72 * fiberCrest) * microGlint * layerAO;
                float shadowGate = smoothstep(-0.12, 0.48, dot(N, L)) * lerp(0.28, 1.0, mainLight.shadowAttenuation);

                float spec1 = AS_StrandSpecular(t1, V, L, _PrimaryPower) * _PrimaryIntensity;
                float spec2 = AS_StrandSpecular(t2, V, L, _SecondaryPower) * _SecondaryIntensity;

                float3 spec1Tint = lerp(albedo * 1.45, _PrimarySpecColor.rgb, 0.46);
                float3 spec2Tint = lerp(albedo * 1.30, _SecondarySpecColor.rgb, 0.48);

                float3 specular = (
                    spec1Tint * spec1 * 0.54 +
                    spec2Tint * spec2 * 0.46
                ) * lightCol * specMask * shadowGate * rootOcclusion;

                // Marschner-style Rim Backlight Separation
                float fresnel = pow(1.0 - saturate(dot(perturbedN, V)), _RimPower);
                float backGate = pow(saturate(dot(V, -L)), 1.6) * 0.5 + 0.5;
                float tipWeight = lerp(0.25, 1.0, smoothstep(0.25, 0.95, t));
                float3 rimTint = lerp(albedo * 1.5, lightCol, 0.28);
                float3 rim = rimTint * fresnel * backGate * tipWeight * _RimIntensity * 0.42 * layerAO;

                float3 finalColor = diffuse + specular + rim;

                // URP 17.6 Forward+ (_CLUSTER_LIGHT_LOOP) & Forward Additional Lights Loop
                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = perturbedN;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #ifdef _LIGHT_LAYERS
                uint meshRenderingLayers = GetMeshRenderingLayer();
                #endif

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(addLight.layerMask, meshRenderingLayers))
                    #endif
                    {
                        float3 aL = normalize(addLight.direction);
                        float3 aCol = addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation;
                        float aNdl = saturate((dot(perturbedN, aL) + 0.45) / 1.45);
                        float aSpec = AS_StrandSpecular(t1, V, aL, _PrimaryPower * 0.8) * _PrimaryIntensity * 0.42;
                        finalColor += (albedo * aNdl * totalAO + _PrimarySpecColor.rgb * aSpec * specMask) * aCol;
                    }
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
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

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
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                AetherApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy);
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
            #pragma target 4.5
            #pragma vertex DepthOnlyVert
            #pragma fragment DepthOnlyFrag
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

            Varyings DepthOnlyVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half DepthOnlyFrag(Varyings input) : SV_TARGET
            {
                AetherApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy);
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
            #pragma target 4.5
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
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 vertColor  : TEXCOORD3;
            };

            Varyings DepthNormVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _GrooveAoMap);
                output.vertColor = input.color;
                return output;
            }

            half4 DepthNormFrag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                AetherApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy);
                float faceSign = isFrontFace ? 1.0 : -1.0;
                float3 N = AetherApplyHeadCenterNormalBlend(input.normalWS * faceSign, input.positionWS);
                return half4(NormalizeNormalPerPixel(N), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
