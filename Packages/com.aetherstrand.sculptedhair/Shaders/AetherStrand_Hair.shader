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
        _AlphaClip ("Alpha Cutoff", Range(0.05, 0.85)) = 0.36
        [Header(Web Light Rig Parity)]
        _UseWebRig ("Use fixed web 3-light rig (parity)", Float) = 0
        _KeyDirW ("Key dir", Vector) = (0.3,0.7,0.6,0)
        _KeyColW ("Key col", Vector) = (1,1,1,0)
        _FillDirW ("Fill dir", Vector) = (-0.5,0.3,0.5,0)
        _FillColW ("Fill col", Vector) = (0.5,0.5,0.5,0)
        _RimDirW ("Rim dir", Vector) = (0.2,0.5,-0.7,0)
        _RimColW ("Rim col", Vector) = (0.7,0.7,0.7,0)
        _AmbColW ("Ambient col", Vector) = (0.5,0.5,0.5,0)
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
            float  _UseWebRig;
            float4 _KeyDirW; float4 _KeyColW; float4 _FillDirW; float4 _FillColW; float4 _RimDirW; float4 _RimColW; float4 _AmbColW;
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
            if (vertColor.a > 0.5 && t > 0.58)
            {
                float2 strandUv = float2(uv.x * 0.92 + vertColor.b * 3.13, t);
                float cardAlpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, strandUv).r;
                float edgeDist = min(abs(uv.x - 0.34), abs(uv.x - 0.66));
                float lateralFray = smoothstep(0.0, 0.075, edgeDist);
                float tipRamp = smoothstep(0.58, 0.98, t);
                float combinedAlpha = cardAlpha * lerp(1.0, 0.44 + 0.56 * lateralFray, tipRamp);
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
                float2 uv1        : TEXCOORD1; // x = scalpRoot
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
                float  scalpRoot   : TEXCOORD7;
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
                output.scalpRoot   = input.uv1.x;
                return output;
            }

            half4 Frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                // ---- Exact port of the AetherStrand web (three.js) hair fragment shader ----
                AetherApplyTipAlphaClip(input.uv, input.vertColor, input.positionCS.xy);

                float t = saturate(input.uv.y);
                float seed = input.vertColor.b;
                float layerAOv = input.vertColor.g;
                float scalpRoot = input.scalpRoot;
                float2 sampleUv = float2(input.uv.x * 0.92 + seed * 3.13, t);

                float faceSign = isFrontFace ? 1.0 : -1.0;
                float3 lockN = normalize(input.normalWS) * faceSign;
                float3 headN = normalize(input.positionWS - _HeadCenterWS.xyz + float3(0.0, 0.0001, 0.0));
                float3 baseN = normalize(lerp(lockN, headN, 0.42));

                float grooveAo = SAMPLE_TEXTURE2D(_GrooveAoMap, sampler_GrooveAoMap, sampleUv).r;
                float3 flowId  = SAMPLE_TEXTURE2D(_FlowIdMap, sampler_FlowIdMap, sampleUv).rgb;
                float3 normalTex = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, sampleUv).rgb * 2.0 - 1.0;

                float3 T = normalize(input.tangentWS);
                // Unity world space is the web's mirrored through X, so a cross product flips sign.
                float3 B = -normalize(cross(baseN, T));
                float3 perturbedN = normalize(baseN + B * normalTex.x * (0.32 * _GrooveDepth) + T * normalTex.y * 0.05);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float3 effRoot = lerp(_BaseColor.rgb, _RootColor.rgb, scalpRoot);
                float3 gradColor = t < 0.28
                    ? lerp(effRoot, _BaseColor.rgb, smoothstep(0.0, 0.28, t))
                    : lerp(_BaseColor.rgb, _TipColor.rgb, smoothstep(0.28, 1.0, t));

                float fiberTone = (flowId.r - 0.5) * 0.92 + (seed - 0.5) * 0.32;
                float3 albedo = clamp(gradColor * (1.0 + fiberTone * _StrandVariation), 0.0, 1.2);

                float rootOcclusion = lerp(1.0 - _RootAO * 0.72 * scalpRoot, 1.0, smoothstep(0.0, 0.22, t));
                float grooveCrevice = lerp(1.0, grooveAo, _GrooveDepth * 0.68);
                float domeGrad = lerp(0.84, 1.08, smoothstep(-0.55, 0.68, headN.y));
                float totalAO = rootOcclusion * grooveCrevice * lerp(0.72, 1.04, layerAOv) * domeGrad;

                // Light rig: web 3-light studio rig (_UseWebRig) or URP main light as key (game mode)
                float3 LKey, LFill, LRim, keyCol, fillCol, rimCol, ambCol;
                if (_UseWebRig > 0.5)
                {
                    LKey = normalize(_KeyDirW.xyz);  keyCol = _KeyColW.rgb;
                    LFill = normalize(_FillDirW.xyz); fillCol = _FillColW.rgb;
                    LRim = normalize(_RimDirW.xyz);  rimCol = _RimColW.rgb;
                    ambCol = _AmbColW.rgb;
                }
                else
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);
                    LKey = normalize(mainLight.direction);
                    keyCol = mainLight.color * mainLight.distanceAttenuation * lerp(0.55, 1.0, mainLight.shadowAttenuation) * 0.42;
                    LFill = normalize(float3(-LKey.x, 0.45 * LKey.y + 0.2, LKey.z)); fillCol = mainLight.color * 0.38 * 0.5;
                    LRim = normalize(float3(-LKey.x * 0.6, 0.65, -LKey.z)); rimCol = mainLight.color * 0.42 * 0.7;
                    ambCol = SampleSH(perturbedN) * 0.55;
                }

                float ndlKey  = saturate((dot(perturbedN, LKey) + 0.46) / 1.46);
                float ndlFill = saturate((dot(perturbedN, LFill) + 0.48) / 1.48);
                float ndlBack = saturate((dot(perturbedN, LRim) + 0.46) / 1.46);
                float skyLift = saturate(baseN.y * 0.5 + 0.5);

                float3 diffuse = albedo * (
                    ambCol * (0.42 + 0.24 * skyLift) +
                    keyCol * ndlKey * 0.88 +
                    fillCol * ndlFill * 0.40 +
                    rimCol * ndlBack * 0.56) * totalAO;

                float shiftTex = (flowId.b - 0.5) * 0.38 + (seed - 0.5) * 0.12;
                float3 t1 = AS_ShiftTangent(T, perturbedN, _PrimaryShift + shiftTex);
                float3 t2 = AS_ShiftTangent(T, perturbedN, _SecondaryShift + shiftTex * 1.2);

                float fiberCrest = smoothstep(0.42, 0.92, grooveAo);
                float microGlint = 0.74 + 0.26 * smoothstep(0.28, 0.86, flowId.r);
                float specMask = smoothstep(0.08, 0.24, t) * (0.24 + 0.76 * fiberCrest) * microGlint * lerp(0.62, 1.0, layerAOv);
                float keyFacing  = smoothstep(-0.14, 0.48, dot(baseN, LKey));
                float backFacing = smoothstep(-0.14, 0.48, dot(baseN, LRim));

                float spec1 = AS_StrandSpecular(t1, V, LKey, _PrimaryPower) * _PrimaryIntensity;
                float spec2 = AS_StrandSpecular(t2, V, LKey, _SecondaryPower) * _SecondaryIntensity;
                float backSpec = AS_StrandSpecular(t1, V, LRim, _PrimaryPower * 0.78) * _PrimaryIntensity * 0.54;

                float3 spec1Tint = lerp(albedo * 1.55, _PrimarySpecColor.rgb, 0.48);
                float3 spec2Tint = lerp(albedo * 1.35, _SecondarySpecColor.rgb, 0.48);
                float3 specular = (
                    (spec1Tint * spec1 * 0.56 + spec2Tint * spec2 * 0.44) * keyCol * keyFacing +
                    spec1Tint * backSpec * rimCol * backFacing) * specMask * rootOcclusion;

                float fresnel = pow(1.0 - max(0.0, dot(baseN, V)), _RimPower);
                float backGate = pow(max(0.0, dot(V, -LRim)), 1.4) * 0.45 + 0.55;
                float tipWeight = lerp(0.22, 1.0, smoothstep(0.28, 0.94, t));
                float3 rimTint = lerp(albedo * 1.65, rimCol, 0.30);
                float3 rimScatter = rimTint * fresnel * backGate * tipWeight * _RimIntensity * 0.42 * lerp(0.72, 1.0, layerAOv);

                float3 finalColor = diffuse + specular + rimScatter;
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
    // Built-in Render Pipeline fallback SubShader (prevents solid-magenta rendering
    // in non-URP or scratch test projects while keeping URP 17.6 SubShader #1 primary)
    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Cull Off

        Pass
        {
            Name "BuiltInForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _GrooveAoMap;
            float4 _GrooveAoMap_ST;
            sampler2D _FlowIdMap;
            sampler2D _NormalMap;
            sampler2D _AlphaMap;

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
                float3 worldN    : TEXCOORD2;
                float3 worldT    : TEXCOORD3;
                float3 worldB    : TEXCOORD4;
                float4 vertColor : TEXCOORD5;
            };

            float3 AS_BiShiftTangent(float3 T, float3 N, float shift)
            {
                return normalize(T + shift * N);
            }

            float AS_BiStrandSpec(float3 T, float3 V, float3 L, float exponent)
            {
                float3 H = normalize(L + V);
                float dotTH = dot(T, H);
                float sinTH = sqrt(max(0.0, 1.0 - dotTH * dotTH));
                float dirAtten = smoothstep(-1.0, 0.0, dotTH);
                return dirAtten * pow(sinTH, exponent);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.worldT = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldB = cross(o.worldN, o.worldT) * v.tangent.w;
                o.uv = TRANSFORM_TEX(v.uv, _GrooveAoMap);
                o.vertColor = v.color;
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float t = saturate(i.uv.y);
                #ifdef _USE_ALPHA_CLIP
                if (i.vertColor.a > 0.5 && t > 0.70)
                {
                    float2 strandUv = float2(i.uv.x * 0.65 + i.vertColor.b * 3.13, t);
                    float cardAlpha = tex2D(_AlphaMap, strandUv).r;
                    float edgeDist = min(abs(i.uv.x - 0.34), abs(i.uv.x - 0.66));
                    float lateralFray = smoothstep(0.0, 0.06, edgeDist);
                    float tipRamp = smoothstep(0.72, 0.98, t);
                    float combinedAlpha = cardAlpha * lerp(1.0, 0.52 + 0.48 * lateralFray, tipRamp);
                    clip(combinedAlpha - _AlphaClip);
                }
                #endif

                float faceSign = facing >= 0 ? 1.0 : -1.0;
                float3 N = normalize(i.worldN * faceSign);
                if (_HeadCenterBlend > 0.001)
                {
                    float3 radial = i.worldPos - _HeadCenterWS.xyz;
                    if (dot(radial, radial) > 1e-6)
                        N = normalize(lerp(N, normalize(radial), _HeadCenterBlend));
                }

                float3 B = normalize(i.worldB);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightCol = _LightColor0.rgb;
                if (dot(lightCol, lightCol) < 1e-4) lightCol = float3(1.0, 0.97, 0.94);

                float grooveAO = tex2D(_GrooveAoMap, i.uv).r;
                float3 flowId  = tex2D(_FlowIdMap, i.uv).rgb;

                float3 albedo = lerp(_RootColor.rgb, _BaseColor.rgb, smoothstep(0.0, 0.45, t));
                albedo = lerp(albedo, _TipColor.rgb, smoothstep(0.45, 1.0, t));
                float varFactor = 1.0 + ((flowId.r - 0.5) * 0.6 + (i.vertColor.b - 0.5) * 0.4) * _StrandVariation * 2.0;
                albedo *= saturate(varFactor);

                float rootShade = lerp(1.0 - _RootAO, 1.0, smoothstep(0.0, 0.36, t));
                float grooveShade = lerp(1.0, grooveAO, _GrooveDepth);
                float ao = rootShade * grooveShade * lerp(0.55, 1.0, i.vertColor.g);

                float wrapNdotL = saturate((dot(N, L) + 0.45) / 1.45);
                float3 ambient = max(ShadeSH9(half4(N, 1.0)), float3(0.22, 0.22, 0.24));
                float3 diffuse = albedo * (ambient + lightCol * wrapNdotL * 0.92) * ao;

                float shiftNoise = (flowId.b - 0.5) * 0.25;
                float3 T1 = AS_BiShiftTangent(B, N, _PrimaryShift + shiftNoise);
                float3 T2 = AS_BiShiftTangent(B, N, _SecondaryShift + shiftNoise);
                float spec1 = AS_BiStrandSpec(T1, V, L, _PrimaryPower) * _PrimaryIntensity;
                float spec2 = AS_BiStrandSpec(T2, V, L, _SecondaryPower) * _SecondaryIntensity;
                float3 spec = (_PrimarySpecColor.rgb * spec1 + _SecondarySpecColor.rgb * spec2) * lightCol * saturate(dot(N, L) + 0.35) * ao;

                float fresnel = pow(1.0 - saturate(dot(N, V)), _RimPower);
                float3 rim = _TipColor.rgb * fresnel * _RimIntensity * lightCol * (0.4 + 0.6 * t);

                return fixed4(diffuse + spec + rim, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Name "BuiltInShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            Cull Off

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature_local _USE_ALPHA_CLIP
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _AlphaMap;
            float4 _GrooveAoMap_ST;
            float _AlphaClip;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv        : TEXCOORD1;
                float4 vertColor : TEXCOORD2;
            };

            v2f vertShadow(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _GrooveAoMap);
                o.vertColor = v.color;
                return o;
            }

            float4 fragShadow(v2f i) : SV_Target
            {
                #ifdef _USE_ALPHA_CLIP
                float t = saturate(i.uv.y);
                if (i.vertColor.a > 0.5 && t > 0.70)
                {
                    float2 strandUv = float2(i.uv.x * 0.65 + i.vertColor.b * 3.13, t);
                    float cardAlpha = tex2D(_AlphaMap, strandUv).r;
                    clip(cardAlpha - _AlphaClip);
                }
                #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"

}
