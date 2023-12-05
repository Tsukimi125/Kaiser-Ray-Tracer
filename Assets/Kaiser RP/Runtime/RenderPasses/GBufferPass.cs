using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
public class GBufferPass
{
    static readonly ProfilingSampler
        samplerOpaque = new("Opaque Geometry"),
        samplerTransparent = new("Transparent Geometry");

    // static readonly ShaderTagId[] shaderTagIDs = {
    //     new("SRPDefaultUnlit"),
    //     new("KaiserLit")
    // };
    static readonly ShaderTagId shaderTagID = new("GBufferPass");


    RendererListHandle listHandle;
    TextureHandle gbuffer0;
    TextureHandle gbuffer1;
    TextureHandle gbuffer2;
    TextureHandle gbuffer3;
    TextureHandle depth;
    void Render(RenderGraphContext context)
    {


        context.cmd.DrawRendererList(listHandle);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
        Debug.Log("GBufferPass");
        // CoreUtils.DrawRendererList(context.renderContext, context.cmd, listHandle);
        context.cmd.SetGlobalTexture("_GBuffer0", gbuffer0);
        context.cmd.SetGlobalTexture("_GBuffer1", gbuffer1);
        context.cmd.SetGlobalTexture("_GBuffer2", gbuffer2);
        context.cmd.SetGlobalTexture("_GBuffer3", gbuffer3);

    }

    public static void Record(
        RenderGraph renderGraph,
        Camera camera,
        CullingResults cullingResults,
        bool useLightsPerObject,
        int renderingLayerMask)
    {
        ProfilingSampler sampler = samplerOpaque;

        RenderGraphBuilder builder = renderGraph.AddRenderPass<GBufferPass>(
            "GBufferPass", out var pass);
        {

            TextureDesc colorRTDesc = new TextureDesc(camera.pixelWidth, camera.pixelHeight)
            {
                colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.BGRA32, QualitySettings.activeColorSpace == ColorSpace.Linear),
                depthBufferBits = 0,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = false,
                clearBuffer = true,
                clearColor = Color.black,
                name = "_GBuffer0"
            };

            TextureDesc depthRTDesc = new TextureDesc(camera.pixelWidth, camera.pixelHeight)
            {
                colorFormat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.Depth, QualitySettings.activeColorSpace == ColorSpace.Linear),
                depthBufferBits = DepthBits.Depth24,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = false,
                clearBuffer = true,
                clearColor = Color.black,
                name = "_Depth"
            };

            pass.gbuffer0 = builder.UseColorBuffer(renderGraph.CreateTexture(colorRTDesc), 0);
            pass.gbuffer1 = builder.UseColorBuffer(renderGraph.CreateTexture(colorRTDesc), 1);
            pass.gbuffer2 = builder.UseColorBuffer(renderGraph.CreateTexture(colorRTDesc), 2);
            pass.gbuffer3 = builder.UseColorBuffer(renderGraph.CreateTexture(colorRTDesc), 3);
            pass.depth = builder.UseDepthBuffer(renderGraph.CreateTexture(depthRTDesc), DepthAccess.Write);
            builder.ReadTexture(pass.gbuffer0);
            builder.ReadTexture(pass.gbuffer1);
            builder.ReadTexture(pass.gbuffer2);
            builder.ReadTexture(pass.gbuffer3);
            builder.ReadTexture(pass.depth);
            RendererListDesc gbufferDesc = new RendererListDesc(shaderTagID, cullingResults, camera)
            {
                sortingCriteria = SortingCriteria.CommonOpaque,
                renderQueueRange = RenderQueueRange.opaque
            };
            pass.listHandle = builder.UseRendererList(renderGraph.CreateRendererList(gbufferDesc));

            // builder.SetRenderFunc((GBufferPassData data, RenderGraphContext context) =>
            // {
            //     CoreUtils.DrawRendererList(context.renderContext, context.cmd, data._renderList_Qpaque);
            // });
            builder.SetRenderFunc<GBufferPass>((pass, context) => pass.Render(context));
            // Debug.Log("1");
            // builder.SetRenderFunc((GBufferPass pass, RenderGraphContext context) =>
            // {
            //     Debug.Log("4");
            //     context.cmd.DrawRendererList(pass.listHandle);
            //     context.renderContext.ExecuteCommandBuffer(context.cmd);
            //     context.cmd.Clear();
            // });
        }

    }
}
