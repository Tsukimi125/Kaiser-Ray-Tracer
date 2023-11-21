using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.Mathematics;
using UnityEngine.Rendering.Universal;
using UnityEngine.Profiling;

public partial class KaiserRayTracer : RenderPipeline
{
    private int frametime = 0;
    private bool initialize = false;
    private CommandBuffer buffer = new CommandBuffer { name = "Ircachhe" };
    private int3[] prev_scroll = new int3[12];
    private int3[] cur_scroll = new int3[12];
    private Vector4[] ircache_cascade_origin = new Vector4[12];
    private Vector4[] ircache_cascade_voxels_scrolled_this_frame = new Vector4[12];

    #region Buffers
    private ComputeBuffer Ircache_pool_buf = new(65536, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Ircache_life_buf = new(65536, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Ircache_grid_meta_buf = new(393216, sizeof(uint) * 2, ComputeBufferType.Raw);
    private ComputeBuffer Ircache_grid_meta_buf2 = new(393216, sizeof(uint) * 2, ComputeBufferType.Raw);
    private ComputeBuffer Ircache_meta_buf = new(8, sizeof(uint), ComputeBufferType.Raw);
    private ComputeBuffer Ircache_entry_cell_buf = new(65536, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Ircache_irradiance_buf = new(65536 * 3, sizeof(float) * 4, ComputeBufferType.Structured);
    private ComputeBuffer IrcacheDispatchArgs = new(4 * 2, sizeof(uint), ComputeBufferType.Raw);
    private ComputeBuffer Entry_occupancy_buf = new(65536, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Segment_sum_buf = new(1024, sizeof(uint), ComputeBufferType.Raw);
    private ComputeBuffer IrcacheDispatchArgs2 = new(4 * 4, sizeof(uint), ComputeBufferType.Raw);
    private ComputeBuffer Ircache_spatial_buf = new(65536, sizeof(float) * 4, ComputeBufferType.Structured);
    private ComputeBuffer Ircache_reposition_proposal_buf = new(65536, sizeof(float) * 4, ComputeBufferType.Structured);
    private ComputeBuffer Ircache_reposition_proposal_count_buf = new(65536, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Ircache_entry_indirection_buf = new(1048576, sizeof(uint), ComputeBufferType.Structured);
    private ComputeBuffer Ircache_aux_buf = new(65536 * 4 * 16, sizeof(float) * 4, ComputeBufferType.Structured);
    #endregion

    private void RenderIrcache(Camera camera, RenderGraphParameters renderGraphParameters)
    {

        TextureDesc skycube_desc = new TextureDesc()
        {
            dimension = TextureDimension.Tex2DArray,
            width = 64,
            height = 64,
            depthBufferBits = 0,
            slices = 6,
            colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
            msaaSamples = MSAASamples.None,
            enableRandomWrite = true,
        };

        TextureDesc convovlesky_desc = new TextureDesc()
        {
            dimension = TextureDimension.Tex2DArray,
            width = 16,
            height = 16,
            depthBufferBits = 0,
            slices = 6,
            colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
            msaaSamples = MSAASamples.None,
            enableRandomWrite = true,
        };

        using (renderGraph.RecordAndExecute(renderGraphParameters))
        {
            {
                ComputeShader skyCubeCS = Resources.Load<ComputeShader>("skyCubeCS");
                ComputeShader convolveSkyCS = Resources.Load<ComputeShader>("convolveSkyCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("skyCubeCS", out var passData);

                TextureHandle sky_cube = builder.CreateTransientTexture(skycube_desc);
                TextureHandle convolvesky = builder.CreateTransientTexture(convovlesky_desc);
                //TextureDesc skyCubeDesc = skycube_desc;
                //skyCubeDesc.dimension = TextureDimension.Cube;
                //TextureHandle skyCube = builder.CreateTransientTexture(skyCubeDesc);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = skyCubeCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeTextureParam(skyCubeCS, kernal, "output_tex", sky_cube);

                    ctx.cmd.DispatchCompute(skyCubeCS, kernal, 8, 8, 6);

                    //Graphics.CopyTexture(sky_cube, skyCube);
                    //kernal = convolveSkyCS.FindKernel("CSMain");

                    //ctx.cmd.SetComputeTextureParam(convolveSkyCS, kernal, "input_tex", skyCube);
                    //ctx.cmd.SetComputeTextureParam(convolveSkyCS, kernal, "output_tex", convolvesky);

                    //ctx.cmd.DispatchCompute(convolveSkyCS, kernal, 2, 2, 6);
                });
            }

            if (frametime == 1)
            {
                SwapHandle(Ircache_grid_meta_buf, Ircache_grid_meta_buf2);
            }

            if (!initialize)
            {
                //Debug.Log("Initialize");
                ComputeShader clearIrcachePoolCS = Resources.Load<ComputeShader>("clearIrcachePoolCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("clearIrcachePoolCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = clearIrcachePoolCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(clearIrcachePoolCS, kernal, "ircache_pool_buf", Ircache_pool_buf);
                    ctx.cmd.SetComputeBufferParam(clearIrcachePoolCS, kernal, "ircache_life_buf", Ircache_life_buf);

                    ctx.cmd.DispatchCompute(clearIrcachePoolCS, kernal, 1024, 1, 1);
                });

                initialize = true;
            }
            else
            {
                //Debug.Log("Scroll Cascade");
                ComputeShader scrollCascadeCS = Resources.Load<ComputeShader>("scrollCascadeCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("scrollCascadeCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = scrollCascadeCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_grid_meta_buf", Ircache_grid_meta_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_entry_cell_buf", Ircache_entry_cell_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_life_buf", Ircache_life_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_pool_buf", Ircache_pool_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(scrollCascadeCS, kernal, "ircache_grid_meta_buf2", Ircache_grid_meta_buf2);

                    ctx.cmd.DispatchCompute(scrollCascadeCS, kernal, 1, 32, 384);
                });

                SwapHandle(Ircache_grid_meta_buf, Ircache_grid_meta_buf2);

                frametime = (frametime + 1) % 2;
            }

            {
                ComputeShader _ircacheDispatchArgsCS = Resources.Load<ComputeShader>("_ircacheDispatchArgsCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("_ircacheDispatchArgsCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = _ircacheDispatchArgsCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(_ircacheDispatchArgsCS, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(_ircacheDispatchArgsCS, kernal, "dispatch_args", IrcacheDispatchArgs);

                    ctx.cmd.DispatchCompute(_ircacheDispatchArgsCS, kernal, 1, 1, 1);
                });
            }

            {
                ComputeShader ageIrcacheEntriesCS = Resources.Load<ComputeShader>("ageIrcacheEntriesCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("ageIrcacheEntriesCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = ageIrcacheEntriesCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_entry_cell_buf", Ircache_entry_cell_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_life_buf", Ircache_life_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_pool_buf", Ircache_pool_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_spatial_buf", Ircache_spatial_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_reposition_proposal_buf", Ircache_reposition_proposal_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_reposition_proposal_count_buf", Ircache_reposition_proposal_count_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "entry_occupancy_buf", Entry_occupancy_buf);
                    ctx.cmd.SetComputeBufferParam(ageIrcacheEntriesCS, kernal, "ircache_grid_meta_buf", Ircache_grid_meta_buf);

                    ctx.cmd.DispatchCompute(ageIrcacheEntriesCS, kernal, 1024, 1, 1);
                });
            }

            {
                ComputeShader _prefixScan1CS = Resources.Load<ComputeShader>("_prefixScan1CS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("_prefixScan1CS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = _prefixScan1CS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(_prefixScan1CS, kernal, "inout_buf", Entry_occupancy_buf);

                    ctx.cmd.DispatchCompute(_prefixScan1CS, kernal, 1024, 1, 1);
                });
            }

            {
                ComputeShader _prefixScan2CS = Resources.Load<ComputeShader>("_prefixScan2CS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("_prefixScan2CS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = _prefixScan2CS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(_prefixScan2CS, kernal, "input_buf", Entry_occupancy_buf);
                    ctx.cmd.SetComputeBufferParam(_prefixScan2CS, kernal, "output_buf", Segment_sum_buf);

                    ctx.cmd.DispatchCompute(_prefixScan2CS, kernal, 1, 1, 1);
                });
            }

            {
                ComputeShader _prefixScanMergeCS = Resources.Load<ComputeShader>("_prefixScanMergeCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("_prefixScanMergeCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = _prefixScanMergeCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(_prefixScanMergeCS, kernal, "segment_sum_buf", Segment_sum_buf);
                    ctx.cmd.SetComputeBufferParam(_prefixScanMergeCS, kernal, "inout_buf", Entry_occupancy_buf);

                    ctx.cmd.DispatchCompute(_prefixScanMergeCS, kernal, 1024, 1, 1);
                });
            }

            {
                ComputeShader ircacheCompactCS = Resources.Load<ComputeShader>("ircacheCompactCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("ircacheCompactCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = ircacheCompactCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(ircacheCompactCS, kernal, "entry_occupancy_buf", Entry_occupancy_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheCompactCS, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheCompactCS, kernal, "ircache_life_buf", Ircache_life_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheCompactCS, kernal, "ircache_entry_indirection_buf", Ircache_entry_indirection_buf);

                    ctx.cmd.DispatchCompute(ircacheCompactCS, kernal, 1024, 1, 1);
                });
            }

            {
                ComputeShader _ircacheDispatchArgsCS2 = Resources.Load<ComputeShader>("_ircacheDispatchArgsCS2");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("_ircacheDispatchArgsCS2", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = _ircacheDispatchArgsCS2.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(_ircacheDispatchArgsCS2, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(_ircacheDispatchArgsCS2, kernal, "dispatch_args", IrcacheDispatchArgs2);

                    ctx.cmd.DispatchCompute(_ircacheDispatchArgsCS2, kernal, 1, 1, 1);
                });
            }

            {
                ComputeShader ircacheResetCS = Resources.Load<ComputeShader>("ircacheResetCS");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("ircacheResetCS", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    int kernal = ircacheResetCS.FindKernel("CSMain");

                    ctx.cmd.SetComputeBufferParam(ircacheResetCS, kernal, "ircache_life_buf", Ircache_life_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheResetCS, kernal, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheResetCS, kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheResetCS, kernal, "ircache_entry_indirection_buf", Ircache_entry_indirection_buf);
                    ctx.cmd.SetComputeBufferParam(ircacheResetCS, kernal, "ircache_aux_buf", Ircache_aux_buf);

                    ctx.cmd.DispatchCompute(ircacheResetCS, kernal, 1024, 1, 1);
                });
            }

            {
                RayTracingShader TraceAccessibilityRgen = Resources.Load<RayTracingShader>("TraceAccessibilityRgen");

                RenderGraphBuilder builder = renderGraph.AddRenderPass<PathTracingRenderPassData>("TraceAccessibilityRgen", out var passData);

                builder.SetRenderFunc((PathTracingRenderPassData data, RenderGraphContext ctx) =>
                {
                    ctx.cmd.BuildRayTracingAccelerationStructure(rtas);

                    ctx.cmd.SetRayTracingAccelerationStructure(TraceAccessibilityRgen, Shader.PropertyToID("acceleration_structure"), rtas);
                    ctx.cmd.SetRayTracingShaderPass(TraceAccessibilityRgen, "TraceAccessibilityRgen");
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_spatial_buf", Ircache_spatial_buf);
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_life_buf", Ircache_life_buf);
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_meta_buf", Ircache_meta_buf);
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_entry_indirection_buf", Ircache_entry_indirection_buf);
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_reposition_proposal_buf", Ircache_reposition_proposal_buf);
                    ctx.cmd.SetRayTracingBufferParam(TraceAccessibilityRgen, "ircache_aux_buf", Ircache_aux_buf);

                    ctx.cmd.DispatchRays(TraceAccessibilityRgen, "TraceAccessibilityRgen", 65536 * 16, 1, 1);
                });
            }
        }

        frameIndex++;
    }

    private void UpdateFrameBuffer(Camera camera, ScriptableRenderContext context)
    {
        float3 eye_position = new(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z);
        float4 ircache_grid_center = new(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, 1.0f);
        for (int cascade = 0; cascade < 12; ++cascade)
        {
            float cell_diameter = 0.02f * (1 << cascade);

            int3 cascade_center = new((int)(eye_position.x / cell_diameter), (int)(eye_position.y / cell_diameter), (int)(eye_position.z / cell_diameter));
            int3 cascade_origin = new(cascade_center.x - 16, cascade_center.y - 16, cascade_center.z - 16);
            prev_scroll[cascade] = cur_scroll[cascade];
            cur_scroll[cascade] = cascade_origin;
            int3 scroll_amount = new(cur_scroll[cascade].x - prev_scroll[cascade].x, cur_scroll[cascade].y - prev_scroll[cascade].y, cur_scroll[cascade].z - prev_scroll[cascade].z);
            ircache_cascade_origin[cascade] = new(cur_scroll[cascade].x, cur_scroll[cascade].y, cur_scroll[cascade].z, 0);
            ircache_cascade_voxels_scrolled_this_frame[cascade] = new(scroll_amount.x, scroll_amount.y, scroll_amount.z, 0);
        }

        float LightPower = 1.0f;
        float pre_exposure = 1.0f;
        float sun_angular_radius_cos = 0.99999f;
        Light sun = RenderSettings.sun;
        float4 sun_direction = new(sun.transform.position, 0.0f);
        float4 sky_ambient = new(0.0f, 0.0f, 0.0f, 0.0f);
        float4 sun_color_multiplier = new(1.5f * LightPower, 1.5f * LightPower, 1.5f * LightPower, 1.5f * LightPower);


        buffer.BeginSample("Ircache");
        buffer.SetGlobalInt("frame_index", frameIndex);
        buffer.SetGlobalVector("ircache_grid_center", ircache_grid_center);
        buffer.SetGlobalVector("sun_direction", sun_direction);
        buffer.SetGlobalFloat("pre_exposure", pre_exposure);
        buffer.SetGlobalFloat("sun_angular_radius_cos", sun_angular_radius_cos);
        buffer.SetGlobalVector("sky_ambient", sky_ambient);
        buffer.SetGlobalVector("sun_color_multiplier", sun_color_multiplier);
        buffer.SetGlobalVectorArray("ircache_cascade_origin", ircache_cascade_origin);
        buffer.SetGlobalVectorArray("ircache_cascade_voxels_scrolled_this_frame", ircache_cascade_voxels_scrolled_this_frame);
        buffer.EndSample("Ircache");
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void SwapHandle(ComputeBuffer A, ComputeBuffer B)
    {
        (_, _)=(A, B);
    }
}
