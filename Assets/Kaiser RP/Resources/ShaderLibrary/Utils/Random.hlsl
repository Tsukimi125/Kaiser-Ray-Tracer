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

#define Pi 3.1416

uint ReverseBits32(uint bits)
{
    #if 0 // Shader model 5
        return reversebits(bits);
    #else
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
        bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
        bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
        bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
        return bits;
    #endif
}

float2 Hammersley16(uint Index, uint NumSamples, uint2 Random)
{
    float E1 = frac((float)Index / NumSamples + float(Random.x) * (1.0 / 65536.0));
    float E2 = float((ReverseBits32(Index) >> 16) ^ Random.y) * (1.0 / 65536.0);
    return float2(E1, E2);
}


#endif // KAISER_RAYTRACING_RANDOM