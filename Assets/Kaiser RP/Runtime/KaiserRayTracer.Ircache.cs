using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using Unity.Mathematics;

public partial class KaiserRayTracer : RenderPipeline
{
    private int frametime = 0;
    private bool initialize = false;
    private CommandBuffer buffer = new CommandBuffer { name = "Ircachhe" };
    private int3[] prev_scroll = new int3[12];
    private int3[] cur_scroll = new int3[12];
    private Vector4[] ircache_cascade_origin = new Vector4[12];
    private Vector4[] ircache_cascade_voxels_scrolled_this_frame = new Vector4[12];
    

    private ComputeShader computeShader;

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
        if (frametime == 1)
        {
            SwapHandle(Ircache_grid_meta_buf, Ircache_grid_meta_buf2);
        }

        if (!initialize)
        {
            //Debug.Log("Initialize");
            computeShader = Resources.Load<ComputeShader>("clearIrcachePoolCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_pool_buf", Ircache_pool_buf);
            computeShader.SetBuffer(kernal, "ircache_life_buf", Ircache_life_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);
            initialize = true;
        }
        else
        {
            //Debug.Log("Scroll Cascade");
            computeShader = Resources.Load<ComputeShader>("scrollCascadeCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_grid_meta_buf", Ircache_grid_meta_buf);
            computeShader.SetBuffer(kernal, "ircache_entry_cell_buf", Ircache_entry_cell_buf);
            computeShader.SetBuffer(kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
            computeShader.SetBuffer(kernal, "ircache_life_buf", Ircache_life_buf);
            computeShader.SetBuffer(kernal, "ircache_pool_buf", Ircache_pool_buf);
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "ircache_grid_meta_buf2", Ircache_grid_meta_buf2);
            computeShader.Dispatch(kernal, 1, 32, 384);
            SwapHandle(Ircache_grid_meta_buf, Ircache_grid_meta_buf2);

            frametime = (frametime + 1) % 2;
        }

        {
            computeShader = Resources.Load<ComputeShader>("_ircacheDispatchArgsCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "dispatch_args", IrcacheDispatchArgs);
            computeShader.Dispatch(kernal, 1, 1, 1);
        }

        {
            computeShader = Resources.Load<ComputeShader>("ageIrcacheEntriesCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "ircache_entry_cell_buf", Ircache_entry_cell_buf);
            computeShader.SetBuffer(kernal, "ircache_life_buf", Ircache_life_buf);
            computeShader.SetBuffer(kernal, "ircache_pool_buf", Ircache_pool_buf);
            computeShader.SetBuffer(kernal, "ircache_spatial_buf", Ircache_spatial_buf);
            computeShader.SetBuffer(kernal, "ircache_reposition_proposal_buf", Ircache_reposition_proposal_buf);
            computeShader.SetBuffer(kernal, "ircache_reposition_proposal_count_buf", Ircache_reposition_proposal_count_buf);
            computeShader.SetBuffer(kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
            computeShader.SetBuffer(kernal, "entry_occupancy_buf", Entry_occupancy_buf);
            computeShader.SetBuffer(kernal, "ircache_grid_meta_buf", Ircache_grid_meta_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);
        }

        {
            computeShader = Resources.Load<ComputeShader>("_prefixScan1CS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "inout_buf", Entry_occupancy_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);

            computeShader = Resources.Load<ComputeShader>("_prefixScan2CS");
            kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "input_buf", Entry_occupancy_buf);
            computeShader.SetBuffer(kernal, "output_buf", Segment_sum_buf);
            computeShader.Dispatch(kernal, 1, 1, 1);

            computeShader = Resources.Load<ComputeShader>("_prefixScanMergeCS");
            kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "segment_sum_buf", Segment_sum_buf);
            computeShader.SetBuffer(kernal, "inout_buf", Entry_occupancy_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);
        }

        {
            computeShader = Resources.Load<ComputeShader>("ircacheCompactCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "entry_occupancy_buf", Entry_occupancy_buf);
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "ircache_life_buf", Ircache_life_buf);
            computeShader.SetBuffer(kernal, "ircache_entry_indirection_buf", Ircache_entry_indirection_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);
        }

        {
            computeShader = Resources.Load<ComputeShader>("_ircacheDispatchArgsCS2");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "dispatch_args", IrcacheDispatchArgs2);
            computeShader.Dispatch(kernal, 1, 1, 1);
        }

        {
            computeShader = Resources.Load<ComputeShader>("ircacheResetCS");
            int kernal = computeShader.FindKernel("CSMain");
            computeShader.SetBuffer(kernal, "ircache_life_buf", Ircache_life_buf);
            computeShader.SetBuffer(kernal, "ircache_meta_buf", Ircache_meta_buf);
            computeShader.SetBuffer(kernal, "ircache_irradiance_buf", Ircache_irradiance_buf);
            computeShader.SetBuffer(kernal, "ircache_entry_indirection_buf", Ircache_entry_indirection_buf);
            computeShader.SetBuffer(kernal, "ircache_aux_buf", Ircache_aux_buf);
            computeShader.Dispatch(kernal, 1024, 1, 1);
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
        
        buffer.BeginSample("Ircache");
        buffer.SetGlobalInt("frame_index", frameIndex);
        buffer.SetGlobalVector("ircache_grid_center", ircache_grid_center);
        buffer.SetGlobalVectorArray("ircache_cascade_origin", ircache_cascade_origin);
        buffer.SetGlobalVectorArray("ircache_cascade_voxels_scrolled_this_frame", ircache_cascade_voxels_scrolled_this_frame);
        buffer.EndSample("Ircache");
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private void SwapHandle(ComputeBuffer A, ComputeBuffer B)
    {
        ComputeBuffer tmp = A;
        A = B;
        B = tmp;
    }
}
