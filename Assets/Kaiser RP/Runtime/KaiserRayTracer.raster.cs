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
            RenderGraphBuilder builder = renderGraph.AddRenderPass<DeferredLightPassData>("Deferred Light Pass", out var passData);

            TextureDesc desc = new TextureDesc()
            {
                dimension = TextureDimension.Tex2D,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                depthBufferBits = 0,
                colorFormat = GraphicsFormat.R16G16B16A16_UNorm,
                slices = 1,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };

            passData.outputTexture = builder.CreateTransientTexture(desc);
            // TextureHandle output = renderGraph.ImportTexture(passData.outputTexture);
            // passData.outputTexture = builder.WriteTexture(output);

            builder.SetRenderFunc((DeferredLightPassData data, RenderGraphContext ctx) =>
            {
                Vector4 bufferSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
                ctx.cmd.SetComputeVectorParam(KaiserShaders.deferredLightPass, "_BufferSize", bufferSize);
                ctx.cmd.SetComputeVectorParam(KaiserShaders.deferredLightPass, "_Jitter", new Vector4(0.5f, 0.5f, 0, 0));
                ctx.cmd.SetComputeTextureParam(KaiserShaders.deferredLightPass, 0, "_Result", passData.outputTexture);

                // dispatch
                int threadGroupsX = Mathf.CeilToInt(camera.pixelWidth / 8.0f);
                int threadGroupsY = Mathf.CeilToInt(camera.pixelHeight / 8.0f);
                ctx.cmd.DispatchCompute(KaiserShaders.deferredLightPass, 0, threadGroupsX, threadGroupsY, 1);
                frameIndex++;
            });

        }
    }

}

