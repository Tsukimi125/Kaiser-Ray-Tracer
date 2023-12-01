#ifndef KAISER_LIT
#define KAISER_LIT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#pragma shader_feature_raytracing _NORMALMAP
#pragma shader_feature_raytracing _METALLICGLOSSMAP
#pragma shader_feature_raytracing _EMISSION
#pragma shader_feature_raytracing _TRANSPARENT

float4 _Color;

Texture2D<float4> _MainTex;
float4 _MainTex_ST;
SamplerState sampler_MainTex;

Texture2D<float4> _BumpMap;
float4 _BumpMap_ST;
SamplerState sampler_BumpMap;

Texture2D<float4> _MetallicGlossMap;
float4 _MetallicGlossMap_ST;
SamplerState sampler_MetallicGlossMap;

float _Glossiness;
float _Metallic;
float _IOR;

Texture2D<float4> _EmissionTex;
float4 _EmissionTex_ST;
SamplerState sampler_EmissionTex;
float4 _EmissionColor;

float _ExtinctionCoefficient;

struct Attributes
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD;
    float4 tangent : TANGENT;
    float4 normal : NORMAL;
};

struct Varyings
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD;
    float3 normalWS: TEXCOORD1;
    float4 tangentWS: TEXCOORD2;	//A: sign
};

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
CBUFFER_END




#endif