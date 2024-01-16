#include "UnityShaderVariables.cginc"
#include "../ShaderLibrary/RayPayload.hlsl"
#include "../ShaderLibrary/Utils/RayTracingHelper.hlsl"
#include "../ShaderLibrary/Utils/Random.hlsl"
#include "../ShaderLibrary/Utils/MathConstant.hlsl"
#include "../ShaderLibrary/RayTracingGlobal.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"
#include "../ShaderLibrary/BRDF/BRDF.hlsl"
#include "../ShaderLibrary/rt/RayTrace.hlsl"
#include "../ShaderLibrary/ReSTIR/Reservoir.hlsl"
// #include "../ShaderLibrary/Utils/SpaceTransforms.hlsl"

#pragma max_recursion_depth 10

#define K_USE_ROUGHNESS_BIAS 0

// Ray Tracing Properties
uint _RE_ConvergenceStep;
uint _RE_FrameIndex;
uint _RE_MaxBounceCount;
int _RE_ResSTIRType;
int _RE_TReservoirSize;
int _RE_SReservoirSize;
int _RE_LongPath;

int _RE_EvaluateDirectLighting;

// Camera Properties
float _RE_Zoom;
float _RE_AspectRatio;

// Environment Properties
TextureCube<float4> _RE_EnvTex;
SamplerState sampler_RE_EnvTex;
float _RE_EnvIntensity;

// GBuffers
Texture2D<float4> _GBuffer0;
Texture2D<float4> _GBuffer1;
Texture2D<float4> _GBuffer2;
Texture2D<float4> _GBuffer3;
Texture2D<float4> _GBuffer4;

RWTexture2D<int4> _TReservoir;
RWTexture2D<int4> _SReservoir;
RWTexture2D<float4> _DirectIllumination;

RWTexture2D<float4> _Diffuse_TReservoir;
RWTexture2D<float4> _Specular_TReservoir;


float3 EvaluateDirectLight(in PathVertex hitVertex, inout TraceData trace, inout VertexData vertex)
{
    // Trace shadow ray
    const bool isShadowed = KaiserRayTracer::Create(CreateNewRay(hitVertex.position, _DirectionalLightDirection, 1e-4, K_T_MAX), trace.pathLength, false).TraceShadowRay(_AccelStruct);
    return trace.throughput * SELECT(isShadowed, 0.0, vertex.brdf.evalDirectionalLight(vertex.wo, vertex.wi) * _DirectionalLightColor);
}

void TraceShadowRay(in PathVertex hitVertex, inout TraceData trace, inout VertexData vertex)
{
    // Trace shadow ray
    const bool isShadowed = KaiserRayTracer::Create(CreateNewRay(hitVertex.position, _DirectionalLightDirection, 1e-4, K_T_MAX), trace.pathLength, false).TraceShadowRay(_AccelStruct);
    trace.firstLuminance = SELECT(isShadowed, 0.0, vertex.brdf.evalDirectionalLight(vertex.wo, vertex.wi) * _DirectionalLightColor);
    trace.radiance += trace.throughput * trace.firstLuminance;
}


bool TraceScatterRay(inout RayDesc ray, inout PathVertex hitVertex, inout TraceData trace, inout VertexData vertex, inout uint rng)
{
    float3 urand;
    urand = float3(RandomFloat01(rng), RandomFloat01(rng), RandomFloat01(rng));
    
    BRDFSample brdfSample = BRDFSample::Invalid();
    brdfSample = vertex.brdf.sample(urand, vertex.wo);

    if (brdfSample.IsValid())
    {
        ray.Origin = ComputeRayOrigin(hitVertex.position, hitVertex.surfaceData.normal);
        ray.Direction = mul(vertex.tangentToWorld, brdfSample.wi);
        ray.TMin = 1e-4;
        trace.throughput *= brdfSample.weight;
        trace.invPDF = 1.0f / (brdfSample.pdf);
    }
    else
    {
        trace.invPDF = 0.0001f;
        return false;
    }
    
    hitVertex = KaiserRayTracer::Create(ray, 0, false).TraceScatterRay(_AccelStruct);

    if (!hitVertex.bHit)
    {
        trace.radiance += trace.throughput * _RE_EnvIntensity * _RE_EnvTex.SampleLevel(sampler_RE_EnvTex, ray.Direction, 0).rgb;
        return false;
    }

    if (dot(hitVertex.surfaceData.normal, ray.Direction) >= 0.0)
    {
        if (0 == trace.pathLength)
        {
            hitVertex.surfaceData.normal = -hitVertex.surfaceData.normal;
        }
        else
        {
            return false;
        }
    }
    trace.radiance += trace.throughput * hitVertex.surfaceData.emission;
    return true;
}


void TracePath(uint2 launchIndex, uint2 launchDim, inout uint rng, inout TraceData trace, inout VertexData vertex, out float3 directRadiance, out float pDiffuse)
{
    float2 frameCoord = launchIndex + float2(0.5, 0.5);
    float3 result = float3(0, 0, 0);
    
    // uint rng = uint(uint(launchIndex.x) * uint(1973) + uint(launchIndex.y) * uint(9277) + uint(_RE_ConvergenceStep + _RE_FrameIndex) * uint(24699)) | uint(1);
    float2 jitter = float2(RandomFloat01(rng), RandomFloat01(rng)) - float2(0.5, 0.5);
    // float2 uv = frameCoord / float2(launchDim.x - 1, launchDim.y - 1);
    float2 uv = (frameCoord + jitter) / float2(launchDim.x - 1, launchDim.y - 1);

    float3 albedo = _GBuffer0.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 normal = _GBuffer1.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 worldPos = _GBuffer2.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 rmao = _GBuffer3.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float roughness = rmao.x;
    float metallic = rmao.y;

    float3 emissive = _GBuffer4.SampleLevel(sampler_point_clamp, uv, 0).xyz;

    SurfaceData surfaceData;
    surfaceData.albedo = albedo;
    surfaceData.normal = normal;
    surfaceData.emission = float3(0, 0, 0);
    surfaceData.roughness = roughness;
    surfaceData.metallic = metallic;

    trace.pathLength = 0;
    trace.throughput = 1;
    trace.radiance = float3(0, 0, 0);
    trace.firstLuminance = float3(0, 0, 0);

    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

    vertex.tangentToWorld = BuildOrthonormalBasis(surfaceData.normal);
    vertex.wo = mul(viewDir, vertex.tangentToWorld);
    vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

    float3 urand3 = float3(RandomFloat01(rng), RandomFloat01(rng), RandomFloat01(rng));
    // LayeredBRDF brdf = LayeredBRDF::Create(surfaceData, wo.z);
    // float3 rayDirection = brdf.sample(urand3, wo).wi;

    RayDesc ray;
    {
        ray = CreateNewRay(worldPos, 0.0f.xxx, K_T_MIN, K_T_MAX);
    }
    
    PathVertex hitVertex;
    hitVertex.position = worldPos;
    hitVertex.surfaceData = surfaceData;

    vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);
    pDiffuse = vertex.brdf.pDiffuse;
    directRadiance = emissive;
    // if (_RE_EvaluateDirectLighting)

    {
        directRadiance += EvaluateDirectLight(hitVertex, trace, vertex);
    }
    

    // invPDF = 1.0;
    uint maxBounceCount = _RE_MaxBounceCount;

    [branch]
    if (_RE_LongPath && RandomFloat01(rng) < 0.25)
    {
        maxBounceCount *= 4;
    }

    [loop]
    for (trace.pathLength = 0; trace.pathLength < maxBounceCount; trace.pathLength++)
    {
        if (!TraceScatterRay(ray, hitVertex, trace, vertex, rng))
        {
            break;
        }

        vertex.tangentToWorld = BuildOrthonormalBasis(hitVertex.surfaceData.normal);

        // To next bounce
        vertex.wo = mul(-ray.Direction, vertex.tangentToWorld);
        if (vertex.wo.z < 0.0)
        {
            vertex.wo.z *= -0.25;
            vertex.wo = normalize(vertex.wo);
        }
        vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);
        vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

        TraceShadowRay(hitVertex, trace, vertex);

        [branch]
        if (trace.pathLength == 0)
        {
            trace.firstSampleDir = ray.Direction;
        }
    }
}


void TraceDiffuse(uint2 launchIndex, uint2 launchDim, inout uint rng, inout TraceData trace, inout VertexData vertex, out float3 directRadiance)
{
    float2 frameCoord = launchIndex + float2(0.5, 0.5);
    float3 result = float3(0, 0, 0);
    
    // uint rng = uint(uint(launchIndex.x) * uint(1973) + uint(launchIndex.y) * uint(9277) + uint(_RE_ConvergenceStep + _RE_FrameIndex) * uint(24699)) | uint(1);
    float2 jitter = float2(RandomFloat01(rng), RandomFloat01(rng)) - float2(0.5, 0.5);
    // float2 uv = frameCoord / float2(launchDim.x - 1, launchDim.y - 1);
    float2 uv = (frameCoord + jitter) / float2(launchDim.x - 1, launchDim.y - 1);

    float3 albedo = _GBuffer0.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 normal = _GBuffer1.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 worldPos = _GBuffer2.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 rmao = _GBuffer3.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float roughness = rmao.x;
    float metallic = rmao.y;

    float3 emissive = _GBuffer4.SampleLevel(sampler_point_clamp, uv, 0).xyz;

    SurfaceData surfaceData;
    surfaceData.albedo = albedo;
    surfaceData.normal = normal;
    surfaceData.emission = float3(0, 0, 0);
    surfaceData.roughness = roughness;
    surfaceData.metallic = metallic;

    trace.pathLength = 0;
    trace.throughput = 1;
    trace.radiance = float3(0, 0, 0);
    trace.firstLuminance = float3(0, 0, 0);

    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

    vertex.tangentToWorld = BuildOrthonormalBasis(surfaceData.normal);
    vertex.wo = mul(viewDir, vertex.tangentToWorld);
    vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

    float3 urand3 = float3(RandomFloat01(rng), RandomFloat01(rng), RandomFloat01(rng));
    // LayeredBRDF brdf = LayeredBRDF::Create(surfaceData, wo.z);
    // float3 rayDirection = brdf.sample(urand3, wo).wi;

    RayDesc ray;
    {
        ray = CreateNewRay(worldPos, 0.0f.xxx, K_T_MIN, K_T_MAX);
    }
    
    PathVertex hitVertex;
    hitVertex.position = worldPos;
    hitVertex.surfaceData = surfaceData;

    vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);

    directRadiance = emissive;
    // if (_RE_EvaluateDirectLighting)

    {
        directRadiance += EvaluateDirectLight(hitVertex, trace, vertex);
    }
    vertex.brdf.ForceDiffuse();

    // invPDF = 1.0;
    uint maxBounceCount = _RE_MaxBounceCount;

    [branch]
    if (_RE_LongPath && RandomFloat01(rng) < 0.25)
    {
        maxBounceCount *= 4;
    }

    [loop]
    for (trace.pathLength = 0; trace.pathLength < maxBounceCount; trace.pathLength++)
    {
        if (!TraceScatterRay(ray, hitVertex, trace, vertex, rng))
        {
            break;
        }

        vertex.tangentToWorld = BuildOrthonormalBasis(hitVertex.surfaceData.normal);

        // To next bounce
        vertex.wo = mul(-ray.Direction, vertex.tangentToWorld);
        if (vertex.wo.z < 0.0)
        {
            vertex.wo.z *= -0.25;
            vertex.wo = normalize(vertex.wo);
        }
        vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);
        vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

        TraceShadowRay(hitVertex, trace, vertex);

        [branch]
        if (trace.pathLength == 0)
        {
            trace.firstSampleDir = ray.Direction;
        }
    }
}


void TraceSpecular(uint2 launchIndex, uint2 launchDim, inout uint rng, inout TraceData trace, inout VertexData vertex, out float3 directRadiance)
{
    float2 frameCoord = launchIndex + float2(0.5, 0.5);
    float3 result = float3(0, 0, 0);
    
    // uint rng = uint(uint(launchIndex.x) * uint(1973) + uint(launchIndex.y) * uint(9277) + uint(_RE_ConvergenceStep + _RE_FrameIndex) * uint(24699)) | uint(1);
    float2 jitter = float2(RandomFloat01(rng), RandomFloat01(rng)) - float2(0.5, 0.5);
    // float2 uv = frameCoord / float2(launchDim.x - 1, launchDim.y - 1);
    float2 uv = (frameCoord + jitter) / float2(launchDim.x - 1, launchDim.y - 1);

    float3 albedo = _GBuffer0.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 normal = _GBuffer1.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 worldPos = _GBuffer2.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float3 rmao = _GBuffer3.SampleLevel(sampler_point_clamp, uv, 0).xyz;
    float roughness = rmao.x;
    float metallic = rmao.y;

    float3 emissive = _GBuffer4.SampleLevel(sampler_point_clamp, uv, 0).xyz;

    SurfaceData surfaceData;
    surfaceData.albedo = albedo;
    surfaceData.normal = normal;
    surfaceData.emission = float3(0, 0, 0);
    surfaceData.roughness = roughness;
    surfaceData.metallic = metallic;

    trace.pathLength = 0;
    trace.throughput = 1;
    trace.radiance = float3(0, 0, 0);
    trace.firstLuminance = float3(0, 0, 0);

    float3 viewDir = normalize(_WorldSpaceCameraPos - worldPos);

    vertex.tangentToWorld = BuildOrthonormalBasis(surfaceData.normal);
    vertex.wo = mul(viewDir, vertex.tangentToWorld);
    vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

    float3 urand3 = float3(RandomFloat01(rng), RandomFloat01(rng), RandomFloat01(rng));

    RayDesc ray;
    {
        ray = CreateNewRay(worldPos, 0.0f.xxx, K_T_MIN, K_T_MAX);
    }
    
    PathVertex hitVertex;
    hitVertex.position = worldPos;
    hitVertex.surfaceData = surfaceData;

    vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);
    
    directRadiance = emissive;
    {
        directRadiance += EvaluateDirectLight(hitVertex, trace, vertex);
    }
    vertex.brdf.ForceSpecular();

    // invPDF = 1.0;
    uint maxBounceCount = _RE_MaxBounceCount;

    [branch]
    if (_RE_LongPath && RandomFloat01(rng) < 0.25)
    {
        maxBounceCount *= 4;
    }

    [loop]
    for (trace.pathLength = 0; trace.pathLength < maxBounceCount; trace.pathLength++)
    {
        if (!TraceScatterRay(ray, hitVertex, trace, vertex, rng))
        {
            break;
        }

        vertex.tangentToWorld = BuildOrthonormalBasis(hitVertex.surfaceData.normal);

        // To next bounce
        vertex.wo = mul(-ray.Direction, vertex.tangentToWorld);
        if (vertex.wo.z < 0.0)
        {
            vertex.wo.z *= -0.25;
            vertex.wo = normalize(vertex.wo);
        }
        vertex.brdf = LayeredBRDF::Create(hitVertex.surfaceData, vertex.wo.z);
        // vertex.brdf.ForceSpecular();
        vertex.wi = mul(_DirectionalLightDirection, vertex.tangentToWorld);

        TraceShadowRay(hitVertex, trace, vertex);

        [branch]
        if (trace.pathLength == 0)
        {
            trace.firstSampleDir = ray.Direction;
        }
    }
}