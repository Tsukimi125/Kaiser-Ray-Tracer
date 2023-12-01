using System.Collections;
using System.Collections.Generic;
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
    static readonly ShaderTagId shaderTagID = new("KaiserLit");


    RendererListHandle listHandle;
    TextureHandle gbuffer0;
    TextureHandle gbuffer1;
    TextureHandle gbuffer2;
    TextureHandle gbuffer3;
    void Render(RenderGraphContext context)
    {
        // context.cmd.DrawRendererList(list);
        // context.renderContext.ExecuteCommandBuffer(context.cmd);
        // context.cmd.Clear();
        CoreUtils.DrawRendererList(context.renderContext, context.cmd, listHandle);
    }

    public static void Record(
        RenderGraph renderGraph,
        Camera camera,
        CullingResults cullingResults,
        bool useLightsPerObject,
        int renderingLayerMask)
    {
        ProfilingSampler sampler = samplerOpaque;

        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            sampler.name, out GBufferPass pass, sampler);

        // pass.gbuffer0 = builder.WriteTexture(renderGraph.CreateTexture(
        //     new TextureDesc(Vector2.one, true, true)
        //     {
        //         colorFormat = GraphicsFormat.R8G8B8A8_SRGB,
        //         enableRandomWrite = true,
        //         name = "GBuffer0"
        //     }));

        RendererListDesc gbufferDesc = new RendererListDesc(shaderTagID, cullingResults, camera)
        {
            sortingCriteria = SortingCriteria.CommonOpaque,
            renderQueueRange = RenderQueueRange.opaque
        };
        pass.listHandle = renderGraph.CreateRendererList(gbufferDesc);
        // passData._renderList_Qpaque = builder.UseRendererList(gbufferListHandle);

        // builder.SetRenderFunc((GBufferPassData data, RenderGraphContext context) =>
        // {
        //     CoreUtils.DrawRendererList(context.renderContext, context.cmd, data._renderList_Qpaque);
        // });
        builder.SetRenderFunc<GBufferPass>((pass, context) => pass.Render(context));

    }
}
