using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
public partial class KaiserRayTracer : RenderPipeline
{
    static class KaiserShaders
    {
        static public ComputeShader deferredLightPass;
        static public RayTracingShader referencePathTracer;
        static public RayTracingShader restir;
        static public RayTracingShader gbuffer;
    };

    static class ReservoirBuffers
    {
        static public RTHandle Temporal;
        static public RTHandle Spatial;
        static public RTHandle DirectIllumination;

        static public RTHandle DiffuseTemporal;
        static public RTHandle SpecularTemporal;

    };

    void ReAllocateRTHandles(Camera camera)
    {
        var reTemporalDesc = new RenderTextureDescriptor()
        {
            dimension = TextureDimension.Tex2D,
            width = camera.pixelWidth,
            height = camera.pixelHeight,
            depthBufferBits = 0,
            volumeDepth = 1,
            msaaSamples = 1,
            graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
            enableRandomWrite = true,
        };

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, colorFormat: GraphicsFormat.R16G16B16A16_UNorm, 0);
        descriptor.enableRandomWrite = true;

        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.Temporal, reTemporalDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_TReservoir");
        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.Spatial, reTemporalDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_SReservoir");
        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.DirectIllumination, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_DirectIllumination");

        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.DiffuseTemporal, reTemporalDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_Diffuse_TReservoir");
        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.SpecularTemporal, reTemporalDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_Specular_TReservoir");
    }

    bool SetupShaders()
    {
        KaiserShaders.deferredLightPass = Resources.Load<ComputeShader>("Shaders/DeferredLightPass");
        KaiserShaders.referencePathTracer = Resources.Load<RayTracingShader>("Shaders/ReferencePathTracer");
        KaiserShaders.restir = Resources.Load<RayTracingShader>("Shaders/ReSTIR");
        KaiserShaders.gbuffer = Resources.Load<RayTracingShader>("Shaders/RayTracedGBuffer");
        if (KaiserShaders.referencePathTracer == null || KaiserShaders.gbuffer == null)
        {
            Debug.Log("Ray Tracing Shader is null!");
            return false;
        }
        return true;
    }

    class PathTracingRenderPassData
    {
        public TextureHandle outputTexture;
    };
    class ReSTIRRenderPassData
    {
        public TextureHandle outputTexture;
        public TextureHandle temporalReservoir;
        public TextureHandle spatialReservoir;
        public TextureHandle directIllumination;
    };

    class GBufferRenderPassData
    {
        public TextureHandle gbuffer0;
        public TextureHandle gbuffer1;
        public TextureHandle gbuffer2;
        public TextureHandle gbuffer3;
        public TextureHandle gbuffer4;
    };

    class DeferredLightPassData
    {
        public TextureHandle outputTexture;
    };


}

