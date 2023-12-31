#include "UnityRaytracingMeshUtils.cginc"
#include "RayPayload.hlsl"
#include "Utils/RayTracingHelper.hlsl"

#pragma raytracing test

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

float3 GetNormalTS(float2 uv)
{
    float4 map = _BumpMap.SampleLevel(sampler_BumpMap, _BumpMap_ST.xy * uv + _BumpMap_ST.zw, 0);
    return UnpackNormal(map);
}

[shader("closesthit")]
void ClosestHitMain(inout RayPayload payload:SV_RayPayload, AttributeData attribs:SV_IntersectionAttributes)
{
    float3 hitPoint = WorldRayOrigin() + WorldRayDirection() * RayTCurrent();
    const float hitDist = length(hitPoint - WorldRayOrigin());

    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    Vertex v0, v1, v2;
    v0 = FetchVertex(triangleIndices.x);
    v1 = FetchVertex(triangleIndices.y);
    v2 = FetchVertex(triangleIndices.z);
    float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);
    Vertex v = InterpolateVertices(v0, v1, v2, barycentricCoords);

    float3 localNormal = v.normal;
    bool isFrontFace = HitKind() == HIT_KIND_TRIANGLE_FRONT_FACE;
    localNormal = isFrontFace ? v.normal: - v.normal;
    float3 worldNormal = normalize(mul((float3x3)ObjectToWorld3x4(), float4(localNormal, 0.0)));
    
    // Construct TBN
    // float3 tangent = normalize(mul(v.tangent, (float3x3)WorldToObject()));
    // float3 N = worldNormal;
    // float3 T = normalize(tangent - dot(tangent, N) * N);
    // float3 Bi = normalize(cross(T, N));
    // float3x3 TBN = float3x3(T, Bi, N);
    

    // float3 albedo = _MainTex.SampleLevel(sampler_MainTex, _MainTex_ST.xy * v.uv + _MainTex_ST.zw, 0).xyz;
    float3 albedo = _Color.xyz * _MainTex.SampleLevel(sampler_MainTex, _MainTex_ST.xy * v.uv + _MainTex_ST.zw, 0).xyz;
    
    float pdf = 0;
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
        // Construct TBN
        // float3 tangent = normalize(mul(v.tangent, (float3x3)WorldToObject()));
        // float3 N = worldNormal;
        // float3 T = normalize(tangent - dot(tangent, N) * N);
        // float3 Bi = normalize(cross(T, N));
        // float3x3 TBN = float3x3(T, Bi, N);
        float3x3 TBN = BuildOrthonormalBasis(worldNormal);
        localNormal = GetNormalTS(v.uv);
        worldNormal = normalize(mul(localNormal, TBN));
    #endif

    payload.surfaceData.albedo = albedo;
    payload.surfaceData.normal = worldNormal;
    payload.surfaceData.metallic = metallic;
    payload.surfaceData.roughness = 1.0 - smoothness;
    payload.surfaceData.emission = emission;

    payload.t = RayTCurrent();
}


// [shader("closesthit")]
// void ClosestHitMain(inout PathPayload payload : SV_RayPayload, AttributeData attribs : SV_IntersectionAttributes)
// {
//     if (payload.bounceIndexOpaque == _PT_MaxBounceCount)
//     {
//         payload.bounceIndexOpaque = -1;
//         return;
//     }

//     uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());

//     Vertex v0, v1, v2;
//     v0 = FetchVertex(triangleIndices.x);
//     v1 = FetchVertex(triangleIndices.y);
//     v2 = FetchVertex(triangleIndices.z);

//     float3 barycentricCoords = float3(1.0 - attribs.barycentrics.x - attribs.barycentrics.y, attribs.barycentrics.x, attribs.barycentrics.y);

//     Vertex v = InterpolateVertices(v0, v1, v2, barycentricCoords);

//     bool isFrontFace = HitKind() == HIT_KIND_TRIANGLE_FRONT_FACE;

//     float3 localNormal = isFrontFace ? v.normal : - v.normal;

//     float3 worldNormal = normalize(mul(localNormal, (float3x3)WorldToObject()));

//     float3 worldPosition = mul(ObjectToWorld(), float4(v.position, 1)).xyz;

//     // Bounced ray origin is pushed off of the surface using the face normal (not the interpolated normal).
//     float3 e0 = v1.position - v0.position;
//     float3 e1 = v2.position - v0.position;

//     float3 worldFaceNormal = normalize(mul(cross(e0, e1), (float3x3)WorldToObject()));

//     // Construct TBN
//     float3 tangent = normalize(mul(v.tangent, (float3x3)WorldToObject()));
//     float3 N = worldNormal;
//     float3 T = normalize(tangent - dot(tangent, N) * N);
//     float3 Bi = normalize(cross(T, N));
//     float3x3 TBN = float3x3(T, Bi, N);

//     float3 albedo = _Color.xyz * _MainTex.SampleLevel(sampler__MainTex, _MainTex_ST.xy * v.uv + _MainTex_ST.zw, 0).xyz;
//     float pdf = 0;
//     float3 metallic = _Metallic;

//     float smoothness = _Glossiness;
//     float3 emission = float3(0, 0, 0);
//     float3 fr = float3(0, 0, 0);

//     #if _MetallicGlossMap
//         float4 metallicSmoothness = _MetallicGlossMap.SampleLevel(sampler__MetallicGlossMap, _MetallicGlossMap_ST.xy * v.uv + _MetallicGlossMap_ST.zw, 0);
//         metallic = metallicSmoothness.xxx;
//         smoothness *= metallicSmoothness.w;
//     #endif



//     #if _EMISSION
//         emission = _EmissionColor * _EmissionTex.SampleLevel(sampler__EmissionTex, _EmissionTex_ST.xy * v.uv + _EmissionTex_ST.zw, 0).xyz;
//     #endif

//     #if _NORMALMAP
//         localNormal = GetNormalTS(v.uv);
//         worldNormal = normalize(mul(localNormal, TBN));
//         N = worldNormal;
//         T = normalize(T - dot(T, N) * N);
//         Bi = normalize(cross(T, N));
//         TBN = float3x3(T, Bi, N);
//     #endif

//     #if _TRANSPARENT
//         float3 roughness = (1 - smoothness) * RandomUnitVector(payload.rngState);

//         worldNormal = normalize(mul(localNormal, (float3x3)WorldToObject()) + roughness);

//         float indexOfRefraction = isFrontFace ? 1 / _IOR : _IOR;

//         float3 reflectionRayDir = reflect(WorldRayDirection(), worldNormal);

//         float3 refractionRayDir = refract(WorldRayDirection(), worldNormal, indexOfRefraction);

//         float fresnelFactor = FresnelReflectAmountTransparent(isFrontFace ? 1 : _IOR, isFrontFace ? _IOR : 1, WorldRayDirection(), worldNormal);

//         float doRefraction = (RandomFloat01(payload.rngState) > fresnelFactor) ? 1 : 0;

//         albedo = !isFrontFace ? exp( - (1 - _Color.xyz) * RayTCurrent() * _ExtinctionCoefficient) : float3(1, 1, 1);

//         float3 radiance = albedo / ((doRefraction == 1) ? 1 - fresnelFactor : fresnelFactor);

//         uint bounceIndexOpaque = payload.bounceIndexOpaque;

//         uint bounceIndexTransparent = payload.bounceIndexTransparent + 1;

//         float3 pushOff = worldNormal * (doRefraction ? - K_RAY_ORIGIN_PUSH_OFF : K_RAY_ORIGIN_PUSH_OFF);

//         float3 bounceRayDir = lerp(reflectionRayDir, refractionRayDir, doRefraction);
//     #else
//         float roughness = clamp(1.0 - smoothness, 0.001, 0.999);

//         float fresnelFactor = FresnelReflectAmountOpaque(isFrontFace ? 1 : _IOR, isFrontFace ? _IOR : 1, WorldRayDirection(), worldNormal);

//         float3 view = -WorldRayDirection();

//         float3 bounceRayDir;

//         float3 specularColor = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

//         float specularChance = lerp(metallic, 1, fresnelFactor * smoothness);

//         // float doSpecular = (RandomFloat01(payload.rngState) < specularChance) ? 1 : 0;

//         fr = SampleBSDF(TBN, view, worldNormal, roughness, albedo, specularColor, specularChance, fresnelFactor, bounceRayDir, pdf, payload.rngState);

//         float3 radiance = albedo;

//         if (dot(fr, fr) > 0 && pdf > 1e-6) radiance = fr * dot(bounceRayDir, worldNormal) / pdf;

//         uint bounceIndexOpaque = payload.bounceIndexOpaque + 1;

//         uint bounceIndexTransparent = payload.bounceIndexTransparent;

//         float3 pushOff = K_RAY_ORIGIN_PUSH_OFF * worldFaceNormal;

//     #endif

//     payload.hitPos = worldPosition;
//     payload.hitNorm = worldNormal;
//     payload.hitBSDF = fr;
//     payload.pdf = pdf;

//     payload.radiance = radiance;
//     payload.emission = emission;
//     payload.bounceIndexOpaque = bounceIndexOpaque;
//     payload.bounceIndexTransparent = bounceIndexTransparent;
//     payload.bounceRayOrigin = worldPosition + pushOff;
//     payload.bounceRayDirection = bounceRayDir;
// }