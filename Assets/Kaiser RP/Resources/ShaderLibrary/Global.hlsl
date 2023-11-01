float4 _Color;

Texture2D<float4> _MainTex;
float4 _MainTex_ST;
SamplerState sampler_MainTex;

Texture2D<float4> _NormalMap;
float4 _NormalMap_ST;
SamplerState sampler_NormalMap;

Texture2D<float4> _MetallicMap;
float4 _MetallicMap_ST;
SamplerState sampler_MetallicMap;

float _Glossiness;
float _Metallic;
float _IOR;

Texture2D<float4> _EmissionTex;
float4 _EmissionTex_ST;
SamplerState sampler_EmissionTex;
float4 _EmissionColor;

float _ExtinctionCoefficient;


//------------------------------------------------------------------






//------------------------------------------------------------------

RaytracingAccelerationStructure _PT_AccelStruct:register(t0, space1);

uint _PT_MaxBounceCount;
uint _PT_BounceCountTransparent;

RWTexture2D<float4> _PT_Output : register(u0);
RWTexture2D<float4> _PT_DebugTex : register(u1);