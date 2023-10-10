using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[SerializeField]
public enum RayTracingStyle
{
    PATH_TRACING,
    RESTIR_DI,
};

public enum ReSTIRGBufferDebugMode
{
    None,
    VPos,
    VNorm,
    VColor,
    SPos,
    SNorm,
    SColor,
};

[CreateAssetMenu(menuName = "Rendering/RayTracingRenderPipelineAsset")]
public class RayTracingRenderPipelineAsset : RenderPipelineAsset
{
    [Header("Path Tracing Assets")]
    public RayTracingShader pathTracingShader;
    [Header("RESTIR Assets")]
    public RayTracingShader restirShader;

    [Header("Environment Settings")]
    public Cubemap envTexture = null;

    [Header("Global RT Settings")]
    public RayTracingStyle rayTracingStyle = RayTracingStyle.PATH_TRACING;
    public uint bounceCount = 8;

    [Header("Path Tracing Settings")]
    public bool progressive = false;
    [Range(1, 64)]
    public int samplePerPixel = 1;

    [Header("ReSTIR Settings")]
    public ReSTIRGBufferDebugMode restirGBufferDebugMode = ReSTIRGBufferDebugMode.None;

    [Header("Active Cameras")]
    public CameraType activeCameraType;

    [Range(1, 100)]
    public uint bounceCountOpaque = 5;
    [Range(1, 100)]
    public uint bounceCountTransparent = 8;

    protected override RenderPipeline CreatePipeline() => new RayTracingRenderPipelineInstance(this);
}