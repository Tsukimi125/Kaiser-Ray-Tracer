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
public enum ReSTIRType { SINGLE_FRAME, TEMPORAL_ONLY, SPATIOTEMPORAL };

[CreateAssetMenu(menuName = "Rendering/KaiserRayTracerAsset")]
public class KaiserRayTracerAsset : RenderPipelineAsset
{
    [Header("Global Settings")]
    public RenderType renderType = RenderType.PATH_TRACING;
    [Header("Environment Settings")]
    public Cubemap envTexture = null;
    [Range(0, 4)]
    public float envIntensity = 0.5f;
    [Header("Accumulate Settings")]
    public AccumulateType accumulateType = AccumulateType.SINGLE_FRAME;
    [Range(1, 128)]
    public int accumulateMaxFrame = 64;

    [Header("Path Tracing Settings")]

    [Range(0, 16)]
    public uint ptBounceCount = 8;
    [Header("ReSTIR Settings")]
    public ReSTIRType restirType = ReSTIRType.SPATIOTEMPORAL;
    [Range(1, 16)]
    public uint restirBounceCount = 1;
    [Range(1, 64)]
    public int restirTReservoirSize = 20;
    [Range(1, 64)]
    public int restirSReservoirSize = 12;
    public bool restirLongPath = false;

    [Header("Active Cameras")]
    public CameraType activeCameraType;

    protected override RenderPipeline CreatePipeline() => new KaiserRayTracer(this);
}
