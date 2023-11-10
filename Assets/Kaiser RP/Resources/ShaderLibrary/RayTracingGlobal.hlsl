RaytracingAccelerationStructure _AccelStruct:register(t0, space1);

uint _PT_MaxBounceCount;
uint _PT_BounceCountTransparent;

RWTexture2D<float4> _PT_Output:register(u0);
RWTexture2D<float4> _PT_DebugTex:register(u1);