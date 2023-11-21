#ifndef FRAME_CONSTANTS_HLSL
#define FRAME_CONSTANTS_HLSL

//#include "packed.hlsl"
//#include "uv.hlsl"
//#include "ray_cone.hlsl"

CBUFFER_START(_FrameConstant)
    uint frame_index;
    float pre_exposure;
    float4 sky_ambient;
    float4 sun_direction;
    float4 sun_color_multiplier;
    float sun_angular_radius_cos;
    float4 ircache_grid_center;
    int4 ircache_cascade_origin[12];
    int4 ircache_cascade_voxels_scrolled_this_frame[12];
CBUFFER_END

//struct ViewRayContext
//{
//    float4 ray_dir_cs; //离相机距离为无限的点的位置
//    float4 ray_dir_vs_h;
//    float4 ray_dir_ws_h;
//
//    float4 ray_origin_cs; //离相机距离为0的点的位置
//    float4 ray_origin_vs_h;
//    float4 ray_origin_ws_h;
//
//    float4 ray_hit_cs; //屏幕上的像素点的位置
//    float4 ray_hit_vs_h;
//    float4 ray_hit_ws_h;
//
//    float3 ray_dir_vs()
//    {
//        return normalize(ray_dir_vs_h.xyz);
//    }
//
//    float3 ray_dir_ws()
//    {
//        return normalize(ray_dir_ws_h.xyz);
//    }
//
//    // TODO: might need previous frame versions of those
//
//    float3 ray_origin_vs()
//    {
//        return ray_origin_vs_h.xyz / ray_origin_vs_h.w;
//    }
//
//    float3 ray_origin_ws()
//    {
//        return ray_origin_ws_h.xyz / ray_origin_ws_h.w;
//    }
//
//    float3 ray_hit_vs()
//    {
//        return ray_hit_vs_h.xyz / ray_hit_vs_h.w;
//    }
//
//    float3 ray_hit_ws()
//    {
//        return ray_hit_ws_h.xyz / ray_hit_ws_h.w;
//    }
//
//    // A biased position from which secondary rays can be shot without too much acne or leaking
//    float3 biased_secondary_ray_origin_ws()
//    {
//        return ray_hit_ws() - ray_dir_ws() * (length(ray_hit_vs()) + length(ray_hit_ws())) * 1e-4;
//    }
//
//    float3 biased_secondary_ray_origin_ws_with_normal(float3 normal) //往屏幕像素的法线和view方向的逆方向的半程向量偏移
//    {
//        
//        float3 ws_abs = abs(ray_hit_ws());
//        float max_comp = max(max(ws_abs.x, ws_abs.y), max(ws_abs.z, -ray_hit_vs().z));
//        return ray_hit_ws() + (normal - ray_dir_ws()) * max(1e-4, max_comp * 1e-6);
//    }
//
//    static ViewRayContext from_uv(float2 uv)
//    {
//        ViewConstants view_constants = frame_constants.view_constants;
//
//        ViewRayContext res;
//        res.ray_dir_cs = float4(uv_to_cs(uv), 0.0, 1.0);
//        res.ray_dir_vs_h = mul(view_constants.sample_to_view, res.ray_dir_cs);
//        res.ray_dir_ws_h = mul(view_constants.view_to_world, res.ray_dir_vs_h);
//
//        res.ray_origin_cs = float4(uv_to_cs(uv), 1.0, 1.0);
//        res.ray_origin_vs_h = mul(view_constants.sample_to_view, res.ray_origin_cs);
//        res.ray_origin_ws_h = mul(view_constants.view_to_world, res.ray_origin_vs_h);
//
//        return res;
//    }
//
//    static ViewRayContext from_uv_and_depth(float2 uv, float depth)
//    {
//        ViewConstants view_constants = frame_constants.view_constants;
//
//        ViewRayContext res;
//        res.ray_dir_cs = float4(uv_to_cs(uv), 0.0, 1.0);
//        res.ray_dir_vs_h = mul(view_constants.sample_to_view, res.ray_dir_cs);
//        res.ray_dir_ws_h = mul(view_constants.view_to_world, res.ray_dir_vs_h);
//
//        res.ray_origin_cs = float4(uv_to_cs(uv), 1.0, 1.0);
//        res.ray_origin_vs_h = mul(view_constants.sample_to_view, res.ray_origin_cs);
//        res.ray_origin_ws_h = mul(view_constants.view_to_world, res.ray_origin_vs_h);
//
//        res.ray_hit_cs = float4(uv_to_cs(uv), depth, 1.0);
//        res.ray_hit_vs_h = mul(view_constants.sample_to_view, res.ray_hit_cs);
//        res.ray_hit_ws_h = mul(view_constants.view_to_world, res.ray_hit_vs_h);
//
//        return res;
//    }
//
//    static ViewRayContext from_uv_and_biased_depth(float2 uv, float depth)
//    {
//        return from_uv_and_depth(uv, min(1.0, depth * asfloat(0x3f800040)));
//    }
//};
//
//float3 get_eye_position()
//{
//    float4 eye_pos_h = mul(frame_constants.view_constants.view_to_world, float4(0, 0, 0, 1));
//    return eye_pos_h.xyz / eye_pos_h.w;
//}
//
//float3 get_prev_eye_position()
//{
//    float4 eye_pos_h = mul(frame_constants.view_constants.prev_view_to_prev_world, float4(0, 0, 0, 1));
//    return eye_pos_h.xyz / eye_pos_h.w;
//}
//
//float depth_to_view_z(float depth)
//{
//    return rcp(depth * -frame_constants.view_constants.clip_to_view._43);
//}
//
//float3 direction_view_to_world(float3 v)
//{
//    return mul(frame_constants.view_constants.view_to_world, float4(v, 0)).xyz;
//}
//
//float3 direction_world_to_view(float3 v)
//{
//    return mul(frame_constants.view_constants.world_to_view, float4(v, 0)).xyz;
//}
//
//float3 position_world_to_view(float3 v)
//{
//    return mul(frame_constants.view_constants.world_to_view, float4(v, 1)).xyz;
//}
//
//float3 position_view_to_world(float3 v)
//{
//    return mul(frame_constants.view_constants.view_to_world, float4(v, 1)).xyz;
//}
//
//float3 position_world_to_clip(float3 v)
//{
//    float4 p = mul(frame_constants.view_constants.world_to_view, float4(v, 1));
//    p = mul(frame_constants.view_constants.view_to_clip, p);
//    return p.xyz / p.w;
//}
//
//float3 position_world_to_sample(float3 v)
//{
//    float4 p = mul(frame_constants.view_constants.world_to_view, float4(v, 1));
//    p = mul(frame_constants.view_constants.view_to_sample, p);
//    return p.xyz / p.w;
//}
//
//float pixel_cone_spread_angle_from_image_height(float image_height)
//{
//    return atan(2.0 * frame_constants.view_constants.clip_to_view._11 / image_height);
//}
//
//RayCone pixel_ray_cone_from_image_height(float image_height)
//{
//    RayCone res;
//    res.width = 0.0;
//    res.spread_angle = pixel_cone_spread_angle_from_image_height(image_height);
//    return res;
//}
//
//static const uint2 hi_px_subpixels[4] =
//{
//    uint2(1, 1),
//    uint2(1, 0),
//    uint2(0, 0),
//    uint2(0, 1),
//};

#define USE_HALFRES_SUBSAMPLE_JITTERING 1

#if USE_HALFRES_SUBSAMPLE_JITTERING
#define HALFRES_SUBSAMPLE_INDEX (frame_constants.frame_index & 3)
#else
#define HALFRES_SUBSAMPLE_INDEX 0
#endif

#define HALFRES_SUBSAMPLE_OFFSET (hi_px_subpixels[HALFRES_SUBSAMPLE_INDEX])

#endif