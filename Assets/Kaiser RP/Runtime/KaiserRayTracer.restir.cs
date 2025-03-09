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
            TextureHandle finalOutput = renderGraph.ImportTexture(outputRTHandle);
            TextureHandle tReservoir = renderGraph.ImportTexture(ReservoirBuffers.Temporal);
            TextureHandle sReservoir = renderGraph.ImportTexture(ReservoirBuffers.Spatial);
            TextureHandle directIllumination = renderGraph.ImportTexture(ReservoirBuffers.DirectIllumination);
            TextureHandle indirectDiffuse = renderGraph.ImportTexture(ReservoirBuffers.IndirectDiffuse);
            TextureHandle IndirectSpecular = renderGraph.ImportTexture(ReservoirBuffers.IndirectSpecular);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<ReSTIRRenderPassData>("ReSTIR Pass", out var passData);

            passData.outputTexture = builder.WriteTexture(output);
            passData.temporalReservoir = builder.WriteTexture(tReservoir);
            passData.spatialReservoir = builder.WriteTexture(sReservoir);
            passData.directIllumination = builder.WriteTexture(directIllumination);
            passData.indirectDiffuse = builder.WriteTexture(indirectDiffuse);
            passData.indirectSpecular = builder.WriteTexture(IndirectSpecular);


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

                    int kernel = 1;
                    Vector4 bufferSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);

                    ctx.cmd.SetComputeVectorParam(KaiserShaders.postprocessPass, "_Screen_Resolution", bufferSize);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 1.0f);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 2.0f);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.indirectDiffuse);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.outputTexture);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 4.0f);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 8.0f);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.indirectDiffuse);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.outputTexture);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    // ctx.cmd.Blit(cameraData.rayTracingOutput, PostprocessBuffers.History);
                }
                else if (renderPipelineAsset.restirSampleType == ReSTIRSampleType.SPECULAR)
                {
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Specular_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                }
                else
                {
                    float denoiseKernelSizeMulti = 0.25f;

                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_SReservoir"), passData.spatialReservoir);
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);
                    // ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Diffuse_TReservoir"), ReservoirBuffers.DiffuseTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Diffuse_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Spatial", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                    int kernel = 1;
                    Vector4 bufferSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, Shader.PropertyToID("_Screen_FrameIndex"), cameraData.frameIndex);
                    ctx.cmd.SetComputeVectorParam(KaiserShaders.postprocessPass, "_Screen_Resolution", bufferSize);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 1.0f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 1.8f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.indirectDiffuse);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.outputTexture);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 3.4f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 5.6f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 8.0f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.outputTexture);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectDiffuse);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);

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
                    ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Specular_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 0.1f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectSpecular);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 0.2f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), passData.indirectSpecular);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.SetComputeFloatParam(KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 0.4f * denoiseKernelSizeMulti);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), ReservoirBuffers.SpecularTemporal);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), passData.indirectSpecular);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);

                    // ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_TReservoir"), passData.temporalReservoir);
                    // ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Diffuse_TReservoir"), ReservoirBuffers.DiffuseTemporal);
                    // ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_Specular_TReservoir"), ReservoirBuffers.SpecularTemporal);
                    // ctx.cmd.DispatchRays(KaiserShaders.restir, "ReSTIR_Combine", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                    kernel = 0;
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_DirectIllumination"), passData.directIllumination);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_DiffuseReservoir"), passData.indirectDiffuse);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_SpecularReservoir"), passData.indirectSpecular);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_History"), PostprocessBuffers.History);
                    ctx.cmd.SetComputeTextureParam(KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Result"), cameraData.rayTracingOutput);
                    ctx.cmd.DispatchCompute(KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                    ctx.cmd.Blit(cameraData.rayTracingOutput, PostprocessBuffers.History);
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

