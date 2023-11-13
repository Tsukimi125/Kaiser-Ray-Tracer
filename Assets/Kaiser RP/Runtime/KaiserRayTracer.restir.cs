using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    private bool RenderReSTIR(Camera camera, RTHandle outputRTHandle, RenderGraphParameters renderGraphParams, KaiserCameraData cameraData)
    {
        if (KaiserShaders.restir == null)
        {
            Debug.Log("Reference Path Tracer Shader is null!");
            return false;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            TextureHandle output = renderGraph.ImportTexture(outputRTHandle);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("ReSTIR Pass", out var passData);

            passData.outputTexture = builder.WriteTexture(output);

            builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(KaiserShaders.restir, "RayTracing");

                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_MaxBounceCount"), (int)renderPipelineAsset.bounceCount);
                // ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_BounceCountTransparent"), (int)renderPipelineAsset.bounceCountTransparent);

                if (renderPipelineAsset.progressive)
                {
                    ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_Progressive"), 1);
                }
                else
                {
                    ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_Progressive"), 0);
                }

                ctx.cmd.SetRayTracingAccelerationStructure(KaiserShaders.restir, Shader.PropertyToID("_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.restir, Shader.PropertyToID("_PT_SamplePerPixel"), (int)renderPipelineAsset.samplePerPixel);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_PT_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.restir, Shader.PropertyToID("_PT_EnvIntensity"), renderPipelineAsset.envIntensity);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.restir, Shader.PropertyToID("_PT_Output"), passData.outputTexture);

                ctx.cmd.DispatchRays(KaiserShaders.restir, "PathTracingRayGenShader", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                frameIndex++;
            });
        }

        return true;
    }


}

