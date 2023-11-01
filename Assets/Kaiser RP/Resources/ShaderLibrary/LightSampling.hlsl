// bool sampleDirectionalLight(const float3 shadingPosW, const LightData light, out AnalyticLightSample ls)
// {
//     // A directional light doesn't have a position. Just clear to zero.
//     ls.posW = float3(0, 0, 0);

//     // For a directional light, the normal is always along its light direction.
//     ls.normalW = light.dirW;

//     // Setup direction and distance to light.
//     ls.distance = kMaxLightDistance;
//     ls.dir = -light.dirW;

//     // Setup incident radiance. For directional lights there is no falloff or cosine term.
//     ls.Li = light.intensity;

//     // For a directional light, the PDF with respect to solid angle is a Dirac function. Set to zero.
//     ls.pdf = 0.f;

//     return true;
// }

bool sampleDirectionalLight(const float3 shadingPosW, const LightData light, out AnalyticLightSample ls){
    
}


/** Samples a point (spot) light.
    \param[in] shadingPosW Shading point in world space.
    \param[in] light Light data.
    \param[out] ls Light sample struct.
    \return True if a sample was generated, false otherwise.
*/
bool samplePointLight(const float3 shadingPosW, const LightData light, out AnalyticLightSample ls)
{
    // Get the position and normal.
    ls.posW = light.posW;
    ls.normalW = light.dirW;

    // Compute direction and distance to light.
    // The distance is clamped to a small epsilon to avoid div-by-zero below.
    float3 toLight = ls.posW - shadingPosW;
    float distSqr = max(dot(toLight, toLight), kMinLightDistSqr);
    ls.distance = sqrt(distSqr);
    ls.dir = toLight / ls.distance;

    // Calculate the falloff for spot-lights.
    float cosTheta = -dot(ls.dir, light.dirW);
    float falloff = 1.f;
    if (cosTheta < light.cosOpeningAngle)
    {
        falloff = 0.f;
    }
    else if (light.penumbraAngle > 0.f)
    {
        float deltaAngle = light.openingAngle - acos(cosTheta);
        falloff = smoothstep(0.f, light.penumbraAngle, deltaAngle);
    }

    // Compute incident radiance at shading point.
    ls.Li = light.intensity * falloff / distSqr;

    // For a point light, the PDF with respect to solid angle is a Dirac function. Set to zero.
    ls.pdf = 0.f;

    return true;
}