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
            TextureHandle tReservoir = renderGraph.ImportTexture(ReservoirBuffers.Temporal);
            TextureHandle sReservoir = renderGraph.ImportTexture(ReservoirBuffers.Spatial);
            TextureHandle directIllumination = renderGraph.ImportTexture(ReservoirBuffers.DirectIllumination);


            RenderGraphBuilder builder = renderGraph.AddRenderPass<ReSTIRRenderPassData>("ReSTIR Pass", out var passData);

            passData.outputTexture = builder.WriteTexture(output);
            passData.temporalReservoir = builder.WriteTexture(tReservoir);
            passData.spatialReservoir = builder.WriteTexture(sReservoir);
            passData.directIllumination = builder.WriteTexture(directIllumination);


            builder.SetRenderFunc((ReSTIRRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(KaiserShaders.restir, "RayTracing");

                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_RE_MaxBounceCount"), (int)renderPipelineAsset.restirBounceCount);
                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_RE_ResSTIRType"), (int)renderPipelineAsset.restirAccumulateType);

                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_LongPath"), renderPipelineAsset.restirLongPath ? 1 : 0);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_TReservoirSize"), renderPipelineAsset.restirTReservoirSize);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_SReservoirSize"), renderPipelineAsset.restirSReservoirSize);

                ctx.cmd.SetRayTracingAccelerationStructure(KaiserShaders.restir, Shader.PropertyToID("_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_RE_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_RE_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_MaxFrameCount"), renderPipelineAsset.accumulateMaxFrame);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_RE_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_RE_EnvIntensity"), renderPipelineAsset.envIntensity);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Output"), passData.outputTexture);

                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);

                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_SReservoir"), passData.spatialReservoir);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_DirectIllumination"), passData.directIllumination);

                if (renderPipelineAsset.restirSampleType == ReSTIRSampleType.BRDF)
                {
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_BRDF_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                }
                else if (renderPipelineAsset.restirSampleType == ReSTIRSampleType.DIFFUSE)
                {
                    ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_TReservoirSize"), renderPipelineAsset.restirTReservoirSize);
                    ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_RE_SReservoirSize"), renderPipelineAsset.restirSReservoirSize);
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Diffuse_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                }
                else if (renderPipelineAsset.restirSampleType == ReSTIRSampleType.SPECULAR)
                {
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Specular_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                }
                else
                {

                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Specular_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);
                    // ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Diffuse_TReservoir"), ReservoirBuffers.DiffuseTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Diffuse_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Diffuse_TReservoir"), ReservoirBuffers.DiffuseTemporal);
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Combine", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                }

                // if (renderPipelineAsset.restirAccumulateType == ReSTIRAccumulateType.SPATIOTEMPORAL)
                // {

                //     ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_SReservoir"), passData.spatialReservoir);
                //     ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_DirectIllumination"), passData.directIllumination);
                //     ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Spatial", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                // }
                // ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Spatial", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                frameIndex++;
            });
        }
        return true;
    }


}

