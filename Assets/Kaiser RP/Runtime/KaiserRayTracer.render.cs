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
        if (!camera.TryGetComponent<KaiserCameraData>(out var cameraData))
        {
            cameraData = camera.gameObject.AddComponent<KaiserCameraData>();
            cameraData.hideFlags = HideFlags.HideAndDontSave;
        }

        if (cameraData.UpdateCameraResources()) frameIndex = 0;

        CommandBuffer cmd = new CommandBuffer();

        if ((camera.cameraType & renderPipelineAsset.activeCameraType) > 0)
        {
            context.SetupCameraProperties(camera);
            var renderGraphParams = new RenderGraphParameters()
            {
                scriptableRenderContext = context,
                commandBuffer = cmd,
                currentFrameIndex = cameraData.frameIndex
            };

            RTHandle outputRTHandle = rtHandleSystem.Alloc(cameraData.rayTracingOutput, "_PT_Output");
            // RTHandle gbuffer0 = rtHandleSystem.Alloc(cameraData, "_PT_Output");
            switch (renderPipelineAsset.renderType)
            {
                // Add RenderType Here
                case RenderType.PATH_TRACING:
                    if (RenderPathTracing(camera, outputRTHandle, renderGraphParams, cameraData))
                    {
                        cmd.Blit(cameraData.rayTracingOutput, camera.activeTexture);
                    }
                    else
                    {
                        cmd.ClearRenderTarget(false, true, Color.black);
                        Debug.Log("Error occurred when Path Tracing!");
                    }
                    break;
                case RenderType.RCGI:
                    // Add RCGI Here
                    UpdateFrameBuffer(camera, context);
                    RenderIrcache(camera, renderGraphParams);
                    break;

            }

            outputRTHandle.Release();
        }
        else
        {
            cmd.ClearRenderTarget(false, true, Color.black);
        }


        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        context.Submit();

        renderGraph.EndFrame();

        cameraData.UpdateCameraData();

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
