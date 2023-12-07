#ifndef KAISER_LIT
#define KAISER_LIT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"





Texture2D<float4> _MainTex;

SamplerState sampler_MainTex;

Texture2D<float4> _BumpMap;

SamplerState sampler_BumpMap;

Texture2D<float4> _MetallicGlossMap;

SamplerState sampler_MetallicGlossMap;



Texture2D<float4> _EmissionTex;

SamplerState sampler_EmissionTex;


CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _MainTex_ST;
    float4 _BumpMap_ST;
    float4 _MetallicGlossMap_ST;
    
    float4 _EmissionTex_ST;
    float4 _EmissionColor;
    float _Glossiness;
    float _Metallic;
    float _IOR;
    float _ExtinctionCoefficient;
CBUFFER_END

struct Attributes
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD;
    float4 tangent : TANGENT;
    float4 normal : NORMAL;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD1;
    float2 uv : TEXCOORD;
    float3 normalWS: TEXCOORD2;
    float4 tangentWS: TEXCOORD3;	//A: sign

};



Varyings vert(Attributes v)
{
    // UNITY_SETUP_INSTANCE_ID(v);
    Varyings o;
    // o.vertex = TransformObjectToHClip(v.vertex.xyz);
    o.positionHCS = TransformObjectToHClip(v.vertex.xyz);
    o.positionWS = TransformObjectToWorld(v.vertex.xyz);
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

GBuffer frag(Varyings i)
{
    // UNITY_SETUP_INSTANCE_ID(i);

    float3 albedo = _MainTex.Sample(sampler_MainTex, i.uv).rgb * _Color.rgb;
    float3 worldPos = i.positionWS.xyz;
    
    //normal
    float3 normal;
    #if _NORMALMAP
        float4 normalMap = _BumpMap.Sample(sampler_BumpMap, i.uv);
        float3 normalTS = UnpackNormalScale(normalMap, 1);
        float sgn = i.tangentWS.w;
        float3 bitangent = sgn * cross(i.normalWS.xyz, i.tangentWS.xyz);
        half3x3 tangentToWorld = half3x3(i.tangentWS.xyz, bitangent.xyz, i.normalWS.xyz);   //TBN矩阵
        normal = TransformTangentToWorld(normalTS, tangentToWorld);
    #else
        normal = normalize(i.normalWS);
    #endif
    
    //pbr
    float3 metallic = _Metallic;
    float smoothness = _Glossiness;
    float3 emission = float3(0, 0, 0);

    #if _METALLICGLOSSMAP
        float4 metallicSmoothness = _MetallicGlossMap.SampleLevel(sampler_MetallicGlossMap, _MetallicGlossMap_ST.xy * i.uv + _MetallicGlossMap_ST.zw, 0);
        metallic = metallicSmoothness.xxx;
        smoothness *= metallicSmoothness.w;
    #endif

    #if _EMISSION
        emission = _EmissionColor * _EmissionTex.SampleLevel(sampler_EmissionTex, _EmissionTex_ST.xy * i.uv + _EmissionTex_ST.zw, 0).xyz;
    #endif


    //gbuffer
    GBuffer gbuffer;
    gbuffer.MRT0 = float4(albedo, 0);
    gbuffer.MRT1 = float4(i.normalWS, 0);
    gbuffer.MRT2 = float4(worldPos, 0);
    gbuffer.MRT3 = float4(1.0 - smoothness, metallic.x, 0, 0);
    return gbuffer;
}

#endif