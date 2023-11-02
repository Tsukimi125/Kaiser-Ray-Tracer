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
    RESTIR_GI
};

[CreateAssetMenu(menuName = "Rendering/KaiserRayTracerAsset")]
public class KaiserRayTracerAsset : RenderPipelineAsset
{
    [Header("Global Settings")]
    public RenderType renderType = RenderType.PATH_TRACING;
    [Header("Environment Settings")]
    public Cubemap envTexture = null;
    [Range(0, 4)]
    public float envIntensity = 0.5f;
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
