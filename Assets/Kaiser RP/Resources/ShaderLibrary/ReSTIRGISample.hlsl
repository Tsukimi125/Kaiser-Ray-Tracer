#include "Utils.cginc"

struct ReSTIRGISample
{
    float3 vPosW;   // visible point
    float3 vNormW;  // visible surface normal
    float3 vColor;  // outgoing radiance at visible point in RGB
    float3 sPosW;   // sample point
    float3 sNormW;  // sample surface normal
    float3 sColor;  // outgoing radiance at sample point in RGB
    float random;   // random numbers used for path

};

struct GIReservoir
{
    float3 vPosW;   // visible point
    float3 vNormW;  // visible surface normal
    float3 sPosW;   // sample point
    float3 sNormW;  // sample surface normal
    float3 radiance;  // outgoing radiance at sample point in RGB
    
    int M;
    float weightSum;
    int age;
};

bool UpdateReservoir(inout GIReservoir reservoir, in ReSTIRGISample sample, in float weight)
{
    reservoir.M++;
    reservoir.age++;
    reservoir.weightSum += weight;
    reservoir.radiance += sample.sColor * weight;

    bool isUpdate = frac(11.4 + sample.random * 5.14) * reservoir.weightSum < weight;

    if (isUpdate)
    {
        // reservoir.vPosW = sample.vPosW;
        // reservoir.vNormW = sample.vNormW;
        reservoir.sPosW = sample.sPosW;
        reservoir.sNormW = sample.sNormW;
        reservoir.radiance = sample.sColor;
        // reservoir.M += 1;
        // reservoir.weightSum += weight;
        // reservoir.age = 0;

    }

    return isUpdate;
}