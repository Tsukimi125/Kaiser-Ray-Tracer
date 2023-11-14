using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

public class KaiserCameraData : MonoBehaviour
{
    [HideInInspector]
    public int frameIndex = 0;

    [HideInInspector]
    public RenderTexture rayTracingOutput = null;
    [HideInInspector]
    public RenderTexture gbuffer0 = null;
    [HideInInspector]
    public RenderTexture gbuffer1 = null;
    [HideInInspector]
    public RenderTexture gbuffer2 = null;
    [HideInInspector]
    public RenderTexture gbuffer3 = null;

    private Camera _camera;

    private Matrix4x4 _prevCameraMatrix = Matrix4x4.zero;

    private void Start()
    {
        frameIndex = 0;

        _camera = GetComponent<Camera>();
    }

    public void UpdateCameraData()
    {
        frameIndex++;
    }

    public bool UpdateCameraResources()
    {
        if (_camera == null) _camera = GetComponent<Camera>();

        if (rayTracingOutput == null || rayTracingOutput.width != _camera.pixelWidth || rayTracingOutput.height != _camera.pixelHeight)
        {
            if (rayTracingOutput) rayTracingOutput.Release();

            var rtDesc = new RenderTextureDescriptor()
            {
                dimension = TextureDimension.Tex2D,
                width = _camera.pixelWidth,
                height = _camera.pixelHeight,
                depthBufferBits = 0,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite = true,
            };

            var gbufferAlbedoDesc = new RenderTextureDescriptor()
            {
                dimension = TextureDimension.Tex2D,
                width = _camera.pixelWidth,
                height = _camera.pixelHeight,
                depthBufferBits = 0,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R16G16B16A16_UNorm,
                enableRandomWrite = true,
            };

            var gbufferNormalDesc = new RenderTextureDescriptor()
            {
                dimension = TextureDimension.Tex2D,
                width = _camera.pixelWidth,
                height = _camera.pixelHeight,
                depthBufferBits = 0,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SNorm,
                enableRandomWrite = true,
            };

            var gbufferWorldPosDesc = new RenderTextureDescriptor()
            {
                dimension = TextureDimension.Tex2D,
                width = _camera.pixelWidth,
                height = _camera.pixelHeight,
                depthBufferBits = 0,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                enableRandomWrite = true,
            };

            var gbufferRMAODesc = new RenderTextureDescriptor()
            {
                dimension = TextureDimension.Tex2D,
                width = _camera.pixelWidth,
                height = _camera.pixelHeight,
                depthBufferBits = 0,
                volumeDepth = 1,
                msaaSamples = 1,
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                enableRandomWrite = true,
            };

            rayTracingOutput = new RenderTexture(rtDesc);
            rayTracingOutput.Create();

            gbuffer0 = new RenderTexture(gbufferAlbedoDesc);
            gbuffer0.Create();

            gbuffer1 = new RenderTexture(gbufferNormalDesc);
            gbuffer1.Create();

            gbuffer2 = new RenderTexture(gbufferWorldPosDesc);
            gbuffer2.Create();

            gbuffer3 = new RenderTexture(gbufferRMAODesc);
            gbuffer3.Create();

            return true;
        }

        if (_camera.cameraToWorldMatrix != _prevCameraMatrix)
        {
            _prevCameraMatrix = _camera.cameraToWorldMatrix;
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (rayTracingOutput != null)
        {
            rayTracingOutput.Release();
            rayTracingOutput = null;
        }

        if (gbuffer0 != null)
        {
            gbuffer0.Release();
            gbuffer0 = null;
        }

        if (gbuffer1 != null)
        {
            gbuffer1.Release();
            gbuffer1 = null;
        }

        if (gbuffer2 != null)
        {
            gbuffer2.Release();
            gbuffer2 = null;
        }

        if (gbuffer3 != null)
        {
            gbuffer3.Release();
            gbuffer3 = null;
        }
    }
}
