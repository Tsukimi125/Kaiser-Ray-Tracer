using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class KaiserRayTracer : RenderPipeline
{
    static class KaiserShaders
    {
        static public ComputeShader deferredLightPass;
        static public RayTracingShader referencePathTracer;
        static public RayTracingShader restir;

        static public RayTracingShader gbuffer;
    };

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

    class GBufferRenderPassData
    {
        public TextureHandle gbuffer0;
        public TextureHandle gbuffer1;
        public TextureHandle gbuffer2;
        public TextureHandle gbuffer3;
    };

    class DeferredLightPassData
    {
        public TextureHandle outputTexture;
    };
}

