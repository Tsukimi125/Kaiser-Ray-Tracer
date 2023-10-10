using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;



public partial class RayTracingRenderPipelineInstance : RenderPipeline
{

    private bool DoPathTracing(Camera camera, RTHandle outputRTHandle, RenderGraphParameters renderGraphParams, AdditionalCameraData additionalData)
    {

        if (renderPipelineAsset.pathTracingShader == null)
        {
            Debug.Log("Ray Tracing Shader is null!");
            return false;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            TextureHandle output = renderGraph.ImportTexture(outputRTHandle);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<RayTracingRenderPassData>("Path Tracing Pass", out var passData);

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

            builder.SetRenderFunc((RayTracingRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(renderPipelineAsset.pathTracingShader, "PathTracing");

                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("g_BounceCountOpaque"), (int)renderPipelineAsset.bounceCountOpaque);
                ctx.cmd.SetGlobalInt(Shader.PropertyToID("g_BounceCountTransparent"), (int)renderPipelineAsset.bounceCountTransparent);

                ctx.cmd.SetRayTracingAccelerationStructure(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_ConvergenceStep"), convergenceStep);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_FrameIndex"), additionalData.frameIndex);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_SamplePerPixel"), (int)renderPipelineAsset.samplePerPixel);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_DebugTex"), debugTexture);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.pathTracingShader, Shader.PropertyToID("g_Output"), passData.outputTexture);

                ctx.cmd.DispatchRays(renderPipelineAsset.pathTracingShader, "PathTracingRayGenShader", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                convergenceStep++;
            });
        }

        return true;
    }
}