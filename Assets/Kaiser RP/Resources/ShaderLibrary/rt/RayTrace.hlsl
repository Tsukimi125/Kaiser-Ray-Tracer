#ifndef KAISER_RAYTRACING_CORE
#define KAISER_RAYTRACING_CORE

#include "../Utils/RayTracingHelper.hlsl"
#include "../BRDF/BRDF.hlsl"
#include "../RayPayload.hlsl"



struct RayCone
{
    float width;
    float spreadAngle;

    static RayCone Create(float width, float spreadAngle)
    {
        RayCone res;
        res.width = width;
        res.spreadAngle = spreadAngle;
        return res;
    }
};

struct PathVertex
{
    bool bHit;
    SurfaceData surfaceData;
    float3 position;
    float rayT;
};

struct VertexData
{
    float pathLength;
    float3 wo;
    float3 wi;
    LayeredBRDF brdf;
    float3x3 tangentToWorld;
    float invPDF;
};

struct RayPayload
{
    SurfaceData surfaceData;
    float t;
    RayCone rayCone;
    uint pathLength;

    static RayPayload CreateMiss()
    {
        RayPayload res;
        res.t = K_FLT_MAX;
        res.rayCone = RayCone::Create(0, 0);
        res.pathLength = 0;
        return res;
    }

    bool isMiss()
    {
        return t == K_FLT_MAX;
    }

    bool isHit()
    {
        return !isMiss();
    }
};


struct ShadowRayPayload
{
    bool isShadowed;

    static ShadowRayPayload NewHit()
    {
        ShadowRayPayload res;
        res.isShadowed = true;
        return res;
    }

    bool isMiss()
    {
        return !isShadowed;
    }

    bool isHit()
    {
        return !isMiss();
    }
};


struct RayTracedGBuffer
{
    float3 albedo;
    float3 normal;
    float3 worldPos;
    float depth;
    float roughness;
    float metallic;
};

struct ScatterRayData
{
    float3 direction; float pdf;
};

void GenerateRayTracedGBufferFromHitPathVertex(in PathVertex hitVertex, out RayTracedGBuffer gbuffer)
{
    gbuffer.albedo = hitVertex.surfaceData.albedo;
    gbuffer.normal = hitVertex.surfaceData.normal;
    gbuffer.worldPos = hitVertex.position;
    gbuffer.depth = hitVertex.rayT; // TODO: Raw Depth? 01 Depth? Linear Depth?
    gbuffer.roughness = hitVertex.surfaceData.roughness;
    gbuffer.metallic = hitVertex.surfaceData.metallic;
}



struct KaiserRayTracer
{
    RayDesc ray;
    uint pathLength;
    bool bCullBackfaces;

    static KaiserRayTracer Create(RayDesc ray, uint pathLength, bool bCullBackfaces)
    {
        KaiserRayTracer res;
        res.ray = ray;
        res.pathLength = pathLength;
        res.bCullBackfaces = bCullBackfaces;
        return res;
    }

    PathVertex TraceScatterRay(RaytracingAccelerationStructure rtas)
    {
        RayPayload payload = RayPayload::CreateMiss();
        payload.pathLength = this.pathLength;

        uint traceFlags = 0;
        if (this.bCullBackfaces)
        {
            traceFlags |= RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
        }

        TraceRay(rtas, traceFlags, 0xff, 0, 1, 0, ray, payload);

        PathVertex res;
        if (payload.isHit())
        {
            res.bHit = true;
            res.surfaceData = payload.surfaceData;
            res.position = ray.Origin + ray.Direction * payload.t;
            res.rayT = payload.t;
        }
        else
        {
            res.bHit = false;
            res.rayT = K_FLT_MAX;
        }
        return res;
    }

    bool TraceShadowRay(RaytracingAccelerationStructure rtas)
    {
        ShadowRayPayload payload = ShadowRayPayload::NewHit();

        TraceRay(rtas, RAY_FLAG_CULL_FRONT_FACING_TRIANGLES | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH,
        0xff, 0, 1, 1, ray, payload);

        return payload.isShadowed;
    }
};


#endif // KAISER_RAYTRACING_CORE