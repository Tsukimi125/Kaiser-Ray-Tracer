using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

public partial class KaiserRayTracer : RenderPipeline
{
    private KaiserRayTracerAsset renderPipelineAsset;
    private RayTracingAccelerationStructure rtas = null;
    private RenderGraph renderGraph = null;
    private RTHandleSystem rtHandleSystem = null;
    private int frameIndex = 0;
    private RayTracingVirtualLighting lighting = new RayTracingVirtualLighting();

    private void Cull()
    {

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

        if (KaiserShaders.referencePathTracer == null)
        {
            Debug.Log("Ray Tracing Shader is null!");
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
    public KaiserRayTracer(KaiserRayTracerAsset asset)
    {
        renderPipelineAsset = asset;

        var settings = new RayTracingAccelerationStructure.RASSettings(
            RayTracingAccelerationStructure.ManagementMode.Manual,
            RayTracingAccelerationStructure.RayTracingModeMask.Everything,
            255
        );

        rtas = new RayTracingAccelerationStructure(settings);

        renderGraph = new RenderGraph("Ray Tracing Render Graph");

        rtHandleSystem = new RTHandleSystem();



        SetupShaders();
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

        ReservoirBuffers.Temporal.Release();
    }
}

