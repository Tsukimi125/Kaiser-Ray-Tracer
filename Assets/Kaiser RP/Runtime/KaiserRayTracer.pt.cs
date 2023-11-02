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

        if (renderPipelineAsset.pathTracingShader == null)
        {
            Debug.Log("Ray Tracing Shader is null!");
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

                ctx.cmd.SetRayTracingShaderPass(renderPipelineAsset.pathTracingShader, "PathTracing");

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

                ctx.cmd.SetRayTracingAccelerationStructure(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_ConvergenceStep"), frameIndex);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_FrameIndex"), cameraData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_SamplePerPixel"), (int)renderPipelineAsset.samplePerPixel);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_EnvIntensity"), renderPipelineAsset.envIntensity);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_DebugTex"), debugTexture);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("_PT_Output"), passData.outputTexture);

                ctx.cmd.DispatchRays(renderPipelineAsset.pathTracingShader, "PathTracingRayGenShader", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                frameIndex++;
            });
        }

        return true;
    }


}

