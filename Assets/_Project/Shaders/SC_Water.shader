// SleepingCalamity/Water: stylised-real URP water for the Hollowmere lake, stream pools and weir falls (Q10, 2026-10-31).
//
// What it is: the standard hand-written URP water pattern (depth-difference absorption + opaque-texture refraction + scrolling normal maps + Fresnel with
// the reflection probe + sun glint + depth-difference shoreline foam + flow-map style foam), one forward transparent pass, no GrabPass, no planar reflection.
// Practice checked before writing it: the Unity URP docs (the Scene Depth / Scene Color nodes need the depth + opaque texture options on the RP asset,
// both on in LookTest/BeautyCorner_RPAsset), Unity's own URP "Boat Attack" water (depth-gradient colour, refraction by normal offset, foam from depth),
// the Shader Graph water samples and the common community recipes (Ben Cloward / Alexander Ameye / Catlike Coding style: Beer-Lambert absorption, refracted
// scene sample guarded by a depth test, Schlick Fresnel, foam from the depth difference broken by noise). The HDRP Water System is HDRP only, paid assets are
// forbidden here, so this is the same pattern written by hand.
//
// Mesh contract (HollowmereWater builds the meshes):
//   uv        metres; v increases DOWNSTREAM (the flow direction), u across (lake: u = world x, v = -world z).
//   tangent   xyz = direction of increasing u, w = handedness so cross(normal, tangent) * w points along +v.
//   colour.r  flow foam 0..1 (1 on a fall, fading along the run-out below it).
//   colour.g  flow speed, 0..1 -> 0..4 x _FlowSpeed (0 = still lake, which only drifts).
//
// Look: shallows are clear and warm green over the bed (bed colour x per-metre absorption, a green scatter colour), deepening to a blue-green that never
// reaches cyan or black (the scatter colour is lit by the sky ambient + the sun, so it dims with the light). Foam is thin: a shoreline lace that laps in and out
// and is broken by noise (never a uniform ring), plus flow foam on falls and their run-out.
//
// Per-pixel cost: 2 normal-map taps, 2 scene-depth taps (water pixel + refracted), 1 opaque-colour tap, 1 reflection-probe lookup (two with probe blending),
// 1 shadow-cascade lookup (soft shadows: up to 9 taps at High), 5 value-noise evaluations (hash ALU, no texture), about 220 ALU. No per-light loop (the
// main light only), no additional lights, no GrabPass, no extra depth/shadow passes: the water neither writes depth nor casts shadows.
Shader "SleepingCalamity/Water"
{
    Properties
    {
        _NormalMap ("Water normal map (tileable)", 2D) = "bump" {}
        _NormalScale ("Normal strength", Range(0, 1.5)) = 0.2
        _Tile1 ("Detail layer 1: metres per tile", Float) = 7.0
        _Tile2 ("Detail layer 2: metres per tile", Float) = 3.1
        _FlowSpeed ("Flow speed (m/s at vertex colour G = 0.25)", Float) = 0.5
        _Drift ("Still-water drift (tiles/s)", Float) = 0.012
        _NormalFadeStart ("Normal detail starts fading (m)", Float) = 18
        _NormalFadeLen ("Normal detail fade length (m)", Float) = 130

        _ShallowColor ("Shallow scatter colour", Color) = (0.30, 0.40, 0.16, 1)
        _DeepColor ("Deep scatter colour", Color) = (0.015, 0.13, 0.15, 1)
        _Absorb ("Absorption per metre (rgb)", Color) = (0.85, 0.30, 0.42, 1)
        _Scatter ("Scatter build-up per metre", Float) = 0.55
        _DeepDepth ("Metres of water to reach the deep colour", Float) = 3.0
        _Refract ("Refraction strength", Float) = 0.06
        _EdgeFade ("Edge fade (m of depth)", Float) = 0.07

        _Roughness ("Perceptual roughness", Range(0.02, 0.6)) = 0.07
        _FresnelMin ("Reflectance at normal incidence", Range(0, 0.15)) = 0.025
        _ReflStrength ("Reflection strength", Range(0, 2)) = 1.0
        _SpecStrength ("Sun glint strength", Range(0, 4)) = 1.0

        _FoamColor ("Foam colour", Color) = (0.90, 0.94, 0.90, 1)
        _FoamWidth ("Shoreline foam width (m of depth)", Float) = 0.10
        _ShoreFoam ("Shoreline foam strength", Range(0, 1)) = 0.5
        _FoamScale ("Foam noise scale (1/m)", Float) = 1.3
        _LapSpeed ("Lapping speed (rad/s)", Float) = 0.9
        _FlowFoam ("Flow foam strength", Range(0, 1.5)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                half _NormalScale;
                float _Tile1, _Tile2, _FlowSpeed, _Drift, _NormalFadeStart, _NormalFadeLen;
                half4 _ShallowColor, _DeepColor, _Absorb;
                float _Scatter, _DeepDepth, _Refract, _EdgeFade;
                half _Roughness, _FresnelMin, _ReflStrength, _SpecStrength;
                half4 _FoamColor;
                float _FoamWidth, _ShoreFoam, _FoamScale, _LapSpeed, _FlowFoam;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0; half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : TEXCOORD2;
                half3 nWS : TEXCOORD3;
                half4 tWS : TEXCOORD4;
                float eyeDepth : TEXCOORD5;
                half fogFactor : TEXCOORD6;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = v.uv;
                o.color = v.color;
                o.nWS = TransformObjectToWorldNormal(v.normalOS);
                o.tWS = half4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.eyeDepth = -TransformWorldToView(p.positionWS).z;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float VNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }
            float2 Rot(float2 p, float a) { float s = sin(a), c = cos(a); return float2(c * p.x - s * p.y, s * p.x + c * p.y); }

            half4 frag(Varyings i) : SV_Target
            {
                float3 posWS = i.positionWS;
                float3 toPos = posWS - _WorldSpaceCameraPos;
                float dist = max(length(toPos), 0.001);
                float3 rayDir = toPos / dist;
                float3 V = -rayDir;
                float2 suv = GetNormalizedScreenSpaceUV(i.positionCS);
                float t = _Time.y;

                // ---- normal: two detail layers; flow from the mesh uv (v = downstream), the lake only drifts.
                float flow = i.color.g * 4.0 * _FlowSpeed;              // m/s: G 0.25 = _FlowSpeed
                float2 uvA = (i.uv - float2(0, flow * t)) / _Tile1 + float2(0.7, 0.3) * (_Drift * t);
                float2 uvB = Rot(i.uv - float2(0, flow * 1.31 * t), 0.55) / _Tile2 + float2(-0.4, 0.9) * (_Drift * 1.3 * t);
                float fade = 1.0 - 0.8 * saturate((dist - _NormalFadeStart) / _NormalFadeLen);
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvA), 1.0);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), 1.0);
                float2 nxy = (n1.xy + n2.xy) * (_NormalScale * fade);
                float3 N0 = normalize(i.nWS);
                float3 T = normalize(i.tWS.xyz);
                float3 B = cross(N0, T) * i.tWS.w;
                float3 N = normalize(T * nxy.x + B * nxy.y + N0);
                float NoV = saturate(dot(N, V));

                // ---- depth: how much water is behind this pixel
                float waterZ = i.eyeDepth;
                float cosF = max(dot(rayDir, -UNITY_MATRIX_V[2].xyz), 0.1);
                float sceneZ = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float rawThick = max(sceneZ - waterZ, 0.0) / cosF;
                float vertDepth = rawThick * abs(rayDir.y);                     // metres of water straight below this pixel

                // refraction: offset by the wave normal (view space), only where the refracted sample is still behind the water
                float3x3 V3 = (float3x3)UNITY_MATRIX_V;
                float2 off = (mul(V3, N).xy - mul(V3, N0).xy) * _Refract * saturate(rawThick * 1.5) / (1.0 + waterZ * 0.04);
                float2 ruv = suv + off;
                float zr = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (zr < waterZ) { ruv = suv; zr = sceneZ; }
                float thick = max(zr - waterZ, 0.0) / cosF;
                half3 bed = SampleSceneColor(ruv);

                // ---- light on the water body
                float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                Light L = GetMainLight(shadowCoord, posWS, half4(1, 1, 1, 1));
                half3 amb = SampleSH(half3(0, 1, 0));
                half3 lightIn = amb + L.color * (saturate(L.direction.y) * L.shadowAttenuation);

                half3 absorb = exp(-thick * _Absorb.rgb);
                half3 scatCol = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(thick / _DeepDepth)) * lightIn;
                half3 body = bed * absorb + scatCol * (1.0 - exp(-thick * _Scatter));

                // ---- Fresnel reflection (probe) + sun glint, softened with distance
                half F = _FresnelMin + (1.0 - _FresnelMin) * pow(1.0 - NoV, 5.0);
                half rough = lerp(_Roughness, 0.5, smoothstep(40.0, 220.0, dist));
                float3 R = reflect(rayDir, N);
                R.y = abs(R.y);
                half3 env = GlossyEnvironmentReflection(R, posWS, rough, 1.0h, suv) * _ReflStrength;
                float3 H = normalize(L.direction + V);
                float NoH = saturate(dot(N, H)), LoH = saturate(dot(L.direction, H)), NoL = saturate(dot(N, L.direction));
                float r2 = rough * rough * rough * rough;
                float d = NoH * NoH * (r2 - 1.0) + 1.00001;
                float spec = r2 / (d * d * max(0.1, LoH * LoH) * (rough * rough * 4.0 + 2.0));
                half3 glint = min(spec, 8.0) * L.color * (NoL * L.shadowAttenuation) * _SpecStrength * (1.0 - smoothstep(60.0, 250.0, dist) * 0.7);
                half3 col = body * (1.0 - F) + env * F + glint;

                // ---- shoreline foam: thin, laps in and out, broken by noise (never a uniform ring)
                float2 fp = posWS.xz;
                float phase = VNoise(fp * 0.7) * 6.2831 + dot(fp, float2(0.23, 0.17));
                float lap = 0.5 + 0.5 * sin(t * _LapSpeed + phase);
                float band = 1.0 - saturate(vertDepth / (_FoamWidth * (0.45 + 0.75 * lap)));
                float fn = VNoise(fp * _FoamScale + t * 0.03) * 0.65 + VNoise(fp * _FoamScale * 2.9 - t * 0.05) * 0.35;
                float shore = saturate((band - fn * 0.95 + 0.05) * 3.0) * _ShoreFoam;

                // ---- flow foam from vertex colour R: streaks that run downstream
                float fr = i.color.r;
                float2 fuv = float2(i.uv.x, i.uv.y - flow * t * 1.2);
                float streak = VNoise(float2(fuv.x * 1.8, fuv.y * 0.55)) * 0.6 + VNoise(float2(fuv.x * 4.7, fuv.y * 1.3 + 7.0)) * 0.4;
                float flowFoam = saturate((fr * 0.95 - streak * 1.1 + 0.05) * 2.2) * _FlowFoam * saturate(fr * 3.0);

                float foam = saturate(max(shore, flowFoam));
                half3 foamCol = _FoamColor.rgb * (amb * 0.9 + L.color * (saturate(L.direction.y) * L.shadowAttenuation * 0.85));
                col = lerp(col, foamCol, foam);

                half alpha = saturate(max(saturate(vertDepth / max(_EdgeFade, 0.001)), foam));
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
