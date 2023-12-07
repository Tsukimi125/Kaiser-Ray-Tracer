using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
public class DeferredLightPass
{
    static readonly ProfilingSampler
        samplerOpaque = new("Opaque Geometry");

    static readonly ShaderTagId shaderTagID = new("DeferredLightPass");

    TextureHandle outputTexture;

    public static void Record(
        RenderGraph renderGraph,
        Camera camera)
    {
        RenderGraphBuilder builder = renderGraph.AddRenderPass<DeferredLightPass>("Deferred Light Pass", out var pass);

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

        ComputeShader deferredLightPassShader = Resources.Load<ComputeShader>("Shaders/DeferredLightPass");

        pass.outputTexture = builder.CreateTransientTexture(desc);
        // TextureHandle output = renderGraph.ImportTexture(pass.outputTexture);
        // pass.outputTexture = builder.WriteTexture(output);

        builder.SetRenderFunc((DeferredLightPass pass, RenderGraphContext ctx) =>
        {
            Vector4 bufferSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
            ctx.cmd.SetComputeVectorParam(deferredLightPassShader, "_BufferSize", bufferSize);
            ctx.cmd.SetComputeVectorParam(deferredLightPassShader, "_Jitter", new Vector4(0.5f, 0.5f, 0, 0));
            ctx.cmd.SetComputeTextureParam(deferredLightPassShader, 0, "_Result", pass.outputTexture);

            // dispatch
            int threadGroupsX = Mathf.CeilToInt(camera.pixelWidth / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(camera.pixelHeight / 8.0f);
            ctx.cmd.DispatchCompute(deferredLightPassShader, 0, threadGroupsX, threadGroupsY, 1);

            ctx.cmd.Blit(pass.outputTexture, camera.activeTexture);
        });


    }
}
