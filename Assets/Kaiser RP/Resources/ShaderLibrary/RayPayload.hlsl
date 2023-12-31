// struct PathPayload
// {
//     float3 hitPos;
//     float3 hitNorm;
//     float3 hitBSDF;

//     float3 radiance;
//     float3 emission;
//     uint bounceIndexOpaque;
//     uint bounceIndexTransparent;
//     float3 bounceRayOrigin;
//     float3 bounceRayDirection;
//     uint rngState;
//     float pdf;
// };

// struct ShadowPayload
// {
//     bool isShadowed;
// };
#ifndef KAISER_RAYTRACING_RAYPAYLOAD
#define KAISER_RAYTRACING_RAYPAYLOAD

#include "BRDF/BRDF.hlsl"
#include "rt/RayTrace.hlsl"


#endif