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

public enum AccumulateType { SINGLE_FRAME, MAX_FRAME, UNLIMITED_FRAME };
public enum ReSTIRAccumulateType { SINGLE_FRAME, TEMPORAL_ONLY, SPATIOTEMPORAL };
public enum ReSTIRSampleType { BRDF, DIFFUSE, SPECULAR, HIERARCHY };

[CreateAssetMenu(menuName = "Rendering/KaiserRayTracerAsset")]
public class KaiserRayTracerAsset : RenderPipelineAsset
{
    [Header("Global Settings")]
    public RenderType renderType = RenderType.PATH_TRACING;
    [Range(0, 128)]
    public float radianceClamp = 16.0f;
    [Header("Environment Settings")]
    public Cubemap envTexture = null;
    [Range(0, 4)]
    public float envIntensity = 0.5f;
    [Header("Accumulate Settings")]
    public AccumulateType accumulateType = AccumulateType.SINGLE_FRAME;
    [Range(1, 128)]
    public int accumulateMaxFrame = 64;

    [Header("Path Tracing Settings")]

    [Range(0, 32)]
    public uint ptBounceCount = 8;
    [Header("ReSTIR Settings")]
    public ReSTIRAccumulateType restirAccumulateType = ReSTIRAccumulateType.SPATIOTEMPORAL;
    public ReSTIRSampleType restirSampleType = ReSTIRSampleType.BRDF;
    [Range(0, 32)]
    public uint restirBounceCount = 1;
    [Range(1, 64)]
    public int restirTReservoirSize = 20;
    [Range(1, 500)]
    public int restirSReservoirSize = 12;
    public bool restirLongPath = false;

    [Header("Active Cameras")]
    public CameraType activeCameraType;

    protected override RenderPipeline CreatePipeline() => new KaiserRayTracer(this);
}
