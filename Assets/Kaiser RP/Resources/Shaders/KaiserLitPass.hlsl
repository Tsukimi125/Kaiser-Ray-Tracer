#ifndef KAISER_LIT
#define KAISER_LIT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// #pragma shader_feature_raytracing _NORMALMAP
// #pragma shader_feature_raytracing _METALLICGLOSSMAP
// #pragma shader_feature_raytracing _EMISSION
// #pragma shader_feature_raytracing _TRANSPARENT

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

// CBUFFER_START(UnityPerMaterial)
//     float4 _MainTex_ST;
//     half4 _BaseColor;
// CBUFFER_END

Varyings vert(Attributes v)
{
    Varyings o;
    // o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.normalWS = TransformObjectToWorldNormal(v.normal.xyz);
    o.tangentWS.xyz = TransformObjectToWorldDir(v.tangent.xyz);
    o.tangentWS.a = real(v.tangent.w) * GetOddNegativeScale();
    o.uv = v.uv;
    
    return o;
}

struct GBuffer
{
    float4 MRT0 : SV_Target0;
    float4 MRT1 : SV_Target1;
    float4 MRT2 : SV_Target2;
    float4 MRT3 : SV_Target3;
};

GBuffer frag(Varyings i) : SV_Target
{

    float3 albedo = _MainTex.Sample(sampler_MainTex, i.uv).rgb * _Color.rgb;

    float3 worldPos = TransformObjectToWorld(i.vertex.xyz).xyz;
    

    //normal
    float4 normalMap = _BumpMap.Sample(sampler_BumpMap, i.uv);
    float3 normalTS = UnpackNormalScale(normalMap, 1);
    float sgn = i.tangentWS.w;
    float3 bitangent = sgn * cross(i.normalWS.xyz, i.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(i.tangentWS.xyz, bitangent.xyz, i.normalWS.xyz);   //TBN矩阵
    i.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
    //pbr
    float3 metallic = _Metallic;
    float smoothness = _Glossiness;
    float3 emission = float3(0, 0, 0);
    
    #if _METALLICGLOSSMAP
        float4 metallicSmoothness = _MetallicGlossMap.SampleLevel(sampler_MetallicGlossMap, _MetallicGlossMap_ST.xy * v.uv + _MetallicGlossMap_ST.zw, 0);
        metallic = metallicSmoothness.xxx;
        smoothness *= metallicSmoothness.w;
    #endif

    #if _EMISSION
        emission = _EmissionColor * _EmissionTex.SampleLevel(sampler_EmissionTex, _EmissionTex_ST.xy * v.uv + _EmissionTex_ST.zw, 0).xyz;
    #endif

    #if _NORMALMAP
        localNormal = GetNormalTS(v.uv);
        worldNormal = normalize(mul(localNormal, TBN));
        N = worldNormal;
        T = normalize(T - dot(T, N) * N);
        Bi = normalize(cross(T, N));
        TBN = float3x3(T, Bi, N);
    #endif

    //gbuffer
    GBuffer gbuffer;
    gbuffer.MRT0 = float4(albedo, 0);
    gbuffer.MRT1 = float4(worldPos, 0);
    gbuffer.MRT2 = float4(i.normalWS, 0);
    gbuffer.MRT3 = float4(1.0 - smoothness, metallic.x, 0, 0);
    return gbuffer;
}

#endif