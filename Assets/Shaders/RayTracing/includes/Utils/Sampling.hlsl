#ifndef KAISER_RAYTRACING_SAMPLING
#define KAISER_RAYTRACING_SAMPLING

#include "../Utils/MathConstant.hlsl"

float2 SampleDiskConcentric(float2 u)
{
    u = 2.f * u - 1.f;
    if (u.x == 0.f && u.y == 0.f) return u;
    float phi, r;
    if (abs(u.x) > abs(u.y))
    {
        r = u.x;
        phi = (u.y / u.x) * K_QUARTER_PI;
    }
    else
    {
        r = u.y;
        phi = K_HALF_PI - (u.x / u.y) * K_QUARTER_PI;
    }
    return r * float2(cos(phi), sin(phi));
}

float3 SampleCosineHemisphereConcentric(float2 u, out float pdf)
{
    float2 d = SampleDiskConcentric(u);
    float z = sqrt(max(0.f, 1.f - dot(d, d)));
    pdf = z * K_INV_PI;
    return float3(d, z);
}

#endif // KAISER_RAYTRACING_SAMPLING