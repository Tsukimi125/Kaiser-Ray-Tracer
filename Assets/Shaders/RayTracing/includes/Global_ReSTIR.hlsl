RaytracingAccelerationStructure g_AccelStruct : register(t0, space1);

uint g_BounceCountOpaque;
uint g_BounceCountTransparent;

RWTexture2D<float4> g_Output : register(u0);
RWTexture2D<float4> g_VPos : register(u1);
RWTexture2D<float4> g_VNorm : register(u2);
RWTexture2D<float4> g_SPos : register(u3);
RWTexture2D<float4> g_SNorm : register(u4);
RWTexture2D<float4> g_SColor : register(u5);
RWTexture2D<float> g_Random : register(u6);

RWTexture2D<float4> g_WeightSum : register(u7);