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

RaytracingAccelerationStructure g_AccelStruct : register(t0, space1);

uint g_BounceCountOpaque;
uint g_BounceCountTransparent;

RWTexture2D<float4> g_Output : register(u0);
RWTexture2D<float4> g_DebugTex : register(u1);