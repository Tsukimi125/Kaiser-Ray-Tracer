#ifndef KAISER_IBL
#define KAISER_IBL

float3 IBL_FresnelSchlickRoughness(float NdotV, float3 f0, float roughness)
{
    float r1 = 1.0f - roughness;
    return f0 + (max(float3(r1, r1, r1), f0) - f0) * pow(1 - NdotV, 5.0f);
}

// 间接光照
float3 IBL(
    float3 N, float3 V,
    float3 albedo, float roughness, float metallic,
    samplerCUBE _diffuseIBL, samplerCUBE _specularIBL, sampler2D _brdfLut)
{
    roughness = min(roughness, 0.99);

    float3 H = normalize(N);    // 用法向作为半角向量
    float NdotV = max(dot(N, V), 0);
    float HdotV = max(dot(H, V), 0);
    float3 R = normalize(reflect(-V, N));   // 反射向量

    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    // float3 F = SchlickFresnel(HdotV, F0);
    float3 F = IBL_FresnelSchlickRoughness(HdotV, F0, roughness);
    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * (1.0 - metallic);

    // 漫反射
    float3 IBLd = texCUBE(_diffuseIBL, N).rgb;
    float3 diffuse = k_d * albedo * IBLd;

    // 镜面反射
    float rgh = roughness * (1.7 - 0.7 * roughness);
    float lod = 6.0 * rgh;  // Unity 默认 6 级 mipmap
    float3 IBLs = texCUBElod(_specularIBL, float4(R, lod)).rgb;
    float2 brdf = tex2D(_brdfLut, float2(NdotV, roughness)).rg;
    float3 specular = IBLs * (F0 * brdf.x + brdf.y);

    float3 ambient = diffuse + specular;

    return ambient;
}

#endif