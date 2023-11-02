using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class KaiserRayTracer : RenderPipeline
{
    class PathTracingRenderPassData
    {
        public TextureHandle outputTexture;
    };

    class GBufferRenderPassData
    {
        public TextureHandle gbuffer0;
        public TextureHandle gbuffer1;
        public TextureHandle gbuffer2;
    };
}

