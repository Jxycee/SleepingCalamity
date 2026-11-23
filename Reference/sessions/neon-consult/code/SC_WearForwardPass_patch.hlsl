// Neon, exchange 3 (2026-11-22). Proposal, not applied. Part A replaces the RUINSKIN helper block; Part B replaces the
// RUINSKIN fragment block and adds the normalWS blend after InitializeInputData.
// ===== Part A =====
#if defined(_SC_RUINSKIN)
// Global ground-bake uniforms (outside UnityPerMaterial => SRP-Batcher safe)
TEXTURE2D(_SC_GroundHeightTex);  SAMPLER(sampler_SC_GroundHeightTex);
TEXTURE2D(_SC_GroundAlbedoTex);  SAMPLER(sampler_SC_GroundAlbedoTex);
float4 _SC_GroundBounds;        // (minX, minZ, 1/sizeX, 1/sizeZ)
float4 _SC_GroundHeightParams;  // (minY, maxY - minY, 1/width, 1/height)

float SCHash(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
float SCNoise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f); return lerp(lerp(SCHash(i), SCHash(i + float2(1, 0)), f.x), lerp(SCHash(i + float2(0, 1)), SCHash(i + float2(1, 1)), f.x), f.y); }
#endif

// ===== Part B (inside LitPassFragment) =====
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#if defined(_SC_RUINSKIN)
    half  scGroundBlend    = 0.0h;
    half3 scGroundNormalWS = half3(0.0h, 1.0h, 0.0h);
    {
        const half3 lumW = half3(0.2126h, 0.7152h, 0.0722h);
        float3 nW = normalize(input.normalWS); float3 pW = input.positionWS;
        float upF = saturate(nW.y); bool isFlat = abs(nW.y) > 0.7;
        float2 pl = isFlat ? pW.xz : float2(abs(nW.z) > abs(nW.x) ? pW.x : pW.z, pW.y);
        float stoneW = max(0.05, _SpecColor.x), mossAmt = _SpecColor.y, footH = _SpecColor.z, footDark = _SpecColor.w;
        half amt = _DetailAlbedoMapScale;

        // Macro + stone variation (slightly calmer per-stone contrast to reduce HF noise)
        float row = floor(pl.y / (stoneW * 0.5));
        float col = floor(pl.x / stoneW + SCHash(float2(row, 7.1)) * 5.0);
        float stoneA = SCHash(float2(col, row)), stoneB = SCHash(float2(col + 31.7, row + 11.3));
        float macro = (SCNoise(pl * (1.0 / 4.5)) - 0.5) * 0.30 + (SCNoise(pl * (1.0 / 1.3)) - 0.5) * 0.14;
        half3 tone = half3(1.0 + (stoneB - 0.5) * 0.16, 1.0, 1.0 - (stoneB - 0.5) * 0.20);
        surfaceData.albedo *= tone * (1.0h + (half)((stoneA - 0.5) * 0.22 + macro) * amt);

        float2 uv2 = float2(input.uv.x * 0.91 - input.uv.y * 0.41, input.uv.x * 0.41 + input.uv.y * 0.91) * 0.37 + float2(0.31, 0.17);
        half meanL = max(0.03h, dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv2, 11.0).rgb, lumW));
        half blotch = dot(SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv2, 4.5).rgb, lumW);
        surfaceData.albedo *= lerp(1.0h, clamp(blotch / meanL, 0.75h, 1.30h), 0.7h * amt);

    #if defined(_NORMALMAP)
        // Dialed back from 0.22h to 0.08h (and 2nd octave 0.5->0.25) to cut 2-3x HF spectral energy
        { float2 gp = pl * 17.0; const float e = 0.35;
          float g0 = SCNoise(gp) + 0.25 * SCNoise(gp * 2.3 + 5.0);
          float gx = SCNoise(gp + float2(e, 0)) + 0.25 * SCNoise((gp + float2(e, 0)) * 2.3 + 5.0);
          float gy = SCNoise(gp + float2(0, e)) + 0.25 * SCNoise((gp + float2(0, e)) * 2.3 + 5.0);
          half2 gr = (half2)(float2(gx - g0, gy - g0) / e) * 0.08h * _DetailNormalMapScale;
          surfaceData.normalTS = normalize(half3(surfaceData.normalTS.xy - gr, surfaceData.normalTS.z)); }
    #endif

        half h = 0.5h;
    #if defined(_PARALLAXMAP)
        h = SAMPLE_TEXTURE2D(_ParallaxMap, sampler_ParallaxMap, input.uv).g;
        surfaceData.albedo *= lerp(0.55h, 1.0h, smoothstep(0.03h, 0.30h, h));
        // Reduced HF white-edge sparkle (0.22->0.09)
        surfaceData.albedo *= 1.0h + smoothstep(0.80h, 0.97h, h) * (0.06h + 0.09h * (half)SCNoise(pl * 5.0));
    #endif

        // Clamp Megascans smoothness on dry masonry so stone/cobbles don't read glossy/plastic
        surfaceData.smoothness = min(surfaceData.smoothness, 0.28h);

        // --- Real ground-relative height + ground albedo/normal sample ---
        float2 gUV = (pW.xz - _SC_GroundBounds.xy) * _SC_GroundBounds.zw;
        bool hasGround = (_SC_GroundBounds.z > 0.0) && all(gUV >= 0.001) && all(gUV <= 0.999);

        float aboveGround = footH > 0.0 ? (pW.y - unity_ObjectToWorld._m13) : 99.0;
        half4 groundAlbedoSmooth = half4(surfaceData.albedo, surfaceData.smoothness);

        if (hasGround)
        {
            float h01 = SAMPLE_TEXTURE2D_LOD(_SC_GroundHeightTex, sampler_SC_GroundHeightTex, gUV, 0.0).r;
            float groundY = _SC_GroundHeightParams.x + h01 * _SC_GroundHeightParams.y;
            aboveGround = pW.y - groundY;

            // Finite-difference ground normal (only needed inside/near foot band)
            if (footH > 0.0 && aboveGround < footH * 1.4)
            {
                groundAlbedoSmooth = SAMPLE_TEXTURE2D_LOD(_SC_GroundAlbedoTex, sampler_SC_GroundAlbedoTex, gUV, 0.0);
                float hR = SAMPLE_TEXTURE2D_LOD(_SC_GroundHeightTex, sampler_SC_GroundHeightTex, gUV + float2(_SC_GroundHeightParams.z, 0.0), 0.0).r;
                float hU = SAMPLE_TEXTURE2D_LOD(_SC_GroundHeightTex, sampler_SC_GroundHeightTex, gUV + float2(0.0, _SC_GroundHeightParams.w), 0.0).r;
                float stepX = _SC_GroundHeightParams.z / max(1e-5, _SC_GroundBounds.z);
                float stepZ = _SC_GroundHeightParams.w / max(1e-5, _SC_GroundBounds.w);
                float dhdx = ((hR - h01) * _SC_GroundHeightParams.y) / stepX;
                float dhdz = ((hU - h01) * _SC_GroundHeightParams.y) / stepZ;
                scGroundNormalWS = normalize(half3((half)(-dhdx), 1.0h, (half)(-dhdz)));
            }
        }

        half cav = smoothstep(0.03h, 0.30h, h);
        half lum1 = saturate(dot(surfaceData.albedo, lumW) * 3.0h);
        half foot = footH > 0.0 ? (1.0h - smoothstep(0.0h, (half)footH, (half)aboveGround)) : 0.0h;

        float field = upF * 1.5 + (h - 0.5) * 0.9 + (SCNoise(pl * 3.1) - 0.5) * 0.8 - 0.55 + foot * (1.0 - cav) * 0.9;
        half moss2 = smoothstep(0.55h, 0.95h, (half)field) * mossAmt;
        half3 mossC = half3(0.060h, 0.095h, 0.026h) * (0.55h + 0.9h * lum1) * (0.75h + 0.5h * (half)SCNoise(pl * 6.0));
        surfaceData.albedo = lerp(surfaceData.albedo, mossC, moss2 * 0.88h);
        surfaceData.smoothness = saturate(surfaceData.smoothness - moss2 * 0.20h);

        // Ground seam blend: noisy height + mortar-first creep, masked off undersides
        if (hasGround && footH > 0.0)
        {
            float seamNoise = (SCNoise(pW.xz * 2.2) - 0.5) * 0.35 + (h - 0.5) * 0.25;
            float noisyH = aboveGround + seamNoise * footH;
            half notUnderside = smoothstep(-0.35h, 0.15h, (half)nW.y);
            scGroundBlend = (1.0h - smoothstep(0.0h, (half)(footH * 0.85), (half)noisyH)) * notUnderside;

            surfaceData.albedo = lerp(surfaceData.albedo, groundAlbedoSmooth.rgb, scGroundBlend);
            surfaceData.smoothness = lerp(surfaceData.smoothness, min(groundAlbedoSmooth.a, 0.25h), scGroundBlend);
            // Subtle contact darkening at the very bottom (without the old +0.06 gloss boost)
            surfaceData.albedo *= 1.0h - foot * footDark * 0.30h;
            surfaceData.smoothness *= 1.0h - foot * 0.35h;
        }
        else
        {
            surfaceData.albedo *= 1.0h - foot * footDark * 0.55h;
            surfaceData.smoothness *= 1.0h - foot * 0.30h;
        }
    }
#endif

    // SC_WEAR vertex-colour masks (grime/moss/polish) ...

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

#if defined(_SC_RUINSKIN)
    // Blend world-space normal into ground normal to erase the hard intersection crease
    if (scGroundBlend > 0.001h)
    {
        inputData.normalWS = normalize(lerp(inputData.normalWS, scGroundNormalWS, scGroundBlend));
    }
#endif
