#ifndef KAISER_RAYTRACING_BRDF
#define KAISER_RAYTRACING_BRDF

#inclue "Utils.hlsl"

struct SurfaceData
{
    float3 albedo;
    float3 normal;
    float roughness;
    float metallic;
};

struct BRDFValue
{
    float3 valueOverPDF;
    float3 value;
    float pdf;

    float3 transmissionFraction;

    static BRDFValue invalid()
    {
        BRDFValue res;
        res.valueOverPDF = 0.0;
        res.pdf = 0.0;
        res.transmissionFraction = 0.0;
        return res;
    }
};

struct DiffuseBRDF
{
    float3 albedo;

    BRDFValue evaluate(float3 wo, float3 wi)
    {
        BRDFValue result;
        result.pdf = SELECT(wi.z > 0.0, 1.0 / PI, 0.0);
        result.valueOverPDF = SELECT(wi.z > 0.0, albedo / PI, 0.0.xxx);
        result.value = albedo;
        
        return result;
    }
};

// Specular BRDFs

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
}
struct SpecularBRDF
{
    float3 albedo;
    float roughness;

    static float GetPDF_GGX_VNDF(float a2, float3 wo, float3 h)
    {
        float g1 = SmithShadowingMasking::g_smith_ggx1(wo.z, a2);
        float d = ggx_ndf(a2, h.z);
        return g1 * d * max(0.f, dot(wo, h)) / wo.z;
    }

    static float GGX_NDF(float a2, float cosTheta)
    {
        float denom_sqrt = cosTheta * cosTheta * (a2 - 1.0) + 1.0;
        return a2 / (K_PI * denom_sqrt * denom_sqrt);
    }

    BRDFValue evaluate(float3 wo, float3 wi)
    {
        if (wi.z <= 0.0 || wo.z <= 0.0)
        {
            return BRDFValue :  : invalid();
        }

        const float a2 = roughness * roughness;
        const float3 m = normalize(wo + wi);
        const float cosTheta = m.z;

        const float pdf_h = GetPDF_GGX_VNDF(a2, wo, m);

        const float mdotwi = dot(m, wi);
        const float jacobian = 1.0 / (4.0 * mdotwi);

        const float3 fresnel = EvalFresnelSchlick(albedo, 1.0, mdotwi);

        SmithShadowingMasking shadowingMasking = SmithShadowingMasking :  : eval(wo.z, wi.z, a2);

        BRDFValue res;
        res.pdf = pdf_h * jacobian / wi.z;
        res.transmissionFraction = 1.0.xxx - fresnel;

        res.valueOverPDF = fresnel * shadowingMasking.g_over_g1_wo;
        res.value = fresnel * shadowingMasking.g * GGX_NDF(a2, cosTheta) / (4.0 * wo.z * wi.z);
        
        return res;
    };

    struct SpecularBRDFEnergyPreservation { }

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

        static LayeredBRDF Create(SurfaceData surfaceData, float ndotv)
        {
            DiffuseBRDF diffuseBRDF;
            diffuseBRDF.albedo = albedo;

            SpecularBRDF specularBRDF;
            specularBRDF.albedo = 0.04;
            specularBRDF.roughness = roughness;

            ApplyMetallicToBRDFs(diffuseBRDF, specularBRDF, metallic);
            
            // TODO: Specular BRDF energy preservation

            LayeredBRDF brdf;
            brdf.diffuseBRDF = diffuseBRDF;
            brdf.specularBRDF = specularBRDF;
            return brdf;
        }

        float3 evaluateDirectionalLight(float3 wo, float3 wi)
        {
            if (wo.z <= 0 || wi.z <= 0)
            {
                return 0;
            }
            
            return diffuseBRDF.evaluate(wo, wi).value * specularBRDF.transmissionFraction + specularBRDF.evaluate(wo, wi).value;
        }
    };



#endif // KAISER_RAYTRACING_BRDF