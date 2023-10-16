

#ifndef KAISER_RAYTRACER_LIGHTING
#define KAISER_RAYTRACER_LIGHTING

CBUFFER_START(_CustomLight)
    float3 _DirectionalLightColor;
    float3 _DirectionalLightDirection;
CBUFFER_END
#endif
