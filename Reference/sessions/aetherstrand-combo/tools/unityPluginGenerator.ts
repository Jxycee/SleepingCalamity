import {
  ExportedHairLock,
  HairPresetId,
  PhysicsConfig,
  PluginFileEntry,
  RenderPipeline,
  ShaderConfig,
} from '../types/hair';

export interface ExportPresetMetadata {
  id: string;
  name: string;
  subtitle: string;
}

const EXPORT_PRESET_MAP: Record<HairPresetId, ExportPresetMetadata> = {
  leon: {
    id: 'parted_shag_medium',
    name: 'Parted Shag Medium',
    subtitle: 'Medium side-parted shingled layers',
  },
  soldier: {
    id: 'spiky_crown_medium',
    name: 'Spiky Crown Medium',
    subtitle: 'Sculpted radial crown spikes with feathered nape',
  },
  silver: {
    id: 'parted_cascade_long',
    name: 'Parted Cascade Long',
    subtitle: 'Long flowing center-parted curtain layers',
  },
  noctis: {
    id: 'layered_swept_long',
    name: 'Layered Swept Long',
    subtitle: 'Asymmetric swept fringe with layered crown volume',
  },
  crimson: {
    id: 'wolf_cut_layered',
    name: 'Wolf Cut Layered',
    subtitle: 'Feathered cheek-framing layers with tapered tail',
  },
};

export function getExportPresetMetadata(presetId: HairPresetId): ExportPresetMetadata {
  return EXPORT_PRESET_MAP[presetId] ?? EXPORT_PRESET_MAP.leon;
}

export function sanitizeExportPresetId(presetId: HairPresetId): string {
  return getExportPresetMetadata(presetId).id;
}

export function sanitizeExportLightingPreset(lightingPreset: string): string {
  if (lightingPreset === 'midgar') return 'cool_dramatic';
  return lightingPreset;
}

function hexToRgbaTuple(hex: string): string {
  const clean = hex.replace('#', '').trim();
  const full =
    clean.length === 3
      ? clean
          .split('')
          .map((c) => c + c)
          .join('')
      : clean.padEnd(6, '0').slice(0, 6);
  const r = (parseInt(full.slice(0, 2), 16) / 255).toFixed(3);
  const g = (parseInt(full.slice(2, 4), 16) / 255).toFixed(3);
  const b = (parseInt(full.slice(4, 6), 16) / 255).toFixed(3);
  return `(${r}, ${g}, ${b}, 1.0)`;
}

function hexToCSharpColor(hex: string): string {
  const clean = hex.replace('#', '').trim();
  const full =
    clean.length === 3
      ? clean
          .split('')
          .map((c) => c + c)
          .join('')
      : clean.padEnd(6, '0').slice(0, 6);
  const r = (parseInt(full.slice(0, 2), 16) / 255).toFixed(3);
  const g = (parseInt(full.slice(2, 4), 16) / 255).toFixed(3);
  const b = (parseInt(full.slice(4, 6), 16) / 255).toFixed(3);
  return `new Color(${r}f, ${g}f, ${b}f, 1f)`;
}

function generateUrpHairShader(shader: ShaderConfig): string {
  return `Shader "AetherStrand/SculptedHair_URP"
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
        _RootColor ("Root Color", Color) = ${hexToRgbaTuple(shader.rootColor)}
        _BaseColor ("Mid / Base Color", Color) = ${hexToRgbaTuple(shader.baseColor)}
        _TipColor ("Tip Color", Color) = ${hexToRgbaTuple(shader.tipColor)}
        _StrandVariation ("Per-Strand Tonal Variation", Range(0.0, 0.4)) = ${shader.strandVariation.toFixed(3)}
        _RootAO ("Root Ambient Occlusion", Range(0.0, 1.0)) = ${shader.rootAO.toFixed(3)}
        _GrooveDepth ("Carved Fiber Groove Depth", Range(0.1, 1.0)) = ${shader.grooveDepth.toFixed(3)}

        [Header(Kajiya Kay Dual Lobe Anisotropic Specular)]
        _PrimarySpecColor ("Primary Specular Color", Color) = ${hexToRgbaTuple(shader.primarySpecColor)}
        _PrimaryShift ("Primary Tangent Shift", Range(-0.6, 0.6)) = ${shader.primaryShift.toFixed(3)}
        _PrimaryPower ("Primary Specular Power", Range(16.0, 256.0)) = ${shader.primaryPower.toFixed(1)}
        _PrimaryIntensity ("Primary Specular Intensity", Range(0.0, 3.0)) = ${shader.primaryIntensity.toFixed(3)}

        _SecondarySpecColor ("Secondary Specular Color", Color) = ${hexToRgbaTuple(shader.secondarySpecColor)}
        _SecondaryShift ("Secondary Tangent Shift", Range(-0.6, 0.6)) = ${shader.secondaryShift.toFixed(3)}
        _SecondaryPower ("Secondary Specular Power", Range(8.0, 140.0)) = ${shader.secondaryPower.toFixed(1)}
        _SecondaryIntensity ("Secondary Specular Intensity", Range(0.0, 3.0)) = ${shader.secondaryIntensity.toFixed(3)}

        [Header(Marschner Rim And Backlight Scatter)]
        _RimIntensity ("Rim Backlight Intensity", Range(0.0, 3.0)) = ${shader.rimIntensity.toFixed(3)}
        _RimPower ("Rim Fresnel Exponent", Range(1.0, 6.0)) = ${shader.rimPower.toFixed(2)}
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
`;
}

function generateBuiltinHairShader(shader: ShaderConfig): string {
  return `Shader "AetherStrand/SculptedHair_BuiltIn"
{
    Properties
    {
        [Header(Textures)]
        _GrooveAoMap ("Carved Strand Groove AO (R)", 2D) = "white" {}
        _FlowIdMap ("Flow / ID / Shift Map (R=ID, G=Grad, B=Shift)", 2D) = "gray" {}
        [Normal] _NormalMap ("Strand Tangent Normal", 2D) = "bump" {}
        _AlphaMap ("Feathered Wispy-Tip Alpha Mask (R)", 2D) = "white" {}
        [Toggle(_USE_ALPHA_CLIP)] _UseAlphaClip ("Enable Feathered Wispy-Tip Clipping", Float) = 1
        _AlphaClip ("Alpha Cutoff", Range(0.05, 0.85)) = 0.34
        _DitherStrength ("Tip Interleaved-Noise Dither", Range(0.0, 1.0)) = 0.0

        [Header(Spherical Head Normal Smoothing)]
        _HeadCenterWS ("Head Center World Position", Vector) = (0.0, 0.865, -0.01, 0.0)
        _HeadCenterBlend ("Head-Center Normal Blend", Range(0.0, 1.0)) = 0.15

        [Header(Root To Tip Color Gradient)]
        _RootColor ("Root Color", Color) = ${hexToRgbaTuple(shader.rootColor)}
        _BaseColor ("Mid / Base Color", Color) = ${hexToRgbaTuple(shader.baseColor)}
        _TipColor ("Tip Color", Color) = ${hexToRgbaTuple(shader.tipColor)}
        _StrandVariation ("Per-Strand Tonal Variation", Range(0.0, 0.4)) = ${shader.strandVariation.toFixed(3)}
        _RootAO ("Root Ambient Occlusion", Range(0.0, 1.0)) = ${shader.rootAO.toFixed(3)}
        _GrooveDepth ("Carved Fiber Groove Depth", Range(0.1, 1.0)) = ${shader.grooveDepth.toFixed(3)}

        [Header(Kajiya Kay Dual Lobe Anisotropic Specular)]
        _PrimarySpecColor ("Primary Specular Color", Color) = ${hexToRgbaTuple(shader.primarySpecColor)}
        _PrimaryShift ("Primary Tangent Shift", Range(-0.6, 0.6)) = ${shader.primaryShift.toFixed(3)}
        _PrimaryPower ("Primary Specular Power", Range(16.0, 256.0)) = ${shader.primaryPower.toFixed(1)}
        _PrimaryIntensity ("Primary Specular Intensity", Range(0.0, 3.0)) = ${shader.primaryIntensity.toFixed(3)}

        _SecondarySpecColor ("Secondary Specular Color", Color) = ${hexToRgbaTuple(shader.secondarySpecColor)}
        _SecondaryShift ("Secondary Tangent Shift", Range(-0.6, 0.6)) = ${shader.secondaryShift.toFixed(3)}
        _SecondaryPower ("Secondary Specular Power", Range(8.0, 140.0)) = ${shader.secondaryPower.toFixed(1)}
        _SecondaryIntensity ("Secondary Specular Intensity", Range(0.0, 3.0)) = ${shader.secondaryIntensity.toFixed(3)}

        [Header(Rim And Backlight Scatter)]
        _RimIntensity ("Rim Backlight Intensity", Range(0.0, 3.0)) = ${shader.rimIntensity.toFixed(3)}
        _RimPower ("Rim Fresnel Exponent", Range(1.0, 6.0)) = ${shader.rimPower.toFixed(2)}
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _GrooveAoMap;
            float4 _GrooveAoMap_ST;
            sampler2D _FlowIdMap;
            sampler2D _NormalMap;
            sampler2D _AlphaMap;

            float4 _HeadCenterWS;
            float4 _RootColor, _BaseColor, _TipColor;
            float4 _PrimarySpecColor, _SecondarySpecColor;
            float _StrandVariation, _RootAO, _GrooveDepth, _AlphaClip, _DitherStrength, _HeadCenterBlend;
            float _PrimaryShift, _PrimaryPower, _PrimaryIntensity;
            float _SecondaryShift, _SecondaryPower, _SecondaryIntensity;
            float _RimIntensity, _RimPower;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
                float4 color   : COLOR;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldPos  : TEXCOORD1;
                float3 worldNorm : TEXCOORD2;
                float3 worldTang : TEXCOORD3;
                float3 worldBit  : TEXCOORD4;
                float4 color     : TEXCOORD5;
                SHADOW_COORDS(6)
                UNITY_FOG_COORDS(7)
            };

            float InterleavedGradientNoise(float2 pixelPos)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixelPos, magic.xy)));
            }

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

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _GrooveAoMap);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNorm = UnityObjectToWorldNormal(v.normal);
                o.worldTang = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBit = cross(o.worldNorm, o.worldTang) * v.tangent.w;
                o.color = v.color;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = saturate(i.uv.y);
                float2 strandUv = float2(i.uv.x * 0.65 + i.color.b * 3.13, t);

                #ifdef _USE_ALPHA_CLIP
                if (i.color.a > 0.5 && t > 0.70)
                {
                    float cardAlpha = tex2D(_AlphaMap, strandUv).r;
                    float edgeDist = min(abs(i.uv.x - 0.34), abs(i.uv.x - 0.66));
                    float lateralFray = smoothstep(0.0, 0.06, edgeDist);
                    float tipRamp = smoothstep(0.72, 0.98, t);
                    float combinedAlpha = cardAlpha * lerp(1.0, 0.52 + 0.48 * lateralFray, tipRamp);
                    float cutoff = _AlphaClip;
                    if (_DitherStrength > 0.001)
                    {
                        float ign = InterleavedGradientNoise(i.pos.xy);
                        cutoff = lerp(_AlphaClip, _AlphaClip + (ign - 0.5) * 0.45, _DitherStrength);
                    }
                    clip(combinedAlpha - cutoff);
                }
                #endif

                float grooveAo = tex2D(_GrooveAoMap, strandUv).r;
                float3 flowId = tex2D(_FlowIdMap, strandUv).rgb;
                float3 normalTS = UnpackNormalWithScale(tex2D(_NormalMap, strandUv), _GrooveDepth * 0.34);

                float3 N = normalize(i.worldNorm);
                if (_HeadCenterBlend > 0.001)
                {
                    float3 sphereDir = normalize(i.worldPos - _HeadCenterWS.xyz + float3(0.0, 0.0001, 0.0));
                    N = normalize(lerp(N, sphereDir, saturate(_HeadCenterBlend)));
                }
                float3 T = normalize(i.worldTang);
                float3 B = normalize(i.worldBit);

                float3 perturbedN = normalize(T * normalTS.x + B * normalTS.y + N * normalTS.z);
                float3 V = normalize(UnityWorldSpaceViewDir(i.worldPos));
                float3 L = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float shadow = SHADOW_ATTENUATION(i);

                float3 gradColor = t < 0.24
                    ? lerp(_RootColor.rgb, _BaseColor.rgb, smoothstep(0.0, 0.24, t))
                    : lerp(_BaseColor.rgb, _TipColor.rgb, smoothstep(0.24, 1.0, t));

                float fiberTone = (flowId.r - 0.5) * 0.85 + (i.color.b - 0.5) * 0.28;
                float3 albedo = saturate(gradColor * (1.0 + fiberTone * _StrandVariation));

                float rootOcclusion = lerp(1.0 - _RootAO * 0.55, 1.0, smoothstep(0.0, 0.16, t));
                float grooveCrevice = lerp(1.0, grooveAo, _GrooveDepth * 0.56);
                float layerAO = lerp(0.84, 1.0, saturate(i.color.g));
                float totalAO = rootOcclusion * grooveCrevice * layerAO;

                float3 lightCol = _LightColor0.rgb * lerp(0.35, 1.0, shadow);
                float ndl = saturate((dot(perturbedN, L) + 0.54) / 1.54);
                float3 ambient = ShadeSH9(half4(perturbedN, 1.0));
                float3 diffuse = albedo * (ambient * 0.64 + lightCol * ndl * 0.86) * totalAO;

                float shiftNoise = (flowId.b - 0.5) * 0.34;
                float3 t1 = AS_ShiftTangent(B, perturbedN, _PrimaryShift + shiftNoise);
                float3 t2 = AS_ShiftTangent(B, perturbedN, _SecondaryShift + shiftNoise * 1.2);

                float fiberCrest = smoothstep(0.46, 0.94, grooveAo);
                float microGlint = 0.78 + 0.22 * smoothstep(0.30, 0.85, flowId.r);
                float specMask = smoothstep(0.06, 0.20, t) * (0.28 + 0.72 * fiberCrest) * microGlint * layerAO;
                float shadowGate = smoothstep(-0.12, 0.48, dot(N, L)) * lerp(0.28, 1.0, shadow);
                float spec1 = AS_StrandSpecular(t1, V, L, _PrimaryPower) * _PrimaryIntensity;
                float spec2 = AS_StrandSpecular(t2, V, L, _SecondaryPower) * _SecondaryIntensity;

                float3 spec1Tint = lerp(albedo * 1.45, _PrimarySpecColor.rgb, 0.46);
                float3 spec2Tint = lerp(albedo * 1.30, _SecondarySpecColor.rgb, 0.48);

                float3 specular = (
                    spec1Tint * spec1 * 0.54 +
                    spec2Tint * spec2 * 0.46
                ) * lightCol * specMask * shadowGate * rootOcclusion;

                float fresnel = pow(1.0 - saturate(dot(perturbedN, V)), _RimPower);
                float backGate = pow(saturate(dot(V, -L)), 1.6) * 0.5 + 0.5;
                float tipWeight = lerp(0.25, 1.0, smoothstep(0.25, 0.95, t));
                float3 rim = lerp(albedo * 1.5, lightCol, 0.28) * fresnel * backGate * tipWeight * _RimIntensity * 0.42 * layerAO;

                float3 col = diffuse + specular + rim;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
`;
}

function generatePhysicsCs(physics: PhysicsConfig): string {
  return `using System;
using System.Collections.Generic;
using UnityEngine;

namespace AetherStrand
{
    public enum HairZone
    {
        Bangs,
        Crown,
        Sides,
        Rear
    }

    [Serializable]
    public struct PhysicsAuditResult
    {
        public int totalChains;
        public int colliderCount;
        public int penetratingNodes;
        public float maxKinkAngleDeg;
        public float meanTipSwayCm;
        public float maxStretchErrorPct;
    }

    /// <summary>
    /// Real-time XPBD / Verlet Hair Bone Chain Simulator for sculpted volumetric hair.
    /// Features substepped head-pose interpolation, framerate-independent exponential damping & root pinning,
    /// XPBD rest-pose compliance tethers, per-zone stiffness multipliers, and deterministic verification audit hooks.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(120)]
    public class AetherStrandPhysics : MonoBehaviour
    {
        [Header("Rig References")]
        [Tooltip("Character Head bone transform used for inertia and rest-pose orientation.")]
        public Transform headTransform;

        [Tooltip("Root bone of each hair lock chain (auto-traverses children).")]
        public List<Transform> hairRootBones = new List<Transform>();

        [Tooltip("Head, face/jaw, neck, chest, and shoulder analytical colliders.")]
        public List<AetherStrandCollider> colliders = new List<AetherStrandCollider>();

        [Tooltip("Optional global wind controller.")]
        public AetherStrandWindZone windZone;

        [Tooltip("Optional hair renderer whose _HeadCenterWS shader property is synced each step.")]
        public Renderer hairRenderer;

        [Header("Silhouette & XPBD Physics")]
        [Range(0.05f, 0.95f)]
        [Tooltip("Rest-pose shape retention. High (0.60-0.85) holds sculpted layered/spiky silhouettes; low (0.25-0.45) flows freely.")]
        public float stiffness = ${physics.stiffness.toFixed(2)}f;

        [Tooltip("Downward gravity acceleration in m/s^2.")]
        public float gravity = ${physics.gravity.toFixed(2)}f;

        [Range(0.01f, 0.35f)]
        [Tooltip("Framerate-independent velocity damping calibrated at 60 Hz.")]
        public float damping = ${physics.damping.toFixed(3)}f;

        [Range(0.0f, 1.5f)]
        [Tooltip("Lag/drag from character head movement.")]
        public float headInertia = ${physics.inertia.toFixed(2)}f;

        [Range(0.0f, 0.95f)]
        [Tooltip("Rest-curvature-preserving bending smoothness. Eliminates accordion kinks across colliding bangs.")]
        public float bendingSmoothness = ${physics.bendingSmoothness.toFixed(2)}f;

        [Range(0.002f, 0.025f)]
        [Tooltip("Minimum surface clearance outside head/neck/chest/shoulder colliders (meters).")]
        public float collisionMargin = ${(physics.collisionMarginMm * 0.001).toFixed(4)}f;

        [Range(0.1f, 1.0f)]
        [Tooltip("Upper scalp parting anchor strength across root bones 1..3.")]
        public float rootAnchor = ${physics.rootAnchor.toFixed(2)}f;

        [Range(1, 6)]
        [Tooltip("Integration substeps per frame for fast head turns and smooth collider sliding.")]
        public int solverSubsteps = 3;

        [Range(1, 6)]
        public int solverIterations = 4;

        [Tooltip("Virtual tip extension length beyond the final leaf bone (meters).")]
        public float leafExtensionLength = 0.06f;

        [Header("Per-Zone Stiffness Multipliers")]
        [Range(0.25f, 2.0f)] public float bangsStiffnessMultiplier = 1.15f;
        [Range(0.25f, 2.0f)] public float crownStiffnessMultiplier = 1.20f;
        [Range(0.25f, 2.0f)] public float sidesStiffnessMultiplier = 0.95f;
        [Range(0.25f, 2.0f)] public float rearStiffnessMultiplier = 0.85f;

        [Header("Fallback Local Wind")]
        [Tooltip("Default 0 so hair stays still indoors/in caves when no AetherStrandWindZone is active.")]
        public float fallbackWindStrength = 0f;
        public float fallbackTurbulence = ${physics.windTurbulence.toFixed(2)}f;
        public float fallbackWindAzimuthDeg = ${physics.windAzimuth.toFixed(1)}f;

        [Header("Debug Visualization")]
        public bool drawGizmos = true;

        private class HairNode
        {
            public Transform bone;
            public Vector3 worldPos;
            public Vector3 prevWorldPos;
            public Vector3 restLocalPos;
            public Vector3 restCurvatureLocal;
            public Quaternion restLocalRot;
            public float segmentLength;
            public float tipFraction;
            public float stiffnessWeight;
            public bool isVirtualLeaf;
        }

        private class HairChain
        {
            public readonly List<HairNode> nodes = new List<HairNode>();
            public HairZone zone;
            public float seedPhase;
        }

        private readonly List<HairChain> _chains = new List<HairChain>();
        private Vector3 _prevHeadPos;
        private Quaternion _prevHeadRot = Quaternion.identity;
        private Vector3 _impulseVelocity;
        private MaterialPropertyBlock _mpb;
        private bool _initialized;

        private static readonly int HeadCenterWsId = Shader.PropertyToID("_HeadCenterWS");

        private void Awake()
        {
            if (headTransform == null) headTransform = transform;
            RebuildChains();
        }

        private static HairZone DetectZone(Transform root, Vector3 restLocalPos, Vector3 tipLocalPos)
        {
            if (root != null)
            {
                string n = root.name;
                if (n.IndexOf("Bangs", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Bangs;
                if (n.IndexOf("Crown", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Crown;
                if (n.IndexOf("Sides", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Sides;
                if (n.IndexOf("Rear", StringComparison.OrdinalIgnoreCase) >= 0) return HairZone.Rear;
            }

            if (tipLocalPos.z > 0.08f && Mathf.Abs(tipLocalPos.x) < 0.20f) return HairZone.Bangs;
            if (restLocalPos.y > 0.16f && tipLocalPos.z > -0.10f) return HairZone.Crown;
            if (restLocalPos.z < -0.04f || tipLocalPos.z < -0.08f) return HairZone.Rear;
            return HairZone.Sides;
        }

        private float GetZoneStiffnessMultiplier(HairZone zone)
        {
            switch (zone)
            {
                case HairZone.Bangs: return bangsStiffnessMultiplier;
                case HairZone.Crown: return crownStiffnessMultiplier;
                case HairZone.Sides: return sidesStiffnessMultiplier;
                case HairZone.Rear:  return rearStiffnessMultiplier;
                default:             return 1.0f;
            }
        }

        [ContextMenu("Rebuild Hair Chains")]
        public void RebuildChains()
        {
            if (headTransform == null) headTransform = transform;
            _chains.Clear();
            int chainIndex = 0;

            foreach (Transform root in hairRootBones)
            {
                if (root == null) continue;
                var chain = new HairChain { seedPhase = (chainIndex++ * 1.618033f) * 12.5f };

                Transform current = root;
                var linearBones = new List<Transform>();
                while (current != null)
                {
                    linearBones.Add(current);
                    current = current.childCount > 0 ? current.GetChild(0) : null;
                }

                if (linearBones.Count < 1) continue;

                int totalNodes = linearBones.Count + 1;
                for (int i = 0; i < linearBones.Count; i++)
                {
                    Transform b = linearBones[i];
                    float segLen = i == 0 ? 0f : Vector3.Distance(b.position, linearBones[i - 1].position);
                    float tipFrac = (float)i / (totalNodes - 1);
                    chain.nodes.Add(new HairNode
                    {
                        bone = b,
                        worldPos = b.position,
                        prevWorldPos = b.position,
                        restLocalPos = headTransform.InverseTransformPoint(b.position),
                        restLocalRot = b.localRotation,
                        segmentLength = segLen,
                        tipFraction = tipFrac,
                        stiffnessWeight = Mathf.Clamp(0.18f + 0.82f * Mathf.Pow(1f - tipFrac, 1.35f), 0.12f, 1f),
                        isVirtualLeaf = false
                    });
                }

                Transform lastBone = linearBones[linearBones.Count - 1];
                Vector3 tipDir = linearBones.Count >= 2
                    ? (lastBone.position - linearBones[linearBones.Count - 2].position).normalized
                    : -lastBone.up;
                Vector3 virtualTipWorld = lastBone.position + tipDir * leafExtensionLength;

                chain.nodes.Add(new HairNode
                {
                    bone = null,
                    worldPos = virtualTipWorld,
                    prevWorldPos = virtualTipWorld,
                    restLocalPos = headTransform.InverseTransformPoint(virtualTipWorld),
                    restLocalRot = Quaternion.identity,
                    segmentLength = leafExtensionLength,
                    tipFraction = 1f,
                    stiffnessWeight = 0.12f,
                    isVirtualLeaf = true
                });

                for (int i = 1; i < chain.nodes.Count - 1; i++)
                {
                    Vector3 midLocal = 0.5f * (chain.nodes[i - 1].restLocalPos + chain.nodes[i + 1].restLocalPos);
                    chain.nodes[i].restCurvatureLocal = chain.nodes[i].restLocalPos - midLocal;
                }

                chain.zone = DetectZone(root, chain.nodes[0].restLocalPos, chain.nodes[chain.nodes.Count - 1].restLocalPos);
                _chains.Add(chain);
            }

            _prevHeadPos = headTransform.position;
            _prevHeadRot = headTransform.rotation;
            _initialized = true;
        }

        [ContextMenu("Reset To Rest Pose")]
        public void ResetToRestPose()
        {
            if (headTransform == null) headTransform = transform;
            if (!_initialized) RebuildChains();

            _impulseVelocity = Vector3.zero;
            _prevHeadPos = headTransform.position;
            _prevHeadRot = headTransform.rotation;

            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    HairNode n = nodes[i];
                    if (!n.isVirtualLeaf && n.bone != null)
                        n.bone.localRotation = n.restLocalRot;
                    Vector3 restW = headTransform.TransformPoint(n.restLocalPos);
                    n.worldPos = restW;
                    n.prevWorldPos = restW;
                }
            }
        }

        public void AddImpulse(Vector3 worldImpulse) => _impulseVelocity += worldImpulse;

        private void LateUpdate()
        {
            Simulate(Time.deltaTime, Time.time);
        }

        /// <summary>
        /// Public deterministic simulation step callable from LateUpdate or automated verification suites.
        /// </summary>
        public void Simulate(float dt, float simTime)
        {
            if (!_initialized || _chains.Count == 0 || headTransform == null) return;

            float clampedDt = Mathf.Min(dt, 0.033f);
            if (clampedDt <= 0.00001f) return;

            int substeps = Mathf.Clamp(solverSubsteps, 1, 6);
            float subDt = clampedDt / substeps;
            float subDtSq = subDt * subDt;

            // Framerate-independent exponential damping rate calibrated at 60 Hz
            float clampedDamp = Mathf.Clamp(damping, 0.01f, 0.35f);
            float dampingRate = -Mathf.Log(1f - clampedDamp) * 60f;
            float dampMul = Mathf.Exp(-dampingRate * subDt);
            float impulseDecay = Mathf.Exp(-4.5f * subDt);

            Vector3 targetHeadPos = headTransform.position;
            Quaternion targetHeadRot = headTransform.rotation;

            float azRad = fallbackWindAzimuthDeg * Mathf.Deg2Rad;
            Vector3 fallbackDir = new Vector3(Mathf.Cos(azRad), 0f, Mathf.Sin(azRad));
            float bendAlpha = Mathf.Clamp01(bendingSmoothness) * 0.30f;

            // Restore rest bone rotations once so bone.position yields exact rest-pose directions
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                for (int i = 0; i < nodes.Count; i++)
                    if (!nodes[i].isVirtualLeaf && nodes[i].bone != null)
                        nodes[i].bone.localRotation = nodes[i].restLocalRot;
            }

            Vector3 headScale = headTransform.lossyScale;

            for (int sub = 0; sub < substeps; sub++)
            {
                float alpha0 = (float)sub / substeps;
                float alpha1 = (float)(sub + 1) / substeps;
                float subTime = simTime - clampedDt + subDt * (sub + 1);

                Vector3 subPrevPos = Vector3.Lerp(_prevHeadPos, targetHeadPos, alpha0);
                Vector3 subHeadPos = Vector3.Lerp(_prevHeadPos, targetHeadPos, alpha1);
                Quaternion subPrevRot = Quaternion.Slerp(_prevHeadRot, targetHeadRot, alpha0);
                Quaternion subHeadRot = Quaternion.Slerp(_prevHeadRot, targetHeadRot, alpha1);

                Matrix4x4 subPrevMat = Matrix4x4.TRS(subPrevPos, subPrevRot, headScale);
                Matrix4x4 subCurrMat = Matrix4x4.TRS(subHeadPos, subHeadRot, headScale);

                _impulseVelocity *= impulseDecay;

                for (int c = 0; c < _chains.Count; c++)
                {
                    HairChain chain = _chains[c];
                    var nodes = chain.nodes;
                    int count = nodes.Count;
                    float zoneStiff = Mathf.Clamp(stiffness * GetZoneStiffnessMultiplier(chain.zone), 0.04f, 0.96f);

                    // Root node pinned strictly to head scalp
                    Vector3 rootWorld = subCurrMat.MultiplyPoint3x4(nodes[0].restLocalPos);
                    nodes[0].worldPos = rootWorld;
                    nodes[0].prevWorldPos = rootWorld;

                    // 1. Substepped Verlet Integration with Rigid-Follow Inertia & Wind
                    for (int i = 1; i < count; i++)
                    {
                        HairNode node = nodes[i];
                        Vector3 prevTargetW = subPrevMat.MultiplyPoint3x4(node.restLocalPos);
                        Vector3 currTargetW = subCurrMat.MultiplyPoint3x4(node.restLocalPos);
                        Vector3 targetDelta = currTargetW - prevTargetW;

                        Vector3 velocity = (node.worldPos - node.prevWorldPos) * dampMul;
                        float followRigid = Mathf.Clamp(1f - headInertia * 0.56f * Mathf.Pow(node.tipFraction, 1.35f), 0.22f, 1f);
                        Vector3 shiftedPos = node.worldPos + targetDelta * followRigid;
                        node.prevWorldPos = shiftedPos;

                        float tipSq = node.tipFraction * node.tipFraction;
                        float tipCube = tipSq * node.tipFraction;
                        velocity -= targetDelta * (0.20f * headInertia * tipSq);

                        Vector3 windAccel;
                        if (windZone != null)
                        {
                            windAccel = windZone.SampleWindAcceleration(shiftedPos, chain.seedPhase, subTime)
                                        * (0.14f + 0.86f * tipSq);
                        }
                        else
                        {
                            float gust = 0.65f + 0.35f * Mathf.Sin(subTime * 2.3f + chain.seedPhase + i * 0.42f)
                                       + fallbackTurbulence * 0.45f * Mathf.Sin(subTime * 5.4f + chain.seedPhase * 1.9f);
                            windAccel = fallbackDir * (fallbackWindStrength * gust * (0.14f + 0.86f * tipSq));
                        }

                        Vector3 extAccel = windAccel * 0.62f
                                           + Vector3.down * (gravity * 0.12f * tipCube)
                                           + _impulseVelocity * (1.35f * (0.1f + 0.9f * tipSq));

                        node.worldPos = shiftedPos + velocity + extAccel * subDtSq;

                        // Framerate-independent exponential root anchor pinning
                        if (i <= 3)
                        {
                            float kFade = i == 1 ? 0.42f : (i == 2 ? 0.22f : 0.09f);
                            float rootPinRate = kFade * zoneStiff * (rootAnchor / 0.6f) * 60f;
                            float rootPin = Mathf.Clamp(1f - Mathf.Exp(-rootPinRate * subDt), 0f, 0.78f);
                            node.worldPos = Vector3.Lerp(node.worldPos, currTargetW, rootPin);
                        }
                    }

                    // 2. XPBD Rest-Pose Compliance Tether + Rest-Curvature Anti-Kink + Distance & Collider Projection
                    int iters = Mathf.Clamp(solverIterations, 1, 6);
                    for (int iter = 0; iter < iters; iter++)
                    {
                        bool isFinalIter = iter == iters - 1;

                        // Rest-curvature-preserving anti-kink smoothing
                        if (bendAlpha > 0.005f || !isFinalIter)
                        {
                            for (int i = 1; i < count - 1; i++)
                            {
                                Vector3 a = nodes[i].worldPos - nodes[i - 1].worldPos;
                                Vector3 b = nodes[i + 1].worldPos - nodes[i].worldPos;
                                float lenA = a.magnitude;
                                float lenB = b.magnitude;
                                float alpha = !isFinalIter ? bendAlpha : 0f;
                                float curvScale = 1f;
                                if (lenA > 0.0001f && lenB > 0.0001f)
                                {
                                    float cosAng = Vector3.Dot(a, b) / (lenA * lenB);
                                    if (cosAng < 0.935f)
                                    {
                                        float u = Mathf.Clamp01((0.935f - cosAng) / 0.17f);
                                        float smoothU = u * u * (3f - 2f * u);
                                        alpha = Mathf.Max(alpha, 0.76f * smoothU);
                                        curvScale = 1f - smoothU;
                                    }
                                }
                                if (alpha > 0.005f)
                                {
                                    Vector3 curvW = (subHeadRot * nodes[i].restCurvatureLocal) * curvScale;
                                    Vector3 targetMid = 0.5f * (nodes[i - 1].worldPos + nodes[i + 1].worldPos) + curvW;
                                    nodes[i].worldPos = Vector3.Lerp(nodes[i].worldPos, targetMid, alpha);
                                }
                            }
                        }

                        // XPBD compliant rest-pose tether + inextensible segment length + analytical collision
                        for (int i = 1; i < count; i++)
                        {
                            HairNode prev = nodes[i - 1];
                            HairNode curr = nodes[i];

                            // XPBD compliance rest-pose tether (unconditionally stable for any subDt & stiffness)
                            Vector3 restWorld = subCurrMat.MultiplyPoint3x4(curr.restLocalPos);
                            for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                            {
                                AetherStrandCollider col = colliders[colIdx];
                                if (col != null && col.enabled)
                                    col.ResolveCollision(ref restWorld, collisionMargin);
                            }

                            float shapedStiff = Mathf.Clamp01(zoneStiff * curr.stiffnessWeight);
                            float compliance = Mathf.Lerp(0.085f, 0.00035f, shapedStiff * shapedStiff);
                            float alphaTilde = (compliance / subDtSq) * iters;
                            float xpbdLambda = 1f / (1f + alphaTilde);
                            curr.worldPos += (restWorld - curr.worldPos) * xpbdLambda;

                            // Distance constraint projection
                            Vector3 diff = curr.worldPos - prev.worldPos;
                            float dist = diff.magnitude;
                            if (dist > 0.00001f)
                            {
                                float err = (dist - curr.segmentLength) / dist;
                                if (i == 1)
                                {
                                    curr.worldPos -= diff * err;
                                }
                                else
                                {
                                    prev.worldPos += diff * (err * 0.20f);
                                    curr.worldPos -= diff * (err * 0.80f);
                                }
                            }

                            // Inelastic collider projection
                            bool hitCollider = false;
                            for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                            {
                                AetherStrandCollider col = colliders[colIdx];
                                if (col != null && col.enabled)
                                {
                                    Vector3 before = curr.worldPos;
                                    col.ResolveCollision(ref curr.worldPos, collisionMargin);
                                    if ((curr.worldPos - before).sqrMagnitude > 1e-8f) hitCollider = true;
                                }
                            }
                            if (hitCollider && isFinalIter)
                            {
                                curr.prevWorldPos = Vector3.Lerp(curr.prevWorldPos, curr.worldPos, 0.86f);
                            }
                        }
                    }
                }
            }

            _prevHeadPos = targetHeadPos;
            _prevHeadRot = targetHeadRot;

            // 3. Aim Bone Transforms Along Simulated Chains
            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                int count = nodes.Count;
                for (int i = 0; i < count - 1; i++)
                {
                    HairNode curr = nodes[i];
                    HairNode next = nodes[i + 1];
                    if (curr.isVirtualLeaf || curr.bone == null) continue;

                    Vector3 restChildPos = next.isVirtualLeaf
                        ? headTransform.TransformPoint(next.restLocalPos)
                        : next.bone.position;

                    Vector3 restDir = (restChildPos - curr.bone.position).normalized;
                    Vector3 simDir = (next.worldPos - curr.worldPos).normalized;

                    if (restDir.sqrMagnitude > 0.0001f && simDir.sqrMagnitude > 0.0001f)
                        curr.bone.rotation = Quaternion.FromToRotation(restDir, simDir) * curr.bone.rotation;
                }
            }

            // 4. Sync optional _HeadCenterWS property block for spherical normal smoothing
            if (hairRenderer != null && headTransform != null)
            {
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                hairRenderer.GetPropertyBlock(_mpb);
                Vector3 centerWS = headTransform.TransformPoint(new Vector3(0f, 0.12f, -0.01f));
                _mpb.SetVector(HeadCenterWsId, new Vector4(centerWS.x, centerWS.y, centerWS.z, 0f));
                hairRenderer.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// Evaluates real-time geometric quality telemetry across all active hair bone chains.
        /// </summary>
        public PhysicsAuditResult ComputeAudit()
        {
            int activeColliders = 0;
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Count; i++)
                {
                    if (colliders[i] != null && colliders[i].enabled) activeColliders++;
                }
            }

            var result = new PhysicsAuditResult
            {
                totalChains = _chains.Count,
                colliderCount = activeColliders
            };
            if (!_initialized || _chains.Count == 0 || headTransform == null) return result;

            int penetrating = 0;
            float maxKink = 0f;
            float tipSwaySumCm = 0f;
            float maxStretchPct = 0f;

            for (int c = 0; c < _chains.Count; c++)
            {
                var nodes = _chains[c].nodes;
                int count = nodes.Count;
                if (count < 2) continue;

                for (int i = 1; i < count; i++)
                {
                    HairNode curr = nodes[i];
                    HairNode prev = nodes[i - 1];

                    if (curr.segmentLength > 0.0001f)
                    {
                        float dist = Vector3.Distance(curr.worldPos, prev.worldPos);
                        float stretchPct = Mathf.Abs(dist - curr.segmentLength) / curr.segmentLength * 100f;
                        if (stretchPct > maxStretchPct) maxStretchPct = stretchPct;
                    }

                    if (i < count - 1)
                    {
                        Vector3 a = curr.worldPos - prev.worldPos;
                        Vector3 b = nodes[i + 1].worldPos - curr.worldPos;
                        if (a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f)
                        {
                            float ang = Vector3.Angle(a, b);
                            if (ang > maxKink) maxKink = ang;
                        }
                    }

                    bool isPenetrating = false;
                    for (int colIdx = 0; colIdx < colliders.Count; colIdx++)
                    {
                        AetherStrandCollider col = colliders[colIdx];
                        if (col != null && col.enabled && col.SignedDistance(curr.worldPos) < -0.001f)
                        {
                            isPenetrating = true;
                            break;
                        }
                    }
                    if (isPenetrating) penetrating++;
                }

                HairNode tip = nodes[count - 1];
                Vector3 restTipWS = headTransform.TransformPoint(tip.restLocalPos);
                tipSwaySumCm += Vector3.Distance(tip.worldPos, restTipWS) * 100f;
            }

            result.penetratingNodes = penetrating;
            result.maxKinkAngleDeg = maxKink;
            result.meanTipSwayCm = _chains.Count > 0 ? tipSwaySumCm / _chains.Count : 0f;
            result.maxStretchErrorPct = maxStretchPct;
            return result;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _chains == null) return;
            Gizmos.color = new Color(0.96f, 0.68f, 0.20f, 0.9f);
            foreach (HairChain chain in _chains)
            {
                for (int i = 0; i < chain.nodes.Count - 1; i++)
                {
                    Gizmos.DrawLine(chain.nodes[i].worldPos, chain.nodes[i + 1].worldPos);
                    Gizmos.DrawSphere(chain.nodes[i + 1].worldPos, 0.007f);
                }
            }
        }
    }
}
`;
}

function generateColliderCs(): string {
  return `using UnityEngine;

namespace AetherStrand
{
    public enum HairColliderShape
    {
        Sphere,
        Capsule
    }

    /// <summary>
    /// Fast analytical Sphere / Capsule collider for head, face/jaw, neck, chest, and shoulder hair collision.
    /// </summary>
    public class AetherStrandCollider : MonoBehaviour
    {
        public HairColliderShape shape = HairColliderShape.Sphere;
        public Vector3 centerOffset = Vector3.zero;
        [Min(0.0001f)] public float radius = 0.12f;
        [Min(0.0f)] public float capsuleHeight = 0.22f;
        public Vector3 capsuleAxis = Vector3.up;

        private float GetUniformLossyScale()
        {
            Vector3 s = transform.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return avg > 1e-5f ? avg : 1f;
        }

        /// <summary>
        /// Exact signed distance from pointWS to collider surface (negative inside, 0 on surface, positive outside).
        /// </summary>
        public float SignedDistance(Vector3 pointWS)
        {
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            Vector3 centerWS = transform.TransformPoint(centerOffset);
            if (shape == HairColliderShape.Sphere)
            {
                return Vector3.Distance(pointWS, centerWS) - worldRadius;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
            if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
            else axisDir = Vector3.up;

            float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
            Vector3 p0 = centerWS - axisDir * halfSeg;
            Vector3 p1 = centerWS + axisDir * halfSeg;

            Vector3 seg = p1 - p0;
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01(Vector3.Dot(pointWS - p0, seg) / segLenSq)
                : 0.5f;

            Vector3 closest = p0 + seg * t;
            return Vector3.Distance(pointWS, closest) - worldRadius;
        }

        public void ResolveCollision(ref Vector3 pointWS, float hairRadius)
        {
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            float minDist = worldRadius + hairRadius;

            if (shape == HairColliderShape.Sphere)
            {
                Vector3 centerWS = transform.TransformPoint(centerOffset);
                Vector3 delta = pointWS - centerWS;
                float sqrMag = delta.sqrMagnitude;
                if (sqrMag < minDist * minDist && sqrMag > 0.000001f)
                    pointWS = centerWS + delta * (minDist / Mathf.Sqrt(sqrMag));
                return;
            }

            Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
            if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
            else axisDir = Vector3.up;

            Vector3 center = transform.TransformPoint(centerOffset);
            float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
            Vector3 p0 = center - axisDir * halfSeg;
            Vector3 p1 = center + axisDir * halfSeg;

            Vector3 seg = p1 - p0;
            float segLenSq = seg.sqrMagnitude;
            float t = segLenSq > 0.00001f
                ? Mathf.Clamp01(Vector3.Dot(pointWS - p0, seg) / segLenSq)
                : 0.5f;

            Vector3 closest = p0 + seg * t;
            Vector3 diff = pointWS - closest;
            float dSq = diff.sqrMagnitude;
            if (dSq < minDist * minDist && dSq > 0.000001f)
                pointWS = closest + diff * (minDist / Mathf.Sqrt(dSq));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.22f, 0.74f, 0.97f, 0.55f);
            float worldScale = GetUniformLossyScale();
            float worldRadius = radius * worldScale;
            Vector3 center = transform.TransformPoint(centerOffset);
            if (shape == HairColliderShape.Sphere)
            {
                Gizmos.DrawWireSphere(center, worldRadius);
            }
            else
            {
                Vector3 axisDir = transform.TransformDirection(capsuleAxis.sqrMagnitude > 1e-6f ? capsuleAxis.normalized : Vector3.up);
                if (axisDir.sqrMagnitude > 1e-6f) axisDir.Normalize();
                else axisDir = Vector3.up;

                float halfSeg = Mathf.Max(0f, capsuleHeight * worldScale * 0.5f);
                Vector3 p0 = center - axisDir * halfSeg;
                Vector3 p1 = center + axisDir * halfSeg;
                Gizmos.DrawWireSphere(p0, worldRadius);
                Gizmos.DrawWireSphere(p1, worldRadius);
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
`;
}

function generateWindZoneCs(physics: PhysicsConfig): string {
  return `using UnityEngine;

namespace AetherStrand
{
    /// <summary>
    /// Global & turbulent wind zone for AetherStrandPhysics hair chains.
    /// </summary>
    public class AetherStrandWindZone : MonoBehaviour
    {
        [Range(0f, 15f)] public float windStrength = ${physics.windStrength.toFixed(2)}f;
        [Range(0f, 2f)]  public float turbulence = ${physics.windTurbulence.toFixed(2)}f;
        [Range(0.2f, 8f)] public float gustFrequency = 2.3f;

        private Vector3 _extraGust;

        private void Update()
        {
            _extraGust = Vector3.Lerp(_extraGust, Vector3.zero, 1f - Mathf.Exp(-4.5f * Time.deltaTime));
        }

        public void TriggerGust(Vector3 worldImpulse) => _extraGust += worldImpulse;

        public Vector3 SampleWindAcceleration(Vector3 worldPos, float strandSeed, float time)
        {
            Vector3 dir = transform.forward;
            float primaryWave = 0.65f + 0.35f * Mathf.Sin(time * gustFrequency + strandSeed);
            float perlin = (Mathf.PerlinNoise(worldPos.x * 1.3f + time * 1.4f, worldPos.z * 1.3f + strandSeed) - 0.5f) * 2f;

            Vector3 lateral = transform.right * (perlin * turbulence * 1.6f);
            Vector3 vertical = Vector3.up * (Mathf.Sin(time * 3.1f + strandSeed) * turbulence * 0.55f);
            return dir * (windStrength * primaryWave) + lateral + vertical + _extraGust;
        }
    }
}
`;
}

function generateEditorWindowCs(
  shader: ShaderConfig,
  physics: PhysicsConfig,
  presetId: HairPresetId,
  pipeline: RenderPipeline
): string {
  const presetMeta = getExportPresetMetadata(presetId);
  const shaderName =
    pipeline === 'urp'
      ? 'AetherStrand/SculptedHair_URP'
      : 'AetherStrand/SculptedHair_BuiltIn';

  return `#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AetherStrand.Editor
{
    [Serializable]
    internal class PresetShaderDto
    {
        public string rootColor;
        public string baseColor;
        public string tipColor;
        public string primarySpecColor;
        public string secondarySpecColor;
        public float primaryShift;
        public float primaryPower;
        public float primaryIntensity;
        public float secondaryShift;
        public float secondaryPower;
        public float secondaryIntensity;
        public float rimIntensity;
        public float rimPower;
        public float rootAO;
        public float grooveDepth;
        public float strandVariation;
    }

    [Serializable]
    internal class PresetPhysicsDto
    {
        public float stiffness;
        public float gravity;
        public float windStrength;
        public float windTurbulence;
        public float windAzimuth;
        public float damping;
        public float inertia;
        public float bendingSmoothness;
        public float collisionMarginMm;
        public float rootAnchor;
        public int solverSubsteps;
    }

    [Serializable]
    internal class PresetRecipeDto
    {
        public string presetId;
        public PresetShaderDto shader;
        public PresetPhysicsDto physics;
    }

    public class AetherStrandSetupWindow : EditorWindow
    {
        private GameObject _characterRoot;
        private Renderer _hairRenderer;
        private string _bonePrefix = "Hair_";
        private string _materialOutputPath = "Assets/AetherStrand_${presetMeta.id}_Mat.mat";
        private string _presetJsonPath = "";
        private float _headHeightOverride = 0f;

        [MenuItem("Tools/AetherStrand/Hair Studio Setup")]
        public static void OpenWindow()
        {
            var win = GetWindow<AetherStrandSetupWindow>("AetherStrand Setup");
            win.minSize = new Vector2(380, 340);
        }

        private void OnGUI()
        {
            GUILayout.Label("AetherStrand — Sculpted Hair Toolkit (Unity 6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Baked preset: ${presetMeta.name} (${pipeline.toUpperCase()}).\\n" +
                "Create/update the hair material in-place (preserving GUIDs) or auto-bind Hair_ bone chains + 6 scaled body colliders.",
                MessageType.Info
            );

            EditorGUILayout.Space(8);
            _characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", _characterRoot, typeof(GameObject), true);
            _hairRenderer = (Renderer)EditorGUILayout.ObjectField("Hair Renderer (Optional)", _hairRenderer, typeof(Renderer), true);
            _bonePrefix = EditorGUILayout.TextField("Hair Bone Prefix", _bonePrefix);
            _materialOutputPath = EditorGUILayout.TextField("Material Output Path", _materialOutputPath);
            _presetJsonPath = EditorGUILayout.TextField("Preset JSON Path (Opt)", _presetJsonPath);
            _headHeightOverride = EditorGUILayout.FloatField("Head Height Override (0=Auto)", _headHeightOverride);

            EditorGUILayout.Space(12);
            if (GUILayout.Button("Create / Update Hair Material Asset", GUILayout.Height(30)))
            {
                Material mat = CreateMaterialAsset(_materialOutputPath, string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath);
                if (mat != null) EditorGUIUtility.PingObject(mat);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUI.DisabledScope(_characterRoot == null))
            {
                if (GUILayout.Button("Auto-Setup Physics & 6 Scaled Colliders", GUILayout.Height(30)))
                {
                    AutoSetupPhysics(
                        _characterRoot,
                        _bonePrefix,
                        string.IsNullOrWhiteSpace(_presetJsonPath) ? null : _presetJsonPath,
                        _headHeightOverride,
                        _hairRenderer
                    );
                }
            }
        }

        /// <summary>
        /// Headless-safe material creator/updater. Updates existing assets in-place to preserve GUIDs.
        /// Never displays modal dialogs.
        /// </summary>
        public static Material CreateMaterialAsset(
            string outputPath = "Assets/AetherStrand_${presetMeta.id}_Mat.mat",
            string presetJsonPath = null)
        {
            Shader s = Shader.Find("${shaderName}");
            if (s == null)
            {
                Debug.LogError("[AetherStrand] Shader '${shaderName}' not found. Ensure AetherStrand_Hair.shader is imported.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = "Assets/AetherStrand_${presetMeta.id}_Mat.mat";

            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // In-place update preserves existing asset GUIDs across scenes and prefabs
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(s);
            }
            else
            {
                mat.shader = s;
            }

            // Apply baked default look values first
            mat.SetColor("_RootColor", ${hexToCSharpColor(shader.rootColor)});
            mat.SetColor("_BaseColor", ${hexToCSharpColor(shader.baseColor)});
            mat.SetColor("_TipColor", ${hexToCSharpColor(shader.tipColor)});
            mat.SetColor("_PrimarySpecColor", ${hexToCSharpColor(shader.primarySpecColor)});
            mat.SetColor("_SecondarySpecColor", ${hexToCSharpColor(shader.secondarySpecColor)});
            mat.SetFloat("_StrandVariation", ${shader.strandVariation.toFixed(3)}f);
            mat.SetFloat("_PrimaryShift", ${shader.primaryShift.toFixed(3)}f);
            mat.SetFloat("_PrimaryPower", ${shader.primaryPower.toFixed(1)}f);
            mat.SetFloat("_PrimaryIntensity", ${shader.primaryIntensity.toFixed(3)}f);
            mat.SetFloat("_SecondaryShift", ${shader.secondaryShift.toFixed(3)}f);
            mat.SetFloat("_SecondaryPower", ${shader.secondaryPower.toFixed(1)}f);
            mat.SetFloat("_SecondaryIntensity", ${shader.secondaryIntensity.toFixed(3)}f);
            mat.SetFloat("_RimIntensity", ${shader.rimIntensity.toFixed(3)}f);
            mat.SetFloat("_RimPower", ${shader.rimPower.toFixed(2)}f);
            mat.SetFloat("_RootAO", ${shader.rootAO.toFixed(3)}f);
            mat.SetFloat("_GrooveDepth", ${shader.grooveDepth.toFixed(3)}f);
            mat.SetFloat("_UseAlphaClip", 1f);
            mat.SetFloat("_AlphaClip", 0.34f);
            mat.SetFloat("_DitherStrength", 0.0f);
            mat.SetFloat("_HeadCenterBlend", 0.15f);
            mat.EnableKeyword("_USE_ALPHA_CLIP");

            // Optional JSON recipe override
            PresetRecipeDto recipe = TryLoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.shader != null)
            {
                ApplyColorHex(mat, "_RootColor", recipe.shader.rootColor);
                ApplyColorHex(mat, "_BaseColor", recipe.shader.baseColor);
                ApplyColorHex(mat, "_TipColor", recipe.shader.tipColor);
                ApplyColorHex(mat, "_PrimarySpecColor", recipe.shader.primarySpecColor);
                ApplyColorHex(mat, "_SecondarySpecColor", recipe.shader.secondarySpecColor);
                if (recipe.shader.primaryPower > 0f) mat.SetFloat("_PrimaryPower", recipe.shader.primaryPower);
                if (recipe.shader.secondaryPower > 0f) mat.SetFloat("_SecondaryPower", recipe.shader.secondaryPower);
                mat.SetFloat("_PrimaryShift", recipe.shader.primaryShift);
                mat.SetFloat("_PrimaryIntensity", recipe.shader.primaryIntensity);
                mat.SetFloat("_SecondaryShift", recipe.shader.secondaryShift);
                mat.SetFloat("_SecondaryIntensity", recipe.shader.secondaryIntensity);
                mat.SetFloat("_RimIntensity", recipe.shader.rimIntensity);
                if (recipe.shader.rimPower > 0f) mat.SetFloat("_RimPower", recipe.shader.rimPower);
                mat.SetFloat("_RootAO", recipe.shader.rootAO);
                if (recipe.shader.grooveDepth > 0f) mat.SetFloat("_GrooveDepth", recipe.shader.grooveDepth);
                mat.SetFloat("_StrandVariation", recipe.shader.strandVariation);
            }

            TryBindTexture(mat, "_GrooveAoMap", "AetherStrand_GrooveAO");
            TryBindTexture(mat, "_FlowIdMap", "AetherStrand_FlowIDShift");
            TryBindTexture(mat, "_NormalMap", "AetherStrand_Normal");
            TryBindTexture(mat, "_AlphaMap", "AetherStrand_AlphaCard");

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, outputPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[AetherStrand] {(isNew ? "Created" : "Updated in-place")} hair material at '{outputPath}'.");
            return mat;
        }

        /// <summary>
        /// Headless-safe deterministic physics & 6-collider auto-setup (Cranium, Face/Jaw, Neck, Upper Chest, L/R Shoulders).
        /// Never displays modal dialogs.
        /// </summary>
        public static AetherStrandPhysics AutoSetupPhysics(
            GameObject characterRoot,
            string bonePrefix = "Hair_",
            string presetJsonPath = null,
            float headHeightOverride = 0f,
            Renderer hairRenderer = null,
            Bounds? headMeshWorldBounds = null)
        {
            if (characterRoot == null)
            {
                Debug.LogError("[AetherStrand] AutoSetupPhysics requires a non-null characterRoot.");
                return null;
            }

            if (string.IsNullOrEmpty(bonePrefix)) bonePrefix = "Hair_";

            AetherStrandPhysics physics = characterRoot.GetComponent<AetherStrandPhysics>();
            if (physics == null)
                physics = characterRoot.AddComponent<AetherStrandPhysics>();

            if (hairRenderer == null)
            {
                Renderer[] renderers = characterRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].name.IndexOf("Hair", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hairRenderer = renderers[i];
                        break;
                    }
                }
            }
            physics.hairRenderer = hairRenderer;

            Animator animator = characterRoot.GetComponentInChildren<Animator>();
            Transform headBone = null;
            Transform neckBone = null;
            Transform chestBone = null;
            Transform leftShoulderBone = null;
            Transform rightShoulderBone = null;

            if (animator != null && animator.isHuman)
            {
                headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                neckBone = animator.GetBoneTransform(HumanBodyBones.Neck);
                chestBone = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                            ?? animator.GetBoneTransform(HumanBodyBones.Chest)
                            ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                leftShoulderBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm)
                                   ?? animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
                rightShoulderBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm)
                                    ?? animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            }

            if (headBone == null)
            {
                foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase))
                    {
                        headBone = t;
                        break;
                    }
                }
            }
            if (headBone == null) headBone = characterRoot.transform;

            physics.headTransform = headBone;

            // Character anatomical basis in world space (immune to arbitrary bone local axis conventions)
            Vector3 upWS = characterRoot.transform.up.sqrMagnitude > 1e-6f ? characterRoot.transform.up.normalized : Vector3.up;
            Vector3 fwdWS = characterRoot.transform.forward.sqrMagnitude > 1e-6f ? characterRoot.transform.forward.normalized : Vector3.forward;
            Vector3 rightWS = Vector3.Cross(upWS, fwdWS);
            if (rightWS.sqrMagnitude > 1e-6f) rightWS.Normalize();
            else rightWS = Vector3.right;
            fwdWS = Vector3.Cross(rightWS, upWS).normalized;

            // Estimate anatomical head height (meters) for proportional collider scaling
            float headHeight = headHeightOverride;
            if (headHeight <= 0.01f && headMeshWorldBounds.HasValue)
            {
                float h = headMeshWorldBounds.Value.size.y;
                if (h > 0.06f) headHeight = Mathf.Clamp(h, 0.14f, 0.45f);
            }
            if (headHeight <= 0.01f)
            {
                if (headBone != null && neckBone != null)
                {
                    float neckToHead = Vector3.Distance(headBone.position, neckBone.position);
                    if (neckToHead > 0.02f) headHeight = Mathf.Clamp(neckToHead * 2.0f, 0.16f, 0.28f);
                }
                if (headHeight <= 0.01f && hairRenderer != null)
                {
                    float h = hairRenderer.bounds.size.y * 0.72f;
                    if (h > 0.08f) headHeight = Mathf.Clamp(h, 0.16f, 0.35f);
                }
                if (headHeight <= 0.01f) headHeight = 0.23f;
            }

            // Compute Cranium and Face/Jaw dimensions in world space (fit inside actual skull bounds when provided)
            Vector3 craniumCenterWS;
            float craniumRadiusWS;
            Vector3 faceCenterWS;
            float faceRadiusWS;
            float faceHeightWS;

            if (headMeshWorldBounds.HasValue)
            {
                Bounds hb = headMeshWorldBounds.Value;
                craniumRadiusWS = Mathf.Max(0.03f, Mathf.Min(hb.extents.x, hb.extents.z) * 0.84f);
                float craniumTopY = hb.max.y - craniumRadiusWS * 1.04f;
                float craniumY = Mathf.Lerp(hb.center.y, craniumTopY, 0.55f);
                craniumCenterWS = new Vector3(hb.center.x, craniumY, hb.center.z) - fwdWS * (craniumRadiusWS * 0.06f);

                faceRadiusWS = craniumRadiusWS * 0.78f;
                faceHeightWS = Mathf.Max(0.02f, hb.extents.y * 0.55f);
                faceCenterWS = new Vector3(hb.center.x, hb.center.y - hb.extents.y * 0.28f, hb.center.z) + fwdWS * (craniumRadiusWS * 0.10f);
            }
            else
            {
                craniumRadiusWS = 0.36f * headHeight;
                craniumCenterWS = headBone.position + upWS * (0.52f * headHeight) - fwdWS * (0.02f * headHeight);

                faceRadiusWS = 0.29f * headHeight;
                faceHeightWS = 0.26f * headHeight;
                faceCenterWS = headBone.position + upWS * (0.28f * headHeight) + fwdWS * (0.01f * headHeight);
            }

            // Create / update 6 world-space-computed analytical colliders converted to each bone's local space
            physics.colliders.Clear();

            // 1. Cranium Sphere
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                headBone,
                "AetherStrand_Col_Cranium",
                HairColliderShape.Sphere,
                craniumCenterWS,
                craniumRadiusWS,
                0f,
                upWS
            ));

            // 2. Face / Jaw Capsule
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                headBone,
                "AetherStrand_Col_FaceJaw",
                HairColliderShape.Capsule,
                faceCenterWS,
                faceRadiusWS,
                faceHeightWS,
                upWS
            ));

            // 3. Neck Capsule
            Transform neckParent = neckBone != null ? neckBone : headBone;
            Vector3 neckCenterWS = neckBone != null
                ? neckBone.position + upWS * (0.02f * headHeight) - fwdWS * (0.02f * headHeight)
                : (headMeshWorldBounds.HasValue
                    ? new Vector3(headMeshWorldBounds.Value.center.x, headMeshWorldBounds.Value.min.y - 0.08f * headHeight, headMeshWorldBounds.Value.center.z) - fwdWS * (0.04f * headHeight)
                    : headBone.position - upWS * (0.08f * headHeight) - fwdWS * (0.04f * headHeight));
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                neckParent,
                "AetherStrand_Col_Neck",
                HairColliderShape.Capsule,
                neckCenterWS,
                0.16f * headHeight,
                0.34f * headHeight,
                upWS
            ));

            // 4. Upper Chest Capsule
            Transform chestParent = chestBone != null ? chestBone : characterRoot.transform;
            Vector3 chestCenterWS = chestBone != null
                ? chestBone.position + upWS * (0.08f * headHeight) - fwdWS * (0.02f * headHeight)
                : neckCenterWS - upWS * (0.34f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                chestParent,
                "AetherStrand_Col_UpperChest",
                HairColliderShape.Capsule,
                chestCenterWS,
                0.22f * headHeight,
                0.44f * headHeight,
                rightWS
            ));

            // 5. Left Shoulder Capsule
            Transform leftParent = leftShoulderBone != null ? leftShoulderBone : chestParent;
            Vector3 leftCenterWS = leftShoulderBone != null
                ? leftShoulderBone.position + upWS * (0.04f * headHeight)
                : chestCenterWS - rightWS * (0.42f * headHeight) + upWS * (0.04f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                leftParent,
                "AetherStrand_Col_LeftShoulder",
                HairColliderShape.Capsule,
                leftCenterWS,
                0.165f * headHeight,
                0.32f * headHeight,
                rightWS
            ));

            // 6. Right Shoulder Capsule
            Transform rightParent = rightShoulderBone != null ? rightShoulderBone : chestParent;
            Vector3 rightCenterWS = rightShoulderBone != null
                ? rightShoulderBone.position + upWS * (0.04f * headHeight)
                : chestCenterWS + rightWS * (0.42f * headHeight) + upWS * (0.04f * headHeight);
            physics.colliders.Add(EnsureNamedColliderWorldSpace(
                rightParent,
                "AetherStrand_Col_RightShoulder",
                HairColliderShape.Capsule,
                rightCenterWS,
                0.165f * headHeight,
                0.32f * headHeight,
                rightWS
            ));

            // Deterministic sorted chain discovery using strict Ordinal comparison (never matches legacy lowercase prefixes)
            var roots = new List<Transform>();
            foreach (Transform t in characterRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(bonePrefix, StringComparison.Ordinal) &&
                    (t.parent == null || !t.parent.name.StartsWith(bonePrefix, StringComparison.Ordinal)))
                {
                    roots.Add(t);
                }
            }
            roots.Sort((a, b) => string.Compare(GetHierarchyPath(a), GetHierarchyPath(b), StringComparison.Ordinal));
            physics.hairRootBones = roots;

            // Apply baked defaults (fallbackWindStrength defaults to 0f for indoor stillness), then optional JSON override
            physics.stiffness = ${physics.stiffness.toFixed(2)}f;
            physics.gravity = ${physics.gravity.toFixed(2)}f;
            physics.damping = ${physics.damping.toFixed(3)}f;
            physics.headInertia = ${physics.inertia.toFixed(2)}f;
            physics.bendingSmoothness = ${physics.bendingSmoothness.toFixed(2)}f;
            physics.collisionMargin = ${(physics.collisionMarginMm * 0.001).toFixed(4)}f;
            physics.rootAnchor = ${physics.rootAnchor.toFixed(2)}f;
            physics.solverSubsteps = 3;
            physics.fallbackWindStrength = 0f;
            physics.fallbackTurbulence = ${physics.windTurbulence.toFixed(2)}f;
            physics.fallbackWindAzimuthDeg = ${physics.windAzimuth.toFixed(1)}f;

            PresetRecipeDto recipe = TryLoadRecipeJson(presetJsonPath);
            if (recipe != null && recipe.physics != null)
            {
                if (recipe.physics.stiffness > 0f) physics.stiffness = recipe.physics.stiffness;
                if (recipe.physics.gravity >= 0f) physics.gravity = recipe.physics.gravity;
                if (recipe.physics.damping > 0f) physics.damping = recipe.physics.damping;
                physics.headInertia = recipe.physics.inertia;
                physics.bendingSmoothness = recipe.physics.bendingSmoothness;
                if (recipe.physics.collisionMarginMm > 0f) physics.collisionMargin = recipe.physics.collisionMarginMm * 0.001f;
                if (recipe.physics.rootAnchor > 0f) physics.rootAnchor = recipe.physics.rootAnchor;
                physics.solverSubsteps = Mathf.Clamp(recipe.physics.solverSubsteps > 0 ? recipe.physics.solverSubsteps : 3, 1, 6);
                physics.fallbackWindStrength = recipe.physics.windStrength;
                physics.fallbackTurbulence = recipe.physics.windTurbulence;
                physics.fallbackWindAzimuthDeg = recipe.physics.windAzimuth;
            }

            physics.RebuildChains();
            EditorUtility.SetDirty(physics);
            Debug.Log($"[AetherStrand] Auto-setup complete on '{characterRoot.name}': {roots.Count} bone chains, {physics.colliders.Count} colliders (headHeight={headHeight:F3}m, craniumR={craniumRadiusWS:F3}m).");
            return physics;
        }

        private static float GetUniformLossyScale(Transform t)
        {
            if (t == null) return 1f;
            Vector3 s = t.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            return avg > 1e-5f ? avg : 1f;
        }

        private static AetherStrandCollider EnsureNamedColliderWorldSpace(
            Transform parent,
            string childName,
            HairColliderShape shape,
            Vector3 centerWS,
            float worldRadius,
            float worldHeight,
            Vector3 axisWS)
        {
            Transform existing = parent.Find(childName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(parent, false);
            }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            float parentScale = GetUniformLossyScale(parent);
            Vector3 localCenter = parent.InverseTransformPoint(centerWS);
            Vector3 localAxis = parent.InverseTransformDirection(axisWS.sqrMagnitude > 1e-6f ? axisWS.normalized : Vector3.up);
            if (localAxis.sqrMagnitude > 1e-6f) localAxis.Normalize();
            else localAxis = Vector3.up;

            AetherStrandCollider col = go.GetComponent<AetherStrandCollider>();
            if (col == null) col = go.AddComponent<AetherStrandCollider>();
            col.shape = shape;
            col.centerOffset = localCenter;
            col.radius = Mathf.Max(0.0001f, worldRadius / parentScale);
            col.capsuleHeight = Mathf.Max(0f, worldHeight / parentScale);
            col.capsuleAxis = localAxis;
            EditorUtility.SetDirty(col);
            return col;
        }

        private static string GetHierarchyPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        private static PresetRecipeDto TryLoadRecipeJson(string presetJsonPath)
        {
            if (string.IsNullOrWhiteSpace(presetJsonPath) || !File.Exists(presetJsonPath)) return null;
            try
            {
                string raw = File.ReadAllText(presetJsonPath);
                return JsonUtility.FromJson<PresetRecipeDto>(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AetherStrand] Could not parse preset JSON '{presetJsonPath}': {ex.Message}");
                return null;
            }
        }

        private static void ApplyColorHex(Material mat, string prop, string hex)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                mat.SetColor(prop, parsed);
            }
        }

        private static string ResolvePackageRootPath()
        {
            // Resolve package root relative to this Editor MonoScript (../Textures/<name>.png)
            string[] scriptGuids = AssetDatabase.FindAssets("AetherStrandSetupWindow t:MonoScript");
            Array.Sort(scriptGuids, StringComparer.Ordinal);
            for (int i = 0; i < scriptGuids.Length; i++)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuids[i]);
                if (!string.IsNullOrEmpty(scriptPath) && scriptPath.EndsWith("AetherStrandSetupWindow.cs", StringComparison.Ordinal))
                {
                    string editorDir = Path.GetDirectoryName(scriptPath);
                    string pkgRoot = string.IsNullOrEmpty(editorDir) ? null : Path.GetDirectoryName(editorDir);
                    if (!string.IsNullOrEmpty(pkgRoot))
                    {
                        return pkgRoot.Replace('\\\\', '/');
                    }
                }
            }
            return "Packages/com.aetherstrand.sculptedhair";
        }

        private static void TryBindTexture(Material mat, string prop, string fileToken)
        {
            Texture2D tex = null;

            // 1. Package-scoped resolution relative to MonoScript path (../Textures/<name>.png)
            string pkgRoot = ResolvePackageRootPath();
            if (!string.IsNullOrEmpty(pkgRoot))
            {
                string scopedPath = $"{pkgRoot}/Textures/{fileToken}.png";
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(scopedPath);
            }

            // 2. Direct UPM package path fallback
            if (tex == null)
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Packages/com.aetherstrand.sculptedhair/Textures/{fileToken}.png");
            }

            // 3. Project-wide AssetDatabase search fallback
            if (tex == null)
            {
                string[] guids = AssetDatabase.FindAssets(fileToken + " t:Texture2D");
                if (guids.Length > 0)
                {
                    Array.Sort(guids, StringComparer.Ordinal);
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            if (tex != null) mat.SetTexture(prop, tex);
        }
    }
}
#endif
`;
}

function generateRuntimeAsmdef(): string {
  return JSON.stringify(
    {
      name: 'AetherStrand.Runtime',
      rootNamespace: 'AetherStrand',
      references: [],
      includePlatforms: [],
      excludePlatforms: [],
      allowUnsafeCode: false,
      overrideReferences: false,
      precompiledReferences: [],
      autoReferenced: true,
      defineConstraints: [],
      versionDefines: [],
      noEngineReferences: false,
    },
    null,
    2
  );
}

function generateEditorAsmdef(): string {
  return JSON.stringify(
    {
      name: 'AetherStrand.Editor',
      rootNamespace: 'AetherStrand.Editor',
      references: ['AetherStrand.Runtime'],
      includePlatforms: ['Editor'],
      excludePlatforms: [],
      allowUnsafeCode: false,
      overrideReferences: false,
      precompiledReferences: [],
      autoReferenced: true,
      defineConstraints: [],
      versionDefines: [],
      noEngineReferences: false,
    },
    null,
    2
  );
}

function generatePresetGeometryJson(
  presetId: HairPresetId,
  physics: PhysicsConfig,
  exportedLocks: ExportedHairLock[]
): string {
  const presetMeta = getExportPresetMetadata(presetId);
  return JSON.stringify(
    {
      schemaVersion: '2.0',
      presetId: presetMeta.id,
      presetName: presetMeta.name,
      coordinateSystem: 'blender_z_up_neg_y_front',
      sourcePivot: [0, 0.44, 0],
      strandWidthScale: Number((physics.strandWidthScale ?? 1.0).toFixed(3)),
      lockCount: exportedLocks.length,
      locks: exportedLocks,
    },
    null,
    2
  );
}

function generateBlenderAddonPy(presetId: HairPresetId): string {
  const presetMeta = getExportPresetMetadata(presetId);
  const isPartedShag = presetId === 'leon' || presetId === 'noctis' || presetId === 'crimson';

  return `bl_info = {
    "name": "AetherStrand — Sculpted Volumetric Hair & Unity Bone Rig Generator",
    "author": "AetherStrand Studio",
    "version": (3, 0, 0),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar (N) > AetherStrand",
    "description": "Imports exact AetherStrand PresetGeometry.json locks (or generates procedural crescent-profile clumps), clusters them into shared Unity Hair_ bone chains, and exports Unity FBX with Linear vertex colors.",
    "category": "Mesh",
}

import bpy
import bmesh
import json
import math
import os
import random
from mathutils import Vector


# Sleek Crescent sculpted cross-section profile (domed carved outer ridge + flatter hugging underside)
PROFILE_U = [0.0, 0.18, 0.36, 0.5, 0.64, 0.82, 1.0]
PROFILE_X = [0.0, 0.76, 1.0, 0.0, -1.0, -0.76, 0.0]
PROFILE_Y = [1.0, 0.54, -0.02, -0.26, -0.02, 0.54, 1.0]

ZONE_ORDER = ("Bangs", "Crown", "Sides", "Rear")


def sample_cubic_bezier(p0, p1, p2, p3, count):
    pts = []
    for i in range(count):
        t = i / max(1, count - 1)
        u = 1.0 - t
        p = (p0 * (u**3)) + (p1 * (3 * u * u * t)) + (p2 * (3 * u * t * t)) + (p3 * (t**3))
        pts.append(p)
    return pts


def resample_polyline(points, target_count):
    if not points:
        return []
    if len(points) == 1 or target_count <= 1:
        return [points[0].copy() for _ in range(max(1, target_count))]

    cum = [0.0]
    for i in range(1, len(points)):
        cum.append(cum[-1] + (points[i] - points[i - 1]).length)
    total_len = cum[-1]
    if total_len < 1e-6:
        return [points[0].copy() for _ in range(target_count)]

    out = []
    seg_idx = 0
    for k in range(target_count):
        target_d = (k / (target_count - 1)) * total_len
        while seg_idx < len(cum) - 2 and cum[seg_idx + 1] < target_d:
            seg_idx += 1
        d0, d1 = cum[seg_idx], cum[seg_idx + 1]
        alpha = (target_d - d0) / max(1e-6, d1 - d0)
        out.append(points[seg_idx].lerp(points[seg_idx + 1], alpha))
    return out


def lock_feature_vector(pts):
    """9D deterministic descriptor (root, mid, tip) for spatial clustering within a hair zone."""
    p0 = pts[0]
    pm = pts[len(pts) // 2]
    pt = pts[-1]
    return (
        p0.x * 1.15, p0.y * 1.15, p0.z * 1.15,
        pm.x * 1.0,  pm.y * 1.0,  pm.z * 1.0,
        pt.x * 0.95, pt.y * 0.95, pt.z * 0.95,
    )


def sq_dist_nd(a, b):
    return sum((x - y) * (x - y) for x, y in zip(a, b))


def deterministic_kmeans(features, k, iterations=14):
    """Deterministic farthest-point init + Lloyd k-means (zero randomness, 100% reproducible)."""
    n = len(features)
    if n == 0 or k <= 0:
        return []
    k = min(k, n)

    # Farthest-point seeding starting from deterministic lexicographically sorted index 0
    seed_indices = [0]
    min_dists = [sq_dist_nd(features[i], features[0]) for i in range(n)]
    for _ in range(1, k):
        best_idx = max(range(n), key=lambda idx: (min_dists[idx], -idx))
        seed_indices.append(best_idx)
        cand = features[best_idx]
        for i in range(n):
            d = sq_dist_nd(features[i], cand)
            if d < min_dists[i]:
                min_dists[i] = d

    dim = len(features[0])
    centroids = [list(features[idx]) for idx in seed_indices]
    assignments = [0] * n

    for _ in range(iterations):
        changed = False
        for i, feat in enumerate(features):
            best_c = min(range(k), key=lambda c: sq_dist_nd(feat, centroids[c]))
            if assignments[i] != best_c:
                assignments[i] = best_c
                changed = True

        sums = [[0.0] * dim for _ in range(k)]
        counts = [0] * k
        for i, c in enumerate(assignments):
            counts[c] += 1
            f = features[i]
            s = sums[c]
            for d in range(dim):
                s[d] += f[d]

        for c in range(k):
            if counts[c] > 0:
                inv = 1.0 / counts[c]
                centroids[c] = [v * inv for v in sums[c]]

        if not changed:
            break

    return assignments


def allocate_zone_chains(zone_counts, target_total):
    """Distributes ~target_total shared bone chains proportionally across active hair zones."""
    active_zones = [z for z in ZONE_ORDER if zone_counts.get(z, 0) > 0]
    if not active_zones:
        return {}

    total_locks = sum(zone_counts[z] for z in active_zones)
    target = min(max(len(active_zones), target_total), total_locks)

    alloc = {}
    remainders = []
    used = 0
    for z in active_zones:
        exact = (zone_counts[z] / total_locks) * target
        base = max(1, min(zone_counts[z], int(math.floor(exact))))
        alloc[z] = base
        used += base
        remainders.append((exact - base, z))

    # Adjust up or down deterministically to hit exact target
    while used < target:
        remainders.sort(key=lambda item: (-item[0], ZONE_ORDER.index(item[1])))
        progressed = False
        for idx, (rem, z) in enumerate(remainders):
            if alloc[z] < zone_counts[z]:
                alloc[z] += 1
                used += 1
                remainders[idx] = (rem - 1.0, z)
                progressed = True
                if used >= target:
                    break
        if not progressed:
            break

    while used > target:
        candidates = sorted(
            [z for z in active_zones if alloc[z] > 1],
            key=lambda z: (-alloc[z], ZONE_ORDER.index(z)),
        )
        if not candidates:
            break
        alloc[candidates[0]] -= 1
        used -= 1

    return alloc


def create_volumetric_lock_mesh(name, spine_points, base_width, base_thickness, seed_val, layer_ao=0.95, is_base_shell=False):
    """Creates a true 3D sculpted volumetric crescent-profile hair lock with a razor-pointed tip."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    uv_layer = bm.loops.layers.uv.new("UVMap")
    color_layer = bm.loops.layers.color.new("Col")
    alpha_mask = 0.0 if is_base_shell else 1.0

    count = len(spine_points)
    ring_count = count - 1
    sides = len(PROFILE_X) - 1  # 6 faces
    rings = []

    head_center = Vector((0.0, 0.01, 0.425))
    ref_out = (spine_points[0] - head_center)
    ref_out = ref_out.normalized() if ref_out.length > 0.001 else Vector((0.0, 0.0, 1.0))

    for k in range(ring_count):
        pt = spine_points[k]
        t = k / max(1, count - 1)
        if k == 0:
            tang = (spine_points[1] - pt)
        else:
            tang = (spine_points[k + 1] - spine_points[k - 1])
        tang = tang.normalized() if tang.length > 1e-6 else Vector((0.0, 0.0, -1.0))

        side = tang.cross(ref_out)
        if side.length < 0.001:
            side = Vector((1.0, 0.0, 0.0))
        else:
            side.normalize()
        out_vec = side.cross(tang).normalized()
        ref_out = ref_out.lerp(out_vec, 0.35).normalized()

        # Exact viewport sculpted taper (cohesive root->mid body, razor-pointed tip)
        root_tuck = 0.7 + 0.3 * math.sin(min(1.0, t * 4.5) * math.pi * 0.5)
        if t < 0.44:
            width_taper = 1.0 - t * 0.14
        else:
            width_taper = (((1.0 - t) / 0.56) ** 0.94) * 0.938
        thick_taper = (1.0 - t) ** 0.86
        half_w = base_width * 0.5 * root_tuck * max(0.04, width_taper)
        half_t = base_thickness * 0.47 * root_tuck * thick_taper

        ring_verts = []
        for r in range(sides):
            pos = pt + side * (PROFILE_X[r] * half_w) + out_vec * (PROFILE_Y[r] * half_t)
            ring_verts.append(bm.verts.new(pos))
        rings.append(ring_verts)

    tip_vert = bm.verts.new(spine_points[-1])
    bm.verts.ensure_lookup_table()

    for k in range(ring_count - 1):
        v0 = k / (count - 1)
        v1 = (k + 1) / (count - 1)
        for r in range(sides):
            rn = (r + 1) % sides
            face = bm.faces.new((rings[k][r], rings[k][rn], rings[k + 1][rn], rings[k + 1][r]))
            face.smooth = True
            u0, u1 = PROFILE_U[r], PROFILE_U[r + 1]
            uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
            cols = [
                (v0, layer_ao, seed_val, alpha_mask),
                (v0, layer_ao, seed_val, alpha_mask),
                (v1, layer_ao, seed_val, alpha_mask),
                (v1, layer_ao, seed_val, alpha_mask),
            ]
            for loop, uv, col in zip(face.loops, uvs, cols):
                loop[uv_layer].uv = uv
                loop[color_layer] = col

    # Converging pointed tip cap
    v_last = (ring_count - 1) / (count - 1)
    for r in range(sides):
        rn = (r + 1) % sides
        face = bm.faces.new((rings[-1][r], rings[-1][rn], tip_vert))
        face.smooth = True
        uvs = [(PROFILE_U[r], v_last), (PROFILE_U[r + 1], v_last), (0.5, 1.0)]
        cols = [
            (v_last, layer_ao, seed_val, alpha_mask),
            (v_last, layer_ao, seed_val, alpha_mask),
            (1.0, layer_ao, seed_val, alpha_mask),
        ]
        for loop, uv, col in zip(face.loops, uvs, cols):
            loop[uv_layer].uv = uv
            loop[color_layer] = col

    bm.to_mesh(mesh)
    bm.free()
    return mesh


def resolve_default_geometry_json():
    try:
        script_dir = os.path.dirname(os.path.abspath(__file__))
        candidate = os.path.join(script_dir, "PresetGeometry.json")
        if os.path.isfile(candidate):
            return candidate
    except Exception:
        pass
    return ""


def load_lock_specs_from_json(filepath):
    with open(filepath, "r", encoding="utf-8") as fh:
        payload = json.load(fh)
    raw_locks = payload.get("locks", [])
    specs = []
    for entry in raw_locks:
        pts_raw = entry.get("points", [])
        if len(pts_raw) < 2:
            continue
        pts = [Vector((float(p[0]), float(p[1]), float(p[2]))) for p in pts_raw]
        zone = entry.get("zone", "Sides")
        if zone not in ZONE_ORDER:
            zone = "Sides"
        specs.append({
            "zone": zone,
            "width": float(entry.get("width", 0.04)),
            "thickness": float(entry.get("thickness", 0.013)),
            "seed": float(entry.get("seed", 0.5)),
            "layerAO": float(entry.get("layerAO", 0.92)),
            "isBaseShell": bool(entry.get("isBaseShell", False)),
            "points": pts,
        })
    return specs, payload.get("presetId", "${presetMeta.id}")


def build_procedural_fallback_specs(lock_count, part_offset_x, tip_flick, lock_width, lock_thickness, segs):
    rng = random.Random(200108)
    specs = []
    for s in range(lock_count):
        side = -1.0 if s < int(lock_count * 0.56) else 1.0
        u = (s % max(1, lock_count // 2)) / max(1, (lock_count // 2) - 1)
        y_root = -0.08 + u * 0.19
        root = Vector((part_offset_x + side * 0.012, y_root, 0.68 - ((u - 0.35) ** 2) * 0.08))

        angle = 0.24 + u * (math.pi * 0.85)
        dx = side * math.sin(angle)
        dy = -math.cos(angle)
        reach = 0.16 + math.sin(u * math.pi) * 0.05 + rng.uniform(-0.015, 0.025)
        drop = 0.22 + (s % 3) * 0.045 + rng.uniform(0.0, 0.04)

        p1 = root + Vector((dx * 0.08, dy * 0.07, 0.048))
        p2 = root + Vector((dx * reach, dy * reach, -drop * 0.38))
        p3 = root + Vector((dx * (reach + tip_flick), dy * (reach + tip_flick * 0.6), -drop))
        pts = sample_cubic_bezier(root, p1, p2, p3, segs)

        if p3.y < -0.10:
            zone = "Bangs"
        elif u < 0.30:
            zone = "Crown"
        elif p3.y > 0.08:
            zone = "Rear"
        else:
            zone = "Sides"

        specs.append({
            "zone": zone,
            "width": lock_width * rng.uniform(0.82, 1.22),
            "thickness": lock_thickness * rng.uniform(0.85, 1.15),
            "seed": rng.random(),
            "layerAO": 0.85 + (s % 2) * 0.15,
            "isBaseShell": False,
            "points": pts,
        })
    return specs


class AETHERSTRAND_OT_GenerateSculptedHair(bpy.types.Operator):
    bl_idname = "aetherstrand.generate_sculpted_hair"
    bl_label = "Generate Sculpted Hair + Clustered Rig"
    bl_description = "Auto-loads PresetGeometry.json (or procedural fallback), builds all crescent locks, and k-means clusters them onto shared Hair_<Zone>_<k>_<i> chains"
    bl_options = {'REGISTER', 'UNDO'}

    geometry_json_path: bpy.props.StringProperty(
        name="PresetGeometry.json",
        description="Optional path to PresetGeometry.json (auto-detected beside addon script if blank)",
        default="",
        subtype='FILE_PATH',
    )
    target_chains: bpy.props.IntProperty(name="Shared Bone Chains", default=44, min=8, max=96)
    bones_per_chain: bpy.props.IntProperty(name="Bones / Chain", default=5, min=3, max=8)
    fallback_lock_count: bpy.props.IntProperty(name="Fallback Locks", default=120, min=24, max=280)
    part_offset_x: bpy.props.FloatProperty(name="Part Offset X", default=${isPartedShag ? '0.032' : '0.0'}, min=-0.12, max=0.12)
    tip_flick: bpy.props.FloatProperty(name="Outward Tip Flick", default=0.038, min=0.0, max=0.16)
    lock_width: bpy.props.FloatProperty(name="Fallback Width", default=0.040, min=0.01, max=0.12)
    lock_thickness: bpy.props.FloatProperty(name="Fallback Thickness", default=0.013, min=0.004, max=0.06)

    def execute(self, context):
        json_path = bpy.path.abspath(self.geometry_json_path) if self.geometry_json_path else resolve_default_geometry_json()
        preset_tag = "${presetMeta.id}"
        loaded_from_json = False

        if json_path and os.path.isfile(json_path):
            specs, preset_tag = load_lock_specs_from_json(json_path)
            loaded_from_json = True
        else:
            specs = build_procedural_fallback_specs(
                self.fallback_lock_count,
                self.part_offset_x,
                self.tip_flick,
                self.lock_width,
                self.lock_thickness,
                self.bones_per_chain + 1,
            )

        if not specs:
            self.report({'ERROR'}, "No valid hair locks found to generate.")
            return {'CANCELLED'}

        collection = bpy.data.collections.new(f"AetherStrand_{preset_tag}")
        context.scene.collection.children.link(collection)

        arm_data = bpy.data.armatures.new("AetherStrand_HairRig")
        arm_obj = bpy.data.objects.new("AetherStrand_HairRig", arm_data)
        collection.objects.link(arm_obj)

        # Group dynamic (non-base-shell) locks by zone and k-means cluster onto ~44 shared bone chains
        zone_indices = {z: [] for z in ZONE_ORDER}
        for idx, sp in enumerate(specs):
            if not sp["isBaseShell"]:
                zone_indices[sp["zone"]].append(idx)

        zone_counts = {z: len(zone_indices[z]) for z in ZONE_ORDER}
        chain_alloc = allocate_zone_chains(zone_counts, self.target_chains)

        lock_chain_map = {}  # lock_idx -> list of bone names along its assigned shared chain
        chain_definitions = []  # (chain_bone_names, mean_spine_pts)

        for zone in ZONE_ORDER:
            indices = zone_indices[zone]
            k_target = chain_alloc.get(zone, 0)
            if not indices or k_target <= 0:
                continue

            features = [lock_feature_vector(specs[i]["points"]) for i in indices]
            assignments = deterministic_kmeans(features, k_target, iterations=14)

            clusters = {}
            for local_i, cluster_id in enumerate(assignments):
                clusters.setdefault(cluster_id, []).append(indices[local_i])

            # Deterministic cluster ordering by mean root azimuth & height
            ordered_clusters = []
            for cid, members in clusters.items():
                resampled = [resample_polyline(specs[m]["points"], self.bones_per_chain + 1) for m in members]
                mean_spine = []
                for step in range(self.bones_per_chain + 1):
                    acc = Vector((0.0, 0.0, 0.0))
                    for r_pts in resampled:
                        acc += r_pts[step]
                    mean_spine.append(acc / len(resampled))
                root_pt = mean_spine[0]
                sort_key = (math.atan2(root_pt.x, -root_pt.y), -root_pt.z)
                ordered_clusters.append((sort_key, members, mean_spine))

            ordered_clusters.sort(key=lambda item: item[0])

            for c_idx, (_, members, mean_spine) in enumerate(ordered_clusters):
                bone_names = [f"Hair_{zone}_{c_idx:02d}_{b}" for b in range(self.bones_per_chain)]
                chain_definitions.append((bone_names, mean_spine))
                for m in members:
                    lock_chain_map[m] = bone_names

        # Build Armature EditBones (5 bones/chain, use_deform = (k > 0))
        context.view_layer.objects.active = arm_obj
        bpy.ops.object.mode_set(mode='EDIT')
        head_bone = arm_data.edit_bones.new("Head")
        head_bone.head = Vector((0.0, 0.0, 0.0))
        head_bone.tail = Vector((0.0, 0.0, 0.425))
        head_bone.use_deform = True

        for bone_names, spine in chain_definitions:
            parent_eb = head_bone
            for k, b_name in enumerate(bone_names):
                eb = arm_data.edit_bones.new(b_name)
                eb.head = spine[k]
                tail_pt = spine[k + 1]
                if (tail_pt - eb.head).length < 1e-4:
                    tail_pt = eb.head + Vector((0.0, 0.0, -0.02))
                eb.tail = tail_pt
                eb.parent = parent_eb
                eb.use_connect = (k > 0)
                eb.use_deform = (k > 0)
                parent_eb = eb

        bpy.ops.object.mode_set(mode='OBJECT')

        # Build volumetric crescent lock meshes & bind weights to shared chains (base shell -> Head)
        created_meshes = []
        sides_count = len(PROFILE_X) - 1

        for idx, sp in enumerate(specs):
            pts = sp["points"]
            mesh = create_volumetric_lock_mesh(
                f"HairLock_{idx:03d}",
                pts,
                sp["width"],
                sp["thickness"],
                sp["seed"],
                sp["layerAO"],
                sp["isBaseShell"],
            )
            obj = bpy.data.objects.new(mesh.name, mesh)
            collection.objects.link(obj)

            vg_head = obj.vertex_groups.new(name="Head")
            ring_count = len(pts) - 1
            tip_vert_idx = ring_count * sides_count

            if sp["isBaseShell"] or idx not in lock_chain_map:
                all_verts = list(range(tip_vert_idx + 1))
                vg_head.add(all_verts, 1.0, 'REPLACE')
            else:
                bone_names = lock_chain_map[idx]
                deform_bones = bone_names[1:]  # use_deform=True bones (k = 1..4)
                deform_vgs = [obj.vertex_groups.new(name=bn) for bn in deform_bones]
                n_def = len(deform_vgs)

                # Pin root ring (k = 0) 100% to Head so scalp roots never detach
                vg_head.add(list(range(0, sides_count)), 1.0, 'REPLACE')

                for k in range(1, ring_count):
                    t = k / max(1, len(pts) - 1)
                    ring_indices = list(range(k * sides_count, (k + 1) * sides_count))
                    # Smooth root transition from Head into first deform bone
                    head_w = max(0.0, 1.0 - t * 4.5)
                    chain_w = 1.0 - head_w
                    if head_w > 0.001:
                        vg_head.add(ring_indices, head_w, 'REPLACE')

                    f_idx = min(n_def - 1.0, max(0.0, t * (n_def - 1)))
                    b0 = int(math.floor(f_idx))
                    b1 = min(n_def - 1, b0 + 1)
                    frac = f_idx - b0
                    if b0 == b1:
                        deform_vgs[b0].add(ring_indices, chain_w, 'REPLACE')
                    else:
                        deform_vgs[b0].add(ring_indices, chain_w * (1.0 - frac), 'REPLACE')
                        deform_vgs[b1].add(ring_indices, chain_w * frac, 'REPLACE')

                deform_vgs[-1].add([tip_vert_idx], 1.0, 'REPLACE')

            created_meshes.append(obj)

        bpy.ops.object.select_all(action='DESELECT')
        for o in created_meshes:
            o.select_set(True)
        context.view_layer.objects.active = created_meshes[0]
        bpy.ops.object.join()

        joined_obj = created_meshes[0]
        joined_obj.name = "AetherStrand_SculptedHairMesh"
        joined_obj.parent = arm_obj
        mod = joined_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = arm_obj

        src_label = "PresetGeometry.json" if loaded_from_json else "procedural fallback"
        self.report(
            {'INFO'},
            f"Built {len(specs)} volumetric locks ({src_label}) clustered onto {len(chain_definitions)} shared Hair_ bone chains.",
        )
        return {'FINISHED'}


class AETHERSTRAND_OT_ExportUnityFbx(bpy.types.Operator):
    bl_idname = "aetherstrand.export_unity_fbx"
    bl_label = "Export Unity FBX (Linear VCol)"
    bl_description = "Export AetherStrand hair mesh & rig to Unity FBX using linear vertex colors (colors_type='LINEAR') and add_leaf_bones=False"
    bl_options = {'REGISTER'}

    filepath: bpy.props.StringProperty(
        name="FBX Output Path",
        default="//AetherStrand_${presetMeta.id}.fbx",
        subtype='FILE_PATH',
    )

    def invoke(self, context, event):
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self, context):
        arm_obj = bpy.data.objects.get("AetherStrand_HairRig")
        mesh_obj = bpy.data.objects.get("AetherStrand_SculptedHairMesh")
        if arm_obj is not None and mesh_obj is not None:
            bpy.ops.object.select_all(action='DESELECT')
            arm_obj.select_set(True)
            mesh_obj.select_set(True)
            context.view_layer.objects.active = arm_obj

        out_path = bpy.path.abspath(self.filepath)
        if not out_path.lower().endswith(".fbx"):
            out_path += ".fbx"

        bpy.ops.export_scene.fbx(
            filepath=out_path,
            use_selection=True,
            object_types={'ARMATURE', 'MESH'},
            apply_scale_options='FBX_SCALE_ALL',
            mesh_smooth_type='FACE',
            use_tspace=True,
            colors_type='LINEAR',
            path_mode='STRIP',
            add_leaf_bones=False,
            use_armature_deform_only=False,
            bake_anim=False,
            axis_forward='-Z',
            axis_up='Y',
        )
        self.report({'INFO'}, f"Exported Unity FBX (Linear VCol, no leaf bones) to {out_path}")
        return {'FINISHED'}


class AETHERSTRAND_OT_CurvesToSculptedLocks(bpy.types.Operator):
    bl_idname = "aetherstrand.curves_to_locks"
    bl_label = "Convert Curves to 3D Sculpted Locks"
    bl_description = "Convert selected Bezier/NURBS hair curves into volumetric crescent-profile locks"
    bl_options = {'REGISTER', 'UNDO'}

    lock_width: bpy.props.FloatProperty(name="Lock Width", default=0.05, min=0.01, max=0.2)
    lock_thickness: bpy.props.FloatProperty(name="Lock Thickness", default=0.025, min=0.005, max=0.1)

    def execute(self, context):
        curve_objs = [o for o in context.selected_objects if o.type == 'CURVE']
        if not curve_objs:
            self.report({'WARNING'}, "Select one or more Curve objects first.")
            return {'CANCELLED'}

        rng = random.Random(77)
        converted = 0
        for obj in curve_objs:
            for idx, spline in enumerate(obj.data.splines):
                pts = [obj.matrix_world @ bp.co for bp in spline.bezier_points] if spline.type == 'BEZIER' else [
                    obj.matrix_world @ Vector((p.co.x, p.co.y, p.co.z)) for p in spline.points
                ]
                if len(pts) < 2:
                    continue
                mesh = create_volumetric_lock_mesh(
                    f"{obj.name}_Lock_{idx}", pts, self.lock_width, self.lock_thickness, rng.random()
                )
                lock_obj = bpy.data.objects.new(mesh.name, mesh)
                context.scene.collection.objects.link(lock_obj)
                converted += 1

        self.report({'INFO'}, f"Converted {converted} guide curves into sculpted 3D hair locks.")
        return {'FINISHED'}


class AETHERSTRAND_PT_MainPanel(bpy.types.Panel):
    bl_label = "AetherStrand Sculpted Hair"
    bl_idname = "AETHERSTRAND_PT_main_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'AetherStrand'

    def draw(self, context):
        layout = self.layout
        layout.label(text="Volumetric Sculpted Hair", icon='STRANDS')
        col = layout.column(align=True)
        col.operator("aetherstrand.generate_sculpted_hair", icon='OUTLINER_OB_HAIR')
        col.operator("aetherstrand.export_unity_fbx", icon='EXPORT')
        col.operator("aetherstrand.curves_to_locks", icon='CURVE_BEZCURVE')


classes = (
    AETHERSTRAND_OT_GenerateSculptedHair,
    AETHERSTRAND_OT_ExportUnityFbx,
    AETHERSTRAND_OT_CurvesToSculptedLocks,
    AETHERSTRAND_PT_MainPanel,
)


def register():
    for cls in classes:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(classes):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
`;
}

function generatePackageJson(pipeline: RenderPipeline): string {
  return JSON.stringify(
    {
      name: 'com.aetherstrand.sculptedhair',
      version: '3.0.0',
      displayName: 'AetherStrand Sculpted Hair Studio',
      description: `Volumetric sculpted hair shader (${pipeline.toUpperCase()} / Unity 6) with carved fiber grooves, Kajiya-Kay dual-lobe specular, and substepped XPBD bone-chain physics.`,
      unity: '6000.0',
      keywords: ['hair', 'sculpted-hair', 'shader', 'kajiya-kay', 'physics', 'xpbd', 'verlet', 'urp'],
      author: { name: 'AetherStrand Toolkit' },
    },
    null,
    2
  );
}

function generateReadme(presetId: HairPresetId, pipeline: RenderPipeline, lockCount: number): string {
  const presetMeta = getExportPresetMetadata(presetId);
  return `# AetherStrand — Sculpted Hair Plugin for Unity 6 & Blender 4.x

Exported Preset: **${presetMeta.name}** (\`${presetMeta.id}\` — ${presetMeta.subtitle})
Exported Viewport Locks: **${lockCount}**
Target Render Pipeline: **${pipeline === 'urp' ? 'Unity 6 URP 17.6 (Universal Render Pipeline, Forward & Forward+)' : 'Unity Built-in Render Pipeline'}**

## Contents
- \`AetherStrand_Unity/package.json\` — Embedded UPM package manifest (\`com.aetherstrand.sculptedhair\`, Unity \`6000.0\`).
- \`AetherStrand_Unity/Runtime/AetherStrand.Runtime.asmdef\` & \`Editor/AetherStrand.Editor.asmdef\` — Assembly definitions for zero-config UPM compilation.
- \`AetherStrand_Unity/Shaders/AetherStrand_Hair.shader\` — SRP-Batcher-safe Carved-Fiber Kajiya-Kay Dual-Lobe Hair Shader (\`AetherStrand/SculptedHair_${pipeline === 'urp' ? 'URP' : 'BuiltIn'}\`) with \`DepthOnly\`, \`DepthNormals\`, Forward+ \`LIGHT_LOOP_BEGIN\`, \`_HeadCenterBlend\`, and \`_DitherStrength\`.
- \`AetherStrand_Unity/Runtime/AetherStrandPhysics.cs\` — Substepped XPBD / Verlet bone-chain solver with framerate-independent damping, per-zone stiffness (\`Bangs\`, \`Crown\`, \`Sides\`, \`Rear\`), \`Simulate(dt, simTime)\`, \`ResetToRestPose()\`, and \`ComputeAudit()\`.
- \`AetherStrand_Unity/Runtime/AetherStrandCollider.cs\` & \`AetherStrandWindZone.cs\` — Analytical Sphere/Capsule colliders with \`SignedDistance()\` and turbulent wind zone.
- \`AetherStrand_Unity/Editor/AetherStrandSetupWindow.cs\` — Headless-safe static material updater (GUID-preserving) and deterministic 6-collider + \`Hair_\` bone auto-setup.
- \`AetherStrand_Unity/Textures/\` — Seamless \`GrooveAO\`, \`FlowIDShift\`, \`Normal\`, and \`AlphaCard\` PNG maps.
- \`AetherStrand_Blender/PresetGeometry.json\` — Exact 1:1 exported viewport hair locks converted to Blender Z-up (\`-Y\` front).
- \`AetherStrand_Blender/aetherstrand_hair_cards.py\` — Blender 4.x addon that auto-loads \`PresetGeometry.json\`, k-means clusters locks onto ~44 shared \`Hair_<Zone>_<k>_<i>\` bone chains (5 bones/chain, \`use_deform = k > 0\`), and exports Unity FBX with Linear vertex colors.
`;
}

function generateAgentPlaybookMd(
  presetId: HairPresetId,
  pipeline: RenderPipeline,
  physics: PhysicsConfig
): string {
  const presetMeta = getExportPresetMetadata(presetId);
  return `# AetherStrand Automation & Verification Playbook

This plugin is engineered for headless visual verification, deterministic XPBD physics auditing, and one-shot Unity 6 / Blender 4.x production handoff.

## 1. Unity 6 Batch / Headless Setup API
Both editor setup entry points are public static headless-safe methods (zero modal dialogs):

\`\`\`csharp
using AetherStrand;
using AetherStrand.Editor;

// 1. Create or update material in-place (preserves GUIDs)
var mat = AetherStrandSetupWindow.CreateMaterialAsset(
    "Assets/AetherStrand_${presetMeta.id}_Mat.mat",
    "Packages/com.aetherstrand.sculptedhair/Presets/ExportedLook.json"
);

// 2. Bind Hair_ bone chains + 6 head-height-scaled colliders deterministically
var phys = AetherStrandSetupWindow.AutoSetupPhysics(
    characterRoot,
    "Hair_",
    "Packages/com.aetherstrand.sculptedhair/Presets/ExportedLook.json",
    headHeightOverride: 0f,
    hairRenderer: hairSkinnedMeshRenderer
);

// 3. Deterministic physics audit loop
phys.ResetToRestPose();
for (int f = 0; f < 180; f++) phys.Simulate(1f / 60f, f / 60f);
PhysicsAuditResult audit = phys.ComputeAudit();
\`\`\`

## 2. Blender 4.x Exact Geometry Import & Unity FBX Export
1. Keep \`PresetGeometry.json\` beside \`aetherstrand_hair_cards.py\`.
2. Run **AetherStrand > Generate Sculpted Hair + Clustered Rig** (imports all viewport locks and k-means clusters them onto ~44 shared \`Hair_<Zone>_<k>_<i>\` bone chains with \`5\` bones/chain and \`use_deform = k > 0\`).
3. Run **AetherStrand > Export Unity FBX (Linear VCol)** (exports with \`colors_type='LINEAR'\` and \`add_leaf_bones=False\`).

## 3. Coordinate & Rig Conventions (Active Preset: ${presetMeta.name} / \`${presetMeta.id}\`)
- **Viewport Space**: Right-handed Y-up (\`+Z\` front, relative to head pivot \`(0, 0.44, 0)\`).
- **Blender Export Space (\`PresetGeometry.json\`)**: Right-handed Z-up (\`-Y\` front): \`[x - px, -(z - pz), y - py]\`.
- **Target Unity Pipeline**: \`${pipeline === 'urp' ? 'Unity 6 URP 17.6 (HLSL, Forward+, SRP Batcher)' : 'Unity Built-in Forward RP (CG)'}\`.
- **Solver Defaults**: \`stiffness=${physics.stiffness.toFixed(2)}\`, \`bendingSmoothness=${physics.bendingSmoothness.toFixed(2)}\`, \`collisionMarginMm=${physics.collisionMarginMm.toFixed(0)}\`, \`solverSubsteps=3\`.
`;
}

function generateTextureMeta(guid: string, isNormalMap: boolean): string {
  return `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 0
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 4
    mipBias: 0
    wrapMode: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: ${isNormalMap ? 1 : 0}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    slices: []
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function generateRecipeJson(
  shader: ShaderConfig,
  physics: PhysicsConfig,
  presetId: HairPresetId,
  pipeline: RenderPipeline
): string {
  const presetMeta = getExportPresetMetadata(presetId);
  return JSON.stringify(
    {
      schemaVersion: '2.0',
      presetId: presetMeta.id,
      presetName: presetMeta.name,
      pipeline,
      shader,
      physics: {
        stiffness: physics.stiffness,
        gravity: physics.gravity,
        windStrength: 0,
        windTurbulence: physics.windTurbulence,
        windAzimuth: physics.windAzimuth,
        damping: physics.damping,
        inertia: physics.inertia,
        bendingSmoothness: physics.bendingSmoothness,
        collisionMarginMm: physics.collisionMarginMm,
        rootAnchor: physics.rootAnchor,
        solverSubsteps: 3,
        strandWidthScale: physics.strandWidthScale,
        collisionEnabled: physics.collisionEnabled,
      },
    },
    null,
    2
  );
}

// Canonical Unity sources: unity-plugin/com.aetherstrand.sculptedhair is the single source of truth for the URP
// plugin (exact web ports of mesh, shader and solver, head fit, eye-clear fringe, push-out). The web generator only
// supplies PresetGeometry.json, ExportedLook.json, the recipe and shader constants (Neon decision, 2026-11-23).
const CANONICAL_PLUGIN_SOURCES = import.meta.glob(
  '../../unity-plugin/com.aetherstrand.sculptedhair/{Editor,Runtime,Shaders}/*.{cs,shader,asmdef}',
  { query: '?raw', import: 'default', eager: true }
) as Record<string, string>;
const CANONICAL_TEXTURE_METAS = import.meta.glob(
  '../../unity-plugin/com.aetherstrand.sculptedhair/Textures/*.meta',
  { query: '?raw', import: 'default', eager: true }
) as Record<string, string>;

function applyCanonicalPlugin(files: PluginFileEntry[], pipeline: RenderPipeline): PluginFileEntry[] {
  if (pipeline !== 'urp') return files;
  const prefix = '../../unity-plugin/com.aetherstrand.sculptedhair/';
  const byPath = new Map(files.map((f) => [f.path, f] as const));
  const langOf = (name: string) => (name.endsWith('.cs') ? 'csharp' : name.endsWith('.shader') ? 'hlsl' : name.endsWith('.meta') ? 'yaml' : 'json');
  for (const [key, content] of [...Object.entries(CANONICAL_PLUGIN_SOURCES), ...Object.entries(CANONICAL_TEXTURE_METAS)]) {
    const rel = key.slice(prefix.length);
    const path = `AetherStrand_Unity/${rel}`;
    const name = rel.split('/').pop() as string;
    const existing = byPath.get(path);
    if (existing) {
      existing.content = content;
    } else {
      files.push({ path, name, category: 'unity', language: langOf(name), description: 'Canonical AetherStrand Unity plugin source (unity-plugin/).', content });
    }
  }
  return files;
}

export function generatePluginFiles(
  shader: ShaderConfig,
  physics: PhysicsConfig,
  presetId: HairPresetId,
  pipeline: RenderPipeline,
  exportedLocks: ExportedHairLock[] = []
): PluginFileEntry[] {
  const shaderCode =
    pipeline === 'urp' ? generateUrpHairShader(shader) : generateBuiltinHairShader(shader);

  return applyCanonicalPlugin([
    {
      path: 'AetherStrand_Unity/Shaders/AetherStrand_Hair.shader',
      name: 'AetherStrand_Hair.shader',
      category: 'unity',
      language: 'hlsl',
      description: `Volumetric Carved-Fiber Kajiya-Kay Hair Shader (${pipeline === 'urp' ? 'Unity 6 URP 17.6 Forward+ & SRP Batcher' : 'Built-in Forward CG'}) synced to live viewport settings.`,
      content: shaderCode,
    },
    {
      path: 'AetherStrand_Unity/Runtime/AetherStrand.Runtime.asmdef',
      name: 'AetherStrand.Runtime.asmdef',
      category: 'unity',
      language: 'json',
      description: 'Unity 6 UPM Runtime assembly definition (rootNamespace: AetherStrand).',
      content: generateRuntimeAsmdef(),
    },
    {
      path: 'AetherStrand_Unity/Runtime/AetherStrandPhysics.cs',
      name: 'AetherStrandPhysics.cs',
      category: 'unity',
      language: 'csharp',
      description: 'Substepped XPBD / Verlet hair bone-chain solver with framerate-independent damping, per-zone stiffness, Simulate(), ResetToRestPose(), and ComputeAudit().',
      content: generatePhysicsCs(physics),
    },
    {
      path: 'AetherStrand_Unity/Runtime/AetherStrandCollider.cs',
      name: 'AetherStrandCollider.cs',
      category: 'unity',
      language: 'csharp',
      description: 'Analytical Sphere & Capsule collider component with SignedDistance() and collision projection.',
      content: generateColliderCs(),
    },
    {
      path: 'AetherStrand_Unity/Runtime/AetherStrandWindZone.cs',
      name: 'AetherStrandWindZone.cs',
      category: 'unity',
      language: 'csharp',
      description: 'Dynamic directional wind & Perlin turbulence zone with TriggerGust() API.',
      content: generateWindZoneCs(physics),
    },
    {
      path: 'AetherStrand_Unity/Editor/AetherStrand.Editor.asmdef',
      name: 'AetherStrand.Editor.asmdef',
      category: 'unity',
      language: 'json',
      description: 'Unity 6 UPM Editor assembly definition referencing AetherStrand.Runtime.',
      content: generateEditorAsmdef(),
    },
    {
      path: 'AetherStrand_Unity/Editor/AetherStrandSetupWindow.cs',
      name: 'AetherStrandSetupWindow.cs',
      category: 'unity',
      language: 'csharp',
      description: 'Headless-safe Unity Editor setup (GUID-preserving material updater, deterministic chain discovery, and 6 head-height-scaled colliders).',
      content: generateEditorWindowCs(shader, physics, presetId, pipeline),
    },
    {
      path: 'AetherStrand_Unity/package.json',
      name: 'package.json',
      category: 'unity',
      language: 'json',
      description: 'Unity 6 Package Manager (UPM) manifest for com.aetherstrand.sculptedhair.',
      content: generatePackageJson(pipeline),
    },
    {
      path: 'AetherStrand_Agent/AGENT_PLAYBOOK.md',
      name: 'AGENT_PLAYBOOK.md',
      category: 'unity',
      language: 'markdown',
      description: 'Headless Unity 6 setup & Blender 4.x verification playbook.',
      content: generateAgentPlaybookMd(presetId, pipeline, physics),
    },
    {
      path: 'AetherStrand_Agent/AetherStrand_Recipe.json',
      name: 'AetherStrand_Recipe.json',
      category: 'unity',
      language: 'json',
      description: 'Deterministic schema v2.0 JSON recipe capturing exact shader and XPBD physics settings.',
      content: generateRecipeJson(shader, physics, presetId, pipeline),
    },
    {
      path: 'AetherStrand_Unity/README.md',
      name: 'README.md',
      category: 'unity',
      language: 'markdown',
      description: 'Unity 6 & Blender 4.x integration guide.',
      content: generateReadme(presetId, pipeline, exportedLocks.length),
    },
    {
      path: 'AetherStrand_Unity/Textures/AetherStrand_GrooveAO.png.meta',
      name: 'AetherStrand_GrooveAO.png.meta',
      category: 'unity',
      language: 'yaml',
      description: 'Deterministic Unity TextureImporter meta (Linear sRGBTexture: 0, Repeat wrapMode: 0).',
      content: generateTextureMeta('a3e710014a7b4f189c2e6b0f10000001', false),
    },
    {
      path: 'AetherStrand_Unity/Textures/AetherStrand_FlowIDShift.png.meta',
      name: 'AetherStrand_FlowIDShift.png.meta',
      category: 'unity',
      language: 'yaml',
      description: 'Deterministic Unity TextureImporter meta (Linear sRGBTexture: 0, Repeat wrapMode: 0).',
      content: generateTextureMeta('a3e710024a7b4f189c2e6b0f10000002', false),
    },
    {
      path: 'AetherStrand_Unity/Textures/AetherStrand_AlphaCard.png.meta',
      name: 'AetherStrand_AlphaCard.png.meta',
      category: 'unity',
      language: 'yaml',
      description: 'Deterministic Unity TextureImporter meta (Linear sRGBTexture: 0, Repeat wrapMode: 0).',
      content: generateTextureMeta('a3e710034a7b4f189c2e6b0f10000003', false),
    },
    {
      path: 'AetherStrand_Unity/Textures/AetherStrand_Normal.png.meta',
      name: 'AetherStrand_Normal.png.meta',
      category: 'unity',
      language: 'yaml',
      description: 'Deterministic Unity TextureImporter meta (NormalMap textureType: 1, Repeat wrapMode: 0).',
      content: generateTextureMeta('a3e710044a7b4f189c2e6b0f10000004', true),
    },
    {
      path: 'AetherStrand_Blender/PresetGeometry.json',
      name: 'PresetGeometry.json',
      category: 'blender',
      language: 'json',
      description: `Exact exported viewport geometry (${exportedLocks.length} volumetric locks in Blender Z-up / -Y front coordinates).`,
      content: generatePresetGeometryJson(presetId, physics, exportedLocks),
    },
    {
      path: 'AetherStrand_Blender/aetherstrand_hair_cards.py',
      name: 'aetherstrand_hair_cards.py',
      category: 'blender',
      language: 'python',
      description: 'Blender 4.x addon that auto-loads PresetGeometry.json, k-means clusters locks onto ~44 shared Hair_ bone chains, and exports Unity FBX (Linear VCol).',
      content: generateBlenderAddonPy(presetId),
    },
  ], pipeline);
}
