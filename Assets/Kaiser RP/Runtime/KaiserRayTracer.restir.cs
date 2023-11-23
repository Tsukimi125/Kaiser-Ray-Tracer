using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    private bool RenderReSTIR(Camera camera, RenderGraphParameters renderGraphParams, KaiserCameraData cameraData, RTHandle outputRTHandle)
    {
        if (KaiserShaders.restir == null)
        {
            Debug.Log("Reference Path Tracer Shader is null!");
            return false;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            TextureHandle output = renderGraph.ImportTexture(outputRTHandle);
            TextureHandle output1 = renderGraph.ImportTexture(ReservoirBuffers.Temporal);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<ReSTIRRenderPassData>("ReSTIR Pass", out var passData);

            passData.outputTexture = builder.WriteTexture(output);
            passData.temporalReservoir = builder.WriteTexture(output1);

            builder.SetRenderFunc((ReSTIRRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(KaiserShaders.restir, "RayTracing");

                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_MaxBounceCount"), (int)renderPipelineAsset.ptBounceCount);
                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_Progressive"), (int)renderPipelineAsset.accumulateType);

                ctx.cmd.SetRayTracingAccelerationStructure(KaiserShaders.restir, Shader.PropertyToID("_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_MaxFrameCount"), renderPipelineAsset.accumulateMaxFrame);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_PT_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_EnvIntensity"), renderPipelineAsset.envIntensity);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_PT_Output"), passData.outputTexture);

                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);

                ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                frameIndex++;
            });
        }
        return true;
    }


}

