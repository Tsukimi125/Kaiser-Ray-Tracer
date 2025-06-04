using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
public class ReSTIRGIPass
{
    static readonly ProfilingSampler
        samplerOpaque = new("Opaque Geometry");

    static readonly ShaderTagId shaderTagID = new("ReSTIRGIPass");

    private TextureHandle outputTexture;
    private TextureHandle output;
    private TextureHandle finalOutput;
    private TextureHandle tReservoir;
    private TextureHandle tReservoir2;
    private TextureHandle sReservoir;
    private TextureHandle directIllumination;
    private TextureHandle indirectDiffuse;
    private TextureHandle indirectSpecular;
    static int frameIndex = 0;

    public static void Record(
        RenderGraph renderGraph,
        Camera camera,
        RayTracingAccelerationStructure rtas,
        KaiserRayTracerAsset renderPipelineAsset
        )
    {
        RenderGraphBuilder builder = renderGraph.AddRenderPass<ReSTIRGIPass>("ReSTIRGI Pass Pass", out var pass);

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
        
        TextureDesc reservoirDesc = new TextureDesc()
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

        RayTracingShader restirGIPassShader = Resources.Load<RayTracingShader>("Shaders/ReSTIRGI");

        pass.outputTexture = builder.CreateTransientTexture(desc);
        // TextureHandle output = renderGraph.ImportTexture(pass.outputTexture);
        // pass.outputTexture = builder.WriteTexture(output);
        
        // TextureHandle output = renderGraph.ImportTexture(outputRTHandle);
        TextureHandle tReservoir = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.Temporal);
        TextureHandle tReservoir2 = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.LastFrameTemporal);
        TextureHandle sReservoir = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.Spatial);
        TextureHandle directIllumination = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.DirectIllumination);
        TextureHandle indirectDiffuse = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.IndirectDiffuse);
        TextureHandle indirectSpecular = renderGraph.ImportTexture(KaiserRayTracer.ReservoirBuffers.IndirectSpecular);

        // pass.outputTexture = builder.WriteTexture(output);
        pass.tReservoir = builder.WriteTexture(tReservoir);
        pass.tReservoir2 = builder.WriteTexture(tReservoir2);
        pass.sReservoir = builder.WriteTexture(sReservoir);
        pass.directIllumination = builder.WriteTexture(directIllumination);
        pass.indirectDiffuse = builder.WriteTexture(indirectDiffuse);
        pass.indirectSpecular = builder.WriteTexture(indirectSpecular);
        
        builder.SetRenderFunc((ReSTIRGIPass pass, RenderGraphContext ctx) =>
        {
            ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

            ctx.cmd.SetRayTracingShaderPass(restirGIPassShader, "RayTracing");

            float zoom = Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f);
            float aspectRatio = camera.pixelWidth / (float)camera.pixelHeight;

            ctx.cmd.SetGlobalInt(Shader.PropertyToID("_RE_MaxBounceCount"), (int)renderPipelineAsset.restirBounceCount);
            ctx.cmd.SetGlobalInt(Shader.PropertyToID("_RE_ResSTIRType"), (int)renderPipelineAsset.restirAccumulateType);

            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_LongPath"), renderPipelineAsset.restirLongPath ? 1 : 0);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_TReservoirSize"), renderPipelineAsset.restirTReservoirSize);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_SReservoirSize"), renderPipelineAsset.restirSReservoirSize);

            ctx.cmd.SetRayTracingAccelerationStructure(restirGIPassShader, Shader.PropertyToID("_AccelStruct"), rtas);
            ctx.cmd.SetRayTracingFloatParam(restirGIPassShader, Shader.PropertyToID("_RE_Zoom"), zoom);
            ctx.cmd.SetRayTracingFloatParam(restirGIPassShader, Shader.PropertyToID("_RE_AspectRatio"), aspectRatio);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_ConvergenceStep"), frameIndex);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_FrameIndex"), frameIndex);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_MaxFrameCount"), renderPipelineAsset.accumulateMaxFrame);
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_RE_EnvTex"), renderPipelineAsset.envTexture);
            ctx.cmd.SetRayTracingFloatParam(restirGIPassShader, Shader.PropertyToID("_RE_EnvIntensity"), renderPipelineAsset.envIntensity);
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_Output"), pass.outputTexture);
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_SReservoir"), pass.sReservoir);
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_DirectIllumination"), pass.directIllumination);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_TReservoirSize"), renderPipelineAsset.restirTReservoirSize);
            ctx.cmd.SetRayTracingIntParam(restirGIPassShader, Shader.PropertyToID("_RE_SReservoirSize"), renderPipelineAsset.restirSReservoirSize);
            // if (frameIndex % 2 == 0)
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_TReservoir"), pass.tReservoir);
            ctx.cmd.SetRayTracingTextureParam(restirGIPassShader, Shader.PropertyToID("_LastFrameTReservoir"), pass.tReservoir2);
            
            ctx.cmd.DispatchRays(restirGIPassShader, "ReSTIR_Diffuse_Temporal", (uint)camera.pixelWidth, (uint)camera.pixelHeight, 1, camera);
            ctx.cmd.CopyTexture(pass.tReservoir, pass.tReservoir2);
            if (renderPipelineAsset.restirSampleType != ReSTIRSampleType.DIFFUSE)
            {
                int kernel = 1;
                Vector4 bufferSize = new Vector4(camera.pixelWidth, camera.pixelHeight, 1.0f / camera.pixelWidth, 1.0f / camera.pixelHeight);
                
                ctx.cmd.SetComputeVectorParam(KaiserRayTracer.KaiserShaders.postprocessPass, "_Screen_Resolution", bufferSize);
                ctx.cmd.SetComputeFloatParam(KaiserRayTracer.KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 1.0f);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), pass.outputTexture);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), pass.indirectDiffuse);
                ctx.cmd.DispatchCompute(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                ctx.cmd.SetComputeFloatParam(KaiserRayTracer.KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 2.0f);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), pass.indirectDiffuse);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), pass.outputTexture);
                // ctx.cmd.DispatchCompute(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                // ctx.cmd.SetComputeFloatParam(KaiserRayTracer.KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 4.0f);
                // ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), pass.outputTexture);
                // ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), pass.indirectDiffuse);
                // ctx.cmd.DispatchCompute(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                // ctx.cmd.SetComputeFloatParam(KaiserRayTracer.KaiserShaders.postprocessPass, "_Screen_DenoiseKernelSize", 8.0f);
                // ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Input"), pass.indirectDiffuse);
                // ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Output"), pass.outputTexture);
                
                kernel = 0;
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_DirectIllumination"), pass.directIllumination);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_DiffuseReservoir"), pass.indirectDiffuse);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_SpecularReservoir"), pass.indirectSpecular);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_History"), KaiserRayTracer.PostprocessBuffers.History);
                ctx.cmd.SetComputeTextureParam(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, Shader.PropertyToID("_Result"), pass.outputTexture);
                ctx.cmd.DispatchCompute(KaiserRayTracer.KaiserShaders.postprocessPass, kernel, camera.pixelWidth / 8, camera.pixelHeight / 8, 1);
                ctx.cmd.Blit(pass.outputTexture, KaiserRayTracer.PostprocessBuffers.History);
            }
            frameIndex++;
            ctx.cmd.Blit(pass.outputTexture, camera.activeTexture);
        });

    }
}
