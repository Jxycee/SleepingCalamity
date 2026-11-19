// SleepingCalamity/WearLit forward pass: a copy of URP 17.6 LitForwardPass.hlsl with three edits, all marked SC_WEAR.
// The mesh vertex colour is the wear mask the B4 facade kit exports (sc_facade_kit.py, colour attribute "wear", linear 0-1):
//   R = grime/soot : albedo toward a dark warm grime colour, smoothness down (rougher)
//   G = moss       : albedo toward a muted moss green, smoothness down
//   B = polish     : hand-worn edges, smoothness up a little and albedo lighter
// Meshes without vertex colour read white (1,1,1) from Unity, so the wear is zeroed when all three channels are 1 (see SC_WEAR_MASK).
// The constants below are the whole tuning surface. Keep it simple: nothing here is per material.
#ifndef UNIVERSAL_FORWARD_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_LIT_PASS_INCLUDED

#define SC_GRIME_COLOR   half3(0.055, 0.045, 0.036)   // linear, dark warm soot
#define SC_GRIME_STRENGTH 0.85h                       // albedo lerp at mask 1
#define SC_GRIME_ROUGH    0.45h                       // smoothness removed at mask 1
#define SC_MOSS_COLOR    half3(0.075, 0.115, 0.03)    // linear, muted yellow-green
#define SC_MOSS_STRENGTH  0.80h
#define SC_POLISH_LIFT    1.18h                       // albedo gain at mask 1
#define SC_POLISH_SMOOTH  0.18h                       // smoothness added at mask 1

// _SC_WEARGB (the ruin shells' own materials, HollowmereBuilder.ShellMaterials): the same masks, strong enough to read at 10-20 m. Soot and char go nearly black
// (the shells' oak carries R 0.38, about 70% char after the remap, so its grain still shows through as dark brown), moss goes a dark olive-grey. Every other WearLit material keeps the constants above.
#if defined(_SC_WEARGB)
    #undef SC_GRIME_COLOR
    #undef SC_GRIME_STRENGTH
    #undef SC_GRIME_ROUGH
    #undef SC_MOSS_COLOR
    #undef SC_MOSS_STRENGTH
    #undef SC_POLISH_LIFT
    #define SC_GRIME_COLOR   half3(0.045, 0.032, 0.022)
    #define SC_GRIME_STRENGTH 1.0h
    #define SC_GRIME_ROUGH    0.15h
    #define SC_MOSS_COLOR    half3(0.066, 0.080, 0.036)      // round 7: olive-grey, dark green-brown (the 3b green read vivid in sun)
    #define SC_MOSS_STRENGTH  0.9h
    #define SC_POLISH_LIFT    1.25h
#endif

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    half4 color         : COLOR;                      // SC_WEAR
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    half4 wear                      : TEXCOORD11;     // SC_WEAR

    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
                           
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);  

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy, inputData.normalWS);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.wear = input.color;                        // SC_WEAR

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

#if defined(_SC_RUINSKIN)
// T23 ruin skin. Per material, through properties the URP Lit cbuffer already carries (no new cbuffer entries, so the SRP batcher keeps working):
//   _DetailAlbedoMapScale = strength of the colour variation (0..1, default 1), _DetailNormalMapScale = strength of the second-scale normal (0..1.5),
//   _SpecColor (set with SetVector, linear, the metallic workflow never reads it) = (stone width in m, moss amount 0..1, foot band height in m (0 = off), foot darkness 0..1).
// All of it is world-space or second-scale, so the 2 m texture repeat stops being the pattern the eye finds.
float SCHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float SCNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(SCHash(i), SCHash(i + float2(1, 0)), f.x), lerp(SCHash(i + float2(0, 1)), SCHash(i + float2(1, 1)), f.x), f.y);
}
#endif

// Used in Standard (Physically Based) shader
void LitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#if defined(_SC_PAINT)
    // SC paint mode: the neighbour variants paint shutters and doors as greyscale(painted_wood albedo) x paint / 0.17 (sc_facade_kit.py, linear), so one scan
    // carries ochre, oxblood or blue-green. _SpecColor (unused in the metallic workflow, but in the SRP-batcher cbuffer) holds paint / 0.17, set linear and raw.
    half3 paintTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
    surfaceData.albedo = dot(paintTex, half3(0.2126h, 0.7152h, 0.0722h)) * _SpecColor.rgb;
#endif

#if defined(_SC_RUINSKIN)
    {
        const half3 lumW = half3(0.2126h, 0.7152h, 0.0722h);
        float3 nW = normalize(input.normalWS);
        float3 pW = input.positionWS;
        float upF = saturate(nW.y);
        bool isFlat = abs(nW.y) > 0.7;
        float2 pl = isFlat ? pW.xz : float2(abs(nW.z) > abs(nW.x) ? pW.x : pW.z, pW.y);   // the surface's own plane in metres
        float stoneW = max(0.05, _SpecColor.x), mossAmt = _SpecColor.y, footH = _SpecColor.z, footDark = _SpecColor.w;
        half amt = _DetailAlbedoMapScale;

        // per-stone value and hue: courses half a stone high, a random bond offset per course
        float row = floor(pl.y / (stoneW * 0.5));
        float col = floor(pl.x / stoneW + SCHash(float2(row, 7.1)) * 5.0);
        float stoneA = SCHash(float2(col, row)), stoneB = SCHash(float2(col + 31.7, row + 11.3));
        float macro = (SCNoise(pl * (1.0 / 4.5)) - 0.5) * 0.30 + (SCNoise(pl * (1.0 / 1.3)) - 0.5) * 0.16;
        half3 tone = half3(1.0 + (stoneB - 0.5) * 0.20, 1.0, 1.0 - (stoneB - 0.5) * 0.24);
        surfaceData.albedo *= tone * (1.0h + (half)((stoneA - 0.5) * 0.30 + macro) * amt);

        // a second, blotch-only layer of the same scan (a high mip: no joints, so no ghost joint lines at another angle) and a grain normal from noise, not from the block texture
        float2 uv2 = float2(input.uv.x * 0.91 - input.uv.y * 0.41, input.uv.x * 0.41 + input.uv.y * 0.91) * 0.37 + float2(0.31, 0.17);
        half meanL = max(0.03h, dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv2, 11.0).rgb, lumW));
        half blotch = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv2, 4.5).rgb, lumW);
        surfaceData.albedo *= lerp(1.0h, clamp(blotch / meanL, 0.7h, 1.35h), 0.7h * amt);
    #if defined(_NORMALMAP)
        {
            float2 gp = pl * 17.0; const float e = 0.35;
            float g0 = SCNoise(gp) + 0.5 * SCNoise(gp * 2.3 + 5.0);
            float gx = SCNoise(gp + float2(e, 0)) + 0.5 * SCNoise((gp + float2(e, 0)) * 2.3 + 5.0);
            float gy = SCNoise(gp + float2(0, e)) + 0.5 * SCNoise((gp + float2(0, e)) * 2.3 + 5.0);
            half2 gr = (half2)(float2(gx - g0, gy - g0) / e) * 0.22h * _DetailNormalMapScale;
            // pl.x runs along the surface's tangent only for walls facing +-z/x and ground: good enough for a grain that has no direction
            surfaceData.normalTS = normalize(half3(surfaceData.normalTS.xy - gr, surfaceData.normalTS.z));
        }
    #endif

        // mortar depth and chipped edges from the scan's height: joints go dark, the tops of the stones lift
        half h = 0.5h;
    #if defined(_PARALLAXMAP)
        h = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, input.uv).g;
        surfaceData.albedo *= lerp(0.50h, 1.0h, smoothstep(0.03h, 0.30h, h));
        surfaceData.albedo *= 1.0h + smoothstep(0.78h, 0.97h, h) * (0.10h + 0.22h * (half)SCNoise(pl * 9.0));
    #endif
        half cav = smoothstep(0.03h, 0.30h, h);
        half lum1 = saturate(dot(surfaceData.albedo, lumW) * 3.0h);

        // moss: world-up faces first (ledges, wall tops), pulled into the low joints by the height, broken by noise; more in the joints at the foot
        float aboveGround = footH > 0.0 ? pW.y - unity_ObjectToWorld._m13 : 99.0;
        half foot = footH > 0.0 ? 1.0h - smoothstep(0.0h, (half)footH, (half)aboveGround) : 0.0h;
        float field = upF * 1.5 + (h - 0.5) * 0.9 + (SCNoise(pl * 3.1) - 0.5) * 0.8 - 0.55 + foot * (1.0 - cav) * 0.9;
        half moss2 = smoothstep(0.55h, 0.95h, (half)field) * mossAmt;
        half3 mossC = half3(0.060h, 0.095h, 0.026h) * (0.55h + 0.9h * lum1) * (0.75h + 0.5h * (half)SCNoise(pl * 6.0));
        surfaceData.albedo = lerp(surfaceData.albedo, mossC, moss2 * 0.88h);
        surfaceData.smoothness = saturate(surfaceData.smoothness - moss2 * 0.25h);

        // foot band: damp and dirty from the ground up, fading over footH
        surfaceData.albedo *= 1.0h - foot * footDark * 0.55h;
        surfaceData.smoothness = saturate(surfaceData.smoothness + foot * footDark * 0.06h);
    }
#endif

    // SC_WEAR: vertex-colour masks. Unity gives meshes with no colour (1,1,1,1), which must mean "no wear".
    half3 wm = input.wear.rgb;
    half hasWear = (wm.r + wm.g + wm.b) < 2.99h ? 1.0h : 0.0h;
    half grime = saturate(wm.r) * hasWear, moss = saturate(wm.g) * hasWear, polish = saturate(wm.b) * hasWear;
#if defined(_SC_WEARGB)
    // The masks are soft (a 0.4 char value darkened the sunlit stone by a third, which hides in the tonemapped frame): push them to clear patches.
    grime = smoothstep(0.12h, 0.55h, grime); moss = smoothstep(0.05h, 0.75h, moss);
    // keep the stone's own relief in the char and the moss (a flat colour read as paint): the target colour follows the texture's luminance
    half wLum = saturate(0.55h + 1.3h * dot(surfaceData.albedo, half3(0.2126h, 0.7152h, 0.0722h)));
    surfaceData.albedo = lerp(surfaceData.albedo, SC_GRIME_COLOR * wLum, grime * SC_GRIME_STRENGTH);
    surfaceData.albedo = lerp(surfaceData.albedo, SC_MOSS_COLOR * wLum, moss * SC_MOSS_STRENGTH);
#else
    surfaceData.albedo = lerp(surfaceData.albedo, SC_GRIME_COLOR, grime * SC_GRIME_STRENGTH);
    surfaceData.albedo = lerp(surfaceData.albedo, SC_MOSS_COLOR, moss * SC_MOSS_STRENGTH);
#endif
    surfaceData.albedo *= lerp(1.0h, SC_POLISH_LIFT, polish);
    surfaceData.smoothness = saturate(surfaceData.smoothness - grime * SC_GRIME_ROUGH - moss * 0.3h + polish * SC_POLISH_SMOOTH);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent());

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
