using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

[SerializeField]
public enum RenderType
{
    PATH_TRACING,
    RCGI,
};

[CreateAssetMenu(menuName = "Rendering/KaiserRayTracerAsset")]
public class KaiserRayTracerAsset : RenderPipelineAsset
{
    [Header("Global Settings")]
    public RenderType renderType = RenderType.PATH_TRACING;
    [Header("Path Tracing Settings")]
    public RayTracingShader pathTracingShader;
        public bool progressive = false;
    [Range(1, 64)]
    public int samplePerPixel = 1;
    [Range(1, 16)]
    public uint bounceCount = 8;

    [Header("Active Cameras")]
    public CameraType activeCameraType;

    protected override RenderPipeline CreatePipeline() => new KaiserRayTracer(this);
}