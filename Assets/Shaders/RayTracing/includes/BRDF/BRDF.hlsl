#ifndef KAISER_RAYTRACING_BRDF
#define KAISER_RAYTRACING_BRDF

#include "../Utils.hlsl"

#define BRDF_SAMPLING_MIN_COS 1e-4
struct SurfaceData
{
    float3 albedo;
    float3 normal;
    float3 emissive;
    float roughness;
    float metallic;
};

struct BRDFValue
{
    float3 valueOverPDF;
    float3 value;
    float pdf;

    float3 transmissionFraction;

    static BRDFValue Invalid()
    {
        BRDFValue res;
        res.valueOverPDF = 0.0;
        res.pdf = 0.0;
        res.transmissionFraction = 0.0;
        return res;
    }
};

struct BRDFSample:BRDFValue
{
    float3 wi;

    float approxRoughness;

    static BRDFSample Invalid()
    {
        BRDFSample res;
        res.valueOverPDF = 0.0;
        res.pdf = 0.0;
        res.wi = float3(0.0, 0.0, -1.0);
        res.transmissionFraction = 0.0;
        res.approxRoughness = 0;
        return res;
    }

    bool IsValid()
    {
        return wi.z > 1e-6;
    }
};

// ------------------------------------------------------------------
// Diffuse BRDFs
// ------------------------------------------------------------------

struct DiffuseBRDF
{
    float3 albedo;

    BRDFValue evaluate(float3 wo, float3 wi)
    {
        
        
        BRDFValue res;

        if (wo.z <= 0 || wi.z <= 0)
        {
            return res;
        }
        res.pdf = SELECT(wi.z > 0.0, K_INV_PI, 0.0);
        res.valueOverPDF = SELECT(wi.z > 0.0, albedo, 0.0.xxx);
        res.value = res.pdf * res.valueOverPDF;

        res.transmissionFraction = 0.0;

        return res;
    }

    BRDFSample sample(float3 wo, float2 urand)
    {
        float phi = urand.x * K_TWO_PI;
        float cosTheta = sqrt(max(0.0, 1.0 - urand.y));
        float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));

        float sinPhi = sin(phi);
        float cosPhi = cos(phi);

        BRDFSample res;
        res.wi = float3(cosPhi * sinTheta, sinPhi * sinTheta, cosTheta);
        res.pdf = K_INV_PI;
        res.valueOverPDF = albedo;
        res.value = res.valueOverPDF * res.pdf;
        res.transmissionFraction = 0.0;
        res.approxRoughness = 1.0;

        return res;
    }
};

// ------------------------------------------------------------------
// Specular BRDFs
// ------------------------------------------------------------------

float3 EvalFresnelSchlick(float3 f0, float3 f90, float cosTheta)
{
    float x = max(0.0, 1.0 - cosTheta);
    float x2 = x * x;
    float x5 = x2 * x2 * x;
    return lerp(f0, f90, x5);
}

struct SmithShadowingMasking
{
    float g;
    float g_over_g1_wo;

    static float gSmithGGX_Correlated(float ndotv, float ndotl, float a2)
    {
        float lambda_v = ndotl * sqrt((-ndotv * a2 + ndotv) * ndotv + a2);
        float lambda_l = ndotv * sqrt((-ndotl * a2 + ndotl) * ndotl + a2);

        return 2.0 * ndotl * ndotv / (lambda_v + lambda_l);
    }

    static float gSmithGGX1(float ndotv, float a2)
    {
        float tan2_v = (1.0 - ndotv * ndotv) / (ndotv * ndotv);
        return 2.0 / (1.0 + sqrt(1.0 + a2 * tan2_v));
    }

    static SmithShadowingMasking eval(float ndotv, float ndotl, float a2)
    {
        SmithShadowingMasking res;
        res.g = gSmithGGX_Correlated(ndotv, ndotl, a2);
        res.g_over_g1_wo = res.g / gSmithGGX1(ndotv, a2);
        return res;
    }
};

struct NDFSample
{
    float3 m;
    float pdf;
};


struct SpecularBRDF
{
    float3 albedo;
    float roughness;
    float3 _fresnel;

    static float GGX_NDF(float a2, float cosTheta)
    {
        float denom_sqrt = cosTheta * cosTheta * (a2 - 1.0) + 1.0;
        return a2 / (K_PI * denom_sqrt * denom_sqrt);
    }

    static float GetPDF_GGX(float a2, float cosTheta)
    {
        return GGX_NDF(a2, cosTheta) * cosTheta;
    }

    static float GetPDF_GGX_VNDF(float a2, float3 wo, float3 h)
    {
        float g1 = SmithShadowingMasking::gSmithGGX1(wo.z, a2);
        float d = GGX_NDF(a2, h.z);
        return g1 * d * max(0.f, dot(wo, h)) / wo.z;
    }

    NDFSample sampleNDF(float2 urand)
    {
        const float a2 = roughness * roughness;

        const float cos2Theta = (1 - urand.x) / (1 - urand.x + a2 * urand.x);
        const float cosTheta = sqrt(cos2Theta);
        const float phi = K_TWO_PI * urand.y;

        const float sinTheta = sqrt(max(0.0, 1.0 - cos2Theta));

        NDFSample res;
        res.m = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
        res.pdf = GetPDF_GGX(a2, cosTheta);

        return res;
    }

    NDFSample SampleVNDF(float alpha, float3 wo, float2 urand)
    {
        float alphaX = alpha, alphaY = alpha;
        float a2 = alphaX * alphaY;

        // Transform the view vector to the hemisphere configuration.
        float3 Vh = normalize(float3(alphaX * wo.x, alphaY * wo.y, wo.z));

        // Construct orthonormal basis (Vh,T1,T2).
        float3 T1 = SELECT((Vh.z < 0.9999f), normalize(cross(float3(0, 0, 1), Vh)), float3(1, 0, 0)); // TODO: fp32 precision
        float3 T2 = cross(Vh, T1);

        // Parameterization of the projected area of the hemisphere.
        float r = sqrt(urand.x);
        float phi = (2.f * K_PI) * urand.y;
        float t1 = r * cos(phi);
        float t2 = r * sin(phi);
        float s = 0.5f * (1.f + Vh.z);
        t2 = (1.f - s) * sqrt(1.f - t1 * t1) + s * t2;

        // Reproject onto hemisphere.
        float3 Nh = t1 * T1 + t2 * T2 + sqrt(max(0.f, 1.f - t1 * t1 - t2 * t2)) * Vh;

        // Transform the normal back to the ellipsoid configuration. This is our half vector.
        float3 h = normalize(float3(alphaX * Nh.x, alphaY * Nh.y, max(0.f, Nh.z)));
        float pdf = GetPDF_GGX_VNDF(a2, wo, h);

        NDFSample res;
        res.m = h;
        res.pdf = pdf;
        return res;
    }

    BRDFValue evaluate(float3 wo, float3 wi)
    {
        BRDFValue res = BRDFValue::Invalid();
        if (wi.z <= 0.0 || wo.z <= 0.0)
        {
            // res = BRDFValue::Invalid();
            return res;
        }

        const float a2 = roughness * roughness;
        const float3 m = normalize(wo + wi);
        const float cosTheta = m.z;

        const float pdf_h = GetPDF_GGX(a2, cosTheta);
        // const float pdf_h = GetPDF_GGX_VNDF(a2, wo, m);

        const float mdotwi = dot(m, wo);
        const float jacobian = 1.0 / (4.0 * mdotwi);

        const float3 fresnel = EvalFresnelSchlick(albedo, 1.0, mdotwi);

        SmithShadowingMasking shadowingMasking = SmithShadowingMasking::eval(wo.z, wi.z, a2);

        res.pdf = pdf_h * jacobian / wi.z;
        res.transmissionFraction = 1.0.xxx - fresnel;

        res.valueOverPDF = fresnel * shadowingMasking.g_over_g1_wo;
        // res.value = fresnel * shadowingMasking.g * GGX_NDF(a2, cosTheta) / (4.0 * wo.z * wi.z);
        res.value = fresnel * shadowingMasking.g * GGX_NDF(a2, cosTheta) * 0.25f / (wo.z * wi.z);
        // res.value = fresnel * shadowingMasking.g / (4.0 * wo.z * wi.z);
        // res.value = cosTheta;

        return res;
    }

    BRDFSample sample(float3 wo, float2 urand)
    {
        NDFSample ndfSample = SampleVNDF(roughness, wo, urand);

        const float3 wi = reflect(-wo, ndfSample.m);

        if (ndfSample.m.z <= BRDF_SAMPLING_MIN_COS || wi.z <= BRDF_SAMPLING_MIN_COS || wo.z <= BRDF_SAMPLING_MIN_COS)
        {
            return BRDFSample::Invalid();
        }

        // Change of variables from the half-direction space to regular lighting geometry.
        const float mdotwi = dot(ndfSample.m, wi);
        const float jacobian = 1.0 / (4.0 * mdotwi);
        const float3 fresnel = EvalFresnelSchlick(albedo, 1.0, mdotwi);
        const float a2 = roughness * roughness;
        const float cosTheta = ndfSample.m.z;
        
        SmithShadowingMasking shadowingMasking = SmithShadowingMasking::eval(wo.z, wi.z, a2);
        BRDFSample res;
        res.pdf = ndfSample.pdf * jacobian / wi.z;
        res.wi = wi;
        res.transmissionFraction = 1.0.xxx - fresnel;
        res.approxRoughness = roughness;
        res.valueOverPDF = fresnel * shadowingMasking.g_over_g1_wo;
        res.value = fresnel * shadowingMasking.g * GGX_NDF(a2, cosTheta) / (4 * wo.z * wi.z);

        return res;
    }
};

// ------------------------------------------------------------------
// Specular BRDFs with lut
// ------------------------------------------------------------------

struct SpecularBRDFEnergyPreservation
{
    static float3 sampleFG_lut(float ndotv, float roughness)
    {
        float2 uv = float2(ndotv, roughness) * 399 * 0.0025 + 0.5;
        return _BRDF_LUT_Texture.SampleLevel(sampler_bilinear_clamp, uv, 0).xyz;
    }
};


// ------------------------------------------------------------------
// Layered BRDFs
// ------------------------------------------------------------------

float3 MetallicAlbedoBoost(float metallic, float3 diffuseAlbedo)
{
    static const float a0 = 1.749;
    static const float a1 = -1.61;
    static const float e1 = 0.5555;
    static const float e3 = 0.8244;

    const float x = metallic;
    const float3 y = diffuseAlbedo;
    const float3 y3 = y * y * y;

    return 1.0 + (0.25 - (x - 0.5) * (x - 0.5)) * (a0 + a1 * abs(x - 0.5)) * (e1 * y + e3 * y3);
}

void ApplyMetallicToBRDFs(inout DiffuseBRDF diffuseBRDF, inout SpecularBRDF specularBRDF, float metallic)
{
    const float3 albedo = diffuseBRDF.albedo;
    specularBRDF.albedo = lerp(specularBRDF.albedo, albedo, metallic);
    diffuseBRDF.albedo = max(0.0, 1.0 - metallic) * albedo;

    const float albedoBoost = MetallicAlbedoBoost(metallic, albedo);
    specularBRDF.albedo = min(1.0, specularBRDF.albedo * albedoBoost);
    diffuseBRDF.albedo = min(1.0, diffuseBRDF.albedo * albedoBoost);
}


struct LayeredBRDF
{
    DiffuseBRDF diffuseBRDF;
    SpecularBRDF specularBRDF;

    // Temp
    float specularChance;

    static LayeredBRDF Create(SurfaceData surfaceData, float ndotv)
    {
        LayeredBRDF brdf;
        
        DiffuseBRDF diffuseBRDF;
        diffuseBRDF.albedo = surfaceData.albedo;

        SpecularBRDF specularBRDF;
        specularBRDF.albedo = 0.04;
        specularBRDF.roughness = surfaceData.roughness;

        ApplyMetallicToBRDFs(diffuseBRDF, specularBRDF, surfaceData.metallic);

        specularBRDF._fresnel = EvalFresnelSchlick(specularBRDF.albedo, 1.0, ndotv);
        brdf.specularChance = lerp(surfaceData.metallic, 1.0, specularBRDF._fresnel * (1.0 - surfaceData.roughness));

        // TODO: Specular BRDF energy preservation

        brdf.diffuseBRDF = diffuseBRDF;
        brdf.specularBRDF = specularBRDF;
        return brdf;
    }

    BRDFSample sample(float3 wo, float3 urand)
    {
        BRDFSample res;
        
        // res = lerp(diffuseBRDF.sample(wo, urand.xy), specularBRDF.sample(wo, urand.xy), specularChance);
        if (urand.z < specularChance)
        {
            res = specularBRDF.sample(wo, urand.xy);
        }
        else
        {
            res = diffuseBRDF.sample(wo, urand.xy);
        }
        return res;
    }

    float3 evaluateDirectionalLight(float3 wo, float3 wi)
    {
        if (wo.z <= 0 || wi.z <= 0)
        {
            return 0.0.xxx;
        }
        
        const BRDFValue diffValue = diffuseBRDF.evaluate(wo, wi);
        const BRDFValue specValue = specularBRDF.evaluate(wo, wi);

        // const float fresnel = EvalFresnelSchlick(specularBRDF.albedo, 1.0, dot(wo, wi));

        // const float specularChance = specularBRDF.roughness * 0.5;
        
        // float specularChance = lerp(_Metallic, 1, fresnel * _Smoothness);
        // return diffValue.value * specValue.transmissionFraction + specValue.value;
        // return diffValue.value;
        // return diffValue.value * specValue.transmissionFraction + specValue.value * (1 - specValue.transmissionFraction);
        return diffValue.value * (1 - specularChance) + specValue.value * specularChance;
    };
};
#endif // KAISER_RAYTRACING_BRDF