#ifndef KAISER_RAYTRACING_CORE
#define KAISER_RAYTRACING_CORE

#include "../Utils.hlsl"
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

struct RayPayload
{
    SurfaceData surfaceData;
    float t;
    RayCone rayCone;
    uint pathLength;

    static RayPayload CreateMiss()
    {
        RayPayload res;
        res.t = FLT_MAX;
        res.rayCone = RayCone::Create(0, 0);
        res.pathLength = 0;
        return res;
    }

    bool isMiss()
    {
        return t == FLT_MAX;
    }

    bool isHit()
    {
        return !isMiss();
    }
};

struct KaiserRayTracer
{
    RayDesc ray;
    RayCone rayCone;
    uint pathLength;
    bool bCullBackfaces;

    static KaiserRayTracer Create(RayDesc ray, RayCone rayCone, uint pathLength, bool bCullBackfaces)
    {
        KaiserRayTracer res;
        res.ray = ray;
        res.rayCone = rayCone;
        res.pathLength = pathLength;
        res.bCullBackfaces = bCullBackfaces;
        return res;
    }

    PathVertex Trace(RaytracingAccelerationStructure rtas)
    {
        RayPayload payload = RayPayload::CreateMiss();
        payload.rayCone = this.rayCone;
        payload.pathLength = this.pathLength;

        uint traceFlags = 0;
        if (this.bCullBackfaces)
        {
            traceFlags |= RAY_FLAG_CULL_BACK_FACING_TRIANGLES;
        }

        TraceRay(rtas, traceFlags, 0xff, 0, 0, 0, ray, payload);

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
            res.rayT = FLT_MAX;
        }
        return res;
    }
};


#endif // KAISER_RAYTRACING_CORE