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
        _GroundMist ("Extra haze at the foot (thins upward)", Range(0, 0.5)) = 0.06
        _MistTop ("Mist reference height (world y)", Float) = -5
        _MistScale ("Mist falloff height (m)", Float) = 30
        _NoiseAmp ("Haze patch noise (+/-)", Range(0, 0.3)) = 0
        _NoiseLit ("Lit value noise (+/-)", Range(0, 0.2)) = 0
        _ShadowTint ("Shadow side sky tint", Color) = (0.30, 0.38, 0.48, 1)
        _HazeCool ("Cool sky haze (x fog colour)", Color) = (0.80, 0.92, 1.12, 1)
        _HazeCoolAmt ("Cool haze away from a low sun", Range(0, 1)) = 0.7
        // A12 r1 (town-art-leftovers): per-material key/fill controls for the tall masses (mountain, windmill). The defaults reproduce the old look exactly.
        _KeyWrap ("Key light slope (wrapped Lambert)", Range(0, 1.5)) = 0.7
        _KeyBias ("Key light bias (wrapped Lambert)", Range(-0.3, 0.5)) = 0.15
        _KeyBoost ("Key light boost", Range(0, 3)) = 1
        _SkyFillDir ("Cool directional sky fill (0 = SH as before)", Range(0, 1)) = 0
        _FillBoost ("Sky fill multiplier (shade side of a tall mass)", Range(0, 8)) = 1
        _HazeTop ("Haze reduction at the top of a tall mass (0 = off)", Range(0, 1)) = 0
        _HazeTopHeight ("Height (world y) where that reduction is full (0 = off)", Float) = 0
        // T11: world-XZ colour patchwork (dark wood patches, sunlit field patches, fine tree grain) so a hill band is not one flat painted green. 0 = off (the old look).
        _Patch ("Patchwork strength (0 = off)", Range(0, 1)) = 0
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
            float _MistTop, _MistScale;
            half _NoiseAmp, _NoiseLit;
            half4 _ShadowTint, _HazeCool;
            half _HazeCoolAmt;
            half _KeyWrap, _KeyBias, _KeyBoost, _SkyFillDir, _FillBoost, _HazeTop;
            float _HazeTopHeight;
            half _Patch;
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

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR0; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float _SC_BackdropDebug;
            float Hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float VNoise(float2 p)
            {
                float2 i0 = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i0), Hash21(i0 + float2(1, 0)), f.x), lerp(Hash21(i0 + float2(0, 1)), Hash21(i0 + float2(1, 1)), f.x), f.y);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.color = i.color;   // vertex colour: rgb multiplies the tint (strata, lit/shade bake), alpha < 1 adds haze (foot fade); meshes without colours read white
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
                #endif
                c.rgb *= i.color.rgb;
                half3 patchFinal = half3(1, 1, 1);
                if (_Patch > 0.0)   // T11: wood and field patches (about 260 m, 120 m) and a fine grain (about 18 m) from the world position, so a ridge reads as land, not paint
                {
                    float2 q = i.positionWS.xz;
                    float f1 = VNoise(q / 260.0 + 3.0) * 0.6 + VNoise(q / 70.0 + 9.0) * 0.4;
                    float f3 = VNoise(q / 18.0 + 21.0);
                    float wood = smoothstep(0.46, 0.58, f1);
                    float field = smoothstep(0.55, 0.68, VNoise(q / 120.0 + 40.0)) * (1.0 - wood);
                    half3 pc = lerp(half3(1, 1, 1), half3(0.72, 0.82, 0.68), wood);
                    pc = lerp(pc, half3(1.18, 1.14, 0.84), field);
                    pc *= 1.0 + (f3 - 0.5) * 0.55;
                    c.rgb *= lerp(half3(1, 1, 1), pc, _Patch);
                    patchFinal = lerp(half3(1, 1, 1), pc, _Patch * 0.35);   // and again on the hazed result, or the haze (0.6-0.9) washes the patches out
                }
                float3 n = normalize(i.normalWS);
                n = IS_FRONT_VFACE(face, n, -n);
                Light l = GetMainLight();
                float ndl = dot(n, l.direction);
                half sun = saturate(ndl * _KeyWrap + _KeyBias);             // default 0.7/0.15: less wrap than before, faces turned from the sun fall darker
                half3 sky = SampleSH(float3(0, 1, 0));
                half3 fill = SampleSH(n);
                // cool sky fill by the normal's up component (up-facing lighter, steep faces darker); 0 keeps the plain SH fill
                half3 skyDir = sky * half3(0.62, 0.78, 1.1) * lerp(0.45, 1.1, smoothstep(0.1, 0.9, n.y));
                fill = lerp(fill, skyDir, _SkyFillDir) * _FillBoost;
                half3 lit = c.rgb * (l.color * (sun * _KeyBoost) + fill + _ShadowTint.rgb * sky * (1.0 - sun));
                float3 V = normalize(i.positionWS - GetCameraPositionWS());
                float d = distance(i.positionWS, GetCameraPositionWS());
                half h = lerp(_HazeNear, _HazeFar, saturate((d - _DistNear) / max(1.0, _DistFar - _DistNear)));
                // vertical gradient: mist holds at the foot and thins smoothly with height, so ridge and tree tops stand crisper against the sky
                h += _GroundMist * (exp(-clamp((i.positionWS.y - _MistTop) / max(1.0, _MistScale), -1.0, 6.0)) - 0.3);
                // two octaves of world-XZ value noise (~150 m and ~40 m) break the ring fields into patches of light and mist
                if (_HazeTopHeight > 0.0)   // less haze on the upper 60% of a tall mass (the foot keeps its vertex-alpha mist)
                    h *= 1.0 - _HazeTop * saturate((i.positionWS.y - 0.4 * _HazeTopHeight) / (0.6 * _HazeTopHeight));
                float nz = VNoise(i.positionWS.xz / 150.0) * 0.6 + VNoise(i.positionWS.xz / 40.0 + 17.0) * 0.4 - 0.5;
                h = saturate(h + nz * 2.0 * _NoiseAmp + (1.0 - i.color.a) * 0.7);
                lit *= 1.0 + nz * 2.0 * _NoiseLit;
                // haze hue: warm fog colour looking toward a low sun, cooler sky haze looking away or sideways (midday sun stays as it was)
                half lowSun = 1.0 - saturate(l.direction.y * 1.5);
                half toSun = pow(saturate(dot(V, l.direction)), 2.0) * 0.7 + 0.3;
                half3 haze = unity_FogColor.rgb * _HazeTint.rgb;
                half3 cool = _HazeCool.rgb / max(0.2, dot(_HazeCool.rgb, half3(0.299, 0.587, 0.114)));   // luminance-normalised: hue only
                haze = lerp(haze, lerp(haze * cool, haze, toSun), _HazeCoolAmt * lowSun);
                if (_SC_BackdropDebug > 0.5)   // capture-only debug (BackdropDebugCapture): 1 albedo, 2 key, 3 fill, 4 normal, 5 haze, 6 lit
                {
                    half3 dbg = _SC_BackdropDebug < 1.5 ? c.rgb : _SC_BackdropDebug < 2.5 ? l.color * sun : _SC_BackdropDebug < 3.5 ? fill : _SC_BackdropDebug < 4.5 ? n * 0.5 + 0.5 : _SC_BackdropDebug < 5.5 ? h.xxx : lit;
                    return half4(dbg, 1.0);
                }
                return half4(lerp(lit, haze, h) * patchFinal, 1.0);
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
