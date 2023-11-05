using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class KaiserRayTracer : RenderPipeline
{
    static class RTShaders
    {
        static public RayTracingShader referencePathTracer;
        static public RayTracingShader gbuffer;
    };

    bool SetupShaders()
    {
        RTShaders.referencePathTracer = Resources.Load<RayTracingShader>("Shaders/ReferencePathTracer");
        RTShaders.gbuffer = Resources.Load<RayTracingShader>("Shaders/RayTracedGBuffer");
        if (RTShaders.referencePathTracer == null || RTShaders.gbuffer == null)
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
}

