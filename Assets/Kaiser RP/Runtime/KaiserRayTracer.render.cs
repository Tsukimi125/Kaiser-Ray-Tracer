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

        var reTemporalDesc = new RenderTextureDescriptor()
        {
            dimension = TextureDimension.Tex2D,
            width = camera.pixelWidth,
            height = camera.pixelHeight,
            depthBufferBits = 0,
            volumeDepth = 1,
            msaaSamples = 1,
            graphicsFormat = GraphicsFormat.R32G32B32A32_UInt,
            enableRandomWrite = true,
        };

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, colorFormat: GraphicsFormat.R32G32B32A32_UInt, 0);
        descriptor.enableRandomWrite = true;

        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.Temporal, reTemporalDesc, FilterMode.Point, TextureWrapMode.Clamp, name: "_TReservoir");
        RenderingUtils.ReAllocateIfNeeded(ref ReservoirBuffers.Spatial, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_SReservoir");

        if (camera.cameraType == CameraType.SceneView)
        {
            // RenderCameraGBffer(camera, renderGraphParams, cameraData, gbufferHandle0, gbufferHandle1, gbufferHandle2, gbufferHandle3);
            // RenderLightPass(camera, renderGraphParams, cameraData);
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
                    using (renderGraph.RecordAndExecute(renderGraphParams))
                    {
                        using var _ = new RenderGraphProfilingScope(renderGraph, cameraSampler);
                        GBufferPass.Record(renderGraph, camera, cull, true, -1);
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
                            ctx.cmd.Blit(passData.outputTexture, camera.activeTexture);
                        });
                    }


                    // RenderCameraGBffer(camera, renderGraphParams, cameraData, gbufferHandle0, gbufferHandle1, gbufferHandle2, gbufferHandle3);
                    // if (RenderReSTIR(camera, renderGraphParams, cameraData, outputRTHandle))
                    // {
                    //     cmd.Blit(cameraData.rayTracingOutput, camera.activeTexture);
                    // }
                    // else
                    // {
                    //     cmd.ClearRenderTarget(false, true, Color.black);
                    //     Debug.Log("Error occurred when ReSTIR!");
                    // }
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

