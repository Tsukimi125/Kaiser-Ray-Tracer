using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;
using UnityEngine.Rendering.Universal;
using TreeEditor;

public partial class KaiserRayTracer : RenderPipeline
{
    void RenderSingleCamera(ScriptableRenderContext context, Camera camera, bool anyPostProcessingEnabled)
    {
        BeginCameraRendering(context, camera);

        if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParams)) return;
        CullingResults cull = context.Cull(ref cullingParams);

        context.SetupCameraProperties(camera);

        if (!camera.TryGetComponent<KaiserCameraData>(out var cameraData))
        {
            cameraData = camera.gameObject.AddComponent<KaiserCameraData>();
            cameraData.hideFlags = HideFlags.HideAndDontSave;
        }

        if (cameraData.UpdateCameraResources()) frameIndex = 0;

        CommandBuffer cmd = new CommandBuffer
        {
            name = "Kaiser Ray Tracer"
        };

        var renderGraphParams = new RenderGraphParameters()
        {
            executionName = "Render Graph",
            scriptableRenderContext = context,
            commandBuffer = cmd,
            currentFrameIndex = cameraData.frameIndex
        };

        ProfilingSampler cameraSampler = new ProfilingSampler("Camera Render");

        RTHandle outputRTHandle = rtHandleSystem.Alloc(cameraData.rayTracingOutput, "_Output");
        RTHandle gbufferHandle0 = rtHandleSystem.Alloc(cameraData.gbuffer0, "_GBuffer0");
        RTHandle gbufferHandle1 = rtHandleSystem.Alloc(cameraData.gbuffer1, "_GBuffer1");
        RTHandle gbufferHandle2 = rtHandleSystem.Alloc(cameraData.gbuffer2, "_GBuffer2");
        RTHandle gbufferHandle3 = rtHandleSystem.Alloc(cameraData.gbuffer3, "_GBuffer3");
        RTHandle gbufferHandle4 = rtHandleSystem.Alloc(cameraData.gbuffer4, "_GBuffer4");

        ReAllocateRTHandles(camera);

        if (camera.cameraType == CameraType.SceneView)
        {
            using (renderGraph.RecordAndExecute(renderGraphParams))
            {
                using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);
                GBufferPass.Record(renderGraph, camera, cull);
                DeferredLightPass.Record(renderGraph, camera);
                frameIndex++;
            }
        }
        else if (camera.cameraType == CameraType.Game)
        {
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
                case RenderType.RESTIR_GI:
                    // using (renderGraph.RecordAndExecute(renderGraphParams))
                    // {
                    //     using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);
                    //     GBufferPass.Record(renderGraph, camera, cull);
                    //     // DeferredLightPass.Record(renderGraph, camera);
                    //     frameIndex++;
                    // }
                    RenderCameraGBffer(camera, renderGraphParams, cameraData, gbufferHandle0, gbufferHandle1, gbufferHandle2, gbufferHandle3, gbufferHandle4);
                    if (RenderReSTIR(camera, renderGraphParams, cameraData, outputRTHandle))
                    {
                        cmd.Blit(cameraData.rayTracingOutput, camera.activeTexture);
                    }
                    else
                    {
                        cmd.ClearRenderTarget(false, true, Color.black);
                        Debug.Log("Error occurred when ReSTIR!");
                    }
                    break;
            }
        }

        outputRTHandle.Release();


        context.ExecuteCommandBuffer(cmd);

        cmd.Release();

        context.Submit();

        renderGraph.EndFrame();

        cameraData.UpdateCameraData();

        EndCameraRendering(context, camera);

    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (!ValidateRayTracing() && !SetupShaders())
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

