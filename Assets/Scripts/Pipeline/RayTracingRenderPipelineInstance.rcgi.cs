using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;



public partial class RayTracingRenderPipelineInstance : RenderPipeline
{
    private bool DoRCGI(Camera camera, RTHandle outputRTHandle, RenderGraphParameters renderGraphParams, AdditionalCameraData additionalData)
    {
        return false;
    }

}