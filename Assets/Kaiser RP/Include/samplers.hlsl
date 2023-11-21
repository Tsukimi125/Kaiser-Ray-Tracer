#ifndef SAMPLERS_HLSL
#define SAMPLERS_HLSL
#include "UnityRaytracingMeshUtils.cginc"

SamplerState gsamLinear : register(s0);

SamplerState sampler_lnc : register(s2);
SamplerState sampler_LinearClamp;
SamplerState sampler_nnc : register(s4);
SamplerState sampler_llc : register(s5);

#endif