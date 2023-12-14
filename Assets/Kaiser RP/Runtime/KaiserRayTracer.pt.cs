using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.VisualScripting;

public partial class KaiserRayTracer : RenderPipeline
{
    private bool RenderPathTracing(Camera camera, RTHandle outputRTHandle, RenderGraphParameters renderGraphParams, KaiserCameraData cameraData)
    {
        if (KaiserShaders.referencePathTracer == null)
        {
            Debug.Log("Reference Path Tracer Shader is null!");
            return false;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            TextureHandle output = renderGraph.ImportTexture(outputRTHandle);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("Path Tracing Pass", out var passData);

            passData.outputTexture = builder.WriteTexture(output);

            TextureDesc desc = new TextureDesc()
            {
                dimension = TextureDimension.Tex2D,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                depthBufferBits = 0,
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                slices = 1,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };
            TextureHandle debugTexture = builder.CreateTransientTexture(desc);

            builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(KaiserShaders.referencePathTracer, "RayTracing");

                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_MaxBounceCount"), (int)renderPipelineAsset.ptBounceCount);
                // ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_BounceCountTransparent"), (int)renderPipelineAsset.bounceCountTransparent);

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_Progressive"), (int)renderPipelineAsset.accumulateType);


                int randomInt = Random.Range(1, 65535);
                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_RandomSeed"), randomInt);

                ctx.cmd.SetRayTracingAccelerationStructure(KaiserShaders.referencePathTracer, Shader.PropertyToID("_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_RadianceClamp"), renderPipelineAsset.radianceClamp);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_MaxFrameCount"), renderPipelineAsset.accumulateMaxFrame);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingFloatParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_EnvIntensity"), renderPipelineAsset.envIntensity);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_PT_DebugTex"), debugTexture);
                ctx.cmd.SetRayTracingTextureParam(KaiserShaders.referencePathTracer, Shader.PropertyToID("_Output"), passData.outputTexture);

                ctx.cmd.DispatchRays(KaiserShaders.referencePathTracer, "PathTracingRayGenShader", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                frameIndex++;
            });
        }

        return true;
    }


}

