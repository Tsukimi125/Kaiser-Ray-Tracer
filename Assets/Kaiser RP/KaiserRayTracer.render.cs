using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    void RenderSingleCamera(ScriptableRenderContext context, Camera camera, bool anyPostProcessingEnabled)
    {
        if (!camera.TryGetComponent<AdditionalCameraData>(out var additionalData))
        {
            additionalData = camera.gameObject.AddComponent<AdditionalCameraData>();
            additionalData.hideFlags = HideFlags.HideAndDontSave;
        }

        if (additionalData.UpdateCameraResources()) frameIndex = 0;

        CommandBuffer cmd = new CommandBuffer();

        var renderGraphParams = new RenderGraphParameters()
        {
            scriptableRenderContext = context,
            commandBuffer = cmd,
            currentFrameIndex = additionalData.frameIndex
        };

        RTHandle outputRTHandle = rtHandleSystem.Alloc(additionalData.rayTracingOutput, "g_Output");
        switch (renderPipelineAsset.renderType)
        {
            case RenderType.PATH_TRACING:
                if (RenderPathTracing(camera, outputRTHandle, renderGraphParams, additionalData))
                {
                    cmd.Blit(additionalData.rayTracingOutput, camera.activeTexture);
                }
                else
                {
                    cmd.ClearRenderTarget(false, true, Color.black);
                    Debug.Log("Error occurred when Path Tracing!");
                }
                break;
        }

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

        lighting.Setup(context);

        foreach (Camera camera in cameras)
        {
            RenderSingleCamera(context, camera, true);
        }
    }



}

