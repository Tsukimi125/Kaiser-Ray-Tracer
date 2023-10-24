using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class KaiserRayTracer : RenderPipeline
{
    static void RenderSingleCamera(ScriptableRenderContext context, Camera camera, bool anyPostProcessingEnabled)
    {


    }


    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (!ValidateRayTracing())
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, true, Color.magenta);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            cmd.Release();
            return;
        }

        CullInstance();

        foreach (Camera camera in cameras)
        {
            RenderSingleCamera(context, camera, true);
        }
    }



}

