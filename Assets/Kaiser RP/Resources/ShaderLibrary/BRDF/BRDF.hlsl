#ifndef KAISER_RAYTRACING_BRDF
#define KAISER_RAYTRACING_BRDF

// #include "../Utils/RayTracingHelper.hlsl"
#include "../Utils/Sampling.hlsl"
#include "BRDFLibrary.hlsl"


struct SurfaceData
{
    float3 albedo;
    float3 normal;
    float3 emission;
    float roughness;
    float metallic;
};

struct BRDFData
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float metallic;

    static BRDFData Create(SurfaceData surfaceData)
    {
        BRDFData res;
        res.diffuse = lerp(surfaceData.albedo, 0.0.xxx, surfaceData.metallic);
        res.specular = lerp(0.04.xxx, surfaceData.albedo, surfaceData.metallic);
        res.roughness = surfaceData.roughness;
        res.metallic = surfaceData.metallic;

        return res;
    }
};

struct BRDFSample
{
    float3 wi;
    float3 weight;
    float pdf;

    static BRDFSample Invalid()
    {
        BRDFSample res;
        res.pdf = 0.0;
        res.wi = float3(0.0, 0.0, -1.0);
        res.weight = float3(0.0, 0.0, 0.0);
        return res;
    }

    bool IsValid()
    {
        return wi.z > 1e-3;
    }
};

struct FrostbiteDiffuseBRDF
{
    float3 albedo;          ///< Diffuse albedo.
    float roughness;        ///< Roughness before remapping.

    float3 eval(const float3 wo, const float3 wi)
    {
        if (min(wo.z, wi.z) < BRDF_SAMPLING_MIN_COS) return 0.0f.xxx;

        return evalWeight(wo, wi) * K_INV_PI * wi.z;
    }

    bool sample(float2 rand2, const float3 wo, out BRDFSample brdfSample)
    {
        float pdf;
        float3 wi = SampleCosineHemisphereConcentric(rand2, pdf);

        if (min(wo.z, wi.z) < BRDF_SAMPLING_MIN_COS)
        {
            brdfSample.weight = 0.0f.xxx;
            return false;
        }

        float3 weight = evalWeight(wo, wi);

        brdfSample.wi = wi;
        brdfSample.weight = weight;
        brdfSample.pdf = pdf;
        return true;
    }

    float evalPdf(const float3 wo, const float3 wi)
    {
        if (min(wo.z, wi.z) < BRDF_SAMPLING_MIN_COS) return 0.f;

        return K_INV_PI * wi.z;
    }

    // Returns f(wo, wi) * pi.
    float3 evalWeight(float3 wo, float3 wi)
    {
        // float3 h = normalize(wo + wi);
        // float wiDotH = dot(wi, h);
        // float energyBias = lerp(0.f, 0.5f, roughness);
        // float energyFactor = lerp(1.f, 1.f / 1.51f, roughness);
        // float fd90 = energyBias + 2.f * wiDotH * wiDotH * roughness;
        // float fd0 = 1.f;
        // float woScatter = EvalFresnelSchlick(fd0, fd90, wo.z);
        // float wiScatter = EvalFresnelSchlick(fd0, fd90, wi.z);
        // return albedo * woScatter * wiScatter * energyFactor;
        return albedo;
    }
};

/** Specular reflection using microfacets.
*/
struct SpecularBRDF
{
    float3 albedo;      ///< Specular albedo.
    float alpha;        ///< GGX width parameter.

    float3 eval(const float3 wo, const float3 wi)
    {
        if (min(wo.z, wi.z) < BRDF_SAMPLING_MIN_COS) return 0.0f.xxx;

        #if ENABLE_DELTA_BSDF
            // Handle delta reflection.
            if (alpha == 0.f) return 0.0f.xxx;
        #endif

        // wi.z : ndotl
        // wo.z : ndotv
        // h.z : ndoth

        float3 h = normalize(wi + wo);
        float woDotH = dot(wo, h);

        float D = EvalNdfGGX(alpha, h.z);
        float G = EvalMaskingSmithGGXCorrelated(alpha, wo.z, wi.z);
        float3 F = EvalFresnelSchlick(albedo, 1.0f.xxx, woDotH);

        return F * D * G * 0.25f / wo.z;
    }

    bool sample(float2 rand2, const float3 wo, out BRDFSample brdfSample)
    {

        if (wo.z < BRDF_SAMPLING_MIN_COS) return false;
        
        #if ENABLE_DELTA_BSDF
            // Handle delta reflection.
            if (alpha == 0.f)
            {
                brdfSample.wi = float3(-wo.x, -wo.y, wo.z);
                brdfSample.pdf = 0.f;
                brdfSample.weight = EvalFresnelSchlick(albedo, 1.f, wo.z);
                return true;
            }
        #endif

        // brdfSample.wi = float3(rand2.x, rand2.y, 1.0);

        float3 weight = 0.0f.xxx;
        float pdf = 0.0f;
        float3 wi = 0.0f.xxx;
        float3 h = SampleGGX_VNDF(alpha, wo, rand2, pdf);    // pdf = G1(wi) * D(h) * max(0,dot(wi,h)) / wi.z
        
        // Reflect the incident direction to find the outgoing direction.
        float woDotH = dot(wo, h);
        wi = 2.f * woDotH * h - wo;
        if (wi.z < BRDF_SAMPLING_MIN_COS) return false;

        float G = EvalMaskingSmithGGXCorrelated(alpha, wo.z, wi.z);
        float GOverG1wo = G * (1.f + EvalLambdaGGX(alpha * alpha, wo.z));
        
        float3 F = EvalFresnelSchlick(albedo, 1.f, woDotH);

        pdf /= (4.f * woDotH); // Jacobian of the reflection operator.
        weight = F * GOverG1wo;

        brdfSample.wi = wi;
        brdfSample.weight = weight;
        brdfSample.pdf = pdf;
        // return true;
        return true;
    }

    float evalPdf(const float3 wo, const float3 wi)
    {
        if (min(wo.z, wi.z) < BRDF_SAMPLING_MIN_COS) return 0.f;

        #if ENABLE_DELTA_BSDF
            // Handle delta reflection.
            if (alpha == 0.f) return 0.f;
        #endif

        float3 h = normalize(wo + wi);
        float woDotH = dot(wo, h);

        float pdf = EvalPdfGGX_VNDF(alpha, wo, h);

        return pdf / (4.f * woDotH);
    }
};

struct LayeredBRDF
{
    FrostbiteDiffuseBRDF diffuseBRDF;
    SpecularBRDF specularBRDF;

    float pDiffuse;
    float pSpecular;

    static LayeredBRDF Create(SurfaceData surfaceData, float ndotv)
    {
        BRDFData brdfData = BRDFData::Create(surfaceData);
        FrostbiteDiffuseBRDF diffuseBRDF;
        SpecularBRDF specularBRDF;
        
        diffuseBRDF.albedo = brdfData.diffuse;
        diffuseBRDF.roughness = brdfData.roughness;

        specularBRDF.albedo = brdfData.specular;
        specularBRDF.alpha = brdfData.roughness * brdfData.roughness;

        float pDiffuse = (1.0f - brdfData.metallic) * luminance(brdfData.diffuse);
        float pSpecular = luminance(EvalFresnelSchlick(brdfData.specular, 1.0f.xxx, ndotv));

        float normFactor = 1.0f / (pDiffuse + pSpecular);

        pDiffuse *= normFactor;
        pSpecular *= normFactor;

        LayeredBRDF res;
        res.diffuseBRDF = diffuseBRDF;
        res.specularBRDF = specularBRDF;
        res.pDiffuse = pDiffuse;
        res.pSpecular = pSpecular;

        return res;
    }

    void ForceDiffuse()
    {
        pDiffuse = 1.0f;
        pSpecular = 0.0f;
    }

    void ForceSpecular()
    {
        pDiffuse = 0.0f;
        pSpecular = 1.0f;
    }

    float3 eval(const float3 wo, const float3 wi)
    {
        float3 result = 0.0f.xxx;
        if (pDiffuse > 0.f) result += diffuseBRDF.eval(wo, wi);
        if (pSpecular > 0.f) result += specularBRDF.eval(wo, wi);
        return result;
    }

    float3 evalDirectionalLight(const float3 wo, const float3 wi)
    {
        return eval(wo, wi);
    }

    BRDFSample sample(float3 rand3, const float3 wo)
    {
        // Default initialization to avoid divergence at returns.
        bool valid = false;
        BRDFSample brdfSample = BRDFSample::Invalid();
        // float uSelect = rand3.z;

        // Note: The commented-out pdf contributions below are always zero, so no need to compute them.
        // else if (uSelect < pDiffuseReflection + pSpecularReflection)
        if (rand3.z < pDiffuse)
        {
            valid = diffuseBRDF.sample(rand3.xy, wo, brdfSample);
            brdfSample.weight /= pDiffuse;
            brdfSample.pdf *= pDiffuse;
            if (pSpecular > 0.f) brdfSample.pdf += pSpecular * specularBRDF.evalPdf(wo, brdfSample.wi);
        }
        else
        {
            valid = specularBRDF.sample(rand3.xy, wo, brdfSample);
            brdfSample.weight /= pSpecular;
            brdfSample.pdf *= pSpecular;
            if (pDiffuse > 0.f) brdfSample.pdf += pDiffuse * diffuseBRDF.evalPdf(wo, brdfSample.wi);
        }

        return brdfSample;
    }
};

#endif // KAISER_RAYTRACING_BRDF