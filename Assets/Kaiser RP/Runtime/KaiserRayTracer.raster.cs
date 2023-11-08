using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    void RenderLightPass(Camera camera, RenderGraphParameters renderGraphParams, KaiserCameraData cameraData)
    {
        if (KaiserShaders.deferredLightPass == null)
        {
            Debug.Log("Deferred Light Pass Shader is null!");
            return;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            RenderGraphBuilder builder = renderGraph.AddRenderPass<DeferredLightPassData>("Path Tracing Pass", out var passData);
            TextureHandle output = renderGraph.ImportTexture(passData.outputTexture);
            passData.outputTexture = builder.WriteTexture(output);

            builder.SetRenderFunc((DeferredLightPassData data, RenderGraphContext ctx) =>
            {
                
            });

        }
    }

}

