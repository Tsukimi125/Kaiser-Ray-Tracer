using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;



public partial class RayTracingRenderPipelineInstance : RenderPipeline
{
    private RayTracingRenderPipelineAsset renderPipelineAsset;

    private RayTracingAccelerationStructure rtas = null;

    private RenderGraph renderGraph = null;

    private RTHandleSystem rtHandleSystem = null;
    private RayTracingVirtualLighting lighting = new RayTracingVirtualLighting();

    private int convergenceStep = 0;

    private struct Reservoir
    {
        Vector3 vPosW;   // visible point
        Vector3 vNormW;  // visible surface normal
        Vector3 sPosW;   // sample point
        Vector3 sNormW;  // sample surface normal
        Vector3 radiance;  // outgoing radiance at sample point in RGB

        int M;
        float weightSum;
        int age;
    }

    class RayTracingRenderPassData
    {
        public TextureHandle outputTexture;
    };

    public RayTracingRenderPipelineInstance(RayTracingRenderPipelineAsset asset)
    {
        renderPipelineAsset = asset;

        var settings = new RayTracingAccelerationStructure.RASSettings(
            RayTracingAccelerationStructure.ManagementMode.Manual,
            RayTracingAccelerationStructure.RayTracingModeMask.Everything,
            255
        );
        // {
        //     rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything,
        //     managementMode = RayTracingAccelerationStructure.ManagementMode.Manual,
        //     layerMask = 255
        // };

        rtas = new RayTracingAccelerationStructure(settings);

        renderGraph = new RenderGraph("Ray Tracing Render Graph");

        rtHandleSystem = new RTHandleSystem();
    }

    protected override void Dispose(bool disposing)
    {
        if (rtas != null)
        {
            rtas.Release();
            rtas = null;
        }

        renderGraph.Cleanup();
        renderGraph = null;

        rtHandleSystem.Dispose();
    }

    private bool ValidateRayTracing()
    {
        if (!SystemInfo.supportsRayTracing)
        {
            Debug.Log("Ray Tracing API is not supported!");
            return false;
        }

        if (rtas == null)
        {
            Debug.Log("Ray Tracing Acceleration Structure is null!");
            return false;
        }

        return true;
    }

    private void CullInstance()
    {
        var instanceCullingTest = new RayTracingInstanceCullingTest()
        {
            allowOpaqueMaterials = true,
            allowTransparentMaterials = false,
            allowAlphaTestedMaterials = true,
            layerMask = -1,
            shadowCastingModeMask = (1 << (int)ShadowCastingMode.Off)
            | (1 << (int)ShadowCastingMode.On)
            | (1 << (int)ShadowCastingMode.TwoSided),
            instanceMask = 1 << 0,
        };

        var instanceCullingTests = new List<RayTracingInstanceCullingTest>() { instanceCullingTest };

        var cullingConfig = new RayTracingInstanceCullingConfig()
        {
            flags = RayTracingInstanceCullingFlags.None,
            subMeshFlagsConfig = new RayTracingSubMeshFlagsConfig()
            {
                opaqueMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly,
                transparentMaterials = RayTracingSubMeshFlags.Disabled,
                alphaTestedMaterials = RayTracingSubMeshFlags.Enabled,
            },
            instanceTests = instanceCullingTests.ToArray(),
        };

        rtas.ClearInstances();
        rtas.CullInstances(ref cullingConfig);
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        if (!ValidateRayTracing())
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.ClearRenderTarget(true, true, Color.magenta);
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
            cmd.Release();
            return;
        }

        CullInstance();

        lighting.Setup(context);

        foreach (Camera camera in cameras)
        {
            
            
            if (!camera.TryGetComponent<AdditionalCameraData>(out var additionalData))
            {
                additionalData = camera.gameObject.AddComponent<AdditionalCameraData>();
                additionalData.hideFlags = HideFlags.HideAndDontSave;
            }

            if (additionalData.UpdateCameraResources()) convergenceStep = 0;

            CommandBuffer cmd = new CommandBuffer();



            if ((camera.cameraType & renderPipelineAsset.activeCameraType) > 0)
            {
                context.SetupCameraProperties(camera);

                var renderGraphParams = new RenderGraphParameters()
                {
                    scriptableRenderContext = context,
                    commandBuffer = cmd,
                    currentFrameIndex = additionalData.frameIndex
                };

                RTHandle outputRTHandle = rtHandleSystem.Alloc(additionalData.rayTracingOutput, "g_Output");

                switch (renderPipelineAsset.rayTracingStyle)
                {
                    case RayTracingStyle.PATH_TRACING:
                        if (DoPathTracing(camera, outputRTHandle, renderGraphParams, additionalData))
                        {
                            cmd.Blit(additionalData.rayTracingOutput, camera.activeTexture);
                        }
                        else
                        {
                            cmd.ClearRenderTarget(false, true, Color.black);
                            Debug.Log("Error occurred when Path Tracing!");
                        }
                        break;
                    case RayTracingStyle.RESTIR_DI:
                        if (DoRestirGI(camera, outputRTHandle, renderGraphParams, additionalData))
                        {
                            cmd.Blit(additionalData.rayTracingOutput, camera.activeTexture);
                        }
                        else
                        {
                            cmd.ClearRenderTarget(false, true, Color.black);
                            Debug.Log("Error occurred when Path Tracing!");
                        }
                        break;
                }

                outputRTHandle.Release();
            }
            else
            {
                cmd.ClearRenderTarget(false, true, Color.black);
            }

            context.ExecuteCommandBuffer(cmd);

            cmd.Release();

            context.Submit();

            renderGraph.EndFrame();

            additionalData.UpdateCameraData();
        }
    }


}