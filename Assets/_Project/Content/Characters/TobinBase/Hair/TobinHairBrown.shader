// Flat-lit brown for the strand hair experiment. The package's default hair graphs colour every strand by its index (debug rainbow) and expose no colour,
// so this derives from the package's unlit pass: base colour + per-strand tint + half-lambert from a fixed key direction. No shadows, no spec (out of scope).
Shader "SleepingCalamity/TobinHairBrown"
{
	Properties
	{
		_BaseColor ("Base colour", Color) = (0.17, 0.105, 0.06, 1)
		_Tint ("Per-strand tint", Range(0, 1)) = 0.35
	}

	HLSLINCLUDE

	#pragma target 5.0

	#pragma multi_compile_local_vertex HAIR_VERTEX_STATIC HAIR_VERTEX_LIVE
	#pragma multi_compile_vertex PROCEDURAL_INSTANCING_ON

	#pragma instancing_options assumeuniformscaling nomatrices nolodfade nolightprobe nolightmap procedural:HairVertexInstancingSetup

	#include "Packages/com.unity.demoteam.hair/Runtime/HairMaterialCommonUnlit.hlsl"

	float4 _BaseColor;
	float _Tint;

	struct BrownVaryings
	{
		float4 positionCS : SV_POSITION;
		float3 normalWS : TEXCOORD0;
		float strandRand : TEXCOORD1;
		float alongStrand : TEXCOORD2;
	};

	BrownVaryings BrownVert(UnlitAttribs IN)
	{
		UNITY_SETUP_INSTANCE_ID(IN);
		HairVertexData v = GetHairVertex(IN.packedID, IN.staticPositionOS, IN.staticNormalOS, IN.staticTangentOS);
		BrownVaryings OUT;
		OUT.positionCS = TransformObjectToHClip(v.surfacePosition);
		OUT.normalWS = normalize(TransformObjectToWorldNormal(v.surfaceNormal));
		OUT.strandRand = v.strandIndexColor.r;
		OUT.alongStrand = v.surfaceUV.y;
		return OUT;
	}

	float4 BrownFrag(BrownVaryings IN) : SV_Target
	{
		float3 L = normalize(float3(0.35, 0.8, 0.5));
		float lit = 0.35 + 0.65 * saturate(dot(normalize(IN.normalWS), L) * 0.5 + 0.5);
		float3 c = _BaseColor.rgb * lerp(1.0 - _Tint, 1.0 + _Tint, IN.strandRand);
		c *= lerp(0.8, 1.15, saturate(IN.alongStrand));   // slightly lighter towards the tips
		return float4(c * lit, 1.0);
	}

	ENDHLSL

	SubShader
	{
		Tags { "RenderType" = "Opaque" "DisableBatching" = "True" "Queue" = "Geometry" }
		ZTest LEqual
		ZWrite On
		Cull Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex BrownVert
			#pragma fragment BrownFrag
			ENDHLSL
		}
	}

	Fallback Off
}
