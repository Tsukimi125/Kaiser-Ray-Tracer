#ifndef KAISER_RAYTRACING_RANDOM
#define KAISER_RAYTRACING_RANDOM

#include "MathConstant.hlsl"

uint WangHash(inout uint seed)
{
    seed = (seed ^ 61) ^(seed >> 16);
    seed *= 9;
    seed = seed ^(seed >> 4);
    seed *= 0x27d4eb2d;
    seed = seed ^(seed >> 15);
    return seed;
}

float RandomFloat01(inout uint seed)
{
    return float(WangHash(seed)) / float(0xFFFFFFFF);
}

float3 RandomUnitVector(inout uint state)
{
    float z = RandomFloat01(state) * 2.0f - 1.0f;
    float a = RandomFloat01(state) * K_TWO_PI;
    float r = sqrt(1.0f - z * z);
    float x = r * cos(a);
    float y = r * sin(a);
    return float3(x, y, z);
}

float hash(float a)
{
    return frac(sin(dot(a.xx, float2(12.9898, 78.233))) * 43758.5453);
}

#endif // KAISER_RAYTRACING_RANDOM