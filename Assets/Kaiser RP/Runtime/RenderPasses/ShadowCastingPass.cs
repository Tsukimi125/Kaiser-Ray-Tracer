using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
public class ShadowCastingPass
{
    static readonly ProfilingSampler
        sampler = new("Shadow Casting");

    static readonly ShaderTagId shaderTagID = new("ShadowCastingPass");

    // Lighting lighting;

    CullingResults cullingResults;

    // ShadowSettings shadowSettings;

    bool useLightsPerObject;

    int renderingLayerMask;

    RendererListHandle listHandle;
    // void Render(RenderGraphContext context) => lighting.Setup(
    // context, cullingResults, shadowSettings,
    // useLightsPerObject, renderingLayerMask);

    // public static void Record(
    //     RenderGraph renderGraph, Lighting lighting,
    //     CullingResults cullingResults, ShadowSettings shadowSettings,
    //     bool useLightsPerObject, int renderingLayerMask)
    // {
    //     using RenderGraphBuilder builder =
    //         renderGraph.AddRenderPass("Shadow Casting", out ShadowCastingPass pass, sampler);
    //     // pass.lighting = lighting;
    //     // pass.cullingResults = cullingResults;
    //     // pass.shadowSettings = shadowSettings;
    //     // pass.useLightsPerObject = useLightsPerObject;
    //     // pass.renderingLayerMask = renderingLayerMask;
    //     builder.SetRenderFunc<ShadowCastingPass>((pass, context) => pass.Render(context));
    // }
}
