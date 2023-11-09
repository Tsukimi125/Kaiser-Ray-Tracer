using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    void RenderCameraGBffer(Camera camera, RenderGraphParameters renderGraphParams, KaiserCameraData cameraData, RTHandle gbufferHandle0, RTHandle gbufferHandle1, RTHandle gbufferHandle2, RTHandle gbufferHandle3)
    {
        if (KaiserShaders.gbuffer == null)
        {
            Debug.Log("Ray Traced GBuffer Shader is null!");
            return;
        }

        // RTHandle gbufferHandle0 = rtHandleSystem.Alloc(cameraData.gbuffer0, "_GBuffer0");
        // RTHandle gbufferHandle1 = rtHandleSystem.Alloc(cameraData.gbuffer1, "_GBuffer1");
        // RTHandle gbufferHandle2 = rtHandleSystem.Alloc(cameraData.gbuffer2, "_GBuffer2");
        // RTHandle gbufferHandle3 = rtHandleSystem.Alloc(cameraData.gbuffer3, "_GBuffer3");

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            RenderGraphBuilder builder = renderGraph.AddRenderPass<GBufferRenderPassData>("RT GBuffer Pass", out var passData);

            passData.gbuffer0 = builder.WriteTexture(renderGraph.ImportTexture(gbufferHandle0));
            passData.gbuffer1 = builder.WriteTexture(renderGraph.ImportTexture(gbufferHandle1));
            passData.gbuffer2 = builder.WriteTexture(renderGraph.ImportTexture(gbufferHandle2));
            passData.gbuffer3 = builder.WriteTexture(renderGraph.ImportTexture(gbufferHandle3));

            builder.SetRenderFunc((GBufferRenderPassData data, RenderGraphContext ctx) =>
            {
                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.gbuffer, Shader.PropertyToID("_RTGBuffer_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.gbuffer, Shader.PropertyToID("_RTGBuffer_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.gbuffer, Shader.PropertyToID("_RTGBuffer_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.gbuffer, Shader.PropertyToID("_RTGBuffer_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);
                ctx.cmd.SetRayTracingShaderPass(KaiserShaders.gbuffer, "RayTracing");
                ctx.cmd.SetRayTracingAccelerationStructure(KaiserShaders.gbuffer, Shader.PropertyToID("_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.gbuffer, Shader.PropertyToID("_GBuffer0"), data.gbuffer0);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.gbuffer, Shader.PropertyToID("_GBuffer1"), data.gbuffer1);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.gbuffer, Shader.PropertyToID("_GBuffer2"), data.gbuffer2);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.gbuffer, Shader.PropertyToID("_GBuffer3"), data.gbuffer3);
                ctx.cmd.DispatchRays(KaiserShaders.gbuffer, "GBffuerRayGenShader", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                ctx.cmd.SetGlobalTexture(Shader.PropertyToID("_GBuffer0"), data.gbuffer0);
                ctx.cmd.SetGlobalTexture(Shader.PropertyToID("_GBuffer1"), data.gbuffer1);
                ctx.cmd.SetGlobalTexture(Shader.PropertyToID("_GBuffer2"), data.gbuffer2);
                ctx.cmd.SetGlobalTexture(Shader.PropertyToID("_GBuffer3"), data.gbuffer3);
            });

            frameIndex++;
        }


    }

}

