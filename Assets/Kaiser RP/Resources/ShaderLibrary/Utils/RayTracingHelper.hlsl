#ifndef KAISER_RAYTRACING_HELPER
#define KAISER_RAYTRACING_HELPER

#define K_MISS_SHADER_PT_SCATTER_RAY_INDEX  0
#define K_MISS_SHADER_PT_SHADOW_RAY_INDEX  1

#define SELECT(a, b, c) ((a) ? (b):(c))
#include "Random.hlsl"
#include "MathConstant.hlsl"
#include "SpaceTransforms.hlsl"
#include "UnityRaytracingMeshUtils.cginc"

Texture2D _BRDF_LUT_Texture;
SamplerState sampler_point_clamp;
SamplerState sampler_bilinear_clamp;

RayDesc CreateNewRay(float3 origin, float3 direction, float tmin, float tmax)
{
    RayDesc ray;
    ray.Origin = origin;
    ray.Direction = direction;
    ray.TMin = tmin;
    ray.TMax = tmax;
    return ray;
}

float3 ComputeRayOrigin(float3 pos, float3 normal)
{

    const float origin = 1.f / 16.f;
    const float fScale = 3.f / 65536.f;
    const float iScale = 3 * 256.f;

    // Per-component integer offset to bit representation of fp32 position.
    int3 iOff = int3(normal * iScale);
    float3 iPos = asfloat(asint(pos) + (pos < 0.f ? - iOff:iOff));

    // Select per-component between small fixed offset or above variable offset depending on distance to origin.
    float3 fOff = normal * fScale;
    return abs(pos) < origin ? pos + fOff:iPos;
}

// --------------------------------------------
struct AttributeData
{
    float2 barycentrics;
};

struct Vertex
{
    float3 position;
    float3 normal;
    float3 tangent;
    float2 uv;
};

Vertex FetchVertex(uint vertexIndex)
{
    Vertex v;
    v.position = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributePosition);
    v.normal = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeNormal);
    v.tangent = UnityRayTracingFetchVertexAttribute3(vertexIndex, kVertexAttributeTangent);
    v.uv = UnityRayTracingFetchVertexAttribute2(vertexIndex, kVertexAttributeTexCoord0);
    return v;
}

Vertex InterpolateVertices(Vertex v0, Vertex v1, Vertex v2, float3 barycentrics)
{
    Vertex v;
    #define INTERPOLATE_ATTRIBUTE(attr) v.attr = v0.attr * barycentrics.x + v1.attr * barycentrics.y + v2.attr * barycentrics.z
    INTERPOLATE_ATTRIBUTE(position);
    INTERPOLATE_ATTRIBUTE(normal);
    INTERPOLATE_ATTRIBUTE(tangent);
    INTERPOLATE_ATTRIBUTE(uv);
    return v;
}

float3 UnpackNormalmapRGorAG(float4 packednormal)
{
    // This do the trick
    packednormal.x *= packednormal.w;

    float3 normal;
    normal.xy = packednormal.xy * 2 - 1;
    normal.z = sqrt(1 - saturate(dot(normal.xy, normal.xy)));
    return normal;
}
inline float3 UnpackNormal(float4 packednormal)
{
    #if defined(UNITY_NO_DXT5nm)
        return packednormal.xyz * 2 - 1;
    #else
        return UnpackNormalmapRGorAG(packednormal);
    #endif
}



#endif