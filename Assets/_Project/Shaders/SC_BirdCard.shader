// Distant bird / butterfly / bee card with a vertex wing flap (Hollowmere ambient life). Unlit, fogged, two-sided, no shadows.
// The mesh (AmbientCreatures.BuildCardMesh) has the wing span along X, body forward +Z, and uv.x = |x| (0 on the body, 1 at the wing tips), so the flap
// lifts each vertex by sin(time) * uv.x^2. The phase comes from the object's world position, so cards sharing one material do not flap in step.
// T11: the card is a bird wherever the player stands, with no capture-camera special case. (1) a resting dihedral lifts the wing tips into a shallow V, so the unflapped baked
// pose reads as a gliding bird from the side and from below, not a flat grey sheet; (2) a screen-door dither fades the card out between _FadeNear and _FadeFar metres of the
// camera, so a card never fills the frame as a huge flat wing and the bird thins out as it comes close.
// _Fold tucks the wings in and stops the flap (a perched bird); it is set per card through a MaterialPropertyBlock.
Shader "SleepingCalamity/BirdCard"
{
    Properties
    {
        _Color ("Colour", Color) = (0.08, 0.08, 0.09, 1)
        _TipColor ("Wing-tip colour (outer wings; alpha 0 = same as Colour)", Color) = (0, 0, 0, 0)
        _Belly ("Underside tone (multiplies Colour at the wing roots)", Range(0, 1.5)) = 1.0
        _FlapSpeed ("Flap speed (rad/s)", Float) = 14
        _FlapAmp ("Flap amplitude (fraction of the wing length)", Float) = 0.45
        _Fold ("Wings folded (0 open, 1 tucked)", Range(0, 1)) = 0
        _Dihedral ("Resting wing-tip lift (fraction of the wing length)", Float) = 0.22
        _FadeNear ("Fully faded at (m from the camera)", Float) = 5
        _FadeFar ("Fully visible from (m from the camera)", Float) = 22
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _TipColor;
                half _Belly;
                float _FlapSpeed;
                float _FlapAmp;
                half _Fold;
                float _Dihedral, _FadeNear, _FadeFar;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fogFactor : TEXCOORD1; float camDist : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 p = i.positionOS.xyz;
                float4x4 m = GetObjectToWorldMatrix();
                float phase = m._m03 * 1.7 + m._m13 * 0.9 + m._m23 * 2.3;
                float w = i.uv.x;
                float flap = sin(_Time.y * _FlapSpeed + phase) * _FlapAmp * w * w * (1.0 - _Fold);
                p.y += flap + _Dihedral * w * w * (1.0 - _Fold);   // the resting V
                p.x *= lerp(1.0, 1.0 - 0.7 * w, _Fold);        // folded: the tips draw in towards the body
                p.z += (1.0 - _Fold) * flap * -0.25;           // the tip trails the beat a little
                VertexPositionInputs v = GetVertexPositionInputs(p);
                o.positionCS = v.positionCS;
                o.camDist = distance(v.positionWS, GetCameraPositionWS());
                o.uv = i.uv;
                o.fogFactor = ComputeFogFactor(v.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(saturate((i.camDist - _FadeNear) / max(0.01, _FadeFar - _FadeNear)) - InterleavedGradientNoise(i.positionCS.xy, 0) * 0.999);
                half3 base = lerp(_Color.rgb, _TipColor.rgb, _TipColor.a * smoothstep(0.35h, 0.95h, i.uv.x));   // two-tone wings: body and inner wing in Colour, outer wing in the tip colour
                half3 c = base * lerp(1.0h, _Belly, saturate(i.uv.x));   // wings a touch lighter or darker than the body
                c = MixFog(c, i.fogFactor);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
