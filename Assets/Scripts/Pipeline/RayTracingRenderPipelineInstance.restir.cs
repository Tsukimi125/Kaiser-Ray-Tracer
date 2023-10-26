using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;



public partial class RayTracingRenderPipelineInstance : RenderPipeline
{
    private bool DoRestirGI(Camera camera, RTHandle outputRTHandle, RenderGraphParameters renderGraphParams, AdditionalCameraData additionalData)
    {
        if (renderPipelineAsset.restirShader == null)
        {
            Debug.Log("ReSTIR Shader is null!");
            return false;
        }

        using (renderGraph.RecordAndExecute(renderGraphParams))
        {
            TextureHandle output = renderGraph.ImportTexture(outputRTHandle);

            RenderGraphBuilder builder = renderGraph.AddRenderPass<RayTracingRenderPassData>("ReSITR GI Pass", out var passData);

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

            TextureDesc descRandom = new TextureDesc()
            {
                dimension = TextureDimension.Tex2D,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                depthBufferBits = 0,
                colorFormat = GraphicsFormat.R16_UNorm,
                slices = 1,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };

            TextureDesc descUNorm = new TextureDesc()
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

            TextureDesc descSNorm = new TextureDesc()
            {
                dimension = TextureDimension.Tex2D,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                depthBufferBits = 0,
                colorFormat = GraphicsFormat.R16G16B16A16_SNorm,
                slices = 1,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };

            TextureDesc descSFloat = new TextureDesc()
            {
                dimension = TextureDimension.Tex2D,
                width = camera.pixelWidth,
                height = camera.pixelHeight,
                depthBufferBits = 0,
                colorFormat = GraphicsFormat.R32G32B32A32_SFloat,
                slices = 1,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
            };



            TextureHandle gVPos = builder.CreateTransientTexture(descSFloat);
            TextureHandle gVNorm = builder.CreateTransientTexture(descSNorm);
            TextureHandle gVColor = builder.CreateTransientTexture(descUNorm);

            TextureHandle gSPos = builder.CreateTransientTexture(descSFloat);
            TextureHandle gSNorm = builder.CreateTransientTexture(descSNorm);
            TextureHandle gSColor = builder.CreateTransientTexture(descUNorm);

            TextureHandle gRandom = builder.CreateTransientTexture(descRandom);


            int reservoirCount = camera.pixelWidth * camera.pixelHeight;
            // reservoirBuffer = new ComputeBuffer(reservoirCount, 72);
            // // reservoirBuffer = builder.CreateTransientComputeBuffer(new ComputeBufferDesc(reservoirCount, 4, ComputeBufferType.Default));
            // Reservoir[] reservoirs = new Reservoir[reservoirCount];
            // reservoirBuffer.SetData(reservoirs);

            // Debug.Log(reservoirBuffer);

            builder.SetRenderFunc((RayTracingRenderPassData data, RenderGraphContext ctx) =>
            {
                ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                ctx.cmd.SetRayTracingShaderPass(renderPipelineAsset.restirShader, "PathTracing");
                float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
                float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

                ctx.cmd.SetGlobalInt(Shader.PropertyToID("_PT_MaxBounceCount"), (int)renderPipelineAsset.bounceCountOpaque);
                ctx.cmd.SetGlobalInt(Shader.PropertyToID("g_BounceCountTransparent"), (int)renderPipelineAsset.bounceCountTransparent);

                ctx.cmd.SetRayTracingAccelerationStructure(renderPipelineAsset.restirShader, Shader.PropertyToID("_PT_AccelStruct"), rtas);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_Zoom"), zoom);
                ctx.cmd.SetRayTracingFloatParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_AspectRatio"), aspectRatio);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_ConvergenceStep"), convergenceStep);
                ctx.cmd.SetRayTracingIntParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_FrameIndex"), additionalData.frameIndex);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_EnvTex"), renderPipelineAsset.envTexture);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_DebugTex"), debugTexture);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_Output"), passData.outputTexture);

                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_VPos"), gVPos);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_VNorm"), gVNorm);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_VColor"), gVColor);

                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_SPos"), gSPos);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_SNorm"), gSNorm);
                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_SColor"), gSColor);

                ctx.cmd.SetRayTracingTextureParam(renderPipelineAsset.restirShader, Shader.PropertyToID("g_Random"), gRandom);

                ctx.cmd.DispatchRays(renderPipelineAsset.restirShader, "ReSTIRRayGen_InitialSample", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);

                convergenceStep++;
            });
        }

        return true;
    }

}